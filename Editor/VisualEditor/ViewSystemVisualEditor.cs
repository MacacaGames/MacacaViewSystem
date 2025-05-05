﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using MacacaGames.ViewSystem;
using UnityEngine.UIElements;
using UnityEditor.SceneManagement;
using System.Reflection;

namespace MacacaGames.ViewSystem.VisualEditor
{

    [InitializeOnLoad]
    internal class PrefabExtension
    {  // Unity Issue, prefab reference in scene will be modify after you apply/revert a prefab
       // A hack to fix it
       // https://issuetracker.unity3d.com/issues/prefabs-references-are-lost-when-modifying-prefab
        static PrefabExtension()
        {
            UnityEditor.PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdate;

        }

        static void OnPrefabInstanceUpdate(GameObject instance)
        {
            // UnityEngine.Debug.Log("[Callback] Prefab.Apply on instance id:" + instance.GetInstanceID() + ", name:" + instance.name);
            ViewSystemVisualEditor.dataReader.RepairPrefabReference();
        }
    }

    public class ViewSystemVisualEditor : EditorWindow
    {
        public static ViewSystemVisualEditor Instance;
        public static ViewSystemDataReaderV2 dataReader;
        public static ViewSystemNodeInspector inspector;
        public static ViewSystemGlobalSettingWindow globalSettingWindow;
        static ViewPageOrderWindow viewPageOrderWindow;
        public OverridePopupWindow overridePopupWindow;
        public ViewPageNavigationWindow navigationWindow;
        public ViewBreakpointWindow breakpointWindow;
        private ViewSystemVerifier viewSystemVerifier;
        public static ViewSystemSaveData saveData;
        bool isInit = false;
        public static bool allowPreviewWhenPlaying = false;
        public static bool overrideFromOrginal = false;
        public static bool removeConnectWithoutAsk = false;
        public Transform ViewControllerRoot => dataReader.ViewControllerTransform;

        [MenuItem("MacacaGames/ViewSystem/Visual Editor")]
        private static void OpenWindow()
        {
            Instance = GetWindow<ViewSystemVisualEditor>();
            Instance.titleContent = new GUIContent("View System Visual Editor");
            Instance.minSize = new Vector2(600, 400);
        }


        public VisualElement nodeViewContianer;
        public VisualElement toolbarContianer;
        public VisualElement inspectorContianer;
        public VisualElement floatWindowContianer;

        bool inspectorResize = false;
        public void OnEnable()
        {
            EditorApplication.playModeStateChanged += playModeStateChanged;

            Instance = this;
            RefreshData();

            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Import UXML
            var visualTree = Resources.Load<VisualTreeAsset>("ViewSystemNodeEditorUIElementUxml");
            VisualElement visulaElementFromUXML = visualTree.CloneTree();
            var styleSheet = Resources.Load<StyleSheet>("ViewSystemNodeEditorUIElementUss");
            visulaElementFromUXML.styleSheets.Add(styleSheet);
            visulaElementFromUXML.style.flexGrow = 1;


            toolbarContianer = new IMGUIContainer(DrawMenuBar);
            toolbarContianer.style.flexGrow = 1;
            visulaElementFromUXML.Q("toolbar").Add(toolbarContianer);

            inspectorContianer = new IMGUIContainer(DrawInspector);
            inspectorContianer.style.flexGrow = 1;
            visulaElementFromUXML.Q("inspector").Add(inspectorContianer);
            if (EditorGUIUtility.isProSkin)
            {
                visulaElementFromUXML.Q("inspector").style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            }
            nodeViewContianer = new IMGUIContainer(DrawNode);
            nodeViewContianer.style.flexGrow = 1;
            visulaElementFromUXML.Q("node-view").Add(nodeViewContianer);

            var dragger = visulaElementFromUXML.Q("dragger");
            dragger.AddManipulator(new VisualElementResizer());

            root.Add(visulaElementFromUXML);
        }

        public static void ApplySafeArea(SafePadding.PerEdgeValues edgeValues)
        {
            dataReader.ApplySafeArea(edgeValues);
        }

        public void RefreshData(bool hardRefresh = true)
        {
            ClearEditor();
            EditMode = false;
            dataReader = new ViewSystemDataReaderV2(this);
            isInit = dataReader.Init();
            saveData = ((ViewSystemDataReaderV2)dataReader).GetSaveData();
            inspector = new ViewSystemNodeInspector(this);
            //ViewControllerRoot = ((ViewSystemDataReaderV2)dataReader).GetViewControllerRoot();
            globalSettingWindow = new ViewSystemGlobalSettingWindow("Global Setting", this, (ViewSystemDataReaderV2)dataReader);
            viewPageOrderWindow = new ViewPageOrderWindow("Overlay Order", this, (ViewSystemDataReaderV2)dataReader);
            overridePopupWindow = new OverridePopupWindow("Override", this, inspector);
            navigationWindow = new ViewPageNavigationWindow("Navigation Setting", this);
            viewSystemVerifier = new ViewSystemVerifier(this, saveData);
            breakpointWindow = new ViewBreakpointWindow("Break Point Setting", this);
            viewStatesPopup.Add("All");
            viewStatesPopup.Add("Overlay Only");
            viewStatesPopup.AddRange(viewStateList.Select(m => m.viewState.name));

            if (hardRefresh == false && lastSelectNode != null)
            {
                inspector.SetCurrentSelectItem(lastSelectNode);
            }
            dataReader.EditEnd();
            CanEnterEditMode = true;
        }
        public void ClearEditor()
        {
            // nodeConnectionLineList.Clear();
            viewStateList.Clear();
            viewPageList.Clear();
            viewStatesPopup.Clear();
            CanEnterEditMode = false;
        }
        void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= playModeStateChanged;
        }
        void Awake()
        {
        }
        private static void playModeStateChanged(PlayModeStateChange obj)
        {
            Debug.Log("playModeStateChanged");
            if (obj == PlayModeStateChange.ExitingEditMode)
            {
                dataReader.SaveUniqueData(true);
                
                dataReader.EditEnd();
            }
        }
        List<ViewPageNode> viewPageList = new List<ViewPageNode>();
        List<ViewStateNode> viewStateList = new List<ViewStateNode>();
        public static float zoomScale = 1.0f;
        Rect scriptViewRect;
        public static Vector2 viewPortScroll;
        Vector2 zoomScaleMinMax = new Vector2(0.25f, 1);
        protected virtual void DoZoom(float delta, Vector2 center)
        {
            var prevZoom = zoomScale;
            zoomScale += delta;
            zoomScale = Mathf.Clamp(zoomScale, zoomScaleMinMax.x, zoomScaleMinMax.y);
            var deltaSize = nodeViewContianer.contentRect.size / prevZoom - nodeViewContianer.contentRect.size / zoomScale;
            var offset = -Vector2.Scale(deltaSize, center);
            viewPortScroll += offset;
        }
        void DrawFloatWindow()
        {

            BeginWindows();
            if (globalSettingWindow != null) globalSettingWindow.OnGUI();
            if (viewPageOrderWindow != null) viewPageOrderWindow.OnGUI();
            if (overridePopupWindow != null) overridePopupWindow.OnGUI();
            if (navigationWindow != null) navigationWindow.OnGUI();
            if (breakpointWindow != null) breakpointWindow.OnGUI();
            EndWindows();
        }
        List<Rect> calculateCache = new List<Rect>();
        void DrawNode()
        {
            scriptViewRect = new Rect(nodeViewContianer.contentRect.x, nodeViewContianer.contentRect.y - menuBarHeight + 2, nodeViewContianer.contentRect.width / zoomScale, nodeViewContianer.contentRect.height / zoomScale);

            EditorZoomArea.NoGroupBegin(zoomScale, scriptViewRect);
            DrawGrid();

            foreach (var item in viewStateList.ToArray())
            {

                if (!string.IsNullOrEmpty(item.viewState.name))
                {
                    //Draw Bounds
                    calculateCache.Clear();
                    calculateCache.Add(item.drawRect);
                    if (viewPageList.Count(m => m.viewPage.viewState == item.viewState.name) > 0)
                    {
                        calculateCache.AddRange(viewPageList.Where(m => m.viewPage.viewState == item.viewState.name).Select(m => m.drawRect));
                        Rect rect = VS_EditorUtility.CalculateBoundsRectFromRects(calculateCache, new Vector2(20, 20));
                        GUI.Box(rect, "", new GUIStyle("SelectionRect"));
                    }
                }
                item.Draw(false);
            }
            foreach (var item in viewPageList.ToArray())
            {
                bool highlight = false;
                if (Application.isPlaying && ViewController.Instance != null)
                {
                    if (item.viewPage.viewPageType == ViewPage.ViewPageType.FullPage) highlight = ViewController.Instance.currentViewPage.name == item.name;
                    else highlight = ViewController.Instance.IsOverPageLive(item.name);
                }
                else
                {
                    highlight = !string.IsNullOrEmpty(search) && item.name.ToLower().Contains(search.ToLower());
                }

                item.Draw(highlight);
            }

            DrawCurrentConnectionLine(Event.current);
            EditorZoomArea.NoGroupEnd();

            DrawFloatWindow();

            ProcessEvents(Event.current);
            CheckRepaint();
        }

        void DrawInspector()
        {
            inspector.Draw();
        }

        public void CheckRepaint()
        {
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
                        if (scriptViewRect.Contains(e.mousePosition))
                        {
                            OnDrag(e.delta * 1 / zoomScale);
                        }
                    }
                    break;
                case EventType.ScrollWheel:
                    Vector2 zoomCenter;
                    zoomCenter.x = e.mousePosition.x / zoomScale / nodeViewContianer.contentRect.width;
                    zoomCenter.y = e.mousePosition.y / zoomScale / nodeViewContianer.contentRect.height;
                    zoomCenter *= zoomScale;
                    DoZoom(-e.delta.y * 0.01f, zoomCenter);
                    e.Use();
                    break;
                case EventType.MouseDown:

                    if (e.button == 1)
                    {
                        if (ViewSystemNodeInspector.isMouseInSideBar() && inspector.show)
                        {
                            return;
                        }
                        if (!nodeViewContianer.contentRect.Contains(e.mousePosition))
                        {
                            return;
                        }
                        GenericMenu genericMenu = new GenericMenu();

                        genericMenu.AddItem(new GUIContent("Add FullPage"), false,
                            () =>
                            {
                                Vector2 pos = new Vector2(e.mousePosition.x - inspectorContianer.contentRect.width, e.mousePosition.y - menuBarHeight);
                                Vector2 unScalePos = new Vector2(-ViewSystemNode.ViewSystemNodeWidth * 0.5f, -ViewSystemNode.ViewPageNodeHeight * .5f);
                                AddViewPageNode((pos * 1 / zoomScale + unScalePos) - viewPortScroll, false);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Add OverlayPage"), false,
                            () =>
                            {
                                Vector2 pos = new Vector2(e.mousePosition.x - inspectorContianer.contentRect.width, e.mousePosition.y - menuBarHeight);
                                Vector2 unScalePos = new Vector2(-ViewSystemNode.ViewSystemNodeWidth * 0.5f, -ViewSystemNode.ViewPageNodeHeight * .5f);
                                AddViewPageNode((pos * 1 / zoomScale + unScalePos) - viewPortScroll, true);
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Add ViewState"), false,
                            () =>
                            {
                                Vector2 pos = new Vector2(e.mousePosition.x - inspectorContianer.contentRect.width, e.mousePosition.y - menuBarHeight);
                                Vector2 unScalePos = new Vector2(-ViewSystemNode.ViewSystemNodeWidth * 0.5f, -ViewSystemNode.ViewStateNodeHeight * .5f);
                                AddViewStateNode((pos * 1 / zoomScale + unScalePos) - viewPortScroll);
                            }
                        );
                        genericMenu.ShowAsContext();
                    }
                    break;
            }
        }

        ViewSystemNode lastSelectNode;
        public ViewStateNode AddViewStateNode(Vector2 position, ViewState viewState = null)
        {
            var node = new ViewStateNode(position, CheckCanMakeConnect, viewState);
            node.OnNodeSelect += (m) =>
            {
                lastSelectNode = m;
                inspector.SetCurrentSelectItem(m);
            };
            node.OnNodeDelete += () =>
            {
                dataReader.OnViewStateDelete(node);
                viewStateList.Remove(node);

            };

            if (viewState == null)
            {
                dataReader.OnViewStateAdd(new Vector2(node.rect.x, node.rect.y), node.viewState);
            }
            viewStateList.Add(node);
            return node;
        }

        public ViewPageNode AddViewPageNode(Vector2 position, bool isOverlay, ViewPage viewPage = null)
        {
            var node = new ViewPageNode(position, isOverlay, CheckCanMakeConnect,
                (vsn, vpn) =>
                {
                    if (vsn != null) vsn.currentLinkedViewPageNode.Remove(vpn);
                },
                viewPage
            );
            node.OnPreviewBtnClick += (m) =>
            {
                if (Application.isPlaying)
                {
                    if (EditorUtility.DisplayDialog(
                                "Warring",
                                "During play mode, the preview function will try to show/changePage to the target view page, do you want to continue?",
                                "Go Ahead",
                                "No, thanks"))
                    {
                        PageChanger pageChanger = null;
                        if (node.nodeType == ViewSystemNode.NodeType.Overlay)
                        {
                            pageChanger = ViewController.OverlayPageChanger();

                            if (ViewController.Instance != null && ViewController.Instance.IsOverPageLive(node.viewPage.name))
                            {
                                (pageChanger.SetPage(node.viewPage.name) as OverlayPageChanger).Leave();
                                return;
                            }
                        }
                        else
                        {
                            pageChanger = ViewController.FullPageChanger();
                        }
                        pageChanger.SetPage(node.viewPage.name)
                        .Show();
                    }
                    return;
                }


                var bps = HasBreakPoint(m);
                if (bps.Length > 0)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent($"Preview with Default BreakPoint  "), false, () =>
                    {
                        dataReader.OnViewPagePreview(m, null);
                    });
                    foreach (var item in bps)
                    {
                        menu.AddItem(new GUIContent($"Preview with {item} BreakPoint  "), false, () =>
                        {
                            dataReader.OnViewPagePreview(m, new Dictionary<string, bool>
                            {
                                {item,true}
                            });
                        });
                    }
                    menu.ShowAsContext();
                }
                else
                {
                    dataReader.OnViewPagePreview(m, null);
                }

                string[] HasBreakPoint(ViewPage _viewPage)
                {
                    List<string> result = new List<string>();
                    foreach (var item in _viewPage.viewPageItems)
                    {
                        if (item.breakPointViewElementTransforms != null)
                        {
                            foreach (var bp in item.breakPointViewElementTransforms)
                            {
                                if (!result.Contains(bp.breakPointName)) result.Add(bp.breakPointName);
                            }
                        }
                    }

                    var viewState = viewStateList.SingleOrDefault(x => x.viewState.name == _viewPage.viewState);
                    if (viewState != null)
                    {
                        foreach (var item in viewState.viewState.viewPageItems)
                        {
                            if (item.breakPointViewElementTransforms != null)
                            {
                                foreach (var bp in item.breakPointViewElementTransforms)
                                {
                                    if (!result.Contains(bp.breakPointName)) result.Add(bp.breakPointName);
                                }
                            }
                        }
                    }
                    return result.ToArray();
                }
            };
            node.OnNodeSelect += (m) =>
            {
                lastSelectNode = m;
                inspector.SetCurrentSelectItem(m);
            };
            node.OnNodeDelete += () =>
            {
                dataReader.OnViewPageDelete(node);
                viewPageList.Remove(node);
            };
            node.OnDisConnect = OnDisconnect;
            if (viewPage == null)
            {
                dataReader.OnViewPageAdd(new Vector2(node.rect.x, node.rect.y), node.viewPage);
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
                        if (selectedViewStateNode.currentLinkedViewPageNode.Count(m => m.nodeType != selectedViewPageNode.nodeType) > 0)
                        {
                            if (EditorUtility.DisplayDialog(
                                "Warring",
                                $"The target ViewState is already connect to one or more {selectedViewPageNode.nodeType} Page.\nSince ViewState can only connect to one type of ViewPage(FullPage or Overlay) you should remove all {selectedViewPageNode.nodeType} connection on ViewState to continue.\nOr press 'OK' the editor will help you do this stuff.",
                                "Go Ahead",
                                "I'll do it myself"))
                            {
                                //刪掉線
                                foreach (var pagenode in selectedViewStateNode.currentLinkedViewPageNode)
                                {
                                    pagenode.viewPage.viewState = string.Empty;
                                    pagenode.currentLinkedViewStateNode = null;
                                }
                                selectedViewStateNode.currentLinkedViewPageNode.Clear();
                                CreateConnection();
                                ClearConnectionSelection();
                            }
                            else
                            {
                                ClearConnectionSelection();
                            }
                            return;
                        }

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

                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        else
                        {
                            ClearConnectionSelection();
                        }
                    }
                    break;
                //被聯線的是 FullPage
                case ViewSystemNode.NodeType.FullPage:
                    selectedViewPageNode = (ViewPageNode)currentClickNode;
                    if (selectedViewStateNode != null)
                    {
                        if (selectedViewStateNode.currentLinkedViewPageNode.Count(m => m.nodeType != selectedViewPageNode.nodeType) > 0)
                        {
                            if (EditorUtility.DisplayDialog(
                                "Warring",
                                $"The target ViewPage is already connect to one or more {selectedViewPageNode.nodeType} Page.\nSince ViewState can only connect to one type of ViewPage(FullPage or Overlay) you should remove all {selectedViewPageNode.nodeType} connection on ViewState to continue.\nOr press 'OK' the editor will help you do this stuff.",
                                "Go Ahead",
                                "I'll do it myself"))
                            {
                                //刪掉線
                                foreach (var pagenode in selectedViewStateNode.currentLinkedViewPageNode)
                                {
                                    pagenode.viewPage.viewState = string.Empty;
                                    pagenode.currentLinkedViewStateNode = null;
                                }
                                selectedViewStateNode.currentLinkedViewPageNode.Clear();

                                CreateConnection();
                                ClearConnectionSelection();
                            }
                            else
                            {
                                ClearConnectionSelection();
                            }

                            return;
                        }
                        //檢查是不是已經有跟這個 ViewStateNode 節點連線過了
                        if (selectedViewPageNode.currentLinkedViewStateNode == null)
                        {
                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        // 如果連線的節點跟原本的不同 刪掉舊的連線 然後建立新了
                        else if (selectedViewPageNode.currentLinkedViewStateNode != selectedViewStateNode)
                        {
                            //刪掉 ViewStateNode 裡的 ViewPageNode
                            selectedViewPageNode.currentLinkedViewStateNode.currentLinkedViewPageNode.Remove(selectedViewPageNode);

                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        else
                        {
                            ClearConnectionSelection();
                        }
                    }
                    break;

                //Overlay
                case ViewSystemNode.NodeType.Overlay:
                    selectedViewPageNode = (ViewPageNode)currentClickNode;
                    if (selectedViewStateNode != null)
                    {
                        if (selectedViewStateNode.currentLinkedViewPageNode.Count(m => m.nodeType != selectedViewPageNode.nodeType) > 0)
                        {
                            if (EditorUtility.DisplayDialog(
                                "Warring",
                                $"The target ViewPage is already connect to one or more {selectedViewPageNode.nodeType} Page.\nSince ViewState can only connect to one type of ViewPage(FullPage or Overlay) you should remove all {selectedViewPageNode.nodeType} connection on ViewState to continue.\nOr press 'OK' the editor will help you do this stuff.",
                                "Go Ahead",
                                "I'll do it myself"))
                            {
                                //刪掉線
                                foreach (var pagenode in selectedViewStateNode.currentLinkedViewPageNode)
                                {
                                    pagenode.viewPage.viewState = string.Empty;
                                    pagenode.currentLinkedViewStateNode = null;
                                }
                                selectedViewStateNode.currentLinkedViewPageNode.Clear();

                                CreateConnection();
                                ClearConnectionSelection();
                            }
                            else
                            {
                                ClearConnectionSelection();
                            }

                            return;
                        }
                        //檢查是不是已經有跟這個 ViewStateNode 節點連線過了
                        if (selectedViewPageNode.currentLinkedViewStateNode == null)
                        {
                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        // 如果連線的節點跟原本的不同 刪掉舊的連線 然後建立新了
                        else if (selectedViewPageNode.currentLinkedViewStateNode != selectedViewStateNode)
                        {
                            //刪掉 ViewStateNode 裡的 ViewPageNode
                            selectedViewPageNode.currentLinkedViewStateNode.currentLinkedViewPageNode.Remove(selectedViewPageNode);

                            //刪掉線
                            CreateConnection();
                            ClearConnectionSelection();
                        }
                        else
                        {
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
                CreateConnection(viewStateNode, item);
            }
        }
        private void CreateConnection(ViewStateNode viewStateNode, ViewPageNode viewPageNode)
        {
            viewStateNode.currentLinkedViewPageNode.Add(viewPageNode);
            viewPageNode.currentLinkedViewStateNode = viewStateNode;

            viewStateNode.OnNodeConnect(viewPageNode);
            viewPageNode.OnNodeConnect(viewStateNode);

        }

        void OnDisconnect(ViewPageNode viewPageNode)
        {
            viewPageNode.currentLinkedViewStateNode.currentLinkedViewPageNode.Remove(viewPageNode);
            viewPageNode.currentLinkedViewStateNode = null;
            viewPageNode.viewPage.viewState = "";
        }

        private void ClearConnectionSelection()
        {
            selectedViewStateNode = null;
            selectedViewPageNode = null;
        }

        private ViewStateNode selectedViewStateNode;
        private ViewPageNode selectedViewPageNode;
        private void DrawCurrentConnectionLine(Event e)
        {
            if (selectedViewStateNode != null && selectedViewPageNode == null)
            {
                Handles.DrawLine(e.mousePosition, selectedViewStateNode.drawRect.center);
                if (e.type == EventType.MouseDown)
                {
                    if (e.button == 0)
                    {
                        //Find only viewpage
                        var node = viewPageList.Where(m => m.drawRect.Contains(e.mousePosition)).FirstOrDefault();
                        if (node != null)
                        {
                            node.OnConnect?.Invoke(node);
                        }
                        else
                        {
                            ClearConnectionSelection();
                        }
                    }
                }
                GUI.changed = true;
            }

            if (selectedViewPageNode != null && selectedViewStateNode == null)
            {
                Handles.DrawLine(e.mousePosition, selectedViewPageNode.drawRect.center);
                if (e.type == EventType.MouseDown)
                {
                    if (e.button == 0)
                    {
                        var node = viewStateList.Where(m => m.drawRect.Contains(e.mousePosition)).FirstOrDefault();
                        if (node != null)
                        {
                            node.OnConnect?.Invoke(node);
                        }
                        else
                        {
                            ClearConnectionSelection();
                        }
                    }
                }
                GUI.changed = true;
            }
        }

        private void OnDrag(Vector2 delta)
        {
            viewPortScroll += delta * zoomScale;
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
        List<string> viewStatesPopup = new List<string>();

        int lastInspectorWidth;

        public bool EditMode = false;
        bool CanEnterEditMode = false;
        string search = "";
        Rect popupBtnRect;
        private void DrawMenuBar()
        {
            menuBar = new Rect(0, 0, position.width, menuBarHeight);

            using (var area = new GUILayout.AreaScope(menuBar, "", EditorStyles.toolbar))
            {
                using (var horizon = new GUILayout.HorizontalScope())
                {
                    using (var disable = new EditorGUI.DisabledGroupScope(!CanEnterEditMode))
                    {
                        using (var changed = new EditorGUI.ChangeCheckScope())
                        {
                            EditMode = GUILayout.Toggle(EditMode, new GUIContent("Edit Mode"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight));
                            if (changed.changed == true)
                            {
                                if (EditMode)
                                {
                                    dataReader.EditStart();
                                }
                                else
                                {
                                    dataReader.EditEnd();
                                }
                            }
                        }
                    }

                    GUILayout.Space(5);
                    if (GUILayout.Button(new GUIContent($"Save{(((ViewSystemDataReaderV2)dataReader).isDirty ? "*" : "")}", EditorGUIUtility.FindTexture("d_SaveAs@2x")), EditorStyles.toolbarButton, GUILayout.Width(50)))
                    {
                        if (isInit == false)
                        {
#if UNITY_2019_1_OR_NEWER
                            ViewSystemLog.ShowNotification(this, new GUIContent("Editor is not Initial."), 2);
#else
                            ViewSystemLog.ShowNotification(this,new GUIContent("Editor is not Initial."));
#endif
                            return;
                        }
                        if (EditorUtility.DisplayDialog("Save", "Save action will also delete all ViewElement in scene. \nDo you really want to continue?", "Yes", "No"))
                        {
                            if (dataReader.GetSaveData().globalSetting.UIRootScene == null)
                            {
                                ViewSystemLog.LogWarning("There is no UIRoot in edit, will ignore the saving of UIRoot Prefab.");
                            }
                            dataReader.Save(viewPageList, viewStateList);
                        }
                    }
                    if (GUILayout.Button(new GUIContent("Reload", Drawer.refreshIcon, "Reload data"), EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        if (overridePopupWindow != null) overridePopupWindow.show = false;
                        RefreshData();
                    }

                    if (GUILayout.Button(new GUIContent("Clear Preview", "Clear all preview item"), EditorStyles.toolbarButton))
                    {
                        ((ViewSystemDataReaderV2)dataReader).ClearAllViewElementInScene();
                    }
                    if (GUILayout.Button(new GUIContent("Normalized", "Normalized all item (Will Delete the Canvas Root Object in Scene)"), EditorStyles.toolbarButton))
                    {
                        overridePopupWindow.show = false;
                        dataReader.Normalized();
                        inspector.Normalized();
                    }
                    GUILayout.Space(5);
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        inspector.show = GUILayout.Toggle(inspector.show, new GUIContent(Drawer.inspectorIcon, "Show Inspector"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight), GUILayout.Width(25));
                        if (check.changed)
                        {
                            inspectorContianer.parent.style.display = inspector.show ? DisplayStyle.Flex : DisplayStyle.None;
                        }
                    }
                    GUILayout.Space(5);
                    if (globalSettingWindow != null)
                        globalSettingWindow.show = GUILayout.Toggle(globalSettingWindow.show, new GUIContent("Global Setting", EditorGUIUtility.FindTexture("SceneViewTools")), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight));

                    GUILayout.Space(5);
                    if (viewPageOrderWindow != null)
                        viewPageOrderWindow.show = GUILayout.Toggle(viewPageOrderWindow.show, new GUIContent("Overlay Order", EditorGUIUtility.FindTexture("CustomSorting"), "Batch modify Overlay Pager Sorting Order"), EditorStyles.toolbarButton, GUILayout.Height(menuBarHeight));
                    GUILayout.Space(5);

                    if (GUILayout.Button(new GUIContent("Verifiers"), EditorStyles.toolbarDropDown, GUILayout.Height(menuBarHeight)))
                    {
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
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        search = EditorGUILayout.TextField(search, new GUIStyle("SearchTextField"));
                    }


                    GUILayout.FlexibleSpace();
                    GUILayout.Label(new GUIContent(Drawer.zoomIcon, "Zoom"), GUIStyle.none);
                    zoomScale = EditorGUILayout.Slider(zoomScale, zoomScaleMinMax.x, zoomScaleMinMax.y, GUILayout.Width(120));

                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("AvatarCompass"), "Reset viewport to (0,0)"), EditorStyles.toolbarButton, GUILayout.Width(30)))
                    {
                        viewPortScroll = Vector2.zero;
                    }

                    if (GUILayout.Button(new GUIContent(Drawer.bakeScritpIcon, "Bake ViewPage and ViewState to script"), EditorStyles.toolbarButton, GUILayout.Width(50)))
                    {
                        ViewSystemScriptBaker.BakeAllViewPageName(
                            viewPageList.Select(m => m.viewPage).ToList(),
                            viewStateList.Select(m => m.viewState).ToList(),
                            saveData.globalSetting.breakPoints.ToList());
                    }
                    if (GUILayout.Button("Options", EditorStyles.toolbarDropDown))
                    {
                        UnityEditor.PopupWindow.Show(popupBtnRect, new EditorPopupSetting());
                    }
                    if (Event.current.type == EventType.Repaint) popupBtnRect = GUILayoutUtility.GetLastRect();
                }
            }
        }

        public void MigrateToSplitTransformData()
        {
            foreach (var vps in viewPageList)
            {
                foreach (var vpi in vps.viewPage.viewPageItems)
                {
                    if (vpi == null)
                    {
                        continue;
                    }
                    vpi.defaultTransformDatas.parent = vpi.parent;
                    vpi.defaultTransformDatas.parentPath = vpi.parentPath;
                    vpi.defaultTransformDatas.rectTransformData = vpi.transformData;
                    vpi.defaultTransformDatas.rectTransformFlag = vpi.transformFlag;
                }
            }
            foreach (var vss in viewStateList)
            {
                foreach (var vpi in vss.viewState.viewPageItems)
                {
                    if (vpi == null)
                    {
                        continue;
                    }
                    vpi.defaultTransformDatas.parent = vpi.parent;
                    vpi.defaultTransformDatas.parentPath = vpi.parentPath;
                    vpi.defaultTransformDatas.rectTransformData = vpi.transformData;
                    vpi.defaultTransformDatas.rectTransformFlag = vpi.transformFlag;
                }
            }

            dataReader.Save(viewPageList, viewStateList);
        }

        public bool IsNodeInactivable
        {
            get
            {
                return !(
                    globalSettingWindow.show ||
                    overridePopupWindow.show ||
                    navigationWindow.show ||
                    viewPageOrderWindow.show ||
                    breakpointWindow.show ||
                    ViewSystemNodeInspector.isMouseInSideBar());
            }
        }



        //對應 方法名稱與 pop index 的字典
        //第 n 個腳本的參照
        public static Dictionary<string, CMEditorLayout.GroupedPopupData[]> classMethodInfo = new Dictionary<string, CMEditorLayout.GroupedPopupData[]>();
        internal static BindingFlags BindFlagsForScript = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        internal static bool RequireAutoRefreshMethodDatabase()
        {
            return ViewSystemVisualEditor.classMethodInfo == null || ViewSystemVisualEditor.classMethodInfo.Count == 0;
        }
        internal static void RefreshMethodDatabase()
        {
            classMethodInfo.Clear();
            classMethodInfo.Add("Nothing Select", null);
            List<CMEditorLayout.GroupedPopupData> VerifiedMethod = new List<CMEditorLayout.GroupedPopupData>();
            var types = GetAllTypeWithMethodHasViewSystemEventAttribute();
            foreach (var type in types)
            {
                MethodInfo[] methodInfos = type.GetMethods(BindFlagsForScript);
                VerifiedMethod.Clear();
                foreach (var item in methodInfos)
                {
                    var para = item.GetParameters();
                    if (para.Where(m => m.ParameterType.IsAssignableFrom(typeof(Component))).Count() == 0)
                    {
                        continue;
                    }

                    var eventMethodInfo = new CMEditorLayout.GroupedPopupData { name = item.Name, group = "" };
                    var attr = System.Attribute.GetCustomAttribute(item, typeof(ViewSystemEventAttribute));

                    if (attr == null)
                    {
                        continue;
                    }
                    ViewSystemEventAttribute a = (ViewSystemEventAttribute)attr;
                    eventMethodInfo.group = a.GetGroupName();
                    VerifiedMethod.Add(eventMethodInfo);
                }
                classMethodInfo.Add(type.ToString(), VerifiedMethod.ToArray());
            }
        }

        internal static IEnumerable<Type> GetAllTypeWithMethodHasViewSystemEventAttribute()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(m => m.FullName.Contains("Assembly-CSharp"));

            return assemblies
                    .SelectMany(s => s.GetTypes())
                    .Where(p =>
                    {
                        var methods = p.GetMethods();
                        foreach (var method in methods)
                        {
                            try
                            {
                                var attr = System.Attribute.GetCustomAttribute(method, typeof(ViewSystemEventAttribute));
                                if (attr != null)
                                {
                                    return true;
                                }
                            }
                            catch { }
                        }
                        return false;
                    });
        }


        class VisualElementResizer : MouseManipulator
        {
            private Vector2 m_Start;
            protected bool m_Active;
            public VisualElementResizer()
            {
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
                m_Active = false;
            }

            protected override void RegisterCallbacksOnTarget()
            {

                target.RegisterCallback<MouseDownEvent>(OnMouseDown);
                target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
                target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            }

            protected void OnMouseDown(MouseDownEvent e)
            {
                if (m_Active)
                {
                    e.StopImmediatePropagation();
                    return;
                }

                if (CanStartManipulation(e))
                {
                    m_Start = e.localMousePosition;

                    m_Active = true;
                    target.CaptureMouse();
                    e.StopPropagation();
                }
            }

            protected void OnMouseMove(MouseMoveEvent e)
            {
                if (!m_Active || !target.HasMouseCapture())
                    return;

                Vector2 diff = e.localMousePosition - m_Start;

                //target.parent.style.height = target.parent.layout.height + diff.x;
                var t = target.parent.parent.ElementAt(0);

                int w = (int)t.style.width.value.value;
                t.style.width = Mathf.Clamp(w + diff.x, 250, 450);

                e.StopPropagation();
            }

            protected void OnMouseUp(MouseUpEvent e)
            {
                if (!m_Active || !target.HasMouseCapture() || !CanStopManipulation(e))
                    return;

                m_Active = false;
                target.ReleaseMouse();
                e.StopPropagation();
            }
        }

        [UnityEditor.Callbacks.OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceID) as ViewSystemSaveData;
            if (asset == null)
                return false;

            OpenWindow();

            return true;
        }
    }

    public class EditorPopupSetting : PopupWindowContent
    {
        public override Vector2 GetWindowSize()
        {
            return new Vector2(250, 100);
        }

        public override void OnGUI(Rect rect)
        {
            ViewSystemVisualEditor.allowPreviewWhenPlaying = EditorGUILayout.ToggleLeft("Allow Preview When Playing", ViewSystemVisualEditor.allowPreviewWhenPlaying);
            ViewSystemVisualEditor.overrideFromOrginal = EditorGUILayout.ToggleLeft("Get Override From Orginal Prefab", ViewSystemVisualEditor.overrideFromOrginal);
            ViewSystemVisualEditor.removeConnectWithoutAsk = EditorGUILayout.ToggleLeft("Remove Connect Without Confirm", ViewSystemVisualEditor.removeConnectWithoutAsk);
            if (GUILayout.Button("Migrate to split transform"))
            {
                ViewSystemVisualEditor.Instance.MigrateToSplitTransformData();
            }
        }
    }


}
