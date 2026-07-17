using Panda3D.Framework;
using Panda3D.Framework.Actors;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Physics;
using Panda3D.Framework.Samples.RoamingRalph;

var builder = GameApplication.CreateBuilder(args);

builder.Services.AddGame(o => o.Window.Title = "Panda3D.Framework - Roaming Ralph");
builder.Services.AddActors();
builder.Services.AddCollision();
builder.Services.AddBootstrap<RoamingRalphDemo>();

builder.Build().Run();
