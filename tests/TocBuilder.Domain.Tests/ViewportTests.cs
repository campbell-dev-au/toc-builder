using TocBuilder.Web.Client;

namespace TocBuilder.Domain.Tests;

public class ViewportTests
{
    [Fact]
    public void Zooming_in_keeps_the_point_under_the_cursor_fixed()
    {
        // Panned by (30, 10), the screen point (200, 120) shows the world point (170, 110).
        var viewport = Viewport.Identity.PanBy(30, 10).ZoomAt(1.5, 200, 120);

        var (x, y) = viewport.ToWorld(200, 120);
        Assert.Equal(170, x, 6);
        Assert.Equal(110, y, 6);
        Assert.Equal(1.5, viewport.Scale, 6);
    }

    [Fact]
    public void Zoom_stays_within_the_allowed_range_and_still_pivots_on_the_cursor()
    {
        var zoomedIn = Viewport.Identity.ZoomAt(1000, 100, 50);
        Assert.Equal(Viewport.MaxScale, zoomedIn.Scale, 6);
        Assert.Equal(100, zoomedIn.ToWorld(100, 50).X, 6);
        Assert.Equal(50, zoomedIn.ToWorld(100, 50).Y, 6);

        var zoomedOut = Viewport.Identity.ZoomAt(0.0001, 100, 50);
        Assert.Equal(Viewport.MinScale, zoomedOut.Scale, 6);
        var (x, y) = zoomedOut.ToWorld(100, 50);
        Assert.Equal(100, x, 6);
        Assert.Equal(50, y, 6);
    }

    [Fact]
    public void Fit_centres_the_content_in_the_view_at_the_largest_scale_that_shows_all_of_it()
    {
        // 400x200 content in an 800x600 view: width limits, so scale 2 (not 3), leaving 100px above and below.
        var viewport = Viewport.Fit(0, 0, 400, 200, 800, 600);

        Assert.Equal(2, viewport.Scale, 6);
        Assert.Equal(0, viewport.PanX, 6);
        Assert.Equal(100, viewport.PanY, 6);
    }

    [Fact]
    public void Fit_never_enlarges_small_content_beyond_the_zoom_range_and_ignores_empty_content()
    {
        Assert.Equal(Viewport.MaxScale, Viewport.Fit(0, 0, 10, 10, 800, 600).Scale, 6);
        Assert.Equal(Viewport.Identity, Viewport.Fit(0, 0, 0, 0, 800, 600));
    }

    [Fact]
    public void Fit_accounts_for_where_the_content_starts()
    {
        // The same 400x200 content, but starting at (-50, 30): its centre, (150, 130), must land at the view's centre.
        var viewport = Viewport.Fit(-50, 30, 400, 200, 800, 600);

        var (x, y) = viewport.ToWorld(400, 300);
        Assert.Equal(150, x, 6);
        Assert.Equal(130, y, 6);
    }
}
