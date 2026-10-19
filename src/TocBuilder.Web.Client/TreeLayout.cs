using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using TocBuilder.Domain;

namespace TocBuilder.Web.Client;

/// <summary>Adapter from the domain tree to a layered layout, effects above causes.</summary>
public static class TreeLayout
{
    public const double EntityWidth = 150, EntityHeight = 44, Margin = 20;
    public const double TextWidth = 220, TextLineHeight = 14, TextPadding = 16;
    public const int TextLineChars = 32;   // how many characters of 12px text fit in a Text view box

    /// <summary>The box an Entity is drawn in: a fixed size in Summary view, and in Text view as tall as its wrapped statement needs.</summary>
    public static (double Width, double Height) SizeOf(Entity entity, ViewMode view)
    {
        if (view == ViewMode.Summary) return (EntityWidth, EntityHeight);
        var lines = TreeSvg.WrapStatement(entity.Statement, TextLineChars).Count;
        return (TextWidth, Math.Max(EntityHeight, lines * TextLineHeight + TextPadding));
    }

    public sealed record Result(IReadOnlyDictionary<EntityId, (double X, double Y)> Positions, double Width, double Height, IReadOnlyDictionary<EntityId, (double Width, double Height)>? Sizes = null)
    {
        public double WidthOf(EntityId id) => Sizes is not null && Sizes.TryGetValue(id, out var s) ? s.Width : EntityWidth;
        public double HeightOf(EntityId id) => Sizes is not null && Sizes.TryGetValue(id, out var s) ? s.Height : EntityHeight;

        /// <summary>The rectangle that frames every Entity with its margin and the room above for tags: left, top, width, height.</summary>
        public (double Left, double Top, double Width, double Height) Frame()
        {
            var left = Positions.Min(p => p.Value.X) - Margin;
            var top = Positions.Min(p => p.Value.Y) - TreeSvg.LabelHeight - Margin;
            var right = Positions.Max(p => p.Value.X + WidthOf(p.Key)) + Margin;
            var bottom = Positions.Max(p => p.Value.Y + HeightOf(p.Key)) + Margin;
            return (left, top, right - left, bottom - top);
        }

        /// <summary>SVG path from the top of the cause to the bottom of the effect, as an S-curve that follows dragged entities.</summary>
        public string LinkPath(CausalLink link)
        {
            var (causeX, causeY) = Positions[link.Cause];
            var (effectX, effectY) = Positions[link.Effect];
            double x1 = causeX + WidthOf(link.Cause) / 2, y1 = causeY;
            double x2 = effectX + WidthOf(link.Effect) / 2, y2 = effectY + HeightOf(link.Effect);
            var mid = (y1 + y2) / 2;
            return $"M{x1},{y1} C{x1},{mid} {x2},{mid} {x2},{y2}";
        }

        /// <summary>An arc across the member links just below their effect, with the point to label it at. Points sit on each link's curve, so the arc follows dragged entities.</summary>
        public (string Path, double LabelX, double LabelY) JunctionArc(AndJunction junction)
        {
            const double t = 0.6;   // how far along each link, measured from the effect
            var (effectX, effectY) = Positions[junction.Effect];
            double x2 = effectX + WidthOf(junction.Effect) / 2, y2 = effectY + HeightOf(junction.Effect);
            var points = junction.Causes.Select(cause =>
            {
                var (causeX, causeY) = Positions[cause];
                double x1 = causeX + WidthOf(cause) / 2, y1 = causeY, mid = (y1 + y2) / 2, u = 1 - t;
                // Cubic Bezier of LinkPath evaluated t of the way back from the effect.
                var x = t * t * t * x1 + 3 * t * t * u * x1 + 3 * t * u * u * x2 + u * u * u * x2;
                var y = t * t * t * y1 + 3 * t * t * u * mid + 3 * t * u * u * mid + u * u * u * y2;
                return (X: x, Y: y);
            }).OrderBy(p => p.X).ToArray();
            var path = "M" + string.Join(" L", points.Select(p => $"{p.X},{p.Y}"));
            return (path, points.Average(p => p.X), points.Min(p => p.Y) - 4);
        }
    }

    public static Result Compute(Tree tree, ViewMode view = ViewMode.Summary)
    {
        if (tree.Entities.Count == 0)
            return new Result(new Dictionary<EntityId, (double, double)>(), 0, 0);

        var graph = new GeometryGraph();
        var nodes = new Dictionary<EntityId, Node>();
        foreach (var entity in tree.Entities)
        {
            var (width, height) = SizeOf(entity, view);
            var node = new Node(CurveFactory.CreateRectangle(width, height, new Point()), entity.Id);
            nodes[entity.Id] = node;
            graph.Nodes.Add(node);
        }
        // Edges run effect -> cause so effects land above their causes.
        foreach (var link in tree.Links)
            graph.Edges.Add(new Edge(nodes[link.Effect], nodes[link.Cause]));

        LayoutHelpers.CalculateLayout(graph, new SugiyamaLayoutSettings { NodeSeparation = 30, LayerSeparation = 60 }, null);

        var box = graph.BoundingBox;
        var positions = new Dictionary<EntityId, (double X, double Y)>();
        var sizes = tree.Entities.ToDictionary(e => e.Id, e => SizeOf(e, view));
        foreach (var (id, node) in nodes)
            positions[id] = (node.Center.X - box.Left + Margin - sizes[id].Width / 2,
                             box.Top - node.Center.Y + Margin - sizes[id].Height / 2);   // MSAGL is y-up, SVG is y-down

        return new Result(positions, box.Width + 2 * Margin, box.Height + 2 * Margin, sizes);
    }
}
