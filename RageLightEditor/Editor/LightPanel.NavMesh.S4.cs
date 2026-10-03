using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        partial void NavStreamControls_S4();

        partial void NavStreamControls_S4()
        {
            var nav = Nav;
            if (nav == null) return;

            bool follow = nav.FollowCamera_S4;
            if (ImGui.Checkbox("Follow the camera##navs4", ref follow)) nav.FollowCamera_S4 = follow;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Keep the cells within the reach above open as you fly, and drop the ones\n" +
                                 "left far behind - the way the map itself streams. Off, only 'Load here'\n" +
                                 "loads anything, and the mesh stops at the edge of the patch you loaded.");

            ImGui.SetNextItemWidth(-1);
            int cap = nav.StreamCellCap_S4;
            if (UiSlider_U28.Int("##navs4cap", ref cap, 8, 256, "at most %d cells at once")) nav.StreamCellCap_S4 = cap;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The reach says how far to look; this says how much to hold. Nearest first, so\n" +
                                 "raising the reach without raising this simply centres the same amount of mesh\n" +
                                 "on you. Each cell is a parsed .ynv in memory - 64 is a comfortable city block or two.");

            ImGui.TextDisabled($"{nav.Docs.Count} cell(s) live  ·  {nav.TotalPolys:N0} polys  ·  {nav.StreamedCount_S4} streamed");
            if (nav.StreamStatus_S4.Length > 0) ImGui.TextDisabled(nav.StreamStatus_S4);
        }
    }
}

