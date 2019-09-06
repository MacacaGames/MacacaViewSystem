using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewElementLifeCycle : MonoBehaviour, IViewElementLifeCycle
{
    // public virtual void OnAfterLeave()
    // {

    // }

    // public virtual void OnAfterShow()
    // {

    // }

    /// <summary>
    /// Invoke Before the ViewElement is Leave, but after OnLeave delay
    /// </summary>
    public virtual void OnBeforeLeave()
    {

    }
    /// <summary>
    /// Invoke Before the ViewElement is Show, but after OnShow delay
    /// </summary>
    public virtual void OnBeforeShow()
    {

    }

    public void OnStartLeave()
    {
        //throw new System.NotImplementedException();
    }

    public void OnStartShow()
    {
        //throw new System.NotImplementedException();
    }
}
