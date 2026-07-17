using System;
using Panda3D.Core;

namespace Panda3D.Framework.Intervals;

/// <summary>Type-directed linear interpolation used by <see cref="Lerp{T}"/>.</summary>
internal static class Interpolators
{
    static float L(float a, float b, double u) => (float)(a + (b - a) * u);

    public static Func<T, T, double, T> Resolve<T>()
    {
        var t = typeof(T);
        if (t == typeof(float)) return Cast<T, float>((a, b, u) => L(a, b, u));
        if (t == typeof(double)) return Cast<T, double>((a, b, u) => a + (b - a) * u);
        if (t == typeof(LVecBase2f))
            return Cast<T, LVecBase2f>((a, b, u) => new LVecBase2f(L(a.GetX(), b.GetX(), u), L(a.GetY(), b.GetY(), u)));
        if (t == typeof(LVecBase3f))
            return Cast<T, LVecBase3f>((a, b, u) => new LVecBase3f(L(a.GetX(), b.GetX(), u), L(a.GetY(), b.GetY(), u), L(a.GetZ(), b.GetZ(), u)));
        if (t == typeof(LVecBase4f))
            return Cast<T, LVecBase4f>((a, b, u) => new LVecBase4f(L(a.GetX(), b.GetX(), u), L(a.GetY(), b.GetY(), u), L(a.GetZ(), b.GetZ(), u), L(a.GetW(), b.GetW(), u)));

        throw new NotSupportedException(
            $"Lerp<{t.Name}> is not supported out of the box. Supported: float, double, LVecBase2f/3f/4f. " +
            "For other types, compose your own ManagedInterval.");
    }

    static Func<T, T, double, T> Cast<T, TConcrete>(Func<TConcrete, TConcrete, double, TConcrete> fn)
        => (Func<T, T, double, T>)(object)fn;
}
