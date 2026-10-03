using System;
using System.Collections.Generic;
using SharpDX;

namespace RageLightEditor.Editor
{
    public partial class TerrainEditor
    {
        public enum BrushFalloff_U28 { Smooth, Sphere, Root, Sharp, Linear, Constant }
        public enum BrushBlend_U28 { Mix, Add, Subtract, Blur }

        public static readonly string[] FalloffNames_U28 = { "Smooth", "Sphere", "Root", "Sharp", "Linear", "Constant" };
        public static readonly string[] BlendNames_U28 = { "Mix", "Add", "Subtract", "Blur" };

        public BrushFalloff_U28 Falloff_U28 = BrushFalloff_U28.Smooth;
        public BrushBlend_U28 Blend_U28 = BrushBlend_U28.Mix;
        public bool Stabilize_U28;
        public float StabilizePx_U28 = 40.0f;

        private readonly Dictionary<Part, int[][]> neighbours_U28 = new Dictionary<Part, int[][]>();

        public static float FalloffCurve_U28(BrushFalloff_U28 kind, float t)
        {
            t = MathUtil.Clamp(t, 0.0f, 1.0f);
            switch (kind)
            {
                case BrushFalloff_U28.Sphere: return (float)Math.Sqrt(Math.Max(0.0f, 1.0f - t * t));
                case BrushFalloff_U28.Root: return 1.0f - (float)Math.Sqrt(t);
                case BrushFalloff_U28.Sharp: return (1.0f - t) * (1.0f - t);
                case BrushFalloff_U28.Linear: return 1.0f - t;
                case BrushFalloff_U28.Constant: return 1.0f;
                default: { float s = 1.0f - t; return s * s * (3.0f - 2.0f * s); }
            }
        }

        public float Falloff_U28At(float x, float hard)
        {
            if (x >= 1.0f) return 0.0f;
            if (x <= hard) return 1.0f;
            return FalloffCurve_U28(Falloff_U28, (x - hard) / Math.Max(1.0f - hard, 1e-4f));
        }

        private int[][] Neighbours_U28(Part part)
        {
            if (neighbours_U28.TryGetValue(part, out var n) && n.Length == part.Verts.Length) return n;
            var sets = new HashSet<int>[part.Verts.Length];
            var idx = part.Indices;
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                int a = idx[i], b = idx[i + 1], c = idx[i + 2];
                if (a >= sets.Length || b >= sets.Length || c >= sets.Length) continue;
                (sets[a] ??= new HashSet<int>()).Add(b); sets[a].Add(c);
                (sets[b] ??= new HashSet<int>()).Add(a); sets[b].Add(c);
                (sets[c] ??= new HashSet<int>()).Add(a); sets[c].Add(b);
            }
            n = new int[sets.Length][];
            for (int i = 0; i < sets.Length; i++) n[i] = sets[i] == null ? Array.Empty<int>() : new List<int>(sets[i]).ToArray();
            neighbours_U28[part] = n;
            return n;
        }

        private bool BlendVertex_U28(Part part, int i, float f, float strength, Vector3 target)
        {
            var verts = part.Verts;
            var cur = verts[i].Colour1;
            if (Blend_U28 == BrushBlend_U28.Blur)
            {
                var nb = Neighbours_U28(part)[i];
                if (nb.Length == 0) return false;
                float y = 0, z = 0;
                foreach (int j in nb) { y += verts[j].Colour1.Y; z += verts[j].Colour1.Z; }
                y /= nb.Length; z /= nb.Length;
                float kb = f * strength;
                stroke?.Record(part, i, cur);
                verts[i].Colour1 = new Vector4(cur.X, cur.Y + (y - cur.Y) * kb, cur.Z + (z - cur.Z) * kb, cur.W);
                return true;
            }
            float k = f * strength * 0.25f;
            stroke?.Record(part, i, cur);
            verts[i].Colour1 = new Vector4(
                cur.X + (0.0f - cur.X) * k,
                cur.Y + (target.Y - cur.Y) * k,
                cur.Z + (target.Z - cur.Z) * k,
                cur.W);
            return true;
        }
    }
}
