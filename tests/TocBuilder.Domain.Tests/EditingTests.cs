namespace TocBuilder.Domain.Tests;

public class EditingTests
{
    [Fact]
    public void Editing_a_statement_changes_it_and_keeps_the_entity_and_its_links()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Late deliveries");
        var cause = tree.AddCause(effect.Id, "Queue");

        tree.EditStatement(cause.Id, "Orders wait in a queue at the bottleneck");

        Assert.Equal(
            [effect, new Entity(cause.Id, cause.Code, "Orders wait in a queue at the bottleneck")],
            tree.Entities);
        Assert.Equal([new CausalLink(cause.Id, effect.Id)], tree.Links);
    }

    [Fact]
    public void A_blank_statement_is_refused_and_the_old_one_is_kept()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Orders wait in a queue");

        Assert.Throws<ArgumentException>(() => tree.EditStatement(entity.Id, "   "));

        Assert.Equal([entity], tree.Entities);
    }

    [Fact]
    public void Editing_an_unknown_entity_is_refused()
    {
        var tree = new Tree();

        Assert.Throws<ArgumentException>(() => tree.EditStatement(new EntityId(999), "Anything"));
    }
}
