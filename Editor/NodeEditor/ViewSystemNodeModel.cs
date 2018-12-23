using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace CloudMacaca.ViewSystem
{
    public class ViewSystemNode
    {

        List<ViewSystemNodeLine> CurrentConnectLine = new List<ViewSystemNodeLine>();
        public ViewSystemNodeLinker nodeConnectionLinker;
        public enum NodeType
        {
            FullPage, Overlay, ViewState
        }
        public NodeType nodeType;
        public System.Action<IEnumerable<ViewSystemNodeLine>> OnNodeDelete;
        public System.Action<ViewSystemNode> OnNodeSelect;
        string name;
        protected static int currentMaxId = 0;
        public Rect rect;
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
            genericMenu.AddItem(new GUIContent("Remove node"), false,
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
        public virtual void Draw() { }
        public virtual void SetupNode() { }
        protected void DrawNode(string _name)
        {
            this.name = _name;

            if (nodeType == ViewStateNode.NodeType.FullPage || nodeType == ViewStateNode.NodeType.Overlay)
            {
            }
            if (nodeType == ViewStateNode.NodeType.ViewState)
            {
                GUI.depth = -1;
            }

            string nodeStyleString = "";
            switch (nodeType)
            {
                case NodeType.FullPage:
                    nodeStyleString = "flow node 2";
                    break;
                case NodeType.Overlay:
                    nodeStyleString = "flow node 0";
                    break;
                case NodeType.ViewState:
                    nodeStyleString = "flow node 1";
                    break;
            }

            if (isSelect)
            {
                GUI.Box(rect, "", new GUIStyle(nodeStyleString + " on"));
            }
            else
            {
                GUI.Box(rect, "", new GUIStyle(nodeStyleString));
            }

            //Ttiel
            if (!string.IsNullOrEmpty(name))
            {
                if (name.Length > 26) titleStyle = new GUIStyle("MiniLabel");
                else if (name.Length > 15) titleStyle = new GUIStyle("ControlLabel");
                else titleStyle = new GUIStyle("TL Selection H2");
                GUI.Label(new Rect(rect.x, rect.y + 5, rect.width, 16), name, titleStyle);
            }

            //Type Bar
            GUI.Label(new Rect(rect.x, rect.y + rect.height - 20, rect.width, 16), "  " + nodeType.ToString(), TextBarStyle);

            //Draw Linker
            nodeConnectionLinker.Draw(rect);

            if (ProcessEvents(Event.current))
            {
                GUI.changed = true;
            }

        }
        public void Drag(Vector2 delta)
        {
            rect = new Rect(rect.x + delta.x, rect.y + delta.y, rect.width, rect.height);
        }

        public bool isMouseInside(Vector2 mousePosition)
        {
            Debug.Log(rect);
            Debug.Log(mousePosition);
            return rect.Contains(mousePosition);
        }
        public bool isSelect = false; public bool isDragged;
        public bool ProcessEvents(Event e)
        {
            bool isMouseInNode = rect.Contains(e.mousePosition);

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
                            GenerateRightBtnMenu();
                            e.Use();
                            GUI.changed = true;
                        }
                    }
                    break;
                case EventType.MouseUp:

                    break;
                case EventType.MouseDrag:
                    if (e.button == 0 && isSelect)
                    {
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
            if (isSelect && !string.IsNullOrEmpty(name))
            {
                GUI.Label(new Rect(rect.x, rect.y - 15, Mathf.Clamp(name.Length * 9, 45, name.Length * 9), 10), name, new GUIStyle("sv_label_1"));
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

        public ViewPageNode(Vector2 mousePosition, bool isOverlay, Action<ViewSystemNode> OnConnectionPointClick, Action<ViewStateNode, ViewPageNode> OnNodeTypeConvert, ViewPage viewPage)
        {
            if (viewPage == null)
            {
                this.viewPage = new ViewPage();
            }
            else
            {
                this.viewPage = viewPage;
            }
            this.rect = new Rect(mousePosition.x, mousePosition.y, 160, 80);
            this.nodeType = isOverlay ? NodeType.Overlay : NodeType.FullPage;
            this.nodeConnectionLinker = new ViewSystemNodeLinker(nodeType == NodeType.Overlay ? ConnectionPointType.None : ConnectionPointType.Left, this, OnConnectionPointClick);
            this.OnNodeTypeConvert = OnNodeTypeConvert;
        }
        public override void SetupNode()
        {
            OnNodeTypeConvert(currentLinkedViewStateNode, this);
            currentLinkedViewStateNode = null;
            var isOverlay = nodeType == NodeType.Overlay;
            if (isOverlay)
            {
                this.viewPage.viewState = "";
            }
            this.viewPage.viewPageType = isOverlay ? ViewPage.ViewPageType.Overlay : ViewPage.ViewPageType.FullPage;
            this.nodeConnectionLinker.type = isOverlay ? ConnectionPointType.None : ConnectionPointType.Left;
        }
        public override void Draw()
        {
            DrawNode(viewPage.name);
            var btnRect = new Rect(rect.x, rect.y + rect.height - 40, rect.width * 0.5f - 0.5f, 18);
            if (GUI.Button(btnRect, "Preview", new GUIStyle("ObjectPickerResultsEven")))
            {
                if (OnPreviewBtnClick != null)
                {
                    OnPreviewBtnClick(viewPage);
                }
            }
            btnRect.x += rect.width * 0.5f;
            btnRect.x += 1;
            if (GUI.Button(btnRect, "Highlight", new GUIStyle("ObjectPickerResultsEven")))
            {
                foreach (var item in viewPage.viewPageItem)
                {
                    EditorGUIUtility.PingObject(item.viewElement);
                }
            }
        }

        public ViewPage viewPage;
    }

    public class ViewStateNode : ViewSystemNode
    {
        // ViewStateNode 可以連到多個 ViewPageNode
        public List<ViewPageNode> currentLinkedViewPageNode = new List<ViewPageNode>();
        public ViewStateNode(Vector2 mousePosition, Action<ViewSystemNode> OnConnectionPointClick, ViewState viewState)
        {
            if (viewState == null)
            {
                this.viewState = new ViewState();
            }
            else
            {
                this.viewState = viewState;
            }
            this.rect = new Rect(mousePosition.x, mousePosition.y, 160, 80);
            this.nodeType = NodeType.ViewState;
            this.nodeConnectionLinker = new ViewSystemNodeLinker(ConnectionPointType.Right, this, OnConnectionPointClick);
        }

        public override void Draw()
        {
            DrawNode(viewState.name);
        }

        public ViewState viewState;
    }


    public enum ConnectionPointType { Right, Left, None }
    /// <summary>
    /// ViewSystemNodeConnectionPoint 代表一個 Node 上的一個連結點
    /// 一個連結點可以使用 ViewSystemNodeConnectionLine 來進行連線
    /// </summary>
    public class ViewSystemNodeLinker
    {
        //連結點所屬的節點
        public ViewSystemNode viewSystemNode;
        const int ConnectNodeWidth = 20;
        const int ConnectNodeHeight = 20;
        public Rect rect;
        public ConnectionPointType type;
        public GUIStyle style;
        public GUIStyle dotStyle;
        public Action<ViewSystemNode> OnConnectionPointClick;

        Texture2D ButtonBackground;

        public ViewSystemNodeLinker(ConnectionPointType type, ViewSystemNode viewSystemNode, Action<ViewSystemNode> OnConnectionPointClick)
        {
            this.type = type;
            this.OnConnectionPointClick = OnConnectionPointClick;
            this.viewSystemNode = viewSystemNode;
        }
        public void Draw(Rect target)
        {
            var dotRect = rect;
            switch (type)
            {
                case ConnectionPointType.Right:
                    rect = new Rect(target.x, target.y + target.height * 0.5f - ConnectNodeHeight * 0.5f, ConnectNodeWidth, ConnectNodeHeight);
                    style = new GUIStyle("SearchCancelButtonEmpty");
                    dotStyle = new GUIStyle("U2D.dragDot");
                    dotRect.y += 4; dotRect.x += 2;
                    break;
                case ConnectionPointType.Left:
                    rect = new Rect(target.x + target.width, target.y + target.height * 0.5f - ConnectNodeHeight * 0.5f, ConnectNodeWidth, ConnectNodeHeight);
                    style = new GUIStyle("SearchCancelButtonEmpty");
                    dotStyle = new GUIStyle("U2D.pivotDot");
                    dotRect.y += 1.2f; dotRect.x -= 1;
                    break;
                case ConnectionPointType.None:
                    return;
            }

            GUI.depth = -2;
            if (GUI.Button(rect, "", style))
            {
                if (OnConnectionPointClick != null) OnConnectionPointClick(viewSystemNode);
            }
            GUI.Label(rect, new GUIContent(ButtonBackground, "Show Console"));
            GUI.Label(dotRect, "", dotStyle);
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

        public void Draw()
        {
            Handles.DrawBezier(
                viewPageNode.nodeConnectionLinker.rect.center,
                viewStateNode.nodeConnectionLinker.rect.center,
                viewPageNode.nodeConnectionLinker.rect.center + Vector2.up * 20f,
                viewStateNode.nodeConnectionLinker.rect.center - Vector2.up * 20f,
                Color.blue,
                null,
                4f
            );
            var pos = (viewPageNode.nodeConnectionLinker.rect.center + viewStateNode.nodeConnectionLinker.rect.center) * 0.5f;
            if (GUI.Button(new Rect(pos.x - 8, pos.y - 8, 16, 16), EditorGUIUtility.FindTexture("d_winbtn_mac_close_h"), GUIStyle.none))
            {
                viewPageNode.viewPage.viewState = "";
                if (OnRemoveConnectionClick != null)
                {
                    OnRemoveConnectionClick(this);
                }
            }
        }
    }

}