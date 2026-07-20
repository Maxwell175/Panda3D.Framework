# Panda3D.Framework.Build

The Panda3D.Framework resource pipeline, delivered as an **MSBuild-only** NuGet package — it ships **no runtime assembly** and emits no mounting code. One `PackageReference` folds `egg2bam`, multifile (`.mf`) packing, and asset copy into `dotnet build` / `run` / `publish`, driven entirely from item groups in your `.csproj` over the [`Panda3D.Tools`](https://www.nuget.org/packages/Panda3D.Tools) native CLIs (pulled in transitively, host-RID-selected).

## What it does

Three composable stages, keyed off a single piece of metadata (`Bundle`):

1. **Build** — run source art through a processor (`.egg`/`.egg.pz` → `.bam` via the built-in `Egg2Bam`, or your own `PandaProcessor` for `blend2bam`, palettize, shader compilers, …).
2. **Pack** — `multify` the results into `.mf` bundles.
3. **Deploy** — copy loose files and finished bundles next to the build output (`$(OutDir)`), carried into `dotnet publish` at the same layout.

An asset tagged `Bundle="x.mf"` is packed into that bundle; without `Bundle` it is loose-copied. Built models carry their referenced textures automatically, and a texture edit re-triggers the bam build (dependency tracking via the transitively referenced `Panda3D.ModelDeps` analyzer).

## Item groups

| Item | Role |
|---|---|
| `PandaEgg` | `.egg`/`.egg.pz` → `.bam` (sugar for a `PandaResource` bound to the built-in `Egg2Bam`). |
| `PandaResource` | any input run through a processor (matched by extension, or an explicit `Processor=`). |
| `PandaProcessor` | a named, reusable build rule (`Command` **or** `Tool`+`Args`, `Extensions`, output naming). |
| `PandaContent` | loose-copied to the content root, or staged for packing when `Bundle` is set. |
| `PandaBundle` | a bundle's identity: `Compression` (0–9) and extra `multify` `Options`. |

Common metadata: `Bundle` (target `.mf`; unset = loose copy), `BundlePath` (layout inside the bundle), `Options` (extra tool flags passed through), `TrackTextures`.

## Usage

From a game's `.csproj` — build the source eggs to `.bam` and pack them into `ralph.mf` next to the executable:

```xml
<ItemGroup>
  <PackageReference Include="Panda3D.Framework.Build" Version="1.11.0-*" />
</ItemGroup>

<ItemGroup>
  <PandaEgg Include="../models/*.egg.pz" Bundle="ralph.mf" />
  <PandaBundle Include="ralph.mf" Compression="6" />
</ItemGroup>
```

The pipeline stops at artifacts and emits no mount code — *when* and *how* to mount `ralph.mf` into the VFS (a C# `VirtualFileSystem.Mount` call, or a `vfs-mount` line in your `.prc`) is a runtime one-liner the game owns. See `docs/04-resources.md`.

## License

BSD-3-Clause.
