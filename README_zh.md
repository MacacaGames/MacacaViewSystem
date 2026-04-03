# ViewSystem

> [English](./README.md) | **中文**

**ViewSystem** 是一套基於元素（Element）的 Unity UI 管理系統，由 [Macaca Games](https://github.com/MacacaGames) 開發。
透過視覺化節點編輯器、元素物件池與執行時期屬性/事件覆寫，大幅簡化複雜的 UI 工作流程。

### 為什麼需要 ViewSystem？

Unity 的 UI 開發經常面臨佈局、邏輯與資料高度耦合的問題，而工作流程往往瓶頸在工程師身上。設計師交付設計稿後，工程師在 Unity 中重建佈局，「設計意圖」與「實作結果」之間的落差導致無止盡的來回修改 — 間距差幾個 pixel、色碼不對、動畫節奏不一致。

ViewSystem 透過**跨角色的關注點分離**來解決這個問題：

- **ViewElement** 定義 UI 元素的*外觀與動畫方式* — **不決定**放在哪裡。
- **ViewPage** 定義元素的*擺放位置* — 可在 Visual Editor 中配置，不需要寫程式。
- **Runtime Override** 讓設計師直接在編輯器中針對不同頁面調整屬性（文字、顏色、圖片、佈局）— 不需要建立 Prefab Variant，不需要改程式碼。
- **Model Injection** 讓工程師專注於資料流與邏輯，視覺配置留在編輯器中處理。

最終效果：**設計師掌控視覺細節，工程師掌控行為與資料** — 各自在擅長的領域工作，將協作摩擦降到最低。

## 使用案例

<img src="./Img~/skySurfing.gif">
<img src="./Img~/skyBandit.gif">
<img src="./Img~/rythemGo.gif">

## 功能特色

- **元素式架構** — 以可重用的 ViewElement 組合 UI 頁面
- **ViewElement 物件池** — 自動管理物件池，優化效能
- **執行時期屬性與事件覆寫** — 無需建立 Prefab Variant 即可為不同頁面建立 ViewElement 變體
- **節點式視覺編輯器** — 直接在編輯器中設計與預覽 UI 頁面
- **Fluent API** — 以鏈式語法撰寫頁面切換，簡潔易讀
- **生命週期鉤子與依賴注入** — `IViewElementLifeCycle`、`ViewElementBehaviour`、`[ViewElementInject]`
- **Safe Area 支援** — 可針對單一頁面或全域設定安全區域
- **Breakpoint 系統** — 依據命名斷點自適應調整 ViewElement 的 Transform

## 安裝

**方式一：OpenUPM（推薦）**

```sh
openupm add com.macacagames.viewsystem
```

**方式二：Unity Package Manager（Git URL）**

在 `Packages/manifest.json` 中加入：

```json
{
    "dependencies": {
        "com.macacagames.utility": "https://github.com/MacacaGames/MacacaUtility.git",
        "com.macacagames.viewsystem": "https://github.com/MacacaGames/MacacaViewSystem.git"
    }
}
```

**方式三：Git Submodule**

```bash
git submodule add https://github.com/MacacaGames/MacacaViewSystem.git Assets/MacacaViewSystem
git submodule add https://github.com/MacacaGames/MacacaUtility.git Assets/MacacaUtility
```

## 核心概念

### ViewElement

ViewSystem 的基本單位。頁面上的任何 UI 項目（按鈕、圖示、面板等）都可以是一個 ViewElement。

<img src='./Img~/viewelement.png' alt="ViewElement 範例" height="400">

ViewElement 定義了**如何**出現與消失，但**不決定**放置的位置。共有 5 種轉場類型：

| 轉場方式 | 說明 |
|---|---|
| **Animator** | 觸發 Animator 狀態（Show、Loop、Leave） |
| **Canvas Group Alpha** | 透過 Tween 調整 CanvasGroup 的 Alpha 實現淡入/淡出 |
| **Active Switch** | 切換 `GameObject.activeSelf` |
| **ViewElement Animation** | 內建 Tween 動畫，控制 Transform（位置、旋轉、縮放）與 Alpha |
| **Custom** | 觸發 UnityEvent，完全自訂控制 |

### ViewPage

ViewPage 由一個或多個 ViewElement 組成，並定義每個 ViewElement **擺放的位置**。分為兩種類型：

- **FullPage** — 同時間只能顯示一個。切換頁面時會自動離開當前 FullPage 並顯示下一個。
- **OverlayPage** — 疊加顯示在當前畫面之上，可同時存在多個 OverlayPage。適合用於對話框、載入畫面等。

### ViewState

定義多個 ViewPage 之間共享的 ViewElement 佈局。每個 ViewPage 最多可參考一個 ViewState。ViewState 中的 ViewElement 會持續存在，直到 ViewState 本身改變。

### ViewController

核心單例元件，管理所有頁面切換與 ViewElement 生命週期。

## 快速上手

### 1. 設定 ViewController

在場景中建立一個 GameObject 並掛載 `ViewController` 元件，指定 `ViewSystemData` 資料。

### 2. 建立 UI Root

開啟 **MacacaGames > ViewSystem > Visual Editor**，點選 **Global Setting**，然後點 **Generate default UI Root Object**。

### 3. 建立 ViewElement

建立一個 UI 物件（Image、Button 等），掛載 `ViewElement` 元件，並儲存為 Prefab。

### 4. 建立 ViewPage

在 Visual Editor 中，右鍵 > **Add FullPage**，將你的 ViewElement 加入頁面的 `ViewPageItems`。

### 5. 顯示頁面

```csharp
public class GameStart : MonoBehaviour
{
    void Awake()
    {
        ViewController
            .FullPageChanger()
            .SetPage("TestPage")
            .Show();
    }
}
```

## 頁面切換 API

ViewSystem 透過 `PageChanger` 提供 **Fluent Interface**：

```csharp
// FullPage — 自動離開前一個頁面
ViewController
    .FullPageChanger()
    .SetPage("MyPage")
    .SetPageModel(someData, "hello")   // 注入資料至 ViewElement
    .SetIgnoreClickProtection(false)    // 啟用點擊防抖（預設 0.2 秒）
    .SetWaitPreviousPageFinish(true)    // 排隊等待而非取消切換
    .SetIgnoreTimeScale(true)           // 忽略 Time.timeScale
    .OnStart(() => Debug.Log("Start"))
    .OnComplete(() => Debug.Log("Done"))
    .Show();

// OverlayPage — 需手動 Show/Leave
ViewController
    .OverlayPageChanger()
    .SetPage("DialogPage")
    .Show();

// 離開 OverlayPage
ViewController
    .OverlayPageChanger()
    .SetPage("DialogPage")
    .Leave();
```

## 執行時期覆寫

無需建立 Prefab Variant，即可針對不同頁面覆寫 ViewElement 的任何屬性：

<img src="./Img~/override_demo.gif" />

### 透過 Visual Editor

在 ViewSystem Editor 的 Override Window 中直接設定屬性覆寫。

### 透過腳本（Attribute）

```csharp
public class MyUILogic : ViewElementBehaviour
{
    [OverrideProperty("Frame", typeof(Image), nameof(Image.sprite))]
    [SerializeField]
    Sprite someSprite;

    [OverrideButtonEvent("TopRect/Button")]
    void OnButtonClick(Component component)
    {
        Debug.Log("Clicked!");
    }
}
```

### 透過腳本（ViewElementOverride）

```csharp
public class MyScript : MonoBehaviour
{
    [SerializeField]
    ViewElementOverride myOverride;

    void ApplyOverride()
    {
        GetComponent<ViewElement>().ApplyOverrides(myOverride);
    }
}
```

### 事件覆寫

使用 `[ViewSystemEvent]` 標記方法，使其可在 Override Window 中選用：

```csharp
[ViewSystemEvent("MyGroup")]
public void OnConfirmClick(Component selectable)
{
    // 處理點擊
}
```

## 生命週期鉤子與注入

### IViewElementLifeCycle

在掛載於 ViewElement 的任何 MonoBehaviour 上實作：

```csharp
public class MyUI : MonoBehaviour, IViewElementLifeCycle
{
    public void OnBeforeShow() { }
    public void OnBeforeLeave() { }
    public void OnStartShow() { /* ViewElement 已可見 */ }
    public void OnStartLeave() { }
    public void OnChangePage(bool show) { }
    public void OnChangedPage() { }
    public void RefreshView() { }
}
```

### ViewElementBehaviour

實作 `IViewElementLifeCycle` 的便利基底類別，支援 Inspector 上的 UnityEvent：

```csharp
public class MyUI : ViewElementBehaviour
{
    public override void OnStartShow()
    {
        RefreshView();
    }
}
```

### Model 注入（`[ViewElementInject]`）

切換頁面時傳遞資料給 ViewElement：

```csharp
public class MyUILogic : ViewElementBehaviour
{
    [ViewElementInject]
    int score;

    [ViewElementInject]
    string playerName { get; set; }
}

// 透過 SetPageModel 傳遞資料
ViewController.FullPageChanger()
    .SetPage("ResultPage")
    .SetPageModel(99999, "Player1")
    .Show();
```

結合 Override 實現一行資料綁定：

```csharp
[ViewElementInject]
[OverrideProperty("Text", typeof(TextMeshProUGUI), nameof(TextMeshProUGUI.text))]
string displayText;
```

同一型別有多個值時，使用 `ViewInjectDictionary<T>`：

```csharp
var data = new ViewInjectDictionary<string>();
data.TryAdd("title", "Hello");
data.TryAdd("subtitle", "World");

ViewController.FullPageChanger()
    .SetPage("MyPage")
    .SetPageModel(data)
    .Show();
```

### Shared Model

`IViewElementSingleton` 的實例會自動成為 **Shared Model**，所有頁面皆可存取，無需呼叫 `SetPageModel()`：

```csharp
public class CurrencyDisplay : ViewElementBehaviour, IViewElementSingleton { }

// 任何地方都可注入，不需要 SetPageModel
public class ShopUI : ViewElementBehaviour
{
    [ViewElementInject]
    CurrencyDisplay currency; // 自動注入
}
```

透過 `InjectScope` 控制搜尋範圍：

```csharp
[ViewElementInject(InjectScope.SharedOnly)]
MyClass myData;
```

| 範圍 | 說明 |
|---|---|
| `PageFirst`（預設） | 先搜尋 PageModel，再搜尋 SharedModel |
| `PageOnly` | 僅搜尋 PageModel |
| `SharedFirst` | 先搜尋 SharedModel，再搜尋 PageModel |
| `SharedOnly` | 僅搜尋 SharedModel |

## 元件

### ViewElementGroup

將 OnShow/OnLeave 傳遞給子 ViewElement（類似 CanvasGroup）。啟用 **Only Manual Mode** 可從腳本控制顯示/隱藏：

```csharp
[SerializeField] ViewElement child;

child.OnShow(true);              // 手動顯示
child.OnLeave(false, true);      // 手動隱藏，不回收至物件池
```

## 視覺編輯器

透過 **MacacaGames > ViewSystem > Visual Editor** 開啟。

| 工具 | 說明 |
|---|---|
| **Edit Mode** | 在編輯模式下以臨時場景預覽 ViewPage |
| **Save** | 儲存所有變更並退出 Edit Mode |
| **Global Setting** | UI Root 生成、Safe Area 設定、Breakpoint、點擊保護間隔 |
| **Overlay Order** | 拖放調整 Overlay 頁面的排序 |
| **Bake to Script** | 產生 ViewPage/ViewState 名稱的 C# 常數 |
| **Verifiers** | 檢查遺失的 GameObject、Override、Event 及頁面參考 |

### Bake to Script

產生型別安全的頁面名稱常數：

```csharp
ViewController
    .OverlayPageChanger()
    .SetPage(ViewSystemScriptable.ViewPages.ConfirmDialog)
    .Show();
```

## 疑難排解

| 問題 | 解決方式 |
|---|---|
| ViewElement 的 `GameObject.activeSelf` 未正確套用 | 避免在 `OnBeforeShow()` 中設定 `activeSelf` — ViewElement 尚未就緒，ViewSystem 可能會覆寫它 |
| Hierarchy 中出現未命名場景 | 在 Edit Mode 期間進入 Play Mode 所導致，手動刪除即可 |
| 無法將 Prefab 拖入編輯器 | 確認 Prefab 已掛載 `ViewElement` 元件 |
| 生命週期事件未觸發 | 確認 `ViewElement` 元件存在。若腳本在子物件上，需在父層 ViewElement 加上 `ViewElementGroup` |
| 更新版本後編輯器顯示空白 | 點選工具列的 **Normalized** 來遷移儲存資料 |

## 連結

- [完整文件](https://macacagames.github.io/MacacaViewSystemDocs/)
- [原始碼](https://github.com/MacacaGames/MacacaViewSystem)
- [範例專案](https://github.com/MacacaGames/ViewSystemExample)
- [Script API 參考](https://macacagames.github.io/MacacaViewSystem/api/MacacaGames.ViewSystem.html)
