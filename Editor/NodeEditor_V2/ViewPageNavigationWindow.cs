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
        List<ViewPageItem> viewPageItems;
        public void SetViewPageItem(IEnumerable<ViewPageItem> viewPageItems)
        {
            this.viewPageItems = viewPageItems?.ToList();
            if (viewPageItems == null) return;
            SetupItem();
        }
        [SerializeField] Button button;
        List<Selectable> selectables = new List<Selectable>();
        void SetupItem()
        {
            selectables.Clear();
            viewPageItems.Select(m => m.viewElement);

            var nav = button.navigation;
            nav.mode = Navigation.Mode.Automatic;
        }

        private static bool s_ShowNavigation = false;
        private static string s_ShowNavigationKey = "SelectableEditor.ShowNavigation";
        private void DrawTab()
        {
            using (var horizon = new GUILayout.HorizontalScope())
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    s_ShowNavigation = GUILayout.Toggle(s_ShowNavigation, "Visualized", EditorStyles.toolbarButton);
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
                if (GUILayout.Button("Preview", EditorStyles.toolbarButton))
                {


                }
            }
        }
        public override void Draw(int id)
        {
            using (var vertical = new GUILayout.VerticalScope())
            {
                DrawTab();
                if (GUILayout.Button(new GUIContent("Close")))
                {
                    show = false;
                    SetViewPageItem(null);
                }
            }
            base.Draw(id);
        }

        int baseWindowWidth = 300;
        public override Vector2 GetWindowSize
        {
            get
            {
                return new Vector2(baseWindowWidth + 300, 300);
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