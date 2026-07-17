using System;
using Panda3D.Core;
using Panda3D.Framework.Actors;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// Another player's Ralph: a <see cref="RalphAvatar"/> plus a floating name tag, driven entirely by
/// <see cref="Apply"/> from incoming <see cref="PlayerState"/> snapshots (no local simulation).
/// </summary>
internal sealed class RemoteRalph : IDisposable
{
    readonly RalphAvatar _avatar;

    public RemoteRalph(IActorLoader actors, NodePath sceneRoot, string modelRoot, string name)
    {
        _avatar = new RalphAvatar(actors, sceneRoot, modelRoot);

        // A name tag that floats above the head and always faces the camera.
        var tag = new TextNode("name");
        tag.SetText(name);
        tag.SetTextColor(1f, 1f, 0.4f, 1f);
        tag.SetAlign(TextPropertiesAlignment.ACenter);
        var tagPath = _avatar.Node.AttachNewNode(tag);
        tagPath.SetScale(2f);          // local units are Ralph-scaled (0.2), so scale the text back up
        tagPath.SetZ(9f);              // above Ralph's head, in his local space
        tagPath.SetBillboardPointEye();
    }

    public void Apply(in PlayerState state)
    {
        _avatar.Node.SetPos(state.X, state.Y, state.Z);
        _avatar.Node.SetH(state.H);
        _avatar.PlayAnim(state.Anim, state.Rate);
    }

    public void Dispose() => _avatar.Dispose();
}
