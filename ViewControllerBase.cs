using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CloudMacaca.ViewSystem
{
    public class ViewControllerBase : TransformCacheBase, IViewController
    {
        public virtual ViewControllerBase GetInstance()
        {
            return this;
        }

        #region Interface Impletetment
        public Coroutine ShowOverlayViewPage(string viewPageName, bool RePlayOnShowWhileSamePage = false, Action OnComplete = null)
        {
            var vp = viewPages.Where(m => m.name == viewPageName).SingleOrDefault();
            return StartCoroutine(ShowOverlayViewPageBase(vp, RePlayOnShowWhileSamePage, OnComplete));
        }

        public void LeaveOverlayViewPage(string viewPageName, float tweenTimeIfNeed = 0.4F, Action OnComplete = null)
        {
            ViewSystemUtilitys.OverlayPageState overlayPageState = null;

            overlayPageStates.TryGetValue(viewPageName, out overlayPageState);

            if (overlayPageState == null)
            {

                //如果 字典裡找不到 則 new 一個
                overlayPageState = new ViewSystemUtilitys.OverlayPageState();
                overlayPageState.viewPage = viewPages.SingleOrDefault(m => m.name == viewPageName);
                if (overlayPageState == null)
                {
                    Debug.LogError("No live overlay viewPage of name: " + viewPageName + "  found, even cannot find in setting file");
                    return;
                }

                Debug.LogError("No live overlay viewPage of name: " + viewPageName + "  found but try hard fix success");
            }

            overlayPageState.pageChangeCoroutine = StartCoroutine(LeaveOverlayViewPageBase(overlayPageState, tweenTimeIfNeed, OnComplete));
        }

        public Coroutine ChangePage(string targetViewPageName, Action OnComplete = null, bool AutoWaitPreviousPageFinish = false)
        {
            if (currentViewPage.name == targetViewPageName)
            {
                Debug.LogError("The ViewPage request to change is same as current ViewPage, nothing will happen!");
                return null;
            }
            if (IsPageTransition && AutoWaitPreviousPageFinish == false)
            {
                Debug.LogError("Page is in Transition. You can set AutoWaitPreviousPageFinish to 'True' then page will auto transition to next page while previous page transition finished.");
                return null;
            }
            else if (IsPageTransition && AutoWaitPreviousPageFinish == true)
            {
                Debug.LogError($"Page is in Transition but AutoWaitPreviousPageFinish Leaving page is [{currentViewPage?.name}] Entering page is [{nextViewPage?.name}] next page is [{targetViewPageName}]");
                ChangePageToCoroutine = StartCoroutine(WaitPrevious(targetViewPageName, OnComplete));
                return ChangePageToCoroutine;
            }
            ChangePageToCoroutine = StartCoroutine(ChangePageBase(targetViewPageName, OnComplete));
            return ChangePageToCoroutine;
        }

        public virtual IEnumerator ShowOverlayViewPageBase(ViewPage vp, bool RePlayOnShowWhileSamePage, Action OnComplete)
        {
            //Empty implement will override in child class
            yield return null;
        }

        public virtual IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageState overlayPageState, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false)
        {
            //Empty implement will override in child class
            yield return null;
        }

        public virtual IEnumerator ChangePageBase(string viewPageName, Action OnComplete)
        {
            //Empty implement will override in child class
            yield return null;
        }

        public virtual void TryLeaveAllOverlayPage()
        {
            //清空自動離場
            autoLeaveQueue.Clear();

            for (int i = 0; i < overlayPageStates.Count; i++)
            {
                var item = overlayPageStates.ElementAt(i);
                StartCoroutine(LeaveOverlayViewPageBase(item.Value, 0.4f, null, true));
            }
        }
        public virtual bool HasOverlayPageLive()
        {
            return overlayPageStates.Count > 0;
        }
        public virtual bool IsOverPageLive(string viewPageName)
        {
            return overlayPageStates.ContainsKey(viewPageName);
        }
        public virtual IEnumerable<string> GetCurrentOverpageNames()
        {
            return overlayPageStates.Select(m => m.Key);
        }
        public bool IsOverlayTransition
        {
            get
            {
                foreach (var item in overlayPageStates)
                {
                    if (item.Value.IsTransition == true)
                    {
                        Debug.LogError("Due to " + item.Key + "is Transition");
                        return true;
                    }
                }
                return false;
            }
        }
        #endregion

        #region  Unity LifeCycle
        protected virtual void Awake()
        {

        }
        protected virtual void Start()
        {
            //開啟無限檢查自動離場的迴圈
            StartCoroutine(AutoLeaveOverlayPage());
        }
        #endregion  


        public List<ViewPage> viewPages = new List<ViewPage>();
        public List<ViewState> viewStates = new List<ViewState>();
        protected static IEnumerable<string> viewStatesNames;

        [ReadOnly, SerializeField]
        protected List<ViewElement> currentLiveElements = new List<ViewElement>();

        [HideInInspector]
        public ViewPage lastViewPage;

        [HideInInspector]
        public ViewState lastViewState;

        [HideInInspector]
        public ViewPage currentViewPage;

        [HideInInspector]
        public ViewState currentViewState;

        [HideInInspector]
        public ViewPage nextViewPage;

        [HideInInspector]
        public ViewState nextViewState;

        public ViewPageItem.PlatformOption platform
        {
            get => ViewSystemUtilitys.SetupPlatformDefine();
        }

        [SerializeField]
        protected Dictionary<string, ViewSystemUtilitys.OverlayPageState> overlayPageStates = new Dictionary<string, ViewSystemUtilitys.OverlayPageState>();


        protected IEnumerable<ViewPageItem> GetAllViewPageItemInViewPage(ViewPage vp)
        {
            List<ViewPageItem> realViewPageItem = new List<ViewPageItem>();

            //先整理出下個頁面應該出現的 ViewPageItem
            ViewState viewPagePresetTemp;
            List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem>();

            //從 ViewState 尋找
            if (!string.IsNullOrEmpty(vp.viewState))
            {
                viewPagePresetTemp = viewStates.SingleOrDefault(m => m.name == vp.viewState);
                if (viewPagePresetTemp != null)
                {
                    viewItemForNextPage.AddRange(viewPagePresetTemp.viewPageItems);
                }
            }

            //從 ViewPage 尋找
            viewItemForNextPage.AddRange(vp.viewPageItems);
            return viewItemForNextPage.Where(m => !m.excludePlatform.IsSet(platform));
        }

        protected List<AutoLeaveData> autoLeaveQueue = new List<AutoLeaveData>();
        protected class AutoLeaveData
        {
            public string name;
            public float times;
            public AutoLeaveData(string _name, float _times)
            {
                name = _name;
                times = _times;
            }
        }

        protected IEnumerator AutoLeaveOverlayPage()
        {
            float deltaTime = 0;
            while (true)
            {
                //Debug.LogError("Find auto leave count " + autoLeaveQueue.Count);
                deltaTime = Time.deltaTime;
                ///更新每個 倒數值
                for (int i = 0; i < autoLeaveQueue.Count; i++)
                {
                    //Debug.LogError("Update auto leave value " + autoLeaveQueue[i].name);

                    autoLeaveQueue[i].times -= deltaTime;
                    if (autoLeaveQueue[i].times <= 0)
                    {
                        LeaveOverlayViewPage(autoLeaveQueue[i].name);
                        autoLeaveQueue.Remove(autoLeaveQueue[i]);
                    }
                }
                yield return null;
            }
        }

        public bool IsPageTransition
        {
            get
            {
                return ChangePageToCoroutine != null;
            }
        }
        protected Coroutine ChangePageToCoroutine = null;
        public IEnumerator WaitPrevious(string viewPageName, Action OnComplete)
        {
            yield return new WaitUntil(() => IsPageTransition == false);
            yield return ChangePage(viewPageName, OnComplete);
        }

        #region  Events
        /// <summary>
        /// OnViewStateChange Calls on the ViewPage is changed and has different ViewState.
        /// </summary>
        public event EventHandler<ViewStateEventArgs> OnViewStateChange;
        protected virtual void InvokeOnViewStateChange(object obj, ViewStateEventArgs args)
        {
            OnViewStateChange?.Invoke(obj, args);
        }

        /// <summary>
        /// OnViewPageChange Calls on last page has leave finished, next page is ready to show.
        /// </summary>
        public event EventHandler<ViewPageEventArgs> OnViewPageChange;
        protected virtual void InvokeOnViewPageChange(object obj, ViewPageEventArgs args)
        {
            OnViewPageChange?.Invoke(obj, args);
        }

        /// <summary>
        /// OnViewPageChangeStart Calls on page is ready to change with no error(eg. no page fonud etc.), and in this moment last page is still in view. 
        /// </summary>
        public event EventHandler<ViewPageTrisitionEventArgs> OnViewPageChangeStart;
        protected virtual void InvokeOnViewPageChangeEnd(object obj, ViewPageTrisitionEventArgs args)
        {
            OnViewPageChangeStart?.Invoke(obj, args);
        }

        /// <summary>
        /// OnViewPageChangeEnd Calls on page is changed finish, all animation include in OnShow or OnLeave is finished. (Note. the sometimes the Event fire early due to the animation time is longer than "Change Page Max Waiting" time)
        /// </summary>
        public event EventHandler<ViewPageEventArgs> OnViewPageChangeEnd;
        protected virtual void InvokeOnViewPageChangeEnd(object obj, ViewPageEventArgs args)
        {
            OnViewPageChangeEnd?.Invoke(obj, args);
        }

        /// <summary>
        /// OnOverlayPageShow Calls on an overlay page is show.(the transition may still working)
        /// </summary>
        public event EventHandler<ViewPageEventArgs> OnOverlayPageShow;
        protected virtual void InvokeOnOverlayPageShow(object obj, ViewPageEventArgs args)
        {
            OnOverlayPageShow?.Invoke(obj, args);
        }

        /// <summary>
        /// OnOverlayPageLeave Calls on an overlay page is leave.(the transition may still working)
        /// </summary>
        public event EventHandler<ViewPageEventArgs> OnOverlayPageLeave;
        protected virtual void InvokeOnOverlayPageLeave(object obj, ViewPageEventArgs args)
        {
            OnOverlayPageShow?.Invoke(obj, args);
        }


        public class ViewStateEventArgs : EventArgs
        {
            public ViewState currentViewState;
            public ViewState lastViewState;
            public ViewStateEventArgs(ViewState CurrentViewState, ViewState LastVilastViewState)
            {
                this.currentViewState = CurrentViewState;
                this.lastViewState = LastVilastViewState;
            }
        }
        public class ViewPageEventArgs : EventArgs
        {
            // ...省略額外參數
            public ViewPage currentViewPage;
            public ViewPage lastViewPage;
            public ViewPageEventArgs(ViewPage CurrentViewPage, ViewPage LastViewPage)
            {
                this.currentViewPage = CurrentViewPage;
                this.lastViewPage = LastViewPage;
            }
        }
        public class ViewPageTrisitionEventArgs : EventArgs
        {
            // ...省略額外參數
            public ViewPage viewPageWillLeave;
            public ViewPage viewPageWillShow;
            public ViewPageTrisitionEventArgs(ViewPage viewPageWillLeave, ViewPage viewPageWillShow)
            {
                this.viewPageWillLeave = viewPageWillLeave;
                this.viewPageWillShow = viewPageWillShow;
            }
        }
        #endregion
    }
}