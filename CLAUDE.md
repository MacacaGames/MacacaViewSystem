# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Role

This is `com.macacagames.viewsystem` — a standalone Unity UPM package distributed via OpenUPM or git link.
The public-facing docs live in [README.md](README.md) / [README_zh.md](README_zh.md) and on [MacacaViewSystemDocs](https://macacagames.github.io/MacacaViewSystemDocs/). This file captures only what's useful when editing the package itself.

## Build / Test

There is no standalone build: Unity compiles the package when opened in the host project (Support Unity 2021 or newer). There are no unit tests, no lint config, and no CI for code — `.github/workflows/main.yml` only publishes `docs~/` to GitHub Pages on changes to that folder.

Iterate by opening the host Unity project and using **MacacaGames > ViewSystem > Visual Editor**.

## Assembly Layout

Two asmdefs, Runtime and Editor, separated by the standard `Editor/` folder convention:

- [Runtime/Macaca.ViewSystem.asmdef](Runtime/Macaca.ViewSystem.asmdef) — depends on `Macaca.Utility`, `Unity.Addressables`, `Unity.ResourceManager`, plus precompiled `DemiLib.dll` / `DOTween.dll`.
- [Editor/Macaca.ViewSystem.Editor.asmdef](Editor/Macaca.ViewSystem.Editor.asmdef) — editor-only tooling.

The hard dependency on `com.macacagames.utility` (declared in [package.json](package.json)) is significant — many helpers (MicroCoroutine, pooling, reflection utils) come from there.

## Runtime Architecture

The system is organized around four primary types. Changes to any of them ripple across the rest:

- **`ViewController`** ([Runtime/ViewController/ViewController.cs](Runtime/ViewController/ViewController.cs)) — singleton that owns page transitions, the ViewElement pool, the Unique ViewElement ownership stack, and Addressable loading. Inherits from `ViewControllerBase`; legacy surface lives in `ViewControllerObsolete.cs`.
- **`ViewElement`** ([Runtime/Components/ViewElement.cs](Runtime/Components/ViewElement.cs)) — a pooled UI instance. Defines *how* (5 transition types: Animator, CanvasGroup Alpha, Active Switch, ViewElement Animation, Custom), not *where*.
- **`ViewPage` / `ViewState`** — composition objects defined by the Visual Editor. FullPage is exclusive; OverlayPage stacks; ViewState provides shared layout across pages. Runtime data lives in [Runtime/Models/ViewPageModel.cs](Runtime/Models/ViewPageModel.cs).
- **`PageChanger`** ([Runtime/Utilities/PageChanger.cs](Runtime/Utilities/PageChanger.cs)) — the fluent API (`ViewController.FullPageChanger()...Show()`). All public page transitions flow through here.

Cross-cutting subsystems:

- **Pooling** — `ViewElementPool` (asset-side) and `ViewElementRuntimePool` (scene-side instances). Unique ViewElements bypass per-page pooling and maintain a LIFO borrow stack (`uniqueBorrowStacks` in ViewController) so ownership unwinds predictably when overlays close.
- **Overrides** — `ViewRuntimeOverride` / `ViewElementOverrideHelper` apply per-page property and event overrides on top of pooled instances. Attribute-driven overrides use `[OverrideProperty]` and `[OverrideButtonEvent]` from [Runtime/Attribute/](Runtime/Attribute/).
- **Injection** — `[ViewElementInject]` resolves values passed via `SetPageModel(...)`, plus anything implementing `IViewElementSingleton` (auto shared model). `ViewInjectDictionary<T>` handles multi-value cases.
- **Lifecycle** — `IViewElementLifeCycle` (interface) / `ViewElementBehaviour` (base class with UnityEvents). Never mutate `GameObject.activeSelf` in `OnBeforeShow()` — ViewSystem overwrites it shortly after.

### Save Data & Addressables

ViewSystem authoring data is persisted as a ScriptableObject derived from `ViewSystemSaveDataBase`:

- `ViewSystemSaveData` — direct prefab references.
- `ViewSystemSaveData_Addressable` — `AssetReferenceGameObject` lookups (`viewPageItemAssetRefs`, `uniqueViewElementAssetRefs`), gated by `IsAddressableMode => true`.

`ViewController` branches on `_useAddressableLoading` at init time. When modifying asset loading, keep both paths working — the host project (ProjectInfinity) uses the Addressable variant for hot updates.

## Editor / Visual Editor

Authoring tools live under [Editor/VisualEditor/](Editor/VisualEditor/) and open at **MacacaGames > ViewSystem > Visual Editor**. Key files:

- `ViewSystemVisualEditor.cs` — main node-based window.
- `ViewSystemNodeModel.cs` / `ViewSystemNodeInspector.cs` / `ViewSystemNodeConsole.cs` — node state, inspector panel, and log console.
- `Overrides/` — override editing UI, property binding selection, event wiring.
- `Window/` — sub-windows (Global Setting, Overlay Order, Bake to Script, Verifiers).

**Bake to Script** generates `ViewSystemScriptable.ViewPages` constants — regenerate after renaming pages.

**Verifiers** detect broken GameObject references, dangling overrides, missing events, and orphan page refs. Prefer fixing what the verifiers flag over suppressing them.

## Working Guidelines

- This package ships to OpenUPM; keep it host-project-agnostic. Don't add dependencies on Host project types. Shared logic that needs both sides goes into `com.macacagames.utility`.
- Public API (anything in `MacacaGames.ViewSystem` namespace used by consumers) is effectively a stable contract. Prefer additive changes. Deprecate via `[Obsolete]` and the `ViewControllerObsolete` partial pattern rather than deleting.
- When editing transitions or pooling behavior, manually verify in the Visual Editor's **Edit Mode** preview; there is no automated test coverage.
