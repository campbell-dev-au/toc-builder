using TocBuilder.Domain;

namespace TocBuilder.Web.Suggestions;

public static class SuggestionEndpoint
{
    /// <summary>Maps the Suggestion API. The server keeps no tree: it reads the one in the request, asks the model and drops candidates that break the tree's rules.</summary>
    public static void MapSuggestions(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/suggestions", async (SuggestionApiRequest request, IModelClient model, ILogger<IModelClient> log, CancellationToken cancellationToken) =>
        {
            var imported = TreeSerializer.Import(request.TreeJson ?? "");
            if (imported.Tree is not { } tree) return Results.BadRequest(imported.Message);

            ModelRequest asked;
            if (request.Kind == SuggestionKind.CoCause)
            {
                if (request.Junction is not { } junctionId || tree.Junctions.FirstOrDefault(j => j.Id == junctionId) is not { } junction)
                    return Results.BadRequest("Co-causes need an AND-junction that is in the tree.");
                asked = new ModelRequest(tree, request.Kind, null, junction);
            }
            else
            {
                if (request.Entity is not { } entityId || tree.Entities.FirstOrDefault(e => e.Id == entityId) is not { } entity)
                    return Results.BadRequest("Causes and effects need an Entity that is in the tree.");
                asked = new ModelRequest(tree, request.Kind, entity, null);
            }

            IReadOnlyList<Suggestion> candidates;
            try { candidates = await model.SuggestAsync(asked, cancellationToken); }
            catch (ModelUnavailableException e) { return Results.Problem(e.Message, statusCode: StatusCodes.Status503ServiceUnavailable); }
            catch (Exception e) when (e is HttpRequestException or FormatException or System.Text.Json.JsonException)
            {
                log.LogWarning(e, "The model call failed.");
                return Results.Problem("The model could not be reached or gave an unusable answer.", statusCode: StatusCodes.Status502BadGateway);
            }

            var set = asked.Junction is { } j
                ? SuggestionSet.ForJunction(tree, j.Id, candidates)
                : SuggestionSet.ForEntity(tree, asked.Entity!.Id, candidates);
            return Results.Ok(set.Pending);
        });
}
