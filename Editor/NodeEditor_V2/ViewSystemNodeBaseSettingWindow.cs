using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemNodeBaseSettingWindow
    {
        ViewSystemNodeEditor editor;
        ViewSystemDataReaderV2 dataReader;
        public bool showBaseSettingWindow;
        private Rect rect;
        ViewSystemSaveData saveData;
        public BaseSettingNode node;

        public ViewSystemNodeBaseSettingWindow(ViewSystemNodeEditor editor, ViewSystemDataReaderV2 dataReader)
        {
            this.editor = editor;
            this.dataReader = dataReader;
            showBaseSettingWindow = false;
            saveData = dataReader.GetSetting();
            node = new BaseSettingNode(saveData.baseSetting.nodePosition);
            node.OnDrag += (p) =>
            {
                saveData.baseSetting.nodePosition = p;
            };
        }

        int windowWidth = 300;
        public void Draw()
        {
            if (!node.isSelect)
            {

                return;
            }

            rect = new Rect(editor.position.width - windowWidth, 20f, windowWidth, editor.position.height - 20f);
            node.clickContainRect = rect;
            GUILayout.BeginArea(rect, "", new GUIStyle("flow node 0"));
            GUILayout.Label("Base Setting", new GUIStyle("DefaultCenteredLargeText"));
            saveData.baseSetting.ViewControllerObjectPath = EditorGUILayout.TextField("View Controller GameObject", saveData.baseSetting.ViewControllerObjectPath);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                saveData.baseSetting.UIRootScene = (GameObject)EditorGUILayout.ObjectField(saveData.baseSetting.UIRootScene, typeof(GameObject), true);
                if (check.changed)
                {


                }
            }

            GUILayout.EndArea();
        }

    }
    public class BaseSettingNode : ViewSystemNode
    {
        public BaseSettingNode(Vector2 pos)
        {
            this.rect = new Rect(pos.x, pos.y, 160, 60);
            this.nodeType = NodeType.BaseSetting;
            nodeStyleString = "flow node 3";
        }
        public override void Draw()
        {
            DrawNode("Base Setting");
        }
    }
}
