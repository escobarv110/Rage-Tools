using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeWalker.GameFiles;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public class LightInstance
    {
        public LightAttributes Light;
        public Vector3 WorldPosition;
        public Vector3 WorldDirection;
        public Vector3 WorldTangent;
        public bool BoneFound;
        public string BoneName = "";
    }

    public class LoadedFile
    {
        public string Path;
        public bool IsYft;
        public YdrFile Ydr;
        public YftFile Yft;

        public DrawableBase Drawable;

        public RenderModel Model;
        public Skeleton Skeleton;
        public bool Dirty;
        public bool Visible = true;
        public string Name => System.IO.Path.GetFileName(Path);

        public bool FromMlo;

        public bool ReadOnly;

        public int InstanceCount = 1;

        public Matrix Placement = Matrix.Identity;
        public bool HasPlacement;

        public readonly List<Matrix> ExtraPlacements = new List<Matrix>();

        public string Label => InstanceCount > 1 ? $"{Name} (x{InstanceCount})" : Name;
    }

    public partial class Scene : IDisposable
    {
        public readonly List<LoadedFile> Files = new List<LoadedFile>();
        public List<LightAttributes> Lights { get; } = new List<LightAttributes>();
        public List<YtdFile> LoadedYtds { get; } = new List<YtdFile>();
        public List<string> LoadedYtdPaths { get; } = new List<string>();
        public string LoadError;

        private readonly Dictionary<LightAttributes, LoadedFile> ownerOf = new Dictionary<LightAttributes, LoadedFile>();

        private readonly Dictionary<LightAttributes, int> instanceGroupOf = new Dictionary<LightAttributes, int>();
        private int nextInstanceGroup = 1;

        public int GeometryVersion { get; private set; }
        public void GeometryChanged_V16() => GeometryVersion++;

        public RenderModel MloModel;
        public MloImportResult MloInfo;
        public bool MloVisible = true;

        public bool HasModel => Files.Count > 0 || MloModel != null;

        public IEnumerable<RenderModel> Models
        {
            get
            {
                foreach (var f in Files) if (f.Model != null && f.Visible) yield return f.Model;
                if (MloModel != null && MloVisible) yield return MloModel;
            }
        }

        public void SetMlo(RenderModel model, MloImportResult info)
        {
            MloModel?.Dispose();
            MloModel = model;
            MloInfo = info;
            GeometryVersion++;
        }

        public void AppendMlo(RenderModel model, MloImportResult info)
        {
            if (MloModel == null) { SetMlo(model, info); return; }
            MloModel.Meshes.AddRange(model.Meshes);
            foreach (var m in model.Meshes)
                MloModel.Bounds = BoundingBox.Merge(MloModel.Bounds, m.WorldBounds);
            if (MloInfo != null && !ReferenceEquals(MloInfo, info)) info.Interiors.InsertRange(0, MloInfo.Interiors);
            MloInfo = info;
            GeometryVersion++;
        }

        public void ClearMlo()
        {
            MloModel?.Dispose();
            MloModel = null;
            MloInfo = null;
            GeometryVersion++;
        }
        public IEnumerable<RenderMesh> AllMeshes => Models.SelectMany(m => m.Meshes);

        public LoadedFile ActiveFile;

        public readonly List<LoadedFile> SelectedFiles = new List<LoadedFile>();

        public bool IsFileSelected(LoadedFile f) => f != null && SelectedFiles.Contains(f);

        public bool KeepBackups;

        public bool LightsOnlyFromSelectedProp;

        public void SelectFile(LoadedFile f, bool ctrl, bool shift)
        {
            if (f == null) return;
            if (ctrl)
            {
                if (!SelectedFiles.Remove(f)) SelectedFiles.Add(f);
            }
            else if (shift && ActiveFile != null)
            {
                int a = Files.IndexOf(ActiveFile), b = Files.IndexOf(f);
                if (a >= 0 && b >= 0)
                {
                    SelectedFiles.Clear();
                    for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++) SelectedFiles.Add(Files[i]);
                }
            }
            else
            {
                SelectedFiles.Clear();
                SelectedFiles.Add(f);
            }
            ActiveFile = f;
        }

        public readonly HashSet<LightAttributes> ShadowsDisabled = new HashSet<LightAttributes>();

        public readonly HashSet<int> GhostGpuIndices = new HashSet<int>();

        public int LastEmittedLights;
        public int LastDroppedLights;

        public bool Dirty
        {
            get => Files.Any(f => f.Dirty);
            set { if (ExternalEditing && value) return; foreach (var f in Files) f.Dirty = value; }
        }

        public bool ExternalEditing;

        public string FilePath => Files.Count > 0 ? Files[0].Path : null;
        public string FileName => Files.Count == 0 ? "" :
            (Files.Count == 1 ? Files[0].Name : $"{Files[0].Name} (+{Files.Count - 1})");
        public bool IsYft => Files.Count > 0 && Files[0].IsYft;

        public Vector3 NextLightSpawnPos = Vector3.Zero;

        public readonly List<int> SelectedIndices = new List<int>();

        public int SelectedIndex
        {
            get => SelectedIndices.Count > 0 ? SelectedIndices[SelectedIndices.Count - 1] : -1;
            set
            {
                SelectedIndices.Clear();
                if (value >= 0 && value < Lights.Count) SelectedIndices.Add(value);
            }
        }

        public bool IsSelected(int i) => SelectedIndices.Contains(i);

        public bool IsLightSelected(LightAttributes l)
        {
            foreach (var i in SelectedIndices)
                if (i >= 0 && i < Lights.Count && Lights[i] == l) return true;
            return false;
        }

        public void ToggleSelect(int i)
        {
            if (!SelectedIndices.Remove(i)) SelectedIndices.Add(i);
        }

        public void RangeSelectTo(int i)
        {
            int from = SelectedIndex >= 0 ? SelectedIndex : 0;
            int lo = Math.Min(from, i), hi = Math.Max(from, i);
            for (int k = lo; k <= hi; k++)
            {
                if (!SelectedIndices.Contains(k)) SelectedIndices.Add(k);
            }
        }

        private readonly ModelRenderer modelRenderer;
        private readonly TextureLoader textureLoader;

        private readonly List<UndoState> undoStack = new List<UndoState>();
        private readonly List<UndoState> redoStack = new List<UndoState>();
        private const int MaxUndo = 128;

        private class UndoState
        {
            public List<LightAttributes> Lights;
            public List<int> Selected;
            public List<int> Owners;
            public List<int> InstanceGroups;
        }

        public Scene(ModelRenderer modelRenderer, TextureLoader textureLoader = null)
        {
            this.modelRenderer = modelRenderer;
            this.textureLoader = textureLoader;
            if (modelRenderer != null) modelRenderer.ImportedTextures = ImportedTextures;
        }

        public LightAttributes SelectedLight =>
            (SelectedIndex >= 0 && SelectedIndex < Lights.Count) ? Lights[SelectedIndex] : null;

        public LoadedFile OwnerFile(LightAttributes l) =>
            (l != null && ownerOf.TryGetValue(l, out var f)) ? f : (Files.Count > 0 ? Files[0] : null);

        public int InstanceGroup(LightAttributes l) =>
            (l != null && instanceGroupOf.TryGetValue(l, out var g)) ? g : 0;

        public bool LoadModelFile(string path, bool additive = false)
        {
            LoadError = null;
            try
            {
                ShaderPresets.Seed();

                var data = File.ReadAllBytes(path);
                var ext = Path.GetExtension(path).ToLowerInvariant();

                if (!additive)
                {
                    CloseAll();
                }
                var existing = Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
                if (existing != null) RemoveFile(existing);

                var lf = new LoadedFile { Path = path, IsYft = ext == ".yft" };

                if (lf.IsYft)
                {
                    var yft = new YftFile();
                    yft.Load(data);
                    if (yft.Fragment == null) throw new Exception("Failed to parse YFT (no fragment)");
                    lf.Yft = yft;
                    lf.Skeleton = yft.Fragment.Drawable?.Skeleton;
                    lf.Model = modelRenderer.BuildFromYft(yft);
                    AppendLights(lf, yft.Fragment.LightAttributes?.data_items);
                }
                else
                {
                    var ydr = new YdrFile();
                    ydr.Load(data);
                    if (ydr.Drawable == null) throw new Exception("Failed to parse YDR (no drawable)");
                    lf.Ydr = ydr;
                    lf.Skeleton = ydr.Drawable.Skeleton;
                    lf.Model = modelRenderer.BuildFromYdr(ydr);
                    AppendLights(lf, ydr.Drawable.LightAttributes?.data_items);
                }

                Files.Add(lf);
                foreach (var d in MaterialEditing.Drawables(lf)) ShaderPresets.HarvestAll(d);
                GeometryVersion++;
                undoStack.Clear();
                redoStack.Clear();

                var ytdPath = Path.ChangeExtension(path, ".ytd");
                if (File.Exists(ytdPath) && LoadYtdFileInternal(ytdPath))
                {
                    RebuildModel();
                }

                return true;
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
                return false;
            }
        }

        private void AppendLights(LoadedFile lf, LightAttributes[] items)
        {
            if (items == null) return;
            foreach (var l in items)
            {
                Lights.Add(l);
                ownerOf[l] = lf;
            }
        }

        public LoadedFile AddImportedProp(string path, YdrFile ydr, YftFile yft, RenderModel model,
            Skeleton skeleton, LightAttributes[] lights, Matrix placement, int instanceCount,
            bool readOnly, string displayName, IReadOnlyList<Matrix> extraPlacements = null,
            bool fromMlo = true, DrawableBase drawable = null)
        {
            var lf = new LoadedFile
            {
                Path = path ?? displayName,
                IsYft = yft != null,
                Ydr = ydr,
                Yft = yft,
                Drawable = drawable,
                Model = model,
                Skeleton = skeleton,
                FromMlo = fromMlo,
                ReadOnly = readOnly || path == null,
                InstanceCount = Math.Max(1, instanceCount),
                Placement = placement,
                HasPlacement = placement != Matrix.Identity,
            };
            if (extraPlacements != null) lf.ExtraPlacements.AddRange(extraPlacements);
            Files.Add(lf);
            AppendLights(lf, lights);
            GeometryVersion++;
            return lf;
        }

        public LoadedFile AddLightProxy(string name)
        {
            var ydr = LightProxyBuilder.Create(name);
            var lf = new LoadedFile
            {
                Path = LightProxyBuilder.Sanitised(name) + ".ydr",
                IsYft = false,
                Ydr = ydr,
                Model = modelRenderer.BuildFromYdr(ydr),
                Skeleton = ydr.Drawable.Skeleton,
                Dirty = true,
            };
            Files.Add(lf);
            ActiveFile = lf;
            GeometryVersion++;
            undoStack.Clear();
            redoStack.Clear();
            return lf;
        }

        public void RemoveMloProps()
        {
            for (int i = Files.Count - 1; i >= 0; i--)
            {
                if (Files[i].FromMlo) RemoveFile(Files[i]);
            }
        }

        public void RemoveFile(LoadedFile lf)
        {
            if (lf == null) return;
            for (int i = Lights.Count - 1; i >= 0; i--)
            {
                if (OwnerFile(Lights[i]) == lf)
                {
                    instanceGroupOf.Remove(Lights[i]);
                    ownerOf.Remove(Lights[i]);
                    Lights.RemoveAt(i);
                }
            }
            SelectedIndices.Clear();
            SelectedFiles.Remove(lf);
            if (ActiveFile == lf) ActiveFile = null;
            lf.Model?.Dispose();
            Files.Remove(lf);
            GeometryVersion++;
            undoStack.Clear();
            redoStack.Clear();
        }

        public void CloseAllFiles()
        {
            CloseAll();
            GeometryVersion++;
        }

        private void CloseAll()
        {
            foreach (var f in Files) f.Model?.Dispose();
            Files.Clear();
            LoadedYtds.Clear();
            LoadedYtdPaths.Clear();
            textureLoader?.Clear();
            Lights.Clear();
            ownerOf.Clear();
            instanceGroupOf.Clear();
            undoStack.Clear();
            redoStack.Clear();
            SelectedIndices.Clear();
        }

        public bool RegisterYtd(YtdFile ytd, string path)
        {
            if (ytd?.TextureDict == null) return false;
            if (LoadedYtdPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) return false;
            LoadedYtds.Add(ytd);
            LoadedYtdPaths.Add(path);
            modelRenderer.ExternalTextureDicts.Add(ytd.TextureDict);
            return true;
        }

        private bool LoadYtdFileInternal(string path)
        {
            var ytd = new YtdFile();
            ytd.Load(File.ReadAllBytes(path));
            if (ytd.TextureDict == null) return false;
            LoadedYtds.Add(ytd);
            LoadedYtdPaths.Add(path);
            modelRenderer.ExternalTextureDicts.Add(ytd.TextureDict);
            return true;
        }

        public bool LoadYtdFile(string path)
        {
            try
            {
                if (!LoadYtdFileInternal(path)) throw new Exception("no texture dict");
                RebuildModel();
                return true;
            }
            catch (Exception ex)
            {
                LoadError = "YTD load failed: " + ex.Message;
                return false;
            }
        }

        public void RebuildModel()
        {
            modelRenderer.ClearMissingTextures_V21();
            foreach (var f in Files)
            {
                if (f.Ydr == null && f.Yft == null && f.Drawable != null)
                {
                    var w2 = f.HasPlacement ? f.Placement : (Matrix?)null;
                    f.Model?.Dispose();
                    f.Model = modelRenderer.BuildFromDrawable(f.Drawable, YddMemberName_V38(f.Path), w2);
                    continue;
                }
                if (f.IsYft ? f.Yft == null : f.Ydr == null) continue;

                var world = f.HasPlacement ? f.Placement : (Matrix?)null;
                f.Model?.Dispose();
                f.Model = f.IsYft
                    ? modelRenderer.BuildFromYft(f.Yft, world)
                    : modelRenderer.BuildFromYdr(f.Ydr, world);
            }
            GeometryVersion++;
        }

        public BoundingBox? GetSceneBounds()
        {
            BoundingBox? bb = null;
            foreach (var m in Models)
            {
                if (m.Bounds.Minimum.X > m.Bounds.Maximum.X) continue;
                bb = bb.HasValue ? BoundingBox.Merge(bb.Value, m.Bounds) : m.Bounds;
            }
            return bb;
        }

        private Skeleton SkeletonFor(LightAttributes l) => OwnerFile(l)?.Skeleton;

        public LightInstance GetInstance(LightAttributes l)
        {
            var owner = OwnerFile(l);
            return GetInstanceAt(l, owner != null && owner.HasPlacement ? owner.Placement : (Matrix?)null);
        }

        public LightInstance GetInstanceAt(LightAttributes l, Matrix? placement)
        {
            var inst = new LightInstance
            {
                Light = l,
                WorldPosition = l.Position,
                WorldDirection = l.Direction,
                WorldTangent = l.Tangent,
            };

            var skeleton = SkeletonFor(l);
            if (l.BoneId != 0 && skeleton?.BonesMap != null &&
                skeleton.BonesMap.TryGetValue(l.BoneId, out var bone) && bone != null)
            {
                var m = bone.AnimTransform;
                inst.WorldPosition = Vector3.TransformCoordinate(l.Position, m);
                inst.WorldDirection = Vector3.Normalize(Vector3.TransformNormal(l.Direction, m));
                inst.WorldTangent = Vector3.Normalize(Vector3.TransformNormal(l.Tangent, m));
                inst.BoneFound = true;
                inst.BoneName = bone.Name ?? "";
            }

            if (placement.HasValue)
            {
                var p = placement.Value;
                inst.WorldPosition = Vector3.TransformCoordinate(inst.WorldPosition, p);
                inst.WorldDirection = Vector3.Normalize(Vector3.TransformNormal(inst.WorldDirection, p));
                inst.WorldTangent = Vector3.Normalize(Vector3.TransformNormal(inst.WorldTangent, p));
            }
            return inst;
        }

        public Vector3 WorldToLightSpace(LightAttributes l, Vector3 worldPos)
        {
            var owner = OwnerFile(l);
            if (owner != null && owner.HasPlacement)
                worldPos = Vector3.TransformCoordinate(worldPos, Matrix.Invert(owner.Placement));

            var skeleton = SkeletonFor(l);
            if (l.BoneId != 0 && skeleton?.BonesMap != null &&
                skeleton.BonesMap.TryGetValue(l.BoneId, out var bone) && bone != null)
            {
                var inv = Matrix.Invert(bone.AnimTransform);
                return Vector3.TransformCoordinate(worldPos, inv);
            }
            return worldPos;
        }

        public Vector3 WorldToLightSpaceDir(LightAttributes l, Vector3 worldDir)
        {
            var owner = OwnerFile(l);
            if (owner != null && owner.HasPlacement)
            {
                worldDir = Vector3.TransformNormal(worldDir, Matrix.Invert(owner.Placement));
                if (worldDir.LengthSquared() > 1e-9f) worldDir.Normalize();
            }

            var skeleton = SkeletonFor(l);
            if (l.BoneId != 0 && skeleton?.BonesMap != null &&
                skeleton.BonesMap.TryGetValue(l.BoneId, out var bone) && bone != null)
            {
                var inv = Matrix.Invert(bone.AnimTransform);
                var d = Vector3.TransformNormal(worldDir, inv);
                if (d.LengthSquared() > 1e-9f) d.Normalize();
                return d;
            }
            return worldDir;
        }

        public IEnumerable<ushort> GetBoneTags()
        {
            var bones = SkeletonFor(SelectedLight)?.Bones?.Items;
            if (bones == null) yield break;
            foreach (var b in bones)
            {
                if (b != null) yield return b.Tag;
            }
        }

        public string GetBoneName(ushort tag)
        {
            var skeleton = SkeletonFor(SelectedLight);
            if (skeleton?.BonesMap != null && skeleton.BonesMap.TryGetValue(tag, out var bone))
            {
                return bone?.Name ?? "";
            }
            return "";
        }

        public List<CodeWalker.GameFiles.Texture> ImportedTextures { get; } = new List<CodeWalker.GameFiles.Texture>();

        private IEnumerable<TextureDictionary> EmbeddedDicts()
        {
            foreach (var f in Files)
            {
                var d = f.IsYft
                    ? f.Yft?.Fragment?.Drawable?.ShaderGroup?.TextureDictionary
                    : f.Ydr?.Drawable?.ShaderGroup?.TextureDictionary;
                if (d != null) yield return d;
            }
        }

        public CodeWalker.GameFiles.Texture FindTexture(uint hash)
        {
            if (hash == 0) return null;
            foreach (var it in ImportedTextures)
            {
                if (it.NameHash == hash && it.Data?.FullData != null) return it;
            }
            foreach (var d in EmbeddedDicts())
            {
                var t = d.Lookup(hash);
                if (t?.Data?.FullData != null) return t;
            }
            foreach (var ytd in LoadedYtds)
            {
                var t = ytd?.TextureDict?.Lookup(hash);
                if (t?.Data?.FullData != null) return t;
            }
            return null;
        }

        public string ImportTexture(string path)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                var tex = ImageImport_V18.Load_V18(path, out var importError);
                if (tex?.Data?.FullData == null)
                { LoadError = "Texture import failed: " + (importError ?? path); return null; }
                tex.Name = name;
                tex.NameHash = JenkHash.GenHash(name);
                ImportedTextures.RemoveAll(t => t.NameHash == tex.NameHash);
                ImportedTextures.Add(tex);
                return name;
            }
            catch (Exception ex)
            {
                LoadError = "Texture import failed: " + ex.Message;
                return null;
            }
        }

        public List<string> GetTextureNames()
        {
            var names = new List<string>();
            foreach (var it in ImportedTextures)
            {
                if (!string.IsNullOrEmpty(it?.Name) && !names.Contains(it.Name)) names.Add(it.Name);
            }
            void AddDict(TextureDictionary d)
            {
                var items = d?.Textures?.data_items;
                if (items == null) return;
                foreach (var t in items)
                {
                    if (!string.IsNullOrEmpty(t?.Name) && !names.Contains(t.Name)) names.Add(t.Name);
                }
            }
            foreach (var d in EmbeddedDicts()) AddDict(d);
            foreach (var ytd in LoadedYtds) AddDict(ytd?.TextureDict);
            return names;
        }

        public struct VolumeDraw
        {
            public Vector3 Pos, Dir;
            public Vector3 Colour, OuterColour;
            public float OuterAngleRad, Falloff, SizeScale, Intensity;
            public float InnerAngleRad, FalloffExponent, LightIntensity, OuterIntensity, OuterExponent;
            public byte Type;
            public float ExtentX;
            public bool HasOuter;
        }

        public int BuildGpuLights(GpuLight[] outLights, int previewHour, bool animateFlashiness, float time,
            List<(Vector3 pos, Vector3 colour, float size, float intensity)> coronasOut, Vector3 cameraPos,
            List<VolumeDraw> volumesOut = null, List<LightAttributes> sourcesOut = null,
            bool cullLights = false, BoundingFrustum frustum = default)
        {
            sourcesOut?.Clear();

            LoadedFile only = null;
            if (LightsOnlyFromSelectedProp)
            {
                only = ActiveFile ?? OwnerFile(SelectedLight);
            }

            var active = new List<(LightAttributes light, LightInstance inst, float flash, bool ghost)>();
            for (int i = 0; i < Lights.Count; i++)
            {
                var al = Lights[i];
                var owner = OwnerFile(al);
                if (only != null && owner != only) continue;
                if (owner != null && !owner.Visible) continue;
                if (!LightDefs.IsActiveAtHour(al.TimeFlags, previewHour)) continue;

                int copies = 1 + (owner?.ExtraPlacements.Count ?? 0);
                for (int c = 0; c < copies; c++)
                {
                    var placement = c == 0
                        ? ((owner != null && owner.HasPlacement) ? owner.Placement : (Matrix?)null)
                        : owner.ExtraPlacements[c - 1];

                    float f = animateFlashiness ? LightDefs.GetFlashMultiplier(al.Flashiness, time, i + c * 977) : 1.0f;
                    if (f <= 0.0001f) continue;
                    var ai = GetInstanceAt(al, placement);
                    if (cullLights)
                    {
                        float infl = al.Falloff + ((byte)al.Type == 4 ? al.Extent.X * 0.5f : 0f);
                        if (frustum.Contains(new BoundingSphere(ai.WorldPosition, Math.Max(infl, 0.05f))) == ContainmentType.Disjoint)
                            continue;
                    }
                    active.Add((al, ai, f, c > 0));
                }
            }
            if (active.Count > GpuLight.MaxLights)
            {
                active.Sort((a, b) =>
                {
                    if (a.ghost != b.ghost) return a.ghost ? 1 : -1;
                    return Vector3.DistanceSquared(cameraPos, a.inst.WorldPosition)
                        .CompareTo(Vector3.DistanceSquared(cameraPos, b.inst.WorldPosition));
                });
            }

            GhostGpuIndices.Clear();
            int count = 0;
            int dropped = 0;
            for (int k = 0; k < active.Count; k++)
            {
                var (l, inst, flash, ghost) = active[k];
                var colour = new Vector3(l.ColorR, l.ColorG, l.ColorB) * (2.0f * l.Intensity / 255.0f) * flash;

                if (volumesOut != null && (l.Flags & LightDefs.FlagDrawVolume) != 0 && l.VolumeIntensity > 0.001f)
                {
                    volumesOut.Add(new VolumeDraw
                    {
                        Pos = inst.WorldPosition,
                        Dir = inst.WorldDirection,
                        Colour = new Vector3(l.ColorR, l.ColorG, l.ColorB) / 255.0f * flash,
                        OuterColour = new Vector3(l.VolumeOuterColorR, l.VolumeOuterColorG, l.VolumeOuterColorB) / 255.0f,
                        OuterAngleRad = Math.Max(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f,
                        Falloff = l.Falloff,
                        SizeScale = Math.Clamp(l.VolumeSizeScale, 0.05f, 10.0f),
                        Intensity = l.VolumeIntensity,
                        Type = (byte)l.Type,
                        ExtentX = l.Extent.X,
                        HasOuter = (l.Flags & LightDefs.FlagVolumeOuterColour) != 0,
                        InnerAngleRad = Math.Min(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f,
                        FalloffExponent = l.FalloffExponent,
                        LightIntensity = l.Intensity,
                        OuterIntensity = l.VolumeOuterIntensity,
                        OuterExponent = l.VolumeOuterExponent,
                    });
                }

                if (coronasOut != null && l.CoronaSize > 0.001f && l.CoronaIntensity > 0.001f)
                {
                    var cpos = inst.WorldPosition;
                    var toCam = cameraPos - cpos;
                    var tlen = toCam.Length();
                    if (tlen > 0.001f)
                    {
                        cpos += toCam * (Math.Min(l.CoronaZBias, tlen * 0.5f) / tlen);
                    }
                    var ccol = new Vector3(l.ColorR, l.ColorG, l.ColorB) / 255.0f;
                    float cview = LightDefs.CoronaView_U25(l, inst.WorldPosition, inst.WorldDirection, cameraPos);
                    if (cview > 0.001f) coronasOut.Add((cpos, ccol, l.CoronaSize * 0.1f, l.CoronaIntensity * flash * cview));
                }

                if ((l.Flags & LightDefs.FlagCoronaOnly) != 0) continue;
                if (count >= GpuLight.MaxLights) { dropped++; continue; }

                var tangentY = Vector3.Cross(inst.WorldDirection, inst.WorldTangent);
                if (tangentY.LengthSquared() > 1e-9f) tangentY.Normalize();

                var inner = Math.Min(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f;
                var outer = Math.Max(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f;

                sourcesOut?.Add(l);
                if (ghost) GhostGpuIndices.Add(count);
                outLights[count++] = new GpuLight
                {
                    ProjTexIndex = -1.0f,
                    ShadowSlot = -1.0f,
                    ShadowBlur = l.ShadowBlur / 255.0f,
                    Position = inst.WorldPosition,
                    Intensity = l.Intensity,
                    Colour = colour,
                    Falloff = l.Falloff,
                    Direction = inst.WorldDirection,
                    FalloffExponent = l.FalloffExponent,
                    TangentX = inst.WorldTangent,
                    ConeInnerAngle = inner,
                    TangentY = tangentY,
                    ConeOuterAngle = outer,
                    CapsuleExtent = l.Extent,
                    Type = (uint)l.Type,
                    CullingPlaneNormal = l.CullingPlaneNormal,
                    CullingPlaneOffset = l.CullingPlaneOffset,
                    CullingPlaneEnable = (l.Flags & LightDefs.FlagCullingPlane) != 0 ? 1u : 0u,
                    Flags = l.Flags,
                };
            }
            LastEmittedLights = count;
            LastDroppedLights = dropped;
            return count;
        }

        public void PushUndo()
        {
            if (ExternalEditing) return;
            undoStack.Add(Snapshot());
            if (undoStack.Count > MaxUndo) undoStack.RemoveAt(0);
            redoStack.Clear();
        }

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        public void Undo()
        {
            if (undoStack.Count == 0) return;
            redoStack.Add(Snapshot());
            Restore(undoStack[undoStack.Count - 1]);
            undoStack.RemoveAt(undoStack.Count - 1);
            Dirty = true;
        }

        public void Redo()
        {
            if (redoStack.Count == 0) return;
            undoStack.Add(Snapshot());
            Restore(redoStack[redoStack.Count - 1]);
            redoStack.RemoveAt(redoStack.Count - 1);
            Dirty = true;
        }

        private UndoState Snapshot()
        {
            return new UndoState
            {
                Lights = Lights.Select(CloneLight).ToList(),
                Selected = new List<int>(SelectedIndices),
                Owners = Lights.Select(l => Files.IndexOf(OwnerFile(l))).ToList(),
                InstanceGroups = Lights.Select(InstanceGroup).ToList(),
            };
        }

        private void Restore(UndoState s)
        {
            Lights.Clear();
            ownerOf.Clear();
            instanceGroupOf.Clear();
            for (int i = 0; i < s.Lights.Count; i++)
            {
                var l = CloneLight(s.Lights[i]);
                Lights.Add(l);
                int oi = (s.Owners != null && i < s.Owners.Count) ? s.Owners[i] : 0;
                if (oi >= 0 && oi < Files.Count) ownerOf[l] = Files[oi];
                int gi = (s.InstanceGroups != null && i < s.InstanceGroups.Count) ? s.InstanceGroups[i] : 0;
                if (gi > 0) instanceGroupOf[l] = gi;
            }
            SelectedIndices.Clear();
            foreach (var i in s.Selected)
            {
                if (i >= 0 && i < Lights.Count) SelectedIndices.Add(i);
            }
        }

        public static LightAttributes CloneLight(LightAttributes src)
        {
            return new LightAttributes
            {
                Unknown_0h = src.Unknown_0h,
                Unknown_4h = src.Unknown_4h,
                Position = src.Position,
                Unknown_14h = src.Unknown_14h,
                ColorR = src.ColorR,
                ColorG = src.ColorG,
                ColorB = src.ColorB,
                Flashiness = src.Flashiness,
                Intensity = src.Intensity,
                Flags = src.Flags,
                BoneId = src.BoneId,
                Type = src.Type,
                GroupId = src.GroupId,
                TimeFlags = src.TimeFlags,
                Falloff = src.Falloff,
                FalloffExponent = src.FalloffExponent,
                CullingPlaneNormal = src.CullingPlaneNormal,
                CullingPlaneOffset = src.CullingPlaneOffset,
                ShadowBlur = src.ShadowBlur,
                Unknown_45h = src.Unknown_45h,
                Unknown_46h = src.Unknown_46h,
                Unknown_48h = src.Unknown_48h,
                VolumeIntensity = src.VolumeIntensity,
                VolumeSizeScale = src.VolumeSizeScale,
                VolumeOuterColorR = src.VolumeOuterColorR,
                VolumeOuterColorG = src.VolumeOuterColorG,
                VolumeOuterColorB = src.VolumeOuterColorB,
                LightHash = src.LightHash,
                VolumeOuterIntensity = src.VolumeOuterIntensity,
                CoronaSize = src.CoronaSize,
                VolumeOuterExponent = src.VolumeOuterExponent,
                LightFadeDistance = src.LightFadeDistance,
                ShadowFadeDistance = src.ShadowFadeDistance,
                SpecularFadeDistance = src.SpecularFadeDistance,
                VolumetricFadeDistance = src.VolumetricFadeDistance,
                ShadowNearClip = src.ShadowNearClip,
                CoronaIntensity = src.CoronaIntensity,
                CoronaZBias = src.CoronaZBias,
                Direction = src.Direction,
                Tangent = src.Tangent,
                ConeInnerAngle = src.ConeInnerAngle,
                ConeOuterAngle = src.ConeOuterAngle,
                Extent = src.Extent,
                ProjectedTextureHash = src.ProjectedTextureHash,
                Unknown_A4h = src.Unknown_A4h,
            };
        }

        private static void CopyParamsOnly(LightAttributes from, LightAttributes to)
        {
            var pos = to.Position; var dir = to.Direction; var tan = to.Tangent; var bone = to.BoneId;
            var c = CloneLight(from);
            to.ColorR = c.ColorR; to.ColorG = c.ColorG; to.ColorB = c.ColorB;
            to.Flashiness = c.Flashiness; to.Intensity = c.Intensity; to.Flags = c.Flags;
            to.Type = c.Type; to.GroupId = c.GroupId; to.TimeFlags = c.TimeFlags;
            to.Falloff = c.Falloff; to.FalloffExponent = c.FalloffExponent;
            to.CullingPlaneNormal = c.CullingPlaneNormal; to.CullingPlaneOffset = c.CullingPlaneOffset;
            to.ShadowBlur = c.ShadowBlur; to.VolumeIntensity = c.VolumeIntensity;
            to.VolumeSizeScale = c.VolumeSizeScale;
            to.VolumeOuterColorR = c.VolumeOuterColorR; to.VolumeOuterColorG = c.VolumeOuterColorG;
            to.VolumeOuterColorB = c.VolumeOuterColorB; to.LightHash = c.LightHash;
            to.VolumeOuterIntensity = c.VolumeOuterIntensity; to.CoronaSize = c.CoronaSize;
            to.VolumeOuterExponent = c.VolumeOuterExponent; to.LightFadeDistance = c.LightFadeDistance;
            to.ShadowFadeDistance = c.ShadowFadeDistance; to.SpecularFadeDistance = c.SpecularFadeDistance;
            to.VolumetricFadeDistance = c.VolumetricFadeDistance; to.ShadowNearClip = c.ShadowNearClip;
            to.CoronaIntensity = c.CoronaIntensity; to.CoronaZBias = c.CoronaZBias;
            to.ConeInnerAngle = c.ConeInnerAngle; to.ConeOuterAngle = c.ConeOuterAngle;
            to.Extent = c.Extent; to.ProjectedTextureHash = c.ProjectedTextureHash;
            to.Position = pos; to.Direction = dir; to.Tangent = tan; to.BoneId = bone;
        }

        public int ApplyEditToSelection(LightAttributes before, LightAttributes primary)
        {
            if (SelectedIndices.Count < 2) return 0;
            int changed = 0;
            foreach (var i in SelectedIndices)
            {
                if (i < 0 || i >= Lights.Count) continue;
                var t = Lights[i];
                if (ReferenceEquals(t, primary)) continue;

                if (before.ColorR != primary.ColorR) t.ColorR = primary.ColorR;
                if (before.ColorG != primary.ColorG) t.ColorG = primary.ColorG;
                if (before.ColorB != primary.ColorB) t.ColorB = primary.ColorB;
                if (before.Flashiness != primary.Flashiness) t.Flashiness = primary.Flashiness;
                if (before.Intensity != primary.Intensity) t.Intensity = primary.Intensity;
                if (before.Flags != primary.Flags) t.Flags = primary.Flags;
                if (before.Type != primary.Type) t.Type = primary.Type;
                if (before.GroupId != primary.GroupId) t.GroupId = primary.GroupId;
                if (before.TimeFlags != primary.TimeFlags) t.TimeFlags = primary.TimeFlags;
                if (before.Falloff != primary.Falloff) t.Falloff = primary.Falloff;
                if (before.FalloffExponent != primary.FalloffExponent) t.FalloffExponent = primary.FalloffExponent;
                if (before.CullingPlaneNormal != primary.CullingPlaneNormal) t.CullingPlaneNormal = primary.CullingPlaneNormal;
                if (before.CullingPlaneOffset != primary.CullingPlaneOffset) t.CullingPlaneOffset = primary.CullingPlaneOffset;
                if (before.ShadowBlur != primary.ShadowBlur) t.ShadowBlur = primary.ShadowBlur;
                if (before.VolumeIntensity != primary.VolumeIntensity) t.VolumeIntensity = primary.VolumeIntensity;
                if (before.VolumeSizeScale != primary.VolumeSizeScale) t.VolumeSizeScale = primary.VolumeSizeScale;
                if (before.VolumeOuterColorR != primary.VolumeOuterColorR) t.VolumeOuterColorR = primary.VolumeOuterColorR;
                if (before.VolumeOuterColorG != primary.VolumeOuterColorG) t.VolumeOuterColorG = primary.VolumeOuterColorG;
                if (before.VolumeOuterColorB != primary.VolumeOuterColorB) t.VolumeOuterColorB = primary.VolumeOuterColorB;
                if (before.VolumeOuterIntensity != primary.VolumeOuterIntensity) t.VolumeOuterIntensity = primary.VolumeOuterIntensity;
                if (before.VolumeOuterExponent != primary.VolumeOuterExponent) t.VolumeOuterExponent = primary.VolumeOuterExponent;
                if (before.CoronaSize != primary.CoronaSize) t.CoronaSize = primary.CoronaSize;
                if (before.CoronaIntensity != primary.CoronaIntensity) t.CoronaIntensity = primary.CoronaIntensity;
                if (before.CoronaZBias != primary.CoronaZBias) t.CoronaZBias = primary.CoronaZBias;
                if (before.LightFadeDistance != primary.LightFadeDistance) t.LightFadeDistance = primary.LightFadeDistance;
                if (before.ShadowFadeDistance != primary.ShadowFadeDistance) t.ShadowFadeDistance = primary.ShadowFadeDistance;
                if (before.SpecularFadeDistance != primary.SpecularFadeDistance) t.SpecularFadeDistance = primary.SpecularFadeDistance;
                if (before.VolumetricFadeDistance != primary.VolumetricFadeDistance) t.VolumetricFadeDistance = primary.VolumetricFadeDistance;
                if (before.ShadowNearClip != primary.ShadowNearClip) t.ShadowNearClip = primary.ShadowNearClip;
                if (before.ConeInnerAngle != primary.ConeInnerAngle) t.ConeInnerAngle = primary.ConeInnerAngle;
                if (before.ConeOuterAngle != primary.ConeOuterAngle) t.ConeOuterAngle = primary.ConeOuterAngle;
                if (before.Extent != primary.Extent) t.Extent = primary.Extent;
                if (before.ProjectedTextureHash.Hash != primary.ProjectedTextureHash.Hash)
                    t.ProjectedTextureHash = primary.ProjectedTextureHash;

                var f = OwnerFile(t);
                if (f != null) f.Dirty = true;
                PropagateInstances(t);
                changed++;
            }
            return changed;
        }

        public static bool LightParamsEqual(LightAttributes a, LightAttributes b)
        {
            return a.ColorR == b.ColorR && a.ColorG == b.ColorG && a.ColorB == b.ColorB &&
                   a.Flashiness == b.Flashiness && a.Intensity == b.Intensity && a.Flags == b.Flags &&
                   a.Type == b.Type && a.GroupId == b.GroupId && a.TimeFlags == b.TimeFlags &&
                   a.Falloff == b.Falloff && a.FalloffExponent == b.FalloffExponent &&
                   a.CullingPlaneNormal == b.CullingPlaneNormal && a.CullingPlaneOffset == b.CullingPlaneOffset &&
                   a.ShadowBlur == b.ShadowBlur && a.VolumeIntensity == b.VolumeIntensity &&
                   a.VolumeSizeScale == b.VolumeSizeScale &&
                   a.VolumeOuterColorR == b.VolumeOuterColorR && a.VolumeOuterColorG == b.VolumeOuterColorG &&
                   a.VolumeOuterColorB == b.VolumeOuterColorB && a.LightHash == b.LightHash &&
                   a.VolumeOuterIntensity == b.VolumeOuterIntensity && a.CoronaSize == b.CoronaSize &&
                   a.VolumeOuterExponent == b.VolumeOuterExponent && a.LightFadeDistance == b.LightFadeDistance &&
                   a.ShadowFadeDistance == b.ShadowFadeDistance && a.SpecularFadeDistance == b.SpecularFadeDistance &&
                   a.VolumetricFadeDistance == b.VolumetricFadeDistance && a.ShadowNearClip == b.ShadowNearClip &&
                   a.CoronaIntensity == b.CoronaIntensity && a.CoronaZBias == b.CoronaZBias &&
                   a.ConeInnerAngle == b.ConeInnerAngle && a.ConeOuterAngle == b.ConeOuterAngle &&
                   a.Extent == b.Extent && a.ProjectedTextureHash.Hash == b.ProjectedTextureHash.Hash;
        }

        public void MakeSelectedUnique()
        {
            foreach (var i in SelectedIndices)
            {
                if (i >= 0 && i < Lights.Count) instanceGroupOf.Remove(Lights[i]);
            }
        }

        public void PropagateInstances(LightAttributes edited)
        {
            int g = InstanceGroup(edited);
            if (g == 0) return;
            foreach (var l in Lights)
            {
                if (l != edited && InstanceGroup(l) == g)
                {
                    CopyParamsOnly(edited, l);
                }
            }
        }

        public const uint AllHoursTimeFlags = 0x00FFFFFF;

        public LightAttributes AddLight(byte type)
        {
            if (Files.Count == 0) return null;
            PushUndo();

            var target = (ActiveFile != null && Files.Contains(ActiveFile))
                ? ActiveFile : (OwnerFile(SelectedLight) ?? Files[0]);
            var spawn = NextLightSpawnPos;
            if (target.HasPlacement)
            {
                spawn = Vector3.TransformCoordinate(spawn, Matrix.Invert(target.Placement));
            }

            var l = new LightAttributes
            {
                Position = spawn,
                ColorR = 255, ColorG = 255, ColorB = 255,
                Intensity = 5.0f,
                Falloff = 8.0f,
                FalloffExponent = 32.0f,
                Type = (LightType)type,
                Direction = new Vector3(0, 0, -1),
                Tangent = new Vector3(-1, 0, 0),
                ConeInnerAngle = type == 2 ? 10.0f : 0.0f,
                ConeOuterAngle = type == 2 ? 35.0f : 0.0f,
                Extent = new Vector3(1, 1, 1),
                CoronaSize = 0.0f,
                CoronaIntensity = 1.0f,
                CoronaZBias = 0.1f,
                ShadowNearClip = 0.05f,
                VolumeIntensity = 1.0f,
                VolumeSizeScale = 1.0f,
                VolumeOuterColorR = 255, VolumeOuterColorG = 255, VolumeOuterColorB = 255,
                VolumeOuterIntensity = 1.0f,
                VolumeOuterExponent = 1.0f,
                TimeFlags = AllHoursTimeFlags,
            };
            Lights.Add(l);
            ownerOf[l] = target;
            SelectedIndex = Lights.Count - 1;
            OwnerFile(l).Dirty = true;
            return l;
        }

        public void DuplicateSelected(bool asInstance = false)
        {
            if (SelectedIndices.Count == 0) return;
            PushUndo();
            var sorted = SelectedIndices.Where(i => i >= 0 && i < Lights.Count).OrderBy(i => i).ToList();
            var newSel = new List<int>();
            foreach (var i in sorted)
            {
                var src = Lights[i];
                var copy = CloneLight(src);
                Lights.Add(copy);
                ownerOf[copy] = OwnerFile(src);
                var owner = OwnerFile(src);
            if (owner != null) owner.Dirty = true;
                if (asInstance)
                {
                    int g = InstanceGroup(src);
                    if (g == 0)
                    {
                        g = nextInstanceGroup++;
                        instanceGroupOf[src] = g;
                    }
                    instanceGroupOf[copy] = g;
                }
                newSel.Add(Lights.Count - 1);
            }
            SelectedIndices.Clear();
            SelectedIndices.AddRange(newSel);
        }

        public void DeleteSelected()
        {
            if (SelectedIndices.Count == 0) return;
            PushUndo();
            foreach (var i in SelectedIndices.Where(i => i >= 0 && i < Lights.Count).OrderByDescending(i => i))
            {
                var l = Lights[i];
                var f = OwnerFile(l);
                if (f != null) f.Dirty = true;
                instanceGroupOf.Remove(l);
                ownerOf.Remove(l);
                Lights.RemoveAt(i);
            }
            SelectedIndices.Clear();
        }

        public LightAttributes BeginCloneDrag(LightAttributes src = null, bool asInstance = false)
        {
            src = src ?? SelectedLight;
            if (src == null) return null;
            PushUndo();
            var copy = CloneLight(src);
            Lights.Add(copy);
            ownerOf[copy] = OwnerFile(src);
            var owner = OwnerFile(src);
            if (owner != null) owner.Dirty = true;
            if (asInstance) LinkAsInstance(src, copy);
            SelectedIndex = Lights.Count - 1;
            return copy;
        }

        public List<LightAttributes> BeginCloneDragGroup(bool asInstance = false)
        {
            if (SelectedIndices.Count == 0) return null;
            PushUndo();
            var clones = new List<LightAttributes>();
            foreach (var i in SelectedIndices.Where(i => i >= 0 && i < Lights.Count).OrderBy(i => i).ToList())
            {
                var src = Lights[i];
                var c = CloneLight(src);
                Lights.Add(c);
                ownerOf[c] = OwnerFile(src);
                var owner = OwnerFile(src);
            if (owner != null) owner.Dirty = true;
                if (asInstance) LinkAsInstance(src, c);
                clones.Add(c);
            }
            SelectedIndices.Clear();
            for (int k = 0; k < clones.Count; k++)
            {
                SelectedIndices.Add(Lights.Count - clones.Count + k);
            }
            return clones;
        }

        private void LinkAsInstance(LightAttributes src, LightAttributes clone)
        {
            int g = InstanceGroup(src);
            if (g == 0)
            {
                g = nextInstanceGroup++;
                instanceGroupOf[src] = g;
            }
            instanceGroupOf[clone] = g;
        }

        private LightAttributes clipboard;
        public bool HasClipboard => clipboard != null;

        private readonly List<LightAttributes> clipboardLights = new List<LightAttributes>();
        public int ClipboardCount => clipboardLights.Count;

        public void CopySettings()
        {
            var l = SelectedLight;
            if (l != null) clipboard = CloneLight(l);
        }

        public void CopyLights()
        {
            clipboardLights.Clear();
            foreach (var i in SelectedIndices.OrderBy(i => i))
            {
                if (i >= 0 && i < Lights.Count) clipboardLights.Add(CloneLight(Lights[i]));
            }
            if (clipboardLights.Count > 0) clipboard = CloneLight(clipboardLights[0]);
        }

        public int PasteLights()
        {
            if (clipboardLights.Count == 0) return 0;
            var target = (ActiveFile != null && Files.Contains(ActiveFile))
                ? ActiveFile : (OwnerFile(SelectedLight) ?? (Files.Count > 0 ? Files[0] : null));
            if (target == null) return 0;

            PushUndo();
            SelectedIndices.Clear();
            foreach (var src in clipboardLights)
            {
                var copy = CloneLight(src);
                Lights.Add(copy);
                ownerOf[copy] = target;
                SelectedIndices.Add(Lights.Count - 1);
            }
            target.Dirty = true;
            return clipboardLights.Count;
        }

        public void PasteSettings()
        {
            if (clipboard == null || SelectedIndices.Count == 0) return;
            PushUndo();
            foreach (var i in SelectedIndices)
            {
                if (i < 0 || i >= Lights.Count) continue;
                CopyParamsOnly(clipboard, Lights[i]);
                var f = OwnerFile(Lights[i]);
                if (f != null) f.Dirty = true;
            }
        }

        public void Save(string path = null)
        {
            if (Files.Count == 0) throw new Exception("No file loaded");

            if (path != null && Files.Count == 1)
            {
                SaveFile(Files[0], path);
                Files[0].Path = path;
                return;
            }

            foreach (var f in Files)
            {
                if (f.ReadOnly) continue;
                if (f.Dirty || path == null)
                {
                    SaveFile(f, f.Path);
                }
            }
        }

        public void SaveOne(LoadedFile f)
        {
            if (f == null || !Files.Contains(f)) return;
            SaveFile(f, f.Path);
        }

        public void SaveOneAs(LoadedFile f, string path)
        {
            if (f == null || !Files.Contains(f) || string.IsNullOrEmpty(path)) return;
            if (HasWritableResource(f)) f.ReadOnly = false;
            SaveFile(f, path);
            f.Path = path;
        }

        public static bool IsUnsaved(LoadedFile f) =>
            f != null && (string.IsNullOrEmpty(f.Path) || !File.Exists(f.Path));

        public static bool HasWritableResource(LoadedFile f)
        {
            if (f == null) return false;
            if (f.IsYft ? f.Yft?.Fragment != null : f.Ydr?.Drawable != null) return true;
            return f.Drawable is Drawable;
        }

        private static YdrFile ResourceToWrite(LoadedFile f)
        {
            if (f.IsYft && f.Yft?.Fragment != null) return null;
            if (f.Ydr?.Drawable != null) return f.Ydr;
            if (f.Drawable is Drawable d)
            {
                var ydr = new YdrFile { Drawable = d };
                f.Ydr = ydr;
                f.IsYft = false;
                return ydr;
            }
            return null;
        }

        private void SaveFile(LoadedFile f, string path)
        {
            if (f.ReadOnly && !HasWritableResource(f))
            {
                throw new Exception($"{f.Name} came from the game files and has nothing that can " +
                    "be written to a loose .ydr.");
            }
            if (!HasWritableResource(f))
            {
                throw new Exception($"{f.Name} has no drawable that can be written to a .ydr - it " +
                    "is a fragment piece resolved out of the game archives. Open the original " +
                    ".yft to edit and save it.");
            }
            ResourceToWrite(f);
            SyncLightsToResource(f);
            byte[] data = f.IsYft ? f.Yft.Save() : f.Ydr.Save();
            if (KeepBackups &&
                File.Exists(path) && string.Equals(path, f.Path, StringComparison.OrdinalIgnoreCase))
            {
                var bak = path + ".bak";
                if (!File.Exists(bak)) File.Copy(path, bak);
            }
            File.WriteAllBytes(path, data);
            f.Dirty = false;
        }

        public void SyncLightsToResource()
        {
            foreach (var f in Files) SyncLightsToResource(f);
        }

        private void SyncLightsToResource(LoadedFile f)
        {
            if (!HasWritableResource(f)) return;
            ResourceToWrite(f);

            var arr = Lights.Where(l => OwnerFile(l) == f).ToArray();
            if (f.IsYft)
            {
                if (f.Yft.Fragment.LightAttributes == null)
                    f.Yft.Fragment.LightAttributes = new ResourceSimpleList64<LightAttributes>();
                f.Yft.Fragment.LightAttributes.data_items = arr;
            }
            else
            {
                if (f.Ydr.Drawable.LightAttributes == null)
                    f.Ydr.Drawable.LightAttributes = new ResourceSimpleList64<LightAttributes>();
                f.Ydr.Drawable.LightAttributes.data_items = arr;
            }
        }

        public void Dispose()
        {
            foreach (var f in Files) f.Model?.Dispose();
            Files.Clear();
            MloModel?.Dispose();
            MloModel = null;
        }
    }
}

