using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using System.Linq;
namespace MacacaGames.ViewSystem
{


    [CanEditMultipleObjects]

    [CustomEditor(typeof(ViewElement))]
    public class ViewElementEditor : Editor
    {
        private ViewElement viewElement = null;
        private ViewElementGroup viewElementGroup = null;
        private ViewElement parentViewElement = null;
        private ViewElementGroup parentViewElementGroup = null;
        private SerializedProperty m_AnimTriggerProperty;
        private SerializedProperty m_Injection;
        private SerializedProperty onShowHandle;
        private SerializedProperty onLeaveHandle;
        private SerializedProperty recoveryPolicy;
        private SerializedProperty recoveryKeepCount;
        AnimBool showV2Setting = new AnimBool(true);

        void OnEnable()
        {
            viewElement = (ViewElement)target;
            viewElementGroup = viewElement.GetComponent<ViewElementGroup>();
            parentViewElement = viewElement?.GetComponentsInParent<ViewElement>().Where(m => m != viewElement).FirstOrDefault();
            parentViewElementGroup = parentViewElement?.GetComponent<ViewElementGroup>();
            onShowHandle = serializedObject.FindProperty("OnShowHandle");
            onLeaveHandle = serializedObject.FindProperty("OnLeaveHandle");
            recoveryPolicy = serializedObject.FindProperty(nameof(ViewElement.recoveryPolicy));
            recoveryKeepCount = serializedObject.FindProperty(nameof(ViewElement.recoveryKeepCount));
            showV2Setting.valueChanged.AddListener(Repaint);
        }
        void OnDisable()
        {
            showV2Setting.valueChanged.RemoveListener(Repaint);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if ((parentViewElement != null && viewElement != parentViewElement) &&
                (parentViewElementGroup == null && viewElementGroup != parentViewElementGroup))
            {
                EditorGUILayout.HelpBox("ViewElement may not work property while it is a child of other ViewElement, please add ViewElementGroup on the root ViewElement", MessageType.Warning);
            }

            if (parentViewElementGroup || viewElementGroup)
            {
                string msg = viewElementGroup == null ? parentViewElementGroup.name : viewElementGroup.name;
                EditorGUILayout.HelpBox($"This ViewElement is managed by ViewElementGroup {msg}", MessageType.Info);
            }
            using (var change = new EditorGUI.ChangeCheckScope())
            {


                viewElement.transition = (ViewElement.TransitionType)EditorGUILayout.EnumPopup("ViewElement Transition", viewElement.transition);

                switch (viewElement.transition)
                {
                    case ViewElement.TransitionType.Animator:
                        viewElement.animatorTransitionType = (ViewElement.AnimatorTransitionType)EditorGUILayout.EnumPopup("Animator Transition", viewElement.animatorTransitionType);
                        viewElement.AnimationStateName_In = EditorGUILayout.TextField("Show State Name", viewElement.AnimationStateName_In);
                        viewElement.AnimationStateName_Loop = EditorGUILayout.TextField("Loop State Name", viewElement.AnimationStateName_Loop);
                        viewElement.AnimationStateName_Out = EditorGUILayout.TextField("Leave State Name", viewElement.AnimationStateName_Out);
                        if (viewElement.animator != null)
                        {
                            EditorGUILayout.HelpBox("Sepup Complete!", MessageType.Info);
                            if (GUILayout.Button("Reset", EditorStyles.miniButton))
                            {
                                viewElement.Setup();
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("There is no available Animator on GameObject or child.", MessageType.Error);
                            if (GUILayout.Button("Reset", EditorStyles.miniButton))
                            {
                                viewElement.Setup();
                            }
                        }
                        break;
                    case ViewElement.TransitionType.CanvasGroupAlpha:
                        viewElement.canvasInEase = (EaseStyle)EditorGUILayout.EnumPopup("Show Curve", viewElement.canvasInEase);
                        viewElement.canvasInTime = EditorGUILayout.FloatField("Show Curve", viewElement.canvasInTime);
                        viewElement.canvasOutEase = (EaseStyle)EditorGUILayout.EnumPopup("Leave Curve", viewElement.canvasOutEase);
                        viewElement.canvasOutTime = EditorGUILayout.FloatField("Leave Curve", viewElement.canvasOutTime);

                        if (viewElement.canvasGroup == null)
                        {
                            EditorGUILayout.HelpBox("No CanvasGroup found on this GameObject", MessageType.Error);
                            if (GUILayout.Button("Add one", EditorStyles.miniButton))
                            {
                                viewElement.gameObject.AddComponent<CanvasGroup>();
                                EditorUtility.SetDirty(viewElement.gameObject);
                            }
                        }

                        break;
                    case ViewElement.TransitionType.ViewElementAnimation:
                        if (viewElement.viewElementAnimation == null)
                        {
                            EditorGUILayout.HelpBox("No ViewElementAnimation found on this GameObject", MessageType.Error);
                            if (GUILayout.Button("Add one", EditorStyles.miniButton))
                            {
                                viewElement.gameObject.AddComponent<ViewElementAnimation>();
                                EditorUtility.SetDirty(viewElement.gameObject);
                            }
                        }
                        break;
                    case ViewElement.TransitionType.ActiveSwitch:
                        break;
                    case ViewElement.TransitionType.Custom:
                        EditorGUILayout.HelpBox("Remember Invoke the 'Action' send from those UnityEvent's parameters at the end of your OnShow/OnLeave handler, or the ViewElement may not recovery to pool correctlly!", MessageType.Info);
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.PropertyField(onShowHandle, true);
                        EditorGUILayout.PropertyField(onLeaveHandle, true);
                        EditorGUILayout.EndVertical();
                        break;
                }

                viewElement.isSkipOutAnimation = EditorGUILayout.Toggle("Skip Out Animation", viewElement.isSkipOutAnimation);

                showV2Setting.target = EditorGUILayout.Foldout(showV2Setting.target, new GUIContent("V2 Setting", "Below scope is only used in V2 Version"));
                string hintText = "";
                using (var fade = new EditorGUILayout.FadeGroupScope(showV2Setting.faded))
                {
                    if (fade.visible)
                    {
                        viewElement.IsUnique = EditorGUILayout.Toggle("Is Unique", viewElement.IsUnique);

                        if (viewElement.IsUnique)
                        {
                            hintText = "Note : Injection will make target component into Singleton, if there is mutil several same component been inject, you will only be able to get the last injection";
                        }
                        else
                        {
                            hintText = "Only Unique ViewElement can be inject";
                        }
                        EditorGUILayout.HelpBox(hintText, MessageType.Info);

                        EditorGUILayout.PropertyField(
                            recoveryPolicy,
                            new GUIContent(
                                "Recovery Policy",
                                "Controls what happens after a non-unique ViewElement completes leave and enters the runtime pool."));

                        if (!recoveryPolicy.hasMultipleDifferentValues)
                        {
                            var policy = (ViewElementRecoveryPolicy)recoveryPolicy.enumValueIndex;
                            if (policy == ViewElementRecoveryPolicy.KeepN)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(
                                    recoveryKeepCount,
                                    new GUIContent(
                                        "Recovery Keep Count",
                                        "Maximum queued instances. Active and pending-recovery instances are not counted."));
                                EditorGUI.indentLevel--;
                            }

                            if (viewElement.IsUnique && policy != ViewElementRecoveryPolicy.KeepForever)
                            {
                                EditorGUILayout.HelpBox(
                                    "Recovery Policy only applies to non-unique ViewElements. " +
                                    "Unique ownership continues to use the existing ownership stack.",
                                    MessageType.Warning);
                            }
                            else if (policy == ViewElementRecoveryPolicy.DestroyOnRecovery)
                            {
                                EditorGUILayout.HelpBox(
                                    "The ViewElement hierarchy will be permanently destroyed after recovery. " +
                                    "Requested child pools are destroyed with it only when the creating code uses DestroyWithOwner. " +
                                    "Nested unique ViewElements will block unsafe destruction at runtime.",
                                    MessageType.Info);
                            }
                        }
                        
                        viewElement.useInstantPosition = EditorGUILayout.Toggle("Use Instant Position", viewElement.useInstantPosition);
                        EditorGUILayout.HelpBox("If enabled, this ViewElement will move instantly to new position instead of tweening.", MessageType.None);
                    }
                }
                if (change.changed)
                {
                    EditorUtility.SetDirty(viewElement);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
