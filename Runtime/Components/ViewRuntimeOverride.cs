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
    [DisallowMultipleComponent]
    public class ViewRuntimeOverride : MonoBehaviour
    {
        #region NavigationOverride
        // ViewElementNavigationData[] navigationDatas;
        public void ApplyNavigation(IEnumerable<ViewElementNavigationData> navigationDatas)
        {
            //this.navigationDatas = navigationDatas.ToArray();
            foreach (var item in navigationDatas)
            {
                Transform targetTansform = GetTransform(item.targetTransformPath);
                if (targetTansform == null)
                {
                    ViewSystemLog.LogError($"Target GameObject cannot be found [{transform.name} / {item.targetTransformPath}]");
                    continue;
                }

                var result = GetCachedComponent(targetTansform, item.targetTransformPath, item.targetComponentType);
                SetPropertyValue(result.Component, item.targetPropertyName, item.navigation);
            }
        }
        Dictionary<int, UnityEngine.UI.Navigation.Mode> lastNavigationDatas = new Dictionary<int, Navigation.Mode>();
        public void DisableNavigation()
        {
            lastNavigationDatas.Clear();
            var selectables = GetComponentsInChildren<Selectable>();
            foreach (var item in selectables)
            {
                var nav = item.navigation;
                lastNavigationDatas.Add(item.GetInstanceID(), nav.mode);
                nav.mode = UnityEngine.UI.Navigation.Mode.None;
                item.navigation = nav;
            }
        }

        public void RevertToLastNavigation()
        {
            var selectables = GetComponentsInChildren<Selectable>();
            foreach (var item in selectables)
            {
                if (lastNavigationDatas.TryGetValue(item.GetInstanceID(), out Navigation.Mode mode))
                {
                    var nav = item.navigation;
                    nav.mode = mode;
                    item.navigation = nav;
                }
            }
        }

        #endregion
        #region EventOverride
        [SerializeField]
        ViewElementEventData[] currentEventDatas;
        class EventRuntimeDatas
        {
            public EventRuntimeDatas(UnityEventBase unityEvent, Component selectable)
            {
                this.unityEvent = unityEvent;
                this.selectable = selectable;
            }
            public UnityEventBase unityEvent;
            public Component selectable;
        }
        delegate void EventDelegate<Self>(Self selectable);
        private static EventDelegate<Component> CreateOpenDelegate(string method, Component target)
        {
            return (EventDelegate<Component>)
                Delegate.CreateDelegate(type: typeof(EventDelegate<Component>), target, method, true, true);
        }
        Dictionary<string, EventDelegate<Component>> cachedDelegate = new Dictionary<string, EventDelegate<Component>>();
        Dictionary<string, EventRuntimeDatas> cachedUnityEvent = new Dictionary<string, EventRuntimeDatas>();
        public void ClearAllEvent()
        {
            foreach (var item in cachedUnityEvent)
            {
                item.Value.unityEvent.RemoveAllListeners();
            }
        }

        public void SetEvent(IEnumerable<ViewElementEventData> eventDatas)
        {
            currentEventDatas = eventDatas.ToArray();

            //Group by Component transform_component_property
            var groupedEventData = eventDatas.GroupBy(item => item.targetTransformPath + "," + item.targetComponentType + "," + item.targetPropertyName);

            foreach (var item in groupedEventData)
            {
                string[] p = item.Key.Split(',');
                //p[0] is targetTransformPath
                Transform targetTansform = GetTransform(p[0]);
                if (targetTansform == null)
                {
                    ViewSystemLog.LogError($"Target GameObject cannot be found [{transform.name} / {p[0]}]");
                    continue;
                }

                EventRuntimeDatas eventRuntimeDatas;

                if (!cachedUnityEvent.TryGetValue(item.Key, out eventRuntimeDatas))
                {
                    //p[1] is targetComponentType
                    //Component selectable = ViewSystemUtilitys.GetComponent(targetTansform, p[1]);
                    var result = GetCachedComponent(targetTansform, p[0], p[1]);
                    //p[2] is targetPropertyPath
                    string property = p[2];
                    if (p[1].Contains("UnityEngine."))
                    {
                        property = ViewSystemUtilitys.ParseUnityEngineProperty(p[2]);
                    }
                    var unityEvent = (UnityEventBase)GetPropertyValue(result.Component, property);
                    eventRuntimeDatas = new EventRuntimeDatas(unityEvent, (Component)result.Component);
                    cachedUnityEvent.Add(item.Key, eventRuntimeDatas);

                }

                // Usually there is only one event on one Selectable
                // But the system allow mutil event on one Selectable
                foreach (var item2 in item)
                {
                    var id_delegate = item2.scriptName + "_" + item2.methodName;
                    EventDelegate<Component> openDelegate;

                    //Try to get the cached openDelegate object first
                    //Or create a new openDelegate
                    if (!cachedDelegate.TryGetValue(id_delegate, out openDelegate))
                    {
                        // Get Method
                        Type type = Utility.GetType(item2.scriptName);
                        //MethodInfo method = type.GetMethod(item2.methodName);

                        //The method impletmented Object
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
                        catch (Exception ex)
                        {
                            ViewSystemLog.LogError($"Create event delegate faild, make sure the method or the instance is exinst. Exception:{ex.ToString()}", this);
                        }
                        cachedDelegate.Add(id_delegate, openDelegate);
                    }

                    if (eventRuntimeDatas.unityEvent is UnityEvent events)
                    {
                        events.AddListener(
                            delegate
                            {
                                if (ViewControllerV2.Instance.IsPageTransition)
                                {
                                    ViewSystemLog.LogWarning("The page is in transition, event will not fire!");
                                    return;
                                }
                                openDelegate?.Invoke(eventRuntimeDatas.selectable);
                            }
                        );
                    }
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
                PrefabDefaultField defaultField;
                if (prefabDefaultFields.TryGetValue(item, out defaultField))
                {
                    SetPropertyValue(cachedComponent[defaultField.id], defaultField.field, defaultField.defaultValue);
                }
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
            foreach (var item in overrideDatas)
            {
                Transform targetTansform = GetTransform(item.targetTransformPath);
                if (targetTansform == null)
                {
                    ViewSystemLog.LogError($"Target GameObject cannot be found [{transform.name} / {item.targetTransformPath}]");
                    continue;
                }

                var result = GetCachedComponent(targetTansform, item.targetTransformPath, item.targetComponentType);

                var idForProperty = result.Id + "#" + item.targetPropertyName;
                if (!prefabDefaultFields.ContainsKey(idForProperty))
                {
                    prefabDefaultFields.Add(idForProperty, new PrefabDefaultField(GetPropertyValue(result.Component, item.targetPropertyName), result.Id, item.targetPropertyName));
                }
                currentModifiedField.Add(idForProperty);
                SetPropertyValue(result.Component, item.targetPropertyName, item.Value.GetValue());
            }
        }
        public Transform GetTransform(string targetTransformPath)
        {
            if (string.IsNullOrEmpty(targetTransformPath))
            {
                return transform;
            }
            else
            {
                return transform.Find(targetTransformPath);
            }
        }
        public (string Id, UnityEngine.Object Component) GetCachedComponent(Transform targetTansform, string targetTransformPath, string targetComponentType)
        {
            UnityEngine.Object c = null;
            var id = targetTransformPath + "#" + targetComponentType;
            if (!cachedComponent.TryGetValue(id, out c))
            {
                if (targetComponentType.Contains("UnityEngine.GameObject"))
                {
                    c = targetTansform.gameObject;
                }
                else
                {
                    c = ViewSystemUtilitys.GetComponent(targetTansform, targetComponentType);
                }
                if (c == null)
                {
                    ViewSystemLog.LogError($"Target Component cannot be found [{targetComponentType}] on GameObject [{transform.name } / {targetTransformPath}]");
                }
                cachedComponent.Add(id, c);
            }
            return (id, c);
        }

        public void SetPropertyValue(object inObj, string fieldName, object newValue)
        {
            System.Type t = inObj.GetType();
            //GameObject hack
            // Due to GameObject.active is obsolete and ativeSelf is read only
            // Use a hack function to override GameObject's active status.
            if (t == typeof(GameObject) && fieldName == "m_IsActive")
            {
                ((GameObject)inObj).SetActive((bool)newValue);
                return;
            }

            // Try search Field first than try property
            System.Reflection.FieldInfo fieldInfo = t.GetField(fieldName, bindingFlags);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(inObj, newValue);
                return;
            }
            if (t.ToString().Contains("UnityEngine."))
            {
                fieldName = ViewSystemUtilitys.ParseUnityEngineProperty(fieldName);
            }
            System.Reflection.PropertyInfo info = t.GetProperty(fieldName, bindingFlags);
            if (info != null)
                info.SetValue(inObj, newValue);
        }

        private object GetPropertyValue(object inObj, string fieldName)
        {
            System.Type t = inObj.GetType();
            //GameObject hack
            // Due to GameObject.active is obsolete and ativeSelf is read only
            // Use a hack function to override GameObject's active status.
            if (t == typeof(GameObject) && fieldName == "m_IsActive")
            {
                return ((GameObject)inObj).activeSelf;
            }
            object ret = null;
            // Try search Field first than try property
            System.Reflection.FieldInfo fieldInfo = t.GetField(fieldName, bindingFlags);
            if (fieldInfo != null)
            {
                ret = fieldInfo.GetValue(inObj);
                return ret;
            }
            if (t.ToString().Contains("UnityEngine."))
            {
                fieldName = ViewSystemUtilitys.ParseUnityEngineProperty(fieldName);
            }
            System.Reflection.PropertyInfo info = t.GetProperty(fieldName, bindingFlags);
            if (info != null)
                ret = info.GetValue(inObj);

            //ViewSystemLog.Log($"GetProperty on [{gameObject.name}] Target Object {((UnityEngine.Object)inObj).name} [{t.ToString()}] on [{fieldName}]  Value [{ret}]");
            return ret;
        }

        [SerializeField]
        List<string> currentModifiedField = new List<string>();
        [SerializeField]
        Dictionary<string, PrefabDefaultField> prefabDefaultFields = new Dictionary<string, PrefabDefaultField>();
        struct PrefabDefaultField
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
