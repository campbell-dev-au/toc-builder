namespace TocBuilder.Domain.Tests;

public class TaggingTests
{
    [Fact]
    public void A_new_entity_has_no_tag()
    {
        var tree = new Tree();

        Assert.Equal(EntityTag.None, tree.AddEntity("Late deliveries").Tag);
    }

    [Fact]
    public void An_entity_can_be_tagged_as_a_UDE_or_the_Core_problem_and_the_tag_cleared()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Late deliveries");

        tree.SetTag(entity.Id, EntityTag.Ude);
        Assert.Equal(EntityTag.Ude, tree.Entities.Single().Tag);

        tree.SetTag(entity.Id, EntityTag.CoreProblem);
        Assert.Equal(EntityTag.CoreProblem, tree.Entities.Single().Tag);

        tree.SetTag(entity.Id, EntityTag.None);
        Assert.Equal(EntityTag.None, tree.Entities.Single().Tag);
    }

    [Fact]
    public void Tagging_an_unknown_entity_is_refused()
    {
        var tree = new Tree();

        Assert.Throws<ArgumentException>(() => tree.SetTag(new EntityId(999), EntityTag.Ude));
    }

    [Fact]
    public void A_Core_problem_may_have_causes_beneath_it()
    {
        var tree = new Tree();
        var core = tree.AddEntity("Work is released without regard to capacity");
        tree.SetTag(core.Id, EntityTag.CoreProblem);

        var deeper = tree.AddCause(core.Id, "Managers are measured on utilisation");

        Assert.Equal(EntityTag.CoreProblem, tree.Entities.Single(e => e.Id == core.Id).Tag);
        Assert.Equal([new CausalLink(deeper.Id, core.Id)], tree.Links);
    }

    [Fact]
    public void Editing_a_statement_keeps_the_tag()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Late deliveries");
        tree.SetTag(entity.Id, EntityTag.Ude);

        tree.EditStatement(entity.Id, "Deliveries arrive late");

        Assert.Equal(EntityTag.Ude, tree.Entities.Single().Tag);
    }

    [Fact]
    public void Tag_changes_are_undoable_and_redoable()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Late deliveries");

        tree.SetTag(entity.Id, EntityTag.Ude);
        tree.Undo();
        Assert.Equal(EntityTag.None, tree.Entities.Single().Tag);

        tree.Redo();
        Assert.Equal(EntityTag.Ude, tree.Entities.Single().Tag);
    }

    [Fact]
    public void Setting_the_tag_an_entity_already_has_records_no_undo_step()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Late deliveries");
        tree.Undo();
        tree.Redo();

        tree.SetTag(entity.Id, EntityTag.None);

        Assert.True(tree.CanUndo);   // still the add, nothing more
        Assert.False(tree.CanRedo);
        tree.Undo();
        Assert.Empty(tree.Entities);
    }
}
