using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CloudMacaca.ViewSystem
{
    public class ViewControllerBase : MonoBehaviour, IViewController
    {
        public List<ViewPage> viewPage = new List<ViewPage>();
        public List<ViewState> viewStates = new List<ViewState>();
        protected static IEnumerable<string> viewStatesNames;

        [ReadOnly, SerializeField]
        protected List<ViewElement> currentLiveElement = new List<ViewElement>();

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
        public bool HasOverlayPageLive()
        {
            return overlayPageStates.Count > 0;
        }
        public bool IsOverPageLive(string viewPageName)
        {
            return overlayPageStates.ContainsKey(viewPageName);
        }
        public IEnumerable<string> GetCurrentOverpageNames()
        {
            return overlayPageStates.Select(m => m.Key);
        }
        [SerializeField]
        protected Dictionary<string, OverlayPageState> overlayPageStates = new Dictionary<string, OverlayPageState>();

        [SerializeField]
        public class OverlayPageState
        {
            public bool IsTransition = false;
            public ViewPage viewPage;
            public Coroutine pageChangeCoroutine;
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
        /// OnViewPageChangeEnd Calls on page is changed finish, all animation include in OnShow or OnLeave is finished.
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
    }
}