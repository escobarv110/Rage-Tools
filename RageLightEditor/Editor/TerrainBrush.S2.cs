using System;
using System.Collections.Generic;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public partial class TerrainEditor
    {
        private sealed class StrokeState
        {
            public readonly Dictionary<Part, float[]> Mask = new Dictionary<Part, float[]>();
            public readonly Dictionary<Part, Vector4[]> Before = new Dictionary<Part, Vector4[]>();
            public Vector3 Last;
            public bool HasLast;
            public int Layer;
        }

        private StrokeState strokeS2;

        public float VertexSpacing { get; private set; }

        public string MeshNote = "";

        public void BeginStrokeAt(int layer, Vector3 point)
        {
            BeginStroke(layer);
            strokeS2 = new StrokeState { Layer = layer, Last = point, HasLast = true };
            PaintSegment(point, point, layer);
        }

        public int StrokeTo(Vector3 point, int layer)
        {
            if (strokeS2 == null) { BeginStrokeAt(layer, point); return 0; }
            var from = strokeS2.HasLast ? strokeS2.Last : point;
            if ((point - from).LengthSquared() > (BrushRadius * 8.0f) * (BrushRadius * 8.0f)) from = point;
            strokeS2.Last = point;
            strokeS2.HasLast = true;
            return PaintSegment(from, point, layer);
        }

        public bool Stroking => strokeS2 != null;

        public void EndStrokeS2()
        {
            strokeS2 = null;
            EndStroke();
        }

        public int PaintSegment(Vector3 a, Vector3 b, int layer, float strengthScale = 1.0f)
        {
            if (!HasMesh) return 0;
            layer = Math.Max(0, Math.Min(LayerCount - 1, layer));
            var target = CornerOf(layer);
            float r = Math.Max(BrushRadius, 0.01f);
            float hard = MathUtil.Clamp(BrushHardness, 0.0f, 0.98f);
            float strength = MathUtil.Clamp(BrushStrength * strengthScale, 0.0f, 1.0f);
            var ab = b - a;
            float abLen2 = ab.LengthSquared();
            int touched = 0;

            foreach (var part in Parts)
                if (part.Verts != null && part.Verts.Length > 1 && part.Box.Minimum == part.Box.Maximum)
                { MeasureMesh(); break; }

            var lo = Vector3.Min(a, b) - new Vector3(r);
            var hi = Vector3.Max(a, b) + new Vector3(r);

            foreach (var part in Parts)
            {
                if (part.Verts == null || part.Verts.Length == 0) continue;
                if (part.Box.Maximum.X < lo.X || part.Box.Minimum.X > hi.X ||
                    part.Box.Maximum.Y < lo.Y || part.Box.Minimum.Y > hi.Y ||
                    part.Box.Maximum.Z < lo.Z || part.Box.Minimum.Z > hi.Z) continue;

                float[] mask = null;
                Vector4[] before = null;
                if (strokeS2 != null)
                {
                    if (!strokeS2.Mask.TryGetValue(part, out mask))
                    {
                        mask = new float[part.Verts.Length];
                        before = new Vector4[part.Verts.Length];
                        for (int i = 0; i < part.Verts.Length; i++) before[i] = part.Verts[i].Colour1;
                        strokeS2.Mask[part] = mask;
                        strokeS2.Before[part] = before;
                    }
                    else before = strokeS2.Before[part];
                }

                var verts = part.Verts;
                bool any = false;
                for (int i = 0; i < verts.Length; i++)
                {
                    var p = verts[i].Position;
                    float t = 0.0f;
                    if (abLen2 > 1e-12f) t = MathUtil.Clamp(Vector3.Dot(p - a, ab) / abLen2, 0.0f, 1.0f);
                    var d = p - (a + ab * t);
                    float dist2 = d.LengthSquared();
                    if (dist2 > r * r) continue;
                    float x = (float)Math.Sqrt(dist2) / r;
                    float f = Falloff_U28At(x, hard);
                    if (f <= 0.0005f) continue;
                    if (Blend_U28 == BrushBlend_U28.Add || Blend_U28 == BrushBlend_U28.Blur)
                    {
                        if (BlendVertex_U28(part, i, f, strength, target)) { any = true; touched++; }
                        continue;
                    }

                    Vector4 basis;
                    float k;
                    if (mask != null)
                    {
                        if (f <= mask[i]) continue;
                        mask[i] = f;
                        basis = before[i];
                        k = f * strength;
                    }
                    else
                    {
                        basis = verts[i].Colour1;
                        k = f * strength;
                    }

                    stroke?.Record(part, i, basis);
                    verts[i].Colour1 = new Vector4(
                        basis.X + (0.0f - basis.X) * k,
                        basis.Y + (target.Y - basis.Y) * k,
                        basis.Z + (target.Z - basis.Z) * k,
                        basis.W);
                    any = true;
                    touched++;
                }
                if (any) { part.Dirty = true; TouchProp_V20(part.Group_V20); }
            }
            if (touched > 0) Dirty = true;
            return touched;
        }

        public void MeasureMesh()
        {
            foreach (var part in Parts)
            {
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                foreach (var v in part.Verts) { min = Vector3.Min(min, v.Position); max = Vector3.Max(max, v.Position); }
                if (part.Verts.Length == 0) { min = max = Vector3.Zero; }
                part.Box = new BoundingBox(min, max);
            }

            var lens = new List<float>();
            foreach (var part in Parts)
            {
                var idx = part.Indices;
                int step = Math.Max(3, (idx.Length / 3 / 2000) * 3);
                for (int i = 0; i + 2 < idx.Length; i += step)
                {
                    var pa = part.Verts[idx[i]].Position;
                    var pb = part.Verts[idx[i + 1]].Position;
                    lens.Add((pb - pa).Length());
                }
            }
            if (lens.Count == 0) { VertexSpacing = 0.0f; return; }
            lens.Sort();
            VertexSpacing = lens[lens.Count / 2];
        }

        public bool BrushFinerThanMesh => VertexSpacing > 0.0f && BrushRadius < VertexSpacing * 1.5f;

        public bool Subdivide(int maxVertices, out string message)
        {
            if (!HasMesh) { message = "nothing to subdivide"; return false; }

            long after = 0;
            foreach (var p in Parts) after += p.Verts.Length + p.Indices.Length / 3 * 3 / 2;
            if (after > maxVertices)
            {
                message = $"that would be about {after:N0} vertices - past the {maxVertices:N0} this tool will build. " +
                          "Export as terrain_cb_w_4lyr_cm_tnt instead: that preset blends from a baked mask texture, " +
                          "which has no vertex limit at all.";
                return false;
            }

            var newParts = new List<Part>();
            foreach (var part in Parts)
            {
                var verts = new List<MeshVertex>(part.Verts);
                var uv0 = new List<Vector2>(part.BaseUV0 ?? new Vector2[0]);
                var uv1 = new List<Vector2>(part.BaseUV1 ?? new Vector2[0]);
                bool haveBase = part.BaseUV0 != null && part.BaseUV0.Length == part.Verts.Length;
                var mid = new Dictionary<long, int>();
                var idx = new List<int>();

                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int m)) return m;
                    var va = verts[a]; var vb = verts[b];
                    var v = new MeshVertex
                    {
                        Position = (va.Position + vb.Position) * 0.5f,
                        Normal = Vector3.Normalize(va.Normal + vb.Normal),
                        Tangent = (va.Tangent + vb.Tangent) * 0.5f,
                        Colour0 = (va.Colour0 + vb.Colour0) * 0.5f,
                        Colour1 = (va.Colour1 + vb.Colour1) * 0.5f,
                        UV0 = (va.UV0 + vb.UV0) * 0.5f,
                        UV1 = (va.UV1 + vb.UV1) * 0.5f,
                    };
                    if (v.Normal.LengthSquared() < 1e-8f) v.Normal = Vector3.UnitZ;
                    m = verts.Count;
                    verts.Add(v);
                    if (haveBase) { uv0.Add((uv0[a] + uv0[b]) * 0.5f); uv1.Add((uv1[a] + uv1[b]) * 0.5f); }
                    mid[key] = m;
                    return m;
                }

                var src = part.Indices;
                for (int i = 0; i + 2 < src.Length; i += 3)
                {
                    int a = src[i], b = src[i + 1], c = src[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    idx.Add(a); idx.Add(ab); idx.Add(ca);
                    idx.Add(ab); idx.Add(b); idx.Add(bc);
                    idx.Add(ca); idx.Add(bc); idx.Add(c);
                    idx.Add(ab); idx.Add(bc); idx.Add(ca);
                }

                SplitForExport(verts, idx, haveBase ? uv0 : null, haveBase ? uv1 : null, newParts);
            }

            foreach (var p in Parts) p.Mesh?.Dispose();
            Parts.Clear();
            Parts.AddRange(newParts);
            History.Clear();
            Dirty = true;
            MeasureMesh();
            MeshNote = $"subdivided - {VertexCount:N0} vertices, about {VertexSpacing:0.##} m apart";
            message = MeshNote + " (the undo stack was cleared: the mesh itself changed)";
            return true;
        }

        private static void SplitForExport(List<MeshVertex> verts, List<int> indices,
                                           List<Vector2> baseUv0, List<Vector2> baseUv1, List<Part> into)
        {
            const int Cap = 65000;
            var pv = new List<MeshVertex>();
            var pu0 = new List<Vector2>();
            var pu1 = new List<Vector2>();
            var pi = new List<ushort>();
            var remap = new Dictionary<int, ushort>();

            void Flush()
            {
                if (pi.Count >= 3)
                    into.Add(new Part
                    {
                        Verts = pv.ToArray(),
                        Indices = pi.ToArray(),
                        BaseUV0 = baseUv0 != null ? pu0.ToArray() : null,
                        BaseUV1 = baseUv0 != null ? pu1.ToArray() : null,
                    });
                pv.Clear(); pu0.Clear(); pu1.Clear(); pi.Clear(); remap.Clear();
            }

            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                if (pv.Count + 3 > Cap) Flush();
                for (int k = 0; k < 3; k++)
                {
                    int src = indices[i + k];
                    if (!remap.TryGetValue(src, out ushort dst))
                    {
                        dst = (ushort)pv.Count;
                        remap[src] = dst;
                        pv.Add(verts[src]);
                        if (baseUv0 != null) { pu0.Add(baseUv0[src]); pu1.Add(baseUv1[src]); }
                    }
                    pi.Add(dst);
                }
            }
            Flush();
        }
    }
}

