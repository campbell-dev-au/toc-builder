namespace TocBuilder.Domain.Tests;

/// <summary>Dangling links are impossible: an id that is not in the tree is refused and changes nothing.</summary>
public class UnknownEntityTests
{
    private static readonly EntityId Missing = new(999);

    [Fact]
    public void Linking_to_an_unknown_entity_is_refused()
    {
        var tree = new Tree();
        var known = tree.AddEntity("Orders wait in a queue");

        Assert.Throws<ArgumentException>(() => tree.Link(known.Id, Missing));
        Assert.Throws<ArgumentException>(() => tree.Link(Missing, known.Id));
        Assert.Empty(tree.Links);
    }

    [Fact]
    public void Adding_a_cause_to_an_unknown_entity_is_refused_and_creates_nothing()
    {
        var tree = new Tree();
        var known = tree.AddEntity("Orders wait in a queue");

        Assert.Throws<ArgumentException>(() => tree.AddCause(Missing, "Anything"));

        Assert.Equal([known], tree.Entities);
        Assert.Empty(tree.Links);
    }

    [Fact]
    public void Adding_an_effect_to_an_unknown_entity_is_refused_and_creates_nothing()
    {
        var tree = new Tree();
        var known = tree.AddEntity("Orders wait in a queue");

        Assert.Throws<ArgumentException>(() => tree.AddEffect(Missing, "Anything"));

        Assert.Equal([known], tree.Entities);
        Assert.Empty(tree.Links);
    }
}
