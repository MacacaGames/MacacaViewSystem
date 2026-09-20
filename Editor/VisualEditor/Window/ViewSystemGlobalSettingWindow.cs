using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AnimatedValues;

namespace MacacaGames.ViewSystem.VisualEditor
{
    public class ViewSystemGlobalSettingWindow : ViewSystemNodeWindow
    {

        ViewSystemDataReader dataReader;

        static ViewSystemSaveData saveData => ViewSystemVisualEditor.saveData;
        public ViewSystemGlobalSettingWindow(string name, ViewSystemVisualEditor editor, ViewSystemDataReader dataReader)
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
                saveData.globalSetting.customPageRootPath = EditorGUILayout.TextField("Custom Page Root path", saveData.globalSetting.customPageRootPath);
                EditorGUILayout.HelpBox(" Define a custom path of the ViewPage, otherwise use the Canvas itself.", MessageType.Info);


                saveData.globalSetting._maxWaitingTime = EditorGUILayout.Slider(new GUIContent("Change Page Max Waiting", "The max waiting for change page, if previous page need time more than this value ,ViewController wiil force transition to next page."), saveData.globalSetting._maxWaitingTime, 0.5f, 2.5f);

                saveData.globalSetting.minimumTimeInterval = EditorGUILayout.Slider(new GUIContent("Minimum Interval", "The minimum effective interval Show/Leave OverlayPage or ChangePage on FullPage call. If user the method call time interval less than this value, the call will be ignore!"), saveData.globalSetting.minimumTimeInterval, 0.05f, 1f);

                saveData.globalSetting.builtInClickProtection = EditorGUILayout.Toggle(new GUIContent("Enable Click Protection", "Enable the builtIn click protection or not, if true, the system will ignore the show page call if any page is transition"), saveData.globalSetting.builtInClickProtection);

                GUILayout.Space(10);
                GUILayout.Label("Addressable Loading", new GUIStyle("TE toolbarbutton"), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                saveData.globalSetting.useAddressableLoading = EditorGUILayout.Toggle(
                    new GUIContent("Use Addressable Loading",
                        "When enabled, Save will generate an addressable save data with AssetReferences for runtime lazy loading. The editor workflow remains unchanged."),
                    saveData.globalSetting.useAddressableLoading);
                if (saveData.globalSetting.useAddressableLoading)
                {
                    // Addressable Group selector dropdown
                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    if (settings != null)
                    {
                        var groupNames = settings.groups
                            .Where(g => g != null)
                            .Select(g => g.Name)
                            .ToArray();

                        int currentIndex = System.Array.IndexOf(groupNames, saveData.globalSetting.addressableGroupName);
                        if (currentIndex < 0) currentIndex = 0;

                        currentIndex = EditorGUILayout.Popup(
                            new GUIContent("Addressable Group", "Select which Addressable Group to assign ViewElement prefabs to."),
                            currentIndex, groupNames);

                        if (groupNames.Length > 0)
                        {
                            saveData.globalSetting.addressableGroupName = groupNames[currentIndex];
                        }

                        saveData.globalSetting.addressableLabelName = EditorGUILayout.TextField(
                            new GUIContent("Addressable Labels", "Comma-separated labels assigned to every ViewElement prefab managed by ViewSystem."),
                            saveData.globalSetting.addressableLabelName);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Addressable Asset Settings not found. Please initialize Addressables first.", MessageType.Warning);
                    }

                    EditorGUILayout.HelpBox(
                        "Addressable Loading is enabled.\n" +
                        "When you Save:\n" +
                        "- All ViewElement prefabs will be auto-assigned to the selected Addressable Group.\n" +
                        "- All managed prefabs will receive the configured Addressable Labels (comma-separated).\n" +
                        "- Two save data files will be generated.\n" +
                        "- Assign ViewSystemData_Addressable to ViewController's Addressable Save Data field.",
                        MessageType.Info);
                }
                DrawGlobalPoolBudgetSettings();
                GUILayout.Space(10);

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

        void DrawGlobalPoolBudgetSettings()
        {
            GUILayout.Space(10);
            GUILayout.Label(
                "Global Pool Budget",
                new GUIStyle("TE toolbarbutton"),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            var globalSetting = saveData.globalSetting;
            var setting = globalSetting.globalPoolBudget;
            if (setting == null)
            {
                Undo.RecordObject(saveData, "Initialize Global Pool Budget Setting");
                setting = new ViewElementGlobalPoolBudgetSetting();
                globalSetting.globalPoolBudget = setting;
                EditorUtility.SetDirty(saveData);
            }

            bool enabled = EditorGUILayout.Toggle(
                new GUIContent(
                    "Enable Global Pool Budget",
                    "Enables automatic trimming of eligible queued ViewElements."),
                setting.enabled);

            ViewElementGlobalEvictionMode mode = setting.mode;
            int highWatermarkInstances = setting.highWatermarkInstances;
            int lowWatermarkInstances = setting.lowWatermarkInstances;
            int highWatermarkHierarchyGameObjects = setting.highWatermarkHierarchyGameObjects;
            int lowWatermarkHierarchyGameObjects = setting.lowWatermarkHierarchyGameObjects;
            int maxEvictionsPerFrame = setting.maxEvictionsPerFrame;
            float autoTrimGraceSeconds = setting.AutoTrimGraceSeconds;
            float idleTrimAfterSeconds = setting.IdleTrimAfterSeconds;
            bool allowEmergencyHardCapOnLowMemory = setting.allowEmergencyHardCapOnLowMemory;

            using (new EditorGUI.DisabledScope(!enabled))
            {
                mode = (ViewElementGlobalEvictionMode)EditorGUILayout.EnumPopup(
                    new GUIContent(
                        "Eviction Mode",
                        "NormalBudget preserves Trim Only In Emergency entries. EmergencyHardCap may trim them when otherwise eligible."),
                    mode);

                highWatermarkInstances = Mathf.Max(
                    0,
                    EditorGUILayout.IntField(
                        new GUIContent(
                            "High Watermark Instances",
                            "Queued ViewElement count that triggers trimming. 0 disables this dimension."),
                        highWatermarkInstances));
                using (new EditorGUI.DisabledScope(highWatermarkInstances == 0))
                {
                    lowWatermarkInstances = EditorGUILayout.IntField(
                        new GUIContent(
                            "Low Watermark Instances",
                            "After trimming starts, continue until the queued ViewElement count reaches this value."),
                        lowWatermarkInstances);
                }
                lowWatermarkInstances = highWatermarkInstances == 0
                    ? 0
                    : Mathf.Clamp(lowWatermarkInstances, 0, highWatermarkInstances);

                highWatermarkHierarchyGameObjects = Mathf.Max(
                    0,
                    EditorGUILayout.IntField(
                        new GUIContent(
                            "High Watermark GameObjects",
                            "Queued hierarchy GameObject count that triggers trimming. 0 disables this dimension."),
                        highWatermarkHierarchyGameObjects));
                using (new EditorGUI.DisabledScope(highWatermarkHierarchyGameObjects == 0))
                {
                    lowWatermarkHierarchyGameObjects = EditorGUILayout.IntField(
                        new GUIContent(
                            "Low Watermark GameObjects",
                            "After trimming starts, continue until the queued hierarchy GameObject count reaches this value."),
                        lowWatermarkHierarchyGameObjects);
                }
                lowWatermarkHierarchyGameObjects = highWatermarkHierarchyGameObjects == 0
                    ? 0
                    : Mathf.Clamp(
                        lowWatermarkHierarchyGameObjects,
                        0,
                        highWatermarkHierarchyGameObjects);

                maxEvictionsPerFrame = Mathf.Max(
                    1,
                    EditorGUILayout.IntField(
                        new GUIContent(
                            "Max Evictions Per Frame",
                            "Maximum number of automatic evictions shared by budget and idle runners in one frame."),
                        maxEvictionsPerFrame));
                autoTrimGraceSeconds = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        new GUIContent(
                            "Auto Trim Grace Seconds",
                            "Minimum time since entering the pool before automatic budget or low-memory trim may evict an entry. Does not enable periodic idle trim."),
                        autoTrimGraceSeconds));
                idleTrimAfterSeconds = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        new GUIContent(
                            "Idle Trim After Seconds",
                            "Time since entering the pool before periodic idle trim may evict an entry. 0 disables periodic idle trim. Automatic grace still applies."),
                        idleTrimAfterSeconds));

                using (new EditorGUI.DisabledScope(mode != ViewElementGlobalEvictionMode.NormalBudget))
                {
                    allowEmergencyHardCapOnLowMemory = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Emergency On Low Memory",
                            "Allows Application.lowMemory to temporarily upgrade NormalBudget trimming to EmergencyHardCap. Auto Trim Grace Seconds still applies."),
                        allowEmergencyHardCapOnLowMemory);
                }
            }

            if (enabled && highWatermarkInstances == 0 &&
                highWatermarkHierarchyGameObjects == 0)
            {
                EditorGUILayout.HelpBox(
                    "Both high watermarks are disabled. No watermark-based trim will run; idle and low-memory triggers remain available.",
                    MessageType.Info);
            }

            if (enabled && mode == ViewElementGlobalEvictionMode.EmergencyHardCap)
            {
                EditorGUILayout.HelpBox(
                    "EmergencyHardCap can evict Trim Only In Emergency entries after the automatic grace period. Nested unique hierarchies remain protected.",
                    MessageType.Warning);
            }

            if (enabled && autoTrimGraceSeconds > 0 && idleTrimAfterSeconds == 0)
            {
                EditorGUILayout.HelpBox(
                    "Periodic idle trim is off. If all candidates are within the automatic grace period, a budget or low-memory attempt stops without a timed retry. Another over-budget enqueue or low-memory event must trigger trimming again.",
                    MessageType.Info);
            }

            if (enabled != setting.enabled ||
                mode != setting.mode ||
                highWatermarkInstances != setting.highWatermarkInstances ||
                lowWatermarkInstances != setting.lowWatermarkInstances ||
                highWatermarkHierarchyGameObjects != setting.highWatermarkHierarchyGameObjects ||
                lowWatermarkHierarchyGameObjects != setting.lowWatermarkHierarchyGameObjects ||
                maxEvictionsPerFrame != setting.maxEvictionsPerFrame ||
                !Mathf.Approximately(autoTrimGraceSeconds, setting.AutoTrimGraceSeconds) ||
                !Mathf.Approximately(idleTrimAfterSeconds, setting.IdleTrimAfterSeconds) ||
                allowEmergencyHardCapOnLowMemory != setting.allowEmergencyHardCapOnLowMemory)
            {
                Undo.RecordObject(saveData, "Change Global Pool Budget Setting");
                setting.enabled = enabled;
                setting.mode = mode;
                setting.highWatermarkInstances = highWatermarkInstances;
                setting.lowWatermarkInstances = lowWatermarkInstances;
                setting.highWatermarkHierarchyGameObjects = highWatermarkHierarchyGameObjects;
                setting.lowWatermarkHierarchyGameObjects = lowWatermarkHierarchyGameObjects;
                setting.maxEvictionsPerFrame = maxEvictionsPerFrame;
                setting.AutoTrimGraceSeconds = autoTrimGraceSeconds;
                setting.IdleTrimAfterSeconds = idleTrimAfterSeconds;
                setting.allowEmergencyHardCapOnLowMemory = allowEmergencyHardCapOnLowMemory;
                EditorUtility.SetDirty(saveData);
            }
        }

    }
}
