using System;
using System.Collections.Generic;

namespace Panda3D.Framework.Input;

/// <summary>Common surface: a name and an enable flag. Evaluated each frame by the input runtime.</summary>
public abstract class InputAction
{
    protected InputAction(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    public string Name { get; }
    public bool Enabled { get; set; } = true;

    /// <summary>Update polling state and raise events from the current device sample.</summary>
    internal abstract void Evaluate(IInputSampler sampler, float dt);

    /// <summary>The physical buttons this action reads, for context contention (higher priority claims them).</summary>
    internal virtual IEnumerable<ButtonId> BoundButtons() => Array.Empty<ButtonId>();
}
