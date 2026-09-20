using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    public static class ViewElementRuntimePoolDiagnostics
    {
        const string MenuRoot = "MacacaGames/ViewSystem/Diagnostics/Runtime Pool/";
        const int MaxEntryDetails = 50;

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
            string eligibilityDetails = FormatEligibilityDetails(snapshot);
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
                $"autoTrimGrace={snapshot.AutoTrimGraceSeconds:0.###}s, " +
                $"idleTrimAfter={snapshot.IdleTrimAfterSeconds:0.###}s, " +
                $"poolMiss={snapshot.PoolMissCount}, instantiate={snapshot.InstantiateCount} " +
                $"({snapshot.InstantiateMilliseconds:0.###}ms), trims={snapshot.TrimCount}." +
                eligibilityDetails,
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

            ViewElementRuntimePoolSnapshot snapshot = runtimePool.GetGlobalPoolSnapshot(
                ViewElementGlobalEvictionMode.EmergencyHardCap,
                minimumIdleSeconds: 0,
                refreshNestedUniqueSafety: true);
            ViewElementPoolTrimResult result = runtimePool.TrimQueuedPool(CreateEmergencyRequest(true));
            Debug.Log(
                FormatResult("Dry-run", result) + FormatEligibilityDetails(snapshot),
                runtimePool);
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

            ViewElementRuntimePoolSnapshot snapshot = runtimePool.GetGlobalPoolSnapshot(
                ViewElementGlobalEvictionMode.EmergencyHardCap,
                minimumIdleSeconds: 0,
                refreshNestedUniqueSafety: true);
            ViewElementPoolTrimResult result = runtimePool.TrimQueuedPool(CreateEmergencyRequest(false));
            Debug.Log(
                FormatResult("Aggressive clear", result) + FormatEligibilityDetails(snapshot),
                runtimePool);
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
                $"projectedAfter={result.ProjectedAfterQueuedInstances}/" +
                $"{result.ProjectedAfterQueuedHierarchyGameObjects}, " +
                $"evicted={result.EvictedInstances}/{result.EvictedHierarchyGameObjects}, " +
                $"blocked(nestedUnique={result.BlockedNestedUnique}, " +
                $"ineligible={result.BlockedIneligible}, tooYoung={result.BlockedTooYoung}), " +
                $"stale={result.SkippedStale}, budgetSatisfied={result.BudgetSatisfied}, " +
                $"remainingOverBudget={result.RemainingOverBudgetInstances}/" +
                $"{result.RemainingOverBudgetHierarchyGameObjects}.";
        }

        static string FormatEligibilityDetails(ViewElementRuntimePoolSnapshot snapshot)
        {
            if (snapshot.QueuedEntries.Count == 0)
            {
                return "\nEligibility reasons: none.";
            }

            string reasonSummary = string.Join(
                ", ",
                snapshot.QueuedEntries
                    .GroupBy(entry => entry.EligibilityReason)
                    .OrderBy(group => group.Key)
                    .Select(group => $"{group.Key}={group.Count()}"));
            string entries = string.Join(
                "\n",
                snapshot.QueuedEntries
                    .Take(MaxEntryDetails)
                    .Select(entry =>
                        $"- source={entry.SourceName}, instanceId={entry.InstanceId}, " +
                        $"hierarchyGO={entry.HierarchyGameObjectCount}, " +
                        $"nestedUnique={entry.NestedUniqueCount}, " +
                        $"nestedEmergencyOnly={entry.NestedEmergencyOnlyCount}, " +
                        $"policy={entry.RecoveryPolicy}({entry.RecoveryKeepCount}), " +
                        $"trimOnlyInEmergency={entry.TrimOnlyInEmergency}, " +
                        $"active={entry.IsActive}, pending={entry.IsPendingRecovery}, " +
                        $"eligible={entry.IsEligible}, reason={entry.EligibilityReason}"));
            int omitted = snapshot.QueuedEntries.Count - MaxEntryDetails;
            string omittedMessage = omitted > 0
                ? $"\n- ... {omitted} additional entries omitted"
                : string.Empty;

            return $"\nEligibility reasons ({snapshot.BudgetMode}): {reasonSummary}" +
                   $"\nQueued entry details:\n{entries}{omittedMessage}";
        }
    }
}
