using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class MloCreatorPanel
    {
        public bool RequestBuildFromShell_R1;
        public float ShellVoxel_R1 = 0.25f;
        public float ShellDoorWidth_R1 = 1.5f;
        public bool ShellOutsidePortals_R1 = true;
        public string ShellReport_R1 = "";
        public bool ShellBusy_R1;

        public string RequestRemoveImportedYtyp_R1;

        internal void InvokeDelete_R1() => DeleteSelected();
        internal void InvokeDuplicate_R1() => DuplicateSelected();

        public void DrawShellBuild_R1()
        {
            ImGui.Spacing();
            ImGui.TextDisabled("BUILD FROM THE SHELL");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Work the interior out of the shell's own geometry: the enclosed spaces become\n" +
                                 "rooms and the openings between them become portals. A first cut you then correct.");
            bool busy = ShellBusy_R1;
            if (busy) ImGui.BeginDisabled();
            ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.AccentDim);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UiTheme.Accent);
            if (ImGui.Button(busy ? "Working..." : "Build rooms + portals from the shell", new Vector2(-1, 0)))
                RequestBuildFromShell_R1 = true;
            ImGui.PopStyleColor(2);
            if (busy) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Voxelises the shell, finds every space with a floor, a ceiling and walls,\n" +
                                 "splits it at the doorways and makes a room per space and a portal per opening.\n" +
                                 "Replaces the rooms and portals there are now - Ctrl+Z puts them back.");

            if (ImGui.TreeNodeEx("How finely##r1shellopt", ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                ImGui.SetNextItemWidth(120);
                ImGui.DragFloat("Grid##r1vox", ref ShellVoxel_R1, 0.01f, 0.08f, 1.0f, "%.2f");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The voxel edge. 0.25 m suits a house; drop it for narrow interiors (slower),\nraise it for something the size of a warehouse.");
                ImGui.SetNextItemWidth(120);
                ImGui.DragFloat("Max doorway##r1core", ref ShellDoorWidth_R1, 0.05f, 0.60f, 8.0f, "%.2f");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("A gap this wide or narrower joins two rooms; anything wider is open space, and\n" +
                                     "the two sides stay ONE room. Raise it to split a big hall at its archways;\n" +
                                     "lower it to stop a wide opening being read as a door.");
                ImGui.Checkbox("Outside portals##r1out", ref ShellOutsidePortals_R1);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("A door or a garage mouth that opens on the world gets a portal to limbo,\nwhich is what lets the sun and the exterior through it.");
                ImGui.TreePop();
            }
            if (!string.IsNullOrEmpty(ShellReport_R1))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Muted);
                ImGui.TextWrapped(ShellReport_R1);
                ImGui.PopStyleColor();
            }
        }

        public void DrawImports_R1(Scene scene)
        {
            var imports = scene?.Imports_R1?.Entries;
            if (imports == null || imports.Count == 0) return;
            ImGui.Spacing();
            ImGui.TextDisabled($"IMPORTED .YTYPS ({imports.Count})");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Every .ytyp imported into this scene. Imports add to each other, so this is\n" +
                                 "how you take one back out without clearing the rest.");
            for (int i = 0; i < imports.Count; i++)
            {
                var e = imports[i];
                if (e == null) continue;
                ImGui.PushID("r1imp" + i);
                if (LightPanel.DangerButton("Remove", new Vector2(70, 0))) RequestRemoveImportedYtyp_R1 = e.Path;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Take {e.Name} out again: its archetypes, its entities and the props only it\n" +
                                     "brought. Props another import also places stay.");
                ImGui.SameLine();
                ImGui.TextUnformatted(e.Name);
                if (ImGui.IsItemHovered())
                {
                    var archs = string.Join(", ", System.Linq.Enumerable.Take(e.ArchetypeNames, 6));
                    ImGui.SetTooltip(e.Path + "\n" + $"{e.MeshCount} meshes, {e.Props.Count} props, {e.EntityCount} placed" +
                                     (string.IsNullOrEmpty(archs) ? "" : "\narchetypes: " + archs));
                }
                ImGui.PopID();
            }
        }
    }
}

