# Panda3D.Framework.Input

Builds Panda's data graph and per-window input chain, registers the `dataLoop` task that traverses and evaluates it each frame, and exposes input through three layers: raw device polling at the bottom, a serializable action / binding / context layer in the middle, and both polling and typed-event (`IObservable<T>`) access on top. Bindings and polling are physical (raw scan position), so WASD is correct on any keyboard layout.

## Provides

- `AddInput()` — devices, the action runtime, and the `dataLoop` frame task (capture edges → traverse → evaluate actions).
- `IInput` — per-view raw polling: `IsDown`/`Pressed`/`Released(ButtonId)`, `MousePosition`, `IsOverUi`, `CaptureNext()`. Per-view — resolve it from the view scope via `view.Input()` (or `view.Services`), not the root provider.
- `Keys` / `Mouse` / `Gamepad` — `ButtonId` vocabularies (`Keys.Space`, `Keys.Ascii('w')`, `Mouse.Left`, `Gamepad.A`, …); `ButtonId` persists by stable `Name`.
- Actions: `ButtonAction` (bool), `AxisAction` (float), `VectorAction` (`LVector2f`) — each with polling state and `Pressed`/`Released`/`Changed` observables; `Bindings` is statically constrained to compatible binding types.
- Bindings: `ButtonBinding`, `AxisBinding`, `CompositeAxisBinding`, `CompositeVectorBinding`, `StickBinding` (deadzone/invert/scale live on the analog binding).
- `IInputContext` — a named, prioritized, enable/disable-able action set; create with `serviceProvider.CreateContext(name, priority)` (e.g. `view.Services.CreateContext(...)`).
- `IDevices` — gamepad/joystick enumeration plus `Connected`/`Disconnected` hotplug observables.
- `IButtonLabels` — layout-aware display labels for HUD / rebind UI.

## Usage

```csharp
var ctx = view.Services.CreateContext("gameplay");

var move = ctx.Add(new VectorAction("move"));
move.Bindings.Add(new CompositeVectorBinding(Keys.Ascii('w'), Keys.Ascii('s'), Keys.Ascii('a'), Keys.Ascii('d')));
move.Bindings.Add(new StickBinding(InputDeviceAxis.LeftX, InputDeviceAxis.LeftY) { Deadzone = 0.15f });
LVector2f dir = move.Value;                       // poll, or move.Changed.Subscribe(v => …)

var jump = ctx.Add(new ButtonAction("jump"));
jump.Bindings.Add(new ButtonBinding(Keys.Space));
jump.Bindings.Add(new ButtonBinding(Gamepad.A));  // multiple bindings, OR'd
jump.Pressed.Subscribe(_ => DoJump());            // observable… or poll: if (jump.WasPressed) …

// raw-polling escape hatch (per view):
if (view.Input().Pressed(Keys.Escape)) Quit();
```

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
