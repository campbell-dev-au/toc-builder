namespace TocBuilder.Domain.Tests;

public class TreeSerializerTests
{
    private static Tree RichTree()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Orders ship late");
        var a = tree.AddCause(effect.Id, "Picking is slow");
        var b = tree.AddCause(effect.Id, "Carrier is delayed");
        var c = tree.AddCause(a.Id, "Warehouse layout is poor");
        tree.SetTag(effect.Id, EntityTag.Ude);
        tree.SetTag(c.Id, EntityTag.CoreProblem);
        var junction = tree.Group(effect.Id, [a.Id, b.Id]).Junction!;
        var onLink = tree.RaiseReservation(new OnLink(new CausalLink(c.Id, a.Id)), ClrCategory.Clarity).Reservation!;
        tree.SetReservationNote(onLink.Id, "What does poor mean?");
        tree.SetReservationState(onLink.Id, ReservationState.Resolved);
        tree.RaiseReservation(new OnJunction(junction.Id), ClrCategory.AdditionalCause);
        tree.RaiseReservation(new OnEntity(b.Id), ClrCategory.EntityExistence);
        return tree;
    }

    [Fact]
    public void A_tree_exported_and_imported_comes_back_identical_with_its_name()
    {
        var original = RichTree();

        var result = TreeSerializer.Import(TreeSerializer.Export("Shipping CRT", original));

        Assert.Equal(ImportOutcome.Imported, result.Outcome);
        Assert.Equal("Shipping CRT", result.Name);
        var imported = result.Tree!;
        Assert.Equal(original.Entities, imported.Entities);
        Assert.Equal(original.Links, imported.Links);
        Assert.Equal(original.Reservations, imported.Reservations);
        Assert.Equal(original.Junctions.Select(j => (j.Id, j.Effect, j.Causes.ToArray())), imported.Junctions.Select(j => (j.Id, j.Effect, j.Causes.ToArray())));
    }

    [Fact]
    public void Nudges_come_back_after_an_export_and_import()
    {
        var tree = RichTree();
        var moved = tree.Entities[0].Id;
        var nudges = new Dictionary<EntityId, Nudge> { [moved] = new Nudge(12.5, -40) };

        var result = TreeSerializer.Import(TreeSerializer.Export("T", tree, nudges));

        Assert.Equal(ImportOutcome.Imported, result.Outcome);
        Assert.Equal(nudges, result.Nudges);
    }

    [Fact]
    public void A_file_saved_before_nudges_existed_imports_with_none()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(TreeSerializer.Export("Old", RichTree()))!.AsObject();
        json.Remove("nudges");

        var result = TreeSerializer.Import(json.ToJsonString());

        Assert.Equal(ImportOutcome.Imported, result.Outcome);
        Assert.Empty(result.Nudges!);
    }

    [Fact]
    public void The_view_comes_back_after_an_export_and_import()
    {
        var result = TreeSerializer.Import(TreeSerializer.Export("T", RichTree(), view: ViewMode.Text));

        Assert.Equal(ImportOutcome.Imported, result.Outcome);
        Assert.Equal(ViewMode.Text, result.View);
    }

    [Fact]
    public void A_file_saved_before_views_existed_imports_in_Summary_view()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(TreeSerializer.Export("Old", RichTree(), view: ViewMode.Text))!.AsObject();
        json.Remove("view");

        var result = TreeSerializer.Import(json.ToJsonString());

        Assert.Equal(ImportOutcome.Imported, result.Outcome);
        Assert.Equal(ViewMode.Summary, result.View);
    }

    [Fact]
    public void A_file_with_an_unknown_view_is_reported_corrupt()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(TreeSerializer.Export("T", RichTree()))!.AsObject();
        json["view"] = "Poster";

        Assert.Equal(ImportOutcome.Corrupt, TreeSerializer.Import(json.ToJsonString()).Outcome);
    }

    [Fact]
    public void A_file_with_a_null_nudge_is_reported_corrupt_not_a_crash()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(TreeSerializer.Export("T", RichTree()))!.AsObject();
        json["nudges"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);

        var result = TreeSerializer.Import(json.ToJsonString());

        Assert.Equal(ImportOutcome.Corrupt, result.Outcome);
    }

    [Fact]
    public void Nudges_for_entities_not_in_the_file_are_dropped()
    {
        var tree = RichTree();
        var kept = tree.Entities[0].Id;
        var nudges = new Dictionary<EntityId, Nudge> { [kept] = new Nudge(1, 2), [new EntityId(999)] = new Nudge(3, 4) };

        var result = TreeSerializer.Import(TreeSerializer.Export("T", tree, nudges));

        Assert.Equal(new Dictionary<EntityId, Nudge> { [kept] = new Nudge(1, 2) }, result.Nudges);
    }

    [Fact]
    public void The_file_carries_a_schema_version()
    {
        var json = TreeSerializer.Export("Any", new Tree());

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void Ids_of_deleted_entities_are_not_reused_after_an_import()
    {
        var tree = new Tree();
        tree.AddEntity("Keep");
        var gone = tree.AddEntity("Delete me");
        tree.Delete(gone.Id);

        var imported = TreeSerializer.Import(TreeSerializer.Export("T", tree)).Tree!;

        Assert.NotEqual(gone.Id, imported.AddEntity("New").Id);
    }

    [Fact]
    public void A_file_from_a_newer_version_is_refused_with_a_message_naming_the_versions()
    {
        var json = TreeSerializer.Export("T", RichTree()).Replace($"\"version\": {TreeSerializer.CurrentVersion}", "\"version\": 99");

        var result = TreeSerializer.Import(json);

        Assert.Equal(ImportOutcome.NewerVersion, result.Outcome);
        Assert.Null(result.Tree);
        Assert.Contains("99", result.Message);
        Assert.Contains(TreeSerializer.CurrentVersion.ToString(), result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{}")]
    [InlineData("{\"version\": 1}")]
    [InlineData("{\"version\": \"one\"}")]
    public void A_corrupt_file_is_refused_with_a_message(string json)
    {
        var result = TreeSerializer.Import(json);

        Assert.Equal(ImportOutcome.Corrupt, result.Outcome);
        Assert.Null(result.Tree);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void A_file_whose_links_point_at_missing_entities_is_refused_as_corrupt()
    {
        var json = TreeSerializer.Export("T", RichTree()).Replace("\"cause\": 4", "\"cause\": 400");

        var result = TreeSerializer.Import(json);

        Assert.Equal(ImportOutcome.Corrupt, result.Outcome);
    }

    [Theory]
    [InlineData("\"entities\": [", "\"entities\": [null,")]
    [InlineData("\"tag\": \"Ude\"", "\"tag\": 99")]
    [InlineData("\"name\": \"T\"", "\"name\": \"  \"")]
    [InlineData("\"category\": \"Clarity\"", "\"category\": \"Nonsense\"")]
    public void A_file_with_a_malformed_value_is_refused_as_corrupt(string find, string replace)
    {
        var json = TreeSerializer.Export("T", RichTree());
        Assert.Contains(find, json);

        var result = TreeSerializer.Import(json.Replace(find, replace));

        Assert.Equal(ImportOutcome.Corrupt, result.Outcome);
        Assert.Null(result.Tree);
    }

    [Fact]
    public void A_file_whose_links_form_a_cycle_a_duplicate_or_a_self_link_is_refused_as_corrupt()
    {
        var tree = new Tree();
        var a = tree.AddEntity("A");
        var b = tree.AddCause(a.Id, "B");
        var json = TreeSerializer.Export("T", tree);
        var link = "{\n      \"cause\": 2,\n      \"effect\": 1\n    }";
        Assert.Contains(link, json);

        var cycle = json.Replace(link, link + ", " + link.Replace("\"cause\": 2", "\"cause\": 1").Replace("\"effect\": 1", "\"effect\": 2"));
        var duplicate = json.Replace(link, link + ", " + link);
        var self = json.Replace(link, link.Replace("\"cause\": 2", "\"cause\": 1"));

        Assert.All([cycle, duplicate, self], bad => Assert.Equal(ImportOutcome.Corrupt, TreeSerializer.Import(bad).Outcome));
    }

    [Fact]
    public void A_file_with_a_junction_that_breaks_the_junction_rules_is_refused_as_corrupt()
    {
        var json = TreeSerializer.Export("T", RichTree());
        Assert.Contains("\"causes\": [", json);

        var underSized = System.Text.RegularExpressions.Regex.Replace(json, "\"causes\": \\[[^\\]]*\\]", "\"causes\": [2]");

        Assert.Equal(ImportOutcome.Corrupt, TreeSerializer.Import(underSized).Outcome);
    }
}
