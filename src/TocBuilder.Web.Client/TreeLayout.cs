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
    public const double NodeWidth = 150, NodeHeight = 44, Margin = 20;

    public sealed record Result(IReadOnlyDictionary<EntityId, (double X, double Y)> Positions, double Width, double Height);

    public static Result Compute(Tree tree)
    {
        if (tree.Entities.Count == 0)
            return new Result(new Dictionary<EntityId, (double, double)>(), 0, 0);

        var graph = new GeometryGraph();
        var nodes = new Dictionary<EntityId, Node>();
        foreach (var entity in tree.Entities)
        {
            var node = new Node(CurveFactory.CreateRectangle(NodeWidth, NodeHeight, new Point()), entity.Id);
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
            positions[id] = (node.Center.X - box.Left + Margin - NodeWidth / 2,
                             box.Top - node.Center.Y + Margin - NodeHeight / 2);   // MSAGL is y-up, SVG is y-down

        return new Result(positions, box.Width + 2 * Margin, box.Height + 2 * Margin);
    }
}
