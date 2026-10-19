namespace TocBuilder.Domain.Tests;

public class SuggestionTests
{
    private static Suggestion Cause(string statement) => new(SuggestionKind.Cause, statement);
    private static Suggestion Effect(string statement) => new(SuggestionKind.Effect, statement);

    [Fact]
    public void Candidates_for_an_entity_are_kept_when_they_are_new_statements_of_the_right_kind()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");

        var set = SuggestionSet.ForEntity(tree, late.Id, [Cause("Machines are overloaded"), Effect("Customers leave"), new(SuggestionKind.CoCause, "Not for an entity")]);

        Assert.Equal([Cause("Machines are overloaded"), Effect("Customers leave")], set.Pending);
    }

    [Fact]
    public void Blank_candidates_and_duplicates_of_existing_entities_or_of_each_other_are_dropped()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        tree.AddCause(late.Id, "Machines are overloaded");

        var set = SuggestionSet.ForEntity(tree, late.Id, [Cause("  "), Cause("  machines ARE overloaded "), Cause("No buffer"), Cause("no buffer"), Effect("Late deliveries")]);

        Assert.Equal([Cause("No buffer")], set.Pending);
    }

    [Fact]
    public void Creating_a_set_changes_nothing_in_the_tree_its_summary_or_its_JSON()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var jsonBefore = TreeSerializer.Export("T", tree);
        var summaryBefore = tree.Summarise();

        _ = SuggestionSet.ForEntity(tree, late.Id, [Cause("Machines are overloaded")]);

        Assert.Equal([late], tree.Entities);
        Assert.Empty(tree.Links);
        Assert.Equal(jsonBefore, TreeSerializer.Export("T", tree));
        Assert.Equal(summaryBefore.UnconnectedEntities, tree.Summarise().UnconnectedEntities);
    }

    [Fact]
    public void Accepting_a_cause_adds_it_as_a_cause_of_the_entity_and_takes_it_off_the_pending_list()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var set = SuggestionSet.ForEntity(tree, late.Id, [Cause("Machines are overloaded"), Effect("Customers leave")]);

        var outcome = set.Accept(tree, Cause("Machines are overloaded"));

        Assert.Equal(AcceptOutcome.Accepted, outcome);
        var added = tree.Entities.Single(e => e.Statement == "Machines are overloaded");
        Assert.Equal([new CausalLink(added.Id, late.Id)], tree.Links);
        Assert.Equal([Effect("Customers leave")], set.Pending);
    }

    [Fact]
    public void Accepting_an_effect_adds_it_as_an_effect_of_the_entity()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var set = SuggestionSet.ForEntity(tree, late.Id, [Effect("Customers leave")]);

        set.Accept(tree, Effect("Customers leave"));

        var added = tree.Entities.Single(e => e.Statement == "Customers leave");
        Assert.Equal([new CausalLink(late.Id, added.Id)], tree.Links);
    }

    [Fact]
    public void A_suggestion_can_be_edited_then_accepted_and_the_edit_is_what_enters_the_tree()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var set = SuggestionSet.ForEntity(tree, late.Id, [Cause("Machines are overloaded")]);

        set.Accept(tree, Cause("Machines are overloaded"), "  Machines run at 110% load ");

        Assert.Equal(["Late deliveries", "Machines run at 110% load"], tree.Entities.Select(e => e.Statement));
        Assert.Empty(set.Pending);
    }

    [Fact]
    public void An_edit_that_duplicates_an_existing_entity_is_refused_and_the_suggestion_stays_pending()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var set = SuggestionSet.ForEntity(tree, late.Id, [Cause("Machines are overloaded")]);

        var outcome = set.Accept(tree, Cause("Machines are overloaded"), "late DELIVERIES");

        Assert.Equal(AcceptOutcome.DuplicateStatement, outcome);
        Assert.Equal([late], tree.Entities);
        Assert.Equal([Cause("Machines are overloaded")], set.Pending);
    }

    [Fact]
    public void Accepting_is_one_undo_step()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var set = SuggestionSet.ForEntity(tree, late.Id, [Cause("Machines are overloaded")]);
        set.Accept(tree, Cause("Machines are overloaded"));

        tree.Undo();

        Assert.Equal([late], tree.Entities);
        Assert.Empty(tree.Links);
    }

    [Fact]
    public void A_dismissed_suggestion_leaves_the_pending_list_and_the_tree_is_untouched()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var set = SuggestionSet.ForEntity(tree, late.Id, [Cause("Machines are overloaded"), Effect("Customers leave")]);

        set.Dismiss(Cause("Machines are overloaded"));

        Assert.Equal([Effect("Customers leave")], set.Pending);
        Assert.Equal([late], tree.Entities);
    }

    [Fact]
    public void Accepting_a_suggestion_that_is_not_pending_is_refused()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var set = SuggestionSet.ForEntity(tree, late.Id, []);

        Assert.Equal(AcceptOutcome.NoLongerValid, set.Accept(tree, Cause("Machines are overloaded")));
        Assert.Equal([late], tree.Entities);
    }

    [Fact]
    public void A_suggestion_may_name_an_existing_entity_and_accepting_it_links_that_entity()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var overload = tree.AddEntity("Machines are overloaded");
        var lost = tree.AddEntity("Customers leave");
        var set = SuggestionSet.ForEntity(tree, late.Id, [new(SuggestionKind.Cause, "ignored wording", overload.Id), new(SuggestionKind.Effect, "", lost.Id)]);

        Assert.Equal(["Machines are overloaded", "Customers leave"], set.Pending.Select(p => p.Statement));
        set.Accept(tree, set.Pending[0]);
        set.Accept(tree, set.Pending[0]);

        Assert.Equal(3, tree.Entities.Count);
        Assert.Equal([new CausalLink(overload.Id, late.Id), new CausalLink(late.Id, lost.Id)], tree.Links);
    }

    [Fact]
    public void A_suggestion_that_would_create_a_cycle_or_repeat_a_link_or_name_a_missing_entity_is_dropped()
    {
        var tree = new Tree();
        var root = tree.AddEntity("Root");
        var middle = tree.AddEffect(root.Id, "Middle");
        var top = tree.AddEffect(middle.Id, "Top");
        var elsewhere = tree.AddEntity("Elsewhere");

        // Suggesting Top as a cause of Root would close the loop Root -> Middle -> Top -> Root.
        var set = SuggestionSet.ForEntity(tree, root.Id, [
            new(SuggestionKind.Cause, "", top.Id),             // cycle
            new(SuggestionKind.Cause, "", root.Id),            // itself
            new(SuggestionKind.Effect, "", middle.Id),         // already linked
            new(SuggestionKind.Cause, "", new EntityId(999)),  // not in this tree
            new(SuggestionKind.Cause, "", elsewhere.Id)]);     // fine

        Assert.Equal([new Suggestion(SuggestionKind.Cause, "Elsewhere", elsewhere.Id)], set.Pending);
    }

    [Fact]
    public void A_reused_entity_cannot_be_reworded_on_accept()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var overload = tree.AddEntity("Machines are overloaded");
        var set = SuggestionSet.ForEntity(tree, late.Id, [new(SuggestionKind.Cause, "", overload.Id)]);

        Assert.Throws<ArgumentException>(() => set.Accept(tree, set.Pending[0], "Something else"));
    }

    [Fact]
    public void A_suggestion_whose_entity_or_link_has_gone_since_it_was_made_is_no_longer_valid()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var overload = tree.AddEntity("Machines are overloaded");
        var set = SuggestionSet.ForEntity(tree, late.Id, [new(SuggestionKind.Cause, "", overload.Id), Cause("No buffer")]);
        tree.Link(overload.Id, late.Id);

        Assert.Equal(AcceptOutcome.NoLongerValid, set.Accept(tree, set.Pending[0]));
        Assert.Equal([Cause("No buffer")], set.Pending);

        tree.Delete(late.Id);
        Assert.Equal(AcceptOutcome.NoLongerValid, set.Accept(tree, Cause("No buffer")));
        Assert.Empty(set.Pending);
    }

    private static (Tree Tree, Entity Effect, Entity A, Entity B, AndJunction Junction) JunctionTree()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Late deliveries");
        var a = tree.AddCause(effect.Id, "Machines are overloaded");
        var b = tree.AddCause(effect.Id, "No buffer");
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        return (tree, effect, a, b, junction);
    }

    [Fact]
    public void A_junction_takes_only_co_cause_candidates_that_are_new()
    {
        var (tree, _, _, _, junction) = JunctionTree();

        var set = SuggestionSet.ForJunction(tree, junction.Id, [new(SuggestionKind.CoCause, "Rush orders"), new(SuggestionKind.CoCause, "no BUFFER"), Cause("Not a co-cause")]);

        Assert.Equal([new Suggestion(SuggestionKind.CoCause, "Rush orders")], set.Pending);
    }

    [Fact]
    public void Accepting_a_co_cause_adds_it_to_the_junction_as_one_undo_step()
    {
        var (tree, effect, a, b, junction) = JunctionTree();
        var set = SuggestionSet.ForJunction(tree, junction.Id, [new(SuggestionKind.CoCause, "Rush orders")]);

        Assert.Equal(AcceptOutcome.Accepted, set.Accept(tree, set.Pending[0], "Rush orders arrive weekly"));

        var added = tree.Entities.Single(e => e.Statement == "Rush orders arrive weekly");
        Assert.Contains(new CausalLink(added.Id, effect.Id), tree.Links);
        Assert.Equal([a.Id, b.Id, added.Id], tree.Junctions.Single().Causes);

        tree.Undo();
        Assert.Equal([a.Id, b.Id], tree.Junctions.Single().Causes);
        Assert.DoesNotContain(tree.Entities, e => e.Id == added.Id);
    }

    [Fact]
    public void An_existing_entity_can_be_suggested_as_a_co_cause_unless_it_is_in_the_junction_or_would_cycle()
    {
        var (tree, effect, a, _, junction) = JunctionTree();
        var spare = tree.AddEntity("Spare");
        var above = tree.AddEffect(effect.Id, "Customers leave");

        var set = SuggestionSet.ForJunction(tree, junction.Id, [
            new(SuggestionKind.CoCause, "", a.Id),       // already in it
            new(SuggestionKind.CoCause, "", above.Id),   // an effect of the junction's effect: would cycle
            new(SuggestionKind.CoCause, "", spare.Id)]);

        Assert.Equal([new Suggestion(SuggestionKind.CoCause, "Spare", spare.Id)], set.Pending);
        set.Accept(tree, set.Pending[0]);
        Assert.Contains(spare.Id, tree.Junctions.Single().Causes);
        Assert.Contains(new CausalLink(spare.Id, effect.Id), tree.Links);
    }

    [Fact]
    public void A_co_cause_is_no_longer_valid_once_its_junction_is_gone()
    {
        var (tree, _, _, _, junction) = JunctionTree();
        var set = SuggestionSet.ForJunction(tree, junction.Id, [new(SuggestionKind.CoCause, "Rush orders")]);
        tree.Ungroup(junction.Id);

        Assert.Equal(AcceptOutcome.NoLongerValid, set.Accept(tree, set.Pending[0]));
        Assert.DoesNotContain(tree.Entities, e => e.Statement == "Rush orders");
    }

    [Fact]
    public void A_suggestion_that_has_become_a_duplicate_since_it_was_made_is_dropped_on_accept()
    {
        var tree = new Tree();
        var late = tree.AddEntity("Late deliveries");
        var set = SuggestionSet.ForEntity(tree, late.Id, [Cause("Machines are overloaded")]);
        tree.AddEntity("machines are overloaded");

        Assert.Equal(AcceptOutcome.NoLongerValid, set.Accept(tree, Cause("Machines are overloaded")));

        Assert.Equal(2, tree.Entities.Count);
        Assert.Empty(set.Pending);
    }
}
