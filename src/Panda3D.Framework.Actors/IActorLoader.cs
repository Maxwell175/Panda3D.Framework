using System.Collections.Generic;
using Panda3D.Async;

namespace Panda3D.Framework.Actors;

/// <summary>Loads models + named animations into an <see cref="IActor"/>.</summary>
public interface IActorLoader
{
    /// <summary>Asynchronously load a single-model actor with optional named animations.</summary>
    PandaTask<IActor> LoadAsync(string model, IReadOnlyDictionary<string, string>? anims = null, ActorOptions? options = null);

    /// <summary>Synchronously load a single-model actor with optional named animations.</summary>
    IActor Load(string model, IReadOnlyDictionary<string, string>? anims = null, ActorOptions? options = null);

    /// <summary>Synchronously load a single-model actor with a set of named animation clips.</summary>
    IActor Load(string model, params AnimClip[] anims);

    /// <summary>Asynchronously load a multipart actor from a part map.</summary>
    PandaTask<IActor> LoadAsync(IReadOnlyDictionary<string, ActorPart> parts, ActorOptions? options = null);

    /// <summary>Asynchronously load an actor from a full definition (multipart + LODs).</summary>
    PandaTask<IActor> LoadAsync(ActorDefinition definition, ActorOptions? options = null);
}
