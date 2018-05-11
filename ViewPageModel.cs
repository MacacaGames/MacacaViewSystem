using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{

    [System.Serializable]
    public class ViewPageItem
    {
        public ViewElement viewElement;
        public Transform parent;
        public float TweenTime = 0.4f;
        //public float parentMoveTweenOut = 0.4f;
        public float delayIn;
        public float delayOut;
        //public bool NeedLeaveWhileIsFloating = false;
        public ViewPageItem(ViewElement ve)
        {
            viewElement = ve;
            parent = null;
        }
        
    }
    [System.Serializable]
    public class ViewState
    {
        public string name;
        public List<ViewPageItem> viewPageItems = new List<ViewPageItem>();
    }

    [System.Serializable]
    public class ViewPage
    {
        public enum ViewPageType
        {
            FullPage, SubPage, Overlay
        }
        public enum ViewPageTransitionTimingType
        {
            與前動畫同時, 接續前動畫, 自行設定
        }
        public string name;
        public float autoLeaveTimes = 0;
        public float customPageTransitionWaitTime = 0.5f;
        public string viewState = "";
        //public bool allowWithOtherPageView = false;
        public ViewPageType viewPageType = ViewPageType.FullPage;
        public ViewPageTransitionTimingType viewPageTransitionTimingType = ViewPageTransitionTimingType.接續前動畫;
        public List<ViewPageItem> viewPageItem = new List<ViewPageItem>();

    }

}