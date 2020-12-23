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
    public class ViewPageOrderWindow : ViewSystemNodeWindow
    {

        ViewSystemDataReaderV2 dataReader;

        static ViewSystemSaveData saveData => ViewSystemNodeEditor.saveData;
        public ViewPageOrderWindow(string name, ViewSystemNodeEditor editor, ViewSystemDataReaderV2 dataReader)
        : base(name, editor)
        {
            this.dataReader = dataReader;
            RebuildList();
        }
        ReorderableList viewPageOrderList;
        Vector2 scrollPosition;

        public override void Draw(int id)
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                viewPageOrderList.DoLayoutList();
                scrollPosition = scroll.scrollPosition;
            }

            if (GUILayout.Button("Apply"))
            {
                ApplySortOrder();
            }
            base.Draw(id);
        }

        void ApplySortOrder()
        {
            int order = overlayPageNames.Count + 1;
            var saveData = dataReader.GetSaveData();
            foreach (var item in overlayPageNames)
            {
                var viewPage = saveData.viewPages.FirstOrDefault(m => m.viewPage.name == item);
                if (viewPage == null)
                {
                    continue;
                }
                viewPage.viewPage.canvasSortOrder = order;
                order--;
            }
            dataReader.Save();
        }

        List<string> overlayPageNames = new List<string>();

        void RebuildList()
        {
            var overlayPages = dataReader.GetSaveData().viewPages.Where(m => m.viewPage.viewPageType == ViewPage.ViewPageType.Overlay);
            overlayPageNames = overlayPages.OrderByDescending(m => m.viewPage.canvasSortOrder).Select(m => m.viewPage.name).ToList();
            viewPageOrderList = null;
            viewPageOrderList = new ReorderableList(overlayPageNames, typeof(List<string>), true, true, false, false);
            viewPageOrderList.drawElementCallback += DrawViewItemElement;
            viewPageOrderList.drawHeaderCallback += DrawHeaderCallback;
            viewPageOrderList.elementHeight = EditorGUIUtility.singleLineHeight;
        }

        private void DrawHeaderCallback(Rect rect)
        {
            GUI.Label(rect, "Change the sortOrder by move the item, top is greater");
        }

        private void DrawViewItemElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            GUI.Label(rect, overlayPageNames[index]);
        }
    }
}
