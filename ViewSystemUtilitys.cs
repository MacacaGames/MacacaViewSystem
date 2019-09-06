using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    public class ViewSystemUtilitys
    {
        [SerializeField]
        public class OverlayPageState
        {
            public bool IsTransition = false;
            public ViewPage viewPage;
            public Coroutine pageChangeCoroutine;
        }

        static CloudMacaca.ViewSystem.ViewPageItem.PlatformOption _platform;
        public static CloudMacaca.ViewSystem.ViewPageItem.PlatformOption SetupPlatformDefine()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
            {
                _platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.iOS;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.tvOS)
            {
                _platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.tvOS;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
            {
                _platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.Android;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WSAPlayer)
            {
                _platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.UWP;
            }
#else
            if (Application.platform == RuntimePlatform.IPhonePlayer) {
                _platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.iOS;
            } else if (Application.platform == RuntimePlatform.tvOS) {
                _platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.tvOS;
            } else if (Application.platform == RuntimePlatform.Android) {
                _platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.Android;
            } else if (Application.platform == RuntimePlatform.WSAPlayerARM ||
                Application.platform == RuntimePlatform.WSAPlayerX64 ||
                Application.platform == RuntimePlatform.WSAPlayerX86) {
                _platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.UWP;
            }
#endif
            return _platform;
        }



        public static float CalculateWaitingTimeForCurrentOnLeave(IEnumerable<ViewPageItem> viewPageItems)
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
        public static float CalculateWaitingTimeForCurrentOnShow(IEnumerable<ViewPageItem> viewPageItems)
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

        public static float CalculateTimesNeedsForOnShow(IEnumerable<ViewElement> viewElements, float maxClampTime = 1)
        {
            float maxInAnitionTime = 0;

            foreach (var item in viewElements)
            {
                float t = 0;
                if (item.transition == ViewElement.TransitionType.Animator)
                {
                    t = item.GetInAnimationLength();
                }
                else if (item.transition == ViewElement.TransitionType.CanvasGroupAlpha)
                {
                    t = item.canvasInTime;
                }

                if (t > maxInAnitionTime)
                {
                    maxInAnitionTime = t;
                }
            }
            return Mathf.Clamp(maxInAnitionTime, 0, maxClampTime);
            //return maxOutAnitionTime;
        }
        public static float CalculateTimesNeedsForOnLeave(IEnumerable<ViewElement> viewElements, float maxClampTime = 1)
        {
            float maxOutAnitionTime = 0;

            foreach (var item in viewElements)
            {
                float t = 0;
                if (item.transition == ViewElement.TransitionType.Animator)
                {
                    t = item.GetOutAnimationLength();
                }
                else if (item.transition == ViewElement.TransitionType.CanvasGroupAlpha)
                {
                    t = item.canvasOutTime;
                }

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
            if (type.Contains("GameObject"))
            {
                throw new System.ArgumentException("ViewSystemUtility.GetComponent doesn't support GameObject due to GameObject is not a Component");
            }
            Component result = null;
            System.Type t = CloudMacaca.Utility.GetType(type);
            result = target.GetComponent(t);
            return result;
        }
    }
}
