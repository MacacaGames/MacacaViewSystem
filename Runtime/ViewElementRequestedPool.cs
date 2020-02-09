using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CloudMacaca.ViewSystem;
using System;

public class ViewElementRequestedPool
{
    ViewElementRuntimePool runtimePool => ViewControllerV2.runtimePool;

    Queue<ViewElement> viewElementQueue = new Queue<ViewElement>();

    ViewElement template;

    public Action<ViewElement> recoveryAction;

    public ViewElementRequestedPool(ViewElement template)
    {
        this.template = template;
    }

    public ViewElementRequestedPool(ViewElement template, Action<ViewElement> recoveryAction)
    {
        this.template = template;
        this.recoveryAction = recoveryAction;
    }

    public ViewElement Request(Transform root)
    {
        var viewElementInstance = runtimePool.RequestViewElement(template);

        viewElementInstance.ChangePage(true, root);
        viewElementQueue.Enqueue(viewElementInstance);

        return viewElementInstance;
    }

    public void RecoveryAll()
    {
        while (viewElementQueue.Count > 0)
        {
            var ve = viewElementQueue.Dequeue();
            ve.ChangePage(false, null, ignoreTransition: true);

            recoveryAction?.Invoke(ve);
        }
    }

    public int GetCurrentInUseViewElementCount()
    {
        return viewElementQueue.Count;
    }

}
