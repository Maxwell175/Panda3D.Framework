using System.Collections.Generic;
using System.Linq;
using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>
/// Owns the contexts and evaluates them each frame: sample the devices, resolve every enabled
/// action, raise events. Higher-priority contexts claim their buttons first, so a contested control
/// only reaches the highest-priority context that binds it.
/// </summary>
internal sealed class InputRuntime
{
    readonly List<InputContext> _contexts = new();
    IInputSampler? _sampler;

    /// <summary>Set the device sampler.</summary>
    public void SetSampler(IInputSampler sampler) => _sampler = sampler;

    public InputContext AddContext(string name, int priority)
    {
        var context = new InputContext(this, name, priority);
        _contexts.Add(context);
        return context;
    }

    public void RemoveContext(InputContext context) => _contexts.Remove(context);

    /// <summary>Sample devices and resolve all actions. Called from the <c>dataLoop</c> each frame.</summary>
    public void Evaluate(float dt)
    {
        if (_sampler is null) return;

        var claimed = new HashSet<ButtonId>();
        foreach (var context in _contexts.Where(c => c.Enabled).OrderByDescending(c => c.Priority))
        {
            IInputSampler scoped = claimed.Count == 0 ? _sampler : new ClaimedSampler(_sampler, claimed);
            foreach (var action in context.Actions)
            {
                if (action.Enabled)
                    action.Evaluate(scoped, dt);
            }
            foreach (var button in context.BoundButtons())
                claimed.Add(button);
        }
    }
}

/// <summary>A sampler that hides buttons already claimed by a higher-priority context.</summary>
internal sealed class ClaimedSampler : IInputSampler
{
    readonly IInputSampler _inner;
    readonly HashSet<ButtonId> _claimed;

    public ClaimedSampler(IInputSampler inner, HashSet<ButtonId> claimed)
    {
        _inner = inner;
        _claimed = claimed;
    }

    public bool IsDown(ButtonId button) => !_claimed.Contains(button) && _inner.IsDown(button);

    // analog axes aren't claimed; contention is button-oriented
    public float Axis(InputDeviceAxis axis) => _inner.Axis(axis);
}
