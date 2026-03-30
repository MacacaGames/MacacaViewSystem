using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MacacaGames.ViewSystem;

namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Backup asset that stores prefab references before they are cleared.
    /// Used by ViewElementAddressableProcessor to restore references after build.
    /// </summary>
    public class ViewElementReferenceBackup : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string address;
            public GameObject prefab;
        }

        public List<Entry> entries = new List<Entry>();
    }

    /// <summary>
    /// Validation-only editor window for checking ViewElement Addressable status.
    /// Addressable group assignment is now automated during builds via ViewElementAddressableProcessor.
    /// </summary>
    public class ViewElementAddressPopulator : EditorWindow
    {
        private ViewSystemSaveData saveData;
        private Vector2 scrollPos;
        private List<ViewElementValidationResult> lastResults = new List<ViewElementValidationResult>();
        private float resultNameWidth = 250f;
        private bool isResizing = false;

        [MenuItem("MacacaGames/ViewSystem/ViewElement Address Populator")]
        public static void ShowWindow()
        {
            var win = GetWindow<ViewElementAddressPopulator>("ViewElement Address Populator");
            win.minSize = new Vector2(500, 400);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("ViewElement Address Populator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Addressable group assignment is now automated during builds.\n" +
                "This tool is for validation only — it does not modify any data.",
                MessageType.Info);

            EditorGUILayout.Space();

            saveData = (ViewSystemSaveData)EditorGUILayout.ObjectField(
                "ViewSystem SaveData", saveData, typeof(ViewSystemSaveData), false);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(saveData == null))
            {
                if (GUILayout.Button("Validate", GUILayout.Height(30)))
                {
                    lastResults = ViewElementAddressableProcessor.Validate(saveData);
                }
            }

            EditorGUILayout.Space();

            if (lastResults.Count > 0)
            {
                DrawResultsTable();
            }
        }

        private void DrawResultsTable()
        {
            int okCount = lastResults.Count(r => r.status == "ok" || r.status == "already_set");
            int unregCount = lastResults.Count(r => r.status == "unregistered");
            int nullCount = lastResults.Count(r => r.status == "no_prefab");
            EditorGUILayout.LabelField(
                $"Results: {lastResults.Count} items  |  Registered: {okCount}  |  Unregistered: {unregCount}  |  No Prefab: {nullCount}",
                EditorStyles.boldLabel);

            EditorGUILayout.Space(2);

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Status", GUILayout.Width(40));
            EditorGUILayout.LabelField("Prefab Name", GUILayout.Width(resultNameWidth));

            var resizeRect = GUILayoutUtility.GetRect(8, 20, GUILayout.Width(8));
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);
            HandleResize(resizeRect);

            EditorGUILayout.LabelField("Address / Status");
            EditorGUILayout.EndHorizontal();

            // Rows
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var r in lastResults)
            {
                EditorGUILayout.BeginHorizontal();

                string icon;
                switch (r.status)
                {
                    case "ok":           icon = "\u2705"; break;
                    case "already_set":  icon = "\u2705"; break;
                    case "unregistered": icon = "\u26A0"; break;
                    case "no_prefab":    icon = "\u274C"; break;
                    default:             icon = "?"; break;
                }
                EditorGUILayout.LabelField(icon, GUILayout.Width(40));

                EditorGUILayout.SelectableLabel(r.name, EditorStyles.label,
                    GUILayout.Width(resultNameWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));

                GUILayout.Space(8);

                string detail;
                switch (r.status)
                {
                    case "ok":           detail = r.address; break;
                    case "already_set":  detail = $"{r.address}  (registered)"; break;
                    case "unregistered": detail = "Not in Addressables (will be auto-registered during build)"; break;
                    case "no_prefab":    detail = "No Prefab reference"; break;
                    default:             detail = r.status; break;
                }
                EditorGUILayout.SelectableLabel(detail, EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void HandleResize(Rect resizeRect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition))
            {
                isResizing = true;
                e.Use();
            }
            if (isResizing)
            {
                if (e.type == EventType.MouseDrag)
                {
                    resultNameWidth = Mathf.Clamp(e.mousePosition.x - 50, 80, position.width - 200);
                    Repaint();
                    e.Use();
                }
                if (e.type == EventType.MouseUp)
                {
                    isResizing = false;
                    e.Use();
                }
            }
        }
    }
}
