using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    public class ViewRuntimeOverride : TransformCacheBase
    {
        public void ResetLastOverride()
        {
            foreach (var item in modifiedFields)
            {
                SetField(cachedComponent[item.id], item.field, item.orignalValue);
            }
            modifiedFields.Clear();
        }
        static BindingFlags bindingFlags = BindingFlags.Public |
                                BindingFlags.NonPublic |
                                BindingFlags.Instance |
                                BindingFlags.Static;
        Dictionary<string, Component> cachedComponent = new Dictionary<string, Component>();
        public void ApplyOverride(IEnumerable<ViewElementPropertyOverrideData> overrideDatas)
        {
            foreach (var item in overrideDatas)
            {
                var id = item.targetTransformPath + "_" + item.targetComponentType;

                Component c;
                if (!cachedComponent.TryGetValue(id, out c))
                {
                    c = transformCache.Find(item.targetTransformPath).GetComponent(item.targetComponentType);
                    cachedComponent.Add(id, c);
                }

                modifiedFields.Add(new ModifiedField(GetField(c, item.targetPropertyPath), id, item.targetPropertyName));
                // Debug.Log(name);
                // Debug.Log(c.GetType());

                // foreach (System.Reflection.FieldInfo field in c.GetType().GetFields(bindingFlags))
                // {
                //     Debug.Log(field.Name);
                // }

                SetField(c, item.targetPropertyPath, item.Value.GetDirtyValue());
            }
        }

        public static void SetField(object inObj, string fieldName, object newValue)
        {
            System.Reflection.FieldInfo info = inObj.GetType().GetField(fieldName, bindingFlags);
            if (info != null)
                info.SetValue(inObj, newValue);
        }
        private static object GetField(object inObj, string fieldName)
        {
            object ret = null;
            System.Reflection.FieldInfo info = inObj.GetType().GetField(fieldName, bindingFlags);
            if (info != null)
                ret = info.GetValue(inObj);
            return ret;
        }
        [SerializeField]
        List<ModifiedField> modifiedFields = new List<ModifiedField>();
        [System.Serializable]
        class ModifiedField
        {
            public ModifiedField(object orignalValue, string id, string field)
            {
                this.orignalValue = orignalValue;
                this.id = id;
                this.field = field;
            }
            public object orignalValue;
            public string id;
            public string field;
        }
    }
}
