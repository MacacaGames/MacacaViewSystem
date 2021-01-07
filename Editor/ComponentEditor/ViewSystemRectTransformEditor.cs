using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MacacaGames.ViewSystem.VisualEditor
{
    [CustomEditor(typeof(RectTransform)), CanEditMultipleObjects]
    public class ViewSystemRectTransformEditor : Editor
    {
        Editor defaultEditor;
        public string text = "";
        ViewElement viewElement;
        void OnEnable()
        {
            //When this inspector is created, also create the built-in inspector
            defaultEditor = Editor.CreateEditor(targets, Type.GetType("UnityEditor.RectTransformEditor, UnityEditor"));
            rectTransform = target as RectTransform;
            viewElement = rectTransform.GetComponent<ViewElement>();
        }

        void OnDisable()
        {
            //When OnDisable is called, the default editor we created should be destroyed to avoid memory leakage.
            //Also, make sure to call any required methods like OnDisable
            MethodInfo disableMethod = defaultEditor.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (disableMethod != null)
                disableMethod.Invoke(defaultEditor, null);
            DestroyImmediate(defaultEditor);
        }
        RectTransform rectTransform;
        float mass;
        public override void OnInspectorGUI()
        {
            defaultEditor.OnInspectorGUI();
            if (viewElement?.currentViewPageItem == null)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                $"ViewSystem Info: \n ViewPage: {viewElement.currentViewPage.name} \n ViewPageItem: {viewElement.currentViewPageItem.Id}",
                MessageType.Info);

            using (var disable = new EditorGUI.DisabledGroupScope(!ViewSystemVisualEditor.Instance.EditMode))
            {
                if (GUILayout.Button("Copy value to Default BreakPoint"))
                {
                    CopyValue(viewElement?.currentViewPageItem.defaultTransformDatas, rectTransform);
                }
                if (viewElement.currentViewPageItem.breakPointViewElementTransforms == null || viewElement.currentViewPageItem.breakPointViewElementTransforms.Count == 0)
                {
                    return;
                }
                foreach (var item in viewElement.currentViewPageItem.breakPointViewElementTransforms)
                {
                    if (GUILayout.Button($"Copy value to {item.breakPointName} BreakPoint"))
                    {
                        CopyValue(item.transformData, rectTransform);
                    }
                }
            }
        }

        void CopyValue(ViewElementTransform transformData, RectTransform rectTransform)
        {
            transformData.rectTransformData.anchoredPosition = rectTransform.anchoredPosition3D;
            transformData.rectTransformData.anchorMax = rectTransform.anchorMax;
            transformData.rectTransformData.anchorMin = rectTransform.anchorMin;
            transformData.rectTransformData.pivot = rectTransform.pivot;
            transformData.rectTransformData.localScale = rectTransform.localScale;
            transformData.rectTransformData.localEulerAngles = rectTransform.localEulerAngles;
            transformData.rectTransformData.sizeDelta = rectTransform.sizeDelta;
        }
    }
}
