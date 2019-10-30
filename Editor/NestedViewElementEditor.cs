using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditorInternal;
using UnityEditor;
using UnityEditor.AnimatedValues;
using System;
using System.Linq;
namespace CloudMacaca.ViewSystem
{


    [CanEditMultipleObjects]
    [CustomEditor(typeof(NestedViewElement))]
    public class NestedViewElementEditor : Editor
    {
        private NestedViewElement nestedViewElement = null;
        private SerializedProperty m_AnimTriggerProperty;
        private SerializedProperty onShowHandle;
        AnimBool showV2Setting = new AnimBool(true);
        ReorderableList list;
        void OnEnable()
        {
            nestedViewElement = (NestedViewElement)target;
            showV2Setting.valueChanged.AddListener(Repaint);

            list = new ReorderableList(nestedViewElement.childViewElements, nestedViewElement.childViewElements.GetType(), true, true, false, false);
            list.elementHeight = EditorGUIUtility.singleLineHeight;
            list.drawElementCallback += drawElementCallback;
            list.drawHeaderCallback += drawHeaderCallback;


        }

        private void drawHeaderCallback(Rect rect)
        {
            rect.x += 10;
            rect.width = rect.width * 0.5f;
            GUI.Label(rect, "ViewElement");
            rect.x += rect.width;
            rect.width = rect.width * 0.5f;
            GUI.Label(rect, "Delay In");
            rect.x += rect.width;
            GUI.Label(rect, "Delay Out");
        }

        private void drawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect.width = rect.width * 0.5f;
            var item = nestedViewElement.childViewElements[index];
            using (var disable = new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUI.ObjectField(rect, item.viewElement, item.viewElement.GetType(), false);
            }

            rect.x += rect.width;
            rect.width = rect.width * 0.5f;

            item.delayIn = EditorGUI.FloatField(rect, item.delayIn);
            rect.x += rect.width;
            item.delayOut = EditorGUI.FloatField(rect, item.delayOut);
        }

        void OnDisable()
        {
            showV2Setting.valueChanged.RemoveListener(Repaint);
        }

        public override void OnInspectorGUI()
        {
            nestedViewElement.transition = ViewElement.TransitionType.ActiveSwitch;
            EditorGUILayout.HelpBox("Nested ViewElement only can set transition to ActiveSwitch", MessageType.Info);

            if (!nestedViewElement.IsSetup && nestedViewElement.dynamicChild == false)
            {
                EditorGUILayout.HelpBox("There is no avaliable ViewElement in child GameObject \n\n If ViewElement will generate in runtime, please chech the 'DynamicChild' check box", MessageType.Error);
            }
            if (nestedViewElement.childViewElements.Count != list.count ||
                nestedViewElement.childViewElements.Count(m => m.viewElement == null) > 0
            )
            {
                EditorGUILayout.HelpBox("One or more ViewElement is missing, please try to refresh.", MessageType.Error);
            }
            if (nestedViewElement.dynamicChild)
            {
                EditorGUILayout.HelpBox("For performance reasons, it is recommended to use 'Dynamic Child' feature as less as you can. \n\n Note : you still need to call ChangePage() method on ViewElement yourself, NestedViewElement will not handle the DynamicChild's ChangePage()", MessageType.Warning);
            }
            nestedViewElement.dynamicChild = EditorGUILayout.Toggle("Dynamic Child", nestedViewElement.dynamicChild);
            if (GUILayout.Button("Refresh", EditorStyles.miniButton))
            {
                nestedViewElement.SetupChild();
                OnEnable();
                Repaint();
            }

            using (var disable = new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.EnumPopup("Transition", nestedViewElement.transition);
            }
            list.DoLayoutList();

            showV2Setting.target = EditorGUILayout.Foldout(showV2Setting.target, new GUIContent("V2 Setting", "Below scope is only used in V2 Version"));
            string hintText = "";
            using (var fade = new EditorGUILayout.FadeGroupScope(showV2Setting.faded))
            {
                if (fade.visible)
                {
                    nestedViewElement.IsUnique = EditorGUILayout.Toggle("Is Unique", nestedViewElement.IsUnique);

                    if (nestedViewElement.IsUnique)
                    {
                        hintText = "Note : Injection will make target component into Singleton, if there is mutil several same component been inject, you will only be able to get the last injection";
                    }
                    else
                    {
                        hintText = "Only Unique ViewElement can be inject";
                    }
                    EditorGUILayout.HelpBox(hintText, MessageType.Info);
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(nestedViewElement);

        }
    }
}