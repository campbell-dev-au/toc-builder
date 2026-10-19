using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TocBuilder.Domain;

public enum ImportOutcome { Imported, NewerVersion, Corrupt }

/// <summary>How far the user has dragged an Entity from where the automatic layout puts it, in layout units.</summary>
public readonly record struct Nudge(double X, double Y);

/// <summary>The result of reading a file. <see cref="Tree"/>, <see cref="Name"/> and <see cref="Nudges"/> are set only when imported; otherwise <see cref="Message"/> says why not.</summary>
public sealed record ImportResult(ImportOutcome Outcome, string? Name = null, Tree? Tree = null, string? Message = null, IReadOnlyDictionary<EntityId, Nudge>? Nudges = null);

/// <summary>Reads and writes a tree as a versioned JSON file.</summary>
public static class TreeSerializer
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Export(string name, Tree tree, IReadOnlyDictionary<EntityId, Nudge>? nudges = null)
    {
        var (nextId, nextJunctionId, nextReservationId) = tree.NextIds;
        var file = new FileDto(
            CurrentVersion,
            name,
            [.. tree.Entities.Select(e => new EntityDto(e.Id.Value, e.Statement, e.Tag))],
            [.. tree.Links.Select(l => new LinkDto(l.Cause.Value, l.Effect.Value))],
            [.. tree.Junctions.Select(j => new JunctionDto(j.Id.Value, j.Effect.Value, [.. j.Causes.Select(c => c.Value)]))],
            [.. tree.Reservations.Select(ToDto)],
            nextId, nextJunctionId, nextReservationId,
            [.. (nudges ?? new Dictionary<EntityId, Nudge>()).Select(n => new NudgeDto(n.Key.Value, n.Value.X, n.Value.Y))],
            tree.Scope);
        return JsonSerializer.Serialize(file, Options);
    }

    /// <summary>Each step rewrites a file of version N (the key) into version N + 1. Add one here whenever <see cref="CurrentVersion"/> goes up.</summary>
    private static readonly Dictionary<int, Func<JsonObject, JsonObject>> Upgrades = [];

    public static ImportResult Import(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root || root["version"] is not JsonValue versionNode || !versionNode.TryGetValue<int>(out var version) || version < 1)
                return Corrupt("This is not a TOC Builder file: it has no valid version.");
            if (version > CurrentVersion)
                return new ImportResult(ImportOutcome.NewerVersion, Message: $"This file is version {version}, but this app only understands up to version {CurrentVersion}. Update the app to open it.");
            for (; version < CurrentVersion; version++)
                root = Upgrades[version](root);
            var file = root.Deserialize<FileDto>(Options) ?? throw new JsonException("The file is empty.");
            return Build(file);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            return Corrupt("This file is not a valid TOC Builder file, so it could not be read.");
        }
    }

    private static ImportResult Corrupt(string message) => new(ImportOutcome.Corrupt, Message: message);

    private static ImportResult Build(FileDto file)
    {
        if (string.IsNullOrWhiteSpace(file.Name) || file.Entities is null || file.Links is null || file.Junctions is null || file.Reservations is null
            || file.Entities.Any(e => e is null || e.Statement is null || !Enum.IsDefined(e.Tag))
            || file.Links.Any(l => l is null)
            || file.Junctions.Any(j => j is null || j.Causes is null)
            || (file.Nudges is not null && file.Nudges.Any(n => n is null))
            || file.Reservations.Any(r => r is null || r.Note is null || r.TargetKind is null || !Enum.IsDefined(r.Category) || !Enum.IsDefined(r.State)))
            return Corrupt("This file is missing parts of the tree.");

        var entityIds = file.Entities.Select(e => e.Id).ToHashSet();
        var junctionIds = file.Junctions.Select(j => j.Id).ToHashSet();
        var links = file.Links.Select(l => (l.Cause, l.Effect)).ToHashSet();
        var consistent =
            entityIds.Count == file.Entities.Length
            && junctionIds.Count == file.Junctions.Length
            && file.Reservations.Select(r => r.Id).Distinct().Count() == file.Reservations.Length
            && links.Count == file.Links.Length
            && file.Links.All(l => l.Cause != l.Effect && entityIds.Contains(l.Cause) && entityIds.Contains(l.Effect))
            && !HasCycle(entityIds, links)
            && file.Junctions.All(j => j.Causes.Length >= 2 && j.Causes.Distinct().Count() == j.Causes.Length && j.Causes.All(c => links.Contains((c, j.Effect))))
            && file.Reservations.All(r => r.TargetKind switch
            {
                "entity" => entityIds.Contains(r.A),
                "link" => links.Contains((r.A, r.B)),
                "junction" => junctionIds.Contains(r.A),
                _ => false,
            })
            && file.NextId > entityIds.DefaultIfEmpty().Max()
            && file.NextJunctionId > junctionIds.DefaultIfEmpty().Max()
            && file.NextReservationId > file.Reservations.Select(r => r.Id).DefaultIfEmpty().Max();
        if (!consistent) return Corrupt("This file's entities, links, junctions and reservations do not agree with each other.");

        var tree = Tree.FromState(
            file.Entities.Select(e => new Entity(new EntityId(e.Id), e.Statement, e.Tag)),
            file.Links.Select(l => new CausalLink(new EntityId(l.Cause), new EntityId(l.Effect))),
            file.Junctions.Select(j => new AndJunction(new JunctionId(j.Id), new EntityId(j.Effect), [.. j.Causes.Select(c => new EntityId(c))])),
            file.Reservations.Select(FromDto),
            file.Scope ?? "",
            file.NextId, file.NextJunctionId, file.NextReservationId);
        var nudges = (file.Nudges ?? []).Where(n => entityIds.Contains(n.Entity)).DistinctBy(n => n.Entity)
            .ToDictionary(n => new EntityId(n.Entity), n => new Nudge(n.X, n.Y));   // a nudge for an Entity that is not there has nothing to move
        return new ImportResult(ImportOutcome.Imported, file.Name, tree, Nudges: nudges);
    }

    private static bool HasCycle(HashSet<int> entityIds, HashSet<(int Cause, int Effect)> links)
    {
        var incoming = entityIds.ToDictionary(id => id, id => links.Count(l => l.Effect == id));
        var ready = new Stack<int>(incoming.Where(i => i.Value == 0).Select(i => i.Key));
        var visited = 0;
        while (ready.TryPop(out var id))
        {
            visited++;
            foreach (var link in links.Where(l => l.Cause == id))
                if (--incoming[link.Effect] == 0) ready.Push(link.Effect);
        }
        return visited < entityIds.Count;
    }

    private static ReservationDto ToDto(Reservation r)
    {
        var (kind, a, b) = r.Target switch
        {
            OnEntity e => ("entity", e.Entity.Value, 0),
            OnLink l => ("link", l.Link.Cause.Value, l.Link.Effect.Value),
            OnJunction j => ("junction", j.Junction.Value, 0),
            _ => throw new ArgumentException($"Unknown reservation target {r.Target}.", nameof(r)),
        };
        return new ReservationDto(r.Id.Value, kind, a, b, r.Category, r.State, r.Note);
    }

    private static Reservation FromDto(ReservationDto r)
    {
        ReservationTarget target = r.TargetKind switch
        {
            "entity" => new OnEntity(new EntityId(r.A)),
            "link" => new OnLink(new CausalLink(new EntityId(r.A), new EntityId(r.B))),
            "junction" => new OnJunction(new JunctionId(r.A)),
            _ => throw new JsonException($"Unknown reservation target '{r.TargetKind}'."),
        };
        return new Reservation(new ReservationId(r.Id), target, r.Category, r.State, r.Note);
    }

    private sealed record FileDto(int Version, string Name, EntityDto[] Entities, LinkDto[] Links, JunctionDto[] Junctions, ReservationDto[] Reservations, int NextId, int NextJunctionId, int NextReservationId, NudgeDto[]? Nudges = null, string? Scope = null);
    private sealed record NudgeDto(int Entity, double X, double Y);
    private sealed record EntityDto(int Id, string Statement, EntityTag Tag);
    private sealed record LinkDto(int Cause, int Effect);
    private sealed record JunctionDto(int Id, int Effect, int[] Causes);
    private sealed record ReservationDto(int Id, string TargetKind, int A, int B, ClrCategory Category, ReservationState State, string Note);
}
