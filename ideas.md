# C# Native Extension Ideas

These are candidates for C#-specific Panda3D binding improvements at the C++
layer, not post-hoc C# extension methods.  The existing mechanism is
`CSHARP_EXTENSION(...)` in the published C++ class declaration, implemented by
an `Extension<T>` specialization in `*_ext_csharp.h/.cxx`, as already used by
`AsyncFuture` and `GraphicsEngine`.

Because pass 1 emits native C wrappers for these methods, adding one of these
APIs requires a native runtime rebuild/package as well as C# regeneration.
Reserve this path for APIs with real semantic value or measurable reduction in
managed/native crossings and native wrapper allocation.

## Highest-Value Candidates

### Transform and Matrix Decomposition

C# currently has to call multiple native methods and allocate multiple managed
wrappers for common decomposition workflows such as position, orientation, and
scale extraction.  Add C#-specific APIs that fill caller-provided Panda objects.

Potential native APIs:

```cpp
CSHARP_EXTENSION(bool decompose_csharp(LVecBase3 *scale, LVecBase3 *hpr, LVecBase3 *translate) const);
CSHARP_EXTENSION(bool decompose_quat_csharp(LVecBase3 *scale, LQuaternion *quat, LVecBase3 *translate) const);
```

For `TransformState` / `NodePath`:

```cpp
CSHARP_EXTENSION(void get_components_csharp(LPoint3 *pos, LQuaternion *quat, LVecBase3 *scale, LVecBase3 *shear) const);
CSHARP_EXTENSION(void get_transform_components_csharp(LPoint3 *pos, LVecBase3 *hpr, LVecBase3 *scale) const);
```

Preferred managed shape can then be either reusable-object APIs or nicer
wrappers layered over them:

```csharp
transform.GetComponentsInto(pos, quat, scale, shear);
var components = transform.GetComponents();
```

The reusable-object path should be documented as the allocation-conscious one.

### Get-Into Variants for Hot Value Returns

Many hot APIs return value objects by native value, which becomes a wrapper
allocation on the C# side.  Add explicit fill variants for common loops.

Candidates:

```cpp
CSHARP_EXTENSION(void get_pos_into_csharp(LPoint3 *result) const);
CSHARP_EXTENSION(void get_hpr_into_csharp(LVecBase3 *result) const);
CSHARP_EXTENSION(void get_scale_into_csharp(LVecBase3 *result) const);
CSHARP_EXTENSION(void get_quat_into_csharp(LQuaternion *result) const);
CSHARP_EXTENSION(void get_row_into_csharp(int row, LVecBase4 *result) const);
```

Likely target classes:

- `NodePath`
- `TransformState`
- `LMatrix3f` / `LMatrix3d`
- `LMatrix4f` / `LMatrix4d`
- `LQuaternionf` / `LQuaterniond`

## Bulk Data and Span-Oriented APIs

### Geometry Buffer Copy

Python has buffer-protocol support around `GeomVertexArrayData`; C# should have
equivalent bulk APIs.  This is likely the biggest performance opportunity for
dynamic meshes and procedural geometry, but it probably needs generator/runtime
support for pointer-plus-size parameters and managed `Span<T>`/`ReadOnlySpan<T>`.

Potential native APIs:

```cpp
CSHARP_EXTENSION(bool copy_data_from_csharp(const unsigned char *source, size_t size));
CSHARP_EXTENSION(bool copy_subdata_from_csharp(size_t to_start, const unsigned char *source, size_t size));
CSHARP_EXTENSION(size_t copy_data_to_csharp(unsigned char *dest, size_t dest_size) const);
```

Managed target shape:

```csharp
arrayData.CopyDataFrom(ReadOnlySpan<byte> source);
arrayData.CopySubdataFrom(nuint offset, ReadOnlySpan<byte> source);
int written = arrayData.CopyDataTo(Span<byte> destination);
```

This should avoid per-vertex writer calls when the caller already has packed
data.

### Texture and Image Bulk Upload

Apply the same span pattern to procedural textures, screenshots, video frames,
and CPU-side image workflows.

Potential targets:

- `Texture`
- `PNMImage`
- `PfMFile`
- screenshot/readback paths if exposed cleanly

Potential native APIs:

```cpp
CSHARP_EXTENSION(bool set_ram_image_csharp(const unsigned char *source, size_t size));
CSHARP_EXTENSION(size_t copy_ram_image_to_csharp(unsigned char *dest, size_t dest_size) const);
CSHARP_EXTENSION(bool copy_pixels_from_csharp(const unsigned char *source, size_t size));
CSHARP_EXTENSION(size_t copy_pixels_to_csharp(unsigned char *dest, size_t dest_size) const);
```

## Shader and Render-State Ergonomics

### Bulk Shader Inputs

Scalar/vector shader input overloads exist, but C# has poor options for arrays
and for setting several inputs without repeated state composition.

Potential native APIs:

```cpp
CSHARP_EXTENSION(void set_shader_input_float_array_csharp(CPT_InternalName id, const float *values, int count, int priority));
CSHARP_EXTENSION(void set_shader_input_vec4_array_csharp(CPT_InternalName id, const LVecBase4 *values, int count, int priority));
CSHARP_EXTENSION(CPT(RenderAttrib) set_shader_inputs_csharp(const ShaderInput *inputs, int count) const);
```

This may require first-class C# binding support for arrays/spans of native
structs or a small managed/native `ShaderInputBuilder` type.

## Collections and Scene Graph

### Collection Snapshot Helpers

Iteration over generated collections can cost one native call and one wrapper
per element.  Add snapshot helpers for collection-heavy workflows.

Potential targets:

- `NodePathCollection`
- `TextureCollection`
- `TextureStageCollection`
- `MaterialCollection`
- `InternalNameCollection`
- `AsyncTaskCollection`

Potential native APIs:

```cpp
CSHARP_EXTENSION(int copy_paths_csharp(NodePath *dest, int count) const);
CSHARP_EXTENSION(int copy_textures_csharp(Texture **dest, int count) const);
```

This needs careful ownership handling.  It may be better to expose a native
array/list wrapper type first, then layer snapshots over that.

### Bounds Convenience

Python exposes tuple-oriented tight-bounds helpers.  C# should prefer a native
method that fills output objects without allocating temporary tuple-like state.

Potential native APIs:

```cpp
CSHARP_EXTENSION(bool get_tight_bounds_csharp(LPoint3 *min_point, LPoint3 *max_point) const);
CSHARP_EXTENSION(bool get_tight_bounds_csharp(const NodePath *other, LPoint3 *min_point, LPoint3 *max_point) const);
```

This is mostly developer experience, but it also keeps the allocation-conscious
pattern consistent.

## Higher-Risk Ideas

### Managed Object Tags

Python has `python_tag`; a C# equivalent could help attach framework components
or managed state to Panda nodes.

Potential native APIs:

```cpp
CSHARP_EXTENSION(void set_csharp_tag(const std::string &key, intptr_t gc_handle));
CSHARP_EXTENSION(intptr_t get_csharp_tag(const std::string &key) const);
CSHARP_EXTENSION(bool has_csharp_tag(const std::string &key) const);
CSHARP_EXTENSION(void clear_csharp_tag(const std::string &key));
```

Do not implement this casually.  It needs a clear `GCHandle` ownership model,
threading rules, cleanup semantics when nodes die, and a cycle/leak story.

## Suggested Order

1. Add `TransformState` / `LMatrix3/4` decomposition helpers.
2. Add `NodePath` and matrix `GetInto` helpers for hot value-return APIs.
3. Add generator/runtime support for pointer-plus-size parameters mapped to
   `Span<byte>` / `ReadOnlySpan<byte>`.
4. Add geometry buffer and texture/image bulk copy APIs on top of that support.
5. Add shader input bulk helpers.
6. Consider collection snapshots after the ownership model is clear.
7. Defer managed object tags until the framework has a concrete use case and
   lifetime design.
