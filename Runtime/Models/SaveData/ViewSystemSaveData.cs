using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    public class ViewSystemSaveData : ScriptableObject
    {


        public ViewSystemBaseSetting globalSetting;
        public List<ViewStateSaveData> viewStates = new List<ViewStateSaveData>();
        public List<ViewPageSaveData> viewPages = new List<ViewPageSaveData>();
        public List<UniqueViewElementTableData> uniqueViewElementTable = new List<UniqueViewElementTableData>();

        public List<ViewStateSaveData> GetViewStateSaveDatas()
        {
            return viewStateNodeSaveDatas.Select(m => m.data).ToList();
        }
        public List<ViewPageSaveData> GetViewPageSaveDatas()
        {
            return viewPagesNodeSaveDatas.Select(m => m.data).ToList();
        }

        public List<ViewStateNodeSaveData> viewStateNodeSaveDatas = new List<ViewStateNodeSaveData>();
        public List<ViewPageNodeSaveData> viewPagesNodeSaveDatas = new List<ViewPageNodeSaveData>();

        public bool RequireMigration()
        {
            return ((viewStates != null || viewStates.Count > 0) ||
                    (viewPages != null || viewPages.Count > 0)) &&
                    ((viewPagesNodeSaveDatas != null && viewPagesNodeSaveDatas.Count == 0) ||
                    (viewStateNodeSaveDatas != null && viewStateNodeSaveDatas.Count == 0));
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
            /// <summary>
            /// The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call.
            /// If user the method call time interval less than this value, the call will be ignore!
            /// </summary>
            public float minimumTimeInterval = 0.2f;

            /// <summary>
            /// Enable the builtIn click protection or not, if true, the system will ignore the show page call if any page is transition
            /// </summary>
            public bool builtInClickProtection = true;

            /// <summary>
            /// Enable on-demand Addressable loading for ViewElement prefabs.
            /// When disabled, the traditional direct reference approach (viewElementObject) is used.
            /// </summary>
            public bool useAddressableLoading = false;

            /// <summary>
            /// The Addressable group name to assign ViewElement prefabs to during build.
            /// </summary>
            public string addressableGroupName = "";

            // public string[] builtInBreakPoints = new string[]{
            //     "Horizon",
            //     "Vertical"
            // };
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


    public class VectorConvert
    {
        public static string Vector3ToString(Vector3 vector)
        {
            return ((Vector3)vector).ToString("F3");
        }
        public static string Vector2ToString(Vector2 vector)
        {
            return ((Vector3)vector).ToString("F3");
        }
        public static Vector3 StringToVector3(string sVector)
        {
            try
            {
                // Remove the parentheses
                if (sVector.StartsWith("(") && sVector.EndsWith(")"))
                {
                    sVector = sVector.Substring(1, sVector.Length - 2);
                }
                // split the items
                string[] sArray = sVector.Split(',');

                // store as a Vector3
                Vector3 result = new Vector3(
                    float.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture.NumberFormat),
                    float.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture.NumberFormat),
                    float.Parse(sArray[2], System.Globalization.CultureInfo.InvariantCulture.NumberFormat));

                return result;
            }
            catch
            {
                return default(Vector3);
            }

        }
        public static Vector2 StringToVector2(string sVector)
        {
            try
            { // Remove the parentheses
                if (sVector.StartsWith("(") && sVector.EndsWith(")"))
                {
                    sVector = sVector.Substring(1, sVector.Length - 2);
                }

                // split the items
                string[] sArray = sVector.Split(',');

                // store as a Vector2
                Vector2 result = new Vector2(
                    float.Parse(sArray[0], System.Globalization.CultureInfo.InvariantCulture.NumberFormat),
                    float.Parse(sArray[1], System.Globalization.CultureInfo.InvariantCulture.NumberFormat));

                return result;
            }
            catch
            {
                return default(Vector2);
            }
        }
    }


    /// <summary>
    /// Delegate for loading a GameObject prefab by Addressable address.
    /// Returns Task&lt;GameObject&gt; so it can be awaited from coroutines.
    /// </summary>
    public delegate System.Threading.Tasks.Task<GameObject> ViewElementLoadDelegate(string address);

    /// <summary>
    /// Delegate for releasing a previously loaded prefab by address.
    /// </summary>
    public delegate void ViewElementReleaseDelegate(string address);

    [System.Serializable]
    public class UniqueViewElementTableData
    {
        public GameObject viewElementGameObject;
        public string viewElementAddress;
        public string type;

        /// <summary>
        /// Runtime cache for the loaded prefab GameObject (loaded via Addressables).
        /// </summary>
        [System.NonSerialized]
        public GameObject loadedViewElementGameObject;

        /// <summary>
        /// Returns the effective GameObject: direct reference if available, otherwise the async-loaded cache.
        /// </summary>
        public GameObject ResolvedGameObject => viewElementGameObject != null ? viewElementGameObject : loadedViewElementGameObject;

        /// <summary>
        /// Whether this item needs async loading (has address but no direct ref or loaded cache yet)
        /// </summary>
        public bool NeedsAsyncLoad => !string.IsNullOrEmpty(viewElementAddress) && viewElementGameObject == null && loadedViewElementGameObject == null;
    }

    [System.Serializable]
    public class ViewPageSaveData
    {
        public ViewPageSaveData(Vector2 nodePosition, ViewPage viewPage)
        {
            this.nodePosition = nodePosition;
            this.viewPage = viewPage;
        }
        public Vector2 nodePosition;
        public ViewPage viewPage;
    }

    //Save Data Model
    [System.Serializable]
    public class ViewStateSaveData
    {
        public ViewStateSaveData(Vector2 nodePosition, ViewState viewState)
        {
            this.nodePosition = nodePosition;
            this.viewState = viewState;
        }
        public Vector2 nodePosition;
        public ViewState viewState;
    }
}
