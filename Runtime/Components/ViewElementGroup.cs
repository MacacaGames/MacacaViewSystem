﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    [DisallowMultipleComponent]
    public class ViewElementGroup : MonoBehaviour
    {
        [HideInInspector]
        public ViewElement viewElement;
        List<ViewElement> childViewElements = new List<ViewElement>();
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

            var childViewElementGroups = GetComponentsInChildren<ViewElementGroup>()
                .Where(m => m != this);

            foreach (var item in childViewElementGroups)
            {
                item.SetupChild();
            }

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
            childViewElements.RemoveAll(m => m == null);
            // childViewElementGroups.RemoveAll(m => m == null);

            if (childViewElements.Count == 0)
            {
                //ViewSystemLog.LogWarning("Target ViewElementGroup doesn't contain child ViewElement, Nothing will happend");
                return;
            }
            foreach (var item in childViewElements)
            {
                item.OnShow();
            }

        }

        public void OnLeaveChild(bool ignoreTransition = false)
        {
            childViewElements.RemoveAll(m => m == null);
            if (childViewElements.Count == 0)
            {
                //ViewSystemLog.LogWarning("Target ViewElementGroup doesn't contain child ViewElement, Nothing will happend");
                return;
            }
            foreach (var item in childViewElements)
            {
                item.OnLeave(false, ignoreTransition);
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