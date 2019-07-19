using System.Collections;
using System.Collections.Generic;
using CloudMacaca.ViewSystem;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
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
                if (currentSelectGameObject)
                {
                    currentSelectGameObject = null;
                    componentDrawer = null;
                }
                if (currentSelectSerializedObject != null)
                {
                    currentSelectGameObject = lastSelectGameObject;
                    currentSelectSerializedObject = null;
                    propertiesDrawer = null;
                    componentDrawer = null;
                }
            }
        }
    }

    Vector2 scrollPosition;
    void DrawScrollView()
    {
        using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPosition))
        {
            scrollPosition = scrollViewScope.scrollPosition;

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

    private void CacheProperties()
    {
        propertiesDrawer = new PropertiesDrawer(currentSelectSerializedObject);
        propertiesDrawer.OnItemClick += (so, sp) =>
        {
            Debug.Log("Target Object : " + so.targetObject.name);
            Debug.Log("Type : " + so.targetObject.GetType().ToString());

            Debug.Log("SerializedPropertyType : " + sp.propertyType);
            Debug.Log("Property Type : " + sp.type);
            Debug.Log("Property Name : " + sp.name);
            Debug.Log("Property DisplayName : " + sp.displayName);
            Debug.Log("GameObject Name : " + lastSelectGameObject.name);

            var viewElementPropertyOverrideData = ScriptableObject.CreateInstance<ViewElementPropertyOverrideData>();

            viewElementPropertyOverrideData.targetComponentType = so.targetObject.GetType().ToString();
            viewElementPropertyOverrideData.targetPropertyName = sp.name;
            viewElementPropertyOverrideData.targetTransformPath = AnimationUtility.CalculateTransformPath(lastSelectGameObject.transform, null);
        };
    }

    private float _time = 2;
}
