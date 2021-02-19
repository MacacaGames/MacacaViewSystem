using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
using System.IO;
using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MacacaGames.ViewSystem.VisualEditor
{
    public class ViewSystemDataReaderV2
    {
        const string ViewSystemResourceFolder = "Assets/ViewSystemResources/";
        const string ViewSystemSaveDataFileName = "ViewSystemData.asset";
        public ViewSystemDataReaderV2(ViewSystemVisualEditor editor)
        {
            this.editor = editor;
        }
        ViewSystemVisualEditor editor;

        ViewSystemSaveData data;
        public Transform ViewControllerTransform;
        public bool isDirty = false;
        bool isInit = false;
        public bool Init()
        {
            CheckAndCreateResourceFolder();

            data = CheckOrReadSaveData();


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
        UnityEngine.SceneManagement.Scene newScene;
        const string ViewSystemEditScene = "ViewSystemEditScene";
        public void EditStart()
        {
            var exsitScene = SceneManager.GetSceneByName(ViewSystemEditScene);
            if (exsitScene != null)
            {
                EditEnd();
            }

            newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            newScene.name = ViewSystemEditScene;
            //建立 UI Hierarchy 環境
            if (!string.IsNullOrEmpty(data.globalSetting.ViewControllerObjectPath))
            {
                var go = new GameObject(data.globalSetting.ViewControllerObjectPath);
                EditorSceneManager.MoveGameObjectToScene(go, newScene);
                ViewControllerTransform = go.transform;
            }
            GameObject ui_root = null;
            if (data.globalSetting.UIRoot != null && data.globalSetting.UIRootScene == null)
            {
                //Always generate a new one to avoid version conflict.
#if UNITY_2019_1_OR_NEWER
                ui_root = PrefabUtility.InstantiatePrefab(data.globalSetting.UIRoot, ViewControllerTransform) as GameObject;
#else
                ui_root = PrefabUtility.InstantiatePrefab(data.globalSetting.UIRoot);
               ((GameObject)ui_root).transform.SetParent(ViewControllerTransform);
#endif
                data.globalSetting.UIRootScene = ui_root;
                PrefabUtility.UnpackPrefabInstance(data.globalSetting.UIRootScene, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            }
        }

        public void EditEnd()
        {
            EditorSceneManager.CloseScene(SceneManager.GetSceneByName(ViewSystemEditScene), true);
            // EditorSceneManager.CloseScene(SceneManager.GetSceneByName("Untitled"), true);

        }

        public void RefeshEdit()
        {
            EditEnd();
            EditStart();
        }

        public void OnViewPageAdd(ViewPageNode node)
        {
            data.viewPages.Add(new ViewSystemSaveData.ViewPageSaveData(new Vector2(node.rect.x, node.rect.y), node.viewPage));
            isDirty = true;
        }

        public void OnViewStateAdd(ViewStateNode node)
        {
            data.viewStates.Add(new ViewSystemSaveData.ViewStateSaveData(new Vector2(node.rect.x, node.rect.y), node.viewState));
            isDirty = true;
        }

        public void OnViewPageDelete(ViewPageNode node)
        {
            var s = data.viewPages.SingleOrDefault(m => m.viewPage == node.viewPage);
            data.viewPages.Remove(s);
            isDirty = true;

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
            isDirty = true;
        }

        public void GenerateDefaultUIRoot()
        {
            if (string.IsNullOrEmpty(data.globalSetting.ViewControllerObjectPath))
            {
                ViewSystemLog.LogError("Please set ViewController Object Path first");
                return;
            }
            GameObject canvasObject = new GameObject("Canvas");
            var viewControllerObject = GameObject.Find(data.globalSetting.ViewControllerObjectPath);
            canvasObject.transform.SetParent(viewControllerObject.transform);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
            var canvasScaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasScaler.referenceResolution = new Vector2(1080, 1920);
            canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 1;
            var graphicRaycaster = canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var eventSystem = canvasObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var inputModule = canvasObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            SetUIRootObject(canvasObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }

        public void SetUIRootObject(GameObject obj)
        {
            if (!Directory.Exists(ViewSystemResourceFolder))
            {
                CheckAndCreateResourceFolder();
            }

            var saveObject = PrefabUtility.SaveAsPrefabAsset(obj, ViewSystemResourceFolder + obj.name + ".prefab");
            data.globalSetting.UIRoot = saveObject;
        }

        ViewSystemUtilitys.PageRootWrapper previewUIRootWrapper;
        public void ApplySafeArea(SafePadding.PerEdgeValues edgeValues)
        {
            if (previewUIRootWrapper != null)
            {
                previewUIRootWrapper.safePadding.SetPaddingValue(edgeValues);
            }
        }

        public void OnViewPagePreview(ViewPage viewPage, Dictionary<string, bool> breakPoints)
        {

            string UIRootName = "";
            if (data.globalSetting.UIRootScene == null)
            {
                ViewSystemLog.ShowNotification(editor, new GUIContent($"There is no canvas in your scene, do you enter EditMode?"), 2);
                ViewSystemLog.LogError($"There is no canvas in your scene, do you enter EditMode?");
                return;
            }
            UIRootName = data.globalSetting.UIRoot.name;
            //throw new System.NotImplementedException();
            ClearAllViewElementInScene();
            // 打開所有相關 ViewElements
            ViewState viewPagePresetTemp;
            List<ViewPageItem> viewItemForNextPage = new List<ViewPageItem>();

            //從 ViewPagePreset 尋找 (ViewState)
            if (!string.IsNullOrEmpty(viewPage.viewState))
            {
                viewPagePresetTemp = data.viewStates.Select(m => m.viewState).SingleOrDefault(m => m.name == viewPage.viewState);
                if (viewPagePresetTemp != null)
                {
                    viewItemForNextPage.AddRange(viewPagePresetTemp.viewPageItems);
                }
            }

            //從 ViewPage 尋找
            viewItemForNextPage.AddRange(viewPage.viewPageItems);

            Transform root = ViewControllerTransform;

            var canvas = root.Find($"{UIRootName}");
            string viewPageName = ViewSystemUtilitys.GetPageRootName(viewPage);
            previewUIRootWrapper = ViewSystemUtilitys.CreatePageTransform(viewPageName, canvas, viewPage.canvasSortOrder);
            ApplySafeArea(viewPage.edgeValues);
            Transform fullPageRoot = root.Find($"{UIRootName}/{viewPageName}");
            //TO do apply viewPage component on fullPageRoot

            //打開相對應物件
            foreach (ViewPageItem item in viewItemForNextPage.OrderBy(m => m.sortingOrder))
            {
                if (item.viewElement == null)
                {
                    ViewSystemLog.LogWarning($"There are some ViewElement didn't setup correctly in this page or state");
                    continue;
                }


                var temp = PrefabUtility.InstantiatePrefab(item.viewElement.gameObject);
                ViewElement tempViewElement = ((GameObject)temp).GetComponent<ViewElement>();
                tempViewElement.currentViewPageItem = item;
                tempViewElement.currentViewPage = viewPage;

                tempViewElement.gameObject.SetActive(true);
                var rectTransform = tempViewElement.GetComponent<RectTransform>();
                Transform tempParent = null;

                // TODO preview viewpage with BreakPoint
                var transformData = item.GetCurrentViewElementTransform(breakPoints);
                if (!string.IsNullOrEmpty(transformData.parentPath))
                {
                    //Custom Parent implement
                    tempParent = root.Find(transformData.parentPath);
                }
                else
                {
                    //RectTransform implement
                    tempParent = fullPageRoot;
                }
                rectTransform.SetParent(tempParent, true);

                if (!string.IsNullOrEmpty(transformData.parentPath))
                {
                    var mFix = tempViewElement.GetComponent<ViewMarginFixer>();
                    if (mFix != null) mFix.ApplyModifyValue();
                    tempViewElement.rectTransform.localScale = Vector3.one;
                    tempViewElement.rectTransform.anchoredPosition3D = Vector3.zero;
                }
                else
                {
                    tempViewElement.ApplyRectTransform(transformData);
                }

                tempViewElement.ApplyOverrides(item.overrideDatas);
                tempViewElement.ApplyNavigation(item.navigationDatas);

                item.previewViewElement = tempViewElement;


                //Sample animator traisintion viewlement to target frame
                if (tempViewElement.transition != ViewElement.TransitionType.Animator)
                    continue;

                Animator animator = tempViewElement.animator;
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                foreach (AnimationClip clip in clips)
                {
                    if (clip.name.ToLower().Contains(tempViewElement.AnimationStateName_Loop.ToLower()))
                    {
                        clip.SampleAnimation(animator.gameObject, 0);
                    }
                }
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        public void Normalized()
        {
            //Clear UI Root Object
            try
            {
                UnityEngine.Object.DestroyImmediate(data.globalSetting.UIRootScene);
            }
            catch
            {
                var c = GameObject.Find(data.globalSetting.UIRoot.name);
                UnityEngine.Object.DestroyImmediate(c);
            }

            editor.ClearEditor();
            editor.EditMode = false;
            EditEnd();
            //throw new System.NotImplementedException();
        }

        public void ClearAllViewElementInScene()
        {
            if (previewUIRootWrapper != null && previewUIRootWrapper.rectTransform)
            {
                UnityEngine.Object.DestroyImmediate(previewUIRootWrapper.rectTransform.gameObject);
                previewUIRootWrapper = null;
            }
            var allViewElement = UnityEngine.Object.FindObjectsOfType<ViewElement>();
            //NestedViewElement is obslote do nothing with NestedViewElement.
            //var allNestedViewElement = UnityEngine.Object.FindObjectsOfType<NestedViewElement>();
            foreach (var item in allViewElement)
            {
                try
                {
                    UnityEngine.Object.DestroyImmediate(item.gameObject);
                }
                catch
                {
                    //ViewSystemLog.LogWarning($"ignore");
                }
            }
        }

        public void Save()
        {
            Save(null, null);
        }

        public void Save(List<ViewPageNode> viewPageNodes, List<ViewStateNode> viewStateNodes)
        {
            if (viewPageNodes != null)
            {
                foreach (var item in viewPageNodes)
                {
                    if (string.IsNullOrEmpty(item.viewPage.name))
                    {
                        continue;
                    }
                    var vp = data.viewPages.SingleOrDefault(m => m.viewPage.name == item.viewPage.name);
                    vp.nodePosition = new Vector2(item.rect.x, item.rect.y);
                }
            }

            if (viewStateNodes != null)
            {
                foreach (var item in viewStateNodes)
                {
                    if (string.IsNullOrEmpty(item.viewState.name))
                    {
                        continue;
                    }
                    var vs = data.viewStates.SingleOrDefault(m => m.viewState.name == item.viewState.name);
                    vs.nodePosition = new Vector2(item.rect.x, item.rect.y);
                }
            }

            if (data.globalSetting != null)
            {
                //Delete all ViewElement in scene before save!!!!
                ClearAllViewElementInScene();
                //Apply Prefab
                if (data.globalSetting.UIRootScene != null)
                    data.globalSetting.UIRoot = PrefabUtility.SaveAsPrefabAsset(data.globalSetting.UIRootScene, ViewSystemResourceFolder + data.globalSetting.UIRootScene.name + ".prefab");
            }
            UnityEditor.EditorUtility.SetDirty(data);
            isDirty = false;
            AssetDatabase.SaveAssets();
        }

        public ViewSystemSaveData GetSaveData()
        {
            return data;
        }



        public static void CheckAndCreateResourceFolder()
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

        // public Transform GetViewControllerRoot()
        // {
        //     return ViewControllerTransform;
        // }
    }

}

