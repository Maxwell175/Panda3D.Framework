using System;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// The managed lerp: custom easing and any target type, tweened inside the same timeline as native
/// intervals. Out of the box <typeparamref name="T"/> may be <see cref="float"/>, <see cref="double"/>,
/// or <c>LVecBase2f</c>/<c>LVecBase3f</c>/<c>LVecBase4f</c>.
/// </summary>
public sealed class Lerp<T> : ManagedInterval
{
    readonly T _from;
    readonly T _to;
    readonly Action<T> _set;
    readonly Func<double, double>? _ease;
    readonly Func<T, T, double, T> _interpolate;

    public Lerp(T from, T to, double dur, Action<T> set, Func<double, double>? ease = null)
        : base(dur, name: "lerp")
    {
        _from = from;
        _to = to;
        _set = set ?? throw new ArgumentNullException(nameof(set));
        _ease = ease;
        _interpolate = Interpolators.Resolve<T>();
    }

    public override void Initialize(double t) => Step(t);

    public override void Step(double t)
    {
        double u = Duration <= 0 ? 1.0 : Math.Clamp(t / Duration, 0.0, 1.0);
        if (_ease is not null) u = _ease(u);
        _set(_interpolate(_from, _to, u));
    }

    public override void Complete() => _set(_to);
}
