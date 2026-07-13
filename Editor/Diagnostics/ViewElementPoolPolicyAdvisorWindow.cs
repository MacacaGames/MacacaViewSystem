using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MacacaGames.ViewSystem.Diagnostics
{
    public sealed class ViewElementPoolPolicyAdvisorWindow : EditorWindow
    {
        const string DefaultSaveDataPath = "Assets/ViewSystemResources/ViewSystemData.asset";

        enum SortColumn
        {
            Name,
            Classification,
            CurrentPolicy,
            Recommendation,
            GameObjects,
            References,
            Confidence,
            RuntimeQueuedGameObjects,
            Warnings,
        }

        [SerializeField] ViewSystemSaveDataBase saveData;
        [SerializeField] string searchText = string.Empty;
        [SerializeField] string classificationFilter = "All";
        [SerializeField] SortColumn sortColumn = SortColumn.Name;
        [SerializeField] bool sortAscending = true;

        [NonSerialized] ViewElementPoolPolicyAdvisorReport report;
        [NonSerialized] ViewElementPoolPolicyAdvice selectedAdvice;
        [NonSerialized] Vector2 resultScroll;
        [NonSerialized] Vector2 detailScroll;

        [MenuItem("MacacaGames/ViewSystem/Diagnostics/Pool Policy Advisor")]
        public static void ShowWindow()
        {
            var window = GetWindow<ViewElementPoolPolicyAdvisorWindow>(false, "ViewSystem Advisor", true);
            window.minSize = new Vector2(1020f, 520f);
            window.Show();
        }

        void OnEnable()
        {
            if (saveData == null)
            {
                saveData = FindDefaultSaveData();
            }
        }

        void OnGUI()
        {
            DrawToolbar();

            if (saveData == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a ViewSystemSaveData or ViewSystemSaveData_Addressable asset before running analysis.",
                    MessageType.Info);
                return;
            }

            DrawSummary();
            DrawFilters();
            DrawResults();
            DrawDetails();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                saveData = (ViewSystemSaveDataBase)EditorGUILayout.ObjectField(
                    saveData,
                    typeof(ViewSystemSaveDataBase),
                    false,
                    GUILayout.MinWidth(260f));
                if (EditorGUI.EndChangeCheck())
                {
                    ClearResults();
                }

                EditorGUI.BeginDisabledGroup(
                    saveData == null || EditorApplication.isPlayingOrWillChangePlaymode);
                if (GUILayout.Button("Analyze", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    EditorApplication.delayCall += RunAnalysis;
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(report == null);
                if (GUILayout.Button("Add Runtime", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    ImportRuntimeReport();
                }

                if (report.hasRuntimeTelemetry &&
                    GUILayout.Button("Clear Runtime", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    ClearRuntimeTelemetry();
                }

                if (GUILayout.Button("Export Results", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    ExportResults();
                }

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    ClearResults();
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();
                if (report != null && !string.IsNullOrEmpty(report.outputPath) &&
                    GUILayout.Button("Reveal JSON", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    EditorUtility.RevealInFinder(report.outputPath);
                }
            }
        }

        void DrawSummary()
        {
            string saveDataPath = AssetDatabase.GetAssetPath(saveData);
            if (report == null)
            {
                EditorGUILayout.HelpBox(
                    $"Source: {saveDataPath}\nOnly prefabs referenced by this SaveData will be analyzed.",
                    MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                $"Source: {report.sourceSaveDataPath}\n" +
                $"Referenced prefabs: {report.distinctReferencedPrefabs}  |  " +
                $"Page/state items: {report.referencedViewPageItems}  |  " +
                $"Unique elements: {report.referencedUniqueElements}  |  " +
                $"Analyzed: {report.entries.Count}  |  Failed: {report.failedPrefabAssets}  |  " +
                $"Runtime matched: {report.entries.Count(entry => entry.runtimeObserved)}  |  " +
                $"Snapshots: {report.runtimeSnapshots.Count}  |  " +
                $"Dry run: {report.dryRun}" +
                (report.hasRuntimeTelemetry
                        ? $"\nLatest runtime snapshot: {report.runtimeTimestamp}  |  Page: {report.runtimeCurrentPage}"
                    : string.Empty),
                report.failedPrefabAssets > 0 ? MessageType.Warning : MessageType.Info);
        }

        void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Filter", GUILayout.Width(35f));
                searchText = GUILayout.TextField(searchText ?? string.Empty, EditorStyles.toolbarSearchField);

                var classifications = GetClassifications();
                int currentIndex = Math.Max(0, Array.IndexOf(classifications, classificationFilter));
                int nextIndex = EditorGUILayout.Popup(
                    currentIndex,
                    classifications,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(190f));
                classificationFilter = classifications[nextIndex];
            }
        }

        void DrawResults()
        {
            DrawResultHeader();
            float resultHeight = Math.Max(150f, position.height * 0.42f);
            using (var scroll = new EditorGUILayout.ScrollViewScope(resultScroll, GUILayout.Height(resultHeight)))
            {
                resultScroll = scroll.scrollPosition;
                foreach (var advice in GetVisibleEntries())
                {
                    DrawResultRow(advice);
                }
            }
        }

        void DrawResultHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawSortButton("Prefab", SortColumn.Name, 230f);
                DrawSortButton("Migration status", SortColumn.Classification, 175f);
                DrawSortButton("Current", SortColumn.CurrentPolicy, 115f);
                DrawSortButton("Target policy", SortColumn.Recommendation, 130f);
                DrawSortButton("GO", SortColumn.GameObjects, 45f);
                DrawSortButton("Refs", SortColumn.References, 45f);
                DrawSortButton("Conf.", SortColumn.Confidence, 55f);
                DrawSortButton("Max Q GO", SortColumn.RuntimeQueuedGameObjects, 75f);
                DrawSortButton("Warn", SortColumn.Warnings, 45f);
            }
        }

        void DrawSortButton(string label, SortColumn column, float width)
        {
            string suffix = sortColumn == column ? (sortAscending ? " ▲" : " ▼") : string.Empty;
            if (GUILayout.Button(label + suffix, EditorStyles.toolbarButton, GUILayout.Width(width)))
            {
                if (sortColumn == column)
                {
                    sortAscending = !sortAscending;
                }
                else
                {
                    sortColumn = column;
                    sortAscending = true;
                }
            }
        }

        void DrawResultRow(ViewElementPoolPolicyAdvice advice)
        {
            bool selected = selectedAdvice == advice;
            var rowStyle = new GUIStyle(selected ? "SelectionRect" : "CN EntryBackEven");
            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                GUILayout.Label(advice.sourceName, GUILayout.Width(230f));
                GUILayout.Label(advice.migrationStatus, GetClassificationStyle(advice.migrationStatus), GUILayout.Width(175f));
                GUILayout.Label(FormatPolicy(advice.currentPolicy, advice.currentKeepCount), GUILayout.Width(115f));
                GUILayout.Label(FormatPolicy(advice.targetPolicy, advice.targetKeepCount), GUILayout.Width(130f));
                GUILayout.Label(advice.gameObjects.ToString(), GUILayout.Width(45f));
                GUILayout.Label(advice.referenceCount.ToString(), GUILayout.Width(45f));
                GUILayout.Label(advice.policyConfidence.ToString("0.00"), GUILayout.Width(55f));
                GUILayout.Label(
                    advice.runtimeObserved ? advice.runtimeQueuedGameObjects.ToString() : "—",
                    GUILayout.Width(75f));
                GUILayout.Label(advice.warnings.Count.ToString(), GUILayout.Width(45f));
            }

            Rect rowRect = GUILayoutUtility.GetLastRect();
            Event current = Event.current;
            if (current.type == EventType.MouseDown && rowRect.Contains(current.mousePosition))
            {
                selectedAdvice = advice;
                if (current.clickCount == 2)
                {
                    PingPrefab(advice.prefabPath);
                }

                current.Use();
                Repaint();
            }
        }

        void DrawDetails()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
            if (selectedAdvice == null)
            {
                EditorGUILayout.HelpBox("Select a result to inspect reasons, warnings and source evidence.", MessageType.None);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(detailScroll))
            {
                detailScroll = scroll.scrollPosition;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(selectedAdvice.prefabPath, EditorStyles.boldLabel);
                    if (GUILayout.Button("Ping Prefab", GUILayout.Width(90f)))
                    {
                        PingPrefab(selectedAdvice.prefabPath);
                    }
                }

                DrawStringList("Referenced by", selectedAdvice.referenceLocations, MessageType.None);
                DrawStringList("Reasons", selectedAdvice.reasons, MessageType.Info);
                DrawStringList("Safety blockers", selectedAdvice.safetyBlockers, MessageType.Error);
                DrawStringList("Warnings", selectedAdvice.warnings, MessageType.Warning);
                EditorGUILayout.HelpBox(
                    $"Target: {FormatPolicy(selectedAdvice.targetPolicy, selectedAdvice.targetKeepCount)}\n" +
                    $"Migration status: {selectedAdvice.migrationStatus}\n" +
                    $"Policy confidence: {selectedAdvice.policyConfidence:0.00}, " +
                    $"safety confidence: {selectedAdvice.safetyConfidence:0.00}\n" +
                    $"Next action: {selectedAdvice.nextAction}",
                    MessageType.None);

                EditorGUILayout.LabelField("Runtime telemetry", EditorStyles.boldLabel);
                if (!selectedAdvice.runtimeObserved)
                {
                    EditorGUILayout.LabelField("Not observed in the imported snapshot.");
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Stable identity: {selectedAdvice.runtimeMatchedByStableIdentity}\n" +
                        $"Observed snapshots: {selectedAdvice.runtimeObservedSnapshotCount}/" +
                        $"{report.runtimeSnapshots.Count}, missing later: " +
                        $"{selectedAdvice.runtimeMissingAfterObservation}, returned to zero: " +
                        $"{selectedAdvice.runtimeReturnedToZeroAfterObservation}\n" +
                        $"Peak pool entries: {selectedAdvice.runtimePoolEntryCount}, " +
                        $"active: {selectedAdvice.runtimeActiveInstances}, " +
                        $"queued: {selectedAdvice.runtimeQueuedInstances}, " +
                        $"pending: {selectedAdvice.runtimePendingRecoveryInstances}\n" +
                        $"Peak hierarchy GO — active: {selectedAdvice.runtimeActiveGameObjects}, " +
                        $"queued: {selectedAdvice.runtimeQueuedGameObjects}, " +
                        $"pending: {selectedAdvice.runtimePendingRecoveryGameObjects}\n" +
                        $"Queued MonoBehaviours: {selectedAdvice.runtimeQueuedMonoBehaviours}, " +
                        $"trimmable GO: {selectedAdvice.runtimeTrimmableGameObjects}\n" +
                        $"Requested pools: {selectedAdvice.runtimeRequestedPoolHandlerInstances}, " +
                        $"owner-aware: {selectedAdvice.runtimeOwnerAwareRequestedPoolInstances}, " +
                        $"DestroyWithOwner: {selectedAdvice.runtimeDestroyWithOwnerRequestedPoolInstances}",
                        MessageType.Info);

                    foreach (var observation in selectedAdvice.runtimeObservations)
                    {
                        EditorGUILayout.LabelField(
                            $"{observation.timestamp} [{observation.currentPage}]  " +
                            $"A {observation.activeInstances}/{observation.activeGameObjects} GO  " +
                            $"Q {observation.queuedInstances}/{observation.queuedGameObjects} GO  " +
                            $"P {observation.pendingRecoveryInstances}/" +
                            $"{observation.pendingRecoveryGameObjects} GO");
                    }
                }

                EditorGUILayout.LabelField("Signal evidence", EditorStyles.boldLabel);
                if (selectedAdvice.signalEvidence.Count == 0)
                {
                    EditorGUILayout.LabelField("None");
                }
                else
                {
                    foreach (var evidence in selectedAdvice.signalEvidence)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                $"{evidence.signalType} ({evidence.count})",
                                GUILayout.Width(190f));
                            string lines = string.Join(", ", evidence.lines);
                            if (GUILayout.Button(
                                    $"{evidence.scriptPath}:{lines}",
                                    EditorStyles.linkLabel))
                            {
                                OpenScript(evidence);
                            }
                        }
                    }
                }
            }
        }

        static void DrawStringList(string title, IReadOnlyList<string> values, MessageType messageType)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (values == null || values.Count == 0)
            {
                EditorGUILayout.LabelField("None");
                return;
            }

            foreach (string value in values)
            {
                EditorGUILayout.HelpBox(value, messageType);
            }
        }

        void RunAnalysis()
        {
            if (saveData == null)
            {
                return;
            }

            var references = ViewSystemSaveDataPrefabResolver.Resolve(saveData);
            report = ViewElementPoolPolicyAdvisor.AnalyzePrefabs(
                references.Paths,
                "ViewSystemSaveDataStaticOnly",
                false);
            report.sourceSaveDataPath = AssetDatabase.GetAssetPath(saveData);
            report.referencedViewPageItems = references.ViewPageItemReferences;
            report.referencedUniqueElements = references.UniqueElementReferences;
            report.distinctReferencedPrefabs = references.Paths.Count;

            foreach (var advice in report.entries)
            {
                if (!references.Locations.TryGetValue(advice.prefabPath, out var locations))
                {
                    continue;
                }

                advice.referenceLocations.AddRange(locations);
                advice.referenceCount = locations.Count;
            }

            ViewElementPoolPolicyAdvisor.WriteReport(report, report.outputPath);
            selectedAdvice = report.entries.FirstOrDefault();
            Repaint();
        }

        void ExportResults()
        {
            string defaultName = $"viewsystem_pool_policy_advisor_{DateTime.Now:yyyyMMdd_HHmmss}";
            string path = EditorUtility.SaveFilePanel("Export ViewSystem Advisor Results", string.Empty, defaultName, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            ViewElementPoolPolicyAdvisor.WriteReport(report, path);
            report.outputPath = path;
        }

        void ImportRuntimeReport()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string defaultDirectory = Path.Combine(projectRoot, "MemoryLeakReports");
            string path = EditorUtility.OpenFilePanel(
                "Import ViewSystem Object Graph",
                defaultDirectory,
                "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            ViewSystemObjectGraphReport runtimeReport;
            try
            {
                runtimeReport = JsonUtility.FromJson<ViewSystemObjectGraphReport>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Import failed", exception.Message, "OK");
                return;
            }

            if (runtimeReport?.poolEntries == null)
            {
                EditorUtility.DisplayDialog("Import failed", "The selected JSON is not a ViewSystem object graph report.", "OK");
                return;
            }

            MergeRuntimeReport(runtimeReport, path);
            ViewElementPoolPolicyAdvisor.WriteReport(report, report.outputPath);
            Repaint();
        }

        void MergeRuntimeReport(ViewSystemObjectGraphReport runtimeReport, string path)
        {
            report.runtimeSnapshots.RemoveAll(snapshot =>
                string.Equals(snapshot.sourcePath, path, StringComparison.OrdinalIgnoreCase));
            foreach (var advice in report.entries)
            {
                advice.runtimeObservations.RemoveAll(observation =>
                    string.Equals(observation.sourcePath, path, StringComparison.OrdinalIgnoreCase));
            }

            var byGuid = report.entries
                .Where(entry => !string.IsNullOrEmpty(entry.prefabGuid))
                .ToDictionary(entry => entry.prefabGuid);
            var byPath = report.entries.ToDictionary(entry => entry.prefabPath);
            var byUniqueName = report.entries
                .GroupBy(entry => entry.sourceName)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single());
            var observations = new Dictionary<
                ViewElementPoolPolicyAdvice,
                ViewElementPoolPolicyRuntimeObservation>();

            foreach (var poolEntry in runtimeReport.poolEntries)
            {
                ViewElementPoolPolicyAdvice advice = null;
                bool stableIdentity = false;
                if (!string.IsNullOrEmpty(poolEntry.prefabGuid) &&
                    byGuid.TryGetValue(poolEntry.prefabGuid, out advice))
                {
                    stableIdentity = true;
                }
                else if (!string.IsNullOrEmpty(poolEntry.prefabPath) &&
                         byPath.TryGetValue(poolEntry.prefabPath, out advice))
                {
                    stableIdentity = true;
                }
                else if (!string.IsNullOrEmpty(poolEntry.sourceName))
                {
                    byUniqueName.TryGetValue(poolEntry.sourceName, out advice);
                }

                if (advice == null)
                {
                    continue;
                }

                if (!observations.TryGetValue(advice, out var observation))
                {
                    observation = new ViewElementPoolPolicyRuntimeObservation
                    {
                        sourcePath = path,
                        timestamp = runtimeReport.timestamp,
                        currentPage = runtimeReport.currentPage,
                        matchedByStableIdentity = true,
                    };
                    observations.Add(advice, observation);
                }

                observation.matchedByStableIdentity &= stableIdentity;
                observation.poolEntryCount++;
                observation.activeInstances += poolEntry.activeInstances;
                observation.activeGameObjects += poolEntry.activeGameObjects;
                observation.activeMonoBehaviours += poolEntry.activeMonoBehaviours;
                observation.queuedInstances += poolEntry.queuedInstances;
                observation.pendingRecoveryInstances += poolEntry.pendingRecoveryInstances;
                observation.pendingRecoveryGameObjects += poolEntry.pendingRecoveryGameObjects;
                observation.pendingRecoveryMonoBehaviours += poolEntry.pendingRecoveryMonoBehaviours;
                observation.queuedGameObjects += poolEntry.queuedGameObjects;
                observation.queuedMonoBehaviours += poolEntry.queuedMonoBehaviours;
                observation.requestedPoolHandlerInstances += poolEntry.requestedPoolHandlerInstances;
                observation.ownerAwareRequestedPoolInstances += poolEntry.ownerAwareRequestedPoolInstances;
                observation.destroyWithOwnerRequestedPoolInstances +=
                    poolEntry.destroyWithOwnerRequestedPoolInstances;
                observation.trimmableInstances += poolEntry.trimmableInstances;
                observation.trimmableGameObjects += poolEntry.trimmableGameObjects;
            }

            foreach (var pair in observations)
            {
                pair.Key.runtimeObservations.Add(pair.Value);
            }

            report.runtimeSnapshots.Add(new ViewElementPoolPolicyRuntimeSnapshot
            {
                sourcePath = path,
                timestamp = runtimeReport.timestamp,
                currentPage = runtimeReport.currentPage,
                matchedPrefabs = observations.Count,
                stableIdentityMatches = observations.Count(pair => pair.Value.matchedByStableIdentity),
            });
            report.runtimeObjectGraphPath = path;
            report.runtimeTimestamp = runtimeReport.timestamp;
            report.runtimeCurrentPage = runtimeReport.currentPage;
            RecalculateRuntimeTelemetry();
        }

        void RecalculateRuntimeTelemetry()
        {
            foreach (var advice in report.entries)
            {
                ResetRuntimeTelemetry(advice);
                if (advice.runtimeObservations.Count == 0)
                {
                    continue;
                }

                advice.runtimeObserved = true;
                advice.runtimeObservedSnapshotCount = advice.runtimeObservations.Count;
                advice.runtimeMatchedByStableIdentity = advice.runtimeObservations.All(
                    observation => observation.matchedByStableIdentity);
                bool observedActivity = false;
                foreach (var snapshot in report.runtimeSnapshots)
                {
                    var observation = advice.runtimeObservations.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate.sourcePath,
                            snapshot.sourcePath,
                            StringComparison.OrdinalIgnoreCase));
                    if (observation == null)
                    {
                        advice.runtimeMissingAfterObservation |= observedActivity;
                        continue;
                    }

                    int instanceCount = observation.activeInstances + observation.queuedInstances +
                                        observation.pendingRecoveryInstances;
                    if (observedActivity && instanceCount == 0)
                    {
                        advice.runtimeReturnedToZeroAfterObservation = true;
                    }

                    observedActivity |= instanceCount > 0;
                }
                advice.runtimeFirstObservedTimestamp = advice.runtimeObservations.First().timestamp;
                advice.runtimeLastObservedTimestamp = advice.runtimeObservations.Last().timestamp;
                advice.runtimePoolEntryCount = advice.runtimeObservations.Max(x => x.poolEntryCount);
                advice.runtimeActiveInstances = advice.runtimeObservations.Max(x => x.activeInstances);
                advice.runtimeActiveGameObjects = advice.runtimeObservations.Max(x => x.activeGameObjects);
                advice.runtimeActiveMonoBehaviours = advice.runtimeObservations.Max(x => x.activeMonoBehaviours);
                advice.runtimeQueuedInstances = advice.runtimeObservations.Max(x => x.queuedInstances);
                advice.runtimePendingRecoveryInstances = advice.runtimeObservations.Max(
                    x => x.pendingRecoveryInstances);
                advice.runtimePendingRecoveryGameObjects = advice.runtimeObservations.Max(
                    x => x.pendingRecoveryGameObjects);
                advice.runtimePendingRecoveryMonoBehaviours = advice.runtimeObservations.Max(
                    x => x.pendingRecoveryMonoBehaviours);
                advice.runtimeQueuedGameObjects = advice.runtimeObservations.Max(x => x.queuedGameObjects);
                advice.runtimeQueuedMonoBehaviours = advice.runtimeObservations.Max(x => x.queuedMonoBehaviours);
                advice.runtimeRequestedPoolHandlerInstances = advice.runtimeObservations.Max(
                    x => x.requestedPoolHandlerInstances);
                advice.runtimeOwnerAwareRequestedPoolInstances = advice.runtimeObservations.Max(
                    x => x.ownerAwareRequestedPoolInstances);
                advice.runtimeDestroyWithOwnerRequestedPoolInstances = advice.runtimeObservations.Max(
                    x => x.destroyWithOwnerRequestedPoolInstances);
                advice.runtimeTrimmableInstances = advice.runtimeObservations.Max(x => x.trimmableInstances);
                advice.runtimeTrimmableGameObjects = advice.runtimeObservations.Max(x => x.trimmableGameObjects);
                ApplyRuntimeRecommendation(advice);
            }

            report.hasRuntimeTelemetry = report.runtimeSnapshots.Count > 0;
        }

        static void ApplyRuntimeRecommendation(ViewElementPoolPolicyAdvice advice)
        {
            if (!advice.runtimeMatchedByStableIdentity)
            {
                advice.warnings.Add(
                    "Runtime telemetry was matched by unique source name because the object graph had no prefab GUID/path.");
            }

            if (ViewElementPoolPolicyAdvisor.IsMigratedPolicy(advice))
            {
                advice.reasons.Add(
                    $"Runtime snapshots preserved current target {advice.currentPolicy}; " +
                    $"peak active hierarchy GO={advice.runtimeActiveGameObjects}, " +
                    $"returned to zero={advice.runtimeReturnedToZeroAfterObservation}, " +
                    $"missing after observation={advice.runtimeMissingAfterObservation}.");
                ViewElementPoolPolicyAdvisor.SyncLegacyDecisionFields(advice);
                return;
            }

            if (advice.safetyBlockers.Count > 0)
            {
                advice.reasons.Add(
                    $"Runtime snapshots observed peak active hierarchy GO={advice.runtimeActiveGameObjects} and " +
                    $"queued hierarchy GO={advice.runtimeQueuedGameObjects}; target policy remains blocked by code ownership review.");
                ViewElementPoolPolicyAdvisor.SyncLegacyDecisionFields(advice);
                return;
            }

            if (advice.runtimeQueuedGameObjects >= 150)
            {
                advice.targetPolicy = ViewElementRecoveryPolicy.DestroyOnRecovery.ToString();
                advice.targetKeepCount = 0;
                advice.policyConfidence = Math.Max(advice.policyConfidence, 0.75f);
                advice.reasons.Add(
                    $"Runtime snapshot retained {advice.runtimeQueuedGameObjects} queued GameObjects; usage frequency and reopen cost are still required.");
            }
            else if (advice.runtimeQueuedGameObjects >= 50)
            {
                advice.targetPolicy = ViewElementRecoveryPolicy.KeepN.ToString();
                advice.targetKeepCount = 1;
                advice.policyConfidence = Math.Max(advice.policyConfidence, 0.65f);
                advice.reasons.Add(
                    $"Runtime snapshot retained {advice.runtimeQueuedGameObjects} queued GameObjects; provisional KeepN(1).");
            }
            else
            {
                advice.policyConfidence = Math.Max(advice.policyConfidence, 0.55f);
                advice.reasons.Add(
                    $"Runtime snapshots observed peak active={advice.runtimeActiveInstances} " +
                    $"({advice.runtimeActiveGameObjects} hierarchy GO), queued={advice.runtimeQueuedInstances} " +
                    $"({advice.runtimeQueuedGameObjects} hierarchy GO), " +
                    $"pending={advice.runtimePendingRecoveryInstances}; " +
                    $"missing after observation={advice.runtimeMissingAfterObservation}, " +
                    $"returned to zero={advice.runtimeReturnedToZeroAfterObservation}.");
            }

            ViewElementPoolPolicyAdvisor.SyncLegacyDecisionFields(advice);
        }

        static void ResetRuntimeTelemetry(ViewElementPoolPolicyAdvice advice)
        {
            advice.reasons.RemoveAll(reason => reason.StartsWith("Runtime snapshot", StringComparison.Ordinal));
            advice.warnings.RemoveAll(warning => warning.StartsWith("Runtime telemetry", StringComparison.Ordinal));

            if (ViewElementPoolPolicyAdvisor.IsMigratedPolicy(advice))
            {
                advice.targetPolicy = advice.currentPolicy;
                advice.targetKeepCount = advice.currentKeepCount;
                advice.policyConfidence = 0.90f;
            }
            else if (advice.gameObjects >= 150)
            {
                advice.targetPolicy = ViewElementRecoveryPolicy.DestroyOnRecovery.ToString();
                advice.targetKeepCount = 0;
                advice.policyConfidence = 0.65f;
            }
            else if (advice.gameObjects >= 50)
            {
                advice.targetPolicy = ViewElementRecoveryPolicy.KeepN.ToString();
                advice.targetKeepCount = 1;
                advice.policyConfidence = 0.55f;
            }
            else
            {
                advice.targetPolicy = "Undetermined";
                advice.targetKeepCount = 0;
                advice.policyConfidence = 0.35f;
            }

            ViewElementPoolPolicyAdvisor.SyncLegacyDecisionFields(advice);

            advice.runtimeObserved = false;
            advice.runtimeMatchedByStableIdentity = false;
            advice.runtimeObservedSnapshotCount = 0;
            advice.runtimeMissingAfterObservation = false;
            advice.runtimeReturnedToZeroAfterObservation = false;
            advice.runtimeFirstObservedTimestamp = string.Empty;
            advice.runtimeLastObservedTimestamp = string.Empty;
            advice.runtimePoolEntryCount = 0;
            advice.runtimeActiveInstances = 0;
            advice.runtimeActiveGameObjects = 0;
            advice.runtimeActiveMonoBehaviours = 0;
            advice.runtimeQueuedInstances = 0;
            advice.runtimePendingRecoveryInstances = 0;
            advice.runtimePendingRecoveryGameObjects = 0;
            advice.runtimePendingRecoveryMonoBehaviours = 0;
            advice.runtimeQueuedGameObjects = 0;
            advice.runtimeQueuedMonoBehaviours = 0;
            advice.runtimeRequestedPoolHandlerInstances = 0;
            advice.runtimeOwnerAwareRequestedPoolInstances = 0;
            advice.runtimeDestroyWithOwnerRequestedPoolInstances = 0;
            advice.runtimeTrimmableInstances = 0;
            advice.runtimeTrimmableGameObjects = 0;
        }

        void ClearRuntimeTelemetry()
        {
            foreach (var advice in report.entries)
            {
                advice.runtimeObservations.Clear();
                ResetRuntimeTelemetry(advice);
            }

            report.runtimeSnapshots.Clear();
            report.hasRuntimeTelemetry = false;
            report.runtimeObjectGraphPath = string.Empty;
            report.runtimeTimestamp = string.Empty;
            report.runtimeCurrentPage = string.Empty;
            ViewElementPoolPolicyAdvisor.WriteReport(report, report.outputPath);
            Repaint();
        }

        void ClearResults()
        {
            report = null;
            selectedAdvice = null;
            Repaint();
        }

        string[] GetClassifications()
        {
            if (report == null)
            {
                return new[] { "All" };
            }

            return new[] { "All" }
                .Concat(report.entries.Select(entry => entry.migrationStatus).Distinct().OrderBy(value => value))
                .ToArray();
        }

        IEnumerable<ViewElementPoolPolicyAdvice> GetVisibleEntries()
        {
            if (report == null)
            {
                return Enumerable.Empty<ViewElementPoolPolicyAdvice>();
            }

            IEnumerable<ViewElementPoolPolicyAdvice> entries = report.entries;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                entries = entries.Where(entry =>
                    entry.sourceName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.prefabPath.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!string.IsNullOrEmpty(classificationFilter) && classificationFilter != "All")
            {
                entries = entries.Where(entry => entry.migrationStatus == classificationFilter);
            }

            Func<ViewElementPoolPolicyAdvice, IComparable> selector = sortColumn switch
            {
                SortColumn.Classification => entry => entry.migrationStatus,
                SortColumn.CurrentPolicy => entry => entry.currentPolicy,
                SortColumn.Recommendation => entry => entry.targetPolicy,
                SortColumn.GameObjects => entry => entry.gameObjects,
                SortColumn.References => entry => entry.referenceCount,
                SortColumn.Confidence => entry => entry.policyConfidence,
                SortColumn.RuntimeQueuedGameObjects => entry => entry.runtimeQueuedGameObjects,
                SortColumn.Warnings => entry => entry.warnings.Count,
                _ => entry => entry.sourceName,
            };
            return sortAscending
                ? entries.OrderBy(selector).ThenBy(entry => entry.sourceName)
                : entries.OrderByDescending(selector).ThenBy(entry => entry.sourceName);
        }

        static GUIStyle GetClassificationStyle(string classification)
        {
            var style = new GUIStyle(EditorStyles.label);
            if (classification == "Validated")
            {
                style.normal.textColor = new Color(0.25f, 0.7f, 0.3f);
            }
            else if (classification == "NeedsRuntimeEvidence" ||
                     classification == "NeedsRuntimeValidation" ||
                     classification == "InsufficientEvidence")
            {
                style.normal.textColor = new Color(0.85f, 0.65f, 0.20f);
            }
            else
            {
                style.normal.textColor = new Color(0.95f, 0.55f, 0.15f);
            }

            return style;
        }

        static string FormatPolicy(string policy, int keepCount)
        {
            return policy == ViewElementRecoveryPolicy.KeepN.ToString()
                ? $"{policy}({keepCount})"
                : policy;
        }

        static void PingPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
        }

        static void OpenScript(ViewElementPoolPolicySignalEvidence evidence)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(evidence.scriptPath);
            if (script != null)
            {
                AssetDatabase.OpenAsset(script, evidence.lines.FirstOrDefault());
            }
        }

        static ViewSystemSaveDataBase FindDefaultSaveData()
        {
            var direct = AssetDatabase.LoadAssetAtPath<ViewSystemSaveDataBase>(DefaultSaveDataPath);
            if (direct != null)
            {
                return direct;
            }

            return AssetDatabase.FindAssets("t:ViewSystemSaveData")
                .Concat(AssetDatabase.FindAssets("t:ViewSystemSaveData_Addressable"))
                .Distinct()
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ViewSystemSaveDataBase>)
                .FirstOrDefault(asset => asset != null);
        }
    }

    static class ViewSystemSaveDataPrefabResolver
    {
        internal sealed class Result
        {
            public readonly List<string> Paths = new List<string>();
            public readonly Dictionary<string, List<string>> Locations =
                new Dictionary<string, List<string>>();
            public int ViewPageItemReferences;
            public int UniqueElementReferences;
        }

        internal static Result Resolve(ViewSystemSaveDataBase saveData)
        {
            var result = new Result();
            if (saveData is ViewSystemSaveData directSaveData)
            {
                ResolveDirect(directSaveData, result);
            }
            else if (saveData is ViewSystemSaveData_Addressable addressableSaveData)
            {
                ResolveAddressable(addressableSaveData, result);
            }

            result.Paths.AddRange(result.Locations.Keys.OrderBy(path => path));
            return result;
        }

        static void ResolveDirect(ViewSystemSaveData saveData, Result result)
        {
            var pages = saveData.viewPagesNodeSaveDatas != null && saveData.viewPagesNodeSaveDatas.Count > 0
                ? saveData.viewPagesNodeSaveDatas
                    .Where(node => node != null && node.data?.viewPage != null)
                    .Select(node => node.data)
                : (saveData.viewPages ?? new List<ViewPageSaveData>())
                    .Where(data => data?.viewPage != null);
            foreach (var pageData in pages)
            {
                AddItems(pageData.viewPage.viewPageItems, $"Page:{pageData.viewPage.name}", result);
            }

            var states = saveData.viewStateNodeSaveDatas != null && saveData.viewStateNodeSaveDatas.Count > 0
                ? saveData.viewStateNodeSaveDatas
                    .Where(node => node != null && node.data?.viewState != null)
                    .Select(node => node.data)
                : (saveData.viewStates ?? new List<ViewStateSaveData>())
                    .Where(data => data?.viewState != null);
            foreach (var stateData in states)
            {
                AddItems(stateData.viewState.viewPageItems, $"State:{stateData.viewState.name}", result);
            }

            foreach (var unique in saveData.uniqueViewElementTable ?? new List<UniqueViewElementTableData>())
            {
                if (unique?.viewElementGameObject == null)
                {
                    continue;
                }

                result.UniqueElementReferences++;
                AddGameObject(unique.viewElementGameObject, $"Unique:{unique.type}", result);
            }
        }

        static void ResolveAddressable(ViewSystemSaveData_Addressable saveData, Result result)
        {
            foreach (var item in saveData.viewPageItemAssetRefs ?? new List<ViewPageItemAssetRef>())
            {
                if (item?.assetReference == null || string.IsNullOrEmpty(item.assetReference.AssetGUID))
                {
                    continue;
                }

                result.ViewPageItemReferences++;
                AddPath(
                    AssetDatabase.GUIDToAssetPath(item.assetReference.AssetGUID),
                    $"AddressablePageItem:{item.viewPageItemId}",
                    result);
            }

            foreach (var unique in saveData.uniqueViewElementAssetRefs ?? new List<UniqueViewElementAssetRef>())
            {
                if (unique?.assetReference == null || string.IsNullOrEmpty(unique.assetReference.AssetGUID))
                {
                    continue;
                }

                result.UniqueElementReferences++;
                AddPath(
                    AssetDatabase.GUIDToAssetPath(unique.assetReference.AssetGUID),
                    $"AddressableUnique:{unique.type}",
                    result);
            }
        }

        static void AddItems(IEnumerable<ViewPageItem> items, string owner, Result result)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item?.viewElementObject == null)
                {
                    continue;
                }

                result.ViewPageItemReferences++;
                AddGameObject(item.viewElementObject, $"{owner}/{item.displayName}", result);
            }
        }

        static void AddGameObject(GameObject gameObject, string location, Result result)
        {
            AddPath(AssetDatabase.GetAssetPath(gameObject), location, result);
        }

        static void AddPath(string path, string location, Result result)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!result.Locations.TryGetValue(path, out var locations))
            {
                locations = new List<string>();
                result.Locations.Add(path, locations);
            }

            if (!locations.Contains(location))
            {
                locations.Add(location);
            }
        }
    }
}
