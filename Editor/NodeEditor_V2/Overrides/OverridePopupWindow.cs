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
    public class OverridePopupWindow : ViewSystemNodeWindow
    {
        static ViewSystemSaveData saveData => ViewSystemNodeEditor.saveData;
        GameObject target;
        public ViewPageItem viewPageItem;
        GUIStyle removeButtonStyle;
#if UNITY_2019_1_OR_NEWER
        static float toastMessageFadeOutTimt = 1.5f;
#endif
        bool isInit => target != null;
        GUIStyle windowStyle;
        public override GUIStyle GetWindowStyle()
        {
            return windowStyle;
        }
        ViewSystemNodeInspector sideBar;
        public OverridePopupWindow(string name, ViewSystemNodeEditor editor, ViewSystemNodeInspector sideBar)
        : base(name, editor)
        {
            this.sideBar = sideBar;
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
            windowStyle = new GUIStyle(Drawer.windowStyle);
            RectOffset padding = windowStyle.padding;
            padding.left = 0;
            padding.right = 1;
            padding.bottom = 0;

            resizeable = true;
        }
        public void SetViewPageItem(ViewPageItem viewPageItem)
        {
            this.viewPageItem = viewPageItem;
            currentSelectGameObject = null;
            if (viewPageItem == null) return;
            target = viewPageItem.viewElement.gameObject;
            CacheHierarchy();
            RefreshMethodDatabase();
            RebuildModifyReorderableList();
        }

        Rect itemRect;
        public void Show(Rect itemRect)
        {
            this.itemRect = itemRect;
            // rect.x =;
            // rect.y = 200;
            rect = new Rect(itemRect.x + itemRect.width + 50, 200, rect.width, rect.height);
            show = true;
        }

        public override void OnGUI()
        {
            base.OnGUI();
            if (show == false) return;
            EditorGUIUtility.labelWidth = 0;
            var targetPos = new Vector2(itemRect.center.x, itemRect.center.y - sideBar.scrollerPos.y);
            Handles.DrawBezier(
                targetPos,
                rect.position,
                targetPos,
                rect.position,
                Color.black,
                null,
                5f
            );
        }
        public override void Draw(int window)
        {
            DrawTab();
            if (!isInit)
            {
                return;
            }
            using (var vertical = new GUILayout.VerticalScope())
            {
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
                if (GUILayout.Button(new GUIContent("Close")))
                {
                    show = false;
                    SetViewPageItem(null);
                }
            }

            base.Draw(window);
        }
        GUIContent header = new GUIContent();
        private void DrawHeader()
        {
            if (currentSelectGameObject)
            {
                header.image = Drawer.prefabIcon;
                header.text = currentSelectGameObject.gameObject.name;
            }
            else if (currentSelectSerializedObject != null)
            {
                header = new GUIContent(EditorGUIUtility.ObjectContent(currentSelectSerializedObject.targetObject, currentSelectSerializedObject.targetObject.GetType()));
            }
            else if (target != null)
            {
                header.text = target.gameObject.name;
                header.image = Drawer.prefabIcon;
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
                    }
                    if (currentSelectSerializedObject != null)
                    {
                        currentSelectGameObject = lastSelectGameObject;
                        currentSelectSerializedObject = null;
                    }
                }
                var r = GUILayoutUtility.GetLastRect();
                r.width = 20;
                GUI.Label(r, new GUIContent(EditorGUIUtility.FindTexture("tab_prev")));
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
                    if (componentTreeView != null) componentTreeView.OnGUI(new Rect(0, 0, rect.width, rect.height - 80));
                }
                else
                {
                    //if (hierarchyDrawer != null) hierarchyDrawer.Draw();
                    if (hierarchyTreeView != null) hierarchyTreeView.OnGUI(new Rect(0, 0, rect.width, rect.height - 80));
                }
            }
        }
        Vector2 scrollPositionModified;
        ReorderableList reorderableListViewModify;
        void DrawScrollViewModify()
        {
            using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPositionModified))
            {
                scrollPositionModified = scrollViewScope.scrollPosition;
                reorderableListViewModify.DoLayoutList();
            }
        }

        Dictionary<int, bool> lockerDict = new Dictionary<int, bool>();
        class ViewElementPropertyOverrideDataWrapper
        {
            public ViewElementPropertyOverrideDataWrapper(ViewElementPropertyOverrideData viewElementPropertyOverrideData)
            {
                lineHeight = EditorGUIUtility.singleLineHeight * 2.5f;
                this.viewElementPropertyOverrideData = viewElementPropertyOverrideData;
            }
            public ViewElementPropertyOverrideData viewElementPropertyOverrideData;
            public float lineHeight;
        }
        void RebuildModifyReorderableList()
        {
            lockerDict.Clear();
            var wrapper = viewPageItem.overrideDatas.Select(m => new ViewElementPropertyOverrideDataWrapper(m)).ToList();
            reorderableListViewModify = new ReorderableList(wrapper, typeof(List<ViewElementPropertyOverrideDataWrapper>), true, false, false, true);
            reorderableListViewModify.displayRemove = false;
            reorderableListViewModify.elementHeight = EditorGUIUtility.singleLineHeight * 2.5f;
            reorderableListViewModify.drawElementBackgroundCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                Rect oddRect = rect;
                oddRect.x -= 20;
                oddRect.width += 100;

                if (isFocused)
                {
                    ReorderableList.defaultBehaviours.DrawElementBackground(rect, index, isActive, isFocused, true);
                    return;
                }

                if (index % 2 == 0) GUI.Box(oddRect, GUIContent.none, Drawer.oddStyle);
            };

            reorderableListViewModify.elementHeightCallback += (index) =>
            {
                return wrapper[index].lineHeight;
            };
            reorderableListViewModify.drawElementCallback += (rect, index, isActive, isFocused) =>
            {
                var item = wrapper[index];
                rect.width -= 20;
                var ori_Rect = rect;
                rect.y += EditorGUIUtility.singleLineHeight * 0.25f;
                rect.height = EditorGUIUtility.singleLineHeight;

                var removeBtnRect = ori_Rect;
                removeBtnRect.x += removeBtnRect.width + 3;
                removeBtnRect.width = 20;
                if (GUI.Button(removeBtnRect, EditorGUIUtility.FindTexture("d_Toolbar Minus"), removeButtonStyle))
                {
                    viewPageItem.overrideDatas.RemoveAll(m => m == item.viewElementPropertyOverrideData);
                    RebuildModifyReorderableList();
                }
                var targetObject = viewPageItem.viewElement.transform.Find(item.viewElementPropertyOverrideData.targetTransformPath);

                if (targetObject == null)
                {
                    if (!lockerDict.ContainsKey(index)) lockerDict.Add(index, true);

                    float lineWidth = rect.width;
                    GUI.Label(rect, new GUIContent($"Target GameObject is Missing : [{viewPageItem.viewElement.name }/{item.viewElementPropertyOverrideData.targetTransformPath }", Drawer.miniErrorIcon));
                    rect.y += EditorGUIUtility.singleLineHeight;
                    rect.width = 16;
                    lockerDict[index] = EditorGUI.Toggle(rect, lockerDict[index], new GUIStyle("IN LockButton"));
                    rect.x += 16;
                    rect.width = lineWidth - 16;
                    using (var disable = new EditorGUI.DisabledGroupScope(lockerDict[index]))
                    {
                        item.viewElementPropertyOverrideData.targetTransformPath = EditorGUI.TextField(rect, item.viewElementPropertyOverrideData.targetTransformPath);
                    }

                    return;
                }

                UnityEngine.Object targetComponent = null;
                if (item.viewElementPropertyOverrideData.targetComponentType.Contains("GameObject"))
                {
                    targetComponent = targetObject.gameObject;
                }
                else
                {
                    targetComponent = ViewSystemUtilitys.GetComponent(targetObject, item.viewElementPropertyOverrideData.targetComponentType);
                }
                // if (item.viewElementPropertyOverrideData.targetComponentType.Contains("RectTransform"))
                // {
                //     targetComponent = targetObject.GetComponent<RectTransform>();
                // }
                // else if (item.viewElementPropertyOverrideData.targetComponentType.Contains("Transform"))
                // {
                //     targetComponent = targetObject.transform;
                // }

                if (targetComponent == null)
                {
                    rect.x += 10;
                    rect.width -= 10;
                    GUI.Label(rect, new GUIContent($"ComponentType : [{item.viewElementPropertyOverrideData.targetComponentType}] is missing!", Drawer.miniErrorIcon));
                    rect.y += rect.height;
                    GUI.Label(rect, new GUIContent($"Use Toolbar>Verifiers>Verify Override to fix the problem."));
                    rect.y += rect.height;
                    if (GUI.Button(rect, new GUIContent("Remove item!")))
                    {
                        viewPageItem.overrideDatas.RemoveAll(m => m == item.viewElementPropertyOverrideData);
                        RebuildModifyReorderableList();
                    }
                    item.lineHeight = EditorGUIUtility.singleLineHeight * 3 + 5;
                    return;
                }
                var _cachedContent = new GUIContent(EditorGUIUtility.ObjectContent(targetComponent, targetComponent.GetType()));
                var so = new SerializedObject(targetComponent);
                var sp = so.FindProperty(item.viewElementPropertyOverrideData.targetPropertyName);

                rect.width = 20;
                GUI.Label(rect, Drawer.prefabIcon);
                rect.x += rect.width;

                GUIContent l = new GUIContent(target.name + (string.IsNullOrEmpty(item.viewElementPropertyOverrideData.targetTransformPath) ? "" : ("/" + item.viewElementPropertyOverrideData.targetTransformPath)));
                rect.width = GUI.skin.label.CalcSize(l).x;
                GUI.Label(rect, l);
                rect.x += rect.width;

                rect.width = 20;
                GUI.Label(rect, Drawer.arrowIcon);
                rect.x += rect.width;

                rect.width = GUI.skin.label.CalcSize(_cachedContent).x; ;
                GUI.Label(rect, _cachedContent);

                rect.y += EditorGUIUtility.singleLineHeight;
                rect.height = EditorGUIUtility.singleLineHeight;
                rect.width = ori_Rect.width;
                rect.x = ori_Rect.x;

                //EditorGUI.PropertyField(rect, sp);
                if (VS_EditorUtility.EditorableField(rect, sp, item.viewElementPropertyOverrideData.Value, out float lh))
                {

                }
                item.lineHeight = lh;
                //var result = EditorGUICM.GetPropertyType(sp);

            };
        }

        //對應 方法名稱與 pop index 的字典
        //第 n 個腳本的參照
        // List<string[]> methodListOfScriptObject = new List<string[]>();
        // List<string> scriptObjectName = new List<string>();
        Dictionary<string, CMEditorLayout.GroupedPopupData[]> classMethodInfo = new Dictionary<string, CMEditorLayout.GroupedPopupData[]>();
        BindingFlags BindFlagsForScript = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        void RefreshMethodDatabase()
        {
            classMethodInfo.Clear();
            classMethodInfo.Add("Nothing Select", null);
            List<CMEditorLayout.GroupedPopupData> VerifiedMethod = new List<CMEditorLayout.GroupedPopupData>();
            for (int i = 0; i < saveData.globalSetting.EventHandleBehaviour.Count; i++)
            {
                var type = Utility.GetType(saveData.globalSetting.EventHandleBehaviour[i].name);
                if (saveData.globalSetting.EventHandleBehaviour[i] == null) return;
                MethodInfo[] methodInfos = type.GetMethods(BindFlagsForScript);
                VerifiedMethod.Clear();
                foreach (var item in methodInfos)
                {
                    var para = item.GetParameters();
                    if (para.Where(m => m.ParameterType.IsAssignableFrom(typeof(UnityEngine.EventSystems.UIBehaviour))).Count() == 0)
                    {
                        continue;
                    }

                    var eventMethodInfo = new CMEditorLayout.GroupedPopupData { name = item.Name, group = "" };
                    var arrts = System.Attribute.GetCustomAttributes(item);
                    foreach (System.Attribute attr in arrts)
                    {
                        if (attr is ViewEventGroup)
                        {
                            ViewEventGroup a = (ViewEventGroup)attr;
                            eventMethodInfo.group = a.GetGroupName();
                            break;
                        }
                    }
                    VerifiedMethod.Add(eventMethodInfo);
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

                                if (targetObject == null)
                                {
                                    using (var vertical2 = new EditorGUILayout.VerticalScope())
                                    {
                                        GUILayout.Label(new GUIContent($"Target GameObject is Missing : [{viewPageItem.viewElement.name}/{item.targetTransformPath}]", Drawer.miniErrorIcon));
                                        item.targetTransformPath = EditorGUILayout.TextField(item.targetTransformPath);
                                    }
                                    return;
                                }

                                UnityEngine.Object targetComponent = targetObject.GetComponent(item.targetComponentType);

                                if (targetComponent == null)
                                {
                                    using (var vertical2 = new EditorGUILayout.VerticalScope())
                                    {
                                        GUILayout.Label(new GUIContent($"ComponentType : [{item.targetComponentType}] is missing!", Drawer.miniErrorIcon), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                                        GUILayout.Label(new GUIContent($"Use Toolvar>Verifiers>Verify Override to fix the problem."));
                                        if (GUILayout.Button(new GUIContent("Remove item!")))
                                        {
                                            viewPageItem.eventDatas.RemoveAll(m => m == item);
                                        }
                                    }
                                    return;
                                }

                                GUIContent l = new GUIContent(target.name + (string.IsNullOrEmpty(item.targetTransformPath) ? "" : ("/" + item.targetTransformPath)), EditorGUIUtility.FindTexture("Prefab Icon"));
                                GUILayout.Label(l, GUILayout.Height(20));
                                GUILayout.Label(Drawer.arrowIcon, GUILayout.Height(20));
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
                                    using (var horizon2 = new EditorGUILayout.HorizontalScope())
                                    {
                                        var c = classMethodInfo.ElementAt(currentSelectClass).Value;
                                        var current = c.SingleOrDefault(m => m.name == item.methodName);

                                        CMEditorLayout.GroupedPopupField(item.GetHashCode(), new GUIContent("Event Method"), c, current,
                                            (select) =>
                                            {
                                                item.methodName = select.name;
                                            }
                                        );
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
                        if (_selectedTab == 1)
                        {
                            RebuildModifyReorderableList();
                        }
                    }
                }
            }
        }

        GameObject currentSelectGameObject;
        GameObject lastSelectGameObject;
        private void CacheHierarchy()
        {

            if (m_HierachyTreeViewState == null)
                m_HierachyTreeViewState = new TreeViewState();


            hierarchyTreeView = new HierarchyTreeView(target.transform, m_HierachyTreeViewState);
            hierarchyTreeView.ExpandAll();
            hierarchyTreeView.OnItemClick += (go) =>
            {
                lastSelectGameObject = currentSelectGameObject;
                currentSelectGameObject = (GameObject)go;
                CacheComponent();
            };
        }

        SerializedObject currentSelectSerializedObject;

        private void CacheComponent()
        {

            if (m_ComponentTreeViewState == null)
                m_ComponentTreeViewState = new TreeViewState();

            componentTreeView = new ComponentTreeView(
                currentSelectGameObject,
                viewPageItem,
                m_ComponentTreeViewState,
                currentSelectGameObject == target);

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

                    System.Reflection.FieldInfo fi = parentType.GetField(sp.propertyPath);

                    System.Type propertyType = null;
                    if (fi != null)
                    {
                        propertyType = fi.FieldType;
                    }

                    string propertyName = sp.propertyPath;

                    if (parentType.ToString().Contains("UnityEngine."))
                    {
                        if (propertyName == "m_Navigation")
                        {
                            var content = new GUIContent("UnityEngine.UI.Navigation is not supported with ViewSystem OverrideSystem");
#if UNITY_2019_1_OR_NEWER
                            editor.ShowNotification(content, toastMessageFadeOutTimt);
#else
                            editor.ShowNotification(content);
#endif
                            return;
                        }
                        propertyName = ViewSystemUtilitys.ParseUnityEngineProperty(sp.propertyPath);
                    }

                    System.Reflection.PropertyInfo pi = parentType.GetProperty(propertyName);
                    if (pi != null && fi == null)
                    {
                        // ViewSystemLog.LogError("property not found");
                        // editor.ShowNotification(new GUIContent("property not found"), toastMessageFadeOutTimt);
                        // return;
                        propertyType = pi.PropertyType;
                    }

                    if (!propertyType.IsSubclassOf(typeof(UnityEngine.Events.UnityEvent)))
                    {
                        ViewSystemLog.LogError("Currently Gereric type only support UnityEvent");
                        var content = new GUIContent("Currently Gereric type only support UnityEvent");
#if UNITY_2019_1_OR_NEWER
                        editor.ShowNotification(content, toastMessageFadeOutTimt);
#else
                        editor.ShowNotification(content);
#endif
                        return;
                    }
                    var eventData = new ViewElementEventData();
                    eventData.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                    eventData.targetPropertyName = sp.name;
                    eventData.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
                    //eventData.targetPropertyType = sp.propertyType.ToString();
                    //eventData.targetPropertyPath = propertyName;

                    if (viewPageItem.eventDatas == null)
                    {
                        viewPageItem.eventDatas = new List<ViewElementEventData>();
                    }

                    var current = viewPageItem.eventDatas
                        .Where(x =>
                            x.targetTransformPath == eventData.targetTransformPath &&
                            x.targetComponentType == eventData.targetComponentType &&
                            x.targetPropertyName == eventData.targetPropertyName
                        );

                    if (current.Count() > 0)
                    {
                        if (current.Where(m => string.IsNullOrEmpty(m.scriptName) && string.IsNullOrEmpty(m.methodName)).Count() > 0)
                        {
                            ViewSystemLog.LogError("You Have 1 event doesn't setup yet");
                            var errorContent = new GUIContent("You Have 1 event doesn't setup yet");
#if UNITY_2019_1_OR_NEWER
                            editor.ShowNotification(errorContent, toastMessageFadeOutTimt);
#else
                            editor.ShowNotification(errorContent);
#endif
                            return;
                        }
                    }

                    var error = new GUIContent("Event Add Success");
                    viewPageItem.eventDatas.Add(eventData);
#if UNITY_2019_1_OR_NEWER
                    editor.ShowNotification(error, toastMessageFadeOutTimt);
#else
                    editor.ShowNotification(error);
#endif
                }
                else
                {
                    var overrideData = new ViewElementPropertyOverrideData();
                    overrideData.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                    overrideData.targetPropertyName = sp.name;
                    overrideData.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
                    //overrideData.targetPropertyType = sp.propertyType.ToString();
                    //overrideData.targetPropertyPath = VS_EditorUtility.ParseUnityEngineProperty(sp.propertyPath);
                    overrideData.Value.SetValue(sp);
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
#if UNITY_2019_1_OR_NEWER
                        editor.ShowNotification(new GUIContent("This property is already in override list."), toastMessageFadeOutTimt);
#else
                        editor.ShowNotification(new GUIContent("This property is already in override list."));
#endif
                    }
                    else
                    {
                        viewPageItem.overrideDatas.Add(overrideData);
#if UNITY_2019_1_OR_NEWER
                        editor.ShowNotification(new GUIContent("Property override add success"), toastMessageFadeOutTimt);
#else
                        editor.ShowNotification(new GUIContent("Property override add success"));
#endif
                    }
                }
            };
        }

    }


}