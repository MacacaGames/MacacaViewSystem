using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace MacacaGames.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeWindow
    {
        protected bool resizeable;
        protected ViewSystemNodeEditor editor;
        private Rect _rect;
        protected Rect rect
        {
            get
            {
                return _rect;
            }
            set
            {
                _rect = new Rect(value.x, value.y, GetWindowSize.x, GetWindowSize.y);
            }
        }

        public bool show;
        public string name;

        public bool dragable = true;
        public ViewSystemNodeWindow(string name, ViewSystemNodeEditor editor)
        {
            this.editor = editor;
            this.name = name;
            rect = new Rect(editor.position.width * 0.3f,
                            editor.position.height * 0.3f,
                            rect.width,
                            rect.height);
            show = false;
        }

        public virtual void OnGUI()
        {
            if (!show)
            {
                return;
            }
            rect = GUILayout.Window(this.GetHashCode(), rect, Draw, name, GetWindowStyle());
        }
        public void Show()
        {
            show = true;
        }

        public void Hide()
        {
            show = false;
        }

        public virtual void Draw(int id)
        {
            if (dragable)
            {
                GUI.DragWindow(new Rect(0, 0, editor.position.width, editor.position.height));
            }
            // if (resizeable)
            // {
            //     DrawResizeBtn();
            // }
        }
        bool resizeBarPressed = false;
        void DrawResizeBtn()
        {
            GUI.depth = 100;
            var ResizeBarRect = new Rect(rect.width - 8, rect.height - 8, 8, 8);
            EditorGUIUtility.AddCursorRect(ResizeBarRect, MouseCursor.ResizeUpLeft);

            GUI.Box(ResizeBarRect, "");
            if (ResizeBarRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    resizeBarPressed = true;
                    dragable = false;
                }
            }
            if (Event.current.type == EventType.MouseUp)
            {
                resizeBarPressed = false;
                dragable = true;
            }
            if (resizeBarPressed && Event.current.type == EventType.MouseDrag)
            {
                rect = new Rect(rect.x, rect.y, rect.width + Event.current.delta.x,
                    rect.height + Event.current.delta.y);
                Event.current.Use();
                GUI.changed = true;
            }
        }
        public virtual Vector2 GetWindowSize
        {
            get
            {
                return new Vector2(350, 400);
            }
        }

        public virtual GUIStyle GetWindowStyle()
        {
            return new GUIStyle("window");
        }
    }
}