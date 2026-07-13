using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MacacaGames.ViewSystem.Diagnostics
{
    [Serializable]
    public sealed class ViewElementPoolPolicyAdvisorReport
    {
        public int schemaVersion = 5;
        public string timestamp;
        public string analysisMode = "SelectedPrefabsStaticOnly";
        public string sourceSaveDataPath;
        public string runtimeObjectGraphPath;
        public string runtimeTimestamp;
        public string runtimeCurrentPage;
        public bool dryRun = true;
        public bool hasRuntimeTelemetry;
        public bool cancelled;
        public int scannedPrefabAssets;
        public int skippedWithoutRootViewElement;
        public int failedPrefabAssets;
        public int referencedViewPageItems;
        public int referencedUniqueElements;
        public int distinctReferencedPrefabs;
        public List<ViewElementPoolPolicyRuntimeSnapshot> runtimeSnapshots =
            new List<ViewElementPoolPolicyRuntimeSnapshot>();
        public List<string> errors = new List<string>();
        public List<ViewElementPoolPolicyAdvice> entries = new List<ViewElementPoolPolicyAdvice>();
        [NonSerialized] public string outputPath;
    }

    [Serializable]
    public sealed class ViewElementPoolPolicyRuntimeSnapshot
    {
        public string sourcePath;
        public string timestamp;
        public string currentPage;
        public int matchedPrefabs;
        public int stableIdentityMatches;
    }

    [Serializable]
    public sealed class ViewElementPoolPolicyRuntimeObservation
    {
        public string sourcePath;
        public string timestamp;
        public string currentPage;
        public bool matchedByStableIdentity;
        public int poolEntryCount;
        public int activeInstances;
        public int activeGameObjects;
        public int activeMonoBehaviours;
        public int queuedInstances;
        public int pendingRecoveryInstances;
        public int pendingRecoveryGameObjects;
        public int pendingRecoveryMonoBehaviours;
        public int queuedGameObjects;
        public int queuedMonoBehaviours;
        public int requestedPoolHandlerInstances;
        public int ownerAwareRequestedPoolInstances;
        public int destroyWithOwnerRequestedPoolInstances;
        public int trimmableInstances;
        public int trimmableGameObjects;
    }

    [Serializable]
    public sealed class ViewElementPoolPolicySignalEvidence
    {
        public string signalType;
        public string scriptPath;
        public int count;
        public List<int> lines = new List<int>();
    }

    [Serializable]
    public sealed class ViewElementPoolPolicyAdvice
    {
        public string prefabPath;
        public string prefabGuid;
        public string sourceName;
        public int referenceCount;
        public List<string> referenceLocations = new List<string>();
        public bool runtimeObserved;
        public bool runtimeMatchedByStableIdentity;
        public int runtimeObservedSnapshotCount;
        public bool runtimeMissingAfterObservation;
        public bool runtimeReturnedToZeroAfterObservation;
        public string runtimeFirstObservedTimestamp;
        public string runtimeLastObservedTimestamp;
        public int runtimePoolEntryCount;
        public int runtimeActiveInstances;
        public int runtimeActiveGameObjects;
        public int runtimeActiveMonoBehaviours;
        public int runtimeQueuedInstances;
        public int runtimePendingRecoveryInstances;
        public int runtimePendingRecoveryGameObjects;
        public int runtimePendingRecoveryMonoBehaviours;
        public int runtimeQueuedGameObjects;
        public int runtimeQueuedMonoBehaviours;
        public int runtimeRequestedPoolHandlerInstances;
        public int runtimeOwnerAwareRequestedPoolInstances;
        public int runtimeDestroyWithOwnerRequestedPoolInstances;
        public int runtimeTrimmableInstances;
        public int runtimeTrimmableGameObjects;
        public List<ViewElementPoolPolicyRuntimeObservation> runtimeObservations =
            new List<ViewElementPoolPolicyRuntimeObservation>();
        public string currentPolicy;
        public int currentKeepCount;
        public int gameObjects;
        public int monoBehaviours;
        public int graphics;
        public int selectables;
        public int layoutGroups;
        public int animators;
        public int nestedViewElements;
        public int uniqueViewElements;
        public int nestedUniqueViewElements;
        public int singletonComponents;
        public int legacyRequestedPoolConstructions;
        public int ownerAwarePoolConstructions;
        public int returnToGlobalPoolSignals;
        public int destroyWithOwnerSignals;
        public int useChildPolicySignals;
        public int asyncVoidMethods;
        public int awaitExpressions;
        public int lifetimeGuardSignals;
        public int eventAddSignals;
        public int eventRemoveSignals;
        public int lifetimeSubscriptionSignals;
        public int addListenerSignals;
        public int removeListenerSignals;
        public int unmatchedUnityEventListenerSignals;
        public int staticRetentionSignals;
        public int addressableOwnershipSignals;
        public int supportingStaticRetentionSignals;
        public int supportingAddressableOwnershipSignals;
        public string targetPolicy;
        public int targetKeepCount;
        public string migrationStatus;
        public List<string> safetyBlockers = new List<string>();
        public string nextAction;
        public float policyConfidence;
        public float safetyConfidence;
        // Schema v4 compatibility aliases. New consumers should use the fields above.
        public string classification;
        public string recommendedPolicy;
        public int recommendedKeepCount;
        public float confidence;
        public bool safeToApplyAutomatically;
        public List<string> reasons = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> inspectedScripts = new List<string>();
        public List<string> supportingComponentScripts = new List<string>();
        public List<ViewElementPoolPolicySignalEvidence> signalEvidence =
            new List<ViewElementPoolPolicySignalEvidence>();
    }

    public static class ViewElementPoolPolicyAdvisor
    {
        static readonly Regex LegacyPoolRegex = new Regex(@"\bnew\s+ViewElementRequestedPool\s*\(");
        static readonly Regex OwnerAwarePoolRegex = new Regex(
            @"\b(?:Lifetime|_lifetime)\s*\.\s*CreatePool\s*\(");
        static readonly Regex ReturnToGlobalPoolRegex = new Regex(
            @"\bViewElementChildRecoveryMode\s*\.\s*ReturnToGlobalPool\b");
        static readonly Regex DestroyWithOwnerRegex = new Regex(
            @"\bViewElementChildRecoveryMode\s*\.\s*DestroyWithOwner\b");
        static readonly Regex UseChildPolicyRegex = new Regex(
            @"\bViewElementChildRecoveryMode\s*\.\s*UseChildPolicy\b");
        static readonly Regex AsyncVoidRegex = new Regex(@"\basync\s+void\s+\w+\s*\(");
        static readonly Regex AwaitRegex = new Regex(@"\bawait\b");
        static readonly Regex LifetimeGuardRegex = new Regex(
            @"\b(?:Lifetime|_lifetime)\s*\.\s*(?:IsAlive|Token|ThrowIfDisposed)|\bIsCurrent(?:Binding|Refresh|Lifetime)\s*\(");
        static readonly Regex EventAddRegex = new Regex(@"(?<!\+)\+=");
        static readonly Regex EventRemoveRegex = new Regex(@"(?<!-)\-=");
        static readonly Regex LifetimeSubscribeRegex = new Regex(@"\b(?:Lifetime|_lifetime)\s*\.\s*Subscribe\s*<");
        static readonly Regex AddListenerRegex = new Regex(@"\.AddListener\s*\(");
        static readonly Regex RemoveListenerRegex = new Regex(@"\.(?:RemoveListener|RemoveAllListeners)\s*\(");
        static readonly Regex StaticRetentionRegex = new Regex(
            @"\bstatic\s+(?:(?:readonly|volatile)\s+)*(?:event\s+)?" +
            @"(?:(?:System\.)?(?:Action(?:\s*<[^;\n>]+>)?|Func\s*<[^;\n>]+>)|" +
            @"(?:Component|MonoBehaviour|ViewElement)\b|" +
            @"(?:Dictionary|List|HashSet)\s*<[^;\n>]*(?:Component|MonoBehaviour|ViewElement)[^;\n>]*>)");
        static readonly Regex AddressableRegex = new Regex(@"\b(?:AssetReference\w*|Addressables\s*\.)");
        static readonly Regex LifetimeOwnerSourceRegex = new Regex(
            @"\bViewElementLifetimeScope\b|\b(?:Lifetime|_lifetime)\s*\.\s*(?:CreatePool|Subscribe)\s*[<(]|\bnew\s+ViewElementRequestedPool\s*\(");

        internal static ViewElementPoolPolicyAdvisorReport AnalyzePrefabs(
            IReadOnlyList<string> paths,
            string analysisMode,
            bool warnIfRootViewElementMissing)
        {
            var report = new ViewElementPoolPolicyAdvisorReport
            {
                timestamp = DateTime.Now.ToString("o"),
                analysisMode = analysisMode,
            };

            try
            {
                for (int index = 0; index < paths.Count; index++)
                {
                    string path = paths[index];
                    if (paths.Count > 1 && EditorUtility.DisplayCancelableProgressBar(
                            "ViewSystem Pool Policy Advisor",
                            path,
                            (float)index / paths.Count))
                    {
                        report.cancelled = true;
                        break;
                    }

                    report.scannedPrefabAssets++;
                    try
                    {
                        var advice = AnalyzePrefab(path, warnIfRootViewElementMissing);
                        if (advice != null)
                        {
                            report.entries.Add(advice);
                        }
                        else
                        {
                            report.skippedWithoutRootViewElement++;
                        }
                    }
                    catch (Exception exception)
                    {
                        report.failedPrefabAssets++;
                        report.errors.Add($"{path}: {exception.GetType().Name}: {exception.Message}");
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            report.outputPath = WriteReport(report);

            Debug.Log(
                $"[ViewSystemAdvisor] Static-only report: {report.outputPath}\n" +
                $"analyzed={report.entries.Count}, scanned={report.scannedPrefabAssets}, " +
                $"skipped={report.skippedWithoutRootViewElement}, failed={report.failedPrefabAssets}, " +
                $"cancelled={report.cancelled}, autoApplicable=0");
            return report;
        }

        internal static string WriteReport(ViewElementPoolPolicyAdvisorReport report, string outputPath = null)
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "MemoryLeakReports");
            Directory.CreateDirectory(outputDirectory);
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    outputDirectory,
                    $"viewsystem_pool_policy_advisor_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
            }

            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            return outputPath;
        }

        static ViewElementPoolPolicyAdvice AnalyzePrefab(string prefabPath, bool warnIfRootViewElementMissing)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var rootViewElement = root.GetComponent<ViewElement>();
                if (rootViewElement == null)
                {
                    if (warnIfRootViewElementMissing)
                    {
                        Debug.LogWarning($"[ViewSystemAdvisor] Root ViewElement not found: {prefabPath}");
                    }

                    return null;
                }

                var transforms = root.GetComponentsInChildren<Transform>(true);
                var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(component => component != null)
                    .ToArray();
                var viewElements = root.GetComponentsInChildren<ViewElement>(true);
                var advice = new ViewElementPoolPolicyAdvice
                {
                    prefabPath = prefabPath,
                    prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath),
                    sourceName = root.name,
                    currentPolicy = rootViewElement.recoveryPolicy.ToString(),
                    currentKeepCount = rootViewElement.recoveryKeepCount,
                    gameObjects = transforms.Length,
                    monoBehaviours = behaviours.Length,
                    graphics = root.GetComponentsInChildren<Graphic>(true).Length,
                    selectables = root.GetComponentsInChildren<Selectable>(true).Length,
                    layoutGroups = root.GetComponentsInChildren<LayoutGroup>(true).Length,
                    animators = root.GetComponentsInChildren<Animator>(true).Length,
                    nestedViewElements = Math.Max(0, viewElements.Length - 1),
                    uniqueViewElements = viewElements.Count(viewElement => viewElement.IsUnique),
                    nestedUniqueViewElements = viewElements.Count(viewElement =>
                        viewElement != rootViewElement && viewElement.IsUnique),
                    singletonComponents = behaviours.Count(component => component is IViewElementSingleton),
                };

                InspectScripts(
                    behaviours.Where(component =>
                        component.GetType().Assembly.GetName().Name != "Macaca.ViewSystem"),
                    advice);
                BuildRecommendation(advice);
                return advice;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void InspectScripts(IEnumerable<MonoBehaviour> behaviours, ViewElementPoolPolicyAdvice advice)
        {
            var scripts = behaviours
                .Select(component => new
                {
                    component,
                    script = MonoScript.FromMonoBehaviour(component),
                })
                .Where(item => item.script != null)
                .GroupBy(item => AssetDatabase.GetAssetPath(item.script))
                .Where(group =>
                    !string.IsNullOrEmpty(group.Key) &&
                    group.Key.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(group => new
                {
                    path = group.Key,
                    script = group.First().script,
                    components = group.Select(item => item.component).ToList(),
                })
                .OrderBy(item => item.path)
                .ToList();

            foreach (var inspectedScript in scripts)
            {
                string source = inspectedScript.script.text;
                bool isLifetimeOwner = inspectedScript.components.Any(component =>
                                           component is IViewElementLifeCycle ||
                                           component is IViewElementSingleton ||
                                           component is ViewElementBehaviour) ||
                                       LifetimeOwnerSourceRegex.IsMatch(source);
                if (!isLifetimeOwner)
                {
                    advice.supportingComponentScripts.Add(inspectedScript.path);
                    advice.supportingStaticRetentionSignals += AddEvidence(
                        advice,
                        "SupportingStaticRetention",
                        inspectedScript.path,
                        source,
                        StaticRetentionRegex);
                    advice.supportingAddressableOwnershipSignals += AddEvidence(
                        advice,
                        "SupportingAddressableOwnership",
                        inspectedScript.path,
                        source,
                        AddressableRegex);
                    continue;
                }

                advice.inspectedScripts.Add(inspectedScript.path);
                advice.legacyRequestedPoolConstructions += AddEvidence(
                    advice, "LegacyRequestedPool", inspectedScript.path, source, LegacyPoolRegex);
                advice.ownerAwarePoolConstructions += AddEvidence(
                    advice, "OwnerAwareRequestedPool", inspectedScript.path, source, OwnerAwarePoolRegex);
                advice.returnToGlobalPoolSignals += AddEvidence(
                    advice, "ReturnToGlobalPool", inspectedScript.path, source, ReturnToGlobalPoolRegex);
                advice.destroyWithOwnerSignals += AddEvidence(
                    advice, "DestroyWithOwner", inspectedScript.path, source, DestroyWithOwnerRegex);
                advice.useChildPolicySignals += AddEvidence(
                    advice, "UseChildPolicy", inspectedScript.path, source, UseChildPolicyRegex);
                advice.asyncVoidMethods += AddEvidence(
                    advice, "AsyncVoid", inspectedScript.path, source, AsyncVoidRegex);
                advice.awaitExpressions += AddEvidence(
                    advice, "Await", inspectedScript.path, source, AwaitRegex);
                advice.lifetimeGuardSignals += AddEvidence(
                    advice, "LifetimeGuard", inspectedScript.path, source, LifetimeGuardRegex);
                advice.eventAddSignals += AddEvidence(
                    advice, "EventAdd", inspectedScript.path, source, EventAddRegex);
                advice.eventRemoveSignals += AddEvidence(
                    advice, "EventRemove", inspectedScript.path, source, EventRemoveRegex);
                advice.lifetimeSubscriptionSignals += AddEvidence(
                    advice, "LifetimeSubscribe", inspectedScript.path, source, LifetimeSubscribeRegex);
                advice.addListenerSignals += AddEvidence(
                    advice, "AddListener", inspectedScript.path, source, AddListenerRegex);
                advice.removeListenerSignals += AddEvidence(
                    advice, "RemoveListener", inspectedScript.path, source, RemoveListenerRegex);
                advice.staticRetentionSignals += AddEvidence(
                    advice, "StaticRetention", inspectedScript.path, source, StaticRetentionRegex);
                advice.addressableOwnershipSignals += AddEvidence(
                    advice, "AddressableOwnership", inspectedScript.path, source, AddressableRegex);
            }
        }

        static int AddEvidence(
            ViewElementPoolPolicyAdvice advice,
            string signalType,
            string scriptPath,
            string source,
            Regex regex)
        {
            var matches = regex.Matches(source);
            if (matches.Count == 0)
            {
                return 0;
            }

            var evidence = new ViewElementPoolPolicySignalEvidence
            {
                signalType = signalType,
                scriptPath = scriptPath,
                count = matches.Count,
            };
            foreach (Match match in matches)
            {
                evidence.lines.Add(GetLineNumber(source, match.Index));
            }

            advice.signalEvidence.Add(evidence);
            return matches.Count;
        }

        static int GetLineNumber(string source, int characterIndex)
        {
            int line = 1;
            for (int index = 0; index < characterIndex; index++)
            {
                if (source[index] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        static void BuildRecommendation(ViewElementPoolPolicyAdvice advice)
        {
            advice.safeToApplyAutomatically = false;
            AddSignalWarnings(advice);
            SetStaticTargetPolicy(advice);

            if (advice.uniqueViewElements > 0 || advice.singletonComponents > 0)
            {
                advice.migrationStatus = "Pinned";
                advice.targetPolicy = ViewElementRecoveryPolicy.KeepForever.ToString();
                advice.targetKeepCount = 0;
                advice.policyConfidence = 0.95f;
                advice.safetyConfidence = 0.95f;
                advice.safetyBlockers.Add("UniqueOrSingletonOwnership");
                advice.nextAction = "Preserve the unique/singleton ownership contract; do not apply ordinary parent destruction.";
                advice.reasons.Add(
                    $"Contains unique ViewElements={advice.uniqueViewElements}, singleton components={advice.singletonComponents}.");
                AddUnsafeCurrentPolicyWarning(advice);
            }
            else
            {
                if (advice.legacyRequestedPoolConstructions > 0)
                {
                    advice.safetyBlockers.Add("OwnerlessRequestedPool");
                    advice.reasons.Add(
                        $"Found {advice.legacyRequestedPoolConstructions} ownerless ViewElementRequestedPool construction(s).");
                }

                if (advice.staticRetentionSignals > 0)
                {
                    advice.safetyBlockers.Add("StaticCallbackOrCacheOwnership");
                    advice.reasons.Add(
                        $"Found {advice.staticRetentionSignals} static callback/cache retention signal(s).");
                }

                bool eventCleanupRisk =
                    advice.eventAddSignals > advice.eventRemoveSignals + advice.lifetimeSubscriptionSignals;
                if (eventCleanupRisk)
                {
                    advice.safetyBlockers.Add("UnmanagedEventSubscription");
                    advice.reasons.Add("C# event subscription signals exceed visible cleanup/lifetime registrations.");
                }

                if (advice.awaitExpressions > 0 && advice.lifetimeGuardSignals == 0)
                {
                    advice.safetyBlockers.Add("AsyncWithoutLifetimeGuard");
                    advice.reasons.Add(
                        $"Found await expressions={advice.awaitExpressions} without recognizable lifetime/binding guards.");
                }

                if (advice.safetyBlockers.Count > 0)
                {
                    advice.migrationStatus = IsMigratedPolicy(advice)
                        ? "NeedsCodeReview"
                        : "NeedsCodeMigration";
                    advice.safetyConfidence = 0.25f;
                    advice.nextAction =
                        "Use an agent to inspect the cited ownership paths and resolve concrete blockers before policy application.";
                    AddUnsafeCurrentPolicyWarning(advice);
                }
                else if (IsMigratedPolicy(advice))
                {
                    advice.migrationStatus = "NeedsRuntimeValidation";
                    advice.safetyConfidence = 0.65f;
                    advice.nextAction = "Preserve the current migrated policy and validate open, return, and reopen snapshots.";
                }
                else if (advice.targetPolicy == "Undetermined")
                {
                    advice.migrationStatus = "InsufficientEvidence";
                    advice.safetyConfidence = 0.55f;
                    advice.nextAction = "Collect runtime cost and usage evidence before choosing a target policy.";
                }
                else
                {
                    advice.migrationStatus = "NeedsRuntimeEvidence";
                    advice.safetyConfidence = 0.55f;
                    advice.nextAction = "Collect open, return, and reopen snapshots, then request an agent ownership audit.";
                }
            }

            advice.warnings.Add("Static-only analysis cannot prove event, async, callback, or Addressable safety.");
            SyncLegacyDecisionFields(advice);
        }

        static void SetStaticTargetPolicy(ViewElementPoolPolicyAdvice advice)
        {
            advice.targetKeepCount = 0;
            if (IsMigratedPolicy(advice))
            {
                advice.targetPolicy = advice.currentPolicy;
                advice.targetKeepCount = advice.currentKeepCount;
                advice.policyConfidence = 0.90f;
                advice.reasons.Add("Current non-default recovery policy is treated as an intentional migration and will not be reverted by static analysis.");
            }
            else if (advice.gameObjects >= 150)
            {
                advice.targetPolicy = ViewElementRecoveryPolicy.DestroyOnRecovery.ToString();
                advice.policyConfidence = 0.65f;
                advice.reasons.Add("Large hierarchy; provisional DestroyOnRecovery target pending runtime frequency and reopen evidence.");
            }
            else if (advice.gameObjects >= 50)
            {
                advice.targetPolicy = ViewElementRecoveryPolicy.KeepN.ToString();
                advice.targetKeepCount = 1;
                advice.policyConfidence = 0.55f;
                advice.reasons.Add("Medium hierarchy; provisional KeepN(1) target pending runtime frequency data.");
            }
            else
            {
                advice.targetPolicy = "Undetermined";
                advice.policyConfidence = 0.35f;
                advice.reasons.Add("Small static hierarchy; retained-memory benefit and target policy are not established.");
            }
        }

        internal static bool IsMigratedPolicy(ViewElementPoolPolicyAdvice advice)
        {
            return advice.currentPolicy != ViewElementRecoveryPolicy.KeepForever.ToString();
        }

        internal static void SyncLegacyDecisionFields(ViewElementPoolPolicyAdvice advice)
        {
            advice.classification = advice.migrationStatus;
            advice.recommendedPolicy = advice.targetPolicy;
            advice.recommendedKeepCount = advice.targetKeepCount;
            advice.confidence = advice.policyConfidence;
        }

        static void AddSignalWarnings(ViewElementPoolPolicyAdvice advice)
        {
            if (advice.staticRetentionSignals > 0)
            {
                advice.warnings.Add(
                    $"Found {advice.staticRetentionSignals} static retention signal(s); manual ownership review required.");
            }

            if (advice.addressableOwnershipSignals > 0)
            {
                advice.warnings.Add(
                    $"Found {advice.addressableOwnershipSignals} Addressable ownership signal(s); handle lifetime is not proven.");
            }

            if (advice.supportingStaticRetentionSignals > 0)
            {
                advice.warnings.Add(
                    $"Supporting component scripts contain {advice.supportingStaticRetentionSignals} static retention signal(s); " +
                    "reported as non-blocking evidence.");
            }

            if (advice.supportingAddressableOwnershipSignals > 0)
            {
                advice.warnings.Add(
                    $"Supporting component scripts contain {advice.supportingAddressableOwnershipSignals} Addressable signal(s); " +
                    "manual ownership review is still required.");
            }

            if (advice.asyncVoidMethods > 0)
            {
                advice.warnings.Add(
                    $"Found {advice.asyncVoidMethods} async void method(s); exception and lifetime handling require manual review.");
            }

            advice.unmatchedUnityEventListenerSignals = Math.Max(
                0,
                advice.addListenerSignals -
                advice.removeListenerSignals -
                advice.lifetimeSubscriptionSignals);
            if (advice.unmatchedUnityEventListenerSignals > 0)
            {
                advice.warnings.Add(
                    $"Found {advice.unmatchedUnityEventListenerSignals} unmatched UnityEvent AddListener signal(s). " +
                    "This is non-blocking because the UnityEvent owner may be destroyed with the same hierarchy.");
            }

            if (advice.currentPolicy == ViewElementRecoveryPolicy.DestroyOnRecovery.ToString() &&
                advice.returnToGlobalPoolSignals > 0)
            {
                advice.warnings.Add(
                    "Current parent policy destroys on recovery, but child pools explicitly return to the global pool.");
            }
        }

        static void AddUnsafeCurrentPolicyWarning(ViewElementPoolPolicyAdvice advice)
        {
            if (advice.currentPolicy == ViewElementRecoveryPolicy.DestroyOnRecovery.ToString())
            {
                advice.warnings.Add(
                    "Current policy is DestroyOnRecovery, but static safety checks found a blocking condition.");
            }
        }

    }
}
