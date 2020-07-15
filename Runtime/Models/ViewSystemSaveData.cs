using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
            public bool UseNavigationSetting = false;
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
            public float _maxWaitingTime = 1.5f;
            /// <summary>
            /// The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call.
            /// If user the method call time interval less than this value, the call will be ignore!
            /// </summary>
            public float minimumTimeInterval = 0.2f;
        }

    }

    public class VectorConvert
    {
        public static Vector3 StringToVector3(string sVector)
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
        public static Vector2 StringToVector2(string sVector)
        {
            // Remove the parentheses
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
    }

    [System.Serializable]
    public class PropertyOverride
    {
        BindingFlags defaultBindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        public object GetValue()
        {
            switch (s_Type)
            {
                case S_Type._vector3:
                    return VectorConvert.StringToVector3(StringValue);
                case S_Type._vector2:
                    return VectorConvert.StringToVector2(StringValue);
                case S_Type._bool:
                    return System.Convert.ToBoolean(StringValue);
                case S_Type._float:
                    return float.Parse(StringValue, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                case S_Type._int:
                    return int.Parse(StringValue, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                case S_Type._color:
                    return ColorUtility.TryParseHtmlString("#" + StringValue, out Color c) ? c : Color.black;
                case S_Type._objcetReferenct:
                    return ObjectReferenceValue;
                case S_Type._string:
                    return StringValue;
                case S_Type._enum:
                    {
                        var s = StringValue.Split(',');
                        var enumType = CloudMacaca.Utility.GetType(s[0]);
                        return System.Enum.Parse(enumType, s[1], false);
                    }
                case S_Type._enumFlag:
                    {
                        var s = StringValue.Split(',');
                        var enumType = CloudMacaca.Utility.GetType(s[0]);
                        int v = 0;
                        int.TryParse(s[1], out v);
                        return System.Enum.ToObject(enumType, v);
                    }
                default:
                    return null;
            }
        }
#if UNITY_EDITOR
        public void SetValue(UnityEditor.SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case UnityEditor.SerializedPropertyType.Vector3:
                    SetValue(property.vector3Value);
                    break;
                case UnityEditor.SerializedPropertyType.Vector2:
                    SetValue(property.vector2Value);
                    break;
                case UnityEditor.SerializedPropertyType.Vector3Int:
                    SetValue(property.vector3IntValue);
                    break;
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
                    if (type == null)
                    {
                        ViewSystemLog.LogError($"The override property cannot find {property.propertyPath}");
                        break;
                    }
                    var e = System.Enum.Parse(type, property.enumNames[property.enumValueIndex]);
                    SetValue(e);
                    break;
            }
        }
        public System.Type TryFindEnumType(UnityEngine.Object unityObject, string propertyPath)
        {
            var to = unityObject.GetType();
            var fildInfo = to.GetField(propertyPath, defaultBindingFlags);

            if (fildInfo != null)
            {
                return fildInfo.FieldType;
            }
            var propertyInfo = to.GetProperty(propertyPath, defaultBindingFlags);
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
            else if (value is Vector3)
            {
                s_Type = S_Type._vector3;
                StringValue = ((Vector3)value).ToString("F3");
                toStringDirectly = false;
            }
            else if (value is Vector2)
            {
                s_Type = S_Type._vector2;
                StringValue = ((Vector2)value).ToString("F3");
                toStringDirectly = false;
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
            else if (value.GetType().IsEnum)
            {
                bool isFlag = false;
                isFlag = value.GetType().GetCustomAttribute(typeof(System.FlagsAttribute)) != null;
                if (isFlag)
                {
                    s_Type = S_Type._enumFlag;
                    StringValue = value.GetType().ToString() + "," + (int)value;
                }
                else
                {
                    s_Type = S_Type._enum;
                    StringValue = value.GetType().ToString() + "," + value.ToString();
                }

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

        public void SetType(S_Type t)
        {
            s_Type = t;
        }
        //Do not modify the order of this enum to avoid serilize problem in unity
        public enum S_Type
        {
            _bool, _float, _int, _color, _objcetReferenct, _string, _enum, _vector3, _vector2, _enumFlag
        }
        public S_Type s_Type = S_Type._string;
        public UnityEngine.Object ObjectReferenceValue;
        public string StringValue;
    }

}