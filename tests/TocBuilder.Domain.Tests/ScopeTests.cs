namespace TocBuilder.Domain.Tests;

public class ScopeTests
{
    [Fact]
    public void A_new_tree_has_an_empty_scope()
    {
        Assert.Equal("", new Tree().Scope);
    }

    [Fact]
    public void The_scope_can_be_set_and_cleared_and_is_trimmed()
    {
        var tree = new Tree();

        tree.SetScope("  A job shop that builds custom furniture  ");
        Assert.Equal("A job shop that builds custom furniture", tree.Scope);

        tree.SetScope("   ");
        Assert.Equal("", tree.Scope);
    }

    [Fact]
    public void Changing_the_scope_is_undoable_and_an_unchanged_scope_leaves_no_step()
    {
        var tree = new Tree();
        tree.SetScope("Furniture");
        tree.SetScope("Furniture");

        Assert.True(tree.Undo());
        Assert.Equal("", tree.Scope);
        Assert.False(tree.CanUndo);

        tree.Redo();
        Assert.Equal("Furniture", tree.Scope);
    }

    [Fact]
    public void The_scope_is_saved_in_the_JSON_and_read_back()
    {
        var tree = new Tree();
        tree.AddEntity("Late deliveries");
        tree.SetScope("Furniture");

        var json = TreeSerializer.Export("T", tree);
        var result = TreeSerializer.Import(json);

        Assert.Contains("\"scope\": \"Furniture\"", json);
        Assert.Equal("Furniture", result.Tree!.Scope);
    }

    [Fact]
    public void A_file_saved_before_scopes_existed_imports_with_an_empty_scope()
    {
        var json = """{"version":1,"name":"Old","entities":[],"links":[],"junctions":[],"reservations":[],"nextId":1,"nextJunctionId":1,"nextReservationId":1}""";

        var result = TreeSerializer.Import(json);

        Assert.Equal(ImportOutcome.Imported, result.Outcome);
        Assert.Equal("", result.Tree!.Scope);
    }
}
