using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Drawer
{

    static Texture2D _miniInfoIcon;
    public static Texture2D miniInfoIcon
    {
        get
        {
            if (_miniErrorIcon == null)
            {
                _miniErrorIcon = EditorGUIUtility.FindTexture("console.infoicon");
            }
            return _miniErrorIcon;
        }
    }

    static Texture2D _miniErrorIcon;
    public static Texture2D miniErrorIcon
    {
        get
        {
            if (_miniErrorIcon == null)
            {
                _miniErrorIcon = EditorGUIUtility.FindTexture("console.erroricon");
            }
            return _miniErrorIcon;
        }
    }

    static Texture2D _refreshIcon;
    public static Texture2D refreshIcon
    {
        get
        {
            if (_refreshIcon == null)
            {
                _refreshIcon = EditorGUIUtility.Load((EditorGUIUtility.isProSkin) ? "icons/d_Refresh.png" : "icons/Refresh.png") as Texture2D;
            }
            return _refreshIcon;
        }
    }

    static Texture2D _sideBarIcon;
    public static Texture2D sideBarIcon
    {
        get
        {
            if (_sideBarIcon == null)
            {
                _sideBarIcon = EditorGUIUtility.FindTexture("CustomSorting");
            }
            return _sideBarIcon;
        }
    }

    static Texture2D _zoomIcon;
    public static Texture2D zoomIcon
    {
        get
        {
            if (_zoomIcon == null)
            {
                _zoomIcon = EditorGUIUtility.FindTexture("ViewToolZoom On");
            }
            return _zoomIcon;
        }
    }

    static Texture2D _normalizedIcon;
    public static Texture2D normalizedIcon
    {
        get
        {
            if (_normalizedIcon == null)
            {
                _normalizedIcon = EditorGUIUtility.FindTexture("TimelineLoop");
            }
            return _normalizedIcon;
        }
    }

    static Texture2D _bakeScritpIcon;
    public static Texture2D bakeScritpIcon
    {
        get
        {
            if (_bakeScritpIcon == null)
            {
                _bakeScritpIcon = EditorGUIUtility.FindTexture("cs Script Icon");
            }
            return _bakeScritpIcon;
        }
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
    private static GUIStyle _oddStyle;
    public static GUIStyle oddStyle
    {
        get
        {
            if (_oddStyle == null)
            {
                _oddStyle = new GUIStyle
                {
                    normal ={
                background = CloudMacaca.CMEditorUtility.CreatePixelTexture("red Pixel (List GUI)", new Color32(0, 0, 0, 20))

                },
                    active =
                {
                    background = CloudMacaca.CMEditorUtility.CreatePixelTexture("red Pixel (List GUI)", new Color32(0, 0, 0, 20))
                },
                    imagePosition = ImagePosition.ImageOnly,
                    alignment = TextAnchor.MiddleCenter,
                    stretchWidth = true,
                    stretchHeight = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }
            return _oddStyle;
        }
    }

    private static GUIStyle _windowStyle;
    public static GUIStyle windowStyle
    {
        get
        {
            if (_windowStyle == null)
            {
                _windowStyle = new GUIStyle("window");
            }
            return _windowStyle;
        }
    }

    private static GUIStyle _bigLableStyle;
    public static GUIStyle bigLableStyle
    {
        get
        {
            if (_bigLableStyle == null)
            {
                _bigLableStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }
            return _bigLableStyle;
        }
    }
    private static GUIStyle _smallLableStyle;
    public static GUIStyle smallLableStyle
    {
        get
        {
            if (_smallLableStyle == null)
            {
                _smallLableStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }
            return _smallLableStyle;
        }
    }
}
