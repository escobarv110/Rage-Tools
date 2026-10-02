using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public float OccluderFill = 0.12f;

        private void DrawHelpersExtras_Occluders_J1()
        {
            bool occMode = SelectionModeEnum == WorldSelectionMode.Occlusion;
            if (!occMode) ImGui.BeginDisabled();
            OptSlider("Occluder fill", ref OccluderFill, 0.0f, 0.5f, "%.2f",
                      "Occlusion selection mode: how much each occluder's volume is tinted (depth-tested,\n" +
                      "so it only shows where the occluder pokes out of its building). The edges are\n" +
                      "always drawn on top: box occluders in the accent, occlude models in red, the\n" +
                      "nearest 60 only. 0 = edges alone." + (occMode ? "" : "\n\nSwitch the selection mode to Occlusion to see it."), def: 0.12f);
            if (!occMode) ImGui.EndDisabled();
        }
    }
}

