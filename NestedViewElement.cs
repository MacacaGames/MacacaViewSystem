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

            //Remove the item which is a child of Other NestedViewElement
            var nestedViewElements = childViewElements.Where(m => m.viewElement is NestedViewElement).Select(m => m.viewElement).Cast<NestedViewElement>();
            foreach (var item in nestedViewElements)
            {
                foreach (var ve in item.childViewElements)
                {
                    childViewElements.RemoveAll(m => m.viewElement == ve.viewElement);
                }
            }
        }

        public override void OnShow(float delayIn = 0)
        {
            gameObject.SetActive(true);
            if (lifeCyclesObjects != null)
            {
                foreach (var item in lifeCyclesObjects)
                {
                    item.OnBeforeShow();
                }
            }
            foreach (var item in childViewElements)
            {
                // if (item.viewElement is NestedViewElement)
                // {
                //     ((NestedViewElement)item.viewElement).OnShow(item.delayIn);
                // }
                // else
                // {

                // }
                item.viewElement.OnShow(item.delayIn);
            }
        }

        public override void OnLeave(float delayOut = 0, bool NeedPool = true, bool ignoreTransition = false)
        {
            needPool = NeedPool;
            if (lifeCyclesObjects != null)
            {
                foreach (var item in lifeCyclesObjects)
                {
                    item.OnBeforeLeave();
                }
            }
            foreach (var item in childViewElements)
            {
                // if (item.viewElement is NestedViewElement)
                // {
                //     ((NestedViewElement)item.viewElement).OnLeave(item.delayOut, false, ignoreTransition);
                // }
                // else
                // {

                // }
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