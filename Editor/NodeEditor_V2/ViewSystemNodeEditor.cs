using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeEditor : EditorWindow
    {
        static Texture2D miniInfoIcon;
        static Texture2D miniErrorIcon;
        static Texture2D refreshIcon;
        static Texture2D sideBarIcon;
        static Texture2D zoomIcon;
        static Texture2D normalizedIcon;
        static Texture2D bakeScritpIcon;
        enum EditorMode
        {
            V1, V2
        }
        static EditorMode currentEditorMode;

        static ViewSystemNodeEditor window;
        static IViewSystemDateReader dataReader;
        static ViewSystemNodeSideBar sideBar;



        [MenuItem("CloudMacaca/ViewSystem/Visual Editor")]
        private static void OpenWindowAsV1()
        {
            window = GetWindow<ViewSystemNodeEditor>();
            window.titleContent = new GUIContent("View System Visual Editor");
            window.minSize = new Vector2(600, 400);
            currentEditorMode = EditorMode.V1;
            window.RefreshData();
        }
        void RefreshData()
        {
            ClearEditor();

            dataReader = new ViewSystemDataReaderV2(this);
            dataReader.Init();

            viewStatesPopup.Add("All");
            viewStatesPopup.Add("Overlay Only");
            viewStatesPopup.AddRange(viewStateList.Select(m => m.viewState.name));
        }
        void ClearEditor()
        {
            nodeConnectionLineList.Clear();
            viewStateList.Clear();
            viewPageList.Clear();
        }
        void OnDestroy()
        {
            dataReader.Normalized();
        }
        void OnFocus()
        {
            if (console == null) console = new ViewSystemNodeConsole();
            if (sideBar == null) sideBar = new ViewSystemNodeSideBar(this);
            if (normalizedIcon == null) normalizedIcon = EditorGUIUtility.FindTexture("TimelineLoop") as Texture2D;
            if (sideBarIcon == null) sideBarIcon = EditorGUIUtility.FindTexture("pane options") as Texture2D;
            if (bakeScritpIcon == null) bakeScritpIcon = EditorGUIUtility.FindTexture("TextAsset Icon") as Texture2D;

            if (miniInfoIcon == null) miniInfoIcon = EditorGUIUtility.Load("icons/console.infoicon.sml.png") as Texture2D;
            if (miniErrorIcon == null) miniErrorIcon = EditorGUIUtility.Load("icons/console.erroricon.sml.png") as Texture2D;
            if (refreshIcon == null) refreshIcon = EditorGUIUtility.Load((EditorGUIUtility.isProSkin) ? "icons/d_Refresh.png" : "icons/Refresh.png") as Texture2D;
            if (zoomIcon == null) zoomIcon = EditorGUIUtility.FindTexture("ViewToolZoom On") as Texture2D;
        }
        List<ViewPageNode> viewPageList = new List<ViewPageNode>();

        List<ViewStateNode> viewStateList = new List<ViewStateNode>();
        List<ViewSystemNodeLine> nodeConnectionLineList = new List<ViewSystemNodeLine>();
        public ViewSystemNodeConsole console;

        float zoomScale = 1.0f;
        Vector2 vanishingPoint = new Vector2(0, 21);
        Rect zoomArea;
        void OnGUI()
        {

            DrawGrid(20, 0.2f, Color.gray);
            DrawGrid(100, 0.4f, Color.gray);
            zoomArea = position;
            zoomArea.height -= menuBarHeight;
            zoomArea.y = menuBarHeight;
            zoomArea.x = 0;

            EditorZoomArea.Begin(zoomScale, zoomArea);

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
            if (console.showConsole) console.Draw(new Vector2(position.width, position.height));
            if (showSideBar) sideBar.Draw();

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
                    if (e.button == 2 && zoomArea.Contains(e.mousePosition))
                    {
                        OnDrag(e.delta * 1 / zoomScale);
                    }
                    break;
                case EventType.ScrollWheel:
                    //OnDrag(e.delta * -1);
                    float target = zoomScale - e.delta.y * 0.1f;
                    zoomScale = Mathf.Clamp(target, 0.1f, 1f);
                    GUI.changed = true;
                    break;
                case EventType.MouseDown:
                    if (selectedViewPageNode != null || selectedViewStateNode != null)
                    {
                        ClearConnectionSelection();
                        return;
                    }
                    if (e.button == 1)
                    {
                        if (sideBar.isMouseInSideBar() && showSideBar)
                        {
                            return;
                        }
                        GenericMenu genericMenu = new GenericMenu();

                        genericMenu.AddItem(new GUIContent("Add FullPage"), false,
                            () =>
                            {
                                AddViewPageNode(e.mousePosition, false);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Add OverlayPage"), false,
                            () =>
                            {
                                AddViewPageNode(e.mousePosition, true);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Add ViewState"), false,
                            () =>
                            {
                                AddViewStateNode(e.mousePosition);
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
                sideBar.SetCurrentSelectItem(m);
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
                sideBar.SetCurrentSelectItem(m);
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
            console.LogMessage("Create Link State:" + viewStateNode.viewState.name + ", Page :" + viewPageNode.viewPage.name);
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
            drag = delta * 1 / zoomScale;
            foreach (var item in viewPageList)
            {
                item.Drag(delta * zoomScale);
            }
            foreach (var item in viewStateList)
            {
                item.Drag(delta * zoomScale);
            }
            GUI.changed = true;
        }


        int nodeId = 0;

        private Vector2 drag;
        private Vector2 offset;
        private void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor)
        {
            int widthDivs = Mathf.CeilToInt(position.width / gridSpacing);
            int heightDivs = Mathf.CeilToInt(position.height / gridSpacing);

            Handles.BeginGUI();
            Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

            offset += drag * 0.5f;
            Vector3 newOffset = new Vector3(offset.x % gridSpacing, offset.y % gridSpacing, 0);

            for (int i = 0; i < widthDivs; i++)
            {
                Handles.DrawLine(new Vector3(gridSpacing * i, -gridSpacing, 0) + newOffset, new Vector3(gridSpacing * i, position.height, 0f) + newOffset);
            }

            for (int j = 0; j < heightDivs; j++)
            {
                Handles.DrawLine(new Vector3(-gridSpacing, gridSpacing * j, 0) + newOffset, new Vector3(position.width, gridSpacing * j, 0f) + newOffset);
            }

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private float menuBarHeight = 20f;
        private Rect menuBar;
        bool showSideBar = true;
        int currentIndex = 0;
        List<string> viewStatesPopup = new List<string>();
        string targetViewState;
        private void DrawMenuBar()
        {
            menuBar = new Rect(0, 0, position.width, menuBarHeight);

            GUILayout.BeginArea(menuBar, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();

            GUILayout.Space(5);
            if (GUILayout.Button(new GUIContent("Save"), EditorStyles.toolbarButton, GUILayout.Width(35)))
            {
                dataReader.Save(viewPageList, viewStateList);
            }
            // GUILayout.Space(5);
            // GUILayout.Button(new GUIContent("Load"), EditorStyles.toolbarButton, GUILayout.Width(35));
            GUILayout.Space(5);
            if (GUILayout.Button(new GUIContent("Reload", refreshIcon, "Reload data"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RefreshData();
            }
            GUILayout.Space(5);
            showSideBar = GUILayout.Toggle(showSideBar, new GUIContent(sideBarIcon, "Show SideBar"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight), GUILayout.Width(25));

            GUILayout.Space(5);
            console.showConsole = GUILayout.Toggle(console.showConsole, new GUIContent(miniErrorIcon, "Show Console"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight), GUILayout.Width(25));

            GUILayout.FlexibleSpace();
            GUILayout.Label(new GUIContent(zoomIcon, "Zoom"));
            zoomScale = EditorGUILayout.Slider(zoomScale, 0.1f, 1, GUILayout.Width(120));

            GUILayout.Label("ViewState:");
            int newIndex = EditorGUILayout.Popup(currentIndex, viewStatesPopup.ToArray(),
                EditorStyles.toolbarPopup, GUILayout.Width(80));
            if (newIndex != currentIndex)
            {
                currentIndex = newIndex;
                targetViewState = viewStatesPopup[currentIndex];
            }

            if (GUILayout.Button(new GUIContent("Baked to Scritpable", bakeScritpIcon, "Bake ViewPage and ViewState to script"), EditorStyles.toolbarButton, GUILayout.Width(140)))
            {
                ViewSystemEditor.BakeAllViewPageName();
            }
            if (GUILayout.Button(new GUIContent("Normalized", normalizedIcon, "Normalized Preview"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                dataReader.Normalized();
            }
            //m_showDescription = GUILayout.Toggle(m_showDescription, new GUIContent(m_miniInfoIcon, "Show graph description"), EditorStyles.toolbarButton, GUILayout.Height(Model.Settings.GUI.TOOLBAR_HEIGHT));
            //m_showVerboseLog = GUILayout.Toggle(m_showVerboseLog, new GUIContent("Verbose Log", "Increse console log messages"), EditorStyles.toolbarButton, GUILayout.Height(Model.Settings.GUI.TOOLBAR_HEIGHT));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private ViewStateNode selectedViewStateNode;
        private ViewPageNode selectedViewPageNode;
        private void DrawCurrentConnectionLine(Event e)
        {
            if (selectedViewStateNode != null && selectedViewPageNode == null)
            {
                // Handles.DrawBezier(
                //     e.mousePosition,
                //     selectedViewStateNode.nodeConnectionLinker.rect.center,
                //     e.mousePosition + Vector2.up * 20f,
                //     selectedViewStateNode.rect.center + Vector2.up * 20f,
                //     Color.yellow,
                //     null,
                //     2f
                // );
                Handles.DrawLine(e.mousePosition, selectedViewStateNode.nodeConnectionLinker.rect.center);
                GUI.changed = true;
            }

            if (selectedViewPageNode != null && selectedViewStateNode == null)
            {
                // Handles.DrawBezier(

                //     selectedViewPageNode.nodeConnectionLinker.rect.center, e.mousePosition,
                //     e.mousePosition - Vector2.up * 20f,
                //     selectedViewPageNode.rect.center + Vector2.up * 20f,
                //     Color.yellow,
                //     null,
                //     2f
                // );
                Handles.DrawLine(e.mousePosition, selectedViewPageNode.nodeConnectionLinker.rect.center);
                GUI.changed = true;
            }
        }
    }
}
