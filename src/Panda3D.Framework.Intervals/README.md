# Panda3D.Framework.Intervals

Cutscene and tweening timelines — lerps, sequences, parallels, waits, and callbacks that are declarative, pausable, reversible, scrubbable, and awaitable — built **on Panda's own C++ interval system** (`CInterval` and friends), not a managed re-implementation. It is the interval/tween layer of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework).

Common lerps run at C++ speed; managed intervals (game logic, custom easing) participate in the same timeline through the engine's external-interval mechanism.

## Provides

- `AddIntervals()` — registers `IIntervalManager` and its per-frame step task.
- `IIntervalManager` — `Play`/`Loop` (return an `IPlayingInterval`) and `Interrupt()`.
- `IPlayingInterval` — playback handle: `Completed` (awaitable/observable), `IsPlaying`, `Time`/`PlayRate`, `Pause`/`Resume`/`Finish`, and native `CInterval` escape hatch.
- Composition: `Sequence`, `Parallel`, `Wait`, `Show`, `Hide`, `FromNative` (adopt any raw `CInterval`), over the `IInterval` currency.
- `NodeLerps` — `NodePath` extension lerps at C++ speed: `PosTo`/`HprTo`/`QuatTo`/`ScaleTo`/`ShearTo`/`ColorTo`/`ColorScaleTo`/`AlphaTo`, the combined `TransformTo`, and `TexOffsetTo`/`TexRotateTo`/`TexScaleTo`; `Ease` picks the native blend.
- Managed intervals: `ManagedInterval` (derive for stepped logic), `Call` (zero-duration side effect), `Lerp<T>` (custom easing, any target), `SoundInterval`.

```csharp
services.AddIntervals();
// ...
var cutscene = new Sequence("intro",
    new Parallel(
        hero.PosTo(doorstep, 4, Ease.InOut),           // native NodePath lerp
        new Lerp<float>(1f, 0f, 4, a => hud.Alpha = a)),// game logic in the same timeline
    new Wait(0.5),
    new Call(() => door.Unlock()));

var play = intervals.Play(cutscene);
skip.Clicked.Subscribe(_ => play.Finish());            // skip = jump to the end state
await play.Completed;                                  // resumes in the gameplay slot
```

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
