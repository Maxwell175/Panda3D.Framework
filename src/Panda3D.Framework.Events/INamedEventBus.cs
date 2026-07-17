using System;
using System.Collections.Generic;

namespace Panda3D.Framework.Events;

/// <summary>
/// The raw name→subscriber routing table for dynamic, string-identified events. Object-specific
/// notifications live as <see cref="IObservable{T}"/> on the object that raises them.
/// </summary>
public interface INamedEventBus
{
    /// <summary>Observe every event drained with this name.</summary>
    IObservable<NamedEvent> Observe(string name);

    /// <summary>Convenience over <c>Observe(name).Subscribe(handler)</c>.</summary>
    IDisposable Subscribe(string name, Action<NamedEvent> handler);

    /// <summary>
    /// Raise a dynamic string event. Queued on the global event queue so it flows through the single
    /// pump like every other event.
    /// </summary>
    void Send(string name, params object[] parameters);
}

/// <summary>
/// A drained <c>Event</c>, parsed into its name and boxed parameter values. Node pointers arrive as
/// their native binding object; numbers/strings as <see cref="int"/>/<see cref="double"/>/<see cref="string"/>.
/// </summary>
public readonly record struct NamedEvent(string Name, IReadOnlyList<object> Parameters)
{
    /// <summary>The number of parameters.</summary>
    public int Count => Parameters.Count;

    /// <summary>
    /// The parameter at <paramref name="index"/> typed as <typeparamref name="T"/>. Throws
    /// <see cref="InvalidCastException"/> (or <see cref="ArgumentOutOfRangeException"/>) if it isn't —
    /// use <see cref="TryGet{T}"/> when the shape is uncertain.
    /// </summary>
    public T Get<T>(int index)
    {
        object value = Parameters[index];
        if (value is T typed) return typed;
        throw new InvalidCastException(
            $"Event '{Name}' parameter {index} is {value?.GetType().Name ?? "null"}, not {typeof(T).Name}.");
    }

    /// <summary>Try to read the parameter at <paramref name="index"/> as <typeparamref name="T"/>.</summary>
    public bool TryGet<T>(int index, out T value)
    {
        if (index >= 0 && index < Parameters.Count && Parameters[index] is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }
}
