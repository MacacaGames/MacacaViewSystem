using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames.ViewSystem;
using System;
using System.Linq;
public class ViewElementRequestedPool
{
    ViewElementRuntimePool runtimePool => ViewController.runtimePool;

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

        viewElementInstance.ChangePage(true, root, null, ViewElement.RectTransformFlag.All);
        viewElementQueue.Enqueue(viewElementInstance);

        return viewElementInstance;
    }

    public T Request<T>(Transform root) where T : Component
    {
        return Request(root).GetComponent<T>(); ;
    }

    public void RecoveryAll(bool ignoreTransition = true)
    {
        while (viewElementQueue.Count > 0)
        {
            var ve = viewElementQueue.Dequeue();
            ve.ChangePage(false, null, null, ViewElement.RectTransformFlag.All, ignoreTransition: ignoreTransition);

            recoveryAction?.Invoke(ve);
        }
        //runtimePool.RecoveryQueuedViewElement(true);
    }

    public void Recovery(ViewElement ve, bool ignoreTransition = true)
    {
        viewElementQueue.Remove(ve);
        ve.ChangePage(false, null, null, ViewElement.RectTransformFlag.All, ignoreTransition: ignoreTransition);
        recoveryAction?.Invoke(ve);
        //runtimePool.RecoveryQueuedViewElement(true);
    }

    public int GetCurrentInUseViewElementCount()
    {
        return viewElementQueue.Count;
    }

}
public static class QueueExtension
{
    public static void Remove<T>(this Queue<T> queue, T itemToRemove) where T : class
    {
        var list = queue.ToList(); //Needs to be copy, so we can clear the queue
        queue.Clear();
        foreach (var item in list)
        {
            if (item == itemToRemove)
                continue;

            queue.Enqueue(item);
        }
    }
}