using System;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public static partial class GizmoStyle
    {
        public static bool CW => Look == GizmoLook.CodeWalker;

        public const float CwLinePx = 2.0f;
        public const float CwMoveSize = 0.75f;
        public const float CwRotateSize = 100.0f / 120.0f;

        private static readonly Vector4 CwX = new Vector4(1f, 0f, 0f, 1f);
        private static readonly Vector4 CwY = new Vector4(0f, 1f, 0f, 1f);
        private static readonly Vector4 CwZ = new Vector4(0f, 0f, 1f, 1f);
        private static readonly Vector4 CwSel = new Vector4(1f, 1f, 0f, 1f);
        private static readonly Vector4 CwSelPlane = new Vector4(1f, 1f, 0f, 0.5f);
        private static readonly Vector4 CwInner = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Vector4 CwOuter = new Vector4(0.7f, 0.7f, 0.7f, 1f);

        private static Vector4 CwCol(int i) => i == 0 ? CwX : (i == 1 ? CwY : CwZ);
        private static Vector4 CwDark(Vector4 c) => new Vector4(c.X * 0.5f, c.Y * 0.5f, c.Z * 0.5f, 1f);

        public static int CwMask(int part, bool lockXY = false)
        {
            if (part < 0) return 0;
            if (part < 3) return lockXY && part < 2 ? 3 : 1 << part;
            if (part == PartCentre || part == PartView) return 7;
            switch (part - PartPlane)
            {
                case 0: return 1 | 2;
                case 1: return 2 | 4;
                case 2: return 1 | 4;
            }
            return 0;
        }

        private static void CwLine(TriRenderer tr, Camera cam, Vector3 a, Vector3 b, Vector4 col, float am)
        {
            float wpp = cam.WorldPerPixel((a + b) * 0.5f);
            tr.AddThickLineAA(a, b, cam.Position, CwLinePx * 0.5f * wpp, 0.75f * wpp, Fade(col, am));
        }

        private static void CwRing(TriRenderer tr, Camera cam, Vector3 c, Vector3 a, Vector3 b, float radius, Vector4 col, float am,
                                   float cullSize = 0f)
        {
            float wpp = cam.WorldPerPixel(c);
            var cc = Fade(col, am);
            var hidden = new Vector4(cc.X, cc.Y, cc.Z, 0f);
            float centreDist = (c - cam.Position).Length();
            tr.AddThickArcAA(c, a, b, radius, 0f, (float)(Math.PI * 2.0), cam.Position, CwLinePx * 0.5f * wpp, 0.75f * wpp,
                ang =>
                {
                    if (cullSize <= 0f) return cc;
                    var p = c + (a * (float)Math.Cos(ang) + b * (float)Math.Sin(ang)) * radius;
                    float cull = (centreDist - (p - cam.Position).Length()) / cullSize;
                    return cull < -0.18f ? hidden : cc;
                }, 160);
        }

        private static void CwScreenAxes(Camera cam, Vector3 pos, out Vector3 ax1, out Vector3 ax2)
        {
            var sdir = pos - cam.Position;
            if (sdir.LengthSquared() < 1e-12f) sdir = cam.GetForward();
            sdir.Normalize();
            float ad1 = Math.Abs(Vector3.Dot(sdir, Vector3.UnitY));
            float ad2 = Math.Abs(Vector3.Dot(sdir, Vector3.UnitZ));
            ax1 = Vector3.Normalize(Vector3.Cross(sdir, ad1 > ad2 ? Vector3.UnitY : Vector3.UnitZ));
            ax2 = Vector3.Normalize(Vector3.Cross(sdir, ax1));
        }

        private static readonly int[] CwSideBit1 = { 2, 4, 1 };
        private static readonly int[] CwSideBit2 = { 4, 1, 2 };

        public static void DrawTranslateCW(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] axes, float scale, int hp, int ap, float am)
        {
            float size = scale * CwMoveSize;
            int sel = CwMask(ap >= 0 ? ap : hp);
            var sides1 = new[] { axes[1], axes[2], axes[0] };
            var sides2 = new[] { axes[2], axes[0], axes[1] };
            float sideval = 0.4f * size;

            for (int i = 0; i < 3; i++)
            {
                bool axsel = (sel & (1 << i)) != 0;
                var ax = axes[i] * sideval;
                var sc1 = axsel && (sel & CwSideBit1[i]) != 0 ? CwSel : CwCol(i);
                var sc2 = axsel && (sel & CwSideBit2[i]) != 0 ? CwSel : CwCol(i);
                CwLine(tr, cam, pos + ax, pos + ax + sides1[i] * sideval, sc1, am);
                CwLine(tr, cam, pos + ax, pos + ax + sides2[i] * sideval, sc2, am);
            }
            for (int i = 0; i < 3; i++)
            {
                bool axsel = (sel & (1 << i)) != 0;
                CwLine(tr, cam, pos + axes[i] * (0.2f * size), pos + axes[i] * size, axsel ? CwSel : CwCol(i), am);
            }

            float hexx = 0.5f, hexy = 0.866025403784f, arrowrad = 0.06f * size;
            var arrowv = new[]
            {
                new Vector2(-1, 0), new Vector2(-hexx, hexy), new Vector2(hexx, hexy),
                new Vector2(1, 0), new Vector2(hexx, -hexy), new Vector2(-hexx, -hexy), new Vector2(-1, 0),
            };
            foreach (int i in DepthOrder(cam, pos, axes, size))
            {
                var aend = pos + axes[i] * (1.33f * size);
                var astart = pos + axes[i] * size;
                var col = Fade(CwCol(i), am);
                var dark = Fade(CwDark(CwCol(i)), am);
                for (int n = 0; n < 6; n++)
                {
                    var a1 = arrowv[n] * arrowrad;
                    var a2 = arrowv[n + 1] * arrowrad;
                    var p1 = astart + sides1[i] * a1.Y + sides2[i] * a1.X;
                    var p2 = astart + sides1[i] * a2.Y + sides2[i] * a2.X;
                    tr.AddTri(astart, p2, p1, dark);
                }
                for (int n = 0; n < 6; n++)
                {
                    var a1 = arrowv[n] * arrowrad;
                    var a2 = arrowv[n + 1] * arrowrad;
                    var p1 = astart + sides1[i] * a1.Y + sides2[i] * a1.X;
                    var p2 = astart + sides1[i] * a2.Y + sides2[i] * a2.X;
                    tr.AddTri(aend, p1, p2, col);
                }
            }

            var pc = Fade(CwSelPlane, am);
            for (int i = 0; i < 3; i++)
            {
                if ((sel & (1 << i)) == 0) continue;
                var ax = axes[i] * sideval;
                for (int n = i + 1; n < 3; n++)
                {
                    if ((sel & (1 << n)) == 0) continue;
                    var tax = axes[n] * sideval;
                    tr.AddTri(pos, pos + ax, pos + tax, pc);
                    tr.AddTri(pos + tax + ax, pos + tax, pos + ax, pc);
                }
            }
        }

        public static void DrawRotateCW(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] axes, float scale, int hp, int ap, float am,
                                        bool[] ringOn, bool viewRing)
        {
            float size = scale * CwRotateSize;
            int part = ap >= 0 ? ap : hp;
            int sel = CwMask(part);
            for (int i = 0; i < 3; i++)
                CwLine(tr, cam, pos, pos + axes[i] * (0.3f * size), (sel & (1 << i)) != 0 ? CwCol(i) : CwInner, am);

            CwScreenAxes(cam, pos, out var ax1, out var ax2);
            if (viewRing) CwRing(tr, cam, pos, ax1, ax2, size, part == PartView ? CwSel : CwOuter, am);
            CwRing(tr, cam, pos, ax1, ax2, 0.75f * size, CwInner, am);

            var planeA = new[] { axes[1], axes[0], axes[0] };
            var planeB = new[] { axes[2], axes[2], axes[1] };
            for (int i = 0; i < 3; i++)
            {
                if (ringOn != null && !ringOn[i]) continue;
                CwRing(tr, cam, pos, planeA[i], planeB[i], 0.75f * size, part == i ? CwSel : CwCol(i), am, size);
            }
        }

        public static void DrawScaleCW(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] axes, float scale, int hp, int ap, float am, bool lockXY)
        {
            float size = scale * CwMoveSize;
            int sel = CwMask(ap >= 0 ? ap : hp, lockXY);
            var sides1 = new[] { axes[1], axes[2], axes[0] };
            var sides2 = new[] { axes[2], axes[0], axes[1] };
            float innertri = 0.7f * size, outertri = size;

            for (int i = 0; i < 3; i++)
            {
                bool axsel = (sel & (1 << i)) != 0;
                bool trisel = axsel && (sel & CwSideBit1[i]) != 0;
                var tricol = trisel ? CwSel : CwCol(i);
                var trincol = trisel ? CwSel : CwCol((i + 1) % 3);
                var inner1 = axes[i] * innertri; var inner2 = sides1[i] * innertri; var innera = (inner1 + inner2) * 0.5f;
                var outer1 = axes[i] * outertri; var outer2 = sides1[i] * outertri; var outera = (outer1 + outer2) * 0.5f;
                CwLine(tr, cam, pos + inner1, pos + innera, tricol, am);
                CwLine(tr, cam, pos + innera, pos + inner2, trincol, am);
                CwLine(tr, cam, pos + outer1, pos + outera, tricol, am);
                CwLine(tr, cam, pos + outera, pos + outer2, trincol, am);
            }
            for (int i = 0; i < 3; i++)
                CwLine(tr, cam, pos, pos + axes[i] * (1.33f * size), (sel & (1 << i)) != 0 ? CwSel : CwCol(i), am);

            float cubesize = 0.025f * size;
            foreach (int i in DepthOrder(cam, pos, axes, size))
            {
                var cstart = pos + axes[i] * (1.28f * size);
                var cend = pos + axes[i] * (1.33f * size);
                var cs1 = sides1[i] * cubesize;
                var cs2 = sides2[i] * cubesize;
                var cv1 = cstart + cs1 - cs2; var cv2 = cstart - cs1 - cs2; var cv3 = cend + cs1 - cs2; var cv4 = cend - cs1 - cs2;
                var cv5 = cstart + cs1 + cs2; var cv6 = cstart - cs1 + cs2; var cv7 = cend + cs1 + cs2; var cv8 = cend - cs1 + cs2;
                var col = Fade(CwCol(i), am);
                var cold = Fade(CwDark(CwCol(i)), am);
                tr.AddTri(cv1, cv2, cv5, cold); tr.AddTri(cv5, cv2, cv6, cold);
                tr.AddTri(cv3, cv4, cv7, col); tr.AddTri(cv7, cv4, cv8, col);
                tr.AddTri(cv1, cv2, cv3, col); tr.AddTri(cv3, cv2, cv4, col);
                tr.AddTri(cv5, cv6, cv7, col); tr.AddTri(cv7, cv6, cv8, col);
                tr.AddTri(cv1, cv5, cv3, col); tr.AddTri(cv3, cv5, cv7, col);
                tr.AddTri(cv2, cv6, cv4, col); tr.AddTri(cv4, cv6, cv8, col);
            }

            var pc = Fade(CwSelPlane, am);
            for (int i = 0; i < 3; i++)
            {
                if (sel == 7)
                {
                    tr.AddTri(pos, pos + axes[i] * innertri, pos + sides1[i] * innertri, pc);
                }
                else if ((sel & (1 << i)) != 0 && (sel & CwSideBit1[i]) != 0)
                {
                    tr.AddTri(pos + axes[i] * innertri, pos + sides1[i] * innertri, pos + axes[i] * outertri, pc);
                    tr.AddTri(pos + axes[i] * outertri, pos + sides1[i] * innertri, pos + sides1[i] * outertri, pc);
                }
            }
        }

        private static bool CwRayBox(Vector3 o, Vector3 d, Vector3 mn, Vector3 mx, out float t)
        {
            float t0 = 0f, t1 = float.MaxValue;
            t = 0f;
            for (int k = 0; k < 3; k++)
            {
                float ok = k == 0 ? o.X : (k == 1 ? o.Y : o.Z);
                float dk = k == 0 ? d.X : (k == 1 ? d.Y : d.Z);
                float lo = k == 0 ? mn.X : (k == 1 ? mn.Y : mn.Z);
                float hi = k == 0 ? mx.X : (k == 1 ? mx.Y : mx.Z);
                if (Math.Abs(dk) < 1e-9f)
                {
                    if (ok < lo || ok > hi) return false;
                    continue;
                }
                float a = (lo - ok) / dk, b = (hi - ok) / dk;
                if (a > b) { var s = a; a = b; b = s; }
                if (a > t0) t0 = a;
                if (b < t1) t1 = b;
                if (t0 > t1) return false;
            }
            t = t0;
            return true;
        }

        private static void CwLocalRay(Ray ray, Vector3 pivot, Vector3[] axes, out Vector3 o, out Vector3 d)
        {
            var r = ray.Position - pivot;
            o = new Vector3(Vector3.Dot(r, axes[0]), Vector3.Dot(r, axes[1]), Vector3.Dot(r, axes[2]));
            d = new Vector3(Vector3.Dot(ray.Direction, axes[0]), Vector3.Dot(ray.Direction, axes[1]), Vector3.Dot(ray.Direction, axes[2]));
        }

        private static Vector3 CwAxisVec(int i, float v) => i == 0 ? new Vector3(v, 0, 0) : (i == 1 ? new Vector3(0, v, 0) : new Vector3(0, 0, v));

        private static bool CwPlaneHit(Ray ray, Vector3 point, Vector3 normal, out Vector3 hit, out float t)
        {
            hit = point; t = 0f;
            float den = Vector3.Dot(ray.Direction, normal);
            if (Math.Abs(den) < 1e-9f) return false;
            t = Vector3.Dot(point - ray.Position, normal) / den;
            if (t < 0f) return false;
            hit = ray.Position + ray.Direction * t;
            return true;
        }

        public static int PlanePart(int a, int b)
        {
            int m = (1 << a) | (1 << b);
            return m == 3 ? PartPlane : (m == 6 ? PartPlane + 1 : PartPlane + 2);
        }

        public static int HitTranslateCW(Ray ray, Vector3 pivot, Vector3[] axes, float scale)
        {
            float size = scale * CwMoveSize;
            CwLocalRay(ray, pivot, axes, out var o, out var d);
            float best = float.MaxValue;
            int part = -1;
            float rad = 0.07f * size;
            for (int i = 0; i < 3; i++)
            {
                var side = new Vector3(rad) - CwAxisVec(i, rad);
                var mn = -side + CwAxisVec(i, 0.2f * size);
                var mx = side + CwAxisVec(i, 1.33f * size);
                if (CwRayBox(o, d, mn, mx, out float t) && t < best) { best = t; part = i; }
                for (int n = i + 1; n < 3; n++)
                {
                    var outer = CwAxisVec(i, 0.5f * size) + CwAxisVec(n, 0.5f * size);
                    var inner = CwAxisVec(i, 0.25f * size) + CwAxisVec(n, 0.25f * size);
                    if (CwRayBox(o, d, Vector3.Zero, outer, out float tp) && !CwRayBox(o, d, Vector3.Zero, inner, out _) && tp < best)
                    {
                        best = tp; part = PlanePart(i, n);
                    }
                }
            }
            var all = new Vector3(0.07f * size);
            if (CwRayBox(o, d, -all, all, out float tc) && tc < best) { part = PartCentre; }
            return part;
        }

        public static int HitRotateCW(Ray ray, Vector3 camPos, Vector3 pivot, Vector3[] axes, float scale, bool[] ringOn, bool viewRing)
        {
            float size = scale * CwRotateSize;
            float inner = 0.75f * size, innerHit = 0.2f * size, outerHit = 0.13f * size;
            float centreDist = (pivot - camPos).Length();
            float best = float.MaxValue;
            int part = -1;
            for (int i = 0; i < 3; i++)
            {
                if (ringOn != null && !ringOn[i]) continue;
                if (!CwPlaneHit(ray, pivot, axes[i], out var hit, out _)) continue;
                float hitDist = (hit - camPos).Length();
                if ((centreDist - hitDist) / size < -0.18f) continue;
                float r = (hit - pivot).Length();
                if (r > inner - innerHit && r < inner + innerHit && hitDist < best) { best = hitDist; part = i; }
            }
            if (part < 0 && viewRing)
            {
                var n = pivot - camPos;
                if (n.LengthSquared() > 1e-12f && CwPlaneHit(ray, pivot, Vector3.Normalize(n), out var hit, out _))
                {
                    float r = (hit - pivot).Length();
                    if (r > size - outerHit && r < size + outerHit) part = PartView;
                }
            }
            return part;
        }

        public static int HitScaleCW(Ray ray, Vector3 pivot, Vector3[] axes, float scale, bool lockXY)
        {
            float size = scale * CwMoveSize;
            CwLocalRay(ray, pivot, axes, out var o, out var d);
            float best = float.MaxValue;
            int part = -1;
            float rad = 0.09f * size, innertri = 0.7f * size, outertri = size;
            for (int i = 0; i < 3; i++)
            {
                var side = new Vector3(rad) - CwAxisVec(i, rad);
                if (CwRayBox(o, d, -side + CwAxisVec(i, 0.4f * size), side + CwAxisVec(i, 1.33f * size), out float t) && t < best)
                {
                    best = t; part = i;
                }
                int j = (i + 1) % 3;
                var normal = Vector3.Cross(axes[i], axes[j]);
                if (!CwPlaneHit(ray, pivot, normal, out var hit, out float th) || th > best) continue;
                var rel = hit - pivot;
                float d1 = Vector3.Dot(rel, axes[i]), d2 = Vector3.Dot(rel, axes[j]);
                if (d1 <= 0 || d2 <= 0) continue;
                if (d1 < innertri && d2 < innertri && d1 + d2 < innertri) { best = th; part = PartCentre; }
                else if (d1 < outertri && d2 < outertri && d1 + d2 < outertri) { best = th; part = PlanePart(i, j); }
            }
            if (lockXY && (part == PartPlane + 1 || part == PartPlane + 2)) part = PartCentre;
            return part;
        }
    }
}
