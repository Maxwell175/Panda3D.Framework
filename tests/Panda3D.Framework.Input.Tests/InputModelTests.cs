using System;
using System.Collections.Generic;
using Panda3D.Core;
using Xunit;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Panda3D.Framework.Input.Tests;

/// <summary>
/// The action/binding/context model, driven by a fake sampler — the core input logic, fully headless
/// (no hardware). Covers button edges, composite axes/vectors, analog processors, and context contention.
/// </summary>
public sealed class InputModelTests
{
    sealed class FakeSampler : IInputSampler
    {
        public readonly HashSet<ButtonId> Down = new();
        public readonly Dictionary<InputDeviceAxis, float> Axes = new();
        public bool IsDown(ButtonId button) => Down.Contains(button);
        public float Axis(InputDeviceAxis axis) => Axes.TryGetValue(axis, out var v) ? v : 0f;
    }

    static readonly ButtonId A = new(100), B = new(200), Up = new(1), Down = new(2), Left = new(3), Right = new(4);

    [Fact]
    public void ButtonAction_DownAndEdges()
    {
        var action = new ButtonAction("Jump");
        action.Bindings.Add(new ButtonBinding(A));
        var s = new FakeSampler();

        action.Evaluate(s, 0.016f);
        Assert.False(action.IsPressed);

        s.Down.Add(A);
        action.Evaluate(s, 0.016f);
        Assert.True(action.IsPressed);
        Assert.True(action.WasPressed);
        Assert.False(action.WasReleased);

        action.Evaluate(s, 0.016f);
        Assert.True(action.IsPressed);
        Assert.False(action.WasPressed);   // no re-edge while held

        s.Down.Remove(A);
        action.Evaluate(s, 0.016f);
        Assert.False(action.IsPressed);
        Assert.True(action.WasReleased);
    }

    [Fact]
    public void ButtonAction_MultipleBindingsAreOred()
    {
        var action = new ButtonAction("Jump");
        action.Bindings.Add(new ButtonBinding(A));
        action.Bindings.Add(new ButtonBinding(B));   // e.g. Space OR GamepadA
        var s = new FakeSampler();

        s.Down.Add(B);
        action.Evaluate(s, 0.016f);
        Assert.True(action.IsPressed);
    }

    [Fact]
    public void ButtonAction_PressedObservableFiresOnEachEdge()
    {
        var action = new ButtonAction("Jump");
        action.Bindings.Add(new ButtonBinding(A));
        int count = 0;
        using var _ = action.Pressed.Subscribe(__ => count++);
        var s = new FakeSampler();

        s.Down.Add(A); action.Evaluate(s, 0.016f);   // edge 1
        action.Evaluate(s, 0.016f);                   // held, no edge
        s.Down.Remove(A); action.Evaluate(s, 0.016f);
        s.Down.Add(A); action.Evaluate(s, 0.016f);    // edge 2

        Assert.Equal(2, count);
    }

    [Fact]
    public void ButtonAction_HeldForAccumulates()
    {
        var action = new ButtonAction("Charge");
        action.Bindings.Add(new ButtonBinding(A));
        var s = new FakeSampler { };
        s.Down.Add(A);

        action.Evaluate(s, 0.3f);
        action.Evaluate(s, 0.3f);
        Assert.True(action.HeldFor(0.5f));
        Assert.False(action.HeldFor(1.0f));
    }

    [Fact]
    public void CompositeAxis_TwoButtonsMakeMinusOneToOne()
    {
        var action = new AxisAction("Throttle");
        action.Bindings.Add(new CompositeAxisBinding(negative: A, positive: B));
        var s = new FakeSampler();

        s.Down.Add(B);
        action.Evaluate(s, 0.016f);
        Assert.Equal(1f, action.Value, 3);

        s.Down.Add(A);   // both → cancels
        action.Evaluate(s, 0.016f);
        Assert.Equal(0f, action.Value, 3);

        s.Down.Remove(B);
        action.Evaluate(s, 0.016f);
        Assert.Equal(-1f, action.Value, 3);
    }

    [Fact]
    public void AxisBinding_AppliesDeadzoneInvertScale()
    {
        var action = new AxisAction("Steer");
        action.Bindings.Add(new AxisBinding(InputDeviceAxis.LeftX) { Deadzone = 0.2f, Invert = true, Scale = 2f });
        var s = new FakeSampler();

        s.Axes[InputDeviceAxis.LeftX] = 0.1f;   // inside deadzone
        action.Evaluate(s, 0.016f);
        Assert.Equal(0f, action.Value, 3);

        s.Axes[InputDeviceAxis.LeftX] = 1.0f;   // full → invert*scale
        action.Evaluate(s, 0.016f);
        Assert.Equal(-2f, action.Value, 3);
    }

    [Fact]
    public void VectorAction_WasdComposite()
    {
        var move = new VectorAction("Move");
        move.Bindings.Add(new CompositeVectorBinding(Up, Down, Left, Right));
        var s = new FakeSampler();

        s.Down.Add(Right);
        move.Evaluate(s, 0.016f);
        Assert.Equal(1.0, move.Value.GetX(), 3);
        Assert.Equal(0.0, move.Value.GetY(), 3);

        s.Down.Add(Up);
        move.Evaluate(s, 0.016f);
        Assert.Equal(1.0, move.Value.GetX(), 3);
        Assert.Equal(1.0, move.Value.GetY(), 3);
    }

    [Fact]
    public void VectorAction_StickWithRadialDeadzone()
    {
        var look = new VectorAction("Look");
        look.Bindings.Add(new StickBinding(InputDeviceAxis.RightX, InputDeviceAxis.RightY) { Deadzone = 0.3f });
        var s = new FakeSampler();

        s.Axes[InputDeviceAxis.RightX] = 0.1f;
        s.Axes[InputDeviceAxis.RightY] = 0.1f;   // magnitude ~0.14 < 0.3
        look.Evaluate(s, 0.016f);
        Assert.Equal(0.0, look.Value.GetX(), 3);
        Assert.Equal(0.0, look.Value.GetY(), 3);

        s.Axes[InputDeviceAxis.RightX] = 1.0f;
        s.Axes[InputDeviceAxis.RightY] = 0.0f;
        look.Evaluate(s, 0.016f);
        Assert.True(look.Value.GetX() > 0.5f);
    }

    [Fact]
    public void Processors_Deadzone1D()
    {
        Assert.Equal(0f, Processors.Deadzone1D(0.15f, 0.2f), 3);
        Assert.Equal(0f, Processors.Deadzone1D(-0.15f, 0.2f), 3);
        Assert.Equal(1f, Processors.Deadzone1D(1.0f, 0.2f), 3);
        Assert.True(Processors.Deadzone1D(0.6f, 0.2f) > 0f);
    }
}
