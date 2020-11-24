using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
namespace CloudMacaca.ViewSystem
{
    [CustomEditor(typeof(ViewElementRuntimePool))]
    public class ViewElementRuntimePoolEditor : Editor
    {
        private ViewElementRuntimePool runtimePool = null;

        void OnEnable()
        {
            runtimePool = (ViewElementRuntimePool)target;

        }
        void OnDisable()
        {
        }
        public override void OnInspectorGUI()
        {
            GUILayout.Label($"Pool Status");
            using (var disable = new EditorGUI.DisabledGroupScope(true))
            {
                foreach (var item in runtimePool.GetDicts())
                {
                    var queue = item.Value;
                    if (queue == null)
                    {
                        continue;
                    }
                    GUILayout.Label($"{TryGetPoolNameByInstanceId(item.Key)} : {queue.Count}");
                }
            }
        }

        public string TryGetPoolNameByInstanceId(int id)
        {
            string name = "";
            runtimePool.veNameDicts.TryGetValue(id, out name);
            if (string.IsNullOrEmpty(name))
            {
                return "ID:" + id.ToString();
            }
            return name;
        }
    }
}