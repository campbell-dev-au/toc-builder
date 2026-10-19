using TocBuilder.Web.Client.Components;

namespace TocBuilder.Domain.Tests;

public class EntityLabelTests
{
    [Fact]
    public void An_entity_with_a_summary_is_listed_as_its_code_and_summary()
    {
        var tree = new Tree();
        var entity = tree.AddEntity("Staff eat a lot of ultra-processed food");
        tree.SetSummary(entity.Id, "ultra-processed food intake");

        var listed = tree.Entities.Single();

        Assert.Equal("E1: ultra-processed food intake", EntityText.Label(listed));
    }

    [Fact]
    public void An_entity_without_a_summary_is_listed_as_its_code_and_statement()
    {
        var tree = new Tree();
        tree.AddEntity("Sales are down");

        Assert.Equal("E1: Sales are down", EntityText.Label(tree.Entities.Single()));
    }
}
