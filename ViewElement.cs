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
    public class ViewElement : MonoBehaviour
    {
        #region  V2 Data
        public static ViewElementRuntimePool runtimePool;
        public static ViewElementPool viewElementPool;
        public string PoolKey;
        public bool IsUnique = false;

        private ViewRuntimeOverride runtimeOverride;
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
            if (runtimeOverride == null)
            {
                runtimeOverride = gameObject.AddComponent<ViewRuntimeOverride>();
            }
            runtimeOverride.SetEvent(eventDatas);
        }
        public void ApplyOverrides(IEnumerable<ViewElementPropertyOverrideData> overrideDatas)
        {
            if (overrideDatas == null)
            {
                return;
            }
            if (overrideDatas.Count() == 0)
            {
                return;
            }
            if (runtimeOverride == null)
            {
                runtimeOverride = gameObject.AddComponent<ViewRuntimeOverride>();
            }
            runtimeOverride.ApplyOverride(overrideDatas);
        }

        #endregion

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

        bool? _hasLoopBool = null;
        bool hasLoopBool
        {
            get
            {
                if (_hasLoopBool == null)
                {
                    if (transition != TransitionType.Animator)
                    {
                        _hasLoopBool = false;
                    }
                    else
                    {
                        foreach (AnimatorControllerParameter param in animator.parameters)
                        {
                            if (param.name == ButtonAnimationBoolKey)
                            {
                                _hasLoopBool = true;
                                return _hasLoopBool.Value;
                            }
                        }
                        _hasLoopBool = false;
                    }
                }
                return _hasLoopBool.Value;
            }
        }
        //ViewElementLifeCycle
        IViewElementLifeCycle[] lifeCyclesObjects;

        //Animator

        //Canvas
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
        //Canvas

        public UnityEvent OnShowHandle;
        public UnityEvent OnLeaveHandle;

        private Vector3 poolPosition;
        private Vector3 poolScale;
        class LastTransform
        {
            public LastTransform(Transform Parent, Vector3 Position, Vector3 Scale)
            {
                this.Parent = Parent;
                this.Position = Position;
                this.Scale = Scale;
            }
            public Transform Parent;
            public Vector3 Position;
            public Vector3 Scale;
        }
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
        void Start()
        {
            Setup();
        }

        public void Setup()
        {
            lifeCyclesObjects = GetComponentsInChildren<IViewElementLifeCycle>();
            // poolParent = viewElementPool.transform;
            // poolScale = transform.localScale;
            // poolPosition = rectTransform.anchoredPosition3D;
            // if (transform.parent == poolParent)
            //     gameObject.SetActive(false);
        }
        Coroutine AnimationIsEndCheck = null;

        public void Reshow()
        {
            OnShow(0);
        }
        IDisposable OnShowObservable;
        public void ChangePage(bool show, Transform parent, float TweenTime, float delayIn, float delayOut, bool ignoreTransition = false)
        {
            //Debug.LogError("ChangePage " + name);
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
                        //Debug.LogWarning("Due to already set the same parent with target parent, ignore " +  name);
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
                                Debug.LogWarning("Try to ReShow ", this);
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

        public void OnShow(float delayIn = 0)
        {
            //Debug.LogError("OnShow " + name);
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

            //簡單暴力的解決 canvasGroup 一開始如果不是 0 的石後的情況
            if (transition == TransitionType.CanvasGroupAlpha)
            {
                if (canvasGroup.alpha != 0)
                {
                    canvasGroup.alpha = 0;
                }
            }
            Observable
                .Timer(TimeSpan.FromSeconds(delayIn))
                .Subscribe(_ =>
                {
                    foreach (var item in lifeCyclesObjects)
                    {
                        item.OnBeforeShow();
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
                        canvasGroup.DOFade(1, canvasInTime).SetEase(canvasInEase);
                    }
                    else if (transition == TransitionType.Custom)
                    {
                        OnShowHandle.Invoke();
                    }
                    else if (transition == TransitionType.ActiveSwitch)
                    {
                        gameObject.SetActive(true);
                    }
                });
        }
        bool OnLeaveWorking = false;
        IDisposable OnLeaveDisposable;
        public void OnLeave(float delayOut = 0, bool NeedPool = true, bool ignoreTransition = false)
        {
            //Debug.LogError("OnLeave " + name);
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
                    foreach (var item in lifeCyclesObjects)
                    {
                        item.OnBeforeLeave();
                    }
                    //在試圖 leave 時 如果已經是 disable 的 那就直接把他送回池子
                    //如果 ignoreTransition 也直接把他送回池子
                    if (gameObject.activeSelf == false || ignoreTransition)
                    {
                        OnLeaveAnimationFinish();
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
                        if (canvasGroup == null) Debug.LogError("No Canvas Group Found on this Object", this);
                        canvasGroup.DOFade(0, canvasOutTime).SetEase(canvasInEase)
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
                        OnLeaveHandle.Invoke();
                        //OnLeaveAnimationFinish ();
                    }
                    else
                    {
                        gameObject.SetActive(false);
                        OnLeaveAnimationFinish();
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

        public void SampleToLoopState()
        {

            if (transition != ViewElement.TransitionType.Animator)
                return;

            animator.Play(AnimationStateName_Loop);
        }
        bool needPool = true;
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
                if (runtimeOverride != null) runtimeOverride.ResetLastOverride();
            }
        }
        public bool DisableGameObjectOnComplete = true;

        public float GetOutAnimationLength()
        {
            if (transition != TransitionType.Animator)
                return 0;
            if (animator == null)
                return 0;

            var clip = animator.runtimeAnimatorController.animationClips.SingleOrDefault(m => m.name.Contains(AnimationStateName_Out));
            if (clip == null)
            {
                return 0;
            }
            else
            {
                return clip.length;
            }

        }
        public float GetInAnimationLength()
        {
            if (transition != TransitionType.Animator)
                return 0;
            if (animator == null)
                return 0;

            var clip = animator.runtimeAnimatorController.animationClips.SingleOrDefault(m => m.name.Contains(AnimationStateName_In));
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