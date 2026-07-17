# AGENTS.md — Panda3D.Framework

Working notes for agents. This is the C#/.NET application framework layered on top of the
Panda3D game engine's C# bindings. If you touch bindings, the engine, or the generator, read the
"Binding pipeline" and "Generator naming model" sections first — they encode hard-won, non-obvious
facts that are easy to get wrong.

## What this is

A DI-first, ASP.NET-style host and set of subsystems (rendering, input, intervals, physics,
scheduling, events) over Panda3D. Design docs live in `docs/` (`00-overview.md` … `10-gui.md`) —
they are the plan of record. The framework consumes the engine only through the generated
`Panda3D.Interop` C# bindings and the `Panda3D.Async` coroutine layer; it never P/Invokes directly.

## The four repos (ecosystem)

| Path | Role |
|---|---|
| `/home/maxwell/CLionProjects/Panda3D.Framework` | **This repo.** The framework + tests. |
| `/home/maxwell/CLionProjects/panda3d` | The engine. Also hosts `csharp/Panda3D.Interop.csproj` (the bindings package) and the CMake build in `cmake-build-debug/`. |
| `/home/maxwell/CLionProjects/interrogate` | The binding generator (`interrogate` = pass 1 native C wrappers + DB; `interrogate_csharp` = pass 2 C# emit). Also `csharp/Interrogate.Core` (runtime base classes: `NativeObject`, `INativeType`, `Unwrap`, …). |
| `/home/maxwell/CLionProjects/Panda3D.Async` | `PandaTask` coroutine/await layer over `AsyncTask`. The framework depends on it. |

All four are **in scope** — bug fixes and changes to the engine and generator are expected, not just
the framework.

## Build & test

```bash
# Framework (from this repo root)
dotnet test -c Debug                     # builds everything + runs all tests
dotnet restore --force-evaluate          # re-resolve the Panda3D.Interop floating version
```

- Framework references `Panda3D.Interop` via the float `PandaInteropVersion = 1.11.0-preview.*`
  (in `Directory.Build.props`) — it picks the **highest** preview from any source, no lock file.
- `nuget.config` adds the local feed `/home/maxwell/Downloads`. Machine sources also include
  `/mnt/ExtraSSD/OhrTech/InternalApps/NuGetPackages` (where CI previews land).
- Tests are headless: offscreen buffers (`BF_refuse_window` → now `BfRefuseWindow`), deterministic
  clock (`M_non_real_time` → `MNonRealTime`), `DisableTestParallelization` for shared engine globals.
  `tests/VisualTestSupport` holds shared harness code for the visual/offscreen tests.
- Current state: **9 test projects, 84 tests, all green** (Core 18, Rendering 16, Input 16, Intervals 9,
  Actors 8, Physics 5, Gui 5, Particles 4, Audio 3).
- `samples/RoamingRalph` is an end-to-end demo (DI host + actors + collisions + input + overlays);
  `dotnet build` it to smoke-test the bindings, `dotnet run` needs a display + the Panda sample assets
  (set `PANDA3D_ROAMING_RALPH_MODELS` or `PANDA3D_SAMPLES`).

## Binding pipeline (how the C# bindings are produced)

Two passes, per module (`panda3d.core`, `.direct`, `.physics`, `.bullet`, `.egg`, `.fx`, `.vision`,
`.ai`, `._rplight` — 9 total):

1. **`interrogate`** parses C++ headers → a binary `.in` database **and** native C wrapper source
   (`p3<lib>_csharp_supplemental.cxx`, e.g. `_inCUH2XPGNw` returning `(self)->value`). These wrappers
   are compiled into `libpanda.so`. Entry-point symbols are content hashes (`_in…`), stable across
   rebuilds and independent of the C# type name.
2. **`interrogate_csharp`** reads the binary `.in` (no live CPPType/instances) → `.cs` files. It
   CANNOT rebuild `FunctionRemap`s for interrogate-synthesized data-member accessors, so it has a
   wrapper-based fallback (`write_property_from_wrapper`) — this is why public data members
   (e.g. `InputDevice::AxisState::{value,axis,known}`, `BatteryData`) exist at all.

### Regenerating all modules with a locally-built generator

```bash
# 1. build the generator from the checkout
make -C /home/maxwell/CLionProjects/interrogate/build interrogate_csharp
# 2. regenerate — see scratchpad regen_all2.sh: capture each pass-2 command from
#    panda3d/cmake-build-debug via `ninja -t commands csharp/panda3d.<m>.csharp.stamp`,
#    substitute the checkout binary for the build-dir one, run EACH in an isolated `bash -c`.
# 3. rebuild the bindings package
cd /home/maxwell/CLionProjects/panda3d/csharp && dotnet build Panda3D.Interop.csproj -c Release
```

**Regen gotchas (each has bitten us):**
- `build.ninja` is at the TOP of `cmake-build-debug/`, not `cmake/panda/`.
- Each pass-2 ninja command starts with `cd .../cmake/panda &&`, which corrupts the shell cwd for the
  next `ninja -t commands`. **Capture all commands first, then run each in its own subshell.**
- `write_enum_files`/collection-facade writers **skip if the `.cs` already exists** — `rm` the module
  `.cs` files before regenerating or you keep stale output.
- The Interop compile (`dotnet build`, 0 errors) is the ground-truth oracle. Use it to iterate.

### Native `.so`: build-dir vs runtime package

- `panda3d/cmake-build-debug/lib/libpanda.so` is a partial local build with **ZERO** csharp `_in*`
  wrappers — do NOT `nm` it to check symbol presence.
- The framework loads the `.so` from the **`Panda3D.Runtime.linux-x64`** NuGet package
  (`~/.nuget/packages/panda3d.runtime.linux-x64/<ver>/runtimes/linux-x64/native/libpanda.so`), which
  has all ~11.8k wrappers. Check symbols there. Pass-1 always emitted the accessor wrappers, so the
  data-member fix was **managed-only** — no native rebuild needed.

### Packaging Interop into the local feed (versioning gotcha)

```bash
# In Panda3D.Interop.csproj set <Version>1.11.0-preview.NNNNN</Version> DIRECTLY, then:
dotnet pack Panda3D.Interop.csproj -c Release -o /home/maxwell/Downloads
# then REVERT the csproj <Version> back to 1.11.0.
```

- **Do NOT `dotnet pack -p:Version=X`** — it leaks globally to the sibling `Interrogate.Core`
  ProjectReference (`../../interrogate/csharp/Interrogate.Core`, real version 1.2.3) and records a
  dependency on the nonexistent `Interrogate.Core >= X`. Setting `<Version>` in the csproj scopes it.
  Verify: `unzip -p …nupkg '*.nuspec' | grep Interrogate.Core` must read the current `Interrogate.Core`
  version (e.g. `version="1.2.3"`).
- Bump the preview number each pack (last used: `1.11.0-preview.10008`). The framework float picks it
  up after `dotnet restore --force-evaluate`. When you change `Interrogate.Core`, bump ITS version too
  (last: `1.2.3`), pack it into the feed FIRST, then pack Interop (which will depend on the new
  version) — else the framework restore can't resolve the transitive `Interrogate.Core`.
- The shipped `Interrogate.Core 1.2.3` DLL exports `Unwrap` and the checked-cast machinery
  (`IRuntimeTyped`, `CastTo`); checkout `NativeObject.cs` matches, so generated code is runtime-safe.

### Panda3D.Tools package (asset toolchain — CI, not local regen)

The panda3d C# CI (`panda3d/.github/workflows/nuget.yml`) also emits **`Panda3D.Tools`**: Panda's CLI
tools (egg2bam, pzip, egg-trans, egg-optchar, egg-palettize, multify, image-resize, **pview**,
**pstats**, …) for all 4 RIDs in **one** package (Grpc.Tools-style — `tools/<rid>/`, host-selected at
build time), each a self-contained bundle (exes + the Panda `.so`/`.dll` they load). Consume via the
shipped `buildTransitive/Panda3D.Tools.targets`: `$(Panda3DToolsDir)` + per-tool props like
`$(Panda3DEgg2Bam)`, **and one MSBuild task per pipeline tool** (not pview/pstats) — a compiled
`Panda3D.Tools.Tasks.dll` (`csharp/Panda3D.Tools.Tasks/`, 48 `ToolTask` subclasses generated by
`gen_tasks.py`) so a pipeline can write `<Egg2Bam Inputs="m.egg" Output="m.bam" />`. It's the foundation
for the build-integrated asset pipeline. Details, CI wiring, and the NuGet PackagePath / exec-bit gotchas
are in the `panda3d-tools-package` memory. **Not yet CI-validated** (packaging, targets, and the task
mechanism proven locally; the cross-platform build needs a real Actions run).

## Generator naming model (`interfaceMakerCSharp.cxx`)

The single most important mental model when editing the generator. A C++ type name resolves to THREE
different C# forms depending on use:

- **Flat** (`get_class_name` / `get_interface_name`) — `InputDevice_AxisState`, `IInputDevice_AxisState`.
  Used for **filenames, P/Invoke friendly names, `__Opaque_*`, `Destroy_*`, collision keys**. Never change.
- **Simple** (`get_simple_class_name` / `get_simple_interface_name`) — `AxisState`, `IAxisState`. Used
  for the **declaration line, constructor name, `INativeType<>`, `__CreateFromNative`, self-refs**.
- **Nested/dotted** (`get_nested_class_name` / `…interface_name`) — `InputDevice.AxisState`. Used for
  **all references** (params, returns, base lists, enum casts). `get_qualified_*` build on these and
  prepend `global::<ns>.` cross-module. Most reference sites already route through `get_qualified_*`,
  so updating those two functions propagates automatically.

### What's implemented (all verified: Interop 0 errors, framework 74 tests green)

1. **PascalCase enum values + de-dup.** The DB records each value under multiple spellings
   (`LEFT_X`, `left_x`). `make_csharp_enum_member` folds to one PascalCase id; `write_enum_type`
   de-dups. `LEFT_X`/`left_x` → `LeftX`; `DS_done` → `DsDone`. (Distinct from `to_pascal_case`, which
   preserves inner caps and is used for property/method names.)
2. **Nested classes/structs are real nested C# types.** `InputDevice::AxisState` →
   `InputDevice.AxisState` (nested partial class + nested interface), by reopening the outer as
   `partial class` in the still-flat-named file (`InputDevice_AxisState.cs`). ~22 such types.
3. **Enums are NOT nested — they're underscore-free flat.** `AsyncTask::DoneStatus` →
   `AsyncTaskDoneStatus` (concatenated, no `_`), via `get_flat_display_name`.
4. **PascalCase operator aliases.** `write_operator_aliases` emits idiomatic aliases for C++ operators
   alongside the raw `op_*` methods.
5. **C#-interface heuristic + `forcecomplexinheritance` override.** `uses_csharp_interface` (cached via
   `rebuild_csharp_interface_cache`) decides which types get a generated `IXxx` interface. When the
   heuristic wrongly drops one that's needed, a per-module `.N` command file (`forcecomplexinheritance
   <Type>`) forces it. This spans the DB layer: `interrogateType` carries a persisted
   `F_forced_complex_inheritance` flag (so pass 2 can read it from the binary `.in`, same as nesting
   info), exposed through `interrogate_interface` and populated by `interrogateBuilder` reading the
   `.N` file. Test fixtures live in `interrogate/tests/csharp/` (`basic_class.{h,cxx,cs,N}`).

6. **Checked `CastTo<T>` (runtime type system).** `NativeObject.CastTo<T>()` was an unchecked
   reinterpret; it's now a genuine checked downcast, generically for any dtool-based interrogate lib.
   `INativeType<T>` gained `static virtual int TypeHandle => 0`; the generator overrides it
   (`TypeHandle => GetClassType()`) on the **492** classes exposing `get_class_type`+`is_of_type`, and
   adds `, Interrogate.IRuntimeTyped` to the sole `is_of_type` declarer (`TypedObject`). `CastTo` calls
   `is_of_type` only when the target is runtime-typed AND the object's own `GetTypeIndex() != 0`.
   **Why that guard is load-bearing:** `is_of_type` looks the object's own type up in the `TypeRegistry`;
   on an object whose type is unregistered (index 0) that dereferences a null node and **hard-segfaults**
   the process. A `ManagedAsyncTask` (Panda3D.Async's per-epoch task) hit exactly this — its
   `init_type()` was missing from `config_event.cxx`, so `get_type()==0`; the checked cast in
   `DispatcherTable` crashed the whole render loop until the guard was added (native root-cause fix:
   `ManagedAsyncTask::init_type();` now in `config_event.cxx`, effective on the next Runtime rebuild).
   Both native `.so` and the C# guard are needed for robustness; the guard alone is sufficient for
   correctness (an unregistered type degrades to the old unchecked reinterpret).

**Why enums can't nest (the hard C# constraint):** Panda pairs almost every nested enum with a
same-named accessor — `enum Format` + `get_format()` → property `Format`; `enum MakeNonindexed` +
`make_nonindexed()` → method `MakeNonindexed`; some share the outer's own name
(`ShaderAttrib::ShaderAttrib`). C# forbids a nested type and a member (or the enclosing type) sharing a
name (CS0102/CS0542), and ~25% of nested enums collide (even `AsyncTask` has nestable `DoneStatus` but
colliding `State`). So `should_nest_type` requires `is_class()||is_struct()` + a same-name-as-outer
guard; enums stay top-level. A top-level `AsyncTaskDoneStatus` never clashes with an
`AsyncTaskState State {get;}` property.

Residual underscores are expected only when (a) the C++ enum's own name contains one
(`PolylightNode::Attenuation_Type` → `PolylightNodeAttenuation_Type`) or (b) it's a **filename** of a
nested class (`InputDevice_AxisState.cs` holds type `InputDevice.AxisState`) — filenames stay flat.

### Downstream churn from renames

Renaming enum values/types ripples to every consumer. When you rename, update **both**
`Panda3D.Async` and this framework, and re-pack Interop. Examples already applied:
`AsyncTask_DoneStatus.DS_done` → `AsyncTaskDoneStatus.DsDone`, `ClockObject_Mode.M_normal` →
`ClockObjectMode.MNormal`, `InputDevice_Axis.LEFT_X` → `InputDeviceAxis.LeftX`,
`GraphicsPipe_BufferCreationFlags.BF_refuse_window` → `GraphicsPipeBufferCreationFlags.BfRefuseWindow`,
`new InputDevice_BatteryData()` → `new InputDevice.BatteryData()`. **Careful:** xUnit test methods use
`Method_Scenario` names (`ButtonAction_DownAndEdges`) — do NOT rewrite those when doing enum renames.

## Analog input status

The analog-axis gap is CLOSED. `InputDevice.AxisState` exposes `Value`/`Axis`/`Known`;
`DeviceSampler.Axis()` reads `set.OpIndex(i).FindAxis(axis).Value` (guarded by `.Known`). The input
model already handled analog via `IInputSampler` (deadzone/invert/scale/composite/stick tests use a
`FakeSampler`). A runtime round-trip is proven by `NativeSmokeTests.NativeValueStructDataMembersRoundTrip`
(constructs `InputDevice.BatteryData`, round-trips `Level`/`MaxLevel` through real native).

## Status

- **Done & green:** all planned subsystems — Abstractions, Events, Scheduling, Hosting, Rendering
  (incl. multi-view: 2-window, 2-scene, split-screen couch-coop), Input, Intervals, Physics, Audio,
  Particles, Actors (single/multipart models, merged LODs, named anims, blending, subparts/joints,
  timelines), and **Gui** (`IGui` per-view scope, `Widget`/`Widgets`, PGui-backed). Generator:
  data-member emission, PascalCase enums, nested classes, underscore-free enum names, operator aliases,
  interface heuristic + `forcecomplexinheritance`.
- **Next candidates:** more samples beyond RoamingRalph; the C#-specific native binding extensions in
  `ideas.md` (transform/matrix decomposition, `GetInto` hot-path variants, `Span<byte>` bulk
  geometry/texture copy, bulk shader inputs, collection snapshots) — these need a native runtime
  rebuild + repackage, not just C# regen.

## Uncommitted work (nothing is committed yet)

The `Panda3D.Framework` repo has **no commits** — everything is untracked. The `interrogate` and
`Panda3D.Async` checkouts carry uncommitted changes (the generator work above; Async's enum-rename
churn). When these are eventually committed:
- `interrogate/tests/csharp/basic_class.N` is **untracked but needed** (the `.N` fixture for the
  `forcecomplexinheritance` test) — `git add` it or the test breaks.
- `interrogate/Testing/Temporary/*` are CTest run artifacts (timestamp-only churn); ignore/revert them,
  don't commit them.
- Always revert the `Panda3D.Interop.csproj` `<Version>` back to `1.11.0` after packing.

## Conventions

- **Do not commit** unless explicitly asked. Current work (generator edits in the interrogate
  checkout, framework/Async edits) is intentionally left uncommitted; the Interop csproj `<Version>`
  is always reverted to `1.11.0` after packing.
- The default/PR branch for this repo is `main` (work happens on `master` locally).
- Build unit tests for new work; keep the headless-determinism patterns.
