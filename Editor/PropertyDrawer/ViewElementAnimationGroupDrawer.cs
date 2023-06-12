using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Reflection;
using UnityEditorInternal;
using System;
using System.IO;
using MacacaGames.ViewSystem.VisualEditor;
using UnityEditor.AnimatedValues;

namespace MacacaGames.ViewSystem
{

    [CustomPropertyDrawer(typeof(ViewElementAnimationGroup))]
    public class ViewElementAnimationGroupDrawer : PropertyDrawer
    {
        public ViewElementAnimationGroupDrawer()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabSaving += OnPrefabSaving;
            Undo.undoRedoPerformed += OnUndo;

        }

        private void OnUndo()
        {

        }

        ~ViewElementAnimationGroupDrawer()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabSaving -= OnPrefabSaving;
            Undo.undoRedoPerformed -= OnUndo;
        }
        private void OnPrefabSaving(GameObject obj)
        {
            // Debug.Log("Do revert");
            // DoRevert();
        }

        ReorderableList reorderableList;
        SerializedProperty propertySource;
        ViewElement viewElement = null;
        ViewElement original = null;
        bool fold = false;
        private Vector2 contextClick;

        private bool instanceUpdate = true;
        bool isPreviewing = false;
        void SetDirty()
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(propertySource.serializedObject.targetObject);
            propertySource.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(viewElement);

        }
        private UnityEditor.AnimatedValues.AnimBool onHideMoveAnimBool;
        private UnityEditor.AnimatedValues.AnimBool onHideRotateAnimBool;
        private UnityEditor.AnimatedValues.AnimBool onHideScaleAnimBool;
        private UnityEditor.AnimatedValues.AnimBool onHideFadeAnimBool;
        private const float labelWidth = 60f;

        bool init = false;
        void Init(SerializedProperty property)
        {
            if (init)
            {
                return;
            }
            ViewElementAnimation Target = (ViewElementAnimation)property.serializedObject.targetObject;
            ViewElementAnimationGroup animation = (ViewElementAnimationGroup)fieldInfo.GetValue(Target);

            onHideMoveAnimBool = new AnimBool(animation.moveToggle);
            onHideRotateAnimBool = new AnimBool(animation.rotateToggle);
            onHideScaleAnimBool = new AnimBool(animation.scaleToggle);
            onHideFadeAnimBool = new AnimBool(animation.fadeToggle);
            viewElement = ((ViewElementAnimation)property.serializedObject.targetObject).GetComponent<ViewElement>();
            init = true;
        }
        public override void OnGUI(Rect oriRect, SerializedProperty property, GUIContent label)
        {
            ViewElementAnimation Target = (ViewElementAnimation)property.serializedObject.targetObject;
            ViewElementAnimationGroup animation = (ViewElementAnimationGroup)fieldInfo.GetValue(Target);

            bool isOverrideTargetTransform = false;

            isOverrideTargetTransform = Target.overrideTarget != null;

            Init(property);
            Color color = GUI.color;

            GUILayout.Label(property.displayName);


            using (var horizon = new EditorGUILayout.HorizontalScope())
            {
                GUI.color = animation.moveToggle ? color : Color.gray;
                if (GUILayout.Button(EditorGUIUtility.IconContent("MoveTool"), "ButtonLeft", GUILayout.Width(25f)))
                {
                    Undo.RecordObject(Target, "On Hide Animation Move Toggle");
                    animation.moveToggle = !animation.moveToggle;
                    onHideMoveAnimBool.target = animation.moveToggle;
                    EditorUtility.SetDirty(Target);
                }
                GUI.color = animation.rotateToggle ? color : Color.gray;
                if (GUILayout.Button(EditorGUIUtility.IconContent("RotateTool"), "ButtonMid", GUILayout.Width(25f)))
                {
                    Undo.RecordObject(Target, "On Hide Animation Rotate Toggle");
                    animation.rotateToggle = !animation.rotateToggle;
                    onHideRotateAnimBool.target = animation.rotateToggle;
                    EditorUtility.SetDirty(Target);
                }
                GUI.color = animation.scaleToggle ? color : Color.gray;
                if (GUILayout.Button(EditorGUIUtility.IconContent("ScaleTool"), "ButtonMid", GUILayout.Width(25f)))
                {
                    Undo.RecordObject(Target, "On Hide Animation Scale Toggle");
                    animation.scaleToggle = !animation.scaleToggle;
                    onHideScaleAnimBool.target = animation.scaleToggle;
                    EditorUtility.SetDirty(Target);
                }
                GUI.color = animation.fadeToggle ? color : Color.gray;
                if (GUILayout.Button(EditorGUIUtility.IconContent("ViewToolOrbit"), "ButtonRight", GUILayout.Width(25f)))
                {
                    Undo.RecordObject(Target, "On Hide Animation Fade Toggle");
                    animation.fadeToggle = !animation.fadeToggle;
                    onHideFadeAnimBool.target = animation.fadeToggle;
                    EditorUtility.SetDirty(Target);
                }
                GUI.color = color;
            }

            if (!animation.HasTweenAnimation)
            {
                EditorGUILayout.HelpBox("Please at lease toggle one animation on the toggle", MessageType.Warning);
            }
            //Move
            var moveAnimation = animation.moveAnimation;
            if (EditorGUILayout.BeginFadeGroup(onHideMoveAnimBool.faded))
            {
                GUILayout.BeginHorizontal("Badge");
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Space(40f);
                        GUILayout.Label(EditorGUIUtility.IconContent("MoveTool"));
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    {
                        //Duration、Delay
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Duration", GUILayout.Width(labelWidth));
                            var newDuration = EditorGUILayout.FloatField(moveAnimation.duration);
                            if (newDuration != moveAnimation.duration)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Move Duration");
                                moveAnimation.duration = newDuration;
                                EditorUtility.SetDirty(Target);
                            }
                            // GUILayout.Label("Delay", GUILayout.Width(labelWidth - 20f));
                            // var newDelay = EditorGUILayout.FloatField(moveAnimation.delay);
                            // if (newDelay != moveAnimation.delay)
                            // {
                            //     Undo.RecordObject(Target, "On Hide Animation Move Delay");
                            //     moveAnimation.delay = newDelay;
                            //     EditorUtility.SetDirty(Target);
                            // }
                        }
                        GUILayout.EndHorizontal();
                        //From
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("From", GUILayout.Width(labelWidth));
                            Vector3 newStartValue = EditorGUILayout.Vector3Field(GUIContent.none, moveAnimation.startValue);
                            if (newStartValue != moveAnimation.startValue)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Move From");
                                moveAnimation.startValue = newStartValue;
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                        //Is Custom
                        GUILayout.BeginHorizontal();
                        {
                            using (var disable = new EditorGUI.DisabledGroupScope(!isOverrideTargetTransform))
                            {
                                GUILayout.Label("To", GUILayout.Width(labelWidth - 2f));
                                if (GUILayout.Button(moveAnimation.isCustom ? "Custom Position" : "Direction", "DropDownButton"))
                                {
                                    GenericMenu gm = new GenericMenu();
                                    gm.AddItem(new GUIContent("Direction"), !moveAnimation.isCustom, () => { moveAnimation.isCustom = false; EditorUtility.SetDirty(Target); });
                                    gm.AddItem(new GUIContent("Custom Position"), moveAnimation.isCustom, () => { moveAnimation.isCustom = true; EditorUtility.SetDirty(Target); });
                                    gm.ShowAsContext();
                                }
                            }
                            if (!isOverrideTargetTransform)
                            {
                                moveAnimation.isCustom = false;
                            }
                        }
                        GUILayout.EndHorizontal();
                        //To
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label(GUIContent.none, GUILayout.Width(labelWidth));
                            if (moveAnimation.isCustom)
                            {
                                Vector3 newEndValue = EditorGUILayout.Vector3Field(GUIContent.none, moveAnimation.endValue);
                                if (newEndValue != moveAnimation.endValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Move End");
                                    moveAnimation.endValue = newEndValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            else
                            {
                                var newMoveDirection = (UIMoveAnimation.UIMoveAnimationDirection)EditorGUILayout.EnumPopup(moveAnimation.direction);
                                if (newMoveDirection != moveAnimation.direction)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Move Direction");
                                    moveAnimation.direction = newMoveDirection;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                        }
                        GUILayout.EndHorizontal();
                        //Ease
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Ease", GUILayout.Width(labelWidth));
                            var newEase = (EaseStyle)EditorGUILayout.EnumPopup(moveAnimation.EaseStyle);
                            if (newEase != moveAnimation.EaseStyle)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Move Ease");
                                moveAnimation.EaseStyle = newEase;
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFadeGroup();


            //RotateAnimation
            var rotateAnimation = animation.rotateAnimation;
            if (EditorGUILayout.BeginFadeGroup(onHideRotateAnimBool.faded))
            {
                GUILayout.BeginHorizontal("Badge");
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Space(rotateAnimation.isCustom ? 50f : 40f);
                        GUILayout.Label(EditorGUIUtility.IconContent("RotateTool"));
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    {
                        //Duration、Delay
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Duration", GUILayout.Width(labelWidth));
                            var newDuration = EditorGUILayout.FloatField(rotateAnimation.duration);
                            if (newDuration != rotateAnimation.duration)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Rotate Duration");
                                rotateAnimation.duration = newDuration;
                                EditorUtility.SetDirty(Target);
                            }
                            // GUILayout.Label("Delay", GUILayout.Width(labelWidth - 20f));
                            // var newDelay = EditorGUILayout.FloatField(rotateAnimation.delay);
                            // if (newDelay != rotateAnimation.delay)
                            // {
                            //     Undo.RecordObject(Target, "On Hide Animation Rotate Delay");
                            //     rotateAnimation.delay = newDelay;
                            //     EditorUtility.SetDirty(Target);
                            // }
                        }
                        GUILayout.EndHorizontal();
                        //Is Custom
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                            if (GUILayout.Button(rotateAnimation.isCustom ? "Fixed Rotation" : "Current Rotation", "DropDownButton"))
                            {
                                GenericMenu gm = new GenericMenu();
                                gm.AddItem(new GUIContent("Current Rotation"), !rotateAnimation.isCustom, () => { rotateAnimation.isCustom = false; EditorUtility.SetDirty(Target); });
                                gm.AddItem(new GUIContent("Fixed Rotation"), rotateAnimation.isCustom, () => { rotateAnimation.isCustom = true; EditorUtility.SetDirty(Target); });
                                gm.ShowAsContext();
                            }
                        }
                        GUILayout.EndHorizontal();
                        if (rotateAnimation.isCustom)
                        {
                            //From
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label(GUIContent.none, GUILayout.Width(labelWidth));
                                Vector3 newStartValue = EditorGUILayout.Vector3Field(GUIContent.none, rotateAnimation.startValue);
                                if (newStartValue != rotateAnimation.startValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Rotate From");
                                    rotateAnimation.startValue = newStartValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                        //To
                        GUILayout.BeginHorizontal();
                        {
                            using (var disable = new EditorGUI.DisabledGroupScope(!isOverrideTargetTransform))
                            {
                                GUILayout.Label("To", GUILayout.Width(labelWidth));
                                Vector3 newEndValue = EditorGUILayout.Vector3Field(GUIContent.none, rotateAnimation.endValue);
                                if (newEndValue != rotateAnimation.endValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Rotate To");
                                    rotateAnimation.endValue = newEndValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                        }
                        GUILayout.EndHorizontal();
                        //Rotate Mode
                        // GUILayout.BeginHorizontal();
                        // {
                        //     GUILayout.Label("Mode", GUILayout.Width(labelWidth));
                        //     var newRotateMode = (RotateMode)EditorGUILayout.EnumPopup(rotateAnimation.rotateMode);
                        //     if (newRotateMode != rotateAnimation.rotateMode)
                        //     {
                        //         Undo.RecordObject(Target, "On Hide Animation Rotate Mode");
                        //         rotateAnimation.rotateMode = newRotateMode;
                        //         EditorUtility.SetDirty(Target);
                        //     }
                        // }
                        // GUILayout.EndHorizontal();
                        //Ease
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Ease", GUILayout.Width(labelWidth));
                            var newEase = (EaseStyle)EditorGUILayout.EnumPopup(rotateAnimation.EaseStyle);
                            if (newEase != rotateAnimation.EaseStyle)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Rotate Ease");
                                rotateAnimation.EaseStyle = newEase;
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFadeGroup();



            //ScaleAnimation
            var scaleAnimation = animation.scaleAnimation;
            if (EditorGUILayout.BeginFadeGroup(onHideScaleAnimBool.faded))
            {
                GUILayout.BeginHorizontal("Badge");
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Space(scaleAnimation.isCustom ? 40f : 30f);
                        GUILayout.Label(EditorGUIUtility.IconContent("ScaleTool"));
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    {
                        //Duration、Delay
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Duration", GUILayout.Width(labelWidth));
                            var newDuration = EditorGUILayout.FloatField(scaleAnimation.duration);
                            if (newDuration != scaleAnimation.duration)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Scale Duration");
                                scaleAnimation.duration = newDuration;
                                EditorUtility.SetDirty(Target);
                            }
                            // GUILayout.Label("Delay", GUILayout.Width(labelWidth - 20f));
                            // var newDelay = EditorGUILayout.FloatField(scaleAnimation.delay);
                            // if (newDelay != scaleAnimation.delay)
                            // {
                            //     Undo.RecordObject(Target, "On Hide Animation Scale Delay");
                            //     scaleAnimation.delay = newDelay;
                            //     EditorUtility.SetDirty(Target);
                            // }
                        }
                        GUILayout.EndHorizontal();
                        //Is Custom
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                            if (GUILayout.Button(scaleAnimation.isCustom ? "Fixed Scale" : "Current Scale", "DropDownButton"))
                            {
                                GenericMenu gm = new GenericMenu();
                                gm.AddItem(new GUIContent("Current Scale"), !scaleAnimation.isCustom, () => { scaleAnimation.isCustom = false; EditorUtility.SetDirty(Target); });
                                gm.AddItem(new GUIContent("Fixed Scale"), scaleAnimation.isCustom, () => { scaleAnimation.isCustom = true; EditorUtility.SetDirty(Target); });
                                gm.ShowAsContext();
                            }
                        }
                        GUILayout.EndHorizontal();
                        if (scaleAnimation.isCustom)
                        {
                            //From
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label(GUIContent.none, GUILayout.Width(labelWidth));
                                Vector3 newStartValue = EditorGUILayout.Vector3Field(GUIContent.none, scaleAnimation.startValue);
                                if (newStartValue != scaleAnimation.startValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Scale From");
                                    scaleAnimation.startValue = newStartValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                        //To
                        GUILayout.BeginHorizontal();
                        {
                            using (var disable = new EditorGUI.DisabledGroupScope(!isOverrideTargetTransform))
                            {
                                GUILayout.Label("To", GUILayout.Width(labelWidth));
                                Vector3 newEndValue = EditorGUILayout.Vector3Field(GUIContent.none, scaleAnimation.endValue);
                                if (newEndValue != scaleAnimation.endValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Scale To");
                                    scaleAnimation.endValue = newEndValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                        }
                        GUILayout.EndHorizontal();
                        //Ease
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Ease", GUILayout.Width(labelWidth));
                            var newEase = (EaseStyle)EditorGUILayout.EnumPopup(scaleAnimation.EaseStyle);
                            if (newEase != scaleAnimation.EaseStyle)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Scale Ease");
                                scaleAnimation.EaseStyle = newEase;
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFadeGroup();



            //FadeAnimation
            var fadeAnimation = animation.fadeAnimation;
            if (EditorGUILayout.BeginFadeGroup(onHideFadeAnimBool.faded))
            {
                GUILayout.BeginHorizontal("Badge");
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Space(fadeAnimation.isCustom ? 40f : 30f);
                        GUILayout.Label(EditorGUIUtility.IconContent("ViewToolOrbit"));
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    {
                        //Duration、Delay
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Duration", GUILayout.Width(labelWidth));
                            var newDuration = EditorGUILayout.FloatField(fadeAnimation.duration);
                            if (newDuration != fadeAnimation.duration)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Fade Duration");
                                fadeAnimation.duration = newDuration;
                                EditorUtility.SetDirty(Target);
                            }
                            // GUILayout.Label("Delay", GUILayout.Width(labelWidth - 20f));
                            // var newDelay = EditorGUILayout.FloatField(fadeAnimation.delay);
                            // if (newDelay != fadeAnimation.delay)
                            // {
                            //     Undo.RecordObject(Target, "On Hide Animation Fade Delay");
                            //     fadeAnimation.delay = newDelay;
                            //     EditorUtility.SetDirty(Target);
                            // }
                        }
                        GUILayout.EndHorizontal();
                        //Is Custom
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                            if (GUILayout.Button(fadeAnimation.isCustom ? "Fixed Alpha" : "Current Alpha", "DropDownButton"))
                            {
                                GenericMenu gm = new GenericMenu();
                                gm.AddItem(new GUIContent("Current Alpha"), !fadeAnimation.isCustom, () => { fadeAnimation.isCustom = false; EditorUtility.SetDirty(Target); });
                                gm.AddItem(new GUIContent("Fixed Alpha"), fadeAnimation.isCustom, () => { fadeAnimation.isCustom = true; EditorUtility.SetDirty(Target); });
                                gm.ShowAsContext();
                            }
                        }
                        GUILayout.EndHorizontal();
                        if (fadeAnimation.isCustom)
                        {
                            //From
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label(GUIContent.none, GUILayout.Width(labelWidth));
                                float newStartValue = EditorGUILayout.FloatField(GUIContent.none, fadeAnimation.startValue);
                                if (newStartValue != fadeAnimation.startValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Fade From");
                                    fadeAnimation.startValue = newStartValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                        //To
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("To", GUILayout.Width(labelWidth));
                            float newEndValue = EditorGUILayout.FloatField(GUIContent.none, fadeAnimation.endValue);
                            if (newEndValue != fadeAnimation.endValue)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Fade To");
                                fadeAnimation.endValue = newEndValue;
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                        //Ease
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Ease", GUILayout.Width(labelWidth));
                            var newEase = (EaseStyle)EditorGUILayout.EnumPopup(fadeAnimation.EaseStyle);
                            if (newEase != fadeAnimation.EaseStyle)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Fade Ease");
                                fadeAnimation.EaseStyle = newEase;
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFadeGroup();

        }

    }
}