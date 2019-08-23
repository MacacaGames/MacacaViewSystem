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
        public ViewElementPropertyOverrideData()
        {
            Value = new PropertyOverride();
        }
        public PropertyOverride Value;
    }
    [System.Serializable]
    public class PropertyOverride
    {
        public object GetValue()
        {
            switch (s_Type)
            {
                case S_Type._bool:
                    return System.Convert.ToBoolean(StringValue);
                case S_Type._float:
                    return (float)System.Convert.ToDouble(StringValue);
                case S_Type._int:
                    return System.Convert.ToInt32(StringValue);
                case S_Type._color:
                    return ColorUtility.TryParseHtmlString("#" + StringValue, out Color c) ? c : Color.black;
                case S_Type._objcetReferenct:
                    return ObjectReferenceValue;
                case S_Type._string:
                    return StringValue;
                case S_Type._enum:
                    var s = StringValue.Split(',');
                    var enumType = CloudMacaca.Utility.GetType(s[0]);
                    return System.Enum.Parse(enumType, s[1], false);
                default:
                    return null;
            }
        }
#if UNITY_EDITOR
        public void SetValue(UnityEditor.SerializedPropertyType type, UnityEditor.PropertyModification modification)
        {
            switch (type)
            {
                case UnityEditor.SerializedPropertyType.Float:
                    SetValue(float.Parse(modification.value));
                    break;
                case UnityEditor.SerializedPropertyType.Integer:
                    SetValue(int.Parse(modification.value));
                    break;
                case UnityEditor.SerializedPropertyType.String:
                    SetValue(modification.value);
                    break;
                case UnityEditor.SerializedPropertyType.Boolean:
                    SetValue(modification.value == "0" ? false : true);
                    break;
                case UnityEditor.SerializedPropertyType.Color:
                    SetValue(new Color());
                    break;
                case UnityEditor.SerializedPropertyType.ObjectReference:
                    SetValue(modification.objectReference);
                    break;
                case UnityEditor.SerializedPropertyType.Enum:
                    var t = TryFindEnumType(modification.target, modification.propertyPath);
                    var e = System.Enum.Parse(t, modification.value);
                    SetValue(e);
                    break;
            }
        }

        public void SetValue(UnityEditor.SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case UnityEditor.SerializedPropertyType.Float:
                    SetValue(property.floatValue);
                    break;
                case UnityEditor.SerializedPropertyType.Integer:
                    SetValue(property.intValue);
                    break;
                case UnityEditor.SerializedPropertyType.String:
                    SetValue(property.stringValue);
                    break;
                case UnityEditor.SerializedPropertyType.Boolean:
                    SetValue(property.boolValue);
                    break;
                case UnityEditor.SerializedPropertyType.Color:
                    SetValue(property.colorValue);
                    break;
                case UnityEditor.SerializedPropertyType.ObjectReference:
                    SetValue(property.objectReferenceValue);
                    break;
                case UnityEditor.SerializedPropertyType.Enum:
                    var type = TryFindEnumType(property);
                    var e = System.Enum.Parse(type, property.enumNames[property.enumValueIndex]);
                    SetValue(e);
                    break;
            }
        }
        public System.Type TryFindEnumType(UnityEngine.Object unityObject, string propertyPath)
        {
            var to = unityObject.GetType();
            var fildInfo = to.GetField(propertyPath);

            if (fildInfo != null)
            {
                return fildInfo.FieldType;
            }
            var propertyInfo = to.GetProperty(propertyPath);
            if (propertyInfo != null)
            {
                return propertyInfo.PropertyType;
            }
            return null;
        }
        public System.Type TryFindEnumType(UnityEditor.SerializedProperty property)
        {
            return TryFindEnumType(property.serializedObject.targetObject, property.propertyPath);
        }
#endif

        public void SetValue(object value)
        {
            bool toStringDirectly = true;
            if (value is int || value is long)
            {
                s_Type = S_Type._int;
            }
            else if (value is string)
            {
                s_Type = S_Type._string;
            }
            else if (value is float || value is double)
            {
                s_Type = S_Type._float;
            }
            else if (value is bool)
            {
                s_Type = S_Type._bool;
            }
            else if (value.GetType().IsEnum)
            {
                s_Type = S_Type._enum;
                StringValue = value.GetType().ToString() + "," + value.ToString();
                toStringDirectly = false;
            }
            else if (value is Color)
            {
                s_Type = S_Type._color;
                StringValue = ColorUtility.ToHtmlStringRGBA((Color)value);
                toStringDirectly = false;
            }
            else if (value == null)
            {
                // Only UnityEngine.Object may be null
                s_Type = S_Type._objcetReferenct;
                ObjectReferenceValue = null;
                toStringDirectly = false;
            }
            else if (value.GetType().IsSubclassOf(typeof(UnityEngine.Object)) ||
                    value.GetType().IsAssignableFrom(typeof(UnityEngine.Object)))
            {
                s_Type = S_Type._objcetReferenct;
                ObjectReferenceValue = (UnityEngine.Object)value;
                toStringDirectly = false;
            }
            if (toStringDirectly) StringValue = value.ToString();
        }

        // public object GetDirtyValue()
        // {
        //     switch (s_Type)
        //     {
        //         case S_Type._bool:
        //             return BooleanValue;
        //         case S_Type._float:
        //             return FloatValue;
        //         case S_Type._int:
        //             return IntValue;
        //         case S_Type._color:
        //             return ColorValue;
        //         case S_Type._objcetReferenct:
        //             return ObjectReferenceValue;
        //         case S_Type._string:
        //             return StringValue;
        //         default:
        //             return null;
        //     }
        // }

        public void SetType(S_Type t)
        {
            s_Type = t;
        }
        public enum S_Type
        {
            _bool, _float, _int, _color, _objcetReferenct, _string, _enum
        }
        public S_Type s_Type = S_Type._string;
        // public AnimationCurve AnimationCurveValue;

        //public bool BooleanValue;

        //public Bounds BoundsValue;

        //public Color ColorValue;

        //public double DoubleValue;

        //public float FloatValue;

        //public int IntValue;

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