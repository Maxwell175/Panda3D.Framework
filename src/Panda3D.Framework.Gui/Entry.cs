using System;
using System.Reactive;
using System.Reactive.Subjects;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Gui;

public sealed class Entry : Widget<PGEntry>
{
    readonly Subject<string> _submitted = new();
    readonly Subject<string> _submitFailed = new();
    readonly Subject<string> _changed = new();
    readonly Subject<Unit> _overflowed = new();
    readonly Subject<int> _cursorMoved = new();
    readonly ButtonHandle _acceptButton;
    bool _acceptEnabled = true;

    public Entry(float width = 10f, int numLines = 1, string name = "entry", bool minimal = false)
        : base(new PGEntry(name))
    {
        if (minimal) Item.SetupMinimal(width, numLines);
        else Item.Setup(width, numLines);
        _acceptButton = new ButtonHandle("enter");
    }

    public string Text
    {
        get => Item.GetPlainText();
        set => Item.SetText(value);
    }

    public int CursorPosition
    {
        get => Item.CursorPosition;
        set => Item.CursorPosition = value;
    }

    public LPoint2f CursorScreenPos => new(Item.GetCursorX(), Item.GetCursorY());

    public int MaxChars
    {
        get => Item.MaxChars;
        set => Item.MaxChars = value;
    }

    public float MaxWidth
    {
        get => Item.MaxWidth;
        set => Item.MaxWidth = value;
    }

    public int NumLines
    {
        get => Item.NumLines;
        set => Item.NumLines = value;
    }

    public bool ObscureMode
    {
        get => Item.ObscureMode;
        set => Item.ObscureMode = value;
    }

    public bool AcceptEnabled
    {
        get => _acceptEnabled;
        set
        {
            Item.SetAcceptEnabled(value);
            _acceptEnabled = value;
        }
    }

    public bool CursorKeysActive
    {
        get => Item.CursorKeysActive;
        set => Item.CursorKeysActive = value;
    }

    public float BlinkRate
    {
        get => Item.BlinkRate;
        set => Item.BlinkRate = value;
    }

    public bool IsFocused
    {
        get => Item.Focus;
        set => Item.Focus = value;
    }

    public void Focus() => Item.SetFocus(true);

    public IObservable<string> Submitted => _submitted;
    public IObservable<string> SubmitFailed => _submitFailed;
    public IObservable<string> Changed => _changed;
    public IObservable<Unit> Overflowed => _overflowed;
    public IObservable<int> CursorMoved => _cursorMoved;

    public string GetAcceptEvent(ButtonHandle button) => GuiEventNames.Accept(Item, button);

    public string GetAcceptFailedEvent(ButtonHandle button) => GuiEventNames.AcceptFailed(Item, button);

    protected override void OnAttached(INamedEventBus bus)
    {
        AddSubscription(bus.Observe(GetAcceptEvent(_acceptButton)).Subscribe(_ => _submitted.OnNext(Text)));
        AddSubscription(bus.Observe(GetAcceptFailedEvent(_acceptButton)).Subscribe(_ => _submitFailed.OnNext(Text)));
        AddSubscription(bus.Observe(Item.GetTypeEvent()).Subscribe(_ => _changed.OnNext(Text)));
        AddSubscription(bus.Observe(Item.GetEraseEvent()).Subscribe(_ => _changed.OnNext(Text)));
        AddSubscription(bus.Observe(Item.GetOverflowEvent()).Subscribe(_ => _overflowed.OnNext(Unit.Default)));
        AddSubscription(bus.Observe(Item.GetCursormoveEvent()).Subscribe(_ => _cursorMoved.OnNext(CursorPosition)));
    }

    public override void Dispose()
    {
        _submitted.OnCompleted();
        _submitFailed.OnCompleted();
        _changed.OnCompleted();
        _overflowed.OnCompleted();
        _cursorMoved.OnCompleted();
        base.Dispose();
    }
}
