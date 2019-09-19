using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CloudMacaca.ViewSystem
{
    public class NestedViewElement : ViewElement
    {
        // public new TransitionType transition
        // {
        //     get
        //     {
        //         return TransitionType.ActiveSwitch;
        //     }
        // }

        public ViewElement[] childViewElements;
        public override void Setup()
        {
            base.Setup();
            childViewElements = GetComponentsInChildren<ViewElement>().Where(m => m != this).ToArray();
        }
        // public override void ChangePage(bool show, Transform parent, float TweenTime, float delayIn, float delayOut, bool ignoreTransition = false)
        // {
        // }

        public override void OnShow(float delayIn = 0)
        {
            gameObject.SetActive(true);
            foreach (var ve in childViewElements)
            {
                ve.OnShow(delayIn);
            }
        }

        public override void OnLeave(float delayOut = 0, bool NeedPool = true, bool ignoreTransition = false)
        {
            foreach (var ve in childViewElements)
            {
                ve.OnLeave(delayOut, false, ignoreTransition);
            }
            StartCoroutine(DisableItem());
        }

        IEnumerator DisableItem()
        {
            yield return Yielders.GetWaitForSeconds(GetOutAnimationLength());
            gameObject.SetActive(false);
        }

        //GetOutAnimationLength in NestedViewElement is the longest animation length in child
        public override float GetOutAnimationLength()
        {
            return childViewElements.Max(m => m.GetOutAnimationLength());
        }
        //GetOutAnimationLength in NestedViewElement is the longest animation length in child
        public override float GetInAnimationLength()
        {
            return childViewElements.Max(m => m.GetInAnimationLength());
        }
    }
}