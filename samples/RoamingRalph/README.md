# Roaming Ralph

A compact Panda3D.Framework version of Panda3D's Python Roaming Ralph sample.

The sample demonstrates the framework shape for a small playable demo:

- `Program.cs` composes the host, rendering, input, scheduling, and actor services.
- `RoamingRalphDemo` loads the world and actor during bootstrap.
- A frame task replaces Python's `taskMgr.add(...)` movement task.
- Input actions replace the Python sample's key map.
- Native collision rays and a pusher keep Ralph and the camera on the terrain.
- The sample uses the generated PascalCase C# aliases, including `SetIntoCollideMask`.
- **Assets come from the build** — a live use of the [resource pipeline](../../docs/04-resources.md).

## Assets

The source art lives in [`samples/models`](../models) (shared with the multiplayer sample) and is never
loaded from there at runtime. The build compiles each `.egg.pz` to a `.bam` with `egg2bam` and packs them
into **`ralph.mf`**, dropped next to the executable — that is the whole declaration:

```xml
<PandaEgg    Include="../models/*.egg.pz" Bundle="ralph.mf" />
<PandaBundle Include="ralph.mf" Compression="6" />
```

The textures aren't listed anywhere: egg2bam copies each one a model references into the bundle and
rewrites the reference to match, so the models carry their own textures. At startup the demo mounts the
multifile at `/models` and loads everything out of it — no asset paths to locate on disk, nothing to
configure. Editing a texture rebuilds the bam that uses it.

Run it with:

```bash
dotnet run -c Debug --project samples/RoamingRalph/RoamingRalph.csproj
```
