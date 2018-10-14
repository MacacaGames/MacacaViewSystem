using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using Unity.Linq;
using UnityEngine;

namespace CloudMacaca.ViewSystem
{
    public class ViewController : MonoBehaviour
    {
        public event EventHandler<ViewStateEventArgs> OnViewStateChange;

        /// <summary>
        /// OnViewPageChange Calls on last page has leave finished, next page is ready to show.
        /// </summary>
        public event EventHandler<ViewPageEventArgs> OnViewPageChange;
        /// <summary>
        /// OnViewPageChangeStart Calls on page is ready to change with no error(eg. no page fonud etc.), and in this moment last page is still in view. 
        /// </summary>
        public event EventHandler<ViewPageTrisitionEventArgs> OnViewPageChangeStart;
        /// <summary>
        /// OnViewPageChangeEnd Calls on page is changed finish, all animation include in OnShow or OnLeave is finished.
        /// </summary>
        public event EventHandler<ViewPageEventArgs> OnViewPageChangeEnd;
        public event EventHandler<ViewPageEventArgs> OnOverlayPageShow;
        public event EventHandler<ViewPageEventArgs> OnOverlayPageLeave;
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
        public static ViewController Instance;
        public List<ViewPage> viewPage = new List<ViewPage>();
        public List<ViewState> viewStates = new List<ViewState>();
        private static IEnumerable<string> viewStatesNames;
        [ReadOnly, SerializeField]
        private List<ViewElement> currentLiveElement = new List<ViewElement>();
        [HideInInspector]
        public ViewPage lastViewPage;
        [HideInInspector]
        public ViewState lastViewState;
        [HideInInspector]
        public ViewPage currentViewPage;
        [HideInInspector]
        public ViewState currentViewState;
        // Use this for initialization
        void Awake()
        {
            Instance = this;
            if (viewElementPool == null)
            {
                try
                {
                    viewElementPool = (ViewElementPool)FindObjectOfType(typeof(ViewElementPool));
                }
                catch
                {

                }
            }
        }
        CloudMacaca.ViewSystem.ViewPageItem.PlatformOption platform;
        void Start()
        {
            viewStatesNames = viewStates.Select(m => m.name);

            SetupPlatformDefine();

            var initPage = viewPage.Where(m => m.initPage == true);
            if (initPage.Count() > 1)
            {
                Debug.LogError("More than 1 viewPage is set to Init Page");
            }
            else if (initPage.Count() == 1)
            {
                currentLiveElement = GetAllViewPageItemInViewPage(initPage.First()).Select(m => m.viewElement).ToList();
                currentViewPage = initPage.First();
            }
        }

        void SetupPlatformDefine()
        {

#if UNITY_EDITOR
            if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
            {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.iOS;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.tvOS)
            {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.tvOS;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
            {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.Android;
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WSAPlayer)
            {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.UWP;
            }
#else
            if (Application.platform == RuntimePlatform.IPhonePlayer) {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.iOS;
            } else if (Application.platform == RuntimePlatform.tvOS) {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.tvOS;
            } else if (Application.platform == RuntimePlatform.Android) {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.Android;
            } else if (Application.platform == RuntimePlatform.WSAPlayerARM ||
                Application.platform == RuntimePlatform.WSAPlayerX64 ||
                Application.platform == RuntimePlatform.WSAPlayerX86) {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.UWP;
            }
#endif
        }

        // Stack 後進先出
        private GameObject currentActiveObject;
        private Stack<ViewPage> subPageViewPage = new Stack<ViewPage>();
        private float nextViewPageWaitTime = 0;
        private Dictionary<string, float> lastPageItemTweenOutTimes = new Dictionary<string, float>();
        private Dictionary<string, float> lastPageItemDelayOutTimes = new Dictionary<string, float>();
        private Dictionary<string, float> lastPageItemDelayOutTimesOverlay = new Dictionary<string, float>();
        private Dictionary<string, bool> lastPageNeedLeaveOnFloat = new Dictionary<string, bool>();
        public bool IsPageTransition = false;
        public Coroutine ChangePageTo(string viewPageName, Action OnComplete = null, bool AutoWaitPreviousPageFinish = false)
        {
            // foreach (var item in currentLiveElement)
            // {
            //     if (!item.AnimatorIsInLoopOrEmptyOrDisableState())
            //     {
            //         Debug.LogWarning("Animation is not in loop state will not continue" + item.name);
            //         return null;
            //     }
            // }
            if (IsPageTransition && AutoWaitPreviousPageFinish == false)
            {
                Debug.LogError("Page is in Transition.");
                return null;
            }
            else if (IsPageTransition && AutoWaitPreviousPageFinish == true)
            {
                Debug.LogError("Page is in Transition but AutoWaitPreviousPageFinish");
                return StartCoroutine(WaitPrevious(viewPageName, OnComplete));
            }
            return StartCoroutine(ChangePageToBase(viewPageName, OnComplete));
        }
        public IEnumerator WaitPrevious(string viewPageName, Action OnComplete)
        {
            yield return new WaitUntil(() => IsPageTransition == false);
            yield return ChangePageToBase(viewPageName, OnComplete);
        }

        IEnumerable<ViewPageItem> GetAllViewPageItemInViewPage(ViewPage vp)
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
            viewItemForNextPage.AddRange(vp.viewPageItem);

            //並排除 Platform 該隔離的 ViewElement 放入 realViewPageItem
            realViewPageItem.Clear();
            foreach (var item in viewItemForNextPage)
            {
                if (item.excludePlatform.Contains(platform))
                {
                    item.parentGameObject.SetActive(false);
                }
                else
                {
                    item.parentGameObject.SetActive(true);
                    realViewPageItem.Add(item);
                }
            }

            return viewItemForNextPage;
        }
        public IEnumerator ChangePageToBase(string viewPageName, Action OnComplete)
        {

            //取得 ViewPage 物件
            var vp = viewPage.Where(m => m.name == viewPageName).SingleOrDefault();

            //沒有找到 
            if (vp == null)
            {
                Debug.LogError("No view page match" + viewPageName + "Found");
                //return;
                yield break;
            }

            if (vp.viewPageType == ViewPage.ViewPageType.Overlay)
            {
                Debug.LogWarning("To show Overlay ViewPage use ShowOverlayViewPage() method \n current version will redirect to this method automatically.");
                ShowOverlayViewPageBase(vp, true, OnComplete);
                //return;
                yield break;
            }

            //所有檢查都通過開始換頁
            IsPageTransition = true;

            if (OnViewPageChangeStart != null)
                OnViewPageChangeStart(this, new ViewPageTrisitionEventArgs(currentViewPage, vp));



            //viewItemNextPage 代表下個 ViewPage 應該出現的所有 ViewPageItem
            var viewItemNextPage = GetAllViewPageItemInViewPage(vp);
            var viewItemCurrentPage = GetAllViewPageItemInViewPage(currentViewPage);


            //尋找這個頁面還在，但下個頁面沒有的元件，這些元件應該先移除
            // 10/13 新邏輯 使用兩個 ViewPageItem 先連集在差集

            // 目前頁面 差集 （目前頁面與目標頁面的交集）
            //var viewElementDoesExitsInNextPage = viewItemCurrentPage.Except(viewItemCurrentPage.Intersect(viewItemNextPage)).Select(m => m.viewElement).ToList();
            var viewElementExitsInBothPage = viewItemNextPage.Intersect(viewItemCurrentPage).Select(m => m.viewElement).ToList();

            List<ViewElement> viewElementDoesExitsInNextPage = new List<ViewElement>();

            //尋找這個頁面還在，但下個頁面沒有的元件
            //就是存在 currentLiveElement 中但不存在 viewItemForNextPage 的傢伙要 ChangePage 
            var allViewElementForNextPage = viewItemNextPage.Select(m => m.viewElement).ToList();
            foreach (var item in currentLiveElement.ToArray())
            {
                //不存在的話就讓他加入應該移除的列表
                if (allViewElementForNextPage.Contains(item) == false)
                {
                    //加入該移除的列表
                    viewElementDoesExitsInNextPage.Add(item);
                }
            }


            currentLiveElement.Clear();
            currentLiveElement = viewItemNextPage.Select(m => m.viewElement).ToList();

            //整理目前在畫面上 Overlay page 的 ViewPageItem
            var CurrentOverlayViewPageItem = new List<ViewPageItem>();

            foreach (var item in overlayViewPageQueue.Select(m => m.viewPageItem))
            {
                CurrentOverlayViewPageItem.AddRange(item);
            }


            var CurrentOverlayViewElement = CurrentOverlayViewPageItem.Select(m => m.viewElement);
            //對離場的呼叫改變狀態
            foreach (var item in viewElementDoesExitsInNextPage)
            {
                //如果 ViewElement 被 Overlay 頁面使用中就不執行 ChangePage
                if (CurrentOverlayViewElement.Contains(item))
                {
                    Debug.Log(item.name);
                    continue;
                }
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
                    TimeForPerviousPageOnLeave = CalculateTimesNeedsForOnLeave(viewItemNextPage.Select(m => m.viewElement));
                    break;
                case ViewPage.ViewPageTransitionTimingType.與前動畫同時:
                    TimeForPerviousPageOnLeave = 0;
                    break;
                case ViewPage.ViewPageTransitionTimingType.自行設定:
                    TimeForPerviousPageOnLeave = vp.customPageTransitionWaitTime;
                    break;
            }
            nextViewPageWaitTime = CalculateWaitingTimeForCurrentOnLeave(viewItemNextPage);


            //等上一個頁面的 OnLeave 結束，注意，如果頁面中有大量的 Animator 這裡只能算出預估的結果 並且會限制最長時間為一秒鐘
            yield return Yielders.GetWaitForSeconds(TimeForPerviousPageOnLeave);

            //對進場的呼叫改變狀態
            foreach (var item in viewItemNextPage)
            {
                //Delay 時間
                if (!lastPageItemDelayOutTimes.ContainsKey(item.viewElement.name))
                    lastPageItemDelayOutTimes.Add(item.viewElement.name, item.delayOut);
                else
                    lastPageItemDelayOutTimes[item.viewElement.name] = item.delayOut;

                //如果 ViewElement 被 Overlay 頁面使用中就不執行 ChangePage
                if (CurrentOverlayViewElement.Contains(item.viewElement))
                {
                    Debug.Log(item.viewElement.name);
                    continue;
                }
                
                item.viewElement.ChangePage(true, item.parent, item.TweenTime, item.delayIn, item.delayOut);
            }

            float OnShowAnimationFinish = CalculateTimesNeedsForOnShow(viewItemNextPage.Select(m => m.viewElement));

            //更新狀態
            UpdateCurrentViewStateAndNotifyEvent(vp);

            //OnComplete Callback，08/28 雖然增加了計算至下個頁面的所需時間，但依然維持原本的時間點呼叫 Callback
            if (OnComplete != null) OnComplete();

            //通知事件
            yield return Yielders.GetWaitForSeconds(OnShowAnimationFinish);
            IsPageTransition = false;

            if (OnViewPageChangeEnd != null)
                OnViewPageChangeEnd(this, new ViewPageEventArgs(currentViewPage, lastViewPage));

        }

        public bool HasOverlayPageLive()
        {
            return overlayViewPageQueue.Count > 0;
        }
        public bool IsOverPageLive(string viewPageName)
        {
            return overlayViewPageQueue.Where(m => m.name == viewPageName).Count() > 0;
        }
        List<ViewPage> overlayViewPageQueue = new List<ViewPage>();
        Dictionary<string, IDisposable> autoLeaveQueue = new Dictionary<string, IDisposable>();
        public void ShowOverlayViewPage(string viewPageName, bool extendShowTimeWhenTryToShowSamePage = false, Action OnComplete = null)
        {
            var vp = viewPage.Where(m => m.name == viewPageName).SingleOrDefault();
            ShowOverlayViewPageBase(vp, extendShowTimeWhenTryToShowSamePage, OnComplete);
        }
        public void ShowOverlayViewPageBase(ViewPage vp, bool extendShowTimeWhenTryToShowSamePage, Action OnComplete)
        {
            if (vp == null)
            {
                Debug.Log("ViewPage is null");
                return;
            }
            if (vp.viewPageType != ViewPage.ViewPageType.Overlay)
            {
                Debug.LogError("ViewPage " + vp.name + " is not an Overlay page");
                return;
            }
            if (overlayViewPageQueue.Contains(vp) == false)
            {
                overlayViewPageQueue.Add(vp);
                foreach (var item in vp.viewPageItem)
                {
                    //Delay 時間
                    if (!lastPageItemDelayOutTimesOverlay.ContainsKey(item.viewElement.name))
                        lastPageItemDelayOutTimesOverlay.Add(item.viewElement.name, item.delayOut);
                    else
                        lastPageItemDelayOutTimesOverlay[item.viewElement.name] = item.delayOut;

                    item.viewElement.ChangePage(true, item.parent, item.TweenTime, item.delayIn, item.delayOut);
                }
            }

            if (extendShowTimeWhenTryToShowSamePage == false)
            {
                foreach (var item in vp.viewPageItem)
                {

                    item.viewElement.OnShow();

                }
            }

            //找到上一個相同名稱的代表這個頁面要繼續出現，所以把上一個計時器移除
            IDisposable lastAutoLeaveQueue;
            if (autoLeaveQueue.TryGetValue(vp.name, out lastAutoLeaveQueue))
            {
                //Debug.Log("find last");
                //停止計時器
                lastAutoLeaveQueue.Dispose();
                //從字典移除
                autoLeaveQueue.Remove(vp.name);
            }

            if (vp.autoLeaveTimes > 0)
            {
                var d = Observable
                    .Timer(TimeSpan.FromSeconds(vp.autoLeaveTimes))
                    .Subscribe(
                        _ =>
                        {
                            //Debug.Log("Try to Leave Page");
                            LeaveOverlayViewPage(vp.name);
                            //計時器成功完成時也要從字典移除
                            autoLeaveQueue.Remove(vp.name);
                            if (OnComplete != null)
                            {
                                OnComplete();
                            }
                        }
                    );
                autoLeaveQueue.Add(vp.name, d);
            }
            if (OnOverlayPageShow != null)
                OnOverlayPageShow(this, new ViewPageEventArgs(vp, null));
        }
        public void TryLeaveAllOverlayPage()
        {
            for (int i = 0; i < overlayViewPageQueue.Count; i++)
            {
                Debug.Log(overlayViewPageQueue[i].name);
                StartCoroutine(LeaveOverlayViewPageBase(overlayViewPageQueue[i], 0.4f, null, true));
            }
        }
        public void LeaveOverlayViewPage(string viewPageName, float tweenTimeIfNeed = 0.4f, Action OnComplete = null)
        {
            var vp = overlayViewPageQueue.Where(m => m.name == viewPageName).SingleOrDefault();

            if (vp == null)
            {
                Debug.Log("No live overlay viewPage of name: " + viewPageName + "  found");
                return;
            }

            StartCoroutine(LeaveOverlayViewPageBase(vp, tweenTimeIfNeed, OnComplete));
        }

        public IEnumerator LeaveOverlayViewPageBase(ViewPage vp, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false)
        {
            var currentVe = currentViewPage.viewPageItem.Select(m => m.viewElement);
            var currentVs = currentViewState.viewPageItems.Select(m => m.viewElement);

            var finishTime = CalculateTimesNeedsForOnLeave(currentVe);

            foreach (var item in vp.viewPageItem)
            {
                if (currentVe.Contains(item.viewElement))
                {
                    //準備自動離場的 ViewElement 目前的頁面正在使用中 所以不要對他操作
                    try
                    {
                        var vpi = currentViewPage.viewPageItem.FirstOrDefault(m => m.viewElement == item.viewElement);
                        Debug.LogWarning("ViewElement : " + item.viewElement.name + "Try to back to origin Transfrom parent : " + vpi.parent.name);
                        item.viewElement.ChangePage(true, vpi.parent, tweenTimeIfNeed, 0, 0);
                    }
                    catch { }
                    continue;
                }
                if (currentVs.Contains(item.viewElement))
                {
                    //準備自動離場的 ViewElement 目前的頁面正在使用中 所以不要對他操作
                    try
                    {
                        var vpi = currentViewState.viewPageItems.FirstOrDefault(m => m.viewElement == item.viewElement);
                        Debug.LogWarning("ViewElement : " + item.viewElement.name + "Try to back to origin Transfrom parent : " + vpi.parent.name);
                        item.viewElement.ChangePage(true, vpi.parent, tweenTimeIfNeed, 0, 0);
                    }
                    catch { }
                    continue;
                }
                float delayOut = 0;
                lastPageItemDelayOutTimesOverlay.TryGetValue(item.viewElement.name, out delayOut);
                item.viewElement.ChangePage(false, null, 0, 0, delayOut, ignoreTransition);
            }

            overlayViewPageQueue.Remove(vp);

            if (OnOverlayPageLeave != null)
                OnOverlayPageLeave(this, new ViewPageEventArgs(vp, null));

            yield return Yielders.GetWaitForSeconds(finishTime);

            if (OnComplete != null)
            {
                OnComplete();
            }
        }

        void UpdateCurrentViewStateAndNotifyEvent(ViewPage vp)
        {
            lastViewPage = currentViewPage;
            currentViewPage = vp;
            if (OnViewPageChange != null)
                OnViewPageChange(this, new ViewPageEventArgs(currentViewPage, lastViewPage));

            if (!string.IsNullOrEmpty(vp.viewState) && viewStatesNames.Contains(vp.viewState) && currentViewState.name != vp.viewState)
            {
                lastViewState = currentViewState;
                currentViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);

                if (OnViewStateChange != null)
                    OnViewStateChange(this, new ViewStateEventArgs(currentViewState, lastViewState));
            }
        }

        static float maxClampTime = 1;

        float CalculateTimesNeedsForOnLeave(IEnumerable<ViewElement> viewElements)
        {
            float maxOutAnitionTime = 0;

            foreach (var item in viewElements)
            {
                float t = 0;
                if (item.transition == ViewElement.TransitionType.Animator)
                {
                    t = item.GetOutAnimationLength();
                }
                else if (item.transition == ViewElement.TransitionType.CanvasGroupAlpha)
                {
                    t = item.canvasOutTime;
                }

                if (t > maxOutAnitionTime)
                {
                    maxOutAnitionTime = t;
                }
            }
            return Mathf.Clamp(maxOutAnitionTime, 0, maxClampTime);
        }

        float CalculateTimesNeedsForOnShow(IEnumerable<ViewElement> viewElements)
        {
            float maxInAnitionTime = 0;

            foreach (var item in viewElements)
            {
                float t = 0;
                if (item.transition == ViewElement.TransitionType.Animator)
                {
                    t = item.GetInAnimationLength();
                }
                else if (item.transition == ViewElement.TransitionType.CanvasGroupAlpha)
                {
                    t = item.canvasInTime;
                }

                if (t > maxInAnitionTime)
                {
                    maxInAnitionTime = t;
                }
            }
            return Mathf.Clamp(maxInAnitionTime, 0, maxClampTime);
            //return maxOutAnitionTime;
        }
        float CalculateWaitingTimeForCurrentOnLeave(IEnumerable<ViewPageItem> viewPageItem)
        {
            float maxDelayTime = 0;
            foreach (var item in viewPageItem)
            {
                float t2 = item.delayOut;
                if (t2 > maxDelayTime)
                {
                    maxDelayTime = t2;
                }
            }
            return maxDelayTime;
        }
        public ViewElementPool viewElementPool;
    }
}