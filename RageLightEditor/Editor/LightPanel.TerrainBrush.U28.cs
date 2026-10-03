using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private void DrawTerrainBrushU28()
        {
            var te = Terrain;
            if (te == null) return;

            ImGui.TextDisabled("Mode");
            var names = TerrainEditor.BlendNames_U28;
            float w = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) ImGui.SameLine();
                bool on = (int)te.Blend_U28 == i;
                ImGui.PushStyleColor(ImGuiCol.Button, on ? UiTheme.ButtonOn : UiTheme.ButtonOff);
                if (ImGui.Button(names[i] + "##u28blend", new Vector2(w, 0))) te.Blend_U28 = (TerrainEditor.BrushBlend_U28)i;
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(i switch
                    {
                        0 => "Paints the layer in, up to the strength - going over the same spot in one stroke does not add more.",
                        1 => "Builds the layer up every time the brush passes, like an airbrush.",
                        2 => "Takes the layer away again, back to the base layer.  Hold Ctrl for this with any brush.",
                        _ => "Smooths the edges between layers by mixing each vertex with its neighbours.",
                    });
            }

            int fall = (int)te.Falloff_U28;
            ImGui.SetNextItemWidth(-ImGui.CalcTextSize("Falloff").X - ImGui.GetStyle().ItemInnerSpacing.X - 70);
            if (ImGui.Combo("##u28fall", ref fall, TerrainEditor.FalloffNames_U28, TerrainEditor.FalloffNames_U28.Length))
                te.Falloff_U28 = (TerrainEditor.BrushFalloff_U28)fall;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The shape of the brush from its centre to its edge.");
            ImGui.SameLine();
            DrawFalloffCurveU28(te, new Vector2(60, ImGui.GetFrameHeight()));
            ImGui.SameLine();
            ImGui.TextUnformatted("Falloff");

            bool stab = te.Stabilize_U28;
            if (ImGui.Checkbox("Stabilize stroke", ref stab)) te.Stabilize_U28 = stab;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The brush trails behind the mouse on a string, so lines come out smooth.");
            if (te.Stabilize_U28)
            {
                float px = te.StabilizePx_U28;
                ImGui.SetNextItemWidth(-90);
                if (UiSlider_U28.Float("String##u28stab", ref px, 5.0f, 150.0f, "%.0f px")) te.StabilizePx_U28 = px;
            }
            ImGui.TextDisabled("F size  -  Shift+F strength  -  Ctrl subtract  -  Home frame");
        }

        private static void DrawFalloffCurveU28(TerrainEditor te, Vector2 size)
        {
            var p0 = ImGui.GetCursorScreenPos();
            ImGui.Dummy(size);
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(p0, p0 + size, ImGui.GetColorU32(ImGuiCol.FrameBg), 3.0f);
            const int n = 24;
            float hard = Math.Clamp(te.BrushHardness, 0.0f, 0.98f);
            var prev = Vector2.Zero;
            uint col = ImGui.GetColorU32(ImGuiCol.CheckMark);
            for (int i = 0; i <= n; i++)
            {
                float x = i / (float)n;
                float y = te.Falloff_U28At(x, hard);
                var pt = new Vector2(p0.X + 3 + x * (size.X - 6), p0.Y + size.Y - 3 - y * (size.Y - 6));
                if (i > 0) dl.AddLine(prev, pt, col, 1.5f);
                prev = pt;
            }
        }
    }
}
