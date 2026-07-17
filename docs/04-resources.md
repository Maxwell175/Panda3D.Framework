# 04 — Resource Pipeline (`Panda3D.Framework.Build`)

**Purpose.** Turn a game's raw art and data into shippable Panda assets *as part of `dotnet build`*, with three levels of engagement that compose into one pipeline: **blind copy**, **multifile packing**, and **resource building** (egg→bam and an escape hatch for arbitrary processors). This is an **MSBuild-only** concern — it ships no runtime assembly and emits **no mounting code**. Its job ends when the finished `.mf` bundles and loose files sit at a fixed, known path next to the build output; *when* and *how* to mount them into the VFS is the consuming game's call, a one-liner (see **Consuming the output**).

**Replaces in `direct`.** Nothing structural — Python has no build step. Today you run `egg2bam`/`multify` by hand (or a Makefile), and you tell the app where the loose models live with `model-path`/env-vars. This pipeline folds that manual, per-project ceremony into the normal build, and the Roaming Ralph sample's `ResolveModelRoot()` env-var probing is exactly the boilerplate it removes.

**Delivery.** A build-tools NuGet package, `Panda3D.Framework.Build` — no managed lib output, ships only `buildTransitive/Panda3D.Framework.Build.props` + `.targets` (applies to direct *and* transitive references). It depends on two host-selected tool packages, both flowing transitively so one `PackageReference` gets the consumer everything: **`Panda3D.Tools`** (the native CLIs — `egg2bam`/`multify`/… as `$(Panda3D…)` path properties, exec-bit auto-repaired) and **`Panda3D.ModelDeps`** (the NativeAOT texture-dependency analyzer, `$(PandaModelDeps)`, in [its own repo](https://github.com/Maxwell175/Panda3D.ModelDeps) so it versions separately). Every tool is driven by a small inline resolver task plus `<Exec>` against those properties — no hand-computed rid math, no shipped *managed* task assembly. Because `.bam`/`.mf` are platform-neutral, tools run on the **build host** and their output ships unchanged to every target RID.

---

## The pipeline

Three stages; every asset item flows through as far upstream as it needs to enter, then rides the rest to the output:

```
   LEVEL 3 · BUILD          LEVEL 2 · PACK          LEVEL 1 · DEPLOY
  processors            ─▶  multify per bundle  ─▶  copy tree to $(PandaContentRoot)
  (egg2bam / custom)        (staged layout)         + drop .mf bundles there
        ▲ raw .egg/.blend         ▲ any asset with          ▲ finished files
        │ + Bundle metadata       │ Bundle set              │ (no Bundle → loose copy)
```

**The rule that unifies the levels:** every asset item carries an optional `Bundle` metadata. **Set → the asset is packed into that `.mf`. Unset → the asset is loose-copied to the content root.** A built `.bam` (Level 3) tagged `Bundle="content.mf"` therefore passes through all three stages; a prebuilt texture with no `Bundle` skips straight to deploy (Level 1). One project mixes the levels freely — models built and packed, config files loose, a prebuilt bundle blind-copied — with no special cases. The *layout inside* a bundle is decoupled from source layout by `BundlePath`.

---

## Public surface (MSBuild)

**Items.** All are optional; a project uses only the levels it needs.

| Item | Level | Role | Key metadata |
|---|---|---|---|
| `PandaContent` | 1 (→2) | copied loose, or staged for pack | `Bundle`, `BundlePath` |
| `PandaResource` | 3 | an input run through a **processor** | `Processor` (optional; else matched by extension), `Options`, `Bundle`, `BundlePath`, `TrackTextures` |
| `PandaProcessor` | 3 (rule) | a **named, reusable** build rule — declared once, applied to many inputs | `Command` **or** `Tool`+`Args`; `Extensions`; `OutputExtension`/`OutputPattern`; `BuiltIn`; `CopiesTextures` |
| `PandaEgg` | 3 (sugar) | `PandaResource` pre-bound to the built-in `Egg2Bam` processor | `Options`, `Bundle`, `BundlePath`, `TrackTextures` |
| `PandaBundle` | 2 (declaration) | bundle identity for the pack stage | `Compression` (0–9), `Options` (extra `multify` flags) |

**Metadata semantics.** `Bundle` = target `.mf` name (unset → loose copy). `BundlePath` = destination path *inside the bundle* / *under the content root* prepended to the source's `%(RecursiveDir)` tree (default empty → tree preserved as-is). `Processor` on `PandaResource` names the `PandaProcessor` to run; **omit it and the input is matched to a processor by its extension** — the common path, no per-item wiring. A processor derives each output from `OutputExtension` (swap the input's extension) or `OutputPattern` (e.g. `{filename}.bam`); that derived output is the incremental key *and* what enters the pack/deploy rule — the item never restates it. `Options` on a resource is the **full-surface escape**: the built-in `Args` templates cover only the common flags, so `Options` passes any extra tool switches straight through (see tokens). `PandaBundle` is *optional*; a `Bundle` referenced with no matching declaration gets defaults, and its own `Options` appends extra `multify` flags (encryption, header prefix, a custom `-Z` no-compress list, …).

**Processor command tokens.** A `PandaProcessor` `Command`/`Args` is templated per input with `{input}`, `{output}`, `{filename}`, `{inputdir}`, `{outputdir}`, `{options}` (the resource's `Options`; if a template omits `{options}` but the item supplies some, they are appended), and **`{modelroots}`** (each `PandaModelRoot` as an `-pp <dir>` flag, so egg2bam searches the same texture roots the analyzer does). A custom processor supplies `Command="…"` (any shell) or `Tool="…" Args="…"`; a built-in processor sets `BuiltIn="true"` with a bare `Tool` name (`egg2bam`) that the resolver expands to the host-bundle path at build time. Ship-provided built-ins: **`Egg2Bam`** (`.egg`/`.egg.pz` → `.bam`). `egg-palettize` and the rest of the ~50 bundled tools are one `PandaProcessor` line away.

**Properties.**

| Property | Default | Meaning |
|---|---|---|
| `PandaResourcePipeline` | `true` | Master switch; `false` disables all targets below. |
| `PandaContentRoot` | `$(OutDir)` | **The fixed output path.** Bundles and loose files land here — directly next to the assembly, no imposed subfolder (a game that wants one puts it in the bundle's own layout or overrides this). |
| `PandaBuildDir` | `$(IntermediateOutputPath)panda/build/` | Intermediate cache for processor outputs (in `obj`). |
| `PandaStageDir` | `$(IntermediateOutputPath)panda/stage/` | Per-bundle staging root (in `obj`). |
| `PandaDefaultCompression` | `6` | Bundle compression when unspecified (`0` = store). |
| `PandaTrackTextures` | `true` | Texture-dependency tracking: a referenced texture changing rebuilds the bam. `false` disables it (per item: `TrackTextures="false"`). |
| `PandaModelRoot` | *(empty)* | `;`-separated dirs the textures are referenced relative to. Fed to the analyzer (`--model-root`) and egg2bam (`-pp`). |
| `PandaAnalyzerExe` | `$(PandaModelDeps)` | Path to `panda-model-deps`; defaults to the host-RID one the `Panda3D.ModelDeps` package exposes. Absent → tracking silently no-ops. |

**Targets** (a driver `PandaResources` runs `AfterTargets="Build"`, so `dotnet build`/`run`/`publish` all trigger it; the stages are ordered by `DependsOnTargets`, each with `Inputs`/`Outputs` for incrementality):

1. `PandaResolve` — the inline resolver: binds each `PandaResource`/`PandaEgg` to its `PandaProcessor` (by `Processor`, else extension), computes each output/dest, expands built-in tool paths, and does the per-file up-to-date check. Cheap; produces the item lists the stages consume.
2. `PandaBuildResources` — runs each stale processor command (`<Exec>`) into `$(PandaBuildDir)`. Per-input up-to-date, so only changed sources rebuild.
3. `PandaStageAssets` — copies built outputs + `PandaContent` to their dests: `$(PandaStageDir)<bundle>/…` for bundled, `$(PandaContentRoot)…` for loose (this *is* the Level-1 deploy).
4. `PandaPackBundles` — one `multify -c` per bundle over its stage dir → `$(PandaContentRoot)<bundle>`. Re-packs only when a *bundled* file changed (a loose change never repacks).

Two more targets sit outside the build chain:
- `PandaPublishResources` (`BeforeTargets="ComputeFilesToPublish"`) — adds the produced bundles + loose files to `@(ResolvedFileToPublish)` at their content-root-relative path, so `dotnet publish` carries them into the publish dir at the same layout they have next to the build output.
- `PandaCleanResources` (`BeforeTargets="Clean"`) — wipes the `obj` caches and deletes exactly the produced files (it must *not* `RemoveDir` the content root now that it is `$(OutDir)`).

**Example — all three levels in one project.**
```xml
<!-- Define a custom processor ONCE: name it, give it a command + how to name outputs. -->
<ItemGroup>
  <PandaProcessor Include="Blend2Bam"
                  Extensions=".blend"
                  Command="blend2bam &quot;{input}&quot; -o &quot;{output}&quot;"
                  OutputExtension=".bam" />
</ItemGroup>

<ItemGroup>
  <!-- L1 blind copy: loose under content root, tree preserved -->
  <PandaContent Include="assets/config/**/*.prc" />

  <!-- L1→L2: same item, Bundle set = packed; BundlePath = layout inside the .mf -->
  <PandaContent Include="assets/textures/**" Bundle="content.mf" BundlePath="tex/" />

  <!-- L3: bare inputs. .egg binds to the built-in Egg2Bam; .blend to Blend2Bam above.
       No command on either line — the processor owns it. -->
  <PandaResource Include="models/**/*.egg"  Bundle="content.mf" />
  <PandaResource Include="art/**/*.blend"    Bundle="content.mf" />

  <!-- Options passes extra tool flags through the processor for these inputs only -->
  <PandaResource Include="models/hi/**/*.egg" Bundle="content.mf" Options="-flatten 1 -ps keep" />

  <!-- optional: declare the bundle's compression + any extra multify flags -->
  <PandaBundle Include="content.mf" Compression="6" Options="-Z &quot;jpg,png&quot;" />
</ItemGroup>
```
`<PandaEgg Include="models/**/*.egg" Bundle="content.mf" />` is exact sugar for the first `PandaResource` line.

---

## Design notes.

- **Three levels are three stages, not three features.** The design is a single process→pack→deploy pipeline with three entry points; the `Bundle` metadata is the whole coupling. This is what lets a built `.bam` be packed, a raw file be blind-copied, and both live in the same `<ItemGroup>` — instead of three parallel, overlapping systems the user has to reconcile.
- **Loose copy is the default.** An untagged asset is copied, not silently swallowed into a bundle. Bundling is opt-in per item (or per glob). During development you can eyeball the exact output tree; you pack only what you deliberately choose to. (Decision of record — the alternative, an implicit default bundle, was rejected as surprising.)
- **Staging gives layout control and a clean incremental boundary.** `multify` stores paths relative to a `-C` directory, so the pipeline first stages every asset into `$(PandaStageDir)<bundle>/` at its final in-bundle layout, then runs one `multify -c` per bundle over that directory (`-C` = the stage dir). `BundlePath` is realized as the staged path. The resolver does per-file up-to-date checks so only changed sources rebuild; the pack step re-runs only when a *bundled* staged file changed. Asset builds are slow — the incremental discipline is load-bearing. (A recreate, `-c`, rather than `-u` update, is deliberate for v1: it keeps the `.mf` an exact mirror of the current asset set; removals are handled by `Clean`.)
- **Processors are declared once, applied to many.** Level 3 is one item type — `PandaResource` (an input) — plus `PandaProcessor` (a build rule). The command lives in the processor; inputs are bare file lists that bind to it by extension (or an explicit `Processor=`). Adding a hundred `.blend` files repeats no command. `egg2bam` ships as a **built-in processor** (so `.egg` just works and `PandaEgg` is a one-word alias), and a game registers `blend2bam`, `egg-palettize`, a shader compiler, or a texture cruncher with a single `PandaProcessor` line — the escape hatch is the default shape, not a special case. (Two processors claiming one extension → the input must name its `Processor=` explicitly.)
- **Models carry their own textures — a game never declares them.** The built-in `Egg2Bam` runs egg2bam with **`-pc {outputdir}`**, which copies every referenced texture next to the `.bam` it writes *and* rewrites the reference to that bare filename. File and reference are therefore consistent by construction — the tool decides both, so they cannot drift. The processor is flagged `CopiesTextures="true"`, which tells the resolver to stage those copies alongside the bam (deduped — models share textures), so they land in the same bundle automatically. This is why the Roaming Ralph samples list only `<PandaEgg …/>` and no textures. (Without `-pc`, egg2bam's default `-ps rel` records the texture relative to the bam's *build* location under `obj/` — a `../../..` chain that walks straight out of a mounted multifile and fails to resolve.)
- **The tool's full surface stays reachable.** A built-in processor's `Args` hard-codes only the common invocation, which is never a tool's whole CLI. `Options` on any resource threads extra flags through the `{options}` slot (and appends them if a custom template omits the slot); a per-`PandaBundle` `Options` does the same for `multify`. When even that isn't enough, a user `PandaProcessor` with a full `Command` replaces the invocation outright. So no built-in ever boxes a game in.
- **Host-built, target-neutral.** Tools execute on the build host via `Panda3D.Tools`' host-RID selection; `.bam`/`.mf` are portable, so one build feeds every published RID. Nothing here is per-target-RID.
- **Dependency-aware model builds (texture tracking).** A file's mtime isn't its whole dependency — an egg/bam *references* textures, and by default egg2bam stores the reference (not the pixels), so a changed texture needs the bam rebuilt for a correct pipeline. Timestamp-only up-to-date checks miss that. The pipeline runs each model through a small analyzer, **`panda-model-deps`**, that loads it through the Panda bindings (the Loader for bam/obj/gltf/…; `EggData` as an egg fast-path that never touches image data) and lists the textures it references, resolved against `PandaModelRoot`. The resolver folds those textures' mtimes into the bam's `NeedsBuild`, so a texture edit re-triggers egg2bam; a referenced texture missing on disk is a warning. On by default (`PandaTrackTextures`), per-item opt-out. The analyzer (`panda-model-deps`) is a **self-contained NativeAOT** exe (built against the lean/AOT Interop variant + static Panda runtime), shipped host-RID-selected by its **own package/repo `Panda3D.ModelDeps`** — so it needs no .NET runtime or side libraries on the build host, versions independently of the framework, and is not the fragile text-parsing a hand-rolled egg/bam reader would be.
- **Build-only — no runtime mounting is emitted.** The pipeline produces artifacts and stops. It writes no mount `.prc`, no manifest, and no C# — mounting is a runtime policy decision (when, at what mount point, read-only vs. writable, on what search path) that belongs to the game, not the build. The output path is fixed and documented precisely so that decision is a one-liner.

---

## Consuming the output

The pipeline guarantees a **fixed layout**: bundles and loose files sit directly next to the assembly, i.e. `Path.Combine(AppContext.BaseDirectory, "<name>.mf")` at runtime — and `dotnet publish` carries them to the publish dir at the same layout. Mount a bundle whenever the game wants to — the confirmed C# VFS surface makes it one call (shown here as *documentation*; the framework ships none of it):

```csharp
// Mount the packed bundle into the VFS (read-only) at a mount point of your choosing.
var bundle = Path.Combine(AppContext.BaseDirectory, "content.mf");
VirtualFileSystem.GetGlobalPtr().Mount(
    Filename.FromOsSpecific(bundle), Filename.FromOsSpecific("/content"), 0);

// Optional: let relative model names resolve inside it (loader.LoadSync("ralph.bam")).
Panda3DCoreGlobals.GetModelPath().AppendDirectory(Filename.FromOsSpecific("/content"));
```

Equivalently, and without any C#, a `vfs-mount content.mf /content` line in the game's own `.prc` auto-mounts it at engine start. Loose (Level-1) files need no mount at all — they're already beside the executable; load them by name or add their directory to `model-path`.

---

## Open items.

- **A changed `Options`/`Command` doesn't invalidate.** The up-to-date check compares source and output timestamps only, so editing a processor's `Options` (or a `PandaProcessor`'s `Command`) does not rebuild the affected outputs — you have to `Clean` first. Hashing the effective command into the incremental key would fix it.
- **Watch/hot-reload.** A `dotnet watch`-style incremental re-pack on asset change is out of scope for v1; the incremental targets already make a manual rebuild cheap.
- **Egg-to-egg references.** Referenced *textures* are now tracked (see the design note); an egg that `<File>`/`<Instance>`-references *another egg* still isn't a tracked dependency. The analyzer could surface those too.
- **Analyzer cost / per-build spawn.** The analyzer runs once per build (even a no-op incremental one) to get the current texture set, spawning a process + loading Panda. Cheap relative to egg2bam but not free; a cached depfile keyed on egg mtime would remove it from clean incremental builds.
- **Prebuilt-bam deploy deps.** An egg built by `Egg2Bam` carries its textures automatically (see the design note). A *prebuilt* `.bam` that is merely packed does not — nothing copies its textures into the bundle, so they must still be declared as `PandaContent`. Staging an existing bam's analyzer-discovered textures would close the gap.
- **Asset removals rely on `Clean`.** Because pack recreates the `.mf` from the stage dir and staging never deletes, removing a source asset needs a `Clean`/rebuild to drop it from a bundle (the stage dir is wiped on `Clean`). A stage-prune step would remove that caveat.
- **Palettize grouping.** Palettization is inherently multi-input (one atlas over many eggs), which fits the one-input→one-output processor shape awkwardly; a grouped/`.txa`-driven built-in `EggPalettize` processor is future work (today it is a hand-declared `PandaProcessor`).
- **Extract to a compiled task.** The resolver is an inline `RoslynCodeTaskFactory` fragment (keeps the package assembly-free). If it grows, a compiled, unit-tested `netstandard2.0` task assembly (à la `Panda3D.Tools.Tasks`) is the natural next step.

> **Verified (Panda3D.Tools, `csharp/Panda3D.Tools.targets`, `…Tasks/PipelineTasks.cs`).** The package ships `buildTransitive/Panda3D.Tools.targets` exposing host-RID path properties (`$(Panda3DEgg2Bam)`, `$(Panda3DMultify)`, `$(Panda3DEggPalettize)`, …, and `$(Panda3DToolsDir)/<name>$(Panda3DToolExtension)` for any other) and typed tasks (`<Egg2Bam>`, `<Multify>`, `<EggPalettize>`, and ~50 more). Host RID comes from `NETCoreSdkPortableRuntimeIdentifier`; the Unix exec-bit is re-applied automatically on restore. `multify` and `egg2bam` are confirmed bundled.

> **Verified (C# bindings, `cmake-build-debug/csharp/Core`).** `VirtualFileSystem.GetGlobalPtr().Mount(Filename physical, Filename mountPoint, int flags)` and `Mount(Multifile, Filename, int)` exist; `Filename.FromOsSpecific(string)` is the only string→`Filename` path (no string ctor); `Panda3DCoreGlobals.GetModelPath()` returns a `ConfigVariableSearchPath` with `AppendDirectory(Filename)`/`PrependDirectory(Filename)`; the `vfs-mount` PRC directive auto-mounts a `.mf` at VFS init. All the "Consuming the output" calls are real, but remain the consumer's to write.

> **Verified (analyzer, `Panda3D.Egg`/`Panda3D.Core` in `Panda3D.Interop.dll`).** Egg path: `EggData.Read(Filename)` only *parses* (never loads image data, so a missing texture can't fail it, and `.egg.pz` is read via the VFS), then `EggTextureCollection.FindUsedTextures(egg)` + `GetTexture(i).GetFullpath()`. Loader path (bam and any loader-plugin format): `Loader.GetGlobalPtr().LoadSync(Filename)` with default options (texture images deferred — filename returned even when the file is absent), then `new NodePath(node).FindAllTextures()` + `GetTexture(i).GetFullpath()`. `Panda3D.Interop` targets **net8.0** (a separate process, not an in-proc .NET-Framework MSBuild task); the lean/Release variant carries all of these (none are debug-gated) and NativeAOT-links against `Panda3D.Runtime.<rid>`'s static libs. Confirmed end-to-end: a 31 MB self-contained AOT exe reads egg/`.egg.pz`/bam, resolves via `--model-root`, and flags missing textures.

**See also.** [00 Overview](00-overview.md) §3 (reference/package model), §6 (build/run shapes); [09 Actors & Animation](09-actors-animation.md) (the loader that reads these assets); [11 Physics, Collision & Particles](11-physics-collision.md) (collision egg data). Roaming Ralph is the migration target for retiring `ResolveModelRoot()`.
