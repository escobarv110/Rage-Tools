using System;
using System.Numerics;
using ImGuiNET;
using Vector3 = SharpDX.Vector3;
using Vector4 = SharpDX.Vector4;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        partial void SeqTest_U28(Action<string, bool, string> check)
        {
            SliderTypingTest_U28(check);
            NamedViewsTest_U28(check);
            TerrainBrushTest_U28(check);
        }

        private void TerrainBrushTest_U28(Action<string, bool, string> check)
        {
            try
            {
                float F(TerrainEditor.BrushFalloff_U28 k, float t) => TerrainEditor.FalloffCurve_U28(k, t);
                check("terrain brush: falloff curves have Blender's shapes",
                      Math.Abs(F(TerrainEditor.BrushFalloff_U28.Linear, 0.5f) - 0.5f) < 1e-4f &&
                      Math.Abs(F(TerrainEditor.BrushFalloff_U28.Sharp, 0.5f) - 0.25f) < 1e-4f &&
                      Math.Abs(F(TerrainEditor.BrushFalloff_U28.Sphere, 0.5f) - (float)Math.Sqrt(0.75)) < 1e-4f &&
                      Math.Abs(F(TerrainEditor.BrushFalloff_U28.Root, 0.25f) - 0.5f) < 1e-4f &&
                      F(TerrainEditor.BrushFalloff_U28.Constant, 0.99f) == 1.0f &&
                      Math.Abs(F(TerrainEditor.BrushFalloff_U28.Smooth, 0.5f) - 0.5f) < 1e-4f, "");

                float Painted(TerrainEditor.BrushBlend_U28 blend, int passes)
                {
                    var te = MakeGrid_S2();
                    te.BrushRadius = 4.0f; te.BrushHardness = 0.0f; te.BrushStrength = 0.4f; te.Blend_U28 = blend;
                    var p = new Vector3(0, 0, 0);
                    te.BeginStrokeAt(1, p);
                    for (int i = 0; i < passes; i++) { te.StrokeTo(p + new Vector3(0.01f, 0, 0), 1); te.StrokeTo(p, 1); }
                    te.EndStrokeS2();
                    return te.Parts[0].Verts[20 * 41 + 20].Colour1.Z;
                }
                float mix = Painted(TerrainEditor.BrushBlend_U28.Mix, 10);
                float add = Painted(TerrainEditor.BrushBlend_U28.Add, 10);
                check("terrain brush: Mix stops at the strength within one stroke", Math.Abs(mix - 0.4f) < 0.02f, $"{mix:0.000}");
                check("terrain brush: Add keeps building up as the brush passes", add > mix + 0.15f, $"add {add:0.000} vs mix {mix:0.000}");

                var g = MakeGrid_S2();
                g.BrushRadius = 3.0f; g.BrushHardness = 0.0f; g.BrushStrength = 1.0f;
                g.Falloff_U28 = TerrainEditor.BrushFalloff_U28.Constant;
                g.BeginStrokeAt(1, new Vector3(-20, 0, 0)); g.StrokeTo(new Vector3(20, 0, 0), 1); g.EndStrokeS2();
                float Edge() { float worst = 0; var v = g.Parts[0].Verts; for (int x = 0; x <= 40; x++) for (int y = 0; y < 40; y++) worst = Math.Max(worst, Math.Abs(v[y * 41 + x].Colour1.Z - v[(y + 1) * 41 + x].Colour1.Z)); return worst; }
                float hardEdge = Edge();
                var before = Colours_S2(g);
                g.Blend_U28 = TerrainEditor.BrushBlend_U28.Blur;
                g.BrushRadius = 6.0f; g.Falloff_U28 = TerrainEditor.BrushFalloff_U28.Smooth;
                g.BeginStrokeAt(1, new Vector3(-20, 3, 0));
                for (int i = 0; i < 6; i++) { g.StrokeTo(new Vector3(20, 3, 0), 1); g.StrokeTo(new Vector3(-20, 3, 0), 1); }
                g.EndStrokeS2();
                float softEdge = Edge();
                check("terrain brush: Blur softens the edge between layers", softEdge < hardEdge * 0.8f, $"steepest step {hardEdge:0.00} -> {softEdge:0.00}");
                g.History.Undo();
                var after = Colours_S2(g);
                float diff = 0; for (int i = 0; i < after.Length; i++) diff = Math.Max(diff, (after[i] - before[i]).Length());
                check("terrain brush: one Undo takes the blur back", diff < 1e-5f, $"{diff}");

                var s = MakeGrid_S2();
                s.BrushRadius = 4.0f; s.BrushStrength = 1.0f; s.Falloff_U28 = TerrainEditor.BrushFalloff_U28.Constant;
                s.BeginStrokeAt(1, Vector3.Zero); s.StrokeTo(Vector3.Zero, 1); s.EndStrokeS2();
                float on = s.Parts[0].Verts[20 * 41 + 20].Colour1.Z;
                s.BrushStrength = 0.5f;
                s.BeginStrokeAt(0, Vector3.Zero); s.StrokeTo(Vector3.Zero, 0); s.EndStrokeS2();
                float off = s.Parts[0].Verts[20 * 41 + 20].Colour1.Z;
                check("terrain brush: Subtract takes the layer back toward the base", on > 0.9f && off < on - 0.3f, $"{on:0.00} -> {off:0.00}");

                var te0 = TerrainEd;
                float r0 = te0.BrushRadius, s0 = te0.BrushStrength;
                terrainRadialWpp_U28 = 0.05f; terrainRadialFrom_U28 = r0; terrainRadialAnchor_U28 = new SharpDX.Vector2(100, 100); terrainRadial_U28 = 1;
                TerrainRadialMove_U28(200, 100);
                bool sized = Math.Abs(te0.BrushRadius - 5.0f) < 1e-3f;
                CancelTerrainRadial_U28();
                check("terrain brush: F then moving the mouse sizes the brush, Esc puts it back", sized && Math.Abs(te0.BrushRadius - r0) < 1e-5f && terrainRadial_U28 == 0, $"{te0.BrushRadius}");
                terrainRadialFrom_U28 = s0; terrainRadial_U28 = 2;
                TerrainRadialMove_U28(100 + (int)(StrengthPx_U28 * 0.5f), 100);
                bool str = Math.Abs(te0.BrushStrength - 0.5f) < 1e-3f;
                terrainRadial_U28 = 0;
                te0.BrushStrength = s0;
                check("terrain brush: Shift+F sets the strength the same way", str, "");
            }
            catch (Exception ex) { check("terrain brush: the test ran", false, ex.ToString()); }
        }

        private void NamedViewsTest_U28(Action<string, bool, string> check)
        {
            var was = panel.Workspace;
            var wasCam = camera.Capture();
            try
            {
                panel.SwitchWorkspace(LightPanel.Space.Light);
                var b = scene.GetSceneBounds();
                var pivot = b.HasValue && b.Value.Minimum.X < b.Value.Maximum.X ? (b.Value.Minimum + b.Value.Maximum) * 0.5f : SharpDX.Vector3.Zero;
                SetView_U28(SharpDX.Vector3.UnitZ);
                var up = camera.Position - pivot;
                check("views: Top looks straight down on the subject", up.Z > 1.0f && new SharpDX.Vector2(up.X, up.Y).Length() < up.Z * 0.1f, $"{up}");
                SetView_U28(-SharpDX.Vector3.UnitY);
                var fr = camera.Position - pivot;
                check("views: Front looks along +Y from the -Y side", fr.Y < -1.0f && Math.Abs(fr.Z) < Math.Abs(fr.Y) * 0.05f && Math.Abs(fr.X) < Math.Abs(fr.Y) * 0.05f, $"{fr}");
                SetView_U28(SharpDX.Vector3.UnitX);
                var rt = camera.Position - pivot;
                check("views: Right looks from +X", rt.X > 1.0f && Math.Abs(rt.Y) < rt.X * 0.05f, $"{rt}");
                check("max view: the empty viewport gets the 3ds Max grey", Math.Abs(MaxBackground_U28.X - 0x36 / 255.0f) < 1e-4f, "");
            }
            finally
            {
                camera.Restore(wasCam);
                panel.SwitchWorkspace(was);
            }
        }

        private static void SliderTypingTest_U28(Action<string, bool, string> check)
        {
            var was = ImGui.GetCurrentContext();
            var ctx = ImGui.CreateContext();
            try
            {
                ImGui.SetCurrentContext(ctx);
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2(800, 600);
                io.DeltaTime = 1.0f / 60.0f;
                io.Fonts.AddFontDefault();
                io.Fonts.GetTexDataAsRGBA32(out IntPtr _, out int _, out int _, out int _);
                float v = 3.0f;
                int iv = 7;
                Vector2 sliderPos = default, intPos = default;
                bool typingSeen = false;

                void Frame(Action input)
                {
                    input?.Invoke();
                    ImGui.NewFrame();
                    ImGui.SetNextWindowPos(new Vector2(10, 10));
                    ImGui.SetNextWindowSize(new Vector2(400, 200));
                    ImGui.Begin("u28 slider test");
                    ImGui.SetNextItemWidth(200);
                    UiSlider_U28.Float("Radius", ref v, 0.0f, 100.0f, "%.1f m");
                    sliderPos = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
                    ImGui.SetNextItemWidth(200);
                    UiSlider_U28.Int("Detail", ref iv, 0, 10);
                    intPos = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * 0.5f;
                    typingSeen |= UiSlider_U28.Typing;
                    ImGui.End();
                    ImGui.EndFrame();
                }

                void DoubleClick(Vector2 at)
                {
                    Frame(() => io.AddMousePosEvent(at.X, at.Y));
                    Frame(() => io.AddMouseButtonEvent(0, true));
                    Frame(() => io.AddMouseButtonEvent(0, false));
                    Frame(() => io.AddMouseButtonEvent(0, true));
                    Frame(() => io.AddMouseButtonEvent(0, false));
                    Frame(null);
                    Frame(null);
                }

                void Type(string s)
                {
                    Frame(() => { io.AddKeyEvent(ImGuiKey.ModCtrl, true); io.AddKeyEvent(ImGuiKey.A, true); });
                    Frame(() => { io.AddKeyEvent(ImGuiKey.A, false); io.AddKeyEvent(ImGuiKey.ModCtrl, false); });
                    foreach (var ch in s) Frame(() => io.AddInputCharacter(ch));
                    Frame(() => io.AddKeyEvent(ImGuiKey.Enter, true));
                    Frame(() => io.AddKeyEvent(ImGuiKey.Enter, false));
                    Frame(null);
                    Frame(null);
                }

                Frame(null);
                Frame(null);
                DoubleClick(sliderPos);
                check("u28 typing: a double-click turns the slider into a number box", typingSeen, $"value {v:0.###}");
                check("u28 typing: ...without the first click moving the value", Math.Abs(v - 3.0f) < 0.001f, $"{v:0.###}");
                Type("42.5");
                check("u28 typing: the typed number lands", Math.Abs(v - 42.5f) < 0.001f, $"{v:0.###}");
                check("u28 typing: ...and Enter hands it back to a slider", !UiSlider_U28.Typing, "");
                typingSeen = false;
                DoubleClick(intPos);
                Type("250");
                check("u28 typing: whole-number sliders type too, kept in range", typingSeen && iv == 10, $"{iv}");
            }
            catch (Exception ex) { check("u28 typing: the slider test ran", false, ex.Message); }
            finally
            {
                ImGui.DestroyContext(ctx);
                ImGui.SetCurrentContext(was);
            }
        }
    }
}
