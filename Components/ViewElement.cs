using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CloudMacaca.ViewSystem
{
    [DisallowMultipleComponent]
    public class ViewElement : MonoBehaviour
    {
        #region V2
        public static ViewElementRuntimePool runtimePool;
        public static ViewElementPool viewElementPool;
        public string PoolKey;
        public bool IsUnique = false;

        bool hasGroupSetup = false;
        private ViewElementGroup _viewElementGroup;
        public ViewElementGroup viewElementGroup
        {
            get
            {

                if (_viewElementGroup == null)
                {
                    _viewElementGroup = GetComponent<ViewElementGroup>();
                    if (hasGroupSetup)
                    {
                        return _viewElementGroup;
                    }
                    hasGroupSetup = true;
                }

                return _viewElementGroup;
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
        public void ApplyEvent(IEnumerable<ViewElementEventData> eventDatas)
        {
            if (eventDatas == null)
            {
                return;
            }
            if (eventDatas.Count() == 0)
            {
                return;
            }
            runtimeOverride.SetEvent(eventDatas);
        }
        public void ApplyOverrides(IEnumerable<ViewElementPropertyOverrideData> overrideDatas)
        {
            runtimeOverride.ClearAllEvent();
            runtimeOverride.ResetToDefaultValues();
            runtimeOverride.RevertToLastNavigation();
            if (overrideDatas == null)
            {
                return;
            }
            if (overrideDatas.Count() == 0)
            {
                return;
            }
            runtimeOverride.ApplyOverride(overrideDatas);
        }

        public virtual Selectable[] GetSelectables()
        {
            return GetComponentsInChildren<Selectable>();
        }

        #endregion
        //ViewElementLifeCycle
        protected IViewElementLifeCycle[] lifeCyclesObjects;
        public enum TransitionType
        {
            Animator,
            CanvasGroupAlpha,
            ActiveSwitch,
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
        const string ButtonAnimationBoolKey = "IsLoop";
        bool hasLoopBool = false;

        //CanvasGroup
        public float canvasInTime = 0.4f;
        public float canvasOutTime = 0.4f;
        public Ease canvasInEase = Ease.OutQuad;
        public Ease canvasOutEase = Ease.OutQuad;
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
        //Custom
        public ViewElementEvent OnShowHandle;
        public ViewElementEvent OnLeaveHandle;

        private RectTransform _rectTransform;
        private RectTransform rectTransform
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
        void Reset()
        {
            //如果還是沒有抓到 Animator 那就設定成一般開關模式
            if (_animator == null)
                transition = TransitionType.ActiveSwitch;
            Setup();
        }
        // void Start()
        // {
        //     Setup();
        // }
        void Awake()
        {
            Setup();
        }
        public virtual void Setup()
        {
            lifeCyclesObjects = GetComponentsInChildren<IViewElementLifeCycle>();
            // poolParent = viewElementPool.transform;
            // poolScale = transform.localScale;
            // poolPosition = rectTransform.anchoredPosition3D;
            // if (transform.parent == poolParent)
            // gameObject.SetActive(false);

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
        Coroutine AnimationIsEndCheck = null;

        public void Reshow()
        {
            OnShow(0);
        }
        IDisposable OnShowObservable;
        public virtual void ChangePage(bool show, Transform parent, float TweenTime = 0, float delayIn = 0, float delayOut = 0, bool ignoreTransition = false)
        {
            if (lifeCyclesObjects != null)
                foreach (var item in lifeCyclesObjects)
                {
                    item.OnChangePage(show);
                }
            //ViewSystemLog.LogError("ChangePage " + name);
            if (show)
            {
                if (parent == null)
                {
                    throw new NullReferenceException(gameObject.name + " does not set the parent for next viewpage.");
                }
                //還在池子裡，應該先 OnShow
                //或是正在離開，都要重播 OnShow
                if (IsShowed == false || OnLeaveWorking)
                {
                    rectTransform.SetParent(parent, true);
                    rectTransform.anchoredPosition3D = Vector3.zero;
                    rectTransform.localScale = Vector3.one;
                    OnShow(delayIn);
                }
                //已經在場上的
                else
                {
                    //如果目前的 parent 跟目標的 parent 是同一個人 那就什麼事都不錯
                    if (parent.GetInstanceID() == rectTransform.parent.GetInstanceID())
                    {
                        //ViewSystemLog.LogWarning("Due to already set the same parent with target parent, ignore " +  name);
                        return;
                    }
                    //其他的情況下用 Tween 過去
                    if (TweenTime >= 0)
                    {
                        rectTransform.SetParent(parent, true);
                        rectTransform.DOAnchorPos3D(Vector3.zero, TweenTime);
                        rectTransform.DOScale(Vector3.one, TweenTime);
                    }
                    //TweenTime 設定為 >0 的情況下，代表要完整 OnLeave 在 OnShow
                    else
                    {
                        OnLeave(0, ignoreTransition: ignoreTransition);
                        OnShowObservable = Observable.EveryUpdate().Where(_ => OnLeaveWorking == false).Subscribe(
                            x =>
                            {
                                ViewSystemLog.LogWarning("Try to ReShow ", this);
                                rectTransform.SetParent(parent, true);
                                rectTransform.anchoredPosition3D = Vector3.zero;
                                rectTransform.localScale = Vector3.one;
                                OnShow(delayIn);
                                OnShowObservable.Dispose();
                            }
                        );
                    }
                }
            }
            else
            {
                OnLeave(delayOut, ignoreTransition: ignoreTransition);
            }
        }

        public virtual void OnShow(float delayIn = 0)
        {
            //ViewSystemLog.LogError("OnShow " + name);
            //停掉正在播放的 Leave 動畫
            if (OnLeaveDisposable != null)
            {
                OnLeaveDisposable.Dispose();
            }
            if (transition != TransitionType.ActiveSwitch)
            {
                gameObject.SetActive(true);
            }
            else
            {
                if (delayIn == 0) gameObject.SetActive(true);
            }

            if (IsShowed && delayIn > 0)
            {
                if (transition == TransitionType.Animator)
                {
                    animator.Play("Empty");
                }
                else if (transition == TransitionType.CanvasGroupAlpha)
                {
                    canvasGroup.alpha = 0;
                }
            }

            Observable
                .Timer(TimeSpan.FromSeconds(delayIn))
                .Subscribe(_ =>
                {
                    if (lifeCyclesObjects != null)
                        foreach (var item in lifeCyclesObjects)
                        {
                            item.OnBeforeShow();
                        }

                    if (viewElementGroup != null)
                    {
                        viewElementGroup.OnShowChild();
                    }

                    if (transition == TransitionType.Animator)
                    {
                        animator.Play(AnimationStateName_In);

                        if (transition == TransitionType.Animator && hasLoopBool)
                        {
                            animator.SetBool(ButtonAnimationBoolKey, true);
                        }
                    }
                    else if (transition == TransitionType.CanvasGroupAlpha)
                    {
                        //ViewSystemLog.Log("canvasGroup alpha");
                        canvasGroup.DOFade(1, canvasInTime).SetUpdate(true).OnStart(
                            () =>
                            {
                                //簡單暴力的解決 canvasGroup 一開始如果不是 0 的時候的情況
                                if (canvasGroup.alpha != 0)
                                {
                                    canvasGroup.alpha = 0;
                                }
                            }
                        ).SetEase(canvasInEase);
                    }
                    else if (transition == TransitionType.Custom)
                    {
                        OnShowHandle.Invoke(null);
                    }
                    else if (transition == TransitionType.ActiveSwitch)
                    {
                        gameObject.SetActive(true);
                    }

                    if (lifeCyclesObjects != null)
                        foreach (var item in lifeCyclesObjects)
                        {
                            item.OnStartShow();
                        }
                });
        }
        bool OnLeaveWorking = false;
        IDisposable OnLeaveDisposable;
        public virtual void OnLeave(float delayOut = 0, bool NeedPool = true, bool ignoreTransition = false)
        {
            //ViewSystemLog.LogError("OnLeave " + name);
            if (transition == TransitionType.Animator && hasLoopBool)
            {
                animator.SetBool(ButtonAnimationBoolKey, false);
            }
            needPool = NeedPool;
            OnLeaveWorking = true;
            OnLeaveDisposable = Observable
                .Timer(TimeSpan.FromSeconds(delayOut))
                .Subscribe(_ =>
                {
                    if (lifeCyclesObjects != null)
                        foreach (var item in lifeCyclesObjects)
                        {
                            item.OnBeforeLeave();
                        }

                    if (viewElementGroup != null)
                    {
                        viewElementGroup.OnLeaveChild(ignoreTransition);
                    }

                    //在試圖 leave 時 如果已經是 disable 的 那就直接把他送回池子
                    //如果 ignoreTransition 也直接把他送回池子
                    if (gameObject.activeSelf == false || ignoreTransition)
                    {
                        gameObject.SetActive(false);
                        OnLeaveAnimationFinish();
                        return;
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
                            //DirectLeaveWhileFloat = directLeaveWhileFloat;
                            DisableGameObjectOnComplete = true;
                        }
                        catch
                        {
                            gameObject.SetActive(false);
                            OnLeaveAnimationFinish();
                        }

                    }
                    else if (transition == TransitionType.CanvasGroupAlpha)
                    {
                        if (canvasGroup == null) ViewSystemLog.LogError("No Canvas Group Found on this Object", this);
                        canvasGroup.DOFade(0, canvasOutTime).SetEase(canvasInEase).SetUpdate(true)
                            .OnComplete(
                                () =>
                                {
                                    //if (disableGameObjectOnComplete == true)
                                    gameObject.SetActive(false);
                                    OnLeaveAnimationFinish();
                                }
                            );
                    }
                    else if (transition == TransitionType.Custom)
                    {
                        OnLeaveHandle.Invoke(OnLeaveAnimationFinish);
                        //OnLeaveAnimationFinish ();
                    }
                    else
                    {
                        gameObject.SetActive(false);
                        OnLeaveAnimationFinish();
                    }
                    if (lifeCyclesObjects != null)
                    {
                        foreach (var item in lifeCyclesObjects)
                        {
                            item.OnStartLeave();
                        }
                    }

                });
        }

        public bool IsShowed
        {
            get
            {
                return rectTransform.parent != viewElementPool.transformCache;
            }
        }

        protected bool needPool = true;
        public void OnLeaveAnimationFinish()
        {
            OnLeaveWorking = false;
            OnLeaveDisposable = null;

            if (needPool == false)
            {
                return;
            }
            rectTransform.SetParent(viewElementPool.transformCache, true);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            if (runtimePool != null)
            {
                runtimePool.QueueViewElementToRecovery(this);
                if (runtimeOverride != null) runtimeOverride.ResetToDefaultValues();
            }
        }
        public bool DisableGameObjectOnComplete = true;

        public virtual float GetOutAnimationLength()
        {
            if (transition != TransitionType.Animator)
                return 0;
            if (animator == null)
                return 0;

            var clip = animator.runtimeAnimatorController.animationClips.SingleOrDefault(m => m.name.Contains("_" + AnimationStateName_Out));
            if (clip == null)
            {
                return 0;
            }
            else
            {
                return clip.length;
            }

        }
        public virtual float GetInAnimationLength()
        {
            if (transition != TransitionType.Animator)
                return 0;
            if (animator == null)
                return 0;

            AnimationClip clip = null;
            try
            {
                clip = animator.runtimeAnimatorController.animationClips.SingleOrDefault(m => m.name.Contains("_" + AnimationStateName_In));
            }
            catch (Exception ex)
            {
                ViewSystemLog.LogError(ex.Message, this);
            }

            if (clip == null)
            {
                return 0;
            }
            else
            {
                return clip.length;
            }
        }
    }

}

[System.Serializable]
public class ViewElementEvent : UnityEvent<Action>
{

}