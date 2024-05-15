using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace MacacaGames.ViewSystem
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
        public override YieldInstruction _Show()
        {
            return _viewController.ChangePage(_targetPage, _OnStart, _OnChanged, _OnComplete, _waitPreviousPageFinish, _ignoreTimeScale, _ignoreClickProtection, _models);
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
            _ignoreTransition = false;
            _tweenTime = 0.4f;
            return base.Reset();
        }
        public override YieldInstruction _Show()
        {
            return _viewController.ShowOverlayViewPage(_targetPage, _replayWhileSamePage, _OnStart, _OnChanged, _OnComplete, _ignoreTimeScale, _ignoreClickProtection, _models);
        }
        public YieldInstruction _Leave()
        {
            return _viewController.LeaveOverlayViewPage(_targetPage, _tweenTime, _OnComplete, _ignoreTransition, _ignoreTimeScale, _ignoreClickProtection, _waitPreviousPageFinish);
        }
    }
    public class PageChanger
    {
        internal static ViewControllerBase _viewController;
        internal Action _OnStart = null;
        internal Action _OnChanged = null;
        internal Action _OnComplete = null;
        internal string _targetPage;
        internal bool _ignoreTimeScale = false;
        internal bool _waitPreviousPageFinish = false;
        internal bool _ignoreClickProtection = false;
        internal object[] _models = null;

        public PageChanger(ViewControllerBase viewController)
        {
            _viewController = viewController;
            Reset();
        }

        public virtual PageChanger Reset()
        {
            _OnStart = null;
            _OnComplete = null;
            _OnComplete += RecoveryToPool;
            _OnChanged = null;
            _targetPage = string.Empty;
            _waitPreviousPageFinish = false;
            _ignoreTimeScale = true;
            _ignoreClickProtection = false;
            _models = null;
            return this;
        }
        void RecoveryToPool()
        {
            Reset();
            _viewController.RecoveryChanger(this);
        }
        public virtual YieldInstruction _Show()
        {
            return null;
        }
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
        }

        public static OverlayPageChanger OnComplete(this OverlayPageChanger selfObj, Action OnComplete)
        {
            return (OverlayPageChanger)PageChangerExtension.OnComplete(selfObj, OnComplete);
        }

        public static YieldInstruction Leave(this OverlayPageChanger selfObj)
        {
            return selfObj._Leave();
        }
        public static CustomYieldInstruction Leave(this OverlayPageChanger selfObj, bool customYieldInstruction)
        {
            return new WaitForStandardYieldInstruction(PageChanger._viewController, selfObj._Leave());
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

        public static YieldInstruction Show(this FullPageChanger selfObj)
        {
            return PageChangerExtension.Show(selfObj);
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

        public static PageChanger OnChanged(this PageChanger selfObj, Action OnChanged)
        {
            selfObj._OnChanged = OnChanged;
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
        public static PageChanger SetIgnoreClickProtection(this PageChanger selfObj, bool ignoreClickProtection)
        {
            selfObj._ignoreClickProtection = ignoreClickProtection;
            return selfObj;
        }

        public static YieldInstruction Show(this PageChanger selfObj)
        {
            return selfObj._Show();
        }

        public static CustomYieldInstruction Show(this PageChanger selfObj, bool customYieldInstruction)
        {
            return new WaitForStandardYieldInstruction(PageChanger._viewController, selfObj._Show());
        }


        /// <summary>
        /// Set the page Model obejct instances, so you can use those data in the ViewElementBehaviour with [ViewElementInject] attribute
        /// </summary>
        /// <param name="selfObj"></param>
        /// <param name="models">The model instance</param>
        /// <returns></returns>
        public static PageChanger SetPageModel(this PageChanger selfObj, params object[] models)
        {
            selfObj._models = models;
            return selfObj;
        }

        // public static YieldInstruction GetYieldInstruction(this PageChanger selfObj)
        // {
        //     if (selfObj._pageChangerRunner != null)
        //         return selfObj._pageChangerRunner;
        //     else
        //         return selfObj.Show()._pageChangerRunner;
        // }

        // public static CustomYieldInstruction GetYieldInstruction(this PageChanger selfObj, bool customYieldInstruction)
        // {
        //     if (selfObj._pageChangerRunner != null)
        //         return new ViewCYInstruction.WaitForStandardCoroutine(selfObj._pageChangerRunner);
        //     else
        //         return new ViewCYInstruction.WaitForStandardCoroutine(selfObj.Show()._pageChangerRunner);
        // }
    }


}
