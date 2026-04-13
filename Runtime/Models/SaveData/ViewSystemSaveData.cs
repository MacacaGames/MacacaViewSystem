using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    public class ViewSystemSaveData : ViewSystemSaveDataBase
    {
        public override bool IsAddressableMode => false;

        public List<ViewStateSaveData> viewStates = new List<ViewStateSaveData>();
        public List<ViewPageSaveData> viewPages = new List<ViewPageSaveData>();
        public List<UniqueViewElementTableData> uniqueViewElementTable = new List<UniqueViewElementTableData>();

        public bool RequireMigration()
        {
            return ((viewStates != null && viewStates.Count > 0) ||
                    (viewPages != null && viewPages.Count > 0)) &&
                    ((viewPagesNodeSaveDatas != null && viewPagesNodeSaveDatas.Count == 0) ||
                    (viewStateNodeSaveDatas != null && viewStateNodeSaveDatas.Count == 0));
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


    [System.Serializable]
    public class UniqueViewElementTableData
    {
        public GameObject viewElementGameObject;
        public string type;
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
