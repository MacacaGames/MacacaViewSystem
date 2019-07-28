using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class PropertiesDrawer : Drawer
{
    SerializedObject serializedObject;
    public PropertiesDrawer(SerializedObject serializedObject)
    {
        this.serializedObject = serializedObject;
        CacheItem(serializedObject, 0);
    }
    public override void Draw()
    {
        base.Draw();

        using (var vertical = new GUILayout.VerticalScope())
        {
            foreach (var item in serializedPropertys)
            {
                using (var horizon = new GUILayout.HorizontalScope(GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                {

                    DrawLabel(item);
                    DrawValue(item);
                    DrawFoldout(item);
                }

                GUI.color = new Color(0, 0, 0, 0);
                if (GUI.Button(GUILayoutUtility.GetLastRect(), GUIContent.none))
                {
                    OnItemClick?.Invoke(serializedObject, item);
                    Debug.Log("click");
                }
                GUI.color = new Color(1, 1, 1, 1);
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
                _valueBoxStyle.stretchHeight = true;
                _valueBoxStyle.stretchWidth = true;
            }
            return _valueBoxStyle;
        }
    }
    protected void DrawLabel(SerializedProperty Target)
    {
        GUILayout.Label("    " + Target.displayName);
    }
    protected void DrawValue(SerializedProperty Target)
    {
        using (var a = new GUILayout.HorizontalScope(GUILayout.Width(150), GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
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
                            var rect2 = GUILayoutUtility.GetLastRect();
                            Rect rect3 = rect2.Contract(1f, 1f, 1f, 1f);
                            EditorGUI.DrawRect(rect3, new Color(Target.colorValue.r, Target.colorValue.g, Target.colorValue.b, 1f));
                            Rect rect4 = rect3.Contract(0f, 16f, 0f, 0f);
                            EditorGUI.DrawRect(rect4, Color.black);
                            EditorGUI.DrawRect(new Rect(rect4.x, rect4.y, rect4.width * Target.colorValue.a, rect4.height), Color.white);
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
        //GameObject Hack
        var active = obj.FindProperty("m_IsActive");
        if (active != null)
        {
            serializedPropertys.Add(active.Copy());
        }

        prop.NextVisible(true);
        do
        {
            serializedPropertys.Add(prop.Copy());
        }
        while (prop.NextVisible(false));

    }
    public delegate void _OnItemClick(SerializedObject targetObject, SerializedProperty targetProperty);
    public event _OnItemClick OnItemClick;



    private void DrawPropertyField(Rect rect, SerializedProperty property, GUIContent label)
    {
        // switch (Target.propertyPath)
        // {
        //     case "m_SortingLayerID":
        //         DrawSortingLayerIdField(rect, property, label);
        //         break;
        //     case "m_Layer":
        //         property.intValue = EditorGUI.LayerField(rect, label, property.intValue);
        //         break;
        //     case "m_TagString":
        //         DrawTagStringField(rect, property, label);
        //         break;
        //     default:
        //         EditorGUI.PropertyField(rect, property, label, false);
        //         break;
        // }
    }
    private void DrawTagStringField(Rect rect, SerializedProperty property, GUIContent label)
    {
        // if (_cachedTagList == null)
        // {
        // 	_cachedTagList = (from t in InternalEditorUtility.tags
        // 		select new GUIContent(t)).ToArray();
        // }
        // int selectedIndex = 0;
        // for (int i = 0; i < _cachedTagList.Length; i++)
        // {
        // 	if (_cachedTagList[i].text == property.stringValue)
        // 	{
        // 		selectedIndex = i;
        // 		break;
        // 	}
        // }
        // EditorGUI.BeginChangeCheck();
        // selectedIndex = EditorGUI.Popup(rect, label, selectedIndex, _cachedTagList);
        // if (EditorGUI.EndChangeCheck())
        // {
        // 	property.stringValue = _cachedTagList[selectedIndex].text;
        // }
    }
    private void DrawSortingLayerIdField(Rect rect, SerializedProperty property, GUIContent label)
    {
        // if (_cachedSortingLayerUniqueIDs == null)
        // {
        // 	_cachedSortingLayerUniqueIDs = (int[])typeof(InternalEditorUtility).GetProperty("sortingLayerUniqueIDs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
        // }
        // if (_cachedSortingLayerNames == null)
        // {
        // 	_cachedSortingLayerNames = (from n in (string[])typeof(InternalEditorUtility).GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null)
        // 		select new GUIContent(n)).ToArray();
        // }
        // int num = _cachedSortingLayerUniqueIDs.IndexOf(property.intValue);
        // if (num == -1)
        // {
        // 	num = 0;
        // }
        // EditorGUI.BeginChangeCheck();
        // num = EditorGUI.Popup(rect, label, num, _cachedSortingLayerNames);
        // if (EditorGUI.EndChangeCheck())
        // {
        // 	property.intValue = _cachedSortingLayerUniqueIDs[num];
        // }
    }

}