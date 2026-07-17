using System;
using Panda3D.Core;
using Panda3D.Framework.Rendering;
using Xunit;

namespace Panda3D.Framework.VisualTestSupport;

internal static class VisualTestHelpers
{
    public static NodePath AddWhiteCard(NodePath parent, string name, float size = 1.0f)
    {
        var cm = new CardMaker(name);
        float half = size * 0.5f;
        cm.SetFrame(-half, half, -half, half);
        cm.SetColor(1f, 1f, 1f, 1f);

        var card = parent.AttachNewNode(cm.Generate());
        card.SetTwoSided(true);
        card.SetLightOff();
        card.SetShaderOff();
        return card;
    }

    public static PNMImage Capture(IView view)
    {
        var image = new PNMImage();
        Assert.True(view.Output.GetScreenshot(image), "offscreen screenshot readback should succeed");
        Assert.True(image.GetXSize() > 0, "screenshot should have nonzero width");
        Assert.True(image.GetYSize() > 0, "screenshot should have nonzero height");
        return image;
    }

    public static int CountBrightPixels(PNMImage image, float threshold = 0.75f)
    {
        int count = 0;
        for (int y = 0; y < image.GetYSize(); y++)
        {
            for (int x = 0; x < image.GetXSize(); x++)
            {
                if (image.GetRed(x, y) >= threshold
                    && image.GetGreen(x, y) >= threshold
                    && image.GetBlue(x, y) >= threshold)
                    count++;
            }
        }

        return count;
    }

    public static int CountBrightPixelsInColumns(PNMImage image, int minXInclusive, int maxXExclusive, float threshold = 0.75f)
    {
        int minX = Math.Clamp(minXInclusive, 0, image.GetXSize());
        int maxX = Math.Clamp(maxXExclusive, 0, image.GetXSize());

        int count = 0;
        for (int y = 0; y < image.GetYSize(); y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                if (image.GetRed(x, y) >= threshold
                    && image.GetGreen(x, y) >= threshold
                    && image.GetBlue(x, y) >= threshold)
                    count++;
            }
        }

        return count;
    }

    public static int CountNonBlackPixels(PNMImage image, float threshold = 0.08f)
    {
        int count = 0;
        for (int y = 0; y < image.GetYSize(); y++)
        {
            for (int x = 0; x < image.GetXSize(); x++)
            {
                if (Math.Max(image.GetRed(x, y), Math.Max(image.GetGreen(x, y), image.GetBlue(x, y))) >= threshold)
                    count++;
            }
        }

        return count;
    }

    public static int CountDifferentPixels(PNMImage a, PNMImage b, float threshold = 0.08f)
    {
        Assert.Equal(a.GetXSize(), b.GetXSize());
        Assert.Equal(a.GetYSize(), b.GetYSize());

        int count = 0;
        for (int y = 0; y < a.GetYSize(); y++)
        {
            for (int x = 0; x < a.GetXSize(); x++)
            {
                float dr = Math.Abs(a.GetRed(x, y) - b.GetRed(x, y));
                float dg = Math.Abs(a.GetGreen(x, y) - b.GetGreen(x, y));
                float db = Math.Abs(a.GetBlue(x, y) - b.GetBlue(x, y));
                if (Math.Max(dr, Math.Max(dg, db)) >= threshold)
                    count++;
            }
        }

        return count;
    }

    public static void UseBlackBackground(IView view)
    {
        view.ClearColor = new LVecBase4f(0f, 0f, 0f, 1f);   // sets the color and enables clearing in one
        foreach (var region in view.Regions)
            region.SetClearColorActive(false);
    }
}
