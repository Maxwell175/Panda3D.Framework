# Panda3D.Framework.Audio

Audio lifecycle, playback handles, and 3-D positional sound for the Panda3D.Framework
game framework, built directly on the engine's native `AudioManager`/`AudioSound`
bindings. Replaces ShowBase's audio managers and `Audio3DManager`.

## Provides

- `AddAudio()` — registers `IAudio` plus the per-frame `audioLoop` update task at `FrameSlots.Audio` (requires events).
- `AddAudio3D()` — a scoped per-view `IAudio3D` positional-sound registry over the SFX manager.
- `IAudio` — the native `Sfx`/`Music` managers, `LoadSfx(path, positional)`, `LoadMusic(path)`, and `Wrap(AudioSound)`.
- `ISound` — a reusable handle: `Play`/`Stop`, `Volume`, `Loop`, `IsPlaying`, an awaitable `Finished` observable, and the native `Native` escape hatch (rate, balance, seek).
- `IAudio3D` — `AttachListener`, `Attach`/`Detach`, `SetVelocity`/`SetVelocityAuto`, and `DistanceFactor`.

## Usage

```csharp
services.AddAudio();          // add AddAudio3D() for positional sound

var audio = provider.GetRequiredService<IAudio>();

var click = audio.LoadSfx("click.ogg");
click.Play();                                       // reusable: play as often as you like

var theme = audio.LoadMusic("theme.ogg");
theme.Loop = true;
theme.Play();
audio.Music.SetVolume(0.6f);                        // volume group = the native manager

await audio.LoadSfx("stinger.ogg").Finished;        // awaitable completion

// 3-D: the emitter node moves with the actor, the camera listens.
var steps = audio.LoadSfx("steps.ogg", positional: true);
audio3d.AttachListener(cameraNode);
audio3d.Attach(steps.Native, emitterNode);
audio3d.SetVelocityAuto(steps.Native);
```

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
