using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;
using System.Reflection;
using UnityEditor.IMGUI.Controls;
using CloudMacaca;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeInspector
    {
        private ViewSystemNode currentSelectNode = null;
        ViewSystemNodeEditor editor;
        public bool show = true;
        AnimBool showBasicInfo;
        AnimBool showViewPageItem;
        ReorderableList viewPageItemList;
        GUIStyle nameStyle;
        GUIStyle nameUnnamedStyle;
        GUIStyle nameErrorStyle;
        GUIStyle nameEditStyle;
        static ViewSystemSaveData saveData => ViewSystemNodeEditor.saveData;
        static GUIContent EditoModifyButton = new GUIContent(Drawer.overridePopupIcon, "Show/Hide Modified Properties and Events");
        SerializedObject serializedObject;
        SerializedProperty serializedProperty;
        public ViewSystemNodeInspector(ViewSystemNodeEditor editor)
        {
            this.editor = editor;
            serializedObject = new SerializedObject(saveData);

            show = true;
            showBasicInfo = new AnimBool(true);
            showBasicInfo.valueChanged.AddListener(this.editor.Repaint);

            showViewPageItem = new AnimBool(true);
            showViewPageItem.valueChanged.AddListener(this.editor.Repaint);

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
        List<ViewPageItem> list;
        List<EditableLockItem> editableLock = new List<EditableLockItem>();
        class EditableLockItem
        {
            public EditableLockItem(bool defaultValue)
            {
                parent = defaultValue;
                name = defaultValue;
            }
            public bool parent;
            public bool name;
        }

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
                list = vp.viewPageItems;
                // var s = saveData.viewPages.Single(m => m.viewPage == vp);
                // var index = saveData.viewPages.IndexOf(s);
                // var sp = serializedObject.FindProperty("viewPages");
                // var x = sp.GetArrayElementAtIndex(index);
                // var y = x.FindPropertyRelative("viewPage");
                // var z = y.FindPropertyRelative("viewPageItems");
                // serializedProperty = z;
            }
            if (currentSelectNode is ViewStateNode)
            {
                var vs = ((ViewStateNode)currentSelectNode).viewState;
                list = vs.viewPageItems;
                // var s = saveData.viewStates.Single(m => m.viewState == vs);
                // var index = saveData.viewStates.IndexOf(s);
                // var sp = serializedObject.FindProperty("viewStates");
                // var x = sp.GetArrayElementAtIndex(index);
                // var y = x.FindPropertyRelative("viewState");
                // var z = y.FindPropertyRelative("viewPageItems");
                // serializedProperty = z;
            }

            editableLock.Clear();
            list.All(x =>
            {
                editableLock.Add(new EditableLockItem(true));
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
            viewPageItemList = null;
            viewPageItemList = new ReorderableList(list, typeof(List<ViewPageItem>), true, true, true, false);
            //viewPageItemList = new ReorderableList(serializedProperty.serializedObject, serializedProperty, true, true, true, false);
            viewPageItemList.drawElementCallback += DrawViewItemElement;
            viewPageItemList.drawHeaderCallback += DrawViewItemHeader;
            viewPageItemList.elementHeight = EditorGUIUtility.singleLineHeight * 5f;
            viewPageItemList.onAddCallback += AddItem;
            viewPageItemList.drawElementBackgroundCallback += DrawItemBackground;
            //viewPageItemList.elementHeightCallback += ElementHight;
            layouted = false;
        }

        private float ElementHight(int index)
        {
            throw new NotImplementedException();
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
            if (list.serializedProperty != null)
            {
                list.serializedProperty.arraySize += 1;
                list.index = list.serializedProperty.arraySize - 1;
                serializedObject.ApplyModifiedProperties();
                var sp = list.serializedProperty.GetArrayElementAtIndex(list.index);
                SerializedPropertyExtensions.SetTargetObjectOfProperty(sp, new ViewPageItem(null));
                var ve_sp = sp.FindPropertyRelative("viewElement");
                ve_sp.objectReferenceValue = null;
            }
            else
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
            }

            editableLock.Add(new EditableLockItem(true));
            // RebuildInspector();
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
            // if (GUI.Button(rect, ReorderableList.defaultBehaviours.iconToolbarPlus, ReorderableList.defaultBehaviours.preButton))
            // {
            //     AddItem(viewPageItemList);
            // }
        }

        const int rightBtnWidth = 0;
        ViewPageItem copyPasteBuffer;
        ViewElementOverridesImporterWindow overrideChecker;
        ViewPageItem CopyItem(bool copyOverride = true, bool copyEvent = true)
        {
            var copyResult = new ViewPageItem(copyPasteBuffer.viewElement);
            copyResult.TweenTime = copyPasteBuffer.TweenTime;
            copyResult.delayOut = copyPasteBuffer.delayOut;
            copyResult.delayIn = copyPasteBuffer.delayIn;
            copyResult.parentPath = copyPasteBuffer.parentPath;
            copyResult.excludePlatform = copyPasteBuffer.excludePlatform;
            copyResult.parent = copyPasteBuffer.parent;

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
            if (index >= list.Count)
            {
                return;
            }
            //var vpi_sp = viewPageItemList.serializedProperty.GetArrayElementAtIndex(index);
            var e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.button == 1 && e.type == EventType.MouseDown)
                {
                    GenericMenu genericMenu = new GenericMenu();
                    genericMenu.AddItem(new GUIContent("Copy"), false,
                        () =>
                        {
                            copyPasteBuffer = list[index];
                        }
                    );
                    if (copyPasteBuffer != null)
                    {
                        genericMenu.AddItem(new GUIContent("Paste (Default)"), false,
                            () =>
                            {
                                list[index] = CopyItem(false, false);
                                GUI.changed = true;
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Paste (with Property Data)"), false,
                            () =>
                            {
                                list[index] = CopyItem(true, false);
                                GUI.changed = true;
                            }
                        );
                        genericMenu.AddItem(new GUIContent("Paste (with Events Data)"), false,
                           () =>
                           {
                               list[index] = CopyItem(false, true);
                               GUI.changed = true;
                           }
                       );
                        genericMenu.AddItem(new GUIContent("Paste (with All Data)"), false,
                           () =>
                           {
                               list[index] = CopyItem(true, true);
                               GUI.changed = true;
                           }
                       );
                    }
                    genericMenu.ShowAsContext();
                }
            }
            EditorGUIUtility.labelWidth = 80.0f;

            Rect oriRect = rect;

            rect.x = oriRect.x;
            rect.width = oriRect.width - rightBtnWidth;
            rect.height = EditorGUIUtility.singleLineHeight;

            rect.y += EditorGUIUtility.singleLineHeight * 0.25f;
            /*Name Part Start */
            var nameRect = rect;
            //nameRect.height += EditorGUIUtility.singleLineHeight * 0.25f;
            nameRect.width = rect.width - 60;
            GUIStyle nameRuntimeStyle;

            if (list.Where(m => m.name == list[index].name).Count() > 1 && !string.IsNullOrEmpty(list[index].name))
            {
                GUI.color = Color.red;
                nameRuntimeStyle = nameErrorStyle;
            }
            else
            {
                GUI.color = Color.white;
                nameRuntimeStyle = nameStyle;
            }

            if (editableLock[index].name)
            {
                string showName;
                if (string.IsNullOrEmpty(list[index].name) == true)
                {
                    if (list[index].viewElement)
                    {
                        showName = $"{list[index].viewElement.name}";
                    }
                    else
                        showName = $"Unnamed";
                    GUI.Label(nameRect, showName, nameUnnamedStyle);
                }
                else
                {
                    showName = list[index].name;
                    GUI.Label(nameRect, showName, nameRuntimeStyle);
                }
            }
            else
            {
                list[index].name = EditorGUI.TextField(nameRect, GUIContent.none, list[index].name);
            }
            if (e.isMouse && e.type == EventType.MouseDown && e.clickCount == 2 && nameRect.Contains(e.mousePosition))
            {
                editableLock[index].name = !editableLock[index].name;
            }
            nameRect.x += nameRect.width;
            nameRect.width = 16;
            editableLock[index].name = EditorGUI.Toggle(nameRect, new GUIContent("", "Manual Name"), editableLock[index].name, nameEditStyle);

            GUI.color = Color.white;

            /*Name Part End */

            /*Toggle Button Part Start */
            Rect rightRect = rect;

            rightRect.x = rect.width;
            rightRect.width = 20;
            if (GUI.Button(rightRect, new GUIContent(EditorGUIUtility.FindTexture("_Popup"), "More Setting"), Drawer.removeButtonStyle))
            {
                PopupWindow.Show(rect, new VS_EditorUtility.ViewPageItemDetailPopup(rect, list[index]));
            }

            rightRect.x -= 20;
            if (GUI.Button(rightRect, new GUIContent(EditorGUIUtility.FindTexture("UnityEditor.InspectorWindow"), "Open in new Instpector tab"), Drawer.removeButtonStyle))
            {
                if (list[index].viewElement == null)
                {
                    editor.console.LogErrorMessage("ViewElement has not been select yet!");
                    return;
                }
                CloudMacaca.CMEditorUtility.InspectTarget(list[index].viewElement.gameObject);
            }

            rightRect.x -= 20;
            /*Toggle Button Part End */


            rect.y += EditorGUIUtility.singleLineHeight * 1.25f;

            var veRect = rect;
            veRect.width = rect.width - 20;
            //var viewElementProperty = vpi_sp.FindPropertyRelative("viewElement");
            //ViewElement ve = (ViewElement)viewElementProperty.objectReferenceValue;
            //ViewElement ve = list[index].viewElement;
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                string oriViewElement = "";
                if (list[index].viewElement != null)
                {
                    oriViewElement = list[index].viewElement.name;
                }

                //EditorGUI.ObjectField(veRect, viewElementProperty,);
                //list[index].viewElement = (ViewElement)EditorGUI.ObjectField(veRect, "View Element", list[index].viewElement, typeof(ViewElement), true);
                list[index].viewElementObject = (GameObject)EditorGUI.ObjectField(veRect, "View Element", list[index].viewElementObject, typeof(GameObject), true);
                if (check.changed)
                {
                    if (list[index].viewElement == null)
                    {
                        ViewSystemLog.LogError("The setup item doesn't contain ViewElement");
                        list[index].viewElementObject = null;
                        return;
                    }

                    if (string.IsNullOrEmpty(list[index].viewElement.gameObject.scene.name))
                    {
                        //is prefabs
                        if (list[index].viewElement.gameObject.name != oriViewElement)
                        {
                            list[index].overrideDatas?.Clear();
                            list[index].eventDatas?.Clear();
                        }
                        return;
                    }

                    var cache = list[index].viewElement;
                    ViewElement original;
                    if (ViewSystemNodeEditor.overrideFromOrginal)
                    {
                        original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(cache);
                    }
                    else
                    {
                        original = PrefabUtility.GetCorrespondingObjectFromSource(cache);
                    }

                    //if (overrideChecker) overrideChecker.Close();
                    overrideChecker = ScriptableObject.CreateInstance<ViewElementOverridesImporterWindow>();
                    overrideChecker.SetData(cache.transform, original.transform, list[index], currentSelectNode);
                    overrideChecker.ShowUtility();
                    //viewElementProperty.objectReferenceValue = original;
                    list[index].viewElement = original;
                    list[index].parent = cache.transform.parent;
                }
            }

            veRect.x += veRect.width;
            veRect.width = 20;

            if (GUI.Button(veRect, EditoModifyButton, Drawer.removeButtonStyle))
            {
                if (list[index].viewElement == null)
                {
                    editor.console.LogErrorMessage("ViewElement has not been select yet!");
                    return;
                }
                if (editor.overridePopupWindow.show == false || editor.overridePopupWindow.viewPageItem != list[index])
                {
                    veRect.y += infoAreaRect.height + EditorGUIUtility.singleLineHeight * 4.5f;
                    editor.overridePopupWindow.SetViewPageItem(list[index]);
                    editor.overridePopupWindow.Show(veRect);
                    currentShowOverrideItem = index;
                }
                else
                {
                    editor.overridePopupWindow.show = false;
                }
            }

            //Has override hint
            if (list[index].overrideDatas?.Count() > 0 ||
                   list[index].eventDatas?.Count() > 0 ||
                   list[index].navigationDatas?.Count() > 0)
            {
                GUI.Label(new Rect(veRect.x, veRect.y, 24, 24), new GUIContent(Drawer.overrideIcon, "This item has override"));
            }
            rect.y += EditorGUIUtility.singleLineHeight;

            veRect = rect;
            veRect.width = rect.width - 20;
            if (!editableLock[index].parent)
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    list[index].parentPath = EditorGUI.TextField(veRect, new GUIContent("Parent", list[index].parentPath), list[index].parentPath);
                    if (check.changed)
                    {
                        if (!string.IsNullOrEmpty(list[index].parentPath))
                        {
                            var target = GameObject.Find(saveData.globalSetting.ViewControllerObjectPath + "/" + list[index].parentPath);
                            if (target)
                            {
                                list[index].parent = target.transform;
                            }
                        }
                        else
                            list[index].parent = null;
                    }
                }
            }
            else
            {
                string shortPath = "";
                if (!string.IsNullOrEmpty(list[index]?.parentPath))
                {
                    shortPath = list[index].parentPath.Split('/').Last();
                }
                using (var disable = new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUI.TextField(veRect, new GUIContent("Parent", list[index].parentPath), shortPath);
                }
            }

            if (!string.IsNullOrEmpty(list[index].parentPath))
            {
                var target = GameObject.Find(saveData.globalSetting.ViewControllerObjectPath + "/" + list[index].parentPath);
                if (target == null)
                {
                    GUI.Label(new Rect(veRect.x - 24, veRect.y, 24, 24), new GUIContent(Drawer.miniErrorIcon, "Transform cannot found in this item."));
                }
            }



            veRect.x += veRect.width;
            veRect.width = 20;
            editableLock[index].parent = EditorGUI.Toggle(veRect, new GUIContent("", "Enable Manual Modify"), editableLock[index].parent, new GUIStyle("IN LockButton"));

            rect.y += EditorGUIUtility.singleLineHeight;

            if (editableLock[index].parent)
            {
                var parentFunctionRect = rect;
                parentFunctionRect.width = rect.width * 0.32f;
                parentFunctionRect.x += rect.width * 0.01f;
                if (GUI.Button(parentFunctionRect, new GUIContent("Pick", "Pick Current Select Transform")))
                {
                    var item = Selection.transforms;
                    if (item.Length > 1)
                    {
                        editor.console.LogErrorMessage("Only object can be selected.");
                        goto PICK_BREAK;
                    }
                    if (item.Length == 0)
                    {
                        editor.console.LogErrorMessage("No object is been select, please check object is in scene or not.");
                        goto PICK_BREAK;
                    }
                    list[index].parent = item.First();
                }
            // Due to while using auto layout we cannot return
            // Therefore use goto to escap the if scope
            PICK_BREAK:
                parentFunctionRect.x += parentFunctionRect.width + rect.width * 0.01f;
                if (GUI.Button(parentFunctionRect, new GUIContent("Select Parent", "Highlight parent Transform object")))
                {
                    var go = GameObject.Find(list[index].parentPath);
                    if (go)
                    {
                        EditorGUIUtility.PingObject(go);
                        Selection.objects = new[] { go };
                    }
                    else editor.console.LogErrorMessage("Target parent is not found, or the target parent is inactive.");
                }
                parentFunctionRect.x += parentFunctionRect.width + rect.width * 0.01f;
                if (GUI.Button(parentFunctionRect, new GUIContent("Select Preview", "Highlight the preview ViewElement object (Only work while is preview the selected page)")))
                {
                    if (list[index].previewViewElement)
                    {
                        EditorGUIUtility.PingObject(list[index].previewViewElement);
                        Selection.objects = new[] { list[index].previewViewElement.gameObject };
                    }
                    else editor.console.LogErrorMessage("Target parent is not found, or the target parent is inactive.");
                    // var go = GameObject.Find(list[index].parentPath);
                    // if (go.transform.childCount > 0)
                    // {
                    //     var pi = go.transform.Find(list[index].viewElement.name);
                    //     if (pi)
                    //     {
                    //         EditorGUIUtility.PingObject(pi);
                    //         Selection.objects = new[] { pi };
                    //     }
                    //     else editor.console.LogErrorMessage("Target parent is not found, or the target parent is inactive.");
                    // }
                    // else editor.console.LogErrorMessage("Target parent is not found, or the target parent is inactive.");
                }
            }
            else
            {
                list[index].parent = (Transform)EditorGUI.ObjectField(rect, "Drag to here", list[index].parent, typeof(Transform), true);
            }

            if (list[index].parent != null)
            {
                var path = AnimationUtility.CalculateTransformPath(list[index].parent, null);
                var sp = path.Split('/');
                if (sp.First() == editor.ViewControllerRoot.name)
                {
                    list[index].parentPath = path.Substring(sp.First().Length + 1);
                }
                else
                {
                    editor.console.LogErrorMessage("Selected Parent is not child of ViewController GameObject");
                    ViewSystemLog.LogError("Selected Parent is not child of ViewController GameObject");
                    list[index].parent = null;
                }
            }

            rect.width = 18;
            rect.x -= 21;
            if (GUI.Button(rect, new GUIContent(EditorGUIUtility.FindTexture("d_TreeEditor.Trash")), Drawer.removeButtonStyle))
            {
                viewPageItemList.index = index;
                RemoveItem(viewPageItemList);
            }
        }
        static float InspectorWidth = 350;
        Rect infoAreaRect;
        public Vector2 scrollerPos;
        bool layouted = false;
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
                showViewPageItem.target = EditorGUILayout.Foldout(showViewPageItem.target, "ViewPageItems");
                using (var scroll = new EditorGUILayout.ScrollViewScope(scrollerPos))
                {
                    scrollerPos = scroll.scrollPosition;
                    using (var fade = new EditorGUILayout.FadeGroupScope(showViewPageItem.faded))
                    {
                        if (viewPageItemList != null && fade.visible) viewPageItemList.DoLayoutList();
                    }
                }
            }
            else
            {
                GUILayout.Label("Nothing selected :)");
            }

            //if (serializedObject != null)
            //    serializedObject.ApplyModifiedProperties();

            GUILayout.EndArea();
            //DrawResizeBar();
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
                            vp.viewPageTransitionTimingType = (ViewPage.ViewPageTransitionTimingType)EditorGUILayout.EnumPopup("ViewPageTransitionTimingType", vp.viewPageTransitionTimingType);
                            using (var disable = new EditorGUI.DisabledGroupScope(vp.viewPageType != ViewPage.ViewPageType.Overlay))
                            {
                                vp.autoLeaveTimes = EditorGUILayout.FloatField("AutoLeaveTimes", vp.autoLeaveTimes);
                            }
                            using (var disable = new EditorGUI.DisabledGroupScope(vp.viewPageTransitionTimingType != ViewPage.ViewPageTransitionTimingType.自行設定))
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
                                EditorGUILayout.EnumPopup("ViewPageTransitionTimingType", ViewPage.ViewPageTransitionTimingType.接續前動畫);
                                EditorGUILayout.FloatField("AutoLeaveTimes", 0);
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