using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemDataReaderV2 : IViewSystemDateReader
    {
        public ViewSystemDataReaderV2(ViewSystemNodeEditor editor)
        {
            this.editor = editor;
        }
        ViewSystemNodeEditor editor;

        ViewSystemSaveData data;
        public bool Init()
        {
            data = Resources.Load<ViewSystemSaveData>("ViewSystemData");

            List<ViewPageNode> viewPageNodes = new List<ViewPageNode>();
            //先整理 ViewPage Node
            foreach (var item in data.viewPages)
            {
                var isOverlay = item.viewPage.viewPageType == ViewPage.ViewPageType.Overlay;

                var node = editor.AddViewPageNode(item.nodePosition, isOverlay, item.viewPage);
                viewPageNodes.Add(node);
            }

            //在整理 ViewState Node
            foreach (var item in data.viewStates)
            {
                var vp_of_vs = viewPageNodes.Where(m => m.viewPage.viewState == item.viewState.name);

                var node = editor.AddViewStateNode(item.nodePosition, item.viewState);
                editor.CreateConnection(node);
            }

            return data ? true : false;
        }

        public void Normalized()
        {
            //throw new System.NotImplementedException();
        }

        public void OnViewPageAdd(ViewPageNode node)
        {
            //throw new System.NotImplementedException();
        }

        public void OnViewPageDelete(ViewPageNode node)
        {
            //throw new System.NotImplementedException();
        }

        public void OnViewPagePreview(ViewPage viewPage)
        {
            //throw new System.NotImplementedException();
        }

        public void OnViewStateAdd(ViewStateNode node)
        {
           // throw new System.NotImplementedException();
        }

        public void OnViewStateDelete(ViewStateNode node)
        {
            //throw new System.NotImplementedException();
        }

        public void Save(List<ViewPageNode> viewPageNodes, List<ViewStateNode> viewStateNodes)
        {
            data.viewPages.Clear();
            data.viewStates.Clear();
            foreach (var item in viewPageNodes)
            {
                data.viewPages.Add(new ViewSystemSaveData.ViewPageSaveData(new Vector2(item.rect.x, item.rect.y), item.viewPage));
            }

            foreach (var item in viewStateNodes)
            {
                data.viewStates.Add(new ViewSystemSaveData.ViewStateSaveData(new Vector2(item.rect.x, item.rect.y), item.viewState));
            }
            UnityEditor.EditorUtility.SetDirty(data);
        }
    }

}

