using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>A 2-D vector action. Combines its bindings by largest magnitude.</summary>
public sealed class VectorAction : InputAction
{
    readonly Subject<LVector2f> _changed = new();

    public VectorAction(string name) : base(name) { }

    public IList<IVectorBindingSource> Bindings { get; } = new List<IVectorBindingSource>();

    public LVector2f Value { get; private set; } = new(0f, 0f);

    /// <summary>Fires only when <see cref="Value"/> changes between frames.</summary>
    public IObservable<LVector2f> Changed => _changed;

    internal override void Evaluate(IInputSampler sampler, float dt)
    {
        LVector2f best = new(0f, 0f);
        float bestMag = 0f;
        foreach (var b in Bindings)
        {
            if (b is IVectorEvaluable e)
            {
                var v = e.Evaluate(sampler);
                float mag = MathF.Sqrt(v.GetX() * v.GetX() + v.GetY() * v.GetY());
                if (mag > bestMag) { best = v; bestMag = mag; }
            }
        }

        if (best.GetX() != Value.GetX() || best.GetY() != Value.GetY())
        {
            Value = best;
            _changed.OnNext(best);
        }
    }
}
