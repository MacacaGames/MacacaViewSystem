using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    public struct ViewElementValidationResult
    {
        public string name;
        public string address;
        public string assetPath;
        /// <summary>
        /// "ok" = registered, "unregistered" = prefab exists but not in addressable,
        /// "no_prefab" = null reference, "already_set" = address already populated
        /// </summary>
        public string status;
    }

    public static class ViewElementAddressableProcessor
    {
        private const string ViewSystemResourceFolder = "Assets/ViewSystemResources/";
        private const string ViewSystemSaveDataFileName = "ViewSystemData.asset";
        private const string FileBackupFolder = "Temp/ViewSystemFileBackup";
        private const string LogTag = "[ViewElementAddressableProcessor]";

        /// <summary>
        /// Prevents IPreprocessBuild from running PopulateAndClear again when BuildCommand already called it.
        /// </summary>
        public static bool IsProcessed { get; private set; }

        public static ViewSystemSaveData FindSaveData()
        {
            var path = ViewSystemResourceFolder + ViewSystemSaveDataFileName;
            return AssetDatabase.LoadAssetAtPath<ViewSystemSaveData>(path);
        }

        public static bool ShouldProcess()
        {
            var saveData = FindSaveData();
            return saveData != null && saveData.globalSetting.useAddressableLoading;
        }

        public static void PopulateAndClear()
        {
            var saveData = FindSaveData();
            if (saveData == null)
            {
                Debug.LogWarning($"{LogTag} ViewSystemSaveData not found, skipping.");
                return;
            }

            if (!saveData.globalSetting.useAddressableLoading)
            {
                Debug.Log($"{LogTag} useAddressableLoading is disabled, skipping.");
                return;
            }

            var groupName = saveData.globalSetting.addressableGroupName;
            if (string.IsNullOrEmpty(groupName))
            {
                Debug.LogError($"{LogTag} addressableGroupName is empty. Please set it in ViewSystem Global Settings.");
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"{LogTag} Addressable Asset Settings not found.");
                return;
            }

            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                Debug.LogError($"{LogTag} Addressable group '{groupName}' not found.");
                return;
            }

            // Backup original asset files before any modification
            BackupAssetFiles(saveData);

            var allPageItems = CollectAllViewPageItems(saveData);
            var processedGuids = new HashSet<string>();
            int populated = 0;
            int created = 0;

            // Process ViewPageItems
            foreach (var item in allPageItems)
            {
                var result = PopulateEntry(settings, group, item.viewElementObject, ref item.viewElementAddress, processedGuids);
                if (result == "ok") populated++;
                else if (result == "created") { populated++; created++; }
            }

            // Process UniqueViewElementTable
            foreach (var entry in saveData.uniqueViewElementTable)
            {
                var result = PopulateEntry(settings, group, entry.viewElementGameObject, ref entry.viewElementAddress, processedGuids);
                if (result == "ok") populated++;
                else if (result == "created") { populated++; created++; }
            }

            // Clear direct references
            int cleared = 0;
            foreach (var item in allPageItems)
            {
                if (item.viewElementObject != null && !string.IsNullOrEmpty(item.viewElementAddress))
                {
                    item.viewElementObject = null;
                    cleared++;
                }
            }
            foreach (var entry in saveData.uniqueViewElementTable)
            {
                if (entry.viewElementGameObject != null && !string.IsNullOrEmpty(entry.viewElementAddress))
                {
                    entry.viewElementGameObject = null;
                    cleared++;
                }
            }

            // Save
            MarkAllDirty(saveData);
            AssetDatabase.SaveAssets();

            IsProcessed = true;
            Debug.Log($"{LogTag} PopulateAndClear: {populated} prefabs registered to group '{groupName}' (new: {created}), {cleared} references cleared.");
        }

        public static void Restore()
        {
            IsProcessed = false;

            RestoreAssetFiles();
        }

        public static List<ViewElementValidationResult> Validate(ViewSystemSaveData saveData)
        {
            var results = new List<ViewElementValidationResult>();
            if (saveData == null) return results;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var processedGuids = new HashSet<string>();

            var allPageItems = CollectAllViewPageItems(saveData);

            foreach (var item in allPageItems)
            {
                var r = ValidateEntry(settings, item.viewElementObject, item.viewElementAddress, processedGuids);
                if (r.status != null) results.Add(r);
            }

            foreach (var entry in saveData.uniqueViewElementTable)
            {
                var r = ValidateEntry(settings, entry.viewElementGameObject, entry.viewElementAddress, processedGuids);
                if (r.status != null) results.Add(r);
            }

            return results;
        }

        #region Private Helpers

        private static List<ViewPageItem> CollectAllViewPageItems(ViewSystemSaveData saveData)
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

        /// <summary>
        /// Register a prefab to the addressable group and set its address field.
        /// Returns: "ok", "created", "already_set", "skip" (duplicate), or "no_prefab"
        /// </summary>
        private static string PopulateEntry(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            GameObject go,
            ref string addressField,
            HashSet<string> processedGuids)
        {
            if (go == null)
            {
                if (!string.IsNullOrEmpty(addressField))
                    return "already_set";
                return "no_prefab";
            }

            string assetPath = AssetDatabase.GetAssetPath(go);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string prefabName = go.name;

            if (!processedGuids.Add(guid))
            {
                // Duplicate: still fill address from existing entry
                var existingEntry = settings.FindAssetEntry(guid);
                if (existingEntry != null && string.IsNullOrEmpty(addressField))
                {
                    addressField = existingEntry.address;
                    return "ok";
                }
                return "skip";
            }

            var entry = settings.FindAssetEntry(guid);

            if (entry == null && group != null)
            {
                entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                entry.address = prefabName;
                addressField = prefabName;
                return "created";
            }

            if (entry == null)
                return "no_prefab";

            if (string.IsNullOrEmpty(addressField) || addressField != entry.address)
            {
                addressField = entry.address;
                return "ok";
            }

            return "already_set";
        }

        private static ViewElementValidationResult ValidateEntry(
            AddressableAssetSettings settings,
            GameObject go,
            string addressField,
            HashSet<string> processedGuids)
        {
            if (go == null)
            {
                if (!string.IsNullOrEmpty(addressField))
                    return new ViewElementValidationResult { name = addressField, address = addressField, status = "already_set" };
                return new ViewElementValidationResult { name = "(null)", status = "no_prefab" };
            }

            string assetPath = AssetDatabase.GetAssetPath(go);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            if (!processedGuids.Add(guid))
                return default; // skip duplicate, status = null

            var entry = settings?.FindAssetEntry(guid);
            string prefabName = go.name;

            if (entry != null)
            {
                return new ViewElementValidationResult
                {
                    name = prefabName,
                    address = entry.address,
                    assetPath = assetPath,
                    status = !string.IsNullOrEmpty(addressField) ? "already_set" : "ok"
                };
            }

            return new ViewElementValidationResult
            {
                name = prefabName,
                assetPath = assetPath,
                status = "unregistered"
            };
        }

        private static HashSet<string> CollectAffectedAssetPaths(ViewSystemSaveData saveData)
        {
            var paths = new HashSet<string>();

            var saveDataPath = AssetDatabase.GetAssetPath(saveData);
            if (!string.IsNullOrEmpty(saveDataPath))
                paths.Add(saveDataPath);

            foreach (var node in saveData.viewPagesNodeSaveDatas)
            {
                if (node == null) continue;
                var p = AssetDatabase.GetAssetPath(node);
                if (!string.IsNullOrEmpty(p)) paths.Add(p);
            }

            foreach (var node in saveData.viewStateNodeSaveDatas)
            {
                if (node == null) continue;
                var p = AssetDatabase.GetAssetPath(node);
                if (!string.IsNullOrEmpty(p)) paths.Add(p);
            }

            // Addressable group asset is modified by CreateOrMoveEntry
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                var groupName = saveData.globalSetting.addressableGroupName;
                if (!string.IsNullOrEmpty(groupName))
                {
                    var group = settings.FindGroup(groupName);
                    if (group != null)
                    {
                        var groupPath = AssetDatabase.GetAssetPath(group);
                        if (!string.IsNullOrEmpty(groupPath)) paths.Add(groupPath);
                    }
                }
            }

            return paths;
        }

        private static void BackupAssetFiles(ViewSystemSaveData saveData)
        {
            var paths = CollectAffectedAssetPaths(saveData);

            if (Directory.Exists(FileBackupFolder))
                Directory.Delete(FileBackupFolder, true);
            Directory.CreateDirectory(FileBackupFolder);

            int count = 0;
            foreach (var assetPath in paths)
            {
                var fullPath = Path.GetFullPath(assetPath);
                if (!File.Exists(fullPath)) continue;

                var destPath = Path.Combine(FileBackupFolder, assetPath.Replace('/', '_'));
                File.Copy(fullPath, destPath, true);

                // Also store a mapping file so we know original path
                File.WriteAllText(destPath + ".path", assetPath);
                count++;
            }

            Debug.Log($"{LogTag} BackupAssetFiles: {count} files backed up to {FileBackupFolder}");
        }

        private static void RestoreAssetFiles()
        {
            if (!Directory.Exists(FileBackupFolder))
            {
                Debug.LogWarning($"{LogTag} No file backup found at {FileBackupFolder}, skipping restore.");
                return;
            }

            var pathFiles = Directory.GetFiles(FileBackupFolder, "*.path");
            int count = 0;

            foreach (var pathFile in pathFiles)
            {
                var assetPath = File.ReadAllText(pathFile).Trim();
                var backupFile = pathFile.Substring(0, pathFile.Length - ".path".Length);

                if (!File.Exists(backupFile)) continue;

                var fullPath = Path.GetFullPath(assetPath);
                File.Copy(backupFile, fullPath, true);
                count++;
            }

            Directory.Delete(FileBackupFolder, true);
            AssetDatabase.Refresh();

            Debug.Log($"{LogTag} RestoreAssetFiles: {count} files restored from backup.");
        }

        private static void MarkAllDirty(ViewSystemSaveData saveData)
        {
            EditorUtility.SetDirty(saveData);
            foreach (var node in saveData.viewPagesNodeSaveDatas)
                if (node != null) EditorUtility.SetDirty(node);
            foreach (var node in saveData.viewStateNodeSaveDatas)
                if (node != null) EditorUtility.SetDirty(node);
        }

        #endregion
    }
}
