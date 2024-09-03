using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewSystemLog
{
    const string viewsystemloghead = "<color=darkblue><b>[View System]</b></color> ";
    public static void Log(object msg, Object context)
    {
        Debug.Log(viewsystemloghead + msg, context);
    }
    public static void LogWarning(object msg, Object context)
    {
        Debug.LogWarning(viewsystemloghead + msg, context);
    }
    public static void LogError(object msg, Object context)
    {
        Debug.LogError(viewsystemloghead + msg, context);
    }
    public static void Log(object msg)
    {
        Log(msg, null);
    }
    public static void LogWarning(object msg)
    {
        LogWarning(msg, null);
    }
    public static void LogError(object msg)
    {
        LogError(msg, null);
    }
#if UNITY_EDITOR
    public static void ShowNotification(UnityEditor.EditorWindow editor, GUIContent content, float time = 2)
    {
#if UNITY_2019_1_OR_NEWER
        editor.ShowNotification(content, time);
#else
        editor.ShowNotification(content);
#endif
    }
#endif


}
