using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace Panda3D.Framework.Input;

/// <summary>A scalar action. Combines its bindings by largest magnitude (keyboard OR stick).</summary>
public sealed class AxisAction : InputAction
{
    readonly Subject<float> _changed = new();

    public AxisAction(string name) : base(name) { }

    public IList<IAxisBindingSource> Bindings { get; } = new List<IAxisBindingSource>();

    public float Value { get; private set; }

    /// <summary>Fires only when <see cref="Value"/> changes between frames.</summary>
    public IObservable<float> Changed => _changed;

    internal override void Evaluate(IInputSampler sampler, float dt)
    {
        float best = 0f;
        foreach (var b in Bindings)
        {
            if (b is IAxisEvaluable e)
            {
                float v = e.Evaluate(sampler);
                if (MathF.Abs(v) > MathF.Abs(best)) best = v;
            }
        }

        if (best != Value)
        {
            Value = best;
            _changed.OnNext(best);
        }
    }
}
