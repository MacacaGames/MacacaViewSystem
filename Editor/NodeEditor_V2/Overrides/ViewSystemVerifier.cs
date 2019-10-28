using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using UnityEngine;
using System;
using System.Reflection;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemVerifier
    {
        ViewSystemNodeEditor editor;
        GUIStyle windowStyle;
        ViewSystemSaveData saveData;
        public ViewSystemVerifier(ViewSystemNodeEditor editor, ViewSystemSaveData saveData)
        {
            this.saveData = saveData;
            this.editor = editor;
            windowStyle = new GUIStyle(Drawer.windowStyle);
            RectOffset padding = windowStyle.padding;
            padding.left = 0;
            padding.right = 1;
            padding.bottom = 0;
        }

        public void VerifyPagesAndStates()
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
            string result = "";
            var viewStates = saveData.viewStates.Select(m => m.viewState).ToList();
            var viewPages = saveData.viewPages.Select(m => m.viewPage).ToList();

            foreach (var states in viewStates)
            {
                foreach (var item in states.viewPageItems)
                {
                    if (item.viewElement == null)
                    {
                        result += $"One ViewElement in State : [{states.name}] is null.";
                    }
                }
            }
            foreach (var pages in viewPages)
            {
                foreach (var item in pages.viewPageItems)
                {
                    if (item.viewElement == null)
                    {
                        result += $"One ViewElement in Page : [{pages.name}] is null.";
                    }
                }
            }
            if (!string.IsNullOrEmpty(result))
            {
                Debug.LogError(result);
            }
            else
            {
                Debug.Log("Great, all pages and states looks good!");
            }
        }
        List<ViewSystemGameObjectMissingData> gameObjectCannotBeFound = new List<ViewSystemGameObjectMissingData>();
        public class ViewSystemGameObjectMissingData
        {
            public bool isViewState;
            public string stateOrPageName;
            public ViewElement viewElement;
            public ViewSystemComponentData viewSystemComponent;
        }
        public enum VerifyTarget
        {
            All,
            Override,
            Event
        }
        public void VerifyGameObject(VerifyTarget verifyTarget = VerifyTarget.All)
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

            gameObjectCannotBeFound.Clear();

            var overrideDatasInPages = saveData.viewPages.Select(m => m.viewPage);
            var overrideDatasInStates = saveData.viewStates.Select(m => m.viewState);

            foreach (var viewPage in overrideDatasInPages)
            {
                foreach (var viewPageItem in viewPage.viewPageItems)
                {
                    List<ViewSystemComponentData> verifyTargets = new List<ViewSystemComponentData>();

                    if (verifyTarget == VerifyTarget.All || verifyTarget == VerifyTarget.Override)
                    {
                        verifyTargets.AddRange(viewPageItem.overrideDatas.Cast<ViewSystemComponentData>());
                    }

                    if (verifyTarget == VerifyTarget.All || verifyTarget == VerifyTarget.Event)
                    {
                        verifyTargets.AddRange(viewPageItem.eventDatas.Cast<ViewSystemComponentData>());
                    }

                    foreach (var verifyData in verifyTargets)
                    {
                        var transform = viewPageItem.viewElement.transform.Find(verifyData.targetTransformPath);
                        if (transform == null)
                        {
                            gameObjectCannotBeFound.Add(
                                new ViewSystemGameObjectMissingData
                                {
                                    isViewState = false,
                                    viewElement = viewPageItem.viewElement,
                                    stateOrPageName = viewPage.name,
                                    viewSystemComponent = verifyData
                                });
                        }
                    }
                }
            }

            foreach (var viewState in overrideDatasInStates)
            {
                foreach (var viewPageItem in viewState.viewPageItems)
                {
                    List<ViewSystemComponentData> verifyTargets = new List<ViewSystemComponentData>();

                    if (verifyTarget == VerifyTarget.All || verifyTarget == VerifyTarget.Override)
                    {
                        verifyTargets.AddRange(viewPageItem.overrideDatas.Cast<ViewSystemComponentData>());
                    }

                    if (verifyTarget == VerifyTarget.All || verifyTarget == VerifyTarget.Event)
                    {
                        verifyTargets.AddRange(viewPageItem.eventDatas.Cast<ViewSystemComponentData>());
                    }

                    foreach (var verifyData in verifyTargets)
                    {
                        var transform = viewPageItem.viewElement.transform.Find(verifyData.targetTransformPath);
                        if (transform == null)
                        {
                            gameObjectCannotBeFound.Add(
                                new ViewSystemGameObjectMissingData
                                {
                                    isViewState = true,
                                    viewElement = viewPageItem.viewElement,
                                    stateOrPageName = viewState.name,
                                    viewSystemComponent = verifyData
                                });
                        }
                    }
                }
            }

            if (gameObjectCannotBeFound.Count > 0)
            {
                if (EditorUtility.DisplayDialog(
                    "Something goes wrong!",
                    "There are some GameObject is missing, do you want to open fixer window",
                    "Yes, Please",
                    "Not now"))
                {
                    var window = ScriptableObject.CreateInstance<GameObjectFixerWindow>();
                    window.SetData(gameObjectCannotBeFound, () =>
                    {
                        //Make sure SetDirty
                        EditorUtility.SetDirty(saveData);
                    });
                    window.ShowUtility();
                }
            }

        }
        List<string> typeNameCannotBeFound = new List<string>();
        public void VerifyComponent(VerifyTarget verifyTarget)
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
            if (verifyTarget == VerifyTarget.Event)
            {
                RefreshEventDatas();
                foreach (var item in allEventDatas)
                {
                    var t = CloudMacaca.Utility.GetType(item.targetComponentType);
                    if (t == null)
                    {
                        Debug.LogError(item.targetComponentType + "  cannot be found");
                        typeNameCannotBeFound.Add(item.targetComponentType);
                    }
                }
            }
            if (verifyTarget == VerifyTarget.Override)
            {
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
            }

            if (typeNameCannotBeFound.Count > 0)
            {
                if (EditorUtility.DisplayDialog(
                    "Something goes wrong!",
                    "There are some override component is missing, do you want to open fixer window",
                    "Yes, Please",
                    "Not now"))
                {
                    List<ViewSystemComponentData> componentDatas;
                    if (verifyTarget == VerifyTarget.Override)
                    {
                        componentDatas = allOverrideDatas.Cast<ViewSystemComponentData>().ToList();
                    }
                    else
                    {
                        componentDatas = allEventDatas.Cast<ViewSystemComponentData>().ToList();
                    }
                    var window = ScriptableObject.CreateInstance<ComponentFixerWindow>();
                    window.SetData(typeNameCannotBeFound, componentDatas, () =>
                         {
                             //Make sure SetDirty
                             EditorUtility.SetDirty(saveData);
                             VerifyProperty(verifyTarget);
                         });
                    window.ShowUtility();
                }
            }
            else
            {
                Debug.Log("Components looks good, let's check properties.");
                VerifyProperty(verifyTarget);
            }
        }
        List<string> propertyCannotBeFound = new List<string>();
        List<UnityEngine.Object> tempDummy = new List<UnityEngine.Object>();
        public static BindingFlags bindingFlags =
            BindingFlags.NonPublic |
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static;
        public void VerifyProperty(VerifyTarget verifyTarget)
        {
            propertyCannotBeFound.Clear();
            IEnumerable<ViewSystemComponentData> targetVerifyDatas = null;
            //Data may changed by Component fixer so refresh again.
            if (verifyTarget == VerifyTarget.Override)
            {
                RefreshOverrideDatas();
                targetVerifyDatas = allOverrideDatas.Cast<ViewSystemComponentData>();
            }
            if (verifyTarget == VerifyTarget.Event)
            {
                RefreshEventDatas();
                targetVerifyDatas = allEventDatas.Cast<ViewSystemComponentData>();
            }

            foreach (var item in targetVerifyDatas)
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
                    window.SetData(propertyCannotBeFound, targetVerifyDatas, () =>
                    {
                        //Make sure SetDirty
                        EditorUtility.SetDirty(saveData);
                        if (verifyTarget == VerifyTarget.Event) VerifyEvents();
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

                            using (var disable = new EditorGUI.DisabledGroupScope(!classMethodInfo.ContainsKey(item.originalScriptName)))
                            {
                                if (GUILayout.Button("Apply Origin Data"))
                                {
                                    item.modifyScriptName = item.originalScriptName;
                                    item.modifyMethodName = item.originalMethodName;
                                }
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
                                    CMEditorLayout.GroupedPopupField(item.GetHashCode(), new GUIContent("Event Method"), c, current,
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
    public class GameObjectFixerWindow : FixerWindow
    {
        class FixerData
        {

            public bool fix = false;
            public ViewSystemVerifier.ViewSystemGameObjectMissingData originalNotFoundItem;
            public string tempPath;
        }
        List<FixerData> fixerDatas;
        public void SetData(IEnumerable<ViewSystemVerifier.ViewSystemGameObjectMissingData> originalNotFoundItems, Action OnComplete)
        {
            titleContent = new GUIContent("Missing GameObject fixer");
            this.icon = EditorGUIUtility.FindTexture("MetaFile Icon");
            this.lable = "Select the GameObject Path your wish to fix";
            fixerDatas = originalNotFoundItems.Select(m =>
            new FixerData
            {
                originalNotFoundItem = m,
                tempPath = m.viewSystemComponent.targetTransformPath
            }).ToList();

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
                    item.originalNotFoundItem.viewSystemComponent.targetTransformPath = item.tempPath;
                }
            };
        }

        public override bool CheckBeforeApply()
        {
            return fixerDatas.Where(m => m.fix).Count() == 0;
        }
        public override void OnDrawScrollArea()
        {
            float width = (position.width - 80) * 0.5f;
            using (var vertical = new GUILayout.VerticalScope("box"))
            {
                foreach (var item in fixerDatas)
                {
                    using (var horizon = new GUILayout.HorizontalScope())
                    {
                        item.fix = EditorGUILayout.ToggleLeft(GUIContent.none, item.fix, GUILayout.Width(20));

                        // GUILayout.Label($"Missing GameObject in {(isViewState ? "State" : "Page")} : [{(isViewState ? item.originalNotFoundItem.viewState.name : item.originalNotFoundItem.viewPage.name)}], Under ViewElement : [{item.originalNotFoundItem.viewPageItem.viewElement.name}]");
                        GUILayout.Label($"{item.originalNotFoundItem.stateOrPageName} ({(item.originalNotFoundItem.isViewState ? "State" : "Page")})");
                        GUILayout.Label(Drawer.arrowIcon);
                        GUILayout.Label($"{item.originalNotFoundItem.viewElement.name} (ViewElement)");
                    }
                    item.tempPath = EditorGUILayout.TextField("Transform Path", item.tempPath);
                    if (item.originalNotFoundItem.viewElement.transform.Find(item.tempPath) == null) GUILayout.Label(new GUIContent($"[{item.tempPath}] is not a vaild Path in target ViewElement", Drawer.miniErrorIcon), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    else GUILayout.Label(new GUIContent($"Good! GameObject can be found with the input Path"), GUILayout.Height(EditorGUIUtility.singleLineHeight));
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
        IEnumerable<ViewSystemComponentData> allComponentDatas;

        Dictionary<string, List<CMEditorLayout.GroupedPopupData>> fieldsInComponents = new Dictionary<string, List<CMEditorLayout.GroupedPopupData>>();

        public void SetData(List<string> propertyCannotBeFound, IEnumerable<ViewSystemComponentData> allComponentDatas, Action OnComplete)
        {
            titleContent = new GUIContent("Missing property fixer");
            this.allComponentDatas = allComponentDatas;
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
                var fis = type.GetFields(ViewSystemVerifier.bindingFlags).Select(m => new CMEditorLayout.GroupedPopupData { name = m.Name, group = "Filed" });
                var pis = type.GetProperties(ViewSystemVerifier.bindingFlags).Select(m => new CMEditorLayout.GroupedPopupData { name = m.Name, group = "Property" });

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
                    var ac = allComponentDatas.Where(m => m.targetComponentType == item.Key);

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
                            CMEditorLayout.GroupedPopupField(item.GetHashCode(), GUIContent.none, fieldsInComponents[item.Key], current,
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
            public bool modifyByMonoscript = true;
            public string originalComponentName;
            public MonoScript targetComponentScript;
            public string targetComponentName;
            public bool CanApply()
            {
                return (modifyByMonoscript && targetComponentScript != null) || !string.IsNullOrEmpty(targetComponentName);
            }
        }
        List<FixerData> fixerDatas = new List<FixerData>();
        IEnumerable<ViewSystemComponentData> allOverrideDatas;
        public void SetData(List<string> typeNameCannotBeFound, IEnumerable<ViewSystemComponentData> allOverrideDatas, Action OnComplete)
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
                   if (item.modifyByMonoscript == false)
                   {
                       allOverrideDatas.Where(m => m.targetComponentType == item.originalComponentName).All(
                           (x) =>
                           {
                               x.targetComponentType = item.targetComponentName;
                               return true;
                           }
                       );
                   }
                   else
                   {
                       allOverrideDatas.Where(m => m.targetComponentType == item.originalComponentName).All(
                            (x) =>
                            {

                                x.targetComponentType = item.targetComponentScript.GetClass().ToString();
                                return true;
                            }
                        );
                   }

               }
           };
        }
        public override bool CheckBeforeApply()
        {
            return fixerDatas.Where(m => m.fix && m.CanApply()).Count() == 0;
        }
        public override void OnDrawScrollArea()
        {
            using (var vertical = new GUILayout.VerticalScope())
            {
                foreach (var item in fixerDatas)
                {
                    using (var horizon = new GUILayout.HorizontalScope("box"))
                    {
                        item.fix = EditorGUILayout.ToggleLeft(GUIContent.none, item.fix, GUILayout.Width(20));
                        GUILayout.Label(new GUIContent(item.originalComponentName, item.originalComponentName));
                        GUILayout.Label(Drawer.arrowIcon);
                        item.modifyByMonoscript = EditorGUILayout.Toggle(item.modifyByMonoscript, new GUIStyle("IN LockButton"), GUILayout.Width(16));
                        if (item.modifyByMonoscript)
                        {
                            item.targetComponentScript = (MonoScript)EditorGUILayout.ObjectField(item.targetComponentScript, typeof(MonoScript), false);
                        }
                        else
                        {
                            item.targetComponentName = EditorGUILayout.TextField(item.targetComponentName);
                        }
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
                using (var disable = new EditorGUI.DisabledGroupScope(CheckBeforeApply()))
                {
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
}