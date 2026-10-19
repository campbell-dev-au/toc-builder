namespace TocBuilder.Domain.Tests;

public class SummaryTests
{
    [Fact]
    public void A_new_entity_has_no_summary()
    {
        var tree = new Tree();

        var entity = tree.AddEntity("Orders ship late");

        Assert.Null(entity.Summary);
    }

    [Fact]
    public void A_summary_is_trimmed_when_set()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");

        tree.SetSummary(entity.Id, "  Late shipping  ");

        Assert.Equal("Late shipping", tree.Entities.Single().Summary);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_summary_means_none(string blank)
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");
        tree.SetSummary(entity.Id, "Late shipping");

        tree.SetSummary(entity.Id, blank);

        Assert.Null(tree.Entities.Single().Summary);
    }

    [Fact]
    public void A_summary_of_exactly_forty_characters_is_accepted()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");
        var forty = new string('a', 40);

        tree.SetSummary(entity.Id, forty);

        Assert.Equal(forty, tree.Entities.Single().Summary);
    }

    [Fact]
    public void A_summary_over_forty_characters_is_refused_and_leaves_the_tree_alone()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");
        tree.SetSummary(entity.Id, "Late shipping");

        Assert.Throws<ArgumentException>(() => tree.SetSummary(entity.Id, new string('a', 41)));

        Assert.Equal("Late shipping", tree.Entities.Single().Summary);
    }

    [Fact]
    public void The_forty_character_cap_applies_after_trimming()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");
        var forty = new string('a', 40);

        tree.SetSummary(entity.Id, $"  {forty}  ");

        Assert.Equal(forty, tree.Entities.Single().Summary);
    }

    [Fact]
    public void Editing_the_statement_leaves_the_summary_untouched()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");
        tree.SetSummary(entity.Id, "Late shipping");

        tree.EditStatement(entity.Id, "Orders leave the warehouse after the cut-off");

        Assert.Equal("Late shipping", tree.Entities.Single().Summary);
    }

    [Fact]
    public void Setting_a_summary_can_be_undone_and_redone()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");

        tree.SetSummary(entity.Id, "Late shipping");
        tree.Undo();
        Assert.Null(tree.Entities.Single().Summary);

        tree.Redo();
        Assert.Equal("Late shipping", tree.Entities.Single().Summary);
    }

    [Fact]
    public void Clearing_a_summary_can_be_undone()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");
        tree.SetSummary(entity.Id, "Late shipping");

        tree.SetSummary(entity.Id, "");
        tree.Undo();

        Assert.Equal("Late shipping", tree.Entities.Single().Summary);
    }

    [Fact]
    public void Setting_the_same_summary_again_adds_no_undo_step()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders ship late");
        tree.SetSummary(entity.Id, "Late shipping");

        tree.SetSummary(entity.Id, " Late shipping ");
        tree.Undo();

        Assert.Null(tree.Entities.Single().Summary);
    }
}
