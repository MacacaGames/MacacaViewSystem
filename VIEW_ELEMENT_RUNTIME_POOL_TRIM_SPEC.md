# ViewElement Runtime Pool Trim 現行規格

> 狀態：已實作（implementation contract）<br>
> 實作基準：`5f12fc7`<br>
> 最後核對：2026-09-20<br>
> 設計沿革：[ViewElement Pool Policy 與 Lifetime 管理設計提案](VIEW_ELEMENT_POOL_POLICY_DESIGN.md)

本文件描述目前 `ViewElementRuntimePool` 的實際行為。若本文件與舊設計提案、README 摘要或歷史討論不一致，以 runtime code 與本文件為準。

## 1. 目的與範圍

Global Pool Trim 用來限制已完成 recovery、目前位於全域 runtime pool 的 non-unique `ViewElement`。它透過永久銷毀 inactive hierarchy，降低長期保留的 GameObject、Component 與相關 managed/native shell。

本機制只處理：

- `ViewElementRuntimePool.veDicts` 中已完成 recovery 的 non-unique queued instance。
- 同一批 instance 的全域 FIFO eviction index。
- manual trim、watermark budget、periodic idle trim 與 `Application.lowMemory` 觸發。

本機制不直接處理：

- active 或 transition 中的 instance。
- `recycleQueue` 中尚未完成 recovery 的 instance。
- unique／singleton instance。
- `DestroyWithOwner` 的 owner-local queue。
- Addressable handle 或 Texture／Material／RenderTexture 的直接 unload。
- managed GC、native allocator reserve 或 OS PSS 的同步回收。

`Destroy()` 在 frame end 才生效；trim 成功只表示 hierarchy 已從 logical queue 移除並排入永久銷毀，不代表呼叫當下的 process memory 立即下降。

## 2. 名詞

| 名詞 | 定義 |
|---|---|
| queued | 已完成 recovery，存在 source FIFO 與 global FIFO index 的 non-unique instance。 |
| pending recovery | 已要求回收，但尚未由 recovery runner 完成處理的 instance。 |
| pending destroy | 已從 logical queue 移除並呼叫 `Destroy()`，但 Unity 尚未在 frame end 完成銷毀的 instance。 |
| hierarchy cost | instance enqueue 時，以 `GetComponentsInChildren<Transform>(true).Length` 計算的 GameObject proxy。 |
| recovery policy | 決定 recovery 時是否進入 pool，以及每個 source 最多保留多少 instance。 |
| trim eligibility | instance 已進入 global queue 後，是否可被 global trim 永久銷毀。 |
| grace | instance 進入 global queue 後，在自動 budget／low-memory trim 前的最短保護時間。 |
| idle threshold | instance 進入 global queue 後，periodic idle trim 可淘汰它的最短時間。 |

## 3. Queue 與永久銷毀狀態

```text
active / leaving
      |
      v
pending recovery (recycleQueue)
      |
      v
apply Recovery Policy
      |
      +-- DestroyOnRecovery / KeepN overflow --> pending destroy --> destroyed
      |
      +-- retained --------------------------> global queued
                                                   |
                                                   +-- Request --> active
                                                   |
                                                   +-- Trim --> pending destroy --> destroyed
```

### 3.1 Bookkeeping 不變條件

每個 global queued instance 必須同時存在於：

1. 以 `PoolKey` 分組的 source FIFO；
2. global oldest-first linked list；
3. instance ID 到 global node 的 dictionary。

logical instance／hierarchy counter 必須與上述結構一致。Editor 與 Development Build 會執行 invariant validation。

永久銷毀的順序固定為：

1. 從 source FIFO 移除；
2. 從 global index 移除；
3. 更新 logical queued counters；
4. 呼叫 `PrepareForPermanentDestroy()`；
5. 呼叫 Unity `Destroy(gameObject)`；
6. 暫存於 pending-destroy accounting；
7. `LateUpdate()` 確認 Unity object 已銷毀後移除 pending-destroy accounting。

Request path 會跳過 null、stale、unique、PoolKey 不一致、pending recovery 或已準備永久銷毀的 queue entry，避免取回已失效物件。

## 4. Recovery Policy 與 Trim Policy

兩種 policy 是互相獨立的決策。

### 4.1 Recovery Policy

| Policy | Recovery 行為 |
|---|---|
| `KeepForever` | 不設 per-source 數量上限，instance 可進 global queue。 |
| `KeepN` | source queue 已達 `recoveryKeepCount` 時直接永久銷毀，否則進 global queue。 |
| `DestroyOnRecovery` | recovery 時直接永久銷毀。 |

若待銷毀 root 含 nested unique，direct recovery destroy 會被阻擋，該 root 仍會進 global queue。

### 4.2 Trim Policy

`trimOnlyInEmergency` 是 global trim 的保護旗標，預設 `false`：

- `false`：可由 `NormalBudget` 與 `EmergencyHardCap` trim。
- `true`：root 只能由 `EmergencyHardCap` trim。
- parent hierarchy 內任一 nested ViewElement 為 `trimOnlyInEmergency=true` 時，該 parent 也不能由 `NormalBudget` trim。

`KeepForever` 不再代表「global budget 永遠不得淘汰」。Global budget 啟用後，所有未設 `trimOnlyInEmergency` 的 queued root 都可由 `NormalBudget` trim，不論 recovery policy 是 `KeepForever` 或 `KeepN`。

`IgnoreRecoveryPolicyOnce`／`ReturnToGlobalPool` 只影響該次 recovery 是否略過 `DestroyOnRecovery`／`KeepN`；它不再是 global trim opt-in 訊號。

## 5. Eviction Mode 與 Eligibility

### 5.1 Mode

| Mode | 行為 |
|---|---|
| `NormalBudget` | 保護 root 或 nested hierarchy 上的 `trimOnlyInEmergency`。 |
| `EmergencyHardCap` | 可淘汰 `trimOnlyInEmergency` root／parent，但仍不可淘汰含 nested unique 的 hierarchy。 |

淘汰順序在兩種 mode 下都固定為 global oldest-first。Mode 不控制排序，只控制保護強度。

### 5.2 永久阻擋與暫時阻擋

| 狀態 | NormalBudget | EmergencyHardCap |
|---|---:|---:|
| stale／已準備永久銷毀 | 從 bookkeeping 清除，不計為 eviction | 同左 |
| nested unique > 0 | 阻擋 | 阻擋 |
| root `trimOnlyInEmergency=true` | 阻擋 | 允許 |
| nested `trimOnlyInEmergency=true` | 阻擋 parent | 允許 parent |
| active／shown | 阻擋 | 阻擋 |
| pending recovery | 阻擋 | 阻擋 |
| queued age 小於要求門檻 | `BlockedTooYoung` | `BlockedTooYoung` |
| 其他有效 inactive queued root | 允許 | 允許 |

Eligibility reason 包含：

- `EligibleByDefault`
- `EligibleByEmergencyHardCap`
- `Stale`
- `BlockedNestedUnique`
- `BlockedActive`
- `BlockedPendingRecovery`
- `BlockedTooYoung`
- `BlockedEmergencyOnly`
- `BlockedNestedEmergencyOnly`

舊 enum numeric values 仍保留，供舊 consumer 反序列化或判讀；現行判斷不再使用 recovery policy／explicit opt-in reason。

### 5.3 Reason 判讀注意事項

Snapshot 以 structural protection 優先判斷，因此同時為 nested unique 與 too-young 的 entry 會顯示 `BlockedNestedUnique`。`IdleOnly` trim 實際執行時先檢查 age，因此 trim result 可能將同一 entry 計入 `BlockedTooYoung`。Snapshot 與單次 trim result 用途不同，不保證 blocker bucket 完全相同。

## 6. Manual Trim API

```csharp
public ViewElementPoolTrimResult TrimQueuedPool(
    ViewElementPoolTrimRequest request);
```

### 6.1 Request

| 欄位 | 規格 |
|---|---|
| `DryRun` | 不修改 queue、不呼叫 Destroy；回傳投影結果。 |
| `Mode` | `NormalBudget` 或 `EmergencyHardCap`。 |
| `TargetQueuedInstances` | nullable；指定 logical queued instance 目標。 |
| `TargetQueuedHierarchyGameObjects` | nullable；指定 cached hierarchy GO 目標。 |
| `MaxEvictions` | 本次呼叫最多淘汰的 root 數，必須大於 0。 |
| `MinimumIdleSeconds` | 本次呼叫使用的 queued age 下限。 |
| `IdleOnly` | 以 idle sweep 行為執行；無 target 時會掃描整個 queue。 |

只要任一已指定 target 尚未達成，trim 就會沿 global FIFO 尋找 eligible entry。指定兩個 target 時，兩者都達成才算 satisfied。

`IdleOnly=true` 且未指定 target 時，不受 high／low watermark 限制；每次呼叫可淘汰所有已達 age 的 eligible entry，直到 `MaxEvictions`。

### 6.2 Result

Result 同時提供：

- before／after logical queue；
- dry-run projected-after；
- evicted instance／hierarchy GO；
- nested unique／ineligible／too-young blocker；
- stale 清除數；
- `BudgetSatisfied`；
- remaining-over-budget。

Dry-run 的 `After*` 仍是實際未變的 logical queue，預估值必須讀 `ProjectedAfter*`。Dry-run 也會增加 `TrimCount` 並覆寫 `LastTrimResult`，但不增加實際 `TrimEvictedInstances`。

## 7. Global Budget 設定

設定儲存在 `ViewSystemBaseSetting.globalPoolBudget`，由 `ViewController.Init()` 建立 runtime pool 後立即套用。

```csharp
public sealed class ViewElementGlobalPoolBudgetSetting
{
    bool enabled;
    ViewElementGlobalEvictionMode mode;
    int highWatermarkInstances;
    int lowWatermarkInstances;
    int highWatermarkHierarchyGameObjects;
    int lowWatermarkHierarchyGameObjects;
    int maxEvictionsPerFrame;
    float AutoTrimGraceSeconds;
    float IdleTrimAfterSeconds;
    bool allowEmergencyHardCapOnLowMemory;
}
```

### 7.1 Package defaults

| 設定 | Default | 說明 |
|---|---:|---|
| enabled | `false` | 升級 package 不會自動啟用 trim。 |
| mode | `NormalBudget` | 日常 budget 保護 emergency-only hierarchy。 |
| high／low instances | `0 / 0` | 該維度停用。 |
| high／low hierarchy GO | `0 / 0` | 該維度停用。 |
| max evictions per frame | `5` | automatic runners 共用的 root eviction allowance。 |
| auto trim grace | `0` | 自動 budget／low-memory 不提供額外 age 保護。 |
| idle trim after | `0` | periodic idle runner 停用。 |
| emergency on low memory | `false` | low-memory 不升級 NormalBudget。 |

Host project 的 rollout 數值不是 package contract，必須由該 host 的目標裝置量測決定。

### 7.2 驗證

- high／low 必須非負。
- low 不得大於 high。
- high 為 0 時 low 必須為 0。
- `MaxEvictionsPerFrame` 必須大於 0。
- grace／idle threshold 必須是有限且非負數。
- mode 必須是已定義 enum。

已啟用但設定無效時，`ViewController` 會記錄 error 並回退成 disabled budget，避免初始化中斷。

## 8. Watermark 語意

- 只有目前值嚴格大於 high（`>`）才觸發 budget runner。
- runner 啟動後會嘗試降到 low，而不是只降回 high。
- instances 與 hierarchy GO 任一維度超過 high 都會觸發。
- 兩個維度都啟用時，兩個 low target 都達成才停止為 satisfied。
- high=0 代表日常 budget 不使用該維度。
- hierarchy GO 是 enqueue 時的 cached proxy，不是實際 byte 數。
- queued 期間動態新增／移除 child 不會自動更新 cached hierarchy cost；重新建立 queue entry 時才重算。

## 9. Automatic Budget Runner

Global queue 新增 entry 後，若 budget enabled 且任一 high watermark 超標，就排程 runner。

```text
enqueue
  -> over high?
  -> next frame start runner
  -> trim at most remaining automatic root allowance
  -> satisfied: stop
  -> evicted > 0 but still over low: yield one frame and continue
  -> evicted == 0: stop as unreachable for this attempt
```

`MaxEvictionsPerFrame`：

- 只限制 automatic budget／idle runner；manual request 使用自己的 `MaxEvictions`。
- budget 與 idle runner 同一 frame 共用 allowance。
- 單位是 root ViewElement，不是 child GameObject 數。
- 不限制 eligibility scan 數量；大量 blocked root 仍可能在一次呼叫被掃描。

### 9.1 無法達成 Low 時

若只剩 nested unique、emergency-only、active、pending 或 too-young entry，單次 trim 會回傳 `BudgetSatisfied=false`。當該次 `EvictedInstances==0` 時，budget runner 立即結束，不會每 frame 無限重試。

runner 結束後，以下事件會重新觸發：

- 新 entry enqueue，且 pool 仍高於 high；
- 新的 `Application.lowMemory`；
- budget 重新設定後當下仍高於 high；
- periodic idle runner（若 enabled）執行自己的 targetless idle sweep。

若失敗原因只有 grace／too-young，而且 `IdleTrimAfterSeconds=0`，目前沒有「grace 到期後自動喚醒 budget runner」的排程。必須等上述其他事件重新觸發。這是現行刻意保留的行為。

## 10. Periodic Idle Runner

`IdleTrimAfterSeconds > 0` 時啟動獨立 coroutine。每輪等待時間為：

```text
clamp(max(AutoTrimGraceSeconds, IdleTrimAfterSeconds), 1s, 10s)
```

每次 idle trim 的 entry age 下限為：

```text
max(AutoTrimGraceSeconds, IdleTrimAfterSeconds)
```

因此 wait cadence 最長 10 秒，但 entry 仍必須滿足完整 age threshold。例如 grace=60 秒、idle=5 秒時，runner 約每 10 秒檢查一次，直到 entry age 達 60 秒才可淘汰。

Idle runner：

- 不檢查 high watermark。
- 不設定 low target。
- 每輪可淘汰達 age 的 eligible entry，直到該 frame allowance 用完。
- 只要 budget enabled、component active 且 idle threshold > 0，就會持續運作。
- 即使所有 entry 都是永久 blocker，也會依 cadence 繼續做 targetless scan；不會變成 per-frame busy loop。

因此很短的 idle threshold 會讓 pooling 接近 TTL cache，增加 pool miss 與重新 Instantiate 成本。

## 11. Low-memory 行為

`Application.lowMemory` 只在 global budget enabled 時處理，且不會在 callback 內同步 Destroy。

1. 排程既有 frame-budgeted runner，`force=true`。
2. 略過 high-watermark gate。
3. 仍套用 `AutoTrimGraceSeconds`。
4. 日常 mode 是 `NormalBudget` 且 `AllowEmergencyHardCapOnLowMemory=true` 時，該次 runner 暫時升級成 `EmergencyHardCap`。
5. 日常 mode 已是 `EmergencyHardCap` 時維持 emergency；flag 不會反向降級。

Force trim 的 target 規則：

- 有啟用 high watermark 的維度使用對應 low。
- high=0 的維度在 force trim 時 target 為 0。

因此 low-memory 可能比日常 budget 更積極地清空 eligible queue，但仍無法越過 nested unique、active、pending recovery 或 grace 保護。

若 low-memory 嘗試因 grace 而 `EvictedInstances=0`，該次 runner 會停止；目前不排程 grace 到期重試，必須等待另一個觸發事件。

## 12. 診斷與結果判讀

Editor menu：

- `MacacaGames > ViewSystem > Diagnostics > Runtime Pool > Print Snapshot`
- `... > Dry Run Emergency Hard Cap`
- `... > Aggressive Clear Eligible Queued`
- `... > Run In-Editor Self-Test`

### 12.1 Snapshot 核心欄位

| 欄位 | 意義 |
|---|---|
| `LogicalQueued*` | source FIFO／global index 內的 logical queued 數。 |
| `PendingRecovery*` | 尚未完成 recovery 的數量／cached hierarchy。 |
| `PendingDestroy*` | 已排入 Destroy、尚未完成 frame-end destruction。 |
| `PostFrameActualHierarchyGameObjects` | queued + pending recovery + pending destroy；不包含 active hierarchy。 |
| `EligibleQueuedInstances` | 依 snapshot mode 與 age threshold 可 trim 的 root 數。 |
| `Blocked*` | nested unique、ineligible、too-young 的彙總。 |
| `PoolMissCount` | source queue 無可用 entry、因而 Instantiate 的累計次數。 |
| `InstantiateCount/Milliseconds` | Instantiate 累計次數與同步耗時。 |
| `TrimCount` | manual、dry-run、budget、idle、low-memory trim 呼叫累計。 |
| `TrimEvictedInstances` | 非 dry-run 實際從 logical queue 淘汰的 root 累計。 |

### 12.2 `LastTrimResult` 限制

`LastTrimResult` 代表最後一次 trim API 呼叫，不是持續性的 budget state。

Periodic idle request 沒有 target，因此它的 `BudgetSatisfied` 依定義為 true。若先前 budget runner 因 blocked floor 回傳 false，下一次 targetless idle sweep 可能覆寫 `LastBudgetSatisfied=true`，即使 logical hierarchy 仍高於 configured high／low。

判斷實際 budget 狀態時必須同時比較：

- `LogicalQueuedInstances` 與 configured high／low；
- `LogicalQueuedHierarchyGameObjects` 與 configured high／low；
- 最後一次有 target 的 trim result；
- eligibility reason 與 blocked hierarchy cost。

### 12.3 Manual diagnostics

`Dry Run Emergency Hard Cap` 與 `Aggressive Clear Eligible Queued` 都使用：

- `EmergencyHardCap`
- age threshold 0
- instance target 0
- hierarchy target 0
- `MaxEvictions=int.MaxValue`

Dry-run 不改變 queue；Aggressive Clear 會同步移除 logical queue，再由 Unity frame end 完成實體銷毀。

## 13. Serialized Backward Compatibility

舊資產只有 `minimumIdleSeconds`。現行設定保留該 serialized field，並加入：

- `autoTrimGraceSeconds`
- `idleTrimAfterSeconds`
- `splitTrimTimingConfigured`

讀取規則：

1. `splitTrimTimingConfigured=false` 時，兩個新 property 都回傳舊 `minimumIdleSeconds`。
2. 第一次設定任一新 property 時，先把舊值複製到兩個新 backing field，再標記 split configured。
3. Runtime `ViewElementGlobalPoolBudgetOptions.MinimumIdleSeconds` setter 同時設定 grace 與 idle threshold，保留舊 source 使用方式。
4. Runtime legacy getter 回傳 `AutoTrimGraceSeconds`；split 後不代表 idle threshold。
5. 缺少 setting、setting 為 null 或 `enabled=false` 時，automatic trim 保持 disabled。

`ViewElementRuntimePoolSnapshot.MinimumIdleSeconds` 仍保留，表示該次 snapshot 使用的 age threshold；新 code 應分別讀取 `AutoTrimGraceSeconds` 與 `IdleTrimAfterSeconds`。

### 13.1 Public API 注意事項

現行 `QueuedEntry` snapshot 已移除舊 `ExplicitlyAllowsGlobalEviction` property，改用 `TrimOnlyInEmergency` 與 `EligibilityReason`。目前 host repository 沒有舊 property consumer；外部 package consumer 若直接引用舊 property，需先完成 source migration，或在正式發佈 package 前補回 obsolete compatibility alias。

## 14. Owner-aware Requested Pool

- `ReturnToGlobalPool`：owner Dispose 時略過 child recovery policy 一次並送回 global queue；之後依 `trimOnlyInEmergency` 決定 global eligibility。
- `UseChildPolicy`：送回 global recovery pipeline並遵守 child recovery policy；若最後進 global queue，trim eligibility 同樣獨立判斷。
- `DestroyWithOwner`：使用 owner-local queue，不進 global queue，不受 global trim。

root trim 會永久銷毀完整 hierarchy。若 root 內含 nested emergency-only child，NormalBudget 必須保護整個 root；EmergencyHardCap 可整體銷毀。若含 nested unique，兩種 mode 都保護整個 root，不會拆解不完整 parent。

## 15. 已知限制與風險

1. hierarchy GO 只是 retained hierarchy proxy，不等於記憶體 bytes。
2. trim instance 不會直接 release Addressable prefab handle 或仍被其他 owner 引用的 Texture／Material／Shader。
3. `MaxEvictionsPerFrame` 以 root 計數；單一大型 root 仍可能在一幀排入大量 child Destroy。
4. eviction allowance 不限制 scan 成本；大量 blocked root 可能造成週期性掃描。
5. nested unique blocked floor 可能高於 configured low watermark，此時 budget runner 會停止於 unsatisfied 狀態。
6. periodic idle runner 會持續重試 blocked queue，但不是每 frame busy loop。
7. idle threshold 過短會增加 pool miss、Instantiate、Awake、Setup、layout、注入與 Addressable access 成本。
8. 預設 `trimOnlyInEmergency=false` 代表既有 `KeepForever` root 在 host opt-in global budget 後也可被 NormalBudget trim；stateful UI 必須能在重開時完整重建狀態。
9. Editor memory、Instantiate timing 與 GC 行為不能取代 Android／iOS Player 實機量測。

## 16. 驗證規格

### 16.1 In-Editor Self-Test

Self-test 必須在 Play Mode、`ViewController.runtimePool` 已初始化且 queue boundary 為空時執行。測試涵蓋：

- duplicate recovery 與 FIFO；
- manual normal／emergency trim；
- recovery policy 與 trim eligibility 解耦；
- hierarchy budget／stale entry；
- nested unique／nested emergency-only；
- pending recovery／pending destroy；
- requested-pool ownership modes；
- watermark validation；
- automatic budget 與 shared per-frame allowance；
- grace／idle threshold 分離；
- low-memory mode upgrade 與 grace；
- serialized legacy timing migration；
- cleanup 後恢復原 runtime options。

實作基準的驗證結果為：

```text
[RuntimePoolSelfTest] PASS checks=136, failures=0.
```

此結果只驗證 runtime contract，不代表 host project 的所有 UI 都可安全永久銷毀。

### 16.2 Host Release Gate

正式啟用前至少驗證：

1. 穩定頁 baseline、開啟、返回、重開、再次返回。
2. 全 UI traversal 後的 reachable low watermark 與 nested-unique blocked floor。
3. 目標手機上的 trim frame、Destroy 後續 frame、reopen P95/P99 frame time。
4. pool miss／Instantiate count 與同步時間變化。
5. App PSS、Unity reserved／used memory 與 retained hierarchy slope。
6. Addressable handle、圖像、動畫、注入資料與按鈕功能。
7. `Application.lowMemory`、背景／前景、scene reload 與長時間 soak。
8. Console 無 MissingReference、NullReference、duplicate callback 或未處理 async exception。

## 17. 與舊設計文件相異的已實作改動

本節專門比較 [舊設計提案](VIEW_ELEMENT_POOL_POLICY_DESIGN.md) 與目前實作，避免將 proposal 當成 runtime contract。

| 主題 | 舊文件 | 現行實作 |
|---|---|---|
| 文件性質 | 設計提案與分階段計畫。 | 已落地 implementation contract。 |
| `KeepForever` | 前段目標強調維持永久保留相容；後段才提出 global budget opt-in 後可淘汰。 | recovery 仍無 per-source 上限，但 global budget 啟用後預設可由 NormalBudget trim。 |
| Trim opt-in | 曾使用 recovery policy／`explicitlyAllowsGlobalEviction` 推導。 | 改為獨立 `trimOnlyInEmergency`；預設 false，所有有效 queued root 可 normal trim。 |
| Child protection | 主要只描述 nested unique。 | nested unique 永久阻擋；nested `trimOnlyInEmergency` 會把 NormalBudget 保護傳播到 parent。 |
| Mode | 提案使用 `OldestFirst`／`ClearEligibleWhenExceeded` 混合排序與策略概念。 | 排序固定 oldest-first；mode 改為 `NormalBudget`／`EmergencyHardCap`。 |
| Timing | 單一 `minimumIdleSeconds` 同時代表 grace／idle。 | 拆成 `AutoTrimGraceSeconds` 與 `IdleTrimAfterSeconds`，並保留 legacy migration。 |
| Idle trim | 視為 optional TTL，未完整定義。 | targetless periodic runner；不看 watermark，可逐輪清除所有達 age 的 eligible entry。 |
| Grace retry | 未明確定義。 | budget／low-memory 若因 grace 零 eviction 就停止；idle off 時不做 grace-expiry timed retry。 |
| Low memory | 描述為略過 high watermark並強制 runner。 | 同樣 force runner，但仍遵守 grace；是否升級 emergency 由 flag 控制，disabled dimension force target 為 0。 |
| Frame budget | 建議每幀限制 eviction。 | budget／idle 共用 root-count allowance；不限制 hierarchy GO destroy 數與 scan 成本。 |
| Watermark | 建議雙重 budget。 | instance／cached hierarchy GO 雙維度已實作，`>` high 觸發、降至 low。 |
| Stop condition | 未完整定義 unreachable low。 | satisfied 或該輪零 eviction 時停止 budget runner；新 enqueue／low-memory／reconfigure 才重啟。 |
| Diagnostics | 建議 statistics 與 blocked count。 | 已提供 snapshot、reason、pending recovery/destroy、dry-run projection、pool miss、instantiate 與 trim counters。 |
| `LastBudgetSatisfied` | 未描述。 | 只是最後一次 trim 結果；targetless idle 可覆寫為 true，不能當持續 budget health。 |
| Serialized setting | 僅描述 runtime options。 | 設定已串入 `ViewSystemBaseSetting`／`ViewController.Init()`，並支援 legacy asset。 |
| Manual tools | 建議先做 manual trim。 | 已提供 Print Snapshot、Dry Run、Aggressive Clear 與 Self-Test。 |
| Addressables | 預期未來建立 handle registry。 | Global trim 仍不 release Addressable handle；registry 不屬於本次實作。 |
| Public compatibility | 原則要求 additive API。 | 大部分 legacy timing API 保留；`ExplicitlyAllowsGlobalEviction` snapshot property 目前已移除，外部 consumer 需特別處理。 |

## 18. Source of Truth

主要實作：

- `Runtime/Components/ViewElementRuntimePool.cs`
- `Runtime/Components/ViewElement.cs`
- `Runtime/Models/SaveData/ViewSystemSaveDataBase.cs`
- `Runtime/ViewController/ViewController.cs`
- `Runtime/Utilities/ViewElementRequestedPool.cs`

Editor 與驗證：

- `Editor/VisualEditor/Window/ViewSystemGlobalSettingWindow.cs`
- `Editor/Diagnostics/ViewElementRuntimePoolDiagnostics.cs`
- `Editor/Diagnostics/ViewElementRuntimePoolSelfTest.cs`
- `Editor/Diagnostics/ViewSystemObjectGraphDiagnostics.cs`
