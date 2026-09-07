#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Development-only compatibility checks for the RuntimePool bookkeeping and trim contract.
    /// The test creates temporary inactive ViewElements and never edits scene or prefab assets.
    /// </summary>
    public static class ViewElementRuntimePoolSelfTest
    {
        const string MenuPath = "MacacaGames/ViewSystem/Diagnostics/Runtime Pool/Run In-Editor Self-Test";
        static bool isRunning;

        [MenuItem(MenuPath)]
        static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("RuntimePool self-test requires Play Mode.");
                return;
            }

            ViewElementRuntimePool runtimePool = ViewController.runtimePool;
            if (runtimePool == null)
            {
                Debug.LogWarning("No active ViewElementRuntimePool was found. Enter Play Mode first.");
                return;
            }

            if (isRunning)
            {
                Debug.LogWarning("RuntimePool self-test is already running.");
                return;
            }

            ViewElementRuntimePoolSnapshot baseline = runtimePool.GetGlobalPoolSnapshot();
            if (baseline.LogicalQueuedInstances != 0 ||
                baseline.PendingRecoveryInstances != 0 ||
                baseline.PendingDestroyInstances != 0)
            {
                Debug.LogWarning(
                    "RuntimePool self-test requires an empty pool boundary; " +
                    $"queued={baseline.LogicalQueuedInstances}, " +
                    $"pendingRecovery={baseline.PendingRecoveryInstances}, " +
                    $"pendingDestroy={baseline.PendingDestroyInstances}.");
                return;
            }

            isRunning = true;
            runtimePool.StartCoroutine(RunCoroutine(runtimePool));
        }

        static IEnumerator RunCoroutine(ViewElementRuntimePool runtimePool)
        {
            var context = new TestContext(runtimePool);
            IEnumerator test = context.Run();
            while (true)
            {
                object current;
                try
                {
                    if (!test.MoveNext())
                    {
                        break;
                    }

                    current = test.Current;
                }
                catch (Exception exception)
                {
                    context.Fail("Unhandled self-test exception: " + exception);
                    break;
                }

                yield return current;
            }

            yield return context.Cleanup();
            Debug.Log(
                $"[RuntimePoolSelfTest] {(context.FailureCount == 0 ? "PASS" : "FAIL")} " +
                $"checks={context.CheckCount}, failures={context.FailureCount}.",
                runtimePool);
            isRunning = false;
        }

        sealed class TestContext
        {
            readonly ViewElementRuntimePool runtimePool;
            readonly List<ViewElement> sources = new List<ViewElement>();
            readonly List<ViewElement> instances = new List<ViewElement>();
            readonly List<int> uniqueSourceKeys = new List<int>();
            GameObject testRoot;
            int checkCount;
            int failureCount;

            public int CheckCount => checkCount;
            public int FailureCount => failureCount;

            public TestContext(ViewElementRuntimePool runtimePool)
            {
                this.runtimePool = runtimePool;
            }

            public IEnumerator Run()
            {
                testRoot = new GameObject("__RuntimePoolSelfTest__");
                testRoot.SetActive(false);

                yield return TestDuplicateRecoveryAndFifo();
                yield return TestNormalAndEmergencyModes();
                yield return TestHierarchyBudgetAndStaleRequest();
                yield return TestProtectedEntries();
                yield return TestNullAndPermanentDestroyEntries();
                yield return TestPendingRecoveryPermanentDestroy();
                yield return TestRequestedPoolOwnershipModes();
                yield return TestWatermarkAndReconfigureValidation();
                yield return TestAutomaticBudgetAndIdleMinimum();
                yield return TestLowMemoryScheduling();
            }

            IEnumerator TestDuplicateRecoveryAndFifo()
            {
                ViewElement source = CreateSource("DuplicateRecovery", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElement first = Request(source);
                runtimePool.QueueViewElementToRecovery(first);
                runtimePool.QueueViewElementToRecovery(first);
                yield return runtimePool.RecoveryQueuedViewElement(true);

                ViewElementRuntimePoolSnapshot snapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(snapshot.LogicalQueuedInstances == 1, "duplicate recovery keeps one global entry");
                Check(snapshot.QueuedEntries.Single().InstanceId == first.GetInstanceID(), "global index preserves entry identity");

                ViewElement returned = TakeQueued(source);
                Check(ReferenceEquals(returned, first), "source FIFO returns the oldest queued instance");
                Destroy(returned);
                yield return FlushDestroy();
            }

            IEnumerator TestNormalAndEmergencyModes()
            {
                ViewElement source = CreateSource("ModePolicy", ViewElementRecoveryPolicy.KeepForever, 0);
                ViewElement instance = null;
                yield return QueueAndRecover(source, value => instance = value);

                ViewElementPoolTrimResult normal = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    TargetQueuedInstances = 0,
                    MaxEvictions = 1,
                });
                Check(normal.EvictedInstances == 0 && normal.BlockedIneligible == 1,
                    "NormalBudget protects KeepForever");
                Check(!normal.BudgetSatisfied && normal.RemainingOverBudgetInstances == 1,
                    "NormalBudget reports blocked remaining budget");
                ViewElementRuntimePoolSnapshot emergencySnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.EmergencyHardCap,
                    refreshNestedUniqueSafety: true);
                Check(emergencySnapshot.EligibleQueuedInstances == 1 &&
                      emergencySnapshot.BudgetMode == ViewElementGlobalEvictionMode.EmergencyHardCap &&
                      runtimePool.GetGlobalPoolSnapshot().BudgetMode == ViewElementGlobalEvictionMode.NormalBudget,
                    "Emergency snapshot mode reports KeepForever as eligible without changing runtime mode");

                ViewElementPoolTrimResult emergency = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    TargetQueuedInstances = 0,
                    MaxEvictions = 1,
                });
                Check(emergency.EvictedInstances == 1, "EmergencyHardCap evicts eligible KeepForever entry");
                Check(emergency.AfterQueuedInstances == 0, "eviction removes logical queue entry before destroy");
                Check(runtimePool.GetGlobalPoolSnapshot().PendingDestroyInstances == 1,
                    "eviction records deferred destroy accounting");
                ViewElement sameFrameReplacement = Request(source);
                Check(!ReferenceEquals(sameFrameReplacement, instance),
                    "same-frame request cannot take the removed eviction victim");
                Destroy(sameFrameReplacement);
                yield return FlushDestroy();
                Check(runtimePool.GetGlobalPoolSnapshot().PendingDestroyInstances == 0,
                    "deferred destroy accounting clears after frame end");

                // The reference is intentionally retained in the local variable to make
                // sure this test never assumes a destroyed Unity object is reusable.
                _ = instance;
            }

            IEnumerator TestHierarchyBudgetAndStaleRequest()
            {
                ViewElement source = CreateSource("HierarchyBudget", ViewElementRecoveryPolicy.KeepN, 1, childCount: 2);
                ViewElement instance = null;
                yield return QueueAndRecover(source, value => instance = value);
                ViewElementRuntimePoolSnapshot snapshot = runtimePool.GetGlobalPoolSnapshot();
                ViewElementRuntimePoolSnapshot.QueuedEntry entry = snapshot.QueuedEntries.Single();
                Check(entry.HierarchyGameObjectCount >= 3, "recovery enqueue caches hierarchy GameObject cost");

                ViewElementPoolTrimResult hierarchyTrim = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    TargetQueuedHierarchyGameObjects = 0,
                    MaxEvictions = 1,
                });
                Check(hierarchyTrim.EvictedInstances == 1 &&
                      hierarchyTrim.EvictedHierarchyGameObjects == entry.HierarchyGameObjectCount,
                    "hierarchy GameObject target evicts using cached cost");
                yield return FlushDestroy();
                _ = instance;

                ViewElement stale = null;
                yield return QueueAndRecover(source, value => stale = value);
                PrepareForPermanentDestroy(stale);
                ViewElementPoolTrimResult staleTrim = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    TargetQueuedInstances = 0,
                    MaxEvictions = 1,
                });
                Check(staleTrim.SkippedStale == 1 && staleTrim.EvictedInstances == 0 &&
                      staleTrim.ProjectedAfterQueuedInstances == 0 && staleTrim.BudgetSatisfied,
                    "trim skips stale entry and projects bookkeeping removal");
                Destroy(stale);
                yield return FlushDestroy();
            }

            IEnumerator TestProtectedEntries()
            {
                ViewElement uniqueSource = CreateSource("UniqueSource", ViewElementRecoveryPolicy.KeepForever, 0, unique: true);
                ViewElement uniqueInstance = Request(uniqueSource);
                ViewElementRuntimePoolSnapshot uniqueSnapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(uniqueSnapshot.LogicalQueuedInstances == 0, "unique source never enters global queue");
                Destroy(uniqueInstance);

                ViewElement pendingSource = CreateSource("PendingRecovery", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElement pending = Request(pendingSource);
                runtimePool.QueueViewElementToRecovery(pending);
                ViewElementRuntimePoolSnapshot pendingSnapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(pendingSnapshot.PendingRecoveryInstances == 1 && pendingSnapshot.LogicalQueuedInstances == 0,
                    "pending recovery is excluded from global queue");
                ViewElementPoolTrimResult pendingTrim = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    TargetQueuedInstances = 0,
                    MaxEvictions = 1,
                });
                Check(pendingTrim.EvictedInstances == 0 && pendingSnapshot.PendingRecoveryInstances == 1,
                    "trim cannot touch pending recovery");
                yield return runtimePool.RecoveryQueuedViewElement(true);
                Destroy(TakeQueued(pendingSource));

                ViewElement activeSource = CreateSource("ActiveQueued", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElement active = null;
                yield return QueueAndRecover(activeSource, value => active = value);
                active.gameObject.SetActive(true);
                ViewElementPoolTrimResult activeTrim = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    TargetQueuedInstances = 0,
                    MaxEvictions = 1,
                });
                Check(activeTrim.EvictedInstances == 0 && activeTrim.BlockedIneligible == 1,
                    "active queued instance is protected");
                active.gameObject.SetActive(false);
                Destroy(TakeQueued(activeSource));

                ViewElement nestedSource = CreateSource("NestedUnique", ViewElementRecoveryPolicy.KeepN, 1, nestedUnique: true);
                ViewElement nested = null;
                yield return QueueAndRecover(nestedSource, value => nested = value);
                ViewElementPoolTrimResult nestedTrim = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    TargetQueuedInstances = 0,
                    MaxEvictions = 1,
                });
                Check(nestedTrim.EvictedInstances == 0 && nestedTrim.BlockedNestedUnique == 1 &&
                      !nestedTrim.BudgetSatisfied,
                    "nested unique hierarchy is blocked in EmergencyHardCap");
                Destroy(TakeQueued(nestedSource));
                yield return FlushDestroy();
            }

            IEnumerator TestNullAndPermanentDestroyEntries()
            {
                ViewElement destroySource = CreateSource(
                    "DestroyOnRecovery",
                    ViewElementRecoveryPolicy.DestroyOnRecovery,
                    0);
                ViewElement destroyInstance = Request(destroySource);
                runtimePool.RecoveryViewElement(destroyInstance);
                ViewElementRuntimePoolSnapshot destroySnapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(destroySnapshot.LogicalQueuedInstances == 0 &&
                      destroySnapshot.PendingDestroyInstances == 1,
                    "DestroyOnRecovery records deferred destroy accounting");
                yield return FlushDestroy();
                Check(runtimePool.GetGlobalPoolSnapshot().PendingDestroyInstances == 0,
                    "DestroyOnRecovery accounting clears after frame end");
                _ = destroyInstance;

                ViewElement source = CreateSource("NullEntry", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElement warmup = Request(source);
                Destroy(warmup);

                Queue<ViewElement> sourceQueue = runtimePool.GetDicts()[source.GetInstanceID()];
                sourceQueue.Enqueue(null);
                ViewElement recovered = Request(source);
                Check(recovered != null, "request skips null source queue entry and instantiates");
                Destroy(recovered);
                yield return FlushDestroy();
            }

            IEnumerator TestPendingRecoveryPermanentDestroy()
            {
                ViewElement source = CreateSource("PendingRecoveryDestroy", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElement instance = Request(source);
                runtimePool.QueueViewElementToRecovery(instance);
                Check(runtimePool.GetGlobalPoolSnapshot().PendingRecoveryInstances == 1,
                    "direct destroy starts from pending recovery");

                ScheduleDirectDestroy(instance);
                ViewElementRuntimePoolSnapshot snapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(snapshot.PendingRecoveryInstances == 0 && snapshot.PendingDestroyInstances == 1,
                    "direct destroy removes pending recovery bookkeeping before scheduling destroy");
                yield return FlushDestroy();
                Check(runtimePool.GetGlobalPoolSnapshot().PendingDestroyInstances == 0,
                    "pending recovery direct destroy accounting clears after frame end");

                ViewElement stalePending = Request(source);
                runtimePool.QueueViewElementToRecovery(stalePending);
                UnityEngine.Object.Destroy(stalePending.gameObject);
                yield return null;
                yield return runtimePool.RecoveryQueuedViewElement(true);
                ViewElementRuntimePoolSnapshot stalePendingSnapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(stalePendingSnapshot.PendingRecoveryInstances == 0 &&
                      stalePendingSnapshot.PendingRecoveryHierarchyGameObjects == 0 &&
                      stalePendingSnapshot.LogicalQueuedInstances == 0,
                    "destroyed pending recovery entry clears its cached hierarchy cost");
                Check(TryValidateBookkeeping(),
                    "destroyed pending recovery preserves bookkeeping invariant");
            }

            IEnumerator TestRequestedPoolOwnershipModes()
            {
                ViewElement owner = CreateSource("Owner", ViewElementRecoveryPolicy.KeepForever, 0);
                ViewElement childSource = CreateSource("OwnerChild", ViewElementRecoveryPolicy.KeepN, 1);

                ViewElementRequestedPool ownerLocalPool = owner.Lifetime.CreatePool(
                    childSource,
                    ViewElementChildRecoveryMode.DestroyWithOwner);
                ViewElement ownerLocal = ownerLocalPool.Request(testRoot.transform);
                ownerLocalPool.Recovery(ownerLocal);
                ViewElementRuntimePoolSnapshot localSnapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(!localSnapshot.QueuedEntries.Any(entry => entry.InstanceId == ownerLocal.GetInstanceID()),
                    "DestroyWithOwner remains outside global queue");
                ViewElement ownerLocalReuse = ownerLocalPool.Request(testRoot.transform);
                Check(ReferenceEquals(ownerLocalReuse, ownerLocal), "DestroyWithOwner returns owner-local instance");
                ownerLocalPool.Dispose();

                ViewElement returnOwner = CreateSource("ReturnOwner", ViewElementRecoveryPolicy.KeepForever, 0);
                ViewElement returnSource = CreateSource("ReturnChild", ViewElementRecoveryPolicy.KeepForever, 0);
                ViewElementRequestedPool returnPool = returnOwner.Lifetime.CreatePool(
                    returnSource,
                    ViewElementChildRecoveryMode.ReturnToGlobalPool);
                ViewElement returned = returnPool.Request(testRoot.transform);
                returnPool.Recovery(returned);
                yield return runtimePool.RecoveryQueuedViewElement(true);
                Check(runtimePool.GetGlobalPoolSnapshot().QueuedEntries.Any(
                        entry => entry.InstanceId == returned.GetInstanceID()),
                    "ReturnToGlobalPool enters global queue");
                ViewElementRuntimePoolSnapshot.QueuedEntry returnEntry = runtimePool
                    .GetGlobalPoolSnapshot()
                    .QueuedEntries
                    .Single(entry => entry.InstanceId == returned.GetInstanceID());
                Check(returnEntry.ExplicitlyAllowsGlobalEviction && returnEntry.IsEligible,
                    "ReturnToGlobalPool explicitly opts into NormalBudget eviction");
                Destroy(TakeQueued(returnSource));
                returnPool.Dispose();

                ViewElement usePolicyOwner = CreateSource("PolicyOwner", ViewElementRecoveryPolicy.KeepForever, 0);
                ViewElement usePolicySource = CreateSource("PolicyChild", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElementRequestedPool usePolicyPool = usePolicyOwner.Lifetime.CreatePool(
                    usePolicySource,
                    ViewElementChildRecoveryMode.UseChildPolicy);
                ViewElement policyChild = usePolicyPool.Request(testRoot.transform);
                usePolicyPool.Recovery(policyChild);
                yield return runtimePool.RecoveryQueuedViewElement(true);
                Check(runtimePool.GetGlobalPoolSnapshot().QueuedEntries.Any(
                        entry => entry.InstanceId == policyChild.GetInstanceID()),
                    "UseChildPolicy follows child recovery policy into global queue");
                Destroy(TakeQueued(usePolicySource));
                usePolicyPool.Dispose();
                yield return FlushDestroy();
            }

            IEnumerator TestWatermarkAndReconfigureValidation()
            {
                ExpectArgumentException(
                    () => runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                    {
                        HighWatermarkInstances = 1,
                        LowWatermarkInstances = 2,
                    }),
                    "low watermark cannot exceed high watermark");
                ExpectArgumentException(
                    () => runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                    {
                        HighWatermarkInstances = 0,
                        LowWatermarkInstances = 1,
                    }),
                    "zero high watermark disables dimension");
                ExpectArgumentException(
                    () => runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                    {
                        MaxEvictionsPerFrame = 0,
                    }),
                    "max evictions per frame must be positive");

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                });
                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions());
                Check(!runtimePool.GetGlobalPoolSnapshot().BudgetEnabled,
                    "reconfigure disabled stops automatic budget");

                ViewElement source = CreateSource("DisabledBudget", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElement instance = null;
                yield return QueueAndRecover(source, value => instance = value);
                yield return null;
                Check(runtimePool.GetGlobalPoolSnapshot().LogicalQueuedInstances == 1,
                    "disabled budget does not auto-evict");
                Destroy(TakeQueued(source));
                _ = instance;
                yield return FlushDestroy();
            }

            IEnumerator TestAutomaticBudgetAndIdleMinimum()
            {
                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                });

                ViewElement source = CreateSource("AutomaticBudget", ViewElementRecoveryPolicy.KeepForever, 0);
                var queued = new List<ViewElement>();
                for (int i = 0; i < 3; i++)
                {
                    ViewElement instance = Request(source);
                    queued.Add(instance);
                    runtimePool.QueueViewElementToRecovery(instance);
                }

                yield return runtimePool.RecoveryQueuedViewElement(true);
                for (int i = 0; i < 8; i++)
                {
                    yield return null;
                }

                ViewElementRuntimePoolSnapshot automaticSnapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(automaticSnapshot.LogicalQueuedInstances == 0,
                    "automatic high/low budget drains eligible queue to low watermark");
                Check(automaticSnapshot.TrimEvictedInstances >= 3,
                    "automatic budget records evictions");

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions());

                ViewElement idleSource = CreateSource("TooYoung", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElement idle = null;
                yield return QueueAndRecover(idleSource, value => idle = value);
                ViewElementPoolTrimResult idleTrim = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    TargetQueuedInstances = 0,
                    MinimumIdleSeconds = 60,
                    MaxEvictions = 1,
                });
                Check(idleTrim.BlockedTooYoung == 1 && !idleTrim.BudgetSatisfied &&
                      idleTrim.RemainingOverBudgetInstances == 1,
                    "minimum idle protects too-young entry and reports unsatisfied budget");
                Destroy(TakeQueued(idleSource));
                _ = idle;
                yield return FlushDestroy();

                ViewElement staleIdleSource = CreateSource("IdleStale", ViewElementRecoveryPolicy.KeepN, 1);
                ViewElement staleIdle = null;
                yield return QueueAndRecover(staleIdleSource, value => staleIdle = value);
                PrepareForPermanentDestroy(staleIdle);
                ViewElementPoolTrimResult staleIdleTrim = runtimePool.TrimQueuedPool(
                    new ViewElementPoolTrimRequest
                    {
                        Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                        MinimumIdleSeconds = 60,
                        MaxEvictions = 1,
                        IdleOnly = true,
                    });
                Check(staleIdleTrim.SkippedStale == 1 && staleIdleTrim.BlockedTooYoung == 0 &&
                      runtimePool.GetGlobalPoolSnapshot().LogicalQueuedInstances == 0,
                    "idle trim removes stale entries before too-young filtering");
                Destroy(staleIdle);
                yield return FlushDestroy();
            }

            IEnumerator TestLowMemoryScheduling()
            {
                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AllowEmergencyHardCapOnLowMemory = false,
                });

                ViewElement normalSource = CreateSource(
                    "LowMemoryNormal",
                    ViewElementRecoveryPolicy.KeepForever,
                    0);
                ViewElement normal = null;
                yield return QueueAndRecover(normalSource, value => normal = value);

                InvokeLowMemory();
                ViewElementRuntimePoolSnapshot immediate = runtimePool.GetGlobalPoolSnapshot();
                Check(immediate.LogicalQueuedInstances == 1 &&
                      immediate.PendingDestroyInstances == 0,
                    "low-memory callback schedules trim without synchronous destroy");

                for (int i = 0; i < 4; i++)
                {
                    yield return null;
                }

                ViewElementRuntimePoolSnapshot normalAfter = runtimePool.GetGlobalPoolSnapshot();
                Check(normalAfter.LogicalQueuedInstances == 1 &&
                      normalAfter.LastTrimResult != null &&
                      normalAfter.LastTrimResult.BlockedIneligible > 0,
                    "low-memory fallback honors NormalBudget without emergency opt-in");
                Destroy(TakeQueued(normalSource));
                yield return FlushDestroy();

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AllowEmergencyHardCapOnLowMemory = true,
                });

                ViewElement emergencySource = CreateSource(
                    "LowMemoryEmergency",
                    ViewElementRecoveryPolicy.KeepForever,
                    0);
                ViewElement emergency = null;
                yield return QueueAndRecover(emergencySource, value => emergency = value);

                InvokeLowMemory();
                immediate = runtimePool.GetGlobalPoolSnapshot();
                Check(immediate.LogicalQueuedInstances == 1 &&
                      immediate.PendingDestroyInstances == 0,
                    "low-memory EmergencyHardCap remains deferred");

                for (int i = 0; i < 4; i++)
                {
                    yield return null;
                }

                ViewElementRuntimePoolSnapshot emergencyAfter = runtimePool.GetGlobalPoolSnapshot();
                Check(emergencyAfter.LogicalQueuedInstances == 0 &&
                      emergencyAfter.TrimEvictedInstances >= 1,
                    "low-memory opt-in performs deferred EmergencyHardCap trim");
                _ = normal;
                _ = emergency;
                yield return FlushDestroy();
            }

            public IEnumerator Cleanup()
            {
                if (runtimePool != null)
                {
                    runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions());
                    // Finish any test-only pending recovery entries before taking
                    // source FIFO entries back. The baseline guard prevents this
                    // from touching a pre-existing application queue.
                    yield return runtimePool.RecoveryQueuedViewElement(true);

                    if (testRoot != null)
                    {
                        var testSourceKeys = new HashSet<int>(sources.Select(source => source.GetInstanceID()));
                        ViewElementRuntimePoolSnapshot snapshot = runtimePool.GetGlobalPoolSnapshot();
                        foreach (int sourceKey in testSourceKeys)
                        {
                            int queuedCount = snapshot.QueuedEntries.Count(entry => entry.SourceKey == sourceKey);
                            if (!sources.Any(source => source.GetInstanceID() == sourceKey))
                            {
                                continue;
                            }

                            ViewElement source = sources.Single(source => source.GetInstanceID() == sourceKey);
                            for (int i = 0; i < queuedCount; i++)
                            {
                                Destroy(runtimePool.RequestViewElement(source));
                            }
                        }
                    }
                }

                foreach (ViewElement instance in instances)
                {
                    Destroy(instance);
                }

                RemoveUniqueDictionaryEntries();
                if (testRoot != null)
                {
                    UnityEngine.Object.Destroy(testRoot);
                }

                yield return FlushDestroy();
            }

            ViewElement CreateSource(
                string name,
                ViewElementRecoveryPolicy recoveryPolicy,
                int recoveryKeepCount,
                bool unique = false,
                bool nestedUnique = false,
                int childCount = 0)
            {
                GameObject sourceObject = new GameObject(name, typeof(RectTransform), typeof(Animator));
                sourceObject.transform.SetParent(testRoot.transform, false);
                sourceObject.SetActive(false);
                ViewElement source = sourceObject.AddComponent<ViewElement>();
                source.transition = ViewElement.TransitionType.ActiveSwitch;
                source.IsUnique = unique;
                source.recoveryPolicy = recoveryPolicy;
                source.recoveryKeepCount = recoveryKeepCount;

                for (int i = 0; i < childCount; i++)
                {
                    GameObject child = new GameObject($"{name}_Child_{i}", typeof(RectTransform));
                    child.transform.SetParent(sourceObject.transform, false);
                    child.SetActive(false);
                }

                if (nestedUnique)
                {
                    GameObject childObject = new GameObject($"{name}_NestedUnique", typeof(RectTransform), typeof(Animator));
                    childObject.transform.SetParent(sourceObject.transform, false);
                    childObject.SetActive(false);
                    ViewElement child = childObject.AddComponent<ViewElement>();
                    child.transition = ViewElement.TransitionType.ActiveSwitch;
                    child.IsUnique = true;
                    child.recoveryPolicy = ViewElementRecoveryPolicy.KeepForever;
                    child.Setup();
                }

                source.Setup();
                sources.Add(source);
                if (unique)
                {
                    uniqueSourceKeys.Add(source.GetInstanceID());
                }

                return source;
            }

            ViewElement Request(ViewElement source)
            {
                ViewElement instance = runtimePool.RequestViewElement(source);
                Check(instance != null, $"request creates instance for {source.name}");
                if (instance != null)
                {
                    instances.Add(instance);
                }

                return instance;
            }

            IEnumerator QueueAndRecover(ViewElement source, Action<ViewElement> completed)
            {
                ViewElement instance = Request(source);
                runtimePool.QueueViewElementToRecovery(instance);
                yield return runtimePool.RecoveryQueuedViewElement(true);
                completed?.Invoke(instance);
            }

            ViewElement TakeQueued(ViewElement source)
            {
                ViewElement instance = runtimePool.RequestViewElement(source);
                Check(instance != null, $"request returns queued instance for {source.name}");
                return instance;
            }

            void PrepareForPermanentDestroy(ViewElement viewElement)
            {
                MethodInfo method = typeof(ViewElement).GetMethod(
                    "PrepareForPermanentDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(viewElement, null);
            }

            void ScheduleDirectDestroy(ViewElement viewElement)
            {
                MethodInfo method = typeof(ViewElementRuntimePool).GetMethod(
                    "DestroyViewElementHierarchy",
                    BindingFlags.Static | BindingFlags.NonPublic);
                method.Invoke(null, new object[] { viewElement });
            }

            bool TryValidateBookkeeping()
            {
                MethodInfo method = typeof(ViewElementRuntimePool).GetMethod(
                    "TryValidateGlobalQueueConsistency",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                object[] arguments = { null };
                return method != null && (bool)method.Invoke(runtimePool, arguments);
            }

            void InvokeLowMemory()
            {
                MethodInfo method = typeof(ViewElementRuntimePool).GetMethod(
                    "OnLowMemory",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(runtimePool, null);
            }

            void ExpectArgumentException(Action action, string description)
            {
                try
                {
                    action();
                    Fail(description);
                }
                catch (ArgumentException)
                {
                    Pass(description);
                }
            }

            void Check(bool condition, string description)
            {
                checkCount++;
                if (!condition)
                {
                    Fail(description);
                    return;
                }

                Debug.Log("[RuntimePoolSelfTest] PASS " + description);
            }

            void Pass(string description)
            {
                checkCount++;
                Debug.Log("[RuntimePoolSelfTest] PASS " + description);
            }

            public void Fail(string description)
            {
                failureCount++;
                Debug.LogError("[RuntimePoolSelfTest] FAIL " + description);
            }

            IEnumerator FlushDestroy()
            {
                yield return null;
                yield return null;
            }

            static void Destroy(ViewElement viewElement)
            {
                if (viewElement != null)
                {
                    UnityEngine.Object.Destroy(viewElement.gameObject);
                }
            }

            void RemoveUniqueDictionaryEntries()
            {
                FieldInfo field = typeof(ViewElementRuntimePool).GetField(
                    "uniqueVeDicts",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (!(field?.GetValue(runtimePool) is IDictionary dictionary))
                {
                    return;
                }

                foreach (int sourceKey in uniqueSourceKeys)
                {
                    dictionary.Remove(sourceKey);
                }
            }
        }
    }
}
#endif
