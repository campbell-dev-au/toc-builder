using TocBuilder.Domain;

namespace TocBuilder.Web.Client.Components;

// The words shown for a Reservation and what it is raised on.
public static class ReservationText
{
    public static string Describe(Tree tree, ReservationTarget target) => target switch
    {
        OnEntity entity => StatementOf(tree, entity.Entity),
        OnLink link => $"{StatementOf(tree, link.Link.Cause)} → {StatementOf(tree, link.Link.Effect)}",
        OnJunction junction => DescribeJunction(tree, tree.Junctions.First(j => j.Id == junction.Junction)),
        _ => throw new InvalidOperationException($"Unhandled reservation target {target.GetType().Name}.")
    };

    private static string StatementOf(Tree tree, EntityId id) => tree.Entities.First(e => e.Id == id).Statement;

    private static string DescribeJunction(Tree tree, AndJunction junction) =>
        $"AND of {string.Join(" + ", junction.Causes.Select(id => StatementOf(tree, id)))} → {StatementOf(tree, junction.Effect)}";

    public static string CategoryLabel(ClrCategory category) => category switch
    {
        ClrCategory.Clarity => "Clarity",
        ClrCategory.EntityExistence => "Entity existence",
        ClrCategory.CausalityExistence => "Causality existence",
        ClrCategory.CauseInsufficiency => "Cause insufficiency",
        ClrCategory.AdditionalCause => "Additional cause",
        ClrCategory.CauseEffectReversal => "Cause-effect reversal",
        ClrCategory.PredictedEffectExistence => "Predicted effect existence",
        ClrCategory.Tautology => "Tautology",
        _ => throw new InvalidOperationException($"Unhandled CLR category {category}.")
    };
}
