using System;
using System.Collections.Generic;
using CodeWalker.GameFiles;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using GameTexture = CodeWalker.GameFiles.Texture;

namespace RageLightEditor.Rendering
{
    public enum GeomAlphaMode : uint
    {
        Opaque = 0,
        Cutout = 1,
        Decal = 2,
        Additive = 3,
        Glass = 4,
        Water = 5,
    }

    public partial class RenderMesh : IDisposable
    {
        public Buffer VB;
        public Buffer IB;
        public int IndexCount;
        public Buffer FinVB;
        public Buffer FinIB;
        public int FinIndexCount;
        public Matrix Transform = Matrix.Identity;
        public int BoneIndex_V16 = -1;
        public Matrix BaseTransform_V16 = Matrix.Identity;
        public ShaderResourceView DiffuseSRV;
        public string DiffuseName;
        public string TintPaletteName;
        public string TintPaletteRows;
        public ShaderResourceView BumpSRV;
        public ShaderResourceView SpecSRV;

        public readonly ShaderResourceView[] LayerSRV = new ShaderResourceView[4];
        public readonly ShaderResourceView[] LayerBumpSRV = new ShaderResourceView[4];
        public ShaderResourceView MaskSRV;
        public uint TerrainBlendMode;
        public bool TerrainLayersUseUv1;
        public ShaderResourceView TintPaletteSRV;
        public int TintPaletteHeight;
        public uint TintMode;
        public uint TintPaletteIndex;
        public float TintPaletteV => TintPaletteHeight > 0
            ? (Math.Min(TintPaletteIndex, (uint)TintPaletteHeight - 1) + 0.5f) / TintPaletteHeight : 0.0f;
        public bool IsTerrain;
        public bool IsMirror;
        public Vector3 MirrorLocalNormal, MirrorLocalPoint;
        public bool MirrorPlaneWorld(out Plane plane)
        {
            plane = default;
            if (!IsMirror || MirrorLocalNormal.LengthSquared() < 1e-6f) return false;
            var p = Vector3.TransformCoordinate(MirrorLocalPoint, Transform);
            var n = Vector3.TransformNormal(MirrorLocalNormal, Transform);
            if (n.LengthSquared() < 1e-12f) return false;
            n.Normalize();
            plane = new Plane(n, -Vector3.Dot(n, p));
            return true;
        }
        public bool DiffuseSrgbView;
        public bool LayersSrgbView;
        public ShaderResourceView DetailSRV;
        public Vector4 DetailSettings = new Vector4(0, 0, 24, 24);
        public Vector4 AnimUV0 = new Vector4(1, 0, 0, 0);
        public Vector4 AnimUV1 = new Vector4(0, 1, 0, 0);
        public float Bumpiness = 1.0f;
        public float SpecIntensity = 1.0f;
        public Vector3 SpecMapIntMask = new Vector3(1.0f, 0.0f, 0.0f);
        public float SpecFalloffMult = 32.0f;
        public float SpecFresnel = 0.96f;
        public float EmissiveMult = 0.0f;
        public Vector4 MatDiffuse = new Vector4(0.6f, 0.6f, 0.6f, 1.0f);
        public GeomAlphaMode AlphaMode = GeomAlphaMode.Opaque;
        public float FadeAlpha = 1.0f;
        public bool DoubleSided;
        public bool SheetSided;
        public string ShaderName = "";
        public uint DecalKind;
        public Vector4 DecalMask;
        public bool Visible = true;
        public bool NeverDraw;
        public bool IsMloShell;

        public float NaturalAmbientScale = 1.0f;
        public float ArtificialAmbientScale = 1.0f;
        public float InInterior;
        public float ReflectIntAmb = 2.0f;
        public Vector4 ArtIntAmbUp, ArtIntAmbDown;
        public float SunScale = 1.0f;

        public ShaderFX Shader;
        public DrawableGeometry Geometry;
        public TextureDictionary EmbeddedDict;
        public int ShaderIndex = -1;
        public uint TxdContext;
        public uint Highlight;

        public Vector3[] PickVerts;
        public ushort[] PickIndices;

        public bool RayHit(ref Ray worldRay, out float dist)
        {
            dist = float.MaxValue;
            if (PickVerts == null || PickIndices == null || PickIndices.Length < 3) return false;

            var inv = Transform;
            inv.Invert();
            var o = Vector3.TransformCoordinate(worldRay.Position, inv);
            var d = Vector3.TransformNormal(worldRay.Direction, inv);
            float dl = d.Length();
            if (dl < 1e-9f) return false;
            d /= dl;
            var local = new Ray(o, d);

            bool hit = false;
            for (int i = 0; i + 2 < PickIndices.Length; i += 3)
            {
                ref var a = ref PickVerts[PickIndices[i]];
                ref var b = ref PickVerts[PickIndices[i + 1]];
                ref var c = ref PickVerts[PickIndices[i + 2]];
                if (!local.Intersects(ref a, ref b, ref c, out float t)) continue;
                t /= dl;
                if (t < dist) { dist = t; hit = true; }
            }
            return hit;
        }
        public BoundingBox WorldBounds;
        public BoundingSphere WorldSphere;
        public BoundingBox LocalBounds;
        public bool OwnsBuffers = true;

        public RenderMesh CreateInstance(Matrix worldTransform)
        {
            var m = (RenderMesh)MemberwiseClone();
            m.OwnsBuffers = false;
            m.Transform = Transform * worldTransform;
            m.SetBoundsFromLocal();
            return m;
        }

        public void SetBoundsFromLocal()
        {
            var min = LocalBounds.Minimum;
            var max = LocalBounds.Maximum;
            var wmin = new Vector3(float.MaxValue);
            var wmax = new Vector3(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(
                    (i & 1) != 0 ? max.X : min.X,
                    (i & 2) != 0 ? max.Y : min.Y,
                    (i & 4) != 0 ? max.Z : min.Z);
                var w = Vector3.TransformCoordinate(c, Transform);
                wmin = Vector3.Min(wmin, w);
                wmax = Vector3.Max(wmax, w);
            }
            WorldBounds = new BoundingBox(wmin, wmax);
            WorldSphere = new BoundingSphere((wmin + wmax) * 0.5f, (wmax - wmin).Length() * 0.5f);
        }

        public void Dispose()
        {
            if (OwnsBuffers)
            {
                VB?.Dispose();
                IB?.Dispose();
                FinVB?.Dispose();
                FinIB?.Dispose();
            }
        }
    }

    public class RenderModel : IDisposable
    {
        public CodeWalker.GameFiles.YdrFile SourceYdr_V22;
        public List<RenderMesh> Meshes = new List<RenderMesh>();
        public BoundingBox Bounds = new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue));
        public string Name = "";

        public void Dispose()
        {
            foreach (var m in Meshes) m.Dispose();
            Meshes.Clear();
        }
    }

    public partial class ModelRenderer : IDisposable
    {
        private static readonly HashSet<uint> EmissiveSps = new HashSet<uint>
        {
            1332909972, 2072061694, 2635608835, 443538781, 2049580179, 1193295596, 1434302180,
            1897917258, 140448747, 1436689415, 179247185, 1314864030, 1478174766, 3733846327,
            3174327089, 3924045432, 837003310, 485710087, 2055615352, 2918136469, 2698880237,
        };
        private const uint BlankTextureHash = 1678728908;

        private readonly Device device;
        private readonly TextureLoader textureLoader;

        public ModelRenderer(Device device, TextureLoader textureLoader)
        {
            this.device = device;
            this.textureLoader = textureLoader;
        }

        public List<TextureDictionary> ExternalTextureDicts { get; } = new List<TextureDictionary>();

        public List<GameTexture> ImportedTextures { get; set; }

        public Func<uint, uint, Texture> TextureFallback;

        public uint TextureContext;

        public Func<uint, uint, uint, Texture> HdTextureFallback;
        public uint AssetNameContext;
        public TextureBase DiffuseOverride;
        public Func<TextureBase, TextureBase> DiffuseRemap_V23;
        public bool HdTextures = true;
        public int HdTextureHits, HdTextureAsks;

        public RenderModel BuildFromYdr(YdrFile ydr, Matrix? world = null)
        {
            var model = new RenderModel { Name = ydr?.Drawable?.Name ?? "drawable" };
            if (ydr?.Drawable != null)
            {
                AddDrawable(model, ydr.Drawable, world ?? Matrix.Identity, null);
            }
            return model;
        }

        public RenderModel BuildFromDrawable(DrawableBase drawable, string name = null, Matrix? world = null)
        {
            var model = new RenderModel { Name = name ?? (drawable as Drawable)?.Name ?? "drawable" };
            if (drawable != null)
            {
                AddDrawable(model, drawable, world ?? Matrix.Identity, null);
                var cloth = (drawable as FragDrawable)?.OwnerFragment?.DrawableCloth;
                if (cloth != null && !ReferenceEquals(cloth, drawable))
                    AddDrawable(model, cloth, world ?? Matrix.Identity, null);
            }
            return model;
        }

        public RenderModel BuildFromYft(YftFile yft, Matrix? world = null)
        {
            var model = new RenderModel { Name = yft?.Fragment?.Drawable?.Name ?? "fragment" };
            var frag = yft?.Fragment;
            if (frag == null) return model;
            var w = world ?? Matrix.Identity;

            if (frag.Drawable != null)
            {
                AddDrawable(model, frag.Drawable, w, frag.BoneTransforms?.Items);
            }
            if (frag.DrawableCloth != null)
            {
                AddDrawable(model, frag.DrawableCloth, w, null);
            }

            var lod = frag.PhysicsLODGroup?.PhysicsLOD1;
            var children = lod?.Children?.data_items;
            if (children != null)
            {
                var xforms = lod.FragTransforms?.Matrices;
                var posOffset = lod.PositionOffset;
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    var cd = child?.Drawable1;
                    if (cd?.AllModels == null || cd.AllModels.Length == 0) continue;
                    if (cd == frag.Drawable) continue;

                    var xform = Matrix.Identity;
                    if (xforms != null && i < xforms.Length)
                    {
                        xform = xforms[i];
                        xform.Row4 = new Vector4(xform.TranslationVector + posOffset, 1.0f);
                    }
                    AddDrawable(model, cd, xform * w, null, frag.Drawable);
                }
            }

            return model;
        }

        private void AddDrawable(RenderModel model, DrawableBase drawable, Matrix baseTransform,
            Matrix3_s[] fragBoneTransforms, DrawableBase shaderSource = null)
        {
            var models = HighestLod(drawable);
            if (models == null) return;

            var skeleton = drawable.Skeleton ?? (shaderSource?.Skeleton);
            var embeddedDict = GetEmbeddedDict(drawable) ?? GetEmbeddedDict(shaderSource);

            foreach (var dmodel in models)
            {
                if (dmodel?.Geometries == null) continue;

                var modelTransform = GetModelTransform(dmodel, skeleton, fragBoneTransforms) * baseTransform;
                int meshBone = (dmodel.HasSkin > 0 || fragBoneTransforms != null) ? -1 : dmodel.BoneIndex;

                foreach (var geom in dmodel.Geometries)
                {
                    var mesh = BuildMesh(geom, modelTransform, embeddedDict);
                    if (mesh != null)
                    {
                        mesh.BoneIndex_V16 = meshBone;
                        mesh.BaseTransform_V16 = baseTransform;
                        SetMeshBounds(mesh, geom, modelTransform);
                        model.Meshes.Add(mesh);
                        model.Bounds = BoundingBox.Merge(model.Bounds, mesh.WorldBounds);
                    }
                }
            }
        }

        public static DrawableModel[] HighestLod(DrawableBase drawable)
        {
            var dm = drawable?.DrawableModels;
            if (dm == null) return drawable?.AllModels;
            if (dm.High != null && dm.High.Length > 0) return dm.High;
            if (dm.Med != null && dm.Med.Length > 0) return dm.Med;
            if (dm.Low != null && dm.Low.Length > 0) return dm.Low;
            if (dm.VLow != null && dm.VLow.Length > 0) return dm.VLow;
            return null;
        }

        private static Matrix GetModelTransform(DrawableModel dmodel, Skeleton skeleton, Matrix3_s[] fragBoneTransforms)
        {
            if (dmodel.HasSkin > 0) return Matrix.Identity;
            int boneidx = dmodel.BoneIndex;

            if (fragBoneTransforms != null)
            {
                if (boneidx < fragBoneTransforms.Length)
                {
                    return FragBoneMatrix(fragBoneTransforms[boneidx]);
                }
                return Matrix.Identity;
            }

            var bones = skeleton?.Bones?.Items;
            if (bones != null && boneidx < bones.Length && boneidx >= 0)
            {
                var b = bones[boneidx];
                if (b != null) return b.AnimTransform;
            }
            return Matrix.Identity;
        }

        private static Matrix FragBoneMatrix(Matrix3_s m)
        {
            var r = new Matrix
            {
                M11 = m.Row1.X, M12 = m.Row2.X, M13 = m.Row3.X, M14 = 0,
                M21 = m.Row1.Y, M22 = m.Row2.Y, M23 = m.Row3.Y, M24 = 0,
                M31 = m.Row1.Z, M32 = m.Row2.Z, M33 = m.Row3.Z, M34 = 0,
                M41 = m.Row1.W, M42 = m.Row2.W, M43 = m.Row3.W, M44 = 1,
            };
            return r;
        }

        private static TextureDictionary GetEmbeddedDict(DrawableBase d)
        {
            return d?.ShaderGroup?.TextureDictionary;
        }

        private RenderMesh BuildMesh(DrawableGeometry geom, Matrix transform, TextureDictionary embeddedDict)
        {
            var vdata = geom.VertexData;
            var indices = geom.IndexBuffer?.Indices;
            if (vdata?.VertexBytes == null || indices == null || indices.Length < 3) return null;

            DecodedGeom_V66 pre = null;
            Precomputed_V66?.TryGetValue(geom, out pre);
            var verts = pre?.Verts ?? VertexDecoder.Decode(vdata, indices, FurMath_U20.IsMaskShader(geom.Shader?.Name.ToString()));
            if (verts == null || verts.Length == 0) return null;

            var mesh = new RenderMesh { Transform = transform };

            mesh.VB = Buffer.Create(device, BindFlags.VertexBuffer, verts);
            mesh.IB = Buffer.Create(device, BindFlags.IndexBuffer, indices);
            mesh.IndexCount = indices.Length;

            Vector3[] pick = pre?.Pick;
            if (pick == null)
            {
                pick = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++) pick[i] = verts[i].Position;
            }
            mesh.PickVerts = pick;
            mesh.PickIndices = indices;

            mesh.Geometry = geom;
            mesh.ShaderIndex = geom.ShaderID;
            mesh.TxdContext = TextureContext;
            ApplyMaterial(mesh, geom.Shader, embeddedDict);

            if (mesh.IsPedFur_V38) BuildPedFurFins_U8(mesh, verts, indices);

            mesh.SheetSided = pre?.Sheet ?? IsOpenSheet(pick, indices, verts);
            if (!mesh.DoubleSided) mesh.DoubleSided = mesh.SheetSided;

            if (mesh.IsMirror) ExtractMirrorPlane(mesh, pick, indices);

            return mesh;
        }

        public static void ExtractMirrorPlane(RenderMesh mesh, Vector3[] verts, ushort[] indices)
        {
            mesh.MirrorLocalNormal = Vector3.Zero;
            mesh.MirrorLocalPoint = Vector3.Zero;
            if (verts == null || indices == null || indices.Length < 3) return;
            var nsum = Vector3.Zero;
            var csum = Vector3.Zero;
            var nabs = Vector3.Zero;
            var nref = Vector3.Zero;
            float refArea = 0;
            double asum = 0;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                if (ia >= verts.Length || ib >= verts.Length || ic >= verts.Length) continue;
                var a = verts[ia]; var b = verts[ib]; var c = verts[ic];
                var n = Vector3.Cross(b - a, c - a);
                float area2 = n.Length();
                if (area2 < 1e-12f) continue;
                if (area2 > refArea) { refArea = area2; nref = n; }
                csum += (a + b + c) * (area2 / 3.0f);
                asum += area2;
            }
            if (asum <= 0 || refArea <= 0) return;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                if (ia >= verts.Length || ib >= verts.Length || ic >= verts.Length) continue;
                var n = Vector3.Cross(verts[ib] - verts[ia], verts[ic] - verts[ia]);
                if (n.LengthSquared() < 1e-24f) continue;
                nsum += n;
                nabs += Vector3.Dot(n, nref) < 0 ? -n : n;
            }
            var pick2 = nsum.LengthSquared() > 0.04f * (float)(asum * asum) ? nsum : nabs;
            if (pick2.LengthSquared() < 1e-12f) return;
            pick2.Normalize();
            mesh.MirrorLocalNormal = pick2;
            mesh.MirrorLocalPoint = csum / (float)asum;
        }

        private static bool IsOpenSheet(Vector3[] verts, ushort[] indices, MeshVertex[] full)
        {
            if (verts == null || indices == null || indices.Length < 6) return true;

            if (full != null && full.Length > 0)
            {
                var n0 = Vector3.Zero;
                foreach (var v in full)
                {
                    if (v.Normal.LengthSquared() > 1e-6f) { n0 = Vector3.Normalize(v.Normal); break; }
                }
                if (n0.LengthSquared() > 1e-6f)
                {
                    const float minDot = 0.9f;
                    foreach (var v in full)
                    {
                        if (v.Normal.LengthSquared() <= 1e-6f) continue;
                        if (Vector3.Dot(Vector3.Normalize(v.Normal), n0) < minDot) return false;
                    }
                }
            }

            const int MaxTris = 12000;
            int triCount = indices.Length / 3;
            int sample = Math.Min(triCount, MaxTris);

            const float Quantum = 1e-4f;
            static ulong Key(Vector3 p)
            {
                unchecked
                {
                    long x = (long)MathF.Round(p.X / Quantum);
                    long y = (long)MathF.Round(p.Y / Quantum);
                    long z = (long)MathF.Round(p.Z / Quantum);
                    ulong h = 1469598103934665603UL;
                    h = (h ^ (ulong)x) * 1099511628211UL;
                    h = (h ^ (ulong)y) * 1099511628211UL;
                    h = (h ^ (ulong)z) * 1099511628211UL;
                    return h;
                }
            }

            var edges = new Dictionary<(ulong, ulong), int>(sample * 3);
            void AddEdge(ulong a, ulong b)
            {
                var k = a < b ? (a, b) : (b, a);
                edges.TryGetValue(k, out int n);
                edges[k] = n + 1;
            }

            for (int t = 0; t < sample; t++)
            {
                int i = t * 3;
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                if (ia >= verts.Length || ib >= verts.Length || ic >= verts.Length) continue;
                ulong ka = Key(verts[ia]), kb = Key(verts[ib]), kc = Key(verts[ic]);
                if (ka == kb || kb == kc || ka == kc) continue;
                AddEdge(ka, kb);
                AddEdge(kb, kc);
                AddEdge(kc, ka);
            }

            if (edges.Count == 0) return true;

            int boundary = 0;
            foreach (var n in edges.Values) if (n == 1) boundary++;

            return boundary * 100 >= edges.Count * 35;
        }

        public void RefreshMaterial(RenderMesh mesh)
        {
            if (mesh?.Shader == null) return;
            mesh.Bumpiness = 1.0f;
            mesh.SpecIntensity = 1.0f;
            mesh.SpecMapIntMask = new Vector3(1.0f, 0.0f, 0.0f);
            mesh.SpecFalloffMult = 32.0f;
            mesh.SpecFresnel = 0.96f;
            mesh.EmissiveMult = 0.0f;
            mesh.MatDiffuse = new Vector4(0.6f, 0.6f, 0.6f, 1.0f);
            mesh.DetailSettings = new Vector4(0, 0, 24, 24);
            mesh.AnimUV0 = new Vector4(1, 0, 0, 0);
            mesh.AnimUV1 = new Vector4(0, 1, 0, 0);
            mesh.DiffuseSRV = null;
            mesh.BumpSRV = null;
            mesh.SpecSRV = null;
            mesh.DetailSRV = null;
            mesh.TintPaletteSRV = null; mesh.TintPaletteHeight = 0; mesh.TintMode = 0;
            mesh.IsMirror = false; mesh.DecalKind = 0;
            ResetFurDefaults_U20(mesh);
            ApplyMaterial(mesh, mesh.Shader, mesh.EmbeddedDict);
        }

        private static readonly HashSet<uint> CutoutSps = new HashSet<uint>
        {
            1530399584,
            3190732435,
            3959636627,
            2219447268,
            3091995132,
            3187789425,
            3339370144,
            1264076685,
            46387092,
            748520668,
            807996366,
            3300978494,
            582493193,
            2322653400,
            3334613197,
            3192134330,
            1224713457,
            1229591973,
            4265705004,
            2245870123,
            4113118754,
            1106229739,
            1800418130,
            476099326,
            170694389,
            38543133,
        };

        private static readonly HashSet<uint> AdditiveSps = new HashSet<uint>
        {
            1478174766,
        };

        private static readonly HashSet<uint> GlassSps = new HashSet<uint>
        {
            3928756789,
            4018753208,
            2800545026,
            1263059426,
            3398951093,
            1520288031,
            3924045432,
            837003310,
            485710087,
            1359281054,
            4237090538,
            430314084,
            2866652360,
            2055615352,
        };

        private static readonly HashSet<uint> NeverDrawnSps = new HashSet<uint>
        {
            1695474112,
            83630553,
            1238547107,
        };

        // CodeWalker's water shader list, both of its passes. The first six are its WaterBatches
        // (the water body itself); the last four are Water2Batches, drawn over the top without
        // depth writes - which is what this renderer's transparent pass already does. Leaving
        // foam and water_decal out of it left river rapids and shorelines to the opaque path,
        // where they read as bare rock.
        private static readonly HashSet<uint> WaterSps = new HashSet<uint>
        {
            1529202445,
            4064804434,
            2871265627,
            1507348828,
            3945561843,
            4234404348,
            1077877097,
            3053856997,
            3066724854,
            1471966282,
        };

        private static readonly HashSet<uint> BlendSps = new HashSet<uint>
        {

            2021887493,
            1086592620,
            1436689415,
            4237650253,
            1564459451,
            763839200,
            179247185,
            1314864030,
            1478174766,
            3733846327,
            3174327089,
            257450439,
            2116642565,
            298255408,
            181295180,
            1896243360,
            3724703640,
            2161953435,
            3203310712,
            3928756789,
            4018753208,
            2800545026,
            1263059426,
            3398951093,
            1520288031,
            3924045432,
            837003310,
            485710087,
            1359281054,
            4237090538,
            430314084,
            2866652360,
            2055615352,
        };

        private static readonly HashSet<uint> DecalSps = new HashSet<uint>
        {
            3140040342,
            1093522222,
            3948167519,
            3880384844,
            341123999,
            2918136469,
            2698880237,
            600733812,
            1145906525,
            1342302630,
            2269053854,
            108580378,
            2189252961,
            992110385,
            941334042,
            2739041469,
            3606424483,
            2842248626,
            3717415672,
            2457676400,
            2655725442,
            2706821972,
            1851110504,
        };

        public static void ClassifyDrawPublic(RenderMesh mesh, ShaderFX shader) => ClassifyDraw(mesh, shader);

        private static void ClassifyDraw(RenderMesh mesh, ShaderFX shader)
        {
            uint sps = shader.FileName.Hash;

            if (NeverDrawnSps.Contains(sps)) { mesh.NeverDraw = true; return; }

            var earlyName = shader.FileName.ToString();
            if (earlyName != null &&
                earlyName.IndexOf("shadow_proxy", StringComparison.OrdinalIgnoreCase) >= 0)
            { mesh.NeverDraw = true; return; }

            if (WaterSps.Contains(sps)) { mesh.AlphaMode = GeomAlphaMode.Water; mesh.DoubleSided = true; mesh.Bumpiness = 0.0f; return; }
            if (sps == 3066724854) { mesh.AlphaMode = GeomAlphaMode.Decal; mesh.DoubleSided = true; mesh.DecalKind = 6; return; }
            if (sps == 3053856997 || sps == 1471966282) { mesh.AlphaMode = GeomAlphaMode.Decal; mesh.DoubleSided = true; mesh.DecalKind = 7; return; }
            if (AdditiveSps.Contains(sps)) { mesh.AlphaMode = GeomAlphaMode.Additive; mesh.DoubleSided = true; return; }
            if (GlassSps.Contains(sps)) { mesh.AlphaMode = GeomAlphaMode.Glass; mesh.DoubleSided = true; return; }
            if (IsGrassFurSps_U20(sps)) { mesh.AlphaMode = GeomAlphaMode.Cutout; mesh.DoubleSided = false; return; }
            if (CutoutSps.Contains(sps)) { mesh.AlphaMode = GeomAlphaMode.Cutout; mesh.DoubleSided = true; return; }
            if (BlendSps.Contains(sps)) { mesh.AlphaMode = GeomAlphaMode.Decal; mesh.DoubleSided = true; return; }
            if (sps == 1658580369u || sps == 129155404u)
            { mesh.AlphaMode = GeomAlphaMode.Opaque; mesh.DoubleSided = false; mesh.IsMirror = true; return; }
            if (sps == 916743331u) { mesh.AlphaMode = GeomAlphaMode.Cutout; mesh.DoubleSided = true; return; }
            if (DecalSps.Contains(sps))
            {
                mesh.AlphaMode = GeomAlphaMode.Decal; mesh.DoubleSided = false;
                switch (sps)
                {
                    case 341123999:
                    case 2457676400:
                        mesh.DecalKind = 3; break;
                    case 2706821972:
                        mesh.DecalKind = 8; mesh.IsMirror = true; break;
                    case 3880384844:
                    case 2842248626:
                        mesh.DecalKind = 4; break;
                    case 2655725442:
                        mesh.DecalKind = 2; break;
                    case 600733812:
                        mesh.DecalKind = 5; break;
                    default:
                        mesh.DecalKind = 1; break;
                }
                return;
            }

            switch (shader.FileName.Hash)
            {
                case 1529202445u:
                case 4064804434u:
                case 2871265627u:
                case 1507348828u:
                case 3945561843u:
                case 4234404348u:
                case 1077877097u:
                case 3053856997u:
                case 3066724854u:
                case 1471966282u:
                    mesh.AlphaMode = GeomAlphaMode.Water;
                    mesh.DoubleSided = true;
                    mesh.Bumpiness = 0.0f;
                    return;
            }

            var name = shader.FileName.ToString();
            if (name != null && !name.StartsWith("hash_", StringComparison.OrdinalIgnoreCase))
            {
                if (name.StartsWith("water", StringComparison.OrdinalIgnoreCase))
                {
                    mesh.AlphaMode = GeomAlphaMode.Water;
                    mesh.DoubleSided = true;
                    mesh.Bumpiness = 0.0f;
                    return;
                }

                if (HasWord(name, "additive")) { mesh.AlphaMode = GeomAlphaMode.Additive; mesh.DoubleSided = true; return; }

                if (HasWord(name, "cutout") || name.StartsWith("trees", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("cloth", StringComparison.OrdinalIgnoreCase))
                { mesh.AlphaMode = GeomAlphaMode.Cutout; mesh.DoubleSided = true; return; }

                if (HasWord(name, "decal")) { mesh.AlphaMode = GeomAlphaMode.Decal; mesh.DoubleSided = false; mesh.DecalKind = 1; return; }

                if (name.StartsWith("glass", StringComparison.OrdinalIgnoreCase))
                { mesh.AlphaMode = GeomAlphaMode.Glass; mesh.DoubleSided = true; return; }

                if (HasWord(name, "alpha") || HasWord(name, "screendooralpha"))
                { mesh.AlphaMode = GeomAlphaMode.Decal; mesh.DoubleSided = true; return; }
            }

            switch (shader.RenderBucket)
            {
                case 3: mesh.AlphaMode = GeomAlphaMode.Cutout; mesh.DoubleSided = true; break;
                case 2: mesh.AlphaMode = GeomAlphaMode.Decal; mesh.DoubleSided = false; mesh.DecalKind = 1; break;
                case 1:
                case 6:
                case 7: mesh.AlphaMode = GeomAlphaMode.Decal; mesh.DoubleSided = true; break;
                default: mesh.AlphaMode = GeomAlphaMode.Opaque; mesh.DoubleSided = false; break;
            }
        }

        private static bool HasWord(string name, string word)
        {
            int i = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            while (i >= 0)
            {
                bool startOk = i == 0 || name[i - 1] == '_';
                int e = i + word.Length;
                bool endOk = e >= name.Length || name[e] == '_' || name[e] == '.';
                if (startOk && endOk) return true;
                i = name.IndexOf(word, i + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private readonly TextureBase[] furCombo_V21 = new TextureBase[4];

        private void ApplyMaterial(RenderMesh mesh, ShaderFX shader, TextureDictionary embeddedDict)
        {
            for (int fi = 0; fi < 4; fi++) furCombo_V21[fi] = null;
            furMask_U20 = null;
            furHf_U20 = null;
            mesh.Shader = shader;
            mesh.EmbeddedDict = embeddedDict;
            if (shader == null) return;

            mesh.ShaderName = shader.Name.ToString();
            ClassifyDraw(mesh, shader);

            if (EmissiveSps.Contains(shader.FileName.Hash))
            {
                mesh.EmissiveMult = 1.0f;
            }

            var plist = shader.ParametersList;
            var prms = plist?.Parameters;
            var hashes = plist?.Hashes;
            if (prms == null || hashes == null) return;

            TextureBase diffuse = null, bump = null, spec = null, detail = null, fallback = null;
            TextureBase mask = null, tintPal = null;
            var layer = new TextureBase[4];
            var layerBump = new TextureBase[4];

            int n = Math.Min(prms.Length, hashes.Length);
            for (int i = 0; i < n; i++)
            {
                uint hash = (uint)hashes[i];
                var data = prms[i].Data;

                if (data is TextureBase tb)
                {
                    if (tb.NameHash == BlankTextureHash) continue;
                    switch ((ShaderParamNames)hash)
                    {
                        case ShaderParamNames.DiffuseSampler:
                        case ShaderParamNames.PlateBgSampler:
                            diffuse = diffuse ?? tb; break;
                        case ShaderParamNames.BumpSampler:
                        case ShaderParamNames.PlateBgBumpSampler:
                            bump = bump ?? tb; break;
                        case ShaderParamNames.SpecSampler:
                            spec = spec ?? tb; break;
                        case ShaderParamNames.DetailSampler:
                            detail = detail ?? tb; break;
                        case ShaderParamNames.FoamSampler:
                            diffuse = diffuse ?? tb; break;

                        case ShaderParamNames.TextureSampler_layer0: layer[0] = layer[0] ?? tb; break;
                        case ShaderParamNames.TextureSampler_layer1: layer[1] = layer[1] ?? tb; break;
                        case ShaderParamNames.TextureSampler_layer2: layer[2] = layer[2] ?? tb; break;
                        case ShaderParamNames.TextureSampler_layer3: layer[3] = layer[3] ?? tb; break;
                        case ShaderParamNames.BumpSampler_layer0: layerBump[0] = layerBump[0] ?? tb; break;
                        case ShaderParamNames.BumpSampler_layer1: layerBump[1] = layerBump[1] ?? tb; break;
                        case ShaderParamNames.BumpSampler_layer2: layerBump[2] = layerBump[2] ?? tb; break;
                        case ShaderParamNames.BumpSampler_layer3: layerBump[3] = layerBump[3] ?? tb; break;
                        case ShaderParamNames.lookupSampler: mask = mask ?? tb; break;
                        case ShaderParamNames.ComboHeightSamplerFur01: furCombo_V21[0] = tb; break;
                        case ShaderParamNames.ComboHeightSamplerFur23: furCombo_V21[1] = tb; break;
                        case ShaderParamNames.ComboHeightSamplerFur45: furCombo_V21[2] = tb; break;
                        case ShaderParamNames.ComboHeightSamplerFur67: furCombo_V21[3] = tb; break;
                        case ShaderParamNames.FurMaskSampler: furMask_U20 = tb; break;
                        case ShaderParamNames.DiffuseHfSampler: furHf_U20 = tb; break;
                        case ShaderParamNames.TintPaletteSampler:
                        case ShaderParamNames.TextureSamplerDiffPal:
                            tintPal = tintPal ?? tb; break;

                        default:
                            fallback = fallback ?? tb; break;
                    }
                }
                else if (data is Vector4 v)
                {
                    switch ((ShaderParamNames)hash)
                    {
                        case ShaderParamNames.bumpiness:
                            // On a water mesh this field is not bump strength: model.hlsl reads it
                            // as the body-depth limit and DISCARDS the surface wherever the water
                            // is deeper than it. ClassifyDraw zeroes it to disable that; letting
                            // the material's own bumpiness (1.0 on most water shaders) land here
                            // deletes every river, lake and pool more than a metre deep.
                            if (mesh.AlphaMode != GeomAlphaMode.Water) mesh.Bumpiness = v.X;
                            break;
                        case ShaderParamNames.specularIntensityMult: mesh.SpecIntensity = v.X; break;
                        case ShaderParamNames.specMapIntMask: mesh.SpecMapIntMask = new Vector3(v.X, v.Y, v.Z); break;
                        case ShaderParamNames.specularFalloffMult:
                        case ShaderParamNames.specularFalloffMultSpecMap:
                            mesh.SpecFalloffMult = v.X; break;
                        case ShaderParamNames.specularFresnel: mesh.SpecFresnel = v.X; break;
                        case ShaderParamNames.matDiffuseColor: mesh.MatDiffuse = new Vector4(v.X, v.Y, v.Z, 1.0f); break;
                        case ShaderParamNames.detailSettings: mesh.DetailSettings = v; break;
                        case ShaderParamNames.globalAnimUV0: mesh.AnimUV0 = v; break;
                        case ShaderParamNames.globalAnimUV1: mesh.AnimUV1 = v; break;
                        case ShaderParamNames.emissiveMultiplier: mesh.EmissiveMult = Math.Max(v.X, 0f); break;
                        case ShaderParamNames.DirtDecalMask: mesh.DecalMask = v; break;
                        default: ReadFurVector_V21(mesh, (ShaderParamNames)hash, v); break;
                    }
                }
            }

            mesh.IsTerrain = layer[0] != null || layer[1] != null || layer[2] != null || layer[3] != null;

            if (!mesh.IsTerrain) diffuse = diffuse ?? fallback;

            if (DiffuseOverride != null && diffuse != null) diffuse = DiffuseOverride;
            if (DiffuseRemap_V23 != null && diffuse != null) diffuse = DiffuseRemap_V23(diffuse) ?? diffuse;
            mesh.DiffuseSRV = ResolveTexture(diffuse, embeddedDict, srgb: true, out var diffuseTex);
            NoteMissing_V21(diffuse, mesh.DiffuseSRV);
            mesh.DiffuseName = diffuse?.Name;
            ClassifyShadowBake_P3(mesh);
            mesh.DiffuseSrgbView = diffuseTex != null && TextureLoader.HasSrgbFormat(diffuseTex.Format);
            mesh.BumpSRV = ResolveTexture(bump, embeddedDict);
            mesh.SpecSRV = ResolveTexture(spec, embeddedDict);
            mesh.DetailSRV = ResolveTexture(detail, embeddedDict);
            NoteMissing_V21(bump, mesh.BumpSRV);
            NoteMissing_V21(spec, mesh.SpecSRV);
            NoteMissing_V21(detail, mesh.DetailSRV);
            ReadFurParams_V21(mesh, shader, furCombo_V21, embeddedDict);
            ReadPedFurParams_V38(mesh, shader, embeddedDict);

            if (mesh.IsTerrain)
            {
                var l0 = ResolveTexture(layer[0], embeddedDict, srgb: true, out var l0tex);
                mesh.LayersSrgbView = l0tex == null || TextureLoader.HasSrgbFormat(l0tex.Format);
                var b0 = ResolveTexture(layerBump[0], embeddedDict) ?? mesh.BumpSRV;
                for (int k = 0; k < 4; k++)
                {
                    mesh.LayerSRV[k] = ResolveTexture(layer[k], embeddedDict, srgb: true, out var lt) ?? l0;
                    if (lt != null && !TextureLoader.HasSrgbFormat(lt.Format)) mesh.LayersSrgbView = false;
                    mesh.LayerBumpSRV[k] = ResolveTexture(layerBump[k], embeddedDict) ?? b0;
                }
                mesh.MaskSRV = ResolveTexture(mask, embeddedDict);
                NoteMissing_V21(mask, mesh.MaskSRV);
                NoteMissing_V21(layer[0], mesh.LayerSRV[0]);

                if (mesh.LayerSRV[0] == null)
                {
                    mesh.IsTerrain = false;
                    if (mesh.DiffuseSRV == null)
                    {
                        mesh.DiffuseSRV = ResolveTexture(fallback, embeddedDict, srgb: true, out var fbTex);
                        mesh.DiffuseSrgbView = fbTex != null && TextureLoader.HasSrgbFormat(fbTex.Format);
                    }
                }
            }

            mesh.TintPaletteSRV = ResolveTexture(tintPal, embeddedDict, srgb: true, out var palTex);
            NoteMissing_V21(tintPal, mesh.TintPaletteSRV);
            mesh.TintPaletteName = tintPal?.Name;
            if (palTex != null && Environment.GetEnvironmentVariable("RLE_MATARCH") != null)
            {
                try
                {
                    var raw = palTex.Data?.FullData;
                    int w = palTex.Width, h = palTex.Height;
                    if (raw != null && palTex.Format == CodeWalker.GameFiles.TextureFormat.D3DFMT_A8R8G8B8 && raw.Length >= w * h * 4)
                    {
                        var sb = new System.Text.StringBuilder();
                        for (int r = 0; r < h; r++)
                        {
                            sb.Append("row").Append(r).Append(':');
                            for (int cx = 0; cx < w; cx += Math.Max(1, w / 8)) { int o = (r * w + cx) * 4; sb.Append($" {raw[o + 2]:X2}{raw[o + 1]:X2}{raw[o]:X2}"); }
                            sb.Append(" |");
                        }
                        mesh.TintPaletteRows = sb.ToString();
                    }
                    else mesh.TintPaletteRows = $"fmt {palTex.Format} {w}x{h}";
                }
                catch { }
            }
            mesh.TintPaletteHeight = mesh.TintPaletteSRV != null ? Math.Max((int)(palTex?.Height ?? 1), 1) : 0;
            if (mesh.TintPaletteSRV == null) mesh.TintMode = 0;
            else
            {
                uint sps = shader.FileName.Hash;
                mesh.TintMode = (sps == 2245870123u || sps == 3334613197u || sps == 1229591973u) ? 2u : 1u;
                if (sps == 231364109u || sps == 3294641629u || sps == 731050667u) mesh.TintMode = 3u;
                if (palTex != null && TextureLoader.HasSrgbFormat(palTex.Format)) mesh.TintMode |= 4u;
            }

            if (mesh.IsTerrain)
            {

                var sn = mesh.ShaderName ?? "";
                if (mesh.MaskSRV == null) mesh.TerrainBlendMode = 0;
                else if (sn.Contains("_2tex_blend")) mesh.TerrainBlendMode = 2;
                else if (sn.Contains("_cm")) mesh.TerrainBlendMode = 1;
                else mesh.TerrainBlendMode = 0;
                var fn = shader.FileName.ToString() ?? "";
                mesh.TerrainLayersUseUv1 = fn.Contains("_2tex") && !fn.Contains("_2tex_blend");

                if (mesh.DiffuseSRV == null) { mesh.DiffuseSRV = mesh.LayerSRV[0]; mesh.DiffuseSrgbView = mesh.LayersSrgbView; }
            }
        }

        private ShaderResourceView ResolveTexture(TextureBase tb, TextureDictionary embeddedDict, bool srgb = false)
            => ResolveTexture(tb, embeddedDict, srgb, out _);

        private ShaderResourceView ResolveTexture(TextureBase tb, TextureDictionary embeddedDict, bool srgb, out GameTexture found)
        {
            found = null;
            if (tb == null) return null;

            if (ImportedTextures != null)
            {
                foreach (var it in ImportedTextures)
                {
                    if (it != null && it.NameHash == tb.NameHash && it.Data?.FullData != null)
                    {
                        found = it;
                        return textureLoader.GetSRV(it, srgb);
                    }
                }
            }

            if (HdTextures && HdTextureFallback != null && tb.NameHash != 0)
            {
                HdTextureAsks++;
                var hd = HdTextureFallback(tb.NameHash, TextureContext, AssetNameContext);
                if (hd?.Data?.FullData != null)
                {
                    HdTextureHits++;
                    found = hd;
                    return textureLoader.GetSRV(hd, srgb);
                }
            }

            if (tb is GameTexture gt && gt.Data?.FullData != null)
            {
                found = gt;
                return textureLoader.GetSRV(gt, srgb);
            }

            var tex = embeddedDict?.Lookup(tb.NameHash);
            if (tex?.Data?.FullData != null)
            {
                found = tex;
                return textureLoader.GetSRV(tex, srgb);
            }

            foreach (var dict in ExternalTextureDicts)
            {
                tex = dict?.Lookup(tb.NameHash);
                if (tex?.Data?.FullData != null)
                {
                    found = tex;
                    return textureLoader.GetSRV(tex, srgb);
                }
            }

            tex = TextureFallback?.Invoke(tb.NameHash, TextureContext);
            if (tex?.Data?.FullData != null)
            {
                found = tex;
                return textureLoader.GetSRV(tex, srgb);
            }

            return null;
        }

        private static void SetMeshBounds(RenderMesh mesh, DrawableGeometry geom, Matrix transform)
        {
            var aabb = geom.AABB;
            mesh.LocalBounds = new BoundingBox(
                new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
                new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Max.Z));
            mesh.SetBoundsFromLocal();
        }

        public void Dispose()
        {
        }
    }
}

