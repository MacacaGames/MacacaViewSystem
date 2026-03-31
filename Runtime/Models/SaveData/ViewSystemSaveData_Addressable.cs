using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

namespace MacacaGames.ViewSystem
{
    public class ViewSystemSaveData_Addressable : ViewSystemSaveDataBase
    {
        public override bool IsAddressableMode => true;

        public List<ViewPageItemAssetRef> viewPageItemAssetRefs = new List<ViewPageItemAssetRef>();
        public List<UniqueViewElementAssetRef> uniqueViewElementAssetRefs = new List<UniqueViewElementAssetRef>();
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
