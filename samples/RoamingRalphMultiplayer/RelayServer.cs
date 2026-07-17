using System;
using Riptide;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// A dumb relay: it never simulates anything, it just forwards each player's snapshot to everyone else and
/// tells everyone when a player leaves. The host runs one of these alongside its own <see cref="GameClient"/>
/// (a "listen server"); dedicated servers would run only this.
/// </summary>
internal sealed class RelayServer : IDisposable
{
    readonly Server _server = new();

    public RelayServer(ushort port)
    {
        _server.MessageReceived += OnMessage;
        _server.ClientDisconnected += OnDisconnected;
        // useMessageHandlers: false → deliver via the MessageReceived event instead of [MessageHandler] statics.
        _server.Start(port, maxClientCount: 16, messageHandlerGroupId: 0, useMessageHandlers: false);
    }

    public void Update() => _server.Update();

    void OnMessage(object? sender, MessageReceivedEventArgs e)
    {
        if (e.MessageId != PlayerState.StateMessage)
            return;

        // Re-broadcast the snapshot to everyone but the sender, stamped with the sender's authoritative id.
        var state = PlayerState.Read(e.Message) with { Id = e.FromConnection.Id };
        var relay = Message.Create(MessageSendMode.Unreliable, PlayerState.StateMessage);
        state.Write(relay);
        _server.SendToAll(relay, exceptToClientId: e.FromConnection.Id);
    }

    void OnDisconnected(object? sender, ServerDisconnectedEventArgs e)
    {
        var left = Message.Create(MessageSendMode.Reliable, PlayerState.LeftMessage);
        left.AddUShort(e.Client.Id);
        _server.SendToAll(left);
    }

    public void Dispose() => _server.Stop();
}
