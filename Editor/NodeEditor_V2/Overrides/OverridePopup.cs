using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class OverridePopup : PopupWindowContent
{
    GameObject target;
    public OverridePopup(float width, float height, GameObject gameObject)
    {
        target = gameObject;
        _time = Time.realtimeSinceStartup;
        maxSize = new Vector2(width * 1.5f, height * 1.5f);
        minSize = new Vector2(width, height);
        currentSelectGameObject = null;
        CacheHierarchy();
    }
    Vector2 maxSize;
    Vector2 minSize;

    public override void OnGUI(Rect rect)
    {
        editorWindow.maxSize = maxSize;
        editorWindow.minSize = minSize;
        // DrawTab();
        switch (_selectedTab)
        {
            case 0:
                DrawHeader();
                DrawScrollView();
                break;
            case 1:

                break;
        }
        base.editorWindow.Repaint();
    }
    GUIContent header = new GUIContent();
    private void DrawHeader()
    {
        if (currentSelectGameObject)
        {
            header.image = EditorGUIUtility.FindTexture("Prefab Icon");
            header.text = currentSelectGameObject.gameObject.name;
        }
        else if (currentSelectSerializedObject != null)
        {
            header = new GUIContent(EditorGUIUtility.ObjectContent(currentSelectSerializedObject.targetObject, currentSelectSerializedObject.targetObject.GetType()));
        }
        else
        {
            header.text = target.gameObject.name;
            header.image = EditorGUIUtility.FindTexture("Prefab Icon");
        }
        using (var horizon = new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button(header, new GUIStyle("dockareaStandalone"), GUILayout.Width(editorWindow.position.width)))
            {
                if (currentSelectGameObject) currentSelectGameObject = null;
                if (currentSelectSerializedObject != null)
                {
                    currentSelectGameObject = lastSelectGameObject;
                    currentSelectSerializedObject = null;
                }
            }
        }
    }

    Vector2 scrollPosition;
    void DrawScrollView()
    {
        using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPosition))
        {
            if (currentSelectGameObject)
            {
                if (componentDrawer == null)
                {
                    CacheComponent();
                }
                componentDrawer.Draw();
                return;
            }

            if (currentSelectSerializedObject != null)
            {
                if (propertiesDrawer == null)
                {
                    CacheProperties();
                }
                propertiesDrawer.Draw();
                return;
            }
            if (hierarchyDrawer != null) hierarchyDrawer.Draw();
            scrollPosition = scrollViewScope.scrollPosition;
        }
    }

    private int _selectedTab;
    private void DrawTab()
    {
        using (var horizon = new GUILayout.HorizontalScope())
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                _selectedTab = GUILayout.Toolbar(_selectedTab, new string[2]
                    { "Hierarchy","Modified Properties"},
                    EditorStyles.toolbarButton);
                if (check.changed)
                {
                    // if (_selectedTab == 1)
                    // {
                    //     CacheModifiedProperties();
                    // }
                    // else
                    // {
                    //     CacheHierarchy();
                    // }
                }
            }
        }
    }

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

    HierarchyDrawer hierarchyDrawer;
    GameObject currentSelectGameObject;
    GameObject lastSelectGameObject;
    private void CacheHierarchy()
    {
        hierarchyDrawer = new HierarchyDrawer(target.transform);
        hierarchyDrawer.OnItemClick += (go) =>
        {
            currentSelectGameObject = (GameObject)go;
        };
    }

    ComponentDrawer componentDrawer;
    SerializedObject currentSelectSerializedObject;

    private void CacheComponent()
    {
        componentDrawer = new ComponentDrawer(currentSelectGameObject);
        componentDrawer.OnItemClick += (go) =>
        {
            currentSelectSerializedObject = go;
            lastSelectGameObject = currentSelectGameObject;
            currentSelectGameObject = null;
        };
    }

    PropertiesDrawer propertiesDrawer;
    SerializedProperty currentSelectSerializedProperty;

    private void CacheProperties()
    {
        propertiesDrawer = new PropertiesDrawer(currentSelectSerializedObject);
        // propertiesDrawer.OnItemClick += (go) =>
        // {
        //     currentSelectSerializedProperty = go;
        // };
    }
    private void CacheModifiedProperties()
    {
        // _hierarchyDrawer.Clear();
        // _addAllModifiedProperties = false;
        // foreach (Modification item in NestedPrefabUtility.GetModificationsForInstance(PrefabOverrides.GetComponent<Prefab>(), true))
        // {
        //     if (item.Type == ModificationType.PropertyValueChanged && PrefabOverrideUtility.CanOverrideProperty(NestedPrefabUtility.GetPrefab(item.DestinationObject.targetObject.AsGameObject()).CachedTransform, item.DestinationObject, item.DestinationProperty) && item.DestinationProperty.propertyType != SerializedPropertyType.Generic && !PrefabOverrideUtility.IsOverridden(PrefabOverrides.transform, item.DestinationProperty))
        //     {
        //         Component component = item.DestinationObject.targetObject as Component;
        //         GameObject target = (!(component != null)) ? (item.DestinationObject.targetObject as GameObject) : component.gameObject;
        //         ToggleObjectDrawer child = _hierarchyDrawer.GetChild<ToggleObjectDrawer>(target, true);
        //         child.Padding = new RectOffset(-3, 3, 0, 0);
        //         child.ComponentsAreExpanded = true;
        //         ComponentDrawer componentDrawer = child.GetComponentDrawer<ToggleComponentDrawer>(item.DestinationObject, true);
        //         componentDrawer.Padding = new RectOffset(-3, 3, 0, 0);
        //         componentDrawer.IsExpanded = true;
        //         componentDrawer.SetDepth(1);
        //         VisualDesignCafe.Editor.Prefabs.Overrides.PropertyDrawer child2 = componentDrawer.GetChild<TogglePropertyDrawer>(item.DestinationProperty, null, true);
        //         child2.Padding = new RectOffset(-3, 3, 0, 0);
        //         child2.IsExpanded = false;
        //         child2.SetDepth(2);
        //     }
        // }
    }
    private float _time = 2;
    private float _maxiumHeight = 330f;
    private float _maximumWidth;

}
