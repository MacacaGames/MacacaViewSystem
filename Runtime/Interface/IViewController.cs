using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    public interface IViewController
    {
        MacacaGames.ViewSystem.ViewPageItem.PlatformOption platform { get; }
        bool IsOverlayTransition { get; }


        Coroutine ShowOverlayViewPage(string viewPageName, bool RePlayOnShowWhileSamePage = false, Action OnStart = null, Action OnChanged = null, Action OnComplete = null, bool ignoreTimeScale = false, params object[] models);
        IEnumerator ShowOverlayViewPageBase(ViewPage vp, bool RePlayOnShowWhileSamePage, Action OnStart, Action OnChanged, Action OnComplete, bool ignoreTimeScale = false, params object[] models);


        Coroutine LeaveOverlayViewPage(string viewPageName, float tweenTimeIfNeed = 0.4f, Action OnComplete = null, bool ignoreTransition = false, bool ignoreTimeScale = false, bool waitForShowFinish = false);
        IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageStatus overlayPageState, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false, bool ignoreTimeScale = false, bool waitForShowFinish = false);


        Coroutine ChangePage(string targetViewPageName, Action OnStart = null, Action OnCheaged = null, Action OnComplete = null, bool AutoWaitPreviousPageFinish = false, bool ignoreTimeScale = false, params object[] models);
        IEnumerator ChangePageBase(string viewPageName, Action OnStart, Action OnCheaged, Action OnComplete, bool ignoreTimeScale, params object[] models);


        void TryLeaveAllOverlayPage();
        bool HasOverlayPageLive();
        bool IsOverPageLive(string viewPageName);
        IEnumerable<string> GetCurrentOverpageNames();
    }
}