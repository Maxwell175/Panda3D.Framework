using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>Four buttons → <c>LVector2</c>.</summary>
public sealed class CompositeVectorBinding : IVectorBindingSource, IVectorEvaluable
{
    public CompositeVectorBinding(ButtonId up, ButtonId down, ButtonId left, ButtonId right)
    {
        Up = up;
        Down = down;
        Left = left;
        Right = right;
    }

    public ButtonId Up { get; set; }
    public ButtonId Down { get; set; }
    public ButtonId Left { get; set; }
    public ButtonId Right { get; set; }

    LVector2f IVectorEvaluable.Evaluate(IInputSampler sampler)
    {
        float x = (sampler.IsDown(Right) ? 1f : 0f) - (sampler.IsDown(Left) ? 1f : 0f);
        float y = (sampler.IsDown(Up) ? 1f : 0f) - (sampler.IsDown(Down) ? 1f : 0f);
        return new LVector2f(x, y);
    }
}
