using System;

namespace Panda3D.Framework;

/// <summary>A disposable handle to a registered task; dispose to remove it.</summary>
public interface IScheduledTask : IDisposable
{
    /// <summary>The task's name, if one was given.</summary>
    string? Name { get; }

    /// <summary>The task's sort on the <see cref="FrameSlots"/> scale.</summary>
    int Sort { get; }
}
