namespace TocBuilder.Domain;

public readonly record struct EntityId(int Value);

/// <summary>A working annotation the user puts on an Entity. It never restricts what can be linked or edited.</summary>
public enum EntityTag { None, Ude, CoreProblem }

public sealed record Entity(EntityId Id, string Statement, EntityTag Tag = EntityTag.None);

/// <summary>A derived report of what looks unfinished in a tree. It only reports and never blocks.</summary>
/// <param name="UdesWithoutCoreProblem">UDEs that no Core problem reaches through Causal links.</param>
/// <param name="CoreProblemsExplainingNoUde">Core problems that reach no UDE.</param>
/// <param name="UnconnectedEntities">Entities with no Causal link at all.</param>
/// <param name="RootCauses">Entities with no cause beneath them but at least one effect above.</param>
public sealed record TreeSummary(
    IReadOnlyList<Entity> UdesWithoutCoreProblem,
    IReadOnlyList<Entity> CoreProblemsExplainingNoUde,
    IReadOnlyList<Entity> UnconnectedEntities,
    IReadOnlyList<Entity> RootCauses);

public sealed record CausalLink(EntityId Cause, EntityId Effect);

public enum LinkOutcome { Linked, AlreadyLinked, WouldCreateCycle }

public sealed class Tree
{
    private readonly List<Entity> _entities = [];
    private readonly List<CausalLink> _links = [];
    private int _nextId = 1;
    private readonly Stack<Snapshot> _undo = new();
    private readonly Stack<Snapshot> _redo = new();

    /// <summary>The whole editable state of a tree. Every tree-changing command is undone by restoring one of these, so commands added later are undoable as long as their state lives here.</summary>
    private sealed record Snapshot(Entity[] Entities, CausalLink[] Links, int NextId);

    public IReadOnlyList<Entity> Entities => _entities;
    public IReadOnlyList<CausalLink> Links => _links;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public bool Undo()
    {
        if (!_undo.TryPop(out var previous)) return false;
        _redo.Push(Capture());
        Restore(previous);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var next)) return false;
        _undo.Push(Capture());
        Restore(next);
        return true;
    }

    public Entity AddEntity(string statement)
    {
        var clean = Clean(statement);
        RecordUndoStep();
        return Add(clean);
    }

    public Entity AddCause(EntityId effect, string statement)
    {
        EnsureExists(effect);
        var clean = Clean(statement);
        RecordUndoStep();
        var cause = Add(clean);
        _links.Add(new CausalLink(cause.Id, effect));
        return cause;
    }

    public Entity AddEffect(EntityId cause, string statement)
    {
        EnsureExists(cause);
        var clean = Clean(statement);
        RecordUndoStep();
        var effect = Add(clean);
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
        RecordUndoStep();
        _links.Add(link);
        return LinkOutcome.Linked;
    }

    public void EditStatement(EntityId id, string statement)
    {
        var index = IndexOf(id);
        var clean = Clean(statement);
        RecordUndoStep();
        _entities[index] = _entities[index] with { Statement = clean };
    }

    public void SetTag(EntityId id, EntityTag tag)
    {
        var index = IndexOf(id);
        if (_entities[index].Tag == tag) return;
        RecordUndoStep();
        _entities[index] = _entities[index] with { Tag = tag };
    }

    public TreeSummary Summarise()
    {
        var udes = _entities.Where(e => e.Tag == EntityTag.Ude).ToList();
        var cores = _entities.Where(e => e.Tag == EntityTag.CoreProblem).ToList();
        var reached = cores.ToDictionary(c => c.Id, c => ReachableFrom(c.Id));
        return new TreeSummary(
            udes.Where(u => !cores.Any(c => reached[c.Id].Contains(u.Id))).ToList(),
            cores.Where(c => !udes.Any(u => reached[c.Id].Contains(u.Id))).ToList(),
            _entities.Where(e => !_links.Any(l => l.Cause == e.Id || l.Effect == e.Id)).ToList(),
            _entities.Where(e => _links.Any(l => l.Cause == e.Id) && !_links.Any(l => l.Effect == e.Id)).ToList());
    }

    public void Delete(EntityId id)
    {
        var index = IndexOf(id);
        RecordUndoStep();
        _entities.RemoveAt(index);
        _links.RemoveAll(l => l.Cause == id || l.Effect == id);
    }

    private Entity Add(string cleanStatement)
    {
        var entity = new Entity(new EntityId(_nextId), cleanStatement);
        _nextId++;
        _entities.Add(entity);
        return entity;
    }

    // Called by each command after it has validated its input and before it changes anything, so refused commands leave no step and keep the redo history.
    private void RecordUndoStep()
    {
        _undo.Push(Capture());
        _redo.Clear();
    }

    private Snapshot Capture() => new([.. _entities], [.. _links], _nextId);

    private void Restore(Snapshot snapshot)
    {
        _entities.Clear();
        _entities.AddRange(snapshot.Entities);
        _links.Clear();
        _links.AddRange(snapshot.Links);
        _nextId = snapshot.NextId;
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

    /// <summary>Every Entity that <paramref name="from"/> reaches through any chain of Causal links, following cause to effect.</summary>
    private HashSet<EntityId> ReachableFrom(EntityId from)
    {
        var seen = new HashSet<EntityId>();
        var pending = new Stack<EntityId>([from]);
        while (pending.TryPop(out var current))
            foreach (var link in _links.Where(l => l.Cause == current))
                if (seen.Add(link.Effect)) pending.Push(link.Effect);
        return seen;
    }

    /// <summary>True when <paramref name="from"/> reaches <paramref name="to"/> through any chain of Causal links, following cause to effect.</summary>
    private bool LeadsTo(EntityId from, EntityId to) => from == to || ReachableFrom(from).Contains(to);
}
