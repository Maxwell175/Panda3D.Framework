using System.Collections.Generic;

namespace Panda3D.Framework.Actors;

/// <summary>A single part of a multipart actor: a model and its named animations.</summary>
public readonly record struct ActorPart(string Model, IReadOnlyDictionary<string, string>? Anims = null);
