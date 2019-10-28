using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using CloudMacaca.ViewSystem;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeEditor : EditorWindow
    {

        static ViewSystemNodeEditor window;
        static IViewSystemDateReader dataReader;
        static ViewSystemNodeInspector inspector;
        static ViewSystemNodeGlobalSettingWindow globalSettingWindow;
        public OverridePopupWindow overridePopupWindow;
        private ViewSystemVerifier viewSystemVerifier;
        public static ViewSystemSaveData saveData;
        bool isInit = false;
        public static bool allowPreviewWhenPlaying = false;
        public Transform ViewControllerRoot;

        [MenuItem("CloudMacaca/ViewSystem/Visual Editor")]
        private static void OpenWindow()
        {
            window = GetWindow<ViewSystemNodeEditor>();
            window.titleContent = new GUIContent("View System Visual Editor");
            window.minSize = new Vector2(600, 400);
            window.RefreshData();
            EditorApplication.playModeStateChanged += playModeStateChanged;
        }

        private static void playModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredPlayMode)
            {
                dataReader.Normalized();
            }
        }

        void RefreshData()
        {
            ClearEditor();
            isInit = dataReader.Init();
            saveData = ((ViewSystemDataReaderV2)dataReader).GetGlobalSetting();
            ViewControllerRoot = ((ViewSystemDataReaderV2)dataReader).GetViewControllerRoot();
            globalSettingWindow = new ViewSystemNodeGlobalSettingWindow(this, (ViewSystemDataReaderV2)dataReader);
            overridePopupWindow = new OverridePopupWindow(this, inspector);
            viewSystemVerifier = new ViewSystemVerifier(this, saveData);
            viewStatesPopup.Add("All");
            viewStatesPopup.Add("Overlay Only");
            viewStatesPopup.AddRange(viewStateList.Select(m => m.viewState.name));
        }
        public void ClearEditor()
        {
            nodeConnectionLineList.Clear();
            viewStateList.Clear();
            viewPageList.Clear();
            viewStatesPopup.Clear();
            inspector.SetCurrentSelectItem(null);
        }
        void OnDestroy()
        {
            dataReader.Normalized();
            EditorApplication.playModeStateChanged -= playModeStateChanged;
        }
        void OnFocus()
        {
            if (dataReader == null) dataReader = new ViewSystemDataReaderV2(this);
            if (console == null) console = new ViewSystemNodeConsole();
            if (inspector == null) inspector = new ViewSystemNodeInspector(this);
        }

        List<ViewPageNode> viewPageList = new List<ViewPageNode>();
        List<ViewStateNode> viewStateList = new List<ViewStateNode>();
        List<ViewSystemNodeLine> nodeConnectionLineList = new List<ViewSystemNodeLine>();
        public ViewSystemNodeConsole console;
        public static float zoomScale = 1.0f;
        Rect zoomArea;
        public static Vector2 viewPortScroll;
        Vector2 zoomScaleMinMax = new Vector2(0.25f, 1);
        protected virtual void DoZoom(float delta, Vector2 center)
        {
            var prevZoom = zoomScale;
            zoomScale += delta;
            zoomScale = Mathf.Clamp(zoomScale, zoomScaleMinMax.x, zoomScaleMinMax.y);
            var deltaSize = position.size / prevZoom - position.size / zoomScale;
            var offset = -Vector2.Scale(deltaSize, center);
            viewPortScroll += offset;
        }
        void OnGUI()
        {

            zoomArea = position;
            zoomArea.height -= menuBarHeight;
            zoomArea.y = menuBarHeight;
            zoomArea.x = 0;

            Rect scriptViewRect = new Rect(0, 0, this.position.width / zoomScale, this.position.height / zoomScale);

            EditorZoomArea.Begin(zoomScale, scriptViewRect);
            DrawGrid();
            foreach (var item in nodeConnectionLineList.ToArray())
            {
                item.Draw();
            }
            foreach (var item in viewPageList.ToArray())
            {
                item.Draw();
            }
            foreach (var item in viewStateList.ToArray())
            {
                item.Draw();
            }
            DrawCurrentConnectionLine(Event.current);
            EditorZoomArea.End();

            GUI.depth = -100;
            DrawMenuBar();

            if (console.show) console.Draw(new Vector2(position.width, position.height));
            if (inspector.show) inspector.Draw();

            BeginWindows();
            if (globalSettingWindow != null)
                if (ViewSystemNodeGlobalSettingWindow.showGlobalSetting) globalSettingWindow.OnGUI();

            if (overridePopupWindow != null) overridePopupWindow.OnGUI();
            EndWindows();

            ProcessEvents(Event.current);
            CheckRepaint();
        }
        public void CheckRepaint()
        {
            //if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout) return;
            if (GUI.changed) Repaint();
        }

        private void ProcessEvents(Event e)
        {
            drag = Vector2.zero;
            switch (e.type)
            {
                case EventType.MouseDrag:
                    if (e.button == 2)
                    {
                        if (zoomArea.Contains(e.mousePosition))
                        {
                            OnDrag(e.delta * 1 / zoomScale);
                        }
                    }
                    break;
                case EventType.ScrollWheel:
                    //OnDrag(e.delta * -1);
                    // float target = zoomScale - e.delta.y * 0.1f;
                    // zoomScale = Mathf.Clamp(target, 0.1f, 1f);
                    // GUI.changed = true;
                    Vector2 zoomCenter;
                    zoomCenter.x = e.mousePosition.x / zoomScale / position.width;
                    zoomCenter.y = e.mousePosition.y / zoomScale / position.height;
                    zoomCenter *= zoomScale;
                    DoZoom(-e.delta.y * 0.01f, zoomCenter);
                    e.Use();
                    break;
                case EventType.MouseDown:
                    if (selectedViewPageNode != null || selectedViewStateNode != null)
                    {
                        ClearConnectionSelection();
                        return;
                    }
                    if (e.button == 1)
                    {
                        if (ViewSystemNodeInspector.isMouseInSideBar() && inspector.show)
                        {
                            return;
                        }
                        GenericMenu genericMenu = new GenericMenu();

                        genericMenu.AddItem(new GUIContent("Add FullPage"), false,
                            () =>
                            {
                                AddViewPageNode(e.mousePosition * 1 / zoomScale - viewPortScroll, false);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Add OverlayPage"), false,
                            () =>
                            {
                                AddViewPageNode(e.mousePosition * 1 / zoomScale - viewPortScroll, true);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Add ViewState"), false,
                            () =>
                            {
                                AddViewStateNode(e.mousePosition * 1 / zoomScale - viewPortScroll);
                            }
                        );
                        genericMenu.ShowAsContext();
                    }
                    break;
            }
        }

        public ViewStateNode AddViewStateNode(Vector2 position, ViewState viewState = null)
        {
            var node = new ViewStateNode(position, CheckCanMakeConnect, viewState);
            node.OnNodeSelect += (m) =>
            {
                inspector.SetCurrentSelectItem(m);
            };
            node.OnNodeDelete += (line) =>
            {
                dataReader.OnViewStateDelete(node);
                viewStateList.Remove(node);
                foreach (var item in line)
                {
                    nodeConnectionLineList.Remove(item);
                }
            };

            if (viewState == null)
            {
                dataReader.OnViewStateAdd(node);
            }
            viewStateList.Add(node);
            return node;
        }

        public ViewPageNode AddViewPageNode(Vector2 position, bool isOverlay, ViewPage viewPage = null)
        {
            var node = new ViewPageNode(position, isOverlay, CheckCanMakeConnect,
                (vsn, vpn) =>
                {
                    var oriLine = FindViewSystemNodeConnectionLine(vsn, vpn);
                    if (vsn != null) vsn.currentLinkedViewPageNode.Remove(vpn);
                    if (oriLine != null) nodeConnectionLineList.Remove(oriLine);
                },
                viewPage
            );
            node.OnPreviewBtnClick += (m) =>
            {
                dataReader.OnViewPagePreview(m);
            };
            node.OnNodeSelect += (m) =>
            {
                inspector.SetCurrentSelectItem(m);
            };
            node.OnNodeDelete += (line) =>
            {
                dataReader.OnViewPageDelete(node);
                viewPageList.Remove(node);
                foreach (var item in line)
                {
                    nodeConnectionLineList.Remove(item);
                }
            };
            if (viewPage == null)
            {
                dataReader.OnViewPageAdd(node);
            }
            viewPageList.Add(node);
            return node;
        }

        void CheckCanMakeConnect(ViewSystemNode currentClickNode)
        {
            switch (currentClickNode.nodeType)
            {
                case ViewSystemNode.NodeType.ViewState:
                    selectedViewStateNode = (ViewStateNode)currentClickNode;
                    //如果當前的 ViewPagePoint 不是 null
                    if (selectedViewPageNode != null)
                    {
                        //檢查是不是已經有跟這個 ViewPageNode 節點連線過了
                        if (!selectedViewStateNode.currentLinkedViewPageNode.Contains(selectedViewPageNode) &&
                            selectedViewPageNode.currentLinkedViewStateNode == null
                            )
                        {
                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        // 如果連線的節點跟原本的不同 刪掉舊的連線 然後建立新了
                        else if (selectedViewPageNode.currentLinkedViewStateNode != null)
                        {
                            //刪掉 ViewStateNode 裡的 ViewPageNode
                            selectedViewPageNode.currentLinkedViewStateNode.currentLinkedViewPageNode.Remove(selectedViewPageNode);

                            //刪掉線
                            console.LogWarringMessage("Break original link, create new link");
                            var oriConnect = FindViewSystemNodeConnectionLine(selectedViewPageNode.currentLinkedViewStateNode, selectedViewPageNode);
                            nodeConnectionLineList.Remove(oriConnect);
                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        else
                        {
                            console.LogErrorMessage("The node has linked before");
                            ClearConnectionSelection();
                        }
                    }
                    break;
                case ViewSystemNode.NodeType.FullPage:
                    selectedViewPageNode = (ViewPageNode)currentClickNode;
                    if (selectedViewStateNode != null)
                    {
                        //檢查是不是已經有跟這個 ViewStateNode 節點連線過了
                        if (selectedViewPageNode.currentLinkedViewStateNode == null)
                        {
                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        // 如果連線的節點跟原本的不同 刪掉舊的連線 然後建立新了
                        else if (selectedViewPageNode.currentLinkedViewStateNode != selectedViewStateNode)
                        {
                            console.LogWarringMessage("Break original link, create new link");
                            //刪掉 ViewStateNode 裡的 ViewPageNode
                            selectedViewPageNode.currentLinkedViewStateNode.currentLinkedViewPageNode.Remove(selectedViewPageNode);

                            //刪掉線
                            var oriConnect = FindViewSystemNodeConnectionLine(selectedViewPageNode.currentLinkedViewStateNode, selectedViewPageNode);
                            nodeConnectionLineList.Remove(oriConnect);
                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        else
                        {
                            console.LogErrorMessage("The node has linked before");
                            ClearConnectionSelection();
                        }
                    }
                    break;
            }
        }

        private void CreateConnection()
        {
            CreateConnection(selectedViewStateNode, selectedViewPageNode);
        }
        public void CreateConnection(ViewStateNode viewStateNode)
        {
            if (string.IsNullOrEmpty(viewStateNode.viewState.name))
            {
                return;
            }
            var vps = viewPageList.Where(m => m.viewPage.viewState == viewStateNode.viewState.name);
            if (vps.Count() == 0)
            {
                return;
            }
            foreach (var item in vps)
            {
                if (item.nodeType == ViewSystemNode.NodeType.Overlay)
                {
                    continue;
                }
                CreateConnection(viewStateNode, item);
            }
        }
        private void CreateConnection(ViewStateNode viewStateNode, ViewPageNode viewPageNode)
        {
            viewStateNode.currentLinkedViewPageNode.Add(viewPageNode);
            viewPageNode.currentLinkedViewStateNode = viewStateNode;
            var line = new ViewSystemNodeLine(
                viewStateNode,
                viewPageNode,
                RemoveConnection);

            viewStateNode.OnNodeConnect(viewPageNode, line);
            viewPageNode.OnNodeConnect(viewStateNode, line);
            // View
            nodeConnectionLineList.Add(line);
            console.LogMessage("Create Link, State:" + viewStateNode.viewState.name + ", Page :" + viewPageNode.viewPage.name);
        }

        ViewSystemNodeLine FindViewSystemNodeConnectionLine(ViewStateNode viewStateNode, ViewPageNode viewPageNode)
        {
            return nodeConnectionLineList.SingleOrDefault(x => x.viewPageNode == viewPageNode && x.viewStateNode == viewStateNode);
        }

        void RemoveConnection(ViewSystemNodeLine connectionLine)
        {
            connectionLine.viewPageNode.currentLinkedViewStateNode = null;
            connectionLine.viewStateNode.currentLinkedViewPageNode.Clear();
            nodeConnectionLineList.Remove(connectionLine);
        }
        private void ClearConnectionSelection()
        {
            selectedViewStateNode = null;
            selectedViewPageNode = null;
        }

        private void OnDrag(Vector2 delta)
        {
            viewPortScroll += delta / zoomScale;
            GUI.changed = true;
        }

        private Vector2 drag;
        protected void DrawGrid()
        {
            float width = this.position.width / zoomScale;
            float height = this.position.height / zoomScale;
            Color c = Color.gray;
            c.a = 0.5f;
            Handles.color = c;


            float gridSize = 32f;

            float x = viewPortScroll.x % gridSize;
            while (x < width)
            {
                Handles.DrawLine(new Vector2(x, 0), new Vector2(x, height));
                x += gridSize;
            }

            float y = (viewPortScroll.y % gridSize);
            while (y < height)
            {
                if (y >= 0)
                {
                    Handles.DrawLine(new Vector2(0, y), new Vector2(width, y));
                }
                y += gridSize;
            }

            Handles.color = Color.white;
        }

        private float menuBarHeight = 20f;
        private Rect menuBar;
        int currentIndex = 0;
        List<string> viewStatesPopup = new List<string>();
        string targetViewState;
        private void DrawMenuBar()
        {
            menuBar = new Rect(0, 0, position.width, menuBarHeight);

            using (var area = new GUILayout.AreaScope(menuBar, "", EditorStyles.toolbar))
            {
                using (var horizon = new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(5);
                    if (GUILayout.Button(new GUIContent("Save"), EditorStyles.toolbarButton, GUILayout.Width(35)))
                    {
                        if (isInit == false)
                        {
                            ShowNotification(new GUIContent("Editor is not Initial."), 2);
                            return;
                        }
                        if (EditorUtility.DisplayDialog("Save", "Save action will also delete all ViewElement in scene. \nDo you really want to continue?", "Yes", "No"))
                        {
                            dataReader.Save(viewPageList, viewStateList);
                        }
                    }
                    GUILayout.Space(5);
                    if (GUILayout.Button(new GUIContent("Reload", Drawer.refreshIcon, "Reload data"), EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        OverridePopupWindow.show = false;
                        RefreshData();
                    }
                    GUILayout.Space(5);
                    inspector.show = GUILayout.Toggle(inspector.show, new GUIContent(Drawer.sideBarIcon, "Show SideBar"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight), GUILayout.Width(25));

                    GUILayout.Space(5);
                    console.show = GUILayout.Toggle(console.show, new GUIContent(Drawer.miniErrorIcon, "Show Console"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight), GUILayout.Width(25));
                    GUILayout.Space(5);
                    ViewSystemNodeGlobalSettingWindow.showGlobalSetting = GUILayout.Toggle(ViewSystemNodeGlobalSettingWindow.showGlobalSetting, new GUIContent("Global Setting", EditorGUIUtility.FindTexture("SceneViewTools")), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight));

                    GUILayout.Space(5);
                    // if (GUILayout.Button(new GUIContent("Verify Overrides"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight)))
                    // {
                    //     viewSystemVerifier.VerifyComponent();
                    // }
                    // if (GUILayout.Button(new GUIContent("Verify Events"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight)))
                    // {
                    //     viewSystemVerifier.VerifyEvents();
                    // }
                    if (GUILayout.Button(new GUIContent("Verifiers"), EditorStyles.toolbarDropDown, GUILayout.Height(menuBarHeight)))
                    {
                        //viewSystemVerifier.VerifyPagesAndStates();
                        GenericMenu genericMenu = new GenericMenu();
                        genericMenu.AddItem(new GUIContent("Verify GameObjects"), false,
                            () =>
                            {
                                viewSystemVerifier.VerifyGameObject(ViewSystemVerifier.VerifyTarget.All);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Verify Overrides"), false,
                            () =>
                            {
                                viewSystemVerifier.VerifyComponent(ViewSystemVerifier.VerifyTarget.Override);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Verify Events"), false,
                            () =>
                            {
                                viewSystemVerifier.VerifyComponent(ViewSystemVerifier.VerifyTarget.Event);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Verify Pages and States"), false,
                            () =>
                            {
                                viewSystemVerifier.VerifyPagesAndStates();
                            }
                        );
                        genericMenu.ShowAsContext();
                    }

                    allowPreviewWhenPlaying = GUILayout.Toggle(allowPreviewWhenPlaying, new GUIContent("Allow Preview when Playing"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(new GUIContent(Drawer.zoomIcon, "Zoom"), GUIStyle.none);
                    zoomScale = EditorGUILayout.Slider(zoomScale, zoomScaleMinMax.x, zoomScaleMinMax.y, GUILayout.Width(120));

                    // GUILayout.Label("ViewState:");
                    // int newIndex = EditorGUILayout.Popup(currentIndex, viewStatesPopup.ToArray(),
                    //     EditorStyles.toolbarPopup, GUILayout.Width(80));
                    // if (newIndex != currentIndex)
                    // {
                    //     currentIndex = newIndex;
                    //     targetViewState = viewStatesPopup[currentIndex];
                    // }

                    if (GUILayout.Button(new GUIContent("Baked to Scritpable", Drawer.bakeScritpIcon, "Bake ViewPage and ViewState to script"), EditorStyles.toolbarButton))
                    {
                        ViewSystemScriptBaker.BakeAllViewPageName(viewPageList.Select(m => m.viewPage).ToList(), viewStateList.Select(m => m.viewState).ToList());
                    }

                    if (GUILayout.Button(new GUIContent("Clear Preview", "Clear all preview item"), EditorStyles.toolbarButton))
                    {
                        ((ViewSystemDataReaderV2)dataReader).ClearAllViewElementInScene();
                    }
                    if (GUILayout.Button(new GUIContent("Normalized", "Normalized all item (Will Delete the Canvas Root Object in Scene)"), EditorStyles.toolbarButton))
                    {
                        OverridePopupWindow.show = false;
                        dataReader.Normalized();
                    }
                }
            }
        }

        private ViewStateNode selectedViewStateNode;
        private ViewPageNode selectedViewPageNode;
        private void DrawCurrentConnectionLine(Event e)
        {
            if (selectedViewStateNode != null && selectedViewPageNode == null)
            {
                Handles.DrawLine(e.mousePosition, selectedViewStateNode.nodeConnectionLinker.rect.center);
                GUI.changed = true;
            }

            if (selectedViewPageNode != null && selectedViewStateNode == null)
            {
                Handles.DrawLine(e.mousePosition, selectedViewPageNode.nodeConnectionLinker.rect.center);
                GUI.changed = true;
            }
        }
    }
}
