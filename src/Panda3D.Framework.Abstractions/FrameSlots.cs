namespace Panda3D.Framework;

/// <summary>
/// Shared per-frame task sort scale. Framework tasks and gameplay tasks registered through
/// <see cref="IFrameScheduler"/> all order on it.
/// </summary>
public static class FrameSlots
{
    /// <summary><c>resetPrevTransform</c> — fluid-motion prep; registered by <c>AddCollision</c>.</summary>
    public const int PrevTransform = -51;

    /// <summary><c>dataLoop</c> — input / data-graph traverse, before gameplay reads it.</summary>
    public const int DataLoop = -50;

    /// <summary><c>eventManager</c> — drains queued Panda events before gameplay reads observables.</summary>
    public const int Events = -1;

    /// <summary>Gameplay — coroutine resumption and user frame tasks (default sort).</summary>
    public const int Gameplay = 0;

    /// <summary><c>ivalLoop</c> — interval/tween stepping.</summary>
    public const int Intervals = 20;

    /// <summary><c>collisionLoop</c> — explicit collision traverse.</summary>
    public const int Collision = 30;

    /// <summary><c>igLoop</c> — <c>engine.RenderFrame()</c>; renders every output. Omitted in headless builds.</summary>
    public const int Render = 50;

    /// <summary><c>audioLoop</c> — audio manager update.</summary>
    public const int Audio = 60;
}
