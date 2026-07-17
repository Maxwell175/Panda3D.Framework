using System;
using System.Reactive.Subjects;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Gui;

public abstract class SliderBarBase : Widget<PGSliderBar>
{
    readonly Subject<float> _valueChanged = new();

    protected SliderBarBase(PGSliderBar item) : base(item) { }

    public (float Min, float Max) Range
    {
        get => (Item.GetMinValue(), Item.GetMaxValue());
        set => Item.SetRange(value.Min, value.Max);
    }

    public float Min
    {
        get => Item.GetMinValue();
        set => Item.SetRange(value, Max);
    }

    public float Max
    {
        get => Item.GetMaxValue();
        set => Item.SetRange(Min, value);
    }

    public float Value
    {
        get => Item.Value;
        set => Item.Value = value;
    }

    public float Ratio
    {
        get => Item.Ratio;
        set => Item.Ratio = value;
    }

    public bool IsDragging => Item.IsButtonDown();

    public IObservable<float> ValueChanged => _valueChanged;

    protected override void OnAttached(INamedEventBus bus)
    {
        AddSubscription(bus.Observe(Item.GetAdjustEvent()).Subscribe(_ => _valueChanged.OnNext(Value)));
    }

    public override void Dispose()
    {
        _valueChanged.OnCompleted();
        base.Dispose();
    }
}
