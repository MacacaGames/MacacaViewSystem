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
    [System.Obsolete("ViewEventGroup is change to ViewSystemEvent", true)]
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


    /// <summary>
    /// Mark a property or field in ViewElementBehaviour that can be inject value which is managed by ViewSystem.
    /// 
    /// Use the injectScope the control where the value will be search
    /// 
    ///  PageModel, the page model is send when you call the ChangePage API.
    ///  SharedModel, the system shareed model, use the ViewController.Instance.RegisteSharedViewElementModel() the registe the system model.
    /// 
    /// InjectScope.PageFirst : Search the value from the PageModel first and then SharedModel
    /// InjectScope.PageOnly : Search the value from the PageModel only.
    /// InjectScope.SharedFirst : Search the value from the SharedModel first, and then PageModel, 
    /// InjectScope.SharedOnly : Search the value from the SharedModel only.
    /// 
    /// Default is InjectScope.PageFirst
    /// 
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, AllowMultiple = true)]
    public class ViewElementInjectAttribute : System.Attribute
    {
        internal InjectScope injectScope = InjectScope.PageFirst;
        public ViewElementInjectAttribute(InjectScope injectScope)
        {
            this.injectScope = injectScope;
        }

        public ViewElementInjectAttribute()
        {
            injectScope = InjectScope.PageFirst;
        }
    }
    public enum InjectScope
    {
        PageFirst,
        SharedFirst,
        PageOnly,
        SharedOnly,
    }
}
