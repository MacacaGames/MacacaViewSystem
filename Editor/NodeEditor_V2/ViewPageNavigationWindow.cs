using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Linq;
namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewPageNavigationWindow : ViewSystemNodeWindow
    {
        public ViewPageNavigationWindow(string name, ViewSystemNodeEditor editor)
        : base(name, editor)
        {
            windowStyle = new GUIStyle(Drawer.windowStyle);
            RectOffset padding = windowStyle.padding;
            padding.left = 0;
            padding.right = 1;
            padding.bottom = 0;
            s_ShowNavigation = EditorPrefs.GetBool(s_ShowNavigationKey);
        }
        ViewPage viewPage;
        public void SetViewPage(ViewPage viewPage)
        {
            this.viewPage = viewPage;
            if (viewPage == null) return;
            SetupItem();
        }

        class NavigationSettingWrapper
        {
            public string path;
            public string component;
        }
        List<NavigationSettingWrapper> navigationSettings = new List<NavigationSettingWrapper>();
        void SetupItem()
        {
            navigationSettings.Clear();
            var viewElements = viewPage.viewPageItems.Select(m => m.viewElement);

            foreach (var ve in viewElements)
            {
                var selectables = ve.GetComponentsInChildren<Selectable>();

                foreach (var select in selectables)
                {
                    NavigationSettingWrapper temp = new NavigationSettingWrapper();
                    temp.path = AnimationUtility.CalculateTransformPath(select.transform, ve.transform);
                    temp.component = new SerializedObject(select).targetObject.GetType().ToString();

                    navigationSettings.Add(temp);
                }
            }
            //    overrideData.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
            //    overrideData.targetPropertyName = sp.name;
            //    overrideData.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
        }

        private static bool s_ShowNavigation = false;
        private static bool autoApplyNavigation = false;
        private static string s_ShowNavigationKey = "SelectableEditor.ShowNavigation";
        private void DrawTab()
        {
            using (var horizon = new GUILayout.HorizontalScope())
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    s_ShowNavigation = GUILayout.Toggle(s_ShowNavigation, "Visualized Navigation", EditorStyles.toolbarButton);
                    if (check.changed)
                    {
                        var field = typeof(UnityEditor.UI.SelectableEditor)
                            .GetField("s_ShowNavigation",
                                System.Reflection.BindingFlags.Static |
                                System.Reflection.BindingFlags.NonPublic);
                        field.SetValue(null, s_ShowNavigation);
                        EditorPrefs.SetBool(s_ShowNavigationKey, s_ShowNavigation);
                        Selectable[] selectables;
#if UNITY_2019_OR_NEWER
                        selectables = Selectable.allSelectablesArray;
#else
                        selectables = Selectable.allSelectables.ToArray();
#endif
                        Selection.objects = new UnityEngine.Object[] { selectables.FirstOrDefault() };
                        SceneView.RepaintAll();
                    }
                }

                autoApplyNavigation = GUILayout.Toggle(autoApplyNavigation, "Auto Apply Navigation On Setting Change", EditorStyles.toolbarButton);

                if (GUILayout.Button("Apply Navigation on Preview", EditorStyles.toolbarButton))
                {


                }
            }
        }
        Vector2 scrollPosition;
        GUILayoutOption height = GUILayout.Height(EditorGUIUtility.singleLineHeight);
        public override void Draw(int id)
        {
            using (var vertical = new GUILayout.VerticalScope())
            {
                DrawTab();
                using (var scroll = new GUILayout.ScrollViewScope(scrollPosition))
                {
                    scrollPosition = scroll.scrollPosition;

                    foreach (var vpi in viewPage.viewPageItems)
                    {
                        using (var vertical2 = new GUILayout.VerticalScope("box"))
                        {
                            GUILayout.Label($"ViewPageItem : {vpi.name}");
                            if (vpi.viewElement == null)
                            {
                                GUILayout.Label(new GUIContent("ViewElement is not set up!", Drawer.miniErrorIcon));
                                continue;
                            }
                            var selectables = vpi.viewElement.GetComponentsInChildren<Selectable>();
                            foreach (var select in selectables)
                            {
                                using (var vertical3 = new GUILayout.VerticalScope("box"))
                                {
                                    var path = AnimationUtility.CalculateTransformPath(select.transform, vpi.viewElement.transform);
                                    var component = select.GetType().ToString();

                                    using (var horizon = new GUILayout.HorizontalScope())
                                    {
                                        GUILayout.Label($"{vpi.viewElement.name}{(string.IsNullOrEmpty(path) ? "" : "/")}{path}");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label(new GUIContent(Drawer.arrowIcon));
                                        var editorContent = new GUIContent(EditorGUIUtility.ObjectContent(select, select.GetType()));
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label(editorContent, height);
                                    }

                                    var currentNavSetting = vpi.navigationDatas
                                        .SingleOrDefault(m =>
                                            m.targetComponentType == component &&
                                            m.targetTransformPath == path
                                        );

                                    bool hasSetting = currentNavSetting != null;

                                    using (var horizon = new GUILayout.HorizontalScope())
                                    {
                                        if (GUILayout.Button("Select In Hierarchy"))
                                        {
                                            if (vpi.previewViewElement)
                                            {
                                                Selection.objects = new UnityEngine.Object[] { vpi.previewViewElement.transform.Find(path).gameObject };
                                            }
                                        }
                                        if (GUILayout.Button("Apply Setting"))
                                        {
                                            vpi.previewViewElement.ApplyNavigation(vpi.navigationDatas);
                                        }
                                    }

                                    if (!hasSetting)
                                    {
                                        if (GUILayout.Button("Add Navigation Setting"))
                                        {
                                            var nav = new ViewElementNavigationData();
                                            nav.targetComponentType = component;
                                            nav.targetTransformPath = path;
                                            nav.mode = select.navigation.mode;
                                            vpi.navigationDatas.Add(nav);
                                        }
                                    }
                                    else
                                    {
                                        using (var horizon = new GUILayout.HorizontalScope())
                                        {
                                            using (var check = new EditorGUI.ChangeCheckScope())
                                            {
                                                var result = (UnityEngine.UI.Navigation.Mode)
                                                  EditorGUILayout.EnumPopup(currentNavSetting.mode);
                                                if (result != UnityEngine.UI.Navigation.Mode.Explicit)
                                                {
                                                    currentNavSetting.mode = result;
                                                }
                                                else
                                                {
                                                    ViewSystemLog.LogError("Currently Navigation Setting doesn't support Mode with Explicit");
                                                }

                                                if (check.changed)
                                                {
                                                    //Do something
                                                }
                                            }
                                            if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("d_TreeEditor.Trash")), Drawer.removeButtonStyle, GUILayout.Width(25)))
                                            {
                                                vpi.navigationDatas.Remove(currentNavSetting);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Close")))
            {
                show = false;
                SetViewPage(null);
            }
            base.Draw(id);
        }

        int baseWindowWidth = 300;
        public override Vector2 GetWindowSize
        {
            get
            {
                return new Vector2(baseWindowWidth + 300, 400);
            }
        }
        GUIStyle windowStyle;
        public override GUIStyle GetWindowStyle()
        {
            return windowStyle;
        }

        //         #region Copy From Unity UI Source
        //         private void RegisterStaticOnSceneGUI()
        //         {
        // #if UNITY_2019_OR_NEWER
        //             SceneView.duringSceneGui -= StaticOnSceneGUI;
        //             if (s_Editors.Count > 0)
        //                 SceneView.duringSceneGui += StaticOnSceneGUI;
        // #else
        //              SceneView.onSceneGUIDelegate -= StaticOnSceneGUI;
        //             if (s_Editors.Count > 0)
        //                 SceneView.onSceneGUIDelegate += StaticOnSceneGUI;
        // #endif
        //         }
        //         private static void StaticOnSceneGUI(SceneView view)
        //         {
        //             if (!s_ShowNavigation)
        //                 return;

        //             Selectable[] selectables;
        // #if UNITY_2019_OR_NEWER
        //             selectables = Selectable.allSelectablesArray;
        // #else
        //             selectables = Selectable.allSelectables.ToArray();
        // #endif
        //             for (int i = 0; i < selectables.Length; i++)
        //             {
        //                 Selectable s = selectables[i];
        //                 if (UnityEditor.SceneManagement.StageUtility.IsGameObjectRenderedByCamera(s.gameObject, Camera.current))
        //                     DrawNavigationForSelectable(s);
        //             }
        //         }

        //         private static void DrawNavigationForSelectable(Selectable sel)
        //         {
        //             if (sel == null)
        //                 return;

        //             Transform transform = sel.transform;
        //             bool active = Selection.transforms.Any(e => e == transform);
        //             Handles.color = new Color(1.0f, 0.9f, 0.1f, active ? 1.0f : 0.4f);
        //             DrawNavigationArrow(-Vector2.right, sel, sel.FindSelectableOnLeft());
        //             DrawNavigationArrow(Vector2.right, sel, sel.FindSelectableOnRight());
        //             DrawNavigationArrow(Vector2.up, sel, sel.FindSelectableOnUp());
        //             DrawNavigationArrow(-Vector2.up, sel, sel.FindSelectableOnDown());
        //         }

        //         const float kArrowThickness = 2.5f;
        //         const float kArrowHeadSize = 1.2f;

        //         private static void DrawNavigationArrow(Vector2 direction, Selectable fromObj, Selectable toObj)
        //         {
        //             if (fromObj == null || toObj == null)
        //                 return;
        //             Transform fromTransform = fromObj.transform;
        //             Transform toTransform = toObj.transform;

        //             Vector2 sideDir = new Vector2(direction.y, -direction.x);
        //             Vector3 fromPoint = fromTransform.TransformPoint(GetPointOnRectEdge(fromTransform as RectTransform, direction));
        //             Vector3 toPoint = toTransform.TransformPoint(GetPointOnRectEdge(toTransform as RectTransform, -direction));
        //             float fromSize = HandleUtility.GetHandleSize(fromPoint) * 0.05f;
        //             float toSize = HandleUtility.GetHandleSize(toPoint) * 0.05f;
        //             fromPoint += fromTransform.TransformDirection(sideDir) * fromSize;
        //             toPoint += toTransform.TransformDirection(sideDir) * toSize;
        //             float length = Vector3.Distance(fromPoint, toPoint);
        //             Vector3 fromTangent = fromTransform.rotation * direction * length * 0.3f;
        //             Vector3 toTangent = toTransform.rotation * -direction * length * 0.3f;

        //             Handles.DrawBezier(fromPoint, toPoint, fromPoint + fromTangent, toPoint + toTangent, Handles.color, null, kArrowThickness);
        //             Handles.DrawAAPolyLine(kArrowThickness, toPoint, toPoint + toTransform.rotation * (-direction - sideDir) * toSize * kArrowHeadSize);
        //             Handles.DrawAAPolyLine(kArrowThickness, toPoint, toPoint + toTransform.rotation * (-direction + sideDir) * toSize * kArrowHeadSize);
        //         }
        //         private static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir)
        //         {
        //             if (rect == null)
        //                 return Vector3.zero;
        //             if (dir != Vector2.zero)
        //                 dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
        //             dir = rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
        //             return dir;
        //         }
        //         #endregion
    }
}