namespace TocBuilder.Domain.Tests;

public class AddingEffectsTests
{
    [Fact]
    public void Adding_an_effect_creates_an_entity_that_is_an_effect_of_the_selected_entity()
    {
        var tree = new Tree();
        var cause = tree.AddEntity("Orders wait in a queue");

        var effect = tree.AddEffect(cause.Id, "Lead time exceeds the quoted time");

        Assert.Contains(tree.Entities, e => e.Id == effect.Id && e.Statement == "Lead time exceeds the quoted time");
        Assert.Equal([new CausalLink(cause.Id, effect.Id)], tree.Links);
    }
}
