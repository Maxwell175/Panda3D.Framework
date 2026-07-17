using Xunit;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Panda3D.Framework.Rendering.Tests;

/// <summary>The aspect-ratio math and window-event demux — pure logic, no output needed.</summary>
public sealed class Overlay2dMathTests
{
    [Fact]
    public void AspectRatioIsWidthOverHeight()
    {
        Assert.Equal(16f / 9f, Overlay2dMath.AspectRatio(1600, 900), 4);
        Assert.Equal(1f, Overlay2dMath.AspectRatio(100, 0)); // guard against div-by-zero
    }

    [Fact]
    public void WideOverlaySquashesX()
    {
        var s = Overlay2dMath.OverlayScale(2f);
        Assert.Equal(0.5f, s.X, 4);
        Assert.Equal(1f, s.Y, 4);
        Assert.Equal(1f, s.Z, 4);
    }

    [Fact]
    public void TallOverlaySquashesYZ()
    {
        var s = Overlay2dMath.OverlayScale(0.5f);
        Assert.Equal(1f, s.X, 4);
        Assert.Equal(0.5f, s.Y, 4);
        Assert.Equal(0.5f, s.Z, 4);
    }

    [Fact]
    public void PixelScaleMakesOneUnitOnePixel()
    {
        var p = Overlay2dMath.PixelScale(800, 600);
        Assert.Equal(2f / 800f, p.X, 6);
        Assert.Equal(1f, p.Y, 6);
        Assert.Equal(2f / 600f, p.Z, 6);
    }

    [Fact]
    public void OverlayEdgesTrackWideAndTallAspect()
    {
        var wide = Overlay2dMath.OverlayEdges(2f);
        Assert.Equal(-2f, wide.Left, 4);
        Assert.Equal(2f, wide.Right, 4);
        Assert.Equal(-1f, wide.Bottom, 4);
        Assert.Equal(1f, wide.Top, 4);

        var tall = Overlay2dMath.OverlayEdges(0.5f);
        Assert.Equal(-1f, tall.Left, 4);
        Assert.Equal(1f, tall.Right, 4);
        Assert.Equal(-2f, tall.Bottom, 4);
        Assert.Equal(2f, tall.Top, 4);
    }
}

public sealed class WindowEventDemuxTests
{
    static WindowSnapshot Open(int w, int h, bool fg = true, bool min = false)
        => new(Open: true, Foreground: fg, Minimized: min, HasSize: true, Width: w, Height: h);

    [Fact]
    public void DetectsResize()
    {
        var c = WindowEventDemux.Diff(Open(800, 600), Open(1024, 768));
        Assert.True(c.Resized);
        Assert.False(c.Closed);
        Assert.False(c.FocusChanged);
    }

    [Fact]
    public void NoResizeWhenSizeUnchanged()
    {
        var c = WindowEventDemux.Diff(Open(800, 600), Open(800, 600));
        Assert.False(c.Resized);
    }

    [Fact]
    public void DetectsClose()
    {
        var prev = Open(800, 600);
        var curr = prev with { Open = false };
        var c = WindowEventDemux.Diff(prev, curr);
        Assert.True(c.Closed);
    }

    [Fact]
    public void DetectsFocusChange()
    {
        var c = WindowEventDemux.Diff(Open(800, 600, fg: true), Open(800, 600, fg: false));
        Assert.True(c.FocusChanged);
        Assert.False(c.Foreground);
    }

    [Fact]
    public void DetectsMinimize()
    {
        var c = WindowEventDemux.Diff(Open(800, 600, min: false), Open(800, 600, min: true));
        Assert.True(c.MinimizedChanged);
        Assert.True(c.Minimized);
    }
}
