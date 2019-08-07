using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
using UnityEngine.Events;
using System;
using UnityEngine.UI;

namespace CloudMacaca.ViewSystem
{
    public class ViewRuntimeOverride : TransformCacheBase
    {


        public ViewElementEventData[] currentEventDatas;
        class EventRuntimeDatas
        {
            public EventRuntimeDatas(UnityEvent unityEvent, Component selectable)
            {
                this.unityEvent = unityEvent;
                this.selectable = selectable;

            }
            public UnityEvent unityEvent;
            public Component selectable;
        }
        delegate void EventDelegate<Self>(Self selectable);
        private static EventDelegate<UnityEngine.EventSystems.UIBehaviour> CreateOpenDelegate(string method, Component target)
        {
            return (EventDelegate<UnityEngine.EventSystems.UIBehaviour>)Delegate.CreateDelegate(type: typeof(EventDelegate<UnityEngine.EventSystems.UIBehaviour>), target, method, true, true);
        }
        Dictionary<string, EventDelegate<UnityEngine.EventSystems.UIBehaviour>> cachedDelegate = new Dictionary<string, EventDelegate<UnityEngine.EventSystems.UIBehaviour>>();
        Dictionary<string, EventRuntimeDatas> cachedUnityEvent = new Dictionary<string, EventRuntimeDatas>();
        public void SetEvent(IEnumerable<ViewElementEventData> eventDatas)
        {
            currentEventDatas = eventDatas.ToArray();

            //Group by Component transform_component_property
            var groupedEventData = eventDatas.GroupBy(item => item.targetTransformPath + "_" + item.targetComponentType + "_" + item.targetPropertyPath);

            foreach (var item in groupedEventData)
            {
                string[] p = item.Key.Split('_');
                //p[0] is targetTransformPath
                Transform targetTansform;
                if (string.IsNullOrEmpty(p[0]))
                {
                    targetTansform = transformCache;
                }
                else
                {
                    targetTansform = transformCache.Find(p[0]);
                }

                EventRuntimeDatas eventRuntimeDatas;

                if (!cachedUnityEvent.TryGetValue(item.Key, out eventRuntimeDatas))
                {
                    //p[1] is targetComponentType
                    Component selectable = targetTansform.GetComponent(p[1]);
                    System.Type t = selectable.GetType();

                    //p[2] is targetPropertyPath
                    UnityEvent unityEvent = (UnityEvent)GetProperty(t, selectable, p[2]);
                    eventRuntimeDatas = new EventRuntimeDatas(unityEvent, selectable);
                    cachedUnityEvent.Add(item.Key, eventRuntimeDatas);
                }

                // Clear last event
                eventRuntimeDatas.unityEvent.RemoveAllListeners();

                // Usually there is only one event on one Selectable
                // But the system allow mutil event on one Selectable
                foreach (var item2 in item)
                {
                    var id_delegate = item2.scriptName + "_" + item2.methodName;
                    EventDelegate<UnityEngine.EventSystems.UIBehaviour> openDelegate;
                    if (!cachedDelegate.TryGetValue(id_delegate, out openDelegate))
                    {
                        // Get Method
                        Type type = Utility.GetType(item2.scriptName);
                        //MethodInfo method = type.GetMethod(item2.methodName);

                        //The method impletment Object
                        Component scriptInstance = (Component)FindObjectOfType(type);

                        if (scriptInstance == null)
                        {
                            scriptInstance = GenerateScriptInstance(type);
                        }

                        //Create Open Delegate
                        try
                        {
                            openDelegate = CreateOpenDelegate(item2.methodName, scriptInstance);
                        }
                        catch
                        {
                            Debug.LogError("Binding Event faild", this);
                        }
                        cachedDelegate.Add(id_delegate, openDelegate);
                    }
                    eventRuntimeDatas.unityEvent.AddListener(delegate { openDelegate.Invoke((UnityEngine.EventSystems.UIBehaviour)eventRuntimeDatas.selectable); });
                }
            }
            // foreach (var item in eventDatas)
            // {
            //     var id_unityEvent = item.targetTransformPath + "_" + item.targetComponentType;

            //     Transform targetTansform;
            //     if (string.IsNullOrEmpty(item.targetTransformPath))
            //     {
            //         targetTansform = transformCache;
            //     }
            //     else
            //     {
            //         targetTansform = transformCache.Find(item.targetTransformPath);
            //     }

            //     EventRuntimeDatas eventRuntimeDatas;

            //     if (!cachedUnityEvent.TryGetValue(id_unityEvent, out eventRuntimeDatas))
            //     {
            //         Component selectable = targetTansform.GetComponent(item.targetComponentType);
            //         System.Type t = selectable.GetType();
            //         UnityEvent unityEvent = (UnityEvent)GetProperty(t, selectable, item.targetPropertyPath);
            //         eventRuntimeDatas = new EventRuntimeDatas(unityEvent, selectable);
            //         cachedUnityEvent.Add(id_unityEvent, eventRuntimeDatas);
            //     }

            //     var id_delegate = item.scriptName + "_" + item.methodName;
            //     EventDelegate<Selectable> openDelegate;
            //     if (!cachedDelegate.TryGetValue(id_delegate, out openDelegate))
            //     {
            //         // Get Method
            //         Type type = Utility.GetType(item.scriptName);
            //         MethodInfo method = type.GetMethod(item.methodName);

            //         //The method impletment Object
            //         var scriptInstance = (MonoBehaviour)FindObjectOfType(type);

            //         //Create Open Delegate
            //         openDelegate = CreateOpenDelegate(method, scriptInstance);
            //         cachedDelegate.Add(id_delegate, openDelegate);
            //     }
            //     eventRuntimeDatas.unityEvent.RemoveAllListeners();
            //     eventRuntimeDatas.unityEvent.AddListener(delegate { openDelegate.Invoke((Selectable)eventRuntimeDatas.selectable); });
            // }
        }

        const string GeneratedScriptInstanceGameObjectName = "Generated_ViewSystem";
        Component GenerateScriptInstance(Type type)
        {
            var go = GameObject.Find(GeneratedScriptInstanceGameObjectName);

            if (go == null)
            {
                go = new GameObject(GeneratedScriptInstanceGameObjectName);
            }
            return go.AddComponent(type);
        }

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
        Dictionary<string, UnityEngine.Object> cachedComponent = new Dictionary<string, UnityEngine.Object>();
        public void ApplyOverride(IEnumerable<ViewElementPropertyOverrideData> overrideDatas)
        {
            foreach (var item in overrideDatas)
            {
                var id = item.targetTransformPath + "_" + item.targetComponentType;
                UnityEngine.Object c;
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
