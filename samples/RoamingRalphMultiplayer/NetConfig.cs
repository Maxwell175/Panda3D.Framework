namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>The networking choice made on the lobby screen: host or join, where, and under what name.</summary>
internal sealed record NetConfig(bool IsHost, string ConnectAddress, ushort Port, string PlayerName);
