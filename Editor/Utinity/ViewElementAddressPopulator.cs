using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using MacacaGames.ViewSystem;

namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Backup asset that stores prefab references before they are cleared.
    /// Can be used to restore viewElementObject references if needed.
    /// </summary>
    public class ViewElementReferenceBackup : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string address;
            public GameObject prefab;
        }

        public List<Entry> entries = new List<Entry>();
    }

    public class ViewElementAddressPopulator : EditorWindow
    {
        private ViewSystemSaveData saveData;
        private bool clearDirectReferences = false;
        private bool autoCreateAddressable = true;
        private int selectedGroupIndex = 0;
        private Vector2 scrollPos;
        private List<PopulateResult> lastResults = new List<PopulateResult>();
        private float resultNameWidth = 250f;
        private bool isResizing = false;

        private struct PopulateResult
        {
            public string name;
            public string address;
            public string assetPath;
            public string status; // "ok", "created", "already_set", "no_prefab"
        }

        [MenuItem("MacacaGames/ViewSystem/ViewElement Address Populator")]
        public static void ShowWindow()
        {
            var win = GetWindow<ViewElementAddressPopulator>("ViewElement Address Populator");
            win.minSize = new Vector2(500, 400);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("ViewElement Address Populator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            saveData = (ViewSystemSaveData)EditorGUILayout.ObjectField(
                "ViewSystem SaveData", saveData, typeof(ViewSystemSaveData), false);

            EditorGUILayout.Space();

            // Auto create addressable option
            autoCreateAddressable = EditorGUILayout.ToggleLeft(
                "Auto-register unregistered Prefabs to Addressables",
                autoCreateAddressable);

            if (autoCreateAddressable)
            {
                EditorGUI.indentLevel++;
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    var groupNames = settings.groups
                        .Where(g => g != null)
                        .Select(g => g.Name)
                        .ToArray();

                    if (groupNames.Length > 0)
                    {
                        selectedGroupIndex = Mathf.Clamp(selectedGroupIndex, 0, groupNames.Length - 1);
                        selectedGroupIndex = EditorGUILayout.Popup("Target Group", selectedGroupIndex, groupNames);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No Addressable Groups found. Please create one in the Addressables Groups window first.", MessageType.Error);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Addressable Asset Settings not initialized.", MessageType.Error);
                }

                EditorGUILayout.HelpBox(
                    "Unregistered Prefabs will be added as Addressable entries when 'Populate' is executed.\n" +
                    "The address will use the Prefab's original name. 'Preview' does not make any changes.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            clearDirectReferences = EditorGUILayout.ToggleLeft(
                "Clear direct references (set viewElementObject = null) to break bundle dependencies",
                clearDirectReferences);

            if (clearDirectReferences)
            {
                EditorGUILayout.HelpBox(
                    "When enabled, viewElementObject will be set to null so the Prefab is no longer pulled in by the ViewSystemSaveData bundle.\n" +
                    "A backup asset will be created automatically before clearing, so you can restore later if needed.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(saveData == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Preview", GUILayout.Height(30)))
                {
                    lastResults = RunPopulate(preview: true);
                }
                if (GUILayout.Button("Populate", GUILayout.Height(30)))
                {
                    lastResults = RunPopulate(preview: false);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Restore button
                if (GUILayout.Button("Restore direct references from backup", GUILayout.Height(24)))
                {
                    RestoreFromBackup();
                }
            }

            EditorGUILayout.Space();

            if (lastResults.Count > 0)
            {
                DrawResultsTable();
            }
        }

        private void DrawResultsTable()
        {
            // Stats
            int okCount = lastResults.Count(r => r.status == "ok" || r.status == "created");
            int skipCount = lastResults.Count(r => r.status == "already_set");
            int nullCount = lastResults.Count(r => r.status == "no_prefab");
            EditorGUILayout.LabelField(
                $"Results: {lastResults.Count} items  |  Populated: {okCount}  |  Already set: {skipCount}  |  No Prefab: {nullCount}",
                EditorStyles.boldLabel);

            EditorGUILayout.Space(2);

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Status", GUILayout.Width(40));
            EditorGUILayout.LabelField("Prefab Name", GUILayout.Width(resultNameWidth));

            // Drag handle for resizing
            var resizeRect = GUILayoutUtility.GetRect(8, 20, GUILayout.Width(8));
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);
            HandleResize(resizeRect);

            EditorGUILayout.LabelField("Address / Status");
            EditorGUILayout.EndHorizontal();

            // Rows
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var r in lastResults)
            {
                EditorGUILayout.BeginHorizontal();

                // Status icon
                string icon;
                switch (r.status)
                {
                    case "ok": icon = "\u2705"; break;          // green check
                    case "created": icon = "\u2728"; break;      // sparkles (new)
                    case "already_set": icon = "\u23ED"; break;  // skip
                    case "no_prefab": icon = "\u274C"; break;    // red X
                    default: icon = "?"; break;
                }
                EditorGUILayout.LabelField(icon, GUILayout.Width(40));

                // Name (selectable so user can copy)
                EditorGUILayout.SelectableLabel(r.name, EditorStyles.label,
                    GUILayout.Width(resultNameWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));

                GUILayout.Space(8);

                // Address or status description
                string detail;
                switch (r.status)
                {
                    case "ok": detail = r.address; break;
                    case "created": detail = $"{r.address}  (new entry in {GetSelectedGroupName()})"; break;
                    case "already_set": detail = $"{r.address}  (already set)"; break;
                    case "no_prefab": detail = "No Prefab reference"; break;
                    default: detail = r.status; break;
                }
                EditorGUILayout.SelectableLabel(detail, EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void HandleResize(Rect resizeRect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition))
            {
                isResizing = true;
                e.Use();
            }
            if (isResizing)
            {
                if (e.type == EventType.MouseDrag)
                {
                    resultNameWidth = Mathf.Clamp(e.mousePosition.x - 50, 80, position.width - 200);
                    Repaint();
                    e.Use();
                }
                if (e.type == EventType.MouseUp)
                {
                    isResizing = false;
                    e.Use();
                }
            }
        }

        private string GetBackupPath()
        {
            var saveDataPath = AssetDatabase.GetAssetPath(saveData);
            var dir = System.IO.Path.GetDirectoryName(saveDataPath);
            var name = System.IO.Path.GetFileNameWithoutExtension(saveDataPath);
            return $"{dir}/{name}_ReferenceBackup.asset";
        }

        private void CreateBackup(List<ViewPageItem> allPageItems, List<UniqueViewElementTableData> uniqueTable)
        {
            var backupPath = GetBackupPath();

            var backup = AssetDatabase.LoadAssetAtPath<ViewElementReferenceBackup>(backupPath);
            if (backup == null)
            {
                backup = ScriptableObject.CreateInstance<ViewElementReferenceBackup>();
                AssetDatabase.CreateAsset(backup, backupPath);
            }

            backup.entries.Clear();
            var seen = new HashSet<string>();

            foreach (var item in allPageItems)
            {
                if (item.viewElementObject != null && !string.IsNullOrEmpty(item.viewElementAddress))
                {
                    if (seen.Add(item.viewElementAddress))
                    {
                        backup.entries.Add(new ViewElementReferenceBackup.Entry
                        {
                            address = item.viewElementAddress,
                            prefab = item.viewElementObject
                        });
                    }
                }
            }

            foreach (var entry in uniqueTable)
            {
                if (entry.viewElementGameObject != null && !string.IsNullOrEmpty(entry.viewElementAddress))
                {
                    if (seen.Add(entry.viewElementAddress))
                    {
                        backup.entries.Add(new ViewElementReferenceBackup.Entry
                        {
                            address = entry.viewElementAddress,
                            prefab = entry.viewElementGameObject
                        });
                    }
                }
            }

            EditorUtility.SetDirty(backup);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ViewElementAddressPopulator] Backup saved to {backupPath} ({backup.entries.Count} entries)");
        }

        private void RestoreFromBackup()
        {
            if (saveData == null) return;

            var backupPath = GetBackupPath();
            var backup = AssetDatabase.LoadAssetAtPath<ViewElementReferenceBackup>(backupPath);
            if (backup == null || backup.entries.Count == 0)
            {
                EditorUtility.DisplayDialog("Restore", "No backup found. Run Populate with 'Clear direct references' first.", "OK");
                return;
            }

            var lookup = new Dictionary<string, GameObject>();
            foreach (var e in backup.entries)
            {
                if (e.prefab != null && !string.IsNullOrEmpty(e.address))
                    lookup[e.address] = e.prefab;
            }

            int restored = 0;

            // Collect all ViewPageItems
            var allPageItems = CollectAllViewPageItems();
            foreach (var item in allPageItems)
            {
                if (item.viewElementObject == null && !string.IsNullOrEmpty(item.viewElementAddress)
                    && lookup.TryGetValue(item.viewElementAddress, out var prefab))
                {
                    item.viewElementObject = prefab;
                    restored++;
                }
            }

            foreach (var entry in saveData.uniqueViewElementTable)
            {
                if (entry.viewElementGameObject == null && !string.IsNullOrEmpty(entry.viewElementAddress)
                    && lookup.TryGetValue(entry.viewElementAddress, out var prefab))
                {
                    entry.viewElementGameObject = prefab;
                    restored++;
                }
            }

            EditorUtility.SetDirty(saveData);
            foreach (var node in saveData.viewPagesNodeSaveDatas)
                if (node != null) EditorUtility.SetDirty(node);
            foreach (var node in saveData.viewStateNodeSaveDatas)
                if (node != null) EditorUtility.SetDirty(node);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ViewElementAddressPopulator] Restored {restored} direct references from backup.");
            EditorUtility.DisplayDialog("Restore",
                $"Restored {restored} direct references from backup.\nYou can now disable 'Use Addressable Loading' to use the traditional approach.",
                "OK");
        }

        private List<ViewPageItem> CollectAllViewPageItems()
        {
            var allPageItems = new List<ViewPageItem>();
            foreach (var node in saveData.viewPagesNodeSaveDatas)
            {
                if (node != null && node.data?.viewPage?.viewPageItems != null)
                    allPageItems.AddRange(node.data.viewPage.viewPageItems);
            }
            foreach (var pageSave in saveData.viewPages)
            {
                if (pageSave?.viewPage?.viewPageItems != null)
                    allPageItems.AddRange(pageSave.viewPage.viewPageItems);
            }
            foreach (var node in saveData.viewStateNodeSaveDatas)
            {
                if (node != null && node.data?.viewState?.viewPageItems != null)
                    allPageItems.AddRange(node.data.viewState.viewPageItems);
            }
            foreach (var stateSave in saveData.viewStates)
            {
                if (stateSave?.viewState?.viewPageItems != null)
                    allPageItems.AddRange(stateSave.viewState.viewPageItems);
            }
            return allPageItems;
        }

        private List<PopulateResult> RunPopulate(bool preview)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Error", "Addressable Asset Settings not found.", "OK");
                return new List<PopulateResult>();
            }

            var results = new List<PopulateResult>();
            int populated = 0;
            int created = 0;
            int skipped = 0;

            // Deduplicate: track processed GUIDs to avoid redundant entries
            var processedGuids = new HashSet<string>();

            var allPageItems = CollectAllViewPageItems();

            // Process ViewPageItems
            foreach (var item in allPageItems)
            {
                var result = ProcessGameObject(settings, item.viewElementObject, ref item.viewElementAddress, preview, processedGuids);
                if (result.status == "ok") populated++;
                else if (result.status == "created") { populated++; created++; }
                else skipped++;
                if (result.status != null) results.Add(result);
            }

            // Process UniqueViewElementTable
            foreach (var entry in saveData.uniqueViewElementTable)
            {
                var result = ProcessGameObject(settings, entry.viewElementGameObject, ref entry.viewElementAddress, preview, processedGuids);
                if (result.status == "ok") populated++;
                else if (result.status == "created") { populated++; created++; }
                else skipped++;
                if (result.status != null) results.Add(result);
            }

            // Apply clearing only on actual populate (not preview)
            if (!preview && clearDirectReferences)
            {
                // Create backup before clearing
                CreateBackup(allPageItems, saveData.uniqueViewElementTable);

                foreach (var item in allPageItems)
                {
                    if (item.viewElementObject != null && !string.IsNullOrEmpty(item.viewElementAddress))
                        item.viewElementObject = null;
                }
                foreach (var entry in saveData.uniqueViewElementTable)
                {
                    if (entry.viewElementGameObject != null && !string.IsNullOrEmpty(entry.viewElementAddress))
                        entry.viewElementGameObject = null;
                }
            }

            if (!preview)
            {
                // Mark all modified ScriptableObjects dirty
                EditorUtility.SetDirty(saveData);
                foreach (var node in saveData.viewPagesNodeSaveDatas)
                    if (node != null) EditorUtility.SetDirty(node);
                foreach (var node in saveData.viewStateNodeSaveDatas)
                    if (node != null) EditorUtility.SetDirty(node);

                AssetDatabase.SaveAssets();
                Debug.Log($"[ViewElementAddressPopulator] Done — populated: {populated} (new addressable: {created}), skipped: {skipped}");
            }
            else
            {
                Debug.Log($"[ViewElementAddressPopulator] Preview — would populate: {populated} (new addressable: {created}), skip: {skipped}");
            }

            return results;
        }

        private string GetSelectedGroupName()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;
            var groups = settings.groups.Where(g => g != null).ToList();
            if (selectedGroupIndex >= 0 && selectedGroupIndex < groups.Count)
                return groups[selectedGroupIndex].Name;
            return null;
        }

        private PopulateResult ProcessGameObject(
            AddressableAssetSettings settings,
            GameObject go,
            ref string addressField,
            bool preview,
            HashSet<string> processedGuids)
        {
            if (go == null)
            {
                if (!string.IsNullOrEmpty(addressField))
                    return new PopulateResult { name = addressField, address = addressField, status = "already_set" };
                return new PopulateResult { name = "(null)", status = "no_prefab" };
            }

            string assetPath = AssetDatabase.GetAssetPath(go);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string prefabName = go.name;

            // Skip duplicates (same prefab referenced multiple times)
            if (!processedGuids.Add(guid))
            {
                // Still fill the address field for this item
                var existingEntry = settings.FindAssetEntry(guid);
                if (existingEntry != null)
                {
                    if (string.IsNullOrEmpty(addressField) || addressField != existingEntry.address)
                    {
                        if (!preview) addressField = existingEntry.address;
                        return new PopulateResult { name = prefabName, address = existingEntry.address, assetPath = assetPath, status = "ok" };
                    }
                    return new PopulateResult { name = prefabName, address = existingEntry.address, assetPath = assetPath, status = "already_set" };
                }
                return new PopulateResult { name = prefabName, status = "no_prefab" };
            }

            var entry = settings.FindAssetEntry(guid);

            // Auto-create addressable entry if not found
            if (entry == null && autoCreateAddressable)
            {
                if (!preview)
                {
                    var groupName = GetSelectedGroupName();
                    var group = settings.FindGroup(groupName);
                    if (group == null)
                    {
                        Debug.LogError($"[ViewElementAddressPopulator] Group '{groupName}' not found.");
                        return new PopulateResult { name = prefabName, status = "no_prefab" };
                    }
                    entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                    entry.address = prefabName;
                    addressField = prefabName;
                }
                return new PopulateResult { name = prefabName, address = prefabName, assetPath = assetPath, status = "created" };
            }

            if (entry == null)
            {
                return new PopulateResult { name = prefabName, status = "no_prefab" };
            }

            string address = entry.address;

            if (!string.IsNullOrEmpty(addressField) && addressField == address)
            {
                return new PopulateResult { name = prefabName, address = address, assetPath = assetPath, status = "already_set" };
            }

            if (!preview)
            {
                addressField = address;
            }

            return new PopulateResult { name = prefabName, address = address, assetPath = assetPath, status = "ok" };
        }
    }
}
