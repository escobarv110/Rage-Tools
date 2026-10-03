using System;
using System.Windows.Forms;
using ImGuiNET;
using RageLightEditor.Editor;
using SharpDX;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private int terrainRadial_U28;
        private Vector2 terrainRadialAnchor_U28, terrainLazy_U28, terrainMouse_U28;
        private float terrainRadialWpp_U28, terrainRadialFrom_U28;

        private const float StrengthPx_U28 = 200.0f;

        public int TerrainRadial_U28 => terrainRadial_U28;

        private void StartTerrainRadial_U28(int mode)
        {
            var te = TerrainEd;
            var p = PointToClient(Cursor.Position);
            var m = new Vector2(p.X, p.Y);
            terrainRadialWpp_U28 = Math.Max(camera.WorldPerPixel(te.CursorOnMesh ? te.CursorPoint : camera.Target), 1e-5f);
            terrainRadialFrom_U28 = mode == 1 ? te.BrushRadius : te.BrushStrength;
            float px = mode == 1 ? te.BrushRadius / terrainRadialWpp_U28 : te.BrushStrength * StrengthPx_U28;
            terrainRadialAnchor_U28 = m - new Vector2(Math.Max(px, 4.0f), 0.0f);
            terrainMouse_U28 = m;
            terrainRadial_U28 = mode;
        }

        private void CancelTerrainRadial_U28()
        {
            if (terrainRadial_U28 == 1) TerrainEd.BrushRadius = terrainRadialFrom_U28;
            else if (terrainRadial_U28 == 2) TerrainEd.BrushStrength = terrainRadialFrom_U28;
            terrainRadial_U28 = 0;
        }

        private bool TerrainRadialMove_U28(int x, int y)
        {
            if (terrainRadial_U28 == 0) return false;
            float px = Vector2.Distance(new Vector2(x, y), terrainRadialAnchor_U28);
            if (terrainRadial_U28 == 1) TerrainEd.BrushRadius = MathUtil.Clamp(px * terrainRadialWpp_U28, 0.05f, 200.0f);
            else TerrainEd.BrushStrength = MathUtil.Clamp(px / StrengthPx_U28, 0.02f, 1.0f);
            return true;
        }

        private bool TerrainStabilized_U28(int x, int y, out Vector3 at)
        {
            at = TerrainEd.CursorPoint;
            var te = TerrainEd;
            if (!te.Stabilize_U28) return te.CursorOnMesh;
            var mouse = new Vector2(x, y);
            var d = mouse - terrainLazy_U28;
            float len = d.Length();
            float r = Math.Max(te.StabilizePx_U28, 1.0f);
            if (len <= r) return false;
            terrainLazy_U28 += d * ((len - r) / len);
            var ray = camera.GetPickRay((int)terrainLazy_U28.X, (int)terrainLazy_U28.Y, deviceResources.Width, deviceResources.Height);
            return te.RayHit(ref ray, out at);
        }

        private void QueueTerrainRing_U28()
        {
            var te = TerrainEd;
            var c = te.CursorPoint;
            float r = te.BrushRadius;
            bool sub = terrainPainting_R4 ? terrainErasing_S2
                     : ((ModifierKeys & (Keys.Alt | Keys.Control)) != 0) != (te.Blend_U28 == TerrainEditor.BrushBlend_U28.Subtract);
            var outer = sub ? new Vector4(0.45f, 0.65f, 1.0f, 1.0f) : new Vector4(1.0f, 1.0f, 1.0f, 0.95f);
            var inner = new Vector4(outer.X, outer.Y, outer.Z, 0.45f);
            const int seg = 64;
            float hard = r * MathUtil.Clamp(te.BrushHardness, 0.0f, 0.98f);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * MathUtil.TwoPi / seg, a1 = (i + 1) * MathUtil.TwoPi / seg;
                var d0 = new Vector3((float)Math.Cos(a0), (float)Math.Sin(a0), 0);
                var d1 = new Vector3((float)Math.Cos(a1), (float)Math.Sin(a1), 0);
                selSegs_U25.Add((c + d0 * r, c + d1 * r, outer, false, 1.6f));
                if (hard > 0.05f && te.Falloff_U28 != TerrainEditor.BrushFalloff_U28.Constant)
                    selSegs_U25.Add((c + d0 * hard, c + d1 * hard, inner, false, 1.0f));
            }
        }

        private void DrawTerrainOverlay_U28()
        {
            if (panel == null || !panel.TerrainMode || !TerrainEd.HasMesh) return;
            var te = TerrainEd;
            var dl = ImGui.GetForegroundDrawList();
            uint white = ImGui.GetColorU32(new NVector4(1, 1, 1, 0.95f));
            uint dim = ImGui.GetColorU32(new NVector4(1, 1, 1, 0.35f));
            if (terrainRadial_U28 != 0)
            {
                var a = new NVector2(terrainRadialAnchor_U28.X, terrainRadialAnchor_U28.Y);
                float px = terrainRadial_U28 == 1 ? te.BrushRadius / terrainRadialWpp_U28 : te.BrushStrength * StrengthPx_U28;
                if (terrainRadial_U28 == 2) dl.AddCircle(a, StrengthPx_U28, dim, 64, 1.0f);
                dl.AddCircle(a, px, white, 64, 1.8f);
                if (terrainRadial_U28 == 2)
                    dl.AddCircleFilled(a, px, ImGui.GetColorU32(new NVector4(1, 1, 1, 0.10f)), 64);
                string label = terrainRadial_U28 == 1 ? $"Radius {te.BrushRadius:0.##} m" : $"Strength {te.BrushStrength:0.00}";
                var ts = ImGui.CalcTextSize(label);
                dl.AddText(new NVector2(a.X - ts.X * 0.5f, a.Y - ts.Y * 0.5f), white, label);
                return;
            }
            if (terrainPainting_R4 && te.Stabilize_U28)
            {
                var lazy = new NVector2(terrainLazy_U28.X, terrainLazy_U28.Y);
                var m = new NVector2(terrainMouse_U28.X, terrainMouse_U28.Y);
                dl.AddLine(lazy, m, dim, 1.5f);
                dl.AddCircleFilled(lazy, 3.0f, white);
            }
        }
    }
}
