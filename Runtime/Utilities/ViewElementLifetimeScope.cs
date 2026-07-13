using System;
using System.Collections.Generic;
using System.Threading;

namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Owns cleanup work that must run when a runtime ViewElement is permanently destroyed.
    /// Recovery to the runtime pool does not dispose the scope.
    /// </summary>
    public sealed class ViewElementLifetimeScope : IDisposable
    {
        readonly object syncRoot = new object();
        readonly List<Action> cleanupActions = new List<Action>();
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken;
        readonly ViewElement owner;

        bool isDisposed;

        internal ViewElementLifetimeScope(ViewElement owner)
        {
            this.owner = owner;
            cancellationToken = cancellationTokenSource.Token;
        }

        public bool IsDisposed
        {
            get
            {
                lock (syncRoot)
                {
                    return isDisposed;
                }
            }
        }

        public bool IsAlive => !IsDisposed;

        public CancellationToken Token => cancellationToken;

        public void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ViewElementLifetimeScope));
            }
        }

        public void AddCleanup(Action cleanup)
        {
            if (cleanup == null)
            {
                throw new ArgumentNullException(nameof(cleanup));
            }

            lock (syncRoot)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(ViewElementLifetimeScope));
                }

                cleanupActions.Add(cleanup);
            }
        }

        public T Own<T>(T disposable) where T : IDisposable
        {
            if (disposable == null)
            {
                throw new ArgumentNullException(nameof(disposable));
            }

            AddCleanup(disposable.Dispose);
            return disposable;
        }

        public ViewElementRequestedPool CreatePool(
            ViewElement template,
            ViewElementChildRecoveryMode childRecoveryMode,
            Action<ViewElement> recoveryAction = null)
        {
            ThrowIfDisposed();
            return new ViewElementRequestedPool(template, this, childRecoveryMode, recoveryAction);
        }

        public void Subscribe<THandler>(Action<THandler> add, Action<THandler> remove, THandler handler)
        {
            if (add == null)
            {
                throw new ArgumentNullException(nameof(add));
            }

            if (remove == null)
            {
                throw new ArgumentNullException(nameof(remove));
            }

            ThrowIfDisposed();
            add(handler);

            try
            {
                AddCleanup(() => remove(handler));
            }
            catch
            {
                remove(handler);
                throw;
            }
        }

        public void Dispose()
        {
            Action[] actions;

            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                actions = cleanupActions.ToArray();
                cleanupActions.Clear();
            }

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (Exception exception)
            {
                ViewSystemLog.LogError(
                    $"Lifetime cancellation failed for ViewElement {owner?.name}: {exception}",
                    owner);
            }

            for (int i = actions.Length - 1; i >= 0; i--)
            {
                try
                {
                    actions[i]();
                }
                catch (Exception exception)
                {
                    ViewSystemLog.LogError(
                        $"Lifetime cleanup failed for ViewElement {owner?.name}: {exception}",
                        owner);
                }
            }

            cancellationTokenSource.Dispose();
        }
    }
}
