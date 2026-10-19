using System.Text.Json.Serialization;

namespace TocBuilder.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuggestionKind { Cause, Effect, CoCause }

/// <summary>A candidate for the tree. It is not part of any tree until the user accepts it. <paramref name="Existing"/> names an Entity already in the tree to link instead of adding a new one.</summary>
public sealed record Suggestion(SuggestionKind Kind, string Statement, EntityId? Existing = null);

public enum AcceptOutcome { Accepted, NoLongerValid, DuplicateStatement }

/// <summary>The Suggestions waiting on one request, kept apart from the tree so that unaccepted ones never reach its state, Tree summary, JSON or exports.</summary>
public sealed class SuggestionSet
{
    private EntityId? _entity;       // set for an Entity's causes and effects
    private JunctionId? _junction;   // set for an AND-junction's co-causes

    public IReadOnlyList<Suggestion> Pending { get; private set; } = [];

    /// <summary>Keeps the candidates that are valid as a cause or effect of <paramref name="entity"/> and drops the rest.</summary>
    public static SuggestionSet ForEntity(Tree tree, EntityId entity, IEnumerable<Suggestion> candidates)
    {
        var set = new SuggestionSet { _entity = entity };
        set.Pending = set.Valid(tree, candidates.Where(c => c.Kind != SuggestionKind.CoCause));
        return set;
    }

    /// <summary>Keeps the candidates that are valid as a further cause in <paramref name="junction"/> and drops the rest.</summary>
    public static SuggestionSet ForJunction(Tree tree, JunctionId junction, IEnumerable<Suggestion> candidates)
    {
        var set = new SuggestionSet { _junction = junction };
        set.Pending = set.Valid(tree, candidates.Where(c => c.Kind == SuggestionKind.CoCause));
        return set;
    }

    /// <summary>Adds the suggestion to the tree as one undoable step. <paramref name="editedStatement"/> replaces its wording.</summary>
    public AcceptOutcome Accept(Tree tree, Suggestion suggestion, string? editedStatement = null)
    {
        if (!Pending.Contains(suggestion)) return AcceptOutcome.NoLongerValid;
        if (suggestion.Existing is not null && editedStatement is not null)
            throw new ArgumentException("A suggestion that names an existing entity cannot be reworded.", nameof(editedStatement));
        if (Check(tree, suggestion) is null)
        {
            Dismiss(suggestion);
            return AcceptOutcome.NoLongerValid;
        }
        var statement = editedStatement ?? suggestion.Statement;
        if (editedStatement is not null && tree.Entities.Any(e => Normalise(e.Statement) == Normalise(statement)))
            return AcceptOutcome.DuplicateStatement;
        switch (suggestion.Kind, suggestion.Existing)
        {
            case (SuggestionKind.Cause, { } existing): tree.Link(existing, _entity!.Value); break;
            case (SuggestionKind.Effect, { } existing): tree.Link(_entity!.Value, existing); break;
            case (SuggestionKind.CoCause, { } existing): tree.AddToJunction(_junction!.Value, existing); break;
            case (SuggestionKind.Cause, null): tree.AddCause(_entity!.Value, statement); break;
            case (SuggestionKind.Effect, null): tree.AddEffect(_entity!.Value, statement); break;
            default: tree.AddCoCause(_junction!.Value, statement); break;
        }
        Dismiss(suggestion);
        return AcceptOutcome.Accepted;
    }

    public void Dismiss(Suggestion suggestion) => Pending = [.. Pending.Where(p => p != suggestion)];

    private List<Suggestion> Valid(Tree tree, IEnumerable<Suggestion> candidates)
    {
        var worded = new HashSet<string>();   // new statements already kept, so a repeat within the batch is dropped
        var valid = new List<Suggestion>();
        foreach (var candidate in candidates)
        {
            if (Check(tree, candidate) is not { } keep) continue;
            if (keep.Existing is null ? worded.Add(Normalise(keep.Statement)) : !valid.Contains(keep)) valid.Add(keep);
        }
        return valid;
    }

    /// <summary>The suggestion as it would enter the tree now, or null when its target has gone , it repeats an Entity already in the tree, or the Entity it names is missing or could not be linked. A suggestion that names an Entity is worded as that Entity.</summary>
    private Suggestion? Check(Tree tree, Suggestion suggestion)
    {
        var targetGone = _junction is { } junction ? tree.Junctions.All(j => j.Id != junction) : tree.Entities.All(e => e.Id != _entity);
        if (targetGone) return null;
        if (suggestion.Existing is not { } existingId)
            return string.IsNullOrWhiteSpace(suggestion.Statement) || tree.Entities.Any(e => Normalise(e.Statement) == Normalise(suggestion.Statement)) ? null : suggestion;
        if (tree.Entities.FirstOrDefault(e => e.Id == existingId) is not { } existing) return null;
        var feasible = suggestion.Kind switch
        {
            SuggestionKind.Cause => tree.CheckLink(existingId, _entity!.Value) == LinkOutcome.Linked,
            SuggestionKind.Effect => tree.CheckLink(_entity!.Value, existingId) == LinkOutcome.Linked,
            _ => tree.CheckAddToJunction(_junction!.Value, existingId) == JunctionOutcome.Done,
        };
        return feasible ? suggestion with { Statement = existing.Statement } : null;
    }

    private static string Normalise(string statement) => statement.Trim().ToLowerInvariant();
}
