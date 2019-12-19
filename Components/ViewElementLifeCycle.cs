using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewElementLifeCycle : MonoBehaviour, IViewElementLifeCycle
{
    [SerializeField]
    UnityEngine.Events.UnityEvent OnBeforeLeaveHandler;
    [SerializeField]
    UnityEngine.Events.UnityEvent OnBeforeShowHandler;
    [SerializeField]
    UnityEngine.Events.UnityEvent OnStartLeaveHandler;
    [SerializeField]
    UnityEngine.Events.UnityEvent OnStartShowHandler;
    [SerializeField]
    BoolEvent OnChangePageHandler;

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

    public void OnChangePage(bool show)
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

    [System.Serializable]
    public class BoolEvent : UnityEngine.Events.UnityEvent<bool>
    {
    }
}
