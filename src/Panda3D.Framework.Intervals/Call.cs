using System;

namespace Panda3D.Framework.Intervals;

/// <summary>A zero-duration side effect placed in a timeline.</summary>
public sealed class Call : ManagedInterval
{
    readonly Action _action;

    public Call(Action action) : base(0, openEnded: true, name: "call")
        => _action = action ?? throw new ArgumentNullException(nameof(action));

    public override void Initialize(double t) => _action();
    public override void Step(double t) { }
}
