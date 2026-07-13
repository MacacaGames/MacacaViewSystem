using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MacacaGames.ViewSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MacacaGames.ViewSystem.Diagnostics
{
    [Serializable]
    public class ViewSystemObjectGraphReport
    {
        public string timestamp;
        public string currentPage;
        public int totalRuntimeGameObjects;
        public int viewControllerGameObjects;
        public int pageRootGameObjects;
        public int poolGameObjects;
        public int inactivePoolGameObjects;
        public int poolSourceCount;
        public int queuedPoolInstanceCount;
        public int pendingRecoveryCount;
        public int dryRunTrimmableInstanceCount;
        public int dryRunTrimmableGameObjects;
        public int dryRunBlockedByNestedUniqueCount;
        public int pageItemAssetReferenceCount;
        public int distinctPagePrefabKeyCount;
        public int validPagePrefabHandleCount;
        public int duplicateValidPagePrefabHandleCount;
        public List<ViewSystemHierarchyRoot> viewSystemRoots = new();
        public List<ViewSystemHierarchyRoot> sceneRoots = new();
        public List<ViewSystemPoolEntry> poolEntries = new();
        public List<ViewSystemHandleEntry> pagePrefabHandles = new();
    }

    [Serializable]
    public class ViewSystemPoolEntry
    {
        public int poolKey;
        public string sourceName;
        public string prefabPath;
        public string prefabGuid;
        public int queuedInstances;
        public int pendingRecoveryInstances;
        public int activeInstances;
        public int activeGameObjects;
        public int activeMonoBehaviours;
        public int pendingRecoveryGameObjects;
        public int pendingRecoveryMonoBehaviours;
        public int queuedGameObjects;
        public int queuedMonoBehaviours;
        public int nestedUniqueViewElements;
        public int requestedPoolHandlerInstances;
        public int queuedRequestedPoolHandlerInstances;
        public int ownerAwareRequestedPoolInstances;
        public int destroyWithOwnerRequestedPoolInstances;
        public int disposedRequestedPoolInstances;
        public List<string> activeInstancePaths = new();
        public List<string> queuedInstancePaths = new();
        public string recoveryPolicy;
        public int recoveryKeepCount;
        public int trimmableInstances;
        public int trimmableGameObjects;
        public string trimBlockReason;
    }

    [Serializable]
    public class ViewSystemHandleEntry
    {
        public string runtimeKey;
        public int pageItemReferenceCount;
        public int validHandleCount;
        public int completedHandleCount;
        public int succeededHandleCount;
        public int distinctLoadedPrefabCount;
        public bool hasDuplicateValidHandles;
    }

    [Serializable]
    public class ViewSystemHierarchyRoot
    {
        public string name;
        public string path;
        public string scene;
        public string category;
        public bool activeSelf;
        public bool activeInHierarchy;
        public int gameObjects;
        public int inactiveGameObjects;
        public int transforms;
        public int rectTransforms;
        public int monoBehaviours;
        public int viewElements;
        public int uniqueViewElements;
        public int graphics;
        public int selectables;
        public int layoutGroups;
        public int animators;
    }

    public static class ViewSystemObjectGraphDiagnostics
    {
        [MenuItem("MacacaGames/ViewSystem/Diagnostics/Dump Object Graph")]
        private static void DumpObjectGraph()
        {
            if (!EditorApplication.isPlaying || ViewController.Instance == null)
            {
                Debug.LogWarning("[ViewSystemGraph] Enter Play Mode and wait for ViewController before dumping.");
                return;
            }

            var report = Capture();
            var outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "MemoryLeakReports");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(
                outputDirectory,
                $"viewsystem_object_graph_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));

            Debug.Log(
                $"[ViewSystemGraph] Report: {outputPath}\n" +
                $"runtimeGO={report.totalRuntimeGameObjects}, " +
                $"viewControllerGO={report.viewControllerGameObjects}, " +
                $"pageRootGO={report.pageRootGameObjects}, " +
                $"poolGO={report.poolGameObjects}, inactivePoolGO={report.inactivePoolGameObjects}, " +
                $"dryRunTrimGO={report.dryRunTrimmableGameObjects}, " +
                $"validPageHandles={report.validPagePrefabHandleCount}, " +
                $"duplicateValidHandles={report.duplicateValidPagePrefabHandleCount}");
            EditorUtility.RevealInFinder(outputPath);
        }

        private static ViewSystemObjectGraphReport Capture()
        {
            var controller = ViewController.Instance;
            var pageRoot = controller.GetPageRootTransform();
            var poolRoot = controller.viewElementPool != null
                ? controller.viewElementPool.transform
                : null;

            var runtimeObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(IsRuntimeSceneObject)
                .ToList();

            var report = new ViewSystemObjectGraphReport
            {
                timestamp = DateTime.Now.ToString("o"),
                currentPage = controller.currentViewPage?.name ?? string.Empty,
                totalRuntimeGameObjects = runtimeObjects.Count,
                viewControllerGameObjects = CountGameObjects(controller.transform),
                pageRootGameObjects = CountGameObjects(pageRoot),
                poolGameObjects = CountGameObjects(poolRoot),
                inactivePoolGameObjects = CountInactiveGameObjects(poolRoot),
            };

            var viewRoots = new HashSet<Transform>();
            AddChildren(viewRoots, pageRoot);
            AddChildren(viewRoots, poolRoot);
            foreach (var child in controller.transform.Cast<Transform>())
            {
                if (child == pageRoot || child == poolRoot ||
                    IsDescendantOf(child, pageRoot) || IsDescendantOf(pageRoot, child)) continue;
                viewRoots.Add(child);
            }

            report.viewSystemRoots = viewRoots
                .Where(x => x != null)
                .Select(x => BuildEntry(x, GetCategory(x, pageRoot, poolRoot)))
                .OrderByDescending(x => x.gameObjects)
                .ToList();

            report.sceneRoots = runtimeObjects
                .Select(x => x.transform.root)
                .Distinct()
                .Select(x => BuildEntry(x, "SceneRoot"))
                .OrderByDescending(x => x.gameObjects)
                .ToList();

            CapturePoolDryRun(controller, report);
            CaptureHandleRegistry(controller, report);

            return report;
        }

        private static void CapturePoolDryRun(ViewController controller, ViewSystemObjectGraphReport report)
        {
            var runtimePool = ViewController.runtimePool;
            if (runtimePool == null) return;

            var poolField = typeof(ViewElementRuntimePool).GetField("veDicts", BindingFlags.Instance | BindingFlags.NonPublic);
            var recoveryField = typeof(ViewElementRuntimePool).GetField("recycleQueue", BindingFlags.Instance | BindingFlags.NonPublic);
            var requestedPoolHandlerField = typeof(ViewElement).GetField(
                "RequestedPoolRecoveryHandler",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var pools = poolField?.GetValue(runtimePool) as Dictionary<int, Queue<ViewElement>>;
            var recoveryQueue = recoveryField?.GetValue(runtimePool) as Queue<ViewElement>;
            if (pools == null) return;

            var pendingRecovery = recoveryQueue != null
                ? recoveryQueue.Where(x => x != null).ToList()
                : new List<ViewElement>();
            var pendingSet = new HashSet<int>(pendingRecovery.Select(x => x.GetInstanceID()));
            report.pendingRecoveryCount = pendingRecovery.Count;

            var runtimeViewElements = Resources.FindObjectsOfTypeAll<ViewElement>()
                .Where(x => x != null && x.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(x))
                .ToList();

            foreach (var pair in pools)
            {
                var queued = pair.Value.Where(x => x != null).ToList();
                var queuedSet = new HashSet<int>(queued.Select(x => x.GetInstanceID()));
                var allForKey = runtimeViewElements.Where(x => x.PoolKey == pair.Key).ToList();
                var activeInstances = allForKey
                    .Where(x => !queuedSet.Contains(x.GetInstanceID()) && !pendingSet.Contains(x.GetInstanceID()))
                    .ToList();
                var active = activeInstances.Count;
                var pendingInstances = pendingRecovery.Where(x => x.PoolKey == pair.Key).ToList();
                var pendingForKey = pendingInstances.Count;

                int activeGameObjects = activeInstances.Sum(
                    instance => instance.GetComponentsInChildren<Transform>(true).Length);
                int activeMonoBehaviours = activeInstances.Sum(
                    instance => instance.GetComponentsInChildren<MonoBehaviour>(true).Length);
                int pendingRecoveryGameObjects = pendingInstances.Sum(
                    instance => instance.GetComponentsInChildren<Transform>(true).Length);
                int pendingRecoveryMonoBehaviours = pendingInstances.Sum(
                    instance => instance.GetComponentsInChildren<MonoBehaviour>(true).Length);

                int queuedGameObjects = 0;
                int queuedMonoBehaviours = 0;
                int nestedUnique = 0;
                int requestedPoolHandlerInstances = 0;
                int queuedRequestedPoolHandlerInstances = 0;
                int ownerAwareRequestedPoolInstances = 0;
                int destroyWithOwnerRequestedPoolInstances = 0;
                int disposedRequestedPoolInstances = 0;
                int trimmableInstances = 0;
                int trimmableGameObjects = 0;
                string sourceName = queued.FirstOrDefault()?.name ?? allForKey.FirstOrDefault()?.name ?? $"PoolKey_{pair.Key}";
                ResolvePoolSourceIdentity(
                    pair.Key,
                    allForKey.Concat(queued),
                    out string prefabPath,
                    out string prefabGuid);

                foreach (var instance in allForKey)
                {
                    var handler = requestedPoolHandlerField?.GetValue(instance) as Delegate;
                    if (handler == null)
                    {
                        continue;
                    }

                    requestedPoolHandlerInstances++;
                    if (queuedSet.Contains(instance.GetInstanceID()))
                    {
                        queuedRequestedPoolHandlerInstances++;
                    }

                    if (handler.Target is ViewElementRequestedPool requestedPool)
                    {
                        if (requestedPool.IsOwnerAware)
                        {
                            ownerAwareRequestedPoolInstances++;
                        }

                        if (requestedPool.ChildRecoveryMode == ViewElementChildRecoveryMode.DestroyWithOwner)
                        {
                            destroyWithOwnerRequestedPoolInstances++;
                        }

                        if (requestedPool.IsDisposed)
                        {
                            disposedRequestedPoolInstances++;
                        }
                    }
                }

                foreach (var instance in queued)
                {
                    int gameObjects = instance.GetComponentsInChildren<Transform>(true).Length;
                    int behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true).Length;
                    int nestedUniqueForInstance = instance
                        .GetComponentsInChildren<ViewElement>(true)
                        .Count(x => x != instance && x.IsUnique);

                    queuedGameObjects += gameObjects;
                    queuedMonoBehaviours += behaviours;
                    nestedUnique += nestedUniqueForInstance;

                    if (!instance.IsUnique && nestedUniqueForInstance == 0)
                    {
                        trimmableInstances++;
                        trimmableGameObjects += gameObjects;
                    }
                }

                report.poolEntries.Add(new ViewSystemPoolEntry
                {
                    poolKey = pair.Key,
                    sourceName = sourceName,
                    prefabPath = prefabPath,
                    prefabGuid = prefabGuid,
                    queuedInstances = queued.Count,
                    pendingRecoveryInstances = pendingForKey,
                    activeInstances = active,
                    activeGameObjects = activeGameObjects,
                    activeMonoBehaviours = activeMonoBehaviours,
                    pendingRecoveryGameObjects = pendingRecoveryGameObjects,
                    pendingRecoveryMonoBehaviours = pendingRecoveryMonoBehaviours,
                    queuedGameObjects = queuedGameObjects,
                    queuedMonoBehaviours = queuedMonoBehaviours,
                    nestedUniqueViewElements = nestedUnique,
                    requestedPoolHandlerInstances = requestedPoolHandlerInstances,
                    queuedRequestedPoolHandlerInstances = queuedRequestedPoolHandlerInstances,
                    ownerAwareRequestedPoolInstances = ownerAwareRequestedPoolInstances,
                    destroyWithOwnerRequestedPoolInstances = destroyWithOwnerRequestedPoolInstances,
                    disposedRequestedPoolInstances = disposedRequestedPoolInstances,
                    activeInstancePaths = activeInstances.Select(x => GetPath(x.transform)).ToList(),
                    queuedInstancePaths = queued.Select(x => GetPath(x.transform)).ToList(),
                    recoveryPolicy = queued.FirstOrDefault()?.recoveryPolicy.ToString() ??
                                     allForKey.FirstOrDefault()?.recoveryPolicy.ToString() ?? string.Empty,
                    recoveryKeepCount = queued.FirstOrDefault()?.recoveryKeepCount ??
                                        allForKey.FirstOrDefault()?.recoveryKeepCount ?? 0,
                    trimmableInstances = trimmableInstances,
                    trimmableGameObjects = trimmableGameObjects,
                    trimBlockReason = nestedUnique > 0 ? "ContainsNestedUniqueViewElement" : string.Empty,
                });
            }

            report.poolEntries = report.poolEntries
                .OrderByDescending(x => x.queuedGameObjects)
                .ToList();
            report.poolSourceCount = report.poolEntries.Count;
            report.queuedPoolInstanceCount = report.poolEntries.Sum(x => x.queuedInstances);
            report.dryRunTrimmableInstanceCount = report.poolEntries.Sum(x => x.trimmableInstances);
            report.dryRunTrimmableGameObjects = report.poolEntries.Sum(x => x.trimmableGameObjects);
            report.dryRunBlockedByNestedUniqueCount = report.poolEntries
                .Where(x => x.nestedUniqueViewElements > 0)
                .Sum(x => x.queuedInstances);
        }

        private static void ResolvePoolSourceIdentity(
            int poolKey,
            IEnumerable<ViewElement> runtimeInstances,
            out string prefabPath,
            out string prefabGuid)
        {
            prefabPath = string.Empty;
            prefabGuid = string.Empty;

            UnityEngine.Object source = EditorUtility.EntityIdToObject(poolKey);
            if (source is GameObject sourceGameObject)
            {
                source = sourceGameObject.GetComponent<ViewElement>();
            }

            if (source == null)
            {
                foreach (var instance in runtimeInstances.Where(instance => instance != null))
                {
                    source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                    if (source != null)
                    {
                        break;
                    }
                }
            }

            if (source == null)
            {
                return;
            }

            prefabPath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            }
        }

        private static void CaptureHandleRegistry(ViewController controller, ViewSystemObjectGraphReport report)
        {
            var lookupField = typeof(ViewController).GetField("_assetRefLookup", BindingFlags.Instance | BindingFlags.NonPublic);
            var lookup = lookupField?.GetValue(controller) as Dictionary<string, AssetReferenceGameObject>;
            if (lookup == null) return;

            report.pageItemAssetReferenceCount = lookup.Count;
            foreach (var group in lookup.Values
                         .Where(x => x != null && x.RuntimeKeyIsValid())
                         .GroupBy(x => x.RuntimeKey.ToString()))
            {
                int valid = 0;
                int completed = 0;
                int succeeded = 0;
                var loadedPrefabIds = new HashSet<int>();

                foreach (var assetReference in group)
                {
                    if (!assetReference.OperationHandle.IsValid()) continue;
                    valid++;
                    var handle = assetReference.OperationHandle.Convert<GameObject>();
                    if (!handle.IsDone) continue;
                    completed++;
                    if (handle.Status != AsyncOperationStatus.Succeeded) continue;
                    succeeded++;
                    if (handle.Result != null)
                        loadedPrefabIds.Add(handle.Result.GetInstanceID());
                }

                report.pagePrefabHandles.Add(new ViewSystemHandleEntry
                {
                    runtimeKey = group.Key,
                    pageItemReferenceCount = group.Count(),
                    validHandleCount = valid,
                    completedHandleCount = completed,
                    succeededHandleCount = succeeded,
                    distinctLoadedPrefabCount = loadedPrefabIds.Count,
                    hasDuplicateValidHandles = valid > 1,
                });
            }

            report.pagePrefabHandles = report.pagePrefabHandles
                .OrderByDescending(x => x.validHandleCount)
                .ThenByDescending(x => x.pageItemReferenceCount)
                .ToList();
            report.distinctPagePrefabKeyCount = report.pagePrefabHandles.Count;
            report.validPagePrefabHandleCount = report.pagePrefabHandles.Sum(x => x.validHandleCount);
            report.duplicateValidPagePrefabHandleCount = report.pagePrefabHandles.Sum(x => Mathf.Max(0, x.validHandleCount - 1));
        }

        private static void AddChildren(HashSet<Transform> result, Transform root)
        {
            if (root == null) return;
            foreach (Transform child in root)
                result.Add(child);
        }

        private static string GetCategory(Transform target, Transform pageRoot, Transform poolRoot)
        {
            if (target != null && target.parent == poolRoot) return "PoolRoot";
            if (target != null && target.parent == pageRoot) return "PageRoot";
            return "ViewControllerChild";
        }

        private static ViewSystemHierarchyRoot BuildEntry(Transform root, string category)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            var viewElements = root.GetComponentsInChildren<ViewElement>(true);
            return new ViewSystemHierarchyRoot
            {
                name = root.name,
                path = GetPath(root),
                scene = root.gameObject.scene.name,
                category = category,
                activeSelf = root.gameObject.activeSelf,
                activeInHierarchy = root.gameObject.activeInHierarchy,
                gameObjects = transforms.Length,
                inactiveGameObjects = transforms.Count(x => !x.gameObject.activeInHierarchy),
                transforms = transforms.Length,
                rectTransforms = transforms.Count(x => x is RectTransform),
                monoBehaviours = behaviours.Length,
                viewElements = viewElements.Length,
                uniqueViewElements = viewElements.Count(x => x.IsUnique),
                graphics = root.GetComponentsInChildren<Graphic>(true).Length,
                selectables = root.GetComponentsInChildren<Selectable>(true).Length,
                layoutGroups = root.GetComponentsInChildren<LayoutGroup>(true).Length,
                animators = root.GetComponentsInChildren<Animator>(true).Length,
            };
        }

        private static bool IsRuntimeSceneObject(GameObject gameObject)
        {
            return gameObject != null &&
                   gameObject.scene.IsValid() &&
                   !EditorUtility.IsPersistent(gameObject);
        }

        private static int CountGameObjects(Transform root)
        {
            return root == null ? 0 : root.GetComponentsInChildren<Transform>(true).Length;
        }

        private static int CountInactiveGameObjects(Transform root)
        {
            return root == null
                ? 0
                : root.GetComponentsInChildren<Transform>(true).Count(x => !x.gameObject.activeInHierarchy);
        }

        private static bool IsDescendantOf(Transform target, Transform possibleParent)
        {
            return target != null && possibleParent != null && target.IsChildOf(possibleParent);
        }

        private static string GetPath(Transform target)
        {
            var names = new Stack<string>();
            for (var current = target; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }
    }
}
