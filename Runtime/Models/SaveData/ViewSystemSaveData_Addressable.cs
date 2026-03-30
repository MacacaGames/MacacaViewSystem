using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.Linq;

namespace MacacaGames.ViewSystem
{
    public class ViewSystemSaveData_Addressable : ScriptableObject
    {
        public List<ViewPageItemAssetRef> viewPageItemAssetRefs = new List<ViewPageItemAssetRef>();

        public List<UniqueViewElementAssetRef> uniqueViewElementAssetRefs = new List<UniqueViewElementAssetRef>();

        public List<ViewStateNodeSaveData> viewStateNodeSaveDatas = new List<ViewStateNodeSaveData>();
        public List<ViewPageNodeSaveData> viewPagesNodeSaveDatas = new List<ViewPageNodeSaveData>();

        public ViewSystemSaveData.ViewSystemBaseSetting globalSetting;

        public List<ViewStateSaveData> GetViewStateSaveDatas()
        {
            return viewStateNodeSaveDatas.Select(m => m.data).ToList();
        }

        public List<ViewPageSaveData> GetViewPageSaveDatas()
        {
            return viewPagesNodeSaveDatas.Select(m => m.data).ToList();
        }
    }

    [System.Serializable]
    public class ViewPageItemAssetRef
    {
        public string viewPageItemId;
        public AssetReferenceGameObject assetReference;
    }

    [System.Serializable]
    public class UniqueViewElementAssetRef
    {
        public string type;
        public AssetReferenceGameObject assetReference;
    }
}
