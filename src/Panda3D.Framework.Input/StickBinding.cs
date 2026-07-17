using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>An axis pair → <c>LVector2</c>, with a radial deadzone.</summary>
public sealed class StickBinding : IVectorBindingSource, IVectorEvaluable
{
    public StickBinding(InputDeviceAxis x, InputDeviceAxis y)
    {
        X = x;
        Y = y;
    }

    public InputDeviceAxis X { get; set; }
    public InputDeviceAxis Y { get; set; }
    public float Deadzone { get; set; }

    LVector2f IVectorEvaluable.Evaluate(IInputSampler sampler)
        => Processors.Deadzone2D(sampler.Axis(X), sampler.Axis(Y), Deadzone);
}
