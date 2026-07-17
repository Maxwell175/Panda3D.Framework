using System;
using System.Reactive.Subjects;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Gui;

public sealed class ScrollFrame : Widget<PGScrollFrame>
{
    readonly Subject<LVecBase2f> _scrolled = new();

    public ScrollFrame(
        float width,
        float height,
        float left,
        float right,
        float bottom,
        float top,
        string name = "scrollframe",
        float sliderWidth = 0.08f,
        float bevel = 0.02f)
        : base(new PGScrollFrame(name))
    {
        Item.Setup(width, height, left, right, bottom, top, sliderWidth, bevel);
    }

    public ScrollFrame(
        float width,
        float height,
        string name = "scrollframe",
        float sliderWidth = 0.08f,
        float bevel = 0.05f)
        : this(width, height, 0f, width, -height, 0f, name, sliderWidth, bevel)
    {
    }

    public NodePath Canvas => new(Item.GetCanvasNode());

    public LVecBase4f VirtualFrame
    {
        get => Item.GetVirtualFrame();
        set => Item.SetVirtualFrame(value);
    }

    public bool AutoHide
    {
        get => Item.AutoHide;
        set => Item.AutoHide = value;
    }

    public bool ManagePieces
    {
        get => Item.ManagePieces;
        set => Item.ManagePieces = value;
    }

    public IPGSliderBar HorizontalSlider => Item.GetHorizontalSlider();
    public IPGSliderBar VerticalSlider => Item.GetVerticalSlider();

    public IObservable<LVecBase2f> Scrolled => _scrolled;

    protected override void OnAttached(INamedEventBus bus)
    {
        AddSubscription(bus.Observe(HorizontalSlider.GetAdjustEvent()).Subscribe(_ => PublishScrolled()));
        AddSubscription(bus.Observe(VerticalSlider.GetAdjustEvent()).Subscribe(_ => PublishScrolled()));
    }

    void PublishScrolled() => _scrolled.OnNext(new LVecBase2f(HorizontalSlider.GetRatio(), VerticalSlider.GetRatio()));

    public override void Dispose()
    {
        _scrolled.OnCompleted();
        base.Dispose();
    }
}
