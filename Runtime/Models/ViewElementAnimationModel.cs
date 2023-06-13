using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System;
namespace MacacaGames.ViewSystem
{

    [Serializable]
    public class ViewElementAnimationGroup
    {
        public bool moveToggle;
        public UIMoveAnimation moveAnimation;
        public bool rotateToggle;
        public UIRotateAnimation rotateAnimation;
        public bool scaleToggle;
        public UIScaleAnimation scaleAnimation;
        public bool fadeToggle;
        public UIFadeAnimation fadeAnimation;
        public bool HasTweenAnimation
        {
            get
            {
                return moveToggle || rotateToggle || scaleToggle || fadeToggle;
            }
        }
        MicroCoroutine microCoroutine = new MicroCoroutine(ex => Debug.LogException(ex));
        public IEnumerator Play(RectTransform view, Action callback = null)
        {
            if (HasTweenAnimation)
            {
                if (moveToggle)
                {
                    microCoroutine.AddCoroutine(moveAnimation.Play(view));
                }
                if (rotateToggle)
                {
                    microCoroutine.AddCoroutine(rotateAnimation.Play(view));
                }
                if (scaleToggle)
                {
                    microCoroutine.AddCoroutine(scaleAnimation.Play(view));
                }
                if (fadeToggle)
                {
                    microCoroutine.AddCoroutine(fadeAnimation.Play(view));
                }
                while (!microCoroutine.IsEmpty)
                {
                    microCoroutine.Update();
                    yield return null;
                }
            }
            else
            {
                callback?.Invoke();
            }
        }

        public float GetDuration()
        {
            float result = 0;
            if (moveToggle)
            {
                result += moveAnimation.duration;
                result += moveAnimation.delay;
            }
            if (rotateToggle)
            {
                result += rotateAnimation.duration;
                result += rotateAnimation.delay;
            }
            if (scaleToggle)
            {
                result += scaleAnimation.duration;
                result += scaleAnimation.delay;
            }
            if (fadeToggle)
            {
                result += fadeAnimation.duration;
                result += fadeAnimation.delay;
            }
            return result;
        }
    }
    /// <summary>
    /// UI动画
    /// </summary>
    [Serializable]
    public class UIAnimation
    {
        public float duration = .4f;
        public float delay = 0;
        public EaseStyle EaseStyle = EaseStyle.Linear;
    }
    /// <summary>
    /// UI移动动画
    /// </summary>
    [Serializable]
    public class UIMoveAnimation : UIAnimation
    {
        public enum MoveMode
        {
            MoveIn,
            MoveOut
        }
        /// <summary>
        /// UI移动动画方向
        /// </summary>
        public enum UIMoveAnimationDirection
        {
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            MiddleCenter,
            BottomLeft,
            BottomRight,
        }

        public Vector3 startValue;
        public Vector3 endValue;
        public MoveMode moveMode = MoveMode.MoveIn;
        public IEnumerator Play(RectTransform target)
        {
            Vector3 pos = Vector3.zero;

            var _endValue = endValue;

            // if (!isCustom)
            // {
            //     _endValue = target.anchoredPosition3D;
            // }

            return EaseUtility.To(startValue, _endValue, duration, EaseStyle, OnAnimated, null, delay);

            void OnAnimated(Vector3 currentValue)
            {
                target.anchoredPosition3D = currentValue;
            }
        }
    }

    /// <summary>
    /// UI旋转动画
    /// </summary>
    [Serializable]
    public class UIRotateAnimation : UIAnimation
    {

        public Vector3 startValue;
        public Vector3 endValue;
        // public RotateMode rotateMode = RotateMode.Fast;
        public IEnumerator Play(RectTransform target)
        {
            Vector3 _endValue = endValue;

            // if (!isCustom)
            // {
            //     _endValue = target.localEulerAngles;
            // }
            // else
            // {
            _endValue = endValue;
            target.localEulerAngles = startValue;
            // }
            return EaseUtility.To(startValue, _endValue, duration, EaseStyle, OnAnimated, null, delay);
            void OnAnimated(Vector3 currentValue)
            {
                target.localEulerAngles = currentValue;
            }
        }


    }
    /// <summary>
    /// UI缩放动画
    /// </summary>
    [Serializable]
    public class UIScaleAnimation : UIAnimation
    {

        public Vector3 startValue = Vector3.zero;
        public Vector3 endValue = Vector3.one;
        public IEnumerator Play(RectTransform target)
        {
            Vector3 _endValue = endValue;
            // if (isCustom)
            // {
            //     _endValue = target.localScale;
            //     target.localScale = startValue;
            // }
            // else
            // {
            _endValue = endValue;
            target.localScale = startValue;
            // }

            // yield return DoDelay();
            return EaseUtility.To(startValue, _endValue, duration, EaseStyle, OnAnimated, null, delay);
            void OnAnimated(Vector3 currentValue)
            {
                target.localScale = currentValue;
            }
        }
    }

    /// <summary>
    /// UI淡入淡出动画
    /// </summary>
    [Serializable]
    public class UIFadeAnimation : UIAnimation
    {
        public float startValue;
        public float endValue = 1f;

        CanvasGroup _cg;
        public IEnumerator Play(RectTransform target)
        {
            if (_cg == null)
            {
                _cg = target.GetComponent<CanvasGroup>();
                if (_cg == null)
                {
                    Debug.LogError("UIFadeAnimation is set but no CanvasGroup found on target object", target);
                }
            }
            float _endValue = endValue;
            // if (isCustom)
            // {
            //     _cg.alpha = startValue;
            // }
            // else
            // {
            _endValue = endValue;
            _cg.alpha = startValue;
            // }
            return EaseUtility.To(startValue, _endValue, duration, EaseStyle, OnAnimated, null, delay);
            void OnAnimated(float currentValue)
            {
                _cg.alpha = currentValue;
            }
        }
    }
}