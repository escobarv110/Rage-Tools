using System.Collections.Generic;
using CodeWalker.GameFiles;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private static readonly Vector4 NavFill_U28 = new Vector4(0.0f, 1.0f, 0.0f, 0.32f);
        private readonly List<BasePathData> sdNavBatch_U28 = new List<BasePathData>();
        private readonly List<(Vector3 a, Vector3 b, Vector3 c)> navFill_U28 = new List<(Vector3, Vector3, Vector3)>();
        private bool navOverlayPending_U28;

        public int NavOverlayDrawn_U28 { get; private set; }

        private void QueueNavPolyFill_U28(YnvPoly poly)
        {
            var ynv = poly?.Ynv;
            if (ynv?.Vertices == null || ynv.Indices == null) return;
            int ic = poly._RawData.IndexCount;
            int startid = poly._RawData.IndexID;
            int vc = ynv.Vertices.Count;
            if (startid >= ynv.Indices.Count || startid + ic > ynv.Indices.Count) return;
            int startind = ynv.Indices[startid];
            if (startind >= vc) return;
            var v0 = ynv.Vertices[startind];
            for (int t = 0; t < ic - 2; t++)
            {
                int ind1 = ynv.Indices[startid + t + 1];
                int ind2 = ynv.Indices[startid + t + 2];
                if (ind1 >= vc || ind2 >= vc) continue;
                navFill_U28.Add((v0, ynv.Vertices[ind1], ynv.Vertices[ind2]));
            }
        }

        private void DrawNavOverlay_U28(SharpDX.Direct3D11.DeviceContext context)
        {
            if (!navOverlayPending_U28 || pathBatch == null) return;
            navOverlayPending_U28 = false;
            bool depth = deviceResources.BeginBackbufferWithDepth_U24();
            pathBatch.Draw(context, camera.ViewProjMatrix, camera.Position, sdNavBatch_U28,
                depth: depth ? CommonStates.DepthReadOnly : CommonStates.DepthDisabled);
            pathBatch.EndFrame();
            deviceResources.BeginBackbuffer();
            NavOverlayDrawn_U28++;
        }
    }
}
