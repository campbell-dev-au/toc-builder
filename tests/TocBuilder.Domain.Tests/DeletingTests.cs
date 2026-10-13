namespace TocBuilder.Domain.Tests;

public class DeletingTests
{
    [Fact]
    public void Deleting_an_entity_removes_it_and_every_link_to_or_from_it()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Customers receive late deliveries");
        var middle = tree.AddCause(effect.Id, "Orders wait in a queue");
        var rootCause = tree.AddCause(middle.Id, "Work is released without regard to capacity");
        var unrelated = tree.AddEntity("Unrelated observation");

        tree.Delete(middle.Id);

        Assert.Equal([effect, rootCause, unrelated], tree.Entities);
        Assert.Empty(tree.Links);   // both the link into the effect and the link from the root cause went with it
    }

    [Fact]
    public void Deleting_an_entity_leaves_links_between_other_entities_alone()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Customers receive late deliveries");
        var kept = tree.AddCause(effect.Id, "Orders wait in a queue");
        var removed = tree.AddCause(effect.Id, "Carrier delays");

        tree.Delete(removed.Id);

        Assert.Equal([new CausalLink(kept.Id, effect.Id)], tree.Links);
    }

    [Fact]
    public void Deleting_an_unknown_entity_is_refused()
    {
        var tree = new Tree();
        var known = tree.AddEntity("Orders wait in a queue");

        Assert.Throws<ArgumentException>(() => tree.Delete(new EntityId(999)));

        Assert.Equal([known], tree.Entities);
    }
}
