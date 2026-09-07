using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using System.Linq;
namespace MacacaGames.ViewSystem
{
    [CustomEditor(typeof(ViewElementRuntimePool))]
    public class ViewElementRuntimePoolEditor : Editor
    {
        private ViewElementRuntimePool runtimePool = null;

        void OnEnable()
        {
            runtimePool = (ViewElementRuntimePool)target;

        }
        void OnDisable()
        {
        }
        public override void OnInspectorGUI()
        {
            var snapshot = runtimePool.GetGlobalPoolSnapshot(refreshNestedUniqueSafety: false);

            GUILayout.Label(
                $"Global Queue: {snapshot.LogicalQueuedInstances} instances / " +
                $"{snapshot.LogicalQueuedHierarchyGameObjects} GameObjects");
            GUILayout.Label(
                $"Pending Recovery: {snapshot.PendingRecoveryInstances} instances / " +
                $"{snapshot.PendingRecoveryHierarchyGameObjects} GameObjects");
            GUILayout.Label(
                $"Pending Destroy: {snapshot.PendingDestroyInstances} instances / " +
                $"{snapshot.PendingDestroyHierarchyGameObjects} GameObjects");

            var entriesBySource = snapshot.QueuedEntries
                .GroupBy(entry => entry.SourceKey)
                .ToDictionary(group => group.Key, group => group.ToList());
            foreach (int sourceKey in snapshot.SourceKeys)
            {
                entriesBySource.TryGetValue(sourceKey, out var sourceEntries);
                sourceEntries ??= new List<ViewElementRuntimePoolSnapshot.QueuedEntry>();
                string sourceName = sourceEntries.FirstOrDefault()?.SourceName;
                if (string.IsNullOrEmpty(sourceName) && snapshot.SourceNames.TryGetValue(sourceKey, out var knownName))
                {
                    sourceName = knownName;
                }
                if (string.IsNullOrEmpty(sourceName))
                {
                    sourceName = "ID:" + sourceKey;
                }

                int hierarchyGameObjects = sourceEntries.Sum(entry => entry.HierarchyGameObjectCount);
                GUILayout.Label(
                    $"{sourceName} ({sourceKey}) : {sourceEntries.Count} / " +
                    $"{hierarchyGameObjects} GameObjects");
            }

            GUILayout.Label($"Recovery Queue Status");

            foreach (int instanceId in snapshot.PendingRecoveryInstanceIds)
            {
                var viewElement = EditorUtility.InstanceIDToObject(instanceId) as ViewElement;
                if (viewElement == null)
                {
                    continue;
                }
                if (GUILayout.Button(viewElement.name))
                {
                    EditorGUIUtility.PingObject(viewElement.gameObject);
                }
            }

        }

        public string TryGetPoolNameByInstanceId(int id)
        {
            var snapshot = runtimePool.GetGlobalPoolSnapshot(refreshNestedUniqueSafety: false);
            if (snapshot.SourceNames.TryGetValue(id, out var sourceName) && !string.IsNullOrEmpty(sourceName))
            {
                return sourceName;
            }

            var entry = snapshot.QueuedEntries
                .FirstOrDefault(item => item.SourceKey == id);
            return string.IsNullOrEmpty(entry?.SourceName)
                ? "ID:" + id
                : entry.SourceName;
        }
    }
}
