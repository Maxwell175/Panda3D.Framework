namespace Panda3D.Framework.Scheduling;

/// <summary>
/// Clock configuration and pacing. <see cref="LimitFrameRate"/> caps the frame rate so an epoch can't
/// free-run; <see cref="MaxDt"/> caps the dt spike on a long frame.
/// </summary>
public sealed class ClockOptions
{
    /// <summary>Put the clock in <c>M_limited</c> mode at <see cref="MaxFps"/>.</summary>
    public bool LimitFrameRate { get; set; }

    /// <summary>Frame-rate cap applied when <see cref="LimitFrameRate"/> is set.</summary>
    public double MaxFps { get; set; } = 60;

    /// <summary>Caps the reported dt on a long frame (0 = off).</summary>
    public double MaxDt { get; set; }

    /// <summary>
    /// Tick the global clock once per epoch from the <c>"default"</c> task chain. Keep on for headless
    /// builds where nothing else advances the clock; set <see langword="false"/> on a client where
    /// <c>RenderFrame</c> already ticks it, to avoid a double-advance.
    /// </summary>
    public bool TickClock { get; set; } = true;
}
