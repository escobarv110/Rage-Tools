using System;
using System.Collections.Generic;
using CodeWalker;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public static readonly Vector4 SelBoxGreen_U24 = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        public const float SelBoxPx_U24 = 2.0f;
        private const int SelBoxEdgeSteps_U24 = 12;

        private readonly List<(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx)> selOverlay_U24 =
            new List<(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx)>();

        public int SelOverlayCount_U24 => selOverlay_U24.Count;

        private void QueueSelectionBox_U24(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx) =>
            selOverlay_U24.Add((pos, ori, mn, mx));

        private void DrawSelectionOverlay_U24(SharpDX.Direct3D11.DeviceContext context)
        {
            if (selOverlay_U24.Count == 0) return;
            bool depth = deviceResources.BeginBackbufferWithDepth_U24();
            foreach (var b in selOverlay_U24) AddSelectionBoxEdges_U24(b.pos, b.ori, b.mn, b.mx);
            selOverlay_U24.Clear();
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha,
                depth ? CommonStates.DepthReadOnly : CommonStates.DepthDisabled);
            deviceResources.BeginBackbuffer();
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
            var cp = camera.Position;
            for (int i = 0; i < e.Length; i += 2)
            {
                var a = c[e[i]];
                var b = c[e[i + 1]];
                for (int s = 0; s < SelBoxEdgeSteps_U24; s++)
                {
                    var p0 = Vector3.Lerp(a, b, s / (float)SelBoxEdgeSteps_U24);
                    var p1 = Vector3.Lerp(a, b, (s + 1) / (float)SelBoxEdgeSteps_U24);
                    float wpp = camera.WorldPerPixel((p0 + p1) * 0.5f);
                    triRenderer.AddThickLineAA(p0, p1, cp, SelBoxPx_U24 * 0.5f * wpp, 0.75f * wpp, SelBoxGreen_U24);
                }
            }
        }
    }
}
