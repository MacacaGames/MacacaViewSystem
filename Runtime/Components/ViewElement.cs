using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Coroutine = MacacaGames.ViewSystem.MicroCoroutine.Coroutine;
namespace MacacaGames.ViewSystem
{
    [DisallowMultipleComponent]
    public class ViewElement : MonoBehaviour
    {
        const BindingFlags defaultBindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

#if UNITY_EDITOR
        public ViewPageItem currentViewPageItem;
        public ViewPage currentViewPage;
#endif

        #region V2
        public int sortingOrder;
        public static ViewElementRuntimePool runtimePool;
        public static ViewElementPool viewElementPool;
        [NonSerialized]
        public int PoolKey;
        public bool IsUnique = false;
        [Tooltip("If true, ViewElement will snap to new position instantly when changing page (no tween).")]
        public bool useInstantPosition = false;
        
        bool hasGroupSetup = false;
        public ViewElementGroup parentViewElementGroup;
        private ViewElementGroup _selfViewElementGroup;
        public ViewElementGroup selfViewElementGroup
        {
            get
            {

                if (_selfViewElementGroup == null)
                {
                    _selfViewElementGroup = GetComponent<ViewElementGroup>();
                    if (hasGroupSetup)
                    {
                        return _selfViewElementGroup;
                    }
                    hasGroupSetup = true;
                }

                return _selfViewElementGroup;
            }
        }

        private ViewRuntimeOverride _runtimeOverride;
        public ViewRuntimeOverride runtimeOverride
        {
            get
            {
                if (_runtimeOverride == null)
                {
                    _runtimeOverride = GetComponent<ViewRuntimeOverride>();
                }
                if (_runtimeOverride == null)
                {
                    _runtimeOverride = gameObject.AddComponent<ViewRuntimeOverride>();
                }
                return _runtimeOverride;
            }
        }
        public void ApplyNavigation(IEnumerable<ViewElementNavigationData> navigationDatas)
        {
            if (navigationDatas == null)
            {
                return;
            }
            if (navigationDatas.Count() == 0)
            {
                return;
            }
            runtimeOverride.ApplyNavigation(navigationDatas);
        }
        public void AddEvent(ViewElementEventData eventData, Component scriptInstance = null)
        {
            if (eventData == null)
            {
                return;
            }
            runtimeOverride.SetEvent(eventData, scriptInstance);
        }
        public void ApplyEvents(IEnumerable<ViewElementEventData> eventDatas)
        {
            if (eventDatas == null)
            {
                return;
            }
            if (eventDatas.Count() == 0)
            {
                return;
            }
            runtimeOverride.SetEvents(eventDatas);
        }
        public void AddOverride(ViewElementPropertyOverrideData overrideDatas)
        {
            AddOverrides(new []{overrideDatas});
        }
        public void AddOverrides(IEnumerable<ViewElementPropertyOverrideData> overrideDatas)
        {
            runtimeOverride.ApplyOverrides(overrideDatas) ;
        }
        public void AddOverrides(ViewElementOverride overrideDatas)
        {
            runtimeOverride.ApplyOverrides(overrideDatas.GetValues());
        }
        public void ApplyOverrides(ViewElementOverride overrideDatas)
        {
            ApplyOverrides(overrideDatas.GetValues());
        }
        public void ApplyOverrides(IEnumerable<ViewElementPropertyOverrideData> overrideDatas)
        {
            RevertOverrides();
            runtimeOverride.ApplyOverrides(overrideDatas);
            if (lifeCyclesObjects != null)
            {
                foreach (var item in lifeCyclesObjects.ToArray())
                {
                    ApplyAutoOverrideAttribute(item);
                }
            }
        }
        public void RevertOverrides()
        {
            runtimeOverride.ClearAllEvent();
            runtimeOverride.ResetToDefaultValues();
            runtimeOverride.RevertToLastNavigation();
        }
        public void ApplyModelInject()
        {
            foreach (var item in lifeCyclesObjects.ToArray())
            {
                ViewController.InjectModels(item);
            }
        }
        [Flags]
        public enum RectTransformFlag
        {
            SizeDelta = 1 << 0,
            AnchoredPosition = 1 << 1,
            AnchorMax = 1 << 2,
            AnchorMin = 1 << 3,
            LocalEulerAngles = 1 << 4,
            LocalScale = 1 << 5,
            Pivot = 1 << 6,
            All = ~0,
        }
        public void ApplyRectTransform(ViewElementTransform viewElementTransform, RectTransformFlag? overrideFlag = null)
        {
            var currentFlag = overrideFlag.HasValue ? overrideFlag.Value : viewElementTransform.rectTransformFlag;

            if (FlagsHelper.IsSet(currentFlag, RectTransformFlag.SizeDelta)) rectTransform.sizeDelta = viewElementTransform.rectTransformData.sizeDelta;
            if (FlagsHelper.IsSet(currentFlag, RectTransformFlag.AnchoredPosition)) rectTransform.anchoredPosition3D = viewElementTransform.rectTransformData.anchoredPosition;
            if (FlagsHelper.IsSet(currentFlag, RectTransformFlag.AnchorMax)) rectTransform.anchorMax = viewElementTransform.rectTransformData.anchorMax;
            if (FlagsHelper.IsSet(currentFlag, RectTransformFlag.AnchorMin)) rectTransform.anchorMin = viewElementTransform.rectTransformData.anchorMin;
            if (FlagsHelper.IsSet(currentFlag, RectTransformFlag.LocalEulerAngles)) rectTransform.localEulerAngles = viewElementTransform.rectTransformData.localEulerAngles;
            if (FlagsHelper.IsSet(currentFlag, RectTransformFlag.LocalScale)) rectTransform.localScale = viewElementTransform.rectTransformData.localScale;
            if (FlagsHelper.IsSet(currentFlag, RectTransformFlag.Pivot)) rectTransform.pivot = viewElementTransform.rectTransformData.pivot;
        }

        public virtual Selectable[] GetSelectables()
        {
            return GetComponentsInChildren<Selectable>();
        }

        #endregion

        public static ViewControllerBase viewController;

        //ViewElementLifeCycle
        protected List<IViewElementLifeCycle> lifeCyclesObjects = new List<IViewElementLifeCycle>();
        public void RegisterLifeCycleObject(IViewElementLifeCycle obj)
        {
            if (!lifeCyclesObjects.Contains(obj))
            {
                lifeCyclesObjects.Add(obj);
            }
        }

        public void UnRegisterLifeCycleObject(IViewElementLifeCycle obj)
        {
            lifeCyclesObjects.Remove(obj);
        }

        public enum TransitionType
        {
            Animator,
            CanvasGroupAlpha,
            ActiveSwitch,
            ViewElementAnimation,
            Custom
        }
        public TransitionType transition = TransitionType.Animator;
        public enum AnimatorTransitionType
        {
            Direct,
            Trigger
        }
        //Animator
        public AnimatorTransitionType animatorTransitionType = AnimatorTransitionType.Direct;
        public string AnimationStateName_In = "In";
        public string AnimationStateName_Out = "Out";
        public string AnimationStateName_Loop = "Loop";
        public bool isSkipOutAnimation = false;
        const string ButtonAnimationBoolKey = "IsLoop";
        bool hasLoopBool = false;

        //CanvasGroup
        public float canvasInTime = 0.25f;
        public float canvasOutTime = 0.25f;
        public EaseStyle canvasInEase = EaseStyle.QuadEaseOut;
        public EaseStyle canvasOutEase = EaseStyle.QuadEaseOut;
        private CanvasGroup _canvasGroup;
        public CanvasGroup canvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                }
                return _canvasGroup;
            }
        }
        Coroutine canvasGroupCoroutine = null;
        Coroutine viewElementAnimationCoroutine = null;

        //Custom
        public ViewElementEvent OnShowHandle;
        public ViewElementEvent OnLeaveHandle;

        private RectTransform _rectTransform;
        public RectTransform rectTransform
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }
                return _rectTransform;
            }
        }

        private Animator _animator;
        public Animator animator
        {
            get
            {
                if (_animator) return _animator;
                _animator = GetComponent<Animator>();
                if (_animator) return _animator;
                _animator = GetComponentInChildren<Animator>();
                return _animator;
            }
        }

        private ViewElementAnimation _viewElementAnimation;
        public ViewElementAnimation viewElementAnimation
        {
            get
            {
                if (_viewElementAnimation) return _viewElementAnimation;
                _viewElementAnimation = GetComponent<ViewElementAnimation>();
                if (_viewElementAnimation) return _viewElementAnimation;
                _viewElementAnimation = GetComponentInChildren<ViewElementAnimation>();
                return _viewElementAnimation;
            }
        }

        void Reset()
        {
            //如果還是沒有抓到 Animator 那就設定成一般開關模式
            if (_animator == null)
                transition = TransitionType.ActiveSwitch;
            Setup();
        }

        void Awake()
        {
            Setup();
        }
        private Graphic[] _allGraphics;
        public virtual void Setup()
        {
            parentViewElementGroup = GetComponentInParent<ViewElementGroup>();
            lifeCyclesObjects = GetComponents<IViewElementLifeCycle>().ToList();

            if (parentViewElementGroup == null || parentViewElementGroup == selfViewElementGroup)
            {
                _allGraphics = gameObject.GetComponentsInChildren<Graphic>();
            }

            CheckAnimatorHasLoopKey();
        }
        void CheckAnimatorHasLoopKey()
        {
            hasLoopBool = false;
            if (transition == TransitionType.Animator)
            {
                foreach (AnimatorControllerParameter param in animator.parameters)
                {
                    if (param.name == ButtonAnimationBoolKey)
                    {
                        hasLoopBool = true;
                    }
                }
            }
        }

        public void Reshow()
        {
            OnShow();
        }
        void NormalizeViewElement()
        {
            if (changePageCoroutine != null)
            {
                viewController.StopMicroCoroutine(changePageCoroutine);
                changePageCoroutine = null;
            }
            if (showCoroutine != null)
            {
                viewController.StopMicroCoroutine(showCoroutine);
                changePageCoroutine = null;
            }
            if (leaveCoroutine != null)
            {
                viewController.StopMicroCoroutine(leaveCoroutine);
                leaveCoroutine = null;
            }

            foreach (var item in tweeningCoroutine)
            {
                viewController.StopMicroCoroutine(item);
            }

            tweeningCoroutine.Clear();
        }
        List<Coroutine> tweeningCoroutine = new List<Coroutine>();
        Coroutine changePageCoroutine = null;

        public virtual void ChangePage(bool show, Transform parent, ViewElementTransform rectTransformData, int sortingOrder = 0, float TweenTime = 0, float delayIn = 0, bool ignoreTransition = false, bool reshowIfSamePage = false)
        {
            this.sortingOrder = sortingOrder;
            NormalizeViewElement();
            changePageCoroutine = viewController.StartMicroCoroutine(OnChangePageRunner(show, parent, rectTransformData, TweenTime, delayIn, ignoreTransition, reshowIfSamePage));
        }
        public IEnumerator OnChangePageRunner(bool show, Transform parent, ViewElementTransform rectTransformData, float TweenTime, float delayIn, bool ignoreTransition, bool reshowIfSamePage)
        {
            if (lifeCyclesObjects != null)
                foreach (var item in lifeCyclesObjects.ToArray())
                {
                    try
                    {
                        item.OnChangePage(show);
                    }
                    catch (Exception ex) { ViewSystemLog.LogError(ex.ToString(), this); }

                }
            if (show)
            {
                if (parent == null)
                {
                    ViewSystemLog.LogError($"{gameObject.name} does not set the parent for next viewpage.", this);
                    goto END;
                }
                //停掉正在播放的 Leave 動畫
                if (leaveCoroutine != null)
                {
                    // viewController.StopCoroutine(OnLeaveCoroutine);
                }
                //還在池子裡，應該先 OnShow
                //或是正在離開，都要重播 OnShow
                if (IsShowed == false || OnLeaveWorking)
                {
                    rectTransform.SetParent(parent, true);

                    if (rectTransformData == null || !string.IsNullOrEmpty(rectTransformData.parentPath))
                    {
                        rectTransform.anchoredPosition3D = Vector3.zero;
                        rectTransform.localScale = Vector3.one;
                    }
                    else
                    {
                        ApplyRectTransform(rectTransformData);
                    }

                    float time = 0;
                    while (time < delayIn)
                    {
                        time += GlobalTimer.deltaTime;
                        yield return null;
                    }
                    OnShow();
                    goto END;
                }
                //已經在場上的
                else
                {
                    //如果目前的 parent 跟目標的 parent 是同一個人 那就什麼事都不錯
                    if ((rectTransformData == null || !string.IsNullOrEmpty(rectTransformData.parentPath)) && parent.GetInstanceID() == rectTransform.parent.GetInstanceID())
                    {
                        //ViewSystemLog.LogWarning("Due to already set the same parent with target parent, ignore " +  name);
                        if (reshowIfSamePage)
                        {
                            OnShow();
                        }
                        goto END;
                    }
                    
                    // 其他的情況下使用useInstantPosition檢查是否要直接顯示在應在位置，或是需要 Tween 過去 
                    if (useInstantPosition)
                    {
                        rectTransform.SetParent(parent, true);

                        if (rectTransformData == null || !string.IsNullOrEmpty(rectTransformData.parentPath))
                        {
                            rectTransform.anchoredPosition3D = Vector3.zero;
                            rectTransform.localScale = Vector3.one;
                        }
                        else
                        {
                            ApplyRectTransform(rectTransformData);
                        }

                        float time = 0;
                        while (time < delayIn)
                        {
                            time += GlobalTimer.deltaTime;
                            yield return null;
                        }

                        OnShow();
                        goto END;
                    }
                    else if (TweenTime >= 0)
                    {
                        rectTransform.SetParent(parent, true);
                        if (rectTransformData == null || !string.IsNullOrEmpty(rectTransformData.parentPath))
                        {
                            var marginFixer = GetComponent<ViewMarginFixer>();
                            var c1 = viewController.StartMicroCoroutine(EaseUtility.To(
                                rectTransform.anchoredPosition3D,
                                Vector3.zero,
                                TweenTime,
                                EaseStyle.QuadEaseOut,
                                (v) =>
                                {
                                    rectTransform.anchoredPosition3D = v;
                                },
                                () =>
                                {
                                    if (marginFixer) marginFixer.ApplyModifyValue();
                                }
                            ));

                            var c2 = viewController.StartMicroCoroutine(EaseUtility.To(
                                rectTransform.localScale,
                                Vector3.one,
                                TweenTime,
                                EaseStyle.QuadEaseOut,
                                (v) =>
                                {
                                    rectTransform.localScale = v;
                                }
                            ));

                            tweeningCoroutine.Add(c1);
                            tweeningCoroutine.Add(c2);
                        }
                        else
                        {
                            rectTransform.SetParent(parent, true);
                            var flag = rectTransformData.rectTransformFlag;
                            FlagsHelper.Unset(ref flag, RectTransformFlag.AnchoredPosition);
                            FlagsHelper.Unset(ref flag, RectTransformFlag.LocalScale);

                            ApplyRectTransform(rectTransformData, flag);

                            var c1 = viewController.StartMicroCoroutine(EaseUtility.To(
                                    rectTransform.anchoredPosition3D,
                                    rectTransformData.rectTransformData.anchoredPosition,
                                    TweenTime,
                                    EaseStyle.QuadEaseOut,
                                    (v) =>
                                    {
                                        rectTransform.anchoredPosition3D = v;
                                    },
                                    () =>
                                    {

                                    }
                                ));
                            var c2 = viewController.StartMicroCoroutine(EaseUtility.To(
                                rectTransform.localScale,
                                rectTransformData.rectTransformData.localScale,
                                TweenTime,
                                EaseStyle.QuadEaseOut,
                                (v) =>
                                {
                                    rectTransform.localScale = v;
                                },
                                () =>
                                {

                                }
                            ));
                            tweeningCoroutine.Add(c1);
                            tweeningCoroutine.Add(c2);
                        }

                        goto END;
                    }
                    //TweenTime 設定為 <0 的情況下，代表要完整 OnLeave 在 OnShow
                    else if(TweenTime < 0)
                    {
                        float time = 0;
                        while (time < delayIn)
                        {
                            time += GlobalTimer.deltaTime;
                            yield return null;
                        }
                        OnLeave(false, ignoreTransition: ignoreTransition);
                        while (OnLeaveWorking == true)
                        {
                            yield return null;
                        }
                        ViewSystemLog.LogWarning("Try to ReShow ", this);
                        rectTransform.SetParent(parent, true);
                        if (rectTransformData == null || !string.IsNullOrEmpty(rectTransformData.parentPath))
                        {
                            rectTransform.anchoredPosition3D = Vector3.zero;
                            rectTransform.localScale = Vector3.one;
                        }
                        else
                        {
                            ApplyRectTransform(rectTransformData);
                        }
                        time = 0;
                        while (time < delayIn)
                        {
                            time += GlobalTimer.deltaTime;
                            yield return null;
                        }
                        OnShow();
                        goto END;
                    }
                }
            }
            else
            {
                OnLeave(ignoreTransition: ignoreTransition);
                goto END;
            }

        END:
            changePageCoroutine = null;
            yield break;
        }

        Coroutine showCoroutine;
        public virtual void OnShow(bool manual = false)
        {
            if (showCoroutine != null)
            {
                viewController.StopMicroCoroutine(showCoroutine);
                showCoroutine = null;

            }
            if (leaveCoroutine != null)
            {
                viewController.StopMicroCoroutine(leaveCoroutine);
                leaveCoroutine = null;
            }
            showCoroutine = viewController.StartMicroCoroutine(OnShowRunner(manual));
        }
        public IEnumerator OnShowRunner(bool manual)
        {
            IsShowed = true;
            if (lifeCyclesObjects != null)
                foreach (var item in lifeCyclesObjects.ToArray())
                {
                    try
                    {
                        item.OnBeforeShow();
                    }
                    catch (Exception ex) { ViewSystemLog.LogError(ex.ToString(), this); }
                }

            SetActive(true);

            if (selfViewElementGroup != null)
            {
                if (selfViewElementGroup.OnlyManualMode && manual == false)
                {
                    if (gameObject.activeSelf) SetActive(false);
                    goto END;
                }
                selfViewElementGroup.OnShowChild();
            }

            if (transition == TransitionType.Animator)
            {
                if (animator != null)
                {
                    animator.Play(AnimationStateName_In);
                    if (transition == TransitionType.Animator && hasLoopBool)
                    {
                        animator.SetBool(ButtonAnimationBoolKey, true);
                    }
                }
                else
                {
                    ViewSystemLog.LogError("The ViewElement doesn't have a animator but the transition is set to Animator, fallback to activeswitch transition", this);
                }
            }
            else if (transition == TransitionType.CanvasGroupAlpha)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0;
                    if (canvasGroupCoroutine != null)
                    {
                        viewController.StopMicroCoroutine(canvasGroupCoroutine);
                    }

                    canvasGroupCoroutine = viewController.StartMicroCoroutine(
                        EaseUtility.To(
                            canvasGroup.alpha,
                            1,
                            canvasInTime,
                            canvasInEase,
                            (v) =>
                            {
                                canvasGroup.alpha = v;
                            },
                            () =>
                            {
                                canvasGroupCoroutine = null;
                            }
                        )
                    );
                }
                else
                {
                    ViewSystemLog.LogError("The ViewElement doesn't have a CanvasGroup but the transition is set to CanvasGroupAlpha, fallback to activeswitch transition", this);
                }
            }
            else if (transition == TransitionType.ViewElementAnimation)
            {
                if (viewElementAnimation != null)
                {
                    if (viewElementAnimationCoroutine != null)
                    {
                        viewController.StopMicroCoroutine(viewElementAnimationCoroutine);
                    }

                    viewElementAnimationCoroutine = viewController.StartMicroCoroutine(
                        viewElementAnimation.PlayIn(
                            () =>
                            {
                                viewElementAnimationCoroutine = null;
                            }
                        )
                    );
                }
                else
                {
                    ViewSystemLog.LogError("The ViewElement doesn't have a ViewElementAnimation but the transition is set to ViewElementAnimation, fallback to activeswitch transition", this);
                }

            }
            else if (transition == TransitionType.Custom)
            {
                OnShowHandle.Invoke(null);
            }

        END:
            if (lifeCyclesObjects != null)
                foreach (var item in lifeCyclesObjects.ToArray())
                {
                    try
                    {
                        item.OnStartShow();
                    }
                    catch (Exception ex) { ViewSystemLog.LogError(ex.ToString(), this); }

                }
            if (_allGraphics != null)
            {
                for (int i = 0; i < _allGraphics.Length; i++)
                {
                    if (_allGraphics[i].raycastTarget == false)
                    {
                        GraphicRegistry.UnregisterGraphicForCanvas(_allGraphics[i].canvas, _allGraphics[i]);
                    }
                }
            }
            showCoroutine = null;
            yield break;
        }

        private void ApplyAutoOverrideAttribute(object source)
        {
            foreach (var item in source.GetType().GetMembers(defaultBindingFlags))
            {
                var attr = item.GetCustomAttribute<OverrideAttribute>();
                if (attr != null)
                {
                    attr.OnMemberApply(this, source, item);
                }
            }
        }

        bool OnLeaveWorking = false;
        //IDisposable OnLeaveDisposable;
        Coroutine leaveCoroutine;
        public virtual void OnLeave(bool NeedPool = true, bool ignoreTransition = false)
        {
            DisableGameObjectOnComplete = !NeedPool;
            if (leaveCoroutine != null)
            {
                viewController.StopMicroCoroutine(leaveCoroutine);
                leaveCoroutine = null;
            }
            leaveCoroutine = viewController.StartMicroCoroutine(OnLeaveRunner(NeedPool, ignoreTransition));
        }
        public IEnumerator OnLeaveRunner(bool NeedPool = true, bool ignoreTransition = false)
        {

            //ViewSystemLog.LogError("OnLeave " + name);
            if (transition == TransitionType.Animator && hasLoopBool)
            {
                animator.SetBool(ButtonAnimationBoolKey, false);
            }
            needPool = NeedPool;
            OnLeaveWorking = true;

            if (lifeCyclesObjects != null)
                foreach (var item in lifeCyclesObjects.ToArray())
                {
                    try
                    {
                        item.OnBeforeLeave();
                    }
                    catch (Exception ex) { ViewSystemLog.LogError(ex.Message, this); }
                }

            if (selfViewElementGroup != null)
            {
                selfViewElementGroup.OnLeaveChild(ignoreTransition);
            }

            //在試圖 leave 時 如果已經是 disable 的 那就直接把他送回池子
            //如果 ignoreTransition 也直接把他送回池子
            if (gameObject.activeSelf == false || ignoreTransition)
            {
                goto END;
            }
            if (transition == TransitionType.Animator)
            {
                try
                {
                    if (animatorTransitionType == AnimatorTransitionType.Direct)
                    {
                        if (animator.HasState(0, Animator.StringToHash(AnimationStateName_Out)))
                            animator.Play(AnimationStateName_Out);
                        else
                            animator.Play("Disable");
                    }
                    else
                    {
                        animator.ResetTrigger(AnimationStateName_Out);
                        animator.SetTrigger(AnimationStateName_Out);
                    }
                }
                catch
                {
                    goto END;
                }
            }
            else if (transition == TransitionType.CanvasGroupAlpha)
            {
                if (canvasGroup == null) ViewSystemLog.LogError("No Canvas Group Found on this Object", this);

                if (canvasGroupCoroutine != null)
                {
                    viewController.StopMicroCoroutine(canvasGroupCoroutine);
                }

                canvasGroupCoroutine = viewController.StartMicroCoroutine(
                   EaseUtility.To(
                       canvasGroup.alpha,
                       0,
                       canvasOutTime,
                       canvasOutEase,
                       (v) =>
                       {
                           canvasGroup.alpha = v;
                       },
                       () =>
                       {
                           canvasGroupCoroutine = null;
                       }
                   )
                );

                goto END;
            }
            else if (transition == TransitionType.ViewElementAnimation)
            {
                if (viewElementAnimationCoroutine != null)
                {
                    viewController.StopMicroCoroutine(viewElementAnimationCoroutine);
                }

                viewElementAnimationCoroutine = viewController.StartMicroCoroutine(
                    viewElementAnimation.PlayOut(
                        () =>
                        {
                            viewElementAnimationCoroutine = null;
                        }
                    )
                );
            }
            else if (transition == TransitionType.Custom)
            {
                OnLeaveHandle.Invoke(OnLeaveAnimationFinish);
            }
            else
            {
                goto END;
            }

        END:
            if (!ignoreTransition && !isSkipOutAnimation)
            {
                float time = 0;
                var outDuration = GetOutDuration();
                while (time < outDuration)
                {
                    time += GlobalTimer.deltaTime;
                    yield return null;
                }
            }

            if (lifeCyclesObjects != null)
            {
                foreach (var item in lifeCyclesObjects.ToArray())
                {
                    try
                    {
                        item.OnStartLeave();
                    }
                    catch (Exception ex) { ViewSystemLog.LogError(ex.Message, this); }

                }
            }
            leaveCoroutine = null;
            OnLeaveAnimationFinish();
            // });
        }

        public void OnChangedPage()
        {
            if (lifeCyclesObjects != null)
            {
                foreach (var item in lifeCyclesObjects.ToArray())
                {
                    try
                    {
                        item.OnChangedPage();
                    }
                    catch (Exception ex) { ViewSystemLog.LogError(ex.Message, this); }

                }
            }
        }

        public void RefreshView()
        {
            foreach (var item in lifeCyclesObjects.ToArray())
            {
                try
                {
                    item.RefreshView();
                }
                catch (Exception ex) { ViewSystemLog.LogError(ex.Message, this); }

            }
        }

        public bool IsShowed
        {
            get;
            private set;
        }

        /// <summary>
        /// A callback to user do something before recovery
        /// </summary>
        public Action OnBeforeRecoveryToPool;
        protected bool needPool = true;
        public bool DisableGameObjectOnComplete = true;
        [NonSerialized]
        public bool DestroyIfNoPool = true;
        public void OnLeaveAnimationFinish()
        {
            IsShowed = false;
            OnLeaveWorking = false;
            leaveCoroutine = null;
            if (_allGraphics != null)
            {
                for (int i = 0; i < _allGraphics.Length; i++)
                {
                    GraphicRegistry.UnregisterGraphicForCanvas(_allGraphics[i].canvas, _allGraphics[i]);
                }
            }
            //先 SetParent 就好除了被託管的
            if (DisableGameObjectOnComplete)
            {
                SetActive(false);
            }

            if (needPool == false)
            {
                return;
            }

            if (runtimePool != null)
            {
                runtimePool.QueueViewElementToRecovery(this);
                OnBeforeRecoveryToPool?.Invoke();
                OnBeforeRecoveryToPool = null;
                if (runtimeOverride != null) runtimeOverride.ResetToDefaultValues();
            }
            else if (DestroyIfNoPool)
            {
                // if there is no runtimePool instance, destroy the viewelement.
                Destroy(gameObject);
            }
        }
        void SetActive(bool active)
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }

        public virtual float GetOutDuration()
        {
            float result = 0;

            if (selfViewElementGroup != null)
            {
                result = Mathf.Max(result, selfViewElementGroup.GetOutDuration());
            }

            if (transition == ViewElement.TransitionType.Animator)
            {
                var clip = animator?.runtimeAnimatorController.animationClips.SingleOrDefault(m => m.name.Split("_").LastOrDefault().Contains(AnimationStateName_Out));
                if (clip != null)
                {
                    result = Mathf.Max(result, clip.length - 0.05f);
                }
            }
            else if (transition == ViewElement.TransitionType.CanvasGroupAlpha)
            {
                result = Mathf.Max(result, canvasOutTime);
            }
            else if (transition == ViewElement.TransitionType.ViewElementAnimation)
            {
                result = Mathf.Max(result, viewElementAnimation.GetOutDuration());
            }
            return Mathf.Clamp(result, 0, 2);
        }
        public virtual float GetInDuration()
        {
            float result = 0;
            if (selfViewElementGroup != null)
            {
                result = Mathf.Max(result, selfViewElementGroup.GetInDuration());
            }

            if (transition == ViewElement.TransitionType.Animator)
            {
                var clip = animator?.runtimeAnimatorController.animationClips.SingleOrDefault(m => m.name.Split("_").LastOrDefault().Contains(AnimationStateName_In));
                if (clip != null)
                {
                    result = Mathf.Max(result, clip.length);
                }
            }
            else if (transition == ViewElement.TransitionType.CanvasGroupAlpha)
            {
                result = Mathf.Max(result, canvasInTime);
            }
            else if (transition == ViewElement.TransitionType.ViewElementAnimation)
            {
                result = Mathf.Max(result, viewElementAnimation.GetInDuration());
            }
            return result;
        }


    }

}

[System.Serializable]
public class ViewElementEvent : UnityEvent<Action>
{

}
