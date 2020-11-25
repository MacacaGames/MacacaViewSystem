using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
namespace CloudMacaca.ViewSystem
{
    [System.Serializable]
    public class View
    {
        public string name;
        public List<ViewPageItem> viewPageItems = new List<ViewPageItem>();
    }
    [System.Serializable]
    public class ViewState : View
    {
        public int targetFrameRate = -1;
    }

    [System.Serializable]
    public class ViewPage : View
    {
        public enum ViewPageType
        {
            FullPage, Overlay
        }
        public enum ViewPageTransitionTimingType
        {
            與前動畫同時, 接續前動畫, 自行設定
        }
        public float customPageTransitionWaitTime = 0.5f;
        public string viewState = "";
        public ViewPageType viewPageType = ViewPageType.FullPage;
        public ViewPageTransitionTimingType viewPageTransitionTimingType = ViewPageTransitionTimingType.與前動畫同時;

        #region Navigation
        public bool IsNavigation = false;
        public ViewElementNavigationTarget _firstSelectSetting;
        public UnityEngine.UI.Selectable firstSelected
        {
            get
            {
                if (string.IsNullOrEmpty(_firstSelectSetting.viewPageItemId))
                {
                    return viewPageItems.SelectMany(m => m.runtimeViewElement.GetComponentsInChildren<UnityEngine.UI.Selectable>()).FirstOrDefault();
                }
                else
                {
                    var vpi = viewPageItems.SingleOrDefault(m => m.Id == _firstSelectSetting.viewPageItemId);
                    var targetTransform = vpi.runtimeViewElement.runtimeOverride.GetTransform(_firstSelectSetting.targetTransformPath);
                    var com = vpi.runtimeViewElement.runtimeOverride.GetCachedComponent(targetTransform, _firstSelectSetting.targetTransformPath, _firstSelectSetting.targetComponentType);
                    return (UnityEngine.UI.Selectable)com.Component;
                }
            }
        }
        public List<ViewElementNavigationDataViewState> navigationDatasForViewState = new List<ViewElementNavigationDataViewState>();
        Dictionary<string, List<ViewElementNavigationData>> _navigationDatasForViewStateDict;
        public Dictionary<string, List<ViewElementNavigationData>> stateNavDict
        {
            get
            {
                if (_navigationDatasForViewStateDict == null)
                {
                    _navigationDatasForViewStateDict = navigationDatasForViewState.GroupBy(m => m.viewPageItemId)
                    .ToDictionary(x => x.Key, x => x.SelectMany(m => m.navigationDatas).ToList());
                }
                return _navigationDatasForViewStateDict;
            }
        }
        #endregion
    }
    [System.Serializable]
    public class ViewPageItem
    {
        public string Id;
#if UNITY_EDITOR
        public ViewElement previewViewElement;
#endif
        public string name;

        public string displayName
        {
            get
            {
                if (string.IsNullOrEmpty(name))
                {
                    return viewElement == null ? "ViewElement not Set" : viewElement.name;
                }
                else return name;
            }
        }
        static Transform ViewControllerObject;

        public ViewElement viewElement
        {
            get
            {
                if (viewElementObject == null)
                {
                    return null;
                }
                return viewElementObject?.GetComponent<ViewElement>();
            }
            set
            {
                viewElementObject = value.gameObject;
            }
        }

        //Due to Unity bug in new prefab workflow, UnityEngine.Object reference which is not a builtin type will missing after prefab enter prefab mode, so currentlly use GameObject to save the reference and get ViewElement by GetCompnent in Runtime
        public GameObject viewElementObject;
        public ViewElement runtimeViewElement = null;
        public List<ViewElementPropertyOverrideData> overrideDatas = new List<ViewElementPropertyOverrideData>();
        public List<ViewElementEventData> eventDatas = new List<ViewElementEventData>();
        public Transform parent;
        public Transform runtimeParent = null;
        public string parentPath;

        public float TweenTime = 0.4f;
        public EaseStyle easeType = EaseStyle.QuadEaseOut;
        public float delayIn;
        public float delayOut;
        public List<ViewElementNavigationData> navigationDatas = new List<ViewElementNavigationData>();
        public PlatformOption excludePlatform = PlatformOption.Nothing;

        public List<ViewSystemRectTransformData> transformData = new List<ViewSystemRectTransformData>();


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
            GenerateId();
        }
        public ViewPageItem()
        {
            GenerateId();
        }
        void GenerateId()
        {
            if (string.IsNullOrEmpty(Id))
                Id = System.Guid.NewGuid().ToString().Substring(0, 8);
        }
    }

    [System.Serializable]
    public class ViewElementNavigationDataViewState
    {
        public string viewPageItemId;
        public List<ViewElementNavigationData> navigationDatas = new List<ViewElementNavigationData>();
    }

    [System.Serializable]
    public class ViewElementNavigationTarget : ViewSystemComponentData
    {
        public string viewPageItemId;
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
    [System.Serializable]
    public class ViewSystemComponentData
    {
        public string targetTransformPath;
        public string targetComponentType;
        /// This value is save as SerializedProperty.PropertyPath
        public string targetPropertyName;
    }
    [System.Serializable]
    public class ViewSystemRectTransformData
    {
        //scale modify may not require
        public Vector3 localScale;
        public Vector3 localEulerAngles;
        public Vector3 anchoredPosition;
        public Vector2 pivot;
        public Vector2 sizeDelta;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
    }
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
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
}