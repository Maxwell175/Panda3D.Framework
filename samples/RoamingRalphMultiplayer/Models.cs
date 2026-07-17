using System;
using System.IO;
using Panda3D.Core;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// The Roaming Ralph assets, shared by the rendered client and the headless bot. The build compiles the
/// source eggs to <c>.bam</c> and packs them with their textures into <c>ralph.mf</c> next to the executable
/// (see the resource pipeline in the .csproj); at startup we mount that multifile and load everything out of
/// it, so there is nothing to locate on disk and no runtime egg→bam conversion.
/// </summary>
internal static class Models
{
    const string BundleName = "ralph.mf";

    /// <summary>Where the bundle is mounted. Paths under it are VFS paths, never OS paths.</summary>
    public const string Root = "/models";

    /// <summary>Mount the build's multifile and return the mount point everything loads from.</summary>
    public static string Mount()
    {
        var bundle = Path.Combine(AppContext.BaseDirectory, BundleName);
        if (!VirtualFileSystem.GetGlobalPtr().Mount(
                Filename.FromOsSpecific(bundle), Filename.FromOsSpecific(Root), 0))
            throw new FileNotFoundException($"Could not mount the model bundle '{bundle}'. Build the project to produce it.");
        return Root;
    }

    /// <summary>A path to an asset inside the mounted bundle (VFS paths are always unix-style).</summary>
    public static string At(string root, string file) => $"{root}/{file}";
}
