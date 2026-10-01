using System;
using SharpDX;
using RageLightEditor.Editor;
using CodeWalker;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private const float SelBracket_V19 = 0.18f;
        private const float SelOutlineScale_V19 = 1.012f;

        private static readonly Vector4 SelShadow_V19 = new Vector4(0.05f, 0.03f, 0.0f, 0.85f);

        private void DrawSelectionBox_V19(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx, Vector4 col, bool full)
        {
            if (!full)
            {
                DrawOrientedBox(pos, ori, mn, mx, col);
                return;
            }

            DrawOrientedBox(pos, ori, mn, mx, col);
        }

        private void DrawEntityBox_V19(CodeWalker.GameFiles.YmapEntityDef e, Vector4 col, bool full)
        {
            if (e == null) return;
            if (!full) { DrawEntityBox(e, col); return; }
            var arche = e.Archetype;
            Vector3 mn, mx;
            if (arche != null && arche.BBMax.X > arche.BBMin.X) { mn = arche.BBMin * e.Scale; mx = arche.BBMax * e.Scale; }
            else { var h = new Vector3(0.5f); mn = -h; mx = h; }
            DrawSelectionBox_V19(e.Position, e.Orientation, mn, mx, col, true);
        }

        private void DrawSelectionBrackets_V19(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx, Vector4 col)
        {
            var size = mx - mn;
            float lx = Math.Min(size.X * SelBracket_V19, size.X * 0.45f);
            float ly = Math.Min(size.Y * SelBracket_V19, size.Y * 0.45f);
            float lz = Math.Min(size.Z * SelBracket_V19, size.Z * 0.45f);

            Vector3 W(float x, float y, float z) => pos + ori.Multiply(new Vector3(x, y, z));

            for (int i = 0; i < 8; i++)
            {
                bool hx = (i & 1) != 0, hy = (i & 2) != 0, hz = (i & 4) != 0;
                float x = hx ? mx.X : mn.X;
                float y = hy ? mx.Y : mn.Y;
                float z = hz ? mx.Z : mn.Z;
                var o = W(x, y, z);
                lineRenderer.AddLine(o, W(hx ? x - lx : x + lx, y, z), col);
                lineRenderer.AddLine(o, W(x, hy ? y - ly : y + ly, z), col);
                lineRenderer.AddLine(o, W(x, y, hz ? z - lz : z + lz), col);
            }
        }
    }
}

