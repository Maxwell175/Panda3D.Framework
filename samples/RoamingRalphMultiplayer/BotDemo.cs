using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Physics;
using Thread = System.Threading.Thread;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// A headless client — no window, no rendering, no models or textures. It loads only the world's collision
/// geometry, drives a <see cref="BotRalph"/> from stdin, and streams its <see cref="PlayerState"/> to the
/// host. This is roughly the shape of an NPC/AI server: pure simulation + networking, controllable by an
/// external process over stdin (the framework's headless build ticks the clock off the task chain, so no
/// rendering is needed to run the loop).
/// </summary>
internal sealed class BotDemo(
    ISceneManager scene,
    IFrameScheduler scheduler,
    ICollisionWorld collisions,
    IHostApplicationLifetime lifetime,
    NetConfig config) : IBootstrap
{
    BotRalph _bot = null!;
    GameClient _client = null!;

    readonly object _inputLock = new();
    float _turn, _forward;
    readonly HashSet<ushort> _seen = new();

    public async PandaTask RunAsync()
    {
        string modelRoot = Models.Mount();
        var start = LoadWorld(modelRoot);
        _bot = new BotRalph(collisions, scene.Root, start);

        _client = new GameClient(config.ConnectAddress);
        _client.StateReceived += OnRemoteState;
        _client.PlayerLeft += id => Console.WriteLine($"[net] player {id} left");

        Console.WriteLine($"Headless bot \"{config.PlayerName}\" joining {config.ConnectAddress}.");
        Console.WriteLine("Commands (stdin): w/s = forward/back, a/d = turn, x = stop, q = quit, or 'move <turn> <forward>'.");
        StartStdinReader();

        using var tick = scheduler.AddFrameTask(Tick, name: "bot-tick");
        try
        {
            while (!lifetime.ApplicationStopping.IsCancellationRequested)
                await PandaTask.NextFrame();
        }
        finally
        {
            _client.Dispose();
        }
    }

    TaskResult Tick(FrameContext frame)
    {
        float turn, forward;
        lock (_inputLock) { turn = _turn; forward = _forward; }

        _bot.Update(turn, forward, (float)frame.Dt);
        _bot.SnapToTerrain();

        _client.Update();
        if (_client.IsConnected)
            _client.SendState(_bot.Snapshot(_client.LocalId, config.PlayerName));
        return TaskResult.Continue;
    }

    // An AI would steer from what it sees here; this demo just announces first sightings.
    void OnRemoteState(PlayerState state)
    {
        if (_seen.Add(state.Id))
            Console.WriteLine($"[net] now seeing \"{state.Name}\" (#{state.Id})");
    }

    void StartStdinReader()
    {
        var thread = new Thread(() =>
        {
            string? line;
            while ((line = Console.ReadLine()) is not null)
            {
                string cmd = line.Trim().ToLowerInvariant();
                lock (_inputLock)
                {
                    switch (cmd)
                    {
                        case "w": _forward = 1f; break;
                        case "s": _forward = -1f; break;
                        case "a": _turn = -1f; break;
                        case "d": _turn = 1f; break;
                        case "x" or "stop": _turn = 0f; _forward = 0f; break;
                        case "q" or "quit": lifetime.StopApplication(); return;
                        default:
                            var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 3 && parts[0] == "move"
                                && float.TryParse(parts[1], out float t) && float.TryParse(parts[2], out float f))
                            {
                                _turn = t;
                                _forward = f;
                            }
                            break;
                    }
                }
            }
        })
        { IsBackground = true, Name = "bot-stdin" };
        thread.Start();
    }

    // Loads the world for its collision geometry and returns the spawn point.
    //
    // We keep the full art asset as a single .bam (shared with the rendered client) rather than a stripped
    // collision-only file, and filter it down to collision *after* loading. Two things keep that cheap: the
    // build already compiled the egg to a .bam (that parse peaks ~280 MB; loading the .bam is ~90 MB), and
    // texture pixels are never read (a headless bot never renders). Stripping the visuals afterwards doesn't
    // return memory to the OS — Panda's allocator keeps freed blocks in its pool — but it *is* reused by later
    // loads, so a server that pulls collision from many models stays bounded instead of growing per model.
    LPoint3f LoadWorld(string modelRoot)
    {
        new ConfigVariableBool("preload-textures").Value = false;          // headless: never read texture pixels
        new ConfigVariableBool("preload-simple-textures").Value = false;

        string bam = Models.At(modelRoot, "world.bam");
        Console.WriteLine($"Loading {bam} from the mounted bundle.");
        var world = new NodePath(new Loader("bot").LoadSync(Filename.FromOsSpecific(bam)));

        world.StripToCollision();                                         // empty visuals; hierarchy survives
        world.ReparentTo(scene.Root);

        Console.WriteLine($"Loaded {world.FindAllMatches("**/+CollisionNode").GetNumPaths()} collision node(s) (visuals stripped).");
        return world.Find("**/start_point").GetPos(scene.Root);          // marker survives the strip
    }
}
