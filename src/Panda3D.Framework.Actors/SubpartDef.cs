namespace Panda3D.Framework.Actors;

/// <summary>A subpart definition: the joints to include and exclude.</summary>
public readonly record struct SubpartDef(string[] IncludeJoints, string[] ExcludeJoints);
