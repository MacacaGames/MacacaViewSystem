using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    public class ViewElementBehaviour : MonoBehaviour, IViewElementLifeCycle
    {
        [SerializeField]
        public UnityEngine.Events.UnityEvent OnBeforeLeaveHandler;
        [SerializeField]
        public UnityEngine.Events.UnityEvent OnBeforeShowHandler;
        [SerializeField]
        public UnityEngine.Events.UnityEvent OnStartLeaveHandler;
        [SerializeField]
        public UnityEngine.Events.UnityEvent OnStartShowHandler;
        [SerializeField]
        public UnityEngine.Events.UnityEvent OnChangedPageHandler;
        [SerializeField]
        public BoolEvent OnChangePageHandler;

        [System.Serializable]
        public class BoolEvent : UnityEngine.Events.UnityEvent<bool> { }

        /// <summary>
        /// Invoke Before the ViewElement is Leave, but after OnLeave delay
        /// </summary>
        public virtual void OnBeforeLeave()
        {
            OnBeforeLeaveHandler?.Invoke();
        }
        /// <summary>
        /// Invoke Before the ViewElement is Show, but after OnShow delay
        /// </summary>
        public virtual void OnBeforeShow()
        {
            OnBeforeShowHandler?.Invoke();
        }

        public virtual void OnChangePage(bool show)
        {
            OnChangePageHandler?.Invoke(show);
        }

        public virtual void OnStartLeave()
        {
            //throw new System.NotImplementedException();
            OnStartLeaveHandler?.Invoke();
        }

        public virtual void OnStartShow()
        {
            //throw new System.NotImplementedException();
            OnStartShowHandler?.Invoke();
        }
        /// <summary>
        /// Invoke Before the ViewElement is Leave, but after OnLeave delay
        /// </summary>
        public virtual void OnChangedPage()
        {
            OnChangedPageHandler?.Invoke();
        }

        ViewElement viewElement;
        protected virtual void Awake()
        {
            viewElement = GetComponent<ViewElement>();
            if (viewElement == null)
            {
                viewElement = GetComponentInParent<ViewElement>();
                if (viewElement)
                {
                    viewElement.RegisterLifeCycleObject(this);
                }
            }
        }

        protected virtual void OnDestroy()
        {
            if (viewElement)
                viewElement.UnRegisterLifeCycleObject(this);
        }

    }
}