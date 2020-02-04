using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace CloudMacaca.ViewSystem
{
    public class VS_EditorUtility
    {
        public static bool IsPropertyNeedIgnore(SerializedProperty prop)
        {
            return prop.name == "m_Script" ||
                prop.name == "m_Name" ||
                prop.propertyType == SerializedPropertyType.LayerMask ||
                prop.propertyType == SerializedPropertyType.Rect ||
                prop.propertyType == SerializedPropertyType.RectInt ||
                prop.propertyType == SerializedPropertyType.Bounds ||
                prop.propertyType == SerializedPropertyType.BoundsInt ||
                prop.propertyType == SerializedPropertyType.Quaternion ||
                prop.propertyType == SerializedPropertyType.Vector2Int ||
                prop.propertyType == SerializedPropertyType.Vector3Int ||
                prop.propertyType == SerializedPropertyType.Vector4 ||
                prop.propertyType == SerializedPropertyType.Gradient ||
                prop.propertyType == SerializedPropertyType.ArraySize ||
                prop.propertyType == SerializedPropertyType.AnimationCurve ||
                prop.propertyType == SerializedPropertyType.Character ||
                prop.propertyType == SerializedPropertyType.FixedBufferSize;
        }

        static BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public static Type GetPropertyType(SerializedProperty property)
        {
            System.Type parentType = property.serializedObject.targetObject.GetType();
            System.Reflection.FieldInfo fi = parentType.GetField(property.propertyPath, flags);

            if (fi != null)
            {
                return fi.FieldType;
            }
            string p = property.propertyPath;
            if (parentType.ToString().Contains("UnityEngine."))
            {
                p = ViewSystemUtilitys.ParseUnityEngineProperty(property.propertyPath);
            }
            System.Reflection.PropertyInfo pi = parentType.GetProperty(p, flags);
            if (pi != null)
            {
                return pi.PropertyType;
            }

            return CloudMacaca.Utility.GetType(property.type) ?? typeof(UnityEngine.Object);
        }

        public static bool EditorableField(Rect rect, SerializedProperty Target, PropertyOverride overProperty, out float lineHeight)
        {
            lineHeight = EditorGUIUtility.singleLineHeight * 2.5f;
            if (Target == null || overProperty == null)
            {
                GUI.Label(rect, "There is some property wrong on the override");
                return false;
            }
            GUIContent content = new GUIContent(Target?.displayName);
            EditorGUI.BeginChangeCheck();
            switch (Target.propertyType)
            {
                case SerializedPropertyType.Vector3:
                    lineHeight = EditorGUIUtility.singleLineHeight * 3.5f;
                    overProperty.SetValue(EditorGUI.Vector3Field(rect, content, (Vector3)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Vector2:
                    lineHeight = EditorGUIUtility.singleLineHeight * 3.5f;
                    overProperty.SetValue(EditorGUI.Vector2Field(rect, content, (Vector2)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Float:
                    overProperty.SetValue(EditorGUI.FloatField(rect, content, (float)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Integer:
                    overProperty.SetValue(EditorGUI.IntField(rect, content, (int)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.String:
                    overProperty.StringValue = EditorGUI.TextField(rect, content, overProperty.StringValue);
                    break;
                case SerializedPropertyType.Boolean:
                    overProperty.SetValue(EditorGUI.Toggle(rect, content, (bool)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.Color:
                    overProperty.SetValue(EditorGUI.ColorField(rect, content, (Color)overProperty.GetValue()));
                    break;
                case SerializedPropertyType.ObjectReference:
                    overProperty.ObjectReferenceValue = EditorGUI.ObjectField(rect, content, overProperty.ObjectReferenceValue, GetPropertyType(Target), false);
                    break;
                case SerializedPropertyType.Enum:
                    overProperty.SetValue(EditorGUI.EnumPopup(rect, content, (Enum)overProperty.GetValue()));
                    break;
            }
            return EditorGUI.EndChangeCheck();
        }

        public class ViewPageItemDetailPopup : UnityEditor.PopupWindowContent
        {
            ViewPageItem viewPageItem;
            Rect rect;
            public ViewPageItemDetailPopup(Rect rect, ViewPageItem viewPageItem)
            {
                this.viewPageItem = viewPageItem;
                this.rect = rect;
            }
            public override Vector2 GetWindowSize()
            {
                return new Vector2(rect.width, EditorGUIUtility.singleLineHeight * 6);
            }

            GUIStyle _toggleStyle;
            GUIStyle toggleStyle
            {
                get
                {
                    if (_toggleStyle == null)
                    {
                        _toggleStyle = new GUIStyle
                        {
                            normal = {
                                background = CMEditorUtility.CreatePixelTexture("_toggleStyle_on",new Color32(64,64,64,255)),
                                textColor = Color.gray
                            },
                            onNormal = {
                                    background = CMEditorUtility.CreatePixelTexture("_toggleStyle",new Color32(128,128,128,255)),

                                 textColor = Color.white
                            },

                            alignment = TextAnchor.MiddleCenter,
                            clipping = TextClipping.Clip,
                            imagePosition = ImagePosition.TextOnly,
                            stretchHeight = true,
                            stretchWidth = true,
                            padding = new RectOffset(0, 0, 0, 0),
                            margin = new RectOffset(0, 0, 0, 0)
                        };
                    }
                    return _toggleStyle;
                }
            }

            public override void OnGUI(Rect rect)
            {
                viewPageItem.easeType = (DG.Tweening.Ease)EditorGUILayout.EnumPopup(new GUIContent("Ease", "The EaseType when needs to tween."), viewPageItem.easeType);

                viewPageItem.TweenTime = EditorGUILayout.Slider(new GUIContent("Tween Time", "Tween Time use to control when ViewElement needs change parent."), viewPageItem.TweenTime, -1, 1);

                viewPageItem.delayIn = EditorGUILayout.Slider("Delay In", viewPageItem.delayIn, 0, 1);

                viewPageItem.delayOut = EditorGUILayout.Slider("Delay Out", viewPageItem.delayOut, 0, 1);
                //viewPageItem.excludePlatform = (ViewPageItem.PlatformOption)EditorGUILayout.EnumFlagsField(new GUIContent("Excude Platform", "Excude Platform define the platform which wish to show the ViewPageItem or not"), viewPageItem.excludePlatform);
                CMEditorLayout.BitMaskField( ref viewPageItem.excludePlatform);
            }
        }
    }
}

