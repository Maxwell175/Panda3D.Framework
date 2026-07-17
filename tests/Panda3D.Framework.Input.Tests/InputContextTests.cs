using System.Collections.Generic;
using Panda3D.Core;
using Xunit;

namespace Panda3D.Framework.Input.Tests;

/// <summary>Context enable/disable and the priority-contention model (higher priority claims contested buttons).</summary>
public sealed class InputContextTests
{
    sealed class FakeSampler : IInputSampler
    {
        public readonly HashSet<ButtonId> Down = new();
        public bool IsDown(ButtonId button) => Down.Contains(button);
        public float Axis(InputDeviceAxis axis) => 0f;
    }

    static readonly ButtonId Contested = new(50);

    static (InputRuntime rt, FakeSampler s) NewRuntime()
    {
        var rt = new InputRuntime();
        var s = new FakeSampler();
        rt.SetSampler(s);
        return (rt, s);
    }

    [Fact]
    public void HigherPriorityContextClaimsContestedButton()
    {
        var (rt, s) = NewRuntime();
        var gameplay = rt.AddContext("gameplay", priority: 0);
        var menu = rt.AddContext("menu", priority: 10);

        var gAction = gameplay.Add(new ButtonAction("g"));
        gAction.Bindings.Add(new ButtonBinding(Contested));
        var mAction = menu.Add(new ButtonAction("m"));
        mAction.Bindings.Add(new ButtonBinding(Contested));

        s.Down.Add(Contested);
        rt.Evaluate(0.016f);

        Assert.True(mAction.IsPressed);    // higher priority wins
        Assert.False(gAction.IsPressed);   // suppressed by the claim
    }

    [Fact]
    public void DisabledContextReleasesItsClaim()
    {
        var (rt, s) = NewRuntime();
        var gameplay = rt.AddContext("gameplay", priority: 0);
        var menu = rt.AddContext("menu", priority: 10);

        var gAction = gameplay.Add(new ButtonAction("g"));
        gAction.Bindings.Add(new ButtonBinding(Contested));
        var mAction = menu.Add(new ButtonAction("m"));
        mAction.Bindings.Add(new ButtonBinding(Contested));

        menu.Enabled = false;
        s.Down.Add(Contested);
        rt.Evaluate(0.016f);

        Assert.True(gAction.IsPressed);   // now gameplay sees it
        Assert.False(mAction.IsPressed);  // disabled context isn't evaluated
    }

    [Fact]
    public void DisposedContextIsNoLongerEvaluated()
    {
        var (rt, s) = NewRuntime();
        var ctx = rt.AddContext("temp", 0);
        var action = ctx.Add(new ButtonAction("a"));
        action.Bindings.Add(new ButtonBinding(Contested));

        ctx.Dispose();
        s.Down.Add(Contested);
        rt.Evaluate(0.016f);

        Assert.False(action.IsPressed);   // removed context isn't evaluated
    }
}
