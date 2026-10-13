namespace TocBuilder.Domain.Tests;

public class LinkingTests
{
    [Fact]
    public void Linking_two_existing_entities_makes_one_a_cause_of_the_other()
    {
        var tree = new Tree();
        var cause = tree.AddEntity("Work is released without regard to capacity");
        var effect = tree.AddEntity("Orders wait in a queue");

        var outcome = tree.Link(cause.Id, effect.Id);

        Assert.Equal(LinkOutcome.Linked, outcome);
        Assert.Equal([new CausalLink(cause.Id, effect.Id)], tree.Links);
    }

    [Fact]
    public void An_entity_can_be_a_cause_of_several_effects()
    {
        var tree = new Tree();
        var cause = tree.AddEntity("Work is released without regard to capacity");
        var firstEffect = tree.AddEntity("Orders wait in a queue");
        var secondEffect = tree.AddEntity("Expediting disrupts the plan");

        tree.Link(cause.Id, firstEffect.Id);
        tree.Link(cause.Id, secondEffect.Id);

        Assert.Equal(
            [new CausalLink(cause.Id, firstEffect.Id), new CausalLink(cause.Id, secondEffect.Id)],
            tree.Links);
    }

    [Fact]
    public void A_link_that_would_create_a_cycle_is_rejected_and_leaves_the_tree_unchanged()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Customers receive late deliveries");
        var middle = tree.AddCause(effect.Id, "Orders wait in a queue");
        var rootCause = tree.AddCause(middle.Id, "Work is released without regard to capacity");
        var linksBefore = tree.Links.ToArray();

        var outcome = tree.Link(effect.Id, rootCause.Id);   // the effect would become a cause of its own root cause

        Assert.Equal(LinkOutcome.WouldCreateCycle, outcome);
        Assert.Equal(linksBefore, tree.Links);
    }

    [Fact]
    public void An_entity_cannot_be_linked_as_a_cause_of_itself()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders wait in a queue");

        var outcome = tree.Link(entity.Id, entity.Id);

        Assert.Equal(LinkOutcome.WouldCreateCycle, outcome);
        Assert.Empty(tree.Links);
    }

    [Fact]
    public void Linking_the_same_pair_twice_leaves_a_single_link()
    {
        var tree = new Tree();
        var cause = tree.AddEntity("Work is released without regard to capacity");
        var effect = tree.AddEntity("Orders wait in a queue");
        tree.Link(cause.Id, effect.Id);

        var outcome = tree.Link(cause.Id, effect.Id);

        Assert.Equal(LinkOutcome.AlreadyLinked, outcome);
        Assert.Single(tree.Links);
    }
}
