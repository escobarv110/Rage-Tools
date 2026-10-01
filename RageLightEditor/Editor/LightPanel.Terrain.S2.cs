using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        partial void TerrainSlotExtra_S2(int slot);
        partial void TerrainBrushExtra_S2();
        partial void TerrainExportTextures_S2();
        partial void TerrainWindows_S2(float displayWidth, float displayHeight);

        partial void TerrainSlotExtra_S2(int slot)
        {
            var te = Terrain;
            if (te == null) return;
            if (ImGui.SmallButton("Textures..."))
            {
                te.LibraryOpen = true;
                te.LibrarySlot = slot;
                te.LibraryWanted = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Browse the ground textures in your GTA V install - grass, dirt, sand, rock,\n" +
                                 "gravel, road - with thumbnails, and fill this layer from one of them.\n" +
                                 "The list is swept out of the archives once per install and remembered.");
        }

        partial void TerrainBrushExtra_S2()
        {
            var te = Terrain;
            if (te == null || !te.HasMesh) return;

            ImGui.Spacing();
            if (te.VertexSpacing > 0.0f)
            {
                bool tight = te.BrushFinerThanMesh;
                if (tight) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Warn);
                ImGui.TextWrapped($"Vertices are about {te.VertexSpacing:0.##} m apart" +
                                  (tight ? " - finer than this brush. The blend is stored PER VERTEX, so a brush " +
                                           "narrower than the spacing pulls whole vertices and the edge shows as triangles. " +
                                           "Subdivide, or use a bigger brush."
                                         : $" - the finest edge this mesh can draw is about {te.VertexSpacing:0.##} m."));
                if (tight) ImGui.PopStyleColor();
            }
            if (ImGui.Button("Subdivide", new Vector2(-1, 0))) te.RequestSubdivide = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Split every triangle into four, carrying the painting with it. Halves the vertex\n" +
                                 "spacing, so the brush can draw an edge half as wide.\n" +
                                 "It rebuilds the mesh, so the undo stack is cleared - and the file gets four times\n" +
                                 "the vertices, which is the real cost.\n" +
                                 "The alternative with no vertex limit at all is the terrain_cb_w_4lyr_cm_tnt preset:\n" +
                                 "that one blends from a mask TEXTURE, baked from this same painting on export.");
            if (!string.IsNullOrEmpty(te.MeshNote)) ImGui.TextDisabled(te.MeshNote);
        }

        partial void TerrainExportTextures_S2()
        {
            var te = Terrain;
            if (te == null) return;
            ImGui.TextDisabled("Layer textures");

            var mode = te.TextureExport;
            if (ImGui.RadioButton("Separate .ytd", mode == TerrainEditor.TexDestination.Ytd))
                te.TextureExport = TerrainEditor.TexDestination.Ytd;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The usual pair - but only INSIDE an RPF: the game loads a .ytd\n" +
                                 "because some archetype names it as its texture dictionary, and nothing loads\n" +
                                 "one that is merely sitting in the same folder. Open such a .ydr on its own in\n" +
                                 "CodeWalker and all four layers bind nothing, which draws as one flat colour\n" +
                                 "with no paint. Pick this only when you are about to pack both files.");

            if (ImGui.RadioButton("Embed them IN the .ydr", mode == TerrainEditor.TexDestination.Embed))
                te.TextureExport = TerrainEditor.TexDestination.Embed;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The drawable carries its own texture dictionary, so the .ydr is one self-contained\n" +
                                 "file - drop it anywhere and it is textured, with no .ytd to name, ship or load.\n" +
                                 "It is bigger by exactly the pixels it now holds.");

            if (ImGui.RadioButton("Names only", mode == TerrainEditor.TexDestination.Reference))
                te.TextureExport = TerrainEditor.TexDestination.Reference;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The material names its four layers and nothing else is written. Right when the\n" +
                                 "textures are the game's own, or already live in a .ytd of yours.");

            bool game = te.IncludeGameTextures;
            if (ImGui.Checkbox("Game textures", ref game)) te.IncludeGameTextures = game;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Off, only the layers you loaded from DISK are written: a texture pulled out of the\n" +
                                 "archives is already somewhere the game resolves it from, and copying it into a mod\n" +
                                 "is how you end up shipping (and overriding) Rockstar's art by accident.\n" +
                                 "On, for a mesh going somewhere the map's dictionaries are not - a FiveM server\n" +
                                 "streaming this on its own.");

            if (te.TextureExport == TerrainEditor.TexDestination.Ytd)
                ImGui.TextColored(UiTheme.Danger, "a loose .ytd is not read on its own - pack it or embed");

            long bytes = TerrainYdr.EmbedSize(te, te.IncludeGameTextures);
            if (te.TextureExport != TerrainEditor.TexDestination.Reference)
                ImGui.TextDisabled(bytes > 0
                    ? $"{bytes / 1024:N0} KB of texture will be written"
                    : "no texture has pixels to write yet");
        }

        private readonly List<TerrainTextureLibrary.Entry> terrainLibRows_S2 = new List<TerrainTextureLibrary.Entry>();
        private int terrainLibTotal_S2;

        partial void TerrainWindows_S2(float displayWidth, float displayHeight)
        {
            var te = Terrain;
            if (te == null || !TerrainMode || !te.LibraryOpen) return;
            var lib = te.Library;

            ImGui.SetNextWindowSize(new Vector2(620, 540), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(displayWidth * 0.5f - 310, displayHeight * 0.5f - 270), ImGuiCond.FirstUseEver);
            bool open = true;
            if (!ImGui.Begin("Terrain textures###terrainlibrary", ref open, ImGuiWindowFlags.NoCollapse))
            {
                ImGui.End();
                te.LibraryOpen = open;
                return;
            }

            ImGui.TextColored(UiTheme.AccentBright, $"Fill layer {te.LibrarySlot}");
            ImGui.SameLine();
            ImGui.TextDisabled(string.IsNullOrEmpty(te.Layers[te.LibrarySlot].Name)
                ? "(empty)" : "currently " + te.Layers[te.LibrarySlot].Name);

            if (lib == null)
            {
                ImGui.TextWrapped("No GTA V folder is open, so there is nothing to browse. Open one in Options and " +
                                  "the ground textures will be swept out of the archives.");
                ImGui.End();
                te.LibraryOpen = open;
                return;
            }

            if (!lib.Ready)
            {
                float f = lib.Total > 0 ? (float)lib.Done / lib.Total : 0.0f;
                ImGui.ProgressBar(f, new Vector2(-1, 0), lib.Total > 0 ? $"{lib.Done:N0} / {lib.Total:N0} dictionaries" : "starting...");
                ImGui.TextDisabled("Sweeping the archives for ground textures. This happens once per install - " +
                                   "after that it is read back in a blink.");
            }

            var q = te.LibraryQuery ?? "";
            ImGui.SetNextItemWidth(-90);
            if (ImGui.InputTextWithHint("##libsearch", "search - grass, hw1, sand, gravel...", ref q, 64)) te.LibraryQuery = q;
            ImGui.SameLine();
            if (ImGui.Button("Clear", new Vector2(-1, 0))) te.LibraryQuery = "";

            var counts = lib.GroupCounts();
            int perRow = 0;
            foreach (var g in TerrainTextureLibrary.Groups)
            {
                counts.TryGetValue(g, out int n);
                if (g != "All" && n == 0) continue;
                if (perRow++ > 0) ImGui.SameLine(0, 4);
                bool on = te.LibraryGroup == g;
                ImGui.PushStyleColor(ImGuiCol.Button, on ? UiTheme.ButtonOn : UiTheme.ButtonOff);
                if (ImGui.SmallButton(g == "All" ? "All" : $"{g} ({n})")) te.LibraryGroup = g;
                ImGui.PopStyleColor();
            }

            terrainLibTotal_S2 = lib.Search(te.LibraryQuery, te.LibraryGroup, terrainLibRows_S2, 400);
            ImGui.TextDisabled(terrainLibTotal_S2 > terrainLibRows_S2.Count
                ? $"{terrainLibRows_S2.Count} of {terrainLibTotal_S2:N0} shown - narrow the search"
                : $"{terrainLibTotal_S2:N0} texture(s)");

            ImGui.BeginChild("##libgrid", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() - 4), ImGuiChildFlags.Borders);
            const float cell = 116.0f;
            int cols = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / cell));
            te.LibraryVisible.Clear();
            for (int i = 0; i < terrainLibRows_S2.Count; i++)
            {
                var e = terrainLibRows_S2[i];
                if (i % cols != 0) ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.PushID(i);
                bool visible = ImGui.IsRectVisible(new Vector2(cell, cell));
                if (visible && e.Thumb == IntPtr.Zero && !e.ThumbTried) te.LibraryVisible.Add(e);

                var at = ImGui.GetCursorScreenPos();
                bool picked = ImGui.Selectable("##pick", false, ImGuiSelectableFlags.None, new Vector2(104, 104));
                bool hover = ImGui.IsItemHovered();
                var next = ImGui.GetCursorScreenPos();
                ImGui.SetCursorScreenPos(new Vector2(at.X + 4, at.Y + 4));
                if (e.Thumb != IntPtr.Zero) ImGui.Image(e.Thumb, new Vector2(96, 96));
                else
                {
                    var dl = ImGui.GetWindowDrawList();
                    var p0 = ImGui.GetCursorScreenPos();
                    var p1 = new Vector2(p0.X + 96, p0.Y + 96);
                    dl.AddRectFilled(p0, p1, ImGui.ColorConvertFloat4ToU32(new Vector4(0.16f, 0.16f, 0.18f, 1f)), 4);
                    dl.AddRect(p0, p1, ImGui.ColorConvertFloat4ToU32(new Vector4(0.35f, 0.35f, 0.38f, 1f)), 4);
                    ImGui.Dummy(new Vector2(96, 96));
                }
                ImGui.SetCursorScreenPos(next);
                if (picked)
                {
                    te.PendingNameSlot = te.LibrarySlot;
                    te.PendingName = e.Name;
                    te.LibraryOpen = false;
                }
                if (hover)
                    ImGui.SetTooltip($"{e.Name}\n{e.Width} x {e.Height}  -  {e.Group}\nfrom {e.Ytd}.ytd\n\nClick to put it on layer {te.LibrarySlot}.");
                var label = e.Name.Length > 15 ? e.Name.Substring(0, 14) + ".." : e.Name;
                ImGui.TextDisabled(label);
                ImGui.PopID();
                ImGui.EndGroup();
            }
            ImGui.EndChild();

            ImGui.TextDisabled("Click a texture to fill the layer. The picture is the game's own file, decoded here.");

            ImGui.End();
            te.LibraryOpen = open;
        }
    }
}

