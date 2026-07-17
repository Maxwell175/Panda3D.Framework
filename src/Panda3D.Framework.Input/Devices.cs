using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Interrogate;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Input;

/// <summary>
/// Engine-wide gamepads/joysticks: enumeration plus hotplug observables (demuxed from Panda's
/// <c>connect-device</c>/<c>disconnect-device</c> events). Devices are the native <see cref="InputDevice"/>.
/// </summary>
public interface IDevices
{
    /// <summary>All currently-connected non-keyboard/mouse devices.</summary>
    IReadOnlyList<InputDevice> All { get; }

    /// <summary>A device connected.</summary>
    IObservable<InputDevice> Connected { get; }

    /// <summary>A device disconnected.</summary>
    IObservable<InputDevice> Disconnected { get; }
}

internal sealed class Devices : IDevices
{
    readonly InputDeviceManager _manager = InputDeviceManager.GetGlobalPtr();
    readonly Subject<InputDevice> _connected = new();
    readonly Subject<InputDevice> _disconnected = new();

    public Devices(INamedEventBus bus)
    {
        bus.Observe("connect-device").Subscribe(e => Emit(_connected, e));
        bus.Observe("disconnect-device").Subscribe(e => Emit(_disconnected, e));
    }

    static void Emit(Subject<InputDevice> subject, NamedEvent e)
    {
        if (e.Parameters.Count > 0 && e.Parameters[0] is INativeObject native)
        {
            var device = native.CastTo<InputDevice>();
            if (device is not null) subject.OnNext(device);
        }
    }

    public IReadOnlyList<InputDevice> All
    {
        // Snapshot: GetDevices() is a live view the manager mutates on hotplug.
        get => new List<InputDevice>(_manager.GetDevices());
    }

    public IObservable<InputDevice> Connected => _connected;
    public IObservable<InputDevice> Disconnected => _disconnected;
}
