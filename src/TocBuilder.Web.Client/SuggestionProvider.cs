using TocBuilder.Domain;

namespace TocBuilder.Web.Client;

/// <summary>What the AI is asked: the tree's Scope, the Entity to attach to, and the Entities already in the tree so it can point at one instead of adding a duplicate.</summary>
public sealed record SuggestionRequest(string Scope, SuggestionKind Kind, string TargetStatement, IReadOnlyList<Entity> Entities);

public interface ISuggestionProvider
{
    Task<IReadOnlyList<Suggestion>> SuggestAsync(SuggestionRequest request);
}

/// <summary>Canned candidates that drive the UI until the real model is wired in. It also returns a duplicate and an existing Entity so the rules visibly apply.</summary>
public sealed class StubSuggestionProvider : ISuggestionProvider
{
    public async Task<IReadOnlyList<Suggestion>> SuggestAsync(SuggestionRequest request)
    {
        await Task.Delay(300);
        var target = request.TargetStatement.Length > 14 ? request.TargetStatement[..14] + "…" : request.TargetStatement;
        var noun = request.Kind switch { SuggestionKind.Cause => "cause", SuggestionKind.Effect => "effect", _ => "co-cause" };
        List<Suggestion> candidates =
        [
            new(request.Kind, $"Stub {noun} A of {target}"),
            new(request.Kind, $"Stub {noun} B of {target}"),
            new(request.Kind, request.TargetStatement),   // repeats the target, so the tree refuses it
        ];
        if (request.Entities.FirstOrDefault(e => e.Statement != request.TargetStatement) is { } other)
            candidates.Add(new Suggestion(request.Kind, other.Statement, other.Id));
        return candidates;
    }
}
