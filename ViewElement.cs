using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UniRx;
using System;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.Events;

namespace CloudMacaca.ViewSystem
{
    public class ViewElement : MonoBehaviour
    {
        public enum TransitionType
        {
            Animator, CanvasGroupAlpha, ActiveSwitch, Custom
        }
        public TransitionType transition = TransitionType.Animator;
        public enum AnimatorTransitionType
        {
            Direct, Trigger
        }
        //Animator
        public AnimatorTransitionType animatorTransitionType = AnimatorTransitionType.Direct;

        public string AnimationStateName_In = "In";

        public string AnimationStateName_Out = "Out";
        public string AnimationStateName_Loop = "Loop";
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


        private Transform poolParent;
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
        Stack<LastTransform> lastTransform = new Stack<LastTransform>();
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
        public bool isFloating
        {
            get
            {
                return lastTransform.Count > 0;
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
        void Awake()
        {
            Setup();
        }

        public void Setup()
        {
            poolParent = transform.parent;
            poolScale = transform.localScale;
            poolPosition = rectTransform.anchoredPosition3D;
            //Get Animator From self first
            _animator = GetComponent<Animator>();
            if (_animator != null) return;
            //Get Animator From child
            _animator = GetComponentInChildren<Animator>();

        }
        Coroutine AnimationIsEndCheck = null;

        public void Reshow()
        {
            OnShow(0);
        }
        public void ChangePage(bool show, Transform parent, float TweenTime, float delayIn, float delayOut)
        {
            if (show)
            {

                if (parent == null)
                {
                    throw new NullReferenceException(gameObject.name + " does not set the parent for next viewpage.");
                }

                //還在池子裡，應該先 OnShow
                //或是正在離開，都要重播 OnShow
                if (rectTransform.parent == poolParent || OnLeaveWorking)
                {
                    rectTransform.SetParent(parent, true);
                    rectTransform.anchoredPosition3D = Vector3.zero;
                    rectTransform.localScale = Vector3.one;
                    OnShow(delayIn);
                }
                else
                {
                    rectTransform.SetParent(parent, true);
                    rectTransform.DOAnchorPos3D(Vector3.zero, TweenTime);
                    rectTransform.DOScale(Vector3.one, TweenTime);
                }
            }
            else
            {
                OnLeave(delayOut);
            }
        }

        public void OnShow(float delayIn = 0)
        {
            //停掉正在播放的 Leave 動畫
            if (OnLeaveDisposable != null)
            {
                OnLeaveDisposable.Dispose();
            }
            gameObject.SetActive(true);
            Observable
                .Timer(TimeSpan.FromSeconds(delayIn))
                .Subscribe(_ =>
                {
                    if (transition == TransitionType.Animator)
                    {
                        animator.Play(AnimationStateName_In);
                    }
                    else if (transition == TransitionType.CanvasGroupAlpha)
                    {
                        canvasGroup.DOFade(1, canvasInTime).SetEase(canvasInEase);
                    }
                    else if (transition == TransitionType.Custom)
                    {
                        OnShowHandle.Invoke();
                    }
                    else
                    {

                    }
                });
        }
        bool OnLeaveWorking = false;
        IDisposable OnLeaveDisposable;
        public void OnLeave(float delayOut = 0,bool NeedPool = true)
        {
            needPool = NeedPool;
            OnLeaveWorking = true;
            OnLeaveDisposable = Observable
                .Timer(TimeSpan.FromSeconds(delayOut))
                .Subscribe(_ =>
                {
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
                        if (canvasGroup == null) Debug.LogError("No Canvas Group Found on this Object",this);
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
                        OnLeaveAnimationFinish();
                    }
                    else
                    {
                        gameObject.SetActive(false);
                        OnLeaveAnimationFinish();
                    }
                });
        }

        //OnShow OnLeave 應該只在乎要進場或出場
        //如果要改變 Parent 應該要用另外的方法
        // public void OnShow(Transform parentOverwrite, bool playInAnimtionWhileParentOverwrite = false, float parentChangeTweenTime = 0.4f, float delayIn = 0)
        // {
        //     if (!gameObject.activeSelf)
        //     {
        //         parentChangeTweenTime = 0;
        //     }
        //     gameObject.SetActive(true);

        //     // 還在 pool 中
        //     if (isUsing == false)
        //     {
        //         isUsing = true;
        //         lastTransform.Clear();
        //         rectTransform.SetParent(parentOverwrite, true);
        //         rectTransform.anchoredPosition = Vector2.zero;
        //         rectTransform.localScale = Vector3.one;
        //     }
        //     else if (parentOverwrite != null)
        //     {
        //         lastTransform.Push(new LastTransform(rectTransform.parent, rectTransform.anchoredPosition3D, rectTransform.localScale));

        //         rectTransform.SetParent(parentOverwrite, true);
        //         rectTransform.DOAnchorPos3D(Vector3.zero, parentChangeTweenTime);
        //         rectTransform.DOScale(Vector3.one, parentChangeTweenTime);
        //         //isFloating = true;
        //         return;
        //     }



        //     Observable
        //     .Timer(TimeSpan.FromSeconds(delayIn))
        //     .Subscribe(_ =>
        //     {


        //         if (transition == TransitionType.Animator)
        //         {
        //             animator.Play(AnimationStateName_In);
        //         }
        //         else if (transition == TransitionType.CanvasGroupAlpha)
        //         {
        //             canvasGroup.DOFade(1, canvasInTime);
        //         }
        //         else if (transition == TransitionType.Custom)
        //         {
        //             OnShowHandle.Invoke();
        //         }
        //         else
        //         {

        //         }

        //     });

        // }

        // public void OnLeave(float parentChangeTweenTime = 0.4f, bool directLeaveWhileFloat = false, float delayOut = 0, bool disableGameObjectOnComplete = true)
        // {
        //     if (lastTransform.Count != 0 && directLeaveWhileFloat == false)
        //     {
        //         Debug.Log(gameObject.name + "float");
        //         var p = lastTransform.Pop();
        //         rectTransform.SetParent(p.Parent, true);
        //         rectTransform.DOAnchorPos3D(p.Position, parentChangeTweenTime);
        //         rectTransform.DOScale(p.Scale, parentChangeTweenTime);
        //         //isFloating = false;
        //         return;
        //     }

        //     Observable
        //         .Timer(TimeSpan.FromSeconds(delayOut))
        //         .Subscribe(_ =>
        //         {
        //             if (transition == TransitionType.Animator)
        //             {
        //                 try
        //                 {

        //                     if (animatorTransitionType == AnimatorTransitionType.Direct)
        //                     {

        //                         if (animator.HasState(0, Animator.StringToHash(AnimationStateName_Out)))
        //                             animator.Play(AnimationStateName_Out);
        //                         else
        //                             animator.Play("Disable");


        //                     }
        //                     else
        //                     {
        //                         animator.ResetTrigger(AnimationStateName_Out);
        //                         animator.SetTrigger(AnimationStateName_Out);
        //                     }

        //                     DirectLeaveWhileFloat = directLeaveWhileFloat;
        //                     DisableGameObjectOnComplete = disableGameObjectOnComplete;

        //                 }
        //                 catch
        //                 {
        //                     gameObject.SetActive(false);
        //                     OnLeaveAnimationFinish();
        //                 }

        //             }
        //             else if (transition == TransitionType.CanvasGroupAlpha)
        //             {
        //                 if (canvasGroup == null) Debug.LogError("No Canvas Group Found on this Object");
        //                 canvasGroup.DOFade(0, canvasOutTime).OnComplete(
        //                     () =>
        //                     {
        //                         if (disableGameObjectOnComplete == true)
        //                             gameObject.SetActive(false);

        //                         OnLeaveAnimationFinish();
        //                     }
        //                 );
        //             }
        //             else if (transition == TransitionType.Custom)
        //             {
        //                 OnLeaveHandle.Invoke();
        //                 OnLeaveAnimationFinish();
        //             }
        //             else
        //             {
        //                 gameObject.SetActive(false);
        //                 OnLeaveAnimationFinish();
        //             }
        //         });
        // }
        bool needPool = true;
        public void OnLeaveAnimationFinish()
        {
            OnLeaveWorking = false;
            OnLeaveDisposable = null;
            
            if(needPool == false){
                return;
            }
            rectTransform.SetParent(poolParent, true);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            
        }

        bool DirectLeaveWhileFloat = false;
        public bool DisableGameObjectOnComplete = true;
        IEnumerator CheckAnimationIsEnd()
        {

            //yield return new WaitUntil( () => animator.GetCurrentAnimatorStateInfo(0).IsName(AnimationStateName_Out) == true);

            while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Disable") && !animator.IsInTransition(0))
            {
                yield return null;
            }

            gameObject.SetActive(false);
            AnimationIsEndCheck = null;


            FloatingObjectCheck();
        }

        public void FloatingObjectCheck()
        {
            if (DirectLeaveWhileFloat)
            {
                rectTransform.SetParent(poolParent, true);
                rectTransform.DOAnchorPos3D(poolPosition, 0);
                //isFloating = false;
            }
        }

        System.Action OnComplete;
        public void RunBeforeDisactive()
        {
            if (OnComplete != null)
                OnComplete();
        }

        public bool AnimatorIsInLoopOrEmptyOrDisableState()
        {
            if (animator == null)
                return true;
            if (!animator.HasState(0, Animator.StringToHash(AnimationStateName_Loop)) || !animator.HasState(0, Animator.StringToHash("Empty")))
                return true;

            return animator.GetCurrentAnimatorStateInfo(0).IsName(AnimationStateName_Loop) || animator.GetCurrentAnimatorStateInfo(0).IsName("Empty") || animator.GetCurrentAnimatorStateInfo(0).IsName("Disable");
        }

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
        public void SampleToOutLastFrame()
        {
            if (animator == null)
            {
                return;
            }
            if (animator.gameObject.activeSelf != true)
            {
                return;
            }

            var clip = animator.runtimeAnimatorController.animationClips.SingleOrDefault(m => m.name.Contains(AnimationStateName_Out));
            if (clip == null)
            {
                return;
            }

            clip.SampleAnimation(animator.gameObject, clip.length - 0.01f);
        }

    }
}