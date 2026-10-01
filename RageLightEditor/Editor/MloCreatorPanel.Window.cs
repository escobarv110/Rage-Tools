using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class MloCreatorPanel
    {
        public void DrawWindow(Scene scene, TimecycleData timecycle, float displayW, float displayH)
        {
            if (!WindowVisible || Detached) return;

            PlaceWindow_S1(displayW, displayH);
            if (WindowSizeOverride_P2.X > 0 && WindowSizeOverride_P2.Y > 0)
            {
                ImGui.SetNextWindowSize(WindowSizeOverride_P2, ImGuiCond.Always);
                ImGui.SetNextWindowPos(new Vector2(Math.Max(displayW - WindowSizeOverride_P2.X - 20, 260), 60), ImGuiCond.Always);
            }
            string title = WindowTitle() + "###mlocreatorwindow";
            bool vis = WindowVisible;
            if (!ImGui.Begin(title, ref vis))
            {
                WindowVisible = vis;
                ImGui.End();
                return;
            }
            WindowVisible = vis;
            HandleWindowShortcuts();
            DrawWindowContents(scene, timecycle);
            ImGui.End();
        }

        public string WindowTitle() =>
            Session != null ? "Interior - " + (string.IsNullOrWhiteSpace(Session.Name) ? "MLO Creator" : Session.Name) : "MLO Creator";

        private void DrawWindowContents(Scene scene, TimecycleData timecycle)
        {
            if (Session == null)
            {
                DrawNoSession(scene);
                DrawDetachButton_M1(false);
                return;
            }

            DrawToolbarCompact_S1(scene);
            ImGui.Separator();

            var avail = ImGui.GetContentRegionAvail();
            float statusH = ImGui.GetTextLineHeightWithSpacing() + 6;
            float bodyH = avail.Y - statusH;
            ImGui.BeginChild("##mlocexplorer", new Vector2(Math.Min(explorerWidth, Math.Max(avail.X * 0.44f, 180.0f)), bodyH), ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeX);
            DrawTree(scene, timecycle);
            explorerWidth = ImGui.GetWindowSize().X;
            ImGui.EndChild();
            ImGui.SameLine();
            ImGui.BeginChild("##mlocpage", new Vector2(0, bodyH), ImGuiChildFlags.Borders);
            DrawPage(scene, timecycle);
            ImGui.EndChild();

            ImGui.Separator();
            if (!string.IsNullOrEmpty(Status))
                ImGui.TextColored(StatusIsError ? UiTheme.Danger : UiTheme.Muted, Status);
            else
                ImGui.TextDisabled($"{Session.Rooms.Count} rooms, {Session.Portals.Count} portals, {Session.Entities.Count(e => e.Include)} entities, {Session.EntitySets.Count} sets");
            DrawDetachButton_M1(true);
        }

        private void DrawNoSession(Scene scene)
        {
            ImGui.TextWrapped("Turn what is loaded here into an interior: rooms as boxes, portals as quads, the props " +
                              "assigned to rooms, written to a .ytyp (and a .ymap that places it).");
            ImGui.Spacing();
            if (ImGui.Button("From scene", new Vector2(-1, 0))) RequestStartFromScene = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("An imported .ytyp seeds its rooms, portals and entity sets (editable);\n" +
                                 "loaded .ydr/.yft files become the entities, the biggest one the shell.");
            if (ImGui.Button("Empty interior", new Vector2(-1, 0)))
            {
                Session = new MloCreatorSession();
                if (scene != null) Session.FitBoundsToScene(scene);
                SelectRoom(0);
                SetStatus("New interior. Add rooms, then portals.");
            }
            if (ImGui.Button("Open project...", new Vector2(-1, 0))) RequestOpenProject = true;
        }

        private void DrawToolbar(Scene scene)
        {
            var s = Session;
            if (ImGui.Button("+ Room")) RequestAddRoomAtView = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A room box at the camera's target - or use 'Room from vertices' on the Vertex snap page to click its corners.");
            ImGui.SameLine();
            if (ImGui.Button("+ Portal")) RequestAddPortalAtView = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A door-sized portal at the view - or 'Portal from 4 vertices' on the Vertex snap page.");
            ImGui.SameLine();
            if (ImGui.Button("+ Entity set")) { s.PushUndo("Add entity set"); s.AddEntitySet(""); SelectSet(s.EntitySets.Count - 1); }
            ImGui.SameLine();
            if (ImGui.Button("From shell")) RequestBuildFromShell_R1 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Build the whole interior out of the shell's geometry: a room per enclosed space,\n" +
                                 "a portal per opening. A first cut you then correct - Ctrl+Z undoes it.");
            ImGui.SameLine();
            if (ImGui.Button("Add prop...")) RequestAddProps = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open .ydr / .yft props into the scene; each becomes an entity.");
            ImGui.SameLine();
            DrawToolbarButtons_L3();
            ImGui.SameLine();
            ImGui.TextDisabled("|"); ImGui.SameLine();
            DrawEditToolbar_N3();
            if (ImGui.Button("Duplicate")) DuplicateSelected();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A copy of what is selected, beside it  (Ctrl+D)");
            ImGui.SameLine();
            if (LightPanel.DangerButton("Delete", Vector2.Zero)) DeleteSelected();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete what is selected  (Del). Ctrl+Z brings it back.");
            ImGui.SameLine();
            ImGui.TextDisabled("|"); ImGui.SameLine();
            ImGui.TextDisabled("Labels"); ImGui.SameLine();
            DrawLabelModeControl_V19();
            ImGui.SameLine();
            ImGui.TextDisabled("|"); ImGui.SameLine();

            var h = History;
            bool cu = h != null && h.CanUndo, cr = h != null && h.CanRedo;
            if (!cu) ImGui.BeginDisabled();
            if (ImGui.Button("Undo")) RequestUndo = true;
            if (ImGui.IsItemHovered() && cu) ImGui.SetTooltip("Undo " + h.UndoName + "  (Ctrl+Z)");
            if (!cu) ImGui.EndDisabled();
            ImGui.SameLine();
            if (!cr) ImGui.BeginDisabled();
            if (ImGui.Button("Redo")) RequestRedo = true;
            if (ImGui.IsItemHovered() && cr) ImGui.SetTooltip("Redo " + h.RedoName + "  (Ctrl+Y)");
            if (!cr) ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextDisabled("|"); ImGui.SameLine();

            var problems = s.Validate();
            if (problems.Count > 0) ImGui.BeginDisabled();
            if (ImGui.Button("Write .ytyp...")) RequestSaveYtyp = true;
            if (ImGui.IsItemHovered() && problems.Count == 0) ImGui.SetTooltip("Build and save the interior's .ytyp.");
            ImGui.SameLine();
            if (ImGui.Button("Write .ymap...")) RequestExportYmap = true;
            if (ImGui.IsItemHovered() && problems.Count == 0) ImGui.SetTooltip("Export a .ymap that places one instance of the interior (fields on the Interior page).");
            if (problems.Count > 0) ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Save project")) RequestSaveProject = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save the whole interior to its .mloproj (its own file, kept apart from the light editor).");
            ImGui.SameLine();
            if (ImGui.Button("Open...")) RequestOpenProject = true;
            if (problems.Count > 0)
            {
                ImGui.SameLine();
                ImGui.TextColored(UiTheme.Warn, "! " + problems[0]);
            }
        }

        private void DrawTree(Scene scene, TimecycleData timecycle)
        {
            var s = Session;
            bool reveal = RevealSelection; RevealSelection = false;

            var rootFlags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
            if (Page == PageKind.Interior) rootFlags |= ImGuiTreeNodeFlags.Selected;
            bool rootOpen = ImGui.TreeNodeEx(s.Name + "###mlocroot", rootFlags);
            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) ShowPage(PageKind.Interior);
            if (!rootOpen) return;

            bool roomsOpen = ImGui.TreeNodeEx($"Rooms ({s.Rooms.Count})###mlocrooms", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth);
            if (roomsOpen)
            {
                for (int i = 0; i < s.Rooms.Count; i++)
                {
                    var r = s.Rooms[i];
                    int n = s.CountInRoom(i);
                    var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
                    if (SelectedRoom == i && Page == PageKind.Room) flags |= ImGuiTreeNodeFlags.Selected;
                    if (n == 0) flags |= ImGuiTreeNodeFlags.Leaf;
                    if (reveal && SelectedRoom == i && Page == PageKind.Room) ImGui.SetNextItemOpen(true);
                    string label = $"{i}: {r.Name}{(i == 0 ? " (limbo)" : "")}{(r.IsValid ? "" : " [empty]")}###mlocroom{i}";
                    bool ro = ImGui.TreeNodeEx(label, flags);
                    RoomNodeDropTarget_O3(i);
                    if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) SelectRoom(i);
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) { SelectRoom(i); RequestFrameSelected = true; }
                    if (reveal && SelectedRoom == i && Page == PageKind.Room) ImGui.SetScrollHereY();
                    if (ro)
                    {
                        for (int ei = 0; ei < s.Entities.Count; ei++)
                        {
                            var e = s.Entities[ei];
                            if (!e.Include || !string.IsNullOrEmpty(e.EntitySet) || e.Room != i) continue;
                            DrawEntityLeaf(ei, reveal);
                        }
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            bool portalsOpen = ImGui.TreeNodeEx($"Portals ({s.Portals.Count})###mlocportals", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth);
            if (portalsOpen)
            {
                for (int i = 0; i < s.Portals.Count; i++)
                {
                    var p = s.Portals[i];
                    var flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                    if (SelectedPortal == i && Page == PageKind.Portal) flags |= ImGuiTreeNodeFlags.Selected;
                    string kind = (p.Flags & 4u) != 0 ? "  mirror" : (p.Flags & 1u) != 0 ? "  one-way" : (p.Flags & 2u) != 0 ? "  link" : "";
                    ImGui.TreeNodeEx($"{i}: {NameOnly(p.RoomFrom)} -> {NameOnly(p.RoomTo)}{kind}###mlocportal{i}", flags);
                    if (ImGui.IsItemClicked()) SelectPortal(i);
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) { SelectPortal(i); RequestFrameSelected = true; }
                    if (reveal && SelectedPortal == i && Page == PageKind.Portal) ImGui.SetScrollHereY();
                }
                ImGui.TreePop();
            }

            bool setsOpen = ImGui.TreeNodeEx($"Entity sets ({s.EntitySets.Count})###mlocsets", ImGuiTreeNodeFlags.SpanAvailWidth);
            if (setsOpen)
            {
                for (int i = 0; i < s.EntitySets.Count; i++)
                {
                    var sn = s.EntitySets[i];
                    int n = s.Entities.Count(e => e.Include && e.EntitySet == sn);
                    var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
                    if (SelectedSet == i && Page == PageKind.Set) flags |= ImGuiTreeNodeFlags.Selected;
                    if (n == 0) flags |= ImGuiTreeNodeFlags.Leaf;
                    bool so = ImGui.TreeNodeEx($"{sn} ({n})###mlocset{i}", flags);
                    if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) SelectSet(i);
                    if (so)
                    {
                        for (int ei = 0; ei < s.Entities.Count; ei++)
                            if (s.Entities[ei].Include && s.Entities[ei].EntitySet == sn) DrawEntityLeaf(ei, reveal);
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            var tcs = s.Rooms.Where(r => !string.IsNullOrWhiteSpace(r.Timecycle) || !string.IsNullOrWhiteSpace(r.SecondaryTimecycle)).ToList();
            var tcFlags = ImGuiTreeNodeFlags.SpanAvailWidth | (Page == PageKind.Timecycles ? ImGuiTreeNodeFlags.Selected : 0) | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
            ImGui.TreeNodeEx($"Timecycles ({tcs.Count})###mloctc", tcFlags);
            if (ImGui.IsItemClicked()) ShowPage(PageKind.Timecycles);

            bool toolsOpen = ImGui.TreeNodeEx("Tools###mloctools", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth);
            if (toolsOpen)
            {
                var f1 = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen | (Page == PageKind.Snap ? ImGuiTreeNodeFlags.Selected : 0);
                ImGui.TreeNodeEx("Vertex snap & corners###mlocsnapnode", f1);
                if (ImGui.IsItemClicked()) ShowPage(PageKind.Snap);
                DrawToolNodes_L3();
                ImGui.TreePop();
            }

            ImGui.TreePop();
        }

        private void DrawEntityLeaf(int ei, bool reveal)
        {
            var e = Session.Entities[ei];
            var flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen;
            if (SelectedEntity == ei && Page == PageKind.Entity) flags |= ImGuiTreeNodeFlags.Selected;
            ImGui.TreeNodeEx($"{e.Label}###mlocent{ei}", flags);
            EntityLeafDragSource_O3(ei);
            if (ImGui.IsItemClicked()) SelectEntityMulti(ei, ImGui.GetIO().KeyCtrl);
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) { SelectEntityMulti(ei, false); RequestFrameSelected = true; }
            if (reveal && SelectedEntity == ei && Page == PageKind.Entity) ImGui.SetScrollHereY();
        }

        private void DuplicateSelected()
        {
            var s = Session;
            if (Page == PageKind.Room && CurrentRoom != null && SelectedRoom > 0)
            {
                s.PushUndo("Duplicate room");
                var r = CurrentRoom.Clone(); r.Name = r.Name + "_copy";
                var off = new SDX.Vector3(r.Size.X + 0.2f, 0, 0);
                r.Min += off; r.Max += off;
                s.Rooms.Add(r); s.AutoAssignRooms(); SelectRoom(s.Rooms.Count - 1);
                SetStatus($"Duplicated room to '{r.Name}'.");
            }
            else if (Page == PageKind.Portal && CurrentPortal != null)
            {
                s.PushUndo("Duplicate portal");
                var p = CurrentPortal.Clone();
                for (int i = 0; i < p.Corners.Length; i++) p.Corners[i] += new SDX.Vector3(0.3f, 0.3f, 0);
                s.Portals.Add(p); SelectPortal(s.Portals.Count - 1);
                SetStatus("Duplicated portal.");
            }
            else if (Page == PageKind.Entity && SelectedEntity >= 0 && SelectedEntity < s.Entities.Count)
                RequestDuplicateEntities = true;
            else SetStatus("Nothing duplicable is selected.", true);
        }

        private void DeleteSelected()
        {
            var s = Session;
            if (Page == PageKind.Room && SelectedRoom > 0)
            {
                s.PushUndo("Delete room " + SelectedRoom);
                s.RemoveRoom(SelectedRoom); ClampSelection(); ShowPage(PageKind.Interior);
                SetStatus("Room deleted.");
            }
            else if (Page == PageKind.Portal && SelectedPortal >= 0)
            {
                s.PushUndo("Delete portal " + SelectedPortal);
                s.RemovePortal(SelectedPortal); ClampSelection(); ShowPage(PageKind.Interior);
                SetStatus("Portal deleted.");
            }
            else if (Page == PageKind.Entity && SelectedEntity >= 0)
                RequestDeleteEntities = true;
            else if (Page == PageKind.Set && SelectedSet >= 0 && SelectedSet < s.EntitySets.Count)
            {
                s.PushUndo("Delete entity set");
                var sn = s.EntitySets[SelectedSet];
                s.RemoveEntitySet(sn); s.YmapDefaultSets.Remove(sn); ClampSelection(); ShowPage(PageKind.Interior);
                SetStatus($"Entity set '{sn}' deleted.");
            }
            else SetStatus("Select a room, portal, entity or set to delete (room 0 / limbo stays).", true);
        }

        private void HandleWindowShortcuts()
        {
            if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)) return;
            var io = ImGui.GetIO();
            if (io.WantTextInput) return;
            if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z)) RequestUndo = true;
            if (io.KeyCtrl && (ImGui.IsKeyPressed(ImGuiKey.Y) || (io.KeyShift && ImGui.IsKeyPressed(ImGuiKey.Z)))) RequestRedo = true;
            if (ImGui.IsKeyPressed(ImGuiKey.Escape) && (SnapMode || PickingPortalCorners || PickingRoomCorners || PickingRoomMesh))
            {
                bool picking = PickingPortalCorners || PickingRoomCorners;
                CancelPicking(); EndSnap(); CancelRoomMeshPick();
                SetStatus(picking ? "Picking cancelled." : "Snap cancelled.");
            }
        }
    }
}

