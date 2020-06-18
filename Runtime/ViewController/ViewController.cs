using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
namespace CloudMacaca.ViewSystem
{

    public class ViewController : ViewControllerBase
    {
        public static ViewController Instance;

        public static ViewElementRuntimePool runtimePool;
        public ViewElementPool viewElementPool;
        static float maxClampTime = 1;

        [SerializeField]
        private ViewSystemSaveData viewSystemSaveData;
        Transform transformCache;
        // Use this for initialization
        protected override void Awake()
        {
            transformCache = transform;
            base.Awake();
            _incance = Instance = this;
            //Create ViewElementPool
            if (gameObject.name != viewSystemSaveData.globalSetting.ViewControllerObjectPath)
            {
                ViewSystemLog.LogWarning("The GameObject which attached ViewController is not match the setting in Base Setting.");
            }

            //Create UIRoot
            var ui = Instantiate(viewSystemSaveData.globalSetting.UIRoot);
            ui.transform.SetParent(transformCache);
            ui.transform.localPosition = viewSystemSaveData.globalSetting.UIRoot.transform.localPosition;
            ui.name = viewSystemSaveData.globalSetting.UIRoot.name;

            var go = new GameObject("ViewElementPool");
            go.transform.SetParent(transformCache);
            go.AddComponent<RectTransform>();
            viewElementPool = go.AddComponent<ViewElementPool>(); ;

            runtimePool = gameObject.AddComponent<ViewElementRuntimePool>();
            runtimePool.Init(viewElementPool);

            ViewElement.runtimePool = runtimePool;
            ViewElement.viewElementPool = viewElementPool;

            maxClampTime = viewSystemSaveData.globalSetting.MaxWaitingTime;
        }

        protected override void Start()
        {
            //Load ViewPages and ViewStates from ViewSystemSaveData

            viewStates = viewSystemSaveData.viewStates.Select(m => m.viewState).ToList();
            viewPages = viewSystemSaveData.viewPages.Select(m => m.viewPage).ToList();

            viewStatesNames = viewStates.Select(m => m.name);

            PrewarmInjection();

            base.Start();
        }
        #region Injection
        private Dictionary<System.Type, Component> InjectionDictionary = new Dictionary<System.Type, Component>();
        public T GetInjectionInstance<T>() where T : Component, IViewElementInjectable
        {
            if (InjectionDictionary.TryGetValue(typeof(T), out Component result))
            {
                return (T)result;
            }
            else
            {
                ViewSystemLog.LogError("Target type cannot been found, are you sure your ViewElement which attach target Component is unique?");
            }
            return null;
        }
        void PrewarmInjection()
        {
            var viewElementsInStates = viewStates.Select(m => m.viewPageItems).SelectMany(ma => ma).Select(m => m.viewElement);
            var viewElementsInPages = viewPages.Select(m => m.viewPageItems).SelectMany(ma => ma).Select(m => m.viewElement);
            foreach (var item in viewElementsInStates)
            {
                if (item == null)
                {
                    ViewSystemLog.Log("I'm null!!!");
                    continue;
                }
                if (!item.IsUnique)
                {
                    continue;
                }

                var r = runtimePool.PrewarmUniqueViewElement(item);
                if (r != null)
                {
                    foreach (var i in r.GetComponents<IViewElementInjectable>())
                    {
                        var c = (Component)i;
                        var t = c.GetType();
                        if (!InjectionDictionary.ContainsKey(t))
                            InjectionDictionary.Add(t, c);
                        else
                        {
                            ViewSystemLog.LogWarning("Type " + t + " has been injected");
                            continue;
                        }
                    }
                }
            }

            foreach (var item in viewElementsInPages)
            {
                if (item == null)
                {
                    ViewSystemLog.Log("I'm null!!!");
                    continue;
                }
                if (!item.IsUnique)
                {
                    continue;
                }

                var r = runtimePool.PrewarmUniqueViewElement(item);
                if (r != null)
                {
                    foreach (var i in r.GetComponents<IViewElementInjectable>())
                    {
                        var c = (Component)i;
                        var t = c.GetType();
                        if (!InjectionDictionary.ContainsKey(t))
                            InjectionDictionary.Add(t, c);
                        else
                        {
                            ViewSystemLog.LogWarning("Type " + t + " has been injected");
                            continue;
                        }
                    }
                }
            }
        }
        #endregion
        IEnumerable<ViewPageItem> PrepareRuntimeReference(IEnumerable<ViewPageItem> viewPageItems)
        {
            foreach (var item in viewPageItems)
            {
                if (item.runtimeParent == null)
                {
                    item.runtimeParent = transformCache.Find(item.parentPath);
                }

                if (item.viewElement != null)
                {
                    item.runtimeViewElement = runtimePool.RequestViewElement(item.viewElement);
                }
                else
                {
                    ViewSystemLog.LogError($"The viewElement in ViewPageItem : {item.Id} is null or missing, that is all we know, please check the page you're trying to change to.");
                }
            }
            return viewPageItems;
        }

        private float nextViewPageWaitTime = 0;
        private Dictionary<string, float> lastPageItemDelayOutTimes = new Dictionary<string, float>();
        private Dictionary<string, float> lastOverlayPageItemDelayOutTimes = new Dictionary<string, float>();

        [SerializeField]
        protected new List<ViewElement> currentLiveElements
        {
            get
            {
                List<ViewElement> result = new List<ViewElement>();
                result.AddRange(currentLiveElementsInViewPage);
                result.AddRange(currentLiveElementsInViewState);
                return result;
            }
        }

        [ReadOnly, SerializeField]
        protected List<ViewElement> currentLiveElementsInViewPage = new List<ViewElement>();
        [ReadOnly, SerializeField]
        protected List<ViewElement> currentLiveElementsInViewState = new List<ViewElement>();

        public override IEnumerator ChangePageBase(string viewPageName, Action OnStart, Action OnChanged, Action OnComplete, bool ignoreTimeScale)
        {
            //取得 ViewPage 物件
            var vp = viewPages.SingleOrDefault(m => m.name == viewPageName);

            //沒有找到 
            if (vp == null)
            {
                ViewSystemLog.LogError("No view page match " + viewPageName + " Found");
                ChangePageToCoroutine = null;
                yield break;
            }

            if (vp.viewPageType == ViewPage.ViewPageType.Overlay)
            {
                ViewSystemLog.LogWarning("To shown Page is an Overlay ViewPage use ShowOverlayViewPage() instead method \n current version will redirect to this method automatically, but this behaviour may be changed in future release.");
                ShowOverlayViewPageBase(vp, true, OnStart, OnComplete, ignoreTimeScale);
                ChangePageToCoroutine = null;
                yield break;
            }

            //所有檢查都通過開始換頁
            //IsPageTransition = true;

            nextViewPage = vp;
            nextViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);

            IEnumerable<ViewPageItem> viewItemNextPage = PrepareRuntimeReference(GetAllViewPageItemInViewPage(vp));
            IEnumerable<ViewPageItem> viewItemNextState = GetAllViewPageItemInViewState(nextViewState);

            // 如果兩個頁面之間的 ViewState 不同的話 才需要更新 ViewState 部分的 RuntimeViewElement
            if (nextViewState != currentViewState)
            {
                viewItemNextState = PrepareRuntimeReference(viewItemNextState);
            }

            // All reference preparing is done start do the stuff for change page
            InvokeOnViewPageChangeStart(this, new ViewPageTrisitionEventArgs(currentViewPage, vp));
            OnStart?.Invoke();

            List<ViewElement> viewElementDoesExitsInNextPage = new List<ViewElement>();

            var allViewElementForNextPageInViewPage = viewItemNextPage.Select(m => m.runtimeViewElement).ToList();
            var allViewElementForNextPageInViewState = viewItemNextState.Select(m => m.runtimeViewElement).ToList();

            foreach (var item in currentLiveElementsInViewPage)
            {
                //不存在的話就讓他加入應該移除的列表
                if (allViewElementForNextPageInViewPage.Contains(item) == false &&
                    allViewElementForNextPageInViewState.Contains(item) == false)
                {
                    //加入該移除的列表
                    viewElementDoesExitsInNextPage.Add(item);
                }
            }
            currentLiveElementsInViewPage.Clear();
            currentLiveElementsInViewPage = allViewElementForNextPageInViewPage;

            if (nextViewState != currentViewState)
            {
                foreach (var item in currentLiveElementsInViewState)
                {
                    //不存在的話就讓他加入應該移除的列表
                    if (allViewElementForNextPageInViewState.Contains(item) == false &&
                        allViewElementForNextPageInViewPage.Contains(item) == false)
                    {
                        //加入該移除的列表
                        viewElementDoesExitsInNextPage.Add(item);
                    }
                }
                currentLiveElementsInViewState.Clear();
                currentLiveElementsInViewState = allViewElementForNextPageInViewState;
            }

            //對離場的呼叫改變狀態
            foreach (var item in viewElementDoesExitsInNextPage)
            {
                float delayOut = 0;
                lastPageItemDelayOutTimes.TryGetValue(item.name, out delayOut);
                item.ChangePage(false, null, 0, 0, delayOut);
            }

            lastPageItemDelayOutTimes.Clear();
            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(nextViewPageWaitTime);
            else
                yield return Yielders.GetWaitForSeconds(nextViewPageWaitTime);

            float TimeForPerviousPageOnLeave = 0;
            switch (vp.viewPageTransitionTimingType)
            {
                case ViewPage.ViewPageTransitionTimingType.接續前動畫:
                    TimeForPerviousPageOnLeave = ViewSystemUtilitys.CalculateTimesNeedsForOnLeave(viewItemNextPage.Select(m => m.viewElement), maxClampTime);
                    break;
                case ViewPage.ViewPageTransitionTimingType.與前動畫同時:
                    TimeForPerviousPageOnLeave = 0;
                    break;
                case ViewPage.ViewPageTransitionTimingType.自行設定:
                    TimeForPerviousPageOnLeave = vp.customPageTransitionWaitTime;
                    break;
            }
            nextViewPageWaitTime = ViewSystemUtilitys.CalculateWaitingTimeForCurrentOnLeave(viewItemNextPage);

            //等上一個頁面的 OnLeave 結束，注意，如果頁面中有大量的 Animator 這裡只能算出預估的結果 並且會限制最長時間為一秒鐘
            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(TimeForPerviousPageOnLeave);
            else
                yield return Yielders.GetWaitForSeconds(TimeForPerviousPageOnLeave);

            //在下一個頁面開始之前 先確保所有 ViewElement 已經被回收到池子
            runtimePool.RecoveryQueuedViewElement();

            //對進場的呼叫改變狀態(ViewPage)
            foreach (var item in viewItemNextPage)
            {
                if (item.runtimeViewElement == null)
                {
                    ViewSystemLog.LogError($"The runtimeViewElement is null for some reason, ignore this item.");
                    continue;
                }
                //套用複寫值
                item.runtimeViewElement.ApplyOverrides(item.overrideDatas);
                item.runtimeViewElement.ApplyEvent(item.eventDatas);

                //Delay 時間
                if (!lastPageItemDelayOutTimes.ContainsKey(item.runtimeViewElement.name))
                    lastPageItemDelayOutTimes.Add(item.runtimeViewElement.name, item.delayOut);
                else
                    lastPageItemDelayOutTimes[item.runtimeViewElement.name] = item.delayOut;

                if (item.runtimeParent == null)
                {
                    item.runtimeParent = transformCache.Find(item.parentPath);
                }

                item.runtimeViewElement.ChangePage(true, item.runtimeParent, item.TweenTime, item.delayIn, item.delayOut);
            }

            //對進場的呼叫改變狀態(ViewPage)
            foreach (var item in viewItemNextState)
            {
                if (item.runtimeViewElement == null)
                {
                    ViewSystemLog.LogError($"The runtimeViewElement is null for some reason, ignore this item.");
                    continue;
                }
                //套用複寫值
                item.runtimeViewElement.ApplyOverrides(item.overrideDatas);
                item.runtimeViewElement.ApplyEvent(item.eventDatas);

                //Delay 時間
                if (!lastPageItemDelayOutTimes.ContainsKey(item.runtimeViewElement.name))
                    lastPageItemDelayOutTimes.Add(item.runtimeViewElement.name, item.delayOut);
                else
                    lastPageItemDelayOutTimes[item.runtimeViewElement.name] = item.delayOut;

                item.runtimeViewElement.ChangePage(true, item.runtimeParent, item.TweenTime, item.delayIn, item.delayOut);
            }

            float OnShowAnimationFinish = ViewSystemUtilitys.CalculateTimesNeedsForOnShow(viewItemNextPage.Select(m => m.runtimeViewElement), maxClampTime);

            //更新狀態
            UpdateCurrentViewStateAndNotifyEvent(vp);

            OnChanged?.Invoke();

            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(OnShowAnimationFinish);
            else
                //通知事件
                yield return Yielders.GetWaitForSeconds(OnShowAnimationFinish);

            ChangePageToCoroutine = null;

            //Callback
            InvokeOnViewPageChangeEnd(this, new ViewPageEventArgs(currentViewPage, lastViewPage));

            //2019.12.18 due to there may be new Callback be add, change the  OnComplete to all is done.
            OnComplete?.Invoke();
            nextViewPage = null;
            nextViewState = null;
        }

        public override IEnumerator ShowOverlayViewPageBase(ViewPage vp, bool RePlayOnShowWhileSamePage, Action OnStart, Action OnComplete, bool ignoreTimeScale)
        {
            if (vp == null)
            {
                ViewSystemLog.Log("ViewPage is null");
                yield break;
            }
            if (vp.viewPageType != ViewPage.ViewPageType.Overlay)
            {
                ViewSystemLog.LogError("ViewPage " + vp.name + " is not an Overlay page");
                yield break;
            }

            var viewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);

            List<ViewElement> viewElementDoesExitsInNextPage = new List<ViewElement>();
            IEnumerable<ViewPageItem> viewItemNextPage = null;
            IEnumerable<ViewPageItem> viewItemNextState = null;

            string OverlayPageStateKey = GetOverlayStateKey(vp);
            bool samePage = false;
            //檢查是否有同 State 的 Overlay 頁面在場上
            if (overlayPageStatusDict.TryGetValue(OverlayPageStateKey, out ViewSystemUtilitys.OverlayPageStatus overlayPageStatus))
            {
                viewItemNextPage = PrepareRuntimeReference(GetAllViewPageItemInViewPage(vp));

                //同 OverlayState 的頁面已經在場上，移除不同的部分，然後顯示新加入的部分
                if (!string.IsNullOrEmpty(vp.viewState))
                {

                    if (overlayPageStatus.viewPage.name != vp.name)
                    {
                        // 同 State 不同 Page 的情況，找到差異的部分

                        foreach (var item in overlayPageStatus.viewPage.viewPageItems)
                        {
                            if (!vp.viewPageItems.Select(m => m.runtimeViewElement).Contains(item.runtimeViewElement))
                                viewElementDoesExitsInNextPage.Add(item.runtimeViewElement);
                        }
                        overlayPageStatus.viewPage = vp;
                    }
                }
                else
                {
                    //只有 ViewPage 卻還是進來這裡的話代表頁面還在場上
                    // RePlayOnShowWhileSamePage == false 那就更新數值 所以停掉舊的 Coroutine
                    if (overlayPageStatus.pageChangeCoroutine != null)
                    {
                        StopCoroutine(overlayPageStatus.pageChangeCoroutine);
                    }
                    samePage = true;
                    overlayPageStatus.transition = ViewSystemUtilitys.OverlayPageStatus.Transition.Show;
                }
            }
            else
            {
                //同 OverlayState 的頁面還不在場上 新建一個 Status

                overlayPageStatus = new ViewSystemUtilitys.OverlayPageStatus();
                overlayPageStatus.viewPage = vp;
                overlayPageStatus.viewState = viewState;
                overlayPageStatus.transition = ViewSystemUtilitys.OverlayPageStatus.Transition.Show;
                viewItemNextPage = PrepareRuntimeReference(GetAllViewPageItemInViewPage(vp));

                // 沒有 viewState 的 Page 不需要處理 viewState 的 runtimeViewElement
                if (!string.IsNullOrEmpty(vp.viewState))
                {
                    nextViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);
                    viewItemNextState = GetAllViewPageItemInViewState(nextViewState);
                    viewItemNextState = PrepareRuntimeReference(viewItemNextState);
                }

                overlayPageStatusDict.Add(OverlayPageStateKey, overlayPageStatus);
            }

            OnStart?.Invoke();

            float onShowTime = ViewSystemUtilitys.CalculateTimesNeedsForOnShow(viewItemNextPage.Select(m => m.runtimeViewElement));
            float onShowDelay = ViewSystemUtilitys.CalculateWaitingTimeForCurrentOnShow(viewItemNextPage);

            //對離場的呼叫改變狀態
            foreach (var item in viewElementDoesExitsInNextPage)
            {
                float delayOut = 0;
                lastOverlayPageItemDelayOutTimes.TryGetValue(item.name, out delayOut);
                item.ChangePage(false, null, 0, 0, delayOut);
            }

            float TimeForPerviousPageOnLeave = 0;
            switch (vp.viewPageTransitionTimingType)
            {
                case ViewPage.ViewPageTransitionTimingType.接續前動畫:
                    TimeForPerviousPageOnLeave = ViewSystemUtilitys.CalculateTimesNeedsForOnLeave(viewItemNextPage.Select(m => m.viewElement), maxClampTime);
                    break;
                case ViewPage.ViewPageTransitionTimingType.與前動畫同時:
                    TimeForPerviousPageOnLeave = 0;
                    break;
                case ViewPage.ViewPageTransitionTimingType.自行設定:
                    TimeForPerviousPageOnLeave = vp.customPageTransitionWaitTime;
                    break;
            }
            //等上一個頁面的 OnLeave 結束，注意，如果頁面中有大量的 Animator 這裡只能算出預估的結果 並且會限制最長時間為一秒鐘

            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(TimeForPerviousPageOnLeave);
            else
                yield return Yielders.GetWaitForSeconds(TimeForPerviousPageOnLeave);

            //對進場的呼叫改變狀態
            foreach (var item in viewItemNextPage)
            {
                if (RePlayOnShowWhileSamePage && samePage)
                {
                    item.runtimeViewElement.OnShow();
                    continue;
                }
                //套用複寫值
                item.runtimeViewElement.ApplyOverrides(item.overrideDatas);
                item.runtimeViewElement.ApplyEvent(item.eventDatas);

                //Delay 時間
                if (!lastOverlayPageItemDelayOutTimes.ContainsKey(item.runtimeViewElement.name))
                    lastOverlayPageItemDelayOutTimes.Add(item.runtimeViewElement.name, item.delayOut);
                else
                    lastOverlayPageItemDelayOutTimes[item.runtimeViewElement.name] = item.delayOut;

                if (item.runtimeParent == null)
                {
                    item.runtimeParent = transformCache.Find(item.parentPath);
                }

                item.runtimeViewElement.ChangePage(true, item.runtimeParent, item.TweenTime, item.delayIn, item.delayOut, reshowIfSamePage: RePlayOnShowWhileSamePage);
            }

            if (viewItemNextState != null)
            {
                foreach (var item in viewItemNextState)
                {
                    if (RePlayOnShowWhileSamePage)
                    {
                        item.runtimeViewElement.OnShow();
                        continue;
                    }
                    //套用複寫值
                    item.runtimeViewElement.ApplyOverrides(item.overrideDatas);
                    item.runtimeViewElement.ApplyEvent(item.eventDatas);

                    //Delay 時間
                    if (!lastOverlayPageItemDelayOutTimes.ContainsKey(item.runtimeViewElement.name))
                        lastOverlayPageItemDelayOutTimes.Add(item.runtimeViewElement.name, item.delayOut);
                    else
                        lastOverlayPageItemDelayOutTimes[item.runtimeViewElement.name] = item.delayOut;

                    if (item.runtimeParent == null)
                    {
                        item.runtimeParent = transformCache.Find(item.parentPath);
                    }

                    item.runtimeViewElement.ChangePage(true, item.runtimeParent, item.TweenTime, item.delayIn, item.delayOut, reshowIfSamePage: RePlayOnShowWhileSamePage);
                }
            }

            SetNavigationTarget(vp);

            //Fire the event
            InvokeOnOverlayPageShow(this, new ViewPageEventArgs(vp, null));

            //當所有表演都結束時
            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(onShowTime + onShowDelay);
            else
                yield return Yielders.GetWaitForSeconds(onShowTime + onShowDelay);

            overlayPageStatus.IsTransition = false;

            OnComplete?.Invoke();
        }

        public override IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageStatus overlayPageState, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false, bool ignoreTimeScale = false, bool waitForShowFinish = false)
        {
            if (waitForShowFinish && overlayPageState.IsTransition)
            {
                ViewSystemLog.Log("Leave Overlay Page wait for pervious page");
                yield return new WaitUntil(() => !overlayPageState.IsTransition);
            }

            var currentVe = currentViewPage.viewPageItems.Select(m => m.runtimeViewElement);
            var currentVs = currentViewState.viewPageItems.Select(m => m.runtimeViewElement);

            var finishTime = ViewSystemUtilitys.CalculateTimesNeedsForOnLeave(overlayPageState.viewPage.viewPageItems.Select(m => m.runtimeViewElement));

            overlayPageState.transition = ViewSystemUtilitys.OverlayPageStatus.Transition.Leave;

            List<ViewPageItem> viewPageItems = new List<ViewPageItem>();

            viewPageItems.AddRange(overlayPageState.viewPage.viewPageItems);
            if (overlayPageState.viewState != null)
                viewPageItems.AddRange(overlayPageState.viewState.viewPageItems);

            foreach (var item in viewPageItems)
            {
                // Unique 的 ViewElement 另外處理借用問題
                // 暫時不處理 多個 overlay 之間借用的問題！！！
                if (item.runtimeViewElement.IsUnique == true && IsPageTransition == false)
                {
                    if (currentVe.Contains(item.runtimeViewElement))
                    {
                        //準備自動離場的 ViewElement 目前的頁面正在使用中 所以不要對他操作
                        try
                        {

                            var vpi = currentViewPage.viewPageItems.FirstOrDefault(m => ReferenceEquals(m.runtimeViewElement, item.runtimeViewElement));
                            item.runtimeViewElement.ChangePage(true, vpi.runtimeParent, tweenTimeIfNeed, 0, 0);
                            ViewSystemLog.LogWarning("ViewElement : " + item.viewElement.name + "Try to back to origin Transfrom parent : " + vpi.runtimeParent.name);
                        }
                        catch { }
                        continue;
                    }
                    if (currentVs.Contains(item.runtimeViewElement))
                    {
                        //準備自動離場的 ViewElement 目前的頁面正在使用中 所以不要對他操作
                        try
                        {
                            var vpi = currentViewState.viewPageItems.FirstOrDefault(m => ReferenceEquals(m.runtimeViewElement, item.runtimeViewElement));
                            item.runtimeViewElement.ChangePage(true, vpi.runtimeParent, tweenTimeIfNeed, 0, 0);
                            ViewSystemLog.LogWarning("ViewElement : " + item.runtimeViewElement.name + "Try to back to origin Transfrom parent : " + vpi.runtimeParent.name);
                        }
                        catch { }
                        continue;
                    }
                }

                lastOverlayPageItemDelayOutTimes.TryGetValue(item.runtimeViewElement.name, out float delayOut);
                item.runtimeViewElement.ChangePage(false, null, 0, 0, delayOut, ignoreTransition);
            }

            //Get Back the Navigation to CurrentPage
            SetNavigationTarget(currentViewPage);
            InvokeOnOverlayPageLeave(this, new ViewPageEventArgs(overlayPageState.viewPage, null));

            if (ignoreTimeScale)
                yield return Yielders.GetWaitForSecondsRealtime(finishTime);
            else
                yield return Yielders.GetWaitForSeconds(finishTime);

            overlayPageState.IsTransition = false;

            string OverlayPageStateKey = GetOverlayStateKey(overlayPageState.viewPage);
            overlayPageStatusDict.Remove(OverlayPageStateKey);

            OnComplete?.Invoke();
        }
        public bool IsOverPageLive(string viewPageName, bool includeLeavingPage = false)
        {
            //取得 ViewPage 物件
            var vp = viewPages.SingleOrDefault(m => m.name == viewPageName);

            //沒有找到 
            if (vp == null)
            {
                ViewSystemLog.LogError("No view page match " + viewPageName + " Found");
                return false;
            }
            return IsOverPageLive(vp);
        }
        public bool IsOverPageLive(ViewPage viewPage, bool includeLeavingPage = false)
        {
            string OverlayPageStateKey = GetOverlayStateKey(viewPage);

            if (overlayPageStatusDict.TryGetValue(OverlayPageStateKey, out ViewSystemUtilitys.OverlayPageStatus overlayPageStatus))
            {
                if (overlayPageStatus.viewPage.name != viewPage.name)
                {
                    return false;
                }
                if (overlayPageStatus.transition == ViewSystemUtilitys.OverlayPageStatus.Transition.Leave)
                {
                    return includeLeavingPage;
                }
                return true;
            }

            return false;
            // IEnumerable<ViewSystemUtilitys.OverlayPageStatus> overlayPage = overlayPageStatusDict.Select(m => m.Value);
            // if (!includeLeavingPage)
            // {
            //     overlayPage = overlayPage.Where(m => m.transition != ViewSystemUtilitys.OverlayPageStatus.Transition.Leave);
            // }

            // foreach (var item in overlayPage)
            // {
            //     if (item.viewPage.name == viewPageName)
            //     {
            //         result = true;
            //         break;
            //     }
            // }
        }
        public override void TryLeaveAllOverlayPage()
        {
            //清空自動離場
            base.TryLeaveAllOverlayPage();
            for (int i = 0; i < overlayPageStatusDict.Count; i++)
            {
                var item = overlayPageStatusDict.ElementAt(i);
                StartCoroutine(LeaveOverlayViewPageBase(item.Value, 0.4f, null, true));
            }
        }
        int lastFrameRate;
        void UpdateCurrentViewStateAndNotifyEvent(ViewPage vp)
        {
            lastViewPage = currentViewPage;
            currentViewPage = vp;

            SetNavigationTarget(vp);

            InvokeOnViewPageChange(this, new ViewPageEventArgs(currentViewPage, lastViewPage));

            if (!string.IsNullOrEmpty(vp.viewState) && viewStatesNames.Contains(vp.viewState) && currentViewState.name != vp.viewState)
            {
                lastViewState = currentViewState;
                currentViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);

#if UNITY_EDITOR
                if (currentViewState.targetFrameRate != -1 &&
                    Application.targetFrameRate > currentViewState.targetFrameRate)
                {
                    lastFrameRate = Application.targetFrameRate;
                    Application.targetFrameRate = Mathf.Clamp(currentViewState.targetFrameRate, 15, 60);
                }
                else if (currentViewState.targetFrameRate == -1)
                {
                    Application.targetFrameRate = lastFrameRate;
                }
#endif

                InvokeOnViewStateChange(this, new ViewStateEventArgs(currentViewState, lastViewState));
            }
        }
        #region Navigation
        void SetNavigationTarget(ViewPage vp)
        {
            if (vp.IsNavigation && vp.firstSelected != null)
            {
                UnityEngine.EventSystems.EventSystem
                    .current.SetSelectedGameObject(vp.firstSelected.gameObject);
            }
        }
        /// <summary>
        /// Forcus the Navigation on target page,
        /// Note : only thi live view page will take effect and this function will not check the ViewPage live or not.
        /// </summary>
        /// <param name="vp"></param>
        public void SetUpNavigationOnViewPage(ViewPage vp)
        {
            DisableCurrentPageNavigation();
            DisableAllOverlayPageNavigation();

            var vpis = vp.viewPageItems;
            foreach (var vpi in vpis)
            {
                vpi.runtimeViewElement.ApplyNavigation(vpi.navigationDatas);
            }

            if (!string.IsNullOrEmpty(vp.viewState))
            {
                var vs = viewStates.SingleOrDefault(m => m.name == vp.viewState);
                var vpis_s = vs.viewPageItems;
                foreach (var vpi in vpis_s)
                {
                    if (vp.stateNavDict.TryGetValue(vpi.Id, out List<ViewElementNavigationData> result))
                    {
                        vpi.runtimeViewElement.ApplyNavigation(result);
                    }
                }
            }
        }

        public void DisableCurrentPageNavigation()
        {
            if (currentViewPage != null)
            {
                var vpis = currentViewPage.viewPageItems;
                foreach (var vpi in vpis)
                {
                    vpi.runtimeViewElement.runtimeOverride.DisableNavigation();
                }
            }
            if (currentViewState != null)
            {
                var vpis = currentViewState.viewPageItems;
                foreach (var vpi in vpis)
                {
                    vpi.runtimeViewElement.runtimeOverride.DisableNavigation();
                }
            }
        }

        public void DisableAllOverlayPageNavigation()
        {
            foreach (var item in overlayPageStatusDict)
            {
                var vpis = item.Value.viewPage.viewPageItems;
                foreach (var vpi in vpis)
                {
                    vpi.runtimeViewElement.runtimeOverride.DisableNavigation();
                }
                if (item.Value.viewState != null)
                {
                    var vpis_s = item.Value.viewState.viewPageItems;
                    foreach (var vpi in vpis)
                    {
                        vpi.runtimeViewElement.runtimeOverride.DisableNavigation();
                    }
                }
            }
        }

        #endregion

        #region Get ViewElement
        //Get ViewElement in viewPage
        public ViewElement GetViewPageElementByName(ViewPage viewPage, string viewPageItemName)
        {
            return viewPage.viewPageItems.SingleOrDefault((_) => _.displayName == viewPageItemName).runtimeViewElement;
        }
        public T GetViewPageElementComponentByName<T>(ViewPage viewPage, string viewPageItemName) where T : Component
        {
            return GetViewPageElementByName(viewPage, viewPageItemName).GetComponent<T>();
        }

        public ViewElement GetViewPageElementByName(string viewPageName, string viewPageItemName)
        {
            return GetViewPageElementByName(viewPages.SingleOrDefault(m => m.name == viewPageName), viewPageItemName);
        }
        public T GetViewPageElementComponentByName<T>(string viewPageName, string viewPageItemName) where T : Component
        {
            return GetViewPageElementByName(viewPageName, viewPageItemName).GetComponent<T>();
        }

        public ViewElement GetCurrentViewPageElementByName(string viewPageItemName)
        {
            return GetViewPageElementByName(currentViewPage, viewPageItemName);
        }
        public T GetCurrentViewPageElementComponentByName<T>(string viewPageItemName) where T : Component
        {
            return GetCurrentViewPageElementByName(viewPageItemName).GetComponent<T>();
        }

        //Get viewElement in statePage

        public ViewElement GetViewStateElementByName(ViewState viewState, string viewStateItemName)
        {
            return viewState.viewPageItems.SingleOrDefault((_) => _.displayName == viewStateItemName).runtimeViewElement;
        }
        public T GetViewStateElementComponentByName<T>(ViewState viewState, string viewStateItemName) where T : Component
        {
            return GetViewStateElementByName(viewState, viewStateItemName).GetComponent<T>();
        }

        public ViewElement GetViewStateElementByName(string viewStateName, string viewStateItemName)
        {
            return GetViewStateElementByName(viewStates.SingleOrDefault(m => m.name == viewStateName), viewStateItemName);
        }
        public T GetViewStateElementComponentByName<T>(string viewStateName, string viewStateItemName) where T : Component
        {
            return GetViewStateElementByName(viewStateName, viewStateItemName).GetComponent<T>();
        }

        public ViewElement GetCurrentViewStateElementByName(string viewStateItemName)
        {
            return GetViewStateElementByName(currentViewState, viewStateItemName);
        }
        public T GetCurrentViewStateElementComponentByName<T>(string viewStateItemName) where T : Component
        {
            return GetCurrentViewStateElementByName(viewStateItemName).GetComponent<T>();
        }

        #endregion
    }
}