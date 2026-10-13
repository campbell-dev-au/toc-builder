namespace TocBuilder.Domain;

public readonly record struct EntityId(int Value);

public sealed record Entity(EntityId Id, string Statement);

public sealed record CausalLink(EntityId Cause, EntityId Effect);

public sealed class Tree
{
    private readonly List<Entity> _entities = [];
    private readonly List<CausalLink> _links = [];
    private int _nextId = 1;

    public IReadOnlyList<Entity> Entities => _entities;
    public IReadOnlyList<CausalLink> Links => _links;

    public Entity AddEntity(string statement)
    {
        var entity = new Entity(new EntityId(_nextId++), statement);
        _entities.Add(entity);
        return entity;
    }

    public Entity AddCause(EntityId effect, string statement)
    {
        var cause = AddEntity(statement);
        _links.Add(new CausalLink(cause.Id, effect));
        return cause;
    }
}
