using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Linq;
using UniRx;
using System;
#if FullInspector
using FullInspector;
#endif

namespace CloudMacaca.ViewSystem
{
#if FullInspector
    public class ViewController : BaseBehavior
#else
    public class ViewController : MonoBehavior
#endif
    {
        public event EventHandler<ViewStateEventArgs> OnViewStateChange;
        public class ViewStateEventArgs : EventArgs
        {
            // ...省略額外參數
            public ViewPage currentViewPage;
            public ViewPage lastViewPage;
            public ViewState currentViewState;
            public ViewState lastViewState;
            public ViewStateEventArgs(ViewPage CurrentViewPage, ViewPage LastViewPage, ViewState CurrentViewState, ViewState LastVilastViewStateew)
            {
                this.currentViewPage = CurrentViewPage;
                this.lastViewPage = LastViewPage;
                this.currentViewState = CurrentViewState;
                this.lastViewState = LastVilastViewStateew;
            }

        }
        public static ViewController Instance;
        //[HideInInspector]
        public List<ViewPage> viewPage = new List<ViewPage>();
        public List<ViewState> viewStates = new List<ViewState>();
        private static IEnumerable<string> viewStatesNames;
        [ShowInInspector]
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

        }
        void Start()
        {

            GameController.Instance.CallMissionHintUI += CallMissionUI;

            viewStatesNames = viewStates.Select(m => m.name);

        }
        void CallMissionUI()
        {
            ChangePageTo("Play_ShowMission",
                () =>
                {
                    GameController.Instance.MissionCheckDone = true;
                }
            );
        }

        // Stack 後進先出
        private GameObject currentActiveObject;
        private Stack<ViewPage> subPageViewPage = new Stack<ViewPage>();
        private float nextViewPageWaitTime = 0;
        private Dictionary<string, float> lastPageItemTweenOutTimes = new Dictionary<string, float>();
        private Dictionary<string, float> lastPageItemDelayOutTimes = new Dictionary<string, float>();
        private Dictionary<string, bool> lastPageNeedLeaveOnFloat = new Dictionary<string, bool>();

        public void ChangePageTo(string viewPageName, Action OnComplete = null)
        {
            foreach (var item in currentLiveElement)
            {
                if (!item.AnimatorIsInLoopOrEmptyOrDisableState())
                {
                    Debug.LogWarning("Animation is not in loop state will not continue" + item.name);
                    return;
                }
            }
            StartCoroutine(ChangePageToBase(viewPageName, OnComplete));
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

            // 普通的頁面不可以再有 overlay 頁面存在時進行轉換
            if (subPageViewPage.Count > 0 && vp.viewPageType != ViewPage.ViewPageType.SubPage)
            {
                Debug.LogError("Full Page viewPage only can change while there is no SubPage viewPage exsit");
                Debug.LogError("Current live SubPage ViewPage");
                foreach (var item in subPageViewPage)
                {
                    Debug.LogError(item.name);
                }
                //return;
                yield break;
            }

            if (vp.viewPageType == ViewPage.ViewPageType.Overlay)
            {
                Debug.LogWarning("To show Overlay ViewPage use ShowOverlayViewPage() method \n current version will redirect to this method automatically.");
                ShowOverlayViewPage(vp.name, true, OnComplete);

                //return;
                yield break;
            }


            //SubPage 頁面最後處理
            if (vp.viewPageType == ViewPage.ViewPageType.SubPage)
            {
                //在已經存在的 SubPage viewpage 中檢查是否有跟下一個要切換的ViewPage相同
                if (subPageViewPage.Count > 0)
                {
                    //是的話
                    if (subPageViewPage.Last() == vp)
                    {
                        Debug.LogWarning("This SubPage ViewPage is in the Front");
                        //return;
                        yield break;
                    }
                    //如果佇列中沒有相同的頁面時就允許繼續加入新頁面
                    if (subPageViewPage.Where(m => m == vp).Count() == 0)
                        subPageViewPage.Push(vp);
                }
                else
                {

                    subPageViewPage.Push(vp);
                }

                //check if is subPag viewpage add to stack 

                foreach (var item in subPageViewPage.Pop().viewPageItem)
                {
                    if (vp.viewPageItem.Where(m => m.viewElement == item.viewElement).Count() > 0) continue;
                    item.viewElement.OnLeave();
                    //return;
                    //yield break;
                }

            }



            //先整理出下個頁面應該出現的 ViewItem
            ViewState viewPagePresetTemp;
            List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem>();

            //從 ViewPagePreset 尋找
            if (!string.IsNullOrEmpty(vp.viewState))
            {
                //viewState.TryGetValue(vp.viewState, out viewPagePresetTemp);
                viewPagePresetTemp = viewStates.SingleOrDefault(m => m.name == vp.viewState);
                if (viewPagePresetTemp != null)
                {
                    foreach (var k in viewPagePresetTemp.viewElements)
                    {
                        viewItemForNextPage.Add(new ViewPageItem(k));
                    }
                }
            }

            //從 ViewPage 尋找
            //viewElementForNextPage.AddRange(vp.viewPageItem.viewElement);
            foreach (var item in vp.viewPageItem)
            {
                viewItemForNextPage.Add(item);
            }

            //要被移開的元件
            List<ViewElement> viewElementNeedsLeave = new List<ViewElement>();

            //尋找應該離場的元件
            //就是存在 currentLiveElement 中但不存在 viewItemForNextPage 的傢伙要 OnLeave
            var allViewElementForNextPage = viewItemForNextPage.Select(m => m.viewElement).ToList();
            foreach (var item in currentLiveElement.ToArray())
            {
                //不存在的話就讓他加入應該移除的列表
                if (!allViewElementForNextPage.Contains(item))
                {
                    //加入該移除的列表
                    viewElementNeedsLeave.Add(item);

                    //從目前存在的頁面移除
                    currentLiveElement.Remove(item);
                }
            }

            //整理已經存在的頁面
            foreach (var item in viewItemForNextPage.ToArray())
            {
                //currentLiveElement 有找到代表不應該執行 OnShow 除非他要改變 Parent
                if (currentLiveElement.Contains(item.viewElement))
                {
                    if (item.parent == null)
                    {
                        viewItemForNextPage.Remove(item);
                    }
                    //因為 有 Parent 的物件會在執行一次 OnShow 所以避免被加入 currentLiveElement 兩次 要移除
                    else
                    {
                        currentLiveElement.Remove(item.viewElement);



                    }
                }
                //最後 在下個頁面中是isFloating 又沒有 parent 的要 OnLeave
                if (item.viewElement.isFloating == true && item.parent == null)
                {
                    viewElementNeedsLeave.Add(item.viewElement);
                }
            }





            //所有下個頁面應該出現的 ViewElement
            //var allViewElementForNextPage = viewItemForNextPage.Select(m => m.viewElement).ToList();
            //檢查所有下個頁面應該出現的 ViewElement 中是不是有已經在 當前頁面存在了
            // foreach (var item in currentLiveElement.ToArray())
            // {

            //     //不存在的話就讓他加入應該移除的列表
            //     if (!allViewElementForNextPage.Contains(item))
            //     {
            //         //加入該移除的列表
            //         viewElementNeedsLeave.Add(item);

            //         //從目前存在的頁面移除
            //         currentLiveElement.Remove(item);
            //     }
            //     //存在的話 從下個頁面該執行 OnShow 的列表移除
            //     else
            //     {
            //         var vpItem = viewItemForNextPage.Where(m => m.viewElement == item).SingleOrDefault();

            //         if (vpItem != null)
            //         {
            //             //存在但沒有 parent 物件的才移除 OnShow
            //             if (vpItem.parent == null)
            //             {
            //                 viewItemForNextPage.Remove(vpItem);
            //             }

            //         }
            //         else
            //         {
            //             //浮起來的物件要 call OnLeave 而且下一頁 parent 是 null 的情況要 OnLeave
            //             if (item.isFloating )
            //             {
            //                 //加入該移除的列表
            //                 viewElementNeedsLeave.Add(item);
            //             }
            //             currentLiveElement.Remove(item);

            //         }
            //     }
            // }



            //對該離開的元件呼叫 OnLeave
            foreach (var item in viewElementNeedsLeave)
            {

                float t = 0;
                float d = 0;
                bool needLeaveWhileFloat = false;
                lastPageItemTweenOutTimes.TryGetValue(item.name, out t);
                lastPageNeedLeaveOnFloat.TryGetValue(item.name, out needLeaveWhileFloat);
                lastPageItemDelayOutTimes.TryGetValue(item.name, out d);
                item.OnLeave(t, needLeaveWhileFloat, d);
            }

            lastPageItemTweenOutTimes.Clear();
            lastPageNeedLeaveOnFloat.Clear();
            lastPageItemDelayOutTimes.Clear();

            //等待上一個頁面的OnLeaveDelay

            yield return new WaitForSeconds(nextViewPageWaitTime);

            float OnStartWaitTime = 0;
            switch (vp.viewPageTransitionTimingType)
            {
                case ViewPage.ViewPageTransitionTimingType.接續前動畫:
                    OnStartWaitTime = CalculateWaitingTimeForNextOnShow(viewItemForNextPage.Select(m => m.viewElement));
                    break;
                case ViewPage.ViewPageTransitionTimingType.與前動畫同時:
                    OnStartWaitTime = 0;
                    break;
                case ViewPage.ViewPageTransitionTimingType.自行設定:
                    OnStartWaitTime = vp.customPageTransitionWaitTime;
                    break;
            }
            nextViewPageWaitTime = CalculateWaitingTimeForCurrentOnLeave(viewItemForNextPage);
            yield return new WaitForSeconds(OnStartWaitTime);
            //對該進場的元件呼叫 OnShow
            foreach (var item in viewItemForNextPage)
            {
                bool playInAnimtionWhileParentOverwrite = false;
                if (item.parent != null && item.viewElement.gameObject.activeSelf == false)
                {
                    playInAnimtionWhileParentOverwrite = true;
                }
                item.viewElement.OnShow(item.parent, playInAnimtionWhileParentOverwrite, item.parentMoveTweenIn, item.delayIn);
                currentLiveElement.Add(item.viewElement);

                //Tween 的時間
                if (item.parent != null && !lastPageItemTweenOutTimes.ContainsKey(item.viewElement.name))
                    lastPageItemTweenOutTimes.Add(item.viewElement.name, item.parentMoveTweenOut);
                else
                    lastPageItemTweenOutTimes[item.viewElement.name] = item.parentMoveTweenOut;

                //在 float 裝態時是否要直接離開
                if (item.parent != null && !lastPageNeedLeaveOnFloat.ContainsKey(item.viewElement.name))
                    lastPageNeedLeaveOnFloat.Add(item.viewElement.name, item.NeedLeaveWhileIsFloating);
                else
                    lastPageNeedLeaveOnFloat[item.viewElement.name] = item.NeedLeaveWhileIsFloating;

                //Delay 時間
                if (!lastPageItemDelayOutTimes.ContainsKey(item.viewElement.name))
                    lastPageItemDelayOutTimes.Add(item.viewElement.name, item.delayOut);
                else
                    lastPageItemDelayOutTimes[item.viewElement.name] = item.delayOut;
            }

            //更新狀態
            UpdateCurrentViewState(vp);
            //通知事件
            OnViewStateChange(this, new ViewStateEventArgs(currentViewPage, lastViewPage, currentViewState, lastViewState));

            //OnComplete Callback
            if (OnComplete != null && vp.autoLeaveTimes <= 0) OnComplete();
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
            if (overlayViewPageQueue.Contains(vp) == false)
            {
                overlayViewPageQueue.Add(vp);
                foreach (var item in vp.viewPageItem)
                {
                    item.viewElement.OnShow(item.parent);
                }
            }

            if (vp.autoLeaveTimes > 0)
            {
                IDisposable lastAutoLeaveQueue;
                if (extendShowTimeWhenTryToShowSamePage == true)
                {
                    //找到上一個相同名稱的代表這個頁面要繼續出現，所以把上一個計時器移除
                    if (autoLeaveQueue.TryGetValue(vp.name, out lastAutoLeaveQueue))
                    {
                        Debug.Log("find last");
                        //停止計時器
                        lastAutoLeaveQueue.Dispose();
                        //從字典移除
                        autoLeaveQueue.Remove(vp.name);
                    }
                }


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
                if (extendShowTimeWhenTryToShowSamePage == true)
                    autoLeaveQueue.Add(vp.name, d);
            }

        }

        public void LeaveOverlayViewPage(string viewPageName)
        {
            var vp = overlayViewPageQueue.Where(m => m.name == viewPageName).SingleOrDefault();

            if (vp == null)
            {
                Debug.Log("No live overlay viewPage of name: " + viewPageName + "  found");
                return;
            }


            foreach (var item in vp.viewPageItem)
            {
                item.viewElement.OnLeave();
            }

            overlayViewPageQueue.Remove(vp);
        }


        void RemoveLastestOverlayView(string revertViewPageName)
        {
            Debug.Log(revertViewPageName);
            var vp = viewPage.Where(m => m.name == revertViewPageName).SingleOrDefault();
            foreach (var item in subPageViewPage.Pop().viewPageItem)
            {
                if (vp != null)
                {
                    if (vp.viewPageItem.Where(m => m.viewElement == item.viewElement).Count() > 0) continue;
                }
                item.viewElement.OnLeave();
            }
            UpdateCurrentViewState(vp);

        }

        void UpdateCurrentViewState(ViewPage vp)
        {
            lastViewPage = currentViewPage;
            currentViewPage = vp;
            if (!string.IsNullOrEmpty(vp.viewState) && viewStatesNames.Contains(vp.viewState))
            {
                lastViewState = currentViewState;
                currentViewState = viewStates.SingleOrDefault(m => m.name == vp.viewState);
            }
        }
        float CalculateWaitingTimeForNextOnShow(IEnumerable<ViewElement> viewElements)
        {
            float maxOutAnitionTime = 0;

            foreach (var item in viewElements)
            {
                float t = item.GetOutAnimationLength();
                if (t > maxOutAnitionTime)
                {
                    maxOutAnitionTime = t;
                }
            }

            return maxOutAnitionTime;
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
        public bool CheckIsOverpageIsLive(string viewPageName)
        {
            return overlayViewPageQueue.Where(m => m.name == viewPageName).Count() > 0 ? true : false;
        }
    }
}