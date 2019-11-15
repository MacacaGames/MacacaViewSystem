using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewSystemLog
{
    const string viewsystemloghead = "<color=darkblue><b>[View System]</b></color>";
    public static void Log(object msg, Object context)
    {
        Debug.Log(viewsystemloghead + msg);
    }
    public static void LogWarning(object msg, Object context)
    {
        Debug.LogWarning(viewsystemloghead + msg);
    }
    public static void LogError(object msg, Object context)
    {
        Debug.LogError(viewsystemloghead + msg);
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
}
