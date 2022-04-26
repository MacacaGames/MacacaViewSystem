using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
namespace MacacaGames.ViewSystem
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
            WithPervious, AfterPervious, Custom
        }
        public RectTransform runtimePageRoot;
        public float customPageTransitionWaitTime = 0.5f;
        public string viewState = "";
        public ViewPageType viewPageType = ViewPageType.FullPage;
        public ViewPageTransitionTimingType viewPageTransitionTimingType = ViewPageTransitionTimingType.WithPervious;
        public int canvasSortOrder = 0;
        #region SafeArea
        public SafePadding.PerEdgeValues edgeValues = new SafePadding.PerEdgeValues();
        public bool flipPadding = false;
        public bool useGlobalSafePadding = true;
      
        #endregion
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

        //Due to Unity bug in new prefab workflow, UnityEngine.Object reference which is not a builtin type will missing after prefab enter prefab mode, so currentlly use GameObject to save the reference and get ViewElement by GetCompnent in Runtime
        /// <summary>
        /// The ViewElement's GameObject in Asset
        /// </summary>
        /// <value>ViewElement's GameObject (Asset)</value>
        public GameObject viewElementObject;

        /// <summary>
        /// The ViewElement in Asset
        /// If you wish to do something on ViewElement use <see cref="runtimeViewElement"> to avoid modify on Asset.
        /// </summary>
        /// <value>ViewElement (Asset)</value>
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

        /// <summary>
        /// ViewElement runtime instance
        /// </summary>
        public ViewElement runtimeViewElement = null;
        public List<ViewElementPropertyOverrideData> overrideDatas = new List<ViewElementPropertyOverrideData>();
        public List<ViewElementEventData> eventDatas = new List<ViewElementEventData>();
        public Transform runtimeParent = null;

        #region Obsolete transform data, only keeps for update tools 
        public Transform parent;
        public string parentPath;
        public ViewSystemRectTransformData transformData = new ViewSystemRectTransformData();
        public ViewElement.RectTransformFlag transformFlag = ViewElement.RectTransformFlag.All;
        #endregion

        public ViewElementTransform defaultTransformDatas = new ViewElementTransform();
        public List<BreakPointViewElementTransform> breakPointViewElementTransforms = new List<BreakPointViewElementTransform>();

        public float TweenTime = 0.4f;
        public EaseStyle easeType = EaseStyle.QuadEaseOut;
        public float delayIn;
        public float delayOut;
        public List<ViewElementNavigationData> navigationDatas = new List<ViewElementNavigationData>();
        public PlatformOption excludePlatform = PlatformOption.Nothing;

        public int sortingOrder = 0;

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
        public ViewElementTransform GetCurrentViewElementTransform(Dictionary<string, bool> currentBreakPoints)
        {
            if (breakPointViewElementTransforms != null &&
                breakPointViewElementTransforms.Count > 0 &&
                currentBreakPoints != null)
            {
                foreach (var item in breakPointViewElementTransforms)
                {
                    if (currentBreakPoints.ContainsKey(item.breakPointName)  && currentBreakPoints[item.breakPointName])
                    {
                        return item.transformData;
                    }
                }
            }
            return defaultTransformDatas;
        }
    }

    [System.Serializable]
    public class ViewElementTransform
    {
        public Transform parent;
        public string parentPath;
        public ViewSystemRectTransformData rectTransformData = new ViewSystemRectTransformData();
        public ViewElement.RectTransformFlag rectTransformFlag = ViewElement.RectTransformFlag.All;
    }
    [System.Serializable]
    public class BreakPointViewElementTransform
    {
        public string breakPointName;
        public ViewElementTransform transformData = new ViewElementTransform();
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

    [System.Serializable, SerializeField]
    public class ViewElementOverride
    {
        [SerializeField]
        List<ViewElementPropertyOverrideData> values;
        public ViewElementOverride()
        {
            values = new List<ViewElementPropertyOverrideData>();
        }

        public List<ViewElementPropertyOverrideData> GetValues()
        {
            return values;
        }

        public void Add(ViewElementPropertyOverrideData item)
        {
            values.Add(item);
        }
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
        public Vector3 localScale = Vector3.one;
        public Vector3 localEulerAngles;
        public Vector3 anchoredPosition;
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public Vector2 sizeDelta = new Vector2(100, 100);
        public Vector2 anchorMin = new Vector2(0.5f, 0.5f);
        public Vector2 anchorMax = new Vector2(0.5f, 0.5f);


        Vector2 anchoredPosition2D
        {
            get
            {
                return anchoredPosition;
            }
            set
            {
                anchoredPosition = new Vector3(value.x, value.y, anchoredPosition.z);
            }
        }

        // Inspire from UnityEngine.CoreModule.dll
        public Vector2 offsetMin
        {
            get
            {
                return anchoredPosition2D - Vector2.Scale(sizeDelta, pivot);
            }
            set
            {
                Vector2 vector = value - (anchoredPosition2D - Vector2.Scale(sizeDelta, pivot));
                sizeDelta -= vector;
                anchoredPosition2D += Vector2.Scale(vector, Vector2.one - pivot);
            }
        }

        public Vector2 offsetMax
        {
            get
            {
                return anchoredPosition2D + Vector2.Scale(sizeDelta, Vector2.one - pivot);
            }
            set
            {
                Vector2 vector = value - (anchoredPosition2D + Vector2.Scale(sizeDelta, Vector2.one - pivot));
                sizeDelta += vector;
                anchoredPosition2D += Vector2.Scale(vector, pivot);
            }
        }
    }

}