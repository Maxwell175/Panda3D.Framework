using System;
using Panda3D.Core;
using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// Factories over <c>CLerpNodePathInterval</c> — transform/color/texture lerps at C++ speed. Omitting
/// <c>from</c> bakes in the start value when the lerp begins playing; supplying it sets an explicit start.
/// Values are Panda's own float value types (<c>LVecBase3f</c> etc.).
/// </summary>
public static class NodeLerps
{
    static CLerpNodePathInterval Make(NodePath n, double dur, Ease ease, bool bakeInStart, bool fluid, NodePath? other)
        => new("nodeLerp", dur, ease.ToBlend(), bakeInStart, fluid, n, other ?? new NodePath());

    public static IInterval PosTo(this NodePath n, LVecBase3f pos, double dur, Ease ease = Ease.None, LVecBase3f? from = null, NodePath? other = null, bool fluid = false)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, from is null, fluid, other);
            l.SetEndPos(pos);
            if (from is not null) l.SetStartPos(from);
            return l;
        });

    public static IInterval HprTo(this NodePath n, LVecBase3f hpr, double dur, Ease ease = Ease.None, LVecBase3f? from = null, NodePath? other = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, from is null, false, other);
            l.SetEndHpr(hpr);
            if (from is not null) l.SetStartHpr(from);
            return l;
        });

    public static IInterval QuatTo(this NodePath n, LQuaternionf quat, double dur, Ease ease = Ease.None, LQuaternionf? from = null, NodePath? other = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, from is null, false, other);
            l.SetEndQuat(quat);
            if (from is not null) l.SetStartQuat(from);
            return l;
        });

    public static IInterval ScaleTo(this NodePath n, LVecBase3f scale, double dur, Ease ease = Ease.None, LVecBase3f? from = null, NodePath? other = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, from is null, false, other);
            l.SetEndScale(scale);
            if (from is not null) l.SetStartScale(from);
            return l;
        });

    public static IInterval ScaleTo(this NodePath n, float scale, double dur, Ease ease = Ease.None)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, true, false, null);
            l.SetEndScale(scale);
            return l;
        });

    public static IInterval ShearTo(this NodePath n, LVecBase3f shear, double dur, Ease ease = Ease.None, LVecBase3f? from = null, NodePath? other = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, from is null, false, other);
            l.SetEndShear(shear);
            if (from is not null) l.SetStartShear(from);
            return l;
        });

    public static IInterval ColorTo(this NodePath n, LVecBase4f color, double dur, Ease ease = Ease.None, LVecBase4f? from = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, from is null, false, null);
            l.SetEndColor(color);
            if (from is not null) l.SetStartColor(from);
            return l;
        });

    public static IInterval ColorScaleTo(this NodePath n, LVecBase4f scale, double dur, Ease ease = Ease.None, LVecBase4f? from = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, from is null, false, null);
            l.SetEndColorScale(scale);
            if (from is not null) l.SetStartColorScale(from);
            return l;
        });

    /// <summary>Convenience: lerp the color-scale alpha (RGB scale held at 1).</summary>
    public static IInterval AlphaTo(this NodePath n, float alpha, double dur, Ease ease = Ease.None)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, true, false, null);
            l.SetEndColorScale(new LVecBase4f(1, 1, 1, alpha));
            return l;
        });

    /// <summary>
    /// One native interval lerping any subset of transform properties simultaneously. All supplied
    /// properties bake in their start.
    /// </summary>
    public static IInterval TransformTo(this NodePath n, double dur,
        LVecBase3f? pos = null, LVecBase3f? hpr = null, LQuaternionf? quat = null,
        LVecBase3f? scale = null, LVecBase3f? shear = null,
        Ease ease = Ease.None, NodePath? other = null, bool fluid = false)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, true, fluid, other);
            if (pos is not null) l.SetEndPos(pos);
            if (hpr is not null) l.SetEndHpr(hpr);
            if (quat is not null) l.SetEndQuat(quat);
            if (scale is not null) l.SetEndScale(scale);
            if (shear is not null) l.SetEndShear(shear);
            return l;
        });

    public static IInterval TexOffsetTo(this NodePath n, LVecBase2f offset, double dur, Ease ease = Ease.None, TextureStage? stage = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, true, false, null);
            if (stage is not null) l.SetTextureStage(stage);
            l.SetEndTexOffset(offset);
            return l;
        });

    public static IInterval TexRotateTo(this NodePath n, float degrees, double dur, Ease ease = Ease.None, TextureStage? stage = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, true, false, null);
            if (stage is not null) l.SetTextureStage(stage);
            l.SetEndTexRotate(degrees);
            return l;
        });

    public static IInterval TexScaleTo(this NodePath n, LVecBase2f scale, double dur, Ease ease = Ease.None, TextureStage? stage = null)
        => new NativeInterval(dur, () =>
        {
            var l = Make(n, dur, ease, true, false, null);
            if (stage is not null) l.SetTextureStage(stage);
            l.SetEndTexScale(scale);
            return l;
        });
}
