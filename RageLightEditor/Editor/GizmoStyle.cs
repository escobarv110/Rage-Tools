using System;
using ImGuiNET;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public static partial class GizmoStyle
    {

        public static float AxisPx = 120.0f;
        public static bool ScreenText = false;

        public static void ApplySettings(AppSettings s)
        {
            if (s == null) return;
            AxisPx = Math.Clamp(s.GizmoSizePx, 40f, 400f);
            Look = LookFromSettings(s);
        }

        public const float StrokePx = 3.5f;
        public const float FeatherPx = 1.25f;
        public const float OutlinePx = 1.25f;
        public const float OccludedAlpha = 0.55f;
        public const float PlaneMin = 0.30f, PlaneMax = 0.55f;
        public const float CentreR = 0.11f;
        public const float ViewRingR = 1.22f;
        public const float HeadR = 0.075f;

        public static readonly Vector4 X = new Vector4(1.00f, 0.10f, 0.10f, 1f);
        public static readonly Vector4 Y = new Vector4(0.10f, 1.00f, 0.10f, 1f);
        public static readonly Vector4 Z = new Vector4(0.20f, 0.45f, 1.00f, 1f);
        public static readonly Vector4 Hot = new Vector4(1.00f, 1.00f, 0.00f, 1f);
        public static readonly Vector4 Centre = new Vector4(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Vector4 ViewRing = new Vector4(0.92f, 0.92f, 0.95f, 0.85f);
        public static readonly Vector4 Outline = new Vector4(0.02f, 0.02f, 0.03f, 0.85f);
        public static readonly Vector4 Shadow = new Vector4(0f, 0f, 0f, 0.40f);
        public static readonly Vector4 Glow = new Vector4(1.00f, 0.86f, 0.18f, 0.28f);
        public static readonly Vector4 HotFill = new Vector4(1f, 0.88f, 0.25f, 0.30f);
        public static Vector4 Direction => Modern ? DirectionModern : DirectionClassic;
        private static readonly Vector4 DirectionModern = new Vector4(0.45f, 0.92f, 1.00f, 1f);
        private static readonly Vector4 DirectionClassic = new Vector4(1f, 0.95f, 0.30f, 1f);

        public static Vector4 AxisColour(int axis, bool hot)
        {
            if (hot) return Hot;
            switch (axis)
            {
                case 0: return X;
                case 1: return Y;
                default: return Z;
            }
        }

        public static float Scale(Camera cam, Vector3 pos) => cam.WorldPerPixel(pos) * AxisPx;

        public static Vector4 Fade(Vector4 c, float alphaMul) => new Vector4(c.X, c.Y, c.Z, c.W * alphaMul);

        private static Vector4 Mul(Vector4 c, float k) => new Vector4(c.X * k, c.Y * k, c.Z * k, c.W);

        public static (Vector3 right, Vector3 up, Vector3 fwd) ScreenBasis(Camera cam)
        {
            var fwd = cam.GetForward();
            var right = Vector3.Cross(fwd, Vector3.UnitZ);
            if (right.LengthSquared() < 1e-6f) right = Vector3.UnitX; else right.Normalize();
            var up = Vector3.Cross(right, fwd);
            if (up.LengthSquared() < 1e-6f) up = Vector3.UnitY; else up.Normalize();
            return (right, up, fwd);
        }

        private static Vector3 LightDir(Camera cam)
        {
            var (r, u, f) = ScreenBasis(cam);
            return Vector3.Normalize(-f + u * 0.7f - r * 0.5f);
        }

        private static Vector3 PixelOffset(Camera cam, Vector3 p, float dx, float dy)
        {
            var (r, u, _) = ScreenBasis(cam);
            float wpp = cam.WorldPerPixel(p);
            return (r * dx - u * dy) * wpp;
        }

        public static void Stroke(TriRenderer tr, Camera cam, Vector3 a, Vector3 b, Vector4 col, float alphaMul,
                                  float widthPx = StrokePx, bool glow = false)
        {
            if (Flat) { StrokeFlat(tr, cam, a, b, col, alphaMul, widthPx); return; }
            if (Modern) { StrokeModern(tr, cam, a, b, col, alphaMul, widthPx, glow); return; }
            var mid = (a + b) * 0.5f;
            float wpp = cam.WorldPerPixel(mid);
            float f = FeatherPx * wpp;
            var cp = cam.Position;
            if (Modern)
            {
                var sh = PixelOffset(cam, mid, 1.2f, 1.2f);
                tr.AddThickLineAA(a + sh, b + sh, cp, (widthPx * 0.5f + OutlinePx) * wpp, 2.5f * wpp, Fade(Shadow, alphaMul));
                if (glow) tr.AddThickLineAA(a, b, cp, (widthPx * 0.5f + 5f) * wpp, 5f * wpp, Fade(Glow, alphaMul));
            }
            tr.AddThickLineAA(a, b, cp, (widthPx * 0.5f + OutlinePx) * wpp, f, Fade(Outline, alphaMul));
            tr.AddThickLineAA(a, b, cp, widthPx * 0.5f * wpp, f, Fade(col, alphaMul));
        }

        public static void Ring(TriRenderer tr, Camera cam, Vector3 centre, Vector3 axisA, Vector3 axisB, float radius,
                                Vector4 col, float alphaMul, float widthPx = StrokePx, int segments = 96,
                                bool dimBack = false, bool glow = false)
        {
            float wpp = cam.WorldPerPixel(centre);
            float f = FeatherPx * wpp;
            var cp = cam.Position;
            var toCam = cp - centre;
            Func<float, float> back = ang =>
            {
                if (!dimBack) return 1f;
                var p = axisA * (float)Math.Cos(ang) + axisB * (float)Math.Sin(ang);
                float d = Vector3.Dot(p * radius, toCam) / Math.Max(toCam.Length() * radius, 1e-6f);
                return 0.32f + 0.68f * Math.Clamp(d * 4f + 0.5f, 0f, 1f);
            };
            const float TwoPi = (float)(Math.PI * 2.0);
            if (Modern)
            {
                var sh = PixelOffset(cam, centre, 1.2f, 1.2f);
                var shc = Fade(Shadow, alphaMul);
                tr.AddThickArcAA(centre + sh, axisA, axisB, radius, 0f, TwoPi, cp, (widthPx * 0.5f + OutlinePx) * wpp, 2.5f * wpp,
                    ang => Fade(shc, back(ang)), segments);
                if (glow) tr.AddThickArcAA(centre, axisA, axisB, radius, 0f, TwoPi, cp, (widthPx * 0.5f + 5f) * wpp, 5f * wpp,
                    Fade(Glow, alphaMul), segments);
            }
            var oc = Fade(Outline, alphaMul);
            var cc = Fade(col, alphaMul);
            tr.AddThickArcAA(centre, axisA, axisB, radius, 0f, TwoPi, cp, (widthPx * 0.5f + OutlinePx) * wpp, f,
                ang => Fade(oc, back(ang)), segments);
            tr.AddThickArcAA(centre, axisA, axisB, radius, 0f, TwoPi, cp, widthPx * 0.5f * wpp, f,
                ang => { float k = back(ang); return Fade(Mul(cc, 0.75f + 0.25f * k), k); }, segments);
        }

        public static void Arc(TriRenderer tr, Camera cam, Vector3 centre, Vector3 axisA, Vector3 axisB, float radius,
                               float a0, float a1, Vector4 col, float alphaMul, float widthPx = StrokePx)
        {
            float wpp = cam.WorldPerPixel(centre);
            float f = FeatherPx * wpp;
            tr.AddThickArcAA(centre, axisA, axisB, radius, a0, a1, cam.Position, (widthPx * 0.5f + OutlinePx) * wpp, f, Fade(Outline, alphaMul), 96);
            tr.AddThickArcAA(centre, axisA, axisB, radius, a0, a1, cam.Position, widthPx * 0.5f * wpp, f, Fade(col, alphaMul), 96);
        }

        public static void ArrowHead(TriRenderer tr, Camera cam, Vector3 tip, Vector3 baseCentre, Vector3 pa, Vector3 pb,
                                     float radius, Vector4 col, float alphaMul, bool hot = false)
        {
            if (Flat) { ArrowHeadFlat(tr, cam, tip, baseCentre, radius, col, alphaMul); return; }
            if (Modern) { ArrowHeadModern(tr, cam, tip, baseCentre, pa, pb, radius, col, alphaMul, hot); return; }
            float wpp = cam.WorldPerPixel(baseCentre);
            var axis = tip - baseCentre;
            float len = axis.Length();
            if (len < 1e-7f) return;
            var n = axis / len;
            var oc = Fade(Outline, alphaMul);
            void Shell(float growPx, Vector4 c)
            {
                float g = growPx * wpp;
                tr.AddCone(tip + n * g * 1.5f, baseCentre - n * g, pa, pb, radius + g, c, c, 24);
                tr.AddDisc(baseCentre - n * g, pa, pb, radius + g, c, c, 24);
            }
            if (Modern)
            {
                var sh = PixelOffset(cam, baseCentre, 1.2f, 1.2f);
                var shc = Fade(Shadow, alphaMul);
                tr.AddCone(tip + sh, baseCentre + sh, pa, pb, radius + 1.5f * wpp, shc, shc, 24);
                tr.AddDisc(baseCentre + sh, pa, pb, radius + 1.5f * wpp, shc, shc, 24);
                if (hot) Shell(4.5f, Fade(Glow, alphaMul));
                Shell(2.2f, Fade(new Vector4(Outline.X, Outline.Y, Outline.Z, 0.35f), alphaMul));
                Shell(OutlinePx, oc);
                tr.AddConeShaded(tip, baseCentre, pa, pb, radius, Fade(col, alphaMul), LightDir(cam), 0.55f, 24);
            }
            else
            {
                Shell(OutlinePx, oc);
                var cc = Fade(col, alphaMul);
                tr.AddCone(tip, baseCentre, pa, pb, radius, cc, cc, 16);
                tr.AddDisc(baseCentre, pa, pb, radius, cc, cc, 16);
            }
        }

        public static void Cube(TriRenderer tr, Camera cam, Vector3 c, Vector3 ax, Vector3 ay, Vector3 az, float r,
                                Vector4 col, float alphaMul, bool hot = false)
        {
            float wpp = cam.WorldPerPixel(c);
            if (Modern)
            {
                var sh = PixelOffset(cam, c, 1.2f, 1.2f);
                SolidBox(tr, c + sh, ax, ay, az, r + 1.5f * wpp, Fade(Shadow, alphaMul), null);
                if (hot) SolidBox(tr, c, ax, ay, az, r + 4.5f * wpp, Fade(Glow, alphaMul), null);
                SolidBox(tr, c, ax, ay, az, r + 2.2f * wpp, Fade(new Vector4(Outline.X, Outline.Y, Outline.Z, 0.35f), alphaMul), null);
                SolidBox(tr, c, ax, ay, az, r + OutlinePx * wpp, Fade(Outline, alphaMul), null);
                SolidBox(tr, c, ax, ay, az, r, Fade(col, alphaMul), LightDir(cam));
            }
            else
            {
                SolidBox(tr, c, ax, ay, az, r + OutlinePx * wpp, Fade(Outline, alphaMul), null);
                SolidBox(tr, c, ax, ay, az, r, Fade(col, alphaMul), null);
            }
        }

        private static void SolidBox(TriRenderer tr, Vector3 c, Vector3 ax, Vector3 ay, Vector3 az, float r, Vector4 col, Vector3? lightDir)
        {
            var x = ax * r; var y = ay * r; var z = az * r;
            Vector3 P(int sx, int sy, int sz) => c + x * sx + y * sy + z * sz;
            Vector4 Shade(Vector3 nrm, float classic)
            {
                if (lightDir == null) return new Vector4(col.X * classic, col.Y * classic, col.Z * classic, col.W);
                float k = 0.5f + 0.5f * Math.Max(0f, Vector3.Dot(nrm, lightDir.Value));
                return new Vector4(col.X * k, col.Y * k, col.Z * k, col.W);
            }
            tr.AddQuad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Shade(az, 1.0f));
            tr.AddQuad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), Shade(-az, 0.55f));
            tr.AddQuad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Shade(-ay, 0.8f));
            tr.AddQuad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Shade(ay, 0.8f));
            tr.AddQuad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), Shade(-ax, 0.7f));
            tr.AddQuad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), Shade(ax, 0.7f));
        }

        public static void PlaneHandle(TriRenderer tr, Camera cam, Vector3 pos, Vector3 a, Vector3 b, int normalAxis,
                                       Vector4 ca, Vector4 cb, float scale, bool hot, float alphaMul)
        {
            float o0 = scale * PlaneMin, o1 = scale * PlaneMax;
            Vector3 p00 = pos + a * o0 + b * o0, p10 = pos + a * o1 + b * o0, p11 = pos + a * o1 + b * o1, p01 = pos + a * o0 + b * o1;
            float wpp = cam.WorldPerPixel(p11);
            if (Modern)
            {
                var nc = AxisColour(normalAxis, hot);
                var fill = hot ? new Vector4(Hot.X, Hot.Y, Hot.Z, 0.55f) : new Vector4(nc.X, nc.Y, nc.Z, 0.28f);
                var sh = PixelOffset(cam, p11, 1.2f, 1.2f);
                tr.AddQuadAA(p00 + sh, p10 + sh, p11 + sh, p01 + sh, 2.0f * wpp, Fade(new Vector4(0, 0, 0, 0.25f), alphaMul));
                tr.AddQuadAA(p00, p10, p11, p01, FeatherPx * wpp, Fade(fill, alphaMul));
                float w = hot ? 2.2f : 1.5f;
                var oc = Fade(Outline, alphaMul);
                var ec = Fade(hot ? Hot : nc, alphaMul);
                Vector3[] q = { p00, p10, p11, p01 };
                for (int i = 0; i < 4; i++)
                    tr.AddThickLineAA(q[i], q[(i + 1) % 4], cam.Position, (w * 0.5f + 0.8f) * wpp, FeatherPx * wpp, oc);
                for (int i = 0; i < 4; i++)
                    tr.AddThickLineAA(q[i], q[(i + 1) % 4], cam.Position, w * 0.5f * wpp, FeatherPx * wpp, ec);
                if (hot) tr.AddQuadAA(p00, p10, p11, p01, 5f * wpp, Fade(new Vector4(Glow.X, Glow.Y, Glow.Z, 0.10f), alphaMul));
                return;
            }
            if (hot) tr.AddQuad(p00, p10, p11, p01, Fade(HotFill, alphaMul));
            float wc = StrokePx * 0.75f;
            var occ = Fade(Outline, alphaMul);
            float f = FeatherPx * wpp;
            tr.AddThickLineAA(p10, p11, cam.Position, (wc * 0.5f + OutlinePx) * wpp, f, occ);
            tr.AddThickLineAA(p01, p11, cam.Position, (wc * 0.5f + OutlinePx) * wpp, f, occ);
            tr.AddThickLineAA(p10, p11, cam.Position, wc * 0.5f * wpp, f, Fade(ca, alphaMul));
            tr.AddThickLineAA(p01, p11, cam.Position, wc * 0.5f * wpp, f, Fade(cb, alphaMul));
        }

        public static void CentreCircle(TriRenderer tr, Camera cam, Vector3 pos, float scale, bool hot, float alphaMul)
        {
            var (r, u, _) = ScreenBasis(cam);
            float rad = scale * CentreR;
            float wpp = cam.WorldPerPixel(pos);
            if (hot) tr.AddDiscAA(pos, r, u, rad, FeatherPx * wpp, Fade(new Vector4(Hot.X, Hot.Y, Hot.Z, 0.35f), alphaMul), 40);
            Ring(tr, cam, pos, r, u, rad, hot ? Hot : Centre, alphaMul, hot ? 3.0f : 2.2f, 48, false, hot);
        }

        public static void ViewRotateRing(TriRenderer tr, Camera cam, Vector3 pos, float scale, bool hot, float alphaMul)
        {
            var (r, u, _) = ScreenBasis(cam);
            Ring(tr, cam, pos, r, u, scale * ViewRingR, hot ? Hot : ViewRing, alphaMul, hot ? 3.2f : 2.4f, 128, false, hot);
        }

        public static void GuideLine(TriRenderer tr, Camera cam, Vector3 pos, Vector3 axis, Vector4 col, float alphaMul, float scale)
        {
            float wpp = cam.WorldPerPixel(pos);
            var c1 = Fade(new Vector4(col.X, col.Y, col.Z, 0.85f), alphaMul);
            var c0 = new Vector4(col.X, col.Y, col.Z, 0f);
            float ext = scale * 6f;
            var oc = Fade(new Vector4(Outline.X, Outline.Y, Outline.Z, 0.45f), alphaMul); var oz = new Vector4(oc.X, oc.Y, oc.Z, 0f);
            tr.AddThickLineAA(pos, pos + axis * ext, cam.Position, 1.5f * wpp, FeatherPx * wpp, oc, oz);
            tr.AddThickLineAA(pos, pos - axis * ext, cam.Position, 1.5f * wpp, FeatherPx * wpp, oc, oz);
            tr.AddThickLineAA(pos, pos + axis * ext, cam.Position, 0.9f * wpp, FeatherPx * wpp, c1, c0);
            tr.AddThickLineAA(pos, pos - axis * ext, cam.Position, 0.9f * wpp, FeatherPx * wpp, c1, c0);
        }

        public static void SnapTicks(TriRenderer tr, Camera cam, Vector3 centre, Vector3 axisA, Vector3 axisB, float radius,
                                     float stepDeg, Vector4 col, float alphaMul)
        {
            if (stepDeg < 0.5f) return;
            int n = Math.Max(4, (int)Math.Round(360.0 / stepDeg));
            if (n > 360) return;
            float wpp = cam.WorldPerPixel(centre);
            var c = Fade(new Vector4(col.X, col.Y, col.Z, 0.9f), alphaMul);
            var oc = Fade(Outline, alphaMul);
            bool major = n >= 24;
            for (int i = 0; i < n; i++)
            {
                float ang = (float)(i * Math.PI * 2.0 / n);
                var dir = axisA * (float)Math.Cos(ang) + axisB * (float)Math.Sin(ang);
                float len = (major && (i % 4 == 0)) ? 0.075f : 0.045f;
                var p0 = centre + dir * (radius * 1.02f);
                var p1 = centre + dir * (radius * (1f + len));
                tr.AddThickLineAA(p0, p1, cam.Position, 1.4f * wpp, FeatherPx * wpp, oc);
                tr.AddThickLineAA(p0, p1, cam.Position, 0.7f * wpp, FeatherPx * wpp, c);
            }
        }

        public static void Pie(TriRenderer tr, Camera cam, Vector3 centre, Vector3 refA, Vector3 refB, float radius,
                               float angle, float alphaMul)
        {
            tr.AddArcFan(centre, refA, refB, radius, 0, angle, Fade(HotFill, alphaMul), 128);
            var cur = refA * (float)Math.Cos(angle) + refB * (float)Math.Sin(angle);
            Stroke(tr, cam, centre, centre + refA * radius, Fade(new Vector4(1f, 1f, 1f, 0.7f), 1f), alphaMul, 1.6f);
            Stroke(tr, cam, centre, centre + cur * radius, Hot, alphaMul, 2.2f);
            Arc(tr, cam, centre, refA, refB, radius, 0, angle, Hot, alphaMul, 2.2f);
        }

        public const int PartCentre = 3, PartPlane = 4, PartView = 7;

        public static (Vector3 a, Vector3 b, Vector3 n) PlaneAxes(Vector3[] basis, int p)
        {
            switch (p)
            {
                case 0: return (basis[0], basis[1], basis[2]);
                case 1: return (basis[1], basis[2], basis[0]);
                default: return (basis[0], basis[2], basis[1]);
            }
        }
        public static int PlaneNormalIndex(int p) => p == 0 ? 2 : (p == 1 ? 0 : 1);
        public static int PlaneAxisIndexA(int p) => p == 0 ? 0 : (p == 1 ? 1 : 0);
        public static int PlaneAxisIndexB(int p) => p == 0 ? 1 : (p == 1 ? 2 : 2);

        public static void DrawTranslate(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] basis, float scale,
                                         int hp, int ap, bool dragging, float am)
        {
            if (CW) { DrawTranslateCW(tr, cam, pos, basis, scale, hp, ap, am); return; }
            if (Flat) { DrawTranslateFlat(tr, cam, pos, basis, scale, hp, ap, dragging, am); return; }
            if (Modern) { DrawTranslateModern(tr, cam, pos, basis, scale, hp, ap, dragging, am); return; }
            if (dragging && Modern)
            {
                if (ap >= 0 && ap < 3) GuideLine(tr, cam, pos, basis[ap], AxisColour(ap, false), am, scale);
                else if (ap >= PartPlane && ap < PartPlane + 3)
                {
                    var (a, b, _) = PlaneAxes(basis, ap - PartPlane);
                    GuideLine(tr, cam, pos, a, AxisColour(PlaneAxisIndexA(ap - PartPlane), false), am, scale);
                    GuideLine(tr, cam, pos, b, AxisColour(PlaneAxisIndexB(ap - PartPlane), false), am, scale);
                }
            }

            for (int p = 0; p < 3; p++)
            {
                bool hot = hp == PartPlane + p || ap == PartPlane + p;
                var (a, b, _) = PlaneAxes(basis, p);
                PlaneHandle(tr, cam, pos, a, b, PlaneNormalIndex(p),
                    AxisColour(PlaneAxisIndexA(p), hot), AxisColour(PlaneAxisIndexB(p), hot), scale, hot, am);
            }

            foreach (int i in DepthOrder(cam, pos, basis, scale))
            {
                bool hot = hp == i || ap == i;
                var col = AxisColour(i, hot);
                var axis = basis[i];
                var tip = pos + axis * scale;
                var coneBase = pos + axis * (scale * (Modern ? 0.80f : 0.78f));
                float start = Modern ? scale * (CentreR + 0.06f) : scale * 0.14f;
                Stroke(tr, cam, pos + axis * start, coneBase, col, am, StrokePx, hot && Modern);
                ArrowHead(tr, cam, tip, coneBase, basis[(i + 1) % 3], basis[(i + 2) % 3], scale * HeadR, col, am, hot);
            }

            bool chot = hp == PartCentre || ap == PartCentre;
            if (Modern) CentreCircle(tr, cam, pos, scale, chot, am);
            else Cube(tr, cam, pos, basis[0], basis[1], basis[2], scale * 0.07f, chot ? Hot : Centre, am);
        }

        public static void DrawRotate(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] basis, float scale,
                                      int hp, int ap, bool dragging, float am, bool[] ringOn, bool viewRing,
                                      Vector3 dragAxis, Vector3 rotStartVec, float angle, float snapDeg)
        {
            if (CW) { DrawRotateCW(tr, cam, pos, basis, scale, hp, ap, am, ringOn, viewRing); return; }
            if (Flat) { DrawRotateFlat(tr, cam, pos, basis, scale, hp, ap, dragging, am, ringOn, viewRing, dragAxis, rotStartVec, angle, snapDeg); return; }
            if (Modern) { DrawRotateModern(tr, cam, pos, basis, scale, hp, ap, dragging, am, ringOn, viewRing, dragAxis, rotStartVec, angle, snapDeg); return; }
            var (sr, su, _) = ScreenBasis(cam);

            for (int i = 0; i < 3; i++)
            {
                if (!ringOn[i]) continue;
                bool hot = hp == i || ap == i;
                if (hot && !dragging)
                {
                    float wpp = cam.WorldPerPixel(pos);
                    tr.AddDiscAA(pos, basis[(i + 1) % 3], basis[(i + 2) % 3], scale, FeatherPx * wpp,
                        Fade(Modern ? new Vector4(Hot.X, Hot.Y, Hot.Z, 0.12f) : HotFill, am), 96);
                }
            }

            if (Modern && viewRing)
            {
                bool vhot = hp == PartView || ap == PartView;
                ViewRotateRing(tr, cam, pos, scale, vhot, am);
                if (vhot && snapDeg > 0.5f && dragging) SnapTicks(tr, cam, pos, sr, su, scale * ViewRingR, snapDeg, Hot, am);
            }

            for (int i = 0; i < 3; i++)
            {
                if (!ringOn[i]) continue;
                bool hot = hp == i || ap == i;
                var a = basis[(i + 1) % 3]; var b = basis[(i + 2) % 3];
                float ringAm = (dragging && !hot && Modern) ? am * 0.45f : am;
                Ring(tr, cam, pos, a, b, scale, AxisColour(i, hot), ringAm, hot ? StrokePx + 0.8f : StrokePx, 96, Modern, hot && Modern);
                if (Modern && hot && snapDeg > 0.5f && dragging) SnapTicks(tr, cam, pos, a, b, scale, snapDeg, Hot, am);
            }

            if (dragging && ap >= 0 && rotStartVec != Vector3.Zero)
            {
                var refA = rotStartVec;
                var refB = Vector3.Cross(dragAxis, refA);
                float r = ap == PartView ? scale * ViewRingR : scale;
                if (Modern) Pie(tr, cam, pos, refA, refB, r, angle, am);
                else
                {
                    tr.AddArcFan(pos, refA, refB, r, 0, angle, Fade(HotFill, am), 64);
                    Stroke(tr, cam, pos, pos + refA * r, Hot, am, StrokePx * 0.6f);
                    var cur = refA * (float)Math.Cos(angle) + refB * (float)Math.Sin(angle);
                    Stroke(tr, cam, pos, pos + cur * r, Hot, am, StrokePx * 0.6f);
                }
            }
        }

        public static void DrawScale(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] basis, float scale,
                                     int hp, int ap, bool dragging, float am, bool lockXY, bool planes)
        {
            if (CW) { DrawScaleCW(tr, cam, pos, basis, scale, hp, ap, am, lockXY); return; }
            if (Flat) { DrawScaleFlat(tr, cam, pos, basis, scale, hp, ap, dragging, am, lockXY, planes); return; }
            if (Modern) { DrawScaleModern(tr, cam, pos, basis, scale, hp, ap, dragging, am, lockXY, planes); return; }
            bool xyHot = hp == 0 || hp == 1 || ap == 0 || ap == 1;
            bool zHot = hp == 2 || ap == 2;
            bool AxisHot(int i) => lockXY ? (i < 2 ? xyHot : zHot) : (hp == i || ap == i);

            if (dragging && Modern && ap >= 0 && ap < 3)
            {
                if (lockXY && ap < 2) { GuideLine(tr, cam, pos, basis[0], X, am, scale); GuideLine(tr, cam, pos, basis[1], Y, am, scale); }
                else GuideLine(tr, cam, pos, basis[ap], AxisColour(ap, false), am, scale);
            }

            if (planes)
            {
                for (int p = 0; p < 3; p++)
                {
                    if (lockXY && p != 0) continue;
                    bool hot = hp == PartPlane + p || ap == PartPlane + p;
                    var (a, b, _) = PlaneAxes(basis, p);
                    PlaneHandle(tr, cam, pos, a, b, PlaneNormalIndex(p),
                        AxisColour(PlaneAxisIndexA(p), hot), AxisColour(PlaneAxisIndexB(p), hot), scale, hot, am);
                }
            }

            foreach (int i in DepthOrder(cam, pos, basis, scale))
            {
                bool hot = AxisHot(i);
                var col = AxisColour(i, hot);
                var axis = basis[i];
                float start = Modern ? scale * 0.16f : scale * 0.14f;
                Stroke(tr, cam, pos + axis * start, pos + axis * (scale * 0.9f), col, am, StrokePx, hot && Modern);
                Cube(tr, cam, pos + axis * scale, basis[0], basis[1], basis[2], scale * HeadR, col, am, hot);
            }

            if (lockXY && !Modern)
            {
                float o = scale * 0.55f;
                var mid = pos + (basis[0] + basis[1]) * (o * 0.5f);
                Stroke(tr, cam, pos + basis[0] * o, mid, AxisColour(0, xyHot), am, StrokePx * 0.75f);
                Stroke(tr, cam, pos + basis[1] * o, mid, AxisColour(1, xyHot), am, StrokePx * 0.75f);
            }

            bool chot = hp == PartCentre || ap == PartCentre;
            Cube(tr, cam, pos, basis[0], basis[1], basis[2], scale * (Modern ? 0.085f : 0.07f), chot ? Hot : Centre, am, chot);
        }

        private static int[] DepthOrder(Camera cam, Vector3 pos, Vector3[] basis, float scale)
        {
            var fwd = cam.GetForward();
            float d0 = Vector3.Dot(basis[0] * scale, fwd), d1 = Vector3.Dot(basis[1] * scale, fwd), d2 = Vector3.Dot(basis[2] * scale, fwd);
            var order = new[] { 0, 1, 2 };
            var d = new[] { d0, d1, d2 };
            Array.Sort(d, order);
            Array.Reverse(order);
            return order;
        }

        private static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(c.X, c.Y, c.Z, c.W));

        public static bool Project(Camera cam, Vector3 world, out System.Numerics.Vector2 px)
        {
            px = default;
            var clip = Vector4.Transform(new Vector4(world, 1.0f), cam.ViewProjMatrix);
            if (clip.W <= 0.01f) return false;
            float nx = clip.X / clip.W, ny = clip.Y / clip.W;
            if (nx < -1.2f || nx > 1.2f || ny < -1.2f || ny > 1.2f) return false;
            var ds = ImGui.GetIO().DisplaySize;
            px = new System.Numerics.Vector2((nx + 1.0f) * 0.5f * ds.X, (1.0f - ny) * 0.5f * ds.Y);
            return true;
        }

        public static void AxisLetters(Camera cam, Vector3 pos, Vector3[] basis, float scale, int hp, int ap, float am, bool[] show = null)
        {
            if (CW) return;
            if (Flat) { AxisLettersFlat(cam, pos, basis, scale, hp, ap, am, show); return; }
            if (Modern) { AxisLettersModern(cam, pos, basis, scale, hp, ap, am, show); return; }
            if (!ScreenText || am < 0.99f || !Modern) return;
            var dl = ImGui.GetBackgroundDrawList();
            string[] names = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                if (show != null && !show[i]) continue;
                bool hot = hp == i || ap == i;
                if (!Project(cam, pos + basis[i] * (scale * 1.16f), out var p)) continue;
                var size = ImGui.CalcTextSize(names[i]);
                p.X -= size.X * 0.5f; p.Y -= size.Y * 0.5f;
                var col = AxisColour(i, hot);
                uint dark = U32(new Vector4(0, 0, 0, 0.85f));
                dl.AddText(new System.Numerics.Vector2(p.X + 1, p.Y + 1), dark, names[i]);
                dl.AddText(new System.Numerics.Vector2(p.X - 1, p.Y + 1), dark, names[i]);
                dl.AddText(new System.Numerics.Vector2(p.X + 1, p.Y - 1), dark, names[i]);
                dl.AddText(new System.Numerics.Vector2(p.X - 1, p.Y - 1), dark, names[i]);
                dl.AddText(p, U32(col), names[i]);
                if (hot) dl.AddText(new System.Numerics.Vector2(p.X + 0.5f, p.Y), U32(col), names[i]);
            }
        }

        public static void Readout(string text, Vector4 accent, Camera cam, Vector3 anchorWorld, float am)
        {
            if (!ScreenText || am < 0.99f || string.IsNullOrEmpty(text)) return;
            var io = ImGui.GetIO();
            var m = io.MousePos;
            if (m.X < -1e5f || m.Y < -1e5f || m.X < 0 || m.Y < 0)
            {
                if (!Project(cam, anchorWorld, out m)) return;
                m.Y += AxisPx * 0.35f;
            }
            var dl = ImGui.GetBackgroundDrawList();
            var size = ImGui.CalcTextSize(text);
            var p0 = new System.Numerics.Vector2(m.X + 18, m.Y + 18);
            var ds = io.DisplaySize;
            if (p0.X + size.X + 12 > ds.X) p0.X = m.X - size.X - 24;
            if (p0.Y + size.Y + 8 > ds.Y) p0.Y = m.Y - size.Y - 24;
            var p1 = new System.Numerics.Vector2(p0.X + size.X + 12, p0.Y + size.Y + 8);
            dl.AddRectFilled(new System.Numerics.Vector2(p0.X + 1.5f, p0.Y + 1.5f), new System.Numerics.Vector2(p1.X + 1.5f, p1.Y + 1.5f), U32(new Vector4(0, 0, 0, 0.35f)), 4f);
            dl.AddRectFilled(p0, p1, U32(new Vector4(0.06f, 0.08f, 0.12f, 0.88f)), 4f);
            dl.AddRect(p0, p1, U32(new Vector4(accent.X, accent.Y, accent.Z, 0.8f)), 4f);
            dl.AddText(new System.Numerics.Vector2(p0.X + 6, p0.Y + 4), U32(new Vector4(0.96f, 0.96f, 0.98f, 1f)), text);
        }

        public static void WorldLabel(Camera cam, Vector3 world, string text, Vector4 col, float am)
        {
            if (CW) return;
            if (!ScreenText || am < 0.99f || string.IsNullOrEmpty(text)) return;
            if (!Project(cam, world, out var p)) return;
            var dl = ImGui.GetBackgroundDrawList();
            var size = ImGui.CalcTextSize(text);
            var p0 = new System.Numerics.Vector2(p.X - size.X * 0.5f - 5, p.Y - size.Y * 0.5f - 3);
            var p1 = new System.Numerics.Vector2(p.X + size.X * 0.5f + 5, p.Y + size.Y * 0.5f + 3);
            dl.AddRectFilled(p0, p1, U32(new Vector4(0.06f, 0.08f, 0.12f, 0.85f)), 4f);
            dl.AddRect(p0, p1, U32(new Vector4(col.X, col.Y, col.Z, 0.8f)), 4f);
            dl.AddText(new System.Numerics.Vector2(p0.X + 5, p0.Y + 3), U32(col), text);
        }

        public static string FormatDelta(Vector3 d) =>
            $"dX {d.X:+0.000;-0.000;0.000}  dY {d.Y:+0.000;-0.000;0.000}  dZ {d.Z:+0.000;-0.000;0.000} m";
        public static string FormatAngle(float rad) => $"{MathUtil.RadiansToDegrees(rad):+0.0;-0.0;0.0} deg";
        public static string FormatScale(float f) => $"x {f:0.000}";
    }
}

