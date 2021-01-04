using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;
using System;
using System.Linq;
namespace MacacaGames.ViewSystem.VisualEditor
{
    public class ViewBreakpointWindow : ViewSystemNodeWindow
    {
        static ViewSystemSaveData saveData => ViewSystemVisualEditor.saveData;

        public ViewBreakpointWindow(string name, ViewSystemVisualEditor editor)
        : base(name, editor)
        {
        }
        public void RebuildWindow(ViewPageItem viewPageItem)
        {
            this.viewPageItem = viewPageItem;
            reorderableList = null;
            reorderableList = new ReorderableList(viewPageItem.breakPointViewElementTransforms, typeof(List<BreakPointViewElementTransform>), true, true, true, true);
            reorderableList.drawElementCallback += DrawViewItemElement;
            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight * 6f;
            reorderableList.onAddCallback += AddItem;
            reorderableList.elementHeightCallback += ElementHight;
        }

        ViewPageItem viewPageItem;
        Vector2 scrollPos;

        private void AddItem(ReorderableList list)
        {
            // this is ugly but there are a lot of cases like null types and default constructors
            var elementType = list.list.GetType().GetElementType();
            if (elementType == typeof(string))
                list.index = list.list.Add("");
            else if (elementType != null && elementType.GetConstructor(Type.EmptyTypes) == null)
                Debug.LogError("Cannot add element. Type " + elementType + " has no default constructor. Implement a default constructor or implement your own add behaviour.");
            else if (list.list.GetType().GetGenericArguments()[0] != null)
                list.index = list.list.Add(Activator.CreateInstance(list.list.GetType().GetGenericArguments()[0]));
            else if (elementType != null)
                list.index = list.list.Add(Activator.CreateInstance(elementType));
            else
                Debug.LogError("Cannot add element of type Null.");

        }

        private void DrawViewItemElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect.height = EditorGUIUtility.singleLineHeight;
            var item = viewPageItem.breakPointViewElementTransforms[index];
            var cRect = rect;
            cRect.width = 80;
            cRect.x = rect.width - 80 + rect.x;
            if (GUI.Button(cRect, "Setting"))
            {
                ViewSystemVisualEditor.globalSettingWindow.show = true;
            }
            cRect.x = rect.x;
            cRect.width = rect.width - 80;
            if (EditorGUI.DropdownButton(cRect, new GUIContent($"{item.breakPointName}"), FocusType.Keyboard))
            {
                GenericMenu menu = new GenericMenu();
                foreach (var bp in saveData.globalSetting.breakPoints)
                {
                    menu.AddItem(new GUIContent(bp), viewPageItem.breakPointViewElementTransforms[index].breakPointName == bp, () =>
                    {
                        viewPageItem.breakPointViewElementTransforms[index].breakPointName = bp;
                    });
                }
                menu.ShowAsContext();
            }

            rect.y += EditorGUIUtility.singleLineHeight;
            ViewSystemVisualEditor.inspector.DrawViewElementTransformDetail(item.transformData, viewPageItem.Id, item.breakPointName, viewPageItem.previewViewElement, rect);
        }

        float ElementHight(int index)
        {
            return GetHeight();
            float GetHeight()
            {
                return EditorGUIUtility.singleLineHeight * 7.5f + (EditorGUIUtility.singleLineHeight * 3 + 6) + (EditorGUIUtility.singleLineHeight * 2 + 8);
            }
        }


        ReorderableList reorderableList;
        float height = EditorGUIUtility.singleLineHeight * 7.5f + (EditorGUIUtility.singleLineHeight * 3 + 6) + (EditorGUIUtility.singleLineHeight * 2 + 8);
        public override void Draw(int id)
        {
            if (!show)
            {
                return;
            }
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
            {
                reorderableList.DoLayoutList();
                //                    OnDrawItem?.Invoke(item.transformData, viewPageItem.Id, item.breakPointName, viewPageItem.previewViewElement, new Rect());

                scrollPos = scroll.scrollPosition;
            }
            if (GUILayout.Button("Close"))
            {
                Hide();
            }
            base.Draw(id);
        }
    }

}
