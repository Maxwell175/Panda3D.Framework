using System;
using Panda3D.Core;

namespace Panda3D.Framework.Input;

/// <summary>
/// The production sampler: keyboard/mouse from a view's <c>MouseWatcher</c>, plus gamepad buttons and
/// analog axes from the <c>InputDeviceManager</c>. An analog axis value needs a binding release to
/// publish it; composite/keyboard axes (built from buttons) are unaffected.
/// </summary>
internal sealed class DeviceSampler : IInputSampler
{
    readonly Func<IMouseWatcher?> _watcher;
    readonly InputDeviceManager _devices;

    public DeviceSampler(Func<IMouseWatcher?> watcher, InputDeviceManager devices)
    {
        _watcher = watcher;
        _devices = devices;
    }

    public bool IsDown(ButtonId button)
    {
        int handle = button.Handle;
        var watcher = _watcher();
        if (watcher is not null && watcher.IsButtonDown(handle))
            return true;

        foreach (var device in _devices.GetDevices())
        {
            var state = device.FindButton(handle);
            if (state is not null && state.Known && state.Pressed)
                return true;
        }
        return false;
    }

    public float Axis(InputDeviceAxis axis)
    {
        // first device that reports this axis; Known separates a live value from an absent axis
        foreach (var device in _devices.GetDevices())
        {
            var state = device.FindAxis(axis);
            if (state is not null && state.Known)
                return (float)state.Value;
        }
        return 0f;
    }
}
