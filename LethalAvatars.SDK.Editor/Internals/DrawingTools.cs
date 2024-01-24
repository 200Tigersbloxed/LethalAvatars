using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LethalAvatars.SDK.Editor.Internals;

internal static class DrawingTools
{
    internal static void DrawList<T>(string listName, ref bool isOpen, ref Vector2 scroll, ref List<T> list,
        Func<T, string> getObjectName, Func<T> createNewObject, Func<T, T> displayObject, Action save,
        string nullString = "Select an Object")
    {
        int remove = -1;
        isOpen = EditorGUILayout.Foldout(isOpen, $"<b>{listName}</b>",
            new GUIStyle(EditorStyles.foldout) {richText = true});
        if(isOpen)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < list.Count; i++)
            {
                T obj = list[i];
                GUILayout.Label(obj != null ? getObjectName.Invoke(obj) : nullString);
                list[i] = displayObject.Invoke(list[i]);
                if (GUILayout.Button("Remove"))
                    remove = i;
            }
            if (remove > -1)
            {
                list.RemoveAt(remove);
                save.Invoke();
            }
            if (GUILayout.Button($"Add New {typeof(T).Name}"))
            {
                list.Add(createNewObject.Invoke());
                save.Invoke();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}