using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Drawer
{

    public virtual void Draw()
    {

    }
    public bool BackgroundIsVisible;
    public bool IsHovering;


    protected virtual void DrawBackground(Rect rect)
    {
        if (BackgroundIsVisible)
        {
            EditorGUI.DrawRect(new Rect(0f, rect.y - 1f, Screen.width, rect.height + 2f), Color.black);
        }
        if (IsHovering)
        {
            EditorGUI.DrawRect(rect, new Color32(62, 125, 231, byte.MaxValue));
        }
    }

    protected virtual void HandleHover(Rect rect)
    {
        IsHovering = rect.Contains(Event.current.mousePosition);
    }




    static Texture2D _prefabIcon;
    static public Texture2D prefabIcon
    {
        get
        {
            if (_prefabIcon == null)
            {
                _prefabIcon = EditorGUIUtility.FindTexture("Prefab Icon");
            }
            return _prefabIcon;
        }
    }
    static Texture2D _arrowIcon;
    static public Texture2D arrowIcon
    {
        get
        {
            if (_arrowIcon == null)
            {
                _arrowIcon = EditorGUIUtility.FindTexture("Animation.Play");
            }
            return _arrowIcon;
        }
    }

    GUIStyle _labelStyle;
    protected GUIStyle labelStyle
    {
        get
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle("label");
                _labelStyle.onHover.background = EditorGUIUtility.FindTexture("AnimationEventBackground");
                _labelStyle.fontSize = 14;
            }
            return _labelStyle;
        }
    }
}
