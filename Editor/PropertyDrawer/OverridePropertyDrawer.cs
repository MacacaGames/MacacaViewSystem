using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    [CustomPropertyDrawer(typeof(ViewElementPropertyOverrideData))]
    public class OverridePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect oriRect, SerializedProperty property, GUIContent label)
        {
            ViewElement viewElement = null;
            try
            {
                viewElement = (property.serializedObject.targetObject as Component).GetComponentInParent<ViewElement>();
            }
            catch { }
            var rootObjectName = viewElement != null ? viewElement.name : "{RootObject}";
            var btnRect = oriRect;
            btnRect.width = 30;
            btnRect.height = EditorGUIUtility.singleLineHeight;
            btnRect.x = oriRect.width;
            using (var disable = new EditorGUI.DisabledGroupScope(viewElement == null))
            {
                if (GUI.Button(btnRect, new GUIContent(EditorGUIUtility.FindTexture("d_Search Icon"))))
                {
                    PopupWindow.Show(btnRect, new OverridePopup(property, viewElement));
                }
            }

            var rect = oriRect;
            rect.height = EditorGUIUtility.singleLineHeight;
            if (viewElement == null)
            {
                GUI.Label(rect, new GUIContent("", "Root Object is not a ViewElement"));
            }
            var targetTransformPath = property.FindPropertyRelative("targetTransformPath");
            var componmentSp = property.FindPropertyRelative("targetComponentType");
            var propertySp = property.FindPropertyRelative("targetPropertyName");

            if (string.IsNullOrEmpty(componmentSp.stringValue) || string.IsNullOrEmpty(propertySp.stringValue))
            {
                rect.y += EditorGUIUtility.singleLineHeight;
                GUI.Label(rect, new GUIContent("Nothing select", Drawer.miniInfoIcon, "Nothing select"));
            }
            else
            {
                float paddind = 20;
                GUI.Label(rect, new GUIContent(rootObjectName + "/" + targetTransformPath.stringValue, Drawer.prefabIcon, targetTransformPath.stringValue));
                rect.y += EditorGUIUtility.singleLineHeight;
                var rectComponent = rect;
                rectComponent.x += paddind;
                rectComponent.width = oriRect.width - paddind;
                var componentContent = new GUIContent(EditorGUIUtility.ObjectContent(null, Utility.GetType(componmentSp.stringValue)));
                GUI.Label(rectComponent, new GUIContent($"{targetTransformPath.stringValue.Split('/').LastOrDefault()} ({componmentSp.stringValue.Split('.').LastOrDefault()})", componentContent.image));
                rect.y += EditorGUIUtility.singleLineHeight;
                VS_EditorUtility.SmartOverrideField(rect, new GUIContent(propertySp.stringValue), property.FindPropertyRelative("Value"), out float lh);
            }
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3;
        }

        public class OverridePopup : PopupWindowContent
        {
            ViewElement target;
            SerializedProperty overrideDataSerializedProperty;
            public OverridePopup(SerializedProperty overrideDataSerializedProperty, ViewElement target)
            {
                this.overrideDataSerializedProperty = overrideDataSerializedProperty;
                this.target = target;
                CacheHierarchy();
            }
            TreeViewState m_HierachyTreeViewState;
            HierarchyTreeView hierarchyTreeView;

            TreeViewState m_ComponentTreeViewState;
            ComponentTreeView componentTreeView;


            public override Vector2 GetWindowSize()
            {
                return new Vector2(300, 400);
            }

            public override void OnGUI(Rect rect)
            {
                DrawScrollViewHierarchy(rect);
            }
            Vector2 scrollPositionHierarchy;
            void DrawScrollViewHierarchy(Rect rect)
            {
                using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPositionHierarchy))
                {
                    scrollPositionHierarchy = scrollViewScope.scrollPosition;

                    if (currentSelectGameObject)
                    {
                        if (componentTreeView != null) componentTreeView.OnGUI(new Rect(0, 0, rect.width + 10, rect.height - 80));
                    }
                    else
                    {
                        //if (hierarchyDrawer != null) hierarchyDrawer.Draw();
                        if (hierarchyTreeView != null) hierarchyTreeView.OnGUI(new Rect(0, 0, rect.width + 10, rect.height - 80));
                    }
                }
            }
            public override void OnOpen()
            {
            }
            public override void OnClose()
            {
            }
            GameObject currentSelectGameObject;
            GameObject lastSelectGameObject;
            private void CacheHierarchy()
            {

                if (m_HierachyTreeViewState == null)
                    m_HierachyTreeViewState = new TreeViewState();

                hierarchyTreeView = new HierarchyTreeView(target.transform, m_HierachyTreeViewState);
                hierarchyTreeView.ExpandAll();
                hierarchyTreeView.OnItemClick += (go) =>
                {
                    lastSelectGameObject = currentSelectGameObject;
                    currentSelectGameObject = (GameObject)go;
                    CacheComponent();
                };
            }
            SerializedObject currentSelectSerializedObject;
            float toastMessageFadeOutTimt = 1;
            private void CacheComponent()
            {
                if (m_ComponentTreeViewState == null)
                    m_ComponentTreeViewState = new TreeViewState();

                componentTreeView = new ComponentTreeView(
                    currentSelectGameObject,
                    target,
                    m_ComponentTreeViewState,
                    currentSelectGameObject == target,
                    (a, b) => { return false; },
                    () => { });

                componentTreeView.OnItemClick += (sp) =>
                {
                    Debug.Log("stestset");
                    Component c;
                    try
                    {
                        c = (Component)sp.serializedObject.targetObject;
                    }
                    catch
                    {
                        c = ((GameObject)sp.serializedObject.targetObject).transform;
                    }

                    if (sp.propertyType == SerializedPropertyType.Generic)
                    {
                        System.Type parentType = sp.serializedObject.targetObject.GetType();

                        System.Reflection.FieldInfo fi = parentType.GetField(sp.propertyPath);

                        System.Type propertyType = null;
                        if (fi != null)
                        {
                            propertyType = fi.FieldType;
                        }

                        string propertyName = sp.propertyPath;

                        if (parentType.ToString().Contains("UnityEngine."))
                        {
                            if (propertyName == "m_Navigation")
                            {
                                var content = new GUIContent("UnityEngine.UI.Navigation is not supported with ViewSystem OverrideSystem");
#if UNITY_2019_1_OR_NEWER
                                editorWindow.ShowNotification(content, toastMessageFadeOutTimt);
#else
                            editorWindow.ShowNotification(content);
#endif
                                return;
                            }
                            propertyName = ViewSystemUtilitys.ParseUnityEngineProperty(sp.propertyPath);
                        }

                        System.Reflection.PropertyInfo pi = parentType.GetProperty(propertyName);
                        if (pi != null && fi == null)
                        {
                            propertyType = pi.PropertyType;
                        }

                        //                         if (!propertyType.IsSubclassOf(typeof(UnityEngine.Events.UnityEvent)) &&
                        //                             !propertyType.IsAssignableFrom(typeof(UnityEngine.Events.UnityEvent)))
                        //                         {
                        //                             var content = new GUIContent("Currently only support UnityEvent without parameters");
                        //                             ViewSystemLog.LogError(content.text);
                        // #if UNITY_2019_1_OR_NEWER
                        //                             editorWindow.ShowNotification(content, toastMessageFadeOutTimt);
                        // #else
                        //                                                     editorWindow.ShowNotification(content);
                        // #endif
                        //                             return;
                        //                         }

                        //                         var eventData = new ViewElementEventData();
                        //                         eventData.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                        //                         eventData.targetPropertyName = sp.name;
                        //                         eventData.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
                        //                         //eventData.targetPropertyType = sp.propertyType.ToString();
                        //                         //eventData.targetPropertyPath = propertyName;

                        //                         if (eventDatas == null)
                        //                         {
                        //                             eventDatas = new List<ViewElementEventData>();
                        //                         }

                        //                         var current = eventDatas
                        //                             .Where(x =>
                        //                                 x.targetTransformPath == eventData.targetTransformPath &&
                        //                                 x.targetComponentType == eventData.targetComponentType &&
                        //                                 x.targetPropertyName == eventData.targetPropertyName
                        //                             );

                        //                         if (current.Count() > 0)
                        //                         {
                        //                             if (current.Where(m => string.IsNullOrEmpty(m.scriptName) && string.IsNullOrEmpty(m.methodName)).Count() > 0)
                        //                             {
                        //                                 ViewSystemLog.LogError("You Have 1 event doesn't setup yet");
                        //                                 var errorContent = new GUIContent("You Have 1 event doesn't setup yet");
                        // #if UNITY_2019_1_OR_NEWER
                        //                                 editorWindow.ShowNotification(errorContent, toastMessageFadeOutTimt);
                        // #else
                        //                                                             editorWindow.ShowNotification(errorContent);
                        // #endif
                        //                                 return;
                        //                             }
                        //                         }

                        //                         var error = new GUIContent("Event Add Success");
                        //                         viewPageItem.eventDatas.Add(eventData);
                        // #if UNITY_2019_1_OR_NEWER
                        //                         editorWindow.ShowNotification(error, toastMessageFadeOutTimt);
                        // #else
                        //                                             editorWindow.ShowNotification(error);
                        // #endif
                    }
                    else
                    {
                        var overrideData = new ViewElementPropertyOverrideData();
                        overrideData.targetTransformPath = AnimationUtility.CalculateTransformPath(c.transform, target.transform);
                        overrideData.targetPropertyName = sp.name;
                        overrideData.targetComponentType = sp.serializedObject.targetObject.GetType().ToString();
                        overrideData.Value.SetValue(sp);
                        overrideDataSerializedProperty.SetTargetObjectOfProperty(overrideData);
                        editorWindow.Close();
                        // var t = overrideDataSerializedProperty.FindPropertyRelative("targetTransformPath");
                        // t.stringValue = overrideData.targetTransformPath;

                        // var s = overrideDataSerializedProperty.FindPropertyRelative("targetComponentType");
                        // s.stringValue = sp.serializedObject.targetObject.GetType().ToString();

                        // var valueProperty = overrideDataSerializedProperty.FindPropertyRelative("Value");
                        // valueProperty.FindPropertyRelative("s_Type").enumValueIndex = (int)overrideData.Value.s_Type;
                        // valueProperty.FindPropertyRelative("ObjectReferenceValue").objectReferenceValue = overrideData.Value.ObjectReferenceValue;
                        // valueProperty.FindPropertyRelative("StringValue").stringValue = overrideData.Value.StringValue;
                        //                         var current = overrideDatas
                        //                             .SingleOrDefault(x =>
                        //                                 x.targetTransformPath == overrideData.targetTransformPath &&
                        //                                 x.targetComponentType == overrideData.targetComponentType &&
                        //                                 x.targetPropertyName == overrideData.targetPropertyName
                        //                             );

                        //                         if (current != null)
                        //                         {
                        //                             current = overrideData;
                        // #if UNITY_2019_1_OR_NEWER
                        //                             editorWindow.ShowNotification(new GUIContent("This property is already in override list."), toastMessageFadeOutTimt);
                        // #else
                        //                             editorWindow.ShowNotification(new GUIContent("This property is already in override list."));
                        // #endif
                        //                         }
                        //                         else
                        //                         {
                        //                             overrideDatas.Add(overrideData);
                        // #if UNITY_2019_1_OR_NEWER
                        //                             editorWindow.ShowNotification(new GUIContent("Property override add success"), toastMessageFadeOutTimt);
                        // #else
                        //                                                     editorWindow.ShowNotification(new GUIContent("Property override add success"));
                        // #endif
                        //                         }
                    }
                };
            }
        }
    }
}