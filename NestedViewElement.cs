using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CloudMacaca.ViewSystem
{
    public class NestedViewElement : ViewElement
    {
        [System.Serializable]
        public class ChildViewElement
        {
            public ViewElement viewElement;
            public float delayIn = 0;
            public float delayOut = 0;
        }

        public List<ChildViewElement> childViewElements;

        public bool IsSetup
        {
            get
            {
                if (childViewElements == null)
                {
                    return false;
                }
                return childViewElements.Count > 0;
            }
        }
        
        public override void Setup()
        {
            base.Setup();
        }

        public void SetupChild()
        {
            childViewElements = GetComponentsInChildren<ViewElement>()
              .Where(m => m != this)
              .Select(m => new ChildViewElement { viewElement = m }).ToList();
        }

        public override void OnShow(float delayIn = 0)
        {
            gameObject.SetActive(true);
            foreach (var item in childViewElements)
            {
                item.viewElement.OnShow(item.delayIn);
            }
        }

        public override void OnLeave(float delayOut = 0, bool NeedPool = true, bool ignoreTransition = false)
        {
            foreach (var item in childViewElements)
            {
                item.viewElement.OnLeave(item.delayOut, false, ignoreTransition);
            }
            StartCoroutine(DisableItem());
        }

        IEnumerator DisableItem()
        {
            yield return Yielders.GetWaitForSeconds(GetOutAnimationLength());
            gameObject.SetActive(false);
            OnLeaveAnimationFinish();
        }

        //GetOutAnimationLength in NestedViewElement is the longest animation length in child
        public override float GetOutAnimationLength()
        {
            return childViewElements.Max(m => m.viewElement.GetOutAnimationLength());
        }
        //GetOutAnimationLength in NestedViewElement is the longest animation length in child
        public override float GetInAnimationLength()
        {
            return childViewElements.Max(m => m.viewElement.GetInAnimationLength());
        }
    }
}