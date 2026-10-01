using System;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public static string DirtyMark_V19(YmapEntityDef ent) =>
            ent?.Ymap != null && ent.Ymap.HasChanged ? " *" : "";

        public static string DirtyMark_V19(YmapFile ymap) =>
            ymap != null && ymap.HasChanged ? " *" : "";

        public bool RequestLocateSelected_V19;

        public void DrawWorldSelHeader_V19(YmapEntityDef sel)
        {
            if (sel == null) return;
            bool dirty = sel.Ymap != null && sel.Ymap.HasChanged;

            if (WorldSelCount_V20 > 1)
            {
                ImGui.TextColored(UiTheme.Warn, WorldSelCount_V20 + " selected - the gizmo moves them together");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Ctrl+click in the world adds or removes one.\n" +
                                     "A plain click starts a new selection.");
            }

            var name = sel.Archetype?.Name ?? sel.Name ?? "(unnamed)";
            if (dirty)
            {
                ImGui.TextWrapped(name);
                ImGui.SameLine(0, 4);
                ImGui.TextColored(UiTheme.Warn, "*");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Moved, and not written to disk yet.\n" +
                                     "Save edited ymaps, at the bottom of this panel.");
            }
            else ImGui.TextWrapped(name);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(SelectionKeysTip_M3);

            var ymapName = sel.Ymap?.Name;
            ImGui.TextDisabled("in " + (string.IsNullOrEmpty(ymapName) ? "(unknown ymap)" : ymapName) + DirtyMark_V19(sel.Ymap));

            if (ImGui.SmallButton("Locate##v19locate")) RequestLocateSelected_V19 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Fly the camera to this prop and highlight it,\n" +
                                 "and open its .ymap in the project tree.");
        }
    }
}

