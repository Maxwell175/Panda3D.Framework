using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>One analog axis → float, with per-binding processors.</summary>
public sealed class AxisBinding : IAxisBindingSource, IAxisEvaluable
{
    public AxisBinding(InputDeviceAxis axis) => Axis = axis;

    public InputDeviceAxis Axis { get; set; }
    public float Deadzone { get; set; }
    public bool Invert { get; set; }
    public float Scale { get; set; } = 1f;

    float IAxisEvaluable.Evaluate(IInputSampler sampler)
    {
        float v = Processors.Deadzone1D(sampler.Axis(Axis), Deadzone);
        if (Invert) v = -v;
        return v * Scale;
    }
}
