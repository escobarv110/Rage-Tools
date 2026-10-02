using System;
using System.Collections.Generic;
using CodeWalker;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public static readonly Vector4 SelBoxGreen_U24 = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        public static readonly Vector4 HoverWhite_U25 = new Vector4(1.0f, 1.0f, 1.0f, 0.9f);
        public const float SelBoxPx_U24 = 2.0f;
        public const float HelperPx_U25 = 1.5f;
        public const float MloMarkerMinPx_U25 = 9.0f;
        private const int SelBoxEdgeSteps_U24 = 12;

        private readonly List<(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx)> selOverlay_U24 =
            new List<(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx)>();
        private readonly List<(Vector3 a, Vector3 b, Vector4 c, bool depth, float px)> selSegs_U25 =
            new List<(Vector3 a, Vector3 b, Vector4 c, bool depth, float px)>();
        private readonly List<(Vector3 a, Vector3 b, Vector4 c)> takeTmp_U25 = new List<(Vector3 a, Vector3 b, Vector4 c)>();

        public int SelOverlayCount_U24 => selOverlay_U24.Count;
        private bool l4Deferred_U25;
        public int SelSegCount_U25 => selSegs_U25.Count;

        private void QueueSelectionBox_U24(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx) =>
            selOverlay_U24.Add((pos, ori, mn, mx));

        private static readonly Vector3 SelHue_U25 = new Vector3(1.0f, 0.78f, 0.30f);

        public static Vector4 Ldr_U25(Vector4 c)
        {
            float m = Math.Max(c.X, Math.Max(c.Y, c.Z));
            if (m < 1e-4f) return c;
            var r = new Vector3(c.X / m, c.Y / m, c.Z / m);
            if ((r - SelHue_U25).Length() < 0.03f) return new Vector4(0f, 1f, 0f, Math.Min(c.W, 1f));
            return new Vector4(r, Math.Min(c.W, 1f));
        }

        private void CaptureLines_U25(int startLine, bool depth, float px, Vector4? force = null)
        {
            takeTmp_U25.Clear();
            lineRenderer.TakeSince(startLine, takeTmp_U25);
            foreach (var t in takeTmp_U25)
            {
                var c = force.HasValue ? new Vector4(force.Value.X, force.Value.Y, force.Value.Z, force.Value.W * Math.Min(t.c.W, 1f)) : Ldr_U25(t.c);
                selSegs_U25.Add((t.a, t.b, c, depth, px));
            }
        }

        private static bool OnTop_U25(in WorldSelection s, WorldSelectionMode mode) =>
            s.MloEntityDef != null || mode == WorldSelectionMode.MloInstance || mode == WorldSelectionMode.NavMesh ||
            mode == WorldSelectionMode.WaterQuad || mode == WorldSelectionMode.CalmingQuad || mode == WorldSelectionMode.WaveQuad;

        private float MloMarkerHalf_U25(Vector3 p) => Math.Max(1.5f, MloMarkerMinPx_U25 * camera.WorldPerPixel(p));

        private void DrawSelectionOverlay_U24(SharpDX.Direct3D11.DeviceContext context)
        {
            if (l4Deferred_U25)
            {
                l4Deferred_U25 = false;
                L4Tris.MapColours_U25(Ldr_U25);
                L4Tris.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthDisabled);
                var faces = L4Faces_U26;
                faces.RasterOverride_U26 = CommonStates.RasterSolidCullBack;
                faces.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthDisabled);
                faces.RasterOverride_U26 = null;
            }
            if (selOverlay_U24.Count == 0 && selSegs_U25.Count == 0 && LightIconCount_U26 == 0) return;
            bool depth = deviceResources.BeginBackbufferWithDepth_U24();
            foreach (var b in selOverlay_U24) AddSelectionBoxEdges_U24(b.pos, b.ori, b.mn, b.mx);
            foreach (var s in selSegs_U25) if (s.depth) AddSeg_U25(s.a, s.b, s.c, s.px);
            selOverlay_U24.Clear();
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha,
                depth ? CommonStates.DepthReadOnly : CommonStates.DepthDisabled);
            deviceResources.BeginBackbuffer();
            foreach (var s in selSegs_U25) if (!s.depth) AddSeg_U25(s.a, s.b, s.c, s.px);
            AddLightIcons_U26(triRenderer);
            selSegs_U25.Clear();
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthDisabled);
        }

        private void AddSeg_U25(Vector3 a, Vector3 b, Vector4 col, float px)
        {
            var cp = camera.Position;
            float wa = camera.WorldPerPixel(a), wb = camera.WorldPerPixel(b);
            int steps = Math.Max(wa, wb) > Math.Min(wa, wb) * 1.3f ? SelBoxEdgeSteps_U24 : 1;
            for (int s = 0; s < steps; s++)
            {
                var p0 = Vector3.Lerp(a, b, s / (float)steps);
                var p1 = Vector3.Lerp(a, b, (s + 1) / (float)steps);
                float wpp = camera.WorldPerPixel((p0 + p1) * 0.5f);
                triRenderer.AddThickLineAA(p0, p1, cp, px * 0.5f * wpp, 0.75f * wpp, col);
            }
        }

        private void AddSelectionBoxEdges_U24(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx)
        {
            Vector3 W(float x, float y, float z) => pos + ori.Multiply(new Vector3(x, y, z));
            var c = new[]
            {
                W(mn.X, mn.Y, mn.Z), W(mx.X, mn.Y, mn.Z), W(mx.X, mx.Y, mn.Z), W(mn.X, mx.Y, mn.Z),
                W(mn.X, mn.Y, mx.Z), W(mx.X, mn.Y, mx.Z), W(mx.X, mx.Y, mx.Z), W(mn.X, mx.Y, mx.Z),
            };
            int[] e = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < e.Length; i += 2) AddSeg_U25(c[e[i]], c[e[i + 1]], SelBoxGreen_U24, SelBoxPx_U24);
        }
    }
}
