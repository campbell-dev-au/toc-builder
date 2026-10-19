namespace TocBuilder.Domain.Tests;

public class AndJunctionTests
{
    private static (Tree Tree, Entity Effect, Entity A, Entity B, Entity C) TreeWithThreeCauses()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Orders ship late");
        var a = tree.AddCause(effect.Id, "Picking is slow");
        var b = tree.AddCause(effect.Id, "Carrier is delayed");
        var c = tree.AddCause(effect.Id, "Stock is short");
        return (tree, effect, a, b, c);
    }

    [Fact]
    public void Causes_of_one_effect_can_be_grouped_into_an_AND_junction()
    {
        var (tree, effect, a, b, _) = TreeWithThreeCauses();

        var result = tree.Group(effect.Id, [a.Id, b.Id]);

        Assert.Equal(JunctionOutcome.Done, result.Outcome);
        var junction = Assert.Single(tree.Junctions);
        Assert.Equal(result.Junction, junction);
        Assert.Equal(effect.Id, junction.Effect);
        Assert.Equal([a.Id, b.Id], junction.Causes);
        Assert.Equal(3, tree.Links.Count);   // grouping keeps every Causal link
    }

    [Fact]
    public void Make_AND_groups_selected_causes_under_the_effect_they_share()
    {
        var (tree, effect, a, b, _) = TreeWithThreeCauses();

        var result = tree.MakeAnd([a.Id, b.Id]);

        Assert.Equal(JunctionOutcome.Done, result.Outcome);
        var junction = Assert.Single(tree.Junctions);
        Assert.Equal(effect.Id, junction.Effect);
        Assert.Equal([a.Id, b.Id], junction.Causes);
    }

    [Fact]
    public void Make_AND_with_fewer_than_two_causes_is_refused()
    {
        var (tree, _, a, _, _) = TreeWithThreeCauses();

        Assert.Equal(JunctionOutcome.TooFewCauses, tree.MakeAnd([]).Outcome);
        Assert.Equal(JunctionOutcome.TooFewCauses, tree.MakeAnd([a.Id]).Outcome);
        Assert.Equal(JunctionOutcome.TooFewCauses, tree.MakeAnd([a.Id, a.Id]).Outcome);
        Assert.Empty(tree.Junctions);
    }

    [Fact]
    public void Make_AND_for_causes_with_different_effects_is_refused()
    {
        var (tree, _, a, _, _) = TreeWithThreeCauses();
        var other = tree.AddEntity("Customers leave");
        var otherCause = tree.AddCause(other.Id, "Complaints go unanswered");

        var result = tree.MakeAnd([a.Id, otherCause.Id]);

        Assert.Equal(JunctionOutcome.DifferentEffects, result.Outcome);
        Assert.Empty(tree.Junctions);
    }

    [Fact]
    public void Make_AND_for_causes_sharing_several_effects_is_refused_as_ambiguous()
    {
        var (tree, effect, a, b, _) = TreeWithThreeCauses();
        var other = tree.AddEntity("Customers leave");
        tree.Link(a.Id, other.Id);
        tree.Link(b.Id, other.Id);

        var result = tree.MakeAnd([a.Id, b.Id]);

        Assert.Equal(JunctionOutcome.AmbiguousEffect, result.Outcome);
        Assert.Empty(tree.Junctions);
    }

    [Fact]
    public void Make_AND_is_one_undo_step()
    {
        var (tree, _, a, b, _) = TreeWithThreeCauses();
        tree.MakeAnd([a.Id, b.Id]);

        tree.Undo();

        Assert.Empty(tree.Junctions);
    }

    [Fact]
    public void Grouping_fewer_than_two_distinct_causes_is_refused()
    {
        var (tree, effect, a, _, _) = TreeWithThreeCauses();

        Assert.Equal(JunctionOutcome.TooFewCauses, tree.Group(effect.Id, [a.Id]).Outcome);
        Assert.Equal(JunctionOutcome.TooFewCauses, tree.Group(effect.Id, [a.Id, a.Id]).Outcome);
        Assert.Empty(tree.Junctions);
    }

    [Fact]
    public void Grouping_an_entity_that_is_not_a_cause_of_the_effect_is_refused()
    {
        var (tree, effect, a, _, _) = TreeWithThreeCauses();
        var stranger = tree.AddEntity("Unrelated");

        var result = tree.Group(effect.Id, [a.Id, stranger.Id]);

        Assert.Equal(JunctionOutcome.NotACauseOfEffect, result.Outcome);
        Assert.Empty(tree.Junctions);
    }

    [Fact]
    public void A_cause_cannot_be_in_two_junctions_of_the_same_effect()
    {
        var (tree, effect, a, b, c) = TreeWithThreeCauses();
        tree.Group(effect.Id, [a.Id, b.Id]);

        var result = tree.Group(effect.Id, [b.Id, c.Id]);

        Assert.Equal(JunctionOutcome.AlreadyInJunction, result.Outcome);
        Assert.Single(tree.Junctions);
    }

    [Fact]
    public void A_cause_can_be_added_to_an_existing_junction()
    {
        var (tree, effect, a, b, c) = TreeWithThreeCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;

        var outcome = tree.AddToJunction(junction.Id, c.Id);

        Assert.Equal(JunctionOutcome.Done, outcome);
        Assert.Equal([a.Id, b.Id, c.Id], Assert.Single(tree.Junctions).Causes);
    }

    [Fact]
    public void Adding_an_unlinked_entity_to_a_junction_also_links_it_as_a_cause()
    {
        var (tree, effect, a, b, _) = TreeWithThreeCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        var extra = tree.AddEntity("Forecast is wrong");

        var outcome = tree.AddToJunction(junction.Id, extra.Id);

        Assert.Equal(JunctionOutcome.Done, outcome);
        Assert.Contains(new CausalLink(extra.Id, effect.Id), tree.Links);
        Assert.Contains(extra.Id, tree.Junctions[0].Causes);
    }

    [Fact]
    public void Adding_an_entity_that_would_create_a_cycle_is_refused()
    {
        var (tree, effect, a, b, _) = TreeWithThreeCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        var downstream = tree.AddEffect(effect.Id, "Customers complain");

        var outcome = tree.AddToJunction(junction.Id, downstream.Id);

        Assert.Equal(JunctionOutcome.WouldCreateCycle, outcome);
        Assert.Equal([a.Id, b.Id], tree.Junctions[0].Causes);
    }

    [Fact]
    public void Adding_a_cause_already_in_a_junction_of_the_effect_is_refused()
    {
        var (tree, effect, a, b, c) = TreeWithThreeCauses();
        var first = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        var d = tree.AddCause(effect.Id, "Pallets are missing");
        var second = tree.Group(effect.Id, [c.Id, d.Id]).Junction!;

        Assert.Equal(JunctionOutcome.AlreadyInJunction, tree.AddToJunction(first.Id, c.Id));
        Assert.Equal(JunctionOutcome.AlreadyInJunction, tree.AddToJunction(first.Id, a.Id));
        Assert.Equal(2, tree.Junctions.Count);
        Assert.Equal([c.Id, d.Id], tree.Junctions.Single(j => j.Id == second.Id).Causes);
    }

    [Fact]
    public void Ungrouping_removes_the_junction_but_keeps_the_causal_links()
    {
        var (tree, effect, a, b, _) = TreeWithThreeCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;

        tree.Ungroup(junction.Id);

        Assert.Empty(tree.Junctions);
        Assert.Equal(3, tree.Links.Count);
    }

    [Fact]
    public void Removing_a_cause_from_a_junction_leaves_it_linked_and_dissolves_a_junction_left_with_one_cause()
    {
        var (tree, effect, a, b, c) = TreeWithThreeCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id, c.Id]).Junction!;

        tree.RemoveFromJunction(junction.Id, c.Id);
        Assert.Equal([a.Id, b.Id], Assert.Single(tree.Junctions).Causes);

        tree.RemoveFromJunction(junction.Id, b.Id);
        Assert.Empty(tree.Junctions);
        Assert.Equal(3, tree.Links.Count);
    }

    [Fact]
    public void Deleting_a_member_cause_shrinks_the_junction_and_deleting_the_effect_removes_it()
    {
        var (tree, effect, a, b, c) = TreeWithThreeCauses();
        tree.Group(effect.Id, [a.Id, b.Id, c.Id]);

        tree.Delete(c.Id);
        Assert.Equal([a.Id, b.Id], Assert.Single(tree.Junctions).Causes);

        tree.Delete(a.Id);
        Assert.Empty(tree.Junctions);

        tree.Group(effect.Id, [b.Id, tree.AddCause(effect.Id, "Another").Id]);
        tree.Delete(effect.Id);
        Assert.Empty(tree.Junctions);
    }

    [Fact]
    public void Grouping_adding_and_ungrouping_are_each_undoable_and_redoable()
    {
        var (tree, effect, a, b, c) = TreeWithThreeCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        tree.AddToJunction(junction.Id, c.Id);
        tree.Ungroup(junction.Id);

        tree.Undo();
        Assert.Equal([a.Id, b.Id, c.Id], Assert.Single(tree.Junctions).Causes);
        tree.Undo();
        Assert.Equal([a.Id, b.Id], Assert.Single(tree.Junctions).Causes);
        tree.Undo();
        Assert.Empty(tree.Junctions);

        tree.Redo();
        tree.Redo();
        tree.Redo();
        Assert.Empty(tree.Junctions);
    }

    [Fact]
    public void Refused_junction_commands_leave_no_undo_step()
    {
        var (tree, effect, a, _, _) = TreeWithThreeCauses();
        tree.Undo();   // creates a redo step we must not lose
        var canRedo = tree.CanRedo;

        tree.Group(effect.Id, [a.Id]);

        Assert.Equal(canRedo, tree.CanRedo);
    }

    [Fact]
    public void Undo_restores_the_junction_id_counter_so_a_regrouped_junction_gets_the_same_id()
    {
        var (tree, effect, a, b, _) = TreeWithThreeCauses();
        var first = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        tree.Undo();

        var second = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;

        Assert.Equal(first.Id, second.Id);   // undo restores the counter, so the id is free again
    }

    [Fact]
    public void Unknown_ids_throw()
    {
        var (tree, effect, a, b, _) = TreeWithThreeCauses();
        Assert.Throws<ArgumentException>(() => tree.Group(new EntityId(99), [a.Id, b.Id]));
        Assert.Throws<ArgumentException>(() => tree.Group(effect.Id, [a.Id, new EntityId(99)]));
        Assert.Throws<ArgumentException>(() => tree.Ungroup(new JunctionId(1)));
        Assert.Throws<ArgumentException>(() => tree.AddToJunction(new JunctionId(1), a.Id));
    }

    [Fact]
    public void Removing_a_cause_and_deleting_a_member_are_undoable()
    {
        var (tree, effect, a, b, c) = TreeWithThreeCauses();
        var junction = tree.Group(effect.Id, [a.Id, b.Id, c.Id]).Junction!;

        tree.RemoveFromJunction(junction.Id, c.Id);
        tree.Undo();
        Assert.Equal([a.Id, b.Id, c.Id], Assert.Single(tree.Junctions).Causes);

        tree.Delete(c.Id);
        tree.Undo();
        Assert.Equal([a.Id, b.Id, c.Id], Assert.Single(tree.Junctions).Causes);
        Assert.Contains(new CausalLink(c.Id, effect.Id), tree.Links);
    }
}
