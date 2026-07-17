using System;
using Riptide;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// This player's link to the relay: connect, push our snapshot each frame, and raise events for the
/// snapshots and leave-notices of everyone else. <see cref="Update"/> pumps Riptide and fires the events
/// synchronously on the calling (game) thread, so handlers can touch the scene graph directly.
/// </summary>
internal sealed class GameClient : IDisposable
{
    readonly Client _client = new();

    public event Action<PlayerState>? StateReceived;
    public event Action<ushort>? PlayerLeft;

    public GameClient(string hostAddress)
    {
        _client.MessageReceived += OnMessage;
        _client.Connect(hostAddress, messageHandlerGroupId: 0, useMessageHandlers: false);
    }

    public bool IsConnected => _client.IsConnected;
    public ushort LocalId => _client.Id;

    public void Update() => _client.Update();

    public void SendState(PlayerState state)
    {
        if (!_client.IsConnected)
            return;
        var m = Message.Create(MessageSendMode.Unreliable, PlayerState.StateMessage);
        state.Write(m);
        _client.Send(m);
    }

    void OnMessage(object? sender, MessageReceivedEventArgs e)
    {
        if (e.MessageId == PlayerState.StateMessage)
            StateReceived?.Invoke(PlayerState.Read(e.Message));
        else if (e.MessageId == PlayerState.LeftMessage)
            PlayerLeft?.Invoke(e.Message.GetUShort());
    }

    public void Dispose() => _client.Disconnect();
}
