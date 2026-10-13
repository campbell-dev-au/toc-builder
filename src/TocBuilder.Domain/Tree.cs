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

public readonly record struct JunctionId(int Value);

/// <summary>Causes of one effect that are only jointly sufficient for it. Each cause also has its own Causal link to the effect.</summary>
public sealed record AndJunction(JunctionId Id, EntityId Effect, IReadOnlyList<EntityId> Causes);

public enum JunctionOutcome { Done, TooFewCauses, NotACauseOfEffect, AlreadyInJunction, WouldCreateCycle }

public sealed record JunctionResult(JunctionOutcome Outcome, AndJunction? Junction = null);

public sealed class Tree
{
    private readonly List<Entity> _entities = [];
    private readonly List<CausalLink> _links = [];
    private readonly List<AndJunction> _junctions = [];
    private int _nextId = 1;
    private int _nextJunctionId = 1;
    private readonly Stack<Snapshot> _undo = new();
    private readonly Stack<Snapshot> _redo = new();

    /// <summary>The whole editable state of a tree. Every tree-changing command is undone by restoring one of these, so commands added later are undoable as long as their state lives here.</summary>
    private sealed record Snapshot(Entity[] Entities, CausalLink[] Links, AndJunction[] Junctions, int NextId, int NextJunctionId);

    public IReadOnlyList<Entity> Entities => _entities;
    public IReadOnlyList<CausalLink> Links => _links;
    public IReadOnlyList<AndJunction> Junctions => _junctions;

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
        _junctions.RemoveAll(j => j.Effect == id);
        foreach (var junction in _junctions.Where(j => j.Causes.Contains(id)).ToArray())
            Shrink(junction, id);
    }

    /// <summary>Groups existing causes of <paramref name="effect"/> into one AND-junction. Their Causal links are kept.</summary>
    public JunctionResult Group(EntityId effect, IReadOnlyCollection<EntityId> causes)
    {
        EnsureExists(effect);
        foreach (var cause in causes) EnsureExists(cause);
        var distinct = causes.Distinct().ToArray();
        if (distinct.Length < 2) return new JunctionResult(JunctionOutcome.TooFewCauses);
        if (distinct.Any(c => !_links.Contains(new CausalLink(c, effect))))
            return new JunctionResult(JunctionOutcome.NotACauseOfEffect);
        if (distinct.Any(c => InJunctionOf(effect, c))) return new JunctionResult(JunctionOutcome.AlreadyInJunction);
        RecordUndoStep();
        var junction = new AndJunction(new JunctionId(_nextJunctionId++), effect, distinct);
        _junctions.Add(junction);
        return new JunctionResult(JunctionOutcome.Done, junction);
    }

    /// <summary>Adds a further cause to a junction, linking it to the junction's effect first when it is not already a cause of it.</summary>
    public JunctionOutcome AddToJunction(JunctionId junctionId, EntityId cause)
    {
        var junction = _junctions[JunctionIndex(junctionId)];
        EnsureExists(cause);
        if (InJunctionOf(junction.Effect, cause)) return JunctionOutcome.AlreadyInJunction;
        var link = new CausalLink(cause, junction.Effect);
        var needsLink = !_links.Contains(link);
        if (needsLink && (cause == junction.Effect || LeadsTo(junction.Effect, cause))) return JunctionOutcome.WouldCreateCycle;
        RecordUndoStep();
        if (needsLink) _links.Add(link);
        Replace(junction, [.. junction.Causes, cause]);
        return JunctionOutcome.Done;
    }

    /// <summary>Removes the junction; the Causal links of its causes stay, so they become independent causes again.</summary>
    public void Ungroup(JunctionId junctionId)
    {
        var index = JunctionIndex(junctionId);
        RecordUndoStep();
        _junctions.RemoveAt(index);
    }

    /// <summary>Takes one cause out of a junction, keeping its Causal link. A junction left with a single cause is dissolved.</summary>
    public void RemoveFromJunction(JunctionId junctionId, EntityId cause)
    {
        var junction = _junctions[JunctionIndex(junctionId)];
        if (!junction.Causes.Contains(cause)) throw new ArgumentException($"Entity {cause.Value} is not in junction {junctionId.Value}.", nameof(cause));
        RecordUndoStep();
        Shrink(junction, cause);
    }

    private bool InJunctionOf(EntityId effect, EntityId cause) =>
        _junctions.Any(j => j.Effect == effect && j.Causes.Contains(cause));

    private void Shrink(AndJunction junction, EntityId cause)
    {
        var remaining = junction.Causes.Where(c => c != cause).ToArray();
        if (remaining.Length < 2) _junctions.Remove(junction);
        else Replace(junction, remaining);
    }

    private void Replace(AndJunction junction, EntityId[] causes) =>
        _junctions[_junctions.IndexOf(junction)] = junction with { Causes = causes };

    private int JunctionIndex(JunctionId id)
    {
        var index = _junctions.FindIndex(j => j.Id == id);
        if (index < 0) throw new ArgumentException($"No AND-junction with id {id.Value} in this tree.", nameof(id));
        return index;
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

    private Snapshot Capture() => new([.. _entities], [.. _links], [.. _junctions], _nextId, _nextJunctionId);

    private void Restore(Snapshot snapshot)
    {
        _entities.Clear();
        _entities.AddRange(snapshot.Entities);
        _links.Clear();
        _links.AddRange(snapshot.Links);
        _junctions.Clear();
        _junctions.AddRange(snapshot.Junctions);
        _nextId = snapshot.NextId;
        _nextJunctionId = snapshot.NextJunctionId;
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
