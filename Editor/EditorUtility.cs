using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    public class VS_EditorUtility
    {
        public static string ParseUnityEngineProperty(string ori)
        {
            if (ori.ToLower().Contains("material"))
            {
                return "material";
            }
            if (ori.ToLower().Contains("sprite"))
            {
                return "sprite";
            }
            if (ori.ToLower().Contains("active"))
            {
                return "active";
            }
            string result = ori.Replace("m_", "");
            result = result.Substring(0, 1).ToLower() + result.Substring(1);
            return result;
        }

        public static Type GetPropertyType(SerializedProperty property)
        {
            var type = property.type;
            var match = System.Text.RegularExpressions.Regex.Match(type, @"PPtr<\$(.*?)>");
            if (match.Success)
                type = "UnityEngine." + match.Groups[1].Value;
            return CloudMacaca.Utility.GetType(type);
        }

        // public static Type GetPropertyObjectType(SerializedProperty property)
        // {
        //     return typeof(UnityEngine.Object).Assembly.GetType("UnityEngine." + GetPropertyType(property));
        // }

        public static bool EditorableField(Rect rect, GUIContent content, SerializedProperty Target, PropertyOverride overProperty)
        {
            EditorGUI.BeginChangeCheck();
            switch (Target.propertyType)
            {
                case SerializedPropertyType.Float:
                    overProperty.SetValue(EditorGUI.FloatField(rect, content, (float)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Integer:
                    overProperty.SetValue(EditorGUI.IntField(rect, content, (int)overProperty.GetValue()));
                    //overProperty.IntValue = EditorGUI.IntField(rect, content, overProperty.IntValue);
                    break;
                case SerializedPropertyType.String:
                    overProperty.StringValue = EditorGUI.TextField(rect, content, overProperty.StringValue);
                    break;
                case SerializedPropertyType.Boolean:
                    overProperty.SetValue(EditorGUI.Toggle(rect, content, (bool)overProperty.GetValue()));
                    //overProperty.BooleanValue = EditorGUI.Toggle(rect, content, overProperty.BooleanValue);
                    break;
                case SerializedPropertyType.Color:
                    overProperty.SetValue(EditorGUI.ColorField(rect, content, (Color)overProperty.GetValue()));
                    //overProperty.ColorValue = EditorGUI.ColorField(rect, content, overProperty.ColorValue);
                    break;
                case SerializedPropertyType.ObjectReference:
                    overProperty.ObjectReferenceValue = EditorGUI.ObjectField(rect, content, overProperty.ObjectReferenceValue, GetPropertyType(Target), false);
                    break;
            }
            return EditorGUI.EndChangeCheck();
        }

    }
}

