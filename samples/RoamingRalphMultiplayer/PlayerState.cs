using Riptide;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// The per-player snapshot synced over the network: who they are (<see cref="Id"/>/<see cref="Name"/>),
/// where they are (<see cref="X"/>/<see cref="Y"/>/<see cref="Z"/>/<see cref="H"/> heading), and what
/// they're doing (<see cref="Anim"/> clip + <see cref="Rate"/>). Read/written straight to a Riptide message.
/// </summary>
internal readonly record struct PlayerState(
    ushort Id, string Name, float X, float Y, float Z, float H, string Anim, float Rate)
{
    public const ushort StateMessage = 1;   // a player snapshot
    public const ushort LeftMessage = 2;    // a player disconnected

    public void Write(Message m)
    {
        m.AddUShort(Id);
        m.AddString(Name);
        m.AddFloat(X);
        m.AddFloat(Y);
        m.AddFloat(Z);
        m.AddFloat(H);
        m.AddString(Anim);
        m.AddFloat(Rate);
    }

    public static PlayerState Read(Message m) => new(
        m.GetUShort(), m.GetString(),
        m.GetFloat(), m.GetFloat(), m.GetFloat(), m.GetFloat(),
        m.GetString(), m.GetFloat());
}
