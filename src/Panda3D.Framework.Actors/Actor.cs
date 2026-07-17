using System;
using System.Collections.Generic;
using System.Linq;
using Panda3D.Core;

namespace Panda3D.Framework.Actors;

internal sealed class Actor : IActor, IActorRig
{
    readonly Loader _loader;
    readonly int _hierarchyFlags;
    readonly IReadOnlyList<LodLevel> _lods;
    readonly LODNode? _lodNode;
    readonly Dictionary<string, PartState> _parts = new(StringComparer.Ordinal);
    readonly HashSet<Character> _characters = new();
    readonly List<JointExposure> _exposures = new();
    readonly Dictionary<string, NodePath> _controls = new(StringComparer.Ordinal);
    bool _disposed;

    public Actor(
        NodePath node,
        Character character,
        Loader loader,
        int hierarchyFlags,
        IReadOnlyList<LodLevel>? lods = null,
        LODNode? lodNode = null)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Character = character ?? throw new ArgumentNullException(nameof(character));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _hierarchyFlags = hierarchyFlags;
        _lods = lods is null ? Array.Empty<LodLevel>() : lods.ToArray();
        _lodNode = lodNode;
    }

    public NodePath Node { get; }
    public Character Character { get; }
    public IActorRig Rig => this;
    public IReadOnlyList<LodLevel> Lods => _lods;
    public LODNode? LodNode => _lodNode;

    public IReadOnlyCollection<string> Anims
        => _parts.Values.SelectMany(p => p.Controls.Keys).Distinct(StringComparer.Ordinal).OrderBy(n => n).ToArray();

    public IAnimControl Anim(string anim, string part = ActorDefaults.DefaultPart)
    {
        if (string.IsNullOrWhiteSpace(anim)) throw new ArgumentException("Animation name must be non-empty.", nameof(anim));
        if (TryAnim(anim, out var control, part))
            return control!;

        throw new KeyNotFoundException($"Actor part '{part}' has no animation named '{anim}'.");
    }

    public bool TryAnim(string anim, out IAnimControl? control, string part = ActorDefaults.DefaultPart)
    {
        control = null;
        if (string.IsNullOrWhiteSpace(anim)) return false;
        if (!_parts.TryGetValue(part, out var state)) return false;
        if (state.Controls.TryGetValue(anim, out var found))
        {
            control = found;
            return true;
        }
        return false;
    }

    public PartBundle Part(string part = ActorDefaults.DefaultPart) => GetPartState(part).Bundle;

    public void EnableBlend(string part = ActorDefaults.DefaultPart) => Part(part).SetAnimBlendFlag(true);
    public void DisableBlend(string part = ActorDefaults.DefaultPart) => Part(part).SetAnimBlendFlag(false);

    public void SetBlendWeight(string anim, float weight, string part = ActorDefaults.DefaultPart)
        => Part(part).SetControlEffect(Anim(anim, part), weight);

    public void MakeSubpart(string name, SubpartDef def, string parent = ActorDefaults.DefaultPart)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Subpart name must be non-empty.", nameof(name));
        if (_parts.ContainsKey(name))
            throw new InvalidOperationException($"Actor part '{name}' already exists.");

        var parentState = GetPartState(parent);
        var subset = new PartSubset(parentState.Subset);
        foreach (var include in def.IncludeJoints ?? Array.Empty<string>())
        {
            using var glob = new GlobPattern(include);
            subset.AddIncludeJoint(glob);
        }
        foreach (var exclude in def.ExcludeJoints ?? Array.Empty<string>())
        {
            using var glob = new GlobPattern(exclude);
            subset.AddExcludeJoint(glob);
        }

        var state = new PartState(name, parentState.Character, parentState.BundleHandle, subset);
        _parts.Add(name, state);
        foreach (var anim in parentState.AnimationFiles)
            BindAnimation(state, anim.Key, anim.Value);
    }

    public NodePath ExposeJoint(string joint, string part = ActorDefaults.DefaultPart, bool local = false)
    {
        if (string.IsNullOrWhiteSpace(joint)) throw new ArgumentException("Joint name must be non-empty.", nameof(joint));
        var state = GetPartState(part);

        var characterJoint = state.Character.FindJoint(joint);
        var node = Node.AttachNewNode($"{joint}-exposed");
        bool ok = local ? characterJoint.AddLocalTransform(node.Node()) : characterJoint.AddNetTransform(node.Node());
        if (!ok)
        {
            node.RemoveNode();
            throw new InvalidOperationException($"Could not expose joint '{joint}' on part '{part}'.");
        }

        _exposures.Add(new JointExposure(characterJoint, node, local));
        return node;
    }

    public NodePath ControlJoint(string joint, string part = ActorDefaults.DefaultPart)
    {
        if (string.IsNullOrWhiteSpace(joint)) throw new ArgumentException("Joint name must be non-empty.", nameof(joint));
        var key = PartJointKey(part, joint);
        if (_controls.TryGetValue(key, out var existing))
            return existing;

        var node = Node.AttachNewNode($"{joint}-control");
        if (!Part(part).ControlJoint(joint, node.Node()))
        {
            node.RemoveNode();
            throw new InvalidOperationException($"Could not control joint '{joint}' on part '{part}'.");
        }

        _controls.Add(key, node);
        return node;
    }

    public void FreezeJoint(string joint, TransformState transform, string part = ActorDefaults.DefaultPart)
    {
        ArgumentNullException.ThrowIfNull(transform);
        if (!Part(part).FreezeJoint(joint, transform))
            throw new InvalidOperationException($"Could not freeze joint '{joint}' on part '{part}'.");
    }

    public void ReleaseJoint(string joint, string part = ActorDefaults.DefaultPart)
    {
        var key = PartJointKey(part, joint);
        Part(part).ReleaseJoint(joint);
        if (_controls.Remove(key, out var node))
            node.RemoveNode();
    }

    public void SetAnimRateLod(LPoint3f center, float far, float near, float delayFactor = 1f)
    {
        foreach (var character in _characters)
            character.SetLodAnimation(center, far, near, delayFactor);
    }

    internal void AddPart(string name, Character character, PartBundleHandle bundleHandle, bool frameBlend)
    {
        if (_parts.ContainsKey(name))
            throw new InvalidOperationException($"Actor part '{name}' already exists.");

        var state = new PartState(name, character, bundleHandle, new PartSubset());
        state.Bundle.SetFrameBlendFlag(frameBlend);
        _parts.Add(name, state);
        AddCharacter(character);
    }

    internal void AddLodCharacter(Character character) => AddCharacter(character);

    internal void RenameDefaultPart(string name)
    {
        if (!_parts.Remove(ActorDefaults.DefaultPart, out var state))
            return;
        state.Name = name;
        _parts.Add(name, state);
    }

    internal void CaptureEmbeddedControls(IPandaNode rootNode, string part)
    {
        var state = GetPartState(part);
        var collection = new AnimControlCollection();
        Panda3DCoreGlobals.AutoBind(rootNode, collection, _hierarchyFlags);
        for (int i = 0; i < collection.GetNumAnims(); i++)
            state.Controls[collection.GetAnimName(i)] = collection.GetAnim(i);
    }

    internal void BindAnimations(IReadOnlyDictionary<string, string>? anims, string part)
    {
        if (anims is null) return;
        var state = GetPartState(part);
        foreach (var anim in anims)
            BindAnimation(state, anim.Key, anim.Value);
    }

    void BindAnimation(PartState state, string name, string filename)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Animation name must be non-empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("Animation filename must be non-empty.", nameof(filename));

        state.AnimationFiles[name] = filename;
        try
        {
            var control = state.Bundle.LoadBindAnim(_loader, ActorLoader.ToFilename(filename), _hierarchyFlags, state.Subset, allow_async: false);
            control.WaitPending();
            if (!control.HasAnim())
                throw new InvalidOperationException($"Animation '{name}' loaded but did not bind an AnimBundle.");
            state.Controls[name] = control;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new InvalidOperationException($"Could not bind animation '{name}' from '{filename}' to actor part '{state.Name}'.", ex);
        }
    }

    PartState GetPartState(string part)
    {
        if (_parts.TryGetValue(part, out var state))
            return state;

        throw new KeyNotFoundException($"Actor has no part named '{part}'.");
    }

    void AddCharacter(Character character)
    {
        _characters.Add(character);
    }

    static string PartJointKey(string part, string joint) => $"{part}\0{joint}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var exposure in _exposures)
        {
            if (exposure.Local)
                exposure.Joint.RemoveLocalTransform(exposure.Node.Node());
            else
                exposure.Joint.RemoveNetTransform(exposure.Node.Node());
            exposure.Node.RemoveNode();
        }
        _exposures.Clear();

        foreach (var pair in _controls)
        {
            var parts = pair.Key.Split('\0');
            if (parts.Length == 2 && _parts.TryGetValue(parts[0], out var state))
                state.Bundle.ReleaseJoint(parts[1]);
            pair.Value.RemoveNode();
        }
        _controls.Clear();

        foreach (var part in _parts.Values)
            part.Dispose();
        _parts.Clear();

        Node.RemoveNode();
    }

    sealed class PartState : IDisposable
    {
        public PartState(string name, Character character, PartBundleHandle bundleHandle, PartSubset subset)
        {
            Name = name;
            Character = character;
            BundleHandle = bundleHandle;
            Subset = subset;
        }

        public string Name { get; set; }
        public Character Character { get; }
        public PartBundleHandle BundleHandle { get; }
        public PartBundle Bundle => BundleHandle.GetBundle();
        public PartSubset Subset { get; }
        public Dictionary<string, IAnimControl> Controls { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> AnimationFiles { get; } = new(StringComparer.Ordinal);

        public void Dispose() => Subset.Dispose();
    }

    readonly record struct JointExposure(CharacterJoint Joint, NodePath Node, bool Local);
}
