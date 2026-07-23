using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    public abstract class ViewSystemSaveDataBase : ScriptableObject
    {
        public ViewSystemBaseSetting globalSetting;
        public List<ViewStateNodeSaveData> viewStateNodeSaveDatas = new List<ViewStateNodeSaveData>();
        public List<ViewPageNodeSaveData> viewPagesNodeSaveDatas = new List<ViewPageNodeSaveData>();

        public List<ViewStateSaveData> GetViewStateSaveDatas()
        {
            return viewStateNodeSaveDatas.Select(m => m.data).ToList();
        }

        public List<ViewPageSaveData> GetViewPageSaveDatas()
        {
            return viewPagesNodeSaveDatas.Select(m => m.data).ToList();
        }

        public abstract bool IsAddressableMode { get; }
    }

    [System.Serializable]
    public class ViewSystemBaseSetting
    {
        public bool UseNavigationSetting = false;
        public string ViewControllerObjectPath;
        public string UIPageTransformLayerName = "Default";
        public string customPageRootPath = "";
        public GameObject UIRoot;
        public GameObject UIRootScene;
        public SafePadding.PerEdgeValues edgeValues = new SafePadding.PerEdgeValues();
        public bool flipPadding = false;
        public float MaxWaitingTime
        {
            get
            {
                return Mathf.Clamp01(_maxWaitingTime);
            }
        }
        public float _maxWaitingTime = 1.5f;
        public float minimumTimeInterval = 0.2f;
        public bool builtInClickProtection = true;
        public bool useAddressableLoading = false;
        public string addressableGroupName = "";
        public string addressableLabelName = "ViewSystem";
        public List<string> userBreakPoints = new List<string>();
        public IEnumerable<string> breakPoints
        {
            get
            {
                return userBreakPoints;
            }
        }
    }
}
