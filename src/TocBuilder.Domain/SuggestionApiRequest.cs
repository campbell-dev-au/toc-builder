namespace TocBuilder.Domain;

/// <summary>What the browser sends when the user asks for Suggestions: the whole tree (as the versioned JSON file, so it carries the Scope) and what to attach to. <paramref name="Entity"/> is set for causes and effects, <paramref name="Junction"/> for co-causes.</summary>
public sealed record SuggestionApiRequest(string TreeJson, SuggestionKind Kind, EntityId? Entity, JunctionId? Junction);
