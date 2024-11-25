using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System.ComponentModel;

namespace MacacaGames.ViewSystem
{
    [System.Serializable]
    public class View
    {
        public string name;
        public List<ViewPageItem> viewPageItems = new List<ViewPageItem>();
    }
    [System.Serializable]
    public class ViewState : View
    {
        public int targetFrameRate = -1;
    }

    [System.Serializable]
    public class ViewPage : View
    {
        public enum ViewPageType
        {
            FullPage, Overlay
        }
        public enum ViewPageTransitionTimingType
        {
            WithPervious, AfterPervious, Custom
        }
        public RectTransform runtimePageRoot;
        public float customPageTransitionWaitTime = 0.5f;
        public string viewState = "";
        public ViewPageType viewPageType = ViewPageType.FullPage;
        public ViewPageTransitionTimingType viewPageTransitionTimingType = ViewPageTransitionTimingType.WithPervious;
        public int canvasSortOrder = 0;
        public bool ignoreAutoSorting = false;
        #region SafeArea
        public SafePadding.PerEdgeValues edgeValues = new SafePadding.PerEdgeValues();
        public bool flipPadding = false;
        public bool useGlobalSafePadding = true;

        #endregion
        #region Navigation
        public bool IsNavigation = false;
        public ViewElementNavigationTarget _firstSelectSetting;
        public UnityEngine.UI.Selectable firstSelected
        {
            get
            {
                if (string.IsNullOrEmpty(_firstSelectSetting.viewPageItemId))
                {
                    return viewPageItems.SelectMany(m => m.runtimeViewElement.GetComponentsInChildren<UnityEngine.UI.Selectable>()).FirstOrDefault();
                }
                else
                {
                    var vpi = viewPageItems.SingleOrDefault(m => m.Id == _firstSelectSetting.viewPageItemId);
                    var targetTransform = vpi.runtimeViewElement.runtimeOverride.GetTransform(_firstSelectSetting.targetTransformPath);
                    var com = vpi.runtimeViewElement.runtimeOverride.GetCachedComponent(targetTransform, _firstSelectSetting.targetTransformPath, _firstSelectSetting.targetComponentType);
                    return (UnityEngine.UI.Selectable)com.Component;
                }
            }
        }
        public List<ViewElementNavigationDataViewState> navigationDatasForViewState = new List<ViewElementNavigationDataViewState>();
        Dictionary<string, List<ViewElementNavigationData>> _navigationDatasForViewStateDict;
        public Dictionary<string, List<ViewElementNavigationData>> stateNavDict
        {
            get
            {
                if (_navigationDatasForViewStateDict == null)
                {
                    _navigationDatasForViewStateDict = navigationDatasForViewState.GroupBy(m => m.viewPageItemId)
                    .ToDictionary(x => x.Key, x => x.SelectMany(m => m.navigationDatas).ToList());
                }
                return _navigationDatasForViewStateDict;
            }
        }
        #endregion
    }

    [System.Serializable]
    public class ViewPageItem
    {
        public string Id;
#if UNITY_EDITOR
        public ViewElement previewViewElement;
#endif
        public string name;

        public string displayName
        {
            get
            {
                if (string.IsNullOrEmpty(name))
                {
                    return viewElement == null ? "ViewElement not Set" : viewElement.name;
                }
                else return name;
            }
        }

        //Due to Unity bug in new prefab workflow, UnityEngine.Object reference which is not a builtin type will missing after prefab enter prefab mode, so currentlly use GameObject to save the reference and get ViewElement by GetCompnent in Runtime
        /// <summary>
        /// The ViewElement's GameObject in Asset
        /// </summary>
        /// <value>ViewElement's GameObject (Asset)</value>
        public GameObject viewElementObject;

        /// <summary>
        /// The ViewElement in Asset
        /// If you wish to do something on ViewElement use <see cref="runtimeViewElement"> to avoid modify on Asset.
        /// </summary>
        /// <value>ViewElement (Asset)</value>
        public ViewElement viewElement
        {
            get
            {
                if (viewElementObject == null)
                {
                    return null;
                }
                return viewElementObject?.GetComponent<ViewElement>();
            }
            set
            {
                viewElementObject = value.gameObject;
            }
        }

        /// <summary>
        /// ViewElement runtime instance
        /// </summary>
        public ViewElement runtimeViewElement = null;
        public List<ViewElementPropertyOverrideData> overrideDatas = new List<ViewElementPropertyOverrideData>();
        public List<ViewElementEventData> eventDatas = new List<ViewElementEventData>();
        public Transform runtimeParent = null;

        #region Obsolete transform data, only keeps for update tools 
        public Transform parent;
        public string parentPath;
        public ViewSystemRectTransformData transformData = new ViewSystemRectTransformData();
        public ViewElement.RectTransformFlag transformFlag = ViewElement.RectTransformFlag.All;
        #endregion

        public ViewElementTransform defaultTransformDatas = new ViewElementTransform();
        public List<BreakPointViewElementTransform> breakPointViewElementTransforms = new List<BreakPointViewElementTransform>();

        public float TweenTime = 0.4f;
        public EaseStyle easeType = EaseStyle.QuadEaseOut;
        public float delayIn;
        public float delayOut;
        public List<ViewElementNavigationData> navigationDatas = new List<ViewElementNavigationData>();
        public PlatformOption excludePlatform = PlatformOption.Nothing;

        public int sortingOrder = 0;

        [System.Flags]
        public enum PlatformOption
        {
            Nothing = 0,
            Android = 1 << 0,
            iOS = 1 << 1,
            UWP = 1 << 2,
            tvOS = 1 << 3,
            All = Android | iOS | UWP | tvOS// Custom name for "Everything" option
        }
        public ViewPageItem(ViewElement ve)
        {
            viewElement = ve;
            GenerateId();
        }
        public ViewPageItem()
        {
            GenerateId();
        }
    
        public void GenerateId()
        {
            if (string.IsNullOrEmpty(Id))
                Id = System.Guid.NewGuid().ToString().Substring(0, 8);
        }
        public ViewElementTransform GetCurrentViewElementTransform(Dictionary<string, bool> currentBreakPoints)
        {
            if (breakPointViewElementTransforms != null &&
                breakPointViewElementTransforms.Count > 0 &&
                currentBreakPoints != null)
            {
                foreach (var item in breakPointViewElementTransforms)
                {
                    if (currentBreakPoints.ContainsKey(item.breakPointName) && currentBreakPoints[item.breakPointName])
                    {
                        return item.transformData;
                    }
                }
            }
            return defaultTransformDatas;
        }
    }

    [System.Serializable]
    public class ViewElementTransform
    {
        public Transform parent;
        public string parentPath;
        public ViewSystemRectTransformData rectTransformData = new ViewSystemRectTransformData();
        public ViewElement.RectTransformFlag rectTransformFlag = ViewElement.RectTransformFlag.All;

    }
    [System.Serializable]
    public class BreakPointViewElementTransform
    {
        public string breakPointName;
        public ViewElementTransform transformData = new ViewElementTransform();
    }

    [System.Serializable]
    public class ViewElementNavigationDataViewState
    {
        public string viewPageItemId;
        public List<ViewElementNavigationData> navigationDatas = new List<ViewElementNavigationData>();
    }

    [System.Serializable]
    public class ViewElementNavigationTarget : ViewSystemComponentData
    {
        public string viewPageItemId;
    }

    [System.Serializable]
    public class ViewElementNavigationData : ViewSystemComponentData
    {
        public ViewElementNavigationData()
        {
            targetPropertyName = "m_Navigation";
        }
        public UnityEngine.UI.Navigation.Mode mode;
        public UnityEngine.UI.Navigation navigation
        {
            get
            {
                var result = UnityEngine.UI.Navigation.defaultNavigation;
                result.mode = mode;
                return result;
            }
        }
    }

    [System.Serializable]
    public class ViewElementEventData : ViewSystemComponentData
    {
        public string scriptName;
        public string methodName;
    }

    [System.Serializable, SerializeField]
    public class ViewElementOverride
    {
        [SerializeField]
        List<ViewElementPropertyOverrideData> values;
        public ViewElementOverride()
        {
            values = new List<ViewElementPropertyOverrideData>();
        }

        public List<ViewElementPropertyOverrideData> GetValues()
        {
            return values;
        }

        public void Add(ViewElementPropertyOverrideData item)
        {
            values.Add(item);
        }
        public void AddRange(List<ViewElementPropertyOverrideData> item)
        {
            values.AddRange(item);
        }
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
    public class ViewSystemComponentData
    {
        public string targetTransformPath;
        public string targetComponentType;
        /// This value is save as SerializedProperty.PropertyPath
        public string targetPropertyName;
    }
    [System.Serializable]
    public class ViewSystemRectTransformData
    {
        //scale modify may not require
        public Vector3 localScale = Vector3.one;
        public Vector3 localEulerAngles;
        public Vector3 anchoredPosition;
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public Vector2 sizeDelta = new Vector2(100, 100);
        public Vector2 anchorMin = new Vector2(0.5f, 0.5f);
        public Vector2 anchorMax = new Vector2(0.5f, 0.5f);

        public ViewSystemRectTransformData()
        {

        }
        public ViewSystemRectTransformData(RectTransform rectTransform)
        {
            localScale = rectTransform.localScale;
            localEulerAngles = rectTransform.localEulerAngles;
            anchoredPosition = rectTransform.anchoredPosition3D;
            pivot = rectTransform.pivot;
            sizeDelta = rectTransform.sizeDelta;
            anchorMin = rectTransform.anchorMin;
            anchorMax = rectTransform.anchorMax;
        }
        public void SetRectTransform(RectTransform rectTransform)
        {
            rectTransform.localScale = localScale;
            rectTransform.localEulerAngles = localEulerAngles;
            rectTransform.anchoredPosition3D = anchoredPosition;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }

        Vector2 anchoredPosition2D
        {
            get
            {
                return anchoredPosition;
            }
            set
            {
                anchoredPosition = new Vector3(value.x, value.y, anchoredPosition.z);
            }
        }

        // Inspire from UnityEngine.CoreModule.dll
        public Vector2 offsetMin
        {
            get
            {
                return anchoredPosition2D - Vector2.Scale(sizeDelta, pivot);
            }
            set
            {
                Vector2 vector = value - (anchoredPosition2D - Vector2.Scale(sizeDelta, pivot));
                sizeDelta -= vector;
                anchoredPosition2D += Vector2.Scale(vector, Vector2.one - pivot);
            }
        }

        public Vector2 offsetMax
        {
            get
            {
                return anchoredPosition2D + Vector2.Scale(sizeDelta, Vector2.one - pivot);
            }
            set
            {
                Vector2 vector = value - (anchoredPosition2D + Vector2.Scale(sizeDelta, Vector2.one - pivot));
                sizeDelta += vector;
                anchoredPosition2D += Vector2.Scale(vector, pivot);
            }
        }
    }

    [System.Serializable]
    public class PropertyOverride
    {
        BindingFlags defaultBindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        public bool IsShowRawContent = false;

        public bool IsSpecialPattern
        {
            get
            {
                return StringValue.Contains("{");
            }
        }
        public object GetValue()
        {
            object result = null;
            if (IsSpecialPattern)
            {
                result = ConvertFromModelRefectString(StringValue);
                if (result == null)
                {
                    ViewSystemLog.LogError("Try to get value from ViewSystem Model Binding faild, fallback to default value");
                }
                else
                {
                    return result;
                }
            }

            if (s_Type == S_Type._objcetReferenct)
            {
                result = ObjectReferenceValue;
            }
            else
            {
                result = ConvertFromStringValue(s_Type, StringValue);
            }
            return result;
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
            if (value is int || value is long)
            {
                s_Type = S_Type._int;
            }
            else if (value is Vector3)
            {
                s_Type = S_Type._vector3;
            }
            else if (value is Vector2)
            {
                s_Type = S_Type._vector2;
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
            }
            else if (value == null)
            {
                // Only UnityEngine.Object may be null
                s_Type = S_Type._objcetReferenct;
                ObjectReferenceValue = null;
            }
            else if (value.GetType().IsSubclassOf(typeof(UnityEngine.Object)) ||
                    value.GetType().IsAssignableFrom(typeof(UnityEngine.Object)))
            {
                s_Type = S_Type._objcetReferenct;
                ObjectReferenceValue = (UnityEngine.Object)value;
            }
            else if (value.GetType().IsEnum)
            {
                bool isFlag = false;
                isFlag = value.GetType().GetCustomAttribute(typeof(System.FlagsAttribute)) != null;
                if (isFlag)
                {
                    s_Type = S_Type._enumFlag;
                }
                else
                {
                    s_Type = S_Type._enum;
                }
            }
            StringValue = ConvertToStringValue(value);
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
        public string ModelReflectValue;

        public static object ConvertFromModelRefectString(string StringValue)
        {
            var data = ParseString(StringValue);
            if (data == null)
            {
                return null;
            }
            // {PageModel.TypeName[key]} format 
            // {PageModel.string} direct usage
            // {PageModel.string[0]} use index
            // {PageModel.string["key"]} use key
            return ViewController.GetModelInstance(data.type, data.key, data.injectScope, data.isMultiple);
        }

        static ReflectData ParseString(string input)
        {
            string pattern = @"\{(.+?)\.([^\[]+?)(?:\[(.+?)\])?\}";
            var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
            bool isKeyInt = false;
            if (match.Success)
            {
                string[] myData = new string[3];
                myData[0] = match.Groups[1].Value; // PageModel
                myData[1] = match.Groups[2].Value; // Property

                if (match.Groups[3].Success)
                {
                    isKeyInt = match.Groups[3].Value.Contains('"');
                    myData[2] = match.Groups[3].Value.Trim('\"'); // Key or Index (if it exists)
                }
                return new ReflectData(myData[0], myData[1], myData[2], isKeyInt);
            }
            return null;
        }

        class ReflectData
        {
            public ReflectData(string source, string type, string key, bool isKeyInt)
            {
                if (!System.Enum.TryParse<InjectScope>(source, out injectScope))
                {
                    // Simple hack to match other available keyword
                    if (source.ToLower() == "model" || source.ToLower() == "pagemodel")
                    {
                        this.injectScope = InjectScope.PageFirst;
                    }
                    else if (source.ToLower() == "sharedmodel")
                    {
                        this.injectScope = InjectScope.SharedFirst;
                    }
                }
                else
                {
                    throw new InvalidEnumArgumentException($"{source} doesn't match any member in Enum InjectScope");
                }

                this.type = Utility.GetType(type);

                if (isKeyInt)
                {
                    ViewSystemLog.LogError("Setting Model in Override Window with array index is not support yet! Will automatically fallback to ViewInjectDictionary's key");
                }

                this.key = key;
            }

            public InjectScope injectScope;
            public System.Type type;
            public string key = null;
            public int? index = null;

            public bool isMultiple
            {
                get
                {
                    return !string.IsNullOrEmpty(key) || index.HasValue;
                }
            }
        }

        public static object ConvertFromStringValue(S_Type sType, string StringValue)
        {
            switch (sType)
            {
                case S_Type._vector3:
                    return VectorConvert.StringToVector3(StringValue);
                case S_Type._vector2:
                    return VectorConvert.StringToVector2(StringValue);
                case S_Type._bool:
                    {
                        try
                        {
                            return System.Convert.ToBoolean(StringValue);
                        }
                        catch
                        {
                            return default(bool);
                        }
                    }
                case S_Type._float:
                    {
                        try
                        {
                            return float.Parse(StringValue, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                        }
                        catch
                        {
                            return default(float);
                        }
                    }
                case S_Type._int:
                    {
                        try
                        {
                            return int.Parse(StringValue, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                        }
                        catch
                        {
                            return default(int);
                        }
                    }
                case S_Type._color:
                    return ColorUtility.TryParseHtmlString("#" + StringValue, out Color c) ? c : Color.black;
                case S_Type._string:
                    return StringValue;
                case S_Type._enum:
                    {
                        try
                        {
                            var s = StringValue.Split(',');
                            var enumType = MacacaGames.Utility.GetType(s[0]);
                            return System.Enum.Parse(enumType, s[1], false);
                        }
                        catch
                        {
                            return 0;
                        }
                    }
                case S_Type._enumFlag:
                    {
                        try
                        {
                            var s = StringValue.Split(',');
                            var enumType = MacacaGames.Utility.GetType(s[0]);
                            int v = 0;
                            int.TryParse(s[1], out v);
                            return System.Enum.ToObject(enumType, v);
                        }
                        catch
                        {
                            return 0;
                        }
                    }
                default:
                    return null;
            }
        }

        public static string ConvertToStringValue(object value)
        {
            if (value == null)
            {
                return "";
            }
            else if (value is Vector3)
            {
                return VectorConvert.Vector3ToString((Vector3)value);
            }
            else if (value is Vector2)
            {
                return VectorConvert.Vector2ToString((Vector2)value);
            }
            else if (value is Color)
            {
                return ColorUtility.ToHtmlStringRGBA((Color)value);
            }
            else if (value.GetType().IsEnum)
            {
                bool isFlag = false;
                isFlag = value.GetType().GetCustomAttribute(typeof(System.FlagsAttribute)) != null;
                if (isFlag)
                {
                    return value.GetType().ToString() + "," + (int)value;
                }
                else
                {
                    return value.GetType().ToString() + "," + value.ToString();
                }
            }
            else
                return value.ToString();
        }
    }
}