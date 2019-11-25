using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeWindow
    {
        protected ViewSystemNodeEditor editor;
        protected Rect rect = new Rect(0, 0, 350, 400);

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

        public virtual void Draw(int id)
        {
            if (dragable)
            {
                GUI.DragWindow(new Rect(0, 0, editor.position.width, editor.position.height));
            }
        }


        public virtual GUIStyle GetWindowStyle()
        {
            return new GUIStyle("window");
        }


    }
}