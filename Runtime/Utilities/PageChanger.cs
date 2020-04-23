using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace CloudMacaca.ViewSystem
{
    public class FullPageChanger : PageChanger
    {
        public FullPageChanger(ViewControllerBase viewController) : base(viewController)
        {
        }
        public override PageChanger Reset()
        {
            return base.Reset();
        }
        public override void _Show()
        {
            //_OnStart?.Invoke();
            _pageChangerRunner = _viewController.ChangePage(_targetPage, _OnStart, _OnChanged, _OnComplete, _waitPreviousPageFinish, _ignoreTimeScale);
            hasStart = true;
        }
    }

    public class OverlayPageChanger : PageChanger
    {
        #region  OverlayPage
        internal bool _replayWhileSamePage = false;
        internal bool _ignoreTransition = false;
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
            _pageChangerRunner = _viewController.ShowOverlayViewPage(_targetPage, _replayWhileSamePage, _OnStart, _OnComplete, _ignoreTimeScale);
            hasStart = true;
        }
        public void _Leave()
        {
            _pageChangerRunner = _viewController.LeaveOverlayViewPage(_targetPage, _tweenTime, _OnComplete, _ignoreTransition, _ignoreTimeScale, _waitPreviousPageFinish);
        }
    }
    public class PageChanger
    {
        protected static ViewControllerBase _viewController;
        internal Action _OnStart = null;
        internal Action _OnChanged = null;
        internal Action _OnComplete = null;
        internal string _targetPage;
        internal Coroutine _pageChangerRunner = null;
        internal bool hasStart = false;
        internal bool _ignoreTimeScale = true;
        internal bool _waitPreviousPageFinish = false;

        public PageChanger(ViewControllerBase viewController)
        {
            _viewController = viewController;
            Reset();
        }

        public virtual PageChanger Reset()
        {
            _OnStart = null;
            _OnComplete = null;
            _OnChanged = null;
            _pageChangerRunner = null;
            _targetPage = string.Empty;
            _waitPreviousPageFinish = false;
            _ignoreTimeScale = true;
            return this;
        }
        public virtual void _Show()
        { }
    }

    public static class OverlayPageChangerExtension
    {
        public static OverlayPageChanger SetIgnoreTransition(this OverlayPageChanger selfObj, bool ignoreTransition)
        {
            selfObj._ignoreTransition = ignoreTransition;
            return selfObj;
        }
        public static OverlayPageChanger SetWaitPreviousPageFinish(this OverlayPageChanger selfObj, bool wait)
        {
            return (OverlayPageChanger)PageChangerExtension.SetWaitPreviousPageFinish(selfObj, wait);
        }

        public static OverlayPageChanger SetIgnoreTimeScale(this OverlayPageChanger selfObj, bool ignoreTimeScale)
        {
            return (OverlayPageChanger)PageChangerExtension.SetIgnoreTimeScale(selfObj, ignoreTimeScale);
        }

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

        [Obsolete("Overpage doesn't have callback of OnChanged.", true)]
        public static OverlayPageChanger _OnChanged(this OverlayPageChanger selfObj, Action OnStart)
        {
            throw new System.InvalidOperationException("Overpage doesn't have callback of OnChanged.");
            //return (OverlayPageChanger)PageChangerExtension.OnStart(selfObj, OnStart);
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
        public static FullPageChanger SetPage(this FullPageChanger selfObj, string targetPageName)
        {
            return (FullPageChanger)PageChangerExtension.SetPage(selfObj, targetPageName);
        }
        public static FullPageChanger OnStart(this FullPageChanger selfObj, Action OnStart)
        {
            return (FullPageChanger)PageChangerExtension.OnStart(selfObj, OnStart);
        }

        public static FullPageChanger OnChanged(this FullPageChanger selfObj, Action OnChanged)
        {
            return (FullPageChanger)PageChangerExtension.OnChanged(selfObj, OnChanged);
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
        // public static OverlayPageChanger ToOverlayPageChanger(this PageChanger selfObj)
        // {
        //     return (selfObj as OverlayPageChanger);
        // }

        // public static FullPageChanger ToFullPageChanger(this PageChanger selfObj)
        // {
        //     return (FullPageChanger)selfObj;
        // }
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

        public static PageChanger OnChanged(this PageChanger selfObj, Action OnChanged)
        {
            selfObj._OnChanged = OnChanged;
            return selfObj;
        }
        public static PageChanger Show(this PageChanger selfObj)
        {
            selfObj._Show();
            return selfObj;
        }
        public static PageChanger SetWaitPreviousPageFinish(this PageChanger selfObj, bool wait)
        {
            selfObj._waitPreviousPageFinish = wait;
            return selfObj;
        }
        public static PageChanger SetIgnoreTimeScale(this PageChanger selfObj, bool ignoreTimeScale)
        {
            selfObj._ignoreTimeScale = ignoreTimeScale;
            return selfObj;
        }
        public static YieldInstruction GetYieldInstruction(this PageChanger selfObj)
        {
            if (selfObj._pageChangerRunner != null)
                return selfObj._pageChangerRunner;
            else
                return selfObj.Show()._pageChangerRunner;
        }

        public static CustomYieldInstruction GetYieldInstruction(this PageChanger selfObj, bool customYieldInstruction)
        {
            if (selfObj._pageChangerRunner != null)
                return new ViewCYInstruction.WaitForStandardCoroutine(selfObj._pageChangerRunner);
            else
                return new ViewCYInstruction.WaitForStandardCoroutine(selfObj.Show()._pageChangerRunner);
        }
    }
}
