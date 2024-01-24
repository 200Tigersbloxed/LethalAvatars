using LethalAvatars.SDK.Editor.Internals;
using UnityEditor;
using UnityEngine;

namespace LethalAvatars.SDK.Editor;

[CustomEditor(typeof(Avatar))]
class AvatarEditor : UnityEditor.Editor
{
    private const string PARAMETERS_DOC_URL = "https://github.com/200Tigersbloxed/LethalAvatars/wiki/Parameters";

    private SerializedProperty Viewpoint;
    private SerializedProperty SmallItemGrab;
    private SerializedProperty BigItemGrab;
    private SerializedProperty AvatarName;
    private SerializedProperty AvatarCreator;
    private SerializedProperty AvatarIcon;
    private SerializedProperty AvatarDescription;
    private SerializedProperty AllowDownloading;
    private Avatar Avatar;
    private Animator Animator;
    
    private static bool IsACListOpen;
    private static Vector2 ACScroll;

    private void OnEnable()
    {
        Viewpoint = serializedObject.FindProperty("Viewpoint");
        SmallItemGrab = serializedObject.FindProperty("SmallItemGrab");
        BigItemGrab = serializedObject.FindProperty("BigItemGrab");
        AvatarName = serializedObject.FindProperty("AvatarName");
        AvatarCreator = serializedObject.FindProperty("AvatarCreator");
        AvatarIcon = serializedObject.FindProperty("AvatarIcon");
        AvatarDescription = serializedObject.FindProperty("AvatarDescription");
        AllowDownloading = serializedObject.FindProperty("AllowDownloading");
        Avatar = target as Avatar;
        if(!BuilderWindow.Avatars.Contains(Avatar))
            BuilderWindow.Avatars.Add(Avatar);
    }

    public override void OnInspectorGUI()
    {
        if (Animator == null)
            Animator = Avatar.GetComponent<Animator>();
        bool problem = false;
        serializedObject.Update();
        if (Viewpoint.objectReferenceValue == null)
        {
            problem = true;
            EditorGUILayout.HelpBox("No Viewpoint attachment! This is required to build.", MessageType.Error);
        }
        Viewpoint.objectReferenceValue =
            EditorGUILayout.ObjectField("Viewpoint", Viewpoint.objectReferenceValue, typeof(Transform), true);
        if (SmallItemGrab.objectReferenceValue == null)
        {
            problem = true;
            EditorGUILayout.HelpBox("No Small Item attachment! This is required to build.", MessageType.Error);
        }
        else
        {
            Transform t = (Transform) SmallItemGrab.objectReferenceValue;
            if (Animator != null && Animator.avatar != null)
            {
                Transform rhand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rhand != null && t.parent != rhand)
                {
                    string n = "nothing";
                    if (t.parent != null)
                        n = t.parent.name;
                    EditorGUILayout.HelpBox(
                        $"Your SmallItem Transform is parented to {n} instead of {rhand.name} which may cause issues.",
                        MessageType.Warning);
                    if(GUILayout.Button($"Parent to {rhand.name}"))
                        t.SetParent(rhand, true);
                }
            }
            EditorGUILayout.HelpBox("You may have to edit the position and rotation values for this object",
                MessageType.Info);
            if (GUILayout.Button("Apply Recommended Values"))
            {
                t.SetLocalPositionAndRotation(new Vector3(-0.002f, 0.036f, -0.042f),
                    new Quaternion(-11.36602f, -7.213734f, 0.2278831f, 1));
                EditorUtility.SetDirty(t);
            }
        }
        SmallItemGrab.objectReferenceValue =
            EditorGUILayout.ObjectField("Small Item Grab", SmallItemGrab.objectReferenceValue, typeof(Transform), true);
        if (BigItemGrab.objectReferenceValue != null)
        {
            Transform t = (Transform) BigItemGrab.objectReferenceValue;
            if (Animator != null && Animator.avatar != null)
            {
                Transform rhand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rhand != null && t.parent != rhand)
                {
                    string n = "nothing";
                    if (t.parent != null)
                        n = t.parent.name;
                    EditorGUILayout.HelpBox(
                        $"Your BigItem Transform is parented to {n} instead of {rhand.name} which may cause issues.",
                        MessageType.Warning);
                    if(GUILayout.Button($"Parent to {rhand.name}"))
                        t.SetParent(rhand, true);
                }
            }
            EditorGUILayout.HelpBox("You may have to edit the position and rotation values for this object",
                MessageType.Info);
            if (GUILayout.Button("Apply Recommended Values"))
            {
                t.SetLocalPositionAndRotation(new Vector3(-0.002f, 0.036f, -0.042f),
                    new Quaternion(-11.36602f, -7.213734f, 0.2278831f, 1));
                EditorUtility.SetDirty(t);
            }
        }
        BigItemGrab.objectReferenceValue =
            EditorGUILayout.ObjectField("Big Item Grab", BigItemGrab.objectReferenceValue, typeof(Transform), true);
        EditorGUILayout.Separator();
        EditorGUILayout.HelpBox("AnimatorControllers allow you to animate your avatar based on parameters.", MessageType.Info);
        if(GUILayout.Button("Click here for a List of Parameters"))
            Application.OpenURL(PARAMETERS_DOC_URL);
        DrawingTools.DrawList("Avatar Controllers", ref IsACListOpen, ref ACScroll, ref Avatar.Animators,
            animatorController =>
            {
                if (animatorController == null)
                    return "New Animator Controller";
                return animatorController.name;
            }, () => null, data =>
            {
                RuntimeAnimatorController lastAnimatorController = data;
                data = (RuntimeAnimatorController) EditorGUILayout.ObjectField("Animator Controller", data, typeof(RuntimeAnimatorController), false);
                if(lastAnimatorController != data)
                    EditorUtility.SetDirty(Avatar.gameObject);
                return data;
            }, () => EditorUtility.SetDirty(Avatar.gameObject));
        EditorGUILayout.Separator();
        GUILayout.Label("Avatar Information", EditorStyles.centeredGreyMiniLabel);
        AvatarName.stringValue = EditorGUILayout.TextField("Avatar Name", AvatarName.stringValue);
        AvatarCreator.stringValue = EditorGUILayout.TextField("Avatar Creator", AvatarCreator.stringValue);
        AvatarIcon.objectReferenceValue =
            EditorGUILayout.ObjectField("Avatar Icon", AvatarIcon.objectReferenceValue, typeof(Sprite), false);
        GUILayout.Label("Avatar Description");
        AvatarDescription.stringValue = EditorGUILayout.TextArea(AvatarDescription.stringValue);
        if (AllowDownloading.boolValue)
            EditorGUILayout.HelpBox(
                "Allowing Downloading will allow users to download your Avatar so they do not have to keep downloading it. " +
                "This can be very dangerous and allow rippers to extract your assets! Only use this with people who you trust.",
                MessageType.Warning);
        AllowDownloading.boolValue = EditorGUILayout.Toggle("Allow Downloading", AllowDownloading.boolValue);
        serializedObject.ApplyModifiedProperties();
        if(problem) return;
        if (GUILayout.Button("Open in Avatar Builder"))
            BuilderWindow.ShowWindow(Avatar);
    }
}