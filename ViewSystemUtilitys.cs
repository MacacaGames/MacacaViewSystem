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
    }
}
