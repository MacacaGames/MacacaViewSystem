using System.Collections;
using System.Collections.Generic;
using CloudMacaca.ViewSystem;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;
using UnityEditorInternal;
using System;

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
    [SerializeField] TreeViewState m_TreeViewState;

    HierarchyTreeView hierarchyTreeView;
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

                // 這裡可能有問題
                return;
            }

            if (currentSelectSerializedObject != null)
            {
                if (propertiesDrawer == null)
                {
                    CacheProperties();
                }
                propertiesDrawer.Draw();
                // 這裡可能有問題
                return;
            }
            //if (hierarchyDrawer != null) hierarchyDrawer.Draw();
            if (hierarchyTreeView != null) hierarchyTreeView.OnGUI(new Rect(0, 0, position.width, position.height));

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
                UnityEngine.Object targetComponent = targetObject.GetComponent(item.targetComponentType);
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

                //EditorGUI.PropertyField(rect, sp);
                if (EditorGUICM.EditorableField(rect, new GUIContent(sp.displayName), sp, item.Value))
                {

                }
                //var result = EditorGUICM.GetPropertyType(sp);
                //}
            };
        }
        using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPositionModified))
        {
            scrollPositionModified = scrollViewScope.scrollPosition;
            reorderableListViewModify.DoLayoutList();
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

        if (m_TreeViewState == null)
            m_TreeViewState = new TreeViewState();

        hierarchyTreeView = new HierarchyTreeView(target.transform, m_TreeViewState);
        hierarchyTreeView.OnItemClick += (go) =>
        {
            currentSelectGameObject = (GameObject)go;
        };
        // hierarchyDrawer = new HierarchyDrawer(target.transform);
        // hierarchyDrawer.OnItemClick += (go) =>
        // {
        //     currentSelectGameObject = (GameObject)go;
        // };
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
            // Debug.Log("Name Of : " + nameof(sp));

            overrideData.targetTransformPath = AnimationUtility.CalculateTransformPath(lastSelectGameObject.transform, target.transform);
            overrideData.targetPropertyName = sp.name;
            overrideData.targetComponentType = so.targetObject.GetType().ToString();
            overrideData.targetPropertyType = sp.propertyType.ToString();
            overrideData.targetPropertyPath = EditorGUICM.ParseUnityEngineProperty(sp.propertyPath);
            overrideData.Value = EditorGUICM.GetValue(sp);
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

    class EditorGUICM
    {
        public static string ParseUnityEngineProperty(string ori)
        {
            if (ori.ToLower().Contains("material"))
            {
                return "material";
            }
            if (ori.ToLower().Contains("sprite"))
            {
                return "sprite";
            }
            if (ori.ToLower().Contains("active"))
            {
                return "active";
            }
            string result = ori.Replace("m_", "");
            result = result.Substring(0, 1).ToLower() + result.Substring(1);
            return result;
        }

        public static Type GetPropertyType(SerializedProperty property)
        {
            var type = property.type;
            var match = System.Text.RegularExpressions.Regex.Match(type, @"PPtr<\$(.*?)>");
            if (match.Success)
                type = "UnityEngine." + match.Groups[1].Value;
            return CloudMacaca.Utility.GetType(type);
        }

        // public static Type GetPropertyObjectType(SerializedProperty property)
        // {
        //     return typeof(UnityEngine.Object).Assembly.GetType("UnityEngine." + GetPropertyType(property));
        // }

        public static bool EditorableField(Rect rect, GUIContent content, SerializedProperty Target, PropertyOverride overProperty)
        {
            EditorGUI.BeginChangeCheck();
            switch (Target.propertyType)
            {
                case SerializedPropertyType.Float:
                    overProperty.FloatValue = EditorGUI.FloatField(rect, content, overProperty.FloatValue);
                    break;
                case SerializedPropertyType.Integer:
                    overProperty.IntValue = EditorGUI.IntField(rect, content, overProperty.IntValue);
                    break;
                case SerializedPropertyType.String:
                    overProperty.StringValue = EditorGUI.TextField(rect, content, overProperty.StringValue);
                    break;
                case SerializedPropertyType.Boolean:
                    overProperty.BooleanValue = EditorGUI.Toggle(rect, content, overProperty.BooleanValue);
                    break;
                case SerializedPropertyType.Color:
                    overProperty.ColorValue = EditorGUI.ColorField(rect, content, overProperty.ColorValue);
                    break;
                case SerializedPropertyType.ObjectReference:
                    overProperty.ObjectReferenceValue = EditorGUI.ObjectField(rect, content, overProperty.ObjectReferenceValue, GetPropertyType(Target), false);
                    break;
            }
            return EditorGUI.EndChangeCheck();
        }

        public static PropertyOverride GetValue(SerializedProperty property)
        {
            PropertyOverride overProperty = new PropertyOverride();

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    overProperty.FloatValue = property.floatValue;
                    overProperty.SetType(PropertyOverride.S_Type._float);
                    break;
                case SerializedPropertyType.Integer:
                    overProperty.IntValue = property.intValue;
                    overProperty.SetType(PropertyOverride.S_Type._float);
                    break;
                case SerializedPropertyType.String:
                    overProperty.StringValue = property.stringValue;
                    overProperty.SetType(PropertyOverride.S_Type._string);
                    break;
                case SerializedPropertyType.Boolean:
                    overProperty.BooleanValue = property.boolValue;
                    overProperty.SetType(PropertyOverride.S_Type._bool);
                    break;
                case SerializedPropertyType.Color:
                    overProperty.ColorValue = property.colorValue;
                    overProperty.SetType(PropertyOverride.S_Type._color);
                    break;
                case SerializedPropertyType.ObjectReference:
                    overProperty.ObjectReferenceValue = property.objectReferenceValue;
                    overProperty.SetType(PropertyOverride.S_Type._objcetReferenct);
                    break;
            }

            return overProperty;
        }
    }
}
