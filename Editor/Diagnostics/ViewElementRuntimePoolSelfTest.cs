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
            readonly ViewElementGlobalPoolBudgetOptions originalBudgetOptions;
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
                FieldInfo budgetOptionsField = typeof(ViewElementRuntimePool).GetField(
                    "budgetOptions", BindingFlags.Instance | BindingFlags.NonPublic);
                originalBudgetOptions =
                    ((ViewElementGlobalPoolBudgetOptions)budgetOptionsField?.GetValue(runtimePool))?.Clone() ??
                    new ViewElementGlobalPoolBudgetOptions();
            }

            public IEnumerator Run()
            {
                testRoot = new GameObject("__RuntimePoolSelfTest__");
                testRoot.SetActive(false);

                TestInitializedBudgetSetting();
                TestSerializedBudgetBackwardCompatibility();
                yield return TestDuplicateRecoveryAndFifo();
                yield return TestNormalAndEmergencyModes();
                yield return TestTrimEligibilityIndependentOfRecoveryPolicy();
                yield return TestHierarchyBudgetAndStaleRequest();
                yield return TestProtectedEntries();
                yield return TestNullAndPermanentDestroyEntries();
                yield return TestPendingRecoveryPermanentDestroy();
                yield return TestRequestedPoolOwnershipModes();
                yield return TestWatermarkAndReconfigureValidation();
                yield return TestAutomaticBudgetAndIdleMinimum();
                yield return TestSplitAutomaticTiming();
                yield return TestLowMemoryScheduling();
            }

            void TestInitializedBudgetSetting()
            {
                ViewController controller = runtimePool.GetComponent<ViewController>();
                FieldInfo saveDataField = typeof(ViewController).GetField(
                    "saveData", BindingFlags.Instance | BindingFlags.NonPublic);
                var data = controller != null
                    ? saveDataField?.GetValue(controller) as ViewSystemSaveDataBase
                    : null;
                if (data?.globalSetting == null)
                {
                    Fail("initialized controller exposes its saved global setting");
                    return;
                }

                ViewElementGlobalPoolBudgetSetting setting = data.globalSetting.globalPoolBudget;
                if (setting == null || !setting.enabled)
                {
                    Check(!originalBudgetOptions.Enabled,
                        "legacy or disabled saved budget starts with automatic trimming off");
                    return;
                }

                Check(originalBudgetOptions.Enabled &&
                      originalBudgetOptions.Mode == setting.mode &&
                      originalBudgetOptions.HighWatermarkInstances == setting.highWatermarkInstances &&
                      originalBudgetOptions.LowWatermarkInstances == setting.lowWatermarkInstances &&
                      originalBudgetOptions.HighWatermarkHierarchyGameObjects ==
                          setting.highWatermarkHierarchyGameObjects &&
                      originalBudgetOptions.LowWatermarkHierarchyGameObjects ==
                          setting.lowWatermarkHierarchyGameObjects &&
                      originalBudgetOptions.MaxEvictionsPerFrame == setting.maxEvictionsPerFrame &&
                      Mathf.Approximately(
                          originalBudgetOptions.AutoTrimGraceSeconds, setting.AutoTrimGraceSeconds) &&
                      Mathf.Approximately(
                          originalBudgetOptions.IdleTrimAfterSeconds, setting.IdleTrimAfterSeconds) &&
                      originalBudgetOptions.AllowEmergencyHardCapOnLowMemory ==
                          setting.allowEmergencyHardCapOnLowMemory,
                    "ViewController.Init applies the saved pool budget to runtime");
            }

            void TestSerializedBudgetBackwardCompatibility()
            {
                ViewController controller = runtimePool.GetComponent<ViewController>() ??
                                            ViewController.Instance;
                MethodInfo applySetting = typeof(ViewController).GetMethod(
                    "ApplyGlobalPoolBudgetSetting",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo runtimeOptionsField = typeof(ViewElementRuntimePool).GetField(
                    "budgetOptions",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (controller == null || applySetting == null || runtimeOptionsField == null)
                {
                    Fail("serialized budget compatibility test can access initialization bridge");
                    return;
                }

                try
                {
                    applySetting.Invoke(controller, new object[] { null });
                    Check(!runtimePool.GetGlobalPoolSnapshot().BudgetEnabled,
                        "missing legacy budget setting keeps automatic trimming disabled");

                    var disabledLegacySetting = new ViewElementGlobalPoolBudgetSetting
                    {
                        enabled = false,
                        mode = (ViewElementGlobalEvictionMode)int.MaxValue,
                        highWatermarkInstances = 0,
                        lowWatermarkInstances = 1,
                        maxEvictionsPerFrame = 0,
                        minimumIdleSeconds = -1,
                    };
                    applySetting.Invoke(controller, new object[] { disabledLegacySetting });
                    Check(!runtimePool.GetGlobalPoolSnapshot().BudgetEnabled,
                        "disabled legacy budget ignores uninitialized inactive fields");

                    var configuredSetting = new ViewElementGlobalPoolBudgetSetting
                    {
                        enabled = true,
                        mode = ViewElementGlobalEvictionMode.NormalBudget,
                        highWatermarkInstances = 9,
                        lowWatermarkInstances = 3,
                        highWatermarkHierarchyGameObjects = 90,
                        lowWatermarkHierarchyGameObjects = 30,
                        maxEvictionsPerFrame = 2,
                        minimumIdleSeconds = 1.25f,
                        allowEmergencyHardCapOnLowMemory = true,
                    };
                    applySetting.Invoke(controller, new object[] { configuredSetting });

                    var appliedOptions =
                        (ViewElementGlobalPoolBudgetOptions)runtimeOptionsField.GetValue(runtimePool);
                    Check(appliedOptions.Enabled &&
                          appliedOptions.Mode == configuredSetting.mode &&
                          appliedOptions.HighWatermarkInstances == 9 &&
                          appliedOptions.LowWatermarkInstances == 3 &&
                          appliedOptions.HighWatermarkHierarchyGameObjects == 90 &&
                          appliedOptions.LowWatermarkHierarchyGameObjects == 30 &&
                          appliedOptions.MaxEvictionsPerFrame == 2 &&
                          Mathf.Approximately(appliedOptions.AutoTrimGraceSeconds, 1.25f) &&
                          Mathf.Approximately(appliedOptions.IdleTrimAfterSeconds, 1.25f) &&
                          appliedOptions.AllowEmergencyHardCapOnLowMemory,
                        "legacy minimum idle value maps to both automatic timing settings");

                    configuredSetting.AutoTrimGraceSeconds = 2.5f;
                    configuredSetting.IdleTrimAfterSeconds = 0;
                    applySetting.Invoke(controller, new object[] { configuredSetting });
                    appliedOptions =
                        (ViewElementGlobalPoolBudgetOptions)runtimeOptionsField.GetValue(runtimePool);
                    Check(Mathf.Approximately(appliedOptions.AutoTrimGraceSeconds, 2.5f) &&
                          Mathf.Approximately(appliedOptions.IdleTrimAfterSeconds, 0) &&
                          Mathf.Approximately(configuredSetting.minimumIdleSeconds, 1.25f),
                        "split timing overrides legacy serialized value without changing it");

                    var legacyOptions = new ViewElementGlobalPoolBudgetOptions
                    {
                        MinimumIdleSeconds = 3,
                    };
                    Check(Mathf.Approximately(legacyOptions.AutoTrimGraceSeconds, 3) &&
                          Mathf.Approximately(legacyOptions.IdleTrimAfterSeconds, 3),
                        "legacy runtime MinimumIdleSeconds setter configures both timings");

                    var deserializedLegacySetting =
                        JsonUtility.FromJson<ViewElementGlobalPoolBudgetSetting>(
                            "{\"minimumIdleSeconds\":1.25}");
                    Check(Mathf.Approximately(deserializedLegacySetting.AutoTrimGraceSeconds, 1.25f) &&
                          Mathf.Approximately(deserializedLegacySetting.IdleTrimAfterSeconds, 1.25f),
                        "deserialized legacy timing remains effective without new fields");

                    configuredSetting.lowWatermarkInstances = 10;
                    applySetting.Invoke(controller, new object[] { configuredSetting });
                    Check(!runtimePool.GetGlobalPoolSnapshot().BudgetEnabled,
                        "invalid enabled budget safely falls back to disabled trimming");
                }
                catch (Exception exception)
                {
                    Fail("serialized budget compatibility test threw: " + exception);
                }
                finally
                {
                    runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions());
                }
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
                source.trimOnlyInEmergency = true;
                ViewElement instance = null;
                yield return QueueAndRecover(source, value => instance = value);
                ViewElementRuntimePoolSnapshot normalSnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.NormalBudget,
                    refreshNestedUniqueSafety: true);
                Check(normalSnapshot.QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.BlockedEmergencyOnly,
                    "NormalBudget snapshot explains emergency-only trim protection");

                ViewElementPoolTrimResult normal = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    TargetQueuedInstances = 0,
                    MaxEvictions = 1,
                });
                Check(normal.EvictedInstances == 0 && normal.BlockedIneligible == 1,
                    "NormalBudget protects emergency-only queued entries");
                Check(!normal.BudgetSatisfied && normal.RemainingOverBudgetInstances == 1,
                    "NormalBudget reports blocked remaining budget");
                ViewElementRuntimePoolSnapshot emergencySnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.EmergencyHardCap,
                    refreshNestedUniqueSafety: true);
                Check(emergencySnapshot.EligibleQueuedInstances == 1 &&
                      emergencySnapshot.QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.EligibleByEmergencyHardCap &&
                      emergencySnapshot.BudgetMode == ViewElementGlobalEvictionMode.EmergencyHardCap &&
                      runtimePool.GetGlobalPoolSnapshot().BudgetMode == ViewElementGlobalEvictionMode.NormalBudget,
                    "Emergency snapshot mode reports protected entry as eligible without changing runtime mode");

                ViewElementPoolTrimResult emergency = runtimePool.TrimQueuedPool(new ViewElementPoolTrimRequest
                {
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    TargetQueuedInstances = 0,
                    MaxEvictions = 1,
                });
                Check(emergency.EvictedInstances == 1, "EmergencyHardCap evicts emergency-only entry");
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

            IEnumerator TestTrimEligibilityIndependentOfRecoveryPolicy()
            {
                foreach (ViewElementRecoveryPolicy policy in new[]
                         {
                             ViewElementRecoveryPolicy.KeepForever,
                             ViewElementRecoveryPolicy.KeepN,
                         })
                {
                    foreach (bool emergencyOnly in new[] { false, true })
                    {
                        ViewElement source = CreateSource(
                            $"TrimEligibility_{policy}_{emergencyOnly}", policy, 1);
                        Check(!source.trimOnlyInEmergency,
                            $"{policy} defaults to normal and emergency trim eligibility");
                        source.trimOnlyInEmergency = emergencyOnly;
                        yield return QueueAndRecover(source, null);

                        ViewElementRuntimePoolSnapshot normal = runtimePool.GetGlobalPoolSnapshot(
                            ViewElementGlobalEvictionMode.NormalBudget,
                            refreshNestedUniqueSafety: true);
                        ViewElementRuntimePoolSnapshot.QueuedEntry normalEntry = normal.QueuedEntries.Single();
                        Check(normalEntry.TrimOnlyInEmergency == emergencyOnly &&
                              normalEntry.IsEligible == !emergencyOnly &&
                              normalEntry.EligibilityReason == (emergencyOnly
                                  ? ViewElementGlobalEvictionEligibilityReason.BlockedEmergencyOnly
                                  : ViewElementGlobalEvictionEligibilityReason.EligibleByDefault),
                            $"{policy} normal eligibility follows trimOnlyInEmergency={emergencyOnly}");
                        ViewElementPoolTrimResult normalDryRun = runtimePool.TrimQueuedPool(
                            new ViewElementPoolTrimRequest
                            {
                                DryRun = true,
                                Mode = ViewElementGlobalEvictionMode.NormalBudget,
                                TargetQueuedInstances = 0,
                                MaxEvictions = 1,
                            });
                        Check(normalDryRun.EvictedInstances == (emergencyOnly ? 0 : 1),
                            $"{policy} normal dry-run follows trimOnlyInEmergency={emergencyOnly}");

                        ViewElementRuntimePoolSnapshot emergency = runtimePool.GetGlobalPoolSnapshot(
                            ViewElementGlobalEvictionMode.EmergencyHardCap,
                            refreshNestedUniqueSafety: true);
                        Check(emergency.QueuedEntries.Single().IsEligible,
                            $"{policy} remains eligible in EmergencyHardCap");

                        Destroy(TakeQueued(source));
                        yield return FlushDestroy();
                    }
                }
            }

            IEnumerator TestHierarchyBudgetAndStaleRequest()
            {
                ViewElement source = CreateSource("HierarchyBudget", ViewElementRecoveryPolicy.KeepN, 1, childCount: 2);
                ViewElement instance = null;
                yield return QueueAndRecover(source, value => instance = value);
                ViewElementRuntimePoolSnapshot snapshot = runtimePool.GetGlobalPoolSnapshot();
                ViewElementRuntimePoolSnapshot.QueuedEntry entry = snapshot.QueuedEntries.Single();
                Check(entry.HierarchyGameObjectCount >= 3 &&
                      entry.EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.EligibleByDefault,
                    "default trim eligibility includes hierarchy cost and reason");
                ViewElementRuntimePoolSnapshot tooYoungSnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.NormalBudget,
                    minimumIdleSeconds: 60,
                    refreshNestedUniqueSafety: true);
                Check(tooYoungSnapshot.QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.BlockedTooYoung,
                    "snapshot idle override explains too-young eligibility block");

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
                ViewElementRuntimePoolSnapshot staleSnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.EmergencyHardCap,
                    minimumIdleSeconds: 0,
                    refreshNestedUniqueSafety: true);
                Check(staleSnapshot.QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.Stale,
                    "snapshot explains stale queued entry");
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
                ViewElementRuntimePoolSnapshot activeSnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.EmergencyHardCap,
                    refreshNestedUniqueSafety: true);
                Check(activeSnapshot.QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.BlockedActive,
                    "active queued snapshot explains eligibility block");
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
                ViewElementRuntimePoolSnapshot nestedSnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.EmergencyHardCap,
                    refreshNestedUniqueSafety: true);
                Check(nestedSnapshot.QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.BlockedNestedUnique,
                    "nested unique snapshot explains eligibility block");
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

                ViewElement protectedParentSource = CreateSource(
                    "NestedEmergencyOnly", ViewElementRecoveryPolicy.KeepForever, 0);
                GameObject protectedChildObject = new GameObject(
                    "ProtectedChild", typeof(RectTransform), typeof(Animator));
                protectedChildObject.transform.SetParent(protectedParentSource.transform, false);
                protectedChildObject.SetActive(false);
                ViewElement protectedChild = protectedChildObject.AddComponent<ViewElement>();
                protectedChild.transition = ViewElement.TransitionType.ActiveSwitch;
                protectedChild.trimOnlyInEmergency = true;
                protectedChild.Setup();
                yield return QueueAndRecover(protectedParentSource, null);
                ViewElementRuntimePoolSnapshot protectedParentSnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.NormalBudget,
                    refreshNestedUniqueSafety: true);
                Check(protectedParentSnapshot.QueuedEntries.Single().NestedEmergencyOnlyCount == 1 &&
                      protectedParentSnapshot.QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.BlockedNestedEmergencyOnly,
                    "normal trim protects a queued parent containing an emergency-only child");
                ViewElementPoolTrimResult protectedParentNormalTrim = runtimePool.TrimQueuedPool(
                    new ViewElementPoolTrimRequest
                    {
                        Mode = ViewElementGlobalEvictionMode.NormalBudget,
                        TargetQueuedInstances = 0,
                        MaxEvictions = 1,
                    });
                Check(protectedParentNormalTrim.EvictedInstances == 0 &&
                      protectedParentNormalTrim.BlockedIneligible == 1,
                    "normal trim cannot indirectly destroy an emergency-only child");
                ViewElementPoolTrimResult protectedParentEmergencyTrim = runtimePool.TrimQueuedPool(
                    new ViewElementPoolTrimRequest
                    {
                        Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                        TargetQueuedInstances = 0,
                        MaxEvictions = 1,
                    });
                Check(protectedParentEmergencyTrim.EvictedInstances == 1,
                    "emergency trim may destroy the protected parent hierarchy");
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
                ViewElement returnSource = CreateSource("ReturnChild", ViewElementRecoveryPolicy.DestroyOnRecovery, 0);
                returnSource.trimOnlyInEmergency = true;
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
                Check(returnEntry.TrimOnlyInEmergency && !returnEntry.IsEligible &&
                      returnEntry.EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.BlockedEmergencyOnly,
                    "ReturnToGlobalPool bypasses recovery policy without bypassing trim protection");
                ViewElementRuntimePoolSnapshot emergencyReturnSnapshot = runtimePool.GetGlobalPoolSnapshot(
                    ViewElementGlobalEvictionMode.EmergencyHardCap,
                    refreshNestedUniqueSafety: true);
                Check(emergencyReturnSnapshot.QueuedEntries.Single().IsEligible,
                    "ReturnToGlobalPool child remains eligible for emergency trim");
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
                ExpectArgumentException(
                    () => runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                    {
                        AutoTrimGraceSeconds = -1,
                    }),
                    "automatic trim grace must be non-negative");
                ExpectArgumentException(
                    () => runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                    {
                        IdleTrimAfterSeconds = float.NaN,
                    }),
                    "periodic idle threshold must be finite");

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

            IEnumerator TestSplitAutomaticTiming()
            {
                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AutoTrimGraceSeconds = 60,
                    IdleTrimAfterSeconds = 0,
                });
                FieldInfo idleRunnerField = typeof(ViewElementRuntimePool).GetField(
                    "idleEvictionRunner", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(idleRunnerField != null && idleRunnerField.GetValue(runtimePool) == null,
                    "automatic grace alone does not start periodic idle trim");

                ViewElement budgetSource = CreateSource(
                    "SplitBudgetGrace", ViewElementRecoveryPolicy.KeepForever, 0);
                for (int i = 0; i < 2; i++)
                {
                    ViewElement instance = Request(budgetSource);
                    runtimePool.QueueViewElementToRecovery(instance);
                }

                yield return runtimePool.RecoveryQueuedViewElement(true);
                for (int i = 0; i < 4; i++)
                {
                    yield return null;
                }

                ViewElementRuntimePoolSnapshot graceSnapshot = runtimePool.GetGlobalPoolSnapshot();
                Check(graceSnapshot.LogicalQueuedInstances == 2 &&
                      graceSnapshot.BlockedTooYoungInstances == 2 &&
                      graceSnapshot.LastTrimResult != null &&
                      graceSnapshot.LastTrimResult.BlockedTooYoung == 2 &&
                      graceSnapshot.LastTrimResult.EvictedInstances == 0,
                    "budget trim respects grace while periodic idle trim is disabled");

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AutoTrimGraceSeconds = 0,
                    IdleTrimAfterSeconds = 0,
                });
                for (int i = 0; i < 8; i++)
                {
                    yield return null;
                }

                Check(runtimePool.GetGlobalPoolSnapshot().LogicalQueuedInstances == 0,
                    "budget trim proceeds after grace is removed by reconfiguration");
                yield return FlushDestroy();

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    MaxEvictionsPerFrame = 1,
                    AutoTrimGraceSeconds = 60,
                    IdleTrimAfterSeconds = 0.05f,
                });
                ViewElement idleSource = CreateSource(
                    "SplitIdleGrace", ViewElementRecoveryPolicy.KeepForever, 0);
                yield return QueueAndRecover(idleSource, null);
                ViewElementRuntimePoolSnapshot protectedIdle = runtimePool.GetGlobalPoolSnapshot();
                Check(idleRunnerField != null &&
                      idleRunnerField.GetValue(runtimePool) != null &&
                      protectedIdle.LogicalQueuedInstances == 1 &&
                      protectedIdle.BlockedTooYoungInstances == 1 &&
                      Mathf.Approximately(protectedIdle.AutoTrimGraceSeconds, 60) &&
                      Mathf.Approximately(protectedIdle.IdleTrimAfterSeconds, 0.05f),
                    "idle threshold starts periodic trim while automatic grace protects its entry");

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    MaxEvictionsPerFrame = 1,
                    AutoTrimGraceSeconds = 0,
                    IdleTrimAfterSeconds = 0.05f,
                });
                yield return new WaitForSecondsRealtime(1.2f);
                Check(runtimePool.GetGlobalPoolSnapshot().LogicalQueuedInstances == 0,
                    "periodic idle trim runs independently when its threshold is enabled");
                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions());
                yield return FlushDestroy();
            }

            IEnumerator TestLowMemoryScheduling()
            {
                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AllowEmergencyHardCapOnLowMemory = false,
                });

                ViewElement normalSource = CreateSource(
                    "LowMemoryNormal",
                    ViewElementRecoveryPolicy.KeepForever,
                    0);
                normalSource.trimOnlyInEmergency = true;
                ViewElement normal = null;
                yield return QueueAndRecover(normalSource, value => normal = value);
                Check(runtimePool.GetGlobalPoolSnapshot().QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.BlockedEmergencyOnly,
                    "low-memory NormalBudget test entry requires emergency trim");

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
                      normalAfter.TrimCount > immediate.TrimCount &&
                      normalAfter.LastTrimResult != null &&
                      normalAfter.LastTrimResult.EvictedInstances == 0 &&
                      normalAfter.LastTrimResult.BlockedIneligible > 0,
                    "low-memory keeps NormalBudget without emergency opt-in");
                Destroy(TakeQueued(normalSource));
                yield return FlushDestroy();

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AllowEmergencyHardCapOnLowMemory = true,
                });

                ViewElement emergencySource = CreateSource(
                    "LowMemoryEmergency",
                    ViewElementRecoveryPolicy.KeepForever,
                    0);
                emergencySource.trimOnlyInEmergency = true;
                ViewElement emergency = null;
                yield return QueueAndRecover(emergencySource, value => emergency = value);
                Check(runtimePool.GetGlobalPoolSnapshot().QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.BlockedEmergencyOnly,
                    "low-memory upgrade test entry starts protected in NormalBudget");

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
                      emergencyAfter.TrimCount > immediate.TrimCount &&
                      emergencyAfter.LastTrimResult != null &&
                      emergencyAfter.LastTrimResult.EvictedInstances == 1,
                    "low-memory opt-in upgrades NormalBudget to deferred EmergencyHardCap trim");

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AllowEmergencyHardCapOnLowMemory = false,
                });

                ViewElement globalEmergencySource = CreateSource(
                    "LowMemoryGlobalEmergency",
                    ViewElementRecoveryPolicy.KeepForever,
                    0);
                globalEmergencySource.trimOnlyInEmergency = true;
                ViewElement globalEmergency = null;
                yield return QueueAndRecover(
                    globalEmergencySource,
                    value => globalEmergency = value);
                Check(runtimePool.GetGlobalPoolSnapshot().QueuedEntries.Single().EligibilityReason ==
                      ViewElementGlobalEvictionEligibilityReason.EligibleByEmergencyHardCap,
                    "configured EmergencyHardCap accepts emergency-only queued entries");

                InvokeLowMemory();
                for (int i = 0; i < 4; i++)
                {
                    yield return null;
                }

                ViewElementRuntimePoolSnapshot globalEmergencyAfter =
                    runtimePool.GetGlobalPoolSnapshot();
                Check(globalEmergencyAfter.LogicalQueuedInstances == 0 &&
                      globalEmergencyAfter.LastTrimResult != null &&
                      globalEmergencyAfter.LastTrimResult.EvictedInstances == 1,
                    "global EmergencyHardCap remains emergency without low-memory opt-in");

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AutoTrimGraceSeconds = 60,
                    IdleTrimAfterSeconds = 0,
                    AllowEmergencyHardCapOnLowMemory = true,
                });
                ViewElement graceSource = CreateSource(
                    "LowMemoryGrace", ViewElementRecoveryPolicy.KeepForever, 0);
                graceSource.trimOnlyInEmergency = true;
                yield return QueueAndRecover(graceSource, null);
                InvokeLowMemory();
                for (int i = 0; i < 4; i++)
                {
                    yield return null;
                }

                ViewElementRuntimePoolSnapshot graceAfter = runtimePool.GetGlobalPoolSnapshot();
                Check(graceAfter.LogicalQueuedInstances == 1 &&
                      graceAfter.LastTrimResult != null &&
                      graceAfter.LastTrimResult.BlockedTooYoung == 1 &&
                      graceAfter.LastTrimResult.EvictedInstances == 0,
                    "low-memory emergency upgrade still respects automatic grace");

                runtimePool.ConfigureGlobalPoolBudget(new ViewElementGlobalPoolBudgetOptions
                {
                    Enabled = true,
                    Mode = ViewElementGlobalEvictionMode.NormalBudget,
                    HighWatermarkInstances = 1,
                    LowWatermarkInstances = 0,
                    MaxEvictionsPerFrame = 1,
                    AutoTrimGraceSeconds = 0,
                    IdleTrimAfterSeconds = 0,
                    AllowEmergencyHardCapOnLowMemory = true,
                });
                InvokeLowMemory();
                for (int i = 0; i < 4; i++)
                {
                    yield return null;
                }

                Check(runtimePool.GetGlobalPoolSnapshot().LogicalQueuedInstances == 0,
                    "a new low-memory event can trim after grace is removed");
                _ = normal;
                _ = emergency;
                _ = globalEmergency;
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
                if (runtimePool != null)
                {
                    runtimePool.ConfigureGlobalPoolBudget(originalBudgetOptions);
                }
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
