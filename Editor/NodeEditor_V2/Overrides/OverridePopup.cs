using System.Collections;
using System.Collections.Generic;
using CloudMacaca.ViewSystem;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;
using UnityEditorInternal;

public class OverridePopup : EditorWindow
{
    GameObject target;
    ViewPageItem viewPageItem;
    public void Init(ViewPageItem viewPageItem)
    {
        target = viewPageItem.viewElement.gameObject;
        this.viewPageItem = viewPageItem;
        // maxSize = new Vector2(width * 1.5f, height * 1.5f);
        // minSize = new Vector2(width, height);
        currentSelectGameObject = null;
        CacheHierarchy();

    }
    public void Awake()
    {

    }
    public void OnGUI()
    {

        //editorWindow.maxSize = maxSize;
        //editorWindow.minSize = minSize;
        DrawTab();
        switch (_selectedTab)
        {
            case 0:
                DrawHeader();
                DrawScrollViewHierarchy();
                break;
            case 1:
                DrawScrollViewModify();
                break;
        }
        //base.editorWindow.Repaint();
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
            if (GUILayout.Button(header, new GUIStyle("dockareaStandalone"), GUILayout.Width(position.width)))
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

    Vector2 scrollPositionHierarchy;
    void DrawScrollViewHierarchy()
    {
        using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPositionHierarchy))
        {
            scrollPositionHierarchy = scrollViewScope.scrollPosition;

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
    Vector2 scrollPositionModified;
    ReorderableList reorderableListViewModify;

    void DrawScrollViewModify()
    {
        if (reorderableListViewModify == null)
        {
            reorderableListViewModify = new ReorderableList(viewPageItem.overrideDatas, typeof(List<ViewElementPropertyOverrideData>), true, false, false, true);
            reorderableListViewModify.elementHeight = EditorGUIUtility.singleLineHeight * 2.5f;
            reorderableListViewModify.drawElementCallback += (rect, index, isActive, isFocused) =>
            {
                var ori_Rect = rect;
                rect.y += EditorGUIUtility.singleLineHeight * 0.25f;
                rect.height = EditorGUIUtility.singleLineHeight;
                // using (var areaScope = new GUILayout.AreaScope(rect))
                // {
                var item = viewPageItem.overrideDatas[index];
                var targetObject = viewPageItem.viewElement.transform.Find(item.targetTransformPath);
                Object targetComponent = targetObject.GetComponent(item.targetComponentType);
                if (item.targetComponentType.Contains("GameObject"))
                {
                    targetComponent = targetObject.gameObject;
                }

                if (targetComponent == null)
                {
                    GUI.Label(rect, new GUIContent("Unsupport ComponentType", EditorGUIUtility.FindTexture("console.erroricon.sml")));
                    rect.y += EditorGUIUtility.singleLineHeight;
                    GUI.Label(rect, new GUIContent("ComponentType : " + item.targetComponentType));
                    return;
                }
                var _cachedContent = new GUIContent(EditorGUIUtility.ObjectContent(targetComponent, targetComponent.GetType()));
                var so = new SerializedObject(targetComponent);
                var sp = so.FindProperty(item.targetPropertyName);

                rect.width = 20;
                GUI.Label(rect, EditorGUIUtility.FindTexture("Prefab Icon"));
                rect.x += rect.width;

                GUIContent l = new GUIContent(target.name + (string.IsNullOrEmpty(item.targetTransformPath) ? "" : ("/" + item.targetTransformPath)));
                rect.width = GUI.skin.label.CalcSize(l).x;
                GUI.Label(rect, l);
                rect.x += rect.width;

                rect.width = 20;
                GUI.Label(rect, EditorGUIUtility.FindTexture("Animation.Play"));
                rect.x += rect.width;

                rect.width = GUI.skin.label.CalcSize(_cachedContent).x; ;
                GUI.Label(rect, _cachedContent);

                rect.y += EditorGUIUtility.singleLineHeight;
                rect.height = EditorGUIUtility.singleLineHeight;
                rect.width = ori_Rect.width;
                rect.x = ori_Rect.x;

                EditorGUI.PropertyField(rect, sp);
                //}
            };
        }
        using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPositionModified))
        {
            scrollPositionModified = scrollViewScope.scrollPosition;
            reorderableListViewModify.DoLayoutList();
            // foreach (var item in viewPageItem.overrideDatas)
            // {
            //     using (var vertical = new GUILayout.VerticalScope("box"))
            //     {
            //         var targetObject = viewPageItem.viewElement.transform.Find(item.targetTransformPath);
            //         Object targetComponent = targetObject.GetComponent(item.targetComponentType);
            //         if (item.targetComponentType.Contains("GameObject"))
            //         {
            //             targetComponent = targetObject.gameObject;
            //         }
            //         var _cachedContent = new GUIContent(EditorGUIUtility.ObjectContent(targetComponent, targetComponent.GetType()));
            //         var so = new SerializedObject(targetComponent);
            //         var sp = so.FindProperty(item.targetPropertyName);

            //         using (var horizon = new GUILayout.HorizontalScope())
            //         {
            //             GUILayout.Label(EditorGUIUtility.FindTexture("Prefab Icon"), GUILayout.Width(20), GUILayout.Height(20));
            //             var l = new GUIContent(target.name + (string.IsNullOrEmpty(item.targetTransformPath) ? "" : ("/" + item.targetTransformPath)));
            //             GUILayout.Label(l, GUILayout.Width(GUI.skin.label.CalcSize(l).x), GUILayout.Height(20));
            //             GUILayout.Label(EditorGUIUtility.FindTexture("Animation.Play"), GUILayout.Width(15), GUILayout.Height(20));
            //             GUILayout.Label(_cachedContent, GUILayout.Height(20));
            //         }
            //         EditorGUILayout.PropertyField(sp);
            //     }
            // }
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
            var overrideData = new ViewElementPropertyOverrideData();

            // Debug.Log("Target Object : " + so.targetObject.name);
            // Debug.Log("Type : " + so.targetObject.GetType().ToString());

            // Debug.Log("SerializedPropertyType : " + sp.propertyType);
            // Debug.Log("Property Type : " + sp.type);
            // Debug.Log("Property Name : " + sp.name);
            // Debug.Log("Property DisplayName : " + sp.displayName);
            // Debug.Log("GameObject Name : " + lastSelectGameObject.name);
            // Debug.Log("Transform Path : " + AnimationUtility.CalculateTransformPath(lastSelectGameObject.transform, target.transform));

            overrideData.targetTransformPath = AnimationUtility.CalculateTransformPath(lastSelectGameObject.transform, target.transform);
            overrideData.targetPropertyName = sp.name;
            overrideData.targetComponentType = so.targetObject.GetType().ToString();

            if (viewPageItem.overrideDatas == null)
            {
                viewPageItem.overrideDatas = new List<ViewElementPropertyOverrideData>();
            }

            var current = viewPageItem.overrideDatas
                .SingleOrDefault(x =>
                    x.targetTransformPath == overrideData.targetTransformPath &&
                    x.targetComponentType == overrideData.targetComponentType &&
                    x.targetPropertyName == overrideData.targetPropertyName
                );

            if (current != null)
            {
                current = overrideData;
            }
            else
            {
                viewPageItem.overrideDatas.Add(overrideData);
            }
        };
    }
}
