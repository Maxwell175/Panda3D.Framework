using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>Easing modes — map to <c>CLerpInterval</c>'s blend types.</summary>
public enum Ease
{
    /// <summary>No blend (linear).</summary>
    None,

    /// <summary>Ease in.</summary>
    In,

    /// <summary>Ease out.</summary>
    Out,

    /// <summary>Ease in and out.</summary>
    InOut,
}

/// <summary>Maps <see cref="Ease"/> to the native <c>CLerpInterval</c> blend type.</summary>
internal static class EaseExtensions
{
    public static CLerpIntervalBlendType ToBlend(this Ease ease) => ease switch
    {
        Ease.In => CLerpIntervalBlendType.BtEaseIn,
        Ease.Out => CLerpIntervalBlendType.BtEaseOut,
        Ease.InOut => CLerpIntervalBlendType.BtEaseInOut,
        _ => CLerpIntervalBlendType.BtNoBlend,
    };
}
