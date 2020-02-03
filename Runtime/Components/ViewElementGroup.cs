using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    [DisallowMultipleComponent]
    public class ViewElementGroup : MonoBehaviour
    {
        [HideInInspector]
        public ViewElement viewElement;
        List<ViewElement> childViewElements = new List<ViewElement>();
        List<ViewElementGroup> childViewElementGroups = new List<ViewElementGroup>();
        void Awake()
        {
            SetupChild();
        }

        public IEnumerable<ViewElement> GetChildElement()
        {
            return childViewElements;
        }
        public bool OnlyManualMode = false;
        public void SetupChild()
        {
            viewElement = GetComponent<ViewElement>();

            childViewElementGroups = GetComponentsInChildren<ViewElementGroup>()
                .Where(m => m != GetComponent<ViewElementGroup>())
                .ToList();

            childViewElements = GetComponentsInChildren<ViewElement>()
                .Where(m => m != viewElement)
                .ToList();

            //Remove the item which is a child of Other ViewElementGroup
            var viewElementsInChildGroup = childViewElementGroups.Select(m => m.childViewElements);
            foreach (var item in viewElementsInChildGroup)
            {
                foreach (var ve in item)
                {
                    childViewElements.RemoveAll(m => m == ve);
                }
            }

        }

        public void OnShowChild()
        {
            if (childViewElements.Count == 0)
            {
                //ViewSystemLog.LogWarning("Target ViewElementGroup doesn't contain child ViewElement, Nothing will happend");
                return;
            }
            foreach (var item in childViewElements)
            {
                item.OnShow();
                Debug.LogError($"{item.name} : ");
            }
            foreach (var item in childViewElementGroups)
            {
                item.viewElement.OnShow();
            }
        }

        public void OnLeaveChild(bool ignoreTransition = false)
        {
            if (childViewElements.Count == 0)
            {
                //ViewSystemLog.LogWarning("Target ViewElementGroup doesn't contain child ViewElement, Nothing will happend");
                return;
            }
            foreach (var item in childViewElements)
            {
                item.OnLeave(false, ignoreTransition);
            }
            foreach (var item in childViewElementGroups)
            {
                item.viewElement.OnLeave(false, ignoreTransition);
            }
        }

        public float GetOutDuration()
        {
            if (childViewElements.Count == 0)
            {
                return 0;
            }
            return childViewElements.Max(m => m.GetOutDuration());
        }
        //GetOutAnimationLength in NestedViewElement is the longest animation length in child
        public float GetInDuration()
        {
            if (childViewElements.Count == 0)
            {
                return 0;
            }
            return childViewElements.Max(m => m.GetInDuration());
        }
    }
}