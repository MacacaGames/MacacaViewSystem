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


        Coroutine ShowOverlayViewPage(string viewPageName, bool RePlayOnShowWhileSamePage = false, Action OnComplete = null);
        IEnumerator ShowOverlayViewPageBase(ViewPage vp, bool RePlayOnShowWhileSamePage, Action OnComplete);


        void LeaveOverlayViewPage(string viewPageName, float tweenTimeIfNeed = 0.4f, Action OnComplete = null);
        IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageState overlayPageState, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false);


        Coroutine ChangePage(string targetViewPageName, Action OnComplete = null, bool AutoWaitPreviousPageFinish = false);
        IEnumerator ChangePageBase(string viewPageName, Action OnComplete);


        void TryLeaveAllOverlayPage();
        bool HasOverlayPageLive();
        bool IsOverPageLive(string viewPageName);
        IEnumerable<string> GetCurrentOverpageNames();
    }
}