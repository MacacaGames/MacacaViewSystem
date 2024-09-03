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
using Coroutine = MacacaGames.ViewSystem.MicroCoroutine.Coroutine;

namespace MacacaGames.ViewSystem
{

    [CustomPropertyDrawer(typeof(ViewElementAnimationGroup))]
    public class ViewElementAnimationGroupDrawer : PropertyDrawer
    {
        public ViewElementAnimationGroupDrawer()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabSaving += OnPrefabSaving;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.update += Update;
        }
        private void OnUndo()
        {

        }
        bool IsPreviewing
        {
            get
            {
                return microCoroutine != null && !microCoroutine.IsEmpty && coroutine != null;
            }
        }

        MicroCoroutine microCoroutine;
        Coroutine coroutine;
        float canvasAlpha = 0;
        ViewSystemRectTransformData cacheTransformData;
        private void Update()
        {
            if (microCoroutine != null && !microCoroutine.IsEmpty)
            {
                microCoroutine.Update();
            }
        }

        ~ViewElementAnimationGroupDrawer()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabSaving -= OnPrefabSaving;
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.update -= Update;

        }
        private void OnPrefabSaving(GameObject obj)
        {
            // Debug.Log("Do revert");
            // DoRevert();
        }

        ReorderableList reorderableList;
        SerializedProperty propertySource;

        bool fold = false;
        private Vector2 contextClick;

        private bool instanceUpdate = true;
        void SetDirty()
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(propertySource.serializedObject.targetObject);
            propertySource.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(propertySource.serializedObject.targetObject);
        }
        private UnityEditor.AnimatedValues.AnimBool onHideMoveAnimBool;
        private UnityEditor.AnimatedValues.AnimBool onHideRotateAnimBool;
        private UnityEditor.AnimatedValues.AnimBool onHideScaleAnimBool;
        private UnityEditor.AnimatedValues.AnimBool onHideFadeAnimBool;
        private const float labelWidth = 60f;
        ViewElement viewElement;

        bool init = false;
        void Init(SerializedProperty property)
        {
            if (init)
            {
                return;
            }
            ViewElementAnimationGroup animation = (ViewElementAnimationGroup)fieldInfo.GetValue(property.serializedObject.targetObject);

            onHideMoveAnimBool = new AnimBool(animation.moveToggle);
            onHideRotateAnimBool = new AnimBool(animation.rotateToggle);
            onHideScaleAnimBool = new AnimBool(animation.scaleToggle);
            onHideFadeAnimBool = new AnimBool(animation.fadeToggle);
            propertySource = property;
            microCoroutine = new MicroCoroutine((e) => Debug.LogError(e.ToString()));

            UnityEngine.Object Target = property.serializedObject.targetObject;
            if (Target as Component)
            {
                viewElement = (Target as Component).GetComponent<ViewElement>();
            }
            init = true;
        }
        public override void OnGUI(Rect oriRect, SerializedProperty property, GUIContent label)
        {
            Init(property);

            if (ViewSystemVisualEditor.Instance == null)
            {
                EditorGUILayout.HelpBox(
                    $"Please open MacacaGames > ViewSystem > Visual Editor to see more info about ViewSystem",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"ViewSystem Info: \n ViewPage: {viewElement?.currentViewPage.name} \n ViewPageItem: {viewElement?.currentViewPageItem.Id}",
                    MessageType.Info);
            }

            UnityEngine.Object Target = property.serializedObject.targetObject;
            ViewElementAnimationGroup animationGroup = (ViewElementAnimationGroup)fieldInfo.GetValue(property.serializedObject.targetObject);

            bool isOverrideTargetTransform = false;
            if (Target is ViewElementAnimation)
            {
                isOverrideTargetTransform = (Target as ViewElementAnimation).IsOverrideTarget;
            }

            Color color = GUI.color;

            GUILayout.Label(property.displayName);
            using (var horizon = new EditorGUILayout.HorizontalScope())
            {

                GUI.color = !animationGroup.moveToggle ? color : Color.gray;
                if (GUILayout.Button(EditorGUIUtility.IconContent("MoveTool"), "ButtonLeft", GUILayout.Width(25f)))
                {
                    Undo.RecordObject(Target, "On Hide Animation Move Toggle");
                    animationGroup.moveToggle = !animationGroup.moveToggle;
                    onHideMoveAnimBool.target = animationGroup.moveToggle;
                    EditorUtility.SetDirty(Target);
                }
                GUI.color = !animationGroup.rotateToggle ? color : Color.gray;
                if (GUILayout.Button(EditorGUIUtility.IconContent("RotateTool"), "ButtonMid", GUILayout.Width(25f)))
                {
                    Undo.RecordObject(Target, "On Hide Animation Rotate Toggle");
                    animationGroup.rotateToggle = !animationGroup.rotateToggle;
                    onHideRotateAnimBool.target = animationGroup.rotateToggle;
                    EditorUtility.SetDirty(Target);
                }
                GUI.color = !animationGroup.scaleToggle ? color : Color.gray;
                if (GUILayout.Button(EditorGUIUtility.IconContent("ScaleTool"), "ButtonMid", GUILayout.Width(25f)))
                {
                    Undo.RecordObject(Target, "On Hide Animation Scale Toggle");
                    animationGroup.scaleToggle = !animationGroup.scaleToggle;
                    onHideScaleAnimBool.target = animationGroup.scaleToggle;
                    EditorUtility.SetDirty(Target);
                }
                GUI.color = !animationGroup.fadeToggle ? color : Color.gray;
                if (GUILayout.Button(EditorGUIUtility.IconContent("ViewToolOrbit"), "ButtonRight", GUILayout.Width(25f)))
                {
                    Undo.RecordObject(Target, "On Hide Animation Fade Toggle");
                    animationGroup.fadeToggle = !animationGroup.fadeToggle;
                    onHideFadeAnimBool.target = animationGroup.fadeToggle;
                    EditorUtility.SetDirty(Target);
                }
                GUILayout.Space(10);
                GUI.color = color;

                using (var disable = new EditorGUI.DisabledGroupScope(!(Target is ViewElementAnimation)))
                {
                    if (IsPreviewing)
                    {
                        if (GUILayout.Button("Stop Animation", GUILayout.Width(120)))
                        {
                            var target = (Target as ViewElementAnimation).targetObject;
                            var targetCanvasGroup = (Target as ViewElementAnimation).targetObject.GetComponent<CanvasGroup>(); ;

                            if (targetCanvasGroup != null)
                            {
                                targetCanvasGroup.alpha = canvasAlpha;
                            }
                            if (coroutine != null)
                                microCoroutine.RemoveCoroutine(coroutine);
                            if (cacheTransformData != null)
                                cacheTransformData.SetRectTransform(target);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Preview Animation", GUILayout.Width(120)))
                        {
                            var target = (Target as ViewElementAnimation).targetObject;
                            var targetCanvasGroup = (Target as ViewElementAnimation).targetObject.GetComponent<CanvasGroup>(); ;

                            if (targetCanvasGroup != null)
                            {
                                canvasAlpha = targetCanvasGroup.alpha;
                            }
                            cacheTransformData = new ViewSystemRectTransformData(target);
                            coroutine = microCoroutine.AddCoroutine(
                                animationGroup.Play(
                                    target,
                                    () =>
                                    {
                                        coroutine = null;
                                    }
                                )
                            );
                        }

                    }
                }
                GUI.color = color;
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("d_SaveAs@2x"), "Save"), Drawer.removeButtonStyle, GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                {
                    SaveAsset(animationGroup);
                }
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.FindTexture("d_Profiler.Open@2x"), "Load"), Drawer.removeButtonStyle, GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                {
                    LoadAsset();
                }
            }

            GUILayout.Space(2);

            if (!animationGroup.HasTweenAnimation)
            {
                EditorGUILayout.HelpBox("Please at lease toggle one animation on the toggle", MessageType.Warning);
            }
            EditorGUIUtility.labelWidth = labelWidth;

            //Move
            var moveAnimation = animationGroup.moveAnimation;
            if (EditorGUILayout.BeginFadeGroup(onHideMoveAnimBool.faded))
            {
                GUILayout.BeginHorizontal("Badge");
                {
                    GUILayout.BeginVertical();
                    {
                        // GUILayout.Space(moveAnimation.isCustom ? 50f : 40f);
                        GUILayout.Label(EditorGUIUtility.IconContent("MoveTool"));
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    {
                        //Duration、Delay
                        GUILayout.BeginHorizontal();
                        {
                            var newDuration = EditorGUILayout.FloatField("Duration", moveAnimation.duration);
                            if (newDuration != moveAnimation.duration)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Move Duration");
                                moveAnimation.duration = Mathf.Clamp(newDuration, 0, 1f);
                                EditorUtility.SetDirty(Target);
                            }
                            var newDelay = EditorGUILayout.FloatField("Delay", moveAnimation.delay);
                            if (newDelay != moveAnimation.delay)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Move Delay");
                                moveAnimation.delay = Mathf.Clamp(newDelay, 0, 1f);
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();

                        //From

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                            using (var disable = new EditorGUI.DisabledGroupScope(moveAnimation.useViewSystemFrom))
                            {
                                Vector3 newStartValue = EditorGUILayout.Vector3Field(GUIContent.none, moveAnimation.startValue);
                                if (newStartValue != moveAnimation.startValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Move From");
                                    moveAnimation.startValue = newStartValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            moveAnimation.useViewSystemFrom = GUILayout.Toggle(moveAnimation.useViewSystemFrom, "Current Position", new GUIStyle("ButtonMid"));
                            using (var disable = new EditorGUI.DisabledGroupScope(!(Target is ViewElementAnimation)))
                            {
                                if (GUILayout.Button("FromTransform", "ButtonLeft", GUILayout.Width(120)))
                                {
                                    var a = (Target as ViewElementAnimation);
                                    moveAnimation.startValue = a.GetComponent<RectTransform>().anchoredPosition3D;
                                }
                            }
                        }
                        GUILayout.EndHorizontal();

                        //To
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("To", GUILayout.Width(labelWidth));
                            using (var disable = new EditorGUI.DisabledGroupScope(moveAnimation.useViewSystemTo))
                            {
                                Vector3 newEndValue = EditorGUILayout.Vector3Field(GUIContent.none, moveAnimation.endValue);
                                if (newEndValue != moveAnimation.endValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Move To");
                                    moveAnimation.endValue = newEndValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            moveAnimation.useViewSystemTo = GUILayout.Toggle(moveAnimation.useViewSystemTo, "Current Position", new GUIStyle("ButtonMid"));
                            using (var disable = new EditorGUI.DisabledGroupScope(!(Target is ViewElementAnimation)))
                            {
                                if (GUILayout.Button("FromTransform", "ButtonLeft", GUILayout.Width(120)))
                                {
                                    var a = (Target as ViewElementAnimation);
                                    moveAnimation.endValue = a.GetComponent<RectTransform>().anchoredPosition3D;
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
                                Undo.RecordObject(Target, "On Hide Animation Rotate Ease");
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
            var rotateAnimation = animationGroup.rotateAnimation;
            if (EditorGUILayout.BeginFadeGroup(onHideRotateAnimBool.faded))
            {
                GUILayout.BeginHorizontal("Badge");
                {
                    GUILayout.BeginVertical();
                    {
                        // GUILayout.Space(rotateAnimation.isCustom ? 50f : 40f);
                        GUILayout.Label(EditorGUIUtility.IconContent("RotateTool"));
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    {
                        //Duration、Delay
                        GUILayout.BeginHorizontal();
                        {
                            var newDuration = EditorGUILayout.FloatField("Duration", rotateAnimation.duration);
                            if (newDuration != rotateAnimation.duration)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Rotate Duration");
                                rotateAnimation.duration = Mathf.Clamp(newDuration, 0, 1f);
                                EditorUtility.SetDirty(Target);
                            }
                            var newDelay = EditorGUILayout.FloatField("Delay", rotateAnimation.delay);
                            if (newDelay != rotateAnimation.delay)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Rotate Delay");
                                rotateAnimation.delay = Mathf.Clamp(newDelay, 0, 1f);
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();

                        //From
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                            using (var disable = new EditorGUI.DisabledGroupScope(rotateAnimation.useViewSystemFrom))
                            {
                                Vector3 newStartValue = EditorGUILayout.Vector3Field(GUIContent.none, rotateAnimation.startValue);
                                if (newStartValue != rotateAnimation.startValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Rotate From");
                                    rotateAnimation.startValue = newStartValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            rotateAnimation.useViewSystemFrom = GUILayout.Toggle(rotateAnimation.useViewSystemFrom, "Current Rotation", new GUIStyle("ButtonMid"));
                            using (var disable = new EditorGUI.DisabledGroupScope(!(Target is ViewElementAnimation)))
                            {
                                if (GUILayout.Button("FromTransform", "ButtonLeft", GUILayout.Width(120)))
                                {
                                    var a = (Target as ViewElementAnimation);
                                    rotateAnimation.startValue = a.GetComponent<RectTransform>().localEulerAngles;
                                }
                            }

                        }
                        GUILayout.EndHorizontal();
                        // }
                        //To
                        GUILayout.BeginHorizontal();
                        {

                            GUILayout.Label("To", GUILayout.Width(labelWidth));
                            using (var disable = new EditorGUI.DisabledGroupScope(rotateAnimation.useViewSystemTo))
                            {
                                Vector3 newEndValue = EditorGUILayout.Vector3Field(GUIContent.none, rotateAnimation.endValue);
                                if (newEndValue != rotateAnimation.endValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Rotate To");
                                    rotateAnimation.endValue = newEndValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            rotateAnimation.useViewSystemTo = GUILayout.Toggle(rotateAnimation.useViewSystemTo, "Current Rotation", new GUIStyle("ButtonMid"));
                            using (var disable = new EditorGUI.DisabledGroupScope(!(Target is ViewElementAnimation)))
                            {
                                if (GUILayout.Button("FromTransform", "ButtonLeft", GUILayout.Width(120)))
                                {
                                    var a = (Target as ViewElementAnimation);
                                    rotateAnimation.endValue = a.GetComponent<RectTransform>().localEulerAngles;
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
            var scaleAnimation = animationGroup.scaleAnimation;
            if (EditorGUILayout.BeginFadeGroup(onHideScaleAnimBool.faded))
            {
                GUILayout.BeginHorizontal("Badge");
                {
                    GUILayout.BeginVertical();
                    {
                        // GUILayout.Space(scaleAnimation.isCustom ? 40f : 30f);
                        GUILayout.Label(EditorGUIUtility.IconContent("ScaleTool"));
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    {
                        //Duration、Delay
                        GUILayout.BeginHorizontal();
                        {
                            var newDuration = EditorGUILayout.FloatField("Duration", scaleAnimation.duration);
                            if (newDuration != scaleAnimation.duration)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Scale Duration");
                                scaleAnimation.duration = Mathf.Clamp(newDuration, 0, 1f);
                                EditorUtility.SetDirty(Target);
                            }
                            var newDelay = EditorGUILayout.FloatField("Delay", scaleAnimation.delay);
                            if (newDelay != scaleAnimation.delay)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Scale Delay");
                                scaleAnimation.delay = Mathf.Clamp(newDelay, 0, 1f);
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                        //Is Custom
                        // GUILayout.BeginHorizontal();
                        // {
                        //     GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                        //     if (GUILayout.Button(scaleAnimation.isCustom ? "Fixed Scale" : "Current Scale", "DropDownButton"))
                        //     {
                        //         GenericMenu gm = new GenericMenu();
                        //         gm.AddItem(new GUIContent("Current Scale"), !scaleAnimation.isCustom, () => { scaleAnimation.isCustom = false; EditorUtility.SetDirty(Target); });
                        //         gm.AddItem(new GUIContent("Fixed Scale"), scaleAnimation.isCustom, () => { scaleAnimation.isCustom = true; EditorUtility.SetDirty(Target); });
                        //         gm.ShowAsContext();
                        //     }
                        // }
                        // GUILayout.EndHorizontal();
                        // if (scaleAnimation.isCustom)
                        // {
                        //From
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                            using (var disable = new EditorGUI.DisabledGroupScope(scaleAnimation.useViewSystemFrom))
                            {
                                Vector3 newStartValue = EditorGUILayout.Vector3Field(GUIContent.none, scaleAnimation.startValue);
                                if (newStartValue != scaleAnimation.startValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Scale From");
                                    scaleAnimation.startValue = newStartValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            scaleAnimation.useViewSystemFrom = GUILayout.Toggle(scaleAnimation.useViewSystemFrom, "Current Scale", new GUIStyle("ButtonMid"));
                            using (var disable = new EditorGUI.DisabledGroupScope(!(Target is ViewElementAnimation)))
                            {
                                if (GUILayout.Button("FromTransform", "ButtonLeft", GUILayout.Width(120)))
                                {
                                    var a = (Target as ViewElementAnimation);
                                    scaleAnimation.startValue = a.GetComponent<RectTransform>().localScale;
                                }
                            }

                        }
                        GUILayout.EndHorizontal();
                        // }
                        //To
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("To", GUILayout.Width(labelWidth));
                            using (var disable = new EditorGUI.DisabledGroupScope(scaleAnimation.useViewSystemTo))
                            {
                                Vector3 newEndValue = EditorGUILayout.Vector3Field(GUIContent.none, scaleAnimation.endValue);
                                if (newEndValue != scaleAnimation.endValue)
                                {
                                    Undo.RecordObject(Target, "On Hide Animation Scale To");
                                    scaleAnimation.endValue = newEndValue;
                                    EditorUtility.SetDirty(Target);
                                }
                            }
                            scaleAnimation.useViewSystemTo = GUILayout.Toggle(scaleAnimation.useViewSystemTo, "Current Scale", new GUIStyle("ButtonMid"));
                            using (var disable = new EditorGUI.DisabledGroupScope(!(Target is ViewElementAnimation)))
                            {
                                if (GUILayout.Button("FromTransform", "ButtonLeft", GUILayout.Width(120)))
                                {
                                    var a = (Target as ViewElementAnimation);
                                    scaleAnimation.endValue = a.GetComponent<RectTransform>().localScale;
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
            var fadeAnimation = animationGroup.fadeAnimation;
            if (EditorGUILayout.BeginFadeGroup(onHideFadeAnimBool.faded))
            {
                GUILayout.BeginHorizontal("Badge");
                {
                    GUILayout.BeginVertical();
                    {
                        // GUILayout.Space(fadeAnimation.isCustom ? 40f : 30f);
                        GUILayout.Label(EditorGUIUtility.IconContent("ViewToolOrbit"));
                    }
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    {
                        //Duration、Delay
                        GUILayout.BeginHorizontal();
                        {
                            var newDuration = EditorGUILayout.FloatField("Duration", fadeAnimation.duration);
                            if (newDuration != fadeAnimation.duration)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Fade Duration");
                                fadeAnimation.duration = Mathf.Clamp(newDuration, 0, 1f);
                                EditorUtility.SetDirty(Target);
                            }
                            var newDelay = EditorGUILayout.FloatField("Delay", fadeAnimation.delay);
                            if (newDelay != fadeAnimation.delay)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Fade Delay");
                                fadeAnimation.delay = Mathf.Clamp(newDelay, 0, 1f);
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                        //Is Custom
                        // GUILayout.BeginHorizontal();
                        // {
                        //     GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                        //     if (GUILayout.Button(fadeAnimation.isCustom ? "Fixed Alpha" : "Current Alpha", "DropDownButton"))
                        //     {
                        //         GenericMenu gm = new GenericMenu();
                        //         gm.AddItem(new GUIContent("Current Alpha"), !fadeAnimation.isCustom, () => { fadeAnimation.isCustom = false; EditorUtility.SetDirty(Target); });
                        //         gm.AddItem(new GUIContent("Fixed Alpha"), fadeAnimation.isCustom, () => { fadeAnimation.isCustom = true; EditorUtility.SetDirty(Target); });
                        //         gm.ShowAsContext();
                        //     }
                        // }
                        // GUILayout.EndHorizontal();
                        // if (fadeAnimation.isCustom)
                        // {
                        //From
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("From", GUILayout.Width(labelWidth - 2f));
                            float newStartValue = EditorGUILayout.FloatField(GUIContent.none, fadeAnimation.startValue);
                            if (newStartValue != fadeAnimation.startValue)
                            {
                                Undo.RecordObject(Target, "On Hide Animation Fade From");
                                fadeAnimation.startValue = newStartValue;
                                EditorUtility.SetDirty(Target);
                            }
                        }
                        GUILayout.EndHorizontal();
                        // }
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

                if (Target is ViewElementAnimation && (Target as ViewElementAnimation).targetObject.GetComponent<CanvasGroup>() == null)
                {
                    EditorGUILayout.HelpBox("No CanvasGroup found on TargetObject", MessageType.Error);
                    if (GUILayout.Button("Add one", EditorStyles.miniButton))
                    {
                        (Target as ViewElementAnimation).targetObject.gameObject.AddComponent<CanvasGroup>();
                        EditorUtility.SetDirty(viewElement.gameObject);
                    }
                }
            }
            EditorGUILayout.EndFadeGroup();

        }
        string extension = "asset";
        void SaveAsset(ViewElementAnimationGroup group)
        {
            var filePath = EditorUtility.SaveFilePanelInProject(
                "Save current data",
                 $"NewViewElementAnimationGroup.{extension}",
                extension, "", "Assets");
            if (string.IsNullOrEmpty(filePath)) return;
            if (!File.Exists(filePath))
            {
                var result = ScriptableObject.CreateInstance<ViewElementAnimationGroupAsset>();
                result.viewElementAnimationGroup = group;
                AssetDatabase.CreateAsset(result, filePath);
            }
            else
            {
                var currentAsset = AssetDatabase.LoadAssetAtPath<ViewElementAnimationGroupAsset>(filePath);
                currentAsset.viewElementAnimationGroup = group;
                EditorUtility.SetDirty(currentAsset);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        void LoadAsset()
        {
            string path = EditorUtility.OpenFilePanel("Select asset", "Assets", extension);
            path = path.Replace(Application.dataPath, "Assets");
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log(path);
                return;
            }
            var result = AssetDatabase.LoadAssetAtPath<ViewElementAnimationGroupAsset>(path);
            var json = JsonUtility.ToJson(result.viewElementAnimationGroup);
            var temp = JsonUtility.FromJson<ViewElementAnimationGroup>(json);
            fieldInfo.SetValue(propertySource.serializedObject.targetObject, temp);
            HandleUtility.Repaint();
        }

        void FromViewSystem(Action<ViewElementTransform> OnSelect)
        {
            if (viewElement.currentViewPageItem.breakPointViewElementTransforms == null || viewElement.currentViewPageItem.breakPointViewElementTransforms.Count == 0)
            {
                OnSelect?.Invoke(viewElement?.currentViewPageItem.defaultTransformDatas);
            }
            else
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent($"Default BreakPoint"), false, () =>
                {
                    OnSelect?.Invoke(viewElement?.currentViewPageItem.defaultTransformDatas);
                });
                foreach (var item in viewElement.currentViewPageItem.breakPointViewElementTransforms)
                {
                    menu.AddItem(new GUIContent($"{item.breakPointName} BreakPoint"), false, () =>
                    {
                        OnSelect?.Invoke(item.transformData);
                    });
                }
                menu.ShowAsContext();
            }
        }
    }
}