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

    static GUIStyle _labelStyle;
    static public GUIStyle labelStyle
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
    private static GUIStyle _valueBoxStyle;
    public static GUIStyle valueBoxStyle
    {
        get
        {
            if (_valueBoxStyle == null)
            {
                _valueBoxStyle = new GUIStyle(GUI.skin.FindStyle("box"));
                _valueBoxStyle.border = new RectOffset(1, 1, 1, 1);
                _valueBoxStyle.contentOffset = Vector2.zero;
                _valueBoxStyle.margin = new RectOffset(0, 0, 0, 0);
                _valueBoxStyle.padding = new RectOffset(0, 0, 0, 0);
                _valueBoxStyle.alignment = TextAnchor.MiddleCenter;
                _valueBoxStyle.wordWrap = false;
                _valueBoxStyle.clipping = TextClipping.Clip;
                _valueBoxStyle.normal.textColor = EditorStyles.label.normal.textColor;
                _valueBoxStyle.stretchHeight = true;
                _valueBoxStyle.stretchWidth = true;
            }
            return _valueBoxStyle;
        }
    }
}
