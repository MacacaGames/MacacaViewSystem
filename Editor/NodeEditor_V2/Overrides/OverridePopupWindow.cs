using System.Collections;
using System.Collections.Generic;
using CloudMacaca.ViewSystem;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;
using UnityEditorInternal;
using System;
using CloudMacaca;
using System.Reflection;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class OverridePopupWindow
    {
        static ViewSystemSaveData saveData => ViewSystemNodeEditor.saveData;
        ViewSystemNodeEditor editor;
        GameObject target;
        ViewPageItem viewPageItem;
        GUIStyle removeButtonStyle;
        static float toastMessageFadeOutTimt = 1.5f;
        bool isInit => target != null;
        public static bool show = false;
        Rect windowRect = new Rect(0, 0, 350, 400);
        GUIStyle windowStyle;
        public OverridePopupWindow(ViewSystemNodeEditor editor)
        {
            this.editor = editor;
            removeButtonStyle = new GUIStyle
            {
                fixedWidth = 25f,
                active =
            {
                background = CMEditorUtility.CreatePixelTexture("Dark Pixel (List GUI)", new Color32(100, 100, 100, 255))
            },
                imagePosition = ImagePosition.ImageOnly,
                alignment = TextAnchor.MiddleCenter
            };
            show = false;
            windowStyle = GUI.skin.window;
            RectOffset padding = windowStyle.padding;
            padding.left = 0;
            padding.right = 1;
            padding.bottom = 0;
        }
        public void SetViewPageItem(ViewPageItem viewPageItem)
        {
            this.viewPageItem = viewPageItem;
            currentSelectGameObject = null;
            if (viewPageItem == null) return;
            target = viewPageItem.viewElement.gameObject;
            CacheHierarchy();
            RefreshMethodDatabase();
        }

        Rect itemRect;
        public void Show(Rect item)
        {
            itemRect = item;
            windowRect.x = item.x + item.width + 50;
            windowRect.y = 200;
            show = true;
        }
        float closeBtnSize = 20;
        public void OnGUI()
        {
            if (!show)
            {
                return;
            }

            windowRect = GUILayout.Window(999, windowRect, Draw, "", GUIStyle.none);

            GUI.Box(windowRect, "ViewElement Override", windowStyle);
            if (GUI.Button(new Rect(windowRect.x + windowRect.width, windowRect.y, closeBtnSize, closeBtnSize), new GUIContent(EditorGUIUtility.FindTexture("winbtn_win_close"))))
            {
                Debug.Log("click");
                show = false;
                SetViewPageItem(null);
            }
            Handles.DrawLine(itemRect.center, windowRect.position);
            // Handles.DrawBezier(
            //     itemRect.center,
            //     windowRect.position,
            //     itemRect.center,
            //     windowRect.position,
            //     Color.magenta,
            //     null,
            //     3f
            // );
        }
        void Draw(int window)
        {
            GUILayout.Space(windowStyle.padding.top);
            DrawTab();
            if (!isInit)
            {
                return;
            }
            switch (_selectedTab)
            {
                case 0:
                    DrawHeader();
                    DrawScrollViewHierarchy();
                    break;
                case 1:
                    DrawScrollViewModify();
                    break;
                case 2:
                    DrawEvent();
                    break;
            }
            GUI.DragWindow();
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
            else if (target != null)
            {
                header.text = target.gameObject.name;
                header.image = EditorGUIUtility.FindTexture("Prefab Icon");
            }
            else
            {
                header.text = "Nothing is selected";
            }
            using (var horizon = new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(header, new GUIStyle("dockareaStandalone")))
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
        [SerializeField] TreeViewState m_HierachyTreeViewState;
        HierarchyTreeView hierarchyTreeView;

        [SerializeField] TreeViewState m_ComponentTreeViewState;
        ComponentTreeView componentTreeView;
        Vector2 scrollPositionHierarchy;
        void DrawScrollViewHierarchy()
        {
            using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPositionHierarchy))
            {
                scrollPositionHierarchy = scrollViewScope.scrollPosition;

                if (currentSelectGameObject)
                {
                    if (componentTreeView != null) componentTreeView.OnGUI(new Rect(0, 0, windowRect.width, windowRect.height));
                }
                else
                {
                    //if (hierarchyDrawer != null) hierarchyDrawer.Draw();
                    if (hierarchyTreeView != null) hierarchyTreeView.OnGUI(new Rect(0, 0, windowRect.width, windowRect.height));
                }
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

                if (hierarchyTreeView != null) reorderableListViewModify.DoLayoutList();

            }
        }
        //對應 方法名稱與 pop index 的字典
        //第 n 個腳本的參照
        // List<string[]> methodListOfScriptObject = new List<string[]>();
        // List<string> scriptObjectName = new List<string>();
        Dictionary<string, string[]> classMethodInfo = new Dictionary<string, string[]>();
        BindingFlags BindFlagsForScript = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        void RefreshMethodDatabase()
        {
            classMethodInfo.Clear();
            classMethodInfo.Add("Nothing Select", null);
            List<string> VerifiedMethod = new List<string>();
            for (int i = 0; i < saveData.globalSetting.EventHandleBehaviour.Count; i++)
            {

                var type = Utility.GetType(saveData.globalSetting.EventHandleBehaviour[i].name);
                if (saveData.globalSetting.EventHandleBehaviour[i] == null) return;
                MethodInfo[] methodInfos = type.GetMethods(BindFlagsForScript);
                VerifiedMethod.Clear();
                VerifiedMethod.Add("Nothing Select");
                foreach (var item in methodInfos)
                {
                    var para = item.GetParameters();
                    if (para.Where(m => m.ParameterType.IsAssignableFrom(typeof(UnityEngine.EventSystems.UIBehaviour))).Count() == 0)
                    {
                        continue;
                    }
                    VerifiedMethod.Add(item.Name);
                }
                classMethodInfo.Add(type.ToString(), VerifiedMethod.ToArray());
            }
        }


        Vector2 scrollPositionEvent;
        void DrawEvent()
        {
            if (classMethodInfo.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox("Only the method which has UnityEngine.EventSystems.UIBehaviour parameters will be shown", MessageType.Info);

            using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPositionEvent))
            {
                scrollPositionEvent = scrollViewScope.scrollPosition;

                foreach (var item in viewPageItem.eventDatas.ToArray())
                {
                    using (var horizon = new EditorGUILayout.HorizontalScope("box"))
                    {
                        using (var vertical = new EditorGUILayout.VerticalScope())
                        {
                            using (var horizon2 = new EditorGUILayout.HorizontalScope())
                            {
                                Transform targetObject;

                                if (!string.IsNullOrEmpty(item.targetTransformPath))
                                {
                                    targetObject = viewPageItem.viewElement.transform.Find(item.targetTransformPath);
                                }
                                else
                                {
                                    targetObject = viewPageItem.viewElement.transform;
                                }

                                UnityEngine.Object targetComponent = targetObject.GetComponent(item.targetComponentType);
                                GUIContent l = new GUIContent(target.name + (string.IsNullOrEmpty(item.targetTransformPath) ? "" : ("/" + item.targetTransformPath)), EditorGUIUtility.FindTexture("Prefab Icon"));
                                GUILayout.Label(l, GUILayout.Height(20));
                                GUILayout.Label(EditorGUIUtility.FindTexture("Animation.Play"), GUILayout.Height(20));
                                var _cachedContent = new GUIContent(EditorGUIUtility.ObjectContent(targetComponent, targetComponent.GetType()));
                                GUILayout.Label(_cachedContent, GUILayout.Height(20));
                            }

                            int currentSelectClass = string.IsNullOrEmpty(item.scriptName) ? 0 : classMethodInfo.Values.ToList().IndexOf(classMethodInfo[item.scriptName]);

                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                currentSelectClass = EditorGUILayout.Popup("Event Script", currentSelectClass, classMethodInfo.Select(m => m.Key).ToArray());
                                if (check.changed)
                                {
                                    if (currentSelectClass != 0)
                                    {
                                        var c = classMethodInfo.ElementAt(currentSelectClass);
                                        item.scriptName = c.Key;
                                        item.methodName = "";
                                    }
                                    else
                                    {
                                        item.scriptName = "";
                                        item.methodName = "";
                                    }
                                }
                            }
                            if (currentSelectClass != 0)
                            {
                                using (var check = new EditorGUI.ChangeCheckScope())
                                {
                                    var c = classMethodInfo.ElementAt(currentSelectClass).Value;

                                    int currentSelectMethod = string.IsNullOrEmpty(item.methodName) ? 0 : c.ToList().IndexOf(item.methodName);
                                    currentSelectMethod = EditorGUILayout.Popup("Event Method", currentSelectMethod, c);

                                    if (check.changed)
                                    {
                                        if (currentSelectMethod != 0)
                                        {
                                            item.methodName = c[currentSelectMethod];
                                        }
                                        else
                                        {
                                            item.methodName = "";
                                        }
                                    }
                                }
                            }
                        }
                        if (GUILayout.Button(ReorderableList.defaultBehaviours.iconToolbarMinus, removeButtonStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                        {
                            viewPageItem.eventDatas.Remove(item);
                        }
                    }
                }
            }
        }

        private int _selectedTab;
        static string[] tabs = new string[3] { "Hierarchy", "Modified Properties", "Event" };
        private void DrawTab()
        {
            using (var horizon = new GUILayout.HorizontalScope())
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    _selectedTab = GUILayout.Toolbar(_selectedTab, tabs, EditorStyles.toolbarButton);
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

            if (m_HierachyTreeViewState == null)
                m_HierachyTreeViewState = new TreeViewState();


            hierarchyTreeView = new HierarchyTreeView(target.transform, m_HierachyTreeViewState);
            hierarchyTreeView.OnItemClick += (go) =>
            {
                currentSelectGameObject = (GameObject)go;
                CacheComponent();
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

            if (m_ComponentTreeViewState == null)
                m_ComponentTreeViewState = new TreeViewState();

            componentTreeView = new ComponentTreeView(currentSelectGameObject, m_ComponentTreeViewState);

            componentTreeView.OnItemClick += (sp) =>
            {
                Component c;
                try
                {
                    c = (Component)sp.serializedObject.targetObject;
                }
                catch
                {
                    c = ((GameObject)sp.serializedObject.targetObject).transform;
                }

                if (sp.propertyType == SerializedPropertyType.Generic)
                {
                    System.Type parentType = sp.serializedObject.targetObject.GetType();
                    string propertyName = EditorGUICM.ParseUnityEngineProperty(sp.propertyPath);
                    System.Reflection.PropertyInfo fi = parentType.GetProperty(propertyName);
                    if (fi == null)
                    {
                        Debug.LogError("property not found");
                        editor.ShowNotification(new GUIContent("property not found"), toastMessageFadeOutTimt);

                        return;
                    }

                    if (!fi.PropertyType.IsSubclassOf(typeof(UnityEngine.Events.UnityEvent)))
                    {
                        Debug.LogError("Currently Gereric type only support UnityEvent");
                        editor.ShowNotification(new GUIContent("Currently Gereric type only support UnityEvent"), toastMessageFadeOutTimt);

                        return;
                    }
                    var eventData = new ViewElementEventData();
                    eventData.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                    eventData.targetPropertyName = sp.name;
                    eventData.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
                    eventData.targetPropertyType = sp.propertyType.ToString();
                    eventData.targetPropertyPath = propertyName;

                    if (viewPageItem.eventDatas == null)
                    {
                        viewPageItem.eventDatas = new List<ViewElementEventData>();
                    }

                    var current = viewPageItem.eventDatas
                        .SingleOrDefault(x =>
                            x.targetTransformPath == eventData.targetTransformPath &&
                            x.targetComponentType == eventData.targetComponentType &&
                            x.targetPropertyName == eventData.targetPropertyName
                        );

                    if (current != null)
                    {
                        Debug.LogError("You Have 1 event doesn't setup yet");
                        editor.ShowNotification(new GUIContent("You Have 1 event doesn't setup yet"), toastMessageFadeOutTimt);
                        return;
                    }
                    // else
                    // {

                    // }

                    viewPageItem.eventDatas.Add(eventData);
                }
                else
                {
                    var overrideData = new ViewElementPropertyOverrideData();
                    overrideData.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                    overrideData.targetPropertyName = sp.name;
                    overrideData.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
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
                        editor.ShowNotification(new GUIContent("This property is already in override list."), toastMessageFadeOutTimt);
                    }
                    else
                    {
                        viewPageItem.overrideDatas.Add(overrideData);
                        editor.ShowNotification(new GUIContent("Property override add success"), toastMessageFadeOutTimt);
                    }
                }
            };
        }
        PropertiesDrawer propertiesDrawer;

        // private void CacheProperties()
        // {
        //     propertiesDrawer = new PropertiesDrawer(currentSelectSerializedObject);
        //     propertiesDrawer.OnItemClick += (so, sp) =>
        //     {

        //         var overrideData = new ViewElementPropertyOverrideData();

        //         // Debug.Log("Target Object : " + so.targetObject.name);
        //         // Debug.Log("Type : " + so.targetObject.GetType().ToString());

        //         // Debug.Log("SerializedPropertyType : " + sp.propertyType);
        //         // Debug.Log("Property Type : " + sp.type);
        //         // Debug.Log("Property Name : " + sp.name);
        //         // Debug.Log("Property DisplayName : " + sp.displayName);
        //         // Debug.Log("GameObject Name : " + lastSelectGameObject.name);
        //         // Debug.Log("Transform Path : " + AnimationUtility.CalculateTransformPath(lastSelectGameObject.transform, target.transform));
        //         // Debug.Log("Name Of : " + nameof(sp));

        //         overrideData.targetTransformPath = AnimationUtility.CalculateTransformPath(lastSelectGameObject.transform, target.transform);
        //         overrideData.targetPropertyName = sp.name;
        //         overrideData.targetComponentType = so.targetObject.GetType().ToString();
        //         overrideData.targetPropertyType = sp.propertyType.ToString();
        //         overrideData.targetPropertyPath = EditorGUICM.ParseUnityEngineProperty(sp.propertyPath);
        //         overrideData.Value = EditorGUICM.GetValue(sp);
        //         if (viewPageItem.overrideDatas == null)
        //         {
        //             viewPageItem.overrideDatas = new List<ViewElementPropertyOverrideData>();
        //         }

        //         var current = viewPageItem.overrideDatas
        //             .SingleOrDefault(x =>
        //                 x.targetTransformPath == overrideData.targetTransformPath &&
        //                 x.targetComponentType == overrideData.targetComponentType &&
        //                 x.targetPropertyName == overrideData.targetPropertyName
        //             );

        //         if (current != null)
        //         {
        //             current = overrideData;
        //         }
        //         else
        //         {
        //             viewPageItem.overrideDatas.Add(overrideData);
        //         }
        //     };
        // }

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
}