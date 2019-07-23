using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace CloudMacaca.ViewSystem
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
        int width = 350;
        int height = 200;
        Vector2 scrollPos;
        GUIStyle renderStyle;

        public bool isMinimal = false;
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
            if (isMinimal)
            {
                height = 70;
            }
            else
            {
                height = 200;
            }
            rect = new Rect(EditorindowWidthAndHeight.x - width - 10, EditorindowWidthAndHeight.y - height, width, height);

            GUI.depth = 1;
            using (var area = new GUILayout.AreaScope(rect, "Console", new GUIStyle("window")))
            {
                GUILayout.Label("");

                using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
                {
                    scrollPos = scroll.scrollPosition;
                    using (var vertical = new EditorGUILayout.VerticalScope())
                    {
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
                                EditorGUILayout.LabelField("", iconStyle, GUILayout.Width(15));
                                EditorGUILayout.SelectableLabel(item.msg, renderStyle);
                            }
                        }
                    }
                }
            }

            GUI.depth = -200;
            DrawMenuBar(rect);

            if (GUI.Button(new Rect(rect.x + rect.width - 30, rect.y + 2, 15, menuBarHeight), new GUIContent(isMinimal ? EditorGUIUtility.FindTexture("winbtn_win_restore") : EditorGUIUtility.FindTexture("Toolbar Minus")), GUIStyle.none))
            {
                isMinimal = !isMinimal;
            }
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

            using (var area = new GUILayout.AreaScope(menuBar, "", EditorStyles.toolbar))
            {
                using (var horizon = new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(5);
                    if (GUILayout.Button(new GUIContent("Clear"), EditorStyles.toolbarButton, GUILayout.Width(40)))
                    {
                        allMsg.Clear();
                    }
                }
            }

        }

    }
}