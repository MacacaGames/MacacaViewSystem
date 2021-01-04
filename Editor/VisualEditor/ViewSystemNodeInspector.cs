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
        ViewSystemNodeEditor editor;
        public bool show = true;
        AnimBool showBasicInfo;
        // AnimBool showViewPageItem;
        ReorderableList viewPageItemReorderableList;
        GUIStyle nameStyle;
        GUIStyle nameUnnamedStyle;
        GUIStyle nameErrorStyle;
        GUIStyle nameEditStyle;
        static ViewSystemSaveData saveData => ViewSystemNodeEditor.saveData;
        static GUIContent EditoModifyButton = new GUIContent(Drawer.overridePopupIcon, "Show/Hide Modified Properties and Events");

        public ViewSystemNodeInspector(ViewSystemNodeEditor editor)
        {
            this.editor = editor;

            show = true;
            showBasicInfo = new AnimBool(true);
            showBasicInfo.valueChanged.AddListener(this.editor.Repaint);

            nameStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.black }
            };
            nameUnnamedStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.BoldAndItalic,
                normal = { textColor = Color.gray }
            };
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
                onNormal = { background = EditorGUIUtility.FindTexture("d_editicon.sml") },
                normal = { background = EditorGUIUtility.FindTexture("d_FilterSelectedOnly") }
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
            return transformEditStatus[$"{item.Id}_default"] == 0 ? GetHeight() : EditorGUIUtility.singleLineHeight * 7f;
            float GetHeight()
            {
                return EditorGUIUtility.singleLineHeight * 7.5f +
               (anchorPivotFoldout[index] ? EditorGUIUtility.singleLineHeight * 3 + 6 : 0) +
               (rotationScaleFoldout[index] ? EditorGUIUtility.singleLineHeight * 2 + 8 : 0);
            }
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
            // copyResult.TweenTime = copyPasteBuffer.TweenTime;
            // copyResult.delayOut = copyPasteBuffer.delayOut;
            // copyResult.delayIn = copyPasteBuffer.delayIn;
            // copyResult.parentPath = copyPasteBuffer.parentPath;
            // copyResult.parent = copyPasteBuffer.parent;
            // copyResult.excludePlatform = copyPasteBuffer.excludePlatform;
            // copyResult.name = copyPasteBuffer.name;

            // if (copyRectTransform == true)
            // {
            //     copyResult.defaultTransformDatas.rectTransformData = new ViewSystemRectTransformData();
            //     copyResult.defaultTransformDatas.rectTransformData.anchoredPosition = copyPasteBuffer.transformData.anchoredPosition;
            //     copyResult.transformData.anchorMax = copyPasteBuffer.transformData.anchorMax;
            //     copyResult.transformData.anchorMin = copyPasteBuffer.transformData.anchorMin;
            //     copyResult.transformData.pivot = copyPasteBuffer.transformData.pivot;
            //     copyResult.transformData.localEulerAngles = copyPasteBuffer.transformData.localEulerAngles;
            //     copyResult.transformData.localScale = copyPasteBuffer.transformData.localScale;
            //     copyResult.transformData.offsetMax = copyPasteBuffer.transformData.offsetMax;
            //     copyResult.transformData.offsetMin = copyPasteBuffer.transformData.offsetMin;
            //     copyResult.transformData.sizeDelta = copyPasteBuffer.transformData.sizeDelta;
            //     copyResult.parentPath = "";
            // }

            // if (copyOverride == true)
            // {
            //     var originalOverrideDatas = copyPasteBuffer.overrideDatas.Select(x => x).ToList();
            //     var copiedOverrideDatas = originalOverrideDatas.Select(x => new ViewElementPropertyOverrideData
            //     {
            //         targetComponentType = x.targetComponentType,
            //         targetPropertyName = x.targetPropertyName,

            //         targetTransformPath = x.targetTransformPath,
            //         Value = new PropertyOverride
            //         {
            //             ObjectReferenceValue = x.Value.ObjectReferenceValue,
            //             s_Type = x.Value.s_Type,
            //             StringValue = x.Value.StringValue,
            //         }
            //     }).ToList();
            //     copyResult.overrideDatas = copiedOverrideDatas;
            // }

            // if (copyEvent == true)
            // {
            //     var originalEventDatas = copyPasteBuffer.eventDatas.Select(x => x).ToList();
            //     var copyEventDatas = originalEventDatas.Select(
            //         x => new ViewElementEventData
            //         {
            //             targetComponentType = x.targetComponentType,
            //             targetPropertyName = x.targetPropertyName,
            //             targetTransformPath = x.targetTransformPath,
            //             methodName = x.methodName,
            //             scriptName = x.scriptName
            //         })
            //     .ToList();

            //     copyResult.eventDatas = copyEventDatas;
            // }

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
            // EditorGUIUtility.labelWidth = 80.0f;

            Rect oriRect = rect;

            rect.x = oriRect.x;
            rect.width = oriRect.width - rightBtnWidth;
            rect.height = EditorGUIUtility.singleLineHeight;

            rect.y += EditorGUIUtility.singleLineHeight * 0.25f;
            /*Name Part Start */
            var nameRect = rect;
            //nameRect.height += EditorGUIUtility.singleLineHeight * 0.25f;
            nameRect.width = rect.width - 80;
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
                    GUI.Label(nameRect, showName, nameRuntimeStyle);
                }
            }
            else
            {
                viewPageItemList[index].name = EditorGUI.TextField(nameRect, GUIContent.none, viewPageItemList[index].name);
            }
            if (e.isMouse && e.type == EventType.MouseDown && e.clickCount == 2 && nameRect.Contains(e.mousePosition))
            {
                nameEditLock[index] = !nameEditLock[index];
            }
            nameRect.x += nameRect.width;
            nameRect.width = 16;
            nameEditLock[index] = EditorGUI.Toggle(nameRect, new GUIContent("", "Manual Name"), nameEditLock[index], nameEditStyle);

            GUI.color = Color.white;

            /*Name Part End */

            /*Toggle Button Part Start */
            Rect rightRect = rect;

            rightRect.x = rect.width;
            rightRect.width = 20;
            if (GUI.Button(rightRect, new GUIContent(EditorGUIUtility.FindTexture("_Popup"), "More Setting"), Drawer.removeButtonStyle))
            {
                PopupWindow.Show(rect, new VS_EditorUtility.ViewPageItemDetailPopup(rect, viewPageItemList[index]));
            }

            rightRect.x -= 20;
            if (GUI.Button(rightRect, new GUIContent(EditorGUIUtility.FindTexture("UnityEditor.InspectorWindow"), "Open in new Instpector tab"), Drawer.removeButtonStyle))
            {
                if (viewPageItemList[index].viewElement == null)
                {
                    editor.console.LogErrorMessage("ViewElement has not been select yet!");
                    return;
                }
                MacacaGames.CMEditorUtility.InspectTarget(viewPageItemList[index].viewElement.gameObject);
            }

            rightRect.x -= 20;
            viewPageItemList[index].sortingOrder = EditorGUI.IntField(rightRect, viewPageItemList[index].sortingOrder);

            /*Toggle Button Part End */


            rect.y += EditorGUIUtility.singleLineHeight * 1.25f;

            var viewElementRect = rect;
            viewElementRect.width = rect.width - 20;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                string oriViewElement = "";
                if (viewPageItemList[index].viewElement != null)
                {
                    oriViewElement = viewPageItemList[index].viewElement.name;
                }


                viewPageItemList[index].viewElementObject = (GameObject)EditorGUI.ObjectField(viewElementRect, "View Element", viewPageItemList[index].viewElementObject, typeof(GameObject), true);
                if (check.changed)
                {
                    if (viewPageItemList[index].viewElement == null)
                    {
                        ViewSystemLog.LogError("The setup item doesn't contain ViewElement");
                        viewPageItemList[index].viewElementObject = null;
                        return;
                    }

                    if (string.IsNullOrEmpty(viewPageItemList[index].viewElement.gameObject.scene.name))
                    {
                        //is prefabs
                        if (viewPageItemList[index].viewElement.gameObject.name != oriViewElement)
                        {
                            viewPageItemList[index].overrideDatas?.Clear();
                            viewPageItemList[index].eventDatas?.Clear();
                        }
                        return;
                    }

                    var cache = viewPageItemList[index].viewElement;
                    ViewElement original;
                    if (ViewSystemNodeEditor.overrideFromOrginal)
                    {
                        original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(cache);
                    }
                    else
                    {
                        original = PrefabUtility.GetCorrespondingObjectFromSource(cache);
                    }

                    overrideChecker = ScriptableObject.CreateInstance<ViewElementOverridesImporterWindow>();
                    overrideChecker.SetData(cache.transform, original.transform, viewPageItemList[index], currentSelectNode);
                    overrideChecker.ShowUtility();
                    viewPageItemList[index].viewElement = original;
                    viewPageItemList[index].previewViewElement = cache;

                    viewPageItemList[index].defaultTransformDatas.rectTransformData = new ViewSystemRectTransformData();
                    PickRectTransformValue(viewPageItemList[index].defaultTransformDatas, viewPageItemList[index].previewViewElement.GetComponent<RectTransform>());
                }
            }

            viewElementRect.x += viewElementRect.width;
            viewElementRect.width = 20;

            if (GUI.Button(viewElementRect, EditoModifyButton, Drawer.removeButtonStyle))
            {
                if (viewPageItemList[index].viewElement == null)
                {
                    editor.console.LogErrorMessage("ViewElement has not been select yet!");
                    return;
                }
                if (editor.overridePopupWindow.show == false || editor.overridePopupWindow.viewPageItem != viewPageItemList[index])
                {
                    viewElementRect.y += infoAreaRect.height + EditorGUIUtility.singleLineHeight * 4.5f;
                    editor.overridePopupWindow.SetViewPageItem(viewPageItemList[index]);
                    editor.overridePopupWindow.Show(viewElementRect);
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
                GUI.Label(new Rect(viewElementRect.x, viewElementRect.y - 16, 24, 24), new GUIContent(Drawer.overrideIcon, "This item has override"));
            }
            rect.y += EditorGUIUtility.singleLineHeight;
            transformEditStatus[$"{viewPageItemList[index].Id}_default"] = GUI.Toolbar(rect, transformEditStatus[$"{viewPageItemList[index].Id}_default"], new string[] { "RectTransfrom", "Custom Parent" });
            rect.y += EditorGUIUtility.singleLineHeight;

            switch (transformEditStatus[$"{viewPageItemList[index].Id}_default"])
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
                            if (GUI.Button(previewBtnRect, new GUIContent("Select", "Highlight and select ViewElement object")))
                            {
                                SelectCurrentViewElement(viewPageItemList[index]);
                            }

                            layoutButtonRect.x -= 10;
                            // layoutButtonRect.y ;
                            layoutButtonRect.width = EditorGUIUtility.singleLineHeight * 2;
                            layoutButtonRect.height = EditorGUIUtility.singleLineHeight * 2;
                            LayoutDropdownButton(layoutButtonRect, viewPageItemList[index].defaultTransformDatas.rectTransformData, false);
                            layoutButtonRect.y += EditorGUIUtility.singleLineHeight * 3;
                            layoutButtonRect.height = EditorGUIUtility.singleLineHeight;
                            using (var disable = new EditorGUI.DisabledGroupScope(viewPageItemList[index].previewViewElement == null))
                            {
                                if (GUI.Button(layoutButtonRect, new GUIContent("Pick", "Pick RectTransform value from preview ViewElement")))
                                {
                                    PickRectTransformValue(viewPageItemList[index].defaultTransformDatas, viewPageItemList[index].previewViewElement.GetComponent<RectTransform>());
                                }
                            }
                            smartPositionAndSizeRect.height = EditorGUIUtility.singleLineHeight * 4;
                            SmartPositionAndSizeFields(smartPositionAndSizeRect, true, viewPageItemList[index].defaultTransformDatas.rectTransformData, false, false);
                            anchorAndPivotRect.height = EditorGUIUtility.singleLineHeight;
                            anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight * 2;
                            anchorAndPivotRect.x += 30;
                            anchorAndPivotRect.width -= 30;
                            anchorAndPivotRect.width -= 70;
                            bool widthMode = EditorGUIUtility.wideMode;
                            float lableWidth = EditorGUIUtility.labelWidth;
                            float fieldWidth = EditorGUIUtility.fieldWidth;
                            EditorGUIUtility.fieldWidth = 40;
                            EditorGUIUtility.labelWidth = 70;
                            EditorGUIUtility.wideMode = true;
                            Rect overrideFlagRect = anchorAndPivotRect;
                            overrideFlagRect.x += 150;
                            overrideFlagRect.width = 100;
                            viewPageItemList[index].defaultTransformDatas.rectTransformFlag = (ViewElement.RectTransformFlag)EditorGUI.EnumFlagsField(overrideFlagRect, viewPageItemList[index].defaultTransformDatas.rectTransformFlag);
                            anchorPivotFoldout[index] = EditorGUI.Foldout(anchorAndPivotRect, anchorPivotFoldout[index], "Anchor and Pivot");

                            if (anchorPivotFoldout[index])
                            {
                                anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight;
                                viewPageItemList[index].defaultTransformDatas.rectTransformData.anchorMin = EditorGUI.Vector2Field(anchorAndPivotRect, "Anchor Min", viewPageItemList[index].defaultTransformDatas.rectTransformData.anchorMin);
                                anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight + 2;
                                viewPageItemList[index].defaultTransformDatas.rectTransformData.anchorMax = EditorGUI.Vector2Field(anchorAndPivotRect, "Anchor Max", viewPageItemList[index].defaultTransformDatas.rectTransformData.anchorMax);
                                anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight + 2;
                                viewPageItemList[index].defaultTransformDatas.rectTransformData.pivot = EditorGUI.Vector2Field(anchorAndPivotRect, "Pivot", viewPageItemList[index].defaultTransformDatas.rectTransformData.pivot);
                            }
                            anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight + 2;
                            rotationScaleFoldout[index] = EditorGUI.Foldout(anchorAndPivotRect, rotationScaleFoldout[index], "Rotation and Scale");

                            if (rotationScaleFoldout[index])
                            {
                                anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight;
                                viewPageItemList[index].defaultTransformDatas.rectTransformData.localEulerAngles = EditorGUI.Vector3Field(anchorAndPivotRect, "Rotation", viewPageItemList[index].defaultTransformDatas.rectTransformData.localEulerAngles);
                                anchorAndPivotRect.y += EditorGUIUtility.singleLineHeight + 2;
                                viewPageItemList[index].defaultTransformDatas.rectTransformData.localScale = EditorGUI.Vector3Field(anchorAndPivotRect, "Scale", viewPageItemList[index].defaultTransformDatas.rectTransformData.localScale);
                            }
                            EditorGUIUtility.wideMode = widthMode;
                            EditorGUIUtility.labelWidth = lableWidth;
                            EditorGUIUtility.fieldWidth = fieldWidth;

                            if (change.changed)
                            {
                                if (viewPageItemList[index].previewViewElement)
                                {
                                    viewPageItemList[index].previewViewElement.ApplyRectTransform(viewPageItemList[index].defaultTransformDatas);
                                }
                            }
                        }
                    }
                    break;
                case 1:
                    {
                        //Transform No found hint
                        if (!string.IsNullOrEmpty(viewPageItemList[index].defaultTransformDatas.parentPath))
                        {
                            var target = GameObject.Find(saveData.globalSetting.ViewControllerObjectPath + "/" + viewPageItemList[index].defaultTransformDatas.parentPath);
                            if (target == null)
                            {
                                GUI.Label(new Rect(rect.x - 24, rect.y, 24, 24), new GUIContent(Drawer.miniErrorIcon, "Transform cannot found in this item."));
                            }
                        }

                        if (string.IsNullOrEmpty(viewPageItemList[index].defaultTransformDatas.parentPath))
                        {
                            viewPageItemList[index].defaultTransformDatas.parent = (Transform)EditorGUI.ObjectField(rect, "Drag to here", viewPageItemList[index].defaultTransformDatas.parent, typeof(Transform), true);
                            rect.y += EditorGUIUtility.singleLineHeight;
                        }
                        else
                        {

                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                viewPageItemList[index].defaultTransformDatas.parentPath = EditorGUI.TextField(rect, new GUIContent("Parent", viewPageItemList[index].defaultTransformDatas.parentPath), viewPageItemList[index].defaultTransformDatas.parentPath);
                                if (check.changed)
                                {
                                    if (!string.IsNullOrEmpty(viewPageItemList[index].defaultTransformDatas.parentPath))
                                    {
                                        var target = GameObject.Find(saveData.globalSetting.ViewControllerObjectPath + "/" + viewPageItemList[index].defaultTransformDatas.parentPath);
                                        if (target)
                                        {
                                            viewPageItemList[index].defaultTransformDatas.parent = target.transform;
                                        }
                                    }
                                    else
                                        viewPageItemList[index].defaultTransformDatas.parent = null;
                                }
                            }
                            rect.y += EditorGUIUtility.singleLineHeight;
                            string shortPath = "";
                            if (!string.IsNullOrEmpty(viewPageItemList[index]?.defaultTransformDatas.parentPath))
                            {
                                shortPath = viewPageItemList[index].defaultTransformDatas.parentPath.Split('/').Last();
                                using (var disable = new EditorGUI.DisabledGroupScope(true))
                                {
                                    EditorGUI.TextField(rect, new GUIContent("Shor Path", viewPageItemList[index].defaultTransformDatas.parentPath), shortPath);
                                }
                            }
                        }

                        rect.y += EditorGUIUtility.singleLineHeight;

                        var parentFunctionRect = rect;
                        parentFunctionRect.width = rect.width * 0.32f;
                        parentFunctionRect.x += rect.width * 0.01f;
                        if (GUI.Button(parentFunctionRect, new GUIContent("Pick", "Pick Current Select Transform")))
                        {
                            var item = Selection.transforms;
                            if (item.Length == 1)
                            {
                                viewPageItemList[index].defaultTransformDatas.parent = item.First();
                            }
                        }

                        parentFunctionRect.x += parentFunctionRect.width + rect.width * 0.01f;
                        if (GUI.Button(parentFunctionRect, new GUIContent("Parent", "Highlight parent Transform object")))
                        {
                            var go = GameObject.Find(viewPageItemList[index].defaultTransformDatas.parentPath);
                            if (go)
                            {
                                EditorGUIUtility.PingObject(go);
                                Selection.objects = new[] { go };
                            }
                            else editor.console.LogErrorMessage("Target parent is not found, or the target parent is inactive.");
                        }
                        parentFunctionRect.x += parentFunctionRect.width + rect.width * 0.01f;

                        if (GUI.Button(parentFunctionRect, new GUIContent("Select", "Highlight and select ViewElement object")))
                        {
                            SelectCurrentViewElement(viewPageItemList[index]);
                        }

                        if (viewPageItemList[index].defaultTransformDatas.parent != null)
                        {
                            var path = AnimationUtility.CalculateTransformPath(viewPageItemList[index].defaultTransformDatas.parent, null);
                            var sp = path.Split('/');
                            if (sp.FirstOrDefault() == editor.ViewControllerRoot?.name)
                            {
                                viewPageItemList[index].defaultTransformDatas.parentPath = path.Substring(sp.FirstOrDefault().Length + 1);
                            }
                            else
                            {
                                editor.console.LogErrorMessage("Selected Parent is not child of ViewController GameObject");
                                ViewSystemLog.LogError("Selected Parent is not child of ViewController GameObject");
                                viewPageItemList[index].defaultTransformDatas.parent = null;
                            }
                        }
                    }
                    break;
            }

            rect.width = 18;
            rect.x -= 21;
            rect.y = oriRect.y += 20;
            if (GUI.Button(rect, new GUIContent(EditorGUIUtility.FindTexture("d_TreeEditor.Trash")), Drawer.removeButtonStyle))
            {
                viewPageItemReorderableList.index = index;
                RemoveItem(viewPageItemReorderableList);
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
            else editor.console.LogErrorMessage("Target parent is not found, or the target parent is inactive.");
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
            // if (show)
            //     rect = new Rect(0, 20f, InspectorWidth, editor.position.height - 20f);
            // else
            //     rect = Rect.zero;

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
                                vp.canvasSortOrder = EditorGUILayout.IntField("sortingOrder", vp.canvasSortOrder);
                                GUILayout.Label("", new GUIStyle("ToolbarSlider"));

                                //SafePadding
                                GUILayout.Label("Safe Padding", new GUIStyle("TE toolbarbutton"), GUILayout.Height(EditorGUIUtility.singleLineHeight));

                                var contents = new string[]{
                                   "Off","On",
                                };
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
                                    if (change.changed && ViewSystemNodeEditor.Instance.EditMode)
                                    {
                                        ViewSystemNodeEditor.ApplySafeArea(vp.edgeValues);
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
        void FloatFieldLabelAbove(Rect position, FloatGetter getter, FloatSetter setter, ViewSystemRectTransformData _rectTransform, DrivenTransformProperties driven, GUIContent label)
        {
            float lableWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 45;
            // using (new EditorGUI.DisabledScope(targets.Any(x => ((x as RectTransform).drivenProperties & driven) != 0)))
            // {
            float value = getter(_rectTransform);
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                Rect positionLabel = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                float newValue = EditorGUI.FloatField(positionLabel, label, value, EditorStyles.miniTextField);
                if (change.changed)
                {
                    setter(_rectTransform, newValue);
                }
            }
            EditorGUIUtility.labelWidth = lableWidth;
            // }
        }
        // void FloatField(Rect position, FloatGetter getter, FloatSetter setter, ViewSystemRectTransformData _rectTransform, DrivenTransformProperties driven, GUIContent label)
        // {
        //     // using (new EditorGUI.DisabledScope(targets.Any(x => ((x as RectTransform).drivenProperties & driven) != 0)))
        //     // {
        //     float value = getter(_rectTransform);
        //     // EditorGUI.showMixedValue = targets.Select(x => getter(x as RectTransform)).Distinct().Count() >= 2;
        //     using (var change = new EditorGUI.ChangeCheckScope())
        //     {
        //         float newValue = EditorGUI.FloatField(position, label, value);
        //         if (change.changed)
        //         {
        //             setter(_rectTransform, newValue);
        //         }
        //     }
        //     // }
        // }
        // void Vector2Field(Rect position,
        //     FloatGetter xGetter, FloatSetter xSetter,
        //     FloatGetter yGetter, FloatSetter ySetter,
        //     ViewSystemRectTransformData _rectTransform,
        //     DrivenTransformProperties xDriven, DrivenTransformProperties yDriven,
        //     GUIContent label)
        // {
        //     // EditorGUI.BeginProperty(position, label, vec2Property);

        //     // SerializedProperty xProperty = vec2Property.FindPropertyRelative("x");
        //     // SerializedProperty yProperty = vec2Property.FindPropertyRelative("y");

        //     // EditorGUI.PrefixLabel(position, -1, label);
        //     // float t = EditorGUIUtility.labelWidth;
        //     // int l = EditorGUI.indentLevel;
        //     Rect r0 = GetColumnRect(position, 0);
        //     Rect r1 = GetColumnRect(position, 1);
        //     // EditorGUIUtility.labelWidth = EditorGUI.CalcPrefixLabelWidth(s_XYLabels[0]);
        //     // EditorGUI.BeginProperty(r0, s_XYLabels[0], xProperty);
        //     FloatField(r0, xGetter, xSetter, _rectTransform, xDriven, s_XYLabels[0]);
        //     // EditorGUI.EndProperty();
        //     // EditorGUI.BeginProperty(r0, s_XYLabels[1], yProperty);
        //     FloatField(r1, yGetter, ySetter, _rectTransform, yDriven, s_XYLabels[1]);
        //     // EditorGUI.EndProperty();
        //     // EditorGUIUtility.labelWidth = t;
        //     // EditorGUI.indentLevel = l;
        //     // EditorGUI.EndProperty();
        // }

        void LayoutDropdownButton(Rect dropdownPosition, ViewSystemRectTransformData rectTransformData, bool anyWithoutParent)
        {
            dropdownPosition.x += 2;
            dropdownPosition.y += 17;
            // using (new EditorGUI.DisabledScope(anyWithoutParent))
            // {
            Color oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.6f) * oldColor;
            if (EditorGUI.DropdownButton(dropdownPosition, GUIContent.none, FocusType.Passive, "box"))
            {
                GUIUtility.keyboardControl = 0;
                LayoutDropdownWindow m_DropdownWindow = new LayoutDropdownWindow(rectTransformData);
                PopupWindow.Show(dropdownPosition, m_DropdownWindow);
            }
            GUI.color = oldColor;
            // }

            // if (!anyWithoutParent)
            // {
            LayoutDropdownWindow.DrawLayoutMode(new RectOffset(7, 7, 7, 7).Remove(dropdownPosition), rectTransformData.anchorMin, rectTransformData.anchorMax, rectTransformData.anchoredPosition, rectTransformData.sizeDelta);
            LayoutDropdownWindow.DrawLayoutModeHeadersOutsideRect(dropdownPosition, rectTransformData.anchorMin, rectTransformData.anchorMax, rectTransformData.anchoredPosition, rectTransformData.sizeDelta);
            // }
        }
        void SmartAnchorFields(Rect anchorRect, ViewSystemRectTransformData rectTransformData)
        {
            // anchorRect.height = EditorGUIUtility.singleLineHeight;

            // // EditorGUI.BeginChangeCheck();
            // // EditorGUI.BeginProperty(anchorRect, null, m_AnchorMin);
            // // EditorGUI.BeginProperty(anchorRect, null, m_AnchorMax);
            // // m_ShowLayoutOptions = EditorGUI.Foldout(anchorRect, m_ShowLayoutOptions, styles.anchorsContent, true);


            // // EditorGUI.EndProperty();
            // // EditorGUI.EndProperty();
            // // if (EditorGUI.EndChangeCheck())
            // //     EditorPrefs.SetBool(kShowAnchorPropsPrefName, m_ShowLayoutOptions);

            // // if (!m_ShowLayoutOptions)
            // //     return;

            // anchorRect.y += EditorGUIUtility.singleLineHeight;
            // Vector2Field(anchorRect,
            //     rectTransform => rectTransform.anchorMin.x,
            //     (rectTransform, val) => rectTransform.anchorMin.x = val,
            //     rectTransform => rectTransform.anchorMin.y,
            //     (rectTransform, val) => rectTransform.anchorMin.y = val,
            //     rectTransformData,
            //     DrivenTransformProperties.AnchorMinX,
            //     DrivenTransformProperties.AnchorMinY,
            //     Drawer.anchorMinContent);

            // // EditorGUILayout.Space(EditorGUI.kVerticalSpacingMultiField);

            // // anchorRect.y += EditorGUIUtility.singleLineHeight + EditorGUI.kVerticalSpacingMultiField;
            // Vector2Field(anchorRect,
            //     rectTransform => rectTransform.anchorMax.x,
            //     (rectTransform, val) => rectTransform.anchorMax.x = val,
            //     rectTransform => rectTransform.anchorMax.y,
            //     (rectTransform, val) => rectTransform.anchorMax.y = val,
            //     rectTransformData,
            //     DrivenTransformProperties.AnchorMaxX,
            //     DrivenTransformProperties.AnchorMaxY,
            //     Drawer.anchorMaxContent);

        }

        // void SmartPivotField()
        // {
        //     Vector2Field(EditorGUILayout.GetControlRect(),
        //         rectTransform => rectTransform.pivot.x,
        //         (rectTransform, val) => SetPivotSmart(rectTransform, val, 0, !m_RawEditMode, false),
        //         rectTransform => rectTransform.pivot.y,
        //         (rectTransform, val) => SetPivotSmart(rectTransform, val, 1, !m_RawEditMode, false),
        //         DrivenTransformProperties.PivotX,
        //         DrivenTransformProperties.PivotY,
        //         m_Pivot,
        //         styles.pivotContent);
        // }
        void SmartPositionAndSizeFields(Rect rect, bool anyWithoutParent, ViewSystemRectTransformData rectTransformData, bool anyDrivenX, bool anyDrivenY)
        {
            rect.height = EditorGUIUtility.singleLineHeight * 2;
            Rect rect2;

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
                    DrivenTransformProperties.AnchoredPositionX,
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
                    DrivenTransformProperties.None,
                    EditorGUIUtility.TrTextContent("Left"));
            }

            rect2 = GetColumnRect(rect, 1);
            if (anyNonStretchY || anyWithoutParent || anyDrivenY)
            {
                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.anchoredPosition.y,
                    (rectTransform, val) => rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, val),
                    rectTransformData,
                    DrivenTransformProperties.AnchoredPositionY,
                    EditorGUIUtility.TrTextContent("Pos Y"));
            }
            else
            {
                // Affected by both anchored position and size delta so do property handling for both. (E.g. showing animated value, prefab override etc.)
                // EditorGUI.BeginProperty(rect2, null, m_AnchoredPosition.FindPropertyRelative("y"));
                // EditorGUI.BeginProperty(rect2, null, m_SizeDelta.FindPropertyRelative("y"));
                // FloatFieldLabelAbove(rect2,
                //     rectTransform => -rectTransform.offsetMax.y,
                //     (rectTransform, val) => rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, -val),
                //     DrivenTransformProperties.None,
                //     EditorGUIUtility.TrTextContent("Top"));
                // SetFadingBasedOnControlID(ref m_ChangingTop, EditorGUIUtility.s_LastControlID);
                // EditorGUI.EndProperty();
                // EditorGUI.EndProperty();

                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => -rectTransform.offsetMax.y,
                    (rectTransform, val) => rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, -val),
                    rectTransformData,
                    DrivenTransformProperties.None,
                    EditorGUIUtility.TrTextContent("Top"));
            }

            rect2 = GetColumnRect(rect, 2);
            // EditorGUI.BeginProperty(rect2, null, m_LocalPositionZ);
            // FloatFieldLabelAbove(rect2,
            //     rectTransform => rectTransform.transform.localPosition.z,
            //     (rectTransform, val) => rectTransform.transform.localPosition = new Vector3(rectTransform.transform.localPosition.x, rectTransform.transform.localPosition.y, val),
            //     DrivenTransformProperties.AnchoredPositionZ,
            //     EditorGUIUtility.TrTextContent("Pos Z"));

            // EditorGUI.EndProperty();
            FloatFieldLabelAbove(
                rect2,
                rectTransform => rectTransform.anchoredPosition.z,
                (rectTransform, val) => rectTransform.anchoredPosition = new Vector3(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y, val),
                rectTransformData,
                DrivenTransformProperties.AnchoredPositionZ,
                EditorGUIUtility.TrTextContent("Pos Z"));
            rect.y += EditorGUIUtility.singleLineHeight;

            rect2 = GetColumnRect(rect, 0);
            if (anyNonStretchX || anyWithoutParent || anyDrivenX)
            {
                // EditorGUI.BeginProperty(rect2, null, m_SizeDelta.FindPropertyRelative("x"));
                // FloatFieldLabelAbove(rect2,
                //     rectTransform => rectTransform.sizeDelta.x,
                //     (rectTransform, val) => rectTransform.sizeDelta = new Vector2(val, rectTransform.sizeDelta.y),
                //     DrivenTransformProperties.SizeDeltaX,
                //     anyStretchX ? EditorGUIUtility.TrTextContent("W Delta") : EditorGUIUtility.TrTextContent("Width"));
                // SetFadingBasedOnControlID(ref m_ChangingWidth, EditorGUIUtility.s_LastControlID);
                // EditorGUI.EndProperty();
                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.sizeDelta.x,
                    (rectTransform, val) => rectTransform.sizeDelta = new Vector2(val, rectTransform.sizeDelta.y),
                    rectTransformData,
                    DrivenTransformProperties.AnchoredPositionZ,
                    anyStretchX ? EditorGUIUtility.TrTextContent("W Delta") : EditorGUIUtility.TrTextContent("Width"));
            }
            else
            {
                // Affected by both anchored position and size delta so do property handling for both. (E.g. showing animated value, prefab override etc.)
                // EditorGUI.BeginProperty(rect2, null, m_AnchoredPosition.FindPropertyRelative("x"));
                // EditorGUI.BeginProperty(rect2, null, m_SizeDelta.FindPropertyRelative("x"));
                // FloatFieldLabelAbove(rect2,
                //     rectTransform => -rectTransform.offsetMax.x,
                //     (rectTransform, val) => rectTransform.offsetMax = new Vector2(-val, rectTransform.offsetMax.y),
                //     DrivenTransformProperties.None,
                //     EditorGUIUtility.TrTextContent("Right"));
                // SetFadingBasedOnControlID(ref m_ChangingRight, EditorGUIUtility.s_LastControlID);
                // EditorGUI.EndProperty();
                // EditorGUI.EndProperty();
                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => -rectTransform.offsetMax.x,
                    (rectTransform, val) => rectTransform.offsetMax = new Vector2(-val, rectTransform.offsetMax.y),
                    rectTransformData,
                    DrivenTransformProperties.AnchoredPositionZ,
                    EditorGUIUtility.TrTextContent("Right"));
            }

            rect2 = GetColumnRect(rect, 1);
            if (anyNonStretchY || anyWithoutParent || anyDrivenY)
            {
                // EditorGUI.BeginProperty(rect2, null, m_SizeDelta.FindPropertyRelative("y"));
                // FloatFieldLabelAbove(rect2,
                //     rectTransform => rectTransform.sizeDelta.y,
                //     (rectTransform, val) => rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, val),
                //     DrivenTransformProperties.SizeDeltaY,
                //     anyStretchY ? EditorGUIUtility.TrTextContent("H Delta") : EditorGUIUtility.TrTextContent("Height"));
                // SetFadingBasedOnControlID(ref m_ChangingHeight, EditorGUIUtility.s_LastControlID);
                // EditorGUI.EndProperty();
                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.sizeDelta.y,
                    (rectTransform, val) => rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, val),
                    rectTransformData,
                    DrivenTransformProperties.AnchoredPositionZ,
                    anyStretchY ? EditorGUIUtility.TrTextContent("H Delta") : EditorGUIUtility.TrTextContent("Height"));
            }
            else
            {
                // Affected by both anchored position and size delta so do property handling for both. (E.g. showing animated value, prefab override etc.)
                // EditorGUI.BeginProperty(rect2, null, m_AnchoredPosition.FindPropertyRelative("y"));
                // EditorGUI.BeginProperty(rect2, null, m_SizeDelta.FindPropertyRelative("y"));
                // FloatFieldLabelAbove(rect2,
                //     rectTransform => rectTransform.offsetMin.y,
                //     (rectTransform, val) => rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, val),
                //     DrivenTransformProperties.None,
                //     EditorGUIUtility.TrTextContent("Bottom"));
                // SetFadingBasedOnControlID(ref m_ChangingBottom, EditorGUIUtility.s_LastControlID);
                // EditorGUI.EndProperty();
                // EditorGUI.EndProperty();
                FloatFieldLabelAbove(
                    rect2,
                    rectTransform => rectTransform.offsetMin.y,
                    (rectTransform, val) => rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, val),
                    rectTransformData,
                    DrivenTransformProperties.AnchoredPositionZ,
                    EditorGUIUtility.TrTextContent("Bottom"));
            }

            rect2 = rect;
            rect2.height = EditorGUIUtility.singleLineHeight;
            rect2.y += EditorGUIUtility.singleLineHeight;
            rect2.yMin -= 2;
            rect2.xMin = rect2.xMax - 26;

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
                    editor.navigationWindow.Show();
                    ViewState vs = saveData.viewStates.SingleOrDefault(m => m.viewState.name == vp.viewState).viewState;
                    editor.navigationWindow.SetViewPage(vp, vs);
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