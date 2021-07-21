using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;
using System.Reflection;
using UnityEditor.IMGUI.Controls;
using MacacaGames;

namespace MacacaGames.ViewSystem.VisualEditor
{
    public class ViewSystemNodeInspector
    {
        private ViewSystemNode currentSelectNode = null;
        ViewSystemVisualEditor editor;
        public bool show = true;
        AnimBool showBasicInfo;
        // AnimBool showViewPageItem;
        ReorderableList viewPageItemReorderableList;
        GUIStyle nameStyle;
        GUIStyle nameUnnamedStyle;
        GUIStyle nameErrorStyle;
        GUIStyle nameEditStyle;
        static ViewSystemSaveData saveData => ViewSystemVisualEditor.saveData;
        static GUIContent EditoModifyButton = new GUIContent(Drawer.overridePopupIcon, "Show/Hide Modified Properties and Events");


        public ViewSystemNodeInspector(ViewSystemVisualEditor editor)
        {
            this.editor = editor;

            show = true;
            showBasicInfo = new AnimBool(true);
            showBasicInfo.valueChanged.AddListener(this.editor.Repaint);

            if (EditorGUIUtility.isProSkin)
            {
                nameStyle = new GUIStyle
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                nameUnnamedStyle = new GUIStyle
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1) }
                };
            }
            else
            {
                nameStyle = new GUIStyle
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.black }
                };
                nameUnnamedStyle = new GUIStyle
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = Color.gray }
                };
            }

            nameErrorStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.red }
            };

            nameEditStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                imagePosition = ImagePosition.ImageOnly,
                stretchWidth = false,
                stretchHeight = false,
                fixedHeight = 14,
                fixedWidth = 14,
                onNormal = { background = EditorGUIUtility.FindTexture("d_FilterSelectedOnly") },
                normal = { background = EditorGUIUtility.FindTexture("d_editicon.sml") }
            };

            excludePlatformOptions.Clear();

            foreach (var item in Enum.GetValues(typeof(ViewPageItem.PlatformOption)))
            {
                excludePlatformOptions.Add(item.ToString());
            }
        }
        public static bool isMouseInSideBar()
        {
            //return rect.Contains(Event.current.mousePosition);

            return false;
        }

        private Rect rect
        {
            get
            {
                return editor.inspectorContianer.contentRect;
            }
        }
        List<ViewPageItem> viewPageItemList;
        List<bool> nameEditLock = new List<bool>();
        List<bool> anchorPivotFoldout = new List<bool>();
        List<bool> rotationScaleFoldout = new List<bool>();
        Dictionary<string, int> transformEditStatus = new Dictionary<string, int>();

        public void SetCurrentSelectItem(ViewSystemNode currentSelectNode)
        {
            this.currentSelectNode = currentSelectNode;
            if (currentSelectNode == null)
            {
                return;
            }

            if (currentSelectNode is ViewPageNode)
            {
                var vp = ((ViewPageNode)currentSelectNode).viewPage;
                viewPageItemList = vp.viewPageItems;
            }
            if (currentSelectNode is ViewStateNode)
            {
                var vs = ((ViewStateNode)currentSelectNode).viewState;
                viewPageItemList = vs.viewPageItems;
            }

            //  editableLock.Clear();//
            nameEditLock.Clear();
            transformEditStatus.Clear();
            anchorPivotFoldout.Clear();
            rotationScaleFoldout.Clear();
            viewPageItemList.All(x =>
            {
                //    editableLock.Add(new EditableLockItem(true));//
                rotationScaleFoldout.Add(false);
                nameEditLock.Add(false);
                anchorPivotFoldout.Add(false);
                transformEditStatus.Add($"{x.Id}_default", string.IsNullOrEmpty(x.defaultTransformDatas.parentPath) ? 0 : 1);
                return true;
            });
            RebuildInspector();
        }
        public void Normalized()
        {
            SetCurrentSelectItem(null);
        }
        public void RebuildInspector()
        {
            viewPageItemReorderableList = null;
            viewPageItemReorderableList = new ReorderableList(viewPageItemList, typeof(List<ViewPageItem>), true, true, true, false);
            viewPageItemReorderableList.drawElementCallback += DrawViewItemElement;
            viewPageItemReorderableList.drawHeaderCallback += DrawViewItemHeader;
            viewPageItemReorderableList.elementHeight = EditorGUIUtility.singleLineHeight * 6f;
            viewPageItemReorderableList.onAddCallback += AddItem;
            viewPageItemReorderableList.drawElementBackgroundCallback += DrawItemBackground;
            viewPageItemReorderableList.elementHeightCallback += ElementHight;
            layouted = false;
        }

        private float ElementHight(int index)
        {
            var item = viewPageItemList[index];
            string key = $"{item.Id}_default";
            if (!transformEditStatus.ContainsKey(key))
            {
                transformEditStatus.Add(key, 0);
            }
            return transformEditStatus[key] == 0 ? GetHeight() : EditorGUIUtility.singleLineHeight * 7f;
            float GetHeight()
            {
                return EditorGUIUtility.singleLineHeight * 7.5f + (EditorGUIUtility.singleLineHeight * 2 + 6) + (EditorGUIUtility.singleLineHeight * 1 + 8);
            }
            // with folddot feature
            // float GetHeight()
            // {
            //     return EditorGUIUtility.singleLineHeight * 7.5f +
            //    (anchorPivotFoldout[index] ? EditorGUIUtility.singleLineHeight * 3 + 6 : 0) +
            //    (rotationScaleFoldout[index] ? EditorGUIUtility.singleLineHeight * 2 + 8 : 0);
            // }
        }
        int _currentShowOverrideItem = -1;

        int currentShowOverrideItem
        {
            get
            {
                if (editor.overridePopupWindow.show == false)
                {
                    return -1;
                }
                return _currentShowOverrideItem;
            }
            set
            {
                _currentShowOverrideItem = value;
            }
        }
        private void DrawItemBackground(Rect rect, int index, bool isActive, bool isFocused)
        {
            Rect oddRect = rect;
            oddRect.x -= 20;
            oddRect.width += 100;

            if (isFocused)
            {
                ReorderableList.defaultBehaviours.DrawElementBackground(rect, index, isActive, isFocused, true);
                return;
            }
            if (index == currentShowOverrideItem)
            {
                GUI.Box(oddRect, GUIContent.none, Drawer.overrideShowedStyle);
            }
            if (index % 2 == 0) GUI.Box(oddRect, GUIContent.none, Drawer.oddStyle);


        }
        private void RemoveItem(ReorderableList list)
        {
            if (EditorUtility.DisplayDialog("Remove", "Do you really want to remove this item", "Sure", "Not now"))
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
                RebuildInspector();
                return;
            }
        }
        private void AddItem(ReorderableList list)
        {
            // this is ugly but there are a lot of cases like null types and default constructors
            var elementType = list.list.GetType().GetElementType();
            if (elementType == typeof(string))
                list.index = list.list.Add("");
            else if (elementType != null && elementType.GetConstructor(Type.EmptyTypes) == null)
                Debug.LogError("Cannot add element. Type " + elementType + " has no default constructor. Implement a default constructor or implement your own add behaviour.");
            else if (list.list.GetType().GetGenericArguments()[0] != null)
                list.index = list.list.Add(Activator.CreateInstance(list.list.GetType().GetGenericArguments()[0]));
            else if (elementType != null)
                list.index = list.list.Add(Activator.CreateInstance(elementType));
            else
                Debug.LogError("Cannot add element of type Null.");

            transformEditStatus.Add($"{viewPageItemList.Last().Id}_default", 0);
            rotationScaleFoldout.Add(false);
            anchorPivotFoldout.Add(false);
            nameEditLock.Add(false);
        }

        private void DrawViewItemHeader(Rect rect)
        {
            float oriWidth = rect.width;
            rect.height = EditorGUIUtility.singleLineHeight;
            rect.x += 15;
            rect.width = 100;
            EditorGUI.LabelField(rect, "ViewPageItem");

            rect.width = 25;
            rect.x = oriWidth - 25;
        }

        const int rightBtnWidth = 0;
        ViewPageItem copyPasteBuffer;
        ViewElementOverridesImporterWindow overrideChecker;
        ViewPageItem CopyItem(bool copyOverride = true, bool copyEvent = true, bool copyRectTransform = true)
        {
            var copyResult = new ViewPageItem(copyPasteBuffer.viewElement);
            copyResult.TweenTime = copyPasteBuffer.TweenTime;
            copyResult.delayOut = copyPasteBuffer.delayOut;
            copyResult.delayIn = copyPasteBuffer.delayIn;
            copyResult.excludePlatform = copyPasteBuffer.excludePlatform;
            copyResult.name = copyPasteBuffer.name;

            if (copyRectTransform == true)
            {
                copyResult.defaultTransformDatas.rectTransformData = new ViewSystemRectTransformData();
                copyResult.defaultTransformDatas.rectTransformData.anchoredPosition = copyPasteBuffer.defaultTransformDatas.rectTransformData.anchoredPosition;
                copyResult.defaultTransformDatas.rectTransformData.anchorMax = copyPasteBuffer.defaultTransformDatas.rectTransformData.anchorMax;
                copyResult.defaultTransformDatas.rectTransformData.anchorMin = copyPasteBuffer.defaultTransformDatas.rectTransformData.anchorMin;
                copyResult.defaultTransformDatas.rectTransformData.pivot = copyPasteBuffer.defaultTransformDatas.rectTransformData.pivot;
                copyResult.defaultTransformDatas.rectTransformData.localEulerAngles = copyPasteBuffer.defaultTransformDatas.rectTransformData.localEulerAngles;
                copyResult.defaultTransformDatas.rectTransformData.localScale = copyPasteBuffer.defaultTransformDatas.rectTransformData.localScale;
                copyResult.defaultTransformDatas.rectTransformData.offsetMax = copyPasteBuffer.defaultTransformDatas.rectTransformData.offsetMax;
                copyResult.defaultTransformDatas.rectTransformData.offsetMin = copyPasteBuffer.defaultTransformDatas.rectTransformData.offsetMin;
                copyResult.defaultTransformDatas.rectTransformData.sizeDelta = copyPasteBuffer.defaultTransformDatas.rectTransformData.sizeDelta;
                copyResult.defaultTransformDatas.parentPath = copyPasteBuffer.defaultTransformDatas.parentPath;
            }

            if (copyOverride == true)
            {
                var originalOverrideDatas = copyPasteBuffer.overrideDatas.Select(x => x).ToList();
                var copiedOverrideDatas = originalOverrideDatas.Select(x => new ViewElementPropertyOverrideData
                {
                    targetComponentType = x.targetComponentType,
                    targetPropertyName = x.targetPropertyName,

                    targetTransformPath = x.targetTransformPath,
                    Value = new PropertyOverride
                    {
                        ObjectReferenceValue = x.Value.ObjectReferenceValue,
                        s_Type = x.Value.s_Type,
                        StringValue = x.Value.StringValue,
                    }
                }).ToList();
                copyResult.overrideDatas = copiedOverrideDatas;
            }

            if (copyEvent == true)
            {
                var originalEventDatas = copyPasteBuffer.eventDatas.Select(x => x).ToList();
                var copyEventDatas = originalEventDatas.Select(
                    x => new ViewElementEventData
                    {
                        targetComponentType = x.targetComponentType,
                        targetPropertyName = x.targetPropertyName,
                        targetTransformPath = x.targetTransformPath,
                        methodName = x.methodName,
                        scriptName = x.scriptName
                    })
                .ToList();

                copyResult.eventDatas = copyEventDatas;
            }

            return copyResult;
        }

        private void DrawViewItemElement(Rect rect, int index, bool isActive, bool isFocused)
        {

            if (index >= viewPageItemList.Count)
            {
                return;
            }

            var e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.button == 1 && e.type == EventType.MouseDown)
                {
                    GenericMenu genericMenu = new GenericMenu();
                    genericMenu.AddItem(new GUIContent("Copy"), false,
                        () =>
                        {
                            copyPasteBuffer = viewPageItemList[index];
                        }
                    );
                    if (copyPasteBuffer != null)
                    {
                        genericMenu.AddItem(new GUIContent("Paste (Default)"), false,
                            () =>
                            {
                                viewPageItemList[index] = CopyItem(false, false, true);
                                GUI.changed = true;
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Paste (with Property Data)"), false,
                            () =>
                            {
                                viewPageItemList[index] = CopyItem(true, false, true);
                                GUI.changed = true;
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Paste (with Events Data)"), false,
                           () =>
                           {
                               viewPageItemList[index] = CopyItem(false, true, true);
                               GUI.changed = true;
                           }
                        );
                        genericMenu.AddItem(new GUIContent("Paste (with All Data)"), false,
                           () =>
                           {
                               viewPageItemList[index] = CopyItem(true, true, true);
                               GUI.changed = true;
                           }
                       );
                    }
                    genericMenu.ShowAsContext();
                }
            }

            Rect oriRect = rect;

            rect.x = oriRect.x;
            rect.width = oriRect.width - rightBtnWidth;
            rect.height = EditorGUIUtility.singleLineHeight;

            rect.y += EditorGUIUtility.singleLineHeight * 0.25f;
            /*Name Part Start */
            var nameRect = rect;
            nameRect.width = rect.width - 20 * 5;
            GUIStyle nameRuntimeStyle;

            if (viewPageItemList.Where(m => m.name == viewPageItemList[index].name).Count() > 1 && !string.IsNullOrEmpty(viewPageItemList[index].name))
            {
                GUI.color = Color.red;
                nameRuntimeStyle = nameErrorStyle;
            }
            else
            {
                GUI.color = Color.white;
                nameRuntimeStyle = nameStyle;
            }

            if (!nameEditLock[index])
            {
                string showName;
                if (string.IsNullOrEmpty(viewPageItemList[index].name) == true)
                {
                    if (viewPageItemList[index].viewElement)
                    {
                        showName = $"{viewPageItemList[index].viewElement.name}";
                    }
                    else
                        showName = $"Unnamed";
                    GUI.Label(nameRect, showName, nameUnnamedStyle);
                }
                else
                {
                    showName = viewPageItemList[index].name;
                    GUI.Label(nameRect, new GUIContent(showName, showName), nameRuntimeStyle);
                }
            }
            else
            {
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    var undoCache = EditorGUI.TextField(nameRect, GUIContent.none, viewPageItemList[index].name);
                    if (change.changed)
                    {
                        Undo.RecordObject(saveData, "ViewSystem_Insptecor");
                    }
                    viewPageItemList[index].name = undoCache;
                }
            }
            if (e.isMouse && e.type == EventType.MouseDown && e.clickCount == 2 && nameRect.Contains(e.mousePosition))
            {
                nameEditLock[index] = !nameEditLock[index];
            }
            GUI.color = Color.white;

            /*Name Part End */

            /*Toggle Button Part Start */
            Rect rightRect = rect;
            rightRect.x = rect.width;
            rightRect.width = 20;
            if (GUI.Button(rightRect, EditoModifyButton, Drawer.removeButtonStyle))
            {
                if (viewPageItemList[index].viewElement == null)
                {
                    ViewSystemLog.LogError("ViewElement has not been select yet!");
                    return;
                }
                if (editor.overridePopupWindow.show == false || editor.overridePopupWindow.viewPageItem != viewPageItemList[index])
                {
                    rightRect.y += infoAreaRect.height + EditorGUIUtility.singleLineHeight * 4.5f;
                    editor.overridePopupWindow.SetViewPageItem(viewPageItemList[index]);
                    editor.overridePopupWindow.Show(rightRect);
                    currentShowOverrideItem = index;
                }
                else
                {
                    editor.overridePopupWindow.show = false;
                }
            }
            //Has override hint
            if (viewPageItemList[index].overrideDatas?.Count() > 0 ||
                   viewPageItemList[index].eventDatas?.Count() > 0 ||
                   viewPageItemList[index].navigationDatas?.Count() > 0)
            {
                GUI.Label(new Rect(rightRect.x, rightRect.y - 16, 24, 24), new GUIContent(Drawer.overrideIcon, "This item has override"));
            }

            rightRect.x -= 20;

            if (GUI.Button(rightRect, new GUIContent(Drawer.breakPointIcon, "BreakPoints Setting"), Drawer.removeButtonStyle))
            {
                if (!editor.breakpointWindow.show)
                {
                    editor.breakpointWindow.RebuildWindow(viewPageItemList[index]);
                    editor.breakpointWindow.Show();
                }
                else
                {
                    editor.breakpointWindow.show = false;
                }
            }

            //Has breakpoint hint
            if (viewPageItemList[index].breakPointViewElementTransforms?.Count() > 0)
            {
                GUI.Label(new Rect(rightRect.x, rightRect.y - 16, 24, 24), new GUIContent(Drawer.overrideIcon, "This item has BreakPoint setup"));
            }

            rightRect.x -= 20;
            if (GUI.Button(rightRect, new GUIContent(EditorGUIUtility.FindTexture("_Popup"), "More Setting"), Drawer.removeButtonStyle))
            {
                PopupWindow.Show(rect, new VS_EditorUtility.ViewPageItemDetailPopup(rect, viewPageItemList[index]));
            }

            rightRect.x -= 20;
            if (GUI.Button(rightRect, new GUIContent(EditorGUIUtility.FindTexture("UnityEditor.InspectorWindow"), "Open in new Instpector tab"), Drawer.removeButtonStyle))
            {
                if (viewPageItemList[index].viewElement == null)
                {
                    ViewSystemLog.LogError("ViewElement has not been select yet!");
                    return;
                }
                MacacaGames.CMEditorUtility.InspectTarget(viewPageItemList[index].viewElement.gameObject);
            }

            rightRect.x -= 20;
            nameEditLock[index] = EditorGUI.Toggle(rightRect, new GUIContent("", "Manual Name"), nameEditLock[index], nameEditStyle);
            /*Toggle Button Part End */


            rect.y += EditorGUIUtility.singleLineHeight;

            var viewElementRect = rect;
            viewElementRect.width = rect.width - 60;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                string oriViewElement = "";
                if (viewPageItemList[index].viewElement != null)
                {
                    oriViewElement = viewPageItemList[index].viewElement.name;
                }

                var lableWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 80;
                viewPageItemList[index].viewElementObject = (GameObject)EditorGUI.ObjectField(viewElementRect, "View Element", viewPageItemList[index].viewElementObject, typeof(GameObject), true);
                EditorGUIUtility.labelWidth = lableWidth;
                if (check.changed)
                {
                    if (viewPageItemList[index].viewElement == null)
                    {
                        ViewSystemLog.LogError("The setup item doesn't contain ViewElement");
                        viewPageItemList[index].viewElementObject = null;
                        return;
                    }
                    PrefabInstanceStatus prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(viewPageItemList[index].viewElementObject);
                    // PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(viewPageItemList[index].viewElementObject);
                    if (prefabInstanceStatus == PrefabInstanceStatus.Connected)
                    {
                        var cache = viewPageItemList[index].viewElement;
                        ViewElement original;
                        if (ViewSystemVisualEditor.overrideFromOrginal)
                        {
                            original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(cache);
                        }
                        else
                        {
                            original = PrefabUtility.GetCorrespondingObjectFromSource(cache);
                        }

                        overrideChecker = ScriptableObject.CreateInstance<ViewElementOverridesImporterWindow>();
                        var result = overrideChecker.SetData(cache.transform, original.transform, (import) =>
                        {
                            viewPageItemList[index].overrideDatas?.Clear();
                            viewPageItemList[index].overrideDatas = import.ToList();
                        }, currentSelectNode);
                        if (result) overrideChecker.ShowUtility();
                        viewPageItemList[index].viewElement = original;
                        viewPageItemList[index].previewViewElement = cache;

                        viewPageItemList[index].defaultTransformDatas.rectTransformData = new ViewSystemRectTransformData();
                        PickRectTransformValue(viewPageItemList[index].defaultTransformDatas, viewPageItemList[index].previewViewElement.GetComponent<RectTransform>());
                    }
                    else
                    {
                        //is prefabs
                        if (viewPageItemList[index].viewElement.gameObject.name != oriViewElement)
                        {
                            viewPageItemList[index].overrideDatas?.Clear();
                            viewPageItemList[index].eventDatas?.Clear();
                        }
                    }
                }
            }

            viewElementRect.x += viewElementRect.width;
            viewElementRect.width = 60;

            if (GUI.Button(viewElementRect, "Select"))
            {
                SelectCurrentViewElement(viewPageItemList[index].Id);
            }

            rect.y += EditorGUIUtility.singleLineHeight;


            //Draw RectTransformDetail
            DrawViewElementTransformDetail(viewPageItemList[index].defaultTransformDatas, $"{viewPageItemList[index].Id}", "default", viewPageItemList[index].previewViewElement, rect);

            rect.width = 20;
            rect.x -= 20;
            rect.y = oriRect.y += 26;
            viewPageItemList[index].sortingOrder = EditorGUI.IntField(rect, viewPageItemList[index].sortingOrder);

            rect.width = 24;
            rect.height = 24;
            rect.y = oriRect.y + oriRect.height - 52;
            if (GUI.Button(rect, new GUIContent(EditorGUIUtility.FindTexture("d_TreeEditor.Trash")), Drawer.removeButtonStyle))
            {
                viewPageItemReorderableList.index = index;
                RemoveItem(viewPageItemReorderableList);
            }
        }

        public void DrawViewElementTransformDetail(ViewElementTransform trasformData, string viewPageItemId, string breakPoint, ViewElement previewViewElement, Rect rect)
        {
            string key = $"{viewPageItemId}_{breakPoint}";
            if (!transformEditStatus.ContainsKey(key))
            {
                transformEditStatus.Add(key, 0);
            }
            transformEditStatus[key] = GUI.Toolbar(rect, transformEditStatus[key], new string[] { "RectTransfrom", "Custom Parent" });
            rect.y += EditorGUIUtility.singleLineHeight;
            RectTransform rectTransform = null;
            if (previewViewElement)
            {
                rectTransform = previewViewElement.GetComponent<RectTransform>();
            }

            switch (transformEditStatus[key])
            {
                case 0:
                    {
                        using (var change = new EditorGUI.ChangeCheckScope())
                        {
                            Rect smartPositionAndSizeRect = rect;
                            Rect layoutButtonRect = rect;
                            Rect anchorAndPivotRect = rect;
                            Rect foldoutRect = rect;
                            Rect previewBtnRect = rect;
                            float btnWidth = 80;
                            previewBtnRect.x = previewBtnRect.width - btnWidth + 20;
                            previewBtnRect.width = btnWidth;
                            previewBtnRect.height = EditorGUIUtility.singleLineHeight;
                            previewBtnRect.y += 18;
                            // if (GUI.Button(previewBtnRect, new GUIContent("Select", "Highlight and select ViewElement object")))
                            // {
                            //     SelectCurrentViewElement(viewPageItemId);
                            // }

                            layoutButtonRect.x -= 10;
                            layoutButtonRect.width = EditorGUIUtility.singleLineHeight * 2;
                            layoutButtonRect.height = EditorGUIUtility.singleLineHeight * 2;
                            LayoutDropdownButton(layoutButtonRect, trasformData.rectTransformData, rectTransform, false);
                            layoutButtonRect.y += EditorGUIUtility.singleLineHeight * 3;
                            layoutButtonRect.height = EditorGUIUtility.singleLineHeight;
                            using (var disable = new EditorGUI.DisabledGroupScope(!rectTransform))
                            {
                                if (GUI.Button(layoutButtonRect, new GUIContent("Pick", "Pick RectTransform value from preview ViewElement")))
                                {
                                    PickRectTransformValue(trasformData, rectTransform);
                                }
                            }

                            smartPositionAndSizeRect.height = EditorGUIUtility.singleLineHeight * 4;
                            var modifyResult = SmartPositionAndSizeFields(smartPositionAndSizeRect, trasformData.rectTransformData, trasformData.rectTransformFlag);
                            smartPositionAndSizeRect.y += EditorGUIUtility.singleLineHeight;
                            var flagRect = GetColumnRect(smartPositionAndSizeRect, 2);
                            trasformData.rectTransformFlag = (ViewElement.RectTransformFlag)EditorGUI.EnumFlagsField(flagRect, trasformData.rectTransformFlag);
                            anchorAndPivotRect.height = EditorGUIUtility.singleLineHeight;
                            anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight * 2;
                            anchorAndPivotRect.x += 30;

                            Vector2Field(anchorAndPivotRect,
                            new GUIContent("Anchor Min"),
                            trasformData.rectTransformData.anchorMin,
                            (v) => { trasformData.rectTransformData.anchorMin = v; },
                            FlagsHelper.IsSet(trasformData.rectTransformFlag, ViewElement.RectTransformFlag.AnchorMin));

                            anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight + 2;
                            Vector2Field(anchorAndPivotRect,
                            new GUIContent("Anchor Max"),
                            trasformData.rectTransformData.anchorMax,
                            (v) => { trasformData.rectTransformData.anchorMax = v; },
                            FlagsHelper.IsSet(trasformData.rectTransformFlag, ViewElement.RectTransformFlag.AnchorMax));

                            anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight + 2;
                            Vector2Field(anchorAndPivotRect,
                            new GUIContent("Pivot"),
                            trasformData.rectTransformData.pivot,
                            (v) => { trasformData.rectTransformData.pivot = v; },
                            FlagsHelper.IsSet(trasformData.rectTransformFlag, ViewElement.RectTransformFlag.Pivot));

                            anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight + 2;

                            // anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight;
                            Vector3Field(anchorAndPivotRect,
                            new GUIContent("Rotation"),
                            trasformData.rectTransformData.localEulerAngles,
                            (v) => { trasformData.rectTransformData.localEulerAngles = v; },
                            FlagsHelper.IsSet(trasformData.rectTransformFlag, ViewElement.RectTransformFlag.LocalEulerAngles));

                            anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight + 2;
                            Vector3Field(anchorAndPivotRect,
                            new GUIContent("Scale"),
                            trasformData.rectTransformData.localScale,
                            (v) => { trasformData.rectTransformData.localScale = v; },
                            FlagsHelper.IsSet(trasformData.rectTransformFlag, ViewElement.RectTransformFlag.LocalScale));

                            if (change.changed)
                            {
                                if (previewViewElement)
                                {
                                    previewViewElement.ApplyRectTransform(trasformData);
                                    // if (modifyResult)
                                    // {
                                    //     previewViewElement.ApplyOffectMax(trasformData);
                                    //     previewViewElement.ApplyOffectMin(trasformData);
                                    // }
                                }
                            }
                        }
                    }
                    break;
                case 1:
                    {
                        //Transform No found hint
                        if (!string.IsNullOrEmpty(trasformData.parentPath))
                        {
                            var target = GameObject.Find(saveData.globalSetting.ViewControllerObjectPath + "/" + trasformData.parentPath);
                            if (target == null)
                            {
                                GUI.Label(new Rect(rect.x - 24, rect.y, 24, 24), new GUIContent(Drawer.miniErrorIcon, "Transform cannot found in this item."));
                            }
                        }

                        if (string.IsNullOrEmpty(trasformData.parentPath))
                        {
                            trasformData.parent = (Transform)EditorGUI.ObjectField(rect, "Drag to here", trasformData.parent, typeof(Transform), true);
                            rect.y += EditorGUIUtility.singleLineHeight;
                        }
                        else
                        {

                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                trasformData.parentPath = EditorGUI.TextField(rect, new GUIContent("Parent", trasformData.parentPath), trasformData.parentPath);
                                if (check.changed)
                                {
                                    if (!string.IsNullOrEmpty(trasformData.parentPath))
                                    {
                                        var target = GameObject.Find(saveData.globalSetting.ViewControllerObjectPath + "/" + trasformData.parentPath);
                                        if (target)
                                        {
                                            trasformData.parent = target.transform;
                                        }
                                    }
                                    else
                                        trasformData.parent = null;
                                }
                            }
                            rect.y += EditorGUIUtility.singleLineHeight;
                            string shortPath = "";
                            if (!string.IsNullOrEmpty(trasformData.parentPath))
                            {
                                shortPath = trasformData.parentPath.Split('/').Last();
                                using (var disable = new EditorGUI.DisabledGroupScope(true))
                                {
                                    EditorGUI.TextField(rect, new GUIContent("Shor Path", trasformData.parentPath), shortPath);
                                }
                            }
                        }

                        rect.y += EditorGUIUtility.singleLineHeight;

                        var parentFunctionRect = rect;
                        parentFunctionRect.width = rect.width * 0.49f;
                        parentFunctionRect.x += rect.width * 0.01f;
                        if (GUI.Button(parentFunctionRect, new GUIContent("Pick", "Pick Current Select Transform")))
                        {
                            var item = Selection.transforms;
                            if (item.Length == 1)
                            {
                                trasformData.parent = item.First();
                            }
                        }

                        parentFunctionRect.x += parentFunctionRect.width + rect.width * 0.01f;
                        if (GUI.Button(parentFunctionRect, new GUIContent("Parent", "Highlight parent Transform object")))
                        {
                            var go = GameObject.Find(trasformData.parentPath);
                            if (go)
                            {
                                EditorGUIUtility.PingObject(go);
                                Selection.objects = new[] { go };
                            }
                            else ViewSystemLog.LogError("Target parent is not found, or the target parent is inactive.");
                        }
                        parentFunctionRect.x += parentFunctionRect.width + rect.width * 0.01f;

                        // if (GUI.Button(parentFunctionRect, new GUIContent("Select", "Highlight and select ViewElement object")))
                        // {
                        //     SelectCurrentViewElement(viewPageItemId);
                        // }

                        if (trasformData.parent != null)
                        {
                            var path = AnimationUtility.CalculateTransformPath(trasformData.parent, null);
                            var sp = path.Split('/');
                            if (sp.FirstOrDefault() == editor.ViewControllerRoot?.name)
                            {
                                trasformData.parentPath = path.Substring(sp.FirstOrDefault().Length + 1);
                            }
                            else
                            {
                                ViewSystemLog.LogError("Selected Parent is not child of ViewController GameObject");
                                ViewSystemLog.LogError("Selected Parent is not child of ViewController GameObject");
                                trasformData.parent = null;
                            }
                        }
                    }
                    break;
            }
        }
        void SelectCurrentViewElement(string viewPageItemId)
        {
            var item = viewPageItemList.SingleOrDefault(m => m.Id == viewPageItemId);
            if (item != null)
            {
                SelectCurrentViewElement(item);
            }
        }
        void SelectCurrentViewElement(ViewPageItem vpi)
        {
            if (Application.isPlaying && vpi.runtimeViewElement)
            {
                EditorGUIUtility.PingObject(vpi.runtimeViewElement);
                Selection.objects = new[] { vpi.runtimeViewElement.gameObject };
            }
            else if (vpi.previewViewElement)
            {
                EditorGUIUtility.PingObject(vpi.previewViewElement);
                Selection.objects = new[] { vpi.previewViewElement.gameObject };
            }
            else ViewSystemLog.LogError("Target parent is not found, or the target parent is inactive.");
        }

        void PickRectTransformValue(ViewElementTransform targetData, RectTransform sourceRectTransform)
        {
            if (sourceRectTransform)
            {
                targetData.rectTransformData.anchoredPosition = sourceRectTransform.anchoredPosition3D;
                targetData.rectTransformData.anchorMax = sourceRectTransform.anchorMax;
                targetData.rectTransformData.anchorMin = sourceRectTransform.anchorMin;
                targetData.rectTransformData.offsetMax = sourceRectTransform.offsetMax;
                targetData.rectTransformData.offsetMin = sourceRectTransform.offsetMin;
                targetData.rectTransformData.pivot = sourceRectTransform.pivot;
                targetData.rectTransformData.localScale = sourceRectTransform.localScale;
                targetData.rectTransformData.sizeDelta = sourceRectTransform.sizeDelta;
                targetData.rectTransformData.localEulerAngles = sourceRectTransform.localEulerAngles;
            }
        }
        static float InspectorWidth = 350;
        Rect infoAreaRect;
        public Vector2 scrollerPos;
        bool layouted = false;
        int tabs = 0;
        public void Draw()
        {
            GUILayout.BeginArea(rect, "");

            if (Event.current.type == EventType.Layout && layouted == false)
            {
                layouted = true;
            }

            if (currentSelectNode != null && layouted)
            {

                if (currentSelectNode.nodeType == ViewStateNode.NodeType.FullPage || currentSelectNode.nodeType == ViewStateNode.NodeType.Overlay)
                {
                    DrawViewPageDetail(((ViewPageNode)currentSelectNode));
                }
                if (currentSelectNode.nodeType == ViewStateNode.NodeType.ViewState)
                {
                    DrawViewStateDetail(((ViewStateNode)currentSelectNode));
                }

                infoAreaRect = GUILayoutUtility.GetLastRect();
                GUILayout.Label("", GUILayout.Height(5));
                tabs = GUILayout.Toolbar(tabs, new string[] { "ViewPageItems", "Components" });
                // showViewPageItem.target = EditorGUILayout.Foldout(showViewPageItem.target, "ViewPageItems");
                using (var scroll = new EditorGUILayout.ScrollViewScope(scrollerPos))
                {
                    scrollerPos = scroll.scrollPosition;
                    switch (tabs)
                    {
                        case 0:
                            if (viewPageItemReorderableList != null) viewPageItemReorderableList.DoLayoutList();
                            break;
                        case 1:
                            if (currentSelectNode.nodeType == ViewStateNode.NodeType.FullPage || currentSelectNode.nodeType == ViewStateNode.NodeType.Overlay)
                            {
                                var vp = ((ViewPageNode)currentSelectNode).viewPage;

                                //Canvas
                                var _canvasContent = EditorGUIUtility.ObjectContent(null, typeof(Canvas));
                                _canvasContent.text = "Canvas";
                                GUILayout.Label(_canvasContent, new GUIStyle("TE toolbarbutton"), GUILayout.Height(EditorGUIUtility.singleLineHeight));

                                using (var change = new EditorGUI.ChangeCheckScope())
                                {
                                    var temp = EditorGUILayout.IntField("sortingOrder", vp.canvasSortOrder);
                                    if (change.changed)
                                    {
                                        Undo.RecordObject(saveData, "ViewSystem_Inspector");
                                    }
                                    vp.canvasSortOrder = temp;
                                }
                                GUILayout.Label("", new GUIStyle("ToolbarSlider"));

                                //SafePadding
                                GUILayout.Label("Safe Padding", new GUIStyle("TE toolbarbutton"), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                                vp.useGlobalSafePadding = EditorGUILayout.Toggle("Use Global Safe Padding?", vp.useGlobalSafePadding);
                                using (var disable = new EditorGUI.DisabledGroupScope(vp.useGlobalSafePadding))
                                {
                                    var contents = new string[] { "Off", "On", };
                                    using (var change = new EditorGUI.ChangeCheckScope())
                                    {

                                        using (var horizon = new GUILayout.HorizontalScope())
                                        {
                                            EditorGUILayout.PrefixLabel("Left");
                                            vp.edgeValues.left = (SafePadding.EdgeEvaluationMode)GUILayout.Toolbar((int)vp.edgeValues.left, contents, EditorStyles.miniButton, GUI.ToolbarButtonSize.Fixed);
                                        }

                                        using (var horizon = new GUILayout.HorizontalScope())
                                        {
                                            EditorGUILayout.PrefixLabel("Bottom");
                                            vp.edgeValues.bottom = (SafePadding.EdgeEvaluationMode)GUILayout.Toolbar((int)vp.edgeValues.bottom, contents, EditorStyles.miniButton, GUI.ToolbarButtonSize.Fixed);
                                        }
                                        using (var horizon = new GUILayout.HorizontalScope())
                                        {
                                            EditorGUILayout.PrefixLabel("Top");
                                            vp.edgeValues.top = (SafePadding.EdgeEvaluationMode)GUILayout.Toolbar((int)vp.edgeValues.top, contents, EditorStyles.miniButton, GUI.ToolbarButtonSize.Fixed);
                                        }
                                        using (var horizon = new GUILayout.HorizontalScope())
                                        {
                                            EditorGUILayout.PrefixLabel("Right");
                                            vp.edgeValues.right = (SafePadding.EdgeEvaluationMode)GUILayout.Toolbar((int)vp.edgeValues.right, contents, EditorStyles.miniButton, GUI.ToolbarButtonSize.Fixed);
                                        }

                                        vp.edgeValues.influence = EditorGUILayout.Slider("Influence", vp.edgeValues.influence, 0, 1);
                                        vp.edgeValues.influenceLeft = EditorGUILayout.Slider("Influence Left", vp.edgeValues.influenceLeft, 0, 1);
                                        vp.edgeValues.influenceBottom = EditorGUILayout.Slider("Influence Bottom", vp.edgeValues.influenceBottom, 0, 1);
                                        vp.edgeValues.influenceTop = EditorGUILayout.Slider("Influence Top", vp.edgeValues.influenceTop, 0, 1);
                                        vp.edgeValues.influenceRight = EditorGUILayout.Slider("Influence Right", vp.edgeValues.influenceRight, 0, 1);
                                        vp.flipPadding = EditorGUILayout.Toggle("Flip Padding", vp.flipPadding);
                                        if (change.changed && ViewSystemVisualEditor.Instance.EditMode)
                                        {
                                            ViewSystemVisualEditor.ApplySafeArea(vp.edgeValues);
                                            Undo.RecordObject(saveData, "ViewSystem_Inspector");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                GUILayout.Label("ViewState doesn't support components setting");
                            }
                            break;
                    }
                }
            }
            else
            {
                GUILayout.Label("Nothing selected :)");
            }

            GUILayout.EndArea();
        }
        //source from https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/RectTransformEditor.cs
        private delegate float FloatGetter(ViewSystemRectTransformData rect);
        private delegate void FloatSetter(ViewSystemRectTransformData rect, float f);
        private delegate void Vector2Setter(Vector2 v);
        private delegate void Vector3Setter(Vector3 v);
        private static GUIContent[] s_XYLabels = { new GUIContent("X"), new GUIContent("Y") };
        private static GUIContent[] s_XYZLabels = { new GUIContent("X"), new GUIContent("Y"), new GUIContent("Z") };
        Rect GetColumnRect(Rect totalRect, int column)
        {
            totalRect.xMin += 30;
            Rect _rect = totalRect;
            _rect.xMin += (totalRect.width - 4) * (column / 3f) + column * 2;
            _rect.width = (totalRect.width - 4) / 3f;
            return _rect;
        }

        void FloatFieldLabelAbove(Rect position, FloatGetter getter, FloatSetter setter, ViewSystemRectTransformData _rectTransform, bool enable, GUIContent label)
        {
            float lableWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 45;

            float value = getter(_rectTransform);
            using (var disable = new EditorGUI.DisabledGroupScope(!enable))
            {
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    Rect positionLabel = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                    float newValue = EditorGUI.FloatField(positionLabel, label, value, EditorStyles.miniTextField);
                    if (change.changed)
                    {
                        setter(_rectTransform, newValue);
                    }
                }
            }
            EditorGUIUtility.labelWidth = lableWidth;
        }

        void Vector2Field(Rect position, GUIContent label, Vector2 value, Vector2Setter setter, bool enable)
        {
            bool widthMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            float lableWidth = EditorGUIUtility.labelWidth;
            float fieldWidth = EditorGUIUtility.fieldWidth;
            EditorGUIUtility.labelWidth = 75;
            EditorGUIUtility.fieldWidth = 40;
            using (var disable = new EditorGUI.DisabledGroupScope(!enable))
            {
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    Rect positionLabel = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                    Vector2 newValue = EditorGUI.Vector2Field(positionLabel, label, value);
                    if (change.changed)
                    {
                        Undo.RecordObject(saveData, "ViewSystem_Indpector");
                        newValue.x = Mathf.Clamp01(newValue.x);
                        newValue.y = Mathf.Clamp01(newValue.y);
                        setter(newValue);
                    }
                }
            }
            EditorGUIUtility.fieldWidth = fieldWidth;
            EditorGUIUtility.labelWidth = lableWidth;
        }

        void Vector3Field(Rect position, GUIContent label, Vector3 value, Vector3Setter setter, bool enable)
        {
            bool widthMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            float lableWidth = EditorGUIUtility.labelWidth;
            float fieldWidth = EditorGUIUtility.fieldWidth;
            EditorGUIUtility.labelWidth = 75;
            EditorGUIUtility.fieldWidth = 40;
            using (var disable = new EditorGUI.DisabledGroupScope(!enable))
            {
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    Rect positionLabel = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                    Vector3 newValue = EditorGUI.Vector3Field(positionLabel, label, value);
                    if (change.changed)
                    {
                        Undo.RecordObject(saveData, "ViewSystem_Indpector");
                        setter(newValue);
                    }
                }
            }
            EditorGUIUtility.fieldWidth = fieldWidth;
            EditorGUIUtility.labelWidth = lableWidth;
        }

        void LayoutDropdownButton(Rect dropdownPosition, ViewSystemRectTransformData rectTransformData, RectTransform rectTransform, bool anyWithoutParent)
        {
            dropdownPosition.x += 2;
            dropdownPosition.y += 17;

            Color oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.6f) * oldColor;
            if (EditorGUI.DropdownButton(dropdownPosition, GUIContent.none, FocusType.Passive, "box"))
            {
                GUIUtility.keyboardControl = 0;
                LayoutDropdownWindow m_DropdownWindow = new LayoutDropdownWindow(rectTransformData, rectTransform, saveData);
                PopupWindow.Show(dropdownPosition, m_DropdownWindow);
            }
            GUI.color = oldColor;

            LayoutDropdownWindow.DrawLayoutMode(new RectOffset(7, 7, 7, 7).Remove(dropdownPosition), rectTransformData.anchorMin, rectTransformData.anchorMax, rectTransformData.anchoredPosition, rectTransformData.sizeDelta);
            LayoutDropdownWindow.DrawLayoutModeHeadersOutsideRect(dropdownPosition, rectTransformData.anchorMin, rectTransformData.anchorMax, rectTransformData.anchoredPosition, rectTransformData.sizeDelta);
        }

        bool SmartPositionAndSizeFields(Rect rect, ViewSystemRectTransformData rectTransformData, ViewElement.RectTransformFlag currentFlags)
        {
            rect.height = EditorGUIUtility.singleLineHeight * 2;
            Rect rect2;
            bool resutl = false;
            bool anyDrivenX = false;
            bool anyDrivenY = false;
            bool anyWithoutParent = false;
            bool anyStretchX = rectTransformData.anchorMin.x != rectTransformData.anchorMax.x;
            bool anyStretchY = rectTransformData.anchorMin.y != rectTransformData.anchorMax.y;
            bool anyNonStretchX = rectTransformData.anchorMin.x == rectTransformData.anchorMax.x;
            bool anyNonStretchY = rectTransformData.anchorMin.y == rectTransformData.anchorMax.y;

            rect2 = GetColumnRect(rect, 0);
            if (anyNonStretchX || anyWithoutParent || anyDrivenX)
            {
                //EditorGUI.BeginProperty(rect2, null, m_AnchoredPosition.FindPropertyRelative("x"));
                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.anchoredPosition.x,
                    (rectTransform, val) => rectTransform.anchoredPosition = new Vector2(val, rectTransform.anchoredPosition.y),
                    rectTransformData,
                    FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.AnchoredPosition),
                    EditorGUIUtility.TrTextContent("Pos X"));
            }
            else
            {
                // Affected by both anchored position and size delta so do property handling for both. (E.g. showing animated value, prefab override etc.)
                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.offsetMin.x,
                    (rectTransform, val) => rectTransform.offsetMin = new Vector2(val, rectTransform.offsetMin.y),
                    rectTransformData,
                    FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.SizeDelta),
                    EditorGUIUtility.TrTextContent("Left"));
                resutl = true;
            }

            rect2 = GetColumnRect(rect, 1);
            if (anyNonStretchY || anyWithoutParent || anyDrivenY)
            {
                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.anchoredPosition.y,
                    (rectTransform, val) => rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, val),
                    rectTransformData,
                    FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.AnchoredPosition),
                    EditorGUIUtility.TrTextContent("Pos Y"));
            }
            else
            {

                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => -rectTransform.offsetMax.y,
                    (rectTransform, val) => rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, -val),
                    rectTransformData,
                    FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.SizeDelta),
                    EditorGUIUtility.TrTextContent("Top"));
                resutl = true;
            }

            rect2 = GetColumnRect(rect, 2);

            FloatFieldLabelAbove(
                rect2,
                rectTransform => rectTransform.anchoredPosition.z,
                (rectTransform, val) => rectTransform.anchoredPosition = new Vector3(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y, val),
                rectTransformData,
                FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.AnchoredPosition),
                EditorGUIUtility.TrTextContent("Pos Z"));
            rect.y += EditorGUIUtility.singleLineHeight;

            rect2 = GetColumnRect(rect, 0);
            if (anyNonStretchX || anyWithoutParent || anyDrivenX)
            {

                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.sizeDelta.x,
                    (rectTransform, val) => rectTransform.sizeDelta = new Vector2(val, rectTransform.sizeDelta.y),
                    rectTransformData,
                    FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.SizeDelta),
                    anyStretchX ? EditorGUIUtility.TrTextContent("W Delta") : EditorGUIUtility.TrTextContent("Width"));
            }
            else
            {

                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => -rectTransform.offsetMax.x,
                    (rectTransform, val) => rectTransform.offsetMax = new Vector2(-val, rectTransform.offsetMax.y),
                    rectTransformData,
                    FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.SizeDelta),
                    EditorGUIUtility.TrTextContent("Right"));
                resutl = true;
            }

            rect2 = GetColumnRect(rect, 1);
            if (anyNonStretchY || anyWithoutParent || anyDrivenY)
            {

                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.sizeDelta.y,
                    (rectTransform, val) => rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, val),
                    rectTransformData,
                    FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.SizeDelta),
                    anyStretchY ? EditorGUIUtility.TrTextContent("H Delta") : EditorGUIUtility.TrTextContent("Height"));
            }
            else
            {

                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.offsetMin.y,
                    (rectTransform, val) => rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, val),
                    rectTransformData,
                    FlagsHelper.IsSet(currentFlags, ViewElement.RectTransformFlag.SizeDelta),
                    EditorGUIUtility.TrTextContent("Bottom"));
                resutl = true;
            }

            rect2 = rect;
            rect2.height = EditorGUIUtility.singleLineHeight;
            rect2.y += EditorGUIUtility.singleLineHeight;
            rect2.yMin -= 2;
            rect2.xMin = rect2.xMax - 26;
            return resutl;
        }

        Rect ResizeBarRect;
        bool resizeBarPressed = false;
        void DrawResizeBar()
        {
            ResizeBarRect = new Rect(rect.x + InspectorWidth, rect.y, 4, rect.height);
            EditorGUIUtility.AddCursorRect(ResizeBarRect, MouseCursor.ResizeHorizontal);

            GUI.Box(ResizeBarRect, "");
            if (ResizeBarRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    resizeBarPressed = true;
                }
            }
            if (Event.current.type == EventType.MouseUp)
            {
                resizeBarPressed = false;
            }
            if (resizeBarPressed && Event.current.type == EventType.MouseDrag)
            {
                InspectorWidth += Event.current.delta.x;
                InspectorWidth = Mathf.Clamp(InspectorWidth, 270, 450);
                Event.current.Use();
                GUI.changed = true;
            }
        }

        static List<string> excludePlatformOptions = new List<string>();
        int currentSelect;

        void DrawViewPageDetail(ViewPageNode viewPageNode)
        {
            var vp = viewPageNode.viewPage;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20), " ViewPage", new GUIStyle("EyeDropperHorizontalLine"));
            using (var disable = new EditorGUI.DisabledGroupScope(false))
            {
                if (GUI.Button(new Rect(rect.width - 25, rect.y, 25, 25), new GUIContent(EditorGUIUtility.IconContent("AnimatorStateMachine Icon").image, "Navigation"), Drawer.removeButtonStyle))
                {
                    ViewState vs = null;

                    if (!string.IsNullOrEmpty(vp.viewState)) vs = saveData.viewStates.SingleOrDefault(m => m.viewState.name == vp.viewState).viewState;
                    editor.navigationWindow.SetViewPage(vp, vs);
                    editor.navigationWindow.Show();
                }
            }
            using (var vertial = new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label(string.IsNullOrEmpty(vp.name) ? "Unnamed" : vp.name, new GUIStyle("DefaultCenteredLargeText"));
                showBasicInfo.target = EditorGUILayout.Foldout(showBasicInfo.target, "Basic Info");
                using (var fade = new EditorGUILayout.FadeGroupScope(showBasicInfo.faded))
                {
                    if (fade.visible)
                    {
                        using (var vertial_in = new EditorGUILayout.VerticalScope())
                        {
                            vp.name = EditorGUILayout.TextField("Name", vp.name);
                            using (var disable = new EditorGUI.DisabledGroupScope(vp.viewPageType == ViewPage.ViewPageType.Overlay))
                            {
                                vp.viewPageTransitionTimingType = (ViewPage.ViewPageTransitionTimingType)EditorGUILayout.EnumPopup("ViewPageTransitionTimingType", vp.viewPageTransitionTimingType);
                            }
                            using (var disable = new EditorGUI.DisabledGroupScope(vp.viewPageTransitionTimingType != ViewPage.ViewPageTransitionTimingType.Custom))
                            {
                                vp.customPageTransitionWaitTime = EditorGUILayout.FloatField("CustomPageTransitionWaitTime", vp.customPageTransitionWaitTime);
                            }

                            using (var disable = new EditorGUI.DisabledGroupScope(true))
                            {
                                EditorGUILayout.IntField("TargetFrameRate", -1);
                            }
                            vp.IsNavigation = EditorGUILayout.Toggle("Use Navigation", vp.IsNavigation);
                        }
                    }
                }

            }
        }

        void DrawViewStateDetail(ViewStateNode viewStateNode)
        {

            var vs = viewStateNode.viewState;
            using (var disable = new EditorGUI.DisabledGroupScope(false))
            {
                GUI.Button(new Rect(rect.width - 25, rect.y - 20, 25, 25), GUIContent.none, GUIStyle.none);
            }
            GUI.Label(new Rect(rect.x, rect.y - 20, rect.width, 20), " ViewState", new GUIStyle("EyeDropperHorizontalLine"));
            using (var vertial = new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label(string.IsNullOrEmpty(vs.name) ? "Unnamed" : vs.name, new GUIStyle("DefaultCenteredLargeText"));

                showBasicInfo.target = EditorGUILayout.Foldout(showBasicInfo.target, "Basic Info");
                using (var fade = new EditorGUILayout.FadeGroupScope(showBasicInfo.faded))
                {
                    if (fade.visible)
                    {
                        using (var vertical = new EditorGUILayout.VerticalScope(GUILayout.Height(70)))
                        {
                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                vs.name = EditorGUILayout.TextField("Name", vs.name);
                                if (check.changed)
                                {
                                    viewStateNode.currentLinkedViewPageNode.All(
                                        m =>
                                        {
                                            m.viewPage.viewState = vs.name;
                                            return true;
                                        }
                                    );
                                }
                            }
                            using (var disable = new EditorGUI.DisabledGroupScope(true))
                            {
                                EditorGUILayout.EnumPopup("ViewPageTransitionTimingType", ViewPage.ViewPageTransitionTimingType.AfterPervious);
                                EditorGUILayout.FloatField("CustomPageTransitionWaitTime", 0);
                            }
                            vs.targetFrameRate = EditorGUILayout.IntField("TargetFrameRate", vs.targetFrameRate);
                            using (var disable = new EditorGUI.DisabledGroupScope(true))
                            {
                                EditorGUILayout.Toggle("Use Navigation", false);
                            }
                        }
                    }
                }
            }
        }

       
    }
}