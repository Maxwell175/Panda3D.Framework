using System.Collections.Generic;
using Panda3D.Core;

namespace Panda3D.Framework.Physics;

/// <summary>
/// Managed reader over a <see cref="CollisionHandlerQueue"/>. Each accessor sorts nearest-first; read it
/// after the world has traversed this frame.
/// </summary>
internal sealed class CollisionQuery : ICollisionQuery
{
    readonly CollisionHandlerQueue _queue;

    public CollisionQuery(CollisionHandlerQueue queue) => _queue = queue;

    public CollisionHandlerQueue Native => _queue;

    public IReadOnlyList<CollisionEntry> Hits
    {
        get
        {
            _queue.SortEntries();
            // copy out: Entries is a live view the next traverse mutates
            return new List<CollisionEntry>(_queue.Entries);
        }
    }

    public CollisionEntry? Nearest
    {
        get
        {
            _queue.SortEntries();
            var entries = _queue.Entries;
            return entries.Count > 0 ? entries[0] : null;
        }
    }

    public CollisionEntry? NearestInto(string intoName)
    {
        _queue.SortEntries();
        foreach (var entry in _queue.Entries)
        {
            if (entry.GetIntoNodePath().GetName() == intoName)
                return entry;
        }
        return null;
    }
}
