using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
namespace MacacaGames.ViewSystem
{
    public class ViewElementRequestedPool
    {
        static Dictionary<int, ViewElementRequestedPool> requestPoolCache = new Dictionary<int, ViewElementRequestedPool>();

        /// <summary>
        /// Get the cached pool
        /// </summary>
        /// <param name="template">The source ViewElement</param>
        /// <returns>the pool</returns>
        public static ViewElementRequestedPool GetPool(ViewElement template)
        {
            if (!requestPoolCache.TryGetValue(template.GetInstanceID(), out ViewElementRequestedPool result))
            {
                result = new ViewElementRequestedPool(template);
                requestPoolCache.Add(template.GetInstanceID(), result);
            }
            return result;
        }

        ViewElementRuntimePool runtimePool => ViewController.runtimePool;

        Queue<ViewElement> viewElementQueue = new Queue<ViewElement>();

        ViewElement template;

        public Action<ViewElement> recoveryAction;

        /// <summary>
        /// Create new ViewElementRequestedPool, it is strongly recommend to use <See cref="ViewElementRequestedPool.GetPool(ViewElement)"> to get the global cached instance instead of create a new one.
        /// </summary>
        /// <param name="template">The source ViewElement</param>
        public ViewElementRequestedPool(ViewElement template)
        {
            this.template = template;
        }

        /// <summary>
        /// Create new ViewElementRequestedPool, it is strongly recommend to use <See cref="ViewElementRequestedPool.GetPool(ViewElement)"> to get the global cached instance instead of create a new one.
        /// </summary>
        /// <param name="template">The source ViewElement</param>
        /// <param name="recoveryAction">The callback will invoke every time while the ViewElement is recovery which managed by ViewElementRequestedPool</param>
        public ViewElementRequestedPool(ViewElement template, Action<ViewElement> recoveryAction)
        {
            this.template = template;
            this.recoveryAction = recoveryAction;
        }

        public ViewElement Request(Transform root)
        {
            var viewElementInstance = runtimePool.RequestViewElement(template);

            viewElementInstance.ChangePage(true, root, null);
            viewElementQueue.Enqueue(viewElementInstance);

            return viewElementInstance;
        }

        public T Request<T>(Transform root) where T : Component
        {
            return Request(root).GetComponent<T>(); ;
        }

        public void RecoveryAll(bool ignoreTransition = true)
        {
            runtimePool.RecoveryQueuedViewElement(true);
            while (viewElementQueue.Count > 0)
            {
                var ve = viewElementQueue.Dequeue();
                ve.ChangePage(false, null, null, ignoreTransition: ignoreTransition);

                recoveryAction?.Invoke(ve);
            }
            //runtimePool.RecoveryQueuedViewElement(true);
        }
        

        public void Recovery(ViewElement ve, bool ignoreTransition = true)
        {
            viewElementQueue.Remove(ve);
            ve.ChangePage(false, null, null, ignoreTransition: ignoreTransition);
            recoveryAction?.Invoke(ve);
            //runtimePool.RecoveryQueuedViewElement(true);
        }
        /// <summary>
        /// While call Recovery or RecoveryAll it's not really recovery, the system will move the element to a queue first and waiting for next system recovery cycle
        /// This API make system force recovery all queue element
        /// </summary>
        public void RecoveryQueuedItems()
        {
            runtimePool.RecoveryQueuedViewElement(true);
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
}