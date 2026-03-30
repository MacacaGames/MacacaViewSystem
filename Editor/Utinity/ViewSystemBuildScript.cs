using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Base build script that wraps Addressable builds with ViewElement auto-populate/restore.
    /// When UseAddressableLoading is enabled in ViewSystem settings, this script will:
    ///   1. Register ViewElement prefabs to the configured Addressable group and clear direct references (pre-build)
    ///   2. Run the normal Addressable packed build
    ///   3. Restore direct references (post-build)
    ///
    /// Usage:
    ///   - If you have no custom build script: create an instance of this and set it as active in Addressable Asset Settings.
    ///   - If you have a custom build script (e.g. RawAddressablesBuildScript), change it to inherit from
    ///     ViewSystemBuildScript instead of BuildScriptPackedMode.
    /// </summary>
    [CreateAssetMenu(menuName = "MacacaGames/ViewSystem/ViewSystem Build Script")]
    public class ViewSystemBuildScript : BuildScriptPackedMode
    {
        protected override TResult DoBuild<TResult>(
            AddressablesDataBuilderInput builderInput,
            AddressableAssetsBuildContext aaContext)
        {
            Debug.Log($"[ViewSystemBuildScript] DoBuild entered. IsProcessed={ViewElementAddressableProcessor.IsProcessed}, ShouldProcess={ViewElementAddressableProcessor.ShouldProcess()}");

            bool processed = false;
            if (!ViewElementAddressableProcessor.IsProcessed && ViewElementAddressableProcessor.ShouldProcess())
            {
                Debug.Log("[ViewSystemBuildScript] PopulateAndClear before Addressable build...");
                ViewElementAddressableProcessor.PopulateAndClear();
                processed = true;
            }
            else
            {
                Debug.Log($"[ViewSystemBuildScript] Skipping PopulateAndClear. (IsProcessed={ViewElementAddressableProcessor.IsProcessed} — likely called by BuildCommand or IPreprocessBuild already)");
            }

            try
            {
                Debug.Log("[ViewSystemBuildScript] Starting base.DoBuild (Addressable content build)...");
                var result = base.DoBuild<TResult>(builderInput, aaContext);
                Debug.Log("[ViewSystemBuildScript] base.DoBuild completed.");
                return result;
            }
            finally
            {
                if (processed)
                {
                    Debug.Log("[ViewSystemBuildScript] Restoring ViewElement direct references after Addressable build...");
                    ViewElementAddressableProcessor.Restore();
                }
                else
                {
                    Debug.Log("[ViewSystemBuildScript] Skipping Restore in DoBuild (will be handled by IPostprocessBuild or BuildCommand).");
                }
            }
        }
    }
}
