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
        void Save(List<ViewPageNode> viewPageNodes, List<ViewStateNode> viewStateNodes);
        void OnViewPagePreview(ViewPage viewPage);
        void Normalized();
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

        public void Save(List<ViewPageNode> viewPageNodes, List<ViewStateNode> viewStateNodes)
        {
            EditorUtility.SetDirty(viewController);
        }
        Transform _poolTransform;
        Transform poolTransform
        {
            get
            {
                if (_poolTransform == null)
                {
                    _poolTransform = ((ViewElementPool)FindObjectOfType(typeof(ViewElementPool))).transform;
                }
                return _poolTransform;
            }
        }
        public void Normalized()
        {
            if (poolTransform == null)
            {
                Debug.LogError("Please Set ViewElementPool");
                return;
            }
            ViewElement[] allElements = FindObjectsOfType<ViewElement>();
            foreach (ViewElement viewElement in allElements)
            {
                var rt = viewElement.GetComponent<RectTransform>();
                rt.SetParent(poolTransform);
            }
        }
        public void OnViewPagePreview(ViewPage viewPage)
        {
            if (poolTransform == null)
            {
                Debug.LogError("Please Set ViewElementPool");
                return;
            }

            Normalized();

            //打開所有相關 ViewElements
            ViewState viewPagePresetTemp;
            List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem>();

            //從 ViewPagePreset 尋找 (ViewState)
            if (!string.IsNullOrEmpty(viewPage.viewState))
            {
                viewPagePresetTemp = viewController.viewStates.SingleOrDefault(m => m.name == viewPage.viewState);
                if (viewPagePresetTemp != null)
                {
                    viewItemForNextPage.AddRange(viewPagePresetTemp.viewPageItems);
                }
            }

            //從 ViewPage 尋找
            viewItemForNextPage.AddRange(viewPage.viewPageItem);

            //打開相對應物件
            foreach (ViewPageItem item in viewItemForNextPage)
            {
                item.viewElement.gameObject.SetActive(true);
                var rectTransform = item.viewElement.GetComponent<RectTransform>();
                rectTransform.SetParent(item.parent, true);
                rectTransform.anchoredPosition3D = Vector3.zero;
                rectTransform.localScale = Vector3.one;

                //item.viewElement.SampleToLoopState();
                if (item.viewElement.transition != ViewElement.TransitionType.Animator)
                    continue;


                Animator animator = item.viewElement.animator;
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                foreach (AnimationClip clip in clips)
                {
                    if (clip.name.ToLower().Contains(item.viewElement.AnimationStateName_Loop.ToLower()))
                    {
                        clip.SampleAnimation(animator.gameObject, 0);
                    }
                }
            }
        }

    }
}