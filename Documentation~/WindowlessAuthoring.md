# Editing ViewSystem data without a Visual Editor window

[繁體中文](WindowlessAuthoring_zh.md)

`ViewSystemDataReader.Open(...)` opens an existing authoring asset directly. It does not create a `ViewSystemVisualEditor`, load a preview scene, or show dialogs. This API lives in the Editor assembly and can be called by project Editor scripts, Unity MCP C# execution, or an Editor batch job. It has no MCP or game-project dependency.

## Edit, validate, save, and bake

The following is an Editor C# snippet. Supply your own authoring asset and ViewElement prefab paths. Run Unity asset operations on the Editor main thread.

```csharp
using System;
using System.Linq;
using MacacaGames.ViewSystem;
using MacacaGames.ViewSystem.VisualEditor;
using UnityEditor;
using UnityEngine;

var reader = ViewSystemDataReader.Open("Assets/ViewSystemResources/ViewSystemData.asset");
var data = reader.GetSaveData();
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/ShopPanel.prefab");
if (prefab == null || prefab.GetComponent<ViewElement>() == null)
    throw new InvalidOperationException("Create and wire the ShopPanel ViewElement prefab first.");

var savedPage = data.GetViewPageSaveDatas().SingleOrDefault(entry => entry.viewPage.name == "Shop");
if (savedPage == null)
{
    reader.OnViewPageAdd(new Vector2(100, 200), new ViewPage
    {
        name = "Shop",
        viewPageType = ViewPage.ViewPageType.FullPage
    });
    savedPage = data.GetViewPageSaveDatas().Single(entry => entry.viewPage.name == "Shop");
}

var panel = savedPage.viewPage.viewPageItems.SingleOrDefault(item => item.name == "ShopPanel");
if (panel == null)
{
    panel = new ViewPageItem { name = "ShopPanel" };
    savedPage.viewPage.viewPageItems.Add(panel);
}
panel.viewElementObject = prefab;
panel.defaultTransformDatas.rectTransformData.sizeDelta = new Vector2(1080, 1920);

var result = reader.Validate();
Debug.Log(JsonUtility.ToJson(result, true));
if (!result.isValid)
    throw new InvalidOperationException("Fix the reported authoring issues before saving.");

reader.SaveData();
reader.Bake("Assets/ViewSystemResources/ViewSystemScriptable.cs");
```

Use the existing `ViewPage`, `ViewState`, `ViewPageItem`, transform, override, and event models to edit other fields. `OnViewStateAdd` creates state assets; assigning a page's `viewState` links it to a state by name. IDs created by `ViewPageItem` must remain unique. For interactive Undo support, record the affected node asset with `Undo.RecordObject` before editing its model.

`DeleteViewPage(page)` and `DeleteViewState(state)` remove the corresponding node assets. Deleting a state clears page links to that state. Both standalone node assets and nodes stored as sub-assets are supported; removing a sub-asset does not delete the main graph. Call `SaveData()` after these edits.

New node assets are stored next to the specified authoring asset, rather than always writing into the default resource folder. Existing source assets and their GUIDs are retained. Generated runtime data uses the same folder and `<authoring-name>_Addressable.asset`.

## Save semantics

- `SaveData()` persists node assets, rebuilds the Unique table, and generates or removes the Addressable runtime copy according to `useAddressableLoading`. It does not clean up preview objects or apply a scene UI root to a prefab.
- `Save()` on a reader returned by `Open` calls `SaveData()`. A reader constructed for the Visual Editor retains its existing preview-aware Save behavior, followed by the same persistence core.
- `Validate()` returns a serializable `ViewSystemValidationResult` with `isValid` and `issues`. Each issue contains `code`, `owner`, and `message`; no fixer window or confirmation dialog is opened. It checks generated names, node/item references, state links, IDs, and authored override/event targets. Validation is explicit; saving a draft does not require every optional binding to be complete.
- For legacy data, call `MigrateToNewSaveData()` before `SaveData()`. The migration can run on a reader opened without a window.
- `Bake(path)` bakes the current models to the supplied Assets-relative C# path. Omitting the path writes `ViewSystemScriptable.cs` next to this data asset. Only one generated `ViewSystemScriptable` definition should be compiled in an assembly; choose the intended authoring asset and output file explicitly when multiple graphs exist.

The Visual Editor can reopen these authoring assets and continue editing them. Refresh or reopen an already-loaded window after external editing so it rebuilds its node views.

## Addressable entries owned by layout rules

By default, `SaveData()` preserves existing Addressable addresses, groups, and labels. Apply the project's layout rules, such as SmartAddresser, before saving an Addressable graph. All referenced ViewElement prefab assets must already have resolvable entries; missing entries produce an exception before generating runtime data. Disabling runtime generation removes the generated copy but preserves rule-owned entries.

`SaveData(updateAddressableEntries: true)` explicitly opts into ViewSystem's existing group/label assignment and cleanup behavior. The existing Visual Editor uses that legacy policy. Projects using external layout rules should keep the default `false` policy for automation.

## Baking without writing files

`ViewSystemScriptBaker.GenerateSource(pages, states, breakPoints)` returns the generated C# source. `BakeAllViewPageName` also has an overload accepting an explicit output path. Both use the same source generation as the Visual Editor. Invalid, duplicate, or enclosing-type-conflicting C# identifiers fail before a source file is written.

## Tests

The package includes EditMode integration tests under `Tests/Editor`. Add `com.macacagames.viewsystem` to the host manifest's `testables` list and use Unity Test Runner. The tests exercise window-free creation/editing, save/bake, migration, validation, preview isolation, and independent Addressable runtime node copies with preserved rule-owned entries.
