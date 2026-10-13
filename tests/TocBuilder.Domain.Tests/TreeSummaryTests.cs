namespace TocBuilder.Domain.Tests;

public class TreeSummaryTests
{
    [Fact]
    public void An_empty_tree_has_an_empty_summary()
    {
        var summary = new Tree().Summarise();

        Assert.Empty(summary.UdesWithoutCoreProblem);
        Assert.Empty(summary.CoreProblemsExplainingNoUde);
        Assert.Empty(summary.UnconnectedEntities);
        Assert.Empty(summary.RootCauses);
    }

    [Fact]
    public void A_UDE_reached_from_a_Core_problem_through_a_chain_is_explained()
    {
        var tree = new Tree();
        var ude = tree.AddEntity("Late deliveries");
        var middle = tree.AddCause(ude.Id, "Orders wait in a queue");
        var core = tree.AddCause(middle.Id, "Work is released without regard to capacity");
        tree.SetTag(ude.Id, EntityTag.Ude);
        tree.SetTag(core.Id, EntityTag.CoreProblem);

        var summary = tree.Summarise();

        Assert.Empty(summary.UdesWithoutCoreProblem);
        Assert.Empty(summary.CoreProblemsExplainingNoUde);
    }

    [Fact]
    public void A_UDE_with_no_path_from_a_Core_problem_is_listed_even_when_no_Core_problem_exists()
    {
        var tree = new Tree();
        var ude = tree.AddEntity("Late deliveries");
        tree.AddCause(ude.Id, "Orders wait in a queue");
        tree.SetTag(ude.Id, EntityTag.Ude);

        Assert.Equal([Tagged(tree, ude.Id, EntityTag.Ude)], tree.Summarise().UdesWithoutCoreProblem);
    }

    [Fact]
    public void A_Core_problem_that_explains_no_UDE_is_listed()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Late deliveries");
        var core = tree.AddCause(effect.Id, "Work is released without regard to capacity");
        tree.SetTag(core.Id, EntityTag.CoreProblem);   // the effect it explains is not tagged as a UDE

        Assert.Equal([Tagged(tree, core.Id, EntityTag.CoreProblem)], tree.Summarise().CoreProblemsExplainingNoUde);
    }

    [Fact]
    public void A_Core_problem_with_causes_beneath_it_still_explains_UDEs_above_it()
    {
        var tree = new Tree();
        var ude = tree.AddEntity("Late deliveries");
        var core = tree.AddCause(ude.Id, "Work is released without regard to capacity");
        tree.AddCause(core.Id, "Managers are measured on utilisation");
        tree.SetTag(ude.Id, EntityTag.Ude);
        tree.SetTag(core.Id, EntityTag.CoreProblem);

        var summary = tree.Summarise();

        Assert.Empty(summary.UdesWithoutCoreProblem);
        Assert.Empty(summary.CoreProblemsExplainingNoUde);
    }

    [Fact]
    public void A_UDE_is_not_explained_by_a_Core_problem_that_is_only_its_effect()
    {
        var tree = new Tree();
        var core = tree.AddEntity("Work is released without regard to capacity");
        var ude = tree.AddCause(core.Id, "Late deliveries");   // the cause points the wrong way round
        tree.SetTag(ude.Id, EntityTag.Ude);
        tree.SetTag(core.Id, EntityTag.CoreProblem);

        var summary = tree.Summarise();

        Assert.Equal([Tagged(tree, ude.Id, EntityTag.Ude)], summary.UdesWithoutCoreProblem);
        Assert.Equal([Tagged(tree, core.Id, EntityTag.CoreProblem)], summary.CoreProblemsExplainingNoUde);
    }

    [Fact]
    public void Entities_with_no_links_are_unconnected()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Late deliveries");
        tree.AddCause(effect.Id, "Orders wait in a queue");
        var alone = tree.AddEntity("Unrelated observation");

        Assert.Equal([alone], tree.Summarise().UnconnectedEntities);
    }

    [Fact]
    public void Root_causes_are_entities_with_no_cause_beneath_them_and_something_above()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Late deliveries");
        var middle = tree.AddCause(effect.Id, "Orders wait in a queue");
        var rootA = tree.AddCause(middle.Id, "Work is released without regard to capacity");
        var rootB = tree.AddCause(effect.Id, "Carrier delays");
        tree.AddEntity("Unrelated observation");   // unconnected, so reported there and not as a root cause

        Assert.Equal([rootA, rootB], tree.Summarise().RootCauses);
    }

    [Fact]
    public void A_tagged_Core_problem_with_causes_beneath_it_is_not_a_root_cause()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Late deliveries");
        var core = tree.AddCause(effect.Id, "Work is released without regard to capacity");
        var deeper = tree.AddCause(core.Id, "Managers are measured on utilisation");
        tree.SetTag(core.Id, EntityTag.CoreProblem);

        Assert.Equal([deeper], tree.Summarise().RootCauses);
    }

    [Fact]
    public void The_summary_follows_edits_and_never_blocks_them()
    {
        var tree = new Tree();
        var ude = tree.AddEntity("Late deliveries");
        tree.SetTag(ude.Id, EntityTag.Ude);
        Assert.Single(tree.Summarise().UdesWithoutCoreProblem);

        var core = tree.AddCause(ude.Id, "Work is released without regard to capacity");
        tree.SetTag(core.Id, EntityTag.CoreProblem);
        Assert.Empty(tree.Summarise().UdesWithoutCoreProblem);

        tree.Undo();
        tree.Undo();
        Assert.Single(tree.Summarise().UdesWithoutCoreProblem);
        Assert.Equal(LinkOutcome.Linked, tree.Link(tree.AddEntity("Another cause").Id, ude.Id));   // unfinished work never blocks an edit
    }

    private static Entity Tagged(Tree tree, EntityId id, EntityTag tag)
    {
        var entity = tree.Entities.Single(e => e.Id == id);
        Assert.Equal(tag, entity.Tag);
        return entity;
    }
}
