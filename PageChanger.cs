using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace CloudMacaca.ViewSystem
{
    public class FullPageChanger : PageChanger
    {
        #region  FullPage
        internal bool _waitPreviousPageFinish = false;
        #endregion
        public FullPageChanger(ViewControllerBase viewController) : base(viewController)
        {
        }
        public override PageChanger Reset()
        {
            _waitPreviousPageFinish = false;
            return base.Reset();
        }
        public override void _Show()
        {
            _OnStart?.Invoke();
            _pageChangerRunner = _viewController.ChangePage(_targetPage, _OnComplete, _waitPreviousPageFinish);
            hasStart = true;
        }
    }

    public class OverlayPageChanger : PageChanger
    {
        #region  OverlayPage
        internal bool _replayWhileSamePage = false;
        internal float _tweenTime = 0.4f;
        #endregion

        public OverlayPageChanger(ViewControllerBase viewController) : base(viewController)
        {
        }
        public override PageChanger Reset()
        {
            _replayWhileSamePage = false;
            _tweenTime = 0.4f;
            return base.Reset();
        }
        public override void _Show()
        {
            _OnStart?.Invoke();
            _pageChangerRunner = _viewController.ShowOverlayViewPage(_targetPage, _replayWhileSamePage, _OnComplete);
            hasStart = true;
        }
        public void _Leave()
        {
            _pageChangerRunner = _viewController.LeaveOverlayViewPage(_targetPage, _tweenTime, _OnComplete);
        }
    }
    public class PageChanger
    {
        protected static ViewControllerBase _viewController;
        internal Action _OnStart = null;
        internal Action _OnComplete = null;
        internal string _targetPage;
        internal Coroutine _pageChangerRunner = null;
        internal bool hasStart = false;

        public PageChanger(ViewControllerBase viewController)
        {
            _viewController = viewController;
            Reset();
        }

        public virtual PageChanger Reset()
        {
            _OnStart = null;
            _OnComplete = null;
            _pageChangerRunner = null;
            _targetPage = string.Empty;
            return this;
        }
        public virtual void _Show()
        { }
    }

    public static class OverlayPageChangerExtension
    {
        public static OverlayPageChanger SetReplayWhileSamePage(this OverlayPageChanger selfObj, bool replay)
        {
            selfObj._replayWhileSamePage = replay;
            return selfObj;
        }
        public static OverlayPageChanger SetTweenTime(this OverlayPageChanger selfObj, float tweenTime)
        {
            selfObj._tweenTime = tweenTime;
            return selfObj;
        }
        public static OverlayPageChanger SetPage(this OverlayPageChanger selfObj, string targetPageName)
        {
            return (OverlayPageChanger)PageChangerExtension.SetPage(selfObj, targetPageName);
        }
        public static OverlayPageChanger OnStart(this OverlayPageChanger selfObj, Action OnStart)
        {
            return (OverlayPageChanger)PageChangerExtension.OnStart(selfObj, OnStart);
        }

        public static OverlayPageChanger OnComplete(this OverlayPageChanger selfObj, Action OnComplete)
        {
            return (OverlayPageChanger)PageChangerExtension.OnComplete(selfObj, OnComplete);
        }
        public static OverlayPageChanger Show(this OverlayPageChanger selfObj)
        {
            return (OverlayPageChanger)PageChangerExtension.Show(selfObj);
        }
        public static OverlayPageChanger Leave(this OverlayPageChanger selfObj)
        {
            selfObj._Leave();
            return selfObj;
        }
    }


    public static class FullPageChangerExtension
    {
        public static FullPageChanger SetWaitPreviousPageFinish(this FullPageChanger selfObj, bool wait)
        {
            ((FullPageChanger)selfObj)._waitPreviousPageFinish = wait;
            return selfObj;
        }

        public static FullPageChanger SetPage(this FullPageChanger selfObj, string targetPageName)
        {
            return (FullPageChanger)PageChangerExtension.SetPage(selfObj, targetPageName);
        }
        public static FullPageChanger OnStart(this FullPageChanger selfObj, Action OnStart)
        {
            return (FullPageChanger)PageChangerExtension.OnStart(selfObj, OnStart);
        }

        public static FullPageChanger OnComplete(this FullPageChanger selfObj, Action OnComplete)
        {
            return (FullPageChanger)PageChangerExtension.OnComplete(selfObj, OnComplete);
        }
        public static FullPageChanger Show(this FullPageChanger selfObj)
        {
            return (FullPageChanger)PageChangerExtension.Show(selfObj);
        }
    }



    public static class PageChangerExtension
    {
        public static PageChanger SetPage(this PageChanger selfObj, string targetPageName)
        {
            selfObj._targetPage = targetPageName;
            return selfObj;
        }
        public static PageChanger OnStart(this PageChanger selfObj, Action OnStart)
        {
            selfObj._OnStart = OnStart;
            return selfObj;
        }

        public static PageChanger OnComplete(this PageChanger selfObj, Action OnComplete)
        {
            selfObj._OnComplete = OnComplete;
            return selfObj;
        }
        public static PageChanger Show(this PageChanger selfObj)
        {
            selfObj._Show();
            return selfObj;
        }
        public static YieldInstruction GetYieldInstruction(this PageChanger selfObj)
        {
            if (selfObj._pageChangerRunner != null)
                return selfObj._pageChangerRunner;
            else
                return selfObj.Show()._pageChangerRunner;
        }
    }
}
