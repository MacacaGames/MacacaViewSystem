using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Reflection;

namespace MacacaGames.ViewSystem
{
    public class ViewSystemUtilitys
    {

        [SerializeField]
        public class OverlayPageStatus
        {
            public bool IsTransition
            {
                get
                {
                    return transition != Transition.None;
                }
                set
                {
                    if (value == false)
                    {
                        transition = Transition.None;
                    }
                    else
                    {
                        transition = Transition.Show;
                    }
                }
            }
            public ViewPage viewPage;
            public ViewState viewState;
            public Coroutine pageChangeCoroutine;
            public Transition transition;
            public enum Transition
            {
                None,
                Show,
                Leave
            }
            public IEnumerable<ViewElement> currentViewElements
            {
                get
                {
                    var tempCurrentLiveElements = new List<ViewElement>();
                    tempCurrentLiveElements.Clear();
                    tempCurrentLiveElements.AddRange(viewPage.viewPageItems.Select(m => m.runtimeViewElement));
                    if (viewState != null)
                    {
                        tempCurrentLiveElements.AddRange(viewState.viewPageItems.Select(m => m.runtimeViewElement));
                    }
                    return tempCurrentLiveElements;
                }
            }
        }

        static MacacaGames.ViewSystem.ViewPageItem.PlatformOption _platform;
        public static MacacaGames.ViewSystem.ViewPageItem.PlatformOption SetupPlatformDefine()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
            {
                _platform = MacacaGames.ViewSystem.ViewPageItem.PlatformOption.iOS;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.tvOS)
            {
                _platform = MacacaGames.ViewSystem.ViewPageItem.PlatformOption.tvOS;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
            {
                _platform = MacacaGames.ViewSystem.ViewPageItem.PlatformOption.Android;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WSAPlayer)
            {
                _platform = MacacaGames.ViewSystem.ViewPageItem.PlatformOption.UWP;
            }
#else
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                _platform = MacacaGames.ViewSystem.ViewPageItem.PlatformOption.iOS;
            }
            else if (Application.platform == RuntimePlatform.tvOS)
            {
                _platform = MacacaGames.ViewSystem.ViewPageItem.PlatformOption.tvOS;
            }
            else if (Application.platform == RuntimePlatform.Android)
            {
                _platform = MacacaGames.ViewSystem.ViewPageItem.PlatformOption.Android;
            }
            else if (Application.platform == RuntimePlatform.WSAPlayerARM ||
              Application.platform == RuntimePlatform.WSAPlayerX64 ||
              Application.platform == RuntimePlatform.WSAPlayerX86)
            {
                _platform = MacacaGames.ViewSystem.ViewPageItem.PlatformOption.UWP;
            }
#endif
            return _platform;
        }

        public static float CalculateDelayOutTime(IEnumerable<ViewPageItem> viewPageItems)
        {
            float maxDelayTime = 0;
            foreach (var item in viewPageItems)
            {
                float t2 = item.delayOut;
                if (t2 > maxDelayTime)
                {
                    maxDelayTime = t2;
                }
            }
            return maxDelayTime;
        }
        public static float CalculateDelayInTime(IEnumerable<ViewPageItem> viewPageItems)
        {
            float maxDelayTime = 0;
            foreach (var item in viewPageItems)
            {
                float t2 = item.delayIn;
                if (t2 > maxDelayTime)
                {
                    maxDelayTime = t2;
                }
            }
            return maxDelayTime;
        }

        public static float CalculateOnShowDuration(IEnumerable<ViewElement> viewElements, float maxClampTime = 1)
        {
            float maxInAnitionTime = 0;

            foreach (var item in viewElements)
            {
                if (item == null)
                {
                    ViewSystemLog.LogError($"One or more ViewElement is null in the page trying to Show, ignore the item.");
                    continue;
                }
                float t = item.GetInDuration();

                if (t > maxInAnitionTime)
                {
                    maxInAnitionTime = t;
                }
            }
            return Mathf.Clamp(maxInAnitionTime, 0, maxClampTime);
            //return maxOutAnitionTime;
        }
        public static float CalculateOnLeaveDuration(IEnumerable<ViewElement> viewElements, float maxClampTime = 1)
        {
            float maxOutAnitionTime = 0;

            foreach (var item in viewElements)
            {
                if (item == null)
                {
                    ViewSystemLog.LogError($"One or more ViewElement is null in the page trying to Leave, ignore the item.");
                    continue;
                }
                float t = item.GetOutDuration();
                if (t > maxOutAnitionTime)
                {
                    maxOutAnitionTime = t;
                }
            }
            return Mathf.Clamp(maxOutAnitionTime, 0, maxClampTime);
        }
        public static string ParseUnityEngineProperty(string ori)
        {
            if (ori.ToLower().Contains("material"))
            {
                return "material";
            }
            if (ori.ToLower().Contains("sprite"))
            {
                return "sprite";
            }
            if (ori.ToLower().Contains("active"))
            {
                return "active";
            }
            string result = ori.Replace("m_", "");
            result = result.Substring(0, 1).ToLower() + result.Substring(1);
            return result;
        }

        public static Component GetComponent(Component target, string type)
        {
            if (type.Contains("UnityEngine.GameObject"))
            {
                throw new System.ArgumentException("ViewSystemUtility.GetComponent doesn't support GameObject due to GameObject is not a Component");
            }
            Component result = null;
            System.Type t = MacacaGames.Utility.GetType(type);
            if (t == null)
            {
                return null;
            }
            result = target.GetComponent(t);
            return result;
        }
        public static string GetPageRootName(ViewPage viewPage)
        {
            string result = "Page_";
            if (viewPage.viewPageType == ViewPage.ViewPageType.FullPage)
            {
                result += "FullPage";
            }
            else
            {
                if (string.IsNullOrEmpty(viewPage.viewState))
                {
                    result += viewPage.name;
                }
                else
                {
                    result += viewPage.viewState;
                }
            }
            return result;
        }

        public class PageRootWrapper
        {
            public RectTransform rectTransform;
            public Canvas canvas;
            public UnityEngine.UI.GraphicRaycaster raycaster;
            public SafePadding safePadding;

        }
        public static void ClearRectTransformCache()
        {
            runtimeRectTransformCache.Clear();
        }
        static Dictionary<string, PageRootWrapper> runtimeRectTransformCache = new Dictionary<string, PageRootWrapper>();
        public static PageRootWrapper CreatePageTransform(string name, Transform canvasRoot, int sortingOrder, string layerName)
        {
            PageRootWrapper wrapper;
            RectTransform previewUIRootRectTransform;
            Canvas canvas;
            UnityEngine.UI.GraphicRaycaster raycaster; SafePadding safePadding;
            if (Application.isPlaying)
            {
                if (runtimeRectTransformCache.TryGetValue(name, out wrapper))
                {
                    wrapper.canvas.overrideSorting = true;
                    wrapper.canvas.sortingOrder = sortingOrder;
                    return wrapper;
                }
            }
            var previewUIRoot = new GameObject(name);

            previewUIRoot.layer = LayerMask.NameToLayer(layerName);
            previewUIRootRectTransform = previewUIRoot.AddComponent<RectTransform>();
            previewUIRootRectTransform.SetParent(canvasRoot, false);
            previewUIRootRectTransform.localScale = Vector3.one;
            previewUIRootRectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 0, 0);
            previewUIRootRectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, 0);
            previewUIRootRectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            previewUIRootRectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 0, 0);
            previewUIRootRectTransform.anchorMin = Vector2.zero;
            previewUIRootRectTransform.anchorMax = Vector2.one;

            canvas = previewUIRoot.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            safePadding = previewUIRoot.AddComponent<SafePadding>();
            raycaster = previewUIRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            wrapper = new PageRootWrapper();
            wrapper.rectTransform = previewUIRootRectTransform;
            wrapper.canvas = canvas;
            wrapper.raycaster = raycaster;
            wrapper.safePadding = safePadding;

            if (Application.isPlaying)
            {
                if (!runtimeRectTransformCache.ContainsKey(name))
                {

                    runtimeRectTransformCache.Add(name, wrapper);
                }
            }
            return wrapper;
        }
    }
}
