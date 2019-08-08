using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    public class ViewSystemSaveData : ScriptableObject
    {
        public ViewSystemBaseSetting globalSetting;
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

        [System.Serializable]
        public class ViewSystemBaseSetting
        {
            public Vector2 nodePosition = new Vector2(500, 500);
            public string ViewControllerObjectPath;
            public GameObject UIRoot;
            public GameObject UIRootScene;
#if UNITY_EDITOR
            public List<UnityEditor.MonoScript> EventHandleBehaviour = new List<UnityEditor.MonoScript>();
#endif
            public float MaxWaitingTime
            {
                get
                {
                    return Mathf.Clamp01(_maxWaitingTime);
                }
            }
            public float _maxWaitingTime = 1;
        }

    }
    [System.Serializable]
    public class ViewSystemComponentData
    {
        public string targetTransformPath;
        public string targetComponentType;
        public string targetPropertyName;
        /// This value is save as SerializedProperty.PropertyPath
        public string targetPropertyType;
        /// if is UnityEngine PropertyPath this lable save modified property name;
        public string targetPropertyPath;
    }

    [System.Serializable]
    public class ViewElementEventData : ViewSystemComponentData
    {
        public string scriptName;
        public string methodName;
    }
    [System.Serializable]
    public class ViewElementPropertyOverrideData : ViewSystemComponentData
    {
        public PropertyOverride Value;
    }
    [System.Serializable]
    public class PropertyOverride
    {
        public object GetDirtyValue()
        {
            switch (s_Type)
            {
                case S_Type._bool:
                    return BooleanValue;
                case S_Type._float:
                    return FloatValue;
                case S_Type._int:
                    return IntValue;
                case S_Type._color:
                    return ColorValue;
                case S_Type._objcetReferenct:
                    return ObjectReferenceValue;
                case S_Type._string:
                    return StringValue;
                default:
                    return null;
            }
        }

        public void SetType(S_Type t)
        {
            s_Type = t;
        }
        public enum S_Type
        {
            _bool, _float, _int, _color, _objcetReferenct, _string
        }
        public S_Type s_Type;
        // public AnimationCurve AnimationCurveValue;

        public bool BooleanValue;

        //public Bounds BoundsValue;

        public Color ColorValue;

        //public double DoubleValue;

        public float FloatValue;

        public int IntValue;

        //public long LongValue;

        public UnityEngine.Object ObjectReferenceValue;

        //public Quaternion QuaternionValue;

        //public Rect RectValue;

        public string StringValue;

        //public Vector2 Vector2Value;

        //public Vector3 Vector3Value;

        //public Vector4 Vector4Value;
    }
}