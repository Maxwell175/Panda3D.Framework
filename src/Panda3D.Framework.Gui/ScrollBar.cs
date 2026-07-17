using Panda3D.Core;

namespace Panda3D.Framework.Gui;

public sealed class ScrollBar : SliderBarBase
{
    public ScrollBar(bool vertical, float length, float width, float bevel = 0.05f, string name = "scrollbar")
        : this(vertical ? GuiOrientation.Vertical : GuiOrientation.Horizontal, name, length, width, bevel)
    {
    }

    public ScrollBar(
        GuiOrientation orientation = GuiOrientation.Vertical,
        string name = "scrollbar",
        float length = 1f,
        float width = 0.08f,
        float bevel = 0.02f)
        : base(new PGSliderBar(name))
    {
        Item.SetupScrollBar(orientation == GuiOrientation.Vertical, length, width, bevel);
    }

    public float PageSize
    {
        get => Item.PageSize;
        set => Item.PageSize = value;
    }

    public float ScrollSize
    {
        get => Item.ScrollSize;
        set => Item.ScrollSize = value;
    }

    public bool ResizeThumb
    {
        get => Item.GetResizeThumb();
        set => Item.SetResizeThumb(value);
    }
}
