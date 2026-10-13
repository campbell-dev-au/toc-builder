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

    public sealed record Result(IReadOnlyDictionary<EntityId, (double X, double Y)> Positions, double Width, double Height)
    {
        /// <summary>SVG path from the top of the cause to the bottom of the effect, as an S-curve that follows dragged entities.</summary>
        public string LinkPath(CausalLink link)
        {
            var (causeX, causeY) = Positions[link.Cause];
            var (effectX, effectY) = Positions[link.Effect];
            double x1 = causeX + EntityWidth / 2, y1 = causeY;
            double x2 = effectX + EntityWidth / 2, y2 = effectY + EntityHeight;
            var mid = (y1 + y2) / 2;
            return $"M{x1},{y1} C{x1},{mid} {x2},{mid} {x2},{y2}";
        }

        /// <summary>An arc across the member links just below their effect, with the point to label it at. Points sit on each link's curve, so the arc follows dragged entities.</summary>
        public (string Path, double LabelX, double LabelY) JunctionArc(AndJunction junction)
        {
            const double t = 0.22;   // how far along each link, measured from the effect
            var (effectX, effectY) = Positions[junction.Effect];
            double x2 = effectX + EntityWidth / 2, y2 = effectY + EntityHeight;
            var points = junction.Causes.Select(cause =>
            {
                var (causeX, causeY) = Positions[cause];
                double x1 = causeX + EntityWidth / 2, y1 = causeY, mid = (y1 + y2) / 2, u = 1 - t;
                // Cubic Bezier of LinkPath evaluated t of the way back from the effect.
                var x = t * t * t * x1 + 3 * t * t * u * x1 + 3 * t * u * u * x2 + u * u * u * x2;
                var y = t * t * t * y1 + 3 * t * t * u * mid + 3 * t * u * u * mid + u * u * u * y2;
                return (X: x, Y: y);
            }).OrderBy(p => p.X).ToArray();
            var path = "M" + string.Join(" L", points.Select(p => $"{p.X},{p.Y}"));
            return (path, points.Average(p => p.X), points.Min(p => p.Y) - 4);
        }
    }

    public static Result Compute(Tree tree)
    {
        if (tree.Entities.Count == 0)
            return new Result(new Dictionary<EntityId, (double, double)>(), 0, 0);

        var graph = new GeometryGraph();
        var nodes = new Dictionary<EntityId, Node>();
        foreach (var entity in tree.Entities)
        {
            var node = new Node(CurveFactory.CreateRectangle(EntityWidth, EntityHeight, new Point()), entity.Id);
            nodes[entity.Id] = node;
            graph.Nodes.Add(node);
        }
        // Edges run effect -> cause so effects land above their causes.
        foreach (var link in tree.Links)
            graph.Edges.Add(new Edge(nodes[link.Effect], nodes[link.Cause]));

        LayoutHelpers.CalculateLayout(graph, new SugiyamaLayoutSettings { NodeSeparation = 30, LayerSeparation = 60 }, null);

        var box = graph.BoundingBox;
        var positions = new Dictionary<EntityId, (double X, double Y)>();
        foreach (var (id, node) in nodes)
            positions[id] = (node.Center.X - box.Left + Margin - EntityWidth / 2,
                             box.Top - node.Center.Y + Margin - EntityHeight / 2);   // MSAGL is y-up, SVG is y-down

        return new Result(positions, box.Width + 2 * Margin, box.Height + 2 * Margin);
    }
}
