using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Panda3D.Framework;
using Panda3D.Framework.Actors;
using Panda3D.Framework.Events;
using Panda3D.Framework.Gui;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Physics;
using Panda3D.Framework.Scheduling;
using Panda3D.Framework.Samples.RoamingRalphMultiplayer;
using Riptide.Utils;

const ushort DefaultPort = 9050;

RiptideLogger.Initialize(Console.WriteLine, includeTimestamps: false);

if (args.Contains("--bot"))
    RunHeadlessBot(args);
else
    RunClient(args);

// A normal player: renders, opens a window, and shows the host/join lobby.
void RunClient(string[] clientArgs)
{
    var builder = GameApplication.CreateBuilder(clientArgs);

    builder.Services.AddClock(o => { o.LimitFrameRate = true; o.MaxDt = 1.0 / 20.0; });
    builder.Services.AddGame(o => o.Window.Title = "Roaming Ralph (multiplayer)");
    builder.Services.AddGui();
    builder.Services.AddActors();
    builder.Services.AddCollision();
    builder.Services.AddBootstrap<MultiplayerRalphDemo>();

    builder.Build().Run();
}

// A headless NPC-server-style client: collision, networking, and stdin — no rendering/window/input/actors.
void RunHeadlessBot(string[] botArgs)
{
    var config = ParseBotConfig(botArgs);

    var builder = GameApplication.CreateBuilder(botArgs);

    builder.Services.AddSceneManager();
    builder.Services.AddEvents();
    builder.Services.AddClock(o => { o.LimitFrameRate = true; o.MaxFps = 30; o.MaxDt = 1.0 / 20.0; });
    builder.Services.AddScheduler();
    builder.Services.AddCollision();
    builder.Services.AddSingleton(config);
    builder.Services.AddBootstrap<BotDemo>();

    builder.Build().Run();
}

NetConfig ParseBotConfig(string[] botArgs)
{
    string connect = "127.0.0.1";
    string name = "Bot-" + Random.Shared.Next(100, 999);
    ushort port = DefaultPort;

    for (int i = 0; i < botArgs.Length; i++)
    {
        if (botArgs[i] == "--connect" && i + 1 < botArgs.Length) connect = botArgs[++i];
        else if (botArgs[i] == "--name" && i + 1 < botArgs.Length) name = botArgs[++i];
        else if (botArgs[i] == "--port" && i + 1 < botArgs.Length) port = ushort.Parse(botArgs[++i]);
    }

    if (!connect.Contains(':'))
        connect += $":{port}";
    return new NetConfig(IsHost: false, ConnectAddress: connect, Port: port, PlayerName: name);
}
