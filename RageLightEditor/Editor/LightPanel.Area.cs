using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public AreaToolState AreaTool;

        private int areaCornerListHeight = 96;
        private string areaRenameBuf;
        private WorldArea areaRenameFor;

        private void DrawAreaTabItem_J5()
        {
            var st = AreaTool;
            bool force = st != null && st.RequestShowTab;
            if (st != null) st.RequestShowTab = false;
            bool dummy = true;
            bool open = force ? ImGui.BeginTabItem("Area", ref dummy, ImGuiTabItemFlags.SetSelected) : ImGui.BeginTabItem("Area");
            if (st != null) st.TabActive = open;
            if (!open) return;
            ImGui.BeginChild("##insparea", new Vector2(0, 0));
            DrawAreaTab();
            ImGui.EndChild();
            ImGui.EndTabItem();
        }

        private void DrawAreaTab()
        {
            var st = AreaTool;
            if (st == null) { ImGui.TextWrapped("The world is not up yet."); return; }
            rowSeq = 0;
            float full = -1;

            ImGui.TextDisabled("CLEAR AREA");
            ImGui.TextWrapped("Draw a region on the ground, list what stands in it, then move it under the map or delete it - one Undo step each.");
            ImGui.Spacing();

            if (st.Areas.Count > 0)
            {
                ImGui.BeginChild("##arealist", new Vector2(0, Math.Min(26 + st.Areas.Count * 22, 120)), ImGuiChildFlags.Borders);
                for (int i = 0; i < st.Areas.Count; i++)
                {
                    var a = st.Areas[i];
                    ImGui.PushID(i);
                    bool vis = a.Visible;
                    if (ImGui.Checkbox("##vis", ref vis)) { a.Visible = vis; st.AreasDirty = true; }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Show this area in the viewport");
                    ImGui.SameLine();
                    string label = $"{a.Name}   ({a.Count} corners{(a.IsValid ? "" : " - incomplete")})";
                    if (ImGui.Selectable(label, i == st.Selected, ImGuiSelectableFlags.None))
                    {
                        if (st.Selected != i) { st.Selected = i; st.SelectedCorner = -1; st.RequestRefresh = true; }
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) st.RequestFrameArea = true;
                    }
                    ImGui.PopID();
                }
                ImGui.EndChild();
            }
            else ImGui.TextDisabled("no areas yet");

            if (st.Drawing)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ButtonOn);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UiTheme.ButtonOn);
                ImGui.Button($"Drawing... {st.Draft.Count} corner{(st.Draft.Count == 1 ? "" : "s")}", new Vector2(full, 0));
                ImGui.PopStyleColor(2);
                ImGui.TextWrapped("Click the ground in the viewport for each corner. Enter, a double click or Done finishes (3 or more corners); Esc cancels; Backspace removes the last corner.");
                float half = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
                if (ImGui.Button("Done##area", new Vector2(half, 0))) st.RequestFinishDraw = true;
                ImGui.SameLine();
                if (ImGui.Button("Cancel##area", new Vector2(half, 0))) st.RequestCancelDraw = true;
                if (ImGui.Button("Undo corner", new Vector2(full, 0))) st.RequestUndoCorner = true;
                var dz = new Vector2(st.DraftZMin, st.DraftZMax);
                ImGui.SetNextItemWidth(full);
                if (ImGui.DragFloat2("##draftz", ref dz, 0.25f, -500.0f, 2000.0f, "%.1f m")) { st.DraftZMin = Math.Min(dz.X, dz.Y); st.DraftZMax = Math.Max(dz.X, dz.Y); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Height range of the new area, relative to its lowest corner (below, above)");
            }
            else
            {
                if (ImGui.Button("Draw area", new Vector2(full, 0))) st.RequestStartDraw = true;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click 3 or more points on the ground in the viewport; Enter / double-click / Done closes the outline.");
                float half = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
                if (ImGui.Button("Box from selection", new Vector2(half, 0))) st.RequestBoxFromSelection = true;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("A rectangle around the selected entity's bounds, plus the margin");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(half);
                ImGui.DragFloat("##boxmargin", ref st.BoxMargin, 0.1f, 0.0f, 100.0f, "margin %.1f m");
                float third = (ImGui.GetContentRegionAvail().X - 2 * ImGui.GetStyle().ItemSpacing.X) / 3.0f;
                if (ImGui.Button("New empty", new Vector2(third, 0))) st.RequestNewEmpty = true;
                ImGui.SameLine();
                if (ImGui.Button("Duplicate", new Vector2(third, 0)) && st.Current != null) st.RequestDuplicateArea = true;
                ImGui.SameLine();
                if (DangerButton("Remove", new Vector2(third, 0)) && st.Current != null) st.RequestRemoveArea = true;
            }

            var cur = st.Current;
            if (cur == null)
            {
                DrawAreaFilesRow(st);
                DrawAreaStatus(st);
                return;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("AREA");
            if (!ReferenceEquals(areaRenameFor, cur)) { areaRenameFor = cur; areaRenameBuf = cur.Name ?? ""; }
            ImGui.TextDisabled("Name");
            ImGui.SameLine(120);
            ImGui.SetNextItemWidth(full);
            if (ImGui.InputText("##areaname", ref areaRenameBuf, 64)) { cur.Name = areaRenameBuf; st.AreasDirty = true; }
            var zr = new Vector2(cur.ZMin, cur.ZMax);
            ImGui.TextDisabled("Height");
            ImGui.SameLine(120);
            ImGui.SetNextItemWidth(full);
            if (ImGui.DragFloat2("##areaz", ref zr, 0.25f, -500.0f, 2000.0f, "%.1f m"))
            {
                cur.ZMin = Math.Min(zr.X, zr.Y); cur.ZMax = Math.Max(zr.X, zr.Y);
                st.AreasDirty = true; st.RequestRefresh = true;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Below / above the lowest corner (z {cur.BaseZ:0.##}): the prism spans {cur.BottomZ:0.#} .. {cur.TopZ:0.#} m");
            var c = cur.Centre;
            Row("Centre", $"{c.X:0.##}, {c.Y:0.##}, {c.Z:0.##}");
            Row("Size", $"{cur.Size:0.#} m across, {cur.Count} corners");
            if (cur.Count > 0)
            {
                ImGui.TextDisabled("Corners  (click one: the gizmo moves it)");
                ImGui.BeginChild("##areacorners", new Vector2(0, Math.Min(8 + cur.Count * 20, areaCornerListHeight)), ImGuiChildFlags.Borders);
                for (int i = 0; i < cur.Count; i++)
                {
                    var p = cur.Corners[i];
                    bool sel = i == st.SelectedCorner;
                    if (ImGui.Selectable($"{i + 1}:  {p.X:0.##}, {p.Y:0.##}, {p.Z:0.##}##corner{i}", sel))
                        st.RequestSelectCorner = sel ? -1 : i;
                }
                ImGui.EndChild();
            }
            if (ImGui.Button("Frame area", new Vector2(full, 0))) st.RequestFrameArea = true;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("CONTENTS");
            bool changed = false;
            changed |= ImGui.Checkbox("LOD levels", ref st.IncludeLod);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Off: only HD and orphan-HD placements - the props. On: the LOD stand-ins too.");
            changed |= ImGui.Checkbox("Interior props", ref st.IncludeInterior);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Props inside interiors whose pivot falls in the area. They can be moved; Delete leaves them (their placement lives in the ytyp).");
            changed |= ImGui.Checkbox("Interior shells", ref st.IncludeMloInstances);
            changed |= ImGui.Checkbox("Bounds centre", ref st.UseBoundsCentre);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("An entity whose pivot is outside but whose box centre is inside counts as inside.");
            ImGui.SetNextItemWidth(full);
            changed |= ImGui.InputTextWithHint("##areaname_f", "name contains...", ref st.NameFilter, 96);
            ImGui.SetNextItemWidth(full);
            changed |= ImGui.InputTextWithHint("##areaarch_f", "only these archetypes (comma separated)", ref st.ArchetypeList, 512);
            ImGui.SetNextItemWidth(full);
            changed |= ImGui.InputTextWithHint("##areaymap_x", "exclude ymaps containing...", ref st.ExcludeYmaps, 256);
            ImGui.SetNextItemWidth(full);
            changed |= ImGui.DragFloat("##arearad", ref st.MaxRadius, 0.5f, 0.0f, 1000.0f, st.MaxRadius > 0 ? "max size %.0f m" : "any size");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Entities with a bounding sphere over this are not props (a terrain tile, a building) and stay. 0 = no limit.");
            if (changed) st.RequestRefresh = true;
            if (ImGui.Button("Refresh list", new Vector2(full, 0))) st.RequestRefresh = true;

            ImGui.TextColored(UiTheme.Accent, $"{st.Contents.Count} entit{(st.Contents.Count == 1 ? "y" : "ies")} inside" + (st.ContentsInteriorSkipped > 0 ? $"  ({st.ContentsInteriorSkipped} interior)" : ""));
            ImGui.SameLine();
            ImGui.Checkbox("Highlight", ref st.Highlight);
            if (st.Contents.Count > 0)
            {
                ImGui.BeginChild("##areacontents", new Vector2(0, 200), ImGuiChildFlags.Borders);
                for (int i = 0; i < st.Contents.Count; i++)
                {
                    var en = st.Contents[i];
                    bool sel = i == st.SelectedEntry;
                    if (ImGui.Selectable($"{en.Name}##ent{i}", sel)) { st.SelectedEntry = i; st.RequestSelectEntry = i; }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"{en.Name}\n{en.Position.X:0.##}, {en.Position.Y:0.##}, {en.Position.Z:0.##}\n{en.Source}   {en.LodLevel}   r {en.Radius:0.#} m\nclick: select in the world - double-click: frame it");
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) st.RequestFrameEntry = i;
                    }
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X > 220 ? 200 : 140);
                    ImGui.TextDisabled(en.Source);
                }
                ImGui.EndChild();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("ACTIONS");
            var off = new Vector3(st.MoveOffset.X, st.MoveOffset.Y, st.MoveOffset.Z);
            ImGui.SetNextItemWidth(full);
            if (ImGui.DragFloat3("##areaoff", ref off, 0.5f)) st.MoveOffset = new SharpDX.Vector3(off.X, off.Y, off.Z);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Offset for Move inside (X Y Z, metres). 0, 0, -100 hides the props under the map without deleting them.");
            if (ImGui.Button($"Move inside by offset ({st.Contents.Count})", new Vector2(full, 0)) && st.Contents.Count > 0) st.RequestMove = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Every listed entity moves by the offset - interior props too. One Undo step; the touched ymaps join the project.");
            if (DangerButton(st.DeleteLabel_R2, new Vector2(full, 0)) && st.CanDelete_R2) st.RequestDelete = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Every listed ymap entity is removed from its ymap (interior props are left - move them, or delete them in the Project window). One Undo step.");
            float h2 = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
            if (ImGui.Button("Undo last", new Vector2(h2, 0))) st.RequestUndo = true;
            ImGui.SameLine();
            if (ImGui.Button("Save list...", new Vector2(h2, 0))) st.RequestSaveList = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A text file: archetype, position, ymap of every entity inside - for the mapper's records.");

            DrawAreaGrassRows_R2(st, full);
            DrawAreaFilesRow(st);
            DrawAreaStatus(st);
        }

        private void DrawAreaFilesRow(AreaToolState st)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("AREAS FILE");
            if (st.HasProjectFile) ImGui.TextWrapped("Areas save with the project (<project>.areas.xml beside the .cwproj) and come back when it opens.");
            else ImGui.TextWrapped("Save the project to keep the areas with it (<project>.areas.xml beside the .cwproj), or save them to a file of their own.");
            if (!string.IsNullOrEmpty(st.AreasPath)) ImGui.TextDisabled(System.IO.Path.GetFileName(st.AreasPath) + (st.AreasDirty ? "  (unsaved changes)" : ""));
            float h2 = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
            if (ImGui.Button("Load areas...", new Vector2(h2, 0))) st.RequestLoadAreas = true;
            ImGui.SameLine();
            if (ImGui.Button("Save areas as...", new Vector2(h2, 0))) st.RequestSaveAreas = true;
        }

        private static void DrawAreaStatus(AreaToolState st)
        {
            if (string.IsNullOrEmpty(st.Status)) return;
            ImGui.Spacing();
            ImGui.TextWrapped(st.Status);
        }
    }
}

