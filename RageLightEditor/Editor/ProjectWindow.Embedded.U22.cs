using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class ProjectWindow
    {
        public bool Docked = true;
        public bool DockAvailable;
        private bool embedded;
        private float embeddedTreeHeight = 260.0f;

        public bool ShowsDocked => Docked && DockAvailable;

        public void DrawEmbedded()
        {
            embedded = true;
            try
            {
                if (Project == null) { DrawEmptyState_U22(); return; }
                if (ImGui.BeginChild("##projembed", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.MenuBar))
                {
                    HandleShortcuts();
                    DrawMenuBar();
                    DrawToolStrip();
                    ImGui.TextDisabled("Double-click to fly there, right-click for more.");
                    float avail = ImGui.GetContentRegionAvail().Y;
                    embeddedTreeHeight = Math.Clamp(embeddedTreeHeight, 80.0f, Math.Max(80.0f, avail - 80.0f));
                    ImGui.BeginChild("##projembedtree", new Vector2(0, embeddedTreeHeight), ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);
                    DrawExplorer();
                    embeddedTreeHeight = ImGui.GetWindowSize().Y;
                    ImGui.EndChild();
                    ImGui.BeginChild("##projembedpage", new Vector2(0, 0), ImGuiChildFlags.Borders);
                    DrawPage();
                    ImGui.EndChild();
                }
                ImGui.EndChild();
            }
            finally { embedded = false; }
        }

        private void DrawEmptyState_U22()
        {
            ImGui.Spacing();
            ImGui.TextWrapped("A project holds the map files you change. What you edit in it replaces the game's copy in the world.");
            ImGui.Spacing();
            float w = ImGui.GetContentRegionAvail().X;
            float h = ImGui.GetFrameHeight() * 1.6f;
            if (ImGui.Button("New project", new Vector2(w, h))) RequestNewProject = true;
            if (ImGui.Button("Open project...", new Vector2(w, h))) RequestOpenProject = true;
            bool hasSel = !string.IsNullOrEmpty(WorldSelectionSummary);
            if (!hasSel) ImGui.BeginDisabled();
            if (ImGui.Button("Add the selected prop", new Vector2(w, h))) RequestAddWorldSelectionToProject = true;
            if (!hasSel) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(hasSel ? "Starts a project with " + WorldSelectionSummary : "Right-click a prop in the world first.");
            ImGui.Spacing();
            if (ImGui.Button("Open files...", new Vector2(w, 0))) RequestOpenAny = true;
        }

        private void DrawEmbeddedButtons_U22()
        {
            float right = ImGui.GetWindowWidth() - 8;
            ImGui.SameLine(Math.Max(right - 70, ImGui.GetCursorPosX() + 8));
            if (ImGui.SmallButton("Pop out##pwpop")) { Docked = false; Visible = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open the project in its own window.");
        }

        private void DrawDockButton_U22()
        {
            if (!DockAvailable) return;
            ImGui.SameLine();
            if (ImGui.SmallButton("Dock##pwdock")) { Docked = true; Visible = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Put the project back as a tab in the right panel.");
        }
    }
}
