# 05 — Input (`Panda3D.Framework.Input`)

**Purpose.** Builds Panda's data graph and per-window input chain, registers the `dataLoop` task that traverses it each epoch, and exposes input through the model the whole industry has converged on: a thin **device-polling** layer at the bottom, a serializable **action / binding / context** layer in the middle, and **both polling and typed-event** access on top. Consumers bind meaning (`"Jump"`, `"Move"`) to physical controls and never touch Panda's global messenger or raw event strings.

**Replaces in `direct`.** ShowBase's data-graph + `MouseAndKeyboard → MouseWatcher → ButtonThrower` chain (`setupMouseCB`), its `dataLoop`, and the `messenger`/`DirectObject.accept` string-event model. `ButtonThrower` stays an internal detail (it throws stringly-typed events — what [Events](06-events.md) moves away from); the input layer reads the per-window `MouseWatcher` directly and, for gamepads, the `InputDeviceManager`.

**Dependencies.** `Abstractions`; `Rendering` (needs `IGraphicsWindow` for the per-window chain, and `DisplayRegion` to constrain a `MouseWatcher`); `Events` (publishes typed action events); the fork's C# bindings.

## The three-layer model

Per the cross-engine survey (Unity Input System, Unreal Enhanced Input, Godot InputMap, Stride virtual buttons, Bevy + leafwing): separate **where input comes from** (devices/windows) from **what it means** (actions), with a data layer between. We adopt that, kept minimal.

1. **Devices (raw).** The per-view `MouseWatcher` (mouse + keyboard, polled) and standalone gamepads/joysticks from `InputDeviceManager`. Reachable directly for the simple case, but most code uses actions.
2. **Actions / bindings / contexts (the indirection).** A named action (`"Jump"`, `"Move"`) resolves from one or more device-control **bindings**; actions live in named **contexts** (gameplay, menu, vehicle) that are enabled/disabled in bulk and prioritized.
3. **Access (both styles).** Per-frame **polling** for gameplay (`button.IsPressed`, `vector.Value`) and **events** for reacting (`button.Pressed/Released`, `value.Changed` — exposed as `System.Reactive` observables; see [06](06-events.md)).

## Keyboard layouts (binding is physical; layout is display-only)

Panda models keys in three tiers:
- **Raw / physical** (`T_raw_down`): the untransformed scan key, "as if the user is using the US QWERTY layout" — i.e. physical *position*. The key where QWERTY's `W` sits, on any layout.
- **Mapped / virtual** (`T_down`): the layout-translated `ButtonHandle`. On AZERTY the physical QWERTY-`A` position arrives as mapped `Q`.
- **Keystroke** (`T_keystroke`): a Unicode codepoint for *typing* (what `PGEntry` consumes), not for key bindings.

**Bindings and polling are always physical.** In-game controls are about ergonomics — *where* a key sits under the hand — so a binding to `Keys.Ascii('w')` matches the physical QWERTY-W position on every layout, and WASD is correct for AZERTY/Dvorak users with zero ceremony. There is no mapped-binding mode and no `physical:` flag; the runtime matches the raw scan stream. (This is the SDL "bind by scancode" consensus.)

**Layout matters only for display.** To show the player which key to press ("press **[X]** to jump"), ask `IButtonLabels.Label(button)`, which reads the main window's `ButtonMap` (`GraphicsWindow.get_keyboard_map()` → `get_mapped_button_label`) and returns the user's actual legend — "Z" on AZERTY for the physical-W key, "W" on QWERTY. Binding ≠ display: only display consults the layout.

## Rebinding

Bindings are **live, mutable state on the action** — there is no separate "defaults vs overrides" system. Configuring at startup and rebinding at runtime are the *same operation*: mutate the action's `Bindings` list.
1. Code constructs actions and adds their default bindings. To persist, **serialize the context's actions** (their bindings) to JSON/config; to restore, deserialize and apply. Loading saved bindings is just constructing the actions with those bindings instead of the defaults.
2. **Interactive rebind:** `IInput.CaptureNext()` returns the next physical button the user presses (a `ButtonId?`, null until one arrives); the rebind UI then mutates the list — `action.Bindings.Clear(); action.Bindings.Add(new ButtonBinding(captured));`. No capture-mode subsystem, no override layer.
3. **Display:** label the current binding via `IButtonLabels.Label(button)`, so the user sees their physical keyboard's legend, not a US-QWERTY name.

**Public surface.**
```csharp
// ---- Layer 1: raw device polling (the simple path; per-view) ----
public interface IInput {                                  // resolve from a view scope (view.Input() / view.Services), not the root provider
    bool IsDown(ButtonId button);                          // physical (raw); button from Keys.* / Mouse.* / Gamepad.*
    bool Pressed(ButtonId button);                         // edge: down this frame, up last
    bool Released(ButtonId button);
    LPoint2f? MousePosition { get; }                        // null when the pointer isn't over this view
    bool IsOverUi { get; }                                  // MouseWatcher.get_over_region() != null (UI gating)
    ButtonId? CaptureNext();                                // next physical button the user presses (for rebind UI); null until one arrives
}
public interface IDevices {                                 // engine-wide; gamepads/joysticks/wheels
    IReadOnlyList<InputDevice> All { get; }                 // all currently-connected non-keyboard/mouse devices
    IObservable<InputDevice> Connected { get; }              // demuxed from Panda's "connect-device" event
    IObservable<InputDevice> Disconnected { get; }           // from "disconnect-device"
}

// ---- Layer 2: actions, bindings, contexts ----
// Buttons are the typed ButtonId struct, from the Keys/Mouse/Gamepad static vocabularies that wrap Panda's
// own button registries (persist ButtonId.Name; reload via ButtonId.FromName):
//   Keys.Space / Keys.Ascii('w'),  Mouse.Left,  Gamepad.A
// Axes are Panda's InputDeviceAxis enum directly. Bindings/polling are PHYSICAL (raw scan position) — see Keyboard layouts.

public interface IInputContext : IDisposable {              // a named, enable/disable-able, prioritized action set
    string Name { get; }
    int Priority { get; set; }                              // higher wins a contested control (Unreal IMC model)
    bool Enabled { get; set; }
    T Add<T>(T action) where T : InputAction;               // construct the action, hand it over; returns it for chaining
    IReadOnlyList<InputAction> Actions { get; }
}

// ---- Binding types: each type's construction guarantees its makeup ----
public interface IBinding { }                               // marker
public interface IAxisBindingSource   : IBinding { }        // produces float    (for AxisAction)
public interface IVectorBindingSource : IBinding { }        // produces LVector2f (for VectorAction)

public sealed class ButtonBinding : IBinding {              // bool (also 0/1 into a value action if used there)
    public ButtonBinding(ButtonId button);
    public ButtonId Button { get; set; }
}
public sealed class AxisBinding : IAxisBindingSource {      // one analog axis -> float
    public AxisBinding(InputDeviceAxis axis);
    public InputDeviceAxis Axis { get; set; }
    public float Deadzone { get; set; }  public bool Invert { get; set; }  public float Scale { get; set; } = 1f;  // processors live here
}
public sealed class CompositeAxisBinding : IAxisBindingSource {   // two buttons -> float [-1,1]; makeup enforced by ctor
    public CompositeAxisBinding(ButtonId negative, ButtonId positive);
    public ButtonId Negative { get; set; }  public ButtonId Positive { get; set; }
}
public sealed class CompositeVectorBinding : IVectorBindingSource {  // four buttons -> LVector2f; makeup enforced by ctor
    public CompositeVectorBinding(ButtonId up, ButtonId down, ButtonId left, ButtonId right);
    public ButtonId Up { get; set; }  public ButtonId Down { get; set; }  public ButtonId Left { get; set; }  public ButtonId Right { get; set; }
}
public sealed class StickBinding : IVectorBindingSource {  // an axis pair -> LVector2f
    public StickBinding(InputDeviceAxis x, InputDeviceAxis y);
    public InputDeviceAxis X { get; set; }  public InputDeviceAxis Y { get; set; }
    public float Deadzone { get; set; }
}

// ---- Action types: the Bindings collection is statically constrained to compatible binding types ----
public abstract class InputAction {                         // common: name, enable
    protected InputAction(string name);
    public string Name { get; }
    public bool Enabled { get; set; } = true;
}
public sealed class ButtonAction : InputAction {           // bool
    public ButtonAction(string name) : base(name);
    public IList<ButtonBinding> Bindings { get; }          // OR'd
    public bool IsPressed { get; }
    public bool WasPressed { get; }  public bool WasReleased { get; }   // polling edges
    public bool HeldFor(float seconds);  public bool Tapped { get; }    // polling conveniences
    public IObservable<Unit> Pressed { get; }  public IObservable<Unit> Released { get; }   // the two edges as observables
}
public sealed class AxisAction : InputAction {             // float
    public AxisAction(string name) : base(name);
    public IList<IAxisBindingSource> Bindings { get; }     // AxisBinding | CompositeAxisBinding
    public float Value { get; }
    public IObservable<float> Changed { get; }             // fires only when Value changes between frames
}
public sealed class VectorAction : InputAction {           // LVector2f
    public VectorAction(string name) : base(name);
    public IList<IVectorBindingSource> Bindings { get; }   // CompositeVectorBinding | StickBinding
    public LVector2f Value { get; }
    public IObservable<LVector2f> Changed { get; }          // fires only when Value changes between frames
}

// Display labels: turn a physical ButtonHandle index into the user's keyboard legend (layout-aware), for HUD / rebind UI.
public interface IButtonLabels { string Label(ButtonId button); }   // main window's ButtonMap.get_mapped_button_label

// (button.Pressed/Released and value.Changed are IObservable<T> on the action itself — see 06; no central bus event)

public static class InputServiceCollectionExtensions {
    public static IServiceCollection AddInput(this IServiceCollection s);   // dataLoop (FrameSlots.DataLoop) + IDevices
    public static IInputContext CreateContext(this IServiceProvider sp, string name, int priority = 0);
}
```

Example:
```csharp
var jump = ctx.Add(new ButtonAction("Jump"));
jump.Bindings.Add(new ButtonBinding(Keys.Space));
jump.Bindings.Add(new ButtonBinding(Gamepad.A));                    // multiple bindings, OR'd
jump.Pressed.Subscribe(_ => DoJump());                             // observable…
// …or poll: if (jump.HeldFor(0.5f)) Charge();   if (jump.WasReleased) …

var move = ctx.Add(new VectorAction("Move"));
move.Bindings.Add(new CompositeVectorBinding(                       // WASD, makeup enforced by the ctor
    Keys.Ascii('w'), Keys.Ascii('s'),
    Keys.Ascii('a'), Keys.Ascii('d')));
move.Bindings.Add(new StickBinding(InputDeviceAxis.LeftX, InputDeviceAxis.LeftY) { Deadzone = 0.15f });
LVector2f dir = move.Value;                                         // poll, or move.Changed.Subscribe(v => …)

// runtime rebind = mutate the list:
if (input.CaptureNext() is { } captured) {                         // ButtonId? — null until the user presses one
    jump.Bindings.Clear();
    jump.Bindings.Add(new ButtonBinding(captured));
}
```

**`dataLoop` registration (abridged).**
```csharp
public static IServiceCollection AddInput(this IServiceCollection s)
{
    s.TryAddSingleton<DataGraph>();
    s.TryAddSingleton<InputRuntime>();
    s.TryAddSingleton<IDevices, Devices>();          // wraps InputDeviceManager (+ hotplug events)
    s.TryAddScoped<IInput, ViewInput>();             // per-view: reads that view's MouseWatcher
    return s.AddFrameTask("dataLoop", FrameSlots.DataLoop, sp =>
    {
        var runtime = sp.GetRequiredService<InputRuntime>();
        var dataGraph = sp.GetRequiredService<DataGraph>();
        var clock = sp.GetRequiredService<IGameClock>();
        return () =>
        {
            dataGraph.Traverse();                 // DataGraphTraverser over the data root
            runtime.Evaluate((float)clock.Dt);    // sample devices → resolve actions → raise events
            return true;                          // stay registered
        };
    });
}
```

**Scoping (devices vs windows vs players).** Mirrors how Unreal scopes one input subsystem per `ULocalPlayer` and leafwing attaches an `InputMap`+`ActionState` per entity:
- The data root (`new NodePath("data")`) is an **engine-wide singleton**; `IDevices` (gamepads) is a **singleton**.
- The mouse/keyboard chain (`MouseAndKeyboard(window,i) → MouseWatcher`) is **per-view scope** — each view has its own `MouseWatcher`, so `IInput` is view-scoped (resolved from `view.Services` — or the `view.Input()` shortcut — via the seeded `ViewContext`, [03](03-rendering.md)) and `MousePosition`/`IsOverUi` answer "in *this* view." Resolving `IInput` from the root provider throws (it has no view); a single-window app reaches its input through `IViewManager.Main.Input()`.
- **Local multiplayer / per-connection**: the framework provides the primitives, not the policy. There is **no `Player` type in the framework** — a game that wants players writes its own (e.g. a `Player` holding a chosen set of native `InputDevice`s plus its own `IInputContext`s), resolved in a per-player DI scope. A `MouseWatcher` can be constrained to a `DisplayRegion` (`set_display_region`) for split-screen. How devices get assigned to players (press-to-join, menu, fixed) is entirely the game's choice.

**Design notes.**
- **Actions are the recommended path; raw polling is the escape hatch.** Most games define contexts and read actions (rebindable, device-agnostic). `IInput` direct polling exists for quick cases and prototypes — the Roaming Ralph port uses a handful of actions, not raw keys.
- **Analog = "every value action has a value," not a separate axis system** (Godot's decision). A button action reads `bool`; an axis reads `float`; a vector reads `LVector2f`. Composite bindings build a vector from four buttons as their own type (`CompositeVectorBinding(up,down,left,right)`) whose constructor enforces the makeup, so users never hand-assemble Unreal-style Swizzle/Negate — a documented Enhanced-Input footgun.
- **Buttons and axes are two systems, mirroring Panda.** Panda registers gamepad digital buttons (`face_a`, `lshoulder`, `dpad_up`, `start`, …) in the *same* `ButtonRegistry` as keyboard/mouse, so a single `ButtonHandle` registry spans all three input kinds; in the framework these handles are wrapped as the typed `ButtonId` struct at the API boundary. Analog inputs are a *separate* `InputDeviceAxis` system (`LeftX/Y`, `LeftTrigger`, `RightX/Y`, `RightTrigger`, plus `Throttle`/`Rudder`/`Wheel`/`Brake` for sim controllers), read as `AxisState` values. Triggers exist as both a digital button (`Gamepad.LeftTrigger`) and an analog axis (`InputDeviceAxis.LeftTrigger`), exactly as Panda exposes them. The platform backends (XInput, evdev with its per-device quirks table, IOKit) already normalize real hardware to these canonical names — Panda is doing the SDL-gamecontroller-style mapping for us, so we don't re-map per controller.
- **Explicit action and binding types carry the guarantees.** `ButtonAction`/`AxisAction`/`VectorAction` each expose a `Bindings` collection statically constrained to compatible binding types, so a vector action can't be handed a button-only binding, and a `CompositeVectorBinding` can't be malformed (its ctor demands four buttons). Bindings are live, mutable state — runtime rebinding is just list mutation (see Rebinding), with no builder and no separate override system.
- **Processors live on the binding that needs them.** Deadzone/invert/scale are properties on `AxisBinding`/`StickBinding` (where an analog signal actually is), not action-level knobs — more correct than a global processor, since a vector action's keyboard composite and its stick want different treatment.
- **No interaction/timing system — that stays in gameplay.** A framework-level tap/hold/multi-tap layer (Unity Interactions / Unreal Triggers) bakes in timing semantics that are really game-design decisions, so it's out. A `ButtonAction` exposes just the honest edges — `Pressed`/`Released` events and `IsPressed`/`WasPressed`/`WasReleased` polling — plus light conveniences `HeldFor(seconds)`/`Tapped` for the common cases. Anything fancier (charge meters, combos, double-tap-dash) is a few lines of gameplay code over those, with `dt` from the clock. Value actions expose `Value` (poll) and a `Changed` event that fires only when the value actually changes between frames.
- **Buttons are a typed `ButtonId`, backed by Panda's own registries.** The `Keys`/`Mouse`/`Gamepad` static vocabularies wrap Panda's `PUBLISHED` registry factories (`KeyboardButton.Space()`/`AsciiKey('w')`, `MouseButton.One()`, `GamepadButton.FaceA()`) into discoverable `ButtonId` values — `Keys.Space`, `Keys.Ascii('w')`, `Mouse.Left`, `Gamepad.A`. `ButtonId` is a small, comparable struct over the (unstable) native registry index, so callers never pass a raw `int`; its registry `Name` is the stable token to persist (reload via `ButtonId.FromName`). Panda's registries stay the source of truth — the vocabularies just surface the named subset (letters/digits have no per-key handle, so `Keys.Ascii` stays open-ended). Bindings/polling are physical (raw); display labels come from `IButtonLabels` over the window's `ButtonMap`.
- **Device pairing is the developer's policy, not ours.** Keeping with the minimally-opinionated principle, the framework does **not** bake in a join flow and does **not** provide a `Player` type. It exposes the primitives — enumerate devices (`IDevices`), hotplug observables (`IDevices.Connected`/`Disconnected`, from Panda's `connect-device`/`disconnect-device`), stable identity (`Name`/`SerialNumber`/`VendorId`:`ProductId`), and per-device polling — and the game writes whatever it wants (press-to-join, menu assignment, fixed P1=keyboard, re-pair by serial on reconnect). Where the docs mention a "`Player`," it is *illustrative game code* grouping some devices and contexts in a per-player scope — one possible shape, not an interface we ship.
- **Contexts switch modes and resolve contention.** Enable/disable a context to switch gameplay↔menu↔vehicle; when two enabled contexts bind the same control, higher `Priority` wins (Unreal IMC model). Switching goes through the owning scope, never a global mutable singleton.
- **UI gating is built in.** A `MouseWatcher` passes button events through *unless* the pointer is over a 2-D region (a `PGItem`), in which case it swallows them — `get_over_region()`/`IsOverUi` expose this. This is the published seam [GUI](10-gui.md) relies on so clicks over widgets don't leak to gameplay; no manual "is the mouse over UI" bookkeeping (the gap Unity and Godot leave to the developer).
- **Observables are official; messenger/`ButtonThrower` stay internal.** `Pressed`/`Released`/`Changed` are `IObservable<T>` on the action (see [06](06-events.md)); consumers never see raw engine strings.
- **Rebinding is data.** Contexts/bindings serialize (Unity `.inputactions` / Godot project-settings precedent); an interactive-rebind helper listens for the next matching control and writes an override persisted separately from defaults. Action→control maps are config, not code.
- **`dataLoop` sort.** Runs at `FrameSlots.DataLoop` (−50) so input is current before the gameplay slot; render (`igLoop`) is last — the point of the sorted-task loop ([02](02-hosting.md)).

**Open items.**
- (none — local-multiplayer player/scope wiring is out of scope; the framework exposes device primitives and a game builds its own.)

> **Verified (1.11 headers):** Keyboard — `KeyboardButton` has individual handles for named keys (`space`, `enter`, `tab`, `escape`, `f1`–`f16`, `left/right/up/down`, `page_up/down`, `home`, `end`, `insert`, `del`, `help`, `menu`, `shift/control/alt/meta` + `l*/r*` variants, `caps_lock`/`num_lock`/`scroll_lock`/`shift_lock`/`print_screen`/`pause`); letters/digits/punctuation come from `ascii_key(char)`, all `PUBLISHED`. Gamepad buttons are `ButtonHandle`s in the shared `ButtonRegistry` (`GamepadButton::face_a/b/c/x/y/z`, `face_1/2`, `lstick`/`rstick`, `lshoulder`/`rshoulder`, `ltrigger`/`rtrigger`, `lgrip`/`rgrip`, `dpad_*`, `back`/`guide`/`start`/`next`/`previous`, `trigger`, `joystick(n)`, `hat_*`). Analog axes are the separate `InputDevice::Axis` enum (`X/Y/Z`, `YAW/PITCH/ROLL`, `LEFT_X/LEFT_Y`, `LEFT_TRIGGER`, `RIGHT_X/RIGHT_Y`, `RIGHT_TRIGGER`, `THROTTLE`, `RUDDER`, `WHEEL`, `ACCELERATOR`, `BRAKE`, `PRESSURE`), read via `find_axis` → `AxisState`. `InputDevice` exposes `name`/`manufacturer`/`serial_number`/`vendor_id`/`product_id`/`device_class`/`connected` as properties; `InputDeviceManager` throws `connect-device`/`disconnect-device` and enumerates via `get_devices(DeviceClass)`. `MouseWatcher` gating (`is_over_region`/`get_over_region`/`set_display_region`/`get_modifier_buttons`) is `PUBLISHED`. The XInput/evdev/IOKit backends normalize hardware to these canonical names (evdev via a per-VID/PID quirks table).

> **Verified — keyboard layout (1.11 headers):** `ButtonEvent` distinguishes `T_down`/`T_up` (mapped/virtual key), `T_raw_down`/`T_raw_up` ("original, untransformed scan key … as if … US qwerty layout"), and `T_keystroke` (a Unicode `keycode` for typing, not a `ButtonHandle`). `GraphicsWindow.get_keyboard_map()` returns a `ButtonMap` (PUBLISHED) mapping raw→mapped buttons with `get_mapped_button(raw)` and human-readable `get_mapped_button_label(raw)` — the layout-aware label source for rebinding UIs. This is Panda's scancode-vs-keycode equivalent; we bind WASD physically and label via the map.

**See also.** [02 Hosting](02-hosting.md) (`dataLoop`, sort, ordering); [06 Events](06-events.md) (the pump the device observables ride); [03 Rendering](03-rendering.md) (per-view `IGraphicsWindow`, `DisplayRegion`); [10 GUI](10-gui.md) (UI gating via `MouseWatcher` regions).
