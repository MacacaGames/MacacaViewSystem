using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;

namespace MacacaGames.ViewSystem
{
    [CustomPropertyDrawer(typeof(ViewSystemDropdownAttribute))]
    public class ViewSystemDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                EditorGUILayout.HelpBox("This attribute is only valid on string properties", MessageType.Error);
                return;
            }
        
            string[] options = GetOptionsFromViewSystemScriptable();
            if (options == null || options.Length == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                EditorGUILayout.HelpBox("Cannot find ViewSystemScriptable, please bake one in [MacacaGames/ViewSystem/VisualEditor] if you want to use Popup.", MessageType.Warning);
                return;
            }
        
            int index = Array.IndexOf(options, property.stringValue);
            index = EditorGUI.Popup(position, label.text, index, options);
            EditorGUILayout.HelpBox("If you cannot find the page you want, please re-bake one in [MacacaGames/ViewSystem/VisualEditor]", MessageType.Info);
            
            if (index >= 0 && index < options.Length)
            {
                property.stringValue = options[index];
            }
        }
        
        public static string[] GetOptionsFromViewSystemScriptable()
        {
            Type viewSystemScriptableType =
                MacacaGames.Utility.GetType("MacacaGames.ViewSystem.ViewSystemScriptable+ViewPages");
            if (viewSystemScriptableType == null)
            {
                // The class does not exist
                return null;
            }
            
            FieldInfo[] fields =
                viewSystemScriptableType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            string[] options = fields.Select(field => field.GetValue(null).ToString()).ToArray();
            return options;
        }
    }
}