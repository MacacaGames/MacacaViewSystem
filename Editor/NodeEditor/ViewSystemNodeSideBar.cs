using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;

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
        }

        private Rect rect;
        List<ViewPageItem> list;

        public void SetCurrentSelectItem(ViewSystemNode currentSelectNode)
        {
            this.currentSelectNode = currentSelectNode;
            if (currentSelectNode is ViewPageNode)
            {
                list = ((ViewPageNode)currentSelectNode).viewPage.viewPageItem;
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
            viewPageItemList.elementHeight = EditorGUIUtility.singleLineHeight * 5;
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
        private void DrawViewItemElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index > list.Count)
            {
                return;
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
                list[index].viewElement = (ViewElement)EditorGUI.ObjectField(rect, "View Element", list[index].viewElement, typeof(ViewElement));
                rect.y += EditorGUIUtility.singleLineHeight;

                list[index].parent = (Transform)EditorGUI.ObjectField(rect, "Parent", list[index].parent, typeof(Transform));
                rect.y += EditorGUIUtility.singleLineHeight;

                list[index].delayIn = EditorGUI.Slider(rect, "Delay In", list[index].delayIn, 0, 1);
                rect.y += EditorGUIUtility.singleLineHeight;

                list[index].delayOut = EditorGUI.Slider(rect, "Delay Out", list[index].delayOut, 0, 1);
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

        public void Draw()
        {
            rect = new Rect(0, 20f, 350, editor.position.height - 20f);

            GUILayout.BeginArea(rect, "", "box");
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

        }

        Vector2 scrollerPos;
        void DrawViewPageDetail(ViewPageNode viewPageNode)
        {
            var vp = viewPageNode.viewPage;
            EditorGUILayout.BeginVertical();

            showBasicInfo.target = EditorGUILayout.Foldout(showBasicInfo.target, "Basic Info");
            if (EditorGUILayout.BeginFadeGroup(showBasicInfo.faded))
            {
                EditorGUILayout.BeginVertical(GUILayout.Height(70));
                vp.name = EditorGUILayout.TextField("Name", vp.name);
                vp.autoLeaveTimes = EditorGUILayout.FloatField("AutoLeaveTimes", vp.autoLeaveTimes);
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
                EditorGUI.BeginDisabledGroup(false);
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