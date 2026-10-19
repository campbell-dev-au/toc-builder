using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TocBuilder.Domain;

namespace TocBuilder.Web.Suggestions;

/// <summary>Asks Claude for candidates. The API key is read from configuration (<c>Anthropic:ApiKey</c>) and never leaves the server.</summary>
public sealed class AnthropicModelClient(HttpClient http, IConfiguration configuration) : IModelClient
{
    private const string System =
        "You help build a Theory of Constraints Current Reality Tree. Each statement is a single, specific, observable condition. " +
        "An arrow runs from a cause to its effect, and a cause must be sufficient for the effect (or jointly sufficient with the other causes in an AND-junction). " +
        "Reply with only a JSON array of 3 to 5 objects shaped {\"statement\": string, \"summary\": string, \"existing\": number or null}. " +
        "Make \"summary\" a short phrase of at most 40 characters that names the statement, such as \"ultra-processed food intake\". " +
        "Set \"existing\" to the id of an Entity already in the tree when that Entity is the candidate, instead of repeating it as a new statement; otherwise null.";

    public async Task<IReadOnlyList<Suggestion>> SuggestAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var key = configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new ModelUnavailableException("Suggestions are not set up on this server: there is no Anthropic API key.");

        var body = new JsonObject
        {
            ["model"] = configuration["Anthropic:Model"] ?? "claude-sonnet-5-5",
            ["max_tokens"] = 1024,
            ["system"] = System,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = Prompt(request) }),
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("x-api-key", key);
        message.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await http.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var reply = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var text = reply?["content"]?.AsArray().Select(block => block?["text"]?.GetValue<string>()).FirstOrDefault(t => t is not null)
            ?? throw new FormatException("The model's reply had no text.");
        return Parse(text, request.Kind);
    }

    private static string Prompt(ModelRequest request)
    {
        var tree = request.Tree;
        var text = new StringBuilder();
        text.AppendLine($"Scope: {(string.IsNullOrWhiteSpace(tree.Scope) ? "(not given)" : tree.Scope)}");
        text.AppendLine("Entities in the tree (id: statement):");
        foreach (var entity in tree.Entities) text.AppendLine($"{entity.Id.Value}: {entity.Statement}");
        text.AppendLine("Causal links (cause -> effect):");
        foreach (var link in tree.Links) text.AppendLine($"{link.Cause.Value} -> {link.Effect.Value}");
        foreach (var junction in tree.Junctions) text.AppendLine($"AND-junction: {string.Join(" AND ", junction.Causes.Select(c => c.Value))} -> {junction.Effect.Value}");
        text.AppendLine();
        if (request.Junction is { } j)
            text.AppendLine($"Propose further causes missing from the AND-junction of {string.Join(" AND ", j.Causes.Select(c => c.Value))} that together are sufficient for entity {j.Effect.Value}.");
        else if (request.Kind == SuggestionKind.Cause)
            text.AppendLine($"Propose likely causes of entity {request.Entity!.Id.Value}: \"{request.Entity.Statement}\".");
        else
            text.AppendLine($"Propose likely effects of entity {request.Entity!.Id.Value}: \"{request.Entity.Statement}\".");
        return text.ToString();
    }

    private static List<Suggestion> Parse(string text, SuggestionKind kind)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end < start) throw new FormatException("The model's reply was not a JSON array.");
        var items = JsonNode.Parse(text[start..(end + 1)])!.AsArray();
        var suggestions = new List<Suggestion>();
        foreach (var item in items)
        {
            if (item is not JsonObject o || o["statement"] is not JsonValue statement || !statement.TryGetValue<string>(out var s)) continue;
            int? existing = o["existing"] is JsonValue id && id.TryGetValue<int>(out var n) ? n : null;
            var summary = o["summary"] is JsonValue sv && sv.TryGetValue<string>(out var proposed) ? proposed : null;
            suggestions.Add(new Suggestion(kind, s, existing is { } e ? new EntityId(e) : null, summary));
        }
        return suggestions;
    }
}
