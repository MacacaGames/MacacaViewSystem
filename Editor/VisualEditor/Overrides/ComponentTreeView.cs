﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;
namespace MacacaGames.ViewSystem
{
    public class ComponentTreeView : TreeView
    {
        // ViewPageItem viewPageItem;
        ViewElement viewElement;
        GameObject go;
        System.Func<SerializedProperty, string, bool> IsModify;
        System.Action OnDelete;
        public ComponentTreeView(GameObject go, ViewElement viewElement, TreeViewState treeViewState, bool isPrefabRoot, System.Func<SerializedProperty, string, bool> IsModify, System.Action OnDelete)
            : base(treeViewState)
        {
            this.go = go;
            this.viewElement = viewElement;
            rowHeight = 24;
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            this.IsModify = IsModify;
            this.OnDelete = OnDelete;
            CacheComponent(go, 0, isPrefabRoot);
            Reload();
        }

        int Id = 1;
        class TreeViewWrapper
        {
            public TreeViewWrapper(int id)
            {
                this.id = id;
            }
            public int id;
            public object values;
        }

        List<TreeViewWrapper> serializedObjects = new List<TreeViewWrapper>();
        void CacheComponent(GameObject go, int layer, bool isPrefabRoot)
        {
            var so = new SerializedObject(go);

            var t = new TreeViewWrapper(Id);
            t.values = so;
            serializedObjects.Add(t);
            Id++;
            CacheProperty(so);

            var components = go.GetComponents(typeof(Component));
            foreach (var item in components)
            {
                if (item == null)
                {
                    ViewSystemLog.LogError("It seems there is some Component's script is missing, Please check your prefab");
                    continue;
                }

                // if (isPrefabRoot == true &&
                //     (item is UnityEngine.Transform || item is UnityEngine.RectTransform)
                // )
                // {
                //     continue;
                // }
                var t1 = new TreeViewWrapper(Id);
                var so1 = new SerializedObject(item);
                t1.values = so1;
                serializedObjects.Add(t1);
                Id++;
                CacheProperty(so1);
            }
        }

        List<TreeViewWrapper> allTreeViewWrappers = new List<TreeViewWrapper>();

        Dictionary<string, List<TreeViewWrapper>> serializedPropertys = new Dictionary<string, List<TreeViewWrapper>>();
        void CacheProperty(SerializedObject obj)
        {
            allTreeViewWrappers.Clear();
            var prop = obj.GetIterator().Copy();
            string key = "";
            if (obj.targetObject as Component == null)
            {
                key = "UnityEngine.GameObject";
            }
            else
            {
                key = obj.targetObject.GetType().Name;
            }
            List<TreeViewWrapper> slist;
            if (!serializedPropertys.TryGetValue(key, out slist))
            {
                slist = new List<TreeViewWrapper>();
                serializedPropertys.Add(key, slist);
            }

            TreeViewWrapper temp;
            //GameObject Hack m_IsActive is not a Visable Property but we still want to modify it.
            if (key == "UnityEngine.GameObject")
            {
                var active = obj.FindProperty("m_IsActive");
                if (active != null)
                {
                    temp = new TreeViewWrapper(Id);
                    temp.values = active.Copy();
                    slist.Add(temp);
                    allTreeViewWrappers.Add(temp);
                    Id++;
                }
            }

            prop.NextVisible(true);
            do
            {
                //排除不希望被修改的欄位
                if (VS_EditorUtility.IsPropertyNeedIgnore(prop))
                {
                    continue;
                }
                temp = new TreeViewWrapper(Id);
                allTreeViewWrappers.Add(temp);
                temp.values = prop.Copy();
                slist.Add(temp);
                Id++;
            }
            while (prop.NextVisible(false));
        }

        TreeViewItem treeRoot;
        protected override TreeViewItem BuildRoot()
        {
            TreeViewItem treeRoot = new TreeViewItem { id = 1, depth = -1, displayName = go.name, icon = Drawer.prefabIcon };

            var allTreeItem = new List<TreeViewItem>();
            foreach (var item in serializedObjects)
            {
                var so = (SerializedObject)item.values;
                var _cachedContent = new GUIContent(EditorGUIUtility.ObjectContent(so.targetObject, so.targetObject.GetType()));
                if (so.targetObject as Component == null)
                {
                    _cachedContent.text = "UnityEngine.GameObject";
                }
                else
                {

                    _cachedContent.text = so.targetObject.GetType().Name;
                }
                string labelSubFix = "";
                if (so.targetObject is Transform || so.targetObject is RectTransform)
                {
                    labelSubFix = " (Override on Transform may not work)";
                }
                allTreeItem.Add(new TreeViewItem { id = item.id, depth = 0, displayName = _cachedContent.text + labelSubFix, icon = (Texture2D)_cachedContent.image });

                foreach (var item2 in serializedPropertys[_cachedContent.text])
                {
                    var sp = (SerializedProperty)item2.values;
                    allTreeItem.Add(new TreeViewItem { id = item2.id, depth = 1, displayName = sp.displayName });
                }
            }

            SetupParentsAndChildrenFromDepths(treeRoot, allTreeItem);
            // Return root of the tree
            return treeRoot;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item;
            extraSpaceBeforeIconAndLabel = 20;
            Rect rect = args.rowRect;
            rect.x += 20;
            //Components
            if (item.depth == 0)
            {
                rect.width = 20;
                rect.height = 20;
                GUI.Label(rect, item.icon);
                rect.x += 20;
                rect.width = args.rowRect.width - 20;

                GUI.Label(rect, item.displayName);
            }

            //Properties
            if (item.depth == 1)
            {
                var path = AnimationUtility.CalculateTransformPath(go.transform, viewElement.transform);
                var single = serializedPropertys.SelectMany(x => x.Value).SingleOrDefault(m => m.id == item.id);
                var sp = (SerializedProperty)single.values;
                // bool isModified = viewPageItem.overrideDatas.Where(m =>
                //      m.targetPropertyName == sp.name &&
                //      m.targetTransformPath == path &&
                //      m.targetComponentType == sp.serializedObject.targetObject.GetType().ToString()
                // ).Count() > 0;
                bool isModified = (IsModify != null && IsModify.Invoke(sp, path));
                if (isModified)
                {
                    GUI.Box(args.rowRect, GUIContent.none, new GUIStyle("U2D.createRect"));
                    Rect r = rect;
                    r.x -= 4;
                    r.width = 24;
                    r.height = 24;
                    // if (GUI.Button(r, new GUIContent(EditorGUIUtility.FindTexture("winbtn_mac_close_h")), new GUIStyle("AC PreviewHeader")))
                    // {
                    //     if (EditorUtility.DisplayDialog("Warring", "Do you want to remove this modified override?", "Yes", "No"))
                    //     {
                    //         // var s = viewPageItem.overrideDatas.Where(m =>
                    //         //     m.targetPropertyName == sp.name &&
                    //         //     m.targetTransformPath == path &&
                    //         //     m.targetComponentType == sp.serializedObject.targetObject.GetType().ToString()
                    //         // ).FirstOrDefault();
                    //         // viewPageItem.overrideDatas.Remove(s);
                    //         OnDelete?.Invoke();
                    //     }
                    // }
                }

                rect.y = args.rowRect.y;
                rect.x += 20;
                //Draw Label
                var content = new GUIContent(item.displayName);
                GUI.Label(rect, content);
                var lastContentSize = GUI.skin.label.CalcSize(content);

                rect.x = rect.width - 150;
                rect.width = 150;

                //Draw Value
                DrawValue(rect, sp);
            }
            //base.RowGUI(args);
        }


        protected void DrawValue(Rect rect, SerializedProperty Target)
        {
            if (Target.propertyType != SerializedPropertyType.Generic)
            {
                Color backgroundColor = GUI.backgroundColor;
                GUI.backgroundColor *= new Color(1f, 1f, 1f, 0.5f);
                switch (Target.propertyType)
                {
                    case SerializedPropertyType.Float:
                        GUI.Box(rect, Target.floatValue.ToString(), Drawer.valueBoxStyle);
                        break;
                    case SerializedPropertyType.Integer:
                        GUI.Box(rect, Target.intValue.ToString(), Drawer.valueBoxStyle);
                        break;
                    case SerializedPropertyType.String:
                        GUI.Box(rect, new GUIContent("\"" + Target.stringValue + "\"", Target.stringValue), Drawer.valueBoxStyle);
                        break;
                    case SerializedPropertyType.Boolean:
                        GUI.Box(rect, Target.boolValue.ToString(), Drawer.valueBoxStyle);
                        break;
                    case SerializedPropertyType.Color:
                        {
                            using (var disable = new EditorGUI.DisabledGroupScope(true))
                            {
                                var n = rect.height - EditorGUIUtility.singleLineHeight;
                                rect.height = EditorGUIUtility.singleLineHeight;
                                rect.y += n * 0.5f;
                                EditorGUI.ColorField(rect, Target.colorValue);
                            }
                            break;
                        }
                    case SerializedPropertyType.Enum:
                        if (Target.enumValueIndex < Target.enumDisplayNames.Length && Target.enumValueIndex >= 0)
                        {
                            GUI.Box(rect, Target.enumDisplayNames[Target.enumValueIndex].ToString(), Drawer.valueBoxStyle);
                        }
                        else
                        {
                            GUI.Box(rect, new GUIContent(Drawer.miniErrorIcon, "The enum value set on prefab is missing, please check the component."), Drawer.valueBoxStyle);
                        }
                        break;
                    case SerializedPropertyType.LayerMask:
                        GUI.Box(rect, Target.intValue.ToString(), Drawer.valueBoxStyle);
                        break;
                    case SerializedPropertyType.Vector3:
                        GUI.Box(rect, Target.vector3Value.ToString(), Drawer.valueBoxStyle);
                        break;
                    case SerializedPropertyType.Vector2:
                        GUI.Box(rect, Target.vector2Value.ToString(), Drawer.valueBoxStyle);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        using (var disable = new EditorGUI.DisabledGroupScope(true))
                        {
                            var n = rect.height - EditorGUIUtility.singleLineHeight;
                            rect.height = EditorGUIUtility.singleLineHeight;
                            rect.y += n * 0.5f;
                            if (Target.objectReferenceValue) EditorGUI.ObjectField(rect, Target.objectReferenceValue, typeof(UnityEngine.Object), false);
                            else GUI.Box(rect, new GUIContent("null", "null"), Drawer.valueBoxStyle);
                        }
                        break;
                    default:
                        GUI.Box(rect, new GUIContent(Target.propertyType.ToString(), Target.propertyType.ToString()), Drawer.valueBoxStyle);
                        break;
                }
                GUI.backgroundColor = backgroundColor;
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            var single = serializedPropertys.SelectMany(x => x.Value).SingleOrDefault(m => m.id == id);
            if (single == null)
            {
                SetExpandedRecursive(id, !IsExpanded(id));
                return;
            }
            if (single.values is SerializedProperty)
            {
                var sp = (SerializedProperty)single.values;
                OnItemClick?.Invoke((SerializedProperty)single.values);
            }
        }

        public delegate void _OnItemClick(SerializedProperty targetProperty);
        public event _OnItemClick OnItemClick;


    }

}
