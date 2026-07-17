using Microsoft.Extensions.DependencyInjection;
using Panda3D.Framework.Rendering;

namespace Panda3D.Framework.Input;

/// <summary>Convenience access to a view's per-view input.</summary>
public static class InputViewExtensions
{
    /// <summary>This view's raw input poller (resolved from the view's scope).</summary>
    public static IInput Input(this IView view) => view.Services.GetRequiredService<IInput>();
}
