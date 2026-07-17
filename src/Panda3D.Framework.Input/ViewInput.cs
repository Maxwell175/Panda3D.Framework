using System;
using System.Collections.Generic;
using System.Threading;
using Interrogate;
using Panda3D.Core;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework.Input;

/// <summary>
/// Per-view raw input: builds the window's <c>MouseAndKeyboard → MouseWatcher</c> chain under the data
/// root, wires the view's 2-D overlay <c>PGTop</c>s to that watcher, and exposes polling. Edges
/// (<see cref="Pressed"/>/<see cref="Released"/>) compare the post-traversal state against a snapshot the
/// <c>dataLoop</c> takes before the traversal (last frame's state).
/// </summary>
internal sealed class ViewInput : IInput, IDisposable
{
    readonly MouseWatcher _watcher;
    readonly NodePath _makNp;
    readonly NodePath _watcherNp;
    readonly InputRegistry _registry;
    readonly HashSet<int> _queried = new();
    readonly HashSet<int> _prevDown = new();
    int _disposed;

    public ViewInput(ViewContext context, DataGraph data, InputRegistry registry)
    {
        _registry = registry;

        var view = context.View
            ?? throw new InvalidOperationException("IInput must be resolved from a view scope (view.Services).");
        var window = view.Window
            ?? throw new InvalidOperationException("IInput requires a windowed view; offscreen views have no mouse/keyboard.");

        var mak = new MouseAndKeyboard(window, 0, "mouse");
        _makNp = data.Root.AttachNewNode(mak);
        _watcher = new MouseWatcher("watcher");
        _watcherNp = _makNp.AttachNewNode(_watcher);

        WireOverlay(view.Overlay2d);
        WireOverlay(view.Pixel2d);

        registry.Register(this);
    }

    /// <summary>The view's watcher — the keyboard/mouse source for the action runtime.</summary>
    internal IMouseWatcher Watcher => _watcher;

    void WireOverlay(NodePath? overlay)
    {
        // a PGItem only receives input under a PGTop bound to a MouseWatcher
        overlay?.Node().CastTo<PGTop>()?.SetMouseWatcher(_watcher);
    }

    public bool IsDown(ButtonId button)
    {
        int handle = button.Handle;
        _queried.Add(handle);
        return _watcher.IsButtonDown(handle);
    }

    public bool Pressed(ButtonId button)
    {
        int handle = button.Handle;
        _queried.Add(handle);
        return _watcher.IsButtonDown(handle) && !_prevDown.Contains(handle);
    }

    public bool Released(ButtonId button)
    {
        int handle = button.Handle;
        _queried.Add(handle);
        return !_watcher.IsButtonDown(handle) && _prevDown.Contains(handle);
    }

    public LPoint2f? MousePosition => _watcher.HasMouse() ? _watcher.GetMouse() : null;

    public bool IsOverUi => _watcher.HasMouse() && _watcher.GetOverRegion() is not null;

    public ButtonId? CaptureNext()
    {
        foreach (var button in CaptureCandidates.All)
        {
            _queried.Add(button);
            if (_watcher.IsButtonDown(button) && !_prevDown.Contains(button))
                return new ButtonId(button);
        }
        return null;
    }

    /// <summary>Snapshot the currently-down state as "last frame"; called by the dataLoop before traversal.</summary>
    internal void CapturePreviousFrame()
    {
        _prevDown.Clear();
        foreach (var button in _queried)
        {
            if (_watcher.IsButtonDown(button))
                _prevDown.Add(button);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _registry.Unregister(this);
        if (!_watcherNp.IsEmpty()) _watcherNp.RemoveNode();
        if (!_makNp.IsEmpty()) _makNp.RemoveNode();
    }
}

/// <summary>Candidate buttons scanned by <see cref="ViewInput.CaptureNext"/> for a rebind UI (keyboard + mouse).</summary>
internal static class CaptureCandidates
{
    public static readonly int[] All = Build();

    static int[] Build()
    {
        var list = new List<int>();
        for (byte c = 32; c < 127; c++)
            list.Add(KeyboardButton.AsciiKey(c));
        list.Add(KeyboardButton.Space());
        list.Add(KeyboardButton.Enter());
        list.Add(KeyboardButton.Escape());
        list.Add(KeyboardButton.Tab());
        list.Add(KeyboardButton.Backspace());
        list.Add(MouseButton.One());
        list.Add(MouseButton.Two());
        list.Add(MouseButton.Three());
        return list.ToArray();
    }
}
