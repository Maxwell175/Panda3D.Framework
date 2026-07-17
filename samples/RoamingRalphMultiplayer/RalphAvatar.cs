using System;
using System.Collections.Generic;
using System.IO;
using Panda3D.Core;
using Panda3D.Framework.Actors;

namespace Panda3D.Framework.Samples.RoamingRalphMultiplayer;

/// <summary>
/// The visual half of a Ralph: the actor model plus the run/walk/idle animation playback. The rendered
/// player and remote avatars each own one; the headless bot owns none. <see cref="PlayAnim"/> takes the same
/// (name, rate) that a <see cref="RalphBody"/> produces or a network snapshot carries.
/// </summary>
internal sealed class RalphAvatar : IDisposable
{
    const string RunAnim = "run", WalkAnim = "walk", IdleAnim = "idle";
    const float ModelScale = 0.2f;

    readonly IActor _actor;
    string _anim = IdleAnim;
    float _rate = 1f;
    string? _playing;   // the clip currently looping, so we can stop it before switching

    public RalphAvatar(IActorLoader actors, NodePath sceneRoot, string modelRoot)
    {
        _actor = actors.Load(Models.At(modelRoot, "ralph.bam"), new Dictionary<string, string>
        {
            [RunAnim] = Models.At(modelRoot, "ralph-run.bam"),
            [WalkAnim] = Models.At(modelRoot, "ralph-walk.bam"),
        });
        _actor.Node.ReparentTo(sceneRoot);
        _actor.Node.SetScale(ModelScale);
        PlayAnim(IdleAnim, 1f);
    }

    public NodePath Node => _actor.Node;

    /// <summary>Switch to a looping clip (or pose the idle frame). A no-op if already playing it.</summary>
    public void PlayAnim(string anim, float rate)
    {
        if (anim == _anim && rate == _rate)
            return;
        _anim = anim;
        _rate = rate;

        if (_playing is not null)
            _actor.Anim(_playing).Stop();
        if (anim == IdleAnim)
        {
            _actor.Anim(WalkAnim).Pose(5);
            _playing = null;
        }
        else
        {
            var control = _actor.Anim(anim);
            control.SetPlayRate(rate);
            control.Loop(restart: true);
            _playing = anim;
        }
    }

    public void Dispose() => _actor.Dispose();
}
