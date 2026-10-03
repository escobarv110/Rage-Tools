using System;
using System.Collections.Generic;
using ImGuiNET;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private static readonly Color4 ClearHdr_U28 = new Color4(0.065f, 0.07f, 0.085f, 1.0f);
        public static readonly Vector3 MaxBackground_U28 = new Vector3(0x36 / 255.0f, 0x36 / 255.0f, 0x36 / 255.0f);
        private static readonly Vector4 GridMinor_U28 = new Vector4(0x42 / 255.0f, 0x42 / 255.0f, 0x42 / 255.0f, 1.0f);
        private static readonly Vector4 GridMajor_U28 = new Vector4(0x60 / 255.0f, 0x60 / 255.0f, 0x60 / 255.0f, 1.0f);
        private static readonly Vector4 GridAxisX_U28 = new Vector4(0.80f, 0.24f, 0.24f, 1.0f);
        private static readonly Vector4 GridAxisY_U28 = new Vector4(0.22f, 0.70f, 0.24f, 1.0f);
        private const int GridMajorEvery_U28 = 10;

        private bool skyDrawn_U28;

        public bool MaxBackgroundOn_U28 { get; private set; }
        public float GridSpacing_U28 { get; private set; } = 1.0f;

        private void SetMaxBackground_U28()
        {
            MaxBackgroundOn_U28 = panel != null && !panel.WorldMode && !skyDrawn_U28 && !RpfExplorerOnly_Q1;
            postFx.Vars.MaxBackground = MaxBackgroundOn_U28 ? new Vector4(MaxBackground_U28, 1.0f) : Vector4.Zero;
            postFx.Vars.ClearColour = new Vector4(ClearHdr_U28.Red, ClearHdr_U28.Green, ClearHdr_U28.Blue, 0.0f);
        }

        private Rendering.GridRenderer_U28 gridRenderer_U28;
        private bool gridWanted_U28;
        public int GridDrawn_U28 { get; private set; }

        private void QueueMaxGrid_U28() => gridWanted_U28 = true;

        private void DrawGridOverlay_U28(SharpDX.Direct3D11.DeviceContext context)
        {
            if (!gridWanted_U28) return;
            gridWanted_U28 = false;
            gridRenderer_U28 ??= new Rendering.GridRenderer_U28(deviceResources.Device);
            if (!gridRenderer_U28.Ready) return;
            var eye = camera.Position;
            float d = Math.Max(Math.Abs(eye.Z), 0.25f);
            float s = (float)Math.Pow(10.0, Math.Floor(Math.Log10(Math.Max(d, 0.1) / 8.0)));
            s = Math.Max(s, 0.1f);
            GridSpacing_U28 = s;
            float half = Math.Max(400.0f * s, d * 20.0f);
            float fadeStart = Math.Max(d * 2.5f, 12.0f * s);
            gridRenderer_U28.Vars = new Rendering.GridVars_U28
            {
                ViewProj = camera.ViewProjMatrix,
                CamPos = new Vector4(eye, 1.0f),
                Spacing = new Vector4(s, s * GridMajorEvery_U28, half, 0.0f),
                Fade = new Vector4(fadeStart, fadeStart * 1.2f, 0.0f, 0.0f),
                MinorCol = GridMinor_U28,
                MajorCol = GridMajor_U28,
                AxisXCol = GridAxisX_U28,
                AxisYCol = GridAxisY_U28,
            };
            bool depth = deviceResources.BeginBackbufferWithDepth_U24();
            gridRenderer_U28.Draw(context, depth ? CommonStates.DepthReadOnly : CommonStates.DepthDisabled);
            deviceResources.BeginBackbuffer();
            GridDrawn_U28++;
        }

        private static readonly (string name, Vector3 n)[] NamedViews_U28 =
        {
            ("Top", Vector3.UnitZ), ("Bottom", -Vector3.UnitZ), ("Front", -Vector3.UnitY),
            ("Back", Vector3.UnitY), ("Left", -Vector3.UnitX), ("Right", Vector3.UnitX),
        };

        private Vector3 ViewDir_U28(Vector3 world)
        {
            var v = Vector3.TransformNormal(world, camera.ViewMatrix);
            return v;
        }

        public void SetView_U28(Vector3 n)
        {
            Vector3 pivot;
            if (panel.WorldMode) pivot = camera.Target;
            else
            {
                var b = scene.GetSceneBounds();
                pivot = b.HasValue && b.Value.Minimum.X < b.Value.Maximum.X ? (b.Value.Minimum + b.Value.Maximum) * 0.5f : Vector3.Zero;
            }
            float dist = Math.Max(Vector3.Distance(camera.Position, pivot), 2.0f);
            n.Normalize();
            float yaw = Math.Abs(n.X) + Math.Abs(n.Y) < 1e-4f ? camera.Yaw : (float)Math.Atan2(n.Y, n.X);
            float pitch = (float)Math.Asin(MathUtil.Clamp(n.Z, -1.0f, 1.0f));
            camera.Target = pivot;
            camera.Distance = camera.TargetDistance = dist;
            camera.MaxDistance = Math.Max(camera.MaxDistance, dist * 2.0f);
            camera.Yaw = camera.TargetYaw = yaw;
            camera.Pitch = camera.TargetPitch = pitch;
            camera.SnapSmoothing();
            camera.Update();
        }

        private void DrawViewportWidgets_U28()
        {
            if (panel == null || deviceResources == null || photoMode || renderingStill || RpfExplorerOnly_Q1) return;
            if (!panel.ShowInterface || panel.ArchiveMode) return;
            DrawTerrainOverlay_U28();
            var r = panel.ViewRect_U28(deviceResources.Width, deviceResources.Height);
            if (r.Z - r.X < 260 || r.W - r.Y < 200) return;
            DrawViewLabels_U28(r);
            DrawTripod_U28(r);
        }

        private const ImGuiWindowFlags OverlayFlags_U28 =
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoMove;

        private static readonly NVector4 LabelText_U28 = new NVector4(0.84f, 0.84f, 0.84f, 1f);

        private bool ViewLabel_U28(string text, string popup)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new NVector4(0, 0, 0, 0));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NVector4(1, 1, 1, 0.10f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new NVector4(1, 1, 1, 0.16f));
            ImGui.PushStyleColor(ImGuiCol.Text, LabelText_U28);
            bool hit = ImGui.Button(text + "   ##" + popup);
            var mn = ImGui.GetItemRectMin(); var mx = ImGui.GetItemRectMax();
            float cy = (mn.Y + mx.Y) * 0.5f, x = mx.X - 9;
            ImGui.GetWindowDrawList().AddTriangleFilled(new NVector2(x - 3, cy - 1.5f), new NVector2(x + 3, cy - 1.5f), new NVector2(x, cy + 2f),
                ImGui.GetColorU32(LabelText_U28));
            ImGui.PopStyleColor(4);
            if (hit) ImGui.OpenPopup(popup);
            ImGui.SameLine(0, 2);
            return hit;
        }

        private void DrawViewLabels_U28(System.Numerics.Vector4 r)
        {
            ImGui.SetNextWindowPos(new NVector2(r.X + 6, r.Y + 4));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NVector2(2, 2));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new NVector2(5, 2));
            if (ImGui.Begin("##u28viewlabels", OverlayFlags_U28 | ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new NVector4(0, 0, 0, 0));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NVector4(1, 1, 1, 0.10f));
                ImGui.PushStyleColor(ImGuiCol.Text, LabelText_U28);
                if (ImGui.Button("+##u28plus")) ImGui.OpenPopup("u28plus");
                ImGui.PopStyleColor(3);
                ImGui.SameLine(0, 2);
                ViewLabel_U28("Perspective", "u28persp");
                ViewLabel_U28("Standard", "u28std");
                ViewLabel_U28(panel.ShadingName_U28(), "u28shade");

                if (ImGui.BeginPopup("u28plus"))
                {
                    if (ImGui.MenuItem("Frame everything", "F")) { if (!FrameWorldSelection_M3()) FrameModel(); }
                    if (ImGui.MenuItem("Reset view")) panel.RequestResetView_U1 = true;
                    ImGui.EndPopup();
                }
                if (ImGui.BeginPopup("u28persp"))
                {
                    ImGui.MenuItem("Perspective", null, true);
                    ImGui.Separator();
                    foreach (var (name, n) in NamedViews_U28)
                        if (ImGui.MenuItem(name)) SetView_U28(n);
                    ImGui.EndPopup();
                }
                if (ImGui.BeginPopup("u28std"))
                {
                    bool grid = panel.ShowGrid, sky = panel.ShowSky;
                    if (!panel.WorldMode && ImGui.MenuItem("Grid", null, grid)) panel.ShowGrid = !grid;
                    if (ImGui.MenuItem("Sky", null, sky)) panel.ShowSky = !sky;
                    ImGui.EndPopup();
                }
                if (ImGui.BeginPopup("u28shade"))
                {
                    panel.DrawShadingMenuItems_U28();
                    ImGui.EndPopup();
                }
            }
            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        private static uint Col_U28(float r, float g, float b, float a = 1f) => ImGui.GetColorU32(new NVector4(r, g, b, a));

        private void DrawTripod_U28(System.Numerics.Vector4 r)
        {
            var dl = ImGui.GetBackgroundDrawList();
            var o = new NVector2(r.X + 34, r.W - 34);
            const float len = 22.0f;
            var axes = new[] { (Vector3.UnitX, "x", Col_U28(0.86f, 0.30f, 0.30f)), (Vector3.UnitY, "y", Col_U28(0.32f, 0.78f, 0.32f)), (Vector3.UnitZ, "z", Col_U28(0.36f, 0.52f, 0.96f)) };
            var list = new List<(float z, NVector2 end, string label, uint col)>();
            foreach (var (dir, label, col) in axes)
            {
                var v = ViewDir_U28(dir);
                list.Add((v.Z, new NVector2(o.X + v.X * len, o.Y - v.Y * len), label, col));
            }
            list.Sort((a, b) => a.z.CompareTo(b.z));
            foreach (var (z, end, label, col) in list)
            {
                dl.AddLine(o, end, col, 2.0f);
                var dir = end - o;
                float l = Math.Max(dir.Length(), 1e-3f);
                var tip = o + dir * ((l + 7.0f) / l);
                var ts = ImGui.CalcTextSize(label);
                dl.AddText(new NVector2(tip.X - ts.X * 0.5f, tip.Y - ts.Y * 0.5f), col, label);
            }
        }
    }
}
