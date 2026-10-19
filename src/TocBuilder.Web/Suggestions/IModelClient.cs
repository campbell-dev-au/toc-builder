using TocBuilder.Domain;

namespace TocBuilder.Web.Suggestions;

/// <summary>What the model is asked: the whole tree (with its Scope) and the Entity or AND-junction the candidates attach to.</summary>
public sealed record ModelRequest(Tree Tree, SuggestionKind Kind, Entity? Entity, AndJunction? Junction);

/// <summary>The model is not set up here, for instance because there is no API key.</summary>
public sealed class ModelUnavailableException(string message) : Exception(message);

public interface IModelClient
{
    /// <summary>Returns raw candidates. They are not yet checked against the tree's rules.</summary>
    Task<IReadOnlyList<Suggestion>> SuggestAsync(ModelRequest request, CancellationToken cancellationToken);
}
