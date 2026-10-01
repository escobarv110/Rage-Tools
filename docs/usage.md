# RAGE Tools

A small standalone Windows tool for previewing and editing the embedded lights
(`LightAttributes`) of GTA V models — a "mini-CodeWalker" focused only on lighting.

## Requirements

- Windows 10/11, 64-bit
- A Direct3D 11 capable GPU
- [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
  (if the app doesn't start, install "Desktop Runtime 8.0.x — Windows x64")

## Run

Unzip and start `Binaries\RageLightEditor.exe`, then drag a `.ydr` or `.yft` onto the
window — or pass a file on the command line:

```bash
Binaries\RageLightEditor.exe path\to\model.ydr
```

A sample scene with three lights is included: `TestAssets\light_test_scene.ydr`.

## Build from source

```bash
dotnet build Source\RageLightEditor.sln -c Release
```

The solution contains two projects: `CodeWalker.Core` (resource parsing/building,
netstandard2.0) and `RageLightEditor` (the app, net8.0-windows). NuGet restores
SharpDX 4.2.0 and ImGui.NET automatically.

---

## Usage

### Viewport

All shortcuts below are defaults — every one is rebindable in the right panel's
**Shortcuts** section (stored in `settings.json` next to the exe).

| Input | Action |
|---|---|
| Left-drag | Orbit camera |
| Right-click | **Select** the light under the cursor, or the prop if there isn't one |
| Middle-drag | Pan |
| Mouse wheel | Zoom (walk speed while in walk mode) |
| Left-click a light marker | Select that light |
| `Q` / `W` / `E` | Gizmo mode: Select / **Move** / **Rotate** (3ds-Max style) |
| `W`/`A`/`S`/`D`, `R`/`C` | Fly / strafe, up / down — see the note below |
| Drag gizmo | Axis arrows / plane handles / centre box move; rings rotate (snapped, default 5°) |
| `Shift`+drag gizmo | **Clone** the light and drag the copy (confirm on drop) |
| `V` | Toggle **walk mode**: WASD + `E`/`Q` up/down, drag = look, wheel = speed, `Esc` exits |
| Arrow keys | Walk, `PgUp`/`PgDn` up/down, `Shift` fast / `Ctrl` slow |
| `F` | Frame selected light (or whole model) |
| `Ctrl+S` | Save |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo (gizmo drags are one undo step) |
| `Ctrl+D` | Duplicate selection (exact copies, same positions) |
| `Delete` | Delete selection (with confirmation) |

**When the fly keys are live.** This follows CodeWalker exactly (`WorldForm.cs`: `enablemove`):
while the gizmo is in **Select** mode the movement keys fly the camera freely, and as soon as a
**Move** or **Rotate** gizmo is active they go quiet unless you're **holding the left button**.
That gate is what lets `W` mean "gizmo: move" and "fly forward" without the two fighting — reach
for the move gizmo and the camera stays put; hold left and W flies. Walk mode (`V`) is a dedicated
fly mode, so movement is always live there. Speed scales with how far out you're zoomed
(`speed × min(distance, 20)`), `Shift` ×5 and `Ctrl` ×0.2, again as CodeWalker does it.

The **left panel** lists all lights — `Ctrl`+click toggles, `Shift`+click selects a range;
Duplicate/Delete act on the whole selection. The **right panel** holds view settings,
shortcut editing and every parameter of the (primary) selected light. Both panels are
resizable by dragging their inner border; widths persist.

New lights spawn a couple of metres in front of the camera. Lights with the
**Draw Volume** flag (bit 12) render an approximate volumetric glow (toggle: *Volumes*).

### Files

- Drop a **.ydr** or **.yft** to load it (embedded textures are used automatically).
- Drop a **.ytd** (or use *Load YTD…*) to add an external texture dictionary;
  a same-name `.ytd` next to the model is loaded automatically.
- **Save** overwrites the loaded file — a one-time `.bak` backup of the original is
  created next to it. **Save As…** writes a new file.
- YDR saves as RSC7 version 165, YFT as version 162 (legacy/gen8 format — the format
  used by OpenIV-era tools and mods; gen9/enhanced is not written).

### Interiors: YTYP / MLO import

Open the **GTA V / MLO import** section in the right panel.

1. **Select GTA V folder…** — the folder containing `GTA5.exe`. The archive key is derived
   from your own copy of the game at runtime; no keys ship with this tool. The first scan
   of every RPF takes ~30 s on a cold cache, a few seconds after that. The folder is
   remembered and starts loading in the background on the next launch.
2. **Import YTYP (MLO)…** — pick an interior `.ytyp`. Every room entity and entity-set
   entity is placed at its exact world transform.

Prop resolution order: files next to the `.ytyp` (its resource folder, found by walking up
to `fxmanifest.lua`/`__resource.lua`) → any folders you added with **Add prop folder…** →
your GTA V archives. Textures follow the same order and fall back to the texture
dictionary parent chain, so props whose textures live in a shared `.ytd` still render.

Each unique prop is built on the GPU once and every placement is a lightweight instance
sharing those buffers, so a 600-entity interior imports in under a second and renders at
several thousand fps. Entities flagged *only render in reflections* (`0x2000000`) are
skipped — they're plain boxes around the room that would hide everything inside.

Import placement follows CodeWalker exactly, including the asymmetry that trips most
implementations — the MLO instance quaternion is used as-is, while entity quaternions are
inverted.

**Props that carry lights become editable files.** They appear in the Props list (tinted
blue) and their lights join the light list at their real world positions, so you can move,
retune and save them like a prop you opened by hand. The prop's placement is applied on top
of the light's own bone transform for display, and inverted when the gizmo writes back — so
saved files still hold drawable-local coordinates and stay game-valid. Props resolved out of
the game archives (or from inside a shared `.ydd`) are editable for preview but their Save
button is disabled: there is no loose file to write to. Turn the whole thing off with
**Import prop lights** if you only want geometry.

**A prop placed more than once lights the room at every placement.** The entry shows `(xN)`
and appears in the lists once — it is one file holding one set of drawable-local
coordinates, so there is exactly one light to edit and one thing to save. The other
placements are rendered as copies: they illuminate, cast coronas and draw volumes, but they
aren't listed, picked or saved, and editing the original moves all of them, exactly as the
game behaves. The stats line reads `3+12 lights` when twelve of them are copies. Copies
don't take shadow-map slots (the original does), and each gets its own flashiness phase so a
row of flickering lamps doesn't blink in lockstep.

### Timecycles

With a GTA V folder selected, the **Weather** dropdown lists every cycle the game ships
with — `w_clear`, `w_thunder`, `w_foggy`, `w_snow`, `underwater_deep` and the rest — read
straight from the archives. `w_clear` loads automatically as soon as the archives are open,
so the scene is lit by the game's global lighting model from the start; pick another to
change the weather. The hour schedule comes from the game's own
`common.rpf\data\levels\gta5\time.xml`, so keyframes line up exactly (13 samples, with the
sun/moon roll the game uses). **Load timecycle XML…** still takes a file from disk for
custom interior cycles, and hot-reloads it while you edit.

**Edit timecycle…** opens the full editor. The *Cycle* tab lists every variable in the loaded
cycle (423 of them for `w_clear`) with a filter box; picking one shows its value at each of the
13 keyframes, labelled with the real game hours and with the current sample highlighted. Drag
any of them and the viewport updates immediately. The *Interior modifier* tab does the same for
the selected modifier's overrides — edit values, remove them, or add an override for any cycle
variable. Both tabs can write their result back out as XML the game can read.

**Interior modifiers** are the other half. A weather cycle describes the world outside; what
a room actually looks like comes from a *timecycle modifier* layered on top. Every
`timecycle_mods_*.xml` in your install is loaded — including the per-DLC copies, which is
where modern interiors keep theirs — giving around a thousand modifiers in the **Interior
mod** dropdown, with a **Strength** slider to blend from the plain cycle to the full interior
look. Importing an MLO reads the `timecycleName` its rooms ask for and selects that modifier
automatically (matched by hash, since rooms store no string). **Load modifiers XML…** merges
in your own `timecycle_mods_*.xml`. A modifier changes a cycle, so a cycle has to be loaded
for it to do anything.

### Maps: YMAP import

**Import YMAP…** places a map's entities at their world positions, using the same prop
resolution as the MLO import. You can select **several .ymap files at once** and they merge
into one scene, sharing the prop cache so a prop used by more than one map is still built once.

A ymap entity that references an MLO archetype places a whole **interior**, and that is
imported too — rooms, entity sets and all — at the ymap's instance transform. (The instance
quaternion is used as-is while the entities inside are inverted; getting that backwards
scrambles the interior.) The user's `police_sheel_v4_3dm.ymap` brings in 424 entities and 356
editable lights this way.

Ymaps place their contents at real map coordinates, thousands of units from the grid — press
**Z** to jump the camera back to `0,0,0`.

### Workspaces

**Lights** and **Materials** at the far left of the menu bar. They are two arrangements of one
program over one scene: switching never unloads a model, moves the camera, drops an imported MLO
or clears the prop library. Start the tool in the material workspace with `--material`.

Both workspaces share the props / archetype / library / ymap tabs, the View, Weather, Import and
Timecycle sections, and one `settings.json`. What changes is the subject the panels are arranged
around — the light workspace leads with the light list and its parameters, the material workspace
leads with the material list and its textures, and each keeps the other available (lights collapse
into a section in the material workspace, materials into the surface you click).

### RPF explorer

The **RPF** workspace walks the install - folders, loose files and `.rpf` archives, which open
like folders - and any folder you drop on its tree. Click a row to select it; **Ctrl+click** adds
or removes one, **Shift+click** takes a range, **Ctrl+A** everything listed (a search result too).
With several rows selected the side panel and the right-click menu work on all of them: **Extract**
into one folder (files flat, folders and archives with their layout), **Export as XML**, **Copy** for
Paste elsewhere, **Copy paths**, **Convert** XML files to game files, and in edit mode **Delete** after
one question (Ctrl+C and Delete do the same from the keyboard). Dragging a selected row out drops the
whole selection on the desktop or into Explorer. Rename stays one item at a time.

### Fur grass

`grass_fur`, `grass_fur_mask` and `grass_fur_tnt` are drawn the way the game's own `grass_fur.fx`
draws them: eight shells pushed out along the normals, one `furLayerParams.x` step per layer, each
keeping the pixels of its height map (`fur_grass_rgba4_*`, two layers per texture) that beat that
layer's clip, shaded root to tip by `furShadow03` / `furShadow47`, thinned by the vertex alpha and
the mask variant's mask texture, multiplied by its area diffuse, and dithered away between the two
`furAlphaDistance` distances. `grass_fur_lod` is the plain ground the game grows its runtime grass
on and draws as such. Every fur texture and parameter is named in the material editor, and with
FiveM live linking on, dragging them changes the lawn in the game as you drag.

### Log

Help > Show log (Ctrl+L) opens the Log window: everything the tool reports, with errors in red and
their full details. A red "N errors" item appears on the menu bar when something fails - a .ytyp that
will not save, for example, lists every problem the interior has there. The log is also written to
`%LOCALAPPDATA%\RAGE Tools\logs\rage_tools.log` (the previous run is kept beside it); Help > Open log
folder takes you there.

### Adding props to an interior (World)

A new entity goes into the interior, not the ymap, when an interior room, portal or entity set is
selected (Project window or viewport), when an interior prop is selected (it joins the same room), or
when you are standing inside an interior room. This applies to the Project window's + Entity, Mlo > New
Entity and "Place in the world". The prop is stored in the interior's .ytyp, in interior space, and
undo/redo keep its room.

### Trying fur on your own models

Switch a material to `grass_fur`, `grass_fur_mask`, `grass_fur_tnt` or `ped_fur` in the Material
editor and it grows fur straight away. `grass_fur` is pointed at the game's own height maps
(`fur_grass_rgba4_0..3`); when a texture cannot be found - or `ped_fur` has no noise texture yet - the
viewport uses a built-in strand pattern so you can still judge the look. For the game, embed real
textures in your .ytd.

### Light prop library

The **Library** tab (bottom of the left panel) is a catalogue of every prop that has lights
inside it, so a lamp can be picked rather than built. Two sources:

- **Your prop folders** — the ones added with *Add prop folder…*. Rescanned in under a
  second with **Rescan folders**.
- **Your GTA V install** — **Scan game archives** sweeps every model in it looking for
  embedded lights. There is no index of this in the game files, so all ~150,000 resources
  have to be decompressed and parsed: about six minutes, once. The result is cached to
  `lightprops.json` next to the exe and loads instantly after that. A stock install yields
  around 4,400 light props.

Type in the search box to filter by name; your own props sort above the game's. Each tile is
a real render of the prop **lit by its own lights** — what matters here is what the light
does, not what the mesh looks like. Thumbnails are rendered a couple per frame as tiles
scroll into view, so a four-thousand-entry browser opens instantly. Hover for the light
count and types.

**Drag a tile into the viewport and let go** to place it. It lands on the first surface
under the cursor (or a few metres out if you drop into empty space) and joins the scene as a
normal prop: its lights are in the light list, the gizmo is on them, and it feeds **YMAP
export** like anything else. A prop that came from the archives is read-only — there is no
loose file to write to — but the ymap references it by archetype name, which the game
already has, so it still streams.

### Projects (.rlep)

**Save project** stores the whole workspace in one JSON file: every open model, the
loaded `.ytd`s, the imported `.ytyp`, the GTA and prop folders, the timecycle XML, the
preview hour and the camera. **Open project…** (or dropping the `.rlep` on the window)
restores all of it; the MLO re-imports automatically once the game files finish loading.
Paths inside the project's own folder are stored relative, so the folder can be zipped
and handed to someone else.

### Light parameters (all editable)

Type (Point 1 / Spot 2 / Capsule 4), position, direction + tangent, colour, intensity,
falloff + falloff exponent, cone inner/outer half-angles (degrees), capsule extent,
corona size / intensity / z-bias, volume intensity / size scale / outer colour /
outer intensity / outer exponent, all 32 flags (named, with tooltips), flashiness
(all 21 modes, animated in the preview), 24-hour time flags (with presets; preview
hour slider in the View panel), culling plane (normal + offset, gizmo), shadow blur,
shadow near clip, light/shadow/specular/volumetric fade distances, bone attachment
(by bone tag, with a bone list from the model's skeleton), group id, light hash and
projected texture hash.

When the culling plane is enabled, the viewport draws it as a bounded orange grid with
a green arrow on the side that keeps the light and a red X on the culled side.

Lights attached to bones (`BoneId` = bone tag) are shown at their bone's rest-pose
world position, exactly as CodeWalker places them; the stored values remain bone-local.

### Presets and adding lights

- **Presets** (Lights panel and the World light inspector): a built-in library of realistic
  lights - warm and cool bulbs, fluorescent tubes, sodium and LED street lamps, sconces, stage and
  vehicle spots, neons, candle, fire, TV flicker, beacons and strobes - and your own: type a name
  and **Save** stores the selected light's settings; the X removes one of yours. A preset never
  moves a light or changes where it points.
- **World, Edit Light**: **+ Point / + Spot / + Capsule** adds a new light to the selected prop's
  drawable (or its fragment for a .yft) - every copy of the prop in the world gets it, and a prop
  with no lights at all can be clicked to add its first. **Save as...** or **Add to project** keeps it.
- Viewport markers: a point light is a ring with four ticks, a spot shows its cone, a capsule its
  two ends joined by a tube.

### View panel

- **Hour** — preview game hour; lights whose TimeFlags exclude that hour go dark
  (TimeFlags = 0 means always on).
- **Day/night ambient** — scales the scene ambient with the preview hour (bright at
  noon, dark blue at night) so time-of-day changes are visible even for always-on lights.
- **Animate flashiness** — animates blink/flicker/strobe patterns (approximation).
- **Ambient / Light boost** — preview-only helpers; at boost 1.0 the light math is
  game-accurate.
- **Shading** — Lit (RAGE) / Unlit / Normals debug views.

---

## Command line

```
RageLightEditor.exe [file.ydr|file.yft|file.ytd|file.ytyp|file.rlep] [options]
  --screenshot out.png    render a few frames, save a screenshot, exit
  --gta <folder>          GTA V install to load game files from
  --mlo <file.ytyp>       import an MLO once the game files are ready, print stats
  --saveproject <f.rlep>  write the current workspace to a project file
  --openheader <text>     force a right-panel section open (for screenshots)
  --modifier <name>       report how a timecycle modifier changes the global lighting
  --select N              pre-select light N (with --screenshot)
  --rendermode N          0 lit, 1 unlit, 2 normals (debug)
  --libscan               build the light prop library headlessly (with --gta and/or
                          folder paths); --limit N stops after N archive models
  --placeprop <name>      drop the first library prop matching <name> into the scene and
                          report where its lights landed (headless check of the drop path)
  --material              open in the material workspace (the top bar switches either way)
  --selftest [file.ydr]   headless round-trip test (loads or synthesizes a drawable,
                          adds a light, saves, reloads, verifies byte-identical lights,
                          then checks a multi-placed prop lights every placement)
  --gentest out.ydr       generate a synthetic test scene (plane + boxes + 3 lights,
                          embedded checker texture); add --spotonly for a single spot
  --dumpmesh file         print decoded geometry/material/texture info (debug)
```

## Accuracy notes

- Per-light illumination uses CodeWalker's exact formulas:
  `attenuation = saturate((1-d)/(1+d²·falloffExponent))` with `d = dist/falloff`;
  spot factor `saturate(1-((ang-inner)/(outer-inner)))` with `ang = acos(-L·dir)`;
  capsules light from the nearest point of the extent segment; colour is premultiplied
  `RGB · 2·Intensity/255`; the culling plane is honoured when flag 18 (0x40000) is set.
- Cone angles are **half-angles in degrees** (GIMS shows them ×2 as hotspot/falloff).
- Surface shading follows CodeWalker's basic shaders: diffuse/bump/spec sampling,
  bumpiness, spec-map intensity mask, emissive materials add unlit albedo,
  opaque alpha-test at 0.33.
- Coronas are billboard sprites driven by corona size/intensity/z-bias (CodeWalker
  itself does not render coronas; this is a faithful visualisation, not a port).

## Known limitations (Phase 1)

- Shadows: the preview shadow-maps the nearest 8 spot + 4 point/capsule lights
  (softness follows each light's Shadow blur value); it approximates, but is not,
  the game's exact shadow pipeline. Volumes and projected textures are likewise
  faithful approximations for preview purposes.
- Preview renders the first 64 active lights (editing/saving handles any count).
- MLO import brings in geometry and prop lights, but not room/portal data, entity-set
  toggles or the interior's timecycle modifier — the layout is right, the room logic isn't
  modelled. Props coming from the game archives can't be saved.
- The extra placements of a multi-placed prop light the scene but don't cast shadows; the
  first placement does.
- Vehicle YFTs: wheels that share a single wheel mesh are not duplicated per wheel;
  cloth meshes render at rest pose.
- Legacy (gen8) resources only — the PC format all common tools use.
- Flag names follow Sollumz conventions; bit 17 is disputed between tools
  ("Only In Reflection" vs GIMS's "Disable corona").

## Project layout

```
Source\RageLightEditor.sln
Source\CodeWalker.Core\        CodeWalker resource library (dexyfex) — parsing + rebuild
Source\RageLightEditor\        the app
  Rendering\                   D3D11 device, camera, model/vertex/texture pipeline,
                               RAGE-lighting shader host, gizmos, coronas
  Shaders\model.hlsl           forward shader with the ported RAGE light math
  Editor\                      scene/undo/save logic, ImGui panels (lights + materials),
                               flag tables, light prop library + its thumbnail renderer
  ImGuiBackend\                Dear ImGui D3D11 renderer + WinForms input
docs\research-notes\           annotated findings from the CodeWalker/Sollumz sources
TestAssets\                    generated sample scene
```

## License

This project is provided as an open-source development tool for the FiveM / Cfx.re community.

* Personal and commercial use is allowed.
* You are free to modify, edit, and adapt the source code.
* You may use the tool in personal or commercial development projects.
* You may not redistribute, rebrand, or resell a modified or unmodified version of this project as your own product.
* You may not sell access to, sublicense, or commercially redistribute modified versions of this project.
* The original project and its source code must remain properly credited when redistributed for permitted purposes.

In short: use it, modify it, and build with it - but don't take a modified version, rebrand it, and sell it as your own.

The full text is in [`LICENSE`](LICENSE).

## Credits

- **CodeWalker** by dexyfex and contributors — resource formats and lighting math.
- **Sollumz** — light flag/parameter semantics reference.
- **GIMS Evo** — parameter conventions cross-validation.
