# Advisor report contract

## Legacy normalization

Schema v4 and older use `recommendedPolicy` for both target selection and safety fallback. Normalize before acting. Schema v5 exposes the separated fields directly:

| Legacy classification | Interpret as | Do not infer |
|---|---|---|
| `NeedsOwnerMigration` | Target is blocked until requested-pool ownership is explicit | KeepForever is optimal |
| `NeedsStaticOwnership` | Inspect static callback/cache evidence | Existing migration must be reverted |
| `NeedsEventCleanup` | Inspect subscription cleanup | KeepForever is optimal |
| `NeedsAsyncLifetime` | Inspect awaited continuations and cancellation | KeepForever is optimal |
| `PinnedByUniqueOrSingleton` | Preserve ownership contract | Ordinary parent destruction is safe |
| `InsufficientRuntimeData` | Collect open/return/reopen snapshots | KeepForever is optimal |

An existing `currentPolicy` of `KeepN` or `DestroyOnRecovery` is an intentional migration. Preserve it until concrete runtime or correctness evidence demonstrates a regression.

## Required decision dimensions

Treat these fields independently in reports and migration plans:

- `targetPolicy`: `KeepForever`, `KeepN`, `DestroyOnRecovery`, or `Undetermined`.
- `targetKeepCount`: meaningful only for `KeepN`.
- `migrationStatus`: `NeedsCodeMigration`, `NeedsCodeReview`, `NeedsRuntimeEvidence`, `ReadyToApply`, `NeedsRuntimeValidation`, `Validated`, `InsufficientEvidence`, `Pinned`, or `Blocked`.
- `safetyBlockers`: concrete ownership issues with source file and line evidence.
- `nextAction`: the smallest code, Editor, or runtime-validation step.
- `policyConfidence`: confidence that the target policy fits cost and frequency.
- `safetyConfidence`: confidence that destroying or trimming is lifecycle-safe.

`targetPolicy` must not change to KeepForever merely because `migrationStatus` is blocked.

## Runtime observations

Prefer GUID/path matches. For every candidate, preserve observations per snapshot rather than only totals:

- page and timestamp;
- active, queued, and pending instances;
- active, queued, and pending hierarchy GO;
- requested-pool owner-aware and DestroyWithOwner counts;
- whether the source returned to zero or disappeared after observation.

Active hierarchy GO may include nested requested-pool children. Do not add hierarchy GO across pool sources without deduplication.

## Migration plan boundary

Agent-authored plans may propose serialized changes, but Unity must apply them through Editor API after checking the current prefab GUID and policy. A plan entry should contain:

- prefab GUID and path;
- expected current policy and keep count;
- target policy and keep count;
- requested-child recovery modes when applicable;
- code changes already completed;
- unresolved blockers;
- required runtime validation steps.

Never use a migration plan as authorization to batch-convert all candidates.
