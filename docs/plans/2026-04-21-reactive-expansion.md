# MitsubishiRx Reactive Expansion Implementation Plan

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Transform MitsubishiRx from a command-oriented reactive PLC client into a subscription-optimized reactive communication library with shared scan planning, quality-rich value streams, reactive write pipelines, full high-value serial parity, and generated typed client access.

**Architecture:** Add a new orchestration layer above the existing command APIs in `src/MitsubishiRx/MitsubishiRx.cs` that plans shared reads, publishes hot cached reactive values, and queues/coalesces writes without breaking the existing low-level APIs. Preserve the current protocol encoders/decoders and typed tag helpers, then layer planner/runtime/generator pieces on top so current consumers remain compatible.

**Tech Stack:** C#/.NET 8/9/10, System.Reactive, ReactiveUI.Extensions, TUnit/Microsoft Testing Platform, existing Mitsubishi MC protocol encoders, optional Roslyn incremental source generator.

---

## Requirements Summary

1. Add a **shared reactive subscription planner / scan engine** that can merge subscriptions, reuse one PLC poll for many observers, and choose the best raw read shape.
2. Add a **reactive value envelope** richer than plain `Responce<T>` for hot streams.
3. Add **reactive write pipelines** for queued/latest-wins/coalesced writes.
4. Finish **remaining serial parity** for the most valuable missing APIs:
   - serial type-name
   - serial loopback
   - serial memory access
   - raw serial command execution
5. Add a **typed generated client surface** from tag schema.
6. Keep the public surface simple and additive; avoid breaking current APIs.
7. Every feature must be test-first and README-synchronized.

## Constraints from Current Codebase

- Current reactive polling is per-stream and does not share scans:
  - `ObserveWords(...)` in `src/MitsubishiRx/MitsubishiRx.cs:1175`
  - `ObserveBits(...)` in `src/MitsubishiRx/MitsubishiRx.cs:1190`
  - `ObserveTagGroup(...)` in `src/MitsubishiRx/MitsubishiRx.cs:1242`
- Group reads are currently sequential per tag rather than batched/planned:
  - `ReadTagGroupSnapshotAsync(...)` in `src/MitsubishiRx/MitsubishiRx.cs:593-610`
- Existing diagnostics/state streams already exist and should be reused:
  - `ConnectionStates` in `src/MitsubishiRx/MitsubishiRx.cs:711`
  - `OperationLogs` in `src/MitsubishiRx/MitsubishiRx.cs:716`
- Retry/backoff already exists in the request core:
  - `ExecuteObservableAsync(...)` in `src/MitsubishiRx/MitsubishiRx.cs:1407-1414`
- Typed tag helpers already exist and should be reused by generated APIs:
  - tag read/write helpers throughout `src/MitsubishiRx/MitsubishiRx.cs:83-356`
- Raw serial execution is still explicitly missing:
  - `ExecuteRawAsync(...)` in `src/MitsubishiRx/MitsubishiRx.cs:805-815`
- Type-name / loopback / memory APIs currently route only through Ethernet encoders:
  - `ReadTypeNameAsync(...)` in `src/MitsubishiRx/MitsubishiRx.cs:1026`
  - `LoopbackAsync(...)` in `src/MitsubishiRx/MitsubishiRx.cs:1117`
  - `ReadMemoryAsync(...)` / `WriteMemoryAsync(...)` in `src/MitsubishiRx/MitsubishiRx.cs:1137-1163`
- Current model layer has operation logs and type info, but no reactive value envelope yet:
  - `MitsubishiOperationLog` and `MitsubishiTypeName` in `src/MitsubishiRx/MitsubishiModels.cs:105-129`

## Acceptance Criteria

1. A caller can observe a tag/address through a **shared hot stream** without creating redundant PLC polls.
2. Overlapping subscriptions can be optimized into fewer requests by a scan planner.
3. Reactive value streams expose timestamp/quality/stale/heartbeat metadata.
4. Callers can use write-side reactive APIs for latest-wins and queued/coalesced writes.
5. Serial `ReadTypeNameAsync`, `LoopbackAsync`, `ReadMemoryAsync`, `WriteMemoryAsync`, and `ExecuteRawAsync` work for the supported serial frame families documented by tests.
6. A generated typed client can expose strongly typed accessors based on the tag schema.
7. Full test suite remains green.
8. README documents only tested behavior.

## ADR

**Decision:** Implement the expansion in phases by layering new reactive planning/runtime components above the stable request/transport core instead of rewriting the current client.

**Drivers:**
- preserve the existing public API and verified protocol behavior
- maximize reuse of the current typed tag helpers and protocol encoders
- keep risk bounded by shipping in independently testable slices

**Alternatives considered:**
1. Rewrite `MitsubishiRx` around a new central subscription runtime immediately.
2. Add only more protocol commands and skip the planner/runtime layer.
3. Layer a planner/runtime/value model/generator on top of the current client incrementally.

**Why chosen:** Option 3 gives the biggest end-user win while preserving the large verified surface already in place. It also maps cleanly onto TDD slices and reduces regression risk.

**Consequences:**
- some temporary duplication between legacy reactive APIs and new planner APIs
- a new runtime/cache layer must be carefully tested for concurrency and disposal correctness
- source generation introduces a new project and build integration

**Follow-ups:**
- add benchmarks after the scan planner stabilizes
- consider a future deprecation path from per-stream polling APIs to planner-backed overloads
- add metrics hooks once the runtime model is in place

---

## Phase 1 — Reactive Value Envelope + Shared Scan Planning Core

### Task 1: Add the reactive value model types

**Objective:** Introduce transport-agnostic reactive value envelopes and quality metadata for hot streams.

**Files:**
- Modify: `src/MitsubishiRx/MitsubishiModels.cs`
- Create: `src/MitsubishiRx/Reactive/MitsubishiReactiveValues.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiReactiveValueTests.cs`

**Step 1: Write failing tests**

Create tests covering:
- `MitsubishiReactiveValue<T>` stores `Value`, `TimestampUtc`, `Quality`, `IsHeartbeat`, `IsStale`, `Source`
- quality enum values distinguish at least `Good`, `Bad`, `Stale`, `Heartbeat`, `Error`
- conversion helpers can wrap `Responce<T>` into a good/error reactive value

**Step 2: Run test to verify failure**

Run:
`"/mnt/c/Program Files/dotnet/dotnet.exe" test --project src/MitsubishiRx.Tests/MitsubishiRx.Tests.csproj -v minimal`

Expected: missing type/member failures.

**Step 3: Write minimal implementation**

Add:
- `MitsubishiReactiveQuality`
- `MitsubishiReactiveValue<T>`
- small helper extensions for wrapping `Responce<T>`

Keep the type immutable and record-based.

**Step 4: Run tests to verify pass**

Run full tests.

**Step 5: Commit**

`git commit -m "feat: add reactive value envelope models"`

---

### Task 2: Add scan request description models

**Objective:** Create internal planner models describing what should be scanned.

**Files:**
- Create: `src/MitsubishiRx/Reactive/MitsubishiScanModels.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiScanPlannerTests.cs`

**Step 1: Write failing tests**

Cover:
- word request spec model
- bit request spec model
- scan class definition model
- planner input can represent tag and raw-address subscriptions

**Step 2: Verify failure**

Run tests.

**Step 3: Implement models**

Add small immutable models such as:
- `MitsubishiScanItem`
- `MitsubishiScanPlan`
- `MitsubishiScanClassDefinition`
- `MitsubishiReactiveSubscriptionKey`

**Step 4: Run tests**

**Step 5: Commit**

`git commit -m "feat: add scan planner models"`

---

### Task 3: Add a first scan planner for contiguous and sparse word requests

**Objective:** Plan optimized reads from a set of requested items.

**Files:**
- Create: `src/MitsubishiRx/Reactive/MitsubishiScanPlanner.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiScanPlannerTests.cs`

**Step 1: Write failing tests**

Cover at least:
- contiguous word tags collapse into one batch read
- sparse word tags collapse into one random read when beneficial
- mixed incompatible types stay separate
- bit requests remain separate from word requests

**Step 2: Verify failure**

**Step 3: Implement minimal planner**

Keep scope tight:
- plan only words and bits first
- use current command families already implemented:
  - batch read
  - random read
  - optionally block read when mixed regions justify it

**Step 4: Run tests**

**Step 5: Commit**

`git commit -m "feat: add initial reactive scan planner"`

---

### Task 4: Add shared hot observable cache/runtime for words

**Objective:** Make multiple subscribers share one underlying poll and get replay/latest semantics.

**Files:**
- Create: `src/MitsubishiRx/Reactive/MitsubishiScanRuntime.cs`
- Modify: `src/MitsubishiRx/MitsubishiRx.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiReactiveScanRuntimeTests.cs`

**Step 1: Write failing tests**

Cover:
- two observers of the same word range share one transport poll cycle
- late subscriber gets latest cached value
- disposing the last subscription tears down the poll

Use `FakeTransport` and request-count assertions.

**Step 2: Verify failure**

**Step 3: Implement minimal runtime**

Add internal cache keyed by scan/subscription key. Use Rx publish/replay patterns and existing scheduler.

**Step 4: Run tests**

**Step 5: Commit**

`git commit -m "feat: add shared reactive scan runtime for words"`

---

### Task 5: Add simple public APIs for hot reactive values

**Objective:** Expose a simple high-level API without replacing existing methods.

**Files:**
- Modify: `src/MitsubishiRx/MitsubishiRx.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiReactiveScanRuntimeTests.cs`
- Docs: `README.md`

**Step 1: Write failing tests**

Add tests for APIs like:
- `ObserveWordValues(...)` or `ObserveReactiveWords(...)`
- `ObserveTag<T>(...)` for at least one word tag
- returned values are `MitsubishiReactiveValue<T>` rather than raw `Responce<T>` only

**Step 2: Verify failure**

**Step 3: Implement minimal API**

Do not replace `ObserveWords(...)`; add planner-backed overloads.

**Step 4: Run tests**

**Step 5: Commit**

`git commit -m "feat: expose planner-backed reactive value APIs"`

---

## Phase 2 — Reactive Write Pipelines

### Task 6: Add write request envelope and queue models

**Objective:** Introduce first-class write-pipeline models.

**Files:**
- Create: `src/MitsubishiRx/Reactive/MitsubishiWritePipelineModels.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiReactiveWritePipelineTests.cs`

**Step 1: Write failing tests**

Cover queue items with:
- target
- payload
- enqueue timestamp
- write mode (`Queued`, `LatestWins`, `Coalescing`)

**Step 2-5:** TDD as above.

---

### Task 7: Implement latest-wins write pipeline

**Objective:** Support noisy setpoint streams without flooding PLC writes.

**Files:**
- Create: `src/MitsubishiRx/Reactive/MitsubishiWritePipeline.cs`
- Modify: `src/MitsubishiRx/MitsubishiRx.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiReactiveWritePipelineTests.cs`

**Step 1: Write failing tests**

Cover:
- rapid writes collapse to latest value
- only final value is sent when configured latest-wins
- errors propagate as failed reactive values/logs

**Step 2-5:** TDD.

---

### Task 8: Implement queued/coalesced writes for tags and raw addresses

**Objective:** Support serialized write queues and write coalescing.

**Files:**
- Modify: `src/MitsubishiRx/Reactive/MitsubishiWritePipeline.cs`
- Modify: `src/MitsubishiRx/MitsubishiRx.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiReactiveWritePipelineTests.cs`

**Step 1: Write failing tests**

Cover:
- queued mode preserves order
- coalesced mode merges equivalent writes
- tag-based write pipeline delegates through existing typed tag writers

**Step 2-5:** TDD.

---

## Phase 3 — Remaining Serial Parity

### Task 9: Add serial type-name support

**Objective:** Route `ReadTypeNameAsync(...)` through serial encoders where supported.

**Files:**
- Modify: `src/MitsubishiRx/MitsubishiSerialProtocolEncoding.cs`
- Modify: `src/MitsubishiRx/MitsubishiRx.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiSerialTypeNameTests.cs`

**Step 1: Write failing tests**

Cover:
- `1C` unsupported if appropriate
- `3C` ASCII request/response
- `4C` binary format 5 request/response if supported in repo conventions

**Step 2-5:** TDD.

---

### Task 10: Add serial loopback support

**Objective:** Route `LoopbackAsync(...)` through serial encoders.

**Files:**
- Modify: `src/MitsubishiRx/MitsubishiSerialProtocolEncoding.cs`
- Modify: `src/MitsubishiRx/MitsubishiRx.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiSerialLoopbackTests.cs`

**Step 1-5:** TDD.

---

### Task 11: Add serial memory access support

**Objective:** Route `ReadMemoryAsync(...)` / `WriteMemoryAsync(...)` through serial encoders where valid.

**Files:**
- Modify: `src/MitsubishiRx/MitsubishiSerialProtocolEncoding.cs`
- Modify: `src/MitsubishiRx/MitsubishiRx.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiSerialMemoryTests.cs`

**Step 1-5:** TDD.

---

### Task 12: Add raw serial command execution

**Objective:** Remove the explicit serial raw-command gap in `ExecuteRawAsync(...)`.

**Files:**
- Modify: `src/MitsubishiRx/MitsubishiRx.cs`
- Modify: `src/MitsubishiRx/MitsubishiSerialProtocolEncoding.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiSerialRawCommandTests.cs`

**Step 1: Write failing tests**

Cover:
- exact request framing for serial raw commands by frame family
- raw payload returned correctly
- unsupported serial frame cases fail explicitly

**Step 2-5:** TDD.

---

## Phase 4 — Generated Typed Client Surface

### Task 13: Add source generator project skeleton

**Objective:** Create an incremental generator project that can emit typed accessors from tag schema metadata.

**Files:**
- Create: `src/MitsubishiRx.Generators/MitsubishiRx.Generators.csproj`
- Create: `src/MitsubishiRx.Generators/*.cs`
- Modify: `src/MitsubishiRx/MitsubishiRx.csproj`
- Test: `src/MitsubishiRx.Tests/MitsubishiGeneratedClientTests.cs`

**Step 1: Write failing tests**

Use golden-output or public API verification for generated members.

**Step 2: Verify failure**

**Step 3: Implement project skeleton**

Add an incremental generator that can at least emit a small strongly typed client class for a provided schema description.

**Step 4: Run tests**

**Step 5: Commit**

`git commit -m "feat: add source generator skeleton for typed clients"`

---

### Task 14: Generate typed tag accessors

**Objective:** Emit strongly typed read/write/observe accessors per tag.

**Files:**
- Modify: `src/MitsubishiRx.Generators/*.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiGeneratedClientTests.cs`

**Step 1: Write failing tests**

Cover generation of APIs like:
- `client.Tags.MotorSpeed.ReadAsync()`
- `client.Tags.MotorSpeed.Observe()`
- `client.Tags.Mode.WriteAsync(value)`

**Step 2-5:** TDD.

---

### Task 15: Generate typed group accessors

**Objective:** Emit grouped clients/snapshots for registered tag groups.

**Files:**
- Modify: `src/MitsubishiRx.Generators/*.cs`
- Test: `src/MitsubishiRx.Tests/MitsubishiGeneratedClientTests.cs`

**Step 1-5:** TDD.

---

## Phase 5 — Documentation / Verification / Cleanup

### Task 16: Update README for new reactive planner/runtime APIs

**Objective:** Document the powerful-but-simple user-facing story.

**Files:**
- Modify: `README.md`

**Step 1:** Update the feature matrix and examples for:
- reactive value envelope
- shared scans / scan classes
- write pipelines
- serial parity additions
- generated typed clients

**Step 2:** Ensure wording stays conservative and test-backed.

**Step 3:** Commit.

---

### Task 17: Full verification pass

**Objective:** Prove the whole repo still builds/tests cleanly.

**Files:**
- No code changes required unless regressions found

**Step 1: Run build**

`"/mnt/c/Program Files/dotnet/dotnet.exe" build src/MitsubishiRx.slnx -v minimal`

**Step 2: Run tests**

`"/mnt/c/Program Files/dotnet/dotnet.exe" test --project src/MitsubishiRx.Tests/MitsubishiRx.Tests.csproj -v minimal`

**Step 3: If failures occur**

Fix them with TDD before proceeding.

**Step 4: Commit final integration result**

`git commit -m "feat: complete reactive scan/write/serial/generated client expansion"`

---

## Risks and Mitigations

### Risk 1: Planner/runtime complexity causes subtle concurrency bugs
**Mitigation:** Keep runtime slices tiny, test request counts/disposal/hot replay explicitly, and reuse current `_requestGate` request serialization in `MitsubishiRx.cs:1419-1444`.

### Risk 2: Generated client adds build complexity across multi-target frameworks
**Mitigation:** Add the generator as a separate project, keep initial output minimal, and verify on the existing `net8.0;net9.0;net10.0` targets in `src/MitsubishiRx/MitsubishiRx.csproj:3`.

### Risk 3: Serial parity claims overreach protocol reality
**Mitigation:** Follow the same conservative pattern already used for serial expansion: tests first, explicit unsupported behavior where needed, and README limited to verified cases.

### Risk 4: New APIs duplicate older APIs and confuse users
**Mitigation:** Keep legacy APIs, but clearly mark new planner-backed APIs in README as the preferred high-level reactive surface.

## Verification Steps

1. Every new slice starts with failing TUnit tests.
2. Full suite must remain green after each slice.
3. README must only claim features with passing tests.
4. For planner/runtime slices, verify reduced transport request counts in tests.
5. For generated-client slices, verify public generated member names and delegate behavior.

## Recommended Execution Order

1. Phase 1 first — this delivers the biggest end-user win.
2. Phase 2 second — makes write-side equally reactive.
3. Phase 3 third — closes major serial capability gaps.
4. Phase 4 fourth — ergonomic polish once runtime concepts are stable.
5. Phase 5 last — docs and full integration verification.

## Available-Agent-Types Roster

- **protocol-runtime implementer** — best for planner/runtime/cache/write-pipeline internals
- **serial-protocol implementer** — best for 1C/3C/4C request/response work
- **test engineer** — best for TUnit fixture design, transport-count assertions, regression coverage
- **generator engineer** — best for Roslyn incremental generator setup and generated API design
- **docs reviewer** — best for README truthfulness and usage examples
- **critic/reviewer** — best for spec and code-quality review passes

## Follow-up Staffing Guidance

### Ralph path
- Lane 1: protocol-runtime implementer, medium/high reasoning
- Lane 2: test engineer, medium reasoning
- Lane 3: serial-protocol implementer, medium reasoning
- Lane 4: generator engineer, high reasoning
- Final pass: docs reviewer + critic

### Team path
- Worker A: Phase 1 planner/runtime core
- Worker B: Phase 2 write pipelines
- Worker C: Phase 3 serial parity
- Worker D: Phase 4 generator work
- Verifier: integration/docs/full-suite review

## Team Verification Path

Before shutdown the team must prove:
1. request-count reduction for shared scans
2. correct hot replay/disposal behavior
3. latest-wins and queued/coalesced write semantics
4. serial parity tests passing for each newly supported command family
5. generated client delegates correctly to existing APIs
6. full build/tests green

After handoff, Ralph verifies README alignment and final integration cleanliness.
