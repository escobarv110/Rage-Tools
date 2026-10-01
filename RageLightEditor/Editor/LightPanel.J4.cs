using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool WorldInteriorCull = true;
        public bool WorldInteriorCullRooms = true;
        public string WorldInteriorCullStatus = "";

        private void DrawInteriorCullOption_J4()
        {
            ImGui.Checkbox("Interior occlusion", ref WorldInteriorCull);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Inside an interior, the outside is drawn only where one of its portals shows it -\n" +
                                 "the way the game does it. Off draws the whole city through the walls (slow).\n" +
                                 (string.IsNullOrEmpty(WorldInteriorCullStatus) ? "" : "\nNow: " + WorldInteriorCullStatus));
        }

        partial void DrawInteriorCullRoomsOption_J4()
        {
            if (!WorldInteriorCull) ImGui.BeginDisabled();
            ImGui.Checkbox("Room culling", ref WorldInteriorCullRooms);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Rooms of the interior that no portal in view leads to are not drawn either (the game's\n" +
                                 "portal traversal). Turn off if a custom MLO with sloppy portals shows holes.\n" +
                                 "Needs Interior occlusion on.");
            if (!WorldInteriorCull) ImGui.EndDisabled();
        }
    }
}

