using System;
using System.Runtime.InteropServices;
using Panda3D.Core;

namespace Panda3D.Framework.Scheduling;

/// <summary>
/// Static <see cref="UnmanagedCallersOnlyAttribute"/> run / free trampolines for <c>ManagedAsyncTask.make</c>.
/// The user_data is a <see cref="GCHandle"/> to a <see cref="PandaFrameTask.Callback"/>; native code owns
/// it and invokes the free trampoline on destruction.
/// </summary>
internal static unsafe class FrameTaskTrampolines
{
    public static ulong RunPtr => (ulong)(nint)(delegate* unmanaged<IntPtr, int>)&Run;
    public static ulong FreePtr => (ulong)(nint)(delegate* unmanaged<IntPtr, void>)&Free;

    [UnmanagedCallersOnly]
    static int Run(IntPtr userData)
    {
        try
        {
            var handle = GCHandle.FromIntPtr(userData);
            if (handle.Target is PandaFrameTask.Callback cb)
                return cb.Run();
            return (int)AsyncTaskDoneStatus.DsDone;
        }
        catch (Exception ex)
        {
            FrameTaskDiagnostics.Report(ex);
            return (int)AsyncTaskDoneStatus.DsDone;
        }
    }

    [UnmanagedCallersOnly]
    static void Free(IntPtr userData)
    {
        try
        {
            var handle = GCHandle.FromIntPtr(userData);
            (handle.Target as PandaFrameTask.Callback)?.Cancel();
            handle.Free();
        }
        catch (Exception ex)
        {
            FrameTaskDiagnostics.Report(ex);
        }
    }
}
