namespace TocBuilder.Domain;

public readonly record struct EntityId(int Value);

public sealed record Entity(EntityId Id, string Statement);

public sealed record CausalLink(EntityId Cause, EntityId Effect);

public enum LinkOutcome { Linked, AlreadyLinked, WouldCreateCycle }

public sealed class Tree
{
    private readonly List<Entity> _entities = [];
    private readonly List<CausalLink> _links = [];
    private int _nextId = 1;

    public IReadOnlyList<Entity> Entities => _entities;
    public IReadOnlyList<CausalLink> Links => _links;

    public Entity AddEntity(string statement)
    {
        var entity = new Entity(new EntityId(_nextId), Clean(statement));
        _nextId++;   // only once the statement is accepted, so a refused add does not consume an id
        _entities.Add(entity);
        return entity;
    }

    public Entity AddCause(EntityId effect, string statement)
    {
        EnsureExists(effect);
        var cause = AddEntity(statement);
        _links.Add(new CausalLink(cause.Id, effect));
        return cause;
    }

    public Entity AddEffect(EntityId cause, string statement)
    {
        EnsureExists(cause);
        var effect = AddEntity(statement);
        _links.Add(new CausalLink(cause, effect.Id));
        return effect;
    }

    public LinkOutcome Link(EntityId cause, EntityId effect)
    {
        EnsureExists(cause);
        EnsureExists(effect);
        if (cause == effect || LeadsTo(effect, cause)) return LinkOutcome.WouldCreateCycle;
        var link = new CausalLink(cause, effect);
        if (_links.Contains(link)) return LinkOutcome.AlreadyLinked;
        _links.Add(link);
        return LinkOutcome.Linked;
    }

    public void EditStatement(EntityId id, string statement)
    {
        var index = IndexOf(id);
        _entities[index] = _entities[index] with { Statement = Clean(statement) };
    }

    public void Delete(EntityId id)
    {
        _entities.RemoveAt(IndexOf(id));
        _links.RemoveAll(l => l.Cause == id || l.Effect == id);
    }

    private static string Clean(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
            throw new ArgumentException("A statement cannot be blank.", nameof(statement));
        return statement.Trim();
    }

    private void EnsureExists(EntityId id) => IndexOf(id);

    private int IndexOf(EntityId id)
    {
        var index = _entities.FindIndex(e => e.Id == id);
        if (index < 0) throw new ArgumentException($"No entity with id {id.Value} in this tree.", nameof(id));
        return index;
    }

    /// <summary>True when <paramref name="from"/> reaches <paramref name="to"/> through any chain of Causal links, following cause to effect.</summary>
    private bool LeadsTo(EntityId from, EntityId to)
    {
        var seen = new HashSet<EntityId>();
        var pending = new Stack<EntityId>([from]);
        while (pending.TryPop(out var current))
        {
            if (current == to) return true;
            if (!seen.Add(current)) continue;
            foreach (var link in _links.Where(l => l.Cause == current)) pending.Push(link.Effect);
        }
        return false;
    }
}
