using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    [DisallowMultipleComponent]
    public class ViewElementGroup : MonoBehaviour
    {
        List<ViewElement> childViewElements = new List<ViewElement>();
        void Awake()
        {
            SetupChild();
        }
        public void SetupChild()
        {
            childViewElements = GetComponentsInChildren<ViewElement>()
                .Where(m => m != GetComponent<ViewElement>())
                .ToList();
        }

        public void OnShowChild()
        {
            if (childViewElements.Count == 0)
            {
                ViewSystemLog.LogWarning("Target ViewElementGroup doesn't contain child ViewElement, Nothing will happend");
                return;
            }
            foreach (var item in childViewElements)
            {
                item.OnShow(0);
            }
        }

        public void OnLeaveChild(bool ignoreTransition = false)
        {
            if (childViewElements.Count == 0)
            {
                ViewSystemLog.LogWarning("Target ViewElementGroup doesn't contain child ViewElement, Nothing will happend");
                return;
            }
            foreach (var item in childViewElements)
            {
                item.OnLeave(0, false, ignoreTransition);
            }
        }

        public float GetOutAnimationLength()
        {
            if (childViewElements.Count == 0)
            {
                return 0;
            }
            return childViewElements.Max(m => m.GetOutAnimationLength());
        }
        //GetOutAnimationLength in NestedViewElement is the longest animation length in child
        public float GetInAnimationLength()
        {
            if (childViewElements.Count == 0)
            {
                return 0;
            }
            return childViewElements.Max(m => m.GetInAnimationLength());
        }
    }
}