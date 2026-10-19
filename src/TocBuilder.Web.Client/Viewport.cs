namespace TocBuilder.Web.Client;

/// <summary>The pan and zoom of the canvas: a screen point is its world point times Scale, plus the pan.</summary>
public sealed record Viewport(double Scale, double PanX, double PanY)
{
    public const double MinScale = 0.1, MaxScale = 4;

    public static Viewport Identity { get; } = new(1, 0, 0);

    /// <summary>The viewport that centres the content rectangle in the view at the largest scale that shows all of it.</summary>
    public static Viewport Fit(double contentLeft, double contentTop, double contentWidth, double contentHeight, double viewWidth, double viewHeight)
    {
        if (contentWidth <= 0 || contentHeight <= 0 || viewWidth <= 0 || viewHeight <= 0) return Identity;
        var scale = Math.Clamp(Math.Min(viewWidth / contentWidth, viewHeight / contentHeight), MinScale, MaxScale);
        return new Viewport(scale, (viewWidth - contentWidth * scale) / 2 - contentLeft * scale, (viewHeight - contentHeight * scale) / 2 - contentTop * scale);
    }

    public Viewport PanBy(double dx, double dy) => this with { PanX = PanX + dx, PanY = PanY + dy };

    /// <summary>Scales by <paramref name="factor"/> about a screen point, which keeps the world point under it in place.</summary>
    public Viewport ZoomAt(double factor, double screenX, double screenY)
    {
        var (worldX, worldY) = ToWorld(screenX, screenY);
        var scale = Math.Clamp(Scale * factor, MinScale, MaxScale);
        return new Viewport(scale, screenX - worldX * scale, screenY - worldY * scale);
    }

    public (double X, double Y) ToWorld(double screenX, double screenY) => ((screenX - PanX) / Scale, (screenY - PanY) / Scale);
}
