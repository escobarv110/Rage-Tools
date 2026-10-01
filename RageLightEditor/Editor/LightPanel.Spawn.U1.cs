using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool RequestResetView_U1;

        private void DrawResetViewRow_U1()
        {
            if (ImGui.SmallButton("Reset view##u1reset")) RequestResetView_U1 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Back to the world spawn - " +
                                 MainForm.FormatSpawn_U1() + " - looking over downtown.\n" +
                                 "That is where the World opens on a fresh install. Once a section has\n" +
                                 "been left, its own remembered placement is what it opens on instead,\n" +
                                 "so this button is the way back to the spawn afterwards.");
        }

        internal void DrawUiSoundsMenuItem_U1()
        {
            _ = settings;
        }
    }
}

