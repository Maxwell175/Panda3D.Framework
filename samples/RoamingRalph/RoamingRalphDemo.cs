using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Actors;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Input;
using Panda3D.Framework.Physics;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework.Samples.RoamingRalph;

/// <summary>
/// Ralph roams a terrain — a tour of the framework: DI-injected services, a loaded actor with blended
/// animations, an input context, and the built-in collision system (a wall pusher + polled ground rays).
/// Fields are assigned once during setup in <see cref="RunAsync"/> (they need the running view / loaded
/// assets, so they can't be constructor-assigned) and read-only in spirit thereafter.
/// </summary>
internal sealed class RoamingRalphDemo(
    ISceneManager scene,
    IViewManager views,
    IFrameScheduler scheduler,
    IActorLoader actors,
    ICollisionWorld collisions,
    IHostApplicationLifetime lifetime) : IBootstrap
{
    const string Run = "run", Walk = "walk", Terrain = "terrain";
    const string BundleName = "ralph.mf", ModelRoot = "/models";   // the build's multifile, and where we mount it
    const float GroundRayHeight = 9f;   // cast the ground ray from above the terrain, straight down
    const float RalphScale = 0.2f, StartHeight = 1.5f, LookAtHeight = 2f;
    const float TurnSpeed = 300f, ForwardSpeed = 20f, BackwardSpeed = 10f, OrbitSpeed = 20f;
    const float MinCameraDist = 5f, MaxCameraDist = 10f, CameraTerrainOffset = 1.5f, CameraMinOffset = 2f;

    IView _view = null!;
    IActor _ralph = null!;
    NodePath _floater = null!;
    ICollisionQuery _ralphGround = null!, _cameraGround = null!;
    VectorAction _move = null!;
    AxisAction _cameraOrbit = null!;
    ButtonAction _quit = null!;
    string? _currentAnim;

    public async PandaTask RunAsync()
    {
        _view = views.Main;
        _view.ShowFrameRate();
        using var closeSub = _view.Closed.Subscribe(_ => lifetime.StopApplication());

        string root = MountModels();
        LoadWorld(root);
        LoadRalph(root);
        SetupCamera();
        SetupCollisions();
        SetupLights();
        SetupInstructions();
        SetupInput();

        using var moveTask = scheduler.AddFrameTask(Move, name: "roaming-ralph-move");
        while (!lifetime.ApplicationStopping.IsCancellationRequested)
            await PandaTask.NextFrame();
    }

    void LoadWorld(string root)
    {
        var world = new NodePath(new Loader("roaming-ralph").LoadSync(Filename.FromOsSpecific(Model(root, "world.bam"))));
        world.ReparentTo(scene.Root);
        _view.ClearColor = new LVecBase4f(0.53f, 0.80f, 0.92f, 1f);   // sky
    }

    void LoadRalph(string root)
    {
        _ralph = actors.Load(Model(root, "ralph.bam"), new Dictionary<string, string>
        {
            [Run] = Model(root, "ralph-run.bam"),
            [Walk] = Model(root, "ralph-walk.bam"),
        });

        var start = scene.Root.Find("**/start_point").GetPos();
        _ralph.Node.ReparentTo(scene.Root);
        _ralph.Node.SetScale(RalphScale);
        _ralph.Node.SetPos(start.GetX(), start.GetY(), start.GetZ() + StartHeight);

        _floater = _ralph.Node.AttachNewNode("floater");
        _floater.SetZ(LookAtHeight);
        PoseIdle();
    }

    void SetupCamera()
    {
        _view.Camera.SetPerspective(60f);
        _view.Camera.Node.SetPos(_ralph.Node.GetX(), _ralph.Node.GetY() + MaxCameraDist, CameraMinOffset);
        _view.Camera.Node.LookAt(_floater);
    }

    // One shared per-frame traverse drives a wall pusher (slides Ralph along walls) and two downward ground
    // rays we poll for terrain height (see UpdateTerrainHeights) — no extra passes.
    void SetupCollisions()
    {
        var body = FromCollider("ralph");
        body.AddSolid(new CollisionSphere(0f, 0f, 2f, 1.5f));
        body.AddSolid(new CollisionSphere(0f, -0.25f, 4f, 1.5f));
        collisions.AddPusher(_ralph.Node.AttachNewNode(body), _ralph.Node).Horizontal = true;

        _ralphGround = GroundRay(_ralph.Node, "ralphRay");
        _cameraGround = GroundRay(_view.Camera.Node, "camRay");
    }

    ICollisionQuery GroundRay(NodePath parent, string name)
    {
        var ray = new CollisionRay();
        ray.SetOrigin(0f, 0f, GroundRayHeight);
        ray.SetDirection(0f, 0f, -1f);
        var collider = FromCollider(name);
        collider.AddSolid(ray);
        return collisions.AddQuery(parent.AttachNewNode(collider));
    }

    static CollisionNode FromCollider(string name)
    {
        var node = new CollisionNode(name);
        node.SetFromCollideMask(BitMask32.Bit(0));   // hits terrain-masked into-geometry
        node.SetIntoCollideMask(BitMask32.AllOff());
        return node;
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
        AddText(_view.OverlayAnchors[OverlayAnchor.TopLeft],
            "Esc: Quit\nLeft/Right or A/D: Turn Ralph\nUp/Down or W/S: Move\nQ/E: Rotate camera",
            0.08f, -0.10f, 0.045f, TextPropertiesAlignment.ALeft);
        AddText(_view.OverlayAnchors[OverlayAnchor.BottomRight],
            "Roaming Ralph - Panda3D.Framework",
            -0.08f, 0.08f, 0.06f, TextPropertiesAlignment.ARight);
    }

    static void AddText(NodePath parent, string text, float x, float z, float scale, TextPropertiesAlignment align)
    {
        var node = new TextNode(text.Split('\n')[0]);
        node.SetText(text);
        node.SetTextColor(1f, 1f, 1f, 1f);
        node.SetShadow(0.04f, 0.04f);
        node.SetShadowColor(0f, 0f, 0f, 1f);
        node.SetAlign(align);

        // Scale on the NodePath (like direct's OnscreenText), not TextNode.SetTextScale — that way the
        // shadow offset scales with the text and stays a subtle 0.04.
        var path = parent.AttachNewNode(node);
        path.SetScale(scale);
        path.SetPos(x, 0f, z);
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

    TaskResult Move(FrameContext frame)
    {
        if (_quit.WasPressed)
        {
            lifetime.StopApplication();
            return TaskResult.Done;
        }

        float dt = (float)frame.Dt;
        var camera = _view.Camera.Node;
        var move = _move.Value;
        // CompositeVectorBinding is +X-right; the original sample turns left on a positive heading, so negate.
        float turn = -move.GetX(), forward = move.GetY();

        if (_cameraOrbit.Value != 0f)
            camera.SetX(camera, _cameraOrbit.Value * OrbitSpeed * dt);
        MoveRalph(turn, forward, dt);
        UpdateAnimation(forward, turn);
        ClampCameraDistance(camera);
        UpdateTerrainHeights(camera);
        camera.LookAt(_floater);
        return TaskResult.Continue;
    }

    void MoveRalph(float turn, float forward, float dt)
    {
        var node = _ralph.Node;
        if (turn != 0f)
            node.SetH(node.GetH() + turn * TurnSpeed * dt);
        if (forward > 0f)
            node.SetY(node, -ForwardSpeed * forward * dt);
        else if (forward < 0f)
            node.SetY(node, BackwardSpeed * -forward * dt);
    }

    // The shared traverse already ran the pusher and filled the ground rays; we just read them. (The read
    // trails the traverse by one frame — fine for a walking character; use AddCollision(autoTraverse: false)
    // for lockstep.)
    void UpdateTerrainHeights(NodePath camera)
    {
        DropToTerrain(_ralphGround, _ralph.Node, 0f);
        DropToTerrain(_cameraGround, camera, CameraTerrainOffset);

        float minZ = _ralph.Node.GetZ() + CameraMinOffset;
        if (camera.GetZ() < minZ)
            camera.SetZ(minZ);
    }

    void DropToTerrain(ICollisionQuery ground, NodePath node, float offset)
    {
        if (ground.NearestInto(Terrain) is { } hit)
            node.SetZ(hit.GetSurfacePoint(scene.Root).GetZ() + offset);
    }

    void UpdateAnimation(float forward, float turn)
    {
        if (forward > 0f) PlayAnimation(Run, 1f);
        else if (forward < 0f) PlayAnimation(Walk, -1f);
        else if (turn != 0f) PlayAnimation(Walk, 1f);
        else if (_currentAnim is not null) PoseIdle();
    }

    void PlayAnimation(string name, double playRate)
    {
        var control = _ralph.Anim(name);
        control.SetPlayRate(playRate);
        if (_currentAnim == name)
            return;

        if (_currentAnim is not null)
            _ralph.Anim(_currentAnim).Stop();
        control.Loop(restart: true);
        _currentAnim = name;
    }

    void PoseIdle()
    {
        if (_currentAnim is not null)
            _ralph.Anim(_currentAnim).Stop();
        _ralph.Anim(Walk).Pose(5);
        _currentAnim = null;
    }

    void ClampCameraDistance(NodePath camera)
    {
        var ralph = _ralph.Node;
        float dx = ralph.GetX() - camera.GetX(), dy = ralph.GetY() - camera.GetY();
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= 0.001f)
            return;

        float correction = distance > MaxCameraDist ? distance - MaxCameraDist
                         : distance < MinCameraDist ? distance - MinCameraDist
                         : 0f;
        if (correction != 0f)
            camera.SetPos(camera.GetX() + dx / distance * correction,
                          camera.GetY() + dy / distance * correction,
                          camera.GetZ());
    }

    // Assets live inside the mounted multifile, so paths are VFS paths (always unix-style), not OS paths.
    static string Model(string root, string file) => $"{root}/{file}";

    /// <summary>
    /// Mount the multifile the build produced (see the resource pipeline in the .csproj: the source eggs
    /// are compiled to .bam and packed with their textures into ralph.mf, dropped next to the executable)
    /// and return the mount point everything loads from.
    /// </summary>
    static string MountModels()
    {
        var bundle = Path.Combine(AppContext.BaseDirectory, BundleName);
        if (!VirtualFileSystem.GetGlobalPtr().Mount(
                Filename.FromOsSpecific(bundle), Filename.FromOsSpecific(ModelRoot), 0))
            throw new FileNotFoundException($"Could not mount the model bundle '{bundle}'. Build the project to produce it.");
        return ModelRoot;
    }
}
