using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        partial void NavCells_R3(float displayHeight);

        partial void NavFileNote_R3(NavMeshEditor.NavDoc doc);

        private static readonly Vector4 NavEmptyColour_R3 = new Vector4(1.0f, 0.35f, 0.75f, 1f);

        partial void NavCells_R3(float displayHeight)
        {
            var nav = Nav;
            if (nav == null) return;

            ImGui.Spacing();
            ImGui.TextDisabled("NAV MESHES");
            ImGui.SameLine();
            if (!nav.CellsReady)
            {
                ImGui.TextDisabled(string.IsNullOrEmpty(nav.CellsStatus) ? "scanning..." : nav.CellsStatus);
                if (ImGui.SmallButton("Rescan##navr3")) nav.RequestRescanCells = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The nav mesh index is built from the game archives the first time this\n" +
                                     "workspace opens. Press this if the archives finished opening after it did.");
                return;
            }
            ImGui.Text($"{nav.Cells.Count:N0} cells");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Every navmesh[x][y].ynv this install has, from x64f.rpf\\levels\\gta5\\navmeshes*.rpf\n" +
                                 "and any DLC that ships its own. The world is a 100x100 grid of 150 m cells;\n" +
                                 "a cell with no nav mesh (open water, mostly) is simply not in this list.");

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##navr3filter", "search: 54 · navmesh[54][20] · 54,20 · or world x,y",
                                    ref nav.CellFilter, 64);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A piece of the name, the two numbers in the brackets, or a pair of WORLD\n" +
                                 "coordinates (anything over 400 is read as metres) to find the cell that\n" +
                                 "covers a place. Try 529,-626 for the block you were just looking at.");

            ImGui.Checkbox("Nearest first##navr3", ref nav.CellsNearestFirst);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Sort by how far the cell is from the camera rather than by grid position.");
            ImGui.SameLine();
            ImGui.Checkbox("Open only##navr3", ref nav.CellsOpenOnly);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show only the cells already open, for finding one again in ten thousand rows.");

            float listH = Math.Min(230.0f, Math.Max(96.0f, displayHeight * 0.24f));
            if (ImGui.BeginChild("##navr3cells", new Vector2(0, listH), ImGuiChildFlags.Borders))
            {
                float rowW = ImGui.GetContentRegionAvail().X;
                int total;
                var rows = nav.FilteredCells(300, out total);
                int shown = 0;
                foreach (var c in rows)
                {
                    shown++;
                    ImGui.PushID(c.Name);
                    var doc = nav.DocOfCell(c);
                    bool open = doc != null;
                    float dist = nav.CellDistance(c);

                    var dot = open
                        ? (doc.PolyCount > 0 ? new Vector4(0.35f, 0.85f, 0.45f, 1f) : NavEmptyColour_R3)
                        : new Vector4(0.45f, 0.48f, 0.55f, 1f);
                    ImGui.ColorButton("##navr3dot", dot,
                        ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(11, 11));
                    ImGui.SameLine();

                    bool sel = ReferenceEquals(c, nav.SelectedCell);
                    if (ImGui.Selectable(c.Name + "##navr3s", sel))
                    {
                        nav.SelectedCell = c;
                        nav.RequestOpenCell = c;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{c.Name}\n" +
                                         $"cell {c.FileX},{c.FileY}   centre {c.Centre.X:0}, {c.Centre.Y:0}   " +
                                         $"{dist:N0} m away\n{c.ArchivePath}\n" +
                                         (open
                                            ? (doc.PolyCount > 0
                                                 ? $"open - {doc.PolyCount:N0} polygons. Click to make it the active file."
                                                 : "open, and it has NO POLYGONS - the game ships this cell empty.")
                                            : "Click to open it for editing. Nothing else in the editor changes.") +
                                         "\nDouble-click to open it AND fly there.");
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    { nav.SelectedCell = c; nav.RequestOpenCell = c; nav.RequestGoToCell = c; }

                    string right = open
                        ? (doc.PolyCount > 0 ? Short_R3(doc.PolyCount) : "EMPTY")
                        : dist < 1.0f ? "here" : $"{dist:N0} m";
                    float w = ImGui.CalcTextSize(right).X;
                    ImGui.SameLine(Math.Max(rowW - w, 90.0f), 0.0f);
                    if (open && doc.PolyCount == 0)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, NavEmptyColour_R3);
                        ImGui.TextUnformatted(right);
                        ImGui.PopStyleColor();
                    }
                    else ImGui.TextDisabled(right);
                    ImGui.PopID();
                }
                if (shown == 0)
                    ImGui.TextDisabled(nav.Cells.Count == 0
                        ? "no nav meshes were found in this install"
                        : nav.CellsOpenOnly ? "nothing is open yet" : "nothing matches that search");
                else if (total > shown)
                    ImGui.TextDisabled($"({shown:N0} of {total:N0} - search to narrow it)");
            }
            ImGui.EndChild();

            float half = (ImGui.GetContentRegionAvail().X - 6.0f) * 0.5f;
            ImGui.BeginDisabled(nav.SelectedCell == null);
            if (ImGui.Button("Go to this cell##navr3", new Vector2(half, 0))) nav.RequestGoToCell = nav.SelectedCell;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(nav.SelectedCell == null
                    ? "Pick a cell in the list first."
                    : $"Fly to {nav.SelectedCell.Centre.X:0}, {nav.SelectedCell.Centre.Y:0} - the middle of\n" +
                      $"{nav.SelectedCell.Name} - and look down at it.");
            ImGui.SameLine();
            if (ImGui.Button("Cells around me##navr3", new Vector2(-1, 0))) nav.RequestLoadAroundCamera = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"The nearest cells within {nav.LoadRadius:0} m of where you are standing, up to the\n" +
                                 $"cap of {nav.StreamCellCap_S4}, opened at once. This is what the workspace does for you\n" +
                                 "when it opens - and with Follow the camera on, it keeps doing it as you fly.");
        }

        private static string Short_R3(int n) =>
            n >= 1000 ? (n / 1000.0f).ToString("0.#") + "k" : n.ToString();

        partial void NavFileNote_R3(NavMeshEditor.NavDoc doc)
        {
            if (doc == null) return;
            string note = doc.PolyCount > 0 ? Short_R3(doc.PolyCount) : "EMPTY";
            float w = ImGui.CalcTextSize(note).X;
            ImGui.SameLine(Math.Max(ImGui.GetWindowWidth() - w - 22.0f, 90.0f), 0.0f);
            if (doc.PolyCount > 0) { ImGui.TextDisabled(note); return; }

            ImGui.PushStyleColor(ImGuiCol.Text, NavEmptyColour_R3);
            ImGui.TextUnformatted(note);
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("This cell really is empty in the game - nothing was lost opening it.\n" +
                                 "Draw or generate polygons into it, or pick another cell from the list above.");
        }
    }
}

