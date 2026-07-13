# ViewElement Pool Policy 與 Lifetime 管理設計提案

## 背景

目前 ViewSystem 的非 unique `ViewElement` 預設永久保留在 runtime pool。這能降低重複 Instantiate 的成本，但大型或低頻 UI 會在第一次開啟後永久占用 GameObject、MonoBehaviour、Graphic、layout cache 與 Addressable dependency。

只替大型 prefab 設定 `DestroyOnRecovery` 並不足夠。永久 pool 可能掩蓋以下既有生命週期問題：

- 訂閱外部 event 後沒有退訂。
- async callback 返回時，擁有者已被永久銷毀。
- 父 ViewElement 建立的 `ViewElementRequestedPool` 沒有 owner，父物件銷毀後子物件仍留在全域 pool。
- static collection、delegate 或 callback 持有已銷毀的 component。
- nested unique / singleton ViewElement 不能跟著一般 parent 任意銷毀。

因此需要同時建立：

1. Pool Policy Advisor：量測並建議適合的 recovery policy。
2. 明確的 ViewElement lifetime contract。
3. Owner-aware requested pool。
4. 靜態與 runtime safety verifier。

## 設計目標

- 大量分析 ViewElement，不再逐 prefab 人工試錯。
- 保持現有 `KeepForever` 行為相容。
- 能判斷高信心的安全候選，也能拒絕自動修改不安全對象。
- 同時考慮 retained memory、Instantiate 成本與使用頻率。
- 支援 `ViewElementBehaviour`、一般 `MonoBehaviour` 與已有其他 base class 的 UI。
- Addressable handle ownership 與 instance ownership 最終能一致管理。

## 非目標

- 不以 GameObject 數量作為唯一決策依據。
- 不承諾只靠 reflection 就能證明任意業務程式可安全銷毀。
- 不在第一階段自動批次修改 prefab。
- 不改變 unique ViewElement 的既有 ownership stack 語意。

## Recovery Policy

建議保留三種基本 policy：

```csharp
public enum ViewElementRecoveryPolicy
{
    KeepForever,
    KeepN,
    DestroyOnRecovery,
}
```

初步判斷規則：

| 特徵 | 建議 | 備註 |
|---|---|---|
| `IViewElementSingleton`、unique 或含 nested unique | `KeepForever` | 除非先完成明確 ownership 遷移 |
| 啟動必用、頻繁切換、Instantiate 昂貴 | `KeepN(1)` 或較小 N | 以實機 frame time 驗證 |
| 大型、低頻、無 unique、可安全銷毀 | `DestroyOnRecovery` | 優先節省 retained memory |
| 小型、短時間反覆使用的 cell | `KeepN(N)` | N 由觀測到的並行需求推算 |
| parent-owned child/cell | `DestroyWithOwner` ownership | 不應只依賴獨立全域 policy |
| 存在未管理 event/async/static callback | `UnsafeToDestroy` | 這是 Advisor 狀態，不是 runtime policy |

## Pool Policy Advisor

Advisor 應整合 Editor prefab scan 與 runtime object graph。每個 source prefab 至少輸出：

- prefab GUID / RuntimeKey / source name。
- recovery policy 與 keep count。
- active、queued、pending recovery instance count。
- hierarchy GameObject、Transform、MonoBehaviour、Graphic、Selectable、LayoutGroup 數量。
- nested ViewElement、unique、singleton 數量。
- 首次與重開 Instantiate 時間。
- open/recovery 次數與最近使用時間。
- queued retained memory 的估計值；若無可靠 native memory API，需標記為 proxy。
- Addressable valid handle、duplicate handle 與 dependency ownership。
- safety verifier 結果。
- 建議 policy、理由與 confidence。

建議輸出分類：

- `SafeAutomaticCandidate`
- `NeedsOwnerMigration`
- `NeedsEventCleanup`
- `NeedsAsyncLifetime`
- `NeedsStaticOwnership`
- `PinnedByUniqueOrSingleton`
- `InsufficientRuntimeData`

第一階段只提供 report、排序與 dry run。只有 `SafeAutomaticCandidate` 且達到最低 confidence 的對象，後續才可開放批次套用。

## ViewElementLifetimeScope

核心 lifetime 不應只存在於 `ViewElementBehaviour`，因為 FancyScrollRect 等 component 已繼承其他 base class。建議由每個 runtime `ViewElement` 擁有可組合的 scope：

```csharp
public sealed class ViewElementLifetimeScope : IDisposable
{
    public bool IsDisposed { get; }
    public CancellationToken Token { get; }

    public void AddCleanup(Action cleanup);
    public void Own(ViewElementRequestedPool pool);
    public void Dispose();
}
```

永久銷毀 ViewElement 時，ViewSystem 統一執行：

1. 將 scope 標記為 disposing，禁止新的 request/subscription。
2. cancel lifetime token。
3. 執行 event、delegate、callback cleanup。
4. recovery 或 dispose owned requested pools。
5. 清除 ViewElement lifecycle registration 與 runtime pool bookkeeping。
6. Destroy hierarchy。

`ViewElementBehaviour` 可提供便利屬性，但不是唯一入口：

```csharp
protected ViewElementLifetimeScope Lifetime { get; private set; }
```

其他 component 可從 parent `ViewElement` 取得同一個 scope。

## Async Lifetime

新 API 應優先接受 `CancellationToken`：

```csharp
var data = await LoadAsync(Lifetime.Token);
Lifetime.ThrowIfDisposed();
Apply(data);
```

對不能取消的既有 Task，可提供統一 helper：

```csharp
var result = await task;
if (!Lifetime.IsAlive)
    return;
```

目標是淘汰每個 UI 自行維護 `_isDestroyed`。Analyzer 應警告：

- `async void`（Unity event handler 等必要入口除外）。
- await 後操作 UnityEngine.Object，但沒有 lifetime/cancellation guard。
- fire-and-forget task 沒有 exception handling 或 lifetime binding。

## Event Subscription

ViewSystem 無法可靠地自動攔截任意 C# `+=`，因此需提供 scope-bound registration：

```csharp
Lifetime.Subscribe(
    add: handler => system.OnChanged += handler,
    remove: handler => system.OnChanged -= handler,
    handler: RefreshView);
```

Dispose 時由 scope 自動退訂。Verifier 同時掃描常見的 `+=` / `-=` 不對稱情況；掃描只能作為警告，不能取代 runtime ownership contract。

## Owner-aware ViewElementRequestedPool

目前 `new ViewElementRequestedPool(template)` 沒有表達 owner。建議新增 additive API：

```csharp
var pool = Lifetime.CreatePool(
    template,
    ViewElementChildRecoveryMode.DestroyWithOwner);
```

或：

```csharp
var pool = new ViewElementRequestedPool(template, ownerViewElement, childRecoveryMode);
```

Child recovery mode 建議包含：

- `ReturnToGlobalPool`
- `DestroyWithOwner`
- `UseChildPolicy`

父 ViewElement 一般 recovery 到 pool 時，owned child pool 可照常回收；只有父物件永久 Destroy 時才套用 child recovery mode。

這可避免大型 parent root 被銷毀，但其 cell 全部轉移到 global pool，導致 retained memory 幾乎沒有下降。

## Addressable Ownership

Prefab handle 不應由每個 page item 各自永久持有。長期方向是建立以 RuntimeKey/GUID 為 key 的共享 registry：

- 合併同 prefab 的重複 OperationHandle。
- 記錄 loading、active、pooled、unique pinned reference count。
- 只有所有 count 都為零時才能 release handle。
- `Object.Instantiate(prefab)` 建立的 clone 留在 pool 時，不能先 release prefab/dependencies。
- owner-aware pool 銷毀最後一個 clone 後，才能讓 registry 評估 release。

Handle release 應晚於 instance ownership 與 pool policy 穩定，不宜先獨立導入。

## Safety Verifier

可整合進既有 ViewSystem Verifier，檢查：

- nested unique / singleton。
- 外部 event 訂閱是否已註冊 cleanup。
- async lifetime guard。
- static field/collection 持有 component 的可疑路徑。
- `ViewElementRequestedPool` 是否有 owner。
- callback、UnityEvent runtime listener 的 cleanup ownership。
- Animator/transition/pending recovery 是否仍在執行。
- Addressable handle 是否仍被 active/pooled instance 使用。

若 prefab 設為 `DestroyOnRecovery` 但 verifier 判定 unsafe，Editor 應顯示 error；runtime 可選擇保守 fallback 到 `KeepForever` 並記錄一次 warning，避免正式環境直接破壞 UI。

## 導入計畫

### Phase 1：只讀 Advisor

- 把現有 object graph 診斷移植為 package 內部、host-agnostic 工具。
- 建立 JSON schema、candidate ranking 與 dry run。
- 不修改 prefab。

目前 v1 Editor 工具入口：`MacacaGames > ViewSystem > Diagnostics > Pool Policy Advisor`。

Advisor Window 的候選集合只來自指定 `ViewSystemSaveDataBase`：direct SaveData 解析 page、state 與 unique table 的 prefab reference；Addressable SaveData 解析 page item 與 unique element 的 AssetReference GUID。未被 SaveData 引用的 prefab 不納入分析。相同 prefab 在多個 page/state 被使用時只分析一次，報告保留所有 reference location。

報告輸出到 host project 的 `MemoryLeakReports/viewsystem_pool_policy_advisor_*.json`。初次 Analyze 只使用 prefab hierarchy 與 component script 靜態訊號，固定標示 `dryRun=true`、`safeToApplyAutomatically=false`；大型 hierarchy 的 `DestroyOnRecovery` 只屬 provisional recommendation，必須再合併 runtime 使用頻率、active/queued/pending 與 reopen 成本後才能成為 `SafeAutomaticCandidate`。

靜態 safety scan 分成 lifetime owner 與 supporting component 兩層。只有實作 ViewElement lifecycle/singleton、繼承 `ViewElementBehaviour`，或直接使用 lifetime/requested-pool ownership API 的 script 可以阻擋建議；UGUI、localization、視覺效果等 supporting component 的 static/Addressable 訊號只列為非阻擋 evidence。JSON schema v2 會為每種命中輸出 script path 與 line number，避免只看到無法追查的 aggregate count。

JSON schema v4 與 Advisor Window 可逐次加入多份 `viewsystem_object_graph_*.json` runtime snapshot。Object graph pool entry 會輸出 prefab GUID/path，以及 active、queued、pending 三種狀態各自的完整 hierarchy GO/MonoBehaviour 數量。Window 優先使用穩定 identity 合併，並保留每份觀測、顯示跨快照峰值與「曾出現但在較後快照消失」；舊報告只能在 source name 唯一時 fallback，並標記低可信度。active hierarchy 指標會包含巢狀 requested-pool child，適合評估單一 root 的展開成本，但不同 pool source 間可能重複計數，不可直接加總。多份 snapshot 仍只是觀測證據；尚未量到足夠的 reopen cycle、使用頻率與 reopen cost 前，不得升級為 `SafeAutomaticCandidate`。

JSON schema v5 將 policy 適配度與 migration safety 分離：`targetPolicy` / `targetKeepCount` 表示記憶體與使用型態上的目標，`migrationStatus` / `safetyBlockers` / `nextAction` 表示目前還需要的 code ownership 或 runtime 驗證工作，`policyConfidence` 與 `safetyConfidence` 分別計分。`NeedsCodeMigration`、`NeedsCodeReview` 或 `NeedsRuntimeValidation` 不得把 target fallback 成 KeepForever；現有 KeepN 或 DestroyOnRecovery 一律視為 intentional migration，靜態掃描只能要求 review/validation，不能建議回退。舊 `classification`、`recommendedPolicy` 與 `confidence` 暫時保留為相容 alias。

### Phase 2：Lifetime Scope

- 在 `ViewElement` 建立 scope。
- 將 Destroy pipeline 統一收斂到 runtime pool。
- 提供 cancellation、cleanup 與 subscription API。
- 保持既有 lifecycle API 相容。

### Phase 3：Owner-aware Requested Pool

- 新增 owner/child recovery mode API。
- 舊 constructor 保留並標記為無 ownership 的 legacy path。
- Verifier 列出所有 legacy pool 使用點。

### Phase 4：試點遷移

- 選擇大型、低頻、nested unique=0 的數個 ViewElement。
- 驗證首次開啟、返回、第二次開啟、功能、圖片與 Console。
- 量測 memory 降幅與重開 frame spike。

### Phase 5：批次建議與套用

- Advisor 只對高信心候選提供批次套用。
- 每次批次變更保留 manifest，方便回滾與 A/B 比較。
- 實機 4 GB tier 加入 memory budget 與 reopen latency gate。

### Phase 6：Addressable Handle Registry

- 合併 duplicate handles。
- 連接 active/pooled/owner/unique reference count。
- 驗證 dependency 不會過早卸載。

## 驗證標準

每個 policy 變更至少驗證：

1. 穩定頁面的 baseline。
2. 首次開啟目標頁。
3. 返回後等待 transition 與 recovery 完成。
4. 第二次開啟並完整操作主要功能。
5. 再次返回並比較 retained object count。
6. Console 無 MissingReference、NullReference、重複 callback 或未觀察 task exception。
7. active instance 不重複；queued/pending 不逐輪成長。
8. 圖片、Addressable dependency、動畫與注入資料正常。
9. 實機量測 reopen frame time，避免以明顯卡頓交換記憶體。

## 已知限制

- 靜態分析無法證明所有外部 delegate、網路 callback 或第三方 SDK callback 的 ownership。
- Unity managed shell、native object 與 Addressable dependency 的實際 byte 數不宜只從物件數推算。
- Editor Instantiate timing 不能取代目標手機的實機量測。
- 某些 UI 即使很大，若開啟頻率極高，仍可能應採 `KeepN(1)`。
- unique/singleton 的行為屬於架構契約，不應由一般 memory heuristic 自動改寫。
