using System;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading;
using Interrogate;
using Panda3D.Core;
using Panda3D.Framework.Events;

namespace Panda3D.Framework.Physics;

/// <summary>
/// A collision world: a native <c>CollisionTraverser</c> and <c>CollisionHandlerEvent</c> that push
/// in/again/out transitions to typed observables of the colliding <see cref="CollisionEntry"/>.
/// </summary>
internal sealed class CollisionWorld : ICollisionWorld
{
    static int _counter;

    readonly CollisionTraverser _traverser = new("ctrav");
    readonly CollisionHandlerEvent _handler = new();
    readonly Subject<CollisionEntry> _entered = new();
    readonly Subject<CollisionEntry> _again = new();
    readonly Subject<CollisionEntry> _exited = new();
    readonly CompositeDisposable _subscriptions = new();
    readonly string _inName;
    readonly string _againName;
    readonly string _outName;
    int _disposed;

    public CollisionWorld(INamedEventBus bus)
    {
        int id = Interlocked.Increment(ref _counter);
        _inName = $"colw-{id}-in";
        _againName = $"colw-{id}-again";
        _outName = $"colw-{id}-out";

        StampPatterns(_handler);

        _subscriptions.Add(bus.Observe(_inName).Subscribe(e => Push(_entered, e)));
        _subscriptions.Add(bus.Observe(_againName).Subscribe(e => Push(_again, e)));
        _subscriptions.Add(bus.Observe(_outName).Subscribe(e => Push(_exited, e)));
    }

    void StampPatterns(CollisionHandlerEvent handler)
    {
        handler.SetInPattern(_inName);
        handler.SetAgainPattern(_againName);
        handler.SetOutPattern(_outName);
    }

    static void Push(Subject<CollisionEntry> subject, NamedEvent e)
    {
        if (e.Parameters.Count == 0) return;
        if (e.Parameters[0] is INativeObject native)
        {
            var entry = native.CastTo<CollisionEntry>();
            if (entry is not null) subject.OnNext(entry);
        }
    }

    public void Add(NodePath collider, CollisionHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(collider);

        var effective = handler ?? _handler;
        // Physical responses derive from CollisionHandlerEvent; stamp our patterns so they still feed the observables.
        if (handler is INativeObject native)
        {
            var asEvent = native.CastTo<CollisionHandlerEvent>();
            if (asEvent is not null) StampPatterns(asEvent);
        }
        _traverser.AddCollider(collider, effective);
    }

    public CollisionHandlerPusher AddPusher(NodePath collider, NodePath pushee)
    {
        ArgumentNullException.ThrowIfNull(collider);
        ArgumentNullException.ThrowIfNull(pushee);
        var pusher = new CollisionHandlerPusher();
        pusher.AddCollider(collider, pushee);
        Add(collider, pusher);
        return pusher;
    }

    public CollisionHandlerFloor AddFloor(NodePath collider, NodePath floored, float offset = 0f)
    {
        ArgumentNullException.ThrowIfNull(collider);
        ArgumentNullException.ThrowIfNull(floored);
        var floor = new CollisionHandlerFloor();
        floor.SetOffset(offset);
        floor.AddCollider(collider, floored);
        Add(collider, floor);
        return floor;
    }

    public CollisionHandlerGravity AddGravity(NodePath collider, NodePath gravitated, float gravity)
    {
        ArgumentNullException.ThrowIfNull(collider);
        ArgumentNullException.ThrowIfNull(gravitated);
        var handler = new CollisionHandlerGravity();
        handler.SetGravity(gravity);
        handler.AddCollider(collider, gravitated);
        Add(collider, handler);
        return handler;
    }

    public ICollisionQuery AddQuery(NodePath collider)
    {
        ArgumentNullException.ThrowIfNull(collider);
        var queue = new CollisionHandlerQueue();
        _traverser.AddCollider(collider, queue);
        return new CollisionQuery(queue);
    }

    public void Remove(NodePath collider) => _traverser.RemoveCollider(collider);
    public void Traverse(NodePath root) => _traverser.Traverse(root);

    public RaycastHit? Raycast(LPoint3f origin, LVector3f direction, NodePath against, BitMask32? mask = null)
    {
        ArgumentNullException.ThrowIfNull(against);

        var ray = new CollisionRay();
        ray.SetOrigin(origin);
        ray.SetDirection(direction);
        // ray and its origin are in against-space
        return QueryNearest(ray, against, against, origin, mask);
    }

    public RaycastHit? Pick(NodePath camera, float filmX, float filmY, NodePath against, BitMask32? mask = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(against);

        var lens = (camera.Node() as INativeObject)?.CastTo<LensNode>()
            ?? throw new ArgumentException("Pick requires a camera node whose node is a LensNode.", nameof(camera));

        var ray = new CollisionRay();
        if (!ray.SetFromLens(lens, filmX, filmY)) return null;   // outside the film
        // SetFromLens puts the ray in camera space
        return QueryNearest(ray, camera, against, ray.GetOrigin(), mask);
    }

    // One-shot: temporary ray-collider under `space`, traverse `against`, return the nearest hit in against-space.
    RaycastHit? QueryNearest(CollisionRay ray, NodePath space, NodePath against, LPoint3f origin, BitMask32? mask)
    {
        var node = new CollisionNode("raycast-query");
        node.AddSolid(ray);
        node.SetFromCollideMask(mask ?? BitMask32.AllOn());
        node.SetIntoCollideMask(BitMask32.AllOff());
        var path = space.AttachNewNode(node);

        try
        {
            var queue = new CollisionHandlerQueue();
            var traverser = new CollisionTraverser("raycast");
            traverser.AddCollider(path, queue);
            traverser.Traverse(against);

            queue.SortEntries();
            var entries = queue.Entries;
            if (entries.Count == 0) return null;

            var entry = entries[0];
            var surface = entry.GetSurfacePoint(against);
            var normal = entry.GetSurfaceNormal(against);
            var eye = against.GetRelativePoint(space, origin);   // ray origin in against-space
            float distance = (surface - eye).Length();
            return new RaycastHit(entry.GetIntoNodePath(), surface, normal, distance);
        }
        finally
        {
            path.RemoveNode();
        }
    }

    public IObservable<CollisionEntry> Entered => _entered;
    public IObservable<CollisionEntry> Again => _again;
    public IObservable<CollisionEntry> Exited => _exited;

    public void ShowColliders(NodePath root) => _traverser.ShowCollisions(root);
    public void HideColliders() => _traverser.HideCollisions();

    public CollisionTraverser Traverser => _traverser;
    public CollisionHandlerEvent Handler => _handler;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _subscriptions.Dispose();
        _entered.OnCompleted();
        _again.OnCompleted();
        _exited.OnCompleted();
    }
}
