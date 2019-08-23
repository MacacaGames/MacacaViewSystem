using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using UniRx;
namespace CloudMacaca.ViewSystem
{
    public class ViewElementRuntimePool
    {
        ViewElementPool _hierachyPool;
        public ViewElementRuntimePool(ViewElementPool hierachyPool)
        {
            _hierachyPool = hierachyPool;
        }

        Dictionary<string, Queue<ViewElement>> veDicts = new Dictionary<string, Queue<ViewElement>>();
        Dictionary<string, ViewElement> uniqueVeDicts = new Dictionary<string, ViewElement>();

        Queue<ViewElement> recycleQueue = new Queue<ViewElement>();
        public void QueueViewElementToRecovery(ViewElement toRecovery)
        {
            recycleQueue.Enqueue(toRecovery);
        }

        public void RecoveryViewElement(ViewElement toRecovery)
        {
            if (toRecovery.IsUnique)
            {
                //Currentlly nothing needs to do.
            }
            else
            {
                if (!veDicts.TryGetValue(toRecovery.PoolKey, out Queue<ViewElement> veQueue))
                {
                    UnityEngine.Debug.LogWarning("Cannot find pool of ViewElement " + toRecovery.name + ", Destroy directly.");
                    UnityEngine.Object.Destroy(toRecovery);
                }
                veQueue.Enqueue(toRecovery);
            }
        }

        public void RecoveryQueuedViewElement()
        {
            while (recycleQueue.Count > 0)
            {
                RecoveryViewElement(recycleQueue.Dequeue());
            }
        }
        public ViewElement PrewarmUniqueViewElement(ViewElement source)
        {
            if (!source.IsUnique)
            {
                Debug.LogWarning("The ViewElement trying to Prewarm is not an unique ViewElement");
                return null;
            }

            if (!uniqueVeDicts.ContainsKey(source.name))
            {
                var temp = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
                temp.name = source.name + "(Unique)";
                uniqueVeDicts.Add(source.name, temp);
                temp.gameObject.SetActive(false);
                return temp;
            }
            else
            {
                Debug.LogWarning("ViewElement " + source.name + " has been prewarmed");
                return null;
            }
        }
        public ViewElement RequestViewElement(ViewElement source, bool isOverlay)
        {
            ViewElement result;

            if (source.IsUnique)
            {
                if (!uniqueVeDicts.TryGetValue(source.name, out result))
                {
                    result = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
                    result.name = source.name + "(Unique)";
                    uniqueVeDicts.Add(source.name, result);
                }
            }
            else
            {
                if (!veDicts.TryGetValue(source.name, out Queue<ViewElement> veQueue))
                {
                    veQueue = new Queue<ViewElement>();
                    veDicts.Add(source.name, veQueue);
                }
                if (veQueue.Count == 0)
                {
                    var a = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
                    //a.name = source.name + (isOverlay ? "(Overlay)" : "(Pooled)");
                    a.name = source.name + ("(Pooled)");
                    veQueue.Enqueue(a);
                }
                result = veQueue.Dequeue();
            }
            result.PoolKey = source.name;
            return result;
        }
    }
    public class ViewControllerV2 : ViewControllerBase
    {
        public static ViewControllerV2 Instance;
        public static ViewElementRuntimePool runtimePool;
        public ViewElementPool viewElementPool;
        static float maxClampTime = 1;

        [SerializeField]
        private ViewSystemSaveData viewSystemSaveData;

        // Use this for initialization
        protected override void Awake()
        {
            base.Awake();
            Instance = this;

            //Create ViewElementPool
            if (gameObject.name != viewSystemSaveData.globalSetting.ViewControllerObjectPath)
            {
                Debug.LogWarning("The GameObject which attached ViewController is not match the setting in Base Setting.");
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

            runtimePool = new ViewElementRuntimePool(viewElementPool);
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
        public T GetInjectionInstance<T>() where T : Component, IViewElementInjectalbe
        {
            if (InjectionDictionary.TryGetValue(typeof(T), out Component result))
            {
                return (T)result;
            }
            throw new MissingReferenceException("Target type cannot been found, are you sure your ViewElement which attach target Component is unique?");
        }
        void PrewarmInjection()
        {
            var viewElementsInStates = viewStates.Select(m => m.viewPageItems).SelectMany(ma => ma).Select(m => m.viewElement);
            var viewElementsInPages = viewPages.Select(m => m.viewPageItems).SelectMany(ma => ma).Select(m => m.viewElement);
            foreach (var item in viewElementsInStates)
            {
                if (!item.IsUnique)
                {
                    continue;
                }
             
                var r = runtimePool.PrewarmUniqueViewElement(item);
                if (r != null)
                {
                    foreach (var i in r.GetComponents<IViewElementInjectalbe>())
                    {
                        var c = (Component)i;
                        var t = c.GetType();
                        if (!InjectionDictionary.ContainsKey(t))
                            InjectionDictionary.Add(t, c);
                        else
                        {
                            Debug.LogWarning("Type " + t + " has been injected");
                            continue;
                        }
                    }
                }
            }

            foreach (var item in viewElementsInPages)
            {
                if (!item.IsUnique)
                {
                    continue;
                }
              
                var r = runtimePool.PrewarmUniqueViewElement(item);
                if (r != null)
                {
                    foreach (var i in r.GetComponents<IViewElementInjectalbe>())
                    {
                        var c = (Component)i;
                        var t = c.GetType();
                        if (!InjectionDictionary.ContainsKey(t))
                            InjectionDictionary.Add(t, c);
                        else
                        {
                            Debug.LogWarning("Type " + t + " has been injected");
                            continue;
                        }
                    }
                }
            }
        }
        #endregion
        IEnumerable<ViewPageItem> PrepareRuntimeReference(IEnumerable<ViewPageItem> viewPageItems, bool isOverlay = false)
        {
            foreach (var item in viewPageItems)
            {
                if (item.runtimeParent == null)
                {
                    item.runtimeParent = transformCache.Find(item.parentPath);
                }
                if (item.runtimeViewElement == null)
                {
                    item.runtimeViewElement = runtimePool.RequestViewElement(item.viewElement, isOverlay);
                }
            }
            return viewPageItems;
        }

        private float nextViewPageWaitTime = 0;
        private Dictionary<string, float> lastPageItemDelayOutTimes = new Dictionary<string, float>();
        private Dictionary<string, float> lastOverlayPageItemDelayOutTimes = new Dictionary<string, float>();

        public override IEnumerator ChangePageBase(string viewPageName, Action OnComplete)
        {
            //取得 ViewPage 物件
            var vp = viewPages.SingleOrDefault(m => m.name == viewPageName);

            //沒有找到 
            if (vp == null)
            {
                Debug.LogError("No view page match " + viewPageName + " Found");
                ChangePageToCoroutine = null;
                yield break;
            }

            if (vp.viewPageType == ViewPage.ViewPageType.Overlay)
            {
                Debug.LogWarning("To shown Page is an Overlay ViewPage use ShowOverlayViewPage() instead method \n current version will redirect to this method automatically.");
                ShowOverlayViewPageBase(vp, true, OnComplete);
                ChangePageToCoroutine = null;
                yield break;
            }

            //所有檢查都通過開始換頁
            //IsPageTransition = true;
            InvokeOnViewPageChangeEnd(this, new ViewPageTrisitionEventArgs(currentViewPage, vp));

            nextViewPage = vp;
            nextViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);

            //viewItemNextPage 代表下個 ViewPage 應該出現的所有 ViewPageItem
            var viewItemNextPage = PrepareRuntimeReference(GetAllViewPageItemInViewPage(vp));
            //viewItemCurrentPage 代表目前 ViewPage 已出現的所有 ViewPageItem
            var viewItemCurrentPage = PrepareRuntimeReference(GetAllViewPageItemInViewPage(currentViewPage));

            //尋找這個頁面還在，但下個頁面沒有的元件，這些元件應該先移除
            // 10/13 新邏輯 使用兩個 ViewPageItem 先連集在差集
            // var viewElementExitsInBothPage = viewItemNextPage.Where(m => m.runtimeViewElement.IsUnique).Intersect(viewItemCurrentPage.Where(m => m.runtimeViewElement.IsUnique)).Select(m => m.viewElement).ToList();

            List<ViewElement> viewElementDoesExitsInNextPage = new List<ViewElement>();
            //尋找這個頁面還在，但下個頁面沒有的元件
            //就是存在 currentLiveElement 中但不存在 viewItemForNextPage 的傢伙要 ChangePage 
            var allViewElementForNextPage = viewItemNextPage.Select(m => m.runtimeViewElement).ToList();
            foreach (var item in currentLiveElements.ToArray())
            {
                //不存在的話就讓他加入應該移除的列表
                if (allViewElementForNextPage.Contains(item) == false)
                {
                    //加入該移除的列表
                    viewElementDoesExitsInNextPage.Add(item);
                }
            }

            currentLiveElements.Clear();
            currentLiveElements = allViewElementForNextPage;

            //對離場的呼叫改變狀態
            foreach (var item in viewElementDoesExitsInNextPage)
            {
                float delayOut = 0;
                lastPageItemDelayOutTimes.TryGetValue(item.name, out delayOut);
                item.ChangePage(false, null, 0, 0, delayOut);
            }

            lastPageItemDelayOutTimes.Clear();

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
            yield return Yielders.GetWaitForSeconds(TimeForPerviousPageOnLeave);

            //在下一個頁面開始之前 先確保所有 ViewElement 已經被回收到池子
            runtimePool.RecoveryQueuedViewElement();

            //對進場的呼叫改變狀態
            foreach (var item in viewItemNextPage)
            {
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

            float OnShowAnimationFinish = ViewSystemUtilitys.CalculateTimesNeedsForOnShow(viewItemNextPage.Select(m => m.viewElement), maxClampTime);

            //更新狀態
            UpdateCurrentViewStateAndNotifyEvent(vp);

            //OnComplete Callback，08/28 雖然增加了計算至下個頁面的所需時間，但依然維持原本的時間點呼叫 Callback
            if (OnComplete != null) OnComplete();

            //通知事件
            yield return Yielders.GetWaitForSeconds(OnShowAnimationFinish);

            ChangePageToCoroutine = null;

            //Callback
            InvokeOnViewPageChangeEnd(this, new ViewPageEventArgs(currentViewPage, lastViewPage));
        }

        public override IEnumerator ShowOverlayViewPageBase(ViewPage vp, bool RePlayOnShowWhileSamePage, Action OnComplete)
        {
            if (vp == null)
            {
                Debug.Log("ViewPage is null");
                yield break;
            }
            if (vp.viewPageType != ViewPage.ViewPageType.Overlay)
            {
                Debug.LogError("ViewPage " + vp.name + " is not an Overlay page");
                yield break;
            }
            var currentPageItem = PrepareRuntimeReference(GetAllViewPageItemInViewPage(vp), true);
            float onShowTime = ViewSystemUtilitys.CalculateTimesNeedsForOnShow(currentPageItem.Select(m => m.runtimeViewElement));
            float onShowDelay = ViewSystemUtilitys.CalculateWaitingTimeForCurrentOnShow(currentPageItem);



            if (overlayPageStates.TryGetValue(vp.name, out ViewSystemUtilitys.OverlayPageState overlayPageState) == false)
            {
                overlayPageState = new ViewSystemUtilitys.OverlayPageState();
                overlayPageState.viewPage = vp;
                overlayPageState.IsTransition = true;
                overlayPageStates.Add(vp.name, overlayPageState);
                foreach (var item in vp.viewPageItems)
                {
                    //套用複寫值
                    item.runtimeViewElement.ApplyOverrides(item.overrideDatas);
                    item.runtimeViewElement.ApplyEvent(item.eventDatas);

                    //Delay 時間
                    //Need review
                    if (!lastOverlayPageItemDelayOutTimes.ContainsKey(item.runtimeViewElement.name))
                        lastOverlayPageItemDelayOutTimes.Add(item.runtimeViewElement.name, item.delayOut);
                    else
                        lastOverlayPageItemDelayOutTimes[item.runtimeViewElement.name] = item.delayOut;

                    item.runtimeViewElement.ChangePage(true, item.runtimeParent, item.TweenTime, item.delayIn, item.delayOut);
                }
            }
            else
            {
                //如果已經存在的話要更新數值 所以停掉舊的 Coroutine
                if (overlayPageState.pageChangeCoroutine != null)
                {
                    StopCoroutine(overlayPageState.pageChangeCoroutine);
                }
                overlayPageState.IsTransition = true;
            }

            //對於指定強制重播的對象 直接重播
            if (RePlayOnShowWhileSamePage == true)
            {
                foreach (var item in vp.viewPageItems)
                {
                    item.runtimeViewElement.OnShow();
                }
            }

            if (vp.autoLeaveTimes > 0)
            {
                var currentAutoLeave = autoLeaveQueue.SingleOrDefault(m => m.name == vp.name);
                if (currentAutoLeave != null)
                {
                    //更新倒數計時器
                    currentAutoLeave.times = vp.autoLeaveTimes;
                }
                else
                {
                    //沒有的話新增一個
                    autoLeaveQueue.Add(new AutoLeaveData(vp.name, vp.autoLeaveTimes));
                }
            }

            //Fire the event
            InvokeOnOverlayPageShow(this, new ViewPageEventArgs(vp, null));

            //當所有表演都結束時
            yield return Yielders.GetWaitForSeconds(onShowTime + onShowDelay);

            if (overlayPageStates.ContainsKey(vp.name)) overlayPageStates[vp.name].IsTransition = false;

            if (OnComplete != null)
            {
                OnComplete();
            }
        }

        public override IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageState overlayPageState, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false)
        {
            // var currentVe = currentViewPage.viewPageItems.Select(m => m.viewElement);
            // var currentVs = currentViewState.viewPageItems.Select(m => m.viewElement);

            var finishTime = ViewSystemUtilitys.CalculateTimesNeedsForOnLeave(overlayPageState.viewPage.viewPageItems.Select(m => m.runtimeViewElement));

            overlayPageState.IsTransition = true;

            foreach (var item in overlayPageState.viewPage.viewPageItems)
            {
                // if (IsPageTransition == false)
                // {
                //     if (currentVe.Contains(item.viewElement))
                //     {
                //         //準備自動離場的 ViewElement 目前的頁面正在使用中 所以不要對他操作
                //         try
                //         {
                //             var vpi = currentViewPage.viewPageItems.FirstOrDefault(m => m.viewElement == item.viewElement);
                //             Debug.LogWarning("ViewElement : " + item.viewElement.name + "Try to back to origin Transfrom parent : " + vpi.parent.name);
                //             item.viewElement.ChangePage(true, vpi.parent, tweenTimeIfNeed, 0, 0);
                //         }
                //         catch { }
                //         continue;
                //     }
                //     if (currentVs.Contains(item.viewElement))
                //     {
                //         //準備自動離場的 ViewElement 目前的頁面正在使用中 所以不要對他操作
                //         try
                //         {
                //             var vpi = currentViewState.viewPageItems.FirstOrDefault(m => m.viewElement == item.viewElement);
                //             Debug.LogWarning("ViewElement : " + item.viewElement.name + "Try to back to origin Transfrom parent : " + vpi.parent.name);
                //             item.viewElement.ChangePage(true, vpi.parent, tweenTimeIfNeed, 0, 0);
                //         }
                //         catch { }
                //         continue;
                //     }
                // }
                // else
                // {
                //     //Do nothing here
                // }
                lastOverlayPageItemDelayOutTimes.TryGetValue(item.viewElement.name, out float delayOut);
                item.runtimeViewElement.ChangePage(false, null, 0, 0, delayOut, ignoreTransition);
            }

            InvokeOnOverlayPageLeave(this, new ViewPageEventArgs(overlayPageState.viewPage, null));

            yield return Yielders.GetWaitForSeconds(finishTime);

            overlayPageState.IsTransition = false;

            overlayPageStates.Remove(overlayPageState.viewPage.name);
            if (OnComplete != null)
            {
                OnComplete();
            }
        }
        int lastFrameRate;
        void UpdateCurrentViewStateAndNotifyEvent(ViewPage vp)
        {
            nextViewPage = null;
            nextViewState = null;

            lastViewPage = currentViewPage;
            currentViewPage = vp;

            InvokeOnViewPageChange(this, new ViewPageEventArgs(currentViewPage, lastViewPage));

            if (!string.IsNullOrEmpty(vp.viewState) && viewStatesNames.Contains(vp.viewState) && currentViewState.name != vp.viewState)
            {
                lastViewState = currentViewState;
                currentViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);

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

                InvokeOnViewStateChange(this, new ViewStateEventArgs(currentViewState, lastViewState));
            }
        }

    }
}