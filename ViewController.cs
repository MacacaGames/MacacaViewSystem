using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using Unity.Linq;
using UnityEngine;

namespace CloudMacaca.ViewSystem {

    public class ViewController : MonoBehaviour {
        public event EventHandler<ViewStateEventArgs> OnViewStateChange;
        public event EventHandler<ViewPageEventArgs> OnViewPageChange;
        public event EventHandler<ViewPageEventArgs> OnOverlayPageShow;
        public event EventHandler<ViewPageEventArgs> OnOverlayPageLeave;
        public class ViewStateEventArgs : EventArgs {
            public ViewState currentViewState;
            public ViewState lastViewState;
            public ViewStateEventArgs (ViewState CurrentViewState, ViewState LastVilastViewState) {
                this.currentViewState = CurrentViewState;
                this.lastViewState = LastVilastViewState;
            }
        }
        public class ViewPageEventArgs : EventArgs {
            // ...省略額外參數
            public ViewPage currentViewPage;
            public ViewPage lastViewPage;
            public ViewPageEventArgs (ViewPage CurrentViewPage, ViewPage LastViewPage) {
                this.currentViewPage = CurrentViewPage;
                this.lastViewPage = LastViewPage;
            }
        }
        public static ViewController Instance;
        public List<ViewPage> viewPage = new List<ViewPage> ();
        public List<ViewState> viewStates = new List<ViewState> ();
        private static IEnumerable<string> viewStatesNames;
        [ReadOnly, SerializeField]
        private List<ViewElement> currentLiveElement = new List<ViewElement> ();
        [HideInInspector]
        public ViewPage lastViewPage;
        [HideInInspector]
        public ViewState lastViewState;
        [HideInInspector]
        public ViewPage currentViewPage;
        [HideInInspector]
        public ViewState currentViewState;
        // Use this for initialization
        void Awake () {
            Instance = this;
        }
        CloudMacaca.ViewSystem.ViewPageItem.PlatformOption platform;
        void Start () {
            viewStatesNames = viewStates.Select (m => m.name);

#if UNITY_EDITOR
            if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS) {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.iOS;
            } else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.tvOS) {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.tvOS;
            } else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android) {
                platform = CloudMacaca.ViewSystem.ViewPageItem.PlatformOption.Android;
            } else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WSAPlayer) {
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
        private Stack<ViewPage> subPageViewPage = new Stack<ViewPage> ();
        private float nextViewPageWaitTime = 0;
        private Dictionary<string, float> lastPageItemTweenOutTimes = new Dictionary<string, float> ();
        private Dictionary<string, float> lastPageItemDelayOutTimes = new Dictionary<string, float> ();
        private Dictionary<string, float> lastPageItemDelayOutTimesOverlay = new Dictionary<string, float> ();
        private Dictionary<string, bool> lastPageNeedLeaveOnFloat = new Dictionary<string, bool> ();

        public Coroutine ChangePageTo (string viewPageName, Action OnComplete = null) {
            foreach (var item in currentLiveElement) {
                if (!item.AnimatorIsInLoopOrEmptyOrDisableState ()) {
                    Debug.LogWarning ("Animation is not in loop state will not continue" + item.name);
                    return null;
                }
            }
            return StartCoroutine (ChangePageToBase (viewPageName, OnComplete));
        }
        public IEnumerator ChangePageToBase (string viewPageName, Action OnComplete) {

            //取得 ViewPage 物件
            var vp = viewPage.Where (m => m.name == viewPageName).SingleOrDefault ();

            //沒有找到 
            if (vp == null) {
                Debug.LogError ("No view page match" + viewPageName + "Found");
                //return;
                yield break;
            }

            if (vp.viewPageType == ViewPage.ViewPageType.Overlay) {
                Debug.LogWarning ("To show Overlay ViewPage use ShowOverlayViewPage() method \n current version will redirect to this method automatically.");
                ShowOverlayViewPageBase (vp, true, OnComplete);
                //return;
                yield break;
            }

            //先整理出下個頁面應該出現的 ViewItem
            ViewState viewPagePresetTemp;
            List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem> ();

            //從 ViewPagePreset 尋找
            if (!string.IsNullOrEmpty (vp.viewState)) {
                viewPagePresetTemp = viewStates.SingleOrDefault (m => m.name == vp.viewState);
                if (viewPagePresetTemp != null) {
                    viewItemForNextPage.AddRange (viewPagePresetTemp.viewPageItems);
                }
            }

            //從 ViewPage 尋找
            viewItemForNextPage.AddRange (vp.viewPageItem);

            List<ViewPageItem> realViewPageItem = new List<ViewPageItem> ();

            foreach (var item in viewItemForNextPage) {
                if (item.excludePlatform.Contains (platform)) {
                    item.parentGameObject.SetActive (false);
                } else {
                    item.parentGameObject.SetActive (true);
                    realViewPageItem.Add (item);
                }
            }

            List<ViewElement> viewElementDoesExitsInNextPage = new List<ViewElement> ();

            //尋找這個頁面還在，但下個頁面沒有的元件
            //就是存在 currentLiveElement 中但不存在 viewItemForNextPage 的傢伙要 ChangePage 
            var allViewElementForNextPage = realViewPageItem.Select (m => m.viewElement).ToList ();
            foreach (var item in currentLiveElement.ToArray ()) {
                //不存在的話就讓他加入應該移除的列表
                if (allViewElementForNextPage.Contains (item) == false) {
                    //加入該移除的列表
                    viewElementDoesExitsInNextPage.Add (item);
                    //從目前存在的頁面移除
                    currentLiveElement.Remove (item);
                }
            }

            var CurrentOverlayViewPageItem = new List<ViewPageItem> ();

            foreach (var item in overlayViewPageQueue.Select (m => m.viewPageItem)) {
                CurrentOverlayViewPageItem.AddRange (item);
            }
            var CurrentOverlayViewElement = CurrentOverlayViewPageItem.Select (m => m.viewElement);
            //對離場的呼叫改變狀態
            foreach (var item in viewElementDoesExitsInNextPage) {
                //如果 ViewElement 被 Overlay 頁面使用中就不執行 ChangePage
                if (CurrentOverlayViewElement.Contains (item)) {
                    Debug.Log(item.name);
                    continue;
                }
                float delayOut = 0;
                lastPageItemDelayOutTimes.TryGetValue (item.name, out delayOut);
                item.ChangePage (false, null, 0, 0, delayOut);
            }

            lastPageItemDelayOutTimes.Clear ();

            yield return Yielders.GetWaitForSeconds (nextViewPageWaitTime);

            float OnStartWaitTime = 0;
            switch (vp.viewPageTransitionTimingType) {
                case ViewPage.ViewPageTransitionTimingType.接續前動畫:
                    OnStartWaitTime = CalculateWaitingTimeForNextOnShow (realViewPageItem.Select (m => m.viewElement));
                    break;
                case ViewPage.ViewPageTransitionTimingType.與前動畫同時:
                    OnStartWaitTime = 0;
                    break;
                case ViewPage.ViewPageTransitionTimingType.自行設定:
                    OnStartWaitTime = vp.customPageTransitionWaitTime;
                    break;
            }
            nextViewPageWaitTime = CalculateWaitingTimeForCurrentOnLeave (realViewPageItem);
            yield return new WaitForSeconds (OnStartWaitTime);

            //對進場的呼叫改變狀態
            foreach (var item in realViewPageItem) {
                
                currentLiveElement.Add (item.viewElement);
                //Delay 時間
                if (!lastPageItemDelayOutTimes.ContainsKey (item.viewElement.name))
                    lastPageItemDelayOutTimes.Add (item.viewElement.name, item.delayOut);
                else
                    lastPageItemDelayOutTimes[item.viewElement.name] = item.delayOut;

                //如果 ViewElement 被 Overlay 頁面使用中就不執行 ChangePage
                if (CurrentOverlayViewElement.Contains (item.viewElement)) {
                    Debug.Log(item.viewElement.name);
                    continue;
                }


                item.viewElement.ChangePage (true, item.parent, item.TweenTime, item.delayIn, item.delayOut);
    

            }

            //更新狀態
            UpdateCurrentViewStateAndNotifyEvent (vp);
            //通知事件

            //OnComplete Callback
            if (OnComplete != null && vp.autoLeaveTimes <= 0) OnComplete ();
        }

        public bool HasOverlayPageLive () {
            return overlayViewPageQueue.Count > 0;
        }
        public bool IsOverPageLive (string viewPageName) {
            return overlayViewPageQueue.Where (m => m.name == viewPageName).Count () > 0;
        }
        List<ViewPage> overlayViewPageQueue = new List<ViewPage> ();
        Dictionary<string, IDisposable> autoLeaveQueue = new Dictionary<string, IDisposable> ();
        public void ShowOverlayViewPage (string viewPageName, bool extendShowTimeWhenTryToShowSamePage = false, Action OnComplete = null) {
            var vp = viewPage.Where (m => m.name == viewPageName).SingleOrDefault ();
            ShowOverlayViewPageBase (vp, extendShowTimeWhenTryToShowSamePage, OnComplete);
        }
        public void ShowOverlayViewPageBase (ViewPage vp, bool extendShowTimeWhenTryToShowSamePage, Action OnComplete) {
            if (vp == null) {
                Debug.Log ("ViewPage is null");
                return;
            }
            if (vp.viewPageType != ViewPage.ViewPageType.Overlay) {
                Debug.LogError ("ViewPage " + vp.name + " is not an Overlay page");
                return;
            }
            if (overlayViewPageQueue.Contains (vp) == false) {
                overlayViewPageQueue.Add (vp);
                foreach (var item in vp.viewPageItem) {
                    //Delay 時間
                    if (!lastPageItemDelayOutTimesOverlay.ContainsKey (item.viewElement.name))
                        lastPageItemDelayOutTimesOverlay.Add (item.viewElement.name, item.delayOut);
                    else
                        lastPageItemDelayOutTimesOverlay[item.viewElement.name] = item.delayOut;

                    item.viewElement.ChangePage (true, item.parent, item.TweenTime, item.delayIn, item.delayOut);
                }
            }

            if (extendShowTimeWhenTryToShowSamePage == false) {
                foreach (var item in vp.viewPageItem) {

                    item.viewElement.OnShow ();

                }
            }

            //找到上一個相同名稱的代表這個頁面要繼續出現，所以把上一個計時器移除
            IDisposable lastAutoLeaveQueue;
            if (autoLeaveQueue.TryGetValue (vp.name, out lastAutoLeaveQueue)) {
                //Debug.Log("find last");
                //停止計時器
                lastAutoLeaveQueue.Dispose ();
                //從字典移除
                autoLeaveQueue.Remove (vp.name);
            }

            if (vp.autoLeaveTimes > 0) {
                var d = Observable
                    .Timer (TimeSpan.FromSeconds (vp.autoLeaveTimes))
                    .Subscribe (
                        _ => {
                            //Debug.Log("Try to Leave Page");
                            LeaveOverlayViewPage (vp.name);
                            //計時器成功完成時也要從字典移除
                            autoLeaveQueue.Remove (vp.name);
                            if (OnComplete != null) {
                                OnComplete ();
                            }
                        }
                    );
                autoLeaveQueue.Add (vp.name, d);
            }
            OnOverlayPageShow (this, new ViewPageEventArgs (vp, null));
        }

        public void LeaveOverlayViewPage (string viewPageName, float tweenTimeIfNeed = 0.4f, Action OnComplete = null) {
            var vp = overlayViewPageQueue.Where (m => m.name == viewPageName).SingleOrDefault ();

            if (vp == null) {
                Debug.Log ("No live overlay viewPage of name: " + viewPageName + "  found");
                return;
            }

            StartCoroutine (LeaveOverlayViewPageBase (vp, tweenTimeIfNeed, OnComplete));
        }

        public IEnumerator LeaveOverlayViewPageBase (ViewPage vp, float tweenTimeIfNeed, Action OnComplete) {
            var currentVe = currentViewPage.viewPageItem.Select (m => m.viewElement);
            var currentVs = currentViewState.viewPageItems.Select (m => m.viewElement);
            // List<ViewElement> finalCurrentVe = new List<ViewElement>();
            // finalCurrentVe.AddRange(currentVe);
            // finalCurrentVe.AddRange(currentVs);
            var finishTime = CalculateWaitingTimeForNextOnShow (currentVe);

            foreach (var item in vp.viewPageItem) {
                if (currentVe.Contains (item.viewElement)) {
                    //準備自動離場的 ViewElement 目前的頁面正在使用中 所以不要對他操作
                    try {
                        var vpi = currentViewPage.viewPageItem.FirstOrDefault (m => m.viewElement == item.viewElement);
                        Debug.LogWarning ("ViewElement : " + item.viewElement.name + "Try to back to origin Transfrom parent : " + vpi.parent.name);
                        item.viewElement.ChangePage (true, vpi.parent, tweenTimeIfNeed, 0, 0);
                    } catch { }
                    continue;
                }
                if (currentVs.Contains (item.viewElement)) {
                    //準備自動離場的 ViewElement 目前的頁面正在使用中 所以不要對他操作
                    try {
                        var vpi = currentViewState.viewPageItems.FirstOrDefault (m => m.viewElement == item.viewElement);
                        Debug.LogWarning ("ViewElement : " + item.viewElement.name + "Try to back to origin Transfrom parent : " + vpi.parent.name);
                        item.viewElement.ChangePage (true, vpi.parent, tweenTimeIfNeed, 0, 0);
                    } catch { }
                    continue;
                }
                float delayOut = 0;
                lastPageItemDelayOutTimesOverlay.TryGetValue (item.viewElement.name, out delayOut);
                item.viewElement.ChangePage (false, null, 0, 0, delayOut);
            }

            overlayViewPageQueue.Remove (vp);
            OnOverlayPageLeave (this, new ViewPageEventArgs (vp, null));
            yield return Yielders.GetWaitForSeconds (finishTime);
            if (OnComplete != null) {
                OnComplete ();
            }
        }

        void UpdateCurrentViewStateAndNotifyEvent (ViewPage vp) {
            lastViewPage = currentViewPage;
            currentViewPage = vp;
            OnViewPageChange (this, new ViewPageEventArgs (currentViewPage, lastViewPage));
            if (!string.IsNullOrEmpty (vp.viewState) && viewStatesNames.Contains (vp.viewState)) {
                lastViewState = currentViewState;
                currentViewState = viewStates.SingleOrDefault (m => m.name == vp.viewState);

                OnViewStateChange (this, new ViewStateEventArgs (currentViewState, lastViewState));

            }
        }
        float CalculateWaitingTimeForNextOnShow (IEnumerable<ViewElement> viewElements) {
            float maxOutAnitionTime = 0;

            foreach (var item in viewElements) {
                float t = 0;
                if (item.transition == ViewElement.TransitionType.Animator) {
                    t = item.GetOutAnimationLength ();
                } else if (item.transition == ViewElement.TransitionType.CanvasGroupAlpha) {
                    t = item.canvasOutTime;
                }

                if (t > maxOutAnitionTime) {
                    maxOutAnitionTime = t;
                }
            }

            return maxOutAnitionTime;
        }
        float CalculateWaitingTimeForCurrentOnLeave (IEnumerable<ViewPageItem> viewPageItem) {
            float maxDelayTime = 0;
            foreach (var item in viewPageItem) {
                float t2 = item.delayOut;
                if (t2 > maxDelayTime) {
                    maxDelayTime = t2;
                }
            }

            return maxDelayTime;
        }

    }
}