using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool TerrainMode => Workspace == Space.Terrain;

        private static readonly Vector4 TerrainWorkspaceColour = new Vector4(0.784f, 0.498f, 0.235f, 1f);

        private const string TerrainWorkspaceTooltip =
            "Terrain workspace: import a mesh (.ydr or .obj), give it four ground textures, and paint\n" +
            "which one shows where.\n" +
            "The four layers sit on the corners of the blend cube the game's terrain shader reads, so\n" +
            "painting writes the mesh's own vertex colours - exactly what terrain_cb_4lyr blends by.\n" +
            "The preview is the real terrain shader, not an approximation, and Export writes a .ydr\n" +
            "whose material is one of the terrain_cb_*4lyr* presets plus a .ytd of any texture that\n" +
            "came off disk.\n" +
            "Left-drag paints the active layer; right-drag is the camera and never paints.\n" +
            "Alt+left-drag paints the base layer back, [ and ] size the brush, 1-4 pick a layer.";

        public TerrainEditor Terrain;

        partial void WorkspaceLeft_R4(float displayHeight, ref bool handled);
        partial void WorkspaceRight_R4(ref bool handled);
        partial void WorkspaceTheme_R4(ref bool handled);
        partial void WorkspaceTabs_R4();
        partial void WorkspaceTabColour_R4(Space space, ref Vector4 col);
        partial void WorkspaceTabTip_R4(Space space, ref string tip);
        partial void WorkspaceOverlay_R4(float displayWidth, float displayHeight);
        partial void WorkspaceLogo_R4(ref IntPtr tex, ref float w, ref float h);

        partial void WorkspaceLeft_R4(float displayHeight, ref bool handled)
        {
            if (!TerrainMode || Terrain == null) return;
            DrawTerrainLeft_R4(displayHeight);
            handled = true;
        }

        partial void WorkspaceRight_R4(ref bool handled)
        {
            if (!TerrainMode || Terrain == null) return;
            DrawTerrainRight_R4();
            handled = true;
        }

        partial void WorkspaceTheme_R4(ref bool handled)
        {
            if (!TerrainMode) return;
            UiTheme.Apply(settings.ThemeIndex, new Vector3(
                TerrainWorkspaceColour.X, TerrainWorkspaceColour.Y, TerrainWorkspaceColour.Z));
            handled = true;
        }

        partial void WorkspaceTabs_R4()
        {
            ImGui.SameLine(0, 2);
            DrawWorkspaceTab("Terrain", Space.Terrain);
        }

        partial void WorkspaceTabColour_R4(Space space, ref Vector4 col)
        {
            if (space == Space.Terrain) col = TerrainWorkspaceColour;
        }

        partial void WorkspaceTabTip_R4(Space space, ref string tip)
        {
            if (space == Space.Terrain) tip = TerrainWorkspaceTooltip;
        }

        public IntPtr TerrainLogoTexture = IntPtr.Zero;
        public int TerrainLogoWidth, TerrainLogoHeight;

        partial void WorkspaceLogo_R4(ref IntPtr tex, ref float w, ref float h)
        {
            if (!TerrainMode || TerrainLogoTexture == IntPtr.Zero) return;
            tex = TerrainLogoTexture; w = TerrainLogoWidth; h = TerrainLogoHeight;
        }

        private static readonly Vector4[] TerrainLayerKey_R4 =
        {
            new Vector4(0.90f, 0.35f, 0.32f, 1f),
            new Vector4(0.40f, 0.85f, 0.45f, 1f),
            new Vector4(0.36f, 0.62f, 0.98f, 1f),
            new Vector4(0.92f, 0.92f, 0.92f, 1f),
        };

        private void DrawTerrainLeft_R4(float displayHeight)
        {
            var te = Terrain;
            DrawWorkspaceLogo(ImGui.GetContentRegionAvail().X);
            ImGui.Spacing();

            if (ImGui.Button("Import mesh...", new Vector2(-1, 0))) te.RequestImportFile = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A .ydr (what GIMS or Sollumz exports) or a .obj straight out of Blender / Max.\n" +
                                 "Only the high LOD is taken - a terrain drawable usually ships _low copies too,\n" +
                                 "and painting all of them at once is never what you meant.");
            if (ImGui.Button("Take the open model", new Vector2(-1, 0))) te.RequestImportOpen = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Use whatever is open in the Lights workspace, without going back to disk.");
            DrawTerrainAddButton_V20();
            DrawTerrainProps_V20();
            if (!te.HasMesh)
            {
                ImGui.Spacing();
                ImGui.TextWrapped("Nothing imported yet. Bring in the ground mesh you want to texture - it keeps its own " +
                                  "UVs, and the four layers tile across them.");
            }
            else
            {
                ImGui.Spacing();
                ImGui.TextColored(UiTheme.AccentBright, te.Name);
                ImGui.TextDisabled($"{te.VertexCount:N0} vertices  -  {te.TriangleCount:N0} triangles  -  {te.Parts.Count} part(s)");
                var size = te.Bounds.Maximum - te.Bounds.Minimum;
                ImGui.TextDisabled($"{size.X:0.#} x {size.Y:0.#} x {size.Z:0.#} m");
                if (ImGui.Button("Frame it", new Vector2(-1, 0))) te.RequestFrame = true;
                if (ImGui.Button("Close mesh", new Vector2(-1, 0))) te.RequestClear = true;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextUnformatted("Layers");
            ImGui.TextDisabled("Click a slot to paint with it");
            ImGui.Spacing();

            var cov = te.HasMesh ? te.Coverage() : new SharpDX.Vector4();
            for (int i = 0; i < TerrainEditor.LayerCount; i++) DrawTerrainLayerSlot_R4(i, cov);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled(te.Status ?? "");
        }

        private void DrawTerrainLayerSlot_R4(int slot, SharpDX.Vector4 coverage)
        {
            var te = Terrain;
            var layer = te.Layers[slot];
            bool active = te.ActiveLayer == slot;
            float cov = slot == 0 ? coverage.X : slot == 1 ? coverage.Y : slot == 2 ? coverage.Z : coverage.W;

            ImGui.PushID("terrlayer" + slot);
            var start = ImGui.GetCursorScreenPos();
            float rowH = 52.0f;
            if (ImGui.Selectable("##slot", active, ImGuiSelectableFlags.None, new Vector2(-1, rowH)))
                te.ActiveLayer = slot;
            var after = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(start.X + 4, start.Y + 4));

            ImGui.BeginGroup();
            if (layer.ThumbId != IntPtr.Zero) ImGui.Image(layer.ThumbId, new Vector2(44, 44));
            else
            {
                var dl = ImGui.GetWindowDrawList();
                var p = ImGui.GetCursorScreenPos();
                dl.AddRectFilled(p, new Vector2(p.X + 44, p.Y + 44), ImGui.ColorConvertFloat4ToU32(TerrainLayerKey_R4[slot] * 0.45f), 4);
                dl.AddRect(p, new Vector2(p.X + 44, p.Y + 44), ImGui.ColorConvertFloat4ToU32(TerrainLayerKey_R4[slot]), 4);
                ImGui.Dummy(new Vector2(44, 44));
            }
            ImGui.EndGroup();
            ImGui.SameLine();

            ImGui.BeginGroup();
            ImGui.TextColored(active ? UiTheme.AccentBright : TerrainLayerKey_R4[slot], $"Layer {slot}");
            ImGui.SameLine();
            ImGui.TextDisabled($"{cov * 100:0}%");
            var name = layer.Name ?? "";
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##name", ref name, 96, ImGuiInputTextFlags.EnterReturnsTrue))
            { te.PendingNameSlot = slot; te.PendingName = name; }
            else if (name != (layer.Name ?? "")) layer.Name = name;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Type a game texture name and press Enter to pull it out of the archives\n" +
                                 "(the ground textures are named things like hw1_02_grass, sandold_01).");
            ImGui.EndGroup();

            ImGui.SetCursorScreenPos(after);

            if (ImGui.SmallButton("From disk")) te.RequestLayerFromDisk = slot;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A .dds / .png off disk. Textures loaded this way are written into the exported .ytd.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Normal")) te.RequestLayerBumpFromDisk = slot;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(string.IsNullOrEmpty(layer.BumpName)
                    ? "Optional normal map for this layer."
                    : "Normal map: " + layer.BumpName);
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear")) te.RequestClearLayer = slot;
            ImGui.SameLine();
            if (ImGui.SmallButton("Fill")) te.RequestFillLayer = slot;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Paint the WHOLE mesh with this layer - where you start from.");
            TerrainSlotExtra_S2(slot);

            bool twoUv = te.TwoUvSets;
            if (slot == 0 || (slot == 1 && twoUv))
            {
                ImGui.SetNextItemWidth(-1);
                float tile = layer.Tiling;
                string fmt = slot == 0
                    ? (twoUv ? "tiling  %.2f x  (UV0, layer 0)" : "tiling  %.2f x  (all four layers)")
                    : "tiling  %.2f x  (UV1, layers 1-3)";
                if (ImGui.DragFloat("##tile", ref tile, 0.25f, 0.01f, 512.0f, fmt))
                    layer.Tiling = Math.Max(tile, 0.01f);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How many times the texture repeats across the mesh's own UVs.\n" +
                                     "It is written into the VERTICES, not into a material parameter - the game's terrain\n" +
                                     "presets have no UV scale of their own - so the preview and the exported file cannot\n" +
                                     "disagree about it.");
            }
            else if (slot >= 1)
            {
                ImGui.TextDisabled(twoUv ? "tiles with layer 1 (UV1)" : "tiles with layer 0 (UV0)");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("A four-layer terrain material has two UV sets between them and no per-layer\n" +
                                     "scale. Pick the _2tex preset in Export to give layers 1-3 a tiling of their own.");
            }
            if (!string.IsNullOrEmpty(layer.Source))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("from the " + (layer.Source == "disk" ? "disk" : "archives"));
            }
            ImGui.Separator();
            ImGui.PopID();
        }

        private void DrawTerrainRight_R4()
        {
            var te = Terrain;
            ImGui.TextColored(UiTheme.AccentBright, "Terrain");
            ImGui.TextDisabled("paint four layers into the mesh's vertex colours");
            ImGui.Separator();

            if (Header("Brush", true)) DrawTerrainBrush_R4();
            if (Header("Preview", true)) DrawTerrainPreview_R4();
            if (Header("Export", true)) DrawTerrainExport_R4();
            if (Header("Shortcuts")) DrawTerrainShortcuts_R4();
        }

        private void DrawTerrainBrush_R4()
        {
            var te = Terrain;
            bool paint = te.PaintEnabled;
            if (ImGui.Checkbox("Paint", ref paint)) te.PaintEnabled = paint;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Off, the left button orbits the camera as it does everywhere else -\n" +
                                 "for framing the mesh without painting a stripe across it on the way.");

            ImGui.Spacing();
            for (int i = 0; i < TerrainEditor.LayerCount; i++)
            {
                if (i > 0) ImGui.SameLine();
                bool on = te.ActiveLayer == i;
                ImGui.PushStyleColor(ImGuiCol.Button, on ? UiTheme.ButtonOn : UiTheme.ButtonOff);
                ImGui.PushStyleColor(ImGuiCol.Text, on ? new Vector4(1, 1, 1, 1) : TerrainLayerKey_R4[i]);
                if (ImGui.Button($"{i}##pick", new Vector2(34, 0))) te.ActiveLayer = i;
                ImGui.PopStyleColor(2);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.IsNullOrEmpty(te.Layers[i].Name)
                        ? $"Layer {i} (no texture yet)  -  key {i + 1}"
                        : $"Layer {i}: {te.Layers[i].Name}  -  key {i + 1}");
            }

            float r = te.BrushRadius;
            if (ImGui.DragFloat("Size", ref r, 0.1f, 0.05f, 200.0f, "%.2f m")) te.BrushRadius = Math.Max(r, 0.05f);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The radius on the ground, in metres.  [ and ] change it.");
            float h = te.BrushHardness;
            if (ImGui.SliderFloat("Hardness", ref h, 0.0f, 0.98f, "%.2f")) te.BrushHardness = h;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How much of the brush is at full strength before the falloff starts.\n" +
                                 "0 is a soft gradient from the centre; near 1 is a hard-edged disc.");
            float s = te.BrushStrength;
            if (ImGui.SliderFloat("Strength", ref s, 0.02f, 1.0f, "%.2f")) te.BrushStrength = s;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How far one dab drags a vertex toward the layer. Low values build up\n" +
                                 "as you go over the same ground twice, which is how you get a soft join.");

            bool ring = te.ShowBrush;
            if (ImGui.Checkbox("Show the brush ring", ref ring)) te.ShowBrush = ring;
            TerrainBrushExtra_S2();

            ImGui.Spacing();
            bool canUndo = te.History.CanUndo, canRedo = te.History.CanRedo;
            if (!canUndo) ImGui.BeginDisabled();
            if (ImGui.Button("Undo", new Vector2(ImGui.GetContentRegionAvail().X * 0.5f - 3, 0))) te.RequestUndo = true;
            if (!canUndo) ImGui.EndDisabled();
            if (canUndo && ImGui.IsItemHovered()) ImGui.SetTooltip("Undo " + te.History.NextUndoName + "  (Ctrl+Z)");
            ImGui.SameLine();
            if (!canRedo) ImGui.BeginDisabled();
            if (ImGui.Button("Redo", new Vector2(-1, 0))) te.RequestRedo = true;
            if (!canRedo) ImGui.EndDisabled();
            if (canRedo && ImGui.IsItemHovered()) ImGui.SetTooltip("Redo " + te.History.NextRedoName + "  (Ctrl+Y)");
            ImGui.TextDisabled($"{te.History.Count} step(s) on the stack");
        }

        private void DrawTerrainPreview_R4()
        {
            var te = Terrain;
            bool w = te.ShowWeights;
            if (ImGui.Checkbox("Show weights", ref w)) te.ShowWeights = w;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Swap the four textures for flat red / green / blue / white and let the REAL blend\n" +
                                 "mix them. What you see is the weight itself, computed by the shader that will run\n" +
                                 "in the game - not a second drawing of it that could disagree.");

            if (!te.HasMesh) return;
            var cov = te.Coverage();
            ImGui.Spacing();
            ImGui.TextDisabled("Coverage");
            for (int i = 0; i < 4; i++)
            {
                float v = i == 0 ? cov.X : i == 1 ? cov.Y : i == 2 ? cov.Z : cov.W;
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, TerrainLayerKey_R4[i]);
                ImGui.ProgressBar(v, new Vector2(-1, 12), "");
                ImGui.PopStyleColor();
                ImGui.SameLine(0, 6);
                ImGui.TextDisabled($"{i}");
            }
            ImGui.TextDisabled("the share of the mesh each layer owns, by vertex weight");
        }

        private void DrawTerrainExport_R4()
        {
            var te = Terrain;
            ImGui.TextDisabled("Material");
            for (int i = 0; i < TerrainYdr.Presets.Length; i++)
            {
                var (name, tip) = TerrainYdr.Presets[i];
                if (ImGui.RadioButton(name, te.ExportPreset == name)) te.ExportPreset = name;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
            }
            if (TerrainYdr.UsesMask(te.ExportPreset))
                ImGui.TextWrapped("This preset reads a mask texture, not the vertex colours - the painted blend is " +
                                  "baked into one on export, and the mesh needs a usable second UV set for that.");

            ImGui.Spacing();
            TerrainExportTextures_S2();

            if (!te.HasMesh) ImGui.BeginDisabled();
            if (ImGui.Button("Export .ydr...", new Vector2(-1, 0))) te.RequestExport = true;
            if (!te.HasMesh) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Writes the mesh with the painted vertex colours and the four layers, then reads the\n" +
                                 "file back and says how many vertices came out carrying a blend.");
            if (te.Dirty) ImGui.TextColored(UiTheme.Warn, "unsaved paint");
        }

        private void DrawTerrainShortcuts_R4()
        {
            ImGui.TextDisabled("Left-drag        paint the active layer");
            ImGui.TextDisabled("Alt+left-drag    paint layer 0 back (the eraser)");
            ImGui.TextDisabled("Right-drag       the camera - it never paints");
            ImGui.TextDisabled("1 - 4            pick the layer to paint with");
            ImGui.TextDisabled("[  ]             brush smaller / larger");
            ImGui.TextDisabled("Ctrl+Z / Ctrl+Y  undo / redo the last stroke");
            ImGui.TextDisabled("F                frame the mesh");
        }

        private void DrawTerrainHint_R4(float displayWidth, float displayHeight)
        {
            var te = Terrain;
            if (te == null) return;
            float x0 = (ShowLeftPanel ? settings.LeftPanelWidth : 0.0f) + 10.0f;
            float x1 = displayWidth - (ShowRightPanel ? settings.RightPanelWidth : 0.0f) - 10.0f;
            float w = Math.Max(220.0f, x1 - x0);
            float h = ImGui.GetTextLineHeightWithSpacing() + 12.0f;
            float y = Math.Max(displayHeight - h - 8.0f, TopBarHeight + 8.0f);
            ImGui.SetNextWindowPos(new Vector2(x0, y), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.80f);
            var wf = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                   | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                   | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoFocusOnAppearing
                   | ImGuiWindowFlags.NoNav;
            if (!ImGui.Begin("##terrainhint", wf)) { ImGui.End(); return; }

            string hint;
            if (!te.HasMesh) hint = "Import a mesh to start - a .ydr from GIMS or Sollumz, or a .obj from Blender.";
            else if (!te.PaintEnabled) hint = "Painting is off: the mouse orbits. Turn it back on in Brush.";
            else if (te.Layers[te.ActiveLayer].HasTexture)
                hint = $"Left-drag paints layer {te.ActiveLayer} ({te.Layers[te.ActiveLayer].Name}), Alt+left-drag paints layer 0 back.";
            else
                hint = $"Left-drag paints layer {te.ActiveLayer} - give it a texture and the blend will show.";
            ImGui.TextUnformatted(hint);

            var status = te.Status ?? "";
            if (status.Length > 0)
            {
                float sw = ImGui.CalcTextSize(status).X;
                float at = ImGui.GetWindowWidth() - sw - 12.0f;
                if (at > ImGui.CalcTextSize(hint).X + 24.0f) { ImGui.SameLine(at); ImGui.TextDisabled(status); }
            }
            ImGui.End();
        }

        partial void WorkspaceOverlay_R4(float displayWidth, float displayHeight)
        {
            if (TerrainMode) DrawTerrainHint_R4(displayWidth, displayHeight);
            TerrainWindows_S2(displayWidth, displayHeight);
        }

    }
}

