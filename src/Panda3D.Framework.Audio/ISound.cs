using System;
using System.Reactive;
using Panda3D.Core;

namespace Panda3D.Framework.Audio;

/// <summary>
/// A reusable managed handle over a native <see cref="AudioSound"/>: load (or wrap) it once, then
/// <see cref="Play"/> it as many times as you like. Exposes <see cref="Native"/> for more (rate,
/// balance, seek) and an awaitable <see cref="Finished"/>. For a 3-D sound, load it positional and
/// attach its <see cref="Native"/> to an emitter via <see cref="IAudio3D"/>.
/// </summary>
public interface ISound
{
    /// <summary>(Re)start playback from the beginning.</summary>
    void Play();

    /// <summary>Stop playback.</summary>
    void Stop();

    /// <summary>Whether the sound is currently playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Volume in [0, 1].</summary>
    float Volume { get; set; }

    /// <summary>Whether playback loops.</summary>
    bool Loop { get; set; }

    /// <summary>
    /// Fires once when playback finishes, then completes. Awaitable directly (<c>await sound.Finished</c>).
    /// A looping sound never finishes. Late subscribers to an already-stopped sound still receive it.
    /// </summary>
    IObservable<Unit> Finished { get; }

    /// <summary>The underlying native sound — escape hatch for rate, balance, seek, and finished-event names.</summary>
    AudioSound Native { get; }
}
