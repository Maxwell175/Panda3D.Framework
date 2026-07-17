using Panda3D.Core;

namespace Panda3D.Framework.Gui;

public sealed class ProgressBar : Widget<PGWaitBar>
{
    public ProgressBar(
        float width,
        float height,
        float range = 100f,
        string name = "progress")
        : base(new PGWaitBar(name))
    {
        Item.Setup(width, height, range);
    }

    public float Range
    {
        get => Item.Range;
        set => Item.Range = value;
    }

    public float Value
    {
        get => Item.Value;
        set => Item.Value = value;
    }

    public float Percent => Item.GetPercent();
}
