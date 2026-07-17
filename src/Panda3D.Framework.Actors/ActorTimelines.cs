using System;
using Panda3D.Direct;
using Panda3D.Framework.Intervals;

namespace Panda3D.Framework.Actors;

/// <summary>Actor animation timeline helpers.</summary>
public static class ActorTimelines
{
    public static IInterval CrossFade(this IActor actor, string fromAnim, string toAnim, double dur, Ease ease = Ease.None, string part = ActorDefaults.DefaultPart)
    {
        ArgumentNullException.ThrowIfNull(actor);
        actor.EnableBlend(part);

        var from = actor.Anim(fromAnim, part);
        var to = actor.Anim(toAnim, part);
        var native = new CLerpAnimEffectInterval($"crossfade-{fromAnim}-{toAnim}", dur, ToNativeBlend(ease));
        native.AddControl(from, fromAnim, begin_effect: 1f, end_effect: 0f);
        native.AddControl(to, toAnim, begin_effect: 0f, end_effect: 1f);
        return new FromNative(native);
    }

    static CLerpIntervalBlendType ToNativeBlend(Ease ease) => ease switch
    {
        Ease.In => CLerpIntervalBlendType.BtEaseIn,
        Ease.Out => CLerpIntervalBlendType.BtEaseOut,
        Ease.InOut => CLerpIntervalBlendType.BtEaseInOut,
        _ => CLerpIntervalBlendType.BtNoBlend,
    };
}
