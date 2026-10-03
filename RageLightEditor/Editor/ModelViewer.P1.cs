using System;
using System.Collections.Generic;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;
using RageLightEditor.Rendering;
using SDX = SharpDX;
using GameTexture = CodeWalker.GameFiles.Texture;

namespace RageLightEditor.Editor
{
    public partial class ModelViewer
    {

        public AssetPreview Preview;
        public RpfFileEntry Entry;
        public string DiskPath;
        public string Title = "Model viewer";
        public string Status = "";
        public bool Visible;
        public bool RequestClose;

        public RpfFileEntry RequestSpawnInWorld;
        public RpfFileEntry RequestToMloCreator;
        public RpfFileEntry RequestExtract;
        public int RequestSelectDrawable = -1;

        public IntPtr ImageId;
        public int ImageWidth, ImageHeight;
        public int WantWidth = 960, WantHeight = 600;

        public Func<GameTexture, IntPtr> TextureId;

        public readonly Camera Cam = new Camera();
        private float yaw = 0.9f, pitch = 0.45f, dist = 5.0f;
        private Vector3 target;
        private float modelRadius = 1.0f;

        public SDX.Vector3 ModelCentre => new SDX.Vector3(target.X, target.Y, target.Z);
        public float ModelRadius => modelRadius;

        public float GridStep
        {
            get
            {
                float want = Math.Max(modelRadius, 0.05f) * 2.0f / 20.0f;
                float best = GridSteps[0];
                foreach (var s in GridSteps)
                    if (Math.Abs(Math.Log(s / want)) < Math.Abs(Math.Log(best / want))) best = s;
                return best;
            }
        }
        private static readonly float[] GridSteps = { 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 25f, 50f, 100f };

        public static readonly string[] ViewModeLabels =
            { "Default", "Unlit (albedo)", "Normals", "Lighting only", "Specular only", "Vertex colours" };
        private static readonly int[] ViewModeValues = { 0, 1, 2, 5, 6, 9 };
        public int ViewMode;
        public bool Wireframe;
        public bool BackfaceCulling = true;
        public bool ShowTextures = true;
        public bool ShowGrid = true;
        public bool ShowAxes = true;
        public int LightSetup;
        public static readonly string[] LightSetupLabels = { "Studio (3-point)", "The file's own lights", "Flat ambient" };
        public float AmbientLevel = 0.22f;
        public float Exposure = 1.0f;
        public Vector3 Background = new Vector3(0.10f, 0.11f, 0.13f);

        public int RenderMode => Wireframe ? 8 : ViewModeValues[Math.Clamp(ViewMode, 0, ViewModeValues.Length - 1)];

        public class GeomRow
        {
            public RenderMesh Mesh;
            public DrawableGeometry Geometry;
            public int Verts, Tris;
            public string Shader = "";
            public string Sps = "";
            public int ShaderIndex = -1;
        }

        public class ModelRow
        {
            public string Label = "";
            public readonly List<GeomRow> Geoms = new List<GeomRow>();
            public int Verts, Tris;
        }

        private readonly List<ModelRow> models = new List<ModelRow>();
        public IReadOnlyList<ModelRow> Models => models;

        private List<AssetMaterialInfo> materials = new List<AssetMaterialInfo>();
        private int selMaterial;
        private int selTexture;

        public int Tab;
        private int forceTab = -1;
        public static readonly string[] TabNames = { "Models", "Materials", "Details", "Options", "Textures", "Weapon" };

        public void SetTab(int index)
        {
            Tab = Math.Clamp(index, 0, TabNames.Length - 1);
            forceTab = Tab;
        }

        public void Adopt(RpfFileEntry entry, AssetPreview preview)
        {
            Preview = preview;
            Entry = entry;
            Title = entry?.Name ?? "Model viewer";
            Visible = true;
            RequestClose = false;
            LightPanel.ModelViewOpened_U1();
            ClearWeaponSearch_V23();
            selMaterial = 0;
            selTexture = 0;
            YtdGrid_V44 = true;
            Rebuild();
            FrameModel();
            if (preview != null && preview.Kind == AssetKind.TextureDict) SetTab(4);
            else if (Tab == 4) SetTab(0);
        }

        public void Rebuild()
        {
            models.Clear();
            materials = Preview?.InspectMaterials() ?? new List<AssetMaterialInfo>();
            selMaterial = 0;
            if (Preview == null) return;

            var byGeom = new Dictionary<DrawableGeometry, RenderMesh>();
            var mdl = Preview.Model;
            if (mdl != null)
                foreach (var m in mdl.Meshes)
                    if (m?.Geometry != null && !byGeom.ContainsKey(m.Geometry)) byGeom[m.Geometry] = m;

            int di = 0;
            foreach (var d in ViewerDrawables())
            {
                var lod = ModelRenderer.HighestLod(d);
                var shaders = d?.ShaderGroup?.Shaders?.data_items;
                if (lod == null) { di++; continue; }
                for (int mi = 0; mi < lod.Length; mi++)
                {
                    var dm = lod[mi];
                    if (dm?.Geometries == null) continue;
                    var row = new ModelRow
                    {
                        Label = MultiDrawable ? $"Drawable {di} / Model {mi}" : $"Model {mi}",
                    };
                    foreach (var g in dm.Geometries)
                    {
                        if (g == null) continue;
                        var gr = new GeomRow
                        {
                            Geometry = g,
                            Verts = g.VertexData?.VertexCount ?? (int)g.VerticesCount,
                            Tris = (g.IndexBuffer?.Indices?.Length ?? (int)g.IndicesCount) / 3,
                            ShaderIndex = g.ShaderID,
                        };
                        if (shaders != null && g.ShaderID < shaders.Length && shaders[g.ShaderID] != null)
                        {
                            gr.Shader = shaders[g.ShaderID].Name.ToString();
                            gr.Sps = shaders[g.ShaderID].FileName.ToString();
                        }
                        byGeom.TryGetValue(g, out var mesh);
                        gr.Mesh = mesh;
                        row.Verts += gr.Verts;
                        row.Tris += gr.Tris;
                        row.Geoms.Add(gr);
                    }
                    if (row.Geoms.Count > 0) models.Add(row);
                }
                di++;
            }
        }

        private bool MultiDrawable => Preview != null &&
            (Preview.Kind == AssetKind.Fragment || Preview.Kind == AssetKind.DrawableDict);

        private IEnumerable<DrawableBase> ViewerDrawables()
        {
            var p = Preview;
            if (p == null) yield break;
            if (p.Kind == AssetKind.Fragment && p.Yft?.Fragment != null)
            {
                var frag = p.Yft.Fragment;
                if (frag.Drawable != null) yield return frag.Drawable;
                if (frag.DrawableCloth != null) yield return frag.DrawableCloth;
                var children = frag.PhysicsLODGroup?.PhysicsLOD1?.Children?.data_items;
                if (children != null)
                    foreach (var c in children)
                    {
                        var cd = c?.Drawable1;
                        if (cd != null && cd != frag.Drawable && cd.AllModels != null && cd.AllModels.Length > 0)
                            yield return cd;
                    }
                yield break;
            }
            if (p.Drawable != null) yield return p.Drawable;
        }

        public void FrameModel()
        {
            var b = Preview?.Stats.Bounds ?? default;
            var min = new Vector3(b.Minimum.X, b.Minimum.Y, b.Minimum.Z);
            var max = new Vector3(b.Maximum.X, b.Maximum.Y, b.Maximum.Z);
            if (Preview?.Model != null && Preview.Model.Meshes.Count > 0)
            {
                var mb = Preview.Model.Bounds;
                if (mb.Minimum.X <= mb.Maximum.X)
                {
                    min = new Vector3(mb.Minimum.X, mb.Minimum.Y, mb.Minimum.Z);
                    max = new Vector3(mb.Maximum.X, mb.Maximum.Y, mb.Maximum.Z);
                }
            }
            var size = max - min;
            if (!(size.X > 0) || float.IsNaN(size.X) || float.IsInfinity(size.X))
            { min = new Vector3(-1); max = new Vector3(1); size = max - min; }
            target = (min + max) * 0.5f;
            modelRadius = Math.Max(size.Length() * 0.5f, 0.05f);
            dist = modelRadius * 2.2f;
            yaw = 0.9f; pitch = 0.45f;
            ApplyCamera(1.0f);
        }

        public void ApplyCamera(float aspect)
        {
            Cam.Target = new SDX.Vector3(target.X, target.Y, target.Z);
            Cam.Yaw = yaw;
            Cam.Pitch = pitch;
            Cam.MinDistance = Math.Max(modelRadius * 0.02f, 0.01f);
            Cam.MaxDistance = Math.Max(modelRadius * 80.0f, 100.0f);
            Cam.Distance = Math.Clamp(dist, Cam.MinDistance, Cam.MaxDistance);
            dist = Cam.Distance;
            Cam.NearClip = Math.Max(modelRadius * 0.004f, 0.01f);
            Cam.FarClip = Math.Max(modelRadius * 400.0f, 1000.0f);
            Cam.FieldOfView = 50.0f * 0.0174533f;
            Cam.SetAspect(aspect > 0.01f ? aspect : 1.0f);
            Cam.SnapSmoothing();
            Cam.Update();
        }

        public void Draw(float w, float h, bool focused)
        {
            ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                      | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus
                      | ImGuiWindowFlags.NoSavedSettings;
            if (!ImGui.Begin("##modelviewer", flags)) { ImGui.End(); return; }

            DrawToolbar();
            ImGui.Separator();

            float bottom = ImGui.GetFrameHeightWithSpacing() * 2 + 12;
            float panelW = Math.Clamp(w * 0.32f, 320.0f, 520.0f);
            float bodyH = Math.Max(h - ImGui.GetCursorPosY() - bottom, 120.0f);

            if (ImGui.BeginChild("##viewport", new Vector2(Math.Max(w - panelW - 16, 120.0f), bodyH), ImGuiChildFlags.Borders))
                DrawViewport();
            ImGui.EndChild();

            ImGui.SameLine();
            if (ImGui.BeginChild("##tabs", new Vector2(0, bodyH), ImGuiChildFlags.Borders))
                DrawTabs();
            ImGui.EndChild();

            ImGui.Separator();
            DrawClipRow();
            DrawCameraHelp_R3();
            ImGui.TextDisabled(string.IsNullOrEmpty(Status) ? " " : Status);
            ImGui.End();
            DrawTexCard_V22();
            DrawYtdPicker_V24();
        }

        private void DrawToolbar()
        {
            ImGui.TextUnformatted(Title);
            if (Preview != null && !string.IsNullOrEmpty(Preview.Stats.Path))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("  " + Preview.Stats.Path);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Preview.Stats.Path);
            }

            bool geo = Preview == null || Preview.Kind != AssetKind.TextureDict;
            ImGui.BeginDisabled(!geo);
            ImGui.SetNextItemWidth(190);
            ImGui.Combo("##viewmode", ref ViewMode, ViewModeLabels, ViewModeLabels.Length);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How the model is shaded. Default is the game's own lighting;\n" +
                                 "the rest are the debug views the main viewport offers.");
            ImGui.SameLine();
            ImGui.Checkbox("Wireframe", ref Wireframe);
            ImGui.SameLine();
            ImGui.Checkbox("Textures", ref ShowTextures);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Off draws opaque materials at their flat diffuse colour - the shape\n" +
                                 "without the artwork, which is how UVs and smoothing get read.\n" +
                                 "Cutouts and decals keep their texture: the alpha IS their shape,\n" +
                                 "and without it the shader drops them rather than draw a slab.");
            ImGui.SameLine();
            ImGui.Checkbox("Grid", ref ShowGrid);
            ImGui.SameLine();
            ImGui.Checkbox("Axes", ref ShowAxes);
            ImGui.SameLine();
            ImGui.Checkbox("Cull", ref BackfaceCulling);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Backface culling. Off shows the inside of a shell, which is how a\n" +
                                 "hole or an inverted normal gets found.");
            ImGui.SameLine();
            if (ImGui.Button("Frame")) FrameModel();
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Put the whole model back in frame (or press F).");
            ImGui.SameLine();
            ImGui.BeginDisabled(Entry == null);
            if (ImGui.Button(DiskPath != null ? "Show in Explorer" : "Extract...")) RequestExtract = Entry;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Write this file out in its on-disk form (RSC7 header back on for a\n" +
                                 "resource), so OpenIV, CodeWalker and this editor can all read it.");
            ImGui.SameLine();
            if (ImGui.Button("Close")) RequestClose = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Close the viewer. The explorer keeps its place; nothing else in the\n" +
                                 "editor knows this window was ever open.");
        }

        private void DrawViewport()
        {
            var avail = ImGui.GetContentRegionAvail();
            WantWidth = Math.Clamp((int)avail.X, 64, 4096);
            WantHeight = Math.Clamp((int)avail.Y, 64, 4096);

            if (Preview != null && Preview.Kind == AssetKind.TextureDict)
            {
                DrawTexturePane(avail);
                return;
            }

            if (ImageId == IntPtr.Zero || ImageWidth <= 0)
            {
                ImGui.TextWrapped(Preview == null
                    ? "Nothing open. Double-click a .ydr, .ydd, .yft, .ytd or .ybn in the RPF explorer."
                    : (string.IsNullOrEmpty(Preview.Error) ? "Building..." : Preview.Error));
                return;
            }

            ImGui.Image(ImageId, new Vector2(ImageWidth, ImageHeight));
            NoteImageRect_V22();
            LightPanel.DrawModelViewTransition_U1();
            bool hovered = ImGui.IsItemHovered();
            { bool r3 = false; ViewportInput_R3(hovered, ref r3); if (r3) return; }
            var io = ImGui.GetIO();
            if (!hovered) return;

            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                yaw -= io.MouseDelta.X * 0.008f;
                pitch = Math.Clamp(pitch + io.MouseDelta.Y * 0.008f, -1.55f, 1.55f);
            }
            else if (ImGui.IsMouseDragging(ImGuiMouseButton.Right) || ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
            {
                float cp = (float)Math.Cos(pitch);
                var fwd = new Vector3((float)(Math.Cos(yaw) * cp), (float)(Math.Sin(yaw) * cp), (float)Math.Sin(pitch));
                var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, fwd));
                var up = Vector3.Normalize(Vector3.Cross(fwd, right));
                float scale = dist * 0.0018f;
                target += right * (io.MouseDelta.X * scale) + up * (io.MouseDelta.Y * scale);
            }
            if (Math.Abs(io.MouseWheel) > 0.001f)
                dist *= (float)Math.Pow(1.0 / 1.12, io.MouseWheel);
            if (ImGui.IsKeyPressed(ImGuiKey.F)) FrameModel();
        }

        private void DrawTexturePane(Vector2 avail)
        {
            var list = Preview.Textures;
            if (list == null || list.Count == 0) { ImGui.TextDisabled("This dictionary holds no textures."); return; }
            selTexture = Math.Clamp(selTexture, 0, list.Count - 1);

            ImGui.Checkbox("All textures##v44", ref YtdGrid_V44);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Every texture in this dictionary as a grid. Click one to open it on its own.");
            if (YtdGrid_V44)
            {
                ImGui.SameLine(0, 12);
                ImGui.SetNextItemWidth(180);
                UiSlider_U28.Float("##ytdcell", ref YtdCell_V44, 64f, 320f, "cell %.0f px");
                ImGui.SameLine(0, 12);
                ImGui.TextDisabled($"{list.Count} texture(s)");
                DrawYtdGrid_V44(list);
                return;
            }

            var t = list[selTexture];
            ImGui.SameLine(0, 12);
            ImGui.TextUnformatted($"{t.Name}   {t.Width}x{t.Height}   {t.Format}" +
                                  (t.Levels > 1 ? $"   {t.Levels} mips" : ""));
            var id = t.HasData ? (TextureId?.Invoke(t.Texture) ?? IntPtr.Zero) : IntPtr.Zero;
            if (id == IntPtr.Zero)
            {
                ImGui.TextDisabled(t.HasData ? "This texture could not be uploaded." : "This texture came without its pixels.");
                return;
            }
            float maxW = Math.Max(avail.X - 8, 32), maxH = Math.Max(avail.Y - ImGui.GetTextLineHeightWithSpacing() * 2, 32);
            float s = Math.Min(Math.Min(maxW / Math.Max(t.Width, 1), maxH / Math.Max(t.Height, 1)), 4.0f);
            ImGui.Image(id, new Vector2(t.Width * s, t.Height * s));
        }

        private void DrawTabs()
        {
            if (!ImGui.BeginTabBar("##viewertabs")) return;
            bool isYtd = Preview != null && Preview.Kind == AssetKind.TextureDict;

            if (TabItem("Models", 0, !isYtd)) { DrawModelsTab(); ImGui.EndTabItem(); }
            if (TabItem("Materials", 1, !isYtd)) { DrawMaterialsTab(); ImGui.EndTabItem(); }
            if (TabItem("Details", 2, true)) { DrawDetailsTab(); ImGui.EndTabItem(); }
            if (TabItem("Options", 3, true)) { DrawOptionsTab(); ImGui.EndTabItem(); }
            if (TabItem("Textures", 4, true)) { DrawTexturesTab(); ImGui.EndTabItem(); }
            if (Preview != null && Preview.IsWeapon && TabItem("Weapon", 5, true)) { DrawWeaponTab_V22(); ImGui.EndTabItem(); }

            ImGui.EndTabBar();
            forceTab = -1;
        }

        private bool TabItem(string label, int index, bool enabled)
        {
            if (!enabled) return false;
            bool open;
            if (forceTab == index)
            {
                bool dummy = true;
                open = ImGui.BeginTabItem(label, ref dummy, ImGuiTabItemFlags.SetSelected);
            }
            else open = ImGui.BeginTabItem(label);
            if (open) Tab = index;
            return open;
        }

        private void DrawModelsTab()
        {
            if (Preview == null) { ImGui.TextDisabled("Nothing open."); return; }

            if (Preview.Kind == AssetKind.DrawableDict && Preview.DrawableNames.Count > 0)
            {
                ImGui.TextDisabled("DRAWABLE");
                int sel = Preview.SelectedDrawable;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##ddsel", sel >= 0 && sel < Preview.DrawableNames.Count
                        ? Preview.DrawableNames[sel] : "(none)"))
                {
                    for (int i = 0; i < Preview.DrawableNames.Count; i++)
                        if (ImGui.Selectable(Preview.DrawableNames[i], i == sel)) RequestSelectDrawable = i;
                    ImGui.EndCombo();
                }
                ImGui.Spacing();
            }

            if (models.Count == 0)
            {
                ImGui.TextWrapped(Preview.Kind == AssetKind.Collision
                    ? "Collision has no drawable geometry - its hulls are in the viewport and its numbers on Details."
                    : "This file has no drawable geometry.");
                return;
            }

            if (ImGui.SmallButton("Show all")) SetAllVisible(true);
            ImGui.SameLine();
            if (ImGui.SmallButton("Hide all")) SetAllVisible(false);
            ImGui.SameLine();
            int shown = 0, total = 0;
            foreach (var m in models) foreach (var g in m.Geoms) { total++; if (g.Mesh != null && g.Mesh.Visible) shown++; }
            ImGui.TextDisabled($"{shown}/{total} drawn");
            ImGui.Separator();

            if (!ImGui.BeginChild("##modeltree", new Vector2(0, 0))) { ImGui.EndChild(); return; }
            for (int mi = 0; mi < models.Count; mi++)
            {
                var m = models[mi];
                ImGui.PushID(mi);
                bool groupOn = false;
                foreach (var g in m.Geoms) if (g.Mesh != null && g.Mesh.Visible) { groupOn = true; break; }
                if (ImGui.Checkbox("##grp", ref groupOn))
                    foreach (var g in m.Geoms) if (g.Mesh != null) g.Mesh.Visible = groupOn;
                ImGui.SameLine();
                bool open = ImGui.TreeNodeEx($"{m.Label}##n",
                    ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth);
                ImGui.SameLine();
                ImGui.TextDisabled($"  {m.Geoms.Count} geom, {m.Verts:N0} verts");
                if (open)
                {
                    for (int gi = 0; gi < m.Geoms.Count; gi++)
                    {
                        var g = m.Geoms[gi];
                        ImGui.PushID(gi);
                        bool on = g.Mesh != null && g.Mesh.Visible;
                        ImGui.BeginDisabled(g.Mesh == null);
                        if (ImGui.Checkbox("##v", ref on) && g.Mesh != null) g.Mesh.Visible = on;
                        ImGui.EndDisabled();
                        ImGui.SameLine();
                        string label = string.IsNullOrEmpty(g.Shader)
                            ? $"{g.Verts:N0} verts, material [{g.ShaderIndex}] (not in this drawable's shader group)"
                            : $"{g.Verts:N0} verts, {g.Shader} ({g.Sps})";
                        if (g.Mesh == null) ImGui.TextDisabled(label + "   [not drawn]");
                        else if (ImGui.Selectable(label, selMaterial == g.ShaderIndex)) selMaterial = g.ShaderIndex;
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"material [{g.ShaderIndex}] {g.Shader} ({g.Sps})\n" +
                                             $"{g.Verts:N0} vertices, {g.Tris:N0} triangles" +
                                             (g.Mesh == null ? "\nthe builder dropped this geometry - a shadow proxy or an empty LOD" : ""));
                        ImGui.PopID();
                    }
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            ImGui.EndChild();
        }

        private void SetAllVisible(bool on)
        {
            foreach (var m in models) foreach (var g in m.Geoms) if (g.Mesh != null) g.Mesh.Visible = on;
        }

        private void DrawMaterialsTab()
        {
            if (materials.Count == 0) { ImGui.TextDisabled("This file declares no materials."); return; }
            selMaterial = Math.Clamp(selMaterial, 0, materials.Count - 1);

            ImGui.TextDisabled("MATERIAL");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##matsel", $"[{selMaterial}] {materials[selMaterial].ShaderName}"))
            {
                for (int i = 0; i < materials.Count; i++)
                    if (ImGui.Selectable($"[{i}] {materials[i].ShaderName} ({materials[i].Sps})", i == selMaterial))
                        selMaterial = i;
                ImGui.EndCombo();
            }

            var mat = materials[selMaterial];
            ImGui.Spacing();
            ImGui.TextUnformatted(mat.ShaderName);
            ImGui.TextDisabled(mat.Sps);
            ImGui.TextDisabled($"bucket {mat.Bucket} ({mat.BucketName})   draws as {mat.DrawMode}" +
                               (mat.DoubleSided ? ", two-sided" : "") + (mat.NeverDrawn ? ", never drawn" : ""));
            ImGui.TextDisabled($"used by {mat.GeometryCount} geometr{(mat.GeometryCount == 1 ? "y" : "ies")}");
            ImGui.Separator();

            if (!ImGui.BeginChild("##matbody", new Vector2(0, 0))) { ImGui.EndChild(); return; }

            ImGui.TextDisabled("TEXTURES");
            if (mat.Textures.Count == 0) ImGui.TextDisabled("(none)");
            foreach (var slot in mat.Textures)
            {
                ImGui.PushID(slot.SlotName + slot.TextureName);
                var id = slot.Resolved ? (TextureId?.Invoke(slot.Texture) ?? IntPtr.Zero) : IntPtr.Zero;
                if (id != IntPtr.Zero) ImGui.Image(id, new Vector2(64, 64));
                else ImGui.Dummy(new Vector2(64, 64));
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.TextUnformatted(slot.SlotName);
                ImGui.TextDisabled(string.IsNullOrEmpty(slot.TextureName) ? "(no name)" : slot.TextureName);
                if (slot.Info != null)
                    ImGui.TextDisabled($"{slot.Info.Width}x{slot.Info.Height} {slot.Info.Format}" +
                                       (slot.Info.Levels > 1 ? $" {slot.Info.Levels} mips" : ""));
                ImGui.TextDisabled(slot.Resolved ? "from " + slot.Source : "unresolved");
                ImGui.EndGroup();
                ImGui.PopID();
                ImGui.Separator();
            }

            ImGui.Spacing();
            ImGui.TextDisabled("PARAMETERS");
            if (mat.Params.Count == 0) ImGui.TextDisabled("(none)");
            foreach (var p in mat.Params)
            {
                ImGui.TextUnformatted(p.Name);
                ImGui.SameLine(210);
                ImGui.TextDisabled(p.ArrayLength > 0
                    ? $"float4[{p.ArrayLength}]"
                    : $"{p.Value.X:0.###}, {p.Value.Y:0.###}, {p.Value.Z:0.###}, {p.Value.W:0.###}");
            }
            ImGui.EndChild();
        }

        private void DrawDetailsTab()
        {
            if (Preview == null) { ImGui.TextDisabled("Nothing open."); return; }
            var s = Preview.Stats;
            if (!ImGui.BeginChild("##details", new Vector2(0, 0))) { ImGui.EndChild(); return; }

            void Row(string k, string v)
            {
                ImGui.TextUnformatted(k);
                ImGui.SameLine(180);
                ImGui.TextDisabled(v ?? "");
            }

            ImGui.TextDisabled("FILE");
            Row("Name", s.Name);
            Row("Kind", s.Kind.ToString());
            Row("Size in memory", RpfExplorer.SizeText(s.FileSize));
            if (Entry != null)
            {
                Row("Packed", RpfExplorer.SizeText(Entry.FileSize));
                if (Entry is RpfResourceFileEntry rr)
                {
                    Row("Resource version", "v" + rr.Version);
                    Row("System / graphics", $"{rr.SystemSize / 1024:N0} KB / {rr.GraphicsSize / 1024:N0} KB");
                }
                if (Entry.IsEncrypted) Row("Encryption", "encrypted in the archive");
            }
            ImGui.TextWrapped(s.Path ?? "");
            if (ImGui.SmallButton("Copy path")) ImGui.SetClipboardText(s.Path ?? "");

            if (s.Kind != AssetKind.TextureDict && s.Kind != AssetKind.Collision)
            {
                ImGui.Spacing(); ImGui.Separator();
                ImGui.TextDisabled("GEOMETRY (highest LOD - what is drawn)");
                Row("Drawables", s.Drawables.ToString("N0"));
                Row("Models", s.Models.ToString("N0"));
                Row("Geometries", s.Geometries.ToString("N0"));
                Row("Vertices", s.Vertices.ToString("N0"));
                Row("Triangles", s.Triangles.ToString("N0"));
                ImGui.Spacing();
                ImGui.TextDisabled("EVERY LOD (what the file weighs)");
                Row("Geometries", s.AllLodGeometries.ToString("N0"));
                Row("Triangles", s.AllLodTriangles.ToString("N0"));
                ImGui.Spacing();
                Row("Materials", s.Shaders.ToString("N0"));
                Row("Skeleton", s.HasSkeleton ? $"{s.Bones} bones" : "none");
                Row("Lights", LightCount().ToString("N0"));
                var b = s.Bounds;
                Row("Bounds min", $"{b.Minimum.X:0.###}, {b.Minimum.Y:0.###}, {b.Minimum.Z:0.###}");
                Row("Bounds max", $"{b.Maximum.X:0.###}, {b.Maximum.Y:0.###}, {b.Maximum.Z:0.###}");
                var size = b.Maximum - b.Minimum;
                Row("Size", $"{size.X:0.###} x {size.Y:0.###} x {size.Z:0.###} m");
            }

            if (s.Kind == AssetKind.Collision)
            {
                ImGui.Spacing(); ImGui.Separator();
                ImGui.TextDisabled("COLLISION");
                Row("Bound type", s.BoundType);
                Row("Parts", s.CollisionChildren.ToString("N0"));
                Row("Polygons", s.CollisionPolygons.ToString("N0"));
                Row("Vertices", s.CollisionVertices.ToString("N0"));
                Row("Materials", s.CollisionMaterials.ToString("N0"));
                var cb = s.Bounds;
                Row("Bounds min", $"{cb.Minimum.X:0.###}, {cb.Minimum.Y:0.###}, {cb.Minimum.Z:0.###}");
                Row("Bounds max", $"{cb.Maximum.X:0.###}, {cb.Maximum.Y:0.###}, {cb.Maximum.Z:0.###}");
            }

            ImGui.Spacing(); ImGui.Separator();
            ImGui.TextDisabled($"EMBEDDED TEXTURES ({s.Textures.Count}, {s.TextureBytes / 1024:N0} KB)");
            if (s.Textures.Count == 0) ImGui.TextDisabled("  (none - its materials resolve theirs from a .ytd)");
            foreach (var t in s.Textures)
                ImGui.TextDisabled($"  {t.Name}  {t.Width}x{t.Height}  {t.Format}" +
                                   (t.Levels > 1 ? $"  {t.Levels} mips" : ""));

            ImGui.Spacing(); ImGui.Separator();
            ImGui.TextDisabled("SEND IT SOMEWHERE");
            bool model = s.Kind == AssetKind.Drawable || s.Kind == AssetKind.Fragment;
            ImGui.BeginDisabled(!model || Entry == null);
            if (ImGui.Button("Place in the world", new Vector2(-1, 0))) RequestSpawnInWorld = Entry;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Adds it to the project's ymap as an entity in front of the World\n" +
                                 "camera and switches there. Nothing happens until you press this.");
            if (ImGui.Button("Send to the MLO Creator", new Vector2(-1, 0))) RequestToMloCreator = Entry;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Puts it in the MLO Creator's assets library and switches there.\n" +
                                 "Nothing happens until you press this.");
            ImGui.EndDisabled();
            ImGui.EndChild();
        }

        private int LightCount()
        {
            var p = Preview;
            if (p == null) return 0;
            if (p.Yft?.Fragment?.LightAttributes?.data_items != null) return p.Yft.Fragment.LightAttributes.data_items.Length;
            if (p.Drawable is Drawable d && d.LightAttributes?.data_items != null) return d.LightAttributes.data_items.Length;
            return 0;
        }

        private void DrawOptionsTab()
        {
            if (!ImGui.BeginChild("##options", new Vector2(0, 0))) { ImGui.EndChild(); return; }

            ImGui.TextDisabled("VIEW");
            ImGui.SetNextItemWidth(-120);
            ImGui.Combo("Shading", ref ViewMode, ViewModeLabels, ViewModeLabels.Length);
            ImGui.Checkbox("Wireframe", ref Wireframe);
            ImGui.Checkbox("Backface culling", ref BackfaceCulling);
            ImGui.Checkbox("Textures", ref ShowTextures);
            ImGui.Checkbox("Grid", ref ShowGrid);
            ImGui.SameLine();
            ImGui.TextDisabled($"({GridStep:0.##} m cells)");
            ImGui.Checkbox("Axes", ref ShowAxes);

            ImGui.Spacing(); ImGui.Separator();
            ImGui.TextDisabled("LIGHTING");
            ImGui.SetNextItemWidth(-120);
            ImGui.Combo("Light setup", ref LightSetup, LightSetupLabels, LightSetupLabels.Length);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Studio: a fixed key / fill / rim rig that reads a shape from any angle.\n" +
                                 "The file's own lights: what the prop carries, at its own origin - what\n" +
                                 "the light workspace would show. Flat ambient: no directional light at all.");
            ImGui.SetNextItemWidth(-120);
            UiSlider_U28.Float("Ambient", ref AmbientLevel, 0.0f, 1.0f, "%.2f");
            ImGui.SetNextItemWidth(-120);
            UiSlider_U28.Float("Exposure", ref Exposure, 0.1f, 4.0f, "%.2f");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Overall brightness. This window has no sky and no auto exposure -\n" +
                                 "on purpose, so the same prop looks the same whatever hour the\n" +
                                 "world workspace is at - so this is the only exposure there is.");

            ImGui.Spacing(); ImGui.Separator();
            ImGui.TextDisabled("BACKGROUND");
            var bg = Background;
            if (ImGui.ColorEdit3("Colour", ref bg, ImGuiColorEditFlags.NoInputs)) Background = bg;
            ImGui.SameLine();
            if (ImGui.SmallButton("Dark")) Background = new Vector3(0.10f, 0.11f, 0.13f);
            ImGui.SameLine();
            if (ImGui.SmallButton("Grey")) Background = new Vector3(0.35f, 0.36f, 0.38f);
            ImGui.SameLine();
            if (ImGui.SmallButton("White")) Background = new Vector3(0.92f, 0.93f, 0.95f);

            ImGui.Spacing(); ImGui.Separator();
            ImGui.TextDisabled("CAMERA");
            if (ImGui.Button("Frame the model", new Vector2(-1, 0))) FrameModel();
            ImGui.TextWrapped("Left-drag orbits, right-drag pans, the wheel zooms, F frames.");

            DrawTextureExportOptions_Q1();
            ImGui.EndChild();
        }

        private void DrawTexturesTab()
        {
            if (DrawTexturesTab_S3()) return;
            if (Preview == null) { ImGui.TextDisabled("Nothing open."); return; }
            var list = Preview.Kind == AssetKind.TextureDict ? Preview.Textures : Preview.Stats.Textures;
            if (list == null || list.Count == 0)
            {
                ImGui.TextWrapped(Preview.Kind == AssetKind.TextureDict
                    ? "This dictionary holds no textures."
                    : "This file carries no embedded textures - its materials resolve theirs from a .ytd.");
                return;
            }
            ImGui.TextDisabled($"{list.Count} texture(s)");
            DrawTextureExportBar_Q1(list);
            ImGui.Separator();
            if (!ImGui.BeginChild("##textures", new Vector2(0, 0))) { ImGui.EndChild(); return; }
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                ImGui.PushID(i);
                var id = t.HasData ? (TextureId?.Invoke(t.Texture) ?? IntPtr.Zero) : IntPtr.Zero;
                if (id != IntPtr.Zero) ImGui.Image(id, new Vector2(56, 56));
                else ImGui.Dummy(new Vector2(56, 56));
                ImGui.SameLine();
                ImGui.BeginGroup();
                if (ImGui.Selectable(t.Name + "##sel", selTexture == i)) { selTexture = i; YtdGrid_V44 = false; }
                ImGui.TextDisabled($"{t.Width}x{t.Height}  {t.Format}" + (t.Levels > 1 ? $"  {t.Levels} mips" : ""));
                ImGui.TextDisabled($"{t.DataBytes / 1024:N0} KB  {t.Usage}");
                ImGui.EndGroup();
                ImGui.PopID();
                ImGui.Separator();
            }
            ImGui.EndChild();
        }

        private void DrawClipRow()
        {
            DrawClipRow_V55();
        }
    }
}

