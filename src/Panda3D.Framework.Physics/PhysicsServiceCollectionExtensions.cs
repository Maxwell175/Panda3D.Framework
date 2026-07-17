using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Panda3D.Core;
using Panda3D.Framework.Scheduling;

namespace Panda3D.Framework.Physics;

/// <summary>
/// Registration for the collision world and its per-frame tasks.
/// </summary>
public static class PhysicsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICollisionWorld"/> and the <c>resetPrevTransform</c> task at
    /// <see cref="FrameSlots.PrevTransform"/> (needed for fluid motion). When <paramref name="autoTraverse"/>
    /// is <see langword="true"/> (the default), also traverses <see cref="ISceneManager.Root"/> at
    /// <see cref="FrameSlots.Collision"/>. Pass <see langword="false"/> to call
    /// <see cref="ICollisionWorld.Traverse"/> from your own task instead.
    /// </summary>
    public static IServiceCollection AddCollision(this IServiceCollection services, bool autoTraverse = true)
    {
        services.TryAddSingleton<ICollisionWorld, CollisionWorld>();

        // reset prev-transforms first (lowest sort) so fluid motion sees continuous frames
        services.AddFrameTask("resetPrevTransform", FrameSlots.PrevTransform,
            _ => () => { PandaNode.ResetAllPrevTransform(); return true; });

        if (autoTraverse)
        {
            services.AddFrameTask("collisionLoop", FrameSlots.Collision, sp =>
            {
                var world = sp.GetRequiredService<ICollisionWorld>();
                var scene = sp.GetRequiredService<ISceneManager>();
                return () => { world.Traverse(scene.Root); return true; };
            });
        }
        return services;
    }
}
