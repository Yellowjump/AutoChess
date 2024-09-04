using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class GameObjectPathTool
{
    [MenuItem("GameObject/Copy Path to Clipboard", false, 0)]
    private static void CopyPathToClipboard()
    {
        if (Selection.activeGameObject != null)
        {
            string path = GetGameObjectPath(Selection.activeGameObject);
            EditorGUIUtility.systemCopyBuffer = path;
            Debug.Log("Path copied to clipboard: " + path);
        }
        else
        {
            Debug.LogWarning("No GameObject selected.");
        }
    }
    private static string GetGameObjectPath(GameObject gameObject)
    {
        StringBuilder path = new StringBuilder(gameObject.name);

        Transform current = gameObject.transform;
        while (current.parent != null)
        {
            current = current.parent;
            path.Insert(0, current.name + "/");
        }

        return path.ToString();
    }
}
