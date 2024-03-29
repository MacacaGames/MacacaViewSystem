using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;

namespace MacacaGames.ViewSystem.VisualEditor
{
    public class ViewSystemNode
    {
        public const int ViewSystemNodeWidth = 160;
        public const int ViewStateNodeHeight = 60;
        public const int ViewPageNodeHeight = 80;
        protected int id;
        public enum NodeType
        {
            FullPage, Overlay, ViewState,
        }
        public NodeType nodeType;
        public System.Action<ViewSystemNode> OnNodeSelect;
        public Action<ViewSystemNode> OnConnect;
        public Action OnNodeDelete;
        public string name;
        protected static int currentMaxId = 0;
        public Rect rect;
        public Rect drawRect;
        const int lableHeight = 15;
        GUIStyle titleStyle;
        static GUIStyle _TextBarStyle;
        static GUIStyle TextBarStyle
        {
            get
            {
                if (_TextBarStyle == null)
                {
                    _TextBarStyle = new GUIStyle("TE NodeBackground");
                    _TextBarStyle.normal.textColor = Color.white;
                }
                return _TextBarStyle;

            }
        }
        public ViewSystemNode(Action<ViewSystemNode> OnConnect)
        {
            id = UnityEngine.Random.Range(-99999, 99999);
            this.OnConnect = OnConnect;
        }
        void _OnNodeDelete()
        {
            OnNodeDelete?.Invoke();
            // CurrentConnectLine.Clear();

            if (this is ViewPageNode)
            {
                ((ViewPageNode)this).currentLinkedViewStateNode = null;
            }
            if (this is ViewStateNode)
            {
                ((ViewStateNode)this).currentLinkedViewPageNode.Clear(); ;
            }
        }
        protected void GenerateRightBtnMenu()
        {

            GenericMenu genericMenu = new GenericMenu();

            genericMenu.AddItem(new GUIContent(this is ViewStateNode ? "Link to ViewPage" : "Link to ViewState"), false,
                () =>
                {
                    OnConnect?.Invoke(this);
                }
            );

            genericMenu.AddItem(new GUIContent("Remove Node"), false,
                () =>
                {
                    _OnNodeDelete();
                }
            );
            if (this is ViewPageNode)
            {
                var vpn = this as ViewPageNode;
                genericMenu.AddItem(new GUIContent("Disconnect from ViewState"), false,
                    () =>
                    {
                        vpn.OnDisConnect?.Invoke(vpn);
                    }
                );
            }
            if (nodeType == NodeType.Overlay)
            {
                genericMenu.AddItem(new GUIContent("Conver to FullPage"), false,
                    () =>
                    {
                        nodeType = NodeType.FullPage;
                        SetupNode();
                    }
                );
            }
            if (nodeType == NodeType.FullPage)
            {
                genericMenu.AddItem(new GUIContent("Conver to Overlay"), false,
                    () =>
                    {
                        nodeType = NodeType.Overlay;
                        SetupNode();
                    }
                );
            }
            if (this is ViewPageNode)
            {
                var vpn = this as ViewPageNode;
                genericMenu.AddItem(new GUIContent("Duplicate"), false,
                    () =>
                    {
                        if (ViewSystemVisualEditor.Instance != null)
                        {
                            var newPageNode = ViewSystemVisualEditor.Instance.AddViewPageNode(new Vector2(vpn.rect.x + 5, vpn.rect.y + 5), vpn.nodeType == NodeType.Overlay);
                            var json = JsonUtility.ToJson(vpn.viewPage);
                            JsonUtility.FromJsonOverwrite(json, newPageNode.viewPage);
                            newPageNode.viewPage.name = newPageNode.viewPage.name + "(Clone)";
                            newPageNode.rect = new Rect(newPageNode.rect.x + 50, newPageNode.rect.y + 50, newPageNode.rect.width, newPageNode.rect.height);
                        }
                    }
                );
            }
            genericMenu.ShowAsContext();
        }
        public virtual void OnNodeConnect(ViewSystemNode node)
        {
            // CurrentConnectLine.Add(line);
            if (this is ViewPageNode)
            {
                ((ViewPageNode)this).viewPage.viewState = ((ViewStateNode)node).viewState.name;
            }
            if (this is ViewStateNode)
            {
                ((ViewPageNode)node).viewPage.viewState = ((ViewStateNode)this).viewState.name;
            }
        }

        public virtual void Draw(bool highlight) { }
        // protected string nodeStyleString = "";
        public static GUIStyle stateNode;
        public static GUIStyle stateNode_on;
        public static GUIStyle fullPageNode;
        public static GUIStyle fullPageNode_on;
        public static GUIStyle overlayPageNode;
        public static GUIStyle overlayPageNode_on;

        GUIStyle currentStyle
        {
            get
            {
                switch (nodeType)
                {
                    case NodeType.FullPage:
                        {
                            if (fullPageNode == null)
                            {
                                fullPageNode = new GUIStyle("flow node 3");
                            }
                            return fullPageNode;
                        }
                    case NodeType.Overlay:
                        {
                            if (overlayPageNode == null)
                            {
                                overlayPageNode = new GUIStyle("flow node 5");
                            }
                            return overlayPageNode;
                        }
                    case NodeType.ViewState:
                        {
                            if (stateNode == null)
                            {
                                stateNode = new GUIStyle("flow node 1");
                            }
                            return stateNode;
                        }
                }
                return null;
            }
        }
        GUIStyle currentStyle_on
        {
            get
            {
                switch (nodeType)
                {
                    case NodeType.FullPage:
                        {
                            if (fullPageNode_on == null)
                            {
                                fullPageNode_on = new GUIStyle("flow node 3 on");
                            }
                            return fullPageNode_on;
                        }
                    case NodeType.Overlay:
                        {
                            if (overlayPageNode_on == null)
                            {
                                overlayPageNode_on = new GUIStyle("flow node 5 on");
                            }
                            return overlayPageNode_on;
                        }
                    case NodeType.ViewState:
                        {
                            if (stateNode_on == null)
                            {
                                stateNode_on = new GUIStyle("flow node 1 on");
                            }
                            return stateNode_on;
                        }
                }
                return null;
            }
        }

        public virtual void SetupNode() { }
        private Vector2 editorViewPortScroll => ViewSystemVisualEditor.viewPortScroll;
        protected void DrawNode(string _name, bool highlight)
        {
            this.name = _name;
            drawRect = rect;
            SnapToGrid_DrawRect();

            drawRect.x += editorViewPortScroll.x;
            drawRect.y += editorViewPortScroll.y;


            if (nodeType == ViewStateNode.NodeType.ViewState)
            {
                GUI.depth = -1;
            }
            if (highlight)
            {
                GUI.Box(drawRect, "", new GUIStyle("LightmapEditorSelectedHighlight"));

                GUI.Box(new Rect(drawRect.x - 1, drawRect.y - 1, drawRect.width + 2, drawRect.height + 2), "", new GUIStyle("LightmapEditorSelectedHighlight"));
                GUI.Box(new Rect(drawRect.x - 2, drawRect.y - 2, drawRect.width + 4, drawRect.height + 4), "", new GUIStyle("LightmapEditorSelectedHighlight"));
                GUI.Box(new Rect(drawRect.x - 3, drawRect.y - 3, drawRect.width + 6, drawRect.height + 6), "", new GUIStyle("LightmapEditorSelectedHighlight"));
            }

            if (isSelect)
            {
                GUI.Box(drawRect, "", currentStyle_on);
            }
            else
            {
                GUI.Box(drawRect, "", currentStyle);
            }

            //Ttiel
            if (!string.IsNullOrEmpty(name))
            {
                if (name.Length > 15) titleStyle = Drawer.smallLableStyle;
                else titleStyle = Drawer.bigLableStyle;
                GUI.Label(new Rect(drawRect.x, drawRect.y + 5, drawRect.width, 16), name, titleStyle);
            }

            //Type Bar
            GUI.Label(new Rect(drawRect.x, drawRect.y + drawRect.height - 20, drawRect.width, 16), "  " + nodeType.ToString(), TextBarStyle);

            if (ProcessEvents(Event.current))
            {
                GUI.changed = true;
            }

            if (HasOverride())
            {
                GUI.Label(new Rect(drawRect.x + drawRect.width - 20, drawRect.y, 20, 20), new GUIContent(Drawer.overridePopupIcon, "This page or state has override."));
            }
            if (HasBreakPoint())
            {
                GUI.Label(new Rect(drawRect.x + drawRect.width - 20, drawRect.y + 15f, 20, 20), new GUIContent(Drawer.breakPointIcon, "This page or state has override."));
            }
            if (HasTransformOverride())
            {
                GUI.Label(new Rect(drawRect.x, drawRect.y, 20, 20), new GUIContent(Drawer.transformIcon, "The root Transform of this page or state  override, please notice this override may conflic with RectTransform setting on ViewPage."));
            }
        }

        public virtual bool HasOverride()
        {
            return false;
        }
        public virtual bool HasBreakPoint()
        {
            return false;
        }
        public virtual bool HasTransformOverride()
        {
            return false;
        }

        public void Drag(Vector2 delta)
        {
            rect = new Rect(rect.x + delta.x, rect.y + delta.y, rect.width, rect.height);
        }

        public void SnapToGrid_Rect()
        {
            float gridSize = 16;
            rect.x = Mathf.FloorToInt(rect.x / gridSize) * gridSize;
            rect.y = Mathf.FloorToInt(rect.y / gridSize) * gridSize;
        }

        public void SnapToGrid_DrawRect()
        {
            float gridSize = 16;
            drawRect.x = Mathf.FloorToInt(drawRect.x / gridSize) * gridSize;
            drawRect.y = Mathf.FloorToInt(drawRect.y / gridSize) * gridSize;
        }

        public bool isSelect = false;
        public bool IsInactivable
        {
            get
            {
                return ViewSystemVisualEditor.Instance.IsNodeInactivable;
            }
        }
        bool rightBtn = false;
        public bool ProcessEvents(Event e)
        {
            if (IsInactivable == false)
            {
                return false;
            }
            bool isMouseInNode = drawRect.Contains(e.mousePosition);

            switch (e.type)
            {
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Delete)
                    {
                        if (OnNodeDelete != null && isSelect)
                        {
                            _OnNodeDelete();
                        }
                        GUI.changed = true;
                        return true;
                    }
                    break;
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        rightBtn = false;

                        if (isMouseInNode)
                        {
                            if (OnNodeSelect != null) OnNodeSelect(this);
                            isSelect = true;
                            GUI.changed = true;
                        }
                        else
                        {
                            if (e.control)
                            {
                                return true;
                            }
                            isSelect = false;
                            GUI.changed = true;

                        }
                    }
                    if (e.button == 1)
                    {
                        if (isMouseInNode)
                        {
                            rightBtn = true;
                            GenerateRightBtnMenu();
                            e.Use();
                            GUI.changed = true;
                        }
                        else
                        {
                            isSelect = false;
                            GUI.changed = true;
                        }
                    }
                    break;
                case EventType.MouseUp:
                    if (e.button == 0 && isSelect)
                    {
                        if (rightBtn)
                        {
                            rightBtn = false;
                            break;
                        }

                        SnapToGrid_Rect();
                        if (this is ViewStateNode)
                        {
                            if (e.alt)
                            {
                                return true;
                            }
                            foreach (var item in ((ViewStateNode)this).currentLinkedViewPageNode)
                            {
                                item.SnapToGrid_Rect();
                            }
                        }
                        return true;
                    }
                    break;
                case EventType.MouseDrag:
                    if (e.button == 0 && isSelect)
                    {
                        if (rightBtn)
                        {
                            rightBtn = false;
                            break;
                        }

                        Drag(e.delta);
                        if (this is ViewStateNode)
                        {
                            if (e.alt)
                            {
                                return true;
                            }
                            foreach (var item in ((ViewStateNode)this).currentLinkedViewPageNode)
                            {
                                item.Drag(e.delta);
                            }
                        }
                        return true;
                    }
                    break;
            }

            return false;
        }
    }
    public class ViewPageNode : ViewSystemNode
    {
        // ViewPageNode 只能連一條線
        public ViewStateNode currentLinkedViewStateNode;
        public Action<ViewStateNode, ViewPageNode> OnNodeTypeConvert;
        public Action<ViewPage> OnPreviewBtnClick;
        public System.Action<ViewPageNode> OnDisConnect;

        public ViewPageNode(Vector2 mousePosition, bool isOverlay, Action<ViewSystemNode> OnConnect, Action<ViewStateNode, ViewPageNode> OnNodeTypeConvert, ViewPage viewPage)
        : base(OnConnect)
        {
            if (viewPage == null)
            {
                this.viewPage = new ViewPage();
                if (isOverlay)
                {
                    this.viewPage.viewPageType = ViewPage.ViewPageType.Overlay;

                    // at least greater than 1 to avoid conflict with FullPage
                    this.viewPage.canvasSortOrder = 1;
                }
            }
            else
            {
                this.viewPage = viewPage;
            }

            this.rect = new Rect(mousePosition.x, mousePosition.y, ViewSystemNodeWidth, ViewPageNodeHeight);
            this.nodeType = isOverlay ? NodeType.Overlay : NodeType.FullPage;
            this.OnNodeTypeConvert = OnNodeTypeConvert;
            SetupNode();
        }

        public override void SetupNode()
        {
            OnNodeTypeConvert(currentLinkedViewStateNode, this);
            currentLinkedViewStateNode = null;
            var isOverlay = nodeType == NodeType.Overlay;
            this.viewPage.viewPageType = isOverlay ? ViewPage.ViewPageType.Overlay : ViewPage.ViewPageType.FullPage;
        }

        void SnapToGrid_DrawRect()
        {
            float gridSize = 16;
            drawRect.x = Mathf.FloorToInt(drawRect.x / gridSize) * gridSize;
            drawRect.y = Mathf.FloorToInt(drawRect.y / gridSize) * gridSize;
        }
        public override void Draw(bool highlight)
        {
            DrawNode(viewPage.name, highlight);
            var btnRect = new Rect(drawRect.x, drawRect.y + drawRect.height - 40, drawRect.width, 18);

            bool btnInteractiable = IsInactivable;
            var btnText = "Preview";
            if (Application.isPlaying)
            {
                btnText = "Force Show Page";

                if (ViewController.Instance != null && ViewController.Instance.IsOverPageLive(viewPage.name))
                {
                    btnText = "Force Leave Page";
                }
            }
            if (CustomElement.Button(id, btnRect, new GUIContent(btnText), new GUIStyle("ObjectPickerResultsEven"), btnInteractiable))
            {
                OnPreviewBtnClick?.Invoke(viewPage);
                isSelect = false;
            }
        }
        public override bool HasOverride()
        {
            if (viewPage == null)
            {
                return false;
            }
            foreach (var item in viewPage.viewPageItems)
            {
                if (item.overrideDatas?.Count > 0 ||
                   item.eventDatas?.Count > 0 ||
                   item.navigationDatas?.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public override bool HasBreakPoint()
        {
            if (viewPage == null)
            {
                return false;
            }
            foreach (var item in viewPage.viewPageItems)
            {
                if (item.breakPointViewElementTransforms != null &&
                    item.breakPointViewElementTransforms?.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public override bool HasTransformOverride()
        {
            if (viewPage == null)
            {
                return false;
            }
            foreach (var item in viewPage.viewPageItems)
            {
                if (item.overrideDatas.Where(m => string.IsNullOrEmpty(m.targetTransformPath) && m.targetComponentType.ToLower().Contains("transform")).Count() > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public ViewPage viewPage;
    }

    public class ViewStateNode : ViewSystemNode
    {
        // ViewStateNode 可以連到多個 ViewPageNode
        public List<ViewPageNode> currentLinkedViewPageNode = new List<ViewPageNode>();
        public ViewStateNode(Vector2 mousePosition, Action<ViewSystemNode> OnConnect, ViewState viewState)
        : base(OnConnect)
        {
            if (viewState == null)
            {
                this.viewState = new ViewState();
            }
            else
            {
                this.viewState = viewState;
            }
            this.rect = new Rect(mousePosition.x, mousePosition.y, ViewSystemNodeWidth, ViewStateNodeHeight);
            this.nodeType = NodeType.ViewState;
        }

        public override void Draw(bool highlight)
        {
            DrawNode(viewState.name, highlight);
        }
        public override bool HasOverride()
        {
            if (viewState == null)
            {
                return false;
            }
            foreach (var item in viewState.viewPageItems)
            {
                if (item.overrideDatas?.Count > 0 ||
                   item.eventDatas?.Count > 0 ||
                   item.navigationDatas?.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public override bool HasBreakPoint()
        {
            if (viewState == null)
            {
                return false;
            }
            foreach (var item in viewState.viewPageItems)
            {
                if (item.breakPointViewElementTransforms != null &&
                    item.breakPointViewElementTransforms?.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public override bool HasTransformOverride()
        {
            if (viewState == null)
            {
                return false;
            }
            foreach (var item in viewState.viewPageItems)
            {
                if (item.overrideDatas.Where(m => string.IsNullOrEmpty(m.targetTransformPath) && m.targetComponentType.ToLower().Contains("transform")).Count() > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public ViewState viewState;
    }
}