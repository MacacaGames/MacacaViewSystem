using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MacacaGames.ViewSystem.VisualEditor
{
    [Serializable]
    public sealed class ViewSystemValidationIssue
    {
        public string code;
        public string owner;
        public string message;
    }

    [Serializable]
    public sealed class ViewSystemValidationResult
    {
        public bool isValid;
        public List<ViewSystemValidationIssue> issues = new List<ViewSystemValidationIssue>();
    }

    /// <summary>Read-only validation shared by editor UI and automation; never opens dialogs.</summary>
    public static class ViewSystemDataValidator
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        public static ViewSystemValidationResult Validate(ViewSystemSaveData data, bool includeBindings = true)
        {
            var result = new ViewSystemValidationResult();
            if (data == null)
            {
                Add(result, "missing_data", "", "ViewSystemSaveData is missing.");
                return result;
            }
            if (data.RequireMigration())
                Add(result, "legacy_data", data.name, "Migrate legacy data before editing node assets.");

            var pages = new List<ViewPage>();
            var states = new List<ViewState>();
            foreach (var node in data.viewPagesNodeSaveDatas ?? new List<ViewPageNodeSaveData>())
            {
                if (node == null || node.data == null || node.data.viewPage == null)
                    Add(result, "missing_page_node", data.name, "A page node or its data is missing.");
                else pages.Add(node.data.viewPage);
            }
            foreach (var node in data.viewStateNodeSaveDatas ?? new List<ViewStateNodeSaveData>())
            {
                if (node == null || node.data == null || node.data.viewState == null)
                    Add(result, "missing_state_node", data.name, "A state node or its data is missing.");
                else states.Add(node.data.viewState);
            }

            ValidateNames(result, pages.Select(page => page.name), "ViewPages");
            ValidateNames(result, states.Select(state => state.name), "ViewStates");
            ValidateNames(result, data.globalSetting?.breakPoints, "BreakPoints");
            var stateNames = new HashSet<string>(states.Select(state => state.name), StringComparer.Ordinal);
            foreach (var page in pages)
                if (!string.IsNullOrEmpty(page.viewState) && !stateNames.Contains(page.viewState))
                    Add(result, "missing_state", page.name, $"ViewState '{page.viewState}' does not exist.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var view in pages.Cast<View>().Concat(states))
            {
                if (view.viewPageItems == null)
                {
                    Add(result, "missing_items", view.name, "The ViewPageItem list is missing.");
                    continue;
                }
                foreach (var item in view.viewPageItems)
                {
                    if (item == null)
                    {
                        Add(result, "missing_item", view.name, "A ViewPageItem is missing.");
                        continue;
                    }
                    string owner = view.name + "/" + item.Id;
                    if (string.IsNullOrEmpty(item.Id) || !ids.Add(item.Id))
                        Add(result, "invalid_item_id", owner, "Item IDs must be non-empty and unique across pages and states.");
                    if (item.viewElementObject == null || item.viewElement == null)
                    {
                        Add(result, "missing_view_element", owner, "Assign a prefab with a ViewElement component.");
                        continue;
                    }
                    if (!PrefabUtility.IsPartOfPrefabAsset(item.viewElementObject))
                        Add(result, "scene_view_element", owner, "ViewElement must reference a prefab asset, not a scene instance.");
                    if (!includeBindings) continue;
                    foreach (var binding in item.overrideDatas ?? new List<ViewElementPropertyOverrideData>())
                        ValidateBinding(result, owner, item, binding);
                    foreach (var binding in item.eventDatas ?? new List<ViewElementEventData>())
                    {
                        ValidateBinding(result, owner, item, binding);
                        if (binding == null) continue;
                        var type = string.IsNullOrEmpty(binding.scriptName) ? null : Utility.GetType(binding.scriptName);
                        if (type == null || !type.GetMethods(Members).Any(method => method.Name == binding.methodName))
                            Add(result, "missing_event_method", owner, $"Event method '{binding.scriptName}.{binding.methodName}' does not exist.");
                    }
                }
            }
            result.isValid = result.issues.Count == 0;
            return result;
        }

        static void ValidateNames(ViewSystemValidationResult result, IEnumerable<string> names, string container)
        {
            if (names == null) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (!ViewSystemScriptBaker.IsValidName(name, container))
                    Add(result, "invalid_baked_name", container, $"'{name}' cannot be baked as a C# identifier.");
                else if (!seen.Add(name.TrimStart('@')))
                    Add(result, "duplicate_name", container, $"Duplicate C# identifier '{name}'.");
            }
        }

        static void ValidateBinding(ViewSystemValidationResult result, string owner,
            ViewPageItem item, ViewSystemComponentData binding)
        {
            if (binding == null)
            {
                Add(result, "missing_binding", owner, "A binding is null.");
                return;
            }
            var target = string.IsNullOrEmpty(binding.targetTransformPath)
                ? item.viewElementObject.transform
                : item.viewElementObject.transform.Find(binding.targetTransformPath);
            if (target == null)
            {
                Add(result, "missing_transform", owner, $"Transform '{binding.targetTransformPath}' does not exist in the prefab.");
                return;
            }
            var type = string.IsNullOrEmpty(binding.targetComponentType) ? null : Utility.GetType(binding.targetComponentType);
            var component = type != null && typeof(Component).IsAssignableFrom(type) ? target.GetComponent(type) : null;
            if (component == null)
            {
                Add(result, "missing_component", owner, $"Component '{binding.targetComponentType}' does not exist on the target.");
                return;
            }
            string property = binding.targetPropertyName;
            if (string.IsNullOrEmpty(property))
            {
                Add(result, "missing_property", owner, "The binding property name is empty.");
                return;
            }
            var parsed = ViewSystemUtilitys.IsUnityEngineTypeOrInheritFromUnityEngineType(type)
                ? ViewSystemUtilitys.ParseUnityEngineProperty(property) : property;
            using (var serialized = new SerializedObject(component))
            {
                if (serialized.FindProperty(property) == null &&
                    type.GetField(property, Members) == null && type.GetProperty(parsed, Members) == null)
                    Add(result, "missing_property", owner, $"Property '{property}' does not exist on '{type.FullName}'.");
            }
        }

        static void Add(ViewSystemValidationResult result, string code, string owner, string message)
        {
            result.issues.Add(new ViewSystemValidationIssue { code = code, owner = owner, message = message });
        }
    }
}
