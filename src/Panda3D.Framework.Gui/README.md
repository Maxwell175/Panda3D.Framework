# Panda3D.Framework.Gui

Per-view GUI for the Panda3D.Framework game framework: explicit, typed C# `Widget`
classes over Panda's native PGui items (`PGButton`, `PGEntry`, `PGSliderBar`,
`PGScrollFrame`, `PGWaitBar`). Every PGui interaction is surfaced as a typed
`IObservable<T>` — no string event ever reaches a consumer. Replaces `direct.gui.DirectGui`.

## Provides

- `AddGui()` — registers `IGui` as a scoped per-view service (requires rendering and events).
- `IGui` — `Add<T>(widget, parent?)` and `Add(label, parent?)` to materialize and wire a widget under the view's overlay roots.
- `Widget` / `Widget<TItem>` — base wrapper: `Node`, the native `Item`, `Visible`, `Enabled`, and typed observables (`Entered`/`Exited`/`Within`/`Without`/`Pressed`/`Released`/`FocusChanged`).
- Widgets: `Button` (`Clicked`), `Entry` (`Text`, `Submitted`/`Changed`/`Overflowed`/`CursorMoved`), `Slider` and `ScrollBar` (`Value`/`Ratio`/`Min`/`Max`/`ValueChanged`), `ScrollFrame` (`Canvas`/`VirtualFrame`/`Scrolled`), `ProgressBar` (`Value`/`Percent`), and `Label` (text-only, non-interactive).

## Usage

```csharp
services.AddGui();

var gui = view.Services.GetRequiredService<IGui>();   // per-view; at root this is the main view

var play = gui.Add(new Button("Play"));
play.Node.SetPos(0, 0, -0.2f);
play.Clicked.Subscribe(_ => StartGame());

var name = gui.Add(new Entry(width: 12));
name.Submitted.Subscribe(text => Join(text));

var volume = gui.Add(new Slider(vertical: false, length: 0.8f, width: 0.08f) { Min = 0, Max = 1, Value = 0.7f });
volume.ValueChanged.Subscribe(v => audio.Sfx.SetVolume(v));
```

Part of [Panda3D.Framework](https://github.com/Maxwell175/Panda3D.Framework); the `Panda3D.Framework` umbrella package pulls in every library plus the bindings and native runtimes.
