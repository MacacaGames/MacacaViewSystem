using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Reflection;
using UnityEditorInternal;
using System;
using System.IO;
using MacacaGames.ViewSystem.VisualEditor;

namespace MacacaGames.ViewSystem
{
    [CustomPropertyDrawer(typeof(ViewElemenOverride))]
    public class OverridePropertyDrawer : PropertyDrawer
    {
        public OverridePropertyDrawer()
        {

        }
        ReorderableList reorderableList;
        SerializedProperty propertySource;
        ViewElement viewElement = null;
        ViewElement original = null;
        public override void OnGUI(Rect oriRect, SerializedProperty property, GUIContent label)
        {
            GUILayout.Label(property.displayName, EditorStyles.boldLabel);
            try
            {
                viewElement = (property.serializedObject.targetObject as Component).GetComponentInParent<ViewElement>();
                original = PrefabUtility.GetCorrespondingObjectFromSource(viewElement);
            }
            catch { }
            if (reorderableList == null)
            {
                List<ViewElementPropertyOverrideData> list = ((ViewElemenOverride)fieldInfo.GetValue(property.serializedObject.targetObject));
                BuildReorderlist(list, property.displayName);
            }
            if (viewElement == null || original == null)
            {
                Color c = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label(new GUIContent("No ViewElement Prefab found", Drawer.miniErrorIcon));
                GUI.color = c;
            }
            using (var horizon = new GUILayout.HorizontalScope())
            {
                using (var disable = new EditorGUI.DisabledGroupScope(viewElement == null || original == null))
                {
                    if (GUILayout.Button("Preview"))
                    {
                        DoPreview();
                    }
                    if (GUILayout.Button("Pick"))
                    {
                        PickCurrent();
                    }
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("d_SaveAs@2x"), "Save"), Drawer.removeButtonStyle, GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                    {
                        SaveAsset((List<ViewElementPropertyOverrideData>)reorderableList.list);
                    }
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("d_Profiler.Open@2x"), "Load"), Drawer.removeButtonStyle, GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                    {
                        LoadAsset();
                    }
                }
            }

            this.propertySource = property;
            reorderableList.DoLayoutList();
        }

        void PickCurrent()
        {

            var overrideChecker = ScriptableObject.CreateInstance<ViewElementOverridesImporterWindow>();
            overrideChecker.SetData(viewElement.transform, original.transform,
            (import) =>
            {
                var data = new ViewElemenOverride();
                foreach (var item in import)
                {
                    data.Add(item);
                }
                fieldInfo.SetValue(propertySource.serializedObject.targetObject, data);
                Refrersh();
            },
            null);
            overrideChecker.ShowUtility();
        }

        void BuildReorderlist(List<ViewElementPropertyOverrideData> list, string displayName)
        {
            reorderableList = new ReorderableList(list, typeof(List<ViewElementPropertyOverrideData>));
            reorderableList.drawElementCallback += DrawElement;
            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight * 4;
            reorderableList.drawHeaderCallback += (rect) =>
            {
                GUI.Label(rect, displayName);
            };
            reorderableList.onAddCallback += (ReorderableList l) =>
            {
                l.list.Add(new ViewElementPropertyOverrideData());
            };
            reorderableList.drawElementBackgroundCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
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
        }
        string extension = "asset";
        void SaveAsset(List<ViewElementPropertyOverrideData> viewElementOverride)
        {
            var filePath = EditorUtility.SaveFilePanelInProject(
                "Save current data",
                 $"{viewElement.name}_Override.{extension}",
                extension, "", "Assets");
            if (string.IsNullOrEmpty(filePath)) return;

            var result = ScriptableObject.CreateInstance<ViewElementOverrideAsset>();
            result.targetViewElement = original;
            result.viewElementOverride = viewElementOverride;
            AssetDatabase.CreateAsset(result, filePath);
            AssetImporter.GetAtPath(filePath);
            AssetDatabase.Refresh();
        }

        void LoadAsset()
        {
            string path = EditorUtility.OpenFilePanel("Overwrite with png", "Assets", extension);
            path = path.Replace(Application.dataPath, "Assets");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            var result = AssetDatabase.LoadAssetAtPath<ViewElementOverrideAsset>(path);
            var data = new ViewElemenOverride();

            foreach (var item in result.viewElementOverride)
            {
                data.Add(item);
            }

            fieldInfo.SetValue(propertySource.serializedObject.targetObject, data);
            Refrersh();
        }
        void Refrersh()
        {
            Rebuild(viewElement);
            EditorUtility.SetDirty(propertySource.serializedObject.targetObject);
            List<ViewElementPropertyOverrideData> list = ((ViewElemenOverride)fieldInfo.GetValue(propertySource.serializedObject.targetObject));
            BuildReorderlist(list, propertySource.displayName);
        }
        void DoPreview()
        {
            // Debug.Log(property.propertyPath);
            // Debug.Log(property.serializedObject);
            // Debug.Log(property.serializedObject.targetObject);
            // Debug.Log(property.serializedObject.targetObject.GetType());
            // var t = property.serializedObject.targetObject.GetType().GetField(property.propertyPath.Split('.').FirstOrDefault());
            // var array = (IEnumerable<ViewElementPropertyOverrideData>)t.GetValue(property.serializedObject.targetObject);
            var list = (IEnumerable<ViewElementPropertyOverrideData>)reorderableList.list;
            // Debug.Log(list);
            if (viewElement != null)
            {
                viewElement.ApplyOverrides(list);
                Rebuild(viewElement);
            }
        }

        void DrawElement(Rect oriRect, int index, bool isActive, bool isFocused)
        {
            var item = (ViewElementPropertyOverrideData)reorderableList.list[index];
            var rootObjectName = viewElement != null ? viewElement.name : "{RootObject}";
            var btnRect = oriRect;
            btnRect.width = 26;
            btnRect.height = EditorGUIUtility.singleLineHeight;
            btnRect.x = oriRect.width;
            using (var disable = new EditorGUI.DisabledGroupScope(viewElement == null))
            {
                if (GUI.Button(btnRect, new GUIContent(EditorGUIUtility.FindTexture("d_Search Icon"))))
                {
                    PopupWindow.Show(btnRect, new OverridePopup(item, viewElement));
                }
            }

            var rect = oriRect;
            rect.y += EditorGUIUtility.singleLineHeight * 0.5f;
            rect.height = EditorGUIUtility.singleLineHeight;
            if (viewElement == null)
            {
                GUI.Label(rect, new GUIContent("", "Root Object is not a ViewElement"));
            }
            var targetTransformPath = item.targetTransformPath;
            var targetComponentType = item.targetComponentType;
            var targetPropertyName = item.targetPropertyName;

            if (string.IsNullOrEmpty(targetComponentType) || string.IsNullOrEmpty(targetPropertyName))
            {
                rect.y += EditorGUIUtility.singleLineHeight;
                GUI.Label(rect, new GUIContent("Nothing select", Drawer.miniInfoIcon, "Nothing select"));
            }
            else
            {
                float paddind = 20;
                GUI.Label(rect, new GUIContent((string.IsNullOrEmpty(targetTransformPath) ? rootObjectName : rootObjectName + "/") + targetTransformPath, Drawer.prefabIcon, targetTransformPath));
                rect.y += EditorGUIUtility.singleLineHeight;

                var arrowRect = rect;
                arrowRect.width = paddind;
                GUI.Label(arrowRect, EditorGUIUtility.FindTexture("tab_next@2x"));

                var rectComponent = rect;
                rectComponent.x += paddind;
                rectComponent.width = oriRect.width - paddind;
                var componentContent = new GUIContent(EditorGUIUtility.ObjectContent(null, Utility.GetType(targetComponentType)));
                GUI.Label(rectComponent, new GUIContent($"{targetTransformPath.Split('/').LastOrDefault()} ({targetComponentType.Split('.').LastOrDefault()})", componentContent.image));
                rect.y += EditorGUIUtility.singleLineHeight;
                VS_EditorUtility.SmartOverrideField(rect, new GUIContent(targetPropertyName), item.Value, out float lh);
            }
        }
        void Rebuild(ViewElement viewElement)
        {
            viewElement.gameObject.SetActive(false);
            viewElement.gameObject.SetActive(true);
            // foreach (var rectTransform in viewElement.GetComponents<RectTransform>())
            // {
            //     rectTransform.ForceUpdateRectTransforms();
            // }
            // foreach (var graphic in viewElement.GetComponents<UnityEngine.UI.Graphic>())
            // {
            //     graphic.SetAllDirty();
            //     graphic.SetVerticesDirty();
            //     UnityEngine.UI.CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(graphic);
            // }
            // Canvas.ForceUpdateCanvases();

            // UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            // UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(viewElement.rectTransform);
        }
        // public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        // {
        //     return EditorGUIUtility.singleLineHeight * 3;
        // }

        public class OverridePopup : PopupWindowContent
        {
            ViewElement target;
            ViewElementPropertyOverrideData data;
            public OverridePopup(ViewElementPropertyOverrideData data, ViewElement target)
            {
                this.data = data;
                this.target = target;
                CacheHierarchy();
            }
            TreeViewState m_HierachyTreeViewState;
            HierarchyTreeView hierarchyTreeView;

            TreeViewState m_ComponentTreeViewState;
            ComponentTreeView componentTreeView;


            public override Vector2 GetWindowSize()
            {
                return new Vector2(300, 400);
            }

            public override void OnGUI(Rect rect)
            {
                DrawHeader();
                DrawScrollViewHierarchy(rect);
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
                    header.text = "Nothing is selected";
                    header.image = null;
                }
                else
                {
                    header.text = "Nothing is selected";
                    header.image = null;
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
                    r.y -= 2;
                    if (currentSelectGameObject != null) GUI.Label(r, EditorGUIUtility.FindTexture("d_Profiler.PrevFrame"));
                }
            }
            Vector2 scrollPositionHierarchy;
            void DrawScrollViewHierarchy(Rect rect)
            {
                using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPositionHierarchy))
                {
                    scrollPositionHierarchy = scrollViewScope.scrollPosition;

                    if (currentSelectGameObject)
                    {
                        if (componentTreeView != null) componentTreeView.OnGUI(new Rect(0, 0, rect.width + 10, rect.height - 80));
                    }
                    else
                    {
                        //if (hierarchyDrawer != null) hierarchyDrawer.Draw();
                        if (hierarchyTreeView != null) hierarchyTreeView.OnGUI(new Rect(0, 0, rect.width + 10, rect.height - 80));
                    }
                }
            }
            public override void OnOpen()
            {
            }
            public override void OnClose()
            {
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
            float toastMessageFadeOutTimt = 1;
            private void CacheComponent()
            {
                if (m_ComponentTreeViewState == null)
                    m_ComponentTreeViewState = new TreeViewState();

                componentTreeView = new ComponentTreeView(
                    currentSelectGameObject,
                    target,
                    m_ComponentTreeViewState,
                    currentSelectGameObject == target,
                    (a, b) => { return false; },
                    () => { });

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
                                editorWindow.ShowNotification(content, toastMessageFadeOutTimt);
#else
                            editorWindow.ShowNotification(content);
#endif
                                return;
                            }
                            propertyName = ViewSystemUtilitys.ParseUnityEngineProperty(sp.propertyPath);
                        }

                        System.Reflection.PropertyInfo pi = parentType.GetProperty(propertyName);
                        if (pi != null && fi == null)
                        {
                            propertyType = pi.PropertyType;
                        }

                        //                         if (!propertyType.IsSubclassOf(typeof(UnityEngine.Events.UnityEvent)) &&
                        //                             !propertyType.IsAssignableFrom(typeof(UnityEngine.Events.UnityEvent)))
                        //                         {
                        //                             var content = new GUIContent("Currently only support UnityEvent without parameters");
                        //                             ViewSystemLog.LogError(content.text);
                        // #if UNITY_2019_1_OR_NEWER
                        //                             editorWindow.ShowNotification(content, toastMessageFadeOutTimt);
                        // #else
                        //                                                     editorWindow.ShowNotification(content);
                        // #endif
                        //                             return;
                        //                         }

                        //                         var eventData = new ViewElementEventData();
                        //                         eventData.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                        //                         eventData.targetPropertyName = sp.name;
                        //                         eventData.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
                        //                         //eventData.targetPropertyType = sp.propertyType.ToString();
                        //                         //eventData.targetPropertyPath = propertyName;

                        //                         if (eventDatas == null)
                        //                         {
                        //                             eventDatas = new List<ViewElementEventData>();
                        //                         }

                        //                         var current = eventDatas
                        //                             .Where(x =>
                        //                                 x.targetTransformPath == eventData.targetTransformPath &&
                        //                                 x.targetComponentType == eventData.targetComponentType &&
                        //                                 x.targetPropertyName == eventData.targetPropertyName
                        //                             );

                        //                         if (current.Count() > 0)
                        //                         {
                        //                             if (current.Where(m => string.IsNullOrEmpty(m.scriptName) && string.IsNullOrEmpty(m.methodName)).Count() > 0)
                        //                             {
                        //                                 ViewSystemLog.LogError("You Have 1 event doesn't setup yet");
                        //                                 var errorContent = new GUIContent("You Have 1 event doesn't setup yet");
                        // #if UNITY_2019_1_OR_NEWER
                        //                                 editorWindow.ShowNotification(errorContent, toastMessageFadeOutTimt);
                        // #else
                        //                                                             editorWindow.ShowNotification(errorContent);
                        // #endif
                        //                                 return;
                        //                             }
                        //                         }

                        //                         var error = new GUIContent("Event Add Success");
                        //                         viewPageItem.eventDatas.Add(eventData);
                        // #if UNITY_2019_1_OR_NEWER
                        //                         editorWindow.ShowNotification(error, toastMessageFadeOutTimt);
                        // #else
                        //                                             editorWindow.ShowNotification(error);
                        // #endif
                    }
                    else
                    {
                        // var overrideData = new ViewElementPropertyOverrideData();
                        data.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                        data.targetPropertyName = sp.name;
                        data.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
                        data.Value.SetValue(sp);
                        // data = overrideData;
                        editorWindow.Close();
                        // var t = overrideDataSerializedProperty.FindPropertyRelative("targetTransformPath");
                        // t.stringValue = overrideData.targetTransformPath;

                        // var s = overrideDataSerializedProperty.FindPropertyRelative("targetComponentType");
                        // s.stringValue = sp.serializedObject.targetObject.GetType().ToString();

                        // var valueProperty = overrideDataSerializedProperty.FindPropertyRelative("Value");
                        // valueProperty.FindPropertyRelative("s_Type").enumValueIndex = (int)overrideData.Value.s_Type;
                        // valueProperty.FindPropertyRelative("ObjectReferenceValue").objectReferenceValue = overrideData.Value.ObjectReferenceValue;
                        // valueProperty.FindPropertyRelative("StringValue").stringValue = overrideData.Value.StringValue;
                        //                         var current = overrideDatas
                        //                             .SingleOrDefault(x =>
                        //                                 x.targetTransformPath == overrideData.targetTransformPath &&
                        //                                 x.targetComponentType == overrideData.targetComponentType &&
                        //                                 x.targetPropertyName == overrideData.targetPropertyName
                        //                             );

                        //                         if (current != null)
                        //                         {
                        //                             current = overrideData;
                        // #if UNITY_2019_1_OR_NEWER
                        //                             editorWindow.ShowNotification(new GUIContent("This property is already in override list."), toastMessageFadeOutTimt);
                        // #else
                        //                             editorWindow.ShowNotification(new GUIContent("This property is already in override list."));
                        // #endif
                        //                         }
                        //                         else
                        //                         {
                        //                             overrideDatas.Add(overrideData);
                        // #if UNITY_2019_1_OR_NEWER
                        //                             editorWindow.ShowNotification(new GUIContent("Property override add success"), toastMessageFadeOutTimt);
                        // #else
                        //                                                     editorWindow.ShowNotification(new GUIContent("Property override add success"));
                        // #endif
                        //                         }
                    }
                };
            }
        }
    }
}