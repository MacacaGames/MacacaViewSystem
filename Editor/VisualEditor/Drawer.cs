using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Drawer
{
    public static GUIContent anchorsContent = EditorGUIUtility.TrTextContent("Anchors");
    public static GUIContent anchorMinContent = EditorGUIUtility.TrTextContent("Min", "The normalized position in the parent rectangle that the lower left corner is anchored to.");
    public static GUIContent anchorMaxContent = EditorGUIUtility.TrTextContent("Max", "The normalized position in the parent rectangle that the upper right corner is anchored to.");
    public static GUIContent pivotContent = EditorGUIUtility.TrTextContent("Pivot", "The pivot point specified in normalized values between 0 and 1. The pivot point is the origin of this rectangle. Rotation and scaling are around this point.");
    public static GUIContent transformScaleContent = EditorGUIUtility.TrTextContent("Scale", "The local scaling of this Game Object relative to the parent. This scales everything including image borders and text.");

    static GUISkin skin = ScriptableObject.Instantiate(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)) as GUISkin;
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

    //EditorGUIUtility.FindTexture( "SavePassive" )
    static Texture2D _savePassiveIcon;
    public static Texture2D savePassiveIcon
    {
        get
        {
            if (_savePassiveIcon == null)
            {
                _savePassiveIcon = EditorGUIUtility.FindTexture("SavePassive");
            }
            return _savePassiveIcon;
        }
    }
    static Texture2D _overrideIcon;
    public static Texture2D overrideIcon
    {
        get
        {
            if (_overrideIcon == null)
            {
                _overrideIcon = EditorGUIUtility.FindTexture("PrefabOverlayAdded Icon");
            }
            return _overrideIcon;
        }
    }
    static Texture2D _overridePopupIcon;
    public static Texture2D overridePopupIcon
    {
        get
        {
            if (_overridePopupIcon == null)
            {
                _overridePopupIcon = EditorGUIUtility.FindTexture("PrefabVariant Icon");
            }
            return _overridePopupIcon;
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
                _labelStyle = new GUIStyle(skin.label);
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
                _valueBoxStyle = new GUIStyle(skin.box);
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
                background = MacacaGames.CMEditorUtility.CreatePixelTexture("red Pixel (List GUI)", new Color32(0, 0, 0, 20))

                },
                    active =
                {
                    background = MacacaGames.CMEditorUtility.CreatePixelTexture("red Pixel (List GUI)", new Color32(0, 0, 0, 20))
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
    private static GUIStyle _overrideShowedStyle;
    public static GUIStyle overrideShowedStyle
    {
        get
        {
            if (_overrideShowedStyle == null)
            {
                _overrideShowedStyle = new GUIStyle
                {
                    normal ={
                background = MacacaGames.CMEditorUtility.CreatePixelTexture("_overrideShowedStyle Pixel (List GUI)", new Color32(120,216,99, 200))

                },
                    active =
                {
                    background = MacacaGames.CMEditorUtility.CreatePixelTexture("_overrideShowedStyle Pixel (List GUI)", new Color32(120,216,99, 200))
                },
                    imagePosition = ImagePosition.ImageOnly,
                    alignment = TextAnchor.MiddleCenter,
                    stretchWidth = true,
                    stretchHeight = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }
            return _overrideShowedStyle;
        }
    }
    private static GUIStyle _windowStyle;
    public static GUIStyle windowStyle
    {
        get
        {
            if (_windowStyle == null)
            {
                _windowStyle = new GUIStyle(skin.window);
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
    private static GUIStyle _removeButtonStyle;
    public static GUIStyle removeButtonStyle
    {
        get
        {
            if (_removeButtonStyle == null)
            {
                _removeButtonStyle = new GUIStyle
                {
                    //fixedWidth = 25f,
                    stretchHeight = true,
                    fontSize = 12,
                    active =
                    {
                        background = MacacaGames.CMEditorUtility.CreatePixelTexture("Dark Pixel (List GUI)", new Color32(100, 100, 100, 255))
                    },
                    imagePosition = ImagePosition.ImageAbove,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            return _removeButtonStyle;
        }
    }

    private static GUIStyle _darkBackgroundStyle;
    public static GUIStyle darkBackgroundStyle
    {
        get
        {
            if (_darkBackgroundStyle == null)
            {
                _darkBackgroundStyle = new GUIStyle
                {
                    normal =
                    {
                        background = MacacaGames.CMEditorUtility.CreatePixelTexture("Dark Pixel (List GUI)", new Color32(100, 100, 100, 255))
                    },
                    active =
                    {
                        background = MacacaGames.CMEditorUtility.CreatePixelTexture("Dark Pixel (List GUI)", new Color32(100, 100, 100, 255))
                    },
                    imagePosition = ImagePosition.ImageOnly,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            return _darkBackgroundStyle;
        }
    }
    private static GUIStyle _darkBoxStyle;
    public static GUIStyle darkBoxStyle
    {
        get
        {
            if (_darkBoxStyle == null)
            {
                _darkBoxStyle = new GUIStyle(skin.box);
                _darkBoxStyle.normal.background = MacacaGames.CMEditorUtility.CreatePixelTexture("Dark Pixel (List GUI)", new Color32(100, 100, 100, 255));
            }
            return _darkBoxStyle;
        }
    }

}
