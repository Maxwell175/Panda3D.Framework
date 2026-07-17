using Panda3D.Async;

namespace Panda3D.Framework.Hosting;

/// <summary>
/// The application's entry coroutine, spawned at the gameplay slot right after the host starts —
/// where sequential startup logic lives (load the first scene, show the menu, …).
/// </summary>
public interface IBootstrap
{
    /// <summary>Run the entry coroutine.</summary>
    PandaTask RunAsync();
}
