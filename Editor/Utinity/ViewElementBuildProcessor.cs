using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Safety net for Player builds triggered from Build Settings > Build.
    /// For CI builds, PopulateAndClear is called directly from BuildCommand before HandleAssetBundle.
    /// For manual Addressable builds, the active build script (ViewSystemAwareBuildScript) handles it.
    /// </summary>
    public class ViewElementBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log($"[ViewElementBuildPreprocessor] OnPreprocessBuild called. IsProcessed={ViewElementAddressableProcessor.IsProcessed}, ShouldProcess={ViewElementAddressableProcessor.ShouldProcess()}");
            if (!ViewElementAddressableProcessor.IsProcessed && ViewElementAddressableProcessor.ShouldProcess())
            {
                Debug.Log("[ViewElementBuildPreprocessor] Auto-populating ViewElement addressables before Player build...");
                ViewElementAddressableProcessor.PopulateAndClear();
            }
            else
            {
                Debug.Log("[ViewElementBuildPreprocessor] Skipping — already processed by ViewSystemBuildScript or BuildCommand.");
            }
        }
    }

    public class ViewElementBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            Debug.Log($"[ViewElementBuildPostprocessor] OnPostprocessBuild called. IsProcessed={ViewElementAddressableProcessor.IsProcessed}");
            if (ViewElementAddressableProcessor.IsProcessed)
            {
                Debug.Log("[ViewElementBuildPostprocessor] Restoring ViewElement direct references after Player build...");
                ViewElementAddressableProcessor.Restore();
            }
            else
            {
                Debug.Log("[ViewElementBuildPostprocessor] Skipping — already restored by ViewSystemBuildScript.");
            }
        }
    }
}
