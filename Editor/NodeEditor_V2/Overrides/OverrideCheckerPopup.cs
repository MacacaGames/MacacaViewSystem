using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.IMGUI.Controls;

namespace MacacaGames.ViewSystem.NodeEditorV2
{
    public class ViewElementOverridesImporterWindow : EditorWindow
    {
        Transform root;
        Transform root_prefab;
        ViewSystemNode node;
        ViewPageItem viewPageItem;
        PropertyModification[] propertyModification;
        List<OverridesPropertiesCheckerData> overridesPropertiesCheckerDatas = new List<OverridesPropertiesCheckerData>();

        public void SetData(Transform root, Transform root_prefab, ViewPageItem viewPageItem, ViewSystemNode node)
        {
            this.root = root;
            this.root_prefab = root_prefab;
            this.viewPageItem = viewPageItem;
            this.node = node;
            titleContent = new GUIContent("ViewElement Overrides Importer");

            propertyModification = PrefabUtility.GetPropertyModifications(root.gameObject)
                .ToArray();

            var groupedByTarget = propertyModification.GroupBy(x => x.target).ToDictionary(o => o.Key, o => o.ToList());
            foreach (var target in groupedByTarget.Keys)
            {
                var groupedByProperty = groupedByTarget[target].GroupBy(x => x.propertyPath.Split('.')[0]).ToDictionary(o => o.Key, o => o.ToList());
                foreach (var property in groupedByProperty.Keys)
                {
                    // This find the orignal SerializedObject
                    var so = new SerializedObject(target);
                    // This find the orignal SerializedProperty
                    var sp = so.FindProperty(property);
                    if (VS_EditorUtility.IsPropertyNeedIgnore(sp))
                    {
                        continue;
                    }
                    var temp = new OverridesPropertiesCheckerData();
                    temp.serializedPropertyType = sp.propertyType;
                    temp.overrideData.targetPropertyName = property;
                    //temp.overrideData.targetPropertyType = sp.propertyType.ToString();
                    //temp.overrideData.targetPropertyPath = VS_EditorUtility.ParseUnityEngineProperty(property);

                    Transform t;
                    if (sp.serializedObject.targetObject as Component == null)
                    {
                        t = ((GameObject)sp.serializedObject.targetObject).transform;
                    }
                    else
                    {
                        t = ((Component)sp.serializedObject.targetObject).transform;
                    }

                    var path = AnimationUtility.CalculateTransformPath(t, root);
                    var selfName = root_prefab.name.Length + 1;
                    if (path.Length > selfName - 1) path = path.Substring(selfName, path.Length - selfName);
                    else if (path.Length == selfName - 1) path = "";
                    temp.overrideData.targetTransformPath = path;
                    temp.overrideData.targetComponentType = target.GetType().ToString();
                    temp.displayName = $"{sp.displayName}  ({property})";

                    //Find the prefab instance SerializedObject
                    var overrideGameObject = root.Find(path);
                    UnityEngine.Object obj;
                    if (target is GameObject)
                    {
                        obj = overrideGameObject.gameObject;
                    }
                    else
                    {
                        obj = overrideGameObject.GetComponent(target.GetType());
                    }
                    var so_instance = new SerializedObject(obj);
                    //Find the prefab instance SerializedProperty
                    var sp_instance = so_instance.FindProperty(property);

                    // Add PropertyOverride to OverridesPropertiesCheckerData
                    PropertyOverride overProperty = new PropertyOverride();
                    overProperty.SetValue(sp_instance);
                    temp.overrideData.Value = overProperty;

                    // Add OverridesPropertiesCheckerData to list wait for user check
                    overridesPropertiesCheckerDatas.Add(temp);
                }
            }
            maxSize = new Vector2(800, 800);
        }
       

        Vector2 scrollPos;
        void OnGUI()
        {
            using (var horizon = new GUILayout.HorizontalScope(new GUIStyle("AnimationKeyframeBackground"), GUILayout.Height(36)))
            {
                string lable = (root == null ? "" : root.name) + (node == null ? "" : " Modified Property in " + node.name + " " + (node is ViewStateNode ? "State" : "Page"));
                GUILayout.Label(new GUIContent(lable, Drawer.prefabIcon), new GUIStyle("AM MixerHeader2"));
            }

            using (var scroll = new GUILayout.ScrollViewScope(scrollPos))
            {
                scrollPos = scroll.scrollPosition;
                using (var vertical = new GUILayout.VerticalScope())
                {
                    foreach (var item in overridesPropertiesCheckerDatas)
                    {
                        //Currently ignore transform and gameobject property override
                        if (item.overrideData.targetComponentType.ToLower().Contains("transform"))
                        {
                            //continue;
                        }
                        using (var horizon = new GUILayout.HorizontalScope("box"))
                        {
                            item.import = EditorGUILayout.ToggleLeft("", item.import, GUILayout.Width(25));
                            using (var vertical2 = new GUILayout.VerticalScope())
                            {
                                // GUIContent l = new GUIContent(root.name + (string.IsNullOrEmpty(item.overrideData.targetTransformPath) ? "" : ("/" + item.overrideData.targetTransformPath)));
                                // GUILayout.Label(l);

                                using (var horizon2 = new GUILayout.HorizontalScope())
                                {
                                    GUIContent l = new GUIContent(root.name + (string.IsNullOrEmpty(item.overrideData.targetTransformPath) ? "" : ("/" + item.overrideData.targetTransformPath)), EditorGUIUtility.FindTexture("Prefab Icon"));
                                    GUILayout.Label(l, GUILayout.Height(16), GUILayout.Width(EditorGUIUtility.labelWidth));
                                    GUILayout.Label(EditorGUIUtility.FindTexture("Animation.Play"), GUILayout.Height(16), GUILayout.Width(16));
                                    Transform targetObject;
                                    //ViewSystemLog.Log(item.overrideData.targetTransformPath);

                                    if (string.IsNullOrEmpty(item.overrideData.targetTransformPath))
                                        targetObject = root;
                                    else
                                        targetObject = root.Find(item.overrideData.targetTransformPath);
                                    //ViewSystemLog.Log(item.overrideData.targetComponentType);

                                    UnityEngine.Object targetComponent;

                                    if (item.overrideData.targetComponentType.ToLower().Contains("gameobject"))
                                    {
                                        targetComponent = targetObject.gameObject;
                                    }
                                    else
                                    {
                                        targetComponent = targetObject.GetComponent(item.overrideData.targetComponentType);
                                    }
                                    // var type = CloudMacaca.Utility.GetType(item.overrideData.targetComponentType);
                                    // UnityEngine.Object targetComponent = targetObject.GetComponent(type);
                                    if (targetComponent == null)
                                    {
                                        var type = MacacaGames.Utility.GetType(item.overrideData.targetComponentType);
                                        targetComponent = targetObject.GetComponent(type);
                                        //targetComponent = targetObject.GetComponent(item.overrideData.targetComponentType.Replace("UnityEngine.", ""));
                                    }
                                    GUIContent _cachedContent;
                                    if (targetComponent == null)
                                    {
                                        _cachedContent = new GUIContent("This property or Component is not support " + item.overrideData.targetComponentType);
                                    }
                                    else
                                    {
                                        _cachedContent = new GUIContent(EditorGUIUtility.ObjectContent(targetComponent, targetComponent.GetType()));
                                    }
                                    GUILayout.Label(_cachedContent, GUILayout.Height(16), GUILayout.Width(position.width * 0.5f));
                                }
                                using (var horizon2 = new GUILayout.HorizontalScope())
                                {
                                    GUILayout.Label(item.displayName);
                                    GUILayout.FlexibleSpace();
                                    DrawValue(item.serializedPropertyType, item.overrideData);
                                }
                            }
                        }
                    }
                }
            }
            using (var horizon = new GUILayout.HorizontalScope(new GUIStyle("AnimationKeyframeBackground"), GUILayout.Height(18)))
            {
                if (GUILayout.Button("All"))
                {
                    overridesPropertiesCheckerDatas.All(x =>
                    {
                        x.import = true;
                        return true;
                    });
                }
                if (GUILayout.Button("None"))
                {
                    overridesPropertiesCheckerDatas.All(x =>
                    {
                        x.import = false;
                        return true;
                    });
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }
                if (GUILayout.Button("Import"))
                {
                    viewPageItem.overrideDatas?.Clear();
                    var import = overridesPropertiesCheckerDatas.Where(m => m.import == true).Select(x => x.overrideData);
                    viewPageItem.overrideDatas = import.ToList();
                    Close();
                }
            }
        }
        int vauleBoxWidth = 200;
        void DrawValue(SerializedPropertyType type, ViewElementPropertyOverrideData overrideData)
        {
            if (type != SerializedPropertyType.Generic)
            {
                Color backgroundColor = GUI.backgroundColor;
                GUI.backgroundColor *= new Color(1f, 1f, 1f, 0.5f);
                switch (type)
                {
                    case SerializedPropertyType.Vector2:
                        GUILayout.Box($"Vector2 {overrideData.Value.GetValue().ToString()}", Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        break;
                    case SerializedPropertyType.Vector3:
                        GUILayout.Box($"Vector3 {overrideData.Value.GetValue().ToString()}", Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        break;
                    case SerializedPropertyType.Float:
                        GUILayout.Box(overrideData.Value.GetValue().ToString(), Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        break;
                    case SerializedPropertyType.Integer:
                        GUILayout.Box(overrideData.Value.GetValue().ToString(), Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        break;
                    case SerializedPropertyType.String:
                        GUILayout.Box(new GUIContent("\"" + overrideData.Value.StringValue + "\"", overrideData.Value.StringValue), Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        break;
                    case SerializedPropertyType.Boolean:
                        GUILayout.Box(overrideData.Value.GetValue().ToString(), Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        break;
                    case SerializedPropertyType.Color:
                        using (var disable = new EditorGUI.DisabledGroupScope(true))
                        {
                            EditorGUILayout.ColorField((Color)overrideData.Value.GetValue(), GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        }
                        break;
                    case SerializedPropertyType.LayerMask:
                        GUILayout.Box(overrideData.Value.GetValue().ToString(), Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        break;
                    case SerializedPropertyType.ObjectReference:
                        if (overrideData.Value.ObjectReferenceValue == null) {
                            GUILayout.Box("Null", Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        }
                        else
                        {
                            using (var disable = new EditorGUI.DisabledGroupScope(true))
                            {
                                EditorGUILayout.ObjectField(overrideData.Value.ObjectReferenceValue, overrideData.Value.ObjectReferenceValue.GetType(), false, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                            }
                        }
                       
                        //GUILayout.Box(overrideData.Value.IntValue.ToString(), Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(150));
                        break;
                    default:
                        GUILayout.Box(new GUIContent(type.ToString(), type.ToString()), Drawer.valueBoxStyle, GUILayout.Height(16), GUILayout.Width(vauleBoxWidth));
                        break;
                }
                GUI.backgroundColor = backgroundColor;
            }
        }

    }


    class OverridesPropertiesCheckerData
    {
        public OverridesPropertiesCheckerData()
        {
            overrideData = new ViewElementPropertyOverrideData();
        }
        public bool import = false;
        public string displayName;
        public SerializedPropertyType serializedPropertyType;
        public ViewElementPropertyOverrideData overrideData;
    }
}
