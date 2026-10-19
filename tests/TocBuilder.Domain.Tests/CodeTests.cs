using System.Text.Json.Nodes;

namespace TocBuilder.Domain.Tests;

public class CodeTests
{
    [Fact]
    public void New_entities_get_sequential_codes_however_they_are_added()
    {
        var tree = new Tree();
        var first = tree.AddEntity("Orders ship late");
        var cause = tree.AddCause(first.Id, "Picking is slow");
        var effect = tree.AddEffect(first.Id, "Customers complain");
        var other = tree.AddCause(first.Id, "Carrier is delayed");
        var junction = tree.Group(first.Id, [cause.Id, other.Id]).Junction!;
        var coCause = tree.AddCoCause(junction.Id, "Stock is short");

        Assert.Equal(["E1", "E2", "E3", "E4", "E5"], new[] { first, cause, effect, other, coCause }.Select(e => e.Code));
        Assert.Equal(["E1", "E2", "E3", "E4", "E5"], tree.Entities.Select(e => e.Code));
    }

    [Fact]
    public void A_code_is_not_reused_after_a_delete()
    {
        var tree = new Tree();
        tree.AddEntity("Orders ship late");
        var last = tree.AddEntity("Picking is slow");

        tree.Delete(last.Id);
        var next = tree.AddEntity("Carrier is delayed");

        Assert.Equal("E3", next.Code);
    }

    [Fact]
    public void Undoing_a_delete_brings_the_entity_back_with_its_code_and_the_next_code_carries_on()
    {
        var tree = new Tree();
        var first = tree.AddEntity("Orders ship late");
        var second = tree.AddEntity("Picking is slow");
        tree.Delete(first.Id);

        tree.Undo();
        var next = tree.AddEntity("Carrier is delayed");

        Assert.Equal([first.Code, second.Code, "E3"], tree.Entities.Select(e => e.Code));
        Assert.Equal("E3", next.Code);
    }

    [Fact]
    public void Redoing_an_add_after_a_delete_keeps_its_code_and_later_entities_never_take_it()
    {
        var tree = new Tree();
        tree.AddEntity("Orders ship late");
        var second = tree.AddEntity("Picking is slow");
        tree.Delete(second.Id);
        var added = tree.AddEntity("Carrier is delayed");

        tree.Undo();
        tree.Redo();
        var next = tree.AddEntity("Stock is short");

        Assert.Equal("E3", added.Code);
        Assert.Equal(["E1", "E3", "E4"], tree.Entities.Select(e => e.Code));
        Assert.Equal("E4", next.Code);
    }

    [Fact]
    public void Codes_and_the_counter_survive_an_export_and_import()
    {
        var tree = new Tree();
        tree.AddEntity("Orders ship late");
        var gone = tree.AddEntity("Picking is slow");
        tree.AddEntity("Carrier is delayed");
        tree.Delete(gone.Id);

        var imported = TreeSerializer.Import(TreeSerializer.Export("T", tree)).Tree!;
        var next = imported.AddEntity("Stock is short");

        Assert.Equal(["E1", "E3"], tree.Entities.Select(e => e.Code));
        Assert.Equal(["E1", "E3", "E4"], imported.Entities.Select(e => e.Code));
        Assert.Equal("E4", next.Code);
    }

    [Fact]
    public void A_file_saved_before_codes_existed_gets_codes_in_storage_order()
    {
        var tree = new Tree();
        tree.AddEntity("Orders ship late");
        tree.AddEntity("Picking is slow");
        tree.AddEntity("Carrier is delayed");
        var old = JsonNode.Parse(TreeSerializer.Export("Old", tree))!.AsObject();
        old["version"] = 1;
        old.Remove("nextCode");
        foreach (var entity in old["entities"]!.AsArray()) entity!.AsObject().Remove("code");

        var result = TreeSerializer.Import(old.ToJsonString());

        Assert.Equal(ImportOutcome.Imported, result.Outcome);
        Assert.Equal(["E1", "E2", "E3"], result.Tree!.Entities.Select(e => e.Code));
        Assert.Equal("E4", result.Tree.AddEntity("Stock is short").Code);
    }

    [Fact]
    public void A_file_with_a_newer_version_is_still_refused_without_a_tree()
    {
        var newer = JsonNode.Parse(TreeSerializer.Export("T", new Tree()))!.AsObject();
        newer["version"] = TreeSerializer.CurrentVersion + 1;

        var result = TreeSerializer.Import(newer.ToJsonString());

        Assert.Equal(ImportOutcome.NewerVersion, result.Outcome);
        Assert.Null(result.Tree);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("counter behind")]
    public void A_file_whose_codes_do_not_hold_together_is_refused_as_corrupt(string fault)
    {
        var tree = new Tree();
        tree.AddEntity("Orders ship late");
        tree.AddEntity("Picking is slow");
        var file = JsonNode.Parse(TreeSerializer.Export("T", tree))!.AsObject();
        var entities = file["entities"]!.AsArray();
        switch (fault)
        {
            case "duplicate": entities[1]!["code"] = "E1"; break;
            case "missing": entities[1]!.AsObject().Remove("code"); break;
            case "malformed": entities[1]!["code"] = "Q2"; break;
            case "counter behind": file["nextCode"] = 2; break;
        }

        var result = TreeSerializer.Import(file.ToJsonString());

        Assert.Equal(ImportOutcome.Corrupt, result.Outcome);
        Assert.Null(result.Tree);
    }
}
