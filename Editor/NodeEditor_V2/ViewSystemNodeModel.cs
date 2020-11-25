using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNode
    {
        public const int ViewSystemNodeWidth = 160;
        public const int ViewStateNodeHeight = 60;
        public const int ViewPageNodeHeight = 80;
        protected int id;
        List<ViewSystemNodeLine> CurrentConnectLine = new List<ViewSystemNodeLine>();
        //public ViewSystemNodeLinker nodeConnectionLinker;
        public enum NodeType
        {
            FullPage, Overlay, ViewState,
        }
        public NodeType nodeType;
        public System.Action<IEnumerable<ViewSystemNodeLine>> OnNodeDelete;
        public System.Action<ViewSystemNode> OnNodeSelect;
        public Action<ViewSystemNode> OnConnect;
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
            if (OnNodeDelete != null)
            {
                OnNodeDelete(CurrentConnectLine);
            }
            CurrentConnectLine.Clear();

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
            genericMenu.ShowAsContext();
        }
        public virtual void OnNodeConnect(ViewSystemNode node, ViewSystemNodeLine line)
        {
            CurrentConnectLine.Add(line);
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
        private Vector2 editorViewPortScroll => ViewSystemNodeEditor.viewPortScroll;
        protected void DrawNode(string _name, bool highlight)
        {
            this.name = _name;
            drawRect = rect;
            drawRect.x += editorViewPortScroll.x;
            drawRect.y += editorViewPortScroll.y;

            //Draw Linker
            //if (nodeConnectionLinker != null) nodeConnectionLinker.Draw(drawRect);

            if (nodeType == ViewStateNode.NodeType.ViewState)
            {
                GUI.depth = -1;
            }
            if (highlight)
            {
                GUI.Box(new Rect(drawRect.x - 1, drawRect.y - 1, drawRect.width + 2, drawRect.height + 2), "", new GUIStyle("LightmapEditorSelectedHighlight"));
            }

            if (isSelect)
            {
                //GUI.Box(drawRect, "", new GUIStyle(nodeStyleString + " on"));
                GUI.Box(drawRect, "", currentStyle_on);
            }
            else
            {
                //GUI.Box(drawRect, "", new GUIStyle(nodeStyleString));
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
                GUI.Label(new Rect(drawRect.x + drawRect.width - 30, drawRect.y - 15f, 30, 30), new GUIContent(Drawer.overrideIcon, "This page or state has override."));
            }
        }

        public virtual bool HasOverride()
        {
            return false;
        }
        public void Drag(Vector2 delta)
        {
            rect = new Rect(rect.x + delta.x, rect.y + delta.y, rect.width, rect.height);
        }

        public bool isSelect = false;
        public bool IsInactivable
        {
            get
            {
                return ViewSystemNodeEditor.Instance.IsNodeInactivable;
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
            // if (isSelect && !string.IsNullOrEmpty(name))
            // {
            //     GUI.Label(new Rect(rect.x, rect.y - 15, 20, 10), "", new GUIStyle("sv_label_1"));
            // }
            return false;
        }
    }
    public class ViewPageNode : ViewSystemNode
    {
        // ViewPageNode 只能連一條線
        public ViewStateNode currentLinkedViewStateNode;
        public Action<ViewStateNode, ViewPageNode> OnNodeTypeConvert;
        public Action<ViewPage> OnPreviewBtnClick;

        public ViewPageNode(Vector2 mousePosition, bool isOverlay, Action<ViewSystemNode> OnConnect, Action<ViewStateNode, ViewPageNode> OnNodeTypeConvert, ViewPage viewPage)
        : base(OnConnect)
        {
            if (viewPage == null)
            {
                this.viewPage = new ViewPage();
                if (isOverlay)
                {
                    this.viewPage.viewPageType = ViewPage.ViewPageType.Overlay;
                }
            }
            else
            {
                this.viewPage = viewPage;
            }
            this.rect = new Rect(mousePosition.x, mousePosition.y, ViewSystemNodeWidth, ViewPageNodeHeight);
            this.nodeType = isOverlay ? NodeType.Overlay : NodeType.FullPage;
            //this.nodeConnectionLinker = new ViewSystemNodeLinker(this, OnConnect);
            this.OnNodeTypeConvert = OnNodeTypeConvert;
            SetupNode();
        }

        public override void SetupNode()
        {
            OnNodeTypeConvert(currentLinkedViewStateNode, this);
            currentLinkedViewStateNode = null;
            var isOverlay = nodeType == NodeType.Overlay;
            // if (isOverlay)
            // {
            //     this.viewPage.viewState = "";
            // }
            this.viewPage.viewPageType = isOverlay ? ViewPage.ViewPageType.Overlay : ViewPage.ViewPageType.FullPage;
            //this.nodeConnectionLinker.type = isOverlay ? ConnectionPointType.Overlay : ConnectionPointType.FullPage;
            // switch (nodeType)
            // {
            //     case NodeType.FullPage:
            //         nodeStyleString = "flow node 3";
            //         break;
            //     case NodeType.Overlay:
            //         nodeStyleString = "flow node 5";
            //         break;
            // }
        }
        public override void Draw(bool highlight)
        {
            DrawNode(viewPage.name, highlight);
            var btnRect = new Rect(drawRect.x, drawRect.y + drawRect.height - 40, drawRect.width, 18);

            bool btnInteractiable = IsInactivable;
            if (Application.isPlaying)
            {
                btnInteractiable = ViewSystemNodeEditor.allowPreviewWhenPlaying && IsInactivable;
            }
            if (CustomElement.Button(id, btnRect, new GUIContent("Preview"), new GUIStyle("ObjectPickerResultsEven"), btnInteractiable))
            {
                if (OnPreviewBtnClick != null)
                {
                    OnPreviewBtnClick(viewPage);
                }
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
            //this.nodeConnectionLinker = new ViewSystemNodeLinker(this, OnConnect);
            // nodeStyleString = "flow node 1";
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
        public ViewState viewState;
    }

    //ViewSystemNodeLinker is no longer used
    public enum ConnectionPointType { State, FullPage, Overlay }
    /// <summary>
    /// ViewSystemNodeConnectionPoint 代表一個 Node 上的一個連結點
    /// 一個連結點可以使用 ViewSystemNodeConnectionLine 來進行連線
    /// </summary>
    public class ViewSystemNodeLinker
    {
        //連結點所屬的節點
        public ViewSystemNode viewSystemNode;
        // const int ConnectNodeWidth = 28;
        // const int ConnectNodeHeight = 28;
        //public Rect rect;
        //public ConnectionPointType type;
        //public GUIStyle style = GUIStyle.none;
        //public GUIContent content;
        public Action<ViewSystemNode> OnConnectionPointClick;

        public ViewSystemNodeLinker(ViewSystemNode viewSystemNode, Action<ViewSystemNode> OnConnectionPointClick)
        {
            //this.type = type;
            this.OnConnectionPointClick = OnConnectionPointClick;
            this.viewSystemNode = viewSystemNode;
        }
        public void Draw(Rect target)
        {
            // switch (type)
            // {
            //     case ConnectionPointType.State:
            //         rect = new Rect(target.x + target.width * 0.5f - ConnectNodeWidth * 0.5f, target.y + target.height - ConnectNodeHeight * 0.5f, ConnectNodeWidth, ConnectNodeHeight);

            //         content = new GUIContent(EditorGUIUtility.FindTexture("sv_icon_dot9_pix16_gizmo"));
            //         //winbtn_mac_min
            //         break;
            //     case ConnectionPointType.FullPage:
            //         rect = new Rect(target.x + target.width * 0.5f - ConnectNodeWidth * 0.5f, target.y - ConnectNodeHeight * 0.5f, ConnectNodeWidth, ConnectNodeHeight);
            //         content = new GUIContent(EditorGUIUtility.FindTexture("sv_icon_dot11_pix16_gizmo"));
            //         break;
            //     case ConnectionPointType.Overlay:
            //         rect = new Rect(target.x + target.width * 0.5f - ConnectNodeWidth * 0.5f, target.y - ConnectNodeHeight * 0.5f, ConnectNodeWidth, ConnectNodeHeight);
            //         content = new GUIContent(EditorGUIUtility.FindTexture("sv_icon_dot13_pix16_gizmo"));
            //         break;
            // }
            // GUI.depth = -2;
            // if (GUI.Button(rect, content, style))
            // {
            //     if (viewSystemNode.IsInactivable == false) return;
            //     if (OnConnectionPointClick != null) OnConnectionPointClick(viewSystemNode);
            // }
        }
    }

    public class ViewSystemNodeLine
    {
        Color _lineColor;

        Color lineColor
        {
            get
            {
                if (_lineColor == null)
                {
                    _lineColor = new Color(0.34f, 0.56f, 0.92f, 1);
                }
                return _lineColor;
            }
        }
        public ViewStateNode viewStateNode;
        public ViewPageNode viewPageNode;
        private Action<ViewSystemNodeLine> OnRemoveConnectionClick;
        public ViewSystemNodeLine(ViewStateNode viewStateNode, ViewPageNode viewPageNode, Action<ViewSystemNodeLine> OnRemoveConnectionClick)
        {
            this.viewStateNode = viewStateNode;
            this.viewPageNode = viewPageNode;
            this.OnRemoveConnectionClick = OnRemoveConnectionClick;
        }
        public bool IsInactivable
        {
            get
            {
                return ViewSystemNodeEditor.Instance.IsNodeInactivable;
            }
        }
        bool isSelect = false;
        public void Draw()
        {
            Vector3 startPoint = viewPageNode.drawRect.center;
            startPoint.y -= viewPageNode.rect.height * 0.5f;
            Vector3 endPoint = viewStateNode.drawRect.center;
            endPoint.y += viewStateNode.rect.height * 0.5f;

            Handles.DrawBezier(
                startPoint,
                endPoint,
                startPoint,
                endPoint,
                Color.gray,
                null,
                5f
            );

            if (GUI.Button(new Rect(startPoint.x - 8, startPoint.y - 12, 16, 16), EditorGUIUtility.FindTexture("d_winbtn_mac_close_h"), GUIStyle.none))
            {
                if (!IsInactivable)
                {
                    return;
                }
                if (!ViewSystemNodeEditor.removeConnectWithoutAsk)
                {
                    if (!EditorUtility.DisplayDialog("Warning",
                                        "Do you really wants to break the connect between ViewState and ViewPage?", "Yes", "No"))
                    {
                        return;
                    }
                }

                viewPageNode.viewPage.viewState = "";
                if (OnRemoveConnectionClick != null)
                {
                    OnRemoveConnectionClick(this);
                }
            }
        }
    }
}