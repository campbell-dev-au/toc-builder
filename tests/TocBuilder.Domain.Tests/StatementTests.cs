namespace TocBuilder.Domain.Tests;

/// <summary>A statement is what makes an Entity meaningful, so blank ones are refused everywhere and the rest are trimmed.</summary>
public class StatementTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_statement_is_refused_when_adding_an_entity(string blank)
    {
        var tree = new Tree();

        Assert.Throws<ArgumentException>(() => tree.AddEntity(blank));

        Assert.Empty(tree.Entities);
    }

    [Fact]
    public void A_blank_statement_is_refused_when_adding_a_cause_and_nothing_is_created()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Customers receive late deliveries");

        Assert.Throws<ArgumentException>(() => tree.AddCause(effect.Id, " "));

        Assert.Equal([effect], tree.Entities);
        Assert.Empty(tree.Links);
    }

    [Fact]
    public void A_blank_statement_is_refused_when_adding_an_effect_and_nothing_is_created()
    {
        var tree = new Tree();
        var cause = tree.AddEntity("Orders wait in a queue");

        Assert.Throws<ArgumentException>(() => tree.AddEffect(cause.Id, ""));

        Assert.Equal([cause], tree.Entities);
        Assert.Empty(tree.Links);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_from_every_statement()
    {
        var tree = new Tree();
        var added = tree.AddEntity("  Customers receive late deliveries ");
        var cause = tree.AddCause(added.Id, "\tOrders wait in a queue\n");
        var effect = tree.AddEffect(added.Id, " Revenue falls ");
        tree.EditStatement(added.Id, "   Deliveries are late  ");

        Assert.Equal(
            ["Deliveries are late", "Orders wait in a queue", "Revenue falls"],
            tree.Entities.Select(e => e.Statement));
    }
}
