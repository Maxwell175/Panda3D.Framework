using System;
using System.Reactive;
using System.Reactive.Subjects;
using Panda3D.Async;
using Panda3D.Direct;

namespace Panda3D.Framework.Intervals;

/// <summary>
/// Thin managed handle over a playing native <see cref="CInterval"/>. Completion (the awaiter and the
/// <see cref="Completed"/> observable) is built lazily from the manager's done-event task, so a fire-and-forget
/// <c>Play</c> that is never awaited or observed subscribes to nothing.
/// </summary>
internal sealed class PlayingInterval : IPlayingInterval
{
    readonly CInterval _iv;
    readonly Func<CInterval, PandaTask> _whenDone;
    PandaTask? _done;
    AsyncSubject<Unit>? _completed;

    public PlayingInterval(CInterval iv, Func<CInterval, PandaTask> whenDone)
    {
        _iv = iv;
        _whenDone = whenDone;
    }

    PandaTask Done => _done ??= _whenDone(_iv);

    public CInterval Native => _iv;
    public bool IsPlaying => _iv.GetState() != CIntervalState.SFinal;

    public double Time { get => _iv.GetT(); set => _iv.SetT(value); }
    public double PlayRate { get => _iv.GetPlayRate(); set => _iv.SetPlayRate(value); }

    public void Pause() => _iv.Pause();
    public void Resume() => _iv.Resume();
    public void Finish() => _iv.Finish();

    public IObservable<Unit> Completed
    {
        get
        {
            if (_completed is null)
            {
                _completed = new AsyncSubject<Unit>();
                Bridge(_completed);
            }
            return _completed;
        }
    }

    // relays the one-shot done-task onto the replaying subject
    async void Bridge(AsyncSubject<Unit> subject)
    {
        await Done;
        subject.OnNext(Unit.Default);
        subject.OnCompleted();
    }
}
