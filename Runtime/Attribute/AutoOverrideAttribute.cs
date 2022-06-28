using UnityEngine;
using System.Reflection;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MacacaGames.ViewSystem
{

    public abstract class OverrideAttribute : System.Attribute
    {
        public OverrideAttribute()
        {

        }
        // Though field is more expensive, but since the serialize field now only use field, so implement field first
        public abstract void OnMemberApply(ViewElement viewElement, object source, MemberInfo memberInfo);

    }

    /// <summary>
    /// Bind a Something(Component target); method to UnityEngine.UI.Button's onClick
    /// </summary>
    /// <param name="targetPath"></param>

    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
    public class OverrideButtonEvent : OverrideAttribute
    {
        string targetPath;
        string targetProperty;
        System.Type targetComponent;


        public OverrideButtonEvent(string targetPath)
        {
            this.targetPath = targetPath;
            this.targetProperty = nameof(UnityEngine.UI.Button.onClick);
            this.targetComponent = typeof(UnityEngine.UI.Button);
        }
        ViewElementEventData overrideData;

        public override void OnMemberApply(ViewElement viewElement, object source, MemberInfo memberInfo)
        {
            if (memberInfo is MethodInfo methodInfo)
            {
                var parameters = methodInfo.GetParameters();
                if (parameters.Count() == 0 || parameters.Count() > 1)
                {
                    ViewSystemLog.LogError($"Target method should have only one [Component] parameter when try to override {memberInfo} on {viewElement}");
                    return;
                }

                if (parameters.FirstOrDefault().ParameterType != typeof(UnityEngine.Component))
                {
                    ViewSystemLog.LogError($"Target method parameter type mismatch when try to override {memberInfo} on {viewElement}");
                    return;
                }

                try
                {
                    if (overrideData == null)
                    {
                        overrideData = new ViewElementEventData();
                        overrideData.targetTransformPath = targetPath;
                        overrideData.targetPropertyName = targetProperty;
                        overrideData.targetComponentType = targetComponent.ToString();
                        overrideData.methodName = methodInfo.Name;
                        overrideData.scriptName = source.GetType().ToString();
                    }

                    viewElement.AddEvent(overrideData);
                }
                catch (System.Exception ex)
                {
                    ViewSystemLog.LogError($"Unhandle exception occur when try to override {memberInfo} on {viewElement}, msg: {ex.ToString()}");
                }
            }

        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="targetComponent">The Component which would like to be override, should be a UnityEngine.Object</param>
    /// <param name="targetProperty">The property which would like to be override</param>
    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = true)]
    public class OverrideProperty : OverrideAttribute
    {
        string targetPath;
        string targetProperty;
        System.Type targetComponent;

        public OverrideProperty(string targetPath, System.Type targetComponent, string targetProperty)
        {
            this.targetPath = targetPath;
            this.targetProperty = targetProperty;
            this.targetComponent = targetComponent;
        }
        ViewElementPropertyOverrideData overrideData;

        public override void OnMemberApply(ViewElement viewElement, object source, MemberInfo memberInfo)
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                var targetField = targetComponent.GetMember(targetProperty).FirstOrDefault();
                System.Type targetType = null;

                if (targetField != null)
                {
                    if (targetField.MemberType == MemberTypes.Property)
                    {
                        targetType = (targetField as PropertyInfo).PropertyType;
                    }
                    else if (targetField.MemberType == MemberTypes.Field)
                    {
                        targetType = (targetField as FieldInfo).FieldType;
                    }
                    if (targetType != fieldInfo.FieldType)
                    {
                        ViewSystemLog.LogError($"OverrideAttribute find type mismatch when try to override {memberInfo} on {viewElement}");
                        return;
                    }
                }
                else
                {
                    ViewSystemLog.LogError($"OverrideAttribute member not found when try to override {memberInfo} on {viewElement}");
                    return;
                }


                try
                {
                    if (overrideData == null)
                    {
                        overrideData = new ViewElementPropertyOverrideData();
                        overrideData.targetTransformPath = targetPath;
                        overrideData.targetPropertyName = targetProperty;
                        overrideData.targetComponentType = targetComponent.ToString();
                        var property = new PropertyOverride();
                        property.SetValue(fieldInfo.GetValue(source));
                        overrideData.Value = property;
                    }

                    viewElement.AddOverride(overrideData);
                }
                catch (System.Exception ex)
                {
                    ViewSystemLog.LogError($"Unhandle exception occur when try to override {memberInfo} on {viewElement}, msg: {ex.ToString()}");

                }
            }
        }
    }
}