using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework.Events;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Scheduling;
using Xunit;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Panda3D.Framework.Physics.Tests;

/// <summary>
/// Drives the real host loop headlessly: two overlapping collision spheres, an explicit traverse at
/// the collision slot, and the in-event flowing through the pump into a typed observable carrying the
/// native <see cref="CollisionEntry"/>.
/// </summary>
public sealed class CollisionWorldTests
{
    sealed class Probe
    {
        public bool Entered;
        public bool EnteredIntoWall;
        public string? FromName;
        public string? IntoName;
        public string? SpaceRootName;
        public bool RaycastHit;
        public string? RaycastInto;
        public float RaycastDistance;
        public bool PusherEntered;
        public int QueryEntries;
        public string? QueryNearestInto;
    }

    sealed class DelegateBootstrap : IBootstrap
    {
        readonly Func<PandaTask> _body;
        public DelegateBootstrap(Func<PandaTask> body) => _body = body;
        public PandaTask RunAsync() => _body();
    }

    [Fact]
    public void OverlappingCollidersRaiseTypedEnteredObservable()
    {
        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddCollision();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => BodyAsync(sp)));

        var app = builder.Build();
        app.Run();

        Assert.True(probe.Entered, "the collision should have raised Entered via the pump");
        Assert.Equal("player", probe.FromName);
        Assert.Equal("wall", probe.IntoName);
        Assert.True(probe.EnteredIntoWall, "the Entered.Into(\"wall\") filter should have matched");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var world = sp.GetRequiredService<ICollisionWorld>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            // From-collider (the moving thing) at the origin.
            var fromNode = new CollisionNode("player");
            fromNode.AddSolid(new CollisionSphere(0, 0, 0, 1));
            fromNode.SetFromCollideMask(BitMask32.AllOn());
            fromNode.SetIntoCollideMask(BitMask32.AllOff());
            var from = scene.Root.AttachNewNode(fromNode);

            // Into-collider (the wall) overlapping it — no registration needed, participates by mask.
            var intoNode = new CollisionNode("wall");
            intoNode.AddSolid(new CollisionSphere(0, 0, 0, 1));
            intoNode.SetIntoCollideMask(BitMask32.AllOn());
            var into = scene.Root.AttachNewNode(intoNode);
            into.SetX(1.0f);   // centres 1 apart, radii 1+1 → overlap

            world.Add(from);
            using var s1 = world.Entered.Subscribe(e =>
            {
                probe.Entered = true;
                probe.FromName = e.GetFromNodePath().GetName();
                probe.IntoName = e.GetIntoNodePath().GetName();
                // Exercise reading geometry off the native entry.
                probe.SpaceRootName = e.GetSurfacePoint(scene.Root) is not null ? "ok" : null;
            });
            using var s2 = world.Entered.Into("wall").Subscribe(_ => probe.EnteredIntoWall = true);

            for (int i = 0; i < 300 && !probe.Entered; i++)
                await PandaTask.NextFrame();

            life.StopApplication();
        }
    }

    static (GameApplication app, Probe probe) NewApp(Func<IServiceProvider, PandaTask> body)
    {
        var probe = new Probe();
        var builder = GameApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddSceneManager();
        builder.Services.AddEvents();
        builder.Services.AddClock();
        builder.Services.AddCollision();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<IBootstrap>(sp => new DelegateBootstrap(() => body(sp)));
        return (builder.Build(), probe);
    }

    [Fact]
    public void RaycastReturnsNearestHitWithIntoAndDistance()
    {
        var (app, probe) = NewApp(BodyAsync);
        app.Run();

        Assert.True(probe.RaycastHit, "a downward ray over the sphere should hit it");
        Assert.Equal("ground", probe.RaycastInto);
        Assert.Equal(8.0, probe.RaycastDistance, 2);   // ray z=10 → sphere top z=2

        static PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var world = sp.GetRequiredService<ICollisionWorld>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var ground = new CollisionNode("ground");
            ground.AddSolid(new CollisionSphere(0, 0, 0, 2));
            ground.SetIntoCollideMask(BitMask32.AllOn());
            scene.Root.AttachNewNode(ground);

            // Self-traversing one-shot query — no frame wait needed.
            var hit = world.Raycast(new LPoint3f(0, 0, 10), new LVector3f(0, 0, -1), scene.Root);
            if (hit is { } h)
            {
                probe.RaycastHit = true;
                probe.RaycastInto = h.Into.GetName();
                probe.RaycastDistance = h.Distance;
            }

            life.StopApplication();
            return PandaTask.CompletedTask;
        }
    }

    [Fact]
    public void AddPusherRegistersAndStillFeedsObservables()
    {
        var (app, probe) = NewApp(BodyAsync);
        app.Run();

        Assert.True(probe.PusherEntered, "a pusher is a CollisionHandlerEvent, so the world's patterns still fire");

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var world = sp.GetRequiredService<ICollisionWorld>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var playerRoot = scene.Root.AttachNewNode("player-root");
            var fromNode = new CollisionNode("player");
            fromNode.AddSolid(new CollisionSphere(0, 0, 0, 1));
            fromNode.SetFromCollideMask(BitMask32.AllOn());
            fromNode.SetIntoCollideMask(BitMask32.AllOff());
            var from = playerRoot.AttachNewNode(fromNode);

            var wallNode = new CollisionNode("wall");
            wallNode.AddSolid(new CollisionSphere(0, 0, 0, 1));
            wallNode.SetIntoCollideMask(BitMask32.AllOn());
            var wall = scene.Root.AttachNewNode(wallNode);
            wall.SetX(1.0f);

            var pusher = world.AddPusher(from, playerRoot);
            Assert.NotNull(pusher);
            using var s = world.Entered.Subscribe(_ => probe.PusherEntered = true);

            for (int i = 0; i < 300 && !probe.PusherEntered; i++)
                await PandaTask.NextFrame();

            life.StopApplication();
        }
    }

    [Fact]
    public void AddQueryPopulatesPolledQueueAfterTraverse()
    {
        var (app, probe) = NewApp(BodyAsync);
        app.Run();

        Assert.True(probe.QueryEntries > 0, "the auto-traverse should fill the query's queue");
        Assert.Equal("wall", probe.QueryNearestInto);

        static async PandaTask BodyAsync(IServiceProvider sp)
        {
            var scene = sp.GetRequiredService<ISceneManager>();
            var world = sp.GetRequiredService<ICollisionWorld>();
            var life = sp.GetRequiredService<IHostApplicationLifetime>();
            var probe = sp.GetRequiredService<Probe>();

            var fromNode = new CollisionNode("probe");
            fromNode.AddSolid(new CollisionSphere(0, 0, 0, 1));
            fromNode.SetFromCollideMask(BitMask32.AllOn());
            fromNode.SetIntoCollideMask(BitMask32.AllOff());
            var from = scene.Root.AttachNewNode(fromNode);

            var wallNode = new CollisionNode("wall");
            wallNode.AddSolid(new CollisionSphere(0, 0, 0, 1));
            wallNode.SetIntoCollideMask(BitMask32.AllOn());
            scene.Root.AttachNewNode(wallNode).SetX(1.0f);

            var query = world.AddQuery(from);   // polled, not observed

            for (int i = 0; i < 300 && probe.QueryEntries == 0; i++)
            {
                await PandaTask.NextFrame();     // the auto-traverse fills the query
                probe.QueryEntries = query.Hits.Count;
            }

            // The managed readers surface the nearest into-node by name (the ground-follow idiom).
            probe.QueryNearestInto = query.NearestInto("wall")?.GetIntoNodePath().GetName();

            life.StopApplication();
        }
    }
}
