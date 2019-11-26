using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeWindow
    {
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