namespace TocBuilder.Domain.Tests;

public class UndoRedoTests
{
    [Fact]
    public void A_new_tree_has_nothing_to_undo_or_redo()
    {
        var tree = new Tree();

        Assert.False(tree.CanUndo);
        Assert.False(tree.CanRedo);
        Assert.False(tree.Undo());
        Assert.False(tree.Redo());
    }

    [Fact]
    public void Undoing_and_redoing_add_entity()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders wait in a queue");

        Assert.True(tree.Undo());
        Assert.Empty(tree.Entities);

        Assert.True(tree.Redo());
        Assert.Equal([entity], tree.Entities);
    }

    [Fact]
    public void Undoing_add_cause_removes_both_the_cause_and_its_link_in_one_step()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Late deliveries");
        var cause = tree.AddCause(effect.Id, "Orders wait in a queue");

        tree.Undo();
        Assert.Equal([effect], tree.Entities);
        Assert.Empty(tree.Links);

        tree.Redo();
        Assert.Equal([effect, cause], tree.Entities);
        Assert.Equal([new CausalLink(cause.Id, effect.Id)], tree.Links);
    }

    [Fact]
    public void Undoing_add_effect_removes_both_the_effect_and_its_link_in_one_step()
    {
        var tree = new Tree();
        var cause = tree.AddEntity("Orders wait in a queue");
        var effect = tree.AddEffect(cause.Id, "Late deliveries");

        tree.Undo();
        Assert.Equal([cause], tree.Entities);
        Assert.Empty(tree.Links);

        tree.Redo();
        Assert.Equal([new CausalLink(cause.Id, effect.Id)], tree.Links);
    }

    [Fact]
    public void Undoing_and_redoing_a_link()
    {
        var tree = new Tree();
        var cause = tree.AddEntity("Orders wait in a queue");
        var effect = tree.AddEntity("Late deliveries");
        tree.Link(cause.Id, effect.Id);

        tree.Undo();
        Assert.Empty(tree.Links);

        tree.Redo();
        Assert.Equal([new CausalLink(cause.Id, effect.Id)], tree.Links);
    }

    [Fact]
    public void Undoing_and_redoing_an_edit()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders wait");
        tree.EditStatement(entity.Id, "Orders wait in a queue");

        tree.Undo();
        Assert.Equal("Orders wait", Assert.Single(tree.Entities).Statement);

        tree.Redo();
        Assert.Equal("Orders wait in a queue", Assert.Single(tree.Entities).Statement);
    }

    [Fact]
    public void One_undo_fully_restores_a_deleted_entity_with_many_links()
    {
        var tree = new Tree();
        var hub = tree.AddEntity("Work is released without regard to capacity");
        var effects = Enumerable.Range(1, 3).Select(i => tree.AddEffect(hub.Id, $"Effect {i}")).ToList();
        var causes = Enumerable.Range(1, 3).Select(i => tree.AddCause(hub.Id, $"Cause {i}")).ToList();
        var entitiesBefore = tree.Entities.ToList();
        var linksBefore = tree.Links.ToList();

        tree.Delete(hub.Id);
        Assert.Empty(tree.Links);

        tree.Undo();

        Assert.Equal(entitiesBefore, tree.Entities);
        Assert.Equal(linksBefore, tree.Links);
        Assert.Equal(6, tree.Links.Count);

        tree.Redo();
        Assert.DoesNotContain(tree.Entities, e => e.Id == hub.Id);
        Assert.Empty(tree.Links);
    }

    [Fact]
    public void Undo_walks_back_through_several_edits_and_redo_walks_forward()
    {
        var tree = new Tree();
        var a = tree.AddEntity("A");
        var b = tree.AddCause(a.Id, "B");
        tree.EditStatement(b.Id, "B2");

        tree.Undo();
        tree.Undo();
        Assert.Equal([a], tree.Entities);

        tree.Redo();
        tree.Redo();
        Assert.Equal(["A", "B2"], tree.Entities.Select(e => e.Statement));
        Assert.False(tree.CanRedo);
    }

    [Fact]
    public void A_new_edit_after_an_undo_clears_the_redo_history()
    {
        var tree = new Tree();
        tree.AddEntity("A");
        tree.AddEntity("B");
        tree.Undo();
        Assert.True(tree.CanRedo);

        tree.AddEntity("C");

        Assert.False(tree.CanRedo);
        Assert.False(tree.Redo());
        Assert.Equal(["A", "C"], tree.Entities.Select(e => e.Statement));
    }

    [Fact]
    public void An_id_freed_by_undo_is_reused_by_the_next_new_entity()
    {
        var tree = new Tree();
        tree.AddEntity("A");
        var undone = tree.AddEntity("B");
        tree.Undo();

        var replacement = tree.AddEntity("C");

        Assert.Equal(undone.Id, replacement.Id);   // the undone entity is gone for good, so its id is free again
        Assert.Equal("C", tree.Entities[^1].Statement);
    }

    [Fact]
    public void Refused_commands_leave_no_undo_step()
    {
        var tree = new Tree();
        var cause = tree.AddEntity("A");
        var effect = tree.AddEffect(cause.Id, "B");

        Assert.Equal(LinkOutcome.AlreadyLinked, tree.Link(cause.Id, effect.Id));
        Assert.Equal(LinkOutcome.WouldCreateCycle, tree.Link(effect.Id, cause.Id));
        Assert.Throws<ArgumentException>(() => tree.AddEntity(" "));
        Assert.Throws<ArgumentException>(() => tree.AddCause(cause.Id, ""));
        Assert.Throws<ArgumentException>(() => tree.EditStatement(cause.Id, " "));
        Assert.Throws<ArgumentException>(() => tree.Delete(new EntityId(999)));

        tree.Undo();   // undoes the AddEffect, not any refused command
        Assert.Equal([cause], tree.Entities);
    }

    [Fact]
    public void Refused_commands_do_not_clear_the_redo_history()
    {
        var tree = new Tree();
        var a = tree.AddEntity("A");
        tree.AddEntity("B");
        tree.Undo();

        Assert.Equal(LinkOutcome.WouldCreateCycle, tree.Link(a.Id, a.Id));

        Assert.True(tree.CanRedo);
    }
}
