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

        static ViewSystemSaveData saveData => ViewSystemVisualEditor.saveData;
        public ViewSystemGlobalSettingWindow(string name, ViewSystemVisualEditor editor, ViewSystemDataReaderV2 dataReader)
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
                saveData.globalSetting.UIPageTransformLayerName = EditorGUILayout.TextField("Layer name for the page root", saveData.globalSetting.UIPageTransformLayerName);
                saveData.globalSetting.ViewControllerObjectPath = EditorGUILayout.TextField("View Controller GameObject", saveData.globalSetting.ViewControllerObjectPath);
                EditorGUILayout.HelpBox("View Controller GameObject is the GameObject name in scene which has ViewController attach on.", MessageType.Info);
                saveData.globalSetting.UIRoot = (GameObject)EditorGUILayout.ObjectField("UI Root Object (In Assets)", saveData.globalSetting.UIRoot, typeof(GameObject), true);
                if (saveData.globalSetting.UIRoot == null)
                {
                    if (GUILayout.Button("Generate default UI Root Object"))
                    {
                        dataReader.GenerateDefaultUIRoot();
                    }
                }
                EditorGUILayout.HelpBox("The Override UI Root Object will generate and set as a child of 'View Controller GameObject' after View System init.", MessageType.Info);

                saveData.globalSetting._maxWaitingTime = EditorGUILayout.Slider(new GUIContent("Change Page Max Waiting", "The max waiting for change page, if previous page need time more than this value ,ViewController wiil force transition to next page."), saveData.globalSetting._maxWaitingTime, 0.5f, 2.5f);
                //EditorGUILayout.HelpBox("The max waiting for change page, if previous page need time more than this value ,ViewController wiil force transition to next page.", MessageType.Info);

                saveData.globalSetting.minimumTimeInterval = EditorGUILayout.Slider(new GUIContent("Minimum Interval", "The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call. If user the method call time interval less than this value, the call will be ignore!"), saveData.globalSetting.minimumTimeInterval, 0.05f, 1f);
                //EditorGUILayout.HelpBox("The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call. If user the method call time interval less than this value, the call will be ignore!", MessageType.Info);

                saveData.globalSetting.builtInClickProtection = EditorGUILayout.Toggle(new GUIContent("Enable Click Protection", "Enable the builtIn click protection or not, if true, the system will ignore the show page call if any page is transition"), saveData.globalSetting.builtInClickProtection);
                
                //SafePadding
                GUILayout.Label("Global Safe Padding", new GUIStyle("TE toolbarbutton"), GUILayout.Height(EditorGUIUtility.singleLineHeight));

                var contents = new string[] { "Off", "On", };
                using (var change = new EditorGUI.ChangeCheckScope())
                {

                    using (var horizon = new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Left");
                        saveData.globalSetting.edgeValues.left = (SafePadding.EdgeEvaluationMode)GUILayout.Toolbar((int)saveData.globalSetting.edgeValues.left, contents, EditorStyles.miniButton, GUI.ToolbarButtonSize.Fixed);
                    }

                    using (var horizon = new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Bottom");
                        saveData.globalSetting.edgeValues.bottom = (SafePadding.EdgeEvaluationMode)GUILayout.Toolbar((int)saveData.globalSetting.edgeValues.bottom, contents, EditorStyles.miniButton, GUI.ToolbarButtonSize.Fixed);
                    }
                    using (var horizon = new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Top");
                        saveData.globalSetting.edgeValues.top = (SafePadding.EdgeEvaluationMode)GUILayout.Toolbar((int)saveData.globalSetting.edgeValues.top, contents, EditorStyles.miniButton, GUI.ToolbarButtonSize.Fixed);
                    }
                    using (var horizon = new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Right");
                        saveData.globalSetting.edgeValues.right = (SafePadding.EdgeEvaluationMode)GUILayout.Toolbar((int)saveData.globalSetting.edgeValues.right, contents, EditorStyles.miniButton, GUI.ToolbarButtonSize.Fixed);
                    }

                    saveData.globalSetting.edgeValues.influence = EditorGUILayout.Slider("Influence", saveData.globalSetting.edgeValues.influence, 0, 1);
                    saveData.globalSetting.edgeValues.influenceLeft = EditorGUILayout.Slider("Influence Left", saveData.globalSetting.edgeValues.influenceLeft, 0, 1);
                    saveData.globalSetting.edgeValues.influenceBottom = EditorGUILayout.Slider("Influence Bottom", saveData.globalSetting.edgeValues.influenceBottom, 0, 1);
                    saveData.globalSetting.edgeValues.influenceTop = EditorGUILayout.Slider("Influence Top", saveData.globalSetting.edgeValues.influenceTop, 0, 1);
                    saveData.globalSetting.edgeValues.influenceRight = EditorGUILayout.Slider("Influence Right", saveData.globalSetting.edgeValues.influenceRight, 0, 1);
                    saveData.globalSetting.flipPadding = EditorGUILayout.Toggle("Flip Padding", saveData.globalSetting.flipPadding);
                    if (change.changed && ViewSystemVisualEditor.Instance.EditMode)
                    {
                        Undo.RecordObject(saveData, "ViewSystem_Inspector");
                        ViewSystemVisualEditor.ApplySafeArea(saveData.globalSetting.edgeValues);
                    }
                }
                GUILayout.Space(20);
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

            }
            if (GUILayout.Button("Close"))
            {
                Hide();
            }
            base.Draw(id);
        }

    }
}
