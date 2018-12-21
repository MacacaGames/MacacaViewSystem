using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
namespace CloudMacaca.ViewSystem
{
    interface ViewSystemDateReader
    {
        bool Init();
        void OnViewPageDelete(ViewPageNode node);
        void OnViewStateDelete(ViewStateNode node);
        void OnViewPageAdd(ViewPageNode node);
        void OnViewStateAdd(ViewStateNode node);
    }
    public class ViewSystemDateReaderV1 : ScriptableObject, ViewSystemDateReader
    {
        public ViewSystemDateReaderV1(ViewSystemNodeEditor editor)
        {
            this.editor = editor;
        }
        ViewSystemNodeEditor editor;
        List<ViewPageNode> viewPageNode = new List<ViewPageNode>();
        List<ViewStateNode> viewStateNode = new List<ViewStateNode>();
        List<ViewSystemNodeLine> viewSystemNodeConnectionLines = new List<ViewSystemNodeLine>();
        static ViewController _viewController;
        static ViewController viewController
        {
            get
            {
                if (_viewController == null)
                    _viewController = FindObjectOfType<ViewController>();
                return _viewController;
            }
        }


        public bool Init()
        {
            if (viewController == null)
            {
                Debug.LogError("ViewController is null");
                return false;
            }

            //One node width is 160

            int posX = 0;
            int posY = 100;
            int overlayPosY = 100;
            int notSetPosY = 100;
            List<ViewPageNode> viewPageNodes = new List<ViewPageNode>();
            //先整理 ViewPage Node
            var vps = viewController.viewPage.OrderBy(m => m.viewState);
            foreach (var item in vps)
            {
                int finalY = 0;
                var isOverlay = item.viewPageType == ViewPage.ViewPageType.Overlay;
                if (isOverlay)
                {
                    posX = 1200;
                    finalY = overlayPosY;
                    overlayPosY += 100;
                }
                else if (string.IsNullOrEmpty(item.viewState))
                {
                    posX = 400;
                    finalY = notSetPosY;
                    notSetPosY += 100;
                }
                else
                {
                    posX = 600;
                    finalY = posY;
                    posY += 100;
                }
                var node = editor.AddViewPageNode(new Vector2(posX, finalY), isOverlay, item);
                viewPageNodes.Add(node);

            }

            posX = 700;
            posY = 100;
            //在整理 ViewState Node
            foreach (var item in viewController.viewStates)
            {
                var vp_of_vs = viewPageNodes.Where(m => m.viewPage.viewState == item.name);

                if (vp_of_vs.Count() == 0)
                {
                    posY = overlayPosY;
                    overlayPosY += 100;
                    posX = 1000;
                }
                else
                {
                    posX = 900;
                    int min = (int)vp_of_vs.Min(m => m.rect.y);
                    int max = (int)vp_of_vs.Max(m => m.rect.y);

                    posY = (int)((min + max) * 0.5f);
                }
                var node = editor.AddViewStateNode(new Vector2(posX, posY), item);
                editor.CreateConnection(node);
            }


            return true;
        }

        public void OnViewPageDelete(ViewPageNode node)
        {
            viewController.viewPage.RemoveAll(m => m == node.viewPage);
        }
        public void OnViewStateDelete(ViewStateNode node)
        {
            //如果是ViewState被刪掉 那要順便把連結的ViewPage 資料都更新
            node.currentLinkedViewPageNode.All(
                (m) =>
                {
                    m.currentLinkedViewStateNode = null;
                    m.viewPage.viewState = "";
                    return true;
                }
            );
            viewController.viewStates.RemoveAll(m => m == node.viewState);
        }
        public void OnViewPageAdd(ViewPageNode node)
        {
            var vp = new ViewPage();
            viewController.viewPage.Add(vp);
            node.viewPage = vp;
        }
        public void OnViewStateAdd(ViewStateNode node)
        {
            var vs = new ViewState();
            viewController.viewStates.Add(vs);
            node.viewState = vs;
        }
    }
}