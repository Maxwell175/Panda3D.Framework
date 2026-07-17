using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Actors;
using Panda3D.Framework.Gui;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Input;
using Panda3D.Framework.Physics;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// Orchestrates the multiplayer demo: a lobby screen chooses host/join and a name, then the world loads and
/// this player's <see cref="LocalRalph"/> is wired to a <see cref="FollowCamera"/>, an input context, and the
/// network. Everyone else's snapshots become <see cref="RemoteRalph"/> avatars. All the Ralph/camera/network
/// behaviour lives in its own class — this type just holds them together.
/// </summary>
internal sealed class MultiplayerRalphDemo(
    ISceneManager scene,
    IViewManager views,
    IFrameScheduler scheduler,
    IActorLoader actors,
    ICollisionWorld collisions,
    IHostApplicationLifetime lifetime) : IBootstrap
{
    const ushort Port = 9050;

    IView _view = null!;
    LocalRalph _localRalph = null!;
    FollowCamera _camera = null!;
    VectorAction _move = null!;
    AxisAction _cameraOrbit = null!;
    ButtonAction _quit = null!;

    readonly string _defaultName = "Player-" + Random.Shared.Next(1000, 9999);
    NetConfig _config = null!;
    string _modelRoot = null!;
    GameClient _client = null!;
    RelayServer? _server;
    readonly Dictionary<ushort, RemoteRalph> _remotes = new();

    public async PandaTask RunAsync()
    {
        _view = views.Main;
        _view.ShowFrameRate();
        _view.ClearColor = new LVecBase4f(0.10f, 0.12f, 0.20f, 1f);   // menu backdrop
        using var closeSub = _view.Closed.Subscribe(_ => lifetime.StopApplication());

        // Resolve the view's input now so its overlay is wired to the MouseWatcher — the lobby needs the
        // mouse (buttons) and keyboard (name/address fields) before any gameplay context exists.
        _view.Services.GetRequiredService<IInput>();
        _modelRoot = Models.Mount();   // fail fast on a missing bundle, before showing the lobby

        var config = await ShowLobbyAsync();
        if (config is null)
            return;   // window closed on the lobby
        _config = config;

        LoadWorld();
        var start = scene.Root.Find("**/start_point").GetPos();
        _localRalph = new LocalRalph(actors, collisions, scene.Root, _modelRoot, start);
        _camera = new FollowCamera(_view.Camera, collisions, scene.Root, _localRalph);
        SetupLights();
        SetupInstructions();
        SetupInput();

        // The host runs a relay AND its own client (a listen-server); a joiner just connects.
        if (_config.IsHost)
            _server = new RelayServer(_config.Port);
        _client = new GameClient(_config.ConnectAddress);
        _client.StateReceived += OnRemoteState;
        _client.PlayerLeft += OnRemoteLeft;

        using var moveTask = scheduler.AddFrameTask(Move, name: "mp-ralph-move");
        try
        {
            while (!lifetime.ApplicationStopping.IsCancellationRequested)
                await PandaTask.NextFrame();
        }
        finally
        {
            _client.Dispose();
            _server?.Dispose();
            _localRalph.Dispose();
            foreach (var remote in _remotes.Values)
                remote.Dispose();
        }
    }

    TaskResult Move(FrameContext frame)
    {
        if (_quit.WasPressed)
        {
            lifetime.StopApplication();
            return TaskResult.Done;
        }

        float dt = (float)frame.Dt;
        var input = _move.Value;
        // CompositeVectorBinding is +X-right; the original sample turns left on a positive heading, so negate.
        float turn = -input.GetX(), forward = input.GetY();

        _localRalph.Update(turn, forward, dt);
        _localRalph.SnapToTerrain();
        _camera.Update(_cameraOrbit.Value, dt);

        NetworkTick();
        return TaskResult.Continue;
    }

    void NetworkTick()
    {
        _server?.Update();
        _client.Update();   // fires OnRemoteState / OnRemoteLeft synchronously on this (game) thread

        if (_client.IsConnected)
            _client.SendState(_localRalph.Snapshot(_client.LocalId, _config.PlayerName));
    }

    void OnRemoteState(PlayerState state)
    {
        if (!_remotes.TryGetValue(state.Id, out var remote))
            _remotes[state.Id] = remote = new RemoteRalph(actors, scene.Root, _modelRoot, state.Name);
        remote.Apply(state);
    }

    void OnRemoteLeft(ushort id)
    {
        if (_remotes.Remove(id, out var remote))
            remote.Dispose();
    }

    // ---- scene setup ----

    void LoadWorld()
    {
        var world = new NodePath(new Loader("mp-ralph").LoadSync(
            Filename.FromOsSpecific(Models.At(_modelRoot, "world.bam"))));
        world.ReparentTo(scene.Root);
        _view.ClearColor = new LVecBase4f(0.53f, 0.80f, 0.92f, 1f);   // sky
    }

    void SetupLights()
    {
        var ambient = new AmbientLight("ambient");
        ambient.SetColor(new LVecBase4f(0.3f, 0.3f, 0.3f, 1f));

        var sun = new DirectionalLight("sun");
        sun.SetDirection(new LVector3f(-5f, -5f, -5f));
        sun.SetColor(new LVecBase4f(1f, 1f, 1f, 1f));
        sun.SetSpecularColor(new LVecBase4f(1f, 1f, 1f, 1f));

        scene.Root.SetLight(scene.Root.AttachNewNode(ambient));
        scene.Root.SetLight(scene.Root.AttachNewNode(sun));
    }

    void SetupInstructions()
    {
        var node = new TextNode("hud");
        node.SetText($"You: {_config.PlayerName}\nArrows/WASD: Move   Q/E: Camera   Esc: Quit");
        node.SetTextColor(1f, 1f, 1f, 1f);
        node.SetShadow(0.04f, 0.04f);
        node.SetShadowColor(0f, 0f, 0f, 1f);
        node.SetAlign(TextPropertiesAlignment.ALeft);

        var path = _view.OverlayAnchors[OverlayAnchor.TopLeft].AttachNewNode(node);
        path.SetScale(0.05f);
        path.SetPos(0.08f, 0f, -0.12f);
    }

    void SetupInput()
    {
        var ctx = _view.Services.CreateContext("gameplay");

        _move = ctx.Add(new VectorAction("move"));
        _move.Bindings.Add(new CompositeVectorBinding(Keys.Up, Keys.Down, Keys.Left, Keys.Right));
        _move.Bindings.Add(new CompositeVectorBinding(Keys.Ascii('w'), Keys.Ascii('s'), Keys.Ascii('a'), Keys.Ascii('d')));

        _cameraOrbit = ctx.Add(new AxisAction("camera-orbit"));
        _cameraOrbit.Bindings.Add(new CompositeAxisBinding(Keys.Ascii('q'), Keys.Ascii('e')));

        _quit = ctx.Add(new ButtonAction("quit"));
        _quit.Bindings.Add(new ButtonBinding(Keys.Escape));
    }

    // ---- lobby ----

    // Builds the lobby overlay and suspends until Host/Join is clicked (the frame loop keeps rendering and
    // pumping GUI events while we await). Returns null if the window is closed first.
    async PandaTask<NetConfig?> ShowLobbyAsync()
    {
        var gui = _view.Services.GetRequiredService<IGui>();
        var widgets = new List<IDisposable>();
        T Track<T>(T widget) where T : IDisposable { widgets.Add(widget); return widget; }

        // Everything is centered on x = 0: labels/title/hint are center-aligned text, and the entry boxes
        // (~0.98 wide at this scale) are offset by half their width so they sit centered too.
        const float entryHalfWidth = 0.49f;

        var title = Track(gui.Add(Centered(new Label("Roaming Ralph  —  Multiplayer", "title"))));
        title.Node.SetScale(0.10f);
        title.Node.SetPos(0f, 0f, 0.58f);

        var nameLabel = Track(gui.Add(Centered(new Label("Name", "name-label"))));
        nameLabel.Node.SetScale(0.06f);
        nameLabel.Node.SetPos(0f, 0f, 0.33f);
        var nameEntry = Track(gui.Add(new Entry(width: 16f, name: "name-entry")));
        nameEntry.Text = _defaultName;
        nameEntry.Node.SetScale(0.06f);
        nameEntry.Node.SetPos(-entryHalfWidth, 0f, 0.22f);

        var addrLabel = Track(gui.Add(Centered(new Label("Host address", "addr-label"))));
        addrLabel.Node.SetScale(0.06f);
        addrLabel.Node.SetPos(0f, 0f, 0.02f);
        var addrEntry = Track(gui.Add(new Entry(width: 16f, name: "addr-entry")));
        addrEntry.Text = "127.0.0.1";
        addrEntry.Node.SetScale(0.06f);
        addrEntry.Node.SetPos(-entryHalfWidth, 0f, -0.09f);

        var hostButton = Track(gui.Add(new Button("Host", name: "host")));
        hostButton.Node.SetPos(-0.28f, 0f, -0.4f);
        var joinButton = Track(gui.Add(new Button("Join", name: "join")));
        joinButton.Node.SetPos(0.28f, 0f, -0.4f);

        var hint = Track(gui.Add(Centered(new Label("Host a game, or type a host's address and Join.", "hint"))));
        hint.Node.SetScale(0.045f);
        hint.Node.SetPos(0f, 0f, -0.58f);

        nameEntry.Focus();

        var choice = new PandaTaskCompletionSource<NetConfig?>();
        using var onHost = hostButton.Clicked.Subscribe(_ =>
            choice.TrySetResult(new NetConfig(true, $"127.0.0.1:{Port}", Port, PlayerName(nameEntry))));
        using var onJoin = joinButton.Clicked.Subscribe(_ =>
            choice.TrySetResult(new NetConfig(false, HostAddress(addrEntry), Port, PlayerName(nameEntry))));
        using var onQuit = lifetime.ApplicationStopping.Register(() => choice.TrySetResult(null));

        var config = await choice.Task;

        foreach (var widget in widgets)
            widget.Dispose();
        return config;
    }

    static Label Centered(Label label)
    {
        label.TextNode.SetAlign(TextPropertiesAlignment.ACenter);
        return label;
    }

    static string PlayerName(Entry entry)
        => string.IsNullOrWhiteSpace(entry.Text) ? "Player" : entry.Text.Trim();

    static string HostAddress(Entry entry)
    {
        string address = entry.Text.Trim();
        if (address.Length == 0)
            address = "127.0.0.1";
        return address.Contains(':') ? address : $"{address}:{Port}";
    }
}
