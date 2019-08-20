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

            public override void OnGUI(Rect rect)
            {
                viewPageItem.easeType = (DG.Tweening.Ease)EditorGUILayout.EnumPopup(new GUIContent("Ease", "The EaseType when needs to tween."), viewPageItem.easeType);

                viewPageItem.TweenTime = EditorGUILayout.Slider(new GUIContent("Tween Time", "Tween Time use to control when ViewElement needs change parent."), viewPageItem.TweenTime, 0, 1);

                viewPageItem.delayIn = EditorGUILayout.Slider("Delay In", viewPageItem.delayIn, 0, 1);

                viewPageItem.delayOut = EditorGUILayout.Slider("Delay Out", viewPageItem.delayOut, 0, 1);


                bool isExcloudAndroid = !viewPageItem.excludePlatform.Contains(ViewPageItem.PlatformOption.Android);
                bool isExcloudiOS = !viewPageItem.excludePlatform.Contains(ViewPageItem.PlatformOption.iOS);
                bool isExcloudtvOS = !viewPageItem.excludePlatform.Contains(ViewPageItem.PlatformOption.tvOS);
                bool isExcloudUWP = !viewPageItem.excludePlatform.Contains(ViewPageItem.PlatformOption.UWP);

                EditorGUIUtility.labelWidth = 20.0f;

                string proIconFix = "";
                if (EditorGUIUtility.isProSkin)
                {
                    proIconFix = "d_";
                }
                else
                {
                    proIconFix = "";
                }

                EditorGUI.BeginChangeCheck();
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    using (var horizon = new EditorGUILayout.HorizontalScope())
                    {
                        isExcloudAndroid = EditorGUILayout.Toggle(new GUIContent(EditorGUIUtility.FindTexture(proIconFix + "BuildSettings.Android.Small")), isExcloudAndroid);
                        isExcloudiOS = EditorGUILayout.Toggle(new GUIContent(EditorGUIUtility.FindTexture(proIconFix + "BuildSettings.iPhone.Small")), isExcloudiOS);
                        isExcloudtvOS = EditorGUILayout.Toggle(new GUIContent(EditorGUIUtility.FindTexture(proIconFix + "BuildSettings.tvOS.Small")), isExcloudtvOS);
                        isExcloudUWP = EditorGUILayout.Toggle(new GUIContent(EditorGUIUtility.FindTexture(proIconFix + "BuildSettings.Standalone.Small")), isExcloudUWP);
                    }

                    if (check.changed)
                    {
                        viewPageItem.excludePlatform.Clear();

                        if (!isExcloudAndroid)
                        {
                            viewPageItem.excludePlatform.Add(ViewPageItem.PlatformOption.Android);
                        }
                        if (!isExcloudiOS)
                        {
                            viewPageItem.excludePlatform.Add(ViewPageItem.PlatformOption.iOS);
                        }
                        if (!isExcloudtvOS)
                        {
                            viewPageItem.excludePlatform.Add(ViewPageItem.PlatformOption.tvOS);
                        }
                        if (!isExcloudUWP)
                        {
                            viewPageItem.excludePlatform.Add(ViewPageItem.PlatformOption.tvOS);
                        }
                    }
                }

            }

        }

    }
}

