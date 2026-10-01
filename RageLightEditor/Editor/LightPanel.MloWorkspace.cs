using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool MloMode => Workspace == Space.Mlo;

        private static readonly Vector4 MloWorkspaceColour = new Vector4(0.16f, 0.72f, 0.66f, 1f);

        private const string MloWorkspaceTooltip =
            "MLO workspace: build an interior - rooms as boxes, portals as quads, the props\n" +
            "assigned to rooms - over the shell and props loaded here, and write the .ytyp\n" +
            "(and a .ymap that places it). Hold V to snap to the mesh's vertices; a 3ds Max\n" +
            "bridge takes picked points from Max. Same scene, same open files - nothing is unloaded.";

        private void DrawMloWorkspaceLeft(float displayHeight)
        {
            DrawWorkspaceLogo(150.0f);
            var ui = MloCreator;
            ImGui.TextDisabled("MLO CREATOR");
            ImGui.SameLine();
            ImGui.Text(ui.Session != null
                ? $"({ui.Session.Rooms.Count} rm, {ui.Session.Portals.Count} pt, {ui.Session.Entities.Count} ent)"
                : "(no interior yet)");
            if (ImGui.IsItemHovered() && ui.Session != null) ImGui.SetTooltip($"{ui.Session.Rooms.Count} rooms, {ui.Session.Portals.Count} portals, {ui.Session.Entities.Count} entities");
            if (!string.IsNullOrEmpty(StatsText)) ImGui.TextDisabled(StatsText);
            ImGui.Separator();

            ImGui.TextDisabled("SCENE");
            if (ImGui.Button("Open shell...", new Vector2(-1, 0))) RequestOpenFile?.Invoke();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Open the interior's own model - walls and floors. It becomes the archetype's drawable\n" +
                                 "(the shell), and everything else loaded becomes an entity in a room.");
            if (ImGui.Button("Add props...", new Vector2(-1, 0))) RequestAddFile?.Invoke();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add .ydr / .yft props to the scene: each becomes an entity of the interior.");
            if (ImGui.Button("Import ytyp...", new Vector2(-1, 0))) RequestImportYtyp?.Invoke();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Load an existing MLO: its shell and props are placed, and the creator is seeded\n" +
                                 "with its rooms, portals and entity sets - editable copies, written to a new file.");
            DrawImportedYtyps_R1(scene);
            if (ui.Session == null)
            {
                if (!scene.HasModel) ImGui.BeginDisabled();
                if (ImGui.Button("Start from scene", new Vector2(-1, 0))) ui.RequestStartFromScene = true;
                if (!scene.HasModel) ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Entering this workspace does this by itself once something is loaded; use it after\n" +
                                     "closing the session, or to start over from what is loaded now.");
            }
            ImGui.Separator();

            if (WalkMode)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Ok);
                ImGui.TextWrapped("WALK MODE  (Esc to exit)");
                ImGui.PopStyleColor();
                ImGui.TextDisabled($"speed x{WalkSpeedDisplay:0.##} (scroll or X/Z)");
            }
            ImGui.TextDisabled("WASD fly  R/C up-down  Shift fast  Ctrl slow");
            ImGui.TextDisabled("Right-drag orbits   Middle-drag pans   Wheel zooms");
            ImGui.TextDisabled("Snap (a page's Snap button / the pickers): the vertices show as dots - click one, Esc cancels");
            ImGui.TextDisabled("Hold V: quick snap of the cursor to the nearest vertex");
            ImGui.TextDisabled("Click a prop to select it (Capture selection makes a room around it)");
            DrawGoToRow_O3();
            ImGui.Separator();

            ImGui.TextDisabled("PROPS");
            ImGui.SameLine();
            ImGui.Text($"({scene.Files.Count})");
            if (ui.Session?.ShellFile != null)
            {
                ImGui.SameLine();
                ImGui.TextColored(AccentText(), $"shell: {ui.Session.ShellFile.Name}");
            }
            ImGui.BeginChild("##mlospaceprops", new Vector2(0, 0), ImGuiChildFlags.None);
            DrawFileSection();
            ImGui.EndChild();
        }

        private string mloPanelForceOpen;

        private void DrawMloWorkspaceRight()
        {
            var ui = MloCreator;
            ImGui.TextDisabled("MLO CREATOR");
            bool win = ui.WindowVisible;
            if (ImGui.Checkbox("Creator window", ref win)) ui.WindowVisible = win;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The interior's tree + pages: rooms, portals, entities, sets, the write buttons.\nA resizable window floating over the viewport - or, detached, an OS window of its own on any monitor.");
            ImGui.SameLine();
            if (ImGui.SmallButton(ui.Detached ? "Attach##mlospaceattach" : "Detach##mlospacedetach")) { if (ui.Detached) ui.RequestAttach = true; else { ui.WindowVisible = true; ui.RequestDetach = true; } }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(ui.Detached ? "Dock the creator window back into the main window." : "Detach the creator window into an OS window of its own - drag it to another monitor.");
            if (ui.Session != null)
                ImGui.TextDisabled($"{ui.Session.Rooms.Count} rooms, {ui.Session.Portals.Count} portals, {ui.Session.Entities.Count} entities");
            if (!string.IsNullOrEmpty(ui.Status))
                ImGui.TextColored(ui.StatusIsError ? UiTheme.Danger : UiTheme.Muted, ui.Status);
            ImGui.Separator();
            if (Header("Vertex snap & corner handles###mlospacesnap", true))
                ui.DrawSnapSection();
            if (Header("View")) DrawViewSection();
            if (Header("Weather, sky & timecycle")) DrawTimecycleSection();
            var forceOpen = mloPanelForceOpen ??= Environment.GetEnvironmentVariable("RLE_MLOPANEL") ?? "";
            if (forceOpen.Contains("assets", StringComparison.OrdinalIgnoreCase)) ImGui.SetNextItemOpen(true, ImGuiCond.Once);
            bool assetsOpen = Header("Assets###mlospaceassets");
            ui.AssetsSectionOpen = assetsOpen;
            if (assetsOpen)
            {
                if (ui.Session == null) ImGui.TextDisabled("Start the interior first (open a shell / import a ytyp).");
                else ui.DrawAssetsPage_L3(scene, compact: true);
            }
            if (forceOpen.Contains("lights", StringComparison.OrdinalIgnoreCase)) ImGui.SetNextItemOpen(true, ImGuiCond.Once);
            bool lightsOpen = Header("Lights###mlospacelights");
            ui.LightsSectionOpen = lightsOpen;
            if (lightsOpen) DrawMloLightsPage(compact: true);
        }
    }
}

