using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{

    [System.Serializable]
    public class ViewPageItem
    {
#if UNITY_EDITOR
        public ViewElement previewViewElement;
#endif
        public string name;
        static Transform ViewControllerObject;
        public ViewElement viewElement;
        public ViewElement runtimeViewElement = null;
        public List<ViewElementPropertyOverrideData> overrideDatas = new List<ViewElementPropertyOverrideData>();
        public List<ViewElementEventData> eventDatas = new List<ViewElementEventData>();

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
        public List<ViewElementNavigationData> navigationDatas;

        [Tooltip("這個可以讓該項目在特定平台時不會出現")]
        //public List<PlatformOption> excludePlatform = new List<PlatformOption>();
        public PlatformOption excludePlatform = PlatformOption.Nothing;

        [System.Flags]
        public enum PlatformOption
        {
            Nothing = 0,
            Android = 1 << 0,
            iOS = 1 << 1,
            UWP = 1 << 2,
            tvOS = 1 << 3,
            All = Android | iOS | UWP | tvOS// Custom name for "Everything" option
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
    [System.AttributeUsage(
        System.AttributeTargets.Method,
        // Multiuse attribute.  
        AllowMultiple = true)]
    public class ViewEventGroup : System.Attribute
    {
        string groupName;
        public ViewEventGroup(string groupName)
        {
            this.groupName = groupName;
        }

        public string GetGroupName()
        {
            return groupName;
        }
    }
    [System.Serializable]
    public class ViewSystemComponentData
    {
        public string targetTransformPath;
        public string targetComponentType;
        /// This value is save as SerializedProperty.PropertyPath
        public string targetPropertyName;
    }
    [System.Serializable]
    public class ViewElementNavigationData : ViewSystemComponentData
    {
        public ViewElementNavigationData()
        {
            targetPropertyName = "m_Navigation";
        }
        public UnityEngine.UI.Navigation.Mode mode;
        public UnityEngine.UI.Navigation navigation
        {
            get
            {
                var result = UnityEngine.UI.Navigation.defaultNavigation;
                result.mode = mode;
                return result;
            }
        }
    }
    [System.Serializable]
    public class ViewElementEventData : ViewSystemComponentData
    {
        public string scriptName;
        public string methodName;
    }
    [System.Serializable]
    public class ViewElementPropertyOverrideData : ViewSystemComponentData
    {
        public ViewElementPropertyOverrideData()
        {
            Value = new PropertyOverride();
        }
        public PropertyOverride Value;
    }
}