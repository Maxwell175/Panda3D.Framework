using System;
using System.Collections.Generic;

namespace Panda3D.Framework.Input;

/// <summary>
/// A named, enable/disable-able, prioritized action set. Enable/disable to switch gameplay↔menu↔vehicle;
/// when two enabled contexts bind the same control, higher <see cref="Priority"/> wins.
/// </summary>
public interface IInputContext : IDisposable
{
    string Name { get; }

    /// <summary>Higher wins a contested control.</summary>
    int Priority { get; set; }

    bool Enabled { get; set; }

    /// <summary>Add an action; returns it for chaining.</summary>
    T Add<T>(T action) where T : InputAction;

    IReadOnlyList<InputAction> Actions { get; }
}

internal sealed class InputContext : IInputContext
{
    readonly InputRuntime _runtime;
    readonly List<InputAction> _actions = new();

    public InputContext(InputRuntime runtime, string name, int priority)
    {
        _runtime = runtime;
        Name = name;
        Priority = priority;
    }

    public string Name { get; }
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;

    public T Add<T>(T action) where T : InputAction
    {
        ArgumentNullException.ThrowIfNull(action);
        _actions.Add(action);
        return action;
    }

    public IReadOnlyList<InputAction> Actions => _actions;

    internal IEnumerable<ButtonId> BoundButtons()
    {
        foreach (var action in _actions)
            foreach (var button in action.BoundButtons())
                yield return button;
    }

    public void Dispose() => _runtime.RemoveContext(this);
}
