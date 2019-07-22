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

        public void OnViewPageAdd(ViewPageNode node)
        {
            data.viewPages.Add(new ViewSystemSaveData.ViewPageSaveData(new Vector2(node.rect.x, node.rect.y), node.viewPage));
        }
        public void OnViewStateAdd(ViewStateNode node)
        {
            data.viewStates.Add(new ViewSystemSaveData.ViewStateSaveData(new Vector2(node.rect.x, node.rect.y), node.viewState));
        }

        public void OnViewPageDelete(ViewPageNode node)
        {
            var s = data.viewPages.SingleOrDefault(m => m.viewPage == node.viewPage);
            data.viewPages.Remove(s);
        }


        public void OnViewStateDelete(ViewStateNode node)
        {
            var s = data.viewStates.SingleOrDefault(m => m.viewState == node.viewState);
            data.viewStates.Remove(s);
        }

        public void OnViewPagePreview(ViewPage viewPage)
        {
            //throw new System.NotImplementedException();
        }
        public void Normalized()
        {
            //throw new System.NotImplementedException();
        }

        public void Save(List<ViewPageNode> viewPageNodes, List<ViewStateNode> viewStateNodes)
        {

            foreach (var item in viewPageNodes)
            {
                var vp = data.viewPages.SingleOrDefault(m => m.viewPage.name == item.viewPage.name);
                vp.nodePosition = new Vector2(item.rect.x, item.rect.y);
            }

            foreach (var item in viewStateNodes)
            {
                var vs = data.viewStates.SingleOrDefault(m => m.viewState.name == item.viewState.name);
                vs.nodePosition = new Vector2(item.rect.x, item.rect.y);
            }
            UnityEditor.EditorUtility.SetDirty(data);
        }

        public ViewSystemSaveData GetSetting()
        {
            return data;
        }

    }

}

