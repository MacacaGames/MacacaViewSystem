using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
namespace CloudMacaca.ViewSystem
{
    public class ViewRuntimeOverride : TransformCacheBase
    {
        public void ResetLastOverride()
        {

            foreach (var item in modifiedFields)
            {
                if (isUnityEngineType(item.type))
                {
                    SetProperty(item.type, cachedComponent[item.id], item.field, item.orignalValue);
                }
                else
                {
                    SetField(item.type, cachedComponent[item.id], item.field, item.orignalValue);
                }
                //Debug.Log("set " + item.type + " to " + item.orignalValue + " on " + cachedComponent[item.id], this);

            }
            modifiedFields.Clear();
        }
        static BindingFlags bindingFlags =
         BindingFlags.NonPublic |

                            BindingFlags.Public |
                                BindingFlags.Instance |
                                BindingFlags.Static;
        Dictionary<string, Object> cachedComponent = new Dictionary<string, Object>();
        public void ApplyOverride(IEnumerable<ViewElementPropertyOverrideData> overrideDatas)
        {
            foreach (var item in overrideDatas)
            {
                var id = item.targetTransformPath + "_" + item.targetComponentType;
                Object c;
                Transform targetTansform = transformCache.Find(item.targetTransformPath);
                if (!cachedComponent.TryGetValue(id, out c))
                {
                    if (item.targetComponentType.Contains("GameObject"))
                    {
                        c = targetTansform.gameObject;
                    }
                    else
                    {
                        c = targetTansform.GetComponent(item.targetComponentType);
                    }
                    cachedComponent.Add(id, c);
                }
                System.Type t = c.GetType();

                if (isUnityEngineType(t))
                {
                    modifiedFields.Add(new ModifiedField(t, GetProperty(t, c, item.targetPropertyPath), id, item.targetPropertyPath));
                    SetProperty(t, c, item.targetPropertyPath, item.Value.GetDirtyValue());
                }
                else
                {
                    modifiedFields.Add(new ModifiedField(t, GetField(t, c, item.targetPropertyName), id, item.targetPropertyName));
                    SetField(t, c, item.targetPropertyName, item.Value.GetDirtyValue());
                }
            }
        }
        bool isUnityEngineType(System.Type t)
        {
            return t.ToString().Contains("UnityEngine");
        }
        public static void SetProperty(System.Type t, object inObj, string fieldName, object newValue)
        {
            System.Reflection.PropertyInfo info = t.GetProperty(fieldName, bindingFlags);
            if (info != null)
                info.SetValue(inObj, newValue);
        }
        private static object GetProperty(System.Type t, object inObj, string fieldName)
        {
            object ret = null;
            System.Reflection.PropertyInfo info = t.GetProperty(fieldName, bindingFlags);
            if (info != null)
                ret = info.GetValue(inObj);
            return ret;
        }
        public static void SetField(System.Type t, object inObj, string fieldName, object newValue)
        {
            System.Reflection.FieldInfo info = t.GetField(fieldName, bindingFlags);
            if (info != null)
                info.SetValue(inObj, newValue);
        }
        private static object GetField(System.Type t, object inObj, string fieldName)
        {
            object ret = null;
            System.Reflection.FieldInfo info = t.GetField(fieldName, bindingFlags);
            if (info != null)
                ret = info.GetValue(inObj);
            return ret;
        }
        [SerializeField]
        List<ModifiedField> modifiedFields = new List<ModifiedField>();

        [System.Serializable]
        class ModifiedField
        {
            public ModifiedField(System.Type type, object orignalValue, string id, string field)
            {
                this.orignalValue = orignalValue;
                this.id = id;
                this.field = field;
                this.type = type;
            }
            public object orignalValue;
            public string id;
            public string field;
            public System.Type type;
        }
    }
}
