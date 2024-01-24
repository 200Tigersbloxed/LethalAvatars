using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using GameNetcodeStuff;
using Hypernex.Tools;
using LethalAvatars.Libs;
using LethalAvatars.Networking.Messages;
using Unity.Netcode;
using UnityEngine;
using Avatar = LethalAvatars.SDK.Avatar;

namespace LethalAvatars;

public class AvatarDriver : MonoBehaviour
{
    /// <summary>
    /// A Dictionary of which Fields to pull from the PlayerControllerB. Key is the name, Value is the Field Name.
    /// </summary>
    public static readonly Dictionary<string, string> ParametersToGet = new()
    {
        ["MovingForward"] = "movingForward",
        ["IsWalking"] = "isWalking",
        ["IsSprinting"] = "isSprinting",
        ["IsExhausted"] = "isExhausted",
        ["IsJumping"] = "isJumping",
        ["IsFallingFromJump"] = "isFallingFromJump",
        ["IsPlayerSliding"] = "isPlayerSliding",
        ["TakingFallDamage"] = "takingFallDamage",
        ["IsCrouching"] = "isCrouching",
        ["IsFallingNoJump"] = "isFallingNoJump",
        ["IsMovementHindered"] = "isMovementHindered",
        ["IsSinking"] = "isSinking",
        ["IsUnderwater"] = "isUnderwater",
        ["UsingJetpack"] = "jetpackControls",
        ["DisablingJetpackControls"] = "disablingJetpackControls",
        ["IsDead"] = "isPlayerDead",
        ["IsControlled"] = "isPlayerControlled",
        ["IsClimbing"] = "isClimbingLadder",
        ["HoldingTwoHandedItem"] = "twoHanded"
    };

    /// <summary>
    /// A Dictionary of which Methods to invoke to apply to the animators. Key is the name, Value is the Method Name
    /// and Parameters.
    /// </summary>
    public static readonly Dictionary<string, (string, Func<AvatarDriver, Avatar, PlayerControllerB, object[]>)>
        ParametersToGetFromInvoke = new()
        {
            ["CanEmote"] = ("CheckConditionsForEmote", (_, _, _) => Array.Empty<object>())
        };

    /// <summary>
    /// A Dictionary of which Parameters apart the original PlayerBodyAnimator to pull values from.
    /// Key is the name, Value is the Parameter Name and Type (int, float, or bool)
    /// </summary>
    public static readonly Dictionary<string, (string, Type)> ParametersToPullFromAnimator = new()
    {
        ["Emote"] = ("emoteNumber", typeof(int)),
        ["ReelingUp"] = ("reelingUp", typeof(bool))
    };

    /// <summary>
    /// Extra parameters to include. Values here are get/set directly, so you can easily add your own here.
    /// </summary>
    public readonly Dictionary<string, object> IncludeParameters = new()
    {
        ["Level"] = 0,
        ["HasBeta"] = false,
        ["MovementSpeed"] = 0f,
        ["PullLever"] = false,
        ["Grabbing"] = false
    };

    /// <summary>
    /// Disable the default Update frame for updating values. Allows developers to override when to check parameters.
    /// </summary>
    public bool DisableDefaultUpdate;
    
    /// <summary>
    /// A List of all active AnimatorPlayers on an Avatar
    /// </summary>
    public List<AnimatorPlayer> Animators => new(animators);

    /// <summary>
    /// How long between each update (in seconds)
    /// </summary>
    public float UpdateTime = 0.01f;

    internal Avatar? Avatar;
    internal PlayerControllerB? player;
    internal List<AnimatorPlayer> animators = new();
    private Animator? mainAnimator;
    private bool noAnimator;
    private Transform? allowTurn;
    internal Transform? lastLocalItemGrab;
    internal Transform? lastServerItemGrab;
    private List<AvatarNearClip> avatarNearClips = new();
    private Dictionary<string, object> lastParameters = new();
    private CancellationTokenSource? cts;

    internal bool twoHanded;
    internal Quaternion headrot;

    private List<string> failedFields = new();

    /// <summary>
    /// Updates all parameters on an avatar
    /// </summary>
    public Dictionary<string, object> Refresh()
    {
        Dictionary<string, object> parameters = new();
        bool runtimeControllerPresent = !noAnimator && mainAnimator!.runtimeAnimatorController != null;
        if((animators.Count <= 0 && !runtimeControllerPresent) || player == null) return parameters;
        // Set MovementSpeed
        IncludeParameters["MovementSpeed"] = player.moveInputVector.magnitude;
        // Include parameters for networking
        foreach (KeyValuePair<string,object> includedParameter in new Dictionary<string, object>(IncludeParameters))
        {
            parameters.SafeAdd(includedParameter.Key, includedParameter.Value);
            SetParameter(includedParameter.Key, includedParameter.Value);
        }
        // Now do anything else
        foreach (KeyValuePair<string,string> keyValuePair in ParametersToGet)
        {
            FieldInfo? fieldInfo = player.GetType().GetField(keyValuePair.Value,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if(fieldInfo == null)
            {
                if(!failedFields.Contains(keyValuePair.Value))
                {
                    Plugin.PluginLogger.LogWarning($"Failed to find field {keyValuePair.Value} on {player.name}");
                    failedFields.Add(keyValuePair.Value);
                }
                continue;
            }
            object value = fieldInfo.GetValue(player);
            parameters.SafeAdd(keyValuePair.Key, value);
            SetParameter(value.GetType(), keyValuePair.Key, value);
        }
        foreach (KeyValuePair<string, (string, Func<AvatarDriver, Avatar, PlayerControllerB, object[]>)> keyValuePair in
                 ParametersToGetFromInvoke)
        {
            MethodInfo? methodInfo = player.GetType().GetMethod(keyValuePair.Value.Item1,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (methodInfo == null)
            {
                if(!failedFields.Contains(keyValuePair.Value.Item1))
                {
                    Plugin.PluginLogger.LogWarning($"Failed to find method {keyValuePair.Value} on {player.name}");
                    failedFields.Add(keyValuePair.Value.Item1);
                }
                continue;
            }
            object value = methodInfo.Invoke(player, keyValuePair.Value.Item2.Invoke(this, Avatar!, player));
            // Lethal Company, just set emoteNumber to negative when you can't emote...
            if(keyValuePair.Key == "CanEmote" && (bool) value == false)
                player.playerBodyAnimator.SetInteger("emoteNumber", -1);
            parameters.SafeAdd(keyValuePair.Key, value);
            SetParameter(value.GetType(), keyValuePair.Key, value);
        }
        if(player.playerBodyAnimator == null) return parameters;
        foreach (KeyValuePair<string, (string, Type)> keyValuePair in ParametersToPullFromAnimator)
        {
            object parameter;
            Type t = keyValuePair.Value.Item2;
            if (t == typeof(float))
                parameter = player.playerBodyAnimator.GetFloat(keyValuePair.Value.Item1);
            else if(t == typeof(int))
                parameter = player.playerBodyAnimator.GetInteger(keyValuePair.Value.Item1);
            else if(t == typeof(bool))
                parameter = player.playerBodyAnimator.GetBool(keyValuePair.Value.Item1);
            else
                continue;
            parameters.SafeAdd(keyValuePair.Key, parameter);
            SetParameter(t, keyValuePair.Key, parameter);
        }
        return parameters;
    }

    public void SetParameter(Type type, string n, object value)
    {
        switch (value)
        {
            case float f:
                float clampedF = Mathf.Clamp(f, -1f, 1);
                animators.ForEach(animator =>
                {
                    if(animator.Parameters.Count(x => x.name == n) <= 0) return;
                    animator.PlayableController.SetFloat(n, clampedF);
                });
                if(mainAnimator != null)
                    mainAnimator.SetFloat(n, clampedF);
                break;
            case int i:
                animators.ForEach(animator =>
                {
                    if(animator.Parameters.Count(x => x.name == n) <= 0) return;
                    animator.PlayableController.SetInteger(n, i);
                });
                if(mainAnimator != null)
                    mainAnimator.SetInteger(n, i);
                break;
            case bool b:
                animators.ForEach(animator =>
                {
                    if(animator.Parameters.Count(x => x.name == n) <= 0) return;
                    animator.PlayableController.SetBool(n, b);
                });
                if(mainAnimator != null)
                    mainAnimator.SetBool(n, b);
                break;
        }
    }

    public void SetParameter<T>(string n, T value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        SetParameter(typeof(T), n, value);
    }

    public Transform? GetBoneFromHumanoid(HumanBodyBones humanBodyBones)
    {
        if (noAnimator)
            return null;
        return mainAnimator!.GetBoneTransform(humanBodyBones);
    }

    internal void AnimationDone(Animator anim, Transform camera, bool isLocal)
    {
        noAnimator = anim == null || anim.avatar == null;
        mainAnimator = anim;
        allowTurn = camera;
        if(noAnimator || !isLocal) return;
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in Avatar!.transform.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (!skinnedMeshRenderer.name.Contains("shadowclone_"))
            {
                AvatarNearClip avatarNearClip = skinnedMeshRenderer.gameObject.AddComponent<AvatarNearClip>();
                if(avatarNearClip != null && avatarNearClip.Setup(this, camera.GetComponent<Camera>()))
                    avatarNearClips.Add(avatarNearClip);
            }
        avatarNearClips.ForEach(x => x.CreateShadows());
    }

    private void Update()
    {
        // Prevent animators from offsetting avatar from center
        transform.SetLocalPositionAndRotation(Vector3.zero, new Quaternion(0, 0, 0, 0));
        if(player == null || Avatar == null) return;
        if (allowTurn != null && player.inTerminalMenu)
        {
            Terminal? terminalScript = PlayerAvatarAPI.Terminal;
            if(terminalScript != null)
            {
                Transform terminal = terminalScript.transform;
                allowTurn.position = new Vector3(terminal.position.x + 0.1f, terminal.position.y + 0.85f,
                    terminal.position.z + 0.3f);
            }
        }
        else if (allowTurn != null)
            allowTurn.position = new Vector3(Avatar.Viewpoint.position.x, Avatar.Viewpoint.position.y,
                Avatar.Viewpoint.position.z);
    }

    private void LateUpdate()
    {
        PlayerControllerB? localPlayer = PlayerAvatarAPI.LocalPlayer;
        if(player == null || localPlayer == null || Avatar == null) return;
        if (!player.IsLocal())
        {
            player.serverItemHolder = twoHanded
                ? Avatar.BigItemGrab == null ? Avatar.SmallItemGrab : Avatar.BigItemGrab
                : Avatar.SmallItemGrab;
            // Only update head rot globally
            Transform? head = GetBoneFromHumanoid(HumanBodyBones.Head);
            if (head != null)
                head.rotation = headrot;
        }
        // Only update from here locally
        if (!player.IsLocal()) return;
        if (cts == null)
        {
            cts = new CancellationTokenSource();
            LethalAvatarsRunner.Instance!.RunCoroutine(NetworkUpdate());
        }
        if(DisableDefaultUpdate) return;
        IncludeParameters["Grabbing"] =
            localPlayer.currentlyHeldObject != null || localPlayer.currentlyHeldObjectServer != null;
        lastParameters = Refresh();
    }

    private IEnumerator NetworkUpdate()
    {
        while (!cts!.IsCancellationRequested)
        {
            yield return new WaitUntil(() => player != null);
            // Update Avatar Parameters
            AvatarParameters avatarParameters = new AvatarParameters(lastParameters, allowTurn, player!.twoHanded);
            avatarParameters.Broadcast(false, NetworkDelivery.Unreliable);
            yield return new WaitForSeconds(UpdateTime);
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        Animators.ForEach(x => x.Dispose());
        if (player != null)
        {
            player.localItemHolder = lastLocalItemGrab;
            player.serverItemHolder = lastServerItemGrab;
        }
    }
}