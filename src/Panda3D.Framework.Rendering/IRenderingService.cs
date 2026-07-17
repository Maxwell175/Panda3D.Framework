using Panda3D.Core;

namespace Panda3D.Framework.Rendering;

/// <summary>Engine-wide rendering service: the <c>GraphicsEngine</c> and the <c>RenderFrame</c> step.</summary>
public interface IRenderingService
{
    /// <summary>The binding graphics engine.</summary>
    GraphicsEngine Engine { get; }

    /// <summary>The default graphics pipe.</summary>
    GraphicsPipe Pipe { get; }

    /// <summary>Render every open output once (the <c>igLoop</c> body). Also ticks the global clock.</summary>
    void RenderFrame();
}
