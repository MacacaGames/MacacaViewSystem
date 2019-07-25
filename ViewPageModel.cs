using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{

    [System.Serializable]
    public class ViewPageItem
    {
        static Transform ViewControllerObject;

        public ViewElement viewElement;
        public ViewElement runtimeViewElement = null;
        [Tooltip("ViewElement 在該頁面時應該對其的父物件")]
        public Transform parent;
        public Transform runtimeParent = null;
        public string parentPath;
        GameObject _parentGameObject;
        [HideInInspector]
        public GameObject parentGameObject
        {
            get
            {
                if (_parentGameObject == null)
                {
                    _parentGameObject = runtimeParent.gameObject;
                }
                return _parentGameObject;
            }
        }
        public float TweenTime = 0.4f;
        public DG.Tweening.Ease easeType = DG.Tweening.Ease.OutQuad;
        public float delayIn;
        public float delayOut;
        [Tooltip("這個可以讓該項目在特定平台時不會出現")]
        public List<PlatformOption> excludePlatform = new List<PlatformOption>();
        public enum PlatformOption
        {
            Android, // 1 << 0
            iOS, // 1 << 1
            UWP, // 1 << 2
            tvOS // 1 << 3
        }
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
        public int targetFrameRate = -1;
        public List<ViewPageItem> viewPageItems = new List<ViewPageItem>();
    }

    [System.Serializable]
    public class ViewPage
    {
        public enum ViewPageType
        {
            FullPage, Overlay
        }
        public enum ViewPageTransitionTimingType
        {
            與前動畫同時, 接續前動畫, 自行設定
        }
        public string name;
        public float autoLeaveTimes = 0;
        public float customPageTransitionWaitTime = 0.5f;
        public string viewState = "";
        public ViewPageType viewPageType = ViewPageType.FullPage;
        public ViewPageTransitionTimingType viewPageTransitionTimingType = ViewPageTransitionTimingType.接續前動畫;
        public List<ViewPageItem> viewPageItems = new List<ViewPageItem>();
    }

}