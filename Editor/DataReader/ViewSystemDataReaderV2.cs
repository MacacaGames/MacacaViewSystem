using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
using System.IO;
using System;

namespace CloudMacaca.ViewSystem.NodeEditorV2
{
    public class ViewSystemDataReaderV2 : IViewSystemDateReader
    {
        const string ViewSystemResourceFolder = "Assets/ViewSystemResources/";
        const string ViewSystemSaveDataFileName = "ViewSystemData.asset";
        public ViewSystemDataReaderV2(ViewSystemNodeEditor editor)
        {
            this.editor = editor;
        }
        ViewSystemNodeEditor editor;

        ViewSystemSaveData data;
        Transform ViewControllerTransform;
        bool isInit = false;
        public bool Init()
        {
            CheckAndCreateResourceFolder();

            data = CheckOrReadSaveData();

            //建立 UI Hierarchy 環境
            if (!string.IsNullOrEmpty(data.globalSetting.ViewControllerObjectPath))
                ViewControllerTransform = GameObject.Find(data.globalSetting.ViewControllerObjectPath).transform;

            if (data.globalSetting.UIRoot != null && data.globalSetting.UIRootScene == null)
            {
                //Try find exsit first
                var current = ViewControllerTransform.Find(data.globalSetting.UIRoot.name);
                if (current != null)
                {
                    data.globalSetting.UIRootScene = current.gameObject;
                }
                //Or Instantiate a Prefab
                else
                {
                    var ui_root = PrefabUtility.InstantiatePrefab(data.globalSetting.UIRoot, ViewControllerTransform);
                    data.globalSetting.UIRootScene = (GameObject)ui_root;
                    PrefabUtility.UnpackPrefabInstance(data.globalSetting.UIRootScene, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                }
            }

            // 整理 Editor 資料
            List<ViewPageNode> viewPageNodes = new List<ViewPageNode>();
            //先整理 ViewPage Node
            foreach (var item in data.viewPages)
            {
                var isOverlay = item.viewPage.viewPageType == ViewPage.ViewPageType.Overlay;

                var node = editor.AddViewPageNode(item.nodePosition, isOverlay, item.viewPage);
                viewPageNodes.Add(node);
            }

            //在整理 ViewState Node
            foreach (var item in data.viewStates)
            {
                var vp_of_vs = viewPageNodes.Where(m => m.viewPage.viewState == item.viewState.name);

                var node = editor.AddViewStateNode(item.nodePosition, item.viewState);
                editor.CreateConnection(node);
            }
            isInit = data ? true : false;
            return isInit;
        }

        public void OnViewPageAdd(ViewPageNode node)
        {
            data.viewPages.Add(new ViewSystemSaveData.ViewPageSaveData(new Vector2(node.rect.x, node.rect.y), node.viewPage));
        }
        public void OnViewStateAdd(ViewStateNode node)
        {
            data.viewStates.Add(new ViewSystemSaveData.ViewStateSaveData(new Vector2(node.rect.x, node.rect.y), node.viewState));
        }

        public void OnViewPageDelete(ViewPageNode node)
        {
            var s = data.viewPages.SingleOrDefault(m => m.viewPage == node.viewPage);
            data.viewPages.Remove(s);
        }


        public void OnViewStateDelete(ViewStateNode node)
        {
            var s = data.viewStates.SingleOrDefault(m => m.viewState == node.viewState);
            node.currentLinkedViewPageNode.All(
                (m) =>
                {
                    m.currentLinkedViewStateNode = null;
                    m.viewPage.viewState = "";
                    return true;
                }
            );
            data.viewStates.Remove(s);
        }

        public void OnViewPagePreview(ViewPage viewPage)
        {
            //throw new System.NotImplementedException();
        }
        public void Normalized()
        {
            //Clear UI Root Object
            // try
            // {
            //     UnityEngine.Object.DestroyImmediate(data.globalSetting.UIRootScene);
            // }
            // catch
            // {
            //     var c = ViewControllerTransform.Find(data.globalSetting.UIRoot.name);
            //     UnityEngine.Object.DestroyImmediate(c);
            // }
            ClearAllViewElementInScene();
            //throw new System.NotImplementedException();
        }

        void ClearAllViewElementInScene()
        {
            var allViewElement = UnityEngine.Object.FindObjectsOfType<ViewElement>();
            foreach (var item in allViewElement)
            {
                if (string.IsNullOrEmpty(item.gameObject.scene.name))
                {
                    continue;
                }
                UnityEngine.Object.DestroyImmediate(item.gameObject);
            }
        }

        public void Save(List<ViewPageNode> viewPageNodes, List<ViewStateNode> viewStateNodes)
        {

            foreach (var item in viewPageNodes)
            {
                var vp = data.viewPages.SingleOrDefault(m => m.viewPage.name == item.viewPage.name);
                vp.nodePosition = new Vector2(item.rect.x, item.rect.y);
            }

            foreach (var item in viewStateNodes)
            {
                var vs = data.viewStates.SingleOrDefault(m => m.viewState.name == item.viewState.name);
                vs.nodePosition = new Vector2(item.rect.x, item.rect.y);
            }

            if (data.globalSetting != null)
            {
                //Delete all ViewElement in scene before save!!!!
                ClearAllViewElementInScene();
                //Apply Prefab
                //PrefabUtility.ApplyPrefabInstance(data.globalSetting.UIRootScene, InteractionMode.AutomatedAction);
                PrefabUtility.SaveAsPrefabAsset(data.globalSetting.UIRootScene, ViewSystemResourceFolder + data.globalSetting.UIRootScene.name + ".prefab");
            }
            UnityEditor.EditorUtility.SetDirty(data);
        }

        public ViewSystemSaveData GetBaseSetting()
        {
            return data;
        }

        public GameObject SetUIRootObject(GameObject obj)
        {
            if (!Directory.Exists(ViewSystemResourceFolder))
            {
                CheckAndCreateResourceFolder();
            }
            return PrefabUtility.SaveAsPrefabAsset(obj, ViewSystemResourceFolder + obj.name + ".prefab");
        }

        void CheckAndCreateResourceFolder()
        {
            if (!Directory.Exists(ViewSystemResourceFolder))
            {
                Directory.CreateDirectory(ViewSystemResourceFolder);
                using (FileStream fs = File.Create(ViewSystemResourceFolder + "Auto Create by ViewSystem.txt"))
                {
                    Byte[] info = System.Text.Encoding.UTF8.GetBytes("This folder and contain datas is auto Created by ViewSystem, Delete this folder or any datas may cause ViewSystem works not properly.");
                    // Add some information to the file.
                    fs.Write(info, 0, info.Length);
                }
                AssetDatabase.Refresh();
            }
        }

        ViewSystemSaveData CheckOrReadSaveData()
        {
            ViewSystemSaveData result = null;
            var filePath = ViewSystemResourceFolder + ViewSystemSaveDataFileName;

            if (!File.Exists(filePath))
            {
                result = ScriptableObject.CreateInstance<ViewSystemSaveData>();
                AssetDatabase.CreateAsset(result, filePath);
                AssetImporter.GetAtPath(filePath);
                AssetDatabase.Refresh();
                return result;
            }

            result = AssetDatabase.LoadAssetAtPath<ViewSystemSaveData>(filePath);
            return result;
        }

        public Transform GetViewControllerRoot()
        {
            return ViewControllerTransform;
        }
    }

}

