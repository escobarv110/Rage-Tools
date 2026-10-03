using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool ShowGrassBrush_S5;
        public int GrassBrushScope_S5 = 3;
        public float GrassBrushRadius_S5 = 5.0f;
        public string GrassBrushStatus_S5 = "";
        public int GrassBrushErased_S5;
        public bool GrassBrushArmed_S5 => ShowGrassBrush_S5 && GrassBrushScope_S5 > 0;

        public static readonly string[] GrassBrushScopes_S5 =
        {
            "Off",
            "Selected ymap",
            "Project ymaps",
            "Any ymap",
        };

        private void GrassBrushWindow_S5(float displayW, float displayH)
        {
            if (!WorldMode || !ShowGrassBrush_S5) return;
            ImGui.SetNextWindowSize(new Vector2(340, 240), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(displayW - 380, 120), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Delete Grass###GrassBrushS5", ref ShowGrassBrush_S5))
            {
                ImGui.TextWrapped("Left-drag in the viewport to rub grass instances out. One drag is one Undo.");
                ImGui.Separator();
                ImGui.SetNextItemWidth(-90);
                ImGui.Combo("Scope", ref GrassBrushScope_S5, GrassBrushScopes_S5, GrassBrushScopes_S5.Length);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Which files the brush may edit. \"Any ymap\" covers both the game's grass\n" +
                                     "and a grass ymap you have added to the project - the edited file joins\n" +
                                     "the project either way, so it can be saved.");
                ImGui.SetNextItemWidth(-90);
                UiSlider_U28.Float("Radius", ref GrassBrushRadius_S5, 0.5f, 25.0f, "%.1f m");
                if (GrassBrushScope_S5 == 0)
                    ImGui.TextDisabled("Armed: no - pick a scope above.");
                else
                    ImGui.TextColored(UiTheme.Warn, "Armed - the left button erases grass.");
                ImGui.Separator();
                if (!string.IsNullOrEmpty(GrassBrushStatus_S5)) ImGui.TextWrapped(GrassBrushStatus_S5);
                if (GrassBrushErased_S5 > 0) ImGui.TextDisabled($"{GrassBrushErased_S5:N0} instance(s) erased this session.");
            }
            ImGui.End();
        }
    }
}

