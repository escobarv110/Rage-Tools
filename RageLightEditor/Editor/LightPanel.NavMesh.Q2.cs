using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {

        partial void WorldToolbar_Q2(float displayWidth, ref bool handled);

        partial void WorldToolbar_Q2(float displayWidth, ref bool handled)
        {
            if (!NavMode) return;
            DrawNavToolbar_Q2(displayWidth);
            handled = true;
        }

        public bool NavHidesWorldTools => NavMode;

        private void DrawNavToolbar_Q2(float displayWidth)
        {
            var nav = Nav;
            ImGui.SetNextWindowPos(new Vector2(0, TopBarHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(displayWidth, ToolbarH), ImGuiCond.Always);
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 5));
            ImGui.Begin("##navtoolbar", flags);

            if (WorldGizmo != null)
            {
                void Tool(string label, WorldGizmoMode m, string tip)
                {
                    bool on = WorldGizmo.Mode == m;
                    if (on) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
                    if (ImGui.SmallButton(label)) WorldGizmo.Mode = m;
                    if (on) ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
                    ImGui.SameLine();
                }
                Tool("Select##navtb", WorldGizmoMode.Select, "Right-click picks polygons; the left button flies the camera  (Q)");
                Tool("Move##navtb", WorldGizmoMode.Translate, "Drag the handles to move the selection  (W)");
                if (nav != null && (nav.SelectedPoint != null || nav.SelectedPortal != null))
                    Tool("Turn##navtb", WorldGizmoMode.Rotate, "Turn the point / portal to face somewhere  (E)");

                ImGui.SetNextItemWidth(70);
                float snap = WorldGizmo.TranslateSnap;
                if (ImGui.DragFloat("##navtbsnap", ref snap, 0.05f, 0.0f, 10.0f, snap <= 0.001f ? "snap off" : "%.2f m"))
                    WorldGizmo.TranslateSnap = snap;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Move snap. Shift suppresses it for one drag.");
                ImGui.SameLine();
            }

            ImGui.TextDisabled("|"); ImGui.SameLine();
            if (!WorldCanUndo) ImGui.BeginDisabled();
            if (ImGui.SmallButton("Undo##navtb")) RequestWorldUndo = true;
            if (!WorldCanUndo) ImGui.EndDisabled();
            if (ImGui.IsItemHovered() && WorldCanUndo) ImGui.SetTooltip("Undo " + (WorldUndoName ?? "") + "  (Ctrl+Z)");
            ImGui.SameLine();
            if (!WorldCanRedo) ImGui.BeginDisabled();
            if (ImGui.SmallButton("Redo##navtb")) RequestWorldRedo = true;
            if (!WorldCanRedo) ImGui.EndDisabled();
            if (ImGui.IsItemHovered() && WorldCanRedo) ImGui.SetTooltip("Redo " + (WorldRedoName ?? "") + "  (Ctrl+Y)");
            ImGui.SameLine();

            if (nav != null)
            {
                ImGui.TextDisabled("|"); ImGui.SameLine();
                bool draw = nav.PlacingPoly;
                ImGui.PushStyleColor(ImGuiCol.Button, draw ? UiTheme.ButtonOn : UiTheme.ButtonOff);
                if (ImGui.Button(draw ? "Drawing  *" : "Draw polygon"))
                { nav.PlacingPoly = !draw; if (draw) nav.PendingPoly.Clear(); }
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Click the ground for each corner, Enter closes the polygon  (P)");
                ImGui.SameLine();

                ImGui.BeginDisabled(nav.SelectedPolys.Count == 0 && nav.SelectedPoint == null && nav.SelectedPortal == null);
                if (ImGui.SmallButton("Frame##navtb")) nav.RequestFrameSelection = true;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fly the camera to the selection  (F)");
                ImGui.SameLine();
                if (ImGui.SmallButton(nav.HardDeletePolys_V15 ? "Delete##navtb" : "Disable##navtb"))
                    nav.RequestDeleteSelection = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(nav.HardDeletePolys_V15
                        ? "REMOVE the selected polygons from the file  (Del)\n\n" +
                          "Every poly after them takes a new index, and the .ynv files around\n" +
                          "this one name its polys BY index - so their edges point at the wrong\n" +
                          "polygons and traffic crossing that seam goes wrong. Re-export every\n" +
                          "neighbouring ynv with this one, or turn it off under Editing."
                        : "Take the selected polygons out of the pathfinding graph  (Del)\n\n" +
                          "They keep their slots, so every index in the file stays where it is\n" +
                          "and the neighbouring .ynv files still point at the right polygons.\n" +
                          "Nothing will walk or drive on them.\n\n" +
                          "To remove them from the file instead, see Editing below.");
                ImGui.EndDisabled();
                ImGui.SameLine();

                ImGui.TextDisabled("|"); ImGui.SameLine();
                var doc = nav.Active;
                ImGui.BeginDisabled(doc == null);
                if (ImGui.SmallButton(doc != null && doc.Dirty ? "Save *##navtb" : "Save##navtb")) nav.RequestSave = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(doc == null ? "Nothing open" : "Write " + doc.Name + "  (Ctrl+S)");
                ImGui.EndDisabled();
                ImGui.SameLine();
            }

            if (ProjectWindow != null)
            {
                bool pv = ProjectWindow.Visible;
                ImGui.PushStyleColor(ImGuiCol.Button, pv ? UiTheme.ButtonOn : UiTheme.ButtonOff);
                if (ImGui.Button(pv ? "Project Window  <" : "Project Window  >")) ProjectWindow.Visible = !pv;
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The map project - a .ynv saved here is shipped with the ymaps  (Ctrl+Shift+P)");
                ImGui.SameLine();
            }

            var stats = StatsText ?? "";
            float sw = ImGui.CalcTextSize(stats).X;
            ImGui.SameLine(Math.Max(ImGui.GetWindowWidth() - sw - 12, ImGui.GetCursorPosX()));
            ImGui.TextDisabled(stats);

            ImGui.End();
            ImGui.PopStyleVar();
        }

        private string navOpenName_Q2 = "";

        private void DrawNavLeft_Q2(float displayHeight)
        {
            DrawWorkspaceLogo(150.0f);
            var nav = Nav;
            ImGui.TextDisabled("NAV MESH");
            if (nav == null) { ImGui.TextWrapped("The nav mesh editor has not started yet."); return; }
            ImGui.SameLine();
            if (nav.Docs.Count == 0) ImGui.TextDisabled("nothing open");
            else ImGui.Text($"{nav.Docs.Count} cells live  ·  {nav.TotalPolys:N0} polys");

            float third = (ImGui.GetContentRegionAvail().X - 10.0f) / 3.0f;
            if (ImGui.Button("Open...##navq2", new Vector2(third, 0))) nav.RequestOpenFile = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open a .ynv from disk.");
            ImGui.SameLine();
            if (ImGui.Button("Load here##navq2", new Vector2(third, 0))) nav.RequestLoadAroundCamera = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Read the nav mesh cells within {nav.LoadRadius:0} m of the camera straight out of\n" +
                                 $"the game archives - the mesh under you, without knowing its name. Nearest\n" +
                                 $"first, up to the cap of {nav.StreamCellCap_S4}.");
            ImGui.SameLine();
            if (ImGui.Button("New##navq2", new Vector2(-1, 0))) nav.RequestNewFile = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("An empty .ynv for the 150 m cell you are standing in - somewhere to\n" +
                                 "draw or generate a mesh for a building the game has none for.");

            if (Header("More"))
            {
                ImGui.SetNextItemWidth(-1);
                UiSlider_U28.Float("##navq2radius", ref nav.LoadRadius, 75.0f, 3000.0f, "the mesh reaches %.0f m");
                NavStreamControls_S4();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##navq2byname", "navmesh[54][20]  (enter opens it from the archives)",
                        ref navOpenName_Q2, 64, ImGuiInputTextFlags.EnterReturnsTrue) && navOpenName_Q2.Trim().Length > 0)
                {
                    nav.RequestOpenByName = navOpenName_Q2.Trim();
                    navOpenName_Q2 = "";
                }
            }

            NavCells_R3(displayHeight);

            ImGui.Spacing();
            ImGui.TextDisabled("FILES");
            if (nav.Docs.Count > 1)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(the ticked one is drawn, the lit one is edited)");
            }
            if (ImGui.BeginChild("##navq2files", new Vector2(0, Math.Min(120.0f, Math.Max(52.0f, displayHeight * 0.13f))), ImGuiChildFlags.Borders))
            {
                if (nav.Docs.Count == 0) ImGui.TextDisabled("nothing open");
                foreach (var d in nav.Docs.ToList())
                {
                    ImGui.PushID(d.Name + d.Source);
                    bool vis = d.Visible;
                    if (ImGui.Checkbox("##vis", ref vis)) { d.Visible = vis; nav.LayerVersion++; }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Draw this file.");
                    ImGui.SameLine();
                    bool act = ReferenceEquals(d, nav.Active);
                    if (ImGui.Selectable($"{d}##sel", act)) nav.Active = d;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{d.PolyCount:N0} polys, {d.PointCount:N0} points, {d.PortalCount:N0} portals\n" +
                                         (string.IsNullOrEmpty(d.FilePath) ? "from " + d.Source : d.FilePath) +
                                         "\nNew polygons land in this one, and Save writes it.");
                    NavFileNote_R3(d);
                    ImGui.PopID();
                }
            }
            ImGui.EndChild();

            ImGui.Spacing();
            if (Header("Legend", true))
            {
                bool any = false;
                for (int i = 0; i < NavMeshEditor.CatNames.Length; i++)
                {
                    var c = NavMeshEditor.CatColours[i];
                    bool on = nav.CatVisible[i];
                    ImGui.ColorButton($"##navq2cat{i}", new Vector4(c.X, c.Y, c.Z, on ? 1f : 0.25f),
                        ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(14, 14));
                    ImGui.SameLine();
                    if (ImGui.Checkbox(NavMeshEditor.CatNames[i] + "##navq2catc" + i, ref on)) { nav.CatVisible[i] = on; any = true; }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(i == (int)NavMeshEditor.NavCat.Isolated
                            ? "A polygon no edge links out of - a ped that lands on one is stuck.\nThe usual fault in a hand-made mesh, so it gets a colour of its own."
                            : "Untick to hide this kind. Hidden polygons cannot be clicked either.");
                }
                if (any) nav.LayerVersion++;
            }

            if (Header("Editing"))
            {
                ImGui.Checkbox("Hard delete", ref nav.HardDeletePolys_V15);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(
                        "OFF (recommended): Del takes the selected polygons OUT of the pathfinding\n" +
                        "graph and leaves them in the file. Nothing walks or drives on them, and\n" +
                        "every polygon keeps the index it had.\n\n" +
                        "ON: Del removes them from the file. Everything after them shifts up one\n" +
                        "index - and a .ynv names the polygons of the cells around it BY INDEX, so\n" +
                        "their edges and portals end up naming the wrong polygons and traffic goes\n" +
                        "wrong along that border. Only use it if you are re-exporting every\n" +
                        "neighbouring navmesh at the same time.");
                var edoc = nav.Active;
                if (edoc != null && edoc.PolyCountOnLoad_V15 >= 0 && edoc.PolyCount != edoc.PolyCountOnLoad_V15)
                    ImGui.TextColored(UiTheme.Warn,
                        $"{edoc.Name}: {edoc.PolyCountOnLoad_V15:N0} -> {edoc.PolyCount:N0} polys - indices moved");
            }

            if (Header("Draw"))
            {
                bool any = false;
                any |= ImGui.Checkbox("Fills", ref nav.ShowFills); ImGui.SameLine();
                any |= ImGui.Checkbox("Edges", ref nav.ShowEdges);
                any |= ImGui.Checkbox("Points", ref nav.ShowPoints); ImGui.SameLine();
                any |= ImGui.Checkbox("Portals", ref nav.ShowPortals);
                any |= ImGui.Checkbox("Links", ref nav.ShowLinks);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The adjacency graph: a line from each polygon to every edge a\n" +
                                     "neighbour is reachable through. This is what the game walks.");
                any |= ImGui.Checkbox("Mark the isolated ones", ref nav.HighlightIsolated);
                ImGui.Checkbox("Draw on top", ref nav.DrawOnTop);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Off (the default), a wall in front of a polygon hides it, as the eye expects -\n" +
                                     "the mesh is still biased onto the ground it lies on, so the road never buries it.\n" +
                                     "On, it draws through everything: for reading a mesh that is under a roof.");
                ImGui.Checkbox("Hint bar", ref nav.ShowLegend);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("The line along the bottom saying what a click does.");
                ImGui.SetNextItemWidth(-1);
                any |= UiSlider_U28.Float("##navq2alpha", ref nav.FillAlpha, 0.1f, 1.0f, "fill %.2f");
                if (any) nav.LayerVersion++;
            }

            ImGui.Spacing();
            ImGui.TextDisabled("POLYGONS");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##navq2filter", "filter: index, or what it is (water, steep...)", ref nav.Filter, 48);
            var doc2 = nav.Active;
            float listH = Math.Max(110.0f, displayHeight - ImGui.GetCursorPosY() - 14.0f);
            if (ImGui.BeginChild("##navq2polys", new Vector2(0, listH), ImGuiChildFlags.Borders))
            {
                if (doc2 == null) ImGui.TextDisabled("no file is active");
                else
                {
                    int shown = 0;
                    foreach (var p in nav.FilteredPolys(doc2, 400))
                    {
                        shown++;
                        var cat = nav.CategoryOf(p);
                        var c = NavMeshEditor.CatColours[(int)cat];
                        ImGui.ColorButton($"##navq2pc{p.Index}", new Vector4(c.X, c.Y, c.Z, 1f),
                            ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(12, 12));
                        ImGui.SameLine();
                        bool sel = nav.SelectedPolys.Contains(p);
                        if (ImGui.Selectable($"{p.Index}  {NavMeshEditor.CatNames[(int)cat]}##navq2p{p.Index}", sel))
                            nav.SelectPoly(p, ImGui.GetIO().KeyCtrl);
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            nav.RequestFrameSelection = true;
                    }
                    if (shown == 0) ImGui.TextDisabled(doc2.PolyCount == 0 ? "this file has no polygons" : "nothing matches");
                    else if (shown >= 400) ImGui.TextDisabled("(first 400 - narrow the filter for the rest)");
                }
            }
            ImGui.EndChild();
        }

        private void DrawNavRight_Q2()
        {
            var nav = Nav;
            if (nav == null) { ImGui.TextDisabled("starting..."); return; }

            DrawNavHeadline_Q2(nav);

            if (nav.SelectedPoint != null)
            {
                if (Header("Point", true)) DrawNavPointEditor_Q2(nav, nav.SelectedPoint);
            }
            else if (nav.SelectedPortal != null)
            {
                if (Header("Portal", true)) DrawNavPortalEditor_Q2(nav, nav.SelectedPortal);
            }
            else
            {
                if (Header("Type", true)) DrawNavType_Q2(nav);
                if (Header("Corners")) DrawNavCorners_Q2(nav);
            }
            if (Header("Edit")) DrawNavCreate_Q2(nav);
            if (Header("Advanced")) DrawNavAdvanced_Q2(nav);

            ImGui.Separator();
            var doc = nav.Active;
            ImGui.BeginDisabled(doc == null);
            if (ImGui.Button(doc != null && doc.Dirty ? "Save .ynv  *" : "Save .ynv", new Vector2(-1, 0))) nav.RequestSave = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(doc?.FilePath == null
                    ? "This file came out of the game archives, which are never written back.\nSave writes your own copy (Save as... chooses where)."
                    : "Writes " + doc.FilePath + " (a .bak is kept the first time).");
            float half = (ImGui.GetContentRegionAvail().X - 6.0f) * 0.5f;
            if (ImGui.Button("Save as...##navq2", new Vector2(half, 0))) nav.RequestSaveAs = true;
            ImGui.SameLine();
            if (ImGui.Button("Close file##navq2", new Vector2(-1, 0))) nav.RequestCloseActive = true;
            if (ImGui.Button("Add to the map project", new Vector2(-1, 0))) nav.RequestAddToProject = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Puts this .ynv in the project beside the ymaps and ytyps, so Save All\nwrites it and the .cwproj remembers it.");
            ImGui.EndDisabled();
        }

        private void DrawNavHeadline_Q2(NavMeshEditor nav)
        {
            if (nav.SelectedPoint != null)
            {
                ImGui.Text($"Point {nav.SelectedPoint.Index}");
                ImGui.TextDisabled("somewhere a ped can be put down, facing a heading");
                return;
            }
            if (nav.SelectedPortal != null)
            {
                ImGui.Text($"Portal {nav.SelectedPortal.Index}");
                ImGui.TextDisabled("a link peds take off the mesh - a ladder, a drop, a squeeze");
                return;
            }
            var p = nav.SelectedPoly;
            if (p == null)
            {
                ImGui.TextWrapped("Nothing selected. Click a polygon in the viewport, or pick one from the list on the left.");
                return;
            }
            var cat = nav.CategoryOf(p);
            var c = NavMeshEditor.CatColours[(int)cat];
            ImGui.ColorButton("##navq2selc", new Vector4(c.X, c.Y, c.Z, 1f),
                ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(14, 14));
            ImGui.SameLine();
            ImGui.Text(nav.SelectedPolys.Count > 1
                ? $"{nav.SelectedPolys.Count} polygons  ({NavMeshEditor.CatNames[(int)cat]})"
                : $"Polygon {p.Index}  ({NavMeshEditor.CatNames[(int)cat]})");
            ImGui.TextDisabled($"{p.Vertices?.Length ?? 0} corners in {p.Ynv?.Name}  ·  " +
                               $"{p.Position.X:0.#}, {p.Position.Y:0.#}, {p.Position.Z:0.#}");
            if (NavMeshEditor.IsIsolated(p))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.35f, 0.75f, 1f));
                ImGui.TextWrapped("No edge of this polygon reaches a neighbour - a ped that lands on it is stuck.");
                ImGui.PopStyleColor();
            }
        }

        private void DrawNavType_Q2(NavMeshEditor nav)
        {
            var sel = nav.SelectedPolys;
            if (sel.Count == 0) { ImGui.TextDisabled("no polygon selected"); return; }
            var p = nav.SelectedPoly;
            var now = nav.CategoryOf(p);

            ImGui.TextDisabled(sel.Count == 1 ? "one click, applied to this polygon" : $"one click, applied to all {sel.Count}");
            float w = (ImGui.GetContentRegionAvail().X - 6.0f) * 0.5f;
            for (int i = 0; i < NavMeshEditor.TypeChoices.Length; i++)
            {
                var cat = NavMeshEditor.TypeChoices[i];
                var c = NavMeshEditor.CatColours[(int)cat];
                bool on = cat == now;
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(c.X * 0.55f, c.Y * 0.55f, c.Z * 0.55f, on ? 1.0f : 0.35f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(c.X * 0.8f, c.Y * 0.8f, c.Z * 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(c.X, c.Y, c.Z, 1.0f));
                if (ImGui.Button(NavMeshEditor.CatNames[(int)cat] + "##navq2type" + i,
                                 new Vector2(i % 2 == 0 ? w : -1, 0)))
                    nav.SetType(WorldHistory, sel.ToArray(), cat);
                ImGui.PopStyleColor(3);
                if (i % 2 == 0) ImGui.SameLine();
            }
            if (NavMeshEditor.TypeChoices.Length % 2 != 0) ImGui.NewLine();

            ImGui.Spacing();
            ImGui.TextDisabled("AND ALSO");
            foreach (var f in NavMeshEditor.OnShelf(NavMeshEditor.Shelf.More))
                DrawNavFlagCheck_Q2(nav, f);
        }

        private void DrawNavFlagCheck_Q2(NavMeshEditor nav, NavMeshEditor.NamedFlag f)
        {
            var sel = nav.SelectedPolys;
            var p = nav.SelectedPoly;
            if (p == null) return;
            bool on = f.Get(p);
            bool mixed = sel.Count > 1 && sel.Any(q => f.Get(q) != on);
            if (mixed) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
            if (ImGui.Checkbox(f.Name + "##navq2f" + f.Bit, ref on))
                nav.SetFlag(WorldHistory, sel.ToArray(), f.Bit, on);
            if (mixed) ImGui.PopStyleVar();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(f.Tip + (mixed ? "\n\n(the selection disagrees on this one)" : ""));
        }

        private void DrawNavCorners_Q2(NavMeshEditor nav)
        {
            var p = nav.SelectedPoly;
            if (p == null) { ImGui.TextDisabled("no polygon selected"); return; }
            ImGui.TextWrapped("The gizmo moves whatever is chosen here. Moving a corner moves every " +
                              "polygon that shares it, so the mesh never tears open.");
            bool whole = nav.SelectedVertex < 0;
            if (ImGui.RadioButton("the whole polygon##navq2vw", whole)) nav.SelectedVertex = -1;
            var vs = p.Vertices;
            if (vs != null)
                for (int i = 0; i < vs.Length; i++)
                    if (ImGui.RadioButton($"corner {i}:  {vs[i].X:0.##}, {vs[i].Y:0.##}, {vs[i].Z:0.##}##navq2v{i}", nav.SelectedVertex == i))
                        nav.SelectedVertex = i;
            if (ImGui.Button("Frame", new Vector2(-1, 0))) nav.RequestFrameSelection = true;
        }

        private void DrawNavCreate_Q2(NavMeshEditor nav)
        {
            ImGui.TextWrapped("Click the corners on the world, then close it. The new polygon is the " +
                              "same kind as the selected one.");
            bool placing = nav.PlacingPoly;
            if (ImGui.Checkbox("Draw polygon", ref placing))
            { nav.PlacingPoly = placing; if (!placing) nav.PendingPoly.Clear(); }
            ImGui.TextDisabled($"{nav.PendingPoly.Count} corner(s) placed");
            ImGui.BeginDisabled(nav.PendingPoly.Count < 3);
            if (ImGui.Button("Close polygon", new Vector2(-1, 0))) nav.RequestClosePoly = true;
            ImGui.EndDisabled();
            ImGui.BeginDisabled(nav.PendingPoly.Count == 0);
            if (ImGui.Button("Discard", new Vector2(-1, 0))) nav.PendingPoly.Clear();
            ImGui.EndDisabled();
            ImGui.Spacing();
            ImGui.BeginDisabled(nav.SelectedPolys.Count == 0);
            if (ImGui.Button($"Delete {Math.Max(nav.SelectedPolys.Count, 1)} selected polygon(s)  (Del)", new Vector2(-1, 0)))
                nav.RequestDeleteSelection = true;
            ImGui.EndDisabled();
        }

        private void DrawNavAdvanced_Q2(NavMeshEditor nav)
        {
            ImGui.Indent(6.0f);

            ImGui.TextDisabled("POINTS AND PORTALS");
            ImGui.TextWrapped("A point is somewhere a ped can be put down facing a heading; a portal is " +
                              "a link peds take off the mesh - a ladder, a drop, a squeeze.");
            if (ImGui.Button("Add a point where I am looking", new Vector2(-1, 0))) nav.RequestAddPoint = true;
            if (ImGui.Button("Add a portal where I am looking", new Vector2(-1, 0))) nav.RequestAddPortal = true;
            ImGui.TextDisabled($"{nav.TotalPoints:N0} points, {nav.TotalPortals:N0} portals open. Click one in the viewport to edit it.");

            ImGui.Spacing();
            ImGui.TextDisabled("GENERATE FROM COLLISION");
            ImGui.TextWrapped("Build walkable polygons over the collision under an area - for a building " +
                              "that has none yet. Every sample is a ray straight down; cells flatter than " +
                              "the slope limit become quads.");
            ImGui.Checkbox("Use area", ref nav.GenUseArea);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Off: a square of the 'Load here' reach, centred on the camera.");
            ImGui.Checkbox("Interior", ref nav.GenInterior);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("What a floor inside an MLO carries. Off: pavement, which is what open ground carries.");
            ImGui.SetNextItemWidth(-1);
            UiSlider_U28.Float("##navq2gendens", ref nav.GenDensity, 0.25f, 4.0f, "grid %.2f m");
            ImGui.SetNextItemWidth(-1);
            UiSlider_U28.Float("##navq2genslope", ref nav.GenSlopeLimit, 5.0f, 70.0f, "slope limit %.0f deg");
            if (ImGui.Button("Generate", new Vector2(-1, 0))) nav.RequestGenerate = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Needs the world collision loaded - turn Collision on in the World workspace's\n" +
                                 "options if nothing is found. The result is one undo step.");

            ImGui.Spacing();
            ImGui.TextDisabled("EVERY OTHER FLAG BIT");
            var p = nav.SelectedPoly;
            if (p == null) ImGui.TextDisabled("no polygon selected");
            else
            {
                ImGui.TextWrapped("The bits the game has never explained - unused, unknown, the four " +
                                  "underground details and the eight slope facings (which the game derives " +
                                  "from the geometry anyway).");
                foreach (var f in NavMeshEditor.OnShelf(NavMeshEditor.Shelf.Advanced))
                    DrawNavFlagCheck_Q2(nav, f);
                ImGui.Spacing();
                ImGui.TextDisabled($"raw  0x{p._RawData.PolyFlags0:X4}  0x{p._RawData.PolyFlags1:X8}  0x{p._RawData.PolyFlags2:X8}");
                ImGui.TextDisabled($"area {p.AreaID}   " +
                                   (p.Edges?.Count(e => e != null && (e.PolyID1 != NavMeshEditor.NoPoly || e.PolyID2 != NavMeshEditor.NoPoly)) ?? 0) +
                                   $" of {p.Edges?.Length ?? 0} edges link to a neighbour");
            }

            ImGui.Spacing();
            ImGui.TextDisabled("KEYS");
            void Row(string k, string what) { ImGui.TextDisabled(k); ImGui.SameLine(120); ImGui.Text(what); }
            Row("Click", "select the polygon under the cursor");
            Row("Ctrl + click", "add to / remove from the selection");
            Row("P", "draw-polygon mode on / off");
            Row("Enter", "close the drawn polygon");
            Row("Esc", "discard the corners / the selection");
            Row("Backspace", "take back the last corner");
            Row("Delete", "delete what is selected");
            Row("F", "frame the selection");
            Row("Q / W / E", "select / move / turn");
            Row("Ctrl+Z / Ctrl+Y", "undo / redo");
            Row("Ctrl+S", "save the active .ynv");

            ImGui.Unindent(6.0f);
        }

        private void DrawNavPointEditor_Q2(NavMeshEditor nav, YnvPoint pt)
        {
            var doc = nav.DocOf(pt.Ynv);
            var pos = new Vector3(pt.Position.X, pt.Position.Y, pt.Position.Z);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat3("##navq2ptpos", ref pos, 0.05f))
                nav.SetField(WorldHistory, doc, pt, "Move nav point", pt.Position,
                             new SDX.Vector3(pos.X, pos.Y, pos.Z), v => pt.Position = v);

            float dir = pt.Direction * 57.29578f;
            ImGui.SetNextItemWidth(-1);
            if (UiSlider_U28.Float("##navq2ptang", ref dir, 0.0f, 360.0f, "faces %.0f deg"))
                nav.SetField(WorldHistory, doc, pt, "Turn nav point", pt.Angle, NavAngleByte_Q2(dir), v => pt.Angle = v);

            int type = pt.Type;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt("kind##navq2pttype", ref type))
                nav.SetField(WorldHistory, doc, pt, "Nav point type", pt.Type,
                             (byte)Math.Clamp(type, 0, 255), v => pt.Type = v);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The values the game ships are 0-5, 128, 171 and 254.");

            if (ImGui.Button("Delete this point  (Del)", new Vector2(-1, 0)))
                nav.DeletePoint(WorldHistory, doc, pt);
        }

        private void DrawNavPortalEditor_Q2(NavMeshEditor nav, YnvPortal po)
        {
            var doc = nav.DocOf(po.Ynv);
            var a = new Vector3(po.PositionFrom.X, po.PositionFrom.Y, po.PositionFrom.Z);
            var b = new Vector3(po.PositionTo.X, po.PositionTo.Y, po.PositionTo.Z);
            ImGui.TextDisabled("from");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat3("##navq2pofrom", ref a, 0.05f))
                nav.SetField(WorldHistory, doc, po, "Move portal start", po.PositionFrom,
                             new SDX.Vector3(a.X, a.Y, a.Z), v => po.PositionFrom = v);
            ImGui.TextDisabled("to");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat3("##navq2poto", ref b, 0.05f))
                nav.SetField(WorldHistory, doc, po, "Move portal end", po.PositionTo,
                             new SDX.Vector3(b.X, b.Y, b.Z), v => po.PositionTo = v);

            float dir = po.Direction * 57.29578f;
            ImGui.SetNextItemWidth(-1);
            if (UiSlider_U28.Float("##navq2poang", ref dir, 0.0f, 360.0f, "faces %.0f deg"))
                nav.SetField(WorldHistory, doc, po, "Turn portal", po.Angle, NavAngleByte_Q2(dir), v => po.Angle = v);

            int type = po.Type;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt("kind##navq2potype", ref type))
                nav.SetField(WorldHistory, doc, po, "Portal type", po.Type,
                             (byte)Math.Clamp(type, 0, 255), v => po.Type = v);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("1, 2 or 3 in the game's own files - climbing, dropping, squeezing through.");

            int pf = po.PolyIDFrom1, pt2 = po.PolyIDTo1;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt("poly from##navq2popf", ref pf))
                nav.SetField(WorldHistory, doc, po, "Portal from poly", po.PolyIDFrom1,
                             (ushort)Math.Clamp(pf, 0, 0x3FFF), v => { po.PolyIDFrom1 = v; po.PolyIDFrom2 = v; });
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt("poly to##navq2popt", ref pt2))
                nav.SetField(WorldHistory, doc, po, "Portal to poly", po.PolyIDTo1,
                             (ushort)Math.Clamp(pt2, 0, 0x3FFF), v => { po.PolyIDTo1 = v; po.PolyIDTo2 = v; });
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Which polygon each end lands on, by index in this file.\nThe list on the left gives the indices.");

            if (ImGui.Button("Delete this portal  (Del)", new Vector2(-1, 0)))
                nav.DeletePortal(WorldHistory, doc, po);
        }

        private static byte NavAngleByte_Q2(float degrees)
        {
            float t = degrees / 360.0f;
            t -= (float)Math.Floor(t);
            return (byte)Math.Clamp(Math.Round(t * 255.0f), 0, 255);
        }

        private void DrawNavHint_Q2(float displayWidth, float displayHeight)
        {
            var nav = Nav;
            if (nav == null || !nav.ShowLegend) return;
            float x0 = (ShowLeftPanel ? settings.LeftPanelWidth : 0.0f) + 10.0f;
            float x1 = displayWidth - (ShowRightPanel ? settings.RightPanelWidth : 0.0f) - 10.0f;
            float w = Math.Max(220.0f, x1 - x0);
            float h = ImGui.GetTextLineHeightWithSpacing() + 12.0f;
            float y = Math.Max(displayHeight - StatusH - h - 8.0f, TopBarHeight + ToolbarH + 8.0f);
            ImGui.SetNextWindowPos(new Vector2(x0, y), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.80f);
            var wf = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                   | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                   | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoFocusOnAppearing
                   | ImGuiWindowFlags.NoNav;
            if (!ImGui.Begin("##navhint", wf)) { ImGui.End(); return; }

            var hint = nav.ClickHint();
            ImGui.TextUnformatted(hint);
            var status = nav.Status ?? "";
            if (status.Length > 0)
            {
                float sw = ImGui.CalcTextSize(status).X;
                float at = ImGui.GetWindowWidth() - sw - 12.0f;
                if (at > ImGui.CalcTextSize(hint).X + 24.0f)
                {
                    ImGui.SameLine(at);
                    ImGui.TextDisabled(status);
                }
            }
            ImGui.End();
        }
    }
}

