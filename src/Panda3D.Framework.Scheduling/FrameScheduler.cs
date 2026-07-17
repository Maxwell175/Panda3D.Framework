using System;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;

namespace Panda3D.Framework.Scheduling;

/// <summary>
/// <see cref="IFrameScheduler"/> that materializes each registration as a <see cref="PandaFrameTask"/>,
/// so gameplay tasks and framework tasks share one ordering space sorted by <see cref="FrameSlots"/>.
/// </summary>
internal sealed class FrameScheduler : IFrameScheduler
{
    readonly IServiceProvider _services;
    readonly IGameClock _clock;
    readonly IHostApplicationLifetime _lifetime;

    public FrameScheduler(IServiceProvider services, IGameClock clock, IHostApplicationLifetime lifetime)
    {
        _services = services;
        _clock = clock;
        _lifetime = lifetime;
    }

    public IScheduledTask AddFrameTask(Func<FrameContext, TaskResult> task, int sort = FrameSlots.Gameplay, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(task);
        var taskName = name ?? "frame-task";
        var ft = PandaFrameTask.Register(taskName, sort, () =>
        {
            var ctx = new FrameContext(_services, _clock.Dt, _lifetime.ApplicationStopping);
            return task(ctx) == TaskResult.Continue;
        });
        return new ScheduledTask(ft, name, sort);
    }

    public IScheduledTask AddTimed(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
        var ft = PandaFrameTask.Register(
            name: "timed",
            sort: FrameSlots.Gameplay,
            tick: () => { action(); return false; },   // run once, then remove
            delay: delay.TotalSeconds);
        return new ScheduledTask(ft, null, FrameSlots.Gameplay);
    }

    public IScheduledTask AddFixedStep(double hz, Action<double> step, int sort = FrameSlots.Gameplay)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (hz <= 0) throw new ArgumentOutOfRangeException(nameof(hz));

        double fixedDt = 1.0 / hz;
        double accumulator = 0;
        // cap catch-up steps so a long stall can't spiral
        const int maxStepsPerFrame = 8;

        var ft = PandaFrameTask.Register("fixed-step", sort, () =>
        {
            accumulator += _clock.Dt;
            int steps = 0;
            while (accumulator >= fixedDt && steps < maxStepsPerFrame)
            {
                step(fixedDt);
                accumulator -= fixedDt;
                steps++;
            }
            // drop leftover backlog beyond the cap
            if (accumulator > fixedDt * maxStepsPerFrame)
                accumulator = 0;
            return true;
        });
        return new ScheduledTask(ft, "fixed-step", sort);
    }

    public PandaTask DelayFrames(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        return DelayFramesImpl(n);
    }

    static async PandaTask DelayFramesImpl(int n)
    {
        for (int i = 0; i < n; i++)
            await PandaTask.NextFrame();
    }

    sealed class ScheduledTask : IScheduledTask
    {
        readonly PandaFrameTask _task;

        public ScheduledTask(PandaFrameTask task, string? name, int sort)
        {
            _task = task;
            Name = name;
            Sort = sort;
        }

        public string? Name { get; }
        public int Sort { get; }

        public void Dispose() => _task.Dispose();
    }
}
