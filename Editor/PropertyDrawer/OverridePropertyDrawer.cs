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
    public static class ClipBoard
    {
        public static object value;
        public static string json;
        public static bool HasValue
        {
            get
            {
                return value != null || !string.IsNullOrEmpty(json);
            }
        }
    }
    [CustomPropertyDrawer(typeof(ViewElementOverride))]
    public class OverridePropertyDrawer : PropertyDrawer
    {
        public OverridePropertyDrawer()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabSaving += OnPrefabSaving;
            Undo.undoRedoPerformed += OnUndo;
        }

        private void OnUndo()
        {
            if (isPreviewing)
            {
                DoPreview();
            }
        }

        ~OverridePropertyDrawer()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabSaving -= OnPrefabSaving;
            Undo.undoRedoPerformed -= OnUndo;
        }
        private void OnPrefabSaving(GameObject obj)
        {
            // Debug.Log("Do revert");
            DoRevert();
        }

        ReorderableList reorderableList;
        SerializedProperty propertySource;
        ViewElement viewElement = null;
        ViewElement original = null;
        bool fold = false;
        private Vector2 contextClick;

        private bool instanceUpdate = true;
        bool isPreviewing = false;
        void SetDirty()
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(propertySource.serializedObject.targetObject);
            propertySource.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(viewElement);

        }
        public override void OnGUI(Rect oriRect, SerializedProperty property, GUIContent label)
        {
            GUILayout.Label(property.displayName, EditorStyles.boldLabel);
            using (var vertical = new EditorGUILayout.VerticalScope("box"))
            {
                try
                {
                    viewElement = (property.serializedObject.targetObject as Component).GetComponentInParent<ViewElement>();
                    original = PrefabUtility.GetCorrespondingObjectFromSource(viewElement);
                }
                catch { }
                Event e = Event.current;
                if (e.type == EventType.ValidateCommand)
                {
                    RebuildList(property);
                    // Debug.Log("Rebuid due to paste");
                }

                if (reorderableList == null)
                {
                    RebuildList(property);
                }

                if (viewElement == null || original == null)
                {
                    Color c = GUI.color;
                    GUI.color = Color.red;
                    GUILayout.Label(new GUIContent("No ViewElement Prefab found, or you are under Prefab Mode.", Drawer.miniErrorIcon, "Due to the limitation of PrefabUtility API these feature cannot work under Prefab Mode"));
                    GUI.color = c;
                }
                using (var horizon = new GUILayout.HorizontalScope())
                {
                    using (var disable = new EditorGUI.DisabledGroupScope(viewElement == null))
                    {
                        if (GUILayout.Button("Revert Preview"))
                        {
                            DoRevert();
                        }
                        if (GUILayout.Button("Preview"))
                        {
                            DoPreview();
                        }
                    }
                    using (var disable = new EditorGUI.DisabledGroupScope(!isPreviewing))
                    {
                        instanceUpdate = GUILayout.Toggle(instanceUpdate, "Realtime Update");
                    }
                    using (var disable = new EditorGUI.DisabledGroupScope(viewElement == null || original == null))
                    {
                        if (GUILayout.Button("Pick"))
                        {
                            PickCurrent();
                        }
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
                this.propertySource = property;
                GUIStyle myStyle = new GUIStyle("Foldout");
                myStyle.margin = new RectOffset(10, 0, 0, 0);
                using (var horizon = new EditorGUILayout.HorizontalScope())
                {
                    fold = EditorGUILayout.Foldout(fold, "Override datas", myStyle);
                    EditorGUILayout.Space();

                    if (GUILayout.Button(EditorGUIUtility.FindTexture("d_Refresh")))
                    {
                        RebuildList(property);
                    }

                }

                if (fold)
                {
                    reorderableList.DoLayoutList();
                }
            }
        }

        void RebuildList(SerializedProperty property)
        {
            List<ViewElementPropertyOverrideData> list = ((ViewElementOverride)fieldInfo.GetValue(property.serializedObject.targetObject)).GetValues();
            BuildReorderlist(list, property.displayName);
        }

        void PickCurrent()
        {
            var overrideChecker = ScriptableObject.CreateInstance<ViewElementOverridesImporterWindow>();
            var result = overrideChecker.SetData(viewElement.transform, original.transform,
            (import) =>
            {
                var data = new ViewElementOverride();
                foreach (var item in import)
                {
                    data.Add(item);
                }
                fieldInfo.SetValue(propertySource.serializedObject.targetObject, data);
                Refrersh();
            },
            null);
            if (result) overrideChecker.ShowUtility();
        }

        void BuildReorderlist(List<ViewElementPropertyOverrideData> list, string displayName)
        {
            reorderableList = new ReorderableList(list, typeof(List<ViewElementPropertyOverrideData>));
            reorderableList.drawElementCallback += DrawElement;
            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight * 4;
            reorderableList.drawHeaderCallback += (rect) =>
            {
                // GUI.Label(rect, displayName);
            };
            reorderableList.onAddCallback += (ReorderableList l) =>
            {
                l.list.Add(new ViewElementPropertyOverrideData());
                SetDirty();
            };
            reorderableList.onRemoveCallback += (ReorderableList l) =>
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(l);
                SetDirty();
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
            if (!File.Exists(filePath))
            {
                var result = ScriptableObject.CreateInstance<ViewElementOverrideAsset>();
                result.targetViewElement = original;
                result.viewElementOverride = viewElementOverride;
                AssetDatabase.CreateAsset(result, filePath);
            }
            else
            {
                var currentAsset = AssetDatabase.LoadAssetAtPath<ViewElementOverrideAsset>(filePath);
                currentAsset.viewElementOverride = viewElementOverride;
                currentAsset.targetViewElement = original;
                EditorUtility.SetDirty(currentAsset);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        void LoadAsset()
        {
            string path = EditorUtility.OpenFilePanel("Select override asset", "Assets", extension);
            path = path.Replace(Application.dataPath, "Assets");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            var result = AssetDatabase.LoadAssetAtPath<ViewElementOverrideAsset>(path);
            var data = new ViewElementOverride();

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
            List<ViewElementPropertyOverrideData> list = ((ViewElementOverride)fieldInfo.GetValue(propertySource.serializedObject.targetObject)).GetValues();
            BuildReorderlist(list, propertySource.displayName);
        }
        void DoPreview()
        {
            var list = (IEnumerable<ViewElementPropertyOverrideData>)reorderableList.list;
            if (viewElement != null)
            {
                isPreviewing = true;
                viewElement.ApplyOverrides(list);
                Rebuild(viewElement);
            }
        }

        void DoRevert()
        {
            if (viewElement != null)
            {
                isPreviewing = false;
                viewElement.RevertOverrides();
                Rebuild(viewElement);
            }
        }
        private void CopyItem(int index)
        {
            if (reorderableList.list.Count <= index)
            {
                // Debug.Log("Return due to out ov range");
                return;
            }

            ViewElementPropertyOverrideData data = reorderableList.list[index] as ViewElementPropertyOverrideData;

            if (data == null)
            {
                // Debug.Log("Return due to null");
                return;
            }

            // ClipBoard.value = data;
            ClipBoard.json = JsonUtility.ToJson(data);
        }

        private void PasteItem(int index)
        {
            ViewElementPropertyOverrideData data = JsonUtility.FromJson<ViewElementPropertyOverrideData>(ClipBoard.json);
            if (reorderableList.list.Count <= index)
            {
                // Debug.Log("Return due to out ov range");
                return;
            }
            reorderableList.list[index] = data;
            propertySource.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(viewElement);
            RebuildList(propertySource);
        }


        void DrawElement(Rect oriRect, int index, bool isActive, bool isFocused)
        {
            var e = Event.current;
            if (e.isMouse && e.type == EventType.MouseDown && e.button == 1 && oriRect.Contains(e.mousePosition))
            {
                // Debug.Log(index);
                Event.current.Use();
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Copy %c"), false, () => CopyItem(index));
                if (!ClipBoard.HasValue)
                {
                    menu.AddDisabledItem(new GUIContent("Paste %v"), false);
                }
                else
                {
                    menu.AddItem(new GUIContent("Paste %v"), false, () => PasteItem(index));
                }
                menu.ShowAsContext();
            }

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
                if (!string.IsNullOrEmpty(targetTransformPath))
                {
                    var target = viewElement.transform.Find(targetTransformPath);
                    if (target == null)
                    {
                        GUI.Label(rect, new GUIContent("Target GameObject not found", Drawer.miniInfoIcon, "Target GameObject not found"));
                        return;
                    }
                }

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

                if (VS_EditorUtility.SmartOverrideField(rect, new GUIContent(targetPropertyName), item.Value, propertySource.serializedObject.targetObject, out float lh))
                {
                    SetDirty();
                    if (isPreviewing && instanceUpdate)
                    {
                        DoPreview();
                    }
                }

            }
        }
        void Rebuild(ViewElement viewElement)
        {
            viewElement.gameObject.SetActive(false);
            viewElement.gameObject.SetActive(true);

        }

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
                    }
                    else
                    {
                        data.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                        data.targetPropertyName = sp.name;
                        data.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
                        data.Value.SetValue(sp);
                        editorWindow.Close();
                    }
                    sp.serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(currentSelectGameObject);

                };
            }
        }
    }
}