using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Gui;

/// <summary>Base wrapper around a native <see cref="PGItem"/>, which stays exposed for APIs too specific to wrap here.</summary>
public abstract class Widget : IDisposable
{
    readonly CompositeDisposable _subscriptions = new();
    readonly Subject<Unit> _entered = new();
    readonly Subject<Unit> _exited = new();
    readonly Subject<Unit> _within = new();
    readonly Subject<Unit> _without = new();
    readonly Subject<Unit> _pressed = new();
    readonly Subject<Unit> _released = new();
    readonly Subject<bool> _focusChanged = new();
    bool _disposed;
    bool _attached;

    protected Widget(PGItem item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Node = new NodePath(item);
    }

    /// <summary>The scene-graph handle for this widget.</summary>
    public NodePath Node { get; }

    /// <summary>The native PGui item.</summary>
    public PGItem Item { get; }

    /// <summary>The default button used for base press/release observables.</summary>
    public ButtonHandle PrimaryButton { get; } = new("mouse1");

    public bool Visible
    {
        get => !Node.IsHidden();
        set
        {
            if (value) Node.Show();
            else Node.Hide();
        }
    }

    public bool Enabled
    {
        get => Item.GetActive();
        set => Item.SetActive(value);
    }

    public IObservable<Unit> Entered => _entered;
    public IObservable<Unit> Exited => _exited;
    public IObservable<Unit> Within => _within;
    public IObservable<Unit> Without => _without;
    public IObservable<Unit> Pressed => _pressed;
    public IObservable<Unit> Released => _released;
    public IObservable<bool> FocusChanged => _focusChanged;

    protected INamedEventBus? EventBus { get; private set; }

    /// <summary>Play the supplied native sound when the pointer enters the widget.</summary>
    public void SetRolloverSound(AudioSound sound) => Item.SetSound(Item.GetEnterEvent(), sound);

    internal void Attach(INamedEventBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ThrowIfDisposed();
        if (_attached) return;
        EventBus = bus;

        AddSubscription(bus.Observe(Item.GetEnterEvent()).Subscribe(_ => _entered.OnNext(Unit.Default)));
        AddSubscription(bus.Observe(Item.GetExitEvent()).Subscribe(_ => _exited.OnNext(Unit.Default)));
        AddSubscription(bus.Observe(Item.GetWithinEvent()).Subscribe(_ => _within.OnNext(Unit.Default)));
        AddSubscription(bus.Observe(Item.GetWithoutEvent()).Subscribe(_ => _without.OnNext(Unit.Default)));
        AddSubscription(bus.Observe(GetPressEvent(PrimaryButton)).Subscribe(_ => _pressed.OnNext(Unit.Default)));
        AddSubscription(bus.Observe(GetReleaseEvent(PrimaryButton)).Subscribe(_ => _released.OnNext(Unit.Default)));
        AddSubscription(bus.Observe(Item.GetFocusInEvent()).Subscribe(_ => _focusChanged.OnNext(true)));
        AddSubscription(bus.Observe(Item.GetFocusOutEvent()).Subscribe(_ => _focusChanged.OnNext(false)));

        OnAttached(bus);
        _attached = true;
    }

    public string GetPressEvent(ButtonHandle button) => GuiEventNames.Press(Item, button);

    public string GetReleaseEvent(ButtonHandle button) => GuiEventNames.Release(Item, button);

    protected void AddSubscription(IDisposable subscription) => _subscriptions.Add(subscription);

    protected virtual void OnAttached(INamedEventBus bus) { }

    protected void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _subscriptions.Dispose();
        CompleteSubjects();

        if (!Node.IsEmpty()) Node.RemoveNode();
    }

    void CompleteSubjects()
    {
        _entered.OnCompleted();
        _exited.OnCompleted();
        _within.OnCompleted();
        _without.OnCompleted();
        _pressed.OnCompleted();
        _released.OnCompleted();
        _focusChanged.OnCompleted();
    }
}

/// <summary>Strongly typed widget wrapper that exposes the concrete native item type.</summary>
public abstract class Widget<TItem> : Widget where TItem : PGItem
{
    protected Widget(TItem item) : base(item) => Item = item;

    /// <summary>The concrete native PGui item.</summary>
    public new TItem Item { get; }
}
