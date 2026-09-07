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

## Policy 遷移偽代碼

以下流程描述「證據收集、Agent code migration、Unity policy 套用、runtime 驗證」的責任邊界。Advisor 不應把 safety blocker 自動轉譯成 `KeepForever`；`targetPolicy` 是策略目標，`migrationStatus` 是目前能否安全前進的狀態。

### Advisor：產生單一候選的 v5 decision

```text
function AnalyzeCandidate(prefab, saveData, runtimeSnapshots):
    assert prefab is referenced by saveData

    advice = ScanPrefabHierarchyAndScripts(prefab)
    advice.currentPolicy = prefab.ViewElement.recoveryPolicy
    advice.currentKeepCount = prefab.ViewElement.recoveryKeepCount
    advice.runtime = MergeSnapshotsByPrefabGuidOrPath(runtimeSnapshots, prefab)

    # 先決定「希望採用什麼 policy」，不要先看 safety blocker
    if advice.currentPolicy != KeepForever:
        advice.targetPolicy = advice.currentPolicy
        advice.targetKeepCount = advice.currentKeepCount
        advice.policyConfidence = 0.90
    else if advice.gameObjects >= LARGE_HIERARCHY_THRESHOLD:
        advice.targetPolicy = DestroyOnRecovery
        advice.targetKeepCount = 0
        advice.policyConfidence = 0.65
    else if advice.gameObjects >= MEDIUM_HIERARCHY_THRESHOLD:
        advice.targetPolicy = KeepN
        advice.targetKeepCount = 1
        advice.policyConfidence = 0.55
    else:
        advice.targetPolicy = Undetermined
        advice.targetKeepCount = 0
        advice.policyConfidence = 0.35

    advice.safetyBlockers = VerifyOwnership(advice)

    if advice.hasUniqueOrSingleton:
        advice.targetPolicy = KeepForever
        advice.targetKeepCount = 0
        advice.migrationStatus = Pinned
        advice.nextAction = "Preserve unique/singleton ownership"
        advice.safetyConfidence = 0.95
    else if advice.safetyBlockers is not empty:
        advice.migrationStatus =
            advice.currentPolicy != KeepForever ? NeedsCodeReview : NeedsCodeMigration
        advice.nextAction = "Fix or review the cited ownership paths"
        advice.safetyConfidence = 0.25
    else if advice.currentPolicy != KeepForever:
        advice.migrationStatus = NeedsRuntimeValidation
        advice.nextAction = "Preserve current policy and validate reopen cycles"
        advice.safetyConfidence = 0.65
    else if advice.targetPolicy == Undetermined:
        advice.migrationStatus = InsufficientEvidence
        advice.nextAction = "Collect runtime cost and usage evidence"
        advice.safetyConfidence = 0.55
    else:
        advice.migrationStatus = NeedsRuntimeEvidence
        advice.nextAction = "Collect open/return/reopen snapshots and review ownership"
        advice.safetyConfidence = 0.55

    return advice
```

### Agent：處理一個需要遷移的候選

```text
function MigrateOneCandidate(advisorReport):
    candidate = SelectOne(advisorReport.entries,
        status in [NeedsCodeMigration, NeedsCodeReview, NeedsRuntimeEvidence])
    if candidate is null:
        return "No candidate requiring migration"

    Read(AGENTS.md, CLAUDE.md, design docs, case study)
    InspectDirtyWorktreeWithoutOverwritingUserChanges()
    InspectCitedSourceLines(candidate.signalEvidence)

    if candidate.migrationStatus == NeedsCodeMigration:
        AddLifetimeCleanupAndAsyncGuards(candidate)
        ReplaceOwnerlessRequestedPool(candidate,
            owner.Lifetime.CreatePool(template, DestroyWithOwner))
        BindEventsToScope(candidate,
            owner.Lifetime.Subscribe(add, remove, handler))
        CompileAffectedAssembly()

    if candidate.targetPolicy != Undetermined and NoBlockingCodeIssue(candidate):
        # prefab/asset 寫入只透過 Unity Editor API 或 Inspector
        plan = {
            prefabGuid: candidate.prefabGuid,
            expectedCurrentPolicy: candidate.currentPolicy,
            targetPolicy: candidate.targetPolicy,
            targetKeepCount: candidate.targetKeepCount,
        }
        ApplyPolicyThroughUnityEditor(plan)

    return "Capture runtime validation snapshots"
```

### Runtime：驗證 recovery 與 owner disposal

```text
function ValidateCandidate(candidate):
    snapshots = [
        DumpObjectGraph(baselinePage),
        DumpObjectGraph(afterOpen(candidate)),
        DumpObjectGraph(afterReturnAndRecoverySettles()),
        DumpObjectGraph(afterReopen(candidate)),
        DumpObjectGraph(afterSecondReturnAndRecoverySettles()),
    ]

    assert no MissingReferenceException / NullReferenceException / duplicate callback
    assert queuedAndPendingCountsDoNotGrowAcrossReopen(snapshots)
    assert ownerBoundChildrenDoNotRemainInGlobalPool(snapshots)
    assert contentButtonsImagesAndInjectedDataAreCorrectOnReopen()
    assert addressableHandleOwnershipIsUnchanged()

    if allChecksPass:
        return Validated
    return NeedsRuntimeValidation
```

`ViewElementLifetimeScope.Dispose` 只代表永久銷毀；一般 leave/recovery 不應觸發 scope cleanup。`DestroyWithOwner` child 也應先回到 owner-local queue，直到 owner 永久銷毀時才釋放完整 hierarchy。這兩個條件是判讀 Object Graph 快照時的必要前提。

## Agent 可執行、可驗證的記憶體優化方案

本方案的原則是：Editor 負責產生 Unity 才能取得的證據，Agent 負責理解 ownership 並修改程式，Unity Editor 負責套用 prefab policy，最後由 runtime snapshot 與功能測試決定是否完成。任何一層不能證明安全時，流程停在目前狀態，不以 `KeepForever` 偽裝成最佳解，也不擅自批次修改。

### 1. 固定輸入與輸出

Agent 每次只接收一個 Advisor report 與一個候選 prefab。必要輸入如下：

```text
advisor_report: MemoryLeakReports/viewsystem_pool_policy_advisor_*.json
candidate.prefabGuid
candidate.prefabPath
candidate.currentPolicy
candidate.targetPolicy
candidate.migrationStatus
candidate.safetyBlockers
candidate.signalEvidence
```

Agent 必須產生一份 migration manifest，並在修改前寫入預期狀態：

```json
{
  "schemaVersion": 1,
  "prefabGuid": "<stable-guid>",
  "prefabPath": "Assets/.../Candidate.prefab",
  "expectedCurrentPolicy": "KeepForever",
  "targetPolicy": "DestroyOnRecovery",
  "targetKeepCount": 0,
  "codeChanges": [],
  "childPoolModes": [],
  "safetyBlockers": [],
  "requiredValidation": [
    "baseline", "open", "return", "reopen", "second-return"
  ],
  "status": "Planned"
}
```

若 prefab GUID、path 或 current policy 與 report 不一致，manifest 必須標記 `StaleEvidence` 並停止；不得以名稱猜測或覆蓋目前設定。

### 2. 不變條件（Compatibility Invariants）

每一個候選在整個流程都必須維持：

- 沒有明確設定的 ViewElement 仍使用 `KeepForever`。
- public API 只做 additive change；既有 ownerless requested-pool constructor 保持原有 global-pool 行為。
- unique ViewElement、singleton 與既有 ownership stack 不被一般 parent policy 改寫。
- 不修改 FancyScrollRect、LayoutGroup 或頁面導覽規範來換取記憶體數字。
- Addressable handle 的 acquire/release 次數與 owner 不因 policy migration 改變。
- Unity 開啟時不直接寫 prefab／asset YAML；policy 由 Inspector 或 Unity Editor API 套用。

### 3. Agent 執行狀態機

```text
Planned
  -> ContextChecked       # 文件、dirty worktree、GUID/current policy 已確認
  -> OwnershipAudited     # event/async/pool/unique/static/addressable 已逐項檢查
  -> CodeMigrated          # 必要程式改造完成
  -> Compiled              # affected assembly 編譯成功
  -> PolicyApplied         # Unity Editor API 套用 target policy
  -> RuntimeValidated      # 五份 snapshot + 功能檢查全部通過
  -> Accepted

任何階段遇到 blocker -> Blocked 或 NeedsRuntimeValidation
```

Agent 不得跳過 `OwnershipAudited` 或 `Compiled` 直接套用 `DestroyOnRecovery`。已有 `KeepN`／`DestroyOnRecovery` 的候選若只是缺少證據，狀態只能是 `NeedsRuntimeValidation`，不得回退 policy。

### 4. Ownership audit checklist

Agent 必須對 report 的每一個 signal evidence 開啟實際 source line，並在 manifest 記錄結果：

```text
[ ] C# event += 有對應 -=，或已改用 Lifetime.Subscribe(add, remove, handler)
[ ] await 後的 Unity object 操作有 Lifetime.Token / IsAlive / generation guard
[ ] async void、fire-and-forget 有明確 exception 與 lifetime 處理
[ ] requested pool 有 owner；child mode 明確是 ReturnToGlobalPool、DestroyWithOwner 或 UseChildPolicy
[ ] DestroyWithOwner template 不包含 nested unique/singleton
[ ] static callback、delegate、cache 不持有已銷毀 component
[ ] Addressable wrapper/handle 的 owner 與 Dispose 次數不變
[ ] transition、pending recovery、callback 完成前不會 destroy hierarchy
```

只看到 regex 命中不能算通過；無法追到 cleanup owner 時，將 blocker 寫入 manifest，狀態維持 `NeedsCodeReview` 或 `NeedsCodeMigration`。

### 5. 小步驟修改與驗證 gate

每個候選依下列順序執行，任何 gate 失敗就停止並保留證據：

| Gate | 執行 | 通過條件 |
|---|---|---|
| G0 Context | 讀 AGENTS、CLAUDE、設計文件、case study，檢查 dirty worktree | 未覆蓋使用者修改；GUID/path/current policy 一致 |
| G1 Static | 完成 event、async、LifetimeScope、owner-aware pool 必要修改 | blocker 已消除或被明確保留；unique/singleton 未被破壞 |
| G2 Compile | 編譯受影響的 Runtime／Editor assembly | 無新增 error；既有 warning 需列出 |
| G3 Apply | Unity 關閉或使用 Unity Editor API 套用 manifest target policy | 實際 prefab GUID 與 manifest 一致；只改指定候選 |
| G4 Baseline | 穩定頁面 dump Object Graph | 建立 baseline；記錄 page、timestamp、handles、A/Q/P |
| G5 Open/Return | 開啟目標 UI、返回並等待 recovery settle，再 dump | 功能正常；target hierarchy peak 可觀測；無 exception |
| G6 Reopen | 再開啟、再返回並 dump | A/Q/P 不隨輪次成長；child 不殘留 global pool |
| G7 Accept | 比對 snapshots 與功能結果 | policy 目標達成、Addressable ownership 不變、Console 無錯誤 |

### 6. 量測與接受標準

Agent 不應只報「memory 變小」。manifest／Advisor report 至少保存：

```text
peakActiveGameObjects / peakActiveMonoBehaviours
peakQueuedGameObjects / peakPendingRecoveryGameObjects
queuedAndPendingSlopeAcrossReopen
requestedPool owner-aware / DestroyWithOwner counts
Addressable valid/completed/succeeded/duplicate handles
console MissingReference / NullReference / duplicate callback count
```

接受條件是候選的 post-return retained hierarchy 符合 target policy，且第二次開啟的星數、按鈕、圖片、注入資料與第一次一致。對 `DestroyOnRecovery`，回收後應為零或從 pool entry 消失；對 `KeepN`，queued instance 不得超過 `recoveryKeepCount`。若為 nested child，還必須檢查 child 沒有因 parent destroy 被轉移到 global pool。

### 7. 停損、回退與交付

以下任一情況都不得標記 `Accepted`：

- Console 出現 MissingReference、NullReference、未處理 task exception 或 duplicate callback。
- 第二次開啟功能、圖片、按鈕或注入資料異常。
- queued／pending 在重開後持續增加。
- unique/singleton ownership 被破壞。
- Addressable handle ownership 或 release 次數改變但沒有明確設計批准。
- manifest 的 expected current policy 已過期。

交付時 Agent 必須回報：manifest 路徑、修改檔案、target/current policy、每個 gate 結果、snapshot 路徑、編譯命令與剩餘 blocker。只有 G0–G7 全部通過才可將 `migrationStatus` 寫成 `Validated`；否則使用 `NeedsRuntimeValidation`、`NeedsCodeReview` 或 `Blocked`。

## Runtime Pool Global Eviction 調查與設計

### 可行性結論

對 `ViewElementRuntimePool.veDicts` 中已完成 recovery 的 non-unique queued instance，強制淘汰是可行的。`RequestViewElement(source)` 在該 source queue 為空時會重新 `Instantiate(source)`，因此正確地從 queue 移除再永久銷毀，不會造成「下一次永遠取不到 ViewElement」。主要代價是下一次開啟的 Instantiate、Awake、Setup、注入、layout 與資源重建時間。

但是「全面清空所有 pool」必須精確定義。以下物件不可視為同一種可淘汰 queue：

| 類別 | 是否由 global eviction 淘汰 | 原因 |
|---|---|---|
| `veDicts` 中的 non-unique queued instance | 是 | 已完成 recovery，Request 可在 queue 空時重建 |
| `recycleQueue` pending recovery | 否 | leave/recovery pipeline 尚未完成，直接 destroy 可能跳過 callback 或 bookkeeping |
| active instance | 否 | 正在頁面或 transition 中使用 |
| `uniqueVeDicts` | 否 | 由 unique ownership stack 與 singleton injection 管理 |
| `DestroyWithOwner` owner-local queue | 否 | 不在 `veDicts`，應由 owner lifetime Dispose |
| 含 nested unique 的 queued hierarchy | 否 | 現有 destroy safety contract 已明確阻擋 |

因此建議將機制定名為 **Global Queued Pool Budget**，而不是模糊的 Clear All Runtime Objects。

### 現行實作需要先補的安全條件

現行 `RequestViewElement` 直接 `Dequeue()` 後使用結果，沒有清除 stale/null queue entry 的防禦。Global eviction 必須先把 victim 從 `veDicts` 與全域 eviction index 移除，再呼叫 `DestroyViewElementHierarchy`。若先 `Destroy()`、仍把 reference 留在 queue，同 frame 或下一 frame Request 可能取到已排程銷毀／已銷毀的 Unity object。

```text
function EvictQueued(victim):
    assert victim belongs to sourceQueue
    RemoveFromSourceQueue(victim)       # 必須先移除
    RemoveFromGlobalEvictionIndex(victim)
    DecrementQueuedBudget(victim)
    DestroyViewElementHierarchy(victim) # PrepareForPermanentDestroy + Destroy
```

Request path 同時應具備容錯：

```text
function RequestViewElement(source):
    queue = GetOrCreateQueue(source)
    while queue is not empty:
        candidate = queue.Dequeue()
        UntrackFromGlobalEviction(candidate)
        if candidate is valid and not permanently-destroying:
            return candidate
    return Instantiate(source)
```

### 建議設定

Global budget 以 additive options 提供，預設 disabled，確保升級 package 後 runtime 行為完全不變：

```csharp
public sealed class ViewElementGlobalPoolBudgetOptions
{
    public bool enabled = false;
    public int highWatermarkInstances = 0; // 0 = unlimited
    public int lowWatermarkInstances = 0;
    public int highWatermarkHierarchyGameObjects = 0; // 0 = disabled proxy budget
    public int lowWatermarkHierarchyGameObjects = 0;
    public int maxEvictionsPerFrame = 5;
    public float minimumIdleSeconds = 0;
    public ViewElementGlobalEvictionMode mode = OldestFirst;
}
```

只用 ViewElement instance 數很便宜，但 1 個小 icon 與 1 個 757 GO hierarchy 的成本完全不同。終極版本應支援雙重 budget：

- `queued instance count`：低成本、保證 pool 不無限增長。
- `queued hierarchy GameObject count`：較接近 retained hierarchy 成本，但仍不是 native memory bytes。

GameObject count 可以在 instance enqueue 時量測並記錄，避免每次 budget check 重新掃描整個 pool；若 hierarchy 在 runtime 動態增減，下一次 recovery 時更新該 instance cost。

### 觸發方式

queued count 只會在 Request 與 Recovery 時變化，因此 count-based eviction 不需要每 frame 掃描：

```text
on RecoveryViewElement enqueue:
    TrackQueued(instance, sourceKey, hierarchyCost, recoverySequence, unscaledTime)
    if AnyHighWatermarkExceeded:
        ScheduleEviction()

EvictionRunner:
    while BudgetAboveLowWatermark:
        evict at most maxEvictionsPerFrame eligible victims
        yield next frame
```

高水位觸發、淘汰到低水位可以避免在臨界值附近每次 recovery 都 Destroy 一個物件。若需要 idle TTL，再加低頻 coroutine 檢查 `minimumIdleSeconds`；不建議在 `Update()` 每 frame 對所有 queue 做 `Sum` 與 hierarchy scan。

### 淘汰順序

第一版建議 `OldestFirst`，而不是 dictionary iteration order。可保留現有 `Dictionary<int, Queue<ViewElement>>`，另外維護：

```text
globalRecoveryOrder: LinkedList<ViewElement>
globalNodeByInstanceId: Dictionary<int, LinkedListNode<ViewElement>>
queuedCostByInstanceId: Dictionary<int, int>
```

每個 source queue 本身也是 FIFO，因此 global oldest victim 必然是該 source queue 的 head；可以安全 Dequeue 後比對 identity，不必把既有 private dictionary 改成新型別，Editor diagnostics 與 Inspector 也較容易保持相容。

可再提供兩種模式：

- `OldestFirst`：一般預設，減少長期未用 UI retained memory。
- `ClearEligibleWhenExceeded`：超過 high watermark 時清空所有 eligible global queued instance，適合 memory warning 或壓力測試，不建議作為預設。

### Policy 優先序

```text
1. ViewElement recovery policy
   DestroyOnRecovery / KeepN 先決定本次是否直接 destroy
2. Global Queued Pool Budget
   對原本將 enqueue 的 eligible instance 做全域容量控制
3. Unique / owner-local ownership protection
   永遠不由 global budget 覆蓋
```

因此啟用 global budget 後，`KeepForever` 表示「沒有 per-source 上限」，不再表示「即使超過 global memory budget 也絕不淘汰」。這是有意義的 opt-in 行為變更，所以 options 預設必須 disabled。

### 預期影響

| 面向 | 預期影響 |
|---|---|
| Runtime correctness | 正確限制在 global queued instance 時，Request 可重新 Instantiate；不應造成缺 prefab 的 runtime error |
| Reopen latency | pool miss 增加，Instantiate、Awake、Setup、layout 與注入成本上升，可能出現 frame spike |
| Destroy cost | 一次銷毀大型 hierarchy 也可能造成主執行緒與後續 GC spike，必須分幀限制 |
| UI state | 任何錯誤依賴 pooled component 欄位長期保存狀態的 UI 會被暴露；正確 UI 應在 Show/Refresh/Inject 重建狀態 |
| Event/async | Permanent destroy 會 Dispose Lifetime、取消 token、執行 cleanup；未管理 callback 可能產生 MissingReference 或重複訂閱 |
| Addressables | 目前 trimming instance 不會釋放 prefab AssetReference handle；可降低 GO/Component，但不保證 texture/atlas/dependency memory 同步下降 |
| Managed/native memory | `Destroy()` 在 frame end 生效，GC 與 native dependency release 也非同步；不可用呼叫當下的 managed memory 判定成功 |
| Owner-aware pools | `ReturnToGlobalPool`／`UseChildPolicy` 最終進 global queue 後可被 budget 淘汰；`DestroyWithOwner` 仍由 owner 管理 |
| Diagnostics | Object Graph 應新增 eviction count、blocked count、queued cost、high/low watermark 與 pool miss/instantiate count |

### 建議 public API 與結果

先提供可測試的 manual trim，再啟用自動 budget：

```csharp
public ViewElementPoolTrimResult TrimQueuedPool(ViewElementPoolTrimRequest request);
public ViewElementGlobalPoolStatistics GetGlobalPoolStatistics();
public void ConfigureGlobalPoolBudget(ViewElementGlobalPoolBudgetOptions options);
```

```text
TrimResult:
    requestedTarget
    beforeQueuedInstances / afterQueuedInstances
    beforeQueuedHierarchyGameObjects / estimatedAfterHierarchyGameObjects
    evictedInstances / evictedHierarchyGameObjects
    blockedNestedUnique
    skippedInvalidEntries
```

Manual API 可以先驗證「清空 eligible global queue 後所有頁面都能重開」，並建立 pool miss／Instantiate latency baseline。自動機制只是在相同安全 trim primitive 上增加 trigger，不應另寫第二套 destroy path。

### 分階段實作與驗證

1. **Harden Request/Queue bookkeeping**：加入 null/stale skip 與 queued tracking invariant；預設行為不變。
2. **Manual trim API**：只淘汰 eligible global queued objects，加入 dry-run/result；不自動觸發。
3. **Diagnostics**：記錄 trim、pool miss、Instantiate count/time、blocked nested unique 與 post-frame actual count。
4. **Aggressive validation**：在測試環境每次回穩定頁後呼叫 `ClearEligibleWhenExceeded`，重複所有主要 ViewPage 3–7 輪。
5. **Budgeted automatic eviction**：加入 disabled-by-default high/low watermark 與分幀 runner。
6. **Device gates**：4 GB tier 驗證 retained memory slope、reopen P95 frame time、Console 與 Addressable handles。
7. **Optional pressure integration**：確認基礎機制穩定後，才考慮接 OS low-memory callback；不要讓 callback 直接同步銷毀上萬個 GO。

驗證至少包含：全面 trim 前後 Object Graph、同頁連續重開、跨大型 UI 切換、overlay transition 中不 trim pending/active、nested unique、owner-local child、Addressable mode 與 direct-reference mode。只有功能完全一致且記憶體 slope 改善、reopen latency 在預算內，才可在 host project opt in。

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
