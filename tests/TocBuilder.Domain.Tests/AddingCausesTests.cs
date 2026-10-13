namespace TocBuilder.Domain.Tests;

public class AddingCausesTests
{
    [Fact]
    public void Adding_a_cause_creates_an_entity_that_is_a_cause_of_the_selected_entity()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Customers receive late deliveries");

        var cause = tree.AddCause(effect.Id, "Orders wait in a queue");

        Assert.Contains(tree.Entities, e => e.Id == cause.Id && e.Statement == "Orders wait in a queue");
        Assert.Equal([new CausalLink(cause.Id, effect.Id)], tree.Links);
    }
}
