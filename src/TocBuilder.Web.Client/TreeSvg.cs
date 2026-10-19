using System.Globalization;
using System.Security;
using System.Text;
using TocBuilder.Domain;

namespace TocBuilder.Web.Client;

/// <summary>The tree as a standalone SVG picture, framed on the whole tree whatever the pan and zoom.</summary>
public static class TreeSvg
{
    public const double LabelHeight = 16;   // room above an entity for its tag
    public const string JunctionColour = "#e65100";
    private const int MaxDisplayedStatement = 22;

    public static string Render(Tree tree, TreeLayout.Result placement)
    {
        var xs = placement.Positions.Values.Select(p => p.X).ToList();
        var ys = placement.Positions.Values.Select(p => p.Y).ToList();
        var left = xs.Min() - TreeLayout.Margin;
        var top = ys.Min() - LabelHeight - TreeLayout.Margin;
        var width = xs.Max() + TreeLayout.EntityWidth + TreeLayout.Margin - left;
        var height = ys.Max() + TreeLayout.EntityHeight + TreeLayout.Margin - top;

        var svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Num(width)}\" height=\"{Num(height)}\" viewBox=\"{Num(left)} {Num(top)} {Num(width)} {Num(height)}\">");
        svg.Append("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M0,0 L10,5 L0,10 z\" fill=\"#555\" /></marker></defs>");
        foreach (var link in tree.Links)
            svg.Append($"<path d=\"{placement.LinkPath(link)}\" fill=\"none\" stroke=\"#555\" stroke-width=\"1.5\" marker-end=\"url(#arrow)\" />");
        foreach (var junction in tree.Junctions)
        {
            var (arc, labelX, labelY) = placement.JunctionArc(junction);
            svg.Append($"<path d=\"{arc}\" fill=\"none\" stroke=\"{JunctionColour}\" stroke-width=\"4\" stroke-linecap=\"round\" />");
            svg.Append($"<text x=\"{Num(labelX)}\" y=\"{Num(labelY)}\" text-anchor=\"middle\" font-size=\"10\" font-weight=\"bold\" font-family=\"sans-serif\" fill=\"{JunctionColour}\">AND</text>");
        }
        foreach (var entity in tree.Entities)
        {
            var (x, y) = placement.Positions[entity.Id];
            svg.Append($"<g><title>{SecurityElement.Escape(entity.Statement)}</title>");
            svg.Append($"<rect x=\"{Num(x)}\" y=\"{Num(y)}\" width=\"{Num(TreeLayout.EntityWidth)}\" height=\"{Num(TreeLayout.EntityHeight)}\" rx=\"6\" fill=\"#f4f6fa\" stroke=\"#888\" stroke-width=\"1.2\" />");
            if (entity.Tag != EntityTag.None)
                svg.Append($"<text x=\"{Num(x + 6)}\" y=\"{Num(y - 4)}\" font-size=\"11\" font-weight=\"bold\" font-family=\"sans-serif\" fill=\"{(entity.Tag == EntityTag.Ude ? "#b71c1c" : "#1b5e20")}\">{TagLabel(entity.Tag)}</text>");
            svg.Append($"<text x=\"{Num(x + TreeLayout.EntityWidth / 2)}\" y=\"{Num(y + TreeLayout.EntityHeight / 2 + 4)}\" text-anchor=\"middle\" font-size=\"12\" font-family=\"sans-serif\">{SecurityElement.Escape(TruncateForDisplay(entity.Summary ?? entity.Statement))}</text></g>");
        }
        svg.Append("</svg>");
        return svg.ToString();
    }

    public static string TagLabel(EntityTag tag) => tag switch { EntityTag.Ude => "UDE", EntityTag.CoreProblem => "CORE PROBLEM", _ => "" };

    /// <summary>What an Entity shows on the canvas, on one or two lines: its Summary, or its Code and the opening words of its statement when it has none.</summary>
    public static IReadOnlyList<string> DisplayLines(Entity entity)
    {
        var text = entity.Summary ?? $"{entity.Code} {entity.Statement}";
        if (text.Length <= MaxDisplayedStatement) return [text];
        var cut = text.LastIndexOf(' ', MaxDisplayedStatement);
        if (cut <= 0) cut = MaxDisplayedStatement;
        return [text[..cut], TruncateForDisplay(text[cut..].TrimStart())];
    }

    public static string TruncateForDisplay(string text) =>
        text.Length > MaxDisplayedStatement ? text[..(MaxDisplayedStatement - 1)] + "…" : text;

    // SVG attributes need invariant numbers whatever the machine's culture.
    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);
}
