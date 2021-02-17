using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MacacaGames.ViewSystem
{
    public class ReadOnlyAttribute : PropertyAttribute { }
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
#endif
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
    [System.Obsolete("ViewEventGroup is change to ViewSystemEvent",true)]
    public class ViewEventGroup : System.Attribute
    {
        string groupName;
        public ViewEventGroup(string groupName)
        {
            this.groupName = groupName;
        }

        public string GetGroupName()
        {
            return groupName;
        }
    }
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
    public class ViewSystemEventAttribute : System.Attribute
    {
        string groupName;
        public ViewSystemEventAttribute()
        {
            this.groupName = "Default";
        }
        public ViewSystemEventAttribute(string groupName)
        {
            this.groupName = groupName;
        }
        public string GetGroupName()
        {
            return groupName;
        }
    }
}
