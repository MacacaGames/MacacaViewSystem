using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using UnityEngine;
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
        public bool dynamicChild = false;

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
        public override Selectable[] GetSelectables()
        {
            return childViewElements.SelectMany(m => m.viewElement.GetSelectables()).ToArray();
        }
        public override void Setup()
        {
            base.Setup();
        }

        public void SetupChild()
        {
            List<ChildViewElement> lastChildViewElements = childViewElements;

            childViewElements = GetComponentsInChildren<ViewElement>()
                .Where(m => m != this)
                .Select(m =>
                {
                    if (m.IsUnique)
                    {
                        Debug.LogWarning($"The child ViewElement [{m.name}] inside [{name}] is setup as Unique ViewElement, since NestedViewElement don't support Unique ViewElement Component Injection in child, the setting will be ignore and component won't be Inject.", this);
                    }
                    var l = lastChildViewElements.SingleOrDefault(x => x.viewElement == m);
                    float d_in = lastChildViewElements == null || l == null ? 0f : l.delayIn;
                    float d_out = lastChildViewElements == null || l == null ? 0f : l.delayOut;
                    return new ChildViewElement
                    {
                        viewElement = m,
                        delayIn = d_in,
                        delayOut = d_out
                    };
                }
                ).ToList();

            //Remove the item which is a child of Other NestedViewElement
            var nestedViewElements = childViewElements.Where(m => m.viewElement is NestedViewElement).Select(m => m.viewElement).Cast<NestedViewElement>().ToArray();
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

            if (IsSetup == false && dynamicChild == true)
            {
                SetupChild();
                return;
            }

            foreach (var item in childViewElements)
            {
                item.viewElement.OnShow(item.delayIn);
            }
            if (lifeCyclesObjects != null)
            {
                foreach (var item in lifeCyclesObjects)
                {
                    item.OnStartShow();
                }
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
            if (dynamicChild == false)
            {
                foreach (var item in childViewElements)
                {
                    item.viewElement.OnLeave(item.delayOut, false, ignoreTransition);
                }
            }
            if (lifeCyclesObjects != null)
            {
                foreach (var item in lifeCyclesObjects)
                {
                    item.OnStartLeave();
                }
            }
            CoroutineManager.Instance.StartCoroutine(DisableItem());
        }

        IEnumerator DisableItem()
        {
            yield return Yielders.GetWaitForSeconds(GetOutAnimationLength());
            gameObject.SetActive(false);
            OnLeaveAnimationFinish();
            if (dynamicChild) childViewElements.Clear();
        }

        //GetOutAnimationLength in NestedViewElement is the longest animation length in child
        public override float GetOutAnimationLength()
        {
            if (childViewElements.Count == 0)
            {
                return 0;
            }
            return childViewElements.Max(m => m.viewElement.GetOutAnimationLength());
        }
        //GetOutAnimationLength in NestedViewElement is the longest animation length in child
        public override float GetInAnimationLength()
        {
            if (childViewElements.Count == 0)
            {
                return 0;
            }
            return childViewElements.Max(m => m.viewElement.GetInAnimationLength());
        }
    }
}