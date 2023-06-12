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
            }
            if (rotateToggle)
            {
                result += rotateAnimation.duration;
            }
            if (scaleToggle)
            {
                result += scaleAnimation.duration;
            }
            if (fadeToggle)
            {
                result += fadeAnimation.duration;
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
        // public float delay;
        public EaseStyle EaseStyle = EaseStyle.Linear;
        public bool isCustom;

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

        public UIMoveAnimationDirection direction = UIMoveAnimationDirection.Left;
        public Vector3 startValue;
        public Vector3 endValue;
        public MoveMode moveMode = MoveMode.MoveIn;
        public IEnumerator Play(RectTransform target)
        {
            Vector3 pos = Vector3.zero;
            float xOffset = target.rect.width / 2 + target.rect.width * target.pivot.x;
            float yOffset = target.rect.height / 2 + target.rect.height * target.pivot.y;
            switch (direction)
            {
                case UIMoveAnimationDirection.Left: pos = new Vector3(-xOffset, 0f, 0f); break;
                case UIMoveAnimationDirection.Right: pos = new Vector3(xOffset, 0f, 0f); break;
                case UIMoveAnimationDirection.Top: pos = new Vector3(0f, yOffset, 0f); break;
                case UIMoveAnimationDirection.Bottom: pos = new Vector3(0f, -yOffset, 0f); break;
                case UIMoveAnimationDirection.TopLeft: pos = new Vector3(-xOffset, yOffset, 0f); break;
                case UIMoveAnimationDirection.TopRight: pos = new Vector3(xOffset, yOffset, 0f); break;
                case UIMoveAnimationDirection.MiddleCenter: pos = Vector3.zero; break;
                case UIMoveAnimationDirection.BottomLeft: pos = new Vector3(-xOffset, -yOffset, 0f); break;
                case UIMoveAnimationDirection.BottomRight: pos = new Vector3(xOffset, -yOffset, 0f); break;
            }
            switch (moveMode)
            {
                case MoveMode.MoveIn:
                    target.anchoredPosition3D = isCustom ? startValue : pos;
                    return EaseUtility.To(target.anchoredPosition3D, endValue, duration, EaseStyle, OnAnimated, null);

                case MoveMode.MoveOut:
                    target.anchoredPosition3D = startValue;
                    return EaseUtility.To(target.anchoredPosition3D, endValue, duration, EaseStyle, OnAnimated, null);
                default: return null;
            }
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
            if (isCustom)
            {
                target.localEulerAngles = startValue;
            }
            // return target.DORotate(endValue, instant ? 0f : duration, rotateMode).SetDelay(instant ? 0f : delay).SetEaseStyle(EaseStyle);
            return EaseUtility.To(target.localEulerAngles, endValue, duration, EaseStyle, OnAnimated, null);
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
        public IEnumerator Play(RectTransform target, bool instant = false)
        {
            if (isCustom)
            {
                target.localScale = startValue;
            }
            // return CoroutineManager.Instance.ProgressionTask(endValue, instant ? 0f : duration).SetDelay(instant ? 0f : delay).SetEaseStyle(EaseStyle);
            return EaseUtility.To(target.localScale, endValue, duration, EaseStyle, OnAnimated, null);
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
                    throw new Exception("UIFadeAnimation is set but no CanvasGroup found on target object");
                }
            }
            if (isCustom)
            {
                _cg.alpha = startValue;
            }
            return EaseUtility.To(_cg.alpha, endValue, duration, EaseStyle, OnAnimated, null);
            void OnAnimated(float currentValue)
            {
                _cg.alpha = currentValue;
            }
        }
    }
}