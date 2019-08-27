using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using UnityEngine;
using System;

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

        List<ViewElementPropertyOverrideData> allOverrideDatas = new List<ViewElementPropertyOverrideData>();
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
        public void VerifyProperty()
        {
            //Data may changed by Component fixer so refresh again.
            RefreshOverrideDatas();
            propertyCannotBeFound.Clear();
            //var go = new GameObject("Verify");
            foreach (var item in allOverrideDatas)
            {
                var t = CloudMacaca.Utility.GetType(item.targetComponentType);
                if (t == null)
                {
                    Debug.LogError(item.targetComponentType + " still not fixed cannot be found, will ignore while verify property");
                    continue;
                }

                if (t.GetField(item.targetPropertyName) == null && t.GetProperty(item.targetPropertyName) == null)
                {
                    Debug.LogError(item.targetPropertyName + " in " + item.targetComponentType + " cannot be found");
                    propertyCannotBeFound.Add(item.targetComponentType + "," + item.targetPropertyName);
                }
            }
            //UnityEngine.Object.DestroyImmediate(go);
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
                Debug.Log("Great everying looks good!");
            }
        }

        void RefreshOverrideDatas()
        {
            var overrideDatasInPages = saveData.viewPages.Select(m => m.viewPage).SelectMany(x => x.viewPageItems).SelectMany(i => i.overrideDatas);
            var overrideDatasInStates = saveData.viewStates.Select(m => m.viewState).SelectMany(x => x.viewPageItems).SelectMany(i => i.overrideDatas);
            allOverrideDatas.Clear();
            allOverrideDatas.AddRange(overrideDatasInPages);
            allOverrideDatas.AddRange(overrideDatasInStates);
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
            public string modifiedPropertyPath;
        }
        List<string> propertyCannotBeFound;
        Dictionary<string, List<FixerData>> fixerDatas = new Dictionary<string, List<FixerData>>();
        IEnumerable<ViewElementPropertyOverrideData> allOverrideDatas;
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
        }
        public override void OnDrawScrollArea()
        {
            float width = (position.width - 80) * 0.66f;
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
                    OnApplyClick?.Invoke();

                    OnComplete?.Invoke();
                    Close();
                }
            }
        }
    }
}