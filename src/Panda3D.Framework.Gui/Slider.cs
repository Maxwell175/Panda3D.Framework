using Panda3D.Core;

namespace Panda3D.Framework.Gui;

public sealed class Slider : SliderBarBase
{
    public Slider(bool vertical, float length, float width, float bevel = 0.05f, string name = "slider")
        : this(0f, 1f, 0f, vertical ? GuiOrientation.Vertical : GuiOrientation.Horizontal, name, length, width, bevel)
    {
    }

    public Slider(
        float min = 0f,
        float max = 1f,
        float value = 0f,
        GuiOrientation orientation = GuiOrientation.Horizontal,
        string name = "slider",
        float length = 1f,
        float width = 0.08f,
        float bevel = 0.02f)
        : base(new PGSliderBar(name))
    {
        Item.SetupSlider(orientation == GuiOrientation.Vertical, length, width, bevel);
        Item.SetRange(min, max);
        Item.Value = value;
    }
}
