using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
using UnityEngine.Events;
using System;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace CloudMacaca.ViewSystem
{
    public class ViewRuntimeOverride : SerializedMonoBehaviour
    {
        #region EventOverride
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
                    targetTansform = transform;
                }
                else
                {
                    targetTansform = transform.Find(p[0]);
                }

                EventRuntimeDatas eventRuntimeDatas;

                if (!cachedUnityEvent.TryGetValue(item.Key, out eventRuntimeDatas))
                {
                    //p[1] is targetComponentType
                    Component selectable = targetTansform.GetComponent(p[1]);
                    //p[2] is targetPropertyPath
                    UnityEvent unityEvent = (UnityEvent)GetProperty(selectable, p[2]);
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
                    eventRuntimeDatas.unityEvent.AddListener(
                        delegate
                        {
                            if (ViewControllerV2.Instance.IsPageTransition)
                            {
                                Debug.Log("The page is in transition, event will not fire!");
                                return;
                            }
                            openDelegate.Invoke((UnityEngine.EventSystems.UIBehaviour)eventRuntimeDatas.selectable);
                        });
                }
            }
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
        #endregion

        #region  Property Override
        public void ResetToDefaultValues()
        {
            foreach (var item in currentModifiedField)
            {
                // if (isUnityEngineType(item.type))
                // {
                PrefabDefaultField defaultField;
                if (prefabDefaultFields.TryGetValue(item, out defaultField))
                {
                    SetProperty(cachedComponent[defaultField.id], defaultField.field, defaultField.defaultValue);
                }
                //Debug.Log($"Reset [{gameObject.name}] [{item.type}] on [{ cachedComponent[item.id]}] field [{item.field}] to [{item.orignalValue}]");
                // }
                // else
                // {
                //     SetField(item.type, cachedComponent[item.id], item.field, item.orignalValue);
                // }
            }
            currentModifiedField.Clear();
        }
        static BindingFlags bindingFlags =
            BindingFlags.NonPublic |
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static;
        Dictionary<string, UnityEngine.Object> cachedComponent = new Dictionary<string, UnityEngine.Object>();
        public void ApplyOverride(IEnumerable<ViewElementPropertyOverrideData> overrideDatas)
        {
            ResetToDefaultValues();
            foreach (var item in overrideDatas)
            {
                var id = item.targetTransformPath + "_" + item.targetComponentType;
                UnityEngine.Object c;
                Transform targetTansform = transform.Find(item.targetTransformPath);
                if (targetTansform == null)
                {
                    Debug.LogError($"Target GameObject cannot be found [{transform.name} / {item.targetTransformPath}]");
                    continue;
                }
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
                    if (c == null)
                    {
                        Debug.LogError($"Target Component cannot be found [{item.targetComponentType}] on GameObject [{transform.name } / {item.targetTransformPath}]");
                        continue;
                    }
                    cachedComponent.Add(id, c);
                }

                var idForProperty = id + "_" + item.targetPropertyPath;
                if (!prefabDefaultFields.ContainsKey(idForProperty))
                {
                    prefabDefaultFields.Add(idForProperty, new PrefabDefaultField(GetProperty(c, item.targetPropertyPath), id, item.targetPropertyPath));
                }
                currentModifiedField.Add(idForProperty);
                SetProperty(c, item.targetPropertyPath, item.Value.GetValue());
            }
        }
        bool isUnityEngineType(System.Type t)
        {
            return t.ToString().Contains("UnityEngine");
        }
        public void SetProperty(object inObj, string fieldName, object newValue)
        {
            System.Type t = inObj.GetType();
            //GameObject hack
            // Due to GameObject.active is obsolete and ativeSelf is read only
            // Use a hack function to override GameObject's active status.
            if (t == typeof(GameObject) && fieldName == "active")
            {
                ((GameObject)inObj).SetActive((bool)newValue);
                return;
            }
            //Debug.Log($"SetProperty on [{gameObject.name}] [{t.ToString()}] on [{fieldName}] target Value {newValue}");
            System.Reflection.PropertyInfo info = t.GetProperty(fieldName, bindingFlags);
            if (info != null)
                info.SetValue(inObj, newValue);
        }

        private object GetProperty(object inObj, string fieldName)
        {
            System.Type t = inObj.GetType();
            //GameObject hack
            // Due to GameObject.active is obsolete and ativeSelf is read only
            // Use a hack function to override GameObject's active status.
            if (t == typeof(GameObject) && fieldName == "active")
            {
                return ((GameObject)inObj).activeSelf;
            }
            object ret = null;
            System.Reflection.PropertyInfo info = t.GetProperty(fieldName, bindingFlags);
            if (info != null)
                ret = info.GetValue(inObj);

            //Debug.Log($"GetProperty on [{gameObject.name}] Target Object {((UnityEngine.Object)inObj).name} [{t.ToString()}] on [{fieldName}]  Value [{ret}]");
            return ret;
        }

        public void SetField(System.Type t, object inObj, string fieldName, object newValue)
        {
            System.Reflection.FieldInfo info = t.GetField(fieldName, bindingFlags);
            if (info != null)
                info.SetValue(inObj, newValue);
        }

        private object GetField(System.Type t, object inObj, string fieldName)
        {
            object ret = null;
            System.Reflection.FieldInfo info = t.GetField(fieldName, bindingFlags);
            if (info != null)
                ret = info.GetValue(inObj);
            return ret;
        }

        List<string> currentModifiedField = new List<string>();
        [SerializeField]
        Dictionary<string, PrefabDefaultField> prefabDefaultFields = new Dictionary<string, PrefabDefaultField>();
        class PrefabDefaultField
        {
            public PrefabDefaultField(object orignalValue, string id, string field)
            {
                this.defaultValue = orignalValue;
                this.id = id;
                this.field = field;
            }
            [SerializeField]
            public object defaultValue;
            public string id;
            public string field;
        }
    }
    #endregion
}
