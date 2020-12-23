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
        // MethodInfo _onSceneGUI;
        // MethodInfo _onHeaderGUI;
        // Editor _provideEditor;
        // RectTransform _target;
        // private void OnEnable()
        // {
        //     _target = target as RectTransform;
        //     var assembly = Assembly.GetAssembly(typeof(Editor));
        //     var provideEditorType = assembly.GetTypes().Where(type => type.Name == "RectTransformEditor").FirstOrDefault();
        //     if (provideEditorType == null)
        //     {
        //         //throw new Exception($"Can not find EditorType. type={editorTypeName}");
        //     }

        //     var provideCustomEditorType = GetCustomEditorType(provideEditorType);
        //     var customEditorType = GetCustomEditorType(this.GetType());
        //     if (provideCustomEditorType == null || customEditorType == null || provideCustomEditorType != customEditorType)
        //     {
        //         throw new Exception($"editor type is {provideCustomEditorType}. but CustomEditorType is {customEditorType}");
        //     }

        //     _onSceneGUI = GetMethodInfo(provideEditorType, "OnSceneGUI");
        //     _onHeaderGUI = GetMethodInfo(provideEditorType, "OnHeaderGUI");
        //     if (_onSceneGUI == null)
        //         throw new Exception("_onSceneGUI is null");
        //     if (_onHeaderGUI == null)
        //         throw new Exception("_onHeaderGUI is null");
        //     _provideEditor = Editor.CreateEditor(targets, provideEditorType);
        //     if (_provideEditor == null)
        //         throw new Exception("_provideEditor is null");
        // }
        // Type GetCustomEditorType(Type type)
        // {
        //     var attributes = type.GetCustomAttributes(typeof(CustomEditor), true) as CustomEditor[];
        //     var attribute = attributes.FirstOrDefault();
        //     if (attribute == null)
        //     {
        //         return null;
        //     }

        //     var field = attribute.GetType().GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance);
        //     return field.GetValue(attribute) as Type;
        // }

        // MethodInfo GetMethodInfo(Type type, string method)
        // {
        //     var methodInfo = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        //     return methodInfo;
        // }
        // public override void OnInspectorGUI()
        // {
        //     _provideEditor.OnInspectorGUI();
        //     using (var disable = new EditorGUI.DisabledGroupScope(ViewSystemNodeEditor.Instance != null && !ViewSystemNodeEditor.Instance.EditMode))
        //     {
        //         if (GUILayout.Button("Copy value to ViewSystem"))
        //         {
        //             if (ViewSystemNodeEditor.Instance == null)
        //             {
        //                 return;
        //             }

        //             ViewSystemNodeEditor.Instance.UpdateRectTransformValue(_target.GetComponent<ViewElement>());
        //         }
        //     }

        // }
        //Unity's built-in editor
        Editor defaultEditor;

        void OnEnable()
        {
            //When this inspector is created, also create the built-in inspector
            defaultEditor = Editor.CreateEditor(targets, Type.GetType("UnityEditor.RectTransformEditor, UnityEditor"));
            rectTransform = target as RectTransform;
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
            using (var disable = new EditorGUI.DisabledGroupScope(ViewSystemNodeEditor.Instance != null && !ViewSystemNodeEditor.Instance.EditMode))
            {
                if (GUILayout.Button("Copy value to ViewSystem"))
                {
                    if (ViewSystemNodeEditor.Instance == null)
                    {
                        return;
                    }

                    ViewSystemNodeEditor.Instance.UpdateRectTransformValue(rectTransform.GetComponent<ViewElement>());
                }
            }

        }
    }
}
