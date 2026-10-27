# 不開啟 Visual Editor 視窗編輯 ViewSystem 資料

[English](WindowlessAuthoring.md)

`ViewSystemDataReader.Open(...)` 可以直接開啟既有的編輯用資料資產，不會建立 `ViewSystemVisualEditor` 實例、載入預覽場景或顯示對話框。這套 API 位於 Editor assembly，可由專案的 Editor 腳本、Unity MCP 的 C# 執行功能，或 Editor 批次工作呼叫，本身不依賴 MCP 或特定遊戲專案。

## 編輯、驗證、儲存與烘焙

以下是 Editor C# 呼叫範例。請將資料資產與 ViewElement Prefab 路徑替換成專案中的實際位置。Unity 資產操作必須在 Editor 主執行緒執行。

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

其他欄位可直接透過既有的 `ViewPage`、`ViewState`、`ViewPageItem`，以及 Transform、Override 與事件資料模型編輯。`OnViewStateAdd` 可建立狀態資產；設定頁面的 `viewState` 名稱，即可連結對應的 ViewState。`ViewPageItem` 產生的 ID 必須保持唯一。若互動式編輯需要支援 Undo，請在修改模型前，對其所屬節點資產呼叫 `Undo.RecordObject`。

`DeleteViewPage(page)` 與 `DeleteViewState(state)` 會移除對應的節點資產。刪除 ViewState 時，也會清除頁面對該狀態的連結。節點可儲存為獨立資產或子資產（sub-asset）；移除子資產不會刪除整份主要資料資產。完成這些修改後，請呼叫 `SaveData()`。

新節點資產會放在指定資料資產的同一個資料夾，不再固定寫入預設的資源目錄。既有來源資產與 GUID 會保留。產生的執行期資料也位於相同資料夾，名稱為 `<authoring-name>_Addressable.asset`。

## 儲存與相關方法的行為

- `SaveData()` 會儲存節點資產、重建 Unique 表，並依照 `useAddressableLoading` 產生或移除 Addressable 執行期副本。它不會清理預覽物件，也不會將場景中的 UI Root 套用至 Prefab。
- 透過 `Open` 取得的 reader，呼叫 `Save()` 時會轉呼叫 `SaveData()`。由 Visual Editor 建立的 reader 則保留原有的預覽儲存流程，再呼叫同一份儲存核心。
- `Validate()` 回傳可序列化的 `ViewSystemValidationResult`，包含 `isValid` 與 `issues`。每筆問題包含 `code`、`owner` 與 `message`，不會開啟修復視窗或確認對話框。檢查範圍包含生成名稱、節點與項目引用、狀態連結、ID，以及 Override／事件的目標。驗證由呼叫端明確執行；儲存草稿時，不要求所有可選綁定都已完成。
- 舊版資料請先呼叫 `MigrateToNewSaveData()`，再呼叫 `SaveData()`。遷移同樣可以透過無視窗方式開啟的 reader 執行。
- `Bake(path)` 會將目前模型烘焙至指定的 C# 檔案，路徑以 `Assets/` 為起點。省略路徑時，會在資料資產旁產生 `ViewSystemScriptable.cs`。同一個 assembly 應只編譯一份生成的 `ViewSystemScriptable` 定義；專案有多份資料圖時，請明確指定要使用的資料資產與輸出檔案。

這些資料資產仍可透過 Visual Editor 重新開啟並繼續編輯。如果視窗原本已載入相同資料，請在外部修改後重新整理或開啟視窗，讓節點顯示與資料同步。

## 由 Layout Rule 管理的 Addressable 項目

`SaveData()` 預設會保留既有的 Addressable 地址、群組與 Label。儲存啟用 Addressable 的資料前，請先套用專案的 Layout Rule，例如 SmartAddresser 規則。所有被引用的 ViewElement Prefab 都必須已有可解析的 Addressable 項目；若缺少項目，會在產生執行期資料前拋出例外。停用執行期資料生成時，會移除生成的副本，並保留由規則管理的項目。

`SaveData(updateAddressableEntries: true)` 會明確啟用 ViewSystem 原有的群組／Label 指派及清理行為。目前 Visual Editor 沿用這項既有策略；使用外部 Layout Rule 的專案，在自動化流程中應保留預設的 `false`。

## 只產生烘焙內容，不寫入檔案

`ViewSystemScriptBaker.GenerateSource(pages, states, breakPoints)` 會回傳生成的 C# 原始碼。`BakeAllViewPageName` 也提供可指定輸出路徑的多載，兩者與 Visual Editor 共用相同的程式碼生成邏輯。若 C# 識別字無效、重複，或與其所屬型別名稱衝突，會在寫入原始碼檔案前報錯。

## 測試

插件的 `Tests/Editor` 內含 EditMode 整合測試。請在宿主專案 manifest 的 `testables` 清單加入 `com.macacagames.viewsystem`，再透過 Unity Test Runner 執行。測試涵蓋無視窗新增／編輯、儲存／烘焙、資料遷移、驗證、預覽物件隔離，以及保留規則管理項目、獨立複製節點的 Addressable 執行期資料生成。
