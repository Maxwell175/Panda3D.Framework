using System;
using Panda3D.Core;

namespace Panda3D.Framework.Rendering;

/// <summary>Engine-wide singletons: <c>GraphicsEngine.GetGlobalPtr()</c> and the default pipe.</summary>
internal sealed class RenderingService : IRenderingService
{
    public GraphicsEngine Engine { get; }
    public GraphicsPipe Pipe { get; }

    public RenderingService()
    {
        Engine = GraphicsEngine.GetGlobalPtr();
        Pipe = GraphicsPipeSelection.GetGlobalPtr().MakeDefaultPipe()
            ?? throw new InvalidOperationException("No graphics pipe available (GraphicsPipeSelection.MakeDefaultPipe returned null).");
    }

    public void RenderFrame() => Engine.RenderFrame();
}

/// <summary>Trivial <see cref="IClockTickSource"/> marker: <c>RenderFrame</c> ticks the global clock.</summary>
internal sealed class RenderClockTickSource : IClockTickSource
{
}
