using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    [CreateAssetMenu]
    public class ViewSystemSaveData : ScriptableObject
    {
        public List<ViewStateSaveData> viewStates = new List<ViewStateSaveData>();
        public List<ViewPageSaveData> viewPages = new List<ViewPageSaveData>();

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

    public class ViewElementPropertyOverrideData : ScriptableObject
    {
        public string id;
        public string targetTransformPath;
        public string targetComponentType;
        public string targetPropertyName;
        public UnityEngine.Object targetOverrideData;
       
    }

}