using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;
using System.Reflection;

namespace CloudMacaca.ViewSystem
{
    public class ViewSystemNodeSideBar
    {
        private ViewSystemNode currentSelectNode = null;
        ViewSystemNodeEditor editor;
        AnimBool showBasicInfo;
        AnimBool showViewPageItem;
        ReorderableList viewPageItemList;
        GUIStyle removeButtonStyle;
        public ViewSystemNodeSideBar(ViewSystemNodeEditor editor)
        {
            this.editor = editor;
            showBasicInfo = new AnimBool(true);
            showBasicInfo.valueChanged.AddListener(this.editor.Repaint);

            showViewPageItem = new AnimBool(true);
            showViewPageItem.valueChanged.AddListener(this.editor.Repaint);
            removeButtonStyle = new GUIStyle
            {
                fixedWidth = 25f,
                active =
            {
                background = CMEditorUtility.CreatePixelTexture("Dark Pixel (List GUI)", new Color32(100, 100, 100, 255))
            },
                imagePosition = ImagePosition.ImageOnly,
                alignment = TextAnchor.MiddleCenter
            };

            excludePlatformOptions.Clear();

            foreach (var item in Enum.GetValues(typeof(ViewPageItem.PlatformOption)))
            {
                excludePlatformOptions.Add(item.ToString());
            }
        }
        public bool isMouseInSideBar()
        {
            return rect.Contains(Event.current.mousePosition);
        }

        private Rect rect;
        List<ViewPageItem> list;

        public void SetCurrentSelectItem(ViewSystemNode currentSelectNode)
        {
            this.currentSelectNode = currentSelectNode;
            if (currentSelectNode is ViewPageNode)
            {
                list = ((ViewPageNode)currentSelectNode).viewPage.viewPageItems;
            }
            if (currentSelectNode is ViewStateNode)
            {
                list = ((ViewStateNode)currentSelectNode).viewState.viewPageItems;
            }
            RefreshSideBar();
        }
        void RefreshSideBar()
        {
            viewPageItemList = null;
            viewPageItemList = new ReorderableList(list, typeof(List<ViewPageItem>), true, true, true, false);
            viewPageItemList.drawElementCallback += DrawViewItemElement;
            viewPageItemList.drawHeaderCallback += DrawViewItemHeader;
            viewPageItemList.elementHeight = EditorGUIUtility.singleLineHeight * 5.5f;
            viewPageItemList.onAddCallback += AddItem;
        }

        private void AddItem(ReorderableList rlist)
        {
            list.Add(new ViewPageItem(null));
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
            if (GUI.Button(rect, ReorderableList.defaultBehaviours.iconToolbarPlus, ReorderableList.defaultBehaviours.preButton))
            {
                AddItem(viewPageItemList);
            }
        }


        const int removeBtnWidth = 25;
        ViewPageItem copyPasteBuffer;
        private void DrawViewItemElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index > list.Count)
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
                            copyPasteBuffer = new ViewPageItem(list[index].viewElement);
                            copyPasteBuffer.TweenTime = list[index].TweenTime;
                            copyPasteBuffer.delayOut = list[index].delayOut;
                            copyPasteBuffer.delayIn = list[index].delayIn;

                            var excludePlatformCopyed = new List<ViewPageItem.PlatformOption>();
                            foreach (var item in list[index].excludePlatform)
                            {
                                switch (item)
                                {
                                    case ViewPageItem.PlatformOption.Android:
                                        excludePlatformCopyed.Add(ViewPageItem.PlatformOption.Android);
                                        break;
                                    case ViewPageItem.PlatformOption.iOS:
                                        excludePlatformCopyed.Add(ViewPageItem.PlatformOption.iOS);

                                        break;
                                    case ViewPageItem.PlatformOption.UWP:
                                        excludePlatformCopyed.Add(ViewPageItem.PlatformOption.UWP);

                                        break;
                                    case ViewPageItem.PlatformOption.tvOS:
                                        excludePlatformCopyed.Add(ViewPageItem.PlatformOption.tvOS);

                                        break;
                                }
                            }
                            copyPasteBuffer.excludePlatform = excludePlatformCopyed;
                            copyPasteBuffer.parent = list[index].parent;
                        }
                    );
                    if (copyPasteBuffer != null)
                    {
                        genericMenu.AddItem(new GUIContent("Paste"), false,
                            () =>
                            {
                                list[index] = copyPasteBuffer;
                                copyPasteBuffer = null;
                                GUI.changed = true;
                            }
                        );
                    }
                    else
                    {
                        genericMenu.AddDisabledItem(new GUIContent("Paste"));
                    }
                    genericMenu.ShowAsContext();
                }
            }



            EditorGUIUtility.labelWidth = 80.0f;
            float oriwidth = rect.width;
            float oriHeigh = rect.height;
            float oriX = rect.x;
            float oriY = rect.y;

            rect.x = oriX;
            rect.width = oriwidth - removeBtnWidth;
            rect.height = EditorGUIUtility.singleLineHeight;

            //Still shows OutOfRangeException when remove item (but everything working fine)
            //Doesn't know how to fix that
            //Currently use a try-catch to avoid console message.
            try
            {
                rect.y += EditorGUIUtility.singleLineHeight * 0.25f;

                list[index].viewElement = (ViewElement)EditorGUI.ObjectField(rect, "View Element", list[index].viewElement, typeof(ViewElement));
                rect.y += EditorGUIUtility.singleLineHeight;

                list[index].parent = (Transform)EditorGUI.ObjectField(rect, "Parent", list[index].parent, typeof(Transform));
                rect.y += EditorGUIUtility.singleLineHeight;

                list[index].delayIn = EditorGUI.Slider(rect, "Delay In", list[index].delayIn, 0, 1);
                rect.y += EditorGUIUtility.singleLineHeight;

                list[index].delayOut = EditorGUI.Slider(rect, "Delay Out", list[index].delayOut, 0, 1);
                rect.y += EditorGUIUtility.singleLineHeight;

                bool isExcloudAndroid = !list[index].excludePlatform.Contains(ViewPageItem.PlatformOption.Android);
                bool isExcloudiOS = !list[index].excludePlatform.Contains(ViewPageItem.PlatformOption.iOS);
                bool isExcloudtvOS = !list[index].excludePlatform.Contains(ViewPageItem.PlatformOption.tvOS);
                bool isExcloudUWP = !list[index].excludePlatform.Contains(ViewPageItem.PlatformOption.UWP);


                EditorGUIUtility.labelWidth = 20.0f;
                rect.width = oriwidth * 0.25f;

                string proIconFix = "";
                if(EditorGUIUtility.isProSkin){
                    proIconFix = "d_";
                }
                else{
                    proIconFix = "";
                }

                EditorGUI.BeginChangeCheck();

                isExcloudAndroid = EditorGUI.Toggle(rect, new GUIContent(EditorGUIUtility.FindTexture(proIconFix + "BuildSettings.Android.Small")), isExcloudAndroid);
                rect.x += rect.width;

                isExcloudiOS = EditorGUI.Toggle(rect, new GUIContent(EditorGUIUtility.FindTexture(proIconFix + "BuildSettings.iPhone.Small")), isExcloudiOS);
                rect.x += rect.width;

                isExcloudtvOS = EditorGUI.Toggle(rect, new GUIContent(EditorGUIUtility.FindTexture(proIconFix + "BuildSettings.tvOS.Small")), isExcloudtvOS);
                rect.x += rect.width;

                isExcloudUWP = EditorGUI.Toggle(rect, new GUIContent(EditorGUIUtility.FindTexture(proIconFix + "BuildSettings.Standalone.Small")), isExcloudUWP);

                if (EditorGUI.EndChangeCheck())
                {
                    list[index].excludePlatform.Clear();

                    if (!isExcloudAndroid)
                    {
                        list[index].excludePlatform.Add(ViewPageItem.PlatformOption.Android);
                    }
                    if (!isExcloudiOS)
                    {
                        list[index].excludePlatform.Add(ViewPageItem.PlatformOption.iOS);
                    }
                    if (!isExcloudtvOS)
                    {
                        list[index].excludePlatform.Add(ViewPageItem.PlatformOption.tvOS);
                    }
                    if (!isExcloudUWP)
                    {
                        list[index].excludePlatform.Add(ViewPageItem.PlatformOption.tvOS);
                    }

                }
                rect.y += EditorGUIUtility.singleLineHeight;
            }
            catch
            {

            }

            rect.x = oriwidth;
            rect.y = oriY;
            rect.height = oriHeigh;
            rect.width = removeBtnWidth;
            if (GUI.Button(rect, ReorderableList.defaultBehaviours.iconToolbarMinus, removeButtonStyle))
            {
                list.RemoveAt(index);
                RefreshSideBar();
                return;
            }
        }
        static float SideBarWidth = 350;
        public void Draw()
        {
            rect = new Rect(0, 20f, SideBarWidth, editor.position.height - 20f);

            GUILayout.BeginArea(rect, "", new GUIStyle("flow node 0"));

            if (currentSelectNode != null)
            {
                if (currentSelectNode.nodeType == ViewStateNode.NodeType.FullPage || currentSelectNode.nodeType == ViewStateNode.NodeType.Overlay)
                {
                    DrawViewPageDetail(((ViewPageNode)currentSelectNode));
                }
                if (currentSelectNode.nodeType == ViewStateNode.NodeType.ViewState)
                {
                    DrawViewStateDetail(((ViewStateNode)currentSelectNode));

                }
            }
            GUILayout.EndArea();
            DrawResizeBar();
        }
        Rect ResizeBarRect;
        bool resizeBarPressed = false;
        void DrawResizeBar()
        {
            ResizeBarRect = new Rect(rect.x + SideBarWidth, rect.y, 4, rect.height);
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
                SideBarWidth += Event.current.delta.x;
                SideBarWidth = Mathf.Clamp(SideBarWidth, 270, 450);
                Event.current.Use();
                GUI.changed = true;
            }
        }

        Vector2 scrollerPos;
        static List<string> excludePlatformOptions = new List<string>();
        int currentSelect;
        void DrawViewPageDetail(ViewPageNode viewPageNode)
        {
            var vp = viewPageNode.viewPage;
            EditorGUILayout.BeginVertical();

            GUILayout.Label(string.IsNullOrEmpty(vp.name) ? "Unnamed" : vp.name, new GUIStyle("DefaultCenteredLargeText"));

            showBasicInfo.target = EditorGUILayout.Foldout(showBasicInfo.target, "Basic Info");
            if (EditorGUILayout.BeginFadeGroup(showBasicInfo.faded))
            {
                EditorGUILayout.BeginVertical(GUILayout.Height(70));
                vp.name = EditorGUILayout.TextField("Name", vp.name);
                EditorGUI.BeginDisabledGroup(vp.viewPageType != ViewPage.ViewPageType.Overlay);
                vp.autoLeaveTimes = EditorGUILayout.FloatField("AutoLeaveTimes", vp.autoLeaveTimes);
                EditorGUI.EndDisabledGroup();
                vp.viewPageTransitionTimingType = (ViewPage.ViewPageTransitionTimingType)EditorGUILayout.EnumPopup("ViewPageTransitionTimingType", vp.viewPageTransitionTimingType);
                EditorGUI.BeginDisabledGroup(vp.viewPageTransitionTimingType != ViewPage.ViewPageTransitionTimingType.自行設定);
                vp.customPageTransitionWaitTime = EditorGUILayout.FloatField("CustomPageTransitionWaitTime", vp.customPageTransitionWaitTime);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFadeGroup();



            showViewPageItem.target = EditorGUILayout.Foldout(showViewPageItem.target, "ViewPageItems");
            scrollerPos = EditorGUILayout.BeginScrollView(scrollerPos);
            if (EditorGUILayout.BeginFadeGroup(showViewPageItem.faded))
            {
                if (viewPageItemList != null) viewPageItemList.DoLayoutList();
            }
            EditorGUILayout.EndFadeGroup();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();


        }
        void DrawViewStateDetail(ViewStateNode viewStateNode)
        {
            var vs = viewStateNode.viewState;


            EditorGUILayout.BeginVertical();
            GUILayout.Label(string.IsNullOrEmpty(vs.name) ? "Unnamed" : vs.name,new GUIStyle("DefaultCenteredLargeText"));

            showBasicInfo.target = EditorGUILayout.Foldout(showBasicInfo.target, "Basic Info");
            if (EditorGUILayout.BeginFadeGroup(showBasicInfo.faded))
            {
                EditorGUILayout.BeginVertical(GUILayout.Height(70));
                EditorGUI.BeginChangeCheck();
                vs.name = EditorGUILayout.TextField("name", vs.name);
                if (EditorGUI.EndChangeCheck())
                {
                    viewStateNode.currentLinkedViewPageNode.All(
                        m =>
                        {
                            m.viewPage.viewState = vs.name;
                            return true;
                        }
                    );
                }
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.FloatField("AutoLeaveTimes", 0);
                EditorGUILayout.EnumPopup("ViewPageTransitionTimingType", ViewPage.ViewPageTransitionTimingType.接續前動畫);
                EditorGUILayout.FloatField("CustomPageTransitionWaitTime", 0);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFadeGroup();



            showViewPageItem.target = EditorGUILayout.Foldout(showViewPageItem.target, "ViewPageItems");
            scrollerPos = EditorGUILayout.BeginScrollView(scrollerPos);
            if (EditorGUILayout.BeginFadeGroup(showViewPageItem.faded))
            {
                if (viewPageItemList != null) viewPageItemList.DoLayoutList();
            }
            EditorGUILayout.EndFadeGroup();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }




    }
}