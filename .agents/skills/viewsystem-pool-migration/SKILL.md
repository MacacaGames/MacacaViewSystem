---
name: viewsystem-pool-migration
description: Migrate MacacaViewSystem ViewElement pools from KeepForever to an evidence-based recovery policy while preserving lifecycle, requested-pool, unique, async, event, and Addressable ownership safety. Use when analyzing a ViewSystem Pool Policy Advisor or Object Graph JSON report, selecting a migration candidate, implementing ViewElementLifetimeScope or owner-aware requested pools, applying KeepN or DestroyOnRecovery, or validating reopen and recovery behavior in a Unity host project.
---

# ViewSystem Pool Migration

Use Unity-generated evidence to migrate one ViewElement candidate at a time. Treat the Advisor as a measurement source; inspect and change host-project code when static analysis cannot prove ownership.

## Establish context

1. Locate the package root by finding the ancestor containing `package.json` and `Runtime/Components/ViewElement.cs`.
2. Locate the Unity host root containing `Assets/` and `ProjectSettings/`.
3. Read completely before editing:
   - host `AGENTS.md`, when present;
   - package `CLAUDE.md`;
   - package `VIEW_ELEMENT_POOL_POLICY_DESIGN.md`;
   - package `VIEW_ELEMENT_POOL_POLICY_CASE_STUDY.md`.
4. Inspect dirty worktrees in both the host and package repositories. Preserve unrelated changes.
5. Read [references/report-contract.md](references/report-contract.md) before interpreting or producing Advisor artifacts.

## Interpret evidence correctly

- Separate the desired target policy from migration readiness. A safety warning means work is required before applying a target; it does not imply KeepForever is the desired target.
- For schema v4 and older reports, ignore `recommendedPolicy=KeepForever` when it was produced only by `NeedsOwnerMigration`, `NeedsStaticOwnership`, `NeedsEventCleanup`, `NeedsAsyncLifetime`, or `InsufficientRuntimeData`.
- Never recommend reverting an existing KeepN or DestroyOnRecovery migration solely because static evidence is incomplete. Classify it as `NeedsRuntimeValidation` or `NeedsCodeReview`.
- Treat unique and singleton ownership as an architectural pin unless repository evidence explicitly proves a compatible ownership path.
- Treat source-name runtime matches as provisional. Prefer prefab GUID and path.
- Do not sum hierarchy GO across nested pool sources; active hierarchy counts can overlap.

## Choose one candidate

Prefer a candidate with meaningful retained or active hierarchy cost, low expected use frequency, stable GUID/path evidence, and no unique/singleton pin. State why it was selected and which evidence is still missing.

Do not batch-apply DestroyOnRecovery. Do not start Addressable handle-registry work as part of a pool migration.

## Audit the ownership boundary

Inspect the prefab's lifetime-owner scripts and direct requested-pool creation sites. Verify:

- C# event subscriptions have symmetric cleanup or scope-bound registration;
- awaited continuations use a lifetime token or a destroyed-object guard;
- requested child pools declare `ReturnToGlobalPool`, `DestroyWithOwner`, or `UseChildPolicy` intentionally;
- nested unique/singleton instances are not destroyed with an ordinary parent;
- static callbacks and caches do not retain destroyed components;
- Addressable ownership is unchanged by the migration.

Static regex evidence is a lead, not proof. Open the cited lines and follow the actual subscription, construction, and cleanup paths.

## Implement in small steps

1. Implement required event, async, lifetime-scope, or requested-pool ownership changes first.
2. Keep package APIs additive and host-project-agnostic.
3. Preserve the existing unique ownership stack.
4. Compile the affected assembly and fix new errors before changing policy.
5. Apply serialized prefab policy only through Unity Editor API or the Inspector. Never externally edit prefab or asset YAML while Unity is open.
6. Record the intended target in a migration plan when the Editor plan importer is available.

Do not modify FancyScrollRect or LayoutGroup conventions as a migration shortcut.

## Validate runtime behavior

Capture snapshots in chronological order:

1. stable baseline page;
2. target open;
3. returned stable page after recovery settles;
4. target reopened;
5. returned stable page again.

Require all applicable checks:

- target active hierarchy cost is observed;
- queued and pending instances do not grow across reopen cycles;
- DestroyOnRecovery or KeepN reaches the intended post-return state;
- owner-bound children do not remain in the global pool;
- reopen content, buttons, images, and state remain correct;
- Console has no MissingReferenceException, NullReferenceException, or duplicate callback;
- Addressable handle ownership is unchanged.

If runtime evidence is unavailable, stop at `NeedsRuntimeValidation`; do not label the migration validated and do not recommend reverting it.

## Report the outcome

Lead with one of these outcomes:

- `Migrated; needs runtime validation`
- `Validated; keep current migrated policy`
- `Blocked by concrete ownership issue`
- `Pinned by unique/singleton contract`
- `Insufficient evidence to choose a target policy`

List changed files, compilation results, the exact remaining validation sequence, and any evidence that must be captured next.
