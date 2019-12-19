using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    public interface IViewController
    {
        CloudMacaca.ViewSystem.ViewPageItem.PlatformOption platform { get; }
        bool IsOverlayTransition { get; }


        Coroutine ShowOverlayViewPage(string viewPageName, bool RePlayOnShowWhileSamePage = false, Action OnComplete = null, bool ignoreTimeScale = false);
        IEnumerator ShowOverlayViewPageBase(ViewPage vp, bool RePlayOnShowWhileSamePage, Action OnComplete, bool ignoreTimeScale = false);


        Coroutine LeaveOverlayViewPage(string viewPageName, float tweenTimeIfNeed = 0.4f, Action OnComplete = null, bool ignoreTimeScale = false);
        IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageState overlayPageState, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false, bool ignoreTimeScale = false);


        Coroutine ChangePage(string targetViewPageName, Action OnCheaged = null, Action OnComplete = null, bool AutoWaitPreviousPageFinish = false, bool ignoreTimeScale = false);
        IEnumerator ChangePageBase(string viewPageName, Action OnCheaged, Action OnComplete, bool ignoreTimeScale);


        void TryLeaveAllOverlayPage();
        bool HasOverlayPageLive();
        bool IsOverPageLive(string viewPageName);
        IEnumerable<string> GetCurrentOverpageNames();
    }
}