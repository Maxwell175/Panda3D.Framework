using System.Collections.Generic;
using Panda3D.Core;

namespace Panda3D.Framework.Physics;

/// <summary>
/// A persistent query on the world's shared per-frame traverse (via <see cref="ICollisionWorld.AddQuery"/>).
/// Read its results after each traverse; they are engine <see cref="CollisionEntry"/> objects.
/// </summary>
public interface ICollisionQuery
{
    /// <summary>This query's hits from the most recent traverse, sorted nearest-first.</summary>
    IReadOnlyList<CollisionEntry> Hits { get; }

    /// <summary>The nearest hit from the most recent traverse, or <see langword="null"/> if there were none.</summary>
    CollisionEntry? Nearest { get; }

    /// <summary>The nearest hit whose into-node is named <paramref name="intoName"/>, or <see langword="null"/>.</summary>
    CollisionEntry? NearestInto(string intoName);

    /// <summary>The native handler queue — escape hatch for reading entries directly.</summary>
    CollisionHandlerQueue Native { get; }
}
