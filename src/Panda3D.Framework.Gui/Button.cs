using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Gui;

public sealed class Button : Widget<PGButton>
{
    readonly Subject<Unit> _clicked = new();
    readonly HashSet<string> _clickEvents = new(StringComparer.Ordinal);

    public Button(string label, string name = "button", float? bevel = null)
        : base(CreateTextButton(name, label, bevel))
    {
        AddClickButton(PrimaryButton);
    }

    public Button(string label, float bevel)
        : this(label, "button", bevel)
    {
    }

    public Button(NodePath ready, string name = "button")
        : base(CreateCustomButton(name, ready))
    {
        AddClickButton(PrimaryButton);
    }

    public Button(NodePath ready, NodePath depressed, string name = "button")
        : base(CreateCustomButton(name, ready, depressed))
    {
        AddClickButton(PrimaryButton);
    }

    public Button(NodePath ready, NodePath depressed, NodePath rollover, string name = "button")
        : base(CreateCustomButton(name, ready, depressed, rollover))
    {
        AddClickButton(PrimaryButton);
    }

    public Button(NodePath ready, NodePath depressed, NodePath rollover, NodePath inactive, string name = "button")
        : base(CreateCustomButton(name, ready, depressed, rollover, inactive))
    {
        AddClickButton(PrimaryButton);
    }

    public IObservable<Unit> Clicked => _clicked;

    public bool AddClickButton(ButtonHandle button)
    {
        ThrowIfDisposed();
        var eventName = GuiEventNames.Click(Item, button);
        bool added = _clickEvents.Add(eventName);
        if (added && EventBus is not null)
            AddSubscription(EventBus.Observe(eventName).Subscribe(_ => _clicked.OnNext(Unit.Default)));
        return added;
    }

    public string GetClickEvent(ButtonHandle button) => GuiEventNames.Click(Item, button);

    public void SetClickSound(AudioSound sound)
    {
        foreach (var eventName in _clickEvents)
            Item.SetSound(eventName, sound);
    }

    protected override void OnAttached(INamedEventBus bus)
    {
        foreach (var eventName in _clickEvents)
            AddSubscription(bus.Observe(eventName).Subscribe(_ => _clicked.OnNext(Unit.Default)));
    }

    public override void Dispose()
    {
        _clicked.OnCompleted();
        base.Dispose();
    }

    static PGButton CreateTextButton(string name, string label, float? bevel)
    {
        var item = new PGButton(name);
        float padding = Math.Max(0.08f, bevel ?? 0.08f);
        float width = Math.Max(0.35f, label.Length * 0.07f + padding * 2f);
        const float height = 0.18f;

        var ready = new NodePath($"{name}-ready");
        var card = new CardMaker($"{name}-card");
        card.SetFrame(-width * 0.5f, width * 0.5f, -height * 0.5f, height * 0.5f);
        card.SetColor(0.72f, 0.72f, 0.72f, 1f);
        ready.AttachNewNode(card.Generate());

        var text = new TextNode($"{name}-text");
        text.SetText(label);
        text.SetTextColor(0f, 0f, 0f, 1f);
        var textNode = ready.AttachNewNode(text);
        textNode.SetScale(0.07f);
        textNode.SetPos(-width * 0.5f + padding * 0.6f, 0f, -0.025f);

        item.Setup(ready);
        item.SetFrame(-width * 0.5f, width * 0.5f, -height * 0.5f, height * 0.5f);
        return item;
    }

    static PGButton CreateCustomButton(string name, NodePath ready)
    {
        var item = new PGButton(name);
        item.Setup(ready);
        return item;
    }

    static PGButton CreateCustomButton(string name, NodePath ready, NodePath depressed)
    {
        var item = new PGButton(name);
        item.Setup(ready, depressed);
        return item;
    }

    static PGButton CreateCustomButton(string name, NodePath ready, NodePath depressed, NodePath rollover)
    {
        var item = new PGButton(name);
        item.Setup(ready, depressed, rollover);
        return item;
    }

    static PGButton CreateCustomButton(string name, NodePath ready, NodePath depressed, NodePath rollover, NodePath inactive)
    {
        var item = new PGButton(name);
        item.Setup(ready, depressed, rollover, inactive);
        return item;
    }
}
