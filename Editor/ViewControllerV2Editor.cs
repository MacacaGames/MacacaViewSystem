using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using CloudMacaca;
using UnityEditor.AnimatedValues;

namespace CloudMacaca.ViewSystem
{
    [CustomEditor(typeof(ViewControllerV2))]
    public class ViewControllerV2Editor : Editor
    {
        private ViewControllerV2 viewController = null;
        private SerializedObject so;
        public ReorderableList list = null;


        private SerializedProperty s_SaveData;
        private SerializedProperty s_vs;
        private SerializedProperty s_vp;
        private SerializedProperty s_current_element;

        AnimBool showDebugView = new AnimBool(false);
        void OnEnable()
        {
            viewController = (ViewControllerV2)target;
            so = serializedObject;
            s_SaveData = so.FindProperty("viewSystemSaveData");
            s_vs = so.FindProperty("viewStates");
            s_vp = so.FindProperty("viewPages");
            s_current_element = so.FindProperty("currentLiveElements");
            showDebugView.valueChanged.AddListener(Repaint);
        }
        void OnDisable()
        {
            showDebugView.valueChanged.RemoveListener(Repaint);
        }
        public override void OnInspectorGUI()
        {
            so.Update();

            EditorGUILayout.PropertyField(s_SaveData);
            serializedObject.ApplyModifiedProperties();

            showDebugView.target = EditorGUILayout.Foldout(showDebugView.target, "Debug");
            using (var disable = new EditorGUI.DisabledGroupScope(true))
            {
                using (var fade = new EditorGUILayout.FadeGroupScope(showDebugView.faded))
                {
                    if (fade.visible)
                    {

                        EditorGUILayout.PropertyField(s_vs, true);
                        EditorGUILayout.PropertyField(s_vp, true);
                        EditorGUILayout.PropertyField(s_current_element, true);
                    }
                }
            }
        }
    }

}