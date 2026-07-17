using System;
using System.Reactive.Linq;
using Panda3D.Core;

namespace Panda3D.Framework.Physics;

/// <summary>
/// Filters the collision streams (<see cref="ICollisionWorld.Entered"/>/<c>Again</c>/<c>Exited</c>) by the
/// colliding pair: <c>world.Entered.Into("wall")</c>, <c>world.Exited.By(playerNode)</c>. A
/// <see cref="NodePath"/> matches that one node; a <see cref="string"/> matches on node name.
/// </summary>
public static class CollisionEntryStreams
{
    /// <summary>Only entries whose from- (collider) side is <paramref name="from"/>.</summary>
    public static IObservable<CollisionEntry> By(this IObservable<CollisionEntry> stream, NodePath from)
        => stream.Where(e => SameNode(e.GetFromNodePath(), from));

    /// <summary>Only entries whose from- (collider) side has node name <paramref name="fromName"/>.</summary>
    public static IObservable<CollisionEntry> By(this IObservable<CollisionEntry> stream, string fromName)
        => stream.Where(e => e.GetFromNodePath().GetName() == fromName);

    /// <summary>Only entries whose into- (struck) side is <paramref name="into"/>.</summary>
    public static IObservable<CollisionEntry> Into(this IObservable<CollisionEntry> stream, NodePath into)
        => stream.Where(e => SameNode(e.GetIntoNodePath(), into));

    /// <summary>Only entries whose into- (struck) side has node name <paramref name="intoName"/>.</summary>
    public static IObservable<CollisionEntry> Into(this IObservable<CollisionEntry> stream, string intoName)
        => stream.Where(e => e.GetIntoNodePath().GetName() == intoName);

    static bool SameNode(NodePath a, NodePath b)
    {
        var na = a?.Node();
        var nb = b?.Node();
        return na is not null && na.Equals(nb);
    }
}
