using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.EventSystems;

namespace MacacaGames.ViewSystem
{
    public abstract class ViewControllerBase : MonoBehaviour
    {

        protected static float minimumTimeInterval = 0.2f;
        internal static bool builtInClickProtection = true;
        public EventSystem eventSystem;
        public virtual Canvas GetCanvas()
        {
            Canvas result = gameObject.GetComponent<Canvas>();
            if (result == null)
            {
                result = gameObject.GetComponentInChildren<Canvas>();
            }

            return result;
        }
        protected static ViewControllerBase _incance;

        /// <summary>
        /// Check a viewpage is exsit in setting data
        /// </summary>
        /// <param name="viewPageName"></param>
        /// <returns></returns>
        public abstract bool IsViewPageExsit(string viewPageName);

        #region Interface Impletetment
        protected string GetOverlayStateKey(ViewPage vp)
        {
            if (!string.IsNullOrEmpty(vp.viewState))
            {
                return vp.viewState;
            }
            else
            {
                return $"vp_{vp.name}";
            }
        }

        public Coroutine ShowOverlayViewPage(string viewPageName, bool RePlayOnShowWhileSamePage = false, Action OnStart = null, Action OnChanged = null, Action OnComplete = null, bool ignoreTimeScale = false, bool ignoreClickProtection = false, RectTransform customRoot = null, params object[] models)
        {
            if (!CheckTimeProtect())
            {
                ViewSystemLog.LogWarning($"Method call return due to TimeProtect.");
                return null;
            }

            if (!viewPages.TryGetValue(viewPageName, out ViewPage _nextOverlayViewPage))
            {
                ViewSystemLog.LogError("No overlay viewPage match the name: " + viewPageName + "  found");
                return null;
            }

            nextOverlayViewPage = _nextOverlayViewPage;

            string OverlayPageStateKey = GetOverlayStateKey(nextOverlayViewPage);
            if (overlayPageStatusDict.TryGetValue(OverlayPageStateKey, out ViewSystemUtilitys.OverlayPageStatus overlayPageStatus))
            {
                if (overlayPageStatus.IsTransition == true && (builtInClickProtection == true && ignoreClickProtection != false))
                {
                    ViewSystemLog.LogError($"The Overlay page with same state {nextOverlayViewPage.name} is in Transition, ignore the ShowOverlayViewPage call.");
                    return null;
                }
                if (overlayPageStatus.viewPage.name == viewPageName)
                {
                    if (RePlayOnShowWhileSamePage == false)
                    {
                        ViewSystemLog.LogError($"The Overlay page {nextOverlayViewPage.name} is in already exsit, ignore the ShowOverlayViewPage call.");
                        return null;
                    }
                }

            }
            return StartCoroutine(ShowOverlayViewPageBase(nextOverlayViewPage, RePlayOnShowWhileSamePage, OnStart, OnChanged, OnComplete, ignoreTimeScale, ignoreClickProtection, customRoot, models));
        }

        public Coroutine LeaveOverlayViewPage(string viewPageName, float tweenTimeIfNeed = 0.4F, Action OnComplete = null, bool ignoreTransition = false, bool ignoreTimeScale = false, bool ignoreClickProtection = false, bool waitForShowFinish = false)
        {
            if (!CheckTimeProtect())
            {
                ViewSystemLog.LogWarning($"Method call return due to TimeProtect.");
                return null;
            }
            // var nextViewPage = viewPages.SingleOrDefault(m => m.name == viewPageName);
            if (!viewPages.TryGetValue(viewPageName, out ViewPage _nextOverlayViewPage))
            {
                ViewSystemLog.LogError("No overlay viewPage match the name: " + viewPageName + "  found");
                return null;
            }

            nextOverlayViewPage = _nextOverlayViewPage;

            string OverlayPageStateKey = GetOverlayStateKey(nextOverlayViewPage);

            if (!overlayPageStatusDict.TryGetValue(OverlayPageStateKey, out ViewSystemUtilitys.OverlayPageStatus overlayPageStatus))
            {
                ViewSystemLog.LogError("No live overlay viewPage of name: " + viewPageName + " found, try to fix.");
                overlayPageStatus = new ViewSystemUtilitys.OverlayPageStatus();
                overlayPageStatus.viewPage = nextOverlayViewPage;

                viewStates.TryGetValue(nextOverlayViewPage.viewState, out ViewState _nextViewState);
                nextViewState = _nextViewState;
                if (nextViewState != null) { overlayPageStatus.viewState = nextViewState; }
            }
            else if (builtInClickProtection == true && ignoreClickProtection != false)
            {
                if (overlayPageStatus.transition == ViewSystemUtilitys.OverlayPageStatus.Transition.Show && waitForShowFinish == false)
                {
                    ViewSystemLog.LogError($"The Overlay page {nextOverlayViewPage.name} is in Transition, ignore the LeaveOverlayViewPage call.");
                    return null;
                }
                if (overlayPageStatus.transition == ViewSystemUtilitys.OverlayPageStatus.Transition.Leave)
                {
                    ViewSystemLog.LogError($"The Overlay page {nextOverlayViewPage.name} is in Leaving, ignore the LeaveOverlayViewPage call.");
                    return null;
                }
            }
            overlayPageStatus.pageChangeCoroutine = StartCoroutine(LeaveOverlayViewPageBase(overlayPageStatus, tweenTimeIfNeed, OnComplete, ignoreTransition, ignoreTimeScale, ignoreClickProtection, waitForShowFinish));
            return overlayPageStatus.pageChangeCoroutine;
        }

        public Coroutine ChangePage(string targetViewPageName, Action OnStart = null, Action OnChanged = null, Action OnComplete = null, bool AutoWaitPreviousPageFinish = false, bool ignoreTimeScale = false, bool ignoreClickProtection = false, params object[] models)
        {
            if (!CheckTimeProtect())
            {
                ViewSystemLog.LogWarning($"Method call return due to TimeProtect.");
                return null;
            }
            if (currentViewPage?.name == targetViewPageName)
            {
                ViewSystemLog.LogWarning("The ViewPage request to change is same as current ViewPage, nothing will happen!");
                return null;
            }

            if (builtInClickProtection == true && ignoreClickProtection != false)
            {
                if (IsPageTransition && AutoWaitPreviousPageFinish == false)
                {
                    ViewSystemLog.LogWarning("Page is in Transition. You can set AutoWaitPreviousPageFinish to 'True' then page will auto transition to next page while previous page transition finished.");
                    return null;
                }
                else if (IsPageTransition && AutoWaitPreviousPageFinish == true)
                {
                    ViewSystemLog.LogWarning($"Page is in Transition but AutoWaitPreviousPageFinish Leaving page is [{currentViewPage?.name}] Entering page is [{nextViewPage?.name}] next page is [{targetViewPageName}]");
                    ChangePageToCoroutine = StartCoroutine(WaitPrevious(targetViewPageName, OnStart, OnChanged, OnComplete, ignoreTimeScale));
                    return ChangePageToCoroutine;
                }
            }
            ChangePageToCoroutine = StartCoroutine(ChangePageBase(targetViewPageName, OnStart, OnChanged, OnComplete, ignoreTimeScale, ignoreClickProtection, models));
            return ChangePageToCoroutine;
        }

        public virtual IEnumerator ShowOverlayViewPageBase(ViewPage vp, bool RePlayOnShowWhileSamePage, Action OnStart, Action OnChanged, Action OnComplete, bool ignoreTimeScale = false, bool ignoreClickProtection = false, RectTransform customRoot = null, params object[] models)
        {
            //Empty implement will override in child class
            yield return null;
        }

        public virtual IEnumerator LeaveOverlayViewPageBase(ViewSystemUtilitys.OverlayPageStatus overlayPageState, float tweenTimeIfNeed, Action OnComplete, bool ignoreTransition = false, bool ignoreTimeScale = false, bool ignoreClickProtection = false, bool waitForShowFinish = false)
        {
            //Empty implement will override in child class
            yield return null;
        }

        public virtual IEnumerator ChangePageBase(string viewPageName, Action OnStart, Action OnCheaged, Action OnComplete, bool ignoreTimeScale, bool ignoreClickProtection, params object[] models)
        {
            //Empty implement will override in child class
            yield return null;
        }

        public virtual void TryLeaveAllOverlayPage()
        {
            //清空自動離場
            for (int i = 0; i < overlayPageStatusDict.Count; i++)
            {
                var item = overlayPageStatusDict.ElementAt(i);
                StartCoroutine(LeaveOverlayViewPageBase(item.Value, 0.4f, null, true));
            }
        }

        public virtual bool HasOverlayPageLive()
        {
            return overlayPageStatusDict.Count > 0;
        }
        public virtual bool IsOverPageLive(string viewPageName)
        {
            return overlayPageStatusDict.ContainsKey(viewPageName);
        }
        public virtual IEnumerable<string> GetCurrentOverpageNames()
        {
            return overlayPageStatusDict.Select(m => m.Key);
        }
        public bool IsOverlayTransition
        {
            get
            {
                foreach (var item in overlayPageStatusDict)
                {
                    if (item.Value.IsTransition == true)
                    {
                        ViewSystemLog.LogWarning("Overlay is Transition Due to " + item.Key + "is Transition");
                        return true;
                    }
                }
                return false;
            }
        }

        float lastMethodTime = 0;

        protected bool CheckTimeProtect()
        {
            float currentTime = Time.realtimeSinceStartup;
            bool result = (currentTime - lastMethodTime) > minimumTimeInterval;
            if (result)
            {
                lastMethodTime = currentTime;
            }
            return true;
        }

        #endregion

        #region  Unity LifeCycle
        Action<Exception> unhandledExceptionCallback = ex => Debug.LogException(ex); // default
        MicroCoroutine microCoroutine = null;

        protected virtual void Awake()
        {
            ViewElement.viewController = this;
            fullPageChangerPool = new Queue<FullPageChanger>();
            overlayPageChangerPool = new Queue<OverlayPageChanger>();

            microCoroutine = new MicroCoroutine(ex => unhandledExceptionCallback(ex));

            RemoveEventSub();


        }

        void CheckEventSystem()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                ViewSystemLog.Log("Create EventSystem due to no instance found on Scene");
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }

        void RemoveEventSub()
        {
            OnViewPageChangeStart = null;
            OnViewPageChange = null;
            OnViewPageChangeEnd = null;
            OnOverlayPageLeave = null;
            OnOverlayPageShow = null;
            OnOverlayPageLeave = null;
            OnViewStateChange = null;
        }

        public void ResetSubscriptions(System.EventHandler<MacacaGames.ViewSystem.ViewControllerBase.ViewPageEventArgs> e) => e = null;

        protected virtual void Start()
        {
            CheckEventSystem();
        }

        void LateUpdate()
        {
            microCoroutine.Update();
        }

        public void StopMicroCoroutine(MicroCoroutine.Coroutine coroutine)
        {
            microCoroutine.RemoveCoroutine(coroutine);
        }

        public MicroCoroutine.Coroutine StartMicroCoroutine(IEnumerator routine)
        {
            return microCoroutine.AddCoroutine(routine);
        }

        #endregion


        public Dictionary<string, ViewPage> viewPages = new Dictionary<string, ViewPage>();
        public Dictionary<string, ViewState> viewStates = new Dictionary<string, ViewState>();
        protected static IEnumerable<string> viewStatesNames;

        [ReadOnly, SerializeField]
        protected List<ViewElement> currentLiveElements = new List<ViewElement>();

        [HideInInspector]
        public ViewPage lastViewPage { get; protected set; }

        [HideInInspector]
        public ViewState lastViewState { get; protected set; }

        [HideInInspector]
        public ViewPage currentViewPage { get; protected set; }

        [HideInInspector]
        public ViewState currentViewState { get; protected set; }

        public ViewPage nextViewPage { get; protected set; }
        [HideInInspector]
        public ViewPage nextOverlayViewPage { get; protected set; }

        [HideInInspector]
        public ViewState nextViewState { get; protected set; }

        public ViewPageItem.PlatformOption platform
        {
            get => ViewSystemUtilitys.SetupPlatformDefine();
        }
        /// <summary>
        /// The current active Overlay Dictionary which has no ViewState. 
        /// </summary>
        /// <typeparam name="string">ViewPage name</typeparam>
        /// <typeparam name="ViewSystemUtilitys.OverlayPageState">The object hold the Overlay Page Status</typeparam>
        /// <returns></returns>
        [SerializeField]
        protected Dictionary<string, ViewSystemUtilitys.OverlayPageStatus> overlayPageStatusDict = new Dictionary<string, ViewSystemUtilitys.OverlayPageStatus>();

        // /// <summary>
        // /// The current active Overlay Dictionary which has ViewState. 
        // /// </summary>
        // /// <typeparam name="string">ViewState name</typeparam>
        // /// <typeparam name="ViewSystemUtilitys.OverlayPageState">The object hold the Overlay Page State</typeparam>
        // /// <returns></returns>
        // protected Dictionary<string, ViewSystemUtilitys.OverlayPageStatus> overlayPageStatesWithOverState = new Dictionary<string, ViewSystemUtilitys.OverlayPageStatus>();
        protected IEnumerable<ViewPageItem> GetAllViewPageItemInViewState(ViewState vs)
        {
            return vs.viewPageItems.Where(m => !m.excludePlatform.IsSet(platform));
        }

        protected IEnumerable<ViewPageItem> GetAllViewPageItemInViewPage(ViewPage vp)
        {
            return vp.viewPageItems.Where(m => !m.excludePlatform.IsSet(platform));
        }

        public bool IsPageTransition
        {
            get
            {
                return ChangePageToCoroutine != null;
            }
        }
        protected Coroutine ChangePageToCoroutine = null;
        public IEnumerator WaitPrevious(string viewPageName, Action OnStart, Action OnChanged, Action OnComplete, bool ignoreTimeScale)
        {
            yield return new WaitUntil(() => IsPageTransition == false);
            yield return ChangePage(viewPageName, OnStart, OnChanged, OnComplete, ignoreTimeScale);
        }

        #region PageChanger

        static Queue<FullPageChanger> fullPageChangerPool;
        static Queue<OverlayPageChanger> overlayPageChangerPool;
        public void RecoveryChanger(PageChanger pageChanger)
        {
            if (pageChanger is FullPageChanger fullPageChanger)
                fullPageChangerPool.Enqueue(fullPageChanger);

            if (pageChanger is OverlayPageChanger overlayPageChanger)
                overlayPageChangerPool.Enqueue(overlayPageChanger);
        }
        public static FullPageChanger FullPageChanger()
        {
            FullPageChanger pageChanger;
            if (fullPageChangerPool.Count == 0)
            {
                pageChanger = new FullPageChanger(_incance);
            }
            else
            {
                pageChanger = fullPageChangerPool.Dequeue();
                pageChanger.Reset();
            }
            return pageChanger;
        }
        public static OverlayPageChanger OverlayPageChanger()
        {
            OverlayPageChanger pageChanger;
            if (overlayPageChangerPool.Count == 0)
            {
                pageChanger = new OverlayPageChanger(_incance);
            }
            else
            {
                pageChanger = overlayPageChangerPool.Dequeue();
                pageChanger.Reset();
            }

            return pageChanger;
        }

        #endregion

        #region  Events
        /// <summary>
        /// OnViewStateChange Calls on the ViewPage is changed and has different ViewState.
        /// </summary>
        public static event EventHandler<ViewStateEventArgs> OnViewStateChange;
        protected virtual void InvokeOnViewStateChange(object obj, ViewStateEventArgs args)
        {
            OnViewStateChange?.Invoke(obj, args);
        }

        /// <summary>
        /// OnViewPageChange Calls on last page has leave finished, next page is ready to show.
        /// </summary>
        public static event EventHandler<ViewPageEventArgs> OnViewPageChange;
        protected virtual void InvokeOnViewPageChange(object obj, ViewPageEventArgs args)
        {
            OnViewPageChange?.Invoke(obj, args);
        }

        /// <summary>
        /// OnViewPageChangeStart Calls on page is ready to change with no error(eg. no page fonud etc.), and in this moment last page is still in view. 
        /// </summary>
        public static event EventHandler<ViewPageTrisitionEventArgs> OnViewPageChangeStart;
        protected virtual void InvokeOnViewPageChangeStart(object obj, ViewPageTrisitionEventArgs args)
        {
            OnViewPageChangeStart?.Invoke(obj, args);
        }

        /// <summary>
        /// OnViewPageChangeEnd Calls on page is changed finish, all animation include in OnShow or OnLeave is finished. (Note. the sometimes the Event fire early due to the animation time is longer than "Change Page Max Waiting" time)
        /// </summary>
        public static event EventHandler<ViewPageEventArgs> OnViewPageChangeEnd;
        protected virtual void InvokeOnViewPageChangeEnd(object obj, ViewPageEventArgs args)
        {
            OnViewPageChangeEnd?.Invoke(obj, args);
        }

        /// <summary>
        /// OnOverlayPageShow Calls on an overlay page is show.(the transition may still working)
        /// </summary>
        public static event EventHandler<ViewPageEventArgs> OnOverlayPageShow;
        protected virtual void InvokeOnOverlayPageShow(object obj, ViewPageEventArgs args)
        {
            OnOverlayPageShow?.Invoke(obj, args);
        }

        /// <summary>
        /// OnOverlayPageLeave Calls on an overlay page is leave.(the transition may still working)
        /// </summary>
        public static event EventHandler<ViewPageEventArgs> OnOverlayPageLeave;
        protected virtual void InvokeOnOverlayPageLeave(object obj, ViewPageEventArgs args)
        {
            OnOverlayPageLeave?.Invoke(obj, args);
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