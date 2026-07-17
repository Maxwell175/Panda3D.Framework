using System;
using Microsoft.Extensions.Hosting;
using Panda3D.Async;
using Panda3D.Core;
using Panda3D.Framework;
using Panda3D.Framework.Hosting;
using Panda3D.Framework.Input;
using Panda3D.Framework.Rendering;

var builder = GameApplication.CreateBuilder(args);

builder.Services.AddGame(o => o.Window.Title = "Spinning Card");

builder.Services.AddViewBootstrap(async (
    ISceneManager scene, IViewManager views, IInput input,
    IGameClock clock, IHostApplicationLifetime life) =>
{
    var view = views.Main;
    view.ClearColor = new LVecBase4f(0.1f, 0.12f, 0.18f, 1f);

    var maker = new CardMaker("card");
    maker.SetFrame(-1f, 1f, -1f, 1f);
    maker.SetColor(0.95f, 0.45f, 0.2f, 1f);
    var card = scene.Root.AttachNewNode(maker.Generate());
    card.SetPos(0f, 12f, 0f);

    using var onClose = view.Closed.Subscribe(_ => life.StopApplication());

    while (!life.ApplicationStopping.IsCancellationRequested && !input.Pressed(Keys.Escape))
    {
        float dt = (float)clock.Dt;
        card.SetH(card.GetH() + 60f * dt);
        float x = (input.IsDown(Keys.Right) ? 1f : 0f) - (input.IsDown(Keys.Left) ? 1f : 0f);
        float z = (input.IsDown(Keys.Up) ? 1f : 0f) - (input.IsDown(Keys.Down) ? 1f : 0f);
        card.SetPos(card.GetX() + x * 8f * dt, card.GetY(), card.GetZ() + z * 8f * dt);
        await PandaTask.NextFrame();
    }
    life.StopApplication();
});

builder.Build().Run();
