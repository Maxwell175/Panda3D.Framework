using System;
using Panda3D.Core;

namespace Panda3D.Framework.Physics;

/// <summary>
/// Panda's collision system exposed as an explicit traverse plus typed observables of
/// <see cref="CollisionEntry"/>, instead of string-event callbacks.
/// </summary>
public interface ICollisionWorld : IDisposable
{
    /// <summary>
    /// Register a from-collider (a <see cref="NodePath"/> whose node is a <c>CollisionNode</c>). Pass a
    /// native handler (queue/pusher/floor/gravity) to add a physical response; the observables still fire.
    /// </summary>
    void Add(NodePath collider, CollisionHandler? handler = null);

    /// <summary>Unregister a collider.</summary>
    void Remove(NodePath collider);

    /// <summary>
    /// Register <paramref name="collider"/> with a <c>CollisionHandlerPusher</c> that keeps
    /// <paramref name="pushee"/> from penetrating into-geometry. Returns the pusher to tune.
    /// </summary>
    CollisionHandlerPusher AddPusher(NodePath collider, NodePath pushee);

    /// <summary>
    /// Register <paramref name="collider"/> with a <c>CollisionHandlerFloor</c> that keeps
    /// <paramref name="floored"/> resting on floor-geometry at <paramref name="offset"/> above it.
    /// </summary>
    CollisionHandlerFloor AddFloor(NodePath collider, NodePath floored, float offset = 0f);

    /// <summary>
    /// Register <paramref name="collider"/> with a <c>CollisionHandlerGravity</c> that pulls
    /// <paramref name="gravitated"/> down at <paramref name="gravity"/> until it meets floor-geometry.
    /// </summary>
    CollisionHandlerGravity AddGravity(NodePath collider, NodePath gravitated, float gravity);

    /// <summary>
    /// Register <paramref name="collider"/> as a persistent query on the shared per-frame traverse and hand
    /// back an <see cref="ICollisionQuery"/> to poll after each traverse. Feeds no observables. Cheaper than
    /// a per-frame <see cref="Raycast"/>, which runs its own traverse each call.
    /// </summary>
    ICollisionQuery AddQuery(NodePath collider);

    /// <summary>Traverse from <paramref name="root"/>. Unless auto-traverse is off, <c>AddCollision</c> registers this at <see cref="FrameSlots.Collision"/>.</summary>
    void Traverse(NodePath root);

    /// <summary>
    /// Cast a ray from <paramref name="origin"/> along <paramref name="direction"/> (both in the space of
    /// <paramref name="against"/>) and return the nearest into-hit, or <see langword="null"/> for a miss.
    /// A one-shot query with its own traverse. <paramref name="mask"/> defaults to all bits.
    /// </summary>
    RaycastHit? Raycast(LPoint3f origin, LVector3f direction, NodePath against, BitMask32? mask = null);

    /// <summary>
    /// Cast a ray through a camera at normalized film coordinates <paramref name="filmX"/>/<paramref name="filmY"/>
    /// (each in [-1, 1], centre = 0) and return the nearest into-hit under <paramref name="against"/>.
    /// <paramref name="camera"/>'s node must be a <c>LensNode</c>.
    /// </summary>
    RaycastHit? Pick(NodePath camera, float filmX, float filmY, NodePath against, BitMask32? mask = null);

    /// <summary>A new collision started this pass.</summary>
    IObservable<CollisionEntry> Entered { get; }

    /// <summary>Still colliding this pass.</summary>
    IObservable<CollisionEntry> Again { get; }

    /// <summary>A collision ended this pass.</summary>
    IObservable<CollisionEntry> Exited { get; }

    /// <summary>
    /// Turn on debug visualization of collisions under <paramref name="root"/>. Debug-only: throws
    /// <see cref="System.MissingMethodException"/> in an optimized/NativeAOT (lean-binding) build, so guard
    /// calls with <c>#if DEBUG</c>.
    /// </summary>
    void ShowColliders(NodePath root);

    /// <summary>Turn debug collision visualization back off. Debug-only — see <see cref="ShowColliders"/>.</summary>
    void HideColliders();

    /// <summary>Native escape hatch — the underlying traverser.</summary>
    CollisionTraverser Traverser { get; }

    /// <summary>Native escape hatch — the world's event handler (constant patterns).</summary>
    CollisionHandlerEvent Handler { get; }
}
