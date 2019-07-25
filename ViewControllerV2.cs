using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
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

        public void RecoverViewElement(ViewElement toRecovery)
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
        public ViewElement RequestViewElement(ViewElement source)
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
                    a.name = source.name + "(Pooled)";
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
            if (gameObject.name != viewSystemSaveData.baseSetting.ViewControllerObjectPath)
            {
                Debug.LogWarning("The GameObject which attached ViewController is not match the setting in Base Setting.");
            }

            //Create UIRoot
            var ui = Instantiate(viewSystemSaveData.baseSetting.UIRoot);
            ui.transform.SetParent(transformCache);
            ui.transform.localPosition = viewSystemSaveData.baseSetting.UIRoot.transform.localPosition;
            ui.name = viewSystemSaveData.baseSetting.UIRoot.name;

            var go = new GameObject("ViewElementPool");
            go.transform.SetParent(transformCache);
            go.AddComponent<RectTransform>();
            viewElementPool = go.AddComponent<ViewElementPool>(); ;

            runtimePool = new ViewElementRuntimePool(viewElementPool);
            ViewElement.runtimePool = runtimePool;
            ViewElement.viewElementPool = viewElementPool;

            maxClampTime = viewSystemSaveData.baseSetting.MaxWaitingTime;
        }
        protected override void Start()
        {
            //Load ViewPages and ViewStates from ViewSystemSaveData

            viewStates = viewSystemSaveData.viewStates.Select(m => m.viewState).ToList();
            viewPages = viewSystemSaveData.viewPages.Select(m => m.viewPage).ToList();

            viewStatesNames = viewStates.Select(m => m.name);

            base.Start();
        }



        IEnumerable<ViewPageItem> PrepareRuntimeReference(IEnumerable<ViewPageItem> viewPageItems)
        {
            foreach (var item in viewPageItems)
            {
                if (item.runtimeParent == null)
                {
                    item.runtimeParent = transformCache.Find(item.parentPath);
                }
                if (item.runtimeViewElement == null)
                {
                    item.runtimeViewElement = runtimePool.RequestViewElement(item.viewElement);
                }
            }
            return viewPageItems;
        }

        private float nextViewPageWaitTime = 0;
        private Dictionary<string, float> lastPageItemDelayOutTimes = new Dictionary<string, float>();
        private Dictionary<string, float> lastPageItemDelayOutTimesOverlay = new Dictionary<string, float>();

        public override IEnumerator ChangePageBase(string viewPageName, Action OnComplete)
        {
            //取得 ViewPage 物件
            var vp = viewPages.SingleOrDefault(m => m.name == viewPageName);

            //沒有找到 
            if (vp == null)
            {
                Debug.LogError("No view page match" + viewPageName + "Found");
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
            var viewElementExitsInBothPage = viewItemNextPage.Where(m => m.runtimeViewElement.IsUnique).Intersect(viewItemCurrentPage.Where(m => m.runtimeViewElement.IsUnique)).Select(m => m.viewElement).ToList();

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
            currentLiveElements = viewItemNextPage.Select(m => m.runtimeViewElement).ToList();

            // Note: In ViewSystem V2 FullPage and OverlayPage will not share same ViewElement 
            //整理目前在畫面上 Overlay page 的 ViewPageItem
            // var CurrentOverlayViewPageItem = new List<ViewPageItem>();

            // foreach (var item in overlayPageStates.Select(m => m.Value.viewPage).Select(x => x.viewPageItems))
            // {
            //     CurrentOverlayViewPageItem.AddRange(item);
            // }

            // var CurrentOverlayViewElement = CurrentOverlayViewPageItem.Select(m => m.runtimeViewElement);
            //對離場的呼叫改變狀態
            foreach (var item in viewElementDoesExitsInNextPage)
            {
                // Note: In ViewSystem V2 FullPage and OverlayPage will not share same ViewElement 
                //如果 ViewElement 被 Overlay 頁面使用中就不執行 ChangePage
                // if (CurrentOverlayViewElement.Contains(item))
                // {
                //     Debug.Log(item.name);
                //     continue;
                // }

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

            //對進場的呼叫改變狀態
            foreach (var item in viewItemNextPage)
            {
                //Delay 時間
                if (!lastPageItemDelayOutTimes.ContainsKey(item.runtimeViewElement.name))
                    lastPageItemDelayOutTimes.Add(item.runtimeViewElement.name, item.delayOut);
                else
                    lastPageItemDelayOutTimes[item.runtimeViewElement.name] = item.delayOut;

                // Note: In ViewSystem V2 FullPage and OverlayPage will not share same ViewElement 
                //如果 ViewElement 被 Overlay 頁面使用中就不執行 ChangePage
                // if (CurrentOverlayViewElement.Contains(item.runtimeViewElement))
                // {
                //     Debug.Log(item.runtimeViewElement.name);
                //     continue;
                // }

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
            //Empty implement will override in child class
            yield return null;
        }

        public override IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageState overlayPageState, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false)
        {
            //Empty implement will override in child class
            yield return null;
        }
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
                Application.targetFrameRate = currentViewState.targetFrameRate;

                InvokeOnViewStateChange(this, new ViewStateEventArgs(currentViewState, lastViewState));
            }
        }

    }
}