using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;
using SharpDX.Direct3D11;
using Buffer = SharpDX.Direct3D11.Buffer;
using GameTexture = CodeWalker.GameFiles.Texture;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public readonly TerrainEditor TerrainEd = new TerrainEditor();

        private bool terrainWired_R4;
        private string terrainLastDir_R4;
        private bool terrainPainting_R4;
        private bool terrainErasing_S2;
        private ShaderResourceView[] terrainWeightSrv_R4;
        private bool terrainWeightApplied_R4;

        private readonly string[] terrainPendingNames_R4 = new string[TerrainEditor.LayerCount];

        private int terrainEnvFrame_R4;
        private bool terrainEnvImported_R4, terrainEnvLayers_R4, terrainEnvPainted_R4, terrainEnvExported_R4;

        private RenderModel terrainModel_R4;

        private bool TerrainHasModel_R4 => panel != null && panel.TerrainMode && terrainModel_R4 != null &&
                                           terrainModel_R4.Meshes.Count > 0;

        private void ApplyTerrainSpaceFlag_R4()
        {
            if (panel != null && panel.Terrain == null) { panel.Terrain = TerrainEd; terrainWired_R4 = true; }
            TerrainLoadLogo_R4();
            if (Environment.GetEnvironmentVariable("RLE_TERRAINSPACE") != "1") return;
            panel.Workspace = LightPanel.Space.Terrain;
            panel.ApplyThemeFromSettings(false);
        }

        private void TerrainLoadLogo_R4()
        {
            if (panel == null || panel.TerrainLogoTexture != IntPtr.Zero || textureLoader == null || imguiRenderer == null) return;
            var srv = textureLoader.LoadEmbeddedPng("terrain_logo.png", out int w, out int h);
            if (srv == null) return;
            panel.TerrainLogoTexture = imguiRenderer.RegisterTexture(srv);
            panel.TerrainLogoWidth = w;
            panel.TerrainLogoHeight = h;
        }

        private void OnWorldTick_Terrain_R4()
        {
            if (panel == null) return;
            if (!terrainWired_R4) { terrainWired_R4 = true; panel.Terrain = TerrainEd; }
            TerrainHeadless_R4();
            if (!panel.TerrainMode) return;
            var te = TerrainEd;

            if (te.RequestImportFile) { te.RequestImportFile = false; TerrainImportDialog_R4(); }
            if (te.RequestImportOpen) { te.RequestImportOpen = false; TerrainImportFromScene_R4(); }
            if (te.RequestClear) { te.RequestClear = false; TerrainDropModel_R4(); te.Clear(); }
            if (te.RequestFrame) { te.RequestFrame = false; TerrainFrame_R4(); }
            if (te.RequestUndo) { te.RequestUndo = false; if (te.History.CanUndo) { te.History.Undo(); te.Status = "Undo"; } }
            if (te.RequestRedo) { te.RequestRedo = false; if (te.History.CanRedo) { te.History.Redo(); te.Status = "Redo"; } }
            if (te.RequestClearLayer >= 0) { int s = te.RequestClearLayer; te.RequestClearLayer = -1; te.Layers[s].Clear(); TerrainApplyLayers_R4(); }
            if (te.RequestFillLayer >= 0) { int s = te.RequestFillLayer; te.RequestFillLayer = -1; te.Fill(s); }
            if (te.RequestLayerFromDisk >= 0) { int s = te.RequestLayerFromDisk; te.RequestLayerFromDisk = -1; TerrainLayerFromDisk_R4(s, false); }
            if (te.RequestLayerBumpFromDisk >= 0) { int s = te.RequestLayerBumpFromDisk; te.RequestLayerBumpFromDisk = -1; TerrainLayerFromDisk_R4(s, true); }
            if (te.RequestLayerFromGame >= 0) { int s = te.RequestLayerFromGame; te.RequestLayerFromGame = -1; TerrainLayerFromGame_R4(s, te.Layers[s].Name); }
            if (te.PendingNameSlot >= 0) { int s = te.PendingNameSlot; var n = te.PendingName; te.PendingNameSlot = -1; te.PendingName = null; TerrainLayerFromGame_R4(s, n); }
            if (te.RequestExport) { te.RequestExport = false; TerrainExportDialog_R4(); }
            TerrainServiceProps_V20();

            if (screenshotPath != null && (terrainEnvFrame_R4 % 60) == 0)
            {
                var m0 = terrainModel_R4 != null && terrainModel_R4.Meshes.Count > 0 ? terrainModel_R4.Meshes[0] : null;
                var cov = te.HasMesh ? te.Coverage() : SharpDX.Vector4.Zero;
                Console.WriteLine($"TERRAINDRAW parts {te.Parts.Count} verts {te.VertexCount} gate {TerrainHasModel_R4} " +
                                  $"blend {(m0 != null && m0.IsTerrain ? "layers" : "flat")} " +
                                  $"layers {string.Join("/", Array.ConvertAll(te.Layers, l => l.Srv != null ? (l.Name ?? "?") : "-"))} " +
                                  $"coverage {cov.X:0.00} {cov.Y:0.00} {cov.Z:0.00} {cov.W:0.00} " +
                                  $"meshesDrawn {sceneRenderer?.DrawnMeshes}");
            }

            TerrainRetryNames_R4();
            OnTerrainTick_S2();
            TerrainSyncWeightView_R4();
            if (te.ApplyTiling()) TerrainApplyPresetFlags_R4();
            TerrainUploadDirty_R4();
        }

        private bool TerrainRightDown_R4(int x, int y)
        {
            return panel != null && panel.TerrainMode;
        }

        private bool TerrainKeyDown_R4(Keys combo)
        {
            if (panel == null || !panel.TerrainMode) return false;
            var te = TerrainEd;
            switch (combo)
            {
                case Keys.D1: case Keys.D2: case Keys.D3: case Keys.D4:
                    te.ActiveLayer = combo - Keys.D1;
                    return true;
                case Keys.OemOpenBrackets:
                    te.BrushRadius = Math.Max(te.BrushRadius * 0.8f, 0.05f);
                    return true;
                case Keys.OemCloseBrackets:
                    te.BrushRadius = Math.Min(te.BrushRadius * 1.25f, 200.0f);
                    return true;
                case Keys.F:
                    if (te.HasMesh && te.PaintEnabled) StartTerrainRadial_U28(1);
                    else te.RequestFrame = true;
                    return true;
                case Keys.Shift | Keys.F:
                    if (te.HasMesh && te.PaintEnabled) StartTerrainRadial_U28(2);
                    return true;
                case Keys.Home:
                    te.RequestFrame = true;
                    return true;
                case Keys.Escape:
                    if (terrainRadial_U28 != 0) { CancelTerrainRadial_U28(); return true; }
                    return false;
                case Keys.Control | Keys.Z:
                    if (te.History.CanUndo) { te.History.Undo(); te.Status = "Undo"; }
                    return true;
                case Keys.Control | Keys.Y:
                    if (te.History.CanRedo) { te.History.Redo(); te.Status = "Redo"; }
                    return true;
            }
            return false;
        }

        private void TerrainImportDialog_R4()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Meshes|*.ydr;*.obj|Drawables (*.ydr)|*.ydr|Wavefront (*.obj)|*.obj|All files|*.*",
            };
            if (!string.IsNullOrEmpty(terrainLastDir_R4) && Directory.Exists(terrainLastDir_R4)) dlg.InitialDirectory = terrainLastDir_R4;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            TerrainImportPath_R4(dlg.FileName);
        }

        public bool TerrainImportPath_R4(string path)
        {
            try
            {
                if (string.Equals(path, "grid", StringComparison.OrdinalIgnoreCase))
                    return TerrainImportGrid_R4(64, 2.0f);
                if (!File.Exists(path)) { TerrainEd.Status = "not found: " + path; return false; }
                terrainLastDir_R4 = Path.GetDirectoryName(path);
                var ext = Path.GetExtension(path).ToLowerInvariant();
                bool ok = ext == ".obj" ? TerrainImportObj_R4(path) : TerrainImportYdr_R4(path);
                if (ok)
                {
                    TerrainEd.SourcePath = path;
                    TerrainEd.Name = Path.GetFileNameWithoutExtension(path);
                    TerrainAfterImport_R4();
                }
                return ok;
            }
            catch (Exception ex)
            {
                TerrainEd.Status = "import failed: " + ex.Message;
                Console.WriteLine("TERRAIN import failed " + path + ": " + ex.Message);
                return false;
            }
        }

        private bool TerrainImportYdr_R4(string path)
        {
            var ydr = new YdrFile();
            ydr.Load(File.ReadAllBytes(path));
            var d = ydr.Drawable;
            if (d == null) { TerrainEd.Status = "not a drawable: " + Path.GetFileName(path); return false; }
            ShaderPresets.HarvestAll(d);
            TerrainAdoptSource_T3(d, path);
            TerrainDropModel_R4();
            var g_V20 = TerrainBeginImport_V20(TerrainEd.Name, TerrainEd.SourcePath);
            var models = d.DrawableModels?.High;
            if (models == null || models.Length == 0) models = d.DrawableModels?.Med;
            if (models == null || models.Length == 0) { TerrainEd.Status = "the drawable has no models"; return false; }
            foreach (var m in models)
            {
                if (m?.Geometries == null) continue;
                foreach (var g in m.Geometries)
                {
                    var idx = g.IndexBuffer?.Indices;
                    var verts = VertexDecoder.Decode(g.VertexData, idx);
                    if (verts == null || verts.Length == 0 || idx == null || idx.Length < 3) continue;
                    TerrainEd.Parts.Add(new TerrainEditor.Part
                    {
                        Verts = verts,
                        Indices = (ushort[])idx.Clone(),
                        SourceName = ShaderPresets.NameOf(g.Shader?.Name.Hash ?? 0) ?? g.Shader?.Name.ToString() ?? "",
                    });
                    TerrainAdoptLayers_R4(g.Shader);
                }
            }
            TerrainEndImport_V20(g_V20.group, g_V20.firstPart);
            return TerrainEd.Parts.Count > 0;
        }

        private bool TerrainImportObj_R4(string path)
        {
            var pos = new List<Vector3>();
            var uvs = new List<Vector2>();
            var nrm = new List<Vector3>();
            var verts = new List<MeshVertex>();
            var indices = new List<int>();
            var map = new Dictionary<(int, int, int), int>();
            var ci = CultureInfo.InvariantCulture;

            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var t = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length == 0) continue;
                switch (t[0])
                {
                    case "v":
                        if (t.Length >= 4) pos.Add(new Vector3(F(t[1]), F(t[2]), F(t[3])));
                        break;
                    case "vt":
                        if (t.Length >= 3) uvs.Add(new Vector2(F(t[1]), 1.0f - F(t[2])));
                        break;
                    case "vn":
                        if (t.Length >= 4) nrm.Add(new Vector3(F(t[1]), F(t[2]), F(t[3])));
                        break;
                    case "f":
                        var corner = new List<int>();
                        for (int i = 1; i < t.Length; i++)
                        {
                            var key = ParseObjCorner_R4(t[i], pos.Count, uvs.Count, nrm.Count);
                            if (key.v < 0) continue;
                            if (!map.TryGetValue(key, out int vi))
                            {
                                vi = verts.Count;
                                map[key] = vi;
                                verts.Add(new MeshVertex
                                {
                                    Position = pos[key.v],
                                    Normal = key.n >= 0 && key.n < nrm.Count ? nrm[key.n] : Vector3.UnitZ,
                                    Tangent = new Vector4(1, 0, 0, 1),
                                    Colour0 = Vector4.One,
                                    Colour1 = new Vector4(0, 0, 0, 1),
                                    UV0 = key.t >= 0 && key.t < uvs.Count ? uvs[key.t] : Vector2.Zero,
                                    UV1 = key.t >= 0 && key.t < uvs.Count ? uvs[key.t] : Vector2.Zero,
                                });
                            }
                            corner.Add(vi);
                        }
                        for (int i = 2; i < corner.Count; i++)
                        {
                            indices.Add(corner[0]); indices.Add(corner[i - 1]); indices.Add(corner[i]);
                        }
                        break;
                }
            }
            float F(string s) => float.TryParse(s, NumberStyles.Float, ci, out float f) ? f : 0.0f;

            if (verts.Count == 0 || indices.Count < 3) { TerrainEd.Status = "the .obj has no faces"; return false; }
            TerrainDropModel_R4();
            var g_V20 = TerrainBeginImport_V20(TerrainEd.Name, TerrainEd.SourcePath);
            TerrainSplitInto64k_R4(verts, indices);
            if (nrm.Count == 0) TerrainComputeNormals_R4();
            TerrainEndImport_V20(g_V20.group, g_V20.firstPart);
            return TerrainEd.Parts.Count > 0;
        }

        private static (int v, int t, int n) ParseObjCorner_R4(string s, int nv, int nt, int nn)
        {
            var p = s.Split('/');
            int Idx(string x, int count)
            {
                if (string.IsNullOrEmpty(x) || !int.TryParse(x, out int i)) return -1;
                return i > 0 ? i - 1 : count + i;
            }
            int v = Idx(p.Length > 0 ? p[0] : null, nv);
            int t = Idx(p.Length > 1 ? p[1] : null, nt);
            int n = Idx(p.Length > 2 ? p[2] : null, nn);
            if (v < 0 || v >= nv) return (-1, -1, -1);
            return (v, t, n);
        }

        private void TerrainSplitInto64k_R4(List<MeshVertex> verts, List<int> indices)
        {
            const int Cap = 65000;
            var partVerts = new List<MeshVertex>();
            var partIdx = new List<ushort>();
            var remap = new Dictionary<int, ushort>();

            void Flush()
            {
                if (partIdx.Count < 3) { partVerts.Clear(); partIdx.Clear(); remap.Clear(); return; }
                TerrainEd.Parts.Add(new TerrainEditor.Part { Verts = partVerts.ToArray(), Indices = partIdx.ToArray() });
                partVerts.Clear(); partIdx.Clear(); remap.Clear();
            }

            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                if (partVerts.Count + 3 > Cap) Flush();
                for (int k = 0; k < 3; k++)
                {
                    int src = indices[i + k];
                    if (!remap.TryGetValue(src, out ushort dst))
                    {
                        dst = (ushort)partVerts.Count;
                        remap[src] = dst;
                        partVerts.Add(verts[src]);
                    }
                    partIdx.Add(dst);
                }
            }
            Flush();
        }

        private void TerrainComputeNormals_R4()
        {
            foreach (var part in TerrainEd.Parts)
            {
                var acc = new Vector3[part.Verts.Length];
                for (int i = 0; i + 2 < part.Indices.Length; i += 3)
                {
                    int a = part.Indices[i], b = part.Indices[i + 1], c = part.Indices[i + 2];
                    var n = Vector3.Cross(part.Verts[b].Position - part.Verts[a].Position,
                                          part.Verts[c].Position - part.Verts[a].Position);
                    acc[a] += n; acc[b] += n; acc[c] += n;
                }
                for (int i = 0; i < acc.Length; i++)
                {
                    var n = acc[i];
                    part.Verts[i].Normal = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.UnitZ;
                }
            }
        }

        public bool TerrainImportGrid_R4(int cells, float step)
        {
            cells = Math.Max(2, Math.Min(200, cells));
            var verts = new List<MeshVertex>();
            var idx = new List<int>();
            float half = cells * step * 0.5f;
            for (int y = 0; y <= cells; y++)
                for (int x = 0; x <= cells; x++)
                {
                    float u = (float)x / cells, v = (float)y / cells;
                    verts.Add(new MeshVertex
                    {
                        Position = new Vector3(x * step - half, y * step - half, 0),
                        Normal = Vector3.UnitZ,
                        Tangent = new Vector4(1, 0, 0, 1),
                        Colour0 = Vector4.One,
                        Colour1 = new Vector4(0, 0, 0, 1),
                        UV0 = new Vector2(u * cells * 0.25f, v * cells * 0.25f),
                        UV1 = new Vector2(u, v),
                    });
                }
            int w = cells + 1;
            for (int y = 0; y < cells; y++)
                for (int x = 0; x < cells; x++)
                {
                    int a = y * w + x, b = a + 1, c = a + w, d = c + 1;
                    idx.Add(a); idx.Add(b); idx.Add(c);
                    idx.Add(b); idx.Add(d); idx.Add(c);
                }
            TerrainDropModel_R4();
            var g_V20 = TerrainBeginImport_V20(TerrainEd.Name, TerrainEd.SourcePath);
            TerrainSplitInto64k_R4(verts, idx);
            TerrainEd.SourcePath = null;
            TerrainEd.Name = "terrain_grid";
            TerrainAfterImport_R4();
            TerrainEndImport_V20(g_V20.group, g_V20.firstPart);
            return TerrainEd.Parts.Count > 0;
        }

        private void TerrainImportFromScene_R4()
        {
            var src = lightScene ?? scene;
            var file = src.Files.Count > 0 ? (src.ActiveFile ?? src.Files[0]) : null;
            if (file?.Path != null && File.Exists(file.Path) &&
                file.Path.EndsWith(".ydr", StringComparison.OrdinalIgnoreCase))
            {
                TerrainImportPath_R4(file.Path);
                return;
            }
            var d = file?.Drawable ?? (DrawableBase)file?.Ydr?.Drawable;
            if (d == null) { TerrainEd.Status = "nothing is open in the light workspace"; return; }
            TerrainDropModel_R4();
            var g_V20 = TerrainBeginImport_V20(TerrainEd.Name, TerrainEd.SourcePath);
            var models = d.DrawableModels?.High ?? d.DrawableModels?.Med;
            if (models != null)
                foreach (var m in models)
                {
                    if (m?.Geometries == null) continue;
                    foreach (var g in m.Geometries)
                    {
                        var gi = g.IndexBuffer?.Indices;
                        var gv = VertexDecoder.Decode(g.VertexData, gi);
                        if (gv == null || gi == null || gi.Length < 3) continue;
                        TerrainEd.Parts.Add(new TerrainEditor.Part { Verts = gv, Indices = (ushort[])gi.Clone() });
                        TerrainAdoptLayers_R4(g.Shader);
                    }
                }
            TerrainEndImport_V20(g_V20.group, g_V20.firstPart);
            if (TerrainEd.Parts.Count == 0) { TerrainEd.Status = "that model has no geometry to paint"; return; }
            TerrainEd.Name = Path.GetFileNameWithoutExtension(file.Path ?? "terrain");
            TerrainEd.SourcePath = null;
            TerrainAfterImport_R4();
        }

        private void TerrainAdoptLayers_R4(ShaderFX shader)
        {
            var ps = shader?.ParametersList?.Parameters;
            var hs = shader?.ParametersList?.Hashes;
            if (ps == null || hs == null) return;
            var want = new[]
            {
                ShaderParamNames.TextureSampler_layer0, ShaderParamNames.TextureSampler_layer1,
                ShaderParamNames.TextureSampler_layer2, ShaderParamNames.TextureSampler_layer3,
            };
            for (int i = 0; i < ps.Length && i < hs.Length; i++)
            {
                if (!(ps[i].Data is TextureBase tb) || string.IsNullOrEmpty(tb.Name)) continue;
                for (int s = 0; s < want.Length; s++)
                {
                    if ((uint)hs[i] != (uint)want[s]) continue;
                    if (!string.IsNullOrEmpty(TerrainEd.Layers[s].Name)) continue;
                    bool local = false;
                    TerrainAdoptLocal_T3(s, tb.Name, ref local);
                    if (!local) TerrainLayerFromGame_R4(s, tb.Name);
                }
            }
        }

        private void TerrainAfterImport_R4()
        {
            TerrainEd.RebuildBounds();
            TerrainEd.CaptureBaseUvs();
            TerrainEd.History.Clear();
            TerrainEd.Dirty = false;
            if (panel != null) panel.ShowGrid = false;
            TerrainBuildModel_R4();
            TerrainFrame_R4();
            var b = TerrainEd.Bounds;
            var size = b.Maximum - b.Minimum;
            TerrainEd.Status = $"{TerrainEd.Name}: {TerrainEd.VertexCount:N0} vertices, {TerrainEd.TriangleCount:N0} triangles, " +
                               $"{TerrainEd.Parts.Count} part(s), {size.X:0.#} x {size.Y:0.#} x {size.Z:0.#} m";
            Console.WriteLine("TERRAIN imported " + TerrainEd.Name + " - " + TerrainEd.Status);
        }

        private void TerrainDropModel_R4()
        {
            terrainModel_R4?.Dispose();
            terrainModel_R4 = null;
            foreach (var p in TerrainEd.Parts) p.Mesh = null;
        }

        private void TerrainBuildModel_R4()
        {
            TerrainDropModel_R4();
            if (!TerrainEd.HasMesh || deviceResources?.Device == null) return;
            var dev = deviceResources.Device;
            var model = new RenderModel { Name = "terrain" };
            foreach (var part in TerrainEd.Parts)
            {
                if (!TerrainEd.IsVisible_V20(part.Group_V20)) continue;
                var mesh = new RenderMesh
                {
                    Transform = Matrix.Identity,
                    IndexCount = part.Indices.Length,
                    ShaderName = TerrainEd.ExportPreset,
                    AlphaMode = GeomAlphaMode.Opaque,
                    IsTerrain = true,
                    DoubleSided = true,
                    TerrainBlendMode = 0,
                    LayersSrgbView = true,
                    MatDiffuse = new Vector4(0.55f, 0.55f, 0.55f, 1.0f),
                };
                mesh.VB = Buffer.Create(dev, part.Verts, new BufferDescription
                {
                    BindFlags = BindFlags.VertexBuffer,
                    Usage = ResourceUsage.Dynamic,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    SizeInBytes = MeshVertex.Stride * part.Verts.Length,
                });
                mesh.IB = Buffer.Create(dev, BindFlags.IndexBuffer, part.Indices);
                var pick = new Vector3[part.Verts.Length];
                for (int i = 0; i < part.Verts.Length; i++) pick[i] = part.Verts[i].Position;
                mesh.PickVerts = pick;
                mesh.PickIndices = part.Indices;
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                foreach (var v in part.Verts) { min = Vector3.Min(min, v.Position); max = Vector3.Max(max, v.Position); }
                mesh.LocalBounds = new BoundingBox(min, max);
                mesh.SetBoundsFromLocal();
                part.Mesh = mesh;
                part.Dirty = false;
                model.Meshes.Add(mesh);
                model.Bounds = BoundingBox.Merge(model.Bounds, mesh.WorldBounds);
            }
            terrainModel_R4 = model;
            TerrainApplyLayers_R4();
            TerrainApplyPresetFlags_R4();
        }

        private void TerrainApplyPresetFlags_R4()
        {
            if (terrainModel_R4 == null) return;
            bool twoUv = TerrainEd.TwoUvSets;
            foreach (var mesh in terrainModel_R4.Meshes)
            {
                mesh.TerrainLayersUseUv1 = twoUv;
                mesh.ShaderName = TerrainEd.ExportPreset;
            }
        }

        private void TerrainApplyLayers_R4()
        {
            if (terrainModel_R4 == null) return;
            bool weights = TerrainEd.ShowWeights;
            var flat = weights ? TerrainWeightSrvs_R4() : null;
            foreach (var mesh in terrainModel_R4.Meshes)
            {
                for (int i = 0; i < 4; i++)
                {
                    var srv = weights ? flat[i] : (TerrainEd.Layers[i].Srv ?? TerrainEd.Layers[0].Srv);
                    mesh.LayerSRV[i] = srv;
                    mesh.LayerBumpSRV[i] = weights ? null : (TerrainEd.Layers[i].BumpSrv ?? TerrainEd.Layers[0].BumpSrv);
                }
                mesh.DiffuseSRV = mesh.LayerSRV[0];
                mesh.IsTerrain = mesh.LayerSRV[0] != null;
                mesh.LayersSrgbView = !weights;
            }
            terrainWeightApplied_R4 = weights;
        }

        private void TerrainSyncWeightView_R4()
        {
            if (terrainWeightApplied_R4 != TerrainEd.ShowWeights) TerrainApplyLayers_R4();
        }

        private ShaderResourceView[] TerrainWeightSrvs_R4()
        {
            if (terrainWeightSrv_R4 != null) return terrainWeightSrv_R4;
            var dev = deviceResources.Device;
            var cols = new[]
            {
                new byte[] { 40, 40, 200, 255 },
                new byte[] { 40, 200, 40, 255 },
                new byte[] { 200, 60, 40, 255 },
                new byte[] { 230, 230, 230, 255 },
            };
            terrainWeightSrv_R4 = new ShaderResourceView[4];
            for (int i = 0; i < 4; i++)
            {
                var desc = new Texture2DDescription
                {
                    Width = 1, Height = 1, MipLevels = 1, ArraySize = 1,
                    Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                    SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                    Usage = ResourceUsage.Immutable,
                    BindFlags = BindFlags.ShaderResource,
                };
                var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(cols[i], 0, ptr, 4);
                    using var tex = new Texture2D(dev, desc, new DataRectangle(ptr, 4));
                    terrainWeightSrv_R4[i] = new ShaderResourceView(dev, tex);
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }
            }
            return terrainWeightSrv_R4;
        }

        private void TerrainUploadDirty_R4()
        {
            if (terrainModel_R4 == null) return;
            var ctx = deviceResources?.Device?.ImmediateContext;
            if (ctx == null) return;
            foreach (var part in TerrainEd.Parts)
            {
                if (!part.Dirty || part.Mesh?.VB == null) continue;
                part.Dirty = false;
                var box = ctx.MapSubresource(part.Mesh.VB, 0, MapMode.WriteDiscard, MapFlags.None);
                try
                {
                    SharpDX.Utilities.Write(box.DataPointer, part.Verts, 0, part.Verts.Length);
                }
                finally { ctx.UnmapSubresource(part.Mesh.VB, 0); }
            }
        }

        partial void AddTerrainModels_R4(List<RenderModel> draw)
        {
            if (!TerrainHasModel_R4 || draw == null) return;
            draw.Add(terrainModel_R4);
        }

        private void TerrainLayerFromDisk_R4(int slot, bool bump)
        {
            if (slot < 0 || slot >= TerrainEditor.LayerCount) return;
            using var dlg = new OpenFileDialog { Filter = "Textures|*.dds;*.png;*.jpg;*.bmp|All files|*.*" };
            if (!string.IsNullOrEmpty(terrainLastDir_R4) && Directory.Exists(terrainLastDir_R4)) dlg.InitialDirectory = terrainLastDir_R4;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            TerrainLayerFromFile_R4(slot, dlg.FileName, bump);
        }

        public bool TerrainLayerFromFile_R4(int slot, string path, bool bump)
        {
            try
            {
                if (!File.Exists(path)) { TerrainEd.Status = "texture not found: " + path; return false; }
                terrainLastDir_R4 = Path.GetDirectoryName(path);
                var name = scene.ImportTexture(path);
                if (name == null) { TerrainEd.Status = scene.LoadError ?? "texture import failed"; return false; }
                GameTexture tex = null;
                foreach (var t in scene.ImportedTextures) if (t.Name == name) tex = t;
                if (tex == null) { TerrainEd.Status = "texture import produced nothing"; return false; }
                var srv = textureLoader.GetSRV(tex, srgb: !bump);
                var l = TerrainEd.Layers[slot];
                if (bump) { l.BumpName = name; l.BumpTexture = tex; l.BumpSrv = srv; }
                else
                {
                    l.Name = name; l.Texture = tex; l.Srv = srv; l.Source = "disk";
                    l.ThumbId = srv != null ? imguiRenderer.RegisterTexture(srv) : IntPtr.Zero;
                }
                TerrainApplyLayers_R4();
                TerrainEd.Status = $"layer {slot} {(bump ? "normal" : "diffuse")} = {name} ({tex.Width}x{tex.Height}, from disk)";
                Console.WriteLine("TERRAIN " + TerrainEd.Status);
                return true;
            }
            catch (Exception ex)
            {
                TerrainEd.Status = "texture load failed: " + ex.Message;
                return false;
            }
        }

        public bool TerrainLayerFromGame_R4(int slot, string name)
        {
            if (slot < 0 || slot >= TerrainEditor.LayerCount || string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim().ToLowerInvariant();
            uint hash = JenkHash.GenHash(name);
            var tex = gameFiles?.FindTexture(hash, 0);
            if (tex?.Data?.FullData == null)
            {
                TerrainEd.Layers[slot].Name = name;
                if (gameFiles != null && !gameFiles.TextureIndexReady)
                {
                    terrainPendingNames_R4[slot] = name;
                    TerrainEd.Status = "looking for \"" + name + "\" - the texture index is still building";
                    return false;
                }
                terrainPendingNames_R4[slot] = null;
                TerrainEd.Status = "\"" + name + "\" is not in this install's textures";
                return false;
            }
            terrainPendingNames_R4[slot] = null;
            var l = TerrainEd.Layers[slot];
            l.Name = tex.Name ?? name;
            l.Texture = tex;
            l.Source = "game";
            l.Srv = textureLoader.GetSRV(tex, srgb: true);
            l.ThumbId = l.Srv != null ? imguiRenderer.RegisterTexture(l.Srv) : IntPtr.Zero;
            TerrainApplyLayers_R4();
            TerrainEd.Status = $"layer {slot} diffuse = {l.Name} ({tex.Width}x{tex.Height}, from the archives)";
            Console.WriteLine("TERRAIN " + TerrainEd.Status);
            return true;
        }

        private void TerrainRetryNames_R4()
        {
            if (gameFiles == null) return;
            for (int i = 0; i < TerrainEditor.LayerCount; i++)
            {
                var n = terrainPendingNames_R4[i];
                if (n == null) continue;
                if (!gameFiles.TextureIndexReady && (terrainEnvFrame_R4 % 30) != 0) continue;
                TerrainLayerFromGame_R4(i, n);
            }
        }

        partial void TerrainMouseDown_R4(int x, int y, bool rightButton, ref bool consumed)
        {
            if (panel == null || !panel.TerrainMode || !TerrainEd.PaintEnabled || !TerrainEd.HasMesh) return;
            if (terrainRadial_U28 != 0) { if (rightButton) CancelTerrainRadial_U28(); else terrainRadial_U28 = 0; consumed = true; return; }
            if (rightButton) return;
            var ray = camera.GetPickRay(x, y, deviceResources.Width, deviceResources.Height);
            if (!TerrainEd.RayHit(ref ray, out var hit)) return;
            bool invert = (ModifierKeys & (Keys.Alt | Keys.Control)) != 0;
            terrainErasing_S2 = invert != (TerrainEd.Blend_U28 == TerrainEditor.BrushBlend_U28.Subtract);
            terrainLazy_U28 = new Vector2(x, y);
            terrainPainting_R4 = true;
            TerrainEd.BeginStrokeAt(TerrainStrokeLayer_S2(), hit);
            consumed = true;
        }

        private int TerrainStrokeLayer_S2() => terrainErasing_S2 ? 0 : TerrainEd.ActiveLayer;

        partial void TerrainMouseMove_R4(int x, int y, bool leftDown, bool rightDown)
        {
            if (panel == null || !panel.TerrainMode || !TerrainEd.HasMesh) return;
            var ray = camera.GetPickRay(x, y, deviceResources.Width, deviceResources.Height);
            TerrainEd.CursorOnMesh = TerrainEd.RayHit(ref ray, out var hit);
            if (TerrainEd.CursorOnMesh) TerrainEd.CursorPoint = hit;
            terrainMouse_U28 = new Vector2(x, y);
            if (TerrainRadialMove_U28(x, y)) return;
            if (!terrainPainting_R4) return;
            if (!leftDown) { TerrainMouseUp_R4(); return; }
            if (!TerrainStabilized_U28(x, y, out var at)) return;
            TerrainEd.StrokeTo(at, TerrainStrokeLayer_S2());
        }

        partial void TerrainMouseUp_R4()
        {
            if (!terrainPainting_R4) return;
            terrainPainting_R4 = false;
            terrainErasing_S2 = false;
            TerrainEd.EndStrokeS2();
            var cov = TerrainEd.Coverage();
            TerrainEd.Status = $"stroke done - coverage {cov.X * 100:0}% / {cov.Y * 100:0}% / {cov.Z * 100:0}% / {cov.W * 100:0}%";
        }

        partial void DrawTerrainHelpers_R4()
        {
            if (panel == null || !panel.TerrainMode || !TerrainEd.ShowBrush) return;
            if (!TerrainEd.CursorOnMesh || !TerrainEd.HasMesh) return;
            QueueTerrainRing_U28();
        }

        private void TerrainFrame_R4()
        {
            if (!TerrainEd.HasMesh) return;
            var b = TerrainEd.Bounds;
            var centre = (b.Minimum + b.Maximum) * 0.5f;
            float radius = Math.Max((b.Maximum - b.Minimum).Length() * 0.5f, 1.0f);
            camera.FrameBounds(centre, radius);
        }

        private void TerrainExportDialog_R4()
        {
            if (!TerrainEd.HasMesh) { TerrainEd.Status = "nothing to export"; return; }
            using var dlg = new SaveFileDialog
            {
                Filter = "Drawable (*.ydr)|*.ydr|All files|*.*",
                FileName = (string.IsNullOrEmpty(TerrainEd.Name) ? "terrain" : TerrainEd.Name) + ".ydr",
            };
            if (!string.IsNullOrEmpty(terrainLastDir_R4) && Directory.Exists(terrainLastDir_R4)) dlg.InitialDirectory = terrainLastDir_R4;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            TerrainExportTo_R4(dlg.FileName, reloadIntoView: false);
        }

        public bool TerrainExportTo_R4(string path, bool reloadIntoView)
        {
            try
            {
                if (!TerrainEd.HasMesh) { TerrainEd.Status = "nothing to export"; return false; }
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var baseName = Path.GetFileNameWithoutExtension(path);
                terrainLastDir_R4 = dir;

                GameTexture mask = null, palette = null;
                if (TerrainYdr.UsesMask(TerrainEd.ExportPreset))
                {
                    mask = TerrainYdr.RasteriseMask(TerrainEd, 512, baseName + "_mask", out string why);
                    if (mask == null)
                    {
                        TerrainEd.Status = "cannot bake the _cm mask: " + why + " - export as terrain_cb_w_4lyr instead";
                        Console.WriteLine("TERRAIN export refused: " + TerrainEd.Status);
                        return false;
                    }
                }
                if (TerrainYdr.UsesTint(TerrainEd.ExportPreset)) palette = TerrainYdr.BuildTintPalette(baseName + "_tint");

                var ydr = TerrainYdr.Build(TerrainEd, baseName, mask?.Name, palette?.Name);
                if (ydr == null) { TerrainEd.Status = "the mesh produced no drawable"; return false; }

                int embedded = 0;
                string embedNote = null;
                if (TerrainEd.TextureExport == TerrainEditor.TexDestination.Embed)
                    embedded = TerrainYdr.EmbedTextures(ydr.Drawable, TerrainEd, mask, palette,
                                                        TerrainEd.IncludeGameTextures, out embedNote);

                var bytes = ydr.Save();
                File.WriteAllBytes(path, bytes);

                int texCount = 0;
                string ytdPath = null;
                if (TerrainEd.TextureExport == TerrainEditor.TexDestination.Ytd)
                {
                    var ytd = TerrainYdr.BuildYtd(TerrainEd, baseName, mask, palette, out texCount,
                                                 TerrainEd.IncludeGameTextures);
                    if (ytd != null)
                    {
                        ytdPath = Path.Combine(dir ?? ".", baseName + ".ytd");
                        File.WriteAllBytes(ytdPath, ytd.Save());
                    }
                }

                var check = new YdrFile();
                check.Load(bytes);
                var cd = check.Drawable;
                int verts = 0, blended = 0;
                string sname = "";
                var geoms = cd?.DrawableModels?.High?[0]?.Geometries;
                if (geoms != null)
                {
                    sname = ShaderPresets.NameOf(geoms[0].Shader?.Name.Hash ?? 0) ?? geoms[0].Shader?.Name.ToString() ?? "?";
                    foreach (var g in geoms)
                    {
                        var dv = VertexDecoder.Decode(g.VertexData, g.IndexBuffer?.Indices);
                        if (dv == null) continue;
                        verts += dv.Length;
                        foreach (var v in dv) if (v.Colour1.Y > 0.004f || v.Colour1.Z > 0.004f) blended++;
                    }
                }
                int reread = cd?.ShaderGroup?.TextureDictionary?.Textures?.data_items?.Length ?? 0;
                TerrainEd.Dirty = false;
                TerrainEd.Status = $"wrote {Path.GetFileName(path)} ({bytes.Length / 1024} KB, {sname}) - read back {verts:N0} vertices, " +
                                   $"{blended:N0} carrying a painted blend" +
                                   (ytdPath != null ? $"; {texCount} texture(s) in {Path.GetFileName(ytdPath)}" : "") +
                                   (TerrainEd.TextureExport == TerrainEditor.TexDestination.Embed
                                        ? $"; {embedded} texture(s) EMBEDDED in the .ydr ({reread} read back)" +
                                          (embedNote != null ? " - " + embedNote : "")
                                        : "") +
                                   (TerrainEd.TextureExport == TerrainEditor.TexDestination.Reference
                                        ? "; textures referenced by name only" : "");
                TerrainExportVerify_T3(bytes, path);
                Console.WriteLine("TERRAIN export " + path + " - " + TerrainEd.Status);

                if (reloadIntoView) TerrainImportPath_R4(path);
                Editor.UiSound.Success();
                return true;
            }
            catch (Exception ex)
            {
                TerrainEd.Status = "export failed: " + ex.Message;
                Console.WriteLine("TERRAIN export failed: " + ex);
                Editor.UiSound.Error();
                return false;
            }
        }

        private void TerrainHeadless_R4()
        {
            terrainEnvFrame_R4++;
            TerrainHeadless_S2();
            if (terrainEnvFrame_R4 < 3) return;

            if (!terrainEnvImported_R4)
            {
                terrainEnvImported_R4 = true;
                var want = Environment.GetEnvironmentVariable("RLE_TERRAINIMPORT");
                if (!string.IsNullOrWhiteSpace(want)) TerrainImportPath_R4(want.Trim());
            }
            if (!terrainEnvLayers_R4 && TerrainEd.HasMesh)
            {
                terrainEnvLayers_R4 = true;
                for (int i = 0; i < TerrainEditor.LayerCount; i++)
                {
                    var v = Environment.GetEnvironmentVariable("RLE_TERRAINLAYER" + i);
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    v = v.Trim();
                    if (File.Exists(v)) TerrainLayerFromFile_R4(i, v, false);
                    else TerrainLayerFromGame_R4(i, v);
                }
                var tile = Environment.GetEnvironmentVariable("RLE_TERRAINTILE");
                if (float.TryParse(tile, NumberStyles.Float, CultureInfo.InvariantCulture, out float tf) && tf > 0)
                    foreach (var l in TerrainEd.Layers) l.Tiling = tf;
                if (Environment.GetEnvironmentVariable("RLE_TERRAINWEIGHTS") == "1") TerrainEd.ShowWeights = true;
                var preset = Environment.GetEnvironmentVariable("RLE_TERRAINPRESET");
                if (!string.IsNullOrWhiteSpace(preset)) TerrainEd.ExportPreset = preset.Trim();
            }
            if (!terrainEnvPainted_R4 && TerrainEd.HasMesh && terrainEnvFrame_R4 > 5)
            {
                terrainEnvPainted_R4 = true;
                var fill = Environment.GetEnvironmentVariable("RLE_TERRAINFILL");
                if (int.TryParse(fill, out int fl)) TerrainEd.Fill(fl);
                var paint = Environment.GetEnvironmentVariable("RLE_TERRAINPAINT");
                if (!string.IsNullOrWhiteSpace(paint))
                {
                    foreach (var one in paint.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var p = one.Split(',');
                        if (p.Length < 5) continue;
                        float G(int i) => float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0.0f;
                        var centre = new Vector3(G(0), G(1), G(2));
                        float saveR = TerrainEd.BrushRadius, saveS = TerrainEd.BrushStrength;
                        TerrainEd.BrushRadius = Math.Max(G(3), 0.01f);
                        if (p.Length >= 6) TerrainEd.BrushStrength = G(5);
                        else TerrainEd.BrushStrength = 1.0f;
                        int layer = (int)G(4);
                        TerrainEd.BeginStroke(layer);
                        int n = TerrainEd.Paint(centre, layer);
                        TerrainEd.EndStroke();
                        TerrainEd.BrushRadius = saveR; TerrainEd.BrushStrength = saveS;
                        Console.WriteLine($"TERRAIN paint at {centre.X:0.##},{centre.Y:0.##},{centre.Z:0.##} r {G(3):0.##} layer {layer}: {n} vertices");
                    }
                    var cov = TerrainEd.Coverage();
                    Console.WriteLine($"TERRAIN coverage {cov.X:0.000} {cov.Y:0.000} {cov.Z:0.000} {cov.W:0.000}");
                }
            }
            if (!terrainEnvExported_R4 && TerrainEd.HasMesh && terrainEnvFrame_R4 > 8)
            {
                terrainEnvExported_R4 = true;
                var outp = Environment.GetEnvironmentVariable("RLE_TERRAINEXPORT");
                if (!string.IsNullOrWhiteSpace(outp))
                    TerrainExportTo_R4(outp.Trim(), Environment.GetEnvironmentVariable("RLE_TERRAINRELOAD") == "1");
            }
        }

        private void DisposeTerrain_R4()
        {
            TerrainDropModel_R4();
            if (terrainWeightSrv_R4 != null)
                foreach (var s in terrainWeightSrv_R4) s?.Dispose();
            terrainWeightSrv_R4 = null;
        }
    }
}

