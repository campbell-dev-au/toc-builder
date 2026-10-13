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
        var entity = new Entity(new EntityId(_nextId++), statement);
        _entities.Add(entity);
        return entity;
    }

    public Entity AddCause(EntityId effect, string statement)
    {
        Require(effect);
        var cause = AddEntity(statement);
        _links.Add(new CausalLink(cause.Id, effect));
        return cause;
    }

    public LinkOutcome Link(EntityId cause, EntityId effect)
    {
        Require(cause);
        Require(effect);
        if (cause == effect || IsCauseOf(effect, cause)) return LinkOutcome.WouldCreateCycle;
        var link = new CausalLink(cause, effect);
        if (_links.Contains(link)) return LinkOutcome.AlreadyLinked;
        _links.Add(link);
        return LinkOutcome.Linked;
    }

    public void EditStatement(EntityId id, string statement)
    {
        var index = IndexOf(id);
        if (string.IsNullOrWhiteSpace(statement))
            throw new ArgumentException("A statement cannot be blank.", nameof(statement));
        _entities[index] = _entities[index] with { Statement = statement };
    }

    public void Delete(EntityId id)
    {
        _entities.RemoveAt(IndexOf(id));
        _links.RemoveAll(l => l.Cause == id || l.Effect == id);
    }

    private void Require(EntityId id) => IndexOf(id);

    private int IndexOf(EntityId id)
    {
        var index = _entities.FindIndex(e => e.Id == id);
        if (index < 0) throw new ArgumentException($"No entity with id {id.Value} in this tree.", nameof(id));
        return index;
    }

    /// <summary>True when <paramref name="cause"/> leads, through any chain of links, to <paramref name="effect"/>.</summary>
    private bool IsCauseOf(EntityId cause, EntityId effect)
    {
        var seen = new HashSet<EntityId>();
        var pending = new Stack<EntityId>([cause]);
        while (pending.TryPop(out var current))
        {
            if (current == effect) return true;
            if (!seen.Add(current)) continue;
            foreach (var link in _links.Where(l => l.Cause == current)) pending.Push(link.Effect);
        }
        return false;
    }

    public Entity AddEffect(EntityId cause, string statement)
    {
        Require(cause);
        var effect = AddEntity(statement);
        _links.Add(new CausalLink(cause, effect.Id));
        return effect;
    }
}
