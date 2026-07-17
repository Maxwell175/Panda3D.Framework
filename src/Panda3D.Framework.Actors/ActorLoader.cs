using System;
using System.Collections.Generic;
using System.Linq;
using Interrogate;
using Panda3D.Async;
using Panda3D.Core;

namespace Panda3D.Framework.Actors;

internal sealed class ActorLoader : IActorLoader
{
    readonly Loader _loader = new("actor-loader");

    public PandaTask<IActor> LoadAsync(string model, IReadOnlyDictionary<string, string>? anims = null, ActorOptions? options = null)
        => new(Load(model, anims, options));

    public IActor Load(string model, params AnimClip[] anims)
        => Load(model, anims.ToDictionary(a => a.Name, a => a.File, StringComparer.Ordinal));

    public IActor Load(string model, IReadOnlyDictionary<string, string>? anims = null, ActorOptions? options = null)
    {
        options ??= new ActorOptions();
        var loaded = LoadPart(model);
        var hierarchyFlags = HierarchyFlags(options);

        var actor = new Actor(loaded.Node, loaded.Character, _loader, hierarchyFlags);
        actor.AddPart(ActorDefaults.DefaultPart, loaded.Character, loaded.BundleHandle, options.FrameBlend);
        actor.CaptureEmbeddedControls(loaded.RootNode, ActorDefaults.DefaultPart);
        actor.BindAnimations(anims, ActorDefaults.DefaultPart);
        return actor;
    }

    public PandaTask<IActor> LoadAsync(IReadOnlyDictionary<string, ActorPart> parts, ActorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return new PandaTask<IActor>(LoadMultipart(parts, options ?? new ActorOptions()));
    }

    public PandaTask<IActor> LoadAsync(ActorDefinition definition, ActorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new ActorOptions();

        if (definition.Lods.Count > 0)
            return new PandaTask<IActor>(LoadLodActor(definition, options));

        var parts = new Dictionary<string, ActorPart>(StringComparer.Ordinal);
        foreach (var entry in definition.Parts)
            parts.Add(entry.Key, new ActorPart(ResolveSingleModel(entry.Key, entry.Value), new Dictionary<string, string>(entry.Value.Anims)));

        return new PandaTask<IActor>(LoadMultipart(parts, options));
    }

    internal static Filename ToFilename(string path) => Filename.FromOsSpecific(path);

    Actor LoadMultipart(IReadOnlyDictionary<string, ActorPart> parts, ActorOptions options)
    {
        if (parts.Count == 0)
            throw new ArgumentException("At least one actor part is required.", nameof(parts));

        var root = new NodePath("actor");
        Actor? actor = null;
        int hierarchyFlags = HierarchyFlags(options);

        foreach (var entry in parts)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
                throw new ArgumentException("Actor part names must be non-empty.", nameof(parts));

            var loaded = LoadPart(entry.Value.Model);
            loaded.Node.ReparentTo(root);

            actor ??= new Actor(root, loaded.Character, _loader, hierarchyFlags);
            actor.AddPart(entry.Key, loaded.Character, loaded.BundleHandle, options.FrameBlend);
            actor.CaptureEmbeddedControls(loaded.RootNode, entry.Key);
            actor.BindAnimations(entry.Value.Anims, entry.Key);
        }

        return actor ?? throw new ArgumentException("At least one actor part is required.", nameof(parts));
    }

    Actor LoadLodActor(ActorDefinition definition, ActorOptions options)
    {
        if (definition.Parts.Count == 0)
            throw new ArgumentException("At least one actor part is required.", nameof(definition));

        var lods = definition.Lods.ToArray();
        var root = new NodePath("actor");
        var lodNode = new LODNode("lodRoot");
        var lodRoot = root.AttachNewNode(lodNode);
        var commonHandles = new Dictionary<string, PartBundleHandle>(StringComparer.Ordinal);
        Actor? actor = null;
        int hierarchyFlags = HierarchyFlags(options);

        foreach (var lod in lods)
        {
            if (string.IsNullOrWhiteSpace(lod.Name))
                throw new ArgumentException("LOD names must be non-empty.", nameof(definition));
            if (lod.SwitchIn < lod.SwitchOut)
                throw new ArgumentException($"LOD '{lod.Name}' has SwitchIn smaller than SwitchOut.", nameof(definition));

            var lodGroup = lodRoot.AttachNewNode(lod.Name);
            lodNode.AddSwitch(lod.SwitchIn, lod.SwitchOut);

            foreach (var part in definition.Parts)
            {
                if (string.IsNullOrWhiteSpace(part.Key))
                    throw new ArgumentException("Actor part names must be non-empty.", nameof(definition));

                var model = ResolveModelForLod(part.Key, part.Value, lod.Name);
                var loaded = LoadPart(model);
                loaded.Node.ReparentTo(lodGroup);

                if (!commonHandles.TryGetValue(part.Key, out var commonHandle))
                {
                    commonHandles.Add(part.Key, loaded.BundleHandle);
                    actor ??= new Actor(root, loaded.Character, _loader, hierarchyFlags, lods, lodNode);
                    actor.AddPart(part.Key, loaded.Character, loaded.BundleHandle, options.FrameBlend);
                    actor.CaptureEmbeddedControls(loaded.RootNode, part.Key);
                }
                else
                {
                    loaded.Character.MergeBundles(loaded.BundleHandle, commonHandle);
                    actor!.AddLodCharacter(loaded.Character);
                }
            }
        }

        if (actor is null)
            throw new ArgumentException("At least one LOD level is required.", nameof(definition));

        foreach (var part in definition.Parts)
            actor.BindAnimations(new Dictionary<string, string>(part.Value.Anims), part.Key);

        return actor;
    }

    LoadedPart LoadPart(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model filename must be non-empty.", nameof(model));

        var rootNode = _loader.LoadSync(ToFilename(model));
        var root = new NodePath(rootNode);
        var character = FindCharacter(root);
        var bundleHandle = character.GetNumBundles() > 0
            ? character.GetBundleHandle(0)
            : throw new InvalidOperationException($"Loaded actor model '{model}' does not contain a PartBundle.");

        return new LoadedPart(root, rootNode, character, bundleHandle);
    }

    static int HierarchyFlags(ActorOptions options)
        => options.LooseHierarchy
            ? (int)(PartGroupHierarchyMatchFlags.HmfOkAnimExtra
                    | PartGroupHierarchyMatchFlags.HmfOkPartExtra
                    | PartGroupHierarchyMatchFlags.HmfOkWrongRootName)
            : 0;

    static string ResolveSingleModel(string partName, ActorPartDef part)
    {
        if (!string.IsNullOrWhiteSpace(part.Model))
            return part.Model!;

        if (part.ModelByLod.Count == 1)
            return part.ModelByLod.Values.First();

        throw new ArgumentException($"Actor part '{partName}' must specify Model when no LOD levels are defined.");
    }

    static string ResolveModelForLod(string partName, ActorPartDef part, string lodName)
    {
        if (part.ModelByLod.TryGetValue(lodName, out var model) && !string.IsNullOrWhiteSpace(model))
            return model;

        if (!string.IsNullOrWhiteSpace(part.Model))
            return part.Model!;

        throw new ArgumentException($"Actor part '{partName}' does not specify a model for LOD '{lodName}'.");
    }

    static Character FindCharacter(NodePath root)
    {
        var rootNode = root.Node();
        if (rootNode.IsOfType(Character.GetClassType()))
            return rootNode.CastTo<Character>()
                   ?? throw new InvalidOperationException("Root node reported Character type but could not be cast.");

        var matches = root.FindAllMatches("**/+Character");
        if (matches.IsEmpty())
            throw new InvalidOperationException($"Loaded model '{root.GetName()}' does not contain a Character node.");

        return matches[0].Node().CastTo<Character>()
            ?? throw new InvalidOperationException("Character path did not contain a Character node.");
    }

    readonly record struct LoadedPart(
        NodePath Node,
        IPandaNode RootNode,
        Character Character,
        PartBundleHandle BundleHandle);
}
