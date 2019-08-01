using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeBaseSettingWindow
    {
        ViewSystemNodeEditor editor;
        ViewSystemDataReaderV2 dataReader;
        public bool showBaseSettingWindow;
        private Rect rect;
        ViewSystemSaveData saveData;
        public BaseSettingNode node;

        public ViewSystemNodeBaseSettingWindow(ViewSystemNodeEditor editor, ViewSystemDataReaderV2 dataReader)
        {
            this.editor = editor;
            this.dataReader = dataReader;
            showBaseSettingWindow = false;
            saveData = dataReader.GetSetting();
            node = new BaseSettingNode(saveData.baseSetting.nodePosition);
            node.OnDrag += (p) =>
            {
                //saveData.baseSetting.nodePosition = p;

            };
            node.OnNodeSelect += (v) =>
            {
                rect.x = v.rect.x * editor.zoomScale + v.rect.width * editor.zoomScale;
                rect.y = v.rect.y * editor.zoomScale + v.rect.height * editor.zoomScale;
                rect.width = 320;
                rect.height = 350;
                showBaseSettingWindow = true;
            };
            m_ShowEventScript = new AnimBool(true);
            m_ShowEventScript.valueChanged.AddListener(editor.Repaint);
        }

        Vector2 scrollPosition;
        AnimBool m_ShowEventScript;
        public void Draw()
        {
            node.clickContainRect = rect;

            using (var area = new GUILayout.AreaScope(rect, "Base Setting", new GUIStyle("window")))
            {
                using (var scroll = new GUILayout.ScrollViewScope(scrollPosition))
                {
                    scrollPosition = scroll.scrollPosition;
                    //GUILayout.Label("Base Setting", new GUIStyle("DefaultCenteredLargeText"));
                    saveData.baseSetting.ViewControllerObjectPath = EditorGUILayout.TextField("View Controller GameObject", saveData.baseSetting.ViewControllerObjectPath);
                    EditorGUILayout.HelpBox("View Controller GameObject is the GameObject name in scene which has ViewController attach on.", MessageType.Info);

                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        saveData.baseSetting.UIRootScene = (GameObject)EditorGUILayout.ObjectField("UI Root Object (In Scene)", saveData.baseSetting.UIRootScene, typeof(GameObject), true);
                        if (check.changed)
                        {
                            if (saveData.baseSetting.UIRootScene == null)
                            {
                                saveData.baseSetting.UIRoot = null;
                            }
                            else
                            {
                                var go = dataReader.SetUIRootObject(saveData.baseSetting.UIRootScene);
                                saveData.baseSetting.UIRoot = go;
                            }
                        }
                    }
                    using (var disable = new EditorGUI.DisabledGroupScope(true))
                    {
                        EditorGUILayout.ObjectField("UI Root Object (In Assets)", saveData.baseSetting.UIRoot, typeof(GameObject), true);
                    }
                    EditorGUILayout.HelpBox("UI Root Object will generate and set as a child of 'View Controller GameObject' after View System init.", MessageType.Info);

                    saveData.baseSetting._maxWaitingTime = EditorGUILayout.Slider(new GUIContent("Change Page Max Waitning", "The max waiting for change page, if previous page need time more than this value ,ViewController wiil force transition to next page."), saveData.baseSetting._maxWaitingTime, 0, 1);
                    EditorGUILayout.HelpBox("The max waiting for change page, if previous page need time more than this value ,ViewController wiil force transition to next page.", MessageType.Info);

                    using (var horizon = new GUILayout.HorizontalScope())
                    {
                        m_ShowEventScript.target = EditorGUILayout.Foldout(m_ShowEventScript.target, "Event Scripts");
                        if (GUILayout.Button("Add", GUILayout.Width(60)))
                        {
                            saveData.baseSetting.EventHandleBehaviour.Add(null);
                        }
                    }
                    using (var fade = new EditorGUILayout.FadeGroupScope(m_ShowEventScript.faded))
                    {
                        if (fade.visible)
                        {
                            for (int i = 0; i < saveData.baseSetting.EventHandleBehaviour.Count; i++)
                            {
                                using (var horizon = new GUILayout.HorizontalScope())
                                {
                                    if (GUILayout.Button(GUIContent.none, new GUIStyle("OL Minus"), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                                    {
                                        saveData.baseSetting.EventHandleBehaviour.Remove(saveData.baseSetting.EventHandleBehaviour[i]);
                                    }
                                    saveData.baseSetting.EventHandleBehaviour[i] = (MonoBehaviour)EditorGUILayout.ObjectField(saveData.baseSetting.EventHandleBehaviour[i], typeof(MonoBehaviour), false);

                                }
                            }
                        }
                    }

                }
            }

            showBaseSettingWindow = node.isSelect;
            if (!showBaseSettingWindow)
            {
                rect = Rect.zero;
                node.clickContainRect = rect;
            }
        }
    }
    public class BaseSettingNode : ViewSystemNode
    {
        public BaseSettingNode(Vector2 pos)
        {
            this.rect = new Rect(pos.x, pos.y, 160, 60);
            this.nodeType = NodeType.BaseSetting;
            nodeStyleString = "flow node 3";
        }
        public override void Draw()
        {
            DrawNode("Base Setting");
        }
    }
}
