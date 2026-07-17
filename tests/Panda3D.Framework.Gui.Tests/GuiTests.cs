using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Rendering;
using Panda3D.Framework.Scheduling;
using Panda3D.Framework.VisualTestSupport;
using Xunit;

namespace Panda3D.Framework.Gui.Tests;

public sealed class GuiTests
{
    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    sealed class Probe
    {
        public bool ScopedToView;
        public bool ButtonParented;
        public bool ButtonState;
        public bool EntryState;
        public bool SliderState;
        public bool ScrollFrameState;
        public bool ProgressState;
        public bool LabelParented;
        public bool EventDelivery;
        public string EventDetails = string.Empty;
        public bool AllEventDelivery;
        public string AllEventDetails = string.Empty;
        public bool EventNamesIsolated;
        public string EventNameDetails = string.Empty;
        public int GuiPixels;
    }

    static ViewOptions Offscreen() => new() { Offscreen = true };

    static bool SameNode(NodePath a, NodePath b) => a.Node().Equals(b.Node());

    static void RunInLoop(Probe probe, Func<IServiceProvider, Probe, PandaTask> body)
    {
        string? frameError = null;
        void OnError(Exception ex) => frameError = ex.ToString();
        FrameTaskDiagnostics.UnhandledException += OnError;
        try
        {
            var builder = GameApplication.CreateBuilder(Array.Empty<string>());
            builder.Services.AddSceneManager();
            builder.Services.AddEvents();
            builder.Services.AddClock();
            builder.Services.AddScheduler();
            builder.Services.AddRendering();
            builder.Services.AddGui();
            builder.Services.AddSingleton(probe);
            builder.Services.AddSingleton<IBootstrap>(sp =>
                new DelegateBootstrap(() => body(sp, sp.GetRequiredService<Probe>())));

            builder.Build().Run();
        }
        finally
        {
            FrameTaskDiagnostics.UnhandledException -= OnError;
        }

        Assert.Null(frameError);
    }

    [Fact]
    public void WidgetsAttachToViewOverlayAndProxyNativeState()
    {
        var probe = new Probe();

        RunInLoop(probe, async (sp, p) =>
        {
            var views = sp.GetRequiredService<IViewManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var view = views.OpenView(Offscreen());
            var gui = view.Services.GetRequiredService<IGui>();

            p.ScopedToView = ReferenceEquals(view, gui.View);

            var button = gui.Add(new Button("Play", name: "play-button"));
            button.Enabled = false;
            button.Visible = false;
            p.ButtonParented = SameNode(button.Node.GetParent(), view.Overlay2d!);
            p.ButtonState = !button.Item.GetActive() && button.Node.IsHidden();

            var entry = gui.Add(new Entry(width: 8f, numLines: 1, name: "name-entry"));
            entry.Text = "Panda";
            entry.CursorPosition = 3;
            entry.MaxChars = 12;
            entry.AcceptEnabled = false;
            p.EntryState = entry.Item.GetPlainText() == "Panda"
                           && entry.Item.CursorPosition == 3
                           && entry.Item.MaxChars == 12
                           && !entry.AcceptEnabled;

            var slider = gui.Add(new Slider(min: -1f, max: 1f, value: 0.25f, name: "gain"));
            slider.Value = 0.5f;
            p.SliderState = Math.Abs(slider.Item.Value - 0.5f) < 0.001f
                            && slider.Range.Min < -0.99f
                            && slider.Range.Max > 0.99f;

            var scrollFrame = gui.Add(new ScrollFrame(
                width: 1f,
                height: 0.7f,
                left: -0.5f,
                right: 1.5f,
                bottom: -0.5f,
                top: 1.5f,
                name: "inventory-scroll"));
            scrollFrame.AutoHide = true;
            p.ScrollFrameState = scrollFrame.AutoHide && !scrollFrame.Canvas.IsEmpty();

            var progress = gui.Add(new ProgressBar(width: 1f, height: 0.1f, range: 100f, name: "load"));
            progress.Value = 25f;
            p.ProgressState = Math.Abs(progress.Percent - 25f) < 0.001f;

            var label = gui.Add(new Label("Ready", "ready-label"));
            label.Node.SetPos(-0.7f, 0f, 0.1f);
            label.Node.SetScale(0.08f);
            p.LabelParented = SameNode(label.Node.GetParent(), view.Overlay2d!) && label.Text == "Ready";

            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            views.CloseView(view);
            life.StopApplication();
        });

        Assert.True(probe.ScopedToView, "IGui should resolve from the view scope it belongs to");
        Assert.True(probe.ButtonParented, "widgets should parent under the view overlay by default");
        Assert.True(probe.ButtonState, "Button should proxy visible/enabled state to native PGui");
        Assert.True(probe.EntryState, "Entry should proxy text, cursor, max length, and accept state");
        Assert.True(probe.SliderState, "Slider should proxy range and value to PGSliderBar");
        Assert.True(probe.ScrollFrameState, "ScrollFrame should expose native canvas and auto-hide state");
        Assert.True(probe.ProgressState, "ProgressBar percent should reflect native PGWaitBar value/range");
        Assert.True(probe.LabelParented, "labels should parent under the same view overlay");
    }

    [Fact]
    public void NativeGuiEventNamesDriveWidgetObservables()
    {
        var probe = new Probe();

        RunInLoop(probe, async (sp, p) =>
        {
            var views = sp.GetRequiredService<IViewManager>();
            var bus = sp.GetRequiredService<INamedEventBus>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var view = views.OpenView(Offscreen());
            var gui = view.Services.GetRequiredService<IGui>();

            var button = gui.Add(new Button("Click", name: "click-button"));
            var entry = gui.Add(new Entry(width: 8f, name: "chat-entry"));
            var slider = gui.Add(new Slider(0f, 10f, 2f, name: "volume-slider"));

            int entered = 0, clicked = 0, changed = 0, submitted = 0, valueChanged = 0;
            var focus = new List<bool>();

            using var s1 = button.Entered.Subscribe(_ => entered++);
            using var s2 = button.FocusChanged.Subscribe(focus.Add);
            using var s3 = button.Clicked.Subscribe(_ => clicked++);
            using var s4 = entry.Changed.Subscribe(_ => changed++);
            using var s5 = entry.Submitted.Subscribe(_ => submitted++);
            using var s6 = slider.ValueChanged.Subscribe(_ => valueChanged++);

            entry.Text = "hello";
            slider.Value = 6f;

            var enter = new ButtonHandle("enter");
            bus.Send(button.Item.GetEnterEvent());
            bus.Send(button.Item.GetFocusInEvent());
            bus.Send(button.Item.GetFocusOutEvent());
            bus.Send(button.GetClickEvent(button.PrimaryButton));
            bus.Send(entry.Item.GetTypeEvent());
            bus.Send(entry.GetAcceptEvent(enter));
            bus.Send(slider.Item.GetAdjustEvent());

            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            p.EventDelivery = entered == 1
                              && clicked == 1
                              && changed == 1
                              && submitted == 1
                              && valueChanged >= 1
                              && focus.Count == 2
                              && focus[0]
                              && !focus[1];
            p.EventDetails = $"entered={entered}, clicked={clicked}, changed={changed}, submitted={submitted}, valueChanged={valueChanged}, focus=[{string.Join(",", focus)}]";

            views.CloseView(view);
            life.StopApplication();
        });

        Assert.True(probe.EventDelivery, $"PGui native event names should flow through typed widget observables ({probe.EventDetails})");
    }

    [Fact]
    public void AllGeneratedGuiEventsDriveExpectedObservables()
    {
        var probe = new Probe();

        RunInLoop(probe, async (sp, p) =>
        {
            var views = sp.GetRequiredService<IViewManager>();
            var bus = sp.GetRequiredService<INamedEventBus>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var view = views.OpenView(Offscreen());
            var gui = view.Services.GetRequiredService<IGui>();

            var button = gui.Add(new Button("All", name: "all-events-button"));
            var entry = gui.Add(new Entry(width: 8f, name: "all-events-entry"));
            var slider = gui.Add(new Slider(0f, 10f, 2f, name: "all-events-slider"));
            var scrollBar = gui.Add(new ScrollBar(name: "all-events-scrollbar"));
            var scrollFrame = gui.Add(new ScrollFrame(
                width: 1f,
                height: 0.7f,
                left: -0.5f,
                right: 1.5f,
                bottom: -0.5f,
                top: 1.5f,
                name: "all-events-frame"));
            entry.Text = "ready";
            entry.CursorPosition = 3;

            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            int entered = 0, exited = 0, within = 0, without = 0, pressed = 0, released = 0;
            int focusIn = 0, focusOut = 0, clicked = 0;
            int submitted = 0, submitFailed = 0, changed = 0, overflowed = 0, cursorMoved = 0;
            int sliderChanged = 0, scrollBarChanged = 0, scrolled = 0;

            using var b1 = button.Entered.Subscribe(_ => entered++);
            using var b2 = button.Exited.Subscribe(_ => exited++);
            using var b3 = button.Within.Subscribe(_ => within++);
            using var b4 = button.Without.Subscribe(_ => without++);
            using var b5 = button.Pressed.Subscribe(_ => pressed++);
            using var b6 = button.Released.Subscribe(_ => released++);
            using var b7 = button.FocusChanged.Subscribe(f => { if (f) focusIn++; else focusOut++; });
            using var b8 = button.Clicked.Subscribe(_ => clicked++);
            using var e1 = entry.Submitted.Subscribe(_ => submitted++);
            using var e2 = entry.SubmitFailed.Subscribe(_ => submitFailed++);
            using var e3 = entry.Changed.Subscribe(_ => changed++);
            using var e4 = entry.Overflowed.Subscribe(_ => overflowed++);
            using var e5 = entry.CursorMoved.Subscribe(_ => cursorMoved++);
            using var s1 = slider.ValueChanged.Subscribe(_ => sliderChanged++);
            using var sb1 = scrollBar.ValueChanged.Subscribe(_ => scrollBarChanged++);
            using var sf1 = scrollFrame.Scrolled.Subscribe(_ => scrolled++);

            var enter = new ButtonHandle("enter");

            bus.Send(button.Item.GetEnterEvent());
            bus.Send(button.Item.GetExitEvent());
            bus.Send(button.Item.GetWithinEvent());
            bus.Send(button.Item.GetWithoutEvent());
            bus.Send(button.GetPressEvent(button.PrimaryButton));
            bus.Send(button.GetReleaseEvent(button.PrimaryButton));
            bus.Send(button.Item.GetFocusInEvent());
            bus.Send(button.Item.GetFocusOutEvent());
            bus.Send(button.GetClickEvent(button.PrimaryButton));

            bus.Send(entry.GetAcceptEvent(enter));
            bus.Send(entry.GetAcceptFailedEvent(enter));
            bus.Send(entry.Item.GetTypeEvent());
            bus.Send(entry.Item.GetEraseEvent());
            bus.Send(entry.Item.GetOverflowEvent());
            bus.Send(entry.Item.GetCursormoveEvent());

            bus.Send(slider.Item.GetAdjustEvent());
            bus.Send(scrollBar.Item.GetAdjustEvent());
            bus.Send(scrollFrame.HorizontalSlider.GetAdjustEvent());
            bus.Send(scrollFrame.VerticalSlider.GetAdjustEvent());

            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            p.AllEventDelivery = entered == 1
                                 && exited == 1
                                 && within == 1
                                 && without == 1
                                 && pressed == 1
                                 && released == 1
                                 && focusIn == 1
                                 && focusOut == 1
                                 && clicked == 1
                                 && submitted == 1
                                 && submitFailed == 1
                                 && changed == 2
                                 && overflowed == 1
                                 && cursorMoved == 1
                                 && sliderChanged == 1
                                 && scrollBarChanged == 1
                                 && scrolled == 2;
            p.AllEventDetails =
                $"entered={entered}, exited={exited}, within={within}, without={without}, pressed={pressed}, released={released}, " +
                $"focusIn={focusIn}, focusOut={focusOut}, clicked={clicked}, submitted={submitted}, submitFailed={submitFailed}, " +
                $"changed={changed}, overflowed={overflowed}, cursorMoved={cursorMoved}, sliderChanged={sliderChanged}, " +
                $"scrollBarChanged={scrollBarChanged}, scrolled={scrolled}";

            views.CloseView(view);
            life.StopApplication();
        });

        Assert.True(probe.AllEventDelivery, $"all generated GUI events should drive their expected observable ({probe.AllEventDetails})");
    }

    [Fact]
    public void GeneratedEventNamesDoNotCollideBetweenControls()
    {
        var probe = new Probe();

        RunInLoop(probe, async (sp, p) =>
        {
            var views = sp.GetRequiredService<IViewManager>();
            var bus = sp.GetRequiredService<INamedEventBus>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var view = views.OpenView(Offscreen());
            var gui = view.Services.GetRequiredService<IGui>();

            var buttonA = gui.Add(new Button("A", name: "duplicate-name"));
            var buttonB = gui.Add(new Button("B", name: "duplicate-name"));
            var entry = gui.Add(new Entry(width: 8f, name: "duplicate-name"));
            var slider = gui.Add(new Slider(name: "duplicate-name"));
            var scrollBar = gui.Add(new ScrollBar(name: "duplicate-name"));
            var scrollFrame = gui.Add(new ScrollFrame(
                width: 1f,
                height: 0.7f,
                left: -0.5f,
                right: 1.5f,
                bottom: -0.5f,
                top: 1.5f,
                name: "duplicate-name"));
            var enter = new ButtonHandle("enter");

            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            string? duplicate = null;
            void AddEvent(string owner, string eventName)
            {
                if (duplicate is not null) return;
                if (seen.TryGetValue(eventName, out var previous))
                {
                    duplicate = $"{owner} collided with {previous}: {eventName}";
                    return;
                }
                seen.Add(eventName, owner);
            }

            AddWidgetEvents("buttonA", buttonA);
            AddEvent("buttonA.click", buttonA.GetClickEvent(buttonA.PrimaryButton));
            AddWidgetEvents("buttonB", buttonB);
            AddEvent("buttonB.click", buttonB.GetClickEvent(buttonB.PrimaryButton));
            AddWidgetEvents("entry", entry);
            AddEvent("entry.accept", entry.GetAcceptEvent(enter));
            AddEvent("entry.acceptFailed", entry.GetAcceptFailedEvent(enter));
            AddEvent("entry.type", entry.Item.GetTypeEvent());
            AddEvent("entry.erase", entry.Item.GetEraseEvent());
            AddEvent("entry.overflow", entry.Item.GetOverflowEvent());
            AddEvent("entry.cursorMove", entry.Item.GetCursormoveEvent());
            AddWidgetEvents("slider", slider);
            AddEvent("slider.adjust", slider.Item.GetAdjustEvent());
            AddWidgetEvents("scrollBar", scrollBar);
            AddEvent("scrollBar.adjust", scrollBar.Item.GetAdjustEvent());
            AddWidgetEvents("scrollFrame", scrollFrame);
            AddEvent("scrollFrame.horizontalAdjust", scrollFrame.HorizontalSlider.GetAdjustEvent());
            AddEvent("scrollFrame.verticalAdjust", scrollFrame.VerticalSlider.GetAdjustEvent());

            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            int buttonAClicked = 0, buttonBClicked = 0, buttonAReleased = 0, buttonBReleased = 0, entrySubmitted = 0;
            using var a1 = buttonA.Clicked.Subscribe(_ => buttonAClicked++);
            using var b1 = buttonB.Clicked.Subscribe(_ => buttonBClicked++);
            using var a2 = buttonA.Released.Subscribe(_ => buttonAReleased++);
            using var b2 = buttonB.Released.Subscribe(_ => buttonBReleased++);
            using var e1 = entry.Submitted.Subscribe(_ => entrySubmitted++);

            bus.Send(buttonA.GetClickEvent(buttonA.PrimaryButton));
            bus.Send(buttonB.GetReleaseEvent(buttonB.PrimaryButton));
            bus.Send(entry.GetAcceptEvent(enter));

            for (int i = 0; i < 3; i++)
                await PandaTask.NextFrame();

            bool dispatchIsolated = buttonAClicked == 1
                                    && buttonBClicked == 0
                                    && buttonAReleased == 0
                                    && buttonBReleased == 1
                                    && entrySubmitted == 1;

            p.EventNamesIsolated = duplicate is null && dispatchIsolated;
            p.EventNameDetails = duplicate
                                 ?? $"dispatch buttonAClicked={buttonAClicked}, buttonBClicked={buttonBClicked}, " +
                                    $"buttonAReleased={buttonAReleased}, buttonBReleased={buttonBReleased}, entrySubmitted={entrySubmitted}";

            views.CloseView(view);
            life.StopApplication();

            void AddWidgetEvents(string owner, Widget widget)
            {
                AddEvent($"{owner}.enter", widget.Item.GetEnterEvent());
                AddEvent($"{owner}.exit", widget.Item.GetExitEvent());
                AddEvent($"{owner}.within", widget.Item.GetWithinEvent());
                AddEvent($"{owner}.without", widget.Item.GetWithoutEvent());
                AddEvent($"{owner}.press", widget.GetPressEvent(widget.PrimaryButton));
                AddEvent($"{owner}.release", widget.GetReleaseEvent(widget.PrimaryButton));
                AddEvent($"{owner}.focusIn", widget.Item.GetFocusInEvent());
                AddEvent($"{owner}.focusOut", widget.Item.GetFocusOutEvent());
            }
        });

        Assert.True(probe.EventNamesIsolated, $"GUI event names should be unique per control and dispatch should not bleed ({probe.EventNameDetails})");
    }

    [Fact]
    public void GuiElementsRenderIntoOffscreenOverlay()
    {
        var probe = new Probe();

        RunInLoop(probe, async (sp, p) =>
        {
            var views = sp.GetRequiredService<IViewManager>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var view = views.OpenView(Offscreen());
            var gui = view.Services.GetRequiredService<IGui>();

            VisualTestHelpers.UseBlackBackground(view);

            var label = gui.Add(new Label("GUI", "rendered-label"));
            label.TextNode.SetTextColor(1f, 1f, 1f, 1f);
            label.Node.SetPos(-0.35f, 0f, 0f);
            label.Node.SetScale(0.18f);

            var progress = gui.Add(new ProgressBar(width: 0.9f, height: 0.15f, range: 1f, name: "rendered-progress"));
            progress.Node.SetPos(-0.45f, 0f, -0.25f);
            progress.Value = 1f;

            for (int i = 0; i < 8; i++)
                await PandaTask.NextFrame();

            p.GuiPixels = VisualTestHelpers.CountBrightPixels(VisualTestHelpers.Capture(view), threshold: 0.6f);

            views.CloseView(view);
            life.StopApplication();
        });

        Assert.True(probe.GuiPixels > 20, "GUI overlay should render visible pixels into the offscreen buffer");
    }
}
