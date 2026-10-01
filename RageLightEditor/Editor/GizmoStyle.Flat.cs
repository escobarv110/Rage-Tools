using System;
using ImGuiNET;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public static partial class GizmoStyle
    {

        public enum GizmoLook { Modern = 0, Shaded = 1, Classic = 2, CodeWalker = 3 }

        public static GizmoLook Look = GizmoLook.Modern;
        public static bool Flat => Look == GizmoLook.Modern;
        public static bool Modern => Look != GizmoLook.Classic;

        public static GizmoLook LookFromSettings(AppSettings s)
        {
            switch (s?.GizmoStyleIndex ?? 0)
            {
                case 1: return GizmoLook.Shaded;
                case 2: return GizmoLook.Classic;
                case 3: return GizmoLook.CodeWalker;
                default: return GizmoLook.Modern;
            }
        }

        public static void SetDebugLook(int code)
        {
            Look = code == 0 ? GizmoLook.Classic : (code == 2 ? GizmoLook.Shaded : (code == 3 ? GizmoLook.CodeWalker : GizmoLook.Modern));
        }

        private const float FLinePx = 3.5f;
        private const float FRimPx = 0.75f;
        private const float FHeadLen = 0.20f, FHeadHalfW = 0.085f;
        private const float FSquareR = 0.048f, FCentreSquareR = 0.062f;
        private const float FRingPx = 2.0f, FViewRingPx = 1.5f, FCentrePx = 1.8f;
        private const float FDimOther = 0.5f;
        private const float FBackAlpha = 0.30f;

        private static readonly Vector4 FRim = new Vector4(0.02f, 0.02f, 0.03f, 0.72f);
        private static readonly Vector4 FHot = new Vector4(1.00f, 1.00f, 0.00f, 1f);
        private static readonly Vector4 FCentre = new Vector4(0.96f, 0.96f, 0.98f, 1f);
        private static readonly Vector4 FViewRing = new Vector4(0.95f, 0.95f, 0.98f, 0.80f);

        private static Vector4 FAxisColour(int axis, bool hot) => hot ? FHot : AxisColour(axis, false);

        private static void FLine(TriRenderer tr, Camera cam, Vector3 a, Vector3 b, Vector4 col, float widthPx, float alphaMul, bool rim = true)
        {
            var mid = (a + b) * 0.5f;
            float wpp = cam.WorldPerPixel(mid);
            float f = FeatherPx * wpp;
            var cp = cam.Position;
            if (rim) tr.AddCapsuleAA(a, b, cp, (widthPx * 0.5f + FRimPx) * wpp, f, Fade(FRim, alphaMul));
            tr.AddCapsuleAA(a, b, cp, widthPx * 0.5f * wpp, f, Fade(col, alphaMul));
        }

        private static Vector3 ScreenSide(Camera cam, Vector3 at, Vector3 dir)
        {
            var toCam = cam.Position - at;
            var side = Vector3.Cross(dir, toCam);
            if (side.LengthSquared() < 1e-12f) side = Vector3.Cross(dir, Vector3.UnitZ);
            if (side.LengthSquared() < 1e-12f) side = Vector3.Cross(dir, Vector3.UnitX);
            if (side.LengthSquared() < 1e-12f) return Vector3.UnitY;
            side.Normalize();
            return side;
        }

        private static void FPoly(TriRenderer tr, Camera cam, Vector3[] pts, Vector4 col, float alphaMul, float rimPx = FRimPx)
        {
            if (pts == null || pts.Length < 3) return;
            float wpp = cam.WorldPerPixel(pts[0]);
            float f = FeatherPx * wpp;
            if (rimPx > 0f) tr.AddConvexPolyAA(TriRenderer.OffsetConvexPoly(pts, rimPx * wpp), f, Fade(FRim, alphaMul));
            tr.AddConvexPolyAA(pts, f, Fade(col, alphaMul));
        }

        private static void FArrowTip(TriRenderer tr, Camera cam, Vector3 tip, Vector3 baseCentre, float halfWidth, Vector4 col, float alphaMul)
        {
            var axis = tip - baseCentre;
            float len = axis.Length();
            if (len < 1e-7f) return;
            var n = axis / len;
            var view = cam.Position - baseCentre;
            if (view.LengthSquared() > 1e-12f) view.Normalize();
            float facing = Math.Abs(Vector3.Dot(n, view));
            var side = ScreenSide(cam, baseCentre, n);
            if (facing > 0.985f)
            {
                var (r, u, _) = ScreenBasis(cam);
                float wpp = cam.WorldPerPixel(tip);
                float f = FeatherPx * wpp;
                tr.AddDiscAA(tip, r, u, halfWidth + FRimPx * wpp, f, Fade(FRim, alphaMul), 40);
                tr.AddDiscAA(tip, r, u, halfWidth, f, Fade(col, alphaMul), 40);
                return;
            }
            var pts = new[] { tip, baseCentre + side * halfWidth, baseCentre - side * halfWidth };
            FPoly(tr, cam, pts, col, alphaMul);
        }

        private static void FSquare(TriRenderer tr, Camera cam, Vector3 c, float half, Vector4 col, float alphaMul)
        {
            var (r, u, _) = ScreenBasis(cam);
            var pts = new[] { c - r * half - u * half, c + r * half - u * half, c + r * half + u * half, c - r * half + u * half };
            FPoly(tr, cam, pts, col, alphaMul);
        }

        private static void FRing(TriRenderer tr, Camera cam, Vector3 centre, Vector3 axisA, Vector3 axisB, float radius,
                                  Vector4 col, float widthPx, float alphaMul, bool dimBack, int segments)
        {
            float wpp = cam.WorldPerPixel(centre);
            float f = FeatherPx * wpp;
            var cp = cam.Position;
            var toCam = cp - centre;
            float toCamLen = Math.Max(toCam.Length(), 1e-6f);
            Func<float, float> back = ang =>
            {
                if (!dimBack) return 1f;
                var p = axisA * (float)Math.Cos(ang) + axisB * (float)Math.Sin(ang);
                float d = Vector3.Dot(p, toCam) / toCamLen;
                return FBackAlpha + (1f - FBackAlpha) * Math.Clamp(d * 4f + 0.5f, 0f, 1f);
            };
            const float TwoPi = (float)(Math.PI * 2.0);
            var rc = Fade(FRim, alphaMul);
            var cc = Fade(col, alphaMul);
            tr.AddThickArcAA(centre, axisA, axisB, radius, 0f, TwoPi, cp, (widthPx * 0.5f + FRimPx) * wpp, f, ang => Fade(rc, back(ang)), segments);
            tr.AddThickArcAA(centre, axisA, axisB, radius, 0f, TwoPi, cp, widthPx * 0.5f * wpp, f, ang => Fade(cc, back(ang)), segments);
        }

        private static void FArc(TriRenderer tr, Camera cam, Vector3 centre, Vector3 axisA, Vector3 axisB, float radius,
                                 float a0, float a1, Vector4 col, float widthPx, float alphaMul)
        {
            float wpp = cam.WorldPerPixel(centre);
            float f = FeatherPx * wpp;
            tr.AddThickArcAA(centre, axisA, axisB, radius, a0, a1, cam.Position, (widthPx * 0.5f + FRimPx) * wpp, f, Fade(FRim, alphaMul), 128);
            tr.AddThickArcAA(centre, axisA, axisB, radius, a0, a1, cam.Position, widthPx * 0.5f * wpp, f, Fade(col, alphaMul), 128);
        }

        private static void FCentreCircle(TriRenderer tr, Camera cam, Vector3 pos, float scale, bool hot, float alphaMul)
        {
            var (r, u, _) = ScreenBasis(cam);
            float rad = scale * CentreR;
            float wpp = cam.WorldPerPixel(pos);
            if (hot) tr.AddDiscAA(pos, r, u, rad, FeatherPx * wpp, Fade(A(FHot, 0.30f), alphaMul), 48);
            FRing(tr, cam, pos, r, u, rad, hot ? FHot : FCentre, hot ? FCentrePx + 0.6f : FCentrePx, alphaMul, false, 56);
        }

        private static void FPlaneHandle(TriRenderer tr, Camera cam, Vector3 pos, Vector3 a, Vector3 b, int normalAxis,
                                         float scale, bool hot, float alphaMul)
        {
            float o0 = scale * PlaneMin, o1 = scale * PlaneMax;
            Vector3 p00 = pos + a * o0 + b * o0, p10 = pos + a * o1 + b * o0, p11 = pos + a * o1 + b * o1, p01 = pos + a * o0 + b * o1;
            float wpp = cam.WorldPerPixel(p11);
            var nc = AxisColour(normalAxis, false);
            var fill = hot ? A(FHot, 0.45f) : A(nc, 0.24f);
            var border = hot ? Lighten(FHot, 0.3f) : Lighten(nc, 0.25f);
            Vector3[] q = { p00, p10, p11, p01 };
            tr.AddQuadAA(p00, p10, p11, p01, FeatherPx * wpp, Fade(fill, alphaMul));
            tr.AddThickPolylineAA(q, cam.Position, (0.6f + 0.7f) * wpp, FeatherPx * wpp, Fade(A(FRim, 0.55f), alphaMul), true);
            tr.AddThickPolylineAA(q, cam.Position, 0.6f * wpp, FeatherPx * wpp, Fade(border, alphaMul), true);
        }

        private static void FPie(TriRenderer tr, Camera cam, Vector3 centre, Vector3 refA, Vector3 refB, float radius, float angle, float alphaMul)
        {
            float wpp = cam.WorldPerPixel(centre);
            var fill = Fade(A(FHot, 0.20f), alphaMul);
            tr.AddFanAA(centre, refA, refB, radius, 0f, angle, FeatherPx * wpp, fill, fill, 160);
            var cur = refA * (float)Math.Cos(angle) + refB * (float)Math.Sin(angle);
            FLine(tr, cam, centre, centre + refA * radius, A(FCentre, 0.8f), 1.2f, alphaMul);
            FLine(tr, cam, centre, centre + cur * radius, FHot, 1.8f, alphaMul);
            FArc(tr, cam, centre, refA, refB, radius, 0f, angle, FHot, 2.2f, alphaMul);
        }

        private static void StrokeFlat(TriRenderer tr, Camera cam, Vector3 a, Vector3 b, Vector4 col, float alphaMul, float widthPx)
        {
            FLine(tr, cam, a, b, col, Math.Min(widthPx, FLinePx), alphaMul);
        }

        private static void ArrowHeadFlat(TriRenderer tr, Camera cam, Vector3 tip, Vector3 baseCentre, float radius, Vector4 col, float alphaMul)
        {
            FArrowTip(tr, cam, tip, baseCentre, radius * 1.3f, col, alphaMul);
        }

        private static void DrawTranslateFlat(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] basis, float scale,
                                              int hp, int ap, bool dragging, float am)
        {
            letterRadius = 1.19f;
            if (dragging)
            {
                if (ap >= 0 && ap < 3) GuideLine(tr, cam, pos, basis[ap], AxisColour(ap, false), am, scale);
                else if (ap >= PartPlane && ap < PartPlane + 3)
                {
                    var (a, b, _) = PlaneAxes(basis, ap - PartPlane);
                    GuideLine(tr, cam, pos, a, AxisColour(PlaneAxisIndexA(ap - PartPlane), false), am, scale);
                    GuideLine(tr, cam, pos, b, AxisColour(PlaneAxisIndexB(ap - PartPlane), false), am, scale);
                }
            }
            float Alpha(bool live) => dragging && !live ? am * FDimOther : am;

            for (int p = 0; p < 3; p++)
            {
                bool hot = hp == PartPlane + p || ap == PartPlane + p;
                var (a, b, _) = PlaneAxes(basis, p);
                FPlaneHandle(tr, cam, pos, a, b, PlaneNormalIndex(p), scale, hot, Alpha(hot));
            }

            foreach (int i in DepthOrder(cam, pos, basis, scale))
            {
                bool hot = hp == i || ap == i;
                var col = FAxisColour(i, hot);
                var axis = basis[i];
                var tip = pos + axis * scale;
                var neck = pos + axis * (scale * (1f - FHeadLen));
                float start = scale * (CentreR + 0.06f);
                float alpha = Alpha(hot);
                FLine(tr, cam, pos + axis * start, neck, col, hot ? FLinePx + 0.5f : FLinePx, alpha);
                FArrowTip(tr, cam, tip, neck, scale * FHeadHalfW, col, alpha);
            }

            bool chot = hp == PartCentre || ap == PartCentre;
            FCentreCircle(tr, cam, pos, scale, chot, Alpha(chot));
        }

        private static void DrawRotateFlat(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] basis, float scale,
                                           int hp, int ap, bool dragging, float am, bool[] ringOn, bool viewRing,
                                           Vector3 dragAxis, Vector3 rotStartVec, float angle, float snapDeg)
        {
            var (sr, su, _) = ScreenBasis(cam);
            float Alpha(bool live) => dragging && !live ? am * FDimOther : am;
            letterRadius = viewRing ? ViewRingR + 0.12f : 1.15f;

            if (viewRing)
            {
                bool vhot = hp == PartView || ap == PartView;
                FRing(tr, cam, pos, sr, su, scale * ViewRingR, vhot ? FHot : FViewRing, vhot ? FRingPx + 0.6f : FViewRingPx, Alpha(vhot), false, 160);
                if (vhot && snapDeg > 0.5f && dragging) SnapTicks(tr, cam, pos, sr, su, scale * ViewRingR, snapDeg, FHot, am);
            }

            for (int i = 0; i < 3; i++)
            {
                if (!ringOn[i]) continue;
                bool hot = hp == i || ap == i;
                var a = basis[(i + 1) % 3]; var b = basis[(i + 2) % 3];
                FRing(tr, cam, pos, a, b, scale, FAxisColour(i, hot), hot ? FRingPx + 0.8f : FRingPx, Alpha(hot), !hot, 128);
                if (hot && snapDeg > 0.5f && dragging) SnapTicks(tr, cam, pos, a, b, scale, snapDeg, FHot, am);
            }

            if (dragging && ap >= 0 && rotStartVec != Vector3.Zero)
            {
                var refA = rotStartVec;
                var refB = Vector3.Cross(dragAxis, refA);
                float r = ap == PartView ? scale * ViewRingR : scale;
                FPie(tr, cam, pos, refA, refB, r, angle, am);
            }
        }

        private static void DrawScaleFlat(TriRenderer tr, Camera cam, Vector3 pos, Vector3[] basis, float scale,
                                          int hp, int ap, bool dragging, float am, bool lockXY, bool planes)
        {
            letterRadius = 1.19f;
            bool uniform = ap == PartCentre;
            bool xyHot = hp == 0 || hp == 1 || ap == 0 || ap == 1;
            bool zHot = hp == 2 || ap == 2;
            bool AxisHot(int i) => uniform || (lockXY ? (i < 2 ? xyHot : zHot) : (hp == i || ap == i));
            float Alpha(bool live) => dragging && !live ? am * FDimOther : am;

            if (dragging && ap >= 0 && ap < 3)
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
                    FPlaneHandle(tr, cam, pos, a, b, PlaneNormalIndex(p), scale, hot, Alpha(hot));
                }
            }

            foreach (int i in DepthOrder(cam, pos, basis, scale))
            {
                bool hot = AxisHot(i);
                var col = FAxisColour(i, hot);
                var axis = basis[i];
                float alpha = Alpha(hot);
                FLine(tr, cam, pos + axis * (scale * (FCentreSquareR + 0.06f)), pos + axis * (scale * (1f - FSquareR)), col, hot ? FLinePx + 0.5f : FLinePx, alpha);
                FSquare(tr, cam, pos + axis * scale, scale * FSquareR, col, alpha);
            }

            bool chot = hp == PartCentre || ap == PartCentre;
            FSquare(tr, cam, pos, scale * FCentreSquareR, chot ? FHot : FCentre, Alpha(chot));
        }

        private static void AxisLettersFlat(Camera cam, Vector3 pos, Vector3[] basis, float scale, int hp, int ap, float am, bool[] show)
        {
            if (!ScreenText || am < 0.99f) return;
            var dl = ImGui.GetBackgroundDrawList();
            var font = ImGui.GetFont();
            float baseSize = ImGui.GetFontSize();
            float fs = baseSize * 1.05f;
            string[] names = { "X", "Y", "Z" };
            uint dark = U32(new Vector4(0.02f, 0.02f, 0.03f, 0.90f));
            for (int i = 0; i < 3; i++)
            {
                if (show != null && !show[i]) continue;
                if (ap == i) continue;
                bool hot = hp == i || ap == i;
                if (!Project(cam, pos + basis[i] * (scale * letterRadius), out var p)) continue;
                var size = ImGui.CalcTextSize(names[i]) * (fs / baseSize);
                p.X = (float)Math.Round(p.X - size.X * 0.5f); p.Y = (float)Math.Round(p.Y - size.Y * 0.5f);
                var col = hot ? FHot : AxisColour(i, false);
                for (int k = 0; k < 8; k++)
                {
                    double ang = k * Math.PI / 4.0;
                    var o = new System.Numerics.Vector2(p.X + (float)Math.Cos(ang) * 1.1f, p.Y + (float)Math.Sin(ang) * 1.1f);
                    dl.AddText(font, fs, o, dark, names[i]);
                    dl.AddText(font, fs, new System.Numerics.Vector2(o.X + 0.6f, o.Y), dark, names[i]);
                }
                dl.AddText(font, fs, p, U32(col), names[i]);
                dl.AddText(font, fs, new System.Numerics.Vector2(p.X + 0.6f, p.Y), U32(col), names[i]);
            }
        }
    }
}

