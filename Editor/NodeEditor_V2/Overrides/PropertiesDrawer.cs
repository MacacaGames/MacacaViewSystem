using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class PropertiesDrawer : Drawer
{
    SerializedObject go;
    public PropertiesDrawer(SerializedObject go)
    {
        this.go = go;
        CacheItem(go, 0);
    }
    public override void Draw()
    {
        base.Draw();
        using (var vertical = new GUILayout.VerticalScope())
        {
            foreach (var item in serializedPropertys)
            {
                using (var horizon = new GUILayout.HorizontalScope())
                {
                    // DrawValue(item);
                    // DrawFoldout(item);

                    EditorGUILayout.PropertyField(item);
                    // var _cachedContent = new GUIContent(EditorGUIUtility.ObjectContent(item.targetObject, item.targetObject.GetType()));
                    //GUILayout.Label(_cachedContent.image, GUILayout.Width(20), GUILayout.Height(20));

                    // if (GUILayout.Button( item.name, labelStyle))
                    // {
                    //     //OnItemClick?.Invoke(item);
                    // }
                    GUILayout.Label(arrowIcon, GUILayout.Width(20), GUILayout.Height(20));
                }
            }
        }
    }
    private GUIStyle _valueBoxStyle;
    private GUIStyle valueBoxStyle
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
            }
            return _valueBoxStyle;
        }
    }
    protected void DrawValue(SerializedProperty Target)
    {
        if (Target.propertyType != SerializedPropertyType.Generic)
        {
            Color backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor *= new Color(1f, 1f, 1f, 0.5f);
            switch (Target.propertyType)
            {
                case SerializedPropertyType.Float:
                    GUILayout.Box(Target.floatValue.ToString(), valueBoxStyle);
                    break;
                case SerializedPropertyType.Integer:
                    GUILayout.Box(Target.intValue.ToString(), valueBoxStyle);
                    break;
                case SerializedPropertyType.String:
                    GUILayout.Box(new GUIContent("\"" + Target.stringValue + "\"", Target.stringValue), valueBoxStyle);
                    break;
                case SerializedPropertyType.Boolean:
                    GUILayout.Box(Target.boolValue.ToString(), valueBoxStyle);
                    break;
                case SerializedPropertyType.Color:
                    {
                        GUILayout.Box(string.Empty, valueBoxStyle);
                        // EditorGUI.DrawRect(rect3, new Color(Target.colorValue.r, Target.colorValue.g, Target.colorValue.b, 1f));
                        // EditorGUI.DrawRect(rect4, Color.black);
                        // EditorGUI.DrawRect(new Rect(rect4.x, rect4.y, rect4.width * Target.colorValue.a, rect4.height), Color.white);
                        break;
                    }
                case SerializedPropertyType.LayerMask:
                    GUILayout.Box(Target.intValue.ToString(), valueBoxStyle);
                    break;
                default:
                    GUILayout.Box(new GUIContent(Target.propertyType.ToString(), Target.propertyType.ToString()), valueBoxStyle);
                    break;
            }
            GUI.backgroundColor = backgroundColor;
        }
    }

    protected void DrawFoldout(SerializedProperty Target)
    {
        if (Target.propertyType == SerializedPropertyType.Generic)
        {
            GUILayout.Label(string.Empty, EditorStyles.foldout);
        }
    }
    List<SerializedProperty> serializedPropertys = new List<SerializedProperty>();
    void CacheItem(SerializedObject obj, int layer)
    {
        var prop = obj.GetIterator().Copy();
        while (prop.NextVisible(true))
        {
            serializedPropertys.Add(prop.Copy());
        }
    }
    public delegate void _OnItemClick(SerializedObject target);
    public event _OnItemClick OnItemClick;

}