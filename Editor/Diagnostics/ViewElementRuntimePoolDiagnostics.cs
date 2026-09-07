using UnityEditor;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    public static class ViewElementRuntimePoolDiagnostics
    {
        const string MenuRoot = "MacacaGames/ViewSystem/Diagnostics/Runtime Pool/";

        [MenuItem(MenuRoot + "Print Snapshot")]
        static void PrintSnapshot()
        {
            ViewElementRuntimePool runtimePool = ViewController.runtimePool;
            if (runtimePool == null)
            {
                Debug.LogWarning("No active ViewElementRuntimePool was found. Enter Play Mode first.");
                return;
            }

            ViewElementRuntimePoolSnapshot snapshot = runtimePool.GetGlobalPoolSnapshot(
                refreshNestedUniqueSafety: true);
            Debug.Log(
                $"RuntimePool queued={snapshot.LogicalQueuedInstances} instances/" +
                $"{snapshot.LogicalQueuedHierarchyGameObjects} GameObjects, " +
                $"pendingRecovery={snapshot.PendingRecoveryInstances}/" +
                $"{snapshot.PendingRecoveryHierarchyGameObjects} GameObjects, " +
                $"pendingDestroy={snapshot.PendingDestroyInstances}/" +
                $"{snapshot.PendingDestroyHierarchyGameObjects} GameObjects, " +
                $"postFrameActualHierarchy={snapshot.PostFrameActualHierarchyGameObjects}, " +
                $"eligible={snapshot.EligibleQueuedInstances}, " +
                $"blocked(nestedUnique={snapshot.BlockedNestedUniqueInstances}, " +
                $"ineligible={snapshot.BlockedIneligibleInstances}, " +
                $"tooYoung={snapshot.BlockedTooYoungInstances}), " +
                $"poolMiss={snapshot.PoolMissCount}, instantiate={snapshot.InstantiateCount} " +
                $"({snapshot.InstantiateMilliseconds:0.###}ms), trims={snapshot.TrimCount}.",
                runtimePool);
        }

        [MenuItem(MenuRoot + "Dry Run Emergency Hard Cap")]
        static void DryRunEmergencyHardCap()
        {
            ViewElementRuntimePool runtimePool = ViewController.runtimePool;
            if (runtimePool == null)
            {
                Debug.LogWarning("No active ViewElementRuntimePool was found. Enter Play Mode first.");
                return;
            }

            ViewElementPoolTrimResult result = runtimePool.TrimQueuedPool(CreateEmergencyRequest(true));
            Debug.Log(FormatResult("Dry-run", result), runtimePool);
        }

        [MenuItem(MenuRoot + "Aggressive Clear Eligible Queued")]
        static void AggressiveClearEligibleQueued()
        {
            ViewElementRuntimePool runtimePool = ViewController.runtimePool;
            if (runtimePool == null)
            {
                Debug.LogWarning("No active ViewElementRuntimePool was found. Enter Play Mode first.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Clear Runtime ViewElement Pool",
                    "Permanently destroy every eligible queued non-unique ViewElement in the active runtime pool?",
                    "Clear Eligible",
                    "Cancel"))
            {
                return;
            }

            ViewElementPoolTrimResult result = runtimePool.TrimQueuedPool(CreateEmergencyRequest(false));
            Debug.Log(FormatResult("Aggressive clear", result), runtimePool);
        }

        static ViewElementPoolTrimRequest CreateEmergencyRequest(bool dryRun)
        {
            return new ViewElementPoolTrimRequest
            {
                DryRun = dryRun,
                Mode = ViewElementGlobalEvictionMode.EmergencyHardCap,
                TargetQueuedInstances = 0,
                TargetQueuedHierarchyGameObjects = 0,
                MaxEvictions = int.MaxValue,
            };
        }

        static string FormatResult(string operation, ViewElementPoolTrimResult result)
        {
            return
                $"{operation}: queued {result.BeforeQueuedInstances}/" +
                $"{result.BeforeQueuedHierarchyGameObjects} -> " +
                $"{result.AfterQueuedInstances}/{result.AfterQueuedHierarchyGameObjects}, " +
                $"evicted={result.EvictedInstances}/{result.EvictedHierarchyGameObjects}, " +
                $"blocked(nestedUnique={result.BlockedNestedUnique}, " +
                $"ineligible={result.BlockedIneligible}, tooYoung={result.BlockedTooYoung}), " +
                $"stale={result.SkippedStale}, budgetSatisfied={result.BudgetSatisfied}, " +
                $"remainingOverBudget={result.RemainingOverBudgetInstances}/" +
                $"{result.RemainingOverBudgetHierarchyGameObjects}.";
        }
    }
}
