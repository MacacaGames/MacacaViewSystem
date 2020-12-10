using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using System.Linq;
namespace MacacaGames.ViewSystem
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

            foreach (var item in runtimePool.GetDicts())
            {
                var queue = item.Value;
                if (queue == null)
                {
                    continue;
                }
                GUILayout.Label($"{TryGetPoolNameByInstanceId(item.Key)} : {queue.Count}");
            }
            GUILayout.Label($"Recovery Queue Status");

            foreach (var item in runtimePool.GetRecycleQueue())
            {
                var queue = item;
                if (queue == null)
                {
                    continue;
                }
                if (GUILayout.Button($"{queue.name}"))
                {
                    EditorGUIUtility.PingObject(queue.gameObject);
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