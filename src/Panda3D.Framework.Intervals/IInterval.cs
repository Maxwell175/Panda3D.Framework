namespace Panda3D.Framework.Intervals;

/// <summary>The composition currency — anything <see cref="Sequence"/>/<see cref="Parallel"/>/<c>Play</c> accepts.</summary>
public interface IInterval
{
    /// <summary>The interval's duration in seconds.</summary>
    double Duration { get; }
}
