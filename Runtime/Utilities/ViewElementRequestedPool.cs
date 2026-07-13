using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    public enum ViewElementChildRecoveryMode
    {
        ReturnToGlobalPool = 0,
        DestroyWithOwner = 1,
        UseChildPolicy = 2,
    }

    public class ViewElementRequestedPool : IDisposable
    {
        static readonly Dictionary<int, ViewElementRequestedPool> requestPoolCache =
            new Dictionary<int, ViewElementRequestedPool>();

        /// <summary>
        /// Get the ownerless, globally cached pool.
        /// </summary>
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

        readonly Queue<ViewElement> viewElementQueue = new Queue<ViewElement>();
        readonly Queue<ViewElement> ownerLocalQueue = new Queue<ViewElement>();
        readonly HashSet<ViewElement> ownedInstances = new HashSet<ViewElement>();
        readonly ViewElement template;
        readonly ViewElementLifetimeScope ownerLifetime;
        readonly ViewElementChildRecoveryMode childRecoveryMode;

        bool isDisposed;

        public Action<ViewElement> recoveryAction;

        public bool IsOwnerAware => ownerLifetime != null;
        public bool IsDisposed => isDisposed;
        public ViewElementChildRecoveryMode ChildRecoveryMode => childRecoveryMode;

        /// <summary>
        /// Creates an ownerless pool using the legacy global-pool behavior.
        /// Prefer ViewElement.Lifetime.CreatePool for pools owned by a runtime ViewElement.
        /// </summary>
        public ViewElementRequestedPool(ViewElement template)
            : this(template, null, ViewElementChildRecoveryMode.UseChildPolicy, null, false)
        {
        }

        /// <summary>
        /// Creates an ownerless pool using the legacy global-pool behavior.
        /// Prefer ViewElement.Lifetime.CreatePool for pools owned by a runtime ViewElement.
        /// </summary>
        public ViewElementRequestedPool(ViewElement template, Action<ViewElement> recoveryAction)
            : this(template, null, ViewElementChildRecoveryMode.UseChildPolicy, recoveryAction, false)
        {
        }

        public ViewElementRequestedPool(
            ViewElement template,
            ViewElement owner,
            ViewElementChildRecoveryMode childRecoveryMode,
            Action<ViewElement> recoveryAction = null)
            : this(
                template,
                owner != null ? owner.Lifetime : throw new ArgumentNullException(nameof(owner)),
                childRecoveryMode,
                recoveryAction,
                true)
        {
        }

        internal ViewElementRequestedPool(
            ViewElement template,
            ViewElementLifetimeScope ownerLifetime,
            ViewElementChildRecoveryMode childRecoveryMode,
            Action<ViewElement> recoveryAction)
            : this(template, ownerLifetime, childRecoveryMode, recoveryAction, true)
        {
        }

        ViewElementRequestedPool(
            ViewElement template,
            ViewElementLifetimeScope ownerLifetime,
            ViewElementChildRecoveryMode childRecoveryMode,
            Action<ViewElement> recoveryAction,
            bool registerWithOwner)
        {
            this.template = template != null ? template : throw new ArgumentNullException(nameof(template));
            this.ownerLifetime = ownerLifetime;
            this.childRecoveryMode = childRecoveryMode;
            this.recoveryAction = recoveryAction;

            if (ownerLifetime == null)
            {
                return;
            }

            ownerLifetime.ThrowIfDisposed();
            ValidateDestroyWithOwnerTemplate();

            if (registerWithOwner)
            {
                ownerLifetime.Own(this);
            }
        }

        public ViewElement Request(Transform root)
        {
            ThrowIfDisposed();
            ownerLifetime?.ThrowIfDisposed();

            ViewElement viewElementInstance = TakeOwnerLocalInstance();
            if (viewElementInstance == null)
            {
                viewElementInstance = runtimePool.RequestViewElement(template);
            }

            if (ownerLifetime != null)
            {
                ownedInstances.Add(viewElementInstance);
                viewElementInstance.RequestedPoolRecoveryHandler = HandleRequestedElementRecovery;
            }

            viewElementInstance.ChangePage(true, root, null);
            viewElementQueue.Enqueue(viewElementInstance);

            return viewElementInstance;
        }

        public T Request<T>(Transform root) where T : Component
        {
            return Request(root).GetComponent<T>();
        }

        public void RecoveryAll(bool ignoreTransition = true)
        {
            if (isDisposed)
            {
                return;
            }

            runtimePool.RecoveryQueuedViewElement(true);
            while (viewElementQueue.Count > 0)
            {
                ViewElement viewElement = viewElementQueue.Dequeue();
                if (viewElement == null)
                {
                    continue;
                }

                if (ignoreTransition)
                {
                    viewElement.RecoverImmediatelyToRequestedPool();
                }
                else
                {
                    viewElement.ChangePage(false, null, null, ignoreTransition: ignoreTransition);
                }
                recoveryAction?.Invoke(viewElement);
            }

            if (ignoreTransition)
            {
                runtimePool.RecoveryQueuedViewElement(true);
            }
        }

        public void Recovery(ViewElement viewElement, bool ignoreTransition = true)
        {
            if (isDisposed || viewElement == null)
            {
                return;
            }

            viewElementQueue.Remove(viewElement);
            if (ignoreTransition)
            {
                viewElement.RecoverImmediatelyToRequestedPool();
            }
            else
            {
                viewElement.ChangePage(false, null, null, ignoreTransition: ignoreTransition);
            }
            recoveryAction?.Invoke(viewElement);

            if (ignoreTransition)
            {
                runtimePool.RecoveryQueuedViewElement(true);
            }
        }

        /// <summary>
        /// Forces ViewElements already queued in the global runtime pool to finish recovery.
        /// DestroyWithOwner elements remain in their owner-local queue.
        /// </summary>
        public void RecoveryQueuedItems()
        {
            if (!isDisposed)
            {
                runtimePool.RecoveryQueuedViewElement(true);
            }
        }

        public int GetCurrentInUseViewElementCount()
        {
            return viewElementQueue.Count;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            ViewElement[] instances = ownedInstances.Where(instance => instance != null).ToArray();

            viewElementQueue.Clear();
            ownerLocalQueue.Clear();
            ownedInstances.Clear();

            foreach (ViewElement instance in instances)
            {
                instance.RequestedPoolRecoveryHandler = null;

                if (childRecoveryMode == ViewElementChildRecoveryMode.DestroyWithOwner)
                {
                    ViewElementRuntimePool.DestroyViewElementHierarchy(instance);
                }
                else
                {
                    bool ignoreChildPolicy = childRecoveryMode == ViewElementChildRecoveryMode.ReturnToGlobalPool;
                    instance.RecoverImmediatelyToRuntimePool(ignoreChildPolicy);
                }
            }
        }

        ViewElement TakeOwnerLocalInstance()
        {
            if (childRecoveryMode != ViewElementChildRecoveryMode.DestroyWithOwner)
            {
                return null;
            }

            while (ownerLocalQueue.Count > 0)
            {
                ViewElement instance = ownerLocalQueue.Dequeue();
                if (instance != null)
                {
                    return instance;
                }
            }

            return null;
        }

        bool HandleRequestedElementRecovery(ViewElement viewElement)
        {
            if (viewElement == null)
            {
                return true;
            }

            viewElementQueue.Remove(viewElement);

            if (isDisposed)
            {
                viewElement.RequestedPoolRecoveryHandler = null;
                ViewElementRuntimePool.DestroyViewElementHierarchy(viewElement);
                return true;
            }

            if (childRecoveryMode == ViewElementChildRecoveryMode.DestroyWithOwner)
            {
                viewElement.gameObject.SetActive(false);
                if (!ownerLocalQueue.Contains(viewElement))
                {
                    ownerLocalQueue.Enqueue(viewElement);
                }

                return true;
            }

            ownedInstances.Remove(viewElement);
            viewElement.RequestedPoolRecoveryHandler = null;
            viewElement.IgnoreRecoveryPolicyOnce =
                childRecoveryMode == ViewElementChildRecoveryMode.ReturnToGlobalPool;
            return false;
        }

        void ValidateDestroyWithOwnerTemplate()
        {
            if (childRecoveryMode != ViewElementChildRecoveryMode.DestroyWithOwner)
            {
                return;
            }

            int uniqueCount = template
                .GetComponentsInChildren<ViewElement>(true)
                .Count(viewElement => viewElement.IsUnique);

            if (uniqueCount > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot create a DestroyWithOwner pool for {template.name}: " +
                    $"the template contains {uniqueCount} unique ViewElement(s).");
            }
        }

        void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(ViewElementRequestedPool));
            }
        }
    }

    public static class QueueExtension
    {
        public static void Remove<T>(this Queue<T> queue, T itemToRemove) where T : class
        {
            List<T> list = queue.ToList();
            queue.Clear();
            foreach (T item in list)
            {
                if (item == itemToRemove)
                {
                    continue;
                }

                queue.Enqueue(item);
            }
        }
    }
}
