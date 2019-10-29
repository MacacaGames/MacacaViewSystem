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
        public void Leave()
        {
            if (!_isOverlayPage)
            {
                _pageChangerRunner = _viewController.LeaveOverlayViewPage(_targetPage, _tweenTime, _OnComplete);
            }
        }
    }
    public class PageChanger
    {
        protected static ViewControllerBase _viewController;
        internal Action _OnStart = null;
        internal Action _OnComplete = null;
        internal string _targetPage;
        internal bool _isOverlayPage = false;
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
            _isOverlayPage = false;
            return this;
        }
        public virtual void _Show()
        { }
    }

    public static class PageChangerExtension
    {
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
        public static PageChanger SetPage(this PageChanger selfObj, string targetPageName, bool isOverlayPage = false)
        {
            selfObj._targetPage = targetPageName;
            selfObj._isOverlayPage = isOverlayPage;
            return selfObj;
        }
        #region FullPage
        public static PageChanger SetWaitPreviousPageFinish(this PageChanger selfObj, bool wait)
        {
            ((FullPageChanger)selfObj)._waitPreviousPageFinish = wait;
            return selfObj;
        }
        #endregion
        #region OverlayPage
        public static PageChanger SetReplayWhileSamePage(this PageChanger selfObj, bool replay)
        {
            ((OverlayPageChanger)selfObj)._replayWhileSamePage = replay;
            return selfObj;
        }
        public static PageChanger SetTweenTime(this PageChanger selfObj, float tweenTime)
        {
            ((OverlayPageChanger)selfObj)._tweenTime = tweenTime;
            return selfObj;
        }
        #endregion
        public static PageChanger SetOverlayPage(this PageChanger selfObj, string targetPageName)
        {
            return selfObj.SetPage(targetPageName, true);
        }
    }
}
