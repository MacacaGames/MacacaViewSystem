using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using UnityEngine;
using System;
using System.Reflection;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class OverrideVerifier
    {
        ViewSystemNodeEditor editor;
        GUIStyle windowStyle;
        ViewSystemSaveData saveData;
        public OverrideVerifier(ViewSystemNodeEditor editor, ViewSystemSaveData saveData)
        {
            this.saveData = saveData;
            this.editor = editor;
            windowStyle = new GUIStyle(Drawer.windowStyle);
            RectOffset padding = windowStyle.padding;
            padding.left = 0;
            padding.right = 1;
            padding.bottom = 0;
        }

        List<string> typeNameCannotBeFound = new List<string>();
        public void VerifyComponent()
        {
            if (editor == null)
            {
                Debug.LogError("Cannot verify save data, is editor init correctlly?");
                return;
            }

            if (saveData == null)
            {
                Debug.LogError("Cannot verify save data, is editor init correctlly?");
                return;
            }
            typeNameCannotBeFound.Clear();

            RefreshOverrideDatas();

            foreach (var item in allOverrideDatas)
            {
                var t = CloudMacaca.Utility.GetType(item.targetComponentType);
                if (t == null)
                {
                    Debug.LogError(item.targetComponentType + "  cannot be found");
                    typeNameCannotBeFound.Add(item.targetComponentType);
                }
            }

            if (typeNameCannotBeFound.Count > 0)
            {
                if (EditorUtility.DisplayDialog(
                    "Something goes wrong!",
                    "There are some override component is missing, do you want to open fixer window",
                    "Yes, Please",
                    "Not now"))
                {

                    var window = ScriptableObject.CreateInstance<ComponentFixerWindow>();
                    window.SetData(typeNameCannotBeFound, allOverrideDatas, () =>
                    {
                        //Make sure SetDirty
                        EditorUtility.SetDirty(saveData);
                        VerifyProperty();
                    });
                    window.ShowUtility();
                }
            }
            else
            {
                Debug.Log("Components looks good, let's check properties.");
                VerifyProperty();

            }
        }
        List<string> propertyCannotBeFound = new List<string>();
        List<UnityEngine.Object> tempDummy = new List<UnityEngine.Object>();
        public static BindingFlags bindingFlags =
            BindingFlags.NonPublic |
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static;
        public void VerifyProperty()
        {
            //Data may changed by Component fixer so refresh again.
            RefreshOverrideDatas();
            propertyCannotBeFound.Clear();
            foreach (var item in allOverrideDatas)
            {
                var t = CloudMacaca.Utility.GetType(item.targetComponentType);
                if (t == null)
                {
                    Debug.LogError(item.targetComponentType + " still not fixed cannot be found, will ignore while verify property");
                    continue;
                }
                var propertyName = item.targetPropertyName;
                if (t.ToString().Contains("UnityEngine."))
                {
                    propertyName = ViewSystemUtilitys.ParseUnityEngineProperty(item.targetPropertyName);
                }

                if (t.GetField(item.targetPropertyName, bindingFlags) == null && t.GetProperty(propertyName, bindingFlags) == null)
                {
                    Debug.LogError($"{item.targetPropertyName} in {item.targetComponentType} cannot be found");
                    propertyCannotBeFound.Add(item.targetComponentType + "," + item.targetPropertyName);
                }
            }
            if (propertyCannotBeFound.Count > 0)
            {
                if (EditorUtility.DisplayDialog(
                    "Something goes wrong!",
                    "There are some override property is missing, do you want to open fixer window",
                    "Yes, Please",
                    "Not now"))
                {
                    var window = ScriptableObject.CreateInstance<PropertyFixerWindow>();
                    window.SetData(propertyCannotBeFound, allOverrideDatas, () =>
                    {
                        //Make sure SetDirty
                        EditorUtility.SetDirty(saveData);
                    });
                    window.ShowUtility();
                }
            }
            else
            {
                Debug.Log("Great, everying looks good!");
            }
        }

        List<ViewElementEventData> eventDataNeedToFix = new List<ViewElementEventData>();

        public void VerifyEvents()
        {
            RefreshMethodDatabase();
            RefreshEventDatas();
            eventDataNeedToFix.Clear();
            foreach (var item in allEventDatas)
            {
                var t = CloudMacaca.Utility.GetType(item.scriptName);
                if (t == null)
                {
                    Debug.LogError(item.targetComponentType + " still not fixed cannot be found, will ignore while verify property");
                    eventDataNeedToFix.Add(item);
                    continue;
                }

                if (t.GetMethod(item.methodName, bindingFlags) == null)
                {
                    Debug.LogError($"{item.methodName} in {item.scriptName} cannot be found");
                    eventDataNeedToFix.Add(item);
                    continue;
                }
            }
            if (eventDataNeedToFix.Count > 0)
            {
                if (EditorUtility.DisplayDialog(
                    "Something goes wrong!",
                    "There are some event data is missing, do you want to open fixer window",
                    "Yes, Please",
                    "Not now"))
                {
                    var window = ScriptableObject.CreateInstance<EventFixerWindow>();
                    window.SetData(eventDataNeedToFix, classMethodInfo, () =>
                    {
                        //Make sure SetDirty
                        EditorUtility.SetDirty(saveData);
                    });
                    window.ShowUtility();
                }
            }
            else
            {
                Debug.Log("Great, all events looks good!");
            }
        }

        //對應 方法名稱與 pop index 的字典
        //第 n 個腳本的參照
        Dictionary<string, CMEditorLayout.GroupedPopupData[]> classMethodInfo = new Dictionary<string, CMEditorLayout.GroupedPopupData[]>();
        BindingFlags BindFlagsForScript = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        void RefreshMethodDatabase()
        {
            classMethodInfo.Clear();
            classMethodInfo.Add("Nothing Select", null);
            List<CMEditorLayout.GroupedPopupData> VerifiedMethod = new List<CMEditorLayout.GroupedPopupData>();
            for (int i = 0; i < saveData.globalSetting.EventHandleBehaviour.Count; i++)
            {
                var type = Utility.GetType(saveData.globalSetting.EventHandleBehaviour[i].name);
                if (saveData.globalSetting.EventHandleBehaviour[i] == null) return;
                MethodInfo[] methodInfos = type.GetMethods(BindFlagsForScript);
                VerifiedMethod.Clear();
                foreach (var item in methodInfos)
                {
                    var para = item.GetParameters();
                    if (para.Where(m => m.ParameterType.IsAssignableFrom(typeof(UnityEngine.EventSystems.UIBehaviour))).Count() == 0)
                    {
                        continue;
                    }
                    var eventMethodInfo = new CMEditorLayout.GroupedPopupData { name = item.Name, group = "" };
                    var arrts = System.Attribute.GetCustomAttributes(item);
                    foreach (System.Attribute attr in arrts)
                    {
                        if (attr is ViewEventGroup)
                        {
                            ViewEventGroup a = (ViewEventGroup)attr;
                            eventMethodInfo.group = a.GetGroupName();
                            break;
                        }
                    }
                    VerifiedMethod.Add(eventMethodInfo);
                }
                classMethodInfo.Add(type.ToString(), VerifiedMethod.ToArray());
            }
        }

        List<ViewElementPropertyOverrideData> allOverrideDatas = new List<ViewElementPropertyOverrideData>();
        void RefreshOverrideDatas()
        {
            var overrideDatasInPages = saveData.viewPages.Select(m => m.viewPage).SelectMany(x => x.viewPageItems).SelectMany(i => i.overrideDatas);
            var overrideDatasInStates = saveData.viewStates.Select(m => m.viewState).SelectMany(x => x.viewPageItems).SelectMany(i => i.overrideDatas);
            allOverrideDatas.Clear();
            allOverrideDatas.AddRange(overrideDatasInPages);
            allOverrideDatas.AddRange(overrideDatasInStates);
        }
        List<ViewElementEventData> allEventDatas = new List<ViewElementEventData>();
        void RefreshEventDatas()
        {
            var eventsDatasInPages = saveData.viewPages.Select(m => m.viewPage).SelectMany(x => x.viewPageItems).SelectMany(i => i.eventDatas);
            var eventsDatasInStates = saveData.viewStates.Select(m => m.viewState).SelectMany(x => x.viewPageItems).SelectMany(i => i.eventDatas);
            allEventDatas.Clear();
            allEventDatas.AddRange(eventsDatasInPages);
            allEventDatas.AddRange(eventsDatasInStates);
        }
    }

    public class EventFixerWindow : FixerWindow
    {
        class FixerData
        {
            public FixerData(ViewElementEventData viewElementEventData)
            {
                this.viewElementEventData = viewElementEventData;
                originalMethodName = viewElementEventData.methodName;
                originalScriptName = viewElementEventData.scriptName;
            }
            public bool fix = false;
            public ViewElementEventData viewElementEventData;
            public string originalMethodName;
            public string originalScriptName;
            public string modifyMethodName;
            public string modifyScriptName;
        }
        List<FixerData> needFixEventData;
        Dictionary<string, CMEditorLayout.GroupedPopupData[]> classMethodInfo;
        public void SetData(IEnumerable<ViewElementEventData> needFixEventDataSource, Dictionary<string, CMEditorLayout.GroupedPopupData[]> classMethodInfo, Action OnComplete)
        {
            titleContent = new GUIContent("Missing Events fixer");
            this.classMethodInfo = classMethodInfo;
            this.icon = EditorGUIUtility.FindTexture("MetaFile Icon");
            this.lable = "Select the event your wish to fix";
            needFixEventData = needFixEventDataSource.Select(m => new FixerData(m)).ToList();

            OnAllClick += () =>
           {
               needFixEventData.All(x =>
               {
                   x.fix = true;
                   return true;
               });
           };
            OnNoneClick += () =>
           {
               needFixEventData.All(x =>
                {
                    x.fix = false;
                    return true;
                });
           };
            OnCancelClick += () =>
           {

           };
            OnApplyClick += () =>
           {
               var f = needFixEventData.Where(m => m.fix);
               foreach (var item in f)
               {
                   if (string.IsNullOrEmpty(item.modifyMethodName) || string.IsNullOrEmpty(item.modifyScriptName))
                   {
                       continue;
                   }
                   item.viewElementEventData.scriptName = item.modifyScriptName;
                   item.viewElementEventData.methodName = item.modifyMethodName;
               }
           };
        }
        public override bool CheckBeforeApply()
        {
            return needFixEventData.Where(m => m.fix).Count() == 0;
        }
        public override void OnDrawScrollArea()
        {
            GUILayout.Label(new GUIContent($"Event fixer will replace all saved EventData please be careful", EditorGUIUtility.FindTexture("console.erroricon")));
            foreach (var item in needFixEventData)
            {
                using (var horizon = new EditorGUILayout.HorizontalScope("box"))
                {
                    item.fix = EditorGUILayout.ToggleLeft(GUIContent.none, item.fix, GUILayout.Width(20));
                    using (var vertical = new EditorGUILayout.VerticalScope())
                    {
                        using (var horizon2 = new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label($"Method : [{item.originalMethodName}] in Script : [{item.originalScriptName}]");
                            if (GUILayout.Button("Apply Origin Data"))
                            {
                                item.modifyScriptName = item.originalScriptName;
                                item.modifyMethodName = item.originalMethodName;
                            }
                        }
                        int currentSelectClass = string.IsNullOrEmpty(item.modifyScriptName) ? 0 : classMethodInfo.Values.ToList().IndexOf(classMethodInfo[item.modifyScriptName]);
                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            currentSelectClass = EditorGUILayout.Popup("Event Script", currentSelectClass, classMethodInfo.Select(m => m.Key).ToArray());
                            if (check.changed)
                            {
                                Debug.Log(currentSelectClass);
                                if (currentSelectClass != 0)
                                {
                                    var c = classMethodInfo.ElementAt(currentSelectClass);
                                    item.modifyScriptName = c.Key;
                                    item.modifyMethodName = "";
                                }
                                else
                                {
                                    item.modifyScriptName = "";
                                    item.modifyMethodName = "";
                                }
                            }
                        }
                        if (currentSelectClass != 0)
                        {
                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                using (var horizon2 = new EditorGUILayout.HorizontalScope())
                                {
                                    var c = classMethodInfo.ElementAt(currentSelectClass).Value;
                                    var current = c.SingleOrDefault(m => m.name == item.modifyMethodName);
                                    CMEditorLayout.GroupedPopupField(new GUIContent("Event Method"), c, current,
                                        (select) =>
                                        {
                                            item.modifyMethodName = select.name;
                                        }
                                    );
                                }
                            }
                        }
                    }

                }
            }
        }
    }
    public class PropertyFixerWindow : FixerWindow
    {
        class FixerData
        {
            public FixerData(string originalPropertyName)
            {
                this.originalPropertyName = originalPropertyName;
            }
            public bool fix = false;
            public string originalPropertyName;
            public string modifiedPropertyName;
        }
        List<string> propertyCannotBeFound;
        Dictionary<string, List<FixerData>> fixerDatas = new Dictionary<string, List<FixerData>>();
        IEnumerable<ViewElementPropertyOverrideData> allOverrideDatas;

        Dictionary<string, List<CMEditorLayout.GroupedPopupData>> fieldsInComponents = new Dictionary<string, List<CMEditorLayout.GroupedPopupData>>();

        public void SetData(List<string> propertyCannotBeFound, IEnumerable<ViewElementPropertyOverrideData> allOverrideDatas, Action OnComplete)
        {
            titleContent = new GUIContent("Missing property fixer");
            this.allOverrideDatas = allOverrideDatas;
            this.icon = EditorGUIUtility.FindTexture("MetaFile Icon");
            this.lable = "Select the property your wish to fix";
            fixerDatas = propertyCannotBeFound.Select(
                m =>
                {
                    var x = m.Split(',');
                    return new { componentName = x[0], propertyName = x[1] };
                }
            ).GroupBy(m => m.componentName).ToDictionary(o => o.Key, o => o.Select(r => new FixerData(r.propertyName)).ToList());

            foreach (var item in fixerDatas)
            {
                var type = CloudMacaca.Utility.GetType(item.Key);
                var fis = type.GetFields(OverrideVerifier.bindingFlags).Select(m => new CMEditorLayout.GroupedPopupData { name = m.Name, group = "Filed" });
                var pis = type.GetProperties(OverrideVerifier.bindingFlags).Select(m => new CMEditorLayout.GroupedPopupData { name = m.Name, group = "Property" });

                List<CMEditorLayout.GroupedPopupData> gList = new List<CMEditorLayout.GroupedPopupData>();
                gList.AddRange(pis);
                gList.AddRange(fis);
                fieldsInComponents.Add(item.Key, gList);
            }
            OnAllClick += () =>
            {
                fixerDatas.All(x =>
                {
                    x.Value.All(
                        r =>
                        {
                            r.fix = true;
                            return true;
                        }
                    );
                    return true;
                });
            };
            OnNoneClick += () =>
               {
                   fixerDatas.All(x =>
                   {
                       x.Value.All(
                           r =>
                           {
                               r.fix = false;
                               return true;
                           }
                       );
                       return true;
                   });
               };
            OnCancelClick += () =>
                       {

                       };
            OnApplyClick += () =>
            {
                foreach (var item in fixerDatas)
                {
                    var ac = allOverrideDatas.Where(m => m.targetComponentType == item.Key);

                    foreach (var item2 in item.Value)
                    {
                        if (string.IsNullOrEmpty(item2.modifiedPropertyName))
                        {
                            continue;
                        }
                        ac.Where(m => m.targetPropertyName == item2.originalPropertyName).All(
                            x =>
                            {
                                x.targetPropertyName = item2.modifiedPropertyName;
                                return true;
                            }
                        );
                    }
                }
            };
        }
        public override bool CheckBeforeApply()
        {
            return fixerDatas.SelectMany(x => x.Value).Where(m => m.fix).Count() == 0;
        }
        public override void OnDrawScrollArea()
        {
            float width = (position.width - 80) * 0.5f;
            using (var vertical = new GUILayout.VerticalScope())
            {
                foreach (var item in fixerDatas)
                {
                    var texture = EditorGUIUtility.ObjectContent(null, CloudMacaca.Utility.GetType(item.Key)).image;
                    if (texture == null)
                    {
                        texture = EditorGUIUtility.FindTexture("cs Script Icon");
                    }
                    var _cachedContent = new GUIContent(item.Key, texture);
                    GUILayout.Label(_cachedContent, GUILayout.Height(20));
                    foreach (var item2 in item.Value)
                    {
                        using (var horizon = new GUILayout.HorizontalScope("box"))
                        {
                            item2.fix = EditorGUILayout.ToggleLeft(GUIContent.none, item2.fix, GUILayout.Width(20));
                            GUILayout.Label(new GUIContent(item2.originalPropertyName, item2.originalPropertyName), GUILayout.Width(width));
                            GUILayout.Label(Drawer.arrowIcon);
                            //item.targetComponentScript = (MonoScript)EditorGUILayout.ObjectField(item.targetComponentScript, typeof(MonoScript), false);
                            var current = fieldsInComponents[item.Key].SingleOrDefault(m => m.name == item2.modifiedPropertyName);
                            CMEditorLayout.GroupedPopupField(GUIContent.none, fieldsInComponents[item.Key], current,
                                (select) =>
                                {
                                    item2.modifiedPropertyName = select.name;
                                }
                            );
                        }
                    }
                }
            }
        }
    }
    public class ComponentFixerWindow : FixerWindow
    {
        class FixerData
        {
            public FixerData(string originalComponentName)
            {
                this.originalComponentName = originalComponentName;
            }
            public bool fix = false;
            public string originalComponentName;
            public MonoScript targetComponentScript;
        }
        List<FixerData> fixerDatas = new List<FixerData>();
        IEnumerable<ViewElementPropertyOverrideData> allOverrideDatas;
        public void SetData(List<string> typeNameCannotBeFound, IEnumerable<ViewElementPropertyOverrideData> allOverrideDatas, Action OnComplete)
        {
            titleContent = new GUIContent("Missing component fixer");
            this.allOverrideDatas = allOverrideDatas;
            this.OnComplete = OnComplete;
            this.icon = EditorGUIUtility.FindTexture("MetaFile Icon");
            this.lable = "Select the component your wish to fix";
            foreach (var item in typeNameCannotBeFound)
            {
                fixerDatas.Add(new FixerData(item));
            }

            OnAllClick += () =>
            {
                fixerDatas.All(x =>
                {
                    x.fix = true;
                    return true;
                });
            };
            OnNoneClick += () =>
           {
               fixerDatas.All(x =>
                {
                    x.fix = false;
                    return true;
                });
           };
            OnCancelClick += () =>
           {

           };
            OnApplyClick += () =>
           {
               var f = fixerDatas.Where(m => m.fix);
               foreach (var item in f)
               {
                   if (item.targetComponentScript == null)
                   {
                       continue;
                   }
                   allOverrideDatas.Where(m => m.targetComponentType == item.originalComponentName).All(
                       (x) =>
                       {

                           x.targetComponentType = item.targetComponentScript.GetClass().ToString();
                           return true;
                       }
                   );
               }
           };
        }
        public override bool CheckBeforeApply()
        {
            return fixerDatas.Where(m => m.fix).Count() == 0;
        }
        public override void OnDrawScrollArea()
        {
            float width = (position.width - 80) * 0.66f;
            using (var vertical = new GUILayout.VerticalScope())
            {
                foreach (var item in fixerDatas)
                {
                    using (var horizon = new GUILayout.HorizontalScope("box"))
                    {
                        item.fix = EditorGUILayout.ToggleLeft(GUIContent.none, item.fix, GUILayout.Width(20));
                        GUILayout.Label(new GUIContent(item.originalComponentName, item.originalComponentName), GUILayout.Width(width));
                        GUILayout.Label(Drawer.arrowIcon);
                        item.targetComponentScript = (MonoScript)EditorGUILayout.ObjectField(item.targetComponentScript, typeof(MonoScript), false);
                    }
                }
            }
        }
    }
    public class FixerWindow : EditorWindow
    {
        protected Action OnComplete;
        Vector2 scrollPos;
        protected Texture icon;
        protected string lable;
        public virtual void OnDrawScrollArea() { }
        protected Action OnAllClick;
        protected Action OnNoneClick;
        protected Action OnCancelClick;
        protected Action OnApplyClick;

        public virtual bool CheckBeforeApply()
        {
            return false;
        }
        public void OnGUI()
        {
            maxSize = new Vector2(600, 400);
            using (var horizon = new GUILayout.HorizontalScope(new GUIStyle("AnimationKeyframeBackground"), GUILayout.Height(24)))
            {
                GUILayout.Label(new GUIContent(lable, icon), new GUIStyle("AM MixerHeader2"));
            }
            using (var scroll = new GUILayout.ScrollViewScope(scrollPos))
            {
                scrollPos = scroll.scrollPosition;
                OnDrawScrollArea();
            }

            using (var horizon = new GUILayout.HorizontalScope(new GUIStyle("AnimationKeyframeBackground"), GUILayout.Height(18)))
            {
                if (GUILayout.Button("All"))
                {
                    OnAllClick?.Invoke();
                }
                if (GUILayout.Button("None"))
                {
                    OnNoneClick?.Invoke();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel"))
                {
                    OnCancelClick?.Invoke();
                    Close();
                }
                if (GUILayout.Button("Apply"))
                {
                    if (CheckBeforeApply())
                    {
                        if (!EditorUtility.DisplayDialog(
                            "Something goes wrong!",
                            "There are nothing selected to apply are you sure you have setting correct?",
                            "Yes",
                            "Not"))
                        {
                            return;
                        }
                    }
                    OnApplyClick?.Invoke();
                    OnComplete?.Invoke();
                    Close();
                }
            }
        }
    }
}