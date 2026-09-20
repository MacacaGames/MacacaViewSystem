using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MacacaGames.ViewSystem
{
    [System.Serializable]
    public sealed class ViewElementGlobalPoolBudgetSetting
    {
        public bool enabled = false;
        public ViewElementGlobalEvictionMode mode = ViewElementGlobalEvictionMode.NormalBudget;

        // A value of 0 disables the corresponding budget dimension.
        public int highWatermarkInstances = 0;
        public int lowWatermarkInstances = 0;
        public int highWatermarkHierarchyGameObjects = 0;
        public int lowWatermarkHierarchyGameObjects = 0;

        public int maxEvictionsPerFrame = 5;
        // Keep the serialized legacy value until this setting is edited. Old
        // assets used one value for both the automatic trim grace and idle trim.
        [HideInInspector] public float minimumIdleSeconds = 0;
        [SerializeField, HideInInspector] float autoTrimGraceSeconds = 0;
        [SerializeField, HideInInspector] float idleTrimAfterSeconds = 0;
        [SerializeField, HideInInspector] bool splitTrimTimingConfigured = false;
        public bool allowEmergencyHardCapOnLowMemory = false;

        public float AutoTrimGraceSeconds
        {
            get => splitTrimTimingConfigured ? autoTrimGraceSeconds : minimumIdleSeconds;
            set
            {
                EnsureSplitTrimTiming();
                autoTrimGraceSeconds = value;
            }
        }

        public float IdleTrimAfterSeconds
        {
            get => splitTrimTimingConfigured ? idleTrimAfterSeconds : minimumIdleSeconds;
            set
            {
                EnsureSplitTrimTiming();
                idleTrimAfterSeconds = value;
            }
        }

        void EnsureSplitTrimTiming()
        {
            if (splitTrimTimingConfigured)
            {
                return;
            }

            autoTrimGraceSeconds = minimumIdleSeconds;
            idleTrimAfterSeconds = minimumIdleSeconds;
            splitTrimTimingConfigured = true;
        }

        public ViewElementGlobalPoolBudgetOptions ToRuntimeOptions()
        {
            return new ViewElementGlobalPoolBudgetOptions
            {
                Enabled = enabled,
                Mode = mode,
                HighWatermarkInstances = highWatermarkInstances,
                LowWatermarkInstances = lowWatermarkInstances,
                HighWatermarkHierarchyGameObjects = highWatermarkHierarchyGameObjects,
                LowWatermarkHierarchyGameObjects = lowWatermarkHierarchyGameObjects,
                MaxEvictionsPerFrame = maxEvictionsPerFrame,
                AutoTrimGraceSeconds = AutoTrimGraceSeconds,
                IdleTrimAfterSeconds = IdleTrimAfterSeconds,
                AllowEmergencyHardCapOnLowMemory = allowEmergencyHardCapOnLowMemory,
            };
        }
    }

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
        public ViewElementGlobalPoolBudgetSetting globalPoolBudget =
            new ViewElementGlobalPoolBudgetSetting();
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
