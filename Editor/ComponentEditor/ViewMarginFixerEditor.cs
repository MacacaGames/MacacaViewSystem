
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MacacaGames.ViewSystem
{

    [CanEditMultipleObjects]

    [CustomEditor(typeof(ViewMarginFixer))]
    public class ViewMarginFixerEditor : Editor
    {
        private ViewMarginFixer m_target = null;

        private ViewMarginFixer Target
        {
            get
            {
                if (m_target == null)
                {
                    m_target = (ViewMarginFixer)target;
                }
                return m_target;
            }
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            var marginProperty = serializedObject.FindProperty("margin");
            var rebuildLayoutAfterFixProperty = serializedObject.FindProperty("RebuildLayoutAfterFix");

            EditorGUILayout.PropertyField(marginProperty, true);
            EditorGUILayout.PropertyField(rebuildLayoutAfterFixProperty);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto Guess Fix Target"))
            {
                Target.AutoGuessFixTarget();
                EditorUtility.SetDirty(Target);
            }
            if (GUILayout.Button("Apply Fixer Value"))
            {
                Target.ApplyModifyValue();
                EditorUtility.SetDirty(Target);
            }
            if (GUILayout.Button("Set Fixer Value From Transform"))
            {
                Target.SetModifyValueFromRectTransform();
                EditorUtility.SetDirty(Target);
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}