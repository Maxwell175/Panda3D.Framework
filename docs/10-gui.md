# 10 — GUI (`Panda3D.Framework.Gui`)

**Purpose.** A modular UI subsystem built **directly on Panda's published C++ PGui classes** (`PGTop`, `PGButton`, `PGEntry`, `PGSliderBar`, `PGScrollFrame`, `PGWaitBar`, `PGFrameStyle`). It does **not** port DirectGUI's Python widget layer — it wraps the same mature C++ primitives DirectGUI sits on, the path the maintainers themselves recommend (issue #1636: PGui *"is mature [and] implements virtually all of the functionality of DirectGUI"*). Widgets are explicit classes in the construct-then-`Add` style of [05](05-input.md)/[08](08-intervals.md). The layer's **central commitment: PGui's string-event callbacks never reach consumers** — every PGui interaction event is surfaced as a typed C# `IObservable<T>` on the widget, and the string layer exists only inside widget implementations. That, plus scoped disposal, is what the wrapper provides; frame/state/style/focus remain the native `PGItem` surface.

**Replaces in `direct`.** `direct.gui.DirectGui`. Note the split: PGui provides the **primitives** (button, entry, slider, scroll bar, scroll frame, progress bar, frame styles); DirectGUI's **composites** (`DirectDialog`, `DirectOptionMenu`, `DirectScrolledList`, `DirectCheckButton`) have no PGui class — they're compositions, and here they're documented recipes rather than shipped classes (see Design notes).

**Dependencies.** `Abstractions`; `Rendering` (the view's `Overlay2d`/`Pixel2d` roots — which **are `PGTop` nodes wired to the view's `MouseWatcher`**, see [03](03-rendering.md)); `Events` (widget observables ride the pump); the fork's C# bindings. Optional: [12](12-audio-misc.md) audio for widget sounds via the native `PGItem.set_sound`.

**Public surface.**
```csharp
public interface IGui {                                        // per-view: resolve from view.Services (root = main view)
    IView View { get; }                                        // the view this gui belongs to
    T Add<T>(T widget, NodePath? parent = null) where T : Widget;    // materialize + wire; default parent = View.Overlay2d
    // parent to View.Pixel2d for pixel-exact layout, or to one of the view's edge-anchor nodes (03)
}

public abstract class Widget : IDisposable {
    public NodePath Node { get; }                              // the PGItem's node: position/scale/parent like any node
    public PGItem Item { get; }                                // NATIVE surface: frame, states, frame_style, focus, set_sound
    public bool Visible { get; set; }
    public bool Enabled { get; set; }                          // set_active: false = the inactive state (greyed, no input)
    // The COMPLETE PGItem event set, typed (no string event ever reaches a consumer):
    public IObservable<Unit> Entered { get; }                  // enter: pointer over, topmost
    public IObservable<Unit> Exited { get; }                   // exit
    public IObservable<Unit> Within { get; }                   // within/without: like enter/exit but ignoring occlusion
    public IObservable<Unit> Without { get; }
    public IObservable<Unit> Pressed { get; }                  // press/release, merged across registered buttons
    public IObservable<Unit> Released { get; }
    public IObservable<bool> FocusChanged { get; }             // focus_in/focus_out (PGItem.set_focus is the setter)
    public void SetRolloverSound(AudioSound sound);            // Item.set_sound(enter event) — typed, no string
}
public abstract class Widget<TItem> : Widget where TItem : PGItem {
    public new TItem Item { get; }                             // the CONCRETE native (PGButton, PGEntry, …) — full escape hatch
}

public sealed class Button : Widget<PGButton> {
    public Button(string label, float bevel = 0.1f);                                    // PGButton.setup(label, bevel)
    public Button(NodePath ready, NodePath depressed, NodePath rollover, NodePath inactive);      // custom 4-state
    public IObservable<Unit> Clicked { get; }                  // merged click events for all registered click buttons
    public void AddClickButton(ButtonHandle b);                // default: mouse one; wires the typed stream for that click event
    public void SetClickSound(AudioSound sound);               // Item.set_sound(click event) — typed, no string
}
public sealed class Entry : Widget<PGEntry> {
    public Entry(float width, int lines = 1);                  // PGEntry.setup
    public string Text { get; set; }                           // get_plain_text / set_text
    public int CursorPosition { get; set; }                    // cursor_position property (character index)
    public LPoint2f CursorScreenPos { get; }                   // get_cursor_X/Y — place IME candidates / tooltips at the caret
    public int MaxChars { get; set; }  public float MaxWidth { get; set; }  public int NumLines { get; set; }
    public bool ObscureMode { get; set; }                      // passwords
    public bool AcceptEnabled { get; set; }                    // whether Enter fires Submitted (set_accept_enabled)
    public bool CursorKeysActive { get; set; }                 // arrow/home/end handling
    public float BlinkRate { get; set; }
    public void Focus();                                       // Item.set_focus(true)
    public IObservable<string> Submitted { get; }              // accept event (payload: current text)
    public IObservable<string> SubmitFailed { get; }           // accept_failed event
    public IObservable<string> Changed { get; }                // type + erase events
    public IObservable<Unit> Overflowed { get; }               // overflow event (input beyond max_chars)
    public IObservable<int> CursorMoved { get; }               // cursormove event: current cursor position
}
public sealed class Slider : Widget<PGSliderBar> {
    public Slider(bool vertical, float length, float width, float bevel = 0.05f);      // PGSliderBar.setup_slider
    public float Min { get; set; }  public float Max { get; set; }  public float Value { get; set; }   // set_range / value
    public float Ratio { get; set; }                           // normalized 0..1 (ratio property)
    public bool IsDragging { get; }                            // is_button_down: thumb currently held
    public IObservable<float> ValueChanged { get; }            // adjust event
}
public sealed class ScrollBar : Widget<PGSliderBar> {          // PGSliderBar.setup_scroll_bar
    public ScrollBar(bool vertical, float length, float width, float bevel = 0.05f);
    public float PageSize { get; set; }  public float ScrollSize { get; set; }  public float Value { get; set; }
    public float Ratio { get; set; }  public bool IsDragging { get; }
    public bool ResizeThumb { get; set; }                      // thumb length tracks page/range
    public IObservable<float> ValueChanged { get; }
    // custom pieces (thumb/left/right PGButtons) and arbitrary axis: via Item (set_thumb_button, set_axis, …)
}
public sealed class ScrollFrame : Widget<PGScrollFrame> {      // PGScrollFrame (manages its own PGSliderBars)
    public ScrollFrame(float width, float height, float sliderWidth = 0.08f, float bevel = 0.05f);  // setup(...)
    public NodePath Canvas { get; }                            // get_canvas_node: parent scrolled content here
    public LVecBase4f VirtualFrame { get; set; }               // the scrollable extent (virtual_frame)
    public bool AutoHide { get; set; }                         // hide sliders when content fits
    public IPGSliderBar? HorizontalSlider { get; }             // the managed piece sliders (native)
    public IPGSliderBar? VerticalSlider { get; }
    public IObservable<LVecBase2f> Scrolled { get; }           // (hRatio, vRatio) from the piece sliders' adjust events
}
public sealed class ProgressBar : Widget<PGWaitBar> {          // PGWaitBar
    public ProgressBar(float width, float height, float range = 100f);
    public float Value { get; set; }  public float Range { get; set; }
    public float Percent { get; }                              // get_percent
}
// Non-interactive text is NOT a PGItem: a Label is a thin TextNode holder (the OnscreenText analog).
public sealed class Label : IDisposable {
    public Label(string text);
    public NodePath Node { get; }  public TextNode TextNode { get; }     // native: align, color, scale, wordwrap
    public string Text { get; set; }
}

public static class GuiServiceCollectionExtensions { public static IServiceCollection AddGui(this IServiceCollection s); }
```

**Usage.**
```csharp
var gui = view.Services.GetRequiredService<IGui>();            // per-view (03); at root this is the main view's gui

var play  = gui.Add(new Button("Play"));
play.Node.SetPos(0, 0, -0.2f);
screen.Add(play.Clicked.Subscribe(_ => StartGame()));          // screen: Rx CompositeDisposable, per-screen scope

var name  = gui.Add(new Entry(width: 12));
name.Submitted.Subscribe(text => Join(text));

var volume = gui.Add(new Slider(vertical: false, length: 0.8f, width: 0.08f) { Min = 0, Max = 1, Value = 0.7f });
volume.ValueChanged.Subscribe(v => audio.Sfx.SetVolume(v));    // native manager volume (12)
```

**Design notes.**
- **Why a `Widget` wrapper passes the bar.** PGui raises interactions as *named string events* whose names are per-item and per-mouse-button (`get_click_event(ButtonHandle)`, `get_accept_event(...)`, `get_adjust_event`, `get_enter/exit/focus_in/focus_out_event`). A widget subscribes to its own names through the [Events](06-events.md) pump, projects payloads, and owns the resulting `Subject`s — state a binding interface cannot hold. **Coverage is total:** every event a PGui class can raise has a corresponding typed observable on its widget (the base `PGItem` set on `Widget`; each subclass's additions on the subclass), so no consumer ever subscribes to a string. Disposal unhooks those subscriptions and removes the node (and its `get_region()` mouse hook). Everything else — frame, state defs, frame styles, active, focus, sounds — is the **native `Item`** surface, deliberately unwrapped.
- **Initialization: a gui belongs to a view via the seeded scope.** `AddGui` registers `IGui` as a *scoped* service whose constructor takes the scope's `ViewContext` ([03](03-rendering.md)) plus `INamedEventBus` — so `view.Services.GetRequiredService<IGui>()` yields the gui wired to that view's `Overlay2d`/`MouseWatcher`, and disposing the view disposes its widgets with it. At the root provider the fallback is the main view, so prototypes never touch scopes.
- **`Add` materializes; the overlay root is already a `PGTop`.** PGui items only receive input when they live under a `PGTop` bound to a `MouseWatcher` — and following ShowBase exactly (`aspect2d = attachNewNode(PGTop(...))`; `aspect2d.node().setMouseWatcher(mw)`), the view's `Overlay2d`/`Pixel2d` roots **are** such `PGTop`s ([03](03-rendering.md)). So `IGui.Add` just parents the widget's node under the chosen root and wires its observables; there is no separate "gui root" object. A second window's views work identically — their own `PGTop`s, their own `MouseWatcher`.
- **Each widget carries its control's full working surface.** The rule applied per class (audited against the full PUBLISHED member list): the control's *function* — value, range, ratio, cursor position, text limits, focus, accept behavior, scroll extent — is a typed property on the widget; *styling and exotica* (frame styles, cursor geometry via `cursor_def`, IME candidate text properties, wtext, custom slider pieces, arbitrary slider axis) go through the typed `Item`, which `Widget<TItem>` exposes as the concrete binding type (`PGEntry`, `PGSliderBar`, …), never just base `PGItem`.
- **Clicks are per-mouse-button.** `PGButton` names a distinct click event per `ButtonHandle`; `Clicked` merges the streams registered with `AddClickButton` (default: mouse one). Current caveat: the generated native `PGButton.add_click_button(ButtonHandle)` overload aborts in this binding build, so `AddClickButton` avoids that call and wires the typed managed stream for the event name. Extra physical click buttons need the native binding fixed before PGui will actually emit those additional click events.
- **Focus and text entry are native.** Keyboard focus is `PGItem.set_focus` (`Entry.Focus()` is sugar); a focused `PGEntry` consumes keystrokes through the data graph's **keystroke tier** ([05](05-input.md)) — no `ButtonThrower`, and gameplay bindings (physical tier) keep working while typing only where you want them (use `IsOverUi`/focus checks in gameplay contexts). No tab-order manager in v1 — call `Focus()` from your own navigation logic.
- **Layout is parent choice, not a layout engine.** `Overlay2d` for aspect-stable coordinates, `Pixel2d` for pixel-exact, the view's edge-anchor nodes ([03](03-rendering.md)) for corner/edge-pinned HUD — position with ordinary node transforms and `Item.SetFrame`. A constraint/flex layout system is deliberately out (bring your own on top if needed).
- **Composites are recipes, not classes (v1).** Check button = a `Button` whose click toggles between two state defs; dialog = a framed node + `Button`s in a modal per-dialog scope (dispose the scope to tear it down); dropdown = a `Button` opening a popup frame of `Button`s; scrolled list = a `ScrollFrame` whose `Canvas` holds item widgets. Shipping composite classes waits for real usage to show the right shapes — the primitives make each a page of app code.
- **Widget sounds — typed helpers over the native mechanism.** The common cases are string-free: `SetRolloverSound`/`SetClickSound` resolve the event name internally and call `Item.set_sound`. The native `Item.SetSound(eventName, sound)` remains as the escape hatch for exotic per-event sounds; audio objects come from [12](12-audio-misc.md).
- **Styling.** Per-state visuals via `Item.SetFrameStyle(state, PGFrameStyle)` — flat/bevel-in/bevel-out/groove/ridge/texture-border types, colors, widths — or hand any `NodePath` as a state def (the custom-geometry `Button` ctor). All native.

**Open items.**
- Runtime check: focused `PGEntry` keystroke consumption in *our* data-graph setup (no `ButtonThrower` present) — expected to work via the watcher group, confirm once running.

> **Verified:** `GuiTests` cover the base `PGItem` events, button click events, entry events, slider/scroll events, event-name uniqueness across controls with duplicate names, and visible offscreen rendering of GUI elements.

> **Verified (1.11 headers + ShowBase.py):** `PGTop.set_mouse_watcher(watcher)`/`get_group` PUBLISHED; ShowBase creates `aspect2d`/`pixel2d` as `PGTop` nodes and calls `setMouseWatcher(mw)` on both. `PGItem` PUBLISHED: `set_frame`, `set_state`/`get_state_def`(+seq)/`set_frame_style`, `set_active`, `set_focus`/`get_focus`, `set_sound(event, AudioSound)`, `get_region`, and event-name getters `get_enter/exit/within/without/focus_in/focus_out/press/release_event` (+static prefixes). `PGButton`: `setup(label, bevel)` and 4-state `setup(ready, depressed, rollover, inactive)`, `add_click_button(ButtonHandle)`, `get_click_event(ButtonHandle)`/`click_prefix`. `PGEntry`: `setup(width, num_lines)`/`setup_minimal`, `set_text`/`get_plain_text`/`get_text`, `max_chars`/`max_width`/`obscure_mode` properties, event getters `get_accept_event(ButtonHandle)`/`accept_failed`/`overflow`/`type`/`erase`/`cursormove`. `PGSliderBar`: `setup_slider(vertical, length, width, bevel)` / `setup_scroll_bar(...)`, `set_range(min,max)`/`set_value`, `page_size`/`scroll_size` properties, adjust-event prefix. `PGWaitBar`: `setup(width, height, range)`, `range`/`value` properties, `get_percent`. Detail audit: `PGEntry` also publishes `cursor_position` (get/set property), `get_cursor_X`/`get_cursor_Y`, `num_lines`/`blink_rate`/`cursor_keys_active`/`overflow_mode`/`accept_enabled` (via `set_accept_enabled`), `cursor_def` (caret `NodePath`), IME `candidate_active`/`candidate_inactive`, and `wtext` variants. `PGSliderBar` also publishes `ratio` (normalized), `is_button_down`, `resize_thumb`, `manage_pieces`, `set_axis`, and settable thumb/left/right `PGButton` pieces. `PGScrollFrame` publishes `setup(width, height, …, slider_width, bevel)`, `virtual_frame`, `manage_pieces`, `auto_hide`, and `get_horizontal_slider`/`get_vertical_slider`; it inherits `PGVirtualFrame`'s `clip_frame` and canvas node. `PGFrameStyle.Type`: `T_flat`, `T_bevel_out`, `T_bevel_in`, `T_groove`, `T_ridge`, `T_texture_border`.

**See also.** [03 Rendering](03-rendering.md) (overlay roots are `PGTop`s; edge anchors); [05 Input](05-input.md) (`IsOverUi` gating; keystroke tier); [06 Events](06-events.md) (the pump the widget observables ride); [12 Audio & Misc](12-audio-misc.md) (widget sounds).
