using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Linq;
namespace MacacaGames.ViewSystem.VisualEditor
{
    public class ViewPageNavigationWindow : ViewSystemNodeWindow
    {
        public ViewPageNavigationWindow(string name, ViewSystemVisualEditor editor)
        : base(name, editor)
        {
            // windowStyle = new GUIStyle(Drawer.windowStyle);
            // RectOffset padding = windowStyle.padding;
            // padding.left = 0;
            // padding.right = 1;
            // padding.bottom = 0;
            EditorPrefs.SetBool(s_ShowNavigationKey, false);
            s_ShowNavigation = EditorPrefs.GetBool(s_ShowNavigationKey);
        }
        ViewPage viewPage;
        ViewState viewState;
        public void SetViewPage(ViewPage viewPage, ViewState viewState)
        {
            this.viewPage = viewPage;
            this.viewState = viewState;
            if (viewPage == null) return;
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
#if UNITY_2019_1_OR_NEWER
                        selectables = Selectable.allSelectablesArray;
#else
                        selectables = Selectable.allSelectables.ToArray();
#endif
                        Selection.objects = new UnityEngine.Object[] { selectables.FirstOrDefault() };
                        SceneView.RepaintAll();
                    }
                }

                autoApplyNavigation = GUILayout.Toggle(autoApplyNavigation, "Apply On Setting Change", EditorStyles.toolbarButton);

                if (GUILayout.Button("Clear All Setting", EditorStyles.toolbarButton))
                {
                    viewPage.navigationDatasForViewState.Clear();

                    foreach (var item in viewPage.viewPageItems)
                    {
                        item.navigationDatas.Clear();
                    }
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
                    using (var horizon = new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label($"   ViewPage : {viewPage.name}", Drawer.bigLableStyle);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove All Setting"))
                        {
                            viewPage.navigationDatasForViewState.Clear();
                        }
                    }
                    foreach (var vpi in viewPage.viewPageItems)
                    {
                        DrawItem(vpi);
                    }
                    if (viewState != null)
                    {
                        GUILayout.Space(10);
                        GUILayout.Box("", Drawer.darkBackgroundStyle, GUILayout.Height(5), GUILayout.Width(rect.width * 0.97f));
                        GUILayout.Space(10);
                        using (var horizon = new GUILayout.HorizontalScope())
                        {
                            GUILayout.Label($"   ViewState : {viewState.name}", Drawer.bigLableStyle);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Remove All Setting"))
                            {
                                foreach (var item in viewPage.viewPageItems)
                                {
                                    item.navigationDatas.Clear();
                                }
                            }
                        }
                        foreach (var vpi in viewState.viewPageItems)
                        {
                            DrawItem(vpi, true);
                        }
                    }
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Close")))
            {
                show = false;
                SetViewPage(null, null);
            }
            base.Draw(id);
        }

        void DrawItem(ViewPageItem vpi, bool isViewState = false)
        {
            using (var vertical2 = new GUILayout.VerticalScope(Drawer.darkBoxStyle))
            {
                using (var horizon = new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent($"{vpi.displayName}", Drawer.prefabIcon), EditorStyles.whiteLabel, GUILayout.Height(16));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"({vpi.Id})");
                }

                if (vpi.viewElement == null)
                {
                    GUILayout.Label(new GUIContent("ViewElement is not set up!", Drawer.miniErrorIcon));
                    return;
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
                            using (var vertical4 = new GUILayout.VerticalScope())
                            {
                                var p = path.Split('/');
                                GUILayout.Label(new GUIContent($"{vpi.viewElement.name}", Drawer.prefabIcon), GUILayout.Height(16));
                                for (int i = 0; i < p.Length; i++)
                                {
                                    if (string.IsNullOrEmpty(p[i]))
                                    {
                                        continue;
                                    }
                                    using (var horizon2 = new GUILayout.HorizontalScope())
                                    {
                                        GUILayout.Space((i + 1) * 15);
                                        GUILayout.Label(new GUIContent($"{p[i]}", Drawer.prefabIcon), GUILayout.Height(16));
                                    }
                                }
                            }
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(new GUIContent(Drawer.arrowIcon));
                            var editorContent = new GUIContent(EditorGUIUtility.ObjectContent(select, select.GetType()));
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(editorContent, height);
                        }
                        ViewElementNavigationData currentNavSetting = null;
                        ViewElementNavigationDataViewState viewElementNavigationDataViewState = null;
                        if (isViewState == false)
                        {
                            currentNavSetting = vpi.navigationDatas
                                .SingleOrDefault(m =>
                                    m.targetComponentType == component &&
                                    m.targetTransformPath == path
                                );
                        }
                        else
                        {
                            viewElementNavigationDataViewState = viewPage.navigationDatasForViewState.SingleOrDefault(m => m.viewPageItemId == vpi.Id);
                            if (viewElementNavigationDataViewState != null)
                            {
                                currentNavSetting = viewElementNavigationDataViewState.navigationDatas
                                    .SingleOrDefault(m =>
                                        m.targetComponentType == component &&
                                        m.targetTransformPath == path
                                    );
                            }
                        }

                        bool hasSetting = currentNavSetting != null;

                        using (var horizon = new GUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Select In Hierarchy", EditorStyles.miniButton))
                            {
                                if (vpi.previewViewElement)
                                {
                                    Selection.objects = new UnityEngine.Object[] { vpi.previewViewElement.transform.Find(path).gameObject };
                                }
                            }
                            using (var disable = new EditorGUI.DisabledGroupScope(!hasSetting))
                            {
                                if (GUILayout.Button("Apply Setting", EditorStyles.miniButton))
                                {
                                    ApplySetting();
                                }
                            }
                        }

                        if (!hasSetting)
                        {
                            if (GUILayout.Button("Add Navigation Setting"))
                            {
                                if (isViewState == false)
                                {
                                    var nav = new ViewElementNavigationData();
                                    nav.targetComponentType = component;
                                    nav.targetTransformPath = path;
                                    nav.mode = select.navigation.mode;
                                    vpi.navigationDatas.Add(nav);
                                }
                                else
                                {
                                    if (viewElementNavigationDataViewState == null)
                                    {
                                        viewElementNavigationDataViewState = new ViewElementNavigationDataViewState();
                                        viewElementNavigationDataViewState.viewPageItemId = vpi.Id;
                                    }
                                    var nav = new ViewElementNavigationData();
                                    nav.targetComponentType = component;
                                    nav.targetTransformPath = path;
                                    nav.mode = select.navigation.mode;
                                    viewElementNavigationDataViewState.navigationDatas.Add(nav);
                                    viewPage.navigationDatasForViewState.Add(viewElementNavigationDataViewState);
                                }
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
                                        if (autoApplyNavigation) ApplySetting();
                                    }
                                }
                                if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("d_TreeEditor.Trash")), Drawer.removeButtonStyle, GUILayout.Width(25)))
                                {
                                    if (isViewState == false)
                                    {
                                        vpi.navigationDatas.Remove(currentNavSetting);
                                    }
                                    else
                                    {
                                        viewElementNavigationDataViewState.navigationDatas.Remove(currentNavSetting);
                                    }
                                }
                            }
                        }
                        void ApplySetting()
                        {
                            if (isViewState == false)
                            {
                                vpi.previewViewElement.ApplyNavigation(vpi.navigationDatas);
                            }
                            else
                            {
                                vpi.previewViewElement.ApplyNavigation(viewElementNavigationDataViewState.navigationDatas);
                            }
                            SceneView.RepaintAll();
                        }
                    }
                }
            }
        }

        int baseWindowWidth = 300;
        public override Vector2 GetWindowSize
        {
            get
            {
                return new Vector2(baseWindowWidth + 300, 400);
            }
        }
        // GUIStyle windowStyle;
        // public override GUIStyle GetWindowStyle()
        // {
        //     return windowStyle;
        // }
    }
}