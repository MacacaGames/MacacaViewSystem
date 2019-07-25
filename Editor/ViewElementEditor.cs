using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

namespace CloudMacaca.ViewSystem
{


    [CanEditMultipleObjects]

    [CustomEditor(typeof(ViewElement))]
    public class ViewElementEditor : Editor
    {
        private ViewElement viewElement = null;
        private SerializedProperty m_AnimTriggerProperty;
        private SerializedProperty onShowHandle;
        private SerializedProperty onLeaveHandle;
        AnimBool showV2Setting = new AnimBool(true);

        void OnEnable()
        {
            viewElement = (ViewElement)target;
            onShowHandle = serializedObject.FindProperty("OnShowHandle");
            onLeaveHandle = serializedObject.FindProperty("OnLeaveHandle");
            showV2Setting.valueChanged.AddListener(Repaint);
        }
        void OnDisable()
        {
            showV2Setting.valueChanged.RemoveListener(Repaint);
        }
        public override void OnInspectorGUI()
        {

            viewElement.transition = (ViewElement.TransitionType)EditorGUILayout.EnumPopup("變換方式", viewElement.transition);

            switch (viewElement.transition)
            {
                case ViewElement.TransitionType.Animator:
                    viewElement.animatorTransitionType = (ViewElement.AnimatorTransitionType)EditorGUILayout.EnumPopup("切換動畫狀態的方法", viewElement.animatorTransitionType);
                    viewElement.AnimationStateName_In = EditorGUILayout.TextField("進場動畫 State Name", viewElement.AnimationStateName_In);
                    viewElement.AnimationStateName_Loop = EditorGUILayout.TextField("Loop 動畫 State Name", viewElement.AnimationStateName_Loop);
                    viewElement.AnimationStateName_Out = EditorGUILayout.TextField("離場動畫 State Name", viewElement.AnimationStateName_Out);
                    if (viewElement.animator != null)
                    {
                        EditorGUILayout.HelpBox("已完成設定", MessageType.Info);
                        if (GUILayout.Button("我要重新設定！", EditorStyles.miniButton))
                        {
                            viewElement.Setup();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("在物件與所有子物件中 沒有找到可用的 Animator", MessageType.Error);
                        if (GUILayout.Button("我要重新設定！", EditorStyles.miniButton))
                        {
                            viewElement.Setup();
                        }
                    }
                    break;
                case ViewElement.TransitionType.CanvasGroupAlpha:
                    viewElement.canvasInEase = (DG.Tweening.Ease)EditorGUILayout.EnumPopup("Tween 進場曲線", viewElement.canvasInEase);
                    viewElement.canvasInTime = EditorGUILayout.FloatField("Tween 進場時間", viewElement.canvasInTime);
                    viewElement.canvasOutEase = (DG.Tweening.Ease)EditorGUILayout.EnumPopup("Tween 離場曲線", viewElement.canvasOutEase);
                    viewElement.canvasOutTime = EditorGUILayout.FloatField("Tween 離場時間", viewElement.canvasOutTime);

                    break;
                case ViewElement.TransitionType.ActiveSwitch:

                    break;
                case ViewElement.TransitionType.Custom:
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.PropertyField(onShowHandle, true);
                    EditorGUILayout.PropertyField(onLeaveHandle, true);
                    EditorGUILayout.EndVertical();
                    break;
            }


            showV2Setting.target = EditorGUILayout.Foldout(showV2Setting.target, new GUIContent("V2 Setting", "Below scope is only used in V2 Version"));

            using (var fade = new EditorGUILayout.FadeGroupScope(showV2Setting.faded))
            {
                if (fade.visible)
                {
                    viewElement.IsUnique = EditorGUILayout.Toggle("Is Unique", viewElement.IsUnique);
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(viewElement);

        }
    }
}