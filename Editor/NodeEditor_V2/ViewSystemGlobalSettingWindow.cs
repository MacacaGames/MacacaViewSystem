using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

namespace MacacaGames.ViewSystem.NodeEditorV2
{
    public class ViewSystemGlobalSettingWindow : ViewSystemNodeWindow
    {

        public static bool showGlobalSetting;
        ViewSystemDataReaderV2 dataReader;

        static ViewSystemSaveData saveData => ViewSystemNodeEditor.saveData;
        public ViewSystemGlobalSettingWindow(string name, ViewSystemNodeEditor editor, ViewSystemDataReaderV2 dataReader)
        : base(name, editor)
        {
            this.dataReader = dataReader;
            m_ShowEventScript = new AnimBool(true);
            m_ShowEventScript.valueChanged.AddListener(editor.Repaint);
        }

        Vector2 scrollPosition;
        AnimBool m_ShowEventScript;

        public override void Draw(int id)
        {
            //node.clickContainRect = rect;
            using (var scroll = new GUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;
                saveData.globalSetting.ViewControllerObjectPath = EditorGUILayout.TextField("View Controller GameObject", saveData.globalSetting.ViewControllerObjectPath);
                EditorGUILayout.HelpBox("View Controller GameObject is the GameObject name in scene which has ViewController attach on.", MessageType.Info);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    saveData.globalSetting.UIRootScene = (GameObject)EditorGUILayout.ObjectField("UI Root Object (In Scene)", saveData.globalSetting.UIRootScene, typeof(GameObject), true);
                    if (check.changed)
                    {
                        if (saveData.globalSetting.UIRootScene == null)
                        {
                            saveData.globalSetting.UIRoot = null;
                        }
                        else
                        {
                            dataReader.SetUIRootObject(saveData.globalSetting.UIRootScene);
                        }
                    }
                }
                using (var disable = new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUILayout.ObjectField("UI Root Object (In Assets)", saveData.globalSetting.UIRoot, typeof(GameObject), true);
                }
                EditorGUILayout.HelpBox("UI Root Object will generate and set as a child of 'View Controller GameObject' after View System init.", MessageType.Info);

                saveData.globalSetting._maxWaitingTime = EditorGUILayout.Slider(new GUIContent("Change Page Max Waiting", "The max waiting for change page, if previous page need time more than this value ,ViewController wiil force transition to next page."), saveData.globalSetting._maxWaitingTime, 0.5f, 2.5f);
                EditorGUILayout.HelpBox("The max waiting for change page, if previous page need time more than this value ,ViewController wiil force transition to next page.", MessageType.Info);

                saveData.globalSetting.minimumTimeInterval = EditorGUILayout.Slider(new GUIContent("Minimum Interval", "The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call. If user the method call time interval less than this value, the call will be ignore!"), saveData.globalSetting.minimumTimeInterval, 0.05f, 1f);
                EditorGUILayout.HelpBox("The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call. If user the method call time interval less than this value, the call will be ignore!", MessageType.Info);

                using (var horizon = new GUILayout.HorizontalScope())
                {
                    m_ShowEventScript.target = EditorGUILayout.Foldout(m_ShowEventScript.target, "Event Scripts");
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        saveData.globalSetting.EventHandleBehaviour.Add(null);
                    }
                }
                using (var fade = new EditorGUILayout.FadeGroupScope(m_ShowEventScript.faded))
                {
                    if (fade.visible)
                    {
                        for (int i = 0; i < saveData.globalSetting.EventHandleBehaviour.Count; i++)
                        {
                            using (var horizon = new GUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button(GUIContent.none, new GUIStyle("OL Minus"), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                                {
                                    saveData.globalSetting.EventHandleBehaviour.Remove(saveData.globalSetting.EventHandleBehaviour[i]);
                                }
                                saveData.globalSetting.EventHandleBehaviour[i] = (MonoScript)EditorGUILayout.ObjectField(saveData.globalSetting.EventHandleBehaviour[i], typeof(MonoScript), false);

                            }
                        }
                    }
                }
            }
            base.Draw(id);
        }
    }
}
