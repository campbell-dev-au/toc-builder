using System.Xml.Linq;
using TocBuilder.Web.Client;

namespace TocBuilder.Domain.Tests;

public class SvgExportTests
{
    private static XElement Render(Tree tree, params (EntityId Id, double X, double Y)[] positions) =>
        XDocument.Parse(TreeSvg.Render(tree, new TreeLayout.Result(
            positions.ToDictionary(p => p.Id, p => (p.X, p.Y)), 0, 0,
            tree.Entities.ToDictionary(e => e.Id, e => TreeLayout.SizeOf(e, ViewMode.Text))))).Root!;

    [Fact]
    public void The_picture_frames_the_whole_tree_with_no_pan_or_zoom_applied()
    {
        var tree = new Tree();
        var a = tree.AddEntity("Late deliveries");
        var b = tree.AddCause(a.Id, "Machines are overloaded");

        var svg = Render(tree, (a.Id, 100, 50), (b.Id, 400, 300));

        // Entities are 220x44; the frame adds a 20 margin all round and 16 above for the tag labels.
        Assert.Equal("80 14 560 350", svg.Attribute("viewBox")!.Value);
        Assert.Equal("560", svg.Attribute("width")!.Value);
        Assert.Equal("350", svg.Attribute("height")!.Value);
        Assert.DoesNotContain(svg.Descendants(), e => e.Attribute("transform") is not null);
    }

    [Fact]
    public void UDE_and_Core_problem_tags_are_visible_and_untagged_entities_have_none()
    {
        var tree = new Tree();
        var ude = tree.AddEntity("Late deliveries");
        var core = tree.AddCause(ude.Id, "Work is released without regard to capacity");
        var plain = tree.AddCause(core.Id, "Plain");
        tree.SetTag(ude.Id, EntityTag.Ude);
        tree.SetTag(core.Id, EntityTag.CoreProblem);

        var svg = Render(tree, (ude.Id, 0, 0), (core.Id, 0, 100), (plain.Id, 0, 200));

        var labels = svg.Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToList();
        Assert.Contains("UDE", labels);
        Assert.Contains("CORE PROBLEM", labels);
        Assert.Equal(2, labels.Count(l => l is "UDE" or "CORE PROBLEM"));
    }

    [Fact]
    public void Full_statements_and_codes_are_shown_wrapped_and_cannot_break_the_markup()
    {
        var tree = new Tree();
        var a = tree.AddEntity("Cost < price & \"x\"");
        var b = tree.AddCause(a.Id, "A very long statement that does not fit in the box");
        tree.SetSummary(b.Id, "Long one");

        var svg = Render(tree, (a.Id, 0, 0), (b.Id, 0, 100));

        var texts = svg.Descendants().Where(e => e.Name.LocalName == "text").ToList();
        Assert.Contains(texts, t => t.Value == "Cost < price & \"x\"");
        Assert.Contains(texts, t => string.Concat(t.Elements().Select(l => l.Value + " ")).Trim() == "A very long statement that does not fit in the box");
        Assert.DoesNotContain(texts, t => t.Value == "Long one");
        var labels = texts.Select(t => t.Value).ToList();
        Assert.Contains("E1", labels);
        Assert.Contains("E2", labels);
    }

    [Fact]
    public void Causal_links_are_drawn_as_arrows_and_AND_junctions_are_labelled()
    {
        var tree = new Tree();
        var effect = tree.AddEntity("Late deliveries");
        var c1 = tree.AddCause(effect.Id, "Overload");
        var c2 = tree.AddCause(effect.Id, "No buffer");
        tree.Group(effect.Id, [c1.Id, c2.Id]);

        var svg = Render(tree, (effect.Id, 100, 0), (c1.Id, 0, 100), (c2.Id, 200, 100));

        var arrows = svg.Descendants().Where(e => e.Name.LocalName == "path" && e.Attribute("marker-end") is not null).ToList();
        Assert.Equal(2, arrows.Count);
        // From the top of a cause (centre x + 110) to the bottom of the effect.
        Assert.StartsWith("M110,100 ", arrows[0].Attribute("d")!.Value);
        Assert.Contains(svg.Descendants(), e => e.Name.LocalName == "text" && e.Value == "AND");
        var markerId = svg.Descendants().Single(e => e.Name.LocalName == "marker").Attribute("id")!.Value;
        Assert.All(arrows, a => Assert.Equal($"url(#{markerId})", a.Attribute("marker-end")!.Value));
    }
}
