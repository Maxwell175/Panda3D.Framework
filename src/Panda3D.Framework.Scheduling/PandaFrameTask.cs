using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Panda3D.Core;

namespace Panda3D.Framework.Scheduling;

/// <summary>
/// A managed per-frame callback registered as a native <see cref="ManagedAsyncTask"/> on a task chain
/// at an explicit sort. Higher layers build their sorted per-frame work on this; the callback runs once
/// per chain epoch in sort order.
/// </summary>
/// <remarks>
/// The callback returns <see langword="true"/> to keep running (<c>DS_cont</c>) or <see langword="false"/>
/// to remove itself (<c>DS_done</c>). Disposing the handle removes the task deterministically. Exceptions
/// thrown by the callback are routed to <see cref="FrameTaskDiagnostics.UnhandledException"/> and remove
/// the task.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class PandaFrameTask : IDisposable
{
    readonly ManagedAsyncTask _task;
    readonly Callback _callback;
    int _disposed;

    PandaFrameTask(ManagedAsyncTask task, Callback callback, int sort, string name)
    {
        _task = task;
        _callback = callback;
        Sort = sort;
        Name = name;
    }

    /// <summary>The sort at which this task runs on its chain.</summary>
    public int Sort { get; }

    /// <summary>The task's name.</summary>
    public string Name { get; }

    /// <summary>Whether the underlying native task is still scheduled.</summary>
    public bool IsAlive => _disposed == 0 && _task.IsAlive();

    /// <summary>
    /// Register <paramref name="tick"/> as a native sorted task on <paramref name="chainName"/>.
    /// </summary>
    /// <param name="name">Task name (diagnostics / removal).</param>
    /// <param name="sort">Sort on the chain's ordering scale (e.g. a <see cref="FrameSlots"/> value).</param>
    /// <param name="tick">Invoked once per epoch; return <see langword="true"/> to continue, <see langword="false"/> to remove.</param>
    /// <param name="chainName">Target task chain (default <c>"default"</c>).</param>
    /// <param name="delay">Optional initial delay in seconds before the first run.</param>
    public static PandaFrameTask Register(
        string name, int sort, Func<bool> tick,
        string chainName = "default", double delay = 0)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(tick);

        var callback = new Callback(tick);
        var handle = GCHandle.Alloc(callback);
        var task = ManagedAsyncTask.Make(
            name,
            FrameTaskTrampolines.RunPtr,
            FrameTaskTrampolines.FreePtr,
            (ulong)(nint)GCHandle.ToIntPtr(handle));

        task.SetSort(sort);
        task.SetTaskChain(chainName);
        if (delay > 0) task.SetDelay(delay);

        AsyncTaskManager.GetGlobalPtr().Add(task);
        return new PandaFrameTask(task, callback, sort, name);
    }

    /// <summary>Remove the native task from its chain. Idempotent.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _callback.Cancel();
        // native destruction fires the free trampoline, which frees the GCHandle
        try { _task.Remove(); } catch { /* already removed */ }
        (_task as IDisposable)?.Dispose();
    }

    /// <summary>Managed side of the native callback: maps a <c>Func&lt;bool&gt;</c> to a DoneStatus.</summary>
    internal sealed class Callback
    {
        Func<bool>? _tick;

        public Callback(Func<bool> tick) => _tick = tick;

        public void Cancel() => _tick = null;

        public int Run()
        {
            var tick = _tick;
            if (tick is null) return (int)AsyncTaskDoneStatus.DsDone;
            try
            {
                return tick()
                    ? (int)AsyncTaskDoneStatus.DsCont
                    : (int)AsyncTaskDoneStatus.DsDone;
            }
            catch (Exception ex)
            {
                FrameTaskDiagnostics.Report(ex);
                return (int)AsyncTaskDoneStatus.DsDone;
            }
        }
    }
}
