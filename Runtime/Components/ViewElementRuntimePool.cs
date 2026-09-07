using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    public enum ViewElementGlobalEvictionMode
    {
        NormalBudget = 0,
        EmergencyHardCap = 1,
    }

    public sealed class ViewElementPoolTrimRequest
    {
        public bool DryRun;
        public ViewElementGlobalEvictionMode Mode = ViewElementGlobalEvictionMode.NormalBudget;
        public int? TargetQueuedInstances;
        public int? TargetQueuedHierarchyGameObjects;
        public int MaxEvictions = int.MaxValue;
        public float MinimumIdleSeconds;
        public bool IdleOnly;
    }

    public sealed class ViewElementPoolTrimResult
    {
        public int BeforeQueuedInstances;
        public int AfterQueuedInstances;
        public int BeforeQueuedHierarchyGameObjects;
        public int AfterQueuedHierarchyGameObjects;
        public int ProjectedAfterQueuedInstances;
        public int ProjectedAfterQueuedHierarchyGameObjects;
        public int EvictedInstances;
        public int EvictedHierarchyGameObjects;
        public int BlockedNestedUnique;
        public int BlockedIneligible;
        public int BlockedTooYoung;
        public int SkippedStale;
        public bool BudgetSatisfied;
        public int RemainingOverBudgetInstances;
        public int RemainingOverBudgetHierarchyGameObjects;
    }

    public sealed class ViewElementGlobalPoolBudgetOptions
    {
        public bool Enabled;
        public ViewElementGlobalEvictionMode Mode = ViewElementGlobalEvictionMode.NormalBudget;
        public int HighWatermarkInstances;
        public int LowWatermarkInstances;
        public int HighWatermarkHierarchyGameObjects;
        public int LowWatermarkHierarchyGameObjects;
        public int MaxEvictionsPerFrame = 5;
        public float MinimumIdleSeconds;
        public bool AllowEmergencyHardCapOnLowMemory;

        public ViewElementGlobalPoolBudgetOptions Clone()
        {
            return (ViewElementGlobalPoolBudgetOptions)MemberwiseClone();
        }
    }

    public sealed class ViewElementRuntimePoolSnapshot
    {
        public sealed class QueuedEntry
        {
            public int SourceKey { get; internal set; }
            public int InstanceId { get; internal set; }
            public int HierarchyGameObjectCount { get; internal set; }
            public int NestedUniqueCount { get; internal set; }
            public float QueuedAt { get; internal set; }
            public string SourceName { get; internal set; }
            public ViewElementRecoveryPolicy RecoveryPolicy { get; internal set; }
            public int RecoveryKeepCount { get; internal set; }
            public bool ExplicitlyAllowsGlobalEviction { get; internal set; }
            public bool IsEligible { get; internal set; }
            public bool IsActive { get; internal set; }
            public bool IsPendingRecovery { get; internal set; }
        }

        public bool BudgetEnabled { get; internal set; }
        public ViewElementGlobalEvictionMode BudgetMode { get; internal set; }
        public int HighWatermarkInstances { get; internal set; }
        public int LowWatermarkInstances { get; internal set; }
        public int HighWatermarkHierarchyGameObjects { get; internal set; }
        public int LowWatermarkHierarchyGameObjects { get; internal set; }
        public float MinimumIdleSeconds { get; internal set; }
        public int LogicalQueuedInstances { get; internal set; }
        public int LogicalQueuedHierarchyGameObjects { get; internal set; }
        public int EligibleQueuedInstances { get; internal set; }
        public int BlockedNestedUniqueInstances { get; internal set; }
        public int BlockedIneligibleInstances { get; internal set; }
        public int BlockedTooYoungInstances { get; internal set; }
        public int PendingRecoveryInstances { get; internal set; }
        public int PendingRecoveryHierarchyGameObjects { get; internal set; }
        public int PendingDestroyInstances { get; internal set; }
        public int PendingDestroyHierarchyGameObjects { get; internal set; }
        public int PostFrameActualHierarchyGameObjects { get; internal set; }
        public long PoolMissCount { get; internal set; }
        public long InstantiateCount { get; internal set; }
        public double InstantiateMilliseconds { get; internal set; }
        public long TrimCount { get; internal set; }
        public long TrimEvictedInstances { get; internal set; }
        public bool LastBudgetSatisfied { get; internal set; }
        public int LastRemainingOverBudgetInstances { get; internal set; }
        public int LastRemainingOverBudgetHierarchyGameObjects { get; internal set; }
        public ViewElementPoolTrimResult LastTrimResult { get; internal set; }
        public IReadOnlyList<QueuedEntry> QueuedEntries { get; internal set; }
        public IReadOnlyList<int> SourceKeys { get; internal set; }
        public IReadOnlyDictionary<int, string> SourceNames { get; internal set; }
        public IReadOnlyList<int> PendingRecoveryInstanceIds { get; internal set; }
    }

    public class ViewElementRuntimePool : MonoBehaviour
    {
        sealed class QueuedEntry
        {
            public readonly ViewElement viewElement;
            public readonly int sourceKey;
            public readonly int instanceId;
            public readonly int hierarchyGameObjectCount;
            public int nestedUniqueCount;
            public bool explicitlyAllowsGlobalEviction;
            public readonly float queuedAt;

            public QueuedEntry(
                ViewElement viewElement,
                int sourceKey,
                bool explicitlyAllowsGlobalEviction)
            {
                this.viewElement = viewElement;
                this.sourceKey = sourceKey;
                this.explicitlyAllowsGlobalEviction = explicitlyAllowsGlobalEviction;
                instanceId = viewElement.GetInstanceID();
                hierarchyGameObjectCount = viewElement
                    .GetComponentsInChildren<Transform>(true)
                    .Length;
                nestedUniqueCount = viewElement
                    .GetComponentsInChildren<ViewElement>(true)
                    .Count(x => x != viewElement && x.IsUnique);
                queuedAt = Time.unscaledTime;
            }
        }

        sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);

            public int GetHashCode(T obj) =>
                ReferenceEquals(obj, null) ? 0 : RuntimeHelpers.GetHashCode(obj);
        }

        ViewElementPool _hierachyPool;
        public void Init(ViewElementPool hierachyPool)
        {
            _hierachyPool = hierachyPool;
        }

        public void ConfigureGlobalPoolBudget(ViewElementGlobalPoolBudgetOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            ValidateBudgetOptions(options);
            StopBudgetEvictionRunner();
            StopIdleEvictionRunner();
            budgetConfigurationVersion++;
            budgetEvictionForceRequested = false;
            budgetOptions = options.Clone();

            if (budgetOptions.Enabled && isActiveAndEnabled && IsOverHighWatermark())
            {
                ScheduleBudgetEviction();
            }

            if (budgetOptions.Enabled && budgetOptions.MinimumIdleSeconds > 0 && isActiveAndEnabled)
            {
                ScheduleIdleEviction();
            }
        }

        void OnEnable()
        {
            Application.lowMemory += OnLowMemory;

            if (budgetOptions.Enabled && IsOverHighWatermark())
            {
                ScheduleBudgetEviction();
            }

            if (budgetOptions.Enabled && budgetOptions.MinimumIdleSeconds > 0)
            {
                ScheduleIdleEviction();
            }
        }

        void OnDisable()
        {
            Application.lowMemory -= OnLowMemory;
            StopBudgetEvictionRunner();
            StopIdleEvictionRunner();
        }

        void OnDestroy()
        {
            Application.lowMemory -= OnLowMemory;
            StopBudgetEvictionRunner();
            StopIdleEvictionRunner();
        }

        void LateUpdate()
        {
            // Destroy is deferred by Unity. Reconcile only the bounded set of
            // victims scheduled by the trim pipeline; never rescan every pool
            // hierarchy just to update accounting.
            RefreshPendingDestroyAccounting();
        }
        [SerializeField]
        Dictionary<int, Queue<ViewElement>> veDicts = new Dictionary<int, Queue<ViewElement>>();

        readonly LinkedList<QueuedEntry> globalQueuedIndex = new LinkedList<QueuedEntry>();
        readonly Dictionary<int, LinkedListNode<QueuedEntry>> globalQueuedNodesByInstanceId =
            new Dictionary<int, LinkedListNode<QueuedEntry>>();
        readonly HashSet<ViewElement> pendingRecoverySet =
            new HashSet<ViewElement>(new ReferenceComparer<ViewElement>());
        readonly Dictionary<ViewElement, int> pendingRecoveryHierarchyCosts =
            new Dictionary<ViewElement, int>(new ReferenceComparer<ViewElement>());
        readonly HashSet<QueuedEntry> pendingDestroyEntries = new HashSet<QueuedEntry>();
        readonly Dictionary<ViewElement, QueuedEntry> pendingDestroyEntriesByViewElement =
            new Dictionary<ViewElement, QueuedEntry>(new ReferenceComparer<ViewElement>());
        ViewElementGlobalPoolBudgetOptions budgetOptions = new ViewElementGlobalPoolBudgetOptions();
        ViewElementPoolTrimResult lastTrimResult;
        Coroutine budgetEvictionRunner;
        Coroutine idleEvictionRunner;
        int budgetConfigurationVersion;
        bool budgetEvictionForceRequested;
        ViewElementGlobalEvictionMode budgetEvictionMode;
        int queuedInstanceCount;
        int queuedHierarchyGameObjectCount;
        int pendingRecoveryHierarchyGameObjectCount;
        int pendingDestroyInstanceCount;
        int pendingDestroyHierarchyGameObjectCount;
        int automaticEvictionFrame = -1;
        int automaticEvictionsThisFrame;
        long poolMissCount;
        long instantiateCount;
        long instantiateTicks;
        long trimCount;
        long trimEvictedInstances;

#if UNITY_EDITOR
        public Dictionary<int, Queue<ViewElement>> GetDicts()
        {
            return veDicts;
        }
        public Dictionary<int, string> veNameDicts = new Dictionary<int, string>();
        public Queue<ViewElement> GetRecycleQueue()
        {
            return recycleQueue;
        }
#endif
        [SerializeField]
        Dictionary<int, ViewElement> uniqueVeDicts = new Dictionary<int, ViewElement>();
        Queue<ViewElement> recycleQueue = new Queue<ViewElement>();
        public void QueueViewElementToRecovery(ViewElement toRecovery)
        {
            if (toRecovery == null || toRecovery.IsPermanentDestroyPrepared)
            {
                return;
            }

            toRecovery.rectTransform.SetParent(_hierachyPool.transformCache, true);

            if (toRecovery.IsUnique)
            {
                // unique ViewElement just needs to disable gameObject
                RecoveryViewElement(toRecovery);
            }
            else
            {
                if (IsGlobalQueued(toRecovery) || !EnqueuePendingRecovery(toRecovery))
                {
                    return;
                }
            }
        }
        public void RecoveryViewElement(ViewElement toRecovery)
        {
            // Debug.Log($"Recovery {toRecovery.name}");
            if (toRecovery == null || toRecovery.IsPermanentDestroyPrepared)
            {
                return;
            }

            if (toRecovery.IsUnique)
            {
                // unique ViewElement just needs to disable gameObject
                toRecovery.gameObject.SetActive(false);
            }
            else
            {
                if (pendingRecoverySet.Contains(toRecovery))
                {
                    // RecoveryQueuedViewElementRunner owns pending entries;
                    // never move one directly to the global queue while it is
                    // still present in recycleQueue.
                    return;
                }

                if (!veDicts.TryGetValue(toRecovery.PoolKey, out Queue<ViewElement> veQueue) ||
                    veQueue == null)
                {
                    ViewSystemLog.LogWarning("Cannot find pool of ViewElement " + toRecovery.name + ", Destroy directly.", toRecovery);
                    DestroyViewElementHierarchy(toRecovery);
                    return;
                }

                int nestedUniqueCount = toRecovery
                    .GetComponentsInChildren<ViewElement>(true)
                    .Count(x => x != toRecovery && x.IsUnique);
                bool ignoreRecoveryPolicy = toRecovery.IgnoreRecoveryPolicyOnce;
                toRecovery.IgnoreRecoveryPolicyOnce = false;
                bool destroyRequested = !ignoreRecoveryPolicy &&
                                        (toRecovery.recoveryPolicy == ViewElementRecoveryPolicy.DestroyOnRecovery ||
                                         (toRecovery.recoveryPolicy == ViewElementRecoveryPolicy.KeepN &&
                                          veQueue.Count >= Mathf.Max(0, toRecovery.recoveryKeepCount)));

                if (destroyRequested && nestedUniqueCount > 0)
                {
                    ViewSystemLog.LogWarning(
                        $"Skip destroying ViewElement {toRecovery.name}: hierarchy contains {nestedUniqueCount} nested unique ViewElement(s).",
                        toRecovery);
                    destroyRequested = false;
                }

                if (destroyRequested)
                {
                    toRecovery.gameObject.SetActive(false);
                    RemoveQueuedViewElement(toRecovery);
                    DestroyViewElementHierarchy(toRecovery);
                    return;
                }

                toRecovery.gameObject.SetActive(false);
                EnqueueGlobalQueuedViewElement(veQueue, toRecovery, ignoreRecoveryPolicy);
            }
        }

        internal static void DestroyViewElementHierarchy(ViewElement viewElement)
        {
            if (viewElement == null)
            {
                return;
            }

            ViewElementRuntimePool runtimePool = ViewController.runtimePool;
            if (runtimePool != null)
            {
                runtimePool.ScheduleDirectPermanentDestroy(viewElement);
                return;
            }

            viewElement.PrepareForPermanentDestroy();
            UnityEngine.Object.Destroy(viewElement.gameObject);
        }
        const int maxRecoveryPerFrame = 5;
        public Coroutine RecoveryQueuedViewElement(bool force = false)
        {
            return StartCoroutine(RecoveryQueuedViewElementRunner(force));
        }

        IEnumerator RecoveryQueuedViewElementRunner(bool force)
        {
            int max = force ? recycleQueue.Count : maxRecoveryPerFrame;
            while (recycleQueue.Count > 0)
            {
                for (int i = 0; i < max; i++)
                {
                    if (TryDequeuePendingRecovery(out var viewElement))
                    {
                        RecoveryViewElement(viewElement);
                    }
                }
                yield return null;
            }
        }
        public ViewElement PrewarmUniqueViewElement(ViewElement source)
        {
            if (source == null || source.IsPermanentDestroyPrepared || !source.IsUnique)
            {
                ViewSystemLog.LogWarning("The ViewElement trying to Prewarm is null, permanently destroying, or not unique");
                return null;
            }
            var i = source.GetInstanceID();
            if (!uniqueVeDicts.TryGetValue(source.GetInstanceID(), out var existing) ||
                existing == null || existing.IsPermanentDestroyPrepared || !existing.IsUnique)
            {
                uniqueVeDicts.Remove(source.GetInstanceID());
                var temp = InstantiateViewElement(source);
                temp.name = source.name;
                uniqueVeDicts.Add(i, temp);
                temp.gameObject.SetActive(false);
                return temp;
            }
            else
            {
                ViewSystemLog.LogWarning("ViewElement " + source.name + " has been prewarmed");
                return uniqueVeDicts[i];
            }
        }
        public ViewElement RequestViewElement(ViewElement source)
        {
            if (source == null || source.IsPermanentDestroyPrepared)
            {
                return null;
            }

            ViewElement result;

            if (source.IsUnique)
            {
                if (!uniqueVeDicts.TryGetValue(source.GetInstanceID(), out result) ||
                    result == null || result.IsPermanentDestroyPrepared || !result.IsUnique)
                {
                    uniqueVeDicts.Remove(source.GetInstanceID());
                    poolMissCount++;
                    result = InstantiateViewElement(source);
                    result.gameObject.SetActive(false);
                    result.name = source.name;
                    uniqueVeDicts.Add(source.GetInstanceID(), result);
                }
            }
            else
            {
                int sourceKey = source.GetInstanceID();
                Queue<ViewElement> veQueue = GetOrCreateSourceQueue(sourceKey, source);
                if (TryDequeueGlobalQueuedViewElement(sourceKey, veQueue, out result))
                {
                    // Debug.Log($"Request {source.name} from dequeue :  { Time.frameCount}");
                }
                else
                {
                    poolMissCount++;
                    result = InstantiateViewElement(source);
                    result.gameObject.SetActive(false);
                    result.name = source.name;
                    // Debug.Log($"Request {source.name} from generate new one : { Time.frameCount}");
                }
            }
            result.PoolKey = source.GetInstanceID();
            return result;
        }

        internal int GlobalQueuedInstanceCount => queuedInstanceCount;
        internal int GlobalQueuedHierarchyGameObjectCount => queuedHierarchyGameObjectCount;
        internal int GlobalQueuedIndexCount => globalQueuedIndex.Count;

        public ViewElementRuntimePoolSnapshot GetGlobalPoolSnapshot()
        {
            return GetGlobalPoolSnapshot(
                budgetOptions.Mode,
                refreshNestedUniqueSafety: false);
        }

        public ViewElementRuntimePoolSnapshot GetGlobalPoolSnapshot(bool refreshNestedUniqueSafety)
        {
            return GetGlobalPoolSnapshot(budgetOptions.Mode, refreshNestedUniqueSafety);
        }

        public ViewElementRuntimePoolSnapshot GetGlobalPoolSnapshot(
            ViewElementGlobalEvictionMode mode,
            bool refreshNestedUniqueSafety)
        {
            if (!Enum.IsDefined(typeof(ViewElementGlobalEvictionMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            RefreshPendingDestroyAccounting();

            var entries = new List<ViewElementRuntimePoolSnapshot.QueuedEntry>(globalQueuedIndex.Count);
            var sourceNames = new Dictionary<int, string>();
#if UNITY_EDITOR
            foreach (var sourceName in veNameDicts)
            {
                sourceNames[sourceName.Key] = sourceName.Value;
            }
#endif
            int eligibleQueuedInstances = 0;
            int blockedNestedUniqueInstances = 0;
            int blockedIneligibleInstances = 0;
            int blockedTooYoungInstances = 0;
            float now = Time.unscaledTime;
            foreach (QueuedEntry entry in globalQueuedIndex)
            {
                ViewElement viewElement = entry.viewElement;
                // Nested unique ownership can change while an instance is
                // queued. One-shot diagnostics can request a refresh so they
                // never advertise a hierarchy that trim would protect. The
                // default snapshot path keeps the cached value and does not
                // scan every hierarchy during inspector/runtime repaint.
                if (refreshNestedUniqueSafety)
                {
                    entry.nestedUniqueCount = CountNestedUniqueViewElements(viewElement);
                }
                if (!sourceNames.ContainsKey(entry.sourceKey))
                {
                    sourceNames[entry.sourceKey] = GetSourceName(entry.sourceKey, viewElement);
                }
                bool isNestedUnique = entry.nestedUniqueCount > 0;
                bool isActive = viewElement != null &&
                                (viewElement.IsShowed || viewElement.gameObject.activeSelf);
                bool isPending = viewElement != null && pendingRecoverySet.Contains(viewElement);
                bool isTooYoung = budgetOptions.MinimumIdleSeconds > 0 &&
                                  now - entry.queuedAt < budgetOptions.MinimumIdleSeconds;

                if (viewElement == null || viewElement.IsPermanentDestroyPrepared)
                {
                    // Stale entries are reported through the trim result when
                    // a trim is requested, but are not eligible here.
                }
                else if (isNestedUnique)
                {
                    blockedNestedUniqueInstances++;
                }
                else if (!IsGlobalEvictionAllowed(entry, mode) || isActive || isPending)
                {
                    blockedIneligibleInstances++;
                }
                else if (isTooYoung)
                {
                    blockedTooYoungInstances++;
                }
                else
                {
                    eligibleQueuedInstances++;
                }

                entries.Add(new ViewElementRuntimePoolSnapshot.QueuedEntry
                {
                    SourceKey = entry.sourceKey,
                    InstanceId = entry.instanceId,
                    HierarchyGameObjectCount = entry.hierarchyGameObjectCount,
                    NestedUniqueCount = entry.nestedUniqueCount,
                    QueuedAt = entry.queuedAt,
                    SourceName = GetSourceName(entry.sourceKey, viewElement),
                    RecoveryPolicy = viewElement != null
                        ? viewElement.recoveryPolicy
                        : ViewElementRecoveryPolicy.KeepForever,
                    RecoveryKeepCount = viewElement != null ? viewElement.recoveryKeepCount : 0,
                    ExplicitlyAllowsGlobalEviction = entry.explicitlyAllowsGlobalEviction,
                    IsEligible = viewElement != null &&
                                 IsGlobalEvictionAllowed(entry, mode) &&
                                 entry.nestedUniqueCount == 0 &&
                                 !viewElement.IsShowed &&
                                 !viewElement.gameObject.activeSelf &&
                                 !pendingRecoverySet.Contains(viewElement) &&
                                 !viewElement.IsPermanentDestroyPrepared &&
                                 (budgetOptions.MinimumIdleSeconds <= 0 ||
                                  now - entry.queuedAt >= budgetOptions.MinimumIdleSeconds),
                    IsActive = viewElement != null &&
                               (viewElement.IsShowed || viewElement.gameObject.activeSelf),
                    IsPendingRecovery = viewElement != null && pendingRecoverySet.Contains(viewElement),
                });
            }

            var pendingRecoveryInstanceIds = new List<int>(recycleQueue.Count);
            foreach (ViewElement viewElement in recycleQueue)
            {
                if (viewElement != null)
                {
                    pendingRecoveryInstanceIds.Add(viewElement.GetInstanceID());
                }
            }

            return new ViewElementRuntimePoolSnapshot
            {
                BudgetEnabled = budgetOptions.Enabled,
                BudgetMode = mode,
                HighWatermarkInstances = budgetOptions.HighWatermarkInstances,
                LowWatermarkInstances = budgetOptions.LowWatermarkInstances,
                HighWatermarkHierarchyGameObjects = budgetOptions.HighWatermarkHierarchyGameObjects,
                LowWatermarkHierarchyGameObjects = budgetOptions.LowWatermarkHierarchyGameObjects,
                MinimumIdleSeconds = budgetOptions.MinimumIdleSeconds,
                LogicalQueuedInstances = queuedInstanceCount,
                LogicalQueuedHierarchyGameObjects = queuedHierarchyGameObjectCount,
                EligibleQueuedInstances = eligibleQueuedInstances,
                BlockedNestedUniqueInstances = blockedNestedUniqueInstances,
                BlockedIneligibleInstances = blockedIneligibleInstances,
                BlockedTooYoungInstances = blockedTooYoungInstances,
                PendingRecoveryInstances = pendingRecoveryInstanceIds.Count,
                PendingRecoveryHierarchyGameObjects = pendingRecoveryHierarchyGameObjectCount,
                PendingDestroyInstances = pendingDestroyInstanceCount,
                PendingDestroyHierarchyGameObjects = pendingDestroyHierarchyGameObjectCount,
                PostFrameActualHierarchyGameObjects =
                    queuedHierarchyGameObjectCount + pendingRecoveryHierarchyGameObjectCount +
                    pendingDestroyHierarchyGameObjectCount,
                PoolMissCount = poolMissCount,
                InstantiateCount = instantiateCount,
                InstantiateMilliseconds = TicksToMilliseconds(instantiateTicks),
                TrimCount = trimCount,
                TrimEvictedInstances = trimEvictedInstances,
                LastBudgetSatisfied = lastTrimResult == null || lastTrimResult.BudgetSatisfied,
                LastRemainingOverBudgetInstances = lastTrimResult?.RemainingOverBudgetInstances ?? 0,
                LastRemainingOverBudgetHierarchyGameObjects =
                    lastTrimResult?.RemainingOverBudgetHierarchyGameObjects ?? 0,
                LastTrimResult = lastTrimResult == null ? null : CloneTrimResult(lastTrimResult),
                QueuedEntries = entries.AsReadOnly(),
                SourceKeys = veDicts.Keys.OrderBy(x => x).ToList().AsReadOnly(),
                SourceNames = new ReadOnlyDictionary<int, string>(sourceNames),
                PendingRecoveryInstanceIds = pendingRecoveryInstanceIds.AsReadOnly(),
            };
        }

        public ViewElementPoolTrimResult TrimQueuedPool(ViewElementPoolTrimRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ValidateTrimRequest(request);

            var result = new ViewElementPoolTrimResult
            {
                BeforeQueuedInstances = queuedInstanceCount,
                BeforeQueuedHierarchyGameObjects = queuedHierarchyGameObjectCount,
                AfterQueuedInstances = queuedInstanceCount,
                AfterQueuedHierarchyGameObjects = queuedHierarchyGameObjectCount,
                ProjectedAfterQueuedInstances = queuedInstanceCount,
                ProjectedAfterQueuedHierarchyGameObjects = queuedHierarchyGameObjectCount,
            };

            int projectedInstances = queuedInstanceCount;
            int projectedHierarchyGameObjects = queuedHierarchyGameObjectCount;
            int evictions = 0;
            float now = Time.unscaledTime;

            LinkedListNode<QueuedEntry> node = globalQueuedIndex.First;
            while (node != null)
            {
                LinkedListNode<QueuedEntry> next = node.Next;
                QueuedEntry entry = node.Value;

                bool hasTrimTarget = request.TargetQueuedInstances.HasValue ||
                                     request.TargetQueuedHierarchyGameObjects.HasValue;
                if ((!request.IdleOnly || hasTrimTarget) &&
                    !NeedsTrim(request, projectedInstances, projectedHierarchyGameObjects))
                {
                    break;
                }

                if (!IsTrackedQueuedEntryStillValid(entry))
                {
                    result.SkippedStale++;
                    if (request.DryRun)
                    {
                        // Dry-run must project the same bookkeeping removal
                        // as the real path, without mutating either queue.
                        projectedInstances--;
                        projectedHierarchyGameObjects -= entry.hierarchyGameObjectCount;
                    }
                    else
                    {
                        if (RemoveQueuedEntry(entry))
                        {
                            projectedInstances--;
                            projectedHierarchyGameObjects -= entry.hierarchyGameObjectCount;
                        }
                    }

                    node = next;
                    continue;
                }

                if (request.IdleOnly &&
                    now - entry.queuedAt < request.MinimumIdleSeconds)
                {
                    result.BlockedTooYoung++;
                    node = next;
                    continue;
                }

                int nestedUniqueCount = CountNestedUniqueViewElements(entry.viewElement);
                entry.nestedUniqueCount = nestedUniqueCount;
                if (nestedUniqueCount > 0)
                {
                    result.BlockedNestedUnique++;
                    node = next;
                    continue;
                }

                if (!IsGlobalEvictionAllowed(entry, request.Mode))
                {
                    result.BlockedIneligible++;
                    node = next;
                    continue;
                }

                if (entry.viewElement.IsShowed || entry.viewElement.gameObject.activeSelf ||
                    pendingRecoverySet.Contains(entry.viewElement))
                {
                    result.BlockedIneligible++;
                    node = next;
                    continue;
                }

                if (!request.IdleOnly &&
                    now - entry.queuedAt < request.MinimumIdleSeconds)
                {
                    result.BlockedTooYoung++;
                    node = next;
                    continue;
                }

                if (evictions >= request.MaxEvictions)
                {
                    break;
                }

                if (request.DryRun)
                {
                    projectedInstances--;
                    projectedHierarchyGameObjects -= entry.hierarchyGameObjectCount;
                    evictions++;
                    result.EvictedInstances++;
                    result.EvictedHierarchyGameObjects += entry.hierarchyGameObjectCount;
                }
                else if (RemoveQueuedEntry(entry))
                {
                    projectedInstances--;
                    projectedHierarchyGameObjects -= entry.hierarchyGameObjectCount;
                    evictions++;
                    result.EvictedInstances++;
                    result.EvictedHierarchyGameObjects += entry.hierarchyGameObjectCount;
                    entry.viewElement.gameObject.SetActive(false);
                    SchedulePermanentDestroy(entry);
                }

                node = next;
            }

            result.AfterQueuedInstances = queuedInstanceCount;
            result.AfterQueuedHierarchyGameObjects = queuedHierarchyGameObjectCount;
            result.ProjectedAfterQueuedInstances = projectedInstances;
            result.ProjectedAfterQueuedHierarchyGameObjects = projectedHierarchyGameObjects;
            int resultingInstances = request.DryRun ? projectedInstances : queuedInstanceCount;
            int resultingHierarchyGameObjects = request.DryRun
                ? projectedHierarchyGameObjects
                : queuedHierarchyGameObjectCount;
            result.RemainingOverBudgetInstances = GetRemainingOverBudget(
                resultingInstances,
                request.TargetQueuedInstances);
            result.RemainingOverBudgetHierarchyGameObjects = GetRemainingOverBudget(
                resultingHierarchyGameObjects,
                request.TargetQueuedHierarchyGameObjects);
            result.BudgetSatisfied =
                result.RemainingOverBudgetInstances == 0 &&
                result.RemainingOverBudgetHierarchyGameObjects == 0;

            lastTrimResult = CloneTrimResult(result);
            trimCount++;
            if (!request.DryRun)
            {
                trimEvictedInstances += result.EvictedInstances;
            }

            ValidateBookkeeping();
            return result;
        }

        static void ValidateTrimRequest(ViewElementPoolTrimRequest request)
        {
            if (!Enum.IsDefined(typeof(ViewElementGlobalEvictionMode), request.Mode))
            {
                throw new ArgumentOutOfRangeException(nameof(request.Mode));
            }

            if (request.TargetQueuedInstances < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.TargetQueuedInstances));
            }

            if (request.TargetQueuedHierarchyGameObjects < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.TargetQueuedHierarchyGameObjects));
            }

            if (request.MaxEvictions <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.MaxEvictions));
            }

            if (float.IsNaN(request.MinimumIdleSeconds) ||
                float.IsInfinity(request.MinimumIdleSeconds) ||
                request.MinimumIdleSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.MinimumIdleSeconds));
            }
        }

        static void ValidateBudgetOptions(ViewElementGlobalPoolBudgetOptions options)
        {
            if (!Enum.IsDefined(typeof(ViewElementGlobalEvictionMode), options.Mode))
            {
                throw new ArgumentOutOfRangeException(nameof(options.Mode));
            }

            ValidateWatermarks(
                options.HighWatermarkInstances,
                options.LowWatermarkInstances,
                nameof(options.HighWatermarkInstances),
                nameof(options.LowWatermarkInstances));
            ValidateWatermarks(
                options.HighWatermarkHierarchyGameObjects,
                options.LowWatermarkHierarchyGameObjects,
                nameof(options.HighWatermarkHierarchyGameObjects),
                nameof(options.LowWatermarkHierarchyGameObjects));

            if (options.MaxEvictionsPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxEvictionsPerFrame));
            }

            if (float.IsNaN(options.MinimumIdleSeconds) ||
                float.IsInfinity(options.MinimumIdleSeconds) ||
                options.MinimumIdleSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MinimumIdleSeconds));
            }
        }

        static void ValidateWatermarks(
            int highWatermark,
            int lowWatermark,
            string highName,
            string lowName)
        {
            if (highWatermark < 0)
            {
                throw new ArgumentOutOfRangeException(highName);
            }

            if (lowWatermark < 0 || lowWatermark > highWatermark)
            {
                throw new ArgumentOutOfRangeException(lowName);
            }

            if (highWatermark == 0 && lowWatermark != 0)
            {
                throw new ArgumentException(
                    $"{lowName} must be 0 when {highName} is 0 (the dimension is disabled).");
            }
        }

        bool IsOverHighWatermark()
        {
            return (budgetOptions.HighWatermarkInstances > 0 &&
                    queuedInstanceCount > budgetOptions.HighWatermarkInstances) ||
                   (budgetOptions.HighWatermarkHierarchyGameObjects > 0 &&
                    queuedHierarchyGameObjectCount > budgetOptions.HighWatermarkHierarchyGameObjects);
        }

        void ScheduleBudgetEvictionIfNeeded()
        {
            if (budgetOptions.Enabled && IsOverHighWatermark())
            {
                ScheduleBudgetEviction();
            }
        }

        void ScheduleBudgetEviction(
            bool force = false,
            ViewElementGlobalEvictionMode? modeOverride = null)
        {
            if (!budgetOptions.Enabled || !isActiveAndEnabled || budgetEvictionRunner != null)
            {
                if (force && budgetEvictionRunner != null)
                {
                    budgetEvictionForceRequested = true;
                    if (modeOverride.HasValue)
                    {
                        budgetEvictionMode = modeOverride.Value;
                    }
                }

                return;
            }

            budgetEvictionForceRequested = force;
            budgetEvictionMode = modeOverride ?? budgetOptions.Mode;
            int configurationVersion = budgetConfigurationVersion;
            budgetEvictionRunner = StartCoroutine(BudgetEvictionRunner(configurationVersion, force));
        }

        IEnumerator BudgetEvictionRunner(int configurationVersion, bool force)
        {
            // Let the recovery/request call stack settle before destroying any
            // queued hierarchy. This also makes repeated schedules coalesce.
            yield return null;

            bool trimActive = force;
            bool forceTrim = force;
            while (configurationVersion == budgetConfigurationVersion &&
                   budgetOptions.Enabled)
            {
                if (budgetEvictionForceRequested)
                {
                    trimActive = true;
                    forceTrim = true;
                    budgetEvictionForceRequested = false;
                }

                trimActive |= IsOverHighWatermark();
                if (!trimActive)
                {
                    break;
                }

                int automaticEvictionBudget = GetAutomaticEvictionBudget();
                if (automaticEvictionBudget <= 0)
                {
                    yield return null;
                    continue;
                }

                ViewElementPoolTrimResult result = TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = budgetEvictionMode,
                    TargetQueuedInstances = budgetOptions.HighWatermarkInstances > 0
                        ? budgetOptions.LowWatermarkInstances
                        : forceTrim
                            ? 0
                            : (int?)null,
                    TargetQueuedHierarchyGameObjects = budgetOptions.HighWatermarkHierarchyGameObjects > 0
                        ? budgetOptions.LowWatermarkHierarchyGameObjects
                        : forceTrim
                            ? 0
                            : (int?)null,
                    MaxEvictions = automaticEvictionBudget,
                    MinimumIdleSeconds = budgetOptions.MinimumIdleSeconds,
                });
                RecordAutomaticEvictions(result.EvictedInstances);

                if (result.BudgetSatisfied || result.EvictedInstances == 0)
                {
                    break;
                }

                yield return null;
            }

            if (configurationVersion == budgetConfigurationVersion)
            {
                budgetEvictionRunner = null;
            }
        }

        void StopBudgetEvictionRunner()
        {
            if (budgetEvictionRunner == null)
            {
                budgetEvictionForceRequested = false;
                return;
            }

            StopCoroutine(budgetEvictionRunner);
            budgetEvictionRunner = null;
            budgetEvictionForceRequested = false;
        }

        void OnLowMemory()
        {
            if (!budgetOptions.Enabled || !isActiveAndEnabled)
            {
                return;
            }

            ViewElementGlobalEvictionMode lowMemoryMode = budgetOptions.Mode;
            if (lowMemoryMode == ViewElementGlobalEvictionMode.EmergencyHardCap &&
                !budgetOptions.AllowEmergencyHardCapOnLowMemory)
            {
                lowMemoryMode = ViewElementGlobalEvictionMode.NormalBudget;
            }

            // Application.lowMemory is a notification only. Destruction is
            // deferred to the existing frame-budgeted runner.
            ScheduleBudgetEviction(force: true, modeOverride: lowMemoryMode);
        }

        void ScheduleIdleEviction()
        {
            if (!budgetOptions.Enabled || budgetOptions.MinimumIdleSeconds <= 0 ||
                !isActiveAndEnabled || idleEvictionRunner != null)
            {
                return;
            }

            int configurationVersion = budgetConfigurationVersion;
            idleEvictionRunner = StartCoroutine(IdleEvictionRunner(configurationVersion));
        }

        IEnumerator IdleEvictionRunner(int configurationVersion)
        {
            while (configurationVersion == budgetConfigurationVersion &&
                   budgetOptions.Enabled && budgetOptions.MinimumIdleSeconds > 0)
            {
                // Idle eviction is deliberately low frequency. The wait also
                // prevents a blocked/too-young pool from becoming a busy loop.
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(1f, Mathf.Min(10f, budgetOptions.MinimumIdleSeconds)));

                if (configurationVersion != budgetConfigurationVersion ||
                    !budgetOptions.Enabled || budgetOptions.MinimumIdleSeconds <= 0)
                {
                    break;
                }

                int automaticEvictionBudget = GetAutomaticEvictionBudget();
                if (automaticEvictionBudget <= 0)
                {
                    yield return null;
                    continue;
                }

                ViewElementPoolTrimResult result = TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = budgetOptions.Mode,
                    MaxEvictions = automaticEvictionBudget,
                    MinimumIdleSeconds = budgetOptions.MinimumIdleSeconds,
                    IdleOnly = true,
                });
                // The idle result is intentionally not needed for control flow,
                // but it still consumes the same global per-frame allowance.
                RecordAutomaticEvictions(result.EvictedInstances);
            }

            if (configurationVersion == budgetConfigurationVersion)
            {
                idleEvictionRunner = null;
            }
        }

        void StopIdleEvictionRunner()
        {
            if (idleEvictionRunner == null)
            {
                return;
            }

            StopCoroutine(idleEvictionRunner);
            idleEvictionRunner = null;
        }

        int GetAutomaticEvictionBudget()
        {
            if (automaticEvictionFrame != Time.frameCount)
            {
                automaticEvictionFrame = Time.frameCount;
                automaticEvictionsThisFrame = 0;
            }

            return Mathf.Max(0, budgetOptions.MaxEvictionsPerFrame - automaticEvictionsThisFrame);
        }

        void RecordAutomaticEvictions(int evictedInstances)
        {
            if (automaticEvictionFrame != Time.frameCount)
            {
                automaticEvictionFrame = Time.frameCount;
                automaticEvictionsThisFrame = 0;
            }

            automaticEvictionsThisFrame += Mathf.Max(0, evictedInstances);
        }

        static bool NeedsTrim(
            ViewElementPoolTrimRequest request,
            int queuedInstances,
            int queuedHierarchyGameObjects)
        {
            return (request.TargetQueuedInstances.HasValue &&
                    queuedInstances > request.TargetQueuedInstances.Value) ||
                   (request.TargetQueuedHierarchyGameObjects.HasValue &&
                    queuedHierarchyGameObjects > request.TargetQueuedHierarchyGameObjects.Value);
        }

        bool IsTrackedQueuedEntryStillValid(QueuedEntry entry)
        {
            if (entry == null || entry.viewElement == null ||
                entry.viewElement.IsPermanentDestroyPrepared ||
                entry.viewElement.IsUnique ||
                entry.viewElement.PoolKey != entry.sourceKey ||
                pendingRecoverySet.Contains(entry.viewElement))
            {
                return false;
            }

            if (!veDicts.TryGetValue(entry.sourceKey, out var sourceQueue) ||
                sourceQueue == null ||
                !QueueContainsReference(sourceQueue, entry.viewElement))
            {
                return false;
            }

            return globalQueuedNodesByInstanceId.TryGetValue(entry.instanceId, out var node) &&
                   ReferenceEquals(node.Value, entry);
        }

        static int CountNestedUniqueViewElements(ViewElement viewElement)
        {
            if (viewElement == null)
            {
                return 0;
            }

            return viewElement
                .GetComponentsInChildren<ViewElement>(true)
                .Count(x => x != viewElement && x.IsUnique);
        }

        static bool IsGlobalEvictionAllowed(
            QueuedEntry entry,
            ViewElementGlobalEvictionMode mode)
        {
            if (entry == null || entry.viewElement == null)
            {
                return false;
            }

            return mode == ViewElementGlobalEvictionMode.EmergencyHardCap ||
                   entry.explicitlyAllowsGlobalEviction ||
                   entry.viewElement.recoveryPolicy != ViewElementRecoveryPolicy.KeepForever;
        }

        static int GetRemainingOverBudget(int value, int? target)
        {
            return target.HasValue ? Mathf.Max(0, value - target.Value) : 0;
        }

        ViewElement InstantiateViewElement(ViewElement source)
        {
            long startTicks = Stopwatch.GetTimestamp();
            ViewElement result = UnityEngine.Object.Instantiate(source, _hierachyPool.rectTransform);
            instantiateTicks += Stopwatch.GetTimestamp() - startTicks;
            instantiateCount++;
            return result;
        }

        void SchedulePermanentDestroy(QueuedEntry entry)
        {
            if (entry == null || entry.viewElement == null)
            {
                return;
            }

            if (pendingDestroyEntriesByViewElement.ContainsKey(entry.viewElement))
            {
                return;
            }

            if (pendingDestroyEntries.Add(entry))
            {
                pendingDestroyEntriesByViewElement.Add(entry.viewElement, entry);
                pendingDestroyInstanceCount++;
                pendingDestroyHierarchyGameObjectCount += entry.hierarchyGameObjectCount;
            }

            entry.viewElement.PrepareForPermanentDestroy();
            UnityEngine.Object.Destroy(entry.viewElement.gameObject);
        }

        void RefreshPendingDestroyAccounting()
        {
            if (pendingDestroyEntries.Count == 0)
            {
                return;
            }

            var completedEntries = new List<QueuedEntry>();
            foreach (QueuedEntry entry in pendingDestroyEntries)
            {
                if (entry.viewElement == null)
                {
                    completedEntries.Add(entry);
                }
            }

            foreach (QueuedEntry entry in completedEntries)
            {
                if (!pendingDestroyEntries.Remove(entry))
                {
                    continue;
                }

                if (pendingDestroyEntriesByViewElement.TryGetValue(entry.viewElement, out var mappedEntry) &&
                    ReferenceEquals(mappedEntry, entry))
                {
                    pendingDestroyEntriesByViewElement.Remove(entry.viewElement);
                }

                pendingDestroyInstanceCount--;
                pendingDestroyHierarchyGameObjectCount -= entry.hierarchyGameObjectCount;
            }
        }

        void ScheduleDirectPermanentDestroy(ViewElement viewElement)
        {
            if (viewElement == null || pendingDestroyEntriesByViewElement.ContainsKey(viewElement))
            {
                return;
            }

            RemovePendingRecovery(viewElement);

            if (globalQueuedNodesByInstanceId.TryGetValue(viewElement.GetInstanceID(), out var node) &&
                ReferenceEquals(node.Value.viewElement, viewElement))
            {
                RemoveQueuedEntry(node.Value);
            }

            SchedulePermanentDestroy(new QueuedEntry(viewElement, viewElement.PoolKey, false));
        }

        void RemovePendingRecovery(ViewElement viewElement)
        {
            if (viewElement == null || !pendingRecoverySet.Remove(viewElement))
            {
                return;
            }

            var remaining = new Queue<ViewElement>(recycleQueue.Count);
            while (recycleQueue.Count > 0)
            {
                ViewElement candidate = recycleQueue.Dequeue();
                if (!ReferenceEquals(candidate, viewElement))
                {
                    remaining.Enqueue(candidate);
                }
            }

            while (remaining.Count > 0)
            {
                recycleQueue.Enqueue(remaining.Dequeue());
            }

            RemovePendingRecoveryHierarchyCost(viewElement);
            ValidateBookkeeping();
        }

        string GetSourceName(int sourceKey, ViewElement viewElement)
        {
#if UNITY_EDITOR
            if (veNameDicts.TryGetValue(sourceKey, out var sourceName) &&
                !string.IsNullOrEmpty(sourceName))
            {
                return sourceName;
            }
#endif
            return viewElement != null ? viewElement.name : $"PoolKey_{sourceKey}";
        }

        static double TicksToMilliseconds(long ticks)
        {
            return ticks * 1000d / Stopwatch.Frequency;
        }

        static ViewElementPoolTrimResult CloneTrimResult(ViewElementPoolTrimResult result)
        {
            return new ViewElementPoolTrimResult
            {
                BeforeQueuedInstances = result.BeforeQueuedInstances,
                AfterQueuedInstances = result.AfterQueuedInstances,
                BeforeQueuedHierarchyGameObjects = result.BeforeQueuedHierarchyGameObjects,
                AfterQueuedHierarchyGameObjects = result.AfterQueuedHierarchyGameObjects,
                ProjectedAfterQueuedInstances = result.ProjectedAfterQueuedInstances,
                ProjectedAfterQueuedHierarchyGameObjects = result.ProjectedAfterQueuedHierarchyGameObjects,
                EvictedInstances = result.EvictedInstances,
                EvictedHierarchyGameObjects = result.EvictedHierarchyGameObjects,
                BlockedNestedUnique = result.BlockedNestedUnique,
                BlockedIneligible = result.BlockedIneligible,
                BlockedTooYoung = result.BlockedTooYoung,
                SkippedStale = result.SkippedStale,
                BudgetSatisfied = result.BudgetSatisfied,
                RemainingOverBudgetInstances = result.RemainingOverBudgetInstances,
                RemainingOverBudgetHierarchyGameObjects = result.RemainingOverBudgetHierarchyGameObjects,
            };
        }

        internal bool TryValidateGlobalQueueConsistency(out string error)
        {
            var queueInstances = new HashSet<ViewElement>(new ReferenceComparer<ViewElement>());
            int expectedInstanceCount = 0;
            int expectedHierarchyGameObjectCount = 0;

            foreach (var pair in veDicts)
            {
                if (pair.Value == null)
                {
                    error = $"Source queue {pair.Key} is null.";
                    return false;
                }

                foreach (ViewElement viewElement in pair.Value)
                {
                    if (viewElement == null)
                    {
                        error = $"Source queue {pair.Key} contains a null entry.";
                        return false;
                    }

                    if (viewElement.IsUnique || viewElement.IsPermanentDestroyPrepared)
                    {
                        error = $"Source queue {pair.Key} contains an invalid ViewElement {viewElement.name}.";
                        return false;
                    }

                    if (viewElement.PoolKey != pair.Key)
                    {
                        error = $"ViewElement {viewElement.name} has PoolKey {viewElement.PoolKey}, expected {pair.Key}.";
                        return false;
                    }

                    if (!queueInstances.Add(viewElement))
                    {
                        error = $"ViewElement {viewElement.name} occurs in more than one source queue.";
                        return false;
                    }

                    if (!globalQueuedNodesByInstanceId.TryGetValue(viewElement.GetInstanceID(), out var node) ||
                        !ReferenceEquals(node.Value.viewElement, viewElement))
                    {
                        error = $"ViewElement {viewElement.name} is missing from the global queue index.";
                        return false;
                    }

                    expectedInstanceCount++;
                    expectedHierarchyGameObjectCount += node.Value.hierarchyGameObjectCount;
                }
            }

            foreach (var node in globalQueuedIndex)
            {
                if (!globalQueuedNodesByInstanceId.TryGetValue(node.instanceId, out var mappedNode) ||
                    !ReferenceEquals(mappedNode.Value, node))
                {
                    error = $"Global queue index mapping is missing for instance {node.instanceId}.";
                    return false;
                }

                if (node.viewElement == null || !queueInstances.Contains(node.viewElement))
                {
                    error = $"Global queue index contains an entry that is absent from its source queue.";
                    return false;
                }
            }

            if (expectedInstanceCount != queuedInstanceCount ||
                expectedHierarchyGameObjectCount != queuedHierarchyGameObjectCount ||
                expectedInstanceCount != globalQueuedIndex.Count ||
                globalQueuedIndex.Count != globalQueuedNodesByInstanceId.Count)
            {
                error =
                    $"Global queue counters disagree: queues={expectedInstanceCount}/{expectedHierarchyGameObjectCount}, " +
                    $"counters={queuedInstanceCount}/{queuedHierarchyGameObjectCount}, index={globalQueuedIndex.Count}.";
                return false;
            }

            foreach (ViewElement pending in pendingRecoverySet)
            {
                if (!QueueContainsReference(recycleQueue, pending))
                {
                    error = $"Pending recovery set contains {pending?.name ?? "<null>"} without a queue entry.";
                    return false;
                }
            }

            foreach (ViewElement pending in pendingRecoverySet)
            {
                if (!pendingRecoveryHierarchyCosts.ContainsKey(pending))
                {
                    error = "Pending recovery hierarchy cost is missing.";
                    return false;
                }
            }

            foreach (ViewElement pending in recycleQueue)
            {
                if (pending == null || !pendingRecoverySet.Contains(pending))
                {
                    error = "Recovery queue and pending recovery set disagree.";
                    return false;
                }
            }

            if (pendingRecoveryHierarchyCosts.Count != pendingRecoverySet.Count ||
                pendingRecoveryHierarchyCosts.Values.Sum() != pendingRecoveryHierarchyGameObjectCount)
            {
                error =
                    "Pending recovery hierarchy counters disagree: " +
                    $"costs={pendingRecoveryHierarchyCosts.Count}/{pendingRecoveryHierarchyCosts.Values.Sum()}, " +
                    $"set={pendingRecoverySet.Count}/{pendingRecoveryHierarchyGameObjectCount}.";
                return false;
            }

            if (pendingDestroyEntries.Count != pendingDestroyEntriesByViewElement.Count ||
                pendingDestroyInstanceCount != pendingDestroyEntries.Count ||
                pendingDestroyHierarchyGameObjectCount != pendingDestroyEntries
                    .Where(entry => entry != null)
                    .Sum(entry => entry.hierarchyGameObjectCount))
            {
                error =
                    "Pending destroy counters disagree: " +
                    $"entries={pendingDestroyEntries.Count}/{pendingDestroyEntriesByViewElement.Count}, " +
                    $"counters={pendingDestroyInstanceCount}/{pendingDestroyHierarchyGameObjectCount}.";
                return false;
            }

            foreach (QueuedEntry pendingDestroy in pendingDestroyEntries)
            {
                if (pendingDestroy == null ||
                    !pendingDestroyEntriesByViewElement.TryGetValue(
                        pendingDestroy.viewElement,
                        out var mappedEntry) ||
                    !ReferenceEquals(mappedEntry, pendingDestroy))
                {
                    error = "Pending destroy entry mapping is missing.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        bool IsGlobalQueued(ViewElement viewElement)
        {
            if (viewElement == null || !globalQueuedNodesByInstanceId.TryGetValue(viewElement.GetInstanceID(), out var node))
            {
                return false;
            }

            return ReferenceEquals(node.Value.viewElement, viewElement);
        }

        Queue<ViewElement> GetOrCreateSourceQueue(int sourceKey, ViewElement source)
        {
            if (!veDicts.TryGetValue(sourceKey, out var queue) || queue == null)
            {
                queue = new Queue<ViewElement>();
                veDicts[sourceKey] = queue;
            }

#if UNITY_EDITOR
            if (!veNameDicts.ContainsKey(sourceKey))
            {
                veNameDicts.Add(sourceKey, source != null ? source.name : string.Empty);
            }
#endif
            return queue;
        }

        bool TryDequeuePendingRecovery(out ViewElement viewElement)
        {
            while (recycleQueue.Count > 0)
            {
                viewElement = recycleQueue.Dequeue();
                pendingRecoverySet.Remove(viewElement);
                RemovePendingRecoveryHierarchyCost(viewElement);
                if (viewElement != null)
                {
                    ValidateBookkeeping();
                    return true;
                }
            }

            viewElement = null;
            ValidateBookkeeping();
            return false;
        }

        bool EnqueuePendingRecovery(ViewElement viewElement)
        {
            if (viewElement == null || viewElement.IsPermanentDestroyPrepared ||
                !pendingRecoverySet.Add(viewElement))
            {
                return false;
            }

            recycleQueue.Enqueue(viewElement);
            int hierarchyGameObjectCount = viewElement
                .GetComponentsInChildren<Transform>(true)
                .Length;
            pendingRecoveryHierarchyCosts.Add(viewElement, hierarchyGameObjectCount);
            pendingRecoveryHierarchyGameObjectCount += hierarchyGameObjectCount;
            ValidateBookkeeping();
            return true;
        }

        void RemovePendingRecoveryHierarchyCost(ViewElement viewElement)
        {
            // A Unity object that has already been destroyed compares equal to
            // null, but it is still the reference used as the dictionary key
            // until the pending recovery entry is removed. Use CLR reference
            // null semantics here so stale destroyed entries cannot leak the
            // cached hierarchy cost or its counter.
            if (ReferenceEquals(viewElement, null))
            {
                return;
            }

            if (!pendingRecoveryHierarchyCosts.TryGetValue(viewElement, out int hierarchyGameObjectCount))
            {
                return;
            }

            pendingRecoveryHierarchyCosts.Remove(viewElement);
            pendingRecoveryHierarchyGameObjectCount -= hierarchyGameObjectCount;
        }

        bool TryDequeueGlobalQueuedViewElement(
            int sourceKey,
            Queue<ViewElement> sourceQueue,
            out ViewElement viewElement)
        {
            while (sourceQueue.Count > 0)
            {
                viewElement = sourceQueue.Dequeue();
                bool wasTracked = RemoveGlobalQueueIndex(viewElement);

                if (!wasTracked || viewElement == null ||
                    viewElement.IsPermanentDestroyPrepared ||
                    viewElement.IsUnique ||
                    viewElement.PoolKey != sourceKey ||
                    pendingRecoverySet.Contains(viewElement))
                {
                    continue;
                }

                ValidateBookkeeping();
                return true;
            }

            viewElement = null;
            ValidateBookkeeping();
            return false;
        }

        void EnqueueGlobalQueuedViewElement(
            Queue<ViewElement> sourceQueue,
            ViewElement viewElement,
            bool explicitlyAllowsGlobalEviction = false)
        {
            if (viewElement == null || viewElement.IsPermanentDestroyPrepared || viewElement.IsUnique)
            {
                return;
            }

            if (QueueContainsReference(sourceQueue, viewElement))
            {
                TrackGlobalQueuedViewElement(viewElement, explicitlyAllowsGlobalEviction);
                ValidateBookkeeping();
                return;
            }

            sourceQueue.Enqueue(viewElement);
            TrackGlobalQueuedViewElement(viewElement, explicitlyAllowsGlobalEviction);
            ValidateBookkeeping();
        }

        void TrackGlobalQueuedViewElement(
            ViewElement viewElement,
            bool explicitlyAllowsGlobalEviction = false)
        {
            if (viewElement == null || viewElement.IsPermanentDestroyPrepared || viewElement.IsUnique)
            {
                return;
            }

            int instanceId = viewElement.GetInstanceID();
            if (globalQueuedNodesByInstanceId.TryGetValue(instanceId, out var existingNode))
            {
                if (ReferenceEquals(existingNode.Value.viewElement, viewElement))
                {
                    existingNode.Value.explicitlyAllowsGlobalEviction |= explicitlyAllowsGlobalEviction;
                    return;
                }

                RemoveSourceQueueReference(existingNode.Value.sourceKey, existingNode.Value.viewElement);
                RemoveGlobalQueueIndex(existingNode);
            }

            var entry = new QueuedEntry(
                viewElement,
                viewElement.PoolKey,
                explicitlyAllowsGlobalEviction);
            var node = globalQueuedIndex.AddLast(entry);
            globalQueuedNodesByInstanceId[entry.instanceId] = node;
            queuedInstanceCount++;
            queuedHierarchyGameObjectCount += entry.hierarchyGameObjectCount;
            ScheduleBudgetEvictionIfNeeded();
        }

        bool RemoveQueuedViewElement(ViewElement viewElement)
        {
            if (viewElement == null ||
                !globalQueuedNodesByInstanceId.TryGetValue(viewElement.GetInstanceID(), out var node) ||
                !ReferenceEquals(node.Value.viewElement, viewElement))
            {
                return false;
            }

            return RemoveQueuedEntry(node.Value);
        }

        bool RemoveQueuedEntry(QueuedEntry entry)
        {
            if (entry == null ||
                !globalQueuedNodesByInstanceId.TryGetValue(entry.instanceId, out var node) ||
                !ReferenceEquals(node.Value, entry))
            {
                return false;
            }

            // A queued instance must disappear from its source FIFO before the
            // global index and logical counters are changed. Permanent destroy
            // callers rely on this ordering to avoid same-frame re-requests.
            RemoveSourceQueueReference(entry.sourceKey, entry.viewElement);
            RemoveGlobalQueueIndex(node);
            ValidateBookkeeping();
            return true;
        }

        void RemoveSourceQueueReference(int sourceKey, ViewElement viewElement)
        {
            if (!veDicts.TryGetValue(sourceKey, out var sourceQueue) || sourceQueue == null)
            {
                return;
            }

            var remaining = new Queue<ViewElement>(sourceQueue.Count);
            while (sourceQueue.Count > 0)
            {
                var candidate = sourceQueue.Dequeue();
                if (!ReferenceEquals(candidate, viewElement))
                {
                    remaining.Enqueue(candidate);
                }
            }

            while (remaining.Count > 0)
            {
                sourceQueue.Enqueue(remaining.Dequeue());
            }
        }

        bool RemoveGlobalQueueIndex(ViewElement viewElement)
        {
            if (ReferenceEquals(viewElement, null) ||
                !globalQueuedNodesByInstanceId.TryGetValue(viewElement.GetInstanceID(), out var node) ||
                !ReferenceEquals(node.Value.viewElement, viewElement))
            {
                return false;
            }

            RemoveGlobalQueueIndex(node);
            return true;
        }

        void RemoveGlobalQueueIndex(LinkedListNode<QueuedEntry> node)
        {
            globalQueuedIndex.Remove(node);
            globalQueuedNodesByInstanceId.Remove(node.Value.instanceId);
            queuedInstanceCount--;
            queuedHierarchyGameObjectCount -= node.Value.hierarchyGameObjectCount;
        }

        static bool QueueContainsReference(Queue<ViewElement> queue, ViewElement viewElement)
        {
            foreach (ViewElement candidate in queue)
            {
                if (ReferenceEquals(candidate, viewElement))
                {
                    return true;
                }
            }

            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void ValidateBookkeeping()
        {
            if (!TryValidateGlobalQueueConsistency(out var error))
            {
                ViewSystemLog.LogError($"ViewElementRuntimePool bookkeeping invariant failed: {error}");
            }
        }
    }
}
