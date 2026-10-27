using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MacacaGames.ViewSystem.VisualEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MacacaGames.ViewSystem.Tests
{
    public class ViewSystemWindowlessEditingTests
    {
        string folder;
        ViewSystemSaveData data;
        ViewSystemDataReader reader;
        int windowCount;

        [SetUp]
        public void SetUp()
        {
            EditorApplication.LockReloadAssemblies();
            windowCount = Resources.FindObjectsOfTypeAll<ViewSystemVisualEditor>().Length;
            folder = "Assets/__ViewSystemEditing_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            data = ScriptableObject.CreateInstance<ViewSystemSaveData>();
            data.globalSetting = new ViewSystemBaseSetting();
            AssetDatabase.CreateAsset(data, folder + "/CustomGraph.asset");
            reader = ViewSystemDataReader.Open(data);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Assert.AreEqual(windowCount, Resources.FindObjectsOfTypeAll<ViewSystemVisualEditor>().Length,
                    "Authoring must not create a Visual Editor window.");
                AssetDatabase.DeleteAsset(folder);
            }
            finally { EditorApplication.UnlockReloadAssemblies(); }
        }

        GameObject CreatePrefab(string name)
        {
            var instance = new GameObject(name, typeof(RectTransform), typeof(ViewElement));
            try { return PrefabUtility.SaveAsPrefabAsset(instance, folder + "/" + name + ".prefab"); }
            finally { Object.DestroyImmediate(instance); }
        }

        ViewPage AddPage(string name = "Lobby", string state = "")
        {
            var page = new ViewPage { name = name, viewState = state };
            page.viewPageItems.Add(new ViewPageItem(CreatePrefab(name + "Panel").GetComponent<ViewElement>()));
            reader.OnViewPageAdd(new Vector2(100, 200), page);
            return page;
        }

        [Test]
        public void CreateEditSaveAndBakeUseTheSpecifiedAssetWithoutAWindow()
        {
            reader.OnViewStateAdd(Vector2.zero, new ViewState { name = "Shared" });
            var page = AddPage(state: "Shared");
            page.canvasSortOrder = 7;
            page.viewPageItems[0].defaultTransformDatas.rectTransformData.sizeDelta = new Vector2(320, 180);
            reader.Save();
            string script = folder + "/Names.cs";
            reader.Bake(script);

            Assert.IsTrue(reader.Validate().isValid);
            var reopened = ViewSystemDataReader.Open(folder + "/CustomGraph.asset");
            var savedPage = reopened.GetSaveData().GetViewPageSaveDatas().Single().viewPage;
            Assert.AreEqual(7, savedPage.canvasSortOrder);
            Assert.AreEqual(new Vector2(320, 180), savedPage.viewPageItems[0].defaultTransformDatas.rectTransformData.sizeDelta);
            Assert.That(AssetDatabase.GetAssetPath(data.viewPagesNodeSaveDatas.Single()), Does.StartWith(folder + "/"));
            Assert.That(File.ReadAllText(script), Does.Contain("public const string  Lobby = \"Lobby\""));
            Assert.That(File.ReadAllText(script), Does.Contain("public const string  Shared = \"Shared\""));
            AssetDatabase.DeleteAsset(script);
        }

        [Test]
        public void SaveDataDoesNotClearPreviewObjectsOrApplyTheSceneUiRoot()
        {
            var sceneObject = new GameObject("UnrelatedPreview", typeof(RectTransform), typeof(ViewElement));
            try
            {
                var prefab = CreatePrefab("UiRoot");
                data.globalSetting.UIRoot = prefab;
                data.globalSetting.UIRootScene = sceneObject;
                reader.ViewControllerTransform = sceneObject.transform;
                reader.SaveData();
                Assert.IsTrue(sceneObject != null);
                Assert.AreSame(prefab, data.globalSetting.UIRoot);
            }
            finally { Object.DestroyImmediate(sceneObject); }
        }

        [Test]
        public void ValidationReturnsMissingReferencesAndBindingsWithoutDialogs()
        {
            var page = AddPage(state: "MissingState");
            page.viewPageItems[0].overrideDatas.Add(new ViewElementPropertyOverrideData
            {
                targetTransformPath = "MissingChild",
                targetComponentType = typeof(RectTransform).FullName,
                targetPropertyName = "m_AnchorMin"
            });
            page.viewPageItems.Add(new ViewPageItem());
            var result = reader.Validate();
            Assert.IsFalse(result.isValid);
            Assert.That(result.issues.Select(issue => issue.code), Does.Contain("missing_state"));
            Assert.That(result.issues.Select(issue => issue.code), Does.Contain("missing_view_element"));
            Assert.That(result.issues.Select(issue => issue.code), Does.Contain("missing_transform"));
            Assert.That(JsonUtility.ToJson(result), Does.Contain("missing_state"));
        }

        [Test]
        public void BakeRejectsNamesThatWouldBreakGeneratedCompilationBeforeWriting()
        {
            AddPage("Invalid Page");
            string script = folder + "/Names.cs";
            Assert.Throws<ArgumentException>(() => reader.Bake(script));
            Assert.IsFalse(File.Exists(script));
            Assert.That(reader.Validate().issues.Select(issue => issue.code), Does.Contain("invalid_baked_name"));
        }

        [Test]
        public void BakeRejectsEscapedIdentifierCollisions()
        {
            Assert.Throws<ArgumentException>(() => ViewSystemScriptBaker.GenerateSource(
                new List<ViewPage> { new ViewPage { name = "Page" }, new ViewPage { name = "@Page" } },
                new List<ViewState>(), null));
        }

        [Test]
        public void LegacyDataCanBeMigratedWithoutAWindow()
        {
            data.viewPages.Add(new ViewPageSaveData(Vector2.zero, new ViewPage { name = "Legacy" }));
            Assert.IsTrue(data.RequireMigration());
            reader.MigrateToNewSaveData();
            reader.SaveData();
            Assert.IsFalse(data.RequireMigration());
            Assert.AreEqual("Legacy", data.GetViewPageSaveDatas().Single().viewPage.name);
        }

        [Test]
        public void DeletingAStateClearsPageLinksAndDeletingAPageRemovesItsAsset()
        {
            var state = new ViewState { name = "Shared" };
            reader.OnViewStateAdd(Vector2.zero, state);
            var page = AddPage(state: "Shared");
            string pageAsset = AssetDatabase.GetAssetPath(data.viewPagesNodeSaveDatas.Single());
            reader.DeleteViewState(state);
            Assert.IsEmpty(page.viewState);
            Assert.IsEmpty(data.viewStateNodeSaveDatas);
            reader.DeleteViewPage(page);
            reader.SaveData();
            Assert.IsEmpty(data.viewPagesNodeSaveDatas);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<ViewPageNodeSaveData>(pageAsset));
        }

        [Test]
        public void SavingSubAssetNodesDoesNotRenameOrDeleteTheMainGraph()
        {
            var node = ScriptableObject.CreateInstance<ViewPageNodeSaveData>();
            node.data = new ViewPageSaveData(Vector2.zero, new ViewPage { name = "Embedded" });
            AssetDatabase.AddObjectToAsset(node, data);
            data.viewPagesNodeSaveDatas.Add(node);
            reader.SaveData();
            Assert.AreEqual(folder + "/CustomGraph.asset", AssetDatabase.GetAssetPath(data));
            Assert.AreEqual("ViewPage_Embedded", node.name);
            reader.DeleteViewPage(node.data.viewPage);
            reader.SaveData();
            Assert.AreSame(data, AssetDatabase.LoadAssetAtPath<ViewSystemSaveData>(folder + "/CustomGraph.asset"));
        }

        [Test]
        public void AddressableGenerationPreservesRuleManagedEntriesAndSourcePrefabReferences()
        {
            var previous = AddressableAssetSettingsDefaultObject.Settings;
            var existingDefaults = new HashSet<string>(AssetDatabase.FindAssets("t:AddressableAssetSettingsDefaultObject"));
            EditorBuildSettings.TryGetConfigObject(AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName,
                out AddressableAssetSettingsDefaultObject previousDefault);
            bool createdDefaultFolder = !AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
            if (createdDefaultFolder)
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder));
            try
            {
                var settings = AddressableAssetSettings.Create(folder, "Addressables", true, true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
                var page = AddPage();
                var prefab = page.viewPageItems[0].viewElementObject;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
                data.globalSetting.useAddressableLoading = true;
                Assert.Throws<InvalidOperationException>(() => reader.SaveData());
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<ViewSystemSaveData_Addressable>(
                    folder + "/CustomGraph_Addressable.asset"));
                var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                entry.address = "rules/lobby";
                entry.SetLabel("RulesLabel", true, true);
                var ruleGroup = entry.parentGroup;
                data.globalSetting.addressableGroupName = "NotManagedByViewSystem";
                data.globalSetting.useAddressableLoading = true;

                reader.SaveData();
                var generated = AssetDatabase.LoadAssetAtPath<ViewSystemSaveData_Addressable>(
                    folder + "/CustomGraph_Addressable.asset");
                Assert.IsNotNull(generated);
                Assert.AreEqual("rules/lobby", settings.FindAssetEntry(guid).address);
                Assert.AreSame(ruleGroup, entry.parentGroup);
                Assert.That(entry.labels, Does.Contain("RulesLabel"));
                Assert.AreSame(prefab, page.viewPageItems[0].viewElementObject);
                var runtimeNode = generated.viewPagesNodeSaveDatas.Single();
                Assert.AreNotSame(data.viewPagesNodeSaveDatas.Single(), runtimeNode);
                Assert.IsNull(runtimeNode.data.viewPage.viewPageItems.Single().viewElementObject);
                Assert.AreEqual(guid, generated.viewPageItemAssetRefs.Single().assetReference.AssetGUID);

                reader.SaveData();
                Assert.AreSame(runtimeNode, generated.viewPagesNodeSaveDatas.Single(), "Generated node identity should remain stable.");
                data.globalSetting.useAddressableLoading = false;
                reader.SaveData();
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<ViewSystemSaveData_Addressable>(
                    folder + "/CustomGraph_Addressable.asset"));
                Assert.IsNotNull(settings.FindAssetEntry(guid), "Layout-rule entries must survive disabling runtime generation.");
            }
            finally
            {
                AddressableAssetSettingsDefaultObject.Settings = previous;
                if (previousDefault != null)
                    EditorBuildSettings.AddConfigObject(AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName, previousDefault, true);
                else
                    EditorBuildSettings.RemoveConfigObject(AddressableAssetSettingsDefaultObject.kDefaultConfigObjectName);
                foreach (var guid in AssetDatabase.FindAssets("t:AddressableAssetSettingsDefaultObject"))
                    if (!existingDefaults.Contains(guid)) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
                if (createdDefaultFolder)
                    AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
            }
        }
    }
}
