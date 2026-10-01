using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class ParticlePanel
    {
        public static bool ShowUnsupportedRules_S6 =
            System.Environment.GetEnvironmentVariable("RLE_PTFXSHOWMODEL") == "1";

        public string UnsupportedNote_S6 = "";

        internal void DrawUnsupportedRulesToggle_S6()
        {
            var show = ShowUnsupportedRules_S6;
            if (ImGui.Checkbox("Rule cards", ref show))
                ShowUnsupportedRules_S6 = show;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Rules that draw geometry (ptxd_Model), a ribbon (ptxd_Trail) or a\n" +
                                 "ground decal are not billboards, and this preview cannot draw them.\n" +
                                 "Off: they are left out, so what you see is what the game shows.\n" +
                                 "On: each one gets a flat stand-in card where its particles are -\n" +
                                 "useful for finding out where a model rule actually spawns.");
            if (!string.IsNullOrEmpty(UnsupportedNote_S6)) ImGui.TextDisabled(UnsupportedNote_S6);
        }
    }
}

