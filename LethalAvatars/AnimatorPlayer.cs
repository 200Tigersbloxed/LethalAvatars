using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Avatar = LethalAvatars.SDK.Avatar;

namespace LethalAvatars;

public struct AnimatorPlayer : IDisposable
{
    public Avatar AttachedAvatar;
    public RuntimeAnimatorController SourceController;
    public AnimatorControllerPlayable PlayableController;
    public PlayableGraph PlayableGraph;
    public AnimatorControllerParameter[] Parameters;
    public AnimatorOverrideController? OverrideController;

    internal AnimatorPlayer(Avatar a, RuntimeAnimatorController src, AnimatorControllerPlayable pc, PlayableGraph pg, 
        AnimatorOverrideController? overrideController = null)
    {
        AttachedAvatar = a;
        SourceController = src;
        PlayableController = pc;
        PlayableGraph = pg;
        OverrideController = overrideController;
        List<AnimatorControllerParameter> parameters = new();
        bool c = true;
        int i = 0;
        while (c)
        {
            try
            {
                AnimatorControllerParameter animatorControllerParameter = pc.GetParameter(i);
                parameters.Add(animatorControllerParameter);
                i++;
            }
            catch (IndexOutOfRangeException) {c = false;}
        }
        Parameters = parameters.ToArray();
    }

    public void Dispose() => PlayableGraph.Stop();
}