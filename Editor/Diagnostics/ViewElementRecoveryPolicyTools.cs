using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MacacaGames.ViewSystem.Diagnostics
{
    public static class ViewElementRecoveryPolicyTools
    {
        private const string MenuRoot = "Assets/MacacaGames/ViewSystem/Recovery Policy/";

        [MenuItem(MenuRoot + "Keep Forever", false, 2000)]
        private static void ConfigureKeepForever()
        {
            ConfigureSelectedPrefabs(ViewElementRecoveryPolicy.KeepForever, 1);
        }

        [MenuItem(MenuRoot + "Keep One", false, 2001)]
        private static void ConfigureKeepOne()
        {
            ConfigureSelectedPrefabs(ViewElementRecoveryPolicy.KeepN, 1);
        }

        [MenuItem(MenuRoot + "Destroy On Recovery", false, 2002)]
        private static void ConfigureDestroyOnRecovery()
        {
            ConfigureSelectedPrefabs(ViewElementRecoveryPolicy.DestroyOnRecovery, 0);
        }

        [MenuItem(MenuRoot + "Keep Forever", true)]
        [MenuItem(MenuRoot + "Keep One", true)]
        [MenuItem(MenuRoot + "Destroy On Recovery", true)]
        private static bool ValidateConfigurePolicy()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   Selection.objects.Any(IsPrefabAsset);
        }

        private static void ConfigureSelectedPrefabs(ViewElementRecoveryPolicy policy, int keepCount)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ViewSystemRecovery] Exit Play Mode before changing prefab recovery policy.");
                return;
            }

            int configured = 0;
            int skipped = 0;
            foreach (var selected in Selection.objects.Where(IsPrefabAsset))
            {
                var prefabPath = AssetDatabase.GetAssetPath(selected);
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var viewElement = prefabRoot.GetComponent<ViewElement>();
                    if (viewElement == null)
                    {
                        skipped++;
                        Debug.LogWarning($"[ViewSystemRecovery] Root ViewElement not found: {prefabPath}");
                        continue;
                    }

                    var serializedObject = new SerializedObject(viewElement);
                    serializedObject.FindProperty(nameof(ViewElement.recoveryPolicy)).enumValueIndex = (int)policy;
                    serializedObject.FindProperty(nameof(ViewElement.recoveryKeepCount)).intValue = keepCount;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    configured++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            Debug.Log(
                $"[ViewSystemRecovery] policy={policy}, keepCount={keepCount}, " +
                $"configured={configured}, skipped={skipped}.");
        }

        private static bool IsPrefabAsset(Object target)
        {
            if (target == null)
                return false;

            var path = AssetDatabase.GetAssetPath(target);
            return !string.IsNullOrEmpty(path) &&
                   PrefabUtility.GetPrefabAssetType(target) != PrefabAssetType.NotAPrefab;
        }
    }
}
