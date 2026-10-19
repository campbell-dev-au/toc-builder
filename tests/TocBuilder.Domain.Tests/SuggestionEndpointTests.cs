using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TocBuilder.Web.Suggestions;

namespace TocBuilder.Domain.Tests;

public class SuggestionEndpointTests
{
    private sealed class FakeModelClient(params Suggestion[] reply) : IModelClient
    {
        public ModelRequest? Received { get; private set; }

        public Task<IReadOnlyList<Suggestion>> SuggestAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Received = request;
            return Task.FromResult<IReadOnlyList<Suggestion>>(reply);
        }
    }

    private static HttpClient ClientWith(IModelClient model, out WebApplicationFactory<Program> factory)
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton(model)));
        return factory.CreateClient();
    }

    private static string Json(Tree tree) => TreeSerializer.Export("tree", tree);

    [Fact]
    public async Task The_model_is_given_the_whole_tree_the_selected_entity_and_the_scope_and_its_candidates_come_back()
    {
        var tree = new Tree();
        tree.SetScope("A small print shop");
        var late = tree.AddEntity("Late deliveries");
        tree.AddCause(late.Id, "Machines are overloaded");
        var model = new FakeModelClient(new Suggestion(SuggestionKind.Cause, "No buffer before the press"));
        using var client = ClientWith(model, out var factory);
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/suggestions", new SuggestionApiRequest(Json(tree), SuggestionKind.Cause, late.Id, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([new Suggestion(SuggestionKind.Cause, "No buffer before the press")], await response.Content.ReadFromJsonAsync<Suggestion[]>());
        Assert.Equal("A small print shop", model.Received!.Tree.Scope);
        Assert.Equal(["Late deliveries", "Machines are overloaded"], model.Received.Tree.Entities.Select(e => e.Statement));
        Assert.Equal(late.Id, model.Received.Entity!.Id);
        Assert.Equal(SuggestionKind.Cause, model.Received.Kind);
    }

    [Fact]
    public async Task Candidates_that_repeat_an_entity_or_would_close_a_cycle_are_dropped_before_the_response()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var overloaded = tree.AddCause(late.Id, "Machines are overloaded");
        var model = new FakeModelClient(
            new Suggestion(SuggestionKind.Cause, "  late DELIVERIES "),              // repeats the tree's own Entity
            new Suggestion(SuggestionKind.Cause, "Machines are overloaded"),         // repeats another Entity
            new Suggestion(SuggestionKind.Cause, "Late deliveries", late.Id),        // an Entity cannot cause itself
            new Suggestion(SuggestionKind.Effect, "Machines are overloaded", overloaded.Id), // would close a cycle
            new Suggestion(SuggestionKind.Cause, "No buffer before the press"));
        using var client = ClientWith(model, out var factory);
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/suggestions", new SuggestionApiRequest(Json(tree), SuggestionKind.Cause, late.Id, null));

        Assert.Equal([new Suggestion(SuggestionKind.Cause, "No buffer before the press")], await response.Content.ReadFromJsonAsync<Suggestion[]>());
    }

    [Fact]
    public async Task Missing_co_causes_are_requested_for_an_AND_junction()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var a = tree.AddCause(late.Id, "Machines are overloaded");
        var b = tree.AddCause(late.Id, "No buffer");
        var junction = tree.Group(late.Id, [a.Id, b.Id]).Junction!;
        var model = new FakeModelClient(new Suggestion(SuggestionKind.CoCause, "Orders are unplanned"), new Suggestion(SuggestionKind.Cause, "Not a co-cause"));
        using var client = ClientWith(model, out var factory);
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/suggestions", new SuggestionApiRequest(Json(tree), SuggestionKind.CoCause, null, junction.Id));

        Assert.Equal([new Suggestion(SuggestionKind.CoCause, "Orders are unplanned")], await response.Content.ReadFromJsonAsync<Suggestion[]>());
        Assert.Equal(junction.Id, model.Received!.Junction!.Id);
    }

    [Fact]
    public async Task A_request_for_an_entity_that_is_not_in_the_tree_is_rejected_without_calling_the_model()
    {
        var tree = new Tree();
        tree.AddEntity("Late deliveries");
        var model = new FakeModelClient();
        using var client = ClientWith(model, out var factory);
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/suggestions", new SuggestionApiRequest(Json(tree), SuggestionKind.Cause, new EntityId(99), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(model.Received);
    }

    [Fact]
    public async Task A_tree_that_is_not_a_valid_file_is_rejected()
    {
        var model = new FakeModelClient();
        using var client = ClientWith(model, out var factory);
        using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/suggestions", new SuggestionApiRequest("not json", SuggestionKind.Cause, new EntityId(1), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(model.Received);
    }

    [Fact]
    public async Task Without_an_API_key_the_server_says_suggestions_are_unavailable_and_does_not_reveal_configuration()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.UseSetting("Anthropic:ApiKey", ""));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/suggestions", new SuggestionApiRequest(Json(tree), SuggestionKind.Cause, late.Id, null));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task The_API_key_never_appears_in_a_response()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("Anthropic:ApiKey", "sk-secret-key");
            host.ConfigureTestServices(services => services.AddSingleton<IModelClient>(new FakeModelClient(new Suggestion(SuggestionKind.Cause, "No buffer"))));
        });
        using var client = factory.CreateClient();

        var suggestions = await client.PostAsJsonAsync("/api/suggestions", new SuggestionApiRequest(Json(tree), SuggestionKind.Cause, late.Id, null));
        var page = await client.GetAsync("/");

        Assert.DoesNotContain("sk-secret-key", await suggestions.Content.ReadAsStringAsync());
        Assert.DoesNotContain("sk-secret-key", await page.Content.ReadAsStringAsync());
        Assert.DoesNotContain("sk-secret-key", string.Join("\n", suggestions.Headers.Concat(page.Headers).SelectMany(h => h.Value)));
    }
}
