using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

namespace MacacaGames.ViewSystem.VisualEditor
{
    public class ViewSystemGlobalSettingWindow : ViewSystemNodeWindow
    {

        ViewSystemDataReaderV2 dataReader;

        static ViewSystemSaveData saveData => ViewSystemNodeEditor.saveData;
        public ViewSystemGlobalSettingWindow(string name, ViewSystemNodeEditor editor, ViewSystemDataReaderV2 dataReader)
        : base(name, editor)
        {
            this.dataReader = dataReader;
            m_ShowEventScript = new AnimBool(true);
            m_ShowUserBreakPoints = new AnimBool(true);
            m_ShowEventScript.valueChanged.AddListener(editor.Repaint);
            m_ShowUserBreakPoints.valueChanged.AddListener(editor.Repaint);
        }

        Vector2 scrollPosition;
        AnimBool m_ShowEventScript;
        AnimBool m_ShowUserBreakPoints;

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
                //EditorGUILayout.HelpBox("The max waiting for change page, if previous page need time more than this value ,ViewController wiil force transition to next page.", MessageType.Info);

                saveData.globalSetting.minimumTimeInterval = EditorGUILayout.Slider(new GUIContent("Minimum Interval", "The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call. If user the method call time interval less than this value, the call will be ignore!"), saveData.globalSetting.minimumTimeInterval, 0.05f, 1f);
                //EditorGUILayout.HelpBox("The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call. If user the method call time interval less than this value, the call will be ignore!", MessageType.Info);

                GUILayout.Space(20);

                GUILayout.Label("Built-In Break Points", EditorStyles.boldLabel);
                foreach (var item in saveData.globalSetting.builtInBreakPoints)
                {
                    GUILayout.TextField(item);
                }

                using (var horizon = new GUILayout.HorizontalScope())
                {
                    m_ShowUserBreakPoints.target = EditorGUILayout.Foldout(m_ShowUserBreakPoints.target, "User Break Points");
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        saveData.globalSetting.userBreakPoints.Add("");
                    }
                }
                using (var fade = new EditorGUILayout.FadeGroupScope(m_ShowUserBreakPoints.faded))
                {
                    if (fade.visible)
                    {
                        for (int i = 0; i < saveData.globalSetting.userBreakPoints.Count; i++)
                        {
                            using (var horizon = new GUILayout.HorizontalScope())
                            {
                                saveData.globalSetting.userBreakPoints[i] = GUILayout.TextField(saveData.globalSetting.userBreakPoints[i]);
                                if (GUILayout.Button(GUIContent.none, new GUIStyle("OL Minus"), GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(20)))
                                {
                                    saveData.globalSetting.userBreakPoints.Remove(saveData.globalSetting.userBreakPoints[i]);
                                }
                            }
                        }
                    }
                }
                GUILayout.Space(20);

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
                                saveData.globalSetting.EventHandleBehaviour[i] = (MonoScript)EditorGUILayout.ObjectField(saveData.globalSetting.EventHandleBehaviour[i], typeof(MonoScript), false);
                                if (GUILayout.Button(GUIContent.none, new GUIStyle("OL Minus"), GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(20)))
                                {
                                    saveData.globalSetting.EventHandleBehaviour.Remove(saveData.globalSetting.EventHandleBehaviour[i]);
                                }
                            }
                        }
                    }
                }
                GUILayout.Space(20);


            }
            base.Draw(id);
        }
    }
}
