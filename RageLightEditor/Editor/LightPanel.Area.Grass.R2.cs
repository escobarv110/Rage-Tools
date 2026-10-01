using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private void DrawAreaGrassRows_R2(AreaToolState st, float full)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("GRASS");
            if (ImGui.Checkbox("Include grass", ref st.IncludeGrass_R2)) st.RequestRefreshGrass_R2 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Grass is not entities - it is the ymap's instance batches.\nOn: 'Delete inside' also removes every grass instance whose position falls in the area,\nand the ymaps it edits join the project so the change can be saved.");

            ImGui.TextColored(UiTheme.Accent, $"{st.GrassInstancesInside_R2} grass instance{(st.GrassInstancesInside_R2 == 1 ? "" : "s")} inside" +
                                              (st.GrassInside_R2.Count > 0 ? $"  ({st.GrassInside_R2.Count} batch{(st.GrassInside_R2.Count == 1 ? "" : "es")})" : ""));
            if (st.GrassInside_R2.Count > 0)
            {
                ImGui.BeginChild("##areagrass", new Vector2(0, 88), ImGuiChildFlags.Borders);
                for (int i = 0; i < st.GrassInside_R2.Count; i++)
                {
                    var h = st.GrassInside_R2[i];
                    ImGui.Text($"{h.Archetype}");
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X > 220 ? 200 : 140);
                    ImGui.TextDisabled($"{h.Inside} of {h.Total}   {h.Ymap}");
                }
                ImGui.EndChild();
                if (st.GrassBatchesEmptied_R2 > 0)
                    ImGui.TextDisabled($"{st.GrassBatchesEmptied_R2} batch(es) would be emptied and removed from their ymap.");
            }
            float h2 = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
            if (ImGui.Button("Recount grass", new Vector2(h2, 0))) st.RequestRefreshGrass_R2 = true;
            ImGui.SameLine();
            if (DangerButton($"Delete grass only ({st.GrassInstancesInside_R2})", new Vector2(h2, 0)) && st.GrassInstancesInside_R2 > 0)
                st.RequestDeleteGrassOnly_R2 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Removes the grass and leaves every prop where it is. One Undo step.");
        }
    }
}

