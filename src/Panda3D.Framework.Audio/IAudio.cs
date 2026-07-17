using System;
using Panda3D.Core;

namespace Panda3D.Framework.Audio;

/// <summary>
/// Audio lifecycle and loading over Panda's native managers. The managers remain the engine binding
/// interfaces; loading hands back a reusable managed <see cref="ISound"/> (play/stop/volume/loop, plus
/// an awaitable <see cref="ISound.Finished"/>).
/// </summary>
public interface IAudio : IDisposable
{
    /// <summary>The sound-effect manager, used directly for volume, active state, cache, and limits.</summary>
    AudioManager Sfx { get; }

    /// <summary>The music manager, used directly for volume, active state, cache, and limits.</summary>
    AudioManager Music { get; }

    /// <summary>
    /// Load a sound effect through <see cref="Sfx"/> once, returning a reusable handle. Set
    /// <paramref name="positional"/> for a 3-D sound (attach its <see cref="ISound.Native"/> via <see cref="IAudio3D"/>).
    /// </summary>
    ISound LoadSfx(string path, bool positional = false);

    /// <summary>Load a music track through <see cref="Music"/> once, returning a reusable handle.</summary>
    ISound LoadMusic(string path);

    /// <summary>
    /// Wrap a native <see cref="AudioSound"/> obtained elsewhere (a GUI sound, a manager sound, a null sound)
    /// in the managed <see cref="ISound"/> surface — controls plus an awaitable <see cref="ISound.Finished"/>.
    /// </summary>
    ISound Wrap(AudioSound sound);
}
