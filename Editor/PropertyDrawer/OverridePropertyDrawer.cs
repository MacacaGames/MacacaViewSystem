using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace MacacaGames.ViewSystem
{
    [CustomPropertyDrawer(typeof(ViewElementPropertyOverrideData))]
    public class OverridePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            var oriRect = rect;
            rect.height = EditorGUIUtility.singleLineHeight;



            rect.y += EditorGUIUtility.singleLineHeight;
            VS_EditorUtility.SmartOverrideField(rect, property.FindPropertyRelative("Value"), out float lh);
            rect.y += lh;
        }
    }
}