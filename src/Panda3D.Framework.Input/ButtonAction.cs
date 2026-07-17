using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;

namespace Panda3D.Framework.Input;

/// <summary>A boolean action — its bindings are OR'd. Exposes edges as polling and as observables.</summary>
public sealed class ButtonAction : InputAction
{
    readonly Subject<Unit> _pressed = new();
    readonly Subject<Unit> _released = new();
    float _heldTime;
    float _lastPressDuration;

    public ButtonAction(string name) : base(name) { }

    /// <summary>Maximum held duration, in seconds, for a release to count as a <see cref="Tapped"/> tap.</summary>
    public const float TapThresholdSeconds = 0.25f;

    public IList<ButtonBinding> Bindings { get; } = new List<ButtonBinding>();

    public bool IsPressed { get; private set; }
    public bool WasPressed { get; private set; }
    public bool WasReleased { get; private set; }

    /// <summary>True while held for at least <paramref name="seconds"/>.</summary>
    public bool HeldFor(float seconds) => IsPressed && _heldTime >= seconds;

    /// <summary>True the frame a brief press is released (a tap held less than <see cref="TapThresholdSeconds"/>).</summary>
    public bool Tapped => WasReleased && _lastPressDuration < TapThresholdSeconds;

    public IObservable<Unit> Pressed => _pressed;
    public IObservable<Unit> Released => _released;

    internal override IEnumerable<ButtonId> BoundButtons()
    {
        foreach (var b in Bindings) yield return b.Button;
    }

    internal override void Evaluate(IInputSampler sampler, float dt)
    {
        bool down = false;
        foreach (var b in Bindings)
        {
            if (sampler.IsDown(b.Button)) { down = true; break; }
        }

        WasPressed = down && !IsPressed;
        WasReleased = !down && IsPressed;

        if (WasPressed) { _heldTime = 0f; _pressed.OnNext(Unit.Default); }
        if (down) { _heldTime += dt; }

        if (WasReleased) { _lastPressDuration = _heldTime; _released.OnNext(Unit.Default); }

        IsPressed = down;
    }
}
