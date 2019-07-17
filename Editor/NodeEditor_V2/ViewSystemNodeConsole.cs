using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeConsole
    {
        public bool showConsole = true;

        public struct ConsoleMsg
        {
            public string msg;
            public MessageType type;
        }
        public enum MessageType
        {
            Normal,
            Warring,
            Error
        }
        public void LogErrorMessage(string msg)
        {
            LogMessage(msg, MessageType.Error);
        }
        public void LogWarringMessage(string msg)
        {
            LogMessage(msg, MessageType.Warring);
        }
        public void LogMessage(string msg, MessageType messageType = MessageType.Normal)
        {
            var m = new ConsoleMsg();
            m.msg = msg;
            m.type = messageType;
            allMsg.Add(m);
        }

        List<ConsoleMsg> allMsg = new List<ConsoleMsg>();
        Rect rect;
        const int consoleId = 65535;
        const int width = 350;
        const int height = 200;
        Vector2 scrollPos;
        GUIStyle renderStyle;
        GUIStyle iconStyle;
        public void Draw(Vector2 EditorindowWidthAndHeight)
        {
            if (!showConsole)
            {
                return;
            }
            if (renderStyle == null)
            {
                renderStyle = new GUIStyle(GUI.skin.label);
            }
            rect = new Rect(EditorindowWidthAndHeight.x - width - 10, EditorindowWidthAndHeight.y - height, width, height);

            GUI.depth = 1;
            GUILayout.BeginArea(rect, "Console", new GUIStyle("window"));

            GUILayout.BeginVertical();
            GUILayout.Label("");
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            foreach (var item in allMsg.ToArray())
            {
                switch (item.type)
                {
                    case MessageType.Normal:
                        iconStyle = new GUIStyle("CN EntryInfoIconSmall");
                        break;
                    case MessageType.Error:
                        iconStyle = new GUIStyle("CN EntryErrorIconSmall");
                        break;
                    case MessageType.Warring:
                        iconStyle = new GUIStyle("CN EntryWarnIconSmall");
                        break;
                }
    
                using (var horizontalScope = new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("", iconStyle,GUILayout.Width(15));
                    EditorGUILayout.SelectableLabel(item.msg, renderStyle);
                }

            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
            GUI.depth = -200;
            DrawMenuBar(rect);
            if (GUI.Button(new Rect(rect.x + rect.width - 15, rect.y + 2, 15, menuBarHeight), new GUIContent(EditorGUIUtility.FindTexture("winbtn_win_close")), GUIStyle.none))
            {
                showConsole = false;
            }
        }

        private float menuBarHeight = 20f;
        private Rect menuBar;
        private void DrawMenuBar(Rect consoleRect)
        {
            menuBar = new Rect(rect.x + 1, rect.y + 16, rect.width - 1, menuBarHeight);

            GUILayout.BeginArea(menuBar, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            GUILayout.Space(5);
            if (GUILayout.Button(new GUIContent("Clear"), EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                allMsg.Clear();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

    }
}