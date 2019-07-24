using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using CloudMacaca;

namespace CloudMacaca.ViewSystem
{
    [CustomEditor(typeof(ViewControllerV2))]
    public class ViewControllerV2Editor : Editor
    {
        private ViewControllerV2 viewController = null;
        private SerializedObject so;
        public ReorderableList list = null;


        private SerializedProperty s_SaveData;

        void OnEnable()
        {
            viewController = (ViewControllerV2)target;
            so = serializedObject;
            s_SaveData = so.FindProperty("viewSystemSaveData");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(s_SaveData);


            serializedObject.ApplyModifiedProperties();
        }
    }

}