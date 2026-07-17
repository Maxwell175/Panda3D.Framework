using System;
using System.ComponentModel;

namespace Panda3D.Framework.Scheduling;

/// <summary>
/// Error channel for <see cref="PandaFrameTask"/> callbacks. The host subscribes to log or escalate a
/// frame task that threw.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class FrameTaskDiagnostics
{
    /// <summary>Raised when a frame-task callback throws. The task is then removed.</summary>
    public static event Action<Exception>? UnhandledException;

    internal static void Report(Exception ex) => UnhandledException?.Invoke(ex);
}
