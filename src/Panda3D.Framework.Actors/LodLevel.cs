namespace Panda3D.Framework.Actors;

/// <summary>One level-of-detail band: a name and its switch-in/switch-out distances.</summary>
public readonly record struct LodLevel(string Name, float SwitchIn, float SwitchOut);
