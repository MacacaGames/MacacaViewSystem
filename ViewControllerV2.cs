using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
namespace CloudMacaca.ViewSystem
{
    public class ViewControllerV2 : ViewControllerBase
    {
        public static ViewControllerV2 Instance;

        public ViewElementPool viewElementPool;

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
            ui.transform.SetParent(transform);
            ui.transform.localPosition = viewSystemSaveData.baseSetting.UIRoot.transform.localPosition;
            ui.name = viewSystemSaveData.baseSetting.UIRoot.name;

            var go = new GameObject("ViewElementPool");
            go.transform.SetParent(transform);
            go.AddComponent<RectTransform>();
            viewElementPool = go.AddComponent<ViewElementPool>(); ;

        }
        protected override void Start()
        {
            //Load ViewPages and ViewStates from ViewSystemSaveData

            viewStates = viewSystemSaveData.viewStates.Select(m=>m.viewState).ToList();
            viewPages = viewSystemSaveData.viewPages.Select(m=>m.viewPage).ToList();

            viewStatesNames = viewStates.Select(m => m.name);

            base.Start();
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

        public override IEnumerator ChangePageBase(string viewPageName, Action OnComplete)
        {
            //Empty implement will override in child class
            yield return null;
        }

    }
}