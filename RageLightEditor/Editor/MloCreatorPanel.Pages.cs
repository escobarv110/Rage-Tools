using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class MloCreatorPanel
    {
        private void Track(string name, string key)
        {
            Session.PushUndo(name, key);
        }
        private void SealHere(string key)
        {
            if (ImGui.IsItemDeactivatedAfterEdit()) History?.Seal(key);
        }

        private void DrawPage(Scene scene, TimecycleData timecycle)
        {
            var s = Session;
            switch (Page)
            {
                case PageKind.Room: if (CurrentRoom != null) DrawRoomPage(scene, timecycle); else DrawInteriorPage(scene); break;
                case PageKind.Portal: if (CurrentPortal != null) DrawPortalPage(); else DrawInteriorPage(scene); break;
                case PageKind.Entity: if (SelectedEntity >= 0 && SelectedEntity < s.Entities.Count) DrawEntityPage(); else DrawInteriorPage(scene); break;
                case PageKind.Set: if (SelectedSet >= 0 && SelectedSet < s.EntitySets.Count) DrawSetPage(); else DrawInteriorPage(scene); break;
                case PageKind.Snap: DrawSnapSection(); break;
                case PageKind.Timecycles: DrawTimecyclesPage(); break;
                case PageKind.Lights: DrawLightsPage_L3(); break;
                case PageKind.Assets: DrawAssetsPage_L3(scene); break;
                default: DrawInteriorPage(scene); break;
            }
        }

        private void DrawInteriorPage(Scene scene)
        {
            var s = Session;
            ImGui.TextDisabled("INTERIOR (the MLO archetype)");
            ImGui.SetNextItemWidth(-110);
            if (ImGui.InputText("Name##mlon", ref s.Name, 64)) Track("Rename interior", "mlo.name");
            SealHere("mlo.name");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The archetype's name - what the ymap's MLO instance refers to. Lower case.");
            ImGui.SetNextItemWidth(-110);
            if (ImGui.InputText("Txd##mlot", ref s.TextureDictionary, 64)) Track("Edit txd", "mlo.txd");
            SealHere("mlo.txd");
            ImGui.SetNextItemWidth(-110);
            if (ImGui.InputText("Physics##mlop", ref s.PhysicsDictionary, 64)) Track("Edit physics", "mlo.phys");
            SealHere("mlo.phys");

            if (scene != null && scene.Files.Count > 0)
            {
                var files = scene.Files.Where(f => !f.FromMlo).ToList();
                string cur = s.ShellFile != null ? s.ShellFile.Name : (string.IsNullOrEmpty(s.ShellName) ? "(none - assetless)" : s.ShellName + " (imported)");
                ImGui.SetNextItemWidth(-110);
                if (ImGui.BeginCombo("Shell##mlos", cur))
                {
                    if (ImGui.Selectable("(none - assetless)", s.ShellFile == null && s.SourceArchetype == null))
                    {
                        Track("Set shell", null);
                        if (s.ShellFile != null) s.AddEntityFromFile(s.ShellFile);
                        s.ShellFile = null; s.AssetLess = true; s.AutoAssignRooms();
                    }
                    foreach (var f in files)
                    {
                        if (ImGui.Selectable(f.Name, f == s.ShellFile) && f != s.ShellFile)
                        {
                            Track("Set shell", null);
                            if (s.ShellFile != null) s.AddEntityFromFile(s.ShellFile);
                            s.Entities.RemoveAll(e => e.SourceFile == f);
                            s.ShellFile = f; s.AssetLess = false;
                            s.ShellName = System.IO.Path.GetFileNameWithoutExtension(f.Name).ToLowerInvariant();
                            if (string.IsNullOrEmpty(s.TextureDictionary)) s.TextureDictionary = s.ShellName;
                if (string.IsNullOrEmpty(s.PhysicsDictionary) &&
                    (MloCreatorSession.SiblingYbn_V31(f.Path) != null ||
                     (MloCreatorSession.Drawable(f) as CodeWalker.GameFiles.Drawable)?.Bound != null))
                    s.PhysicsDictionary = s.ShellName;
                            s.FitBoundsToScene(scene); s.AutoAssignRooms();
                        }
                    }
                    ImGui.EndCombo();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The model that IS the interior (walls, floors): the archetype's own drawable.\nEverything else loaded becomes an entity in a room.");
                if (s.ShellFile != null && !string.Equals(System.IO.Path.GetFileNameWithoutExtension(s.ShellFile.Name), s.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                    ImGui.TextColored(UiTheme.Warn, $"! the game finds the shell by the interior's name: ship it as {s.Name.Trim().ToLowerInvariant()}.ydr");

                if (ImGui.Button("Import collision...##v34", new Vector2(-1, 0)))
                    RequestImportShellYbn_V34 = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Pick the collision file for this interior. It is named in the .ytyp so the game finds it, and copied beside the .ytyp when you export.");
                if (!string.IsNullOrEmpty(s.ShellYbnNote_V34))
                {
                    ImGui.TextColored(UiTheme.AccentBright, s.ShellYbnNote_V34);
                    if (ImGui.SmallButton("forget it##v34ybn")) { s.ClearShellYbn_V34(); Track("Clear ybn", "mlo.ybn"); }
                }
                else if (!string.IsNullOrWhiteSpace(s.PhysicsDictionary))
                {
                    if (s.ShellYbnResolves_V34(out var whereYbn))
                        ImGui.TextDisabled("collision: " + System.IO.Path.GetFileName(whereYbn));
                    else
                        ImGui.TextColored(UiTheme.Warn, $"! the .ytyp names '{s.PhysicsDictionary}' but no such .ybn was found - import it here.");
                }

                bool shellLimbo = s.ShellGoesInLimbo_V33;
                if (ImGui.Checkbox("Shell in limbo##v33", ref shellLimbo))
                {
                    s.ShellInLimbo_V33 = shellLimbo;
                    Track("Shell in limbo", "mlo.shelllimbo");
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Put the shell in room 0 alongside the props, instead of relying on the archetype's own drawable.");
                {
                    var note = s.ShellInLimboNote_V33;
                    if (s.ShellDrawnTwice_V34)
                    {
                        ImGui.TextColored(UiTheme.Warn, note);
                        if (ImGui.SmallButton("Make it assetless##v34al"))
                        {
                            s.AssetLess = true;
                            Track("Assetless", "mlo.assetless");
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("The archetype stops naming its own drawable, so the shell is drawn once - as the limbo entity.");
                    }
                    else ImGui.TextDisabled(note);
                }
                if (s.ShellInLimbo_V33.HasValue)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton("auto##v33shell")) s.ShellInLimbo_V33 = null;
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Back to following the asset type.");
                }
            }
            DrawShellBuild_R1();
            DrawRemoveYtypBlock_S1(scene);
            ImGui.SetNextItemWidth(120);
            if (ImGui.DragFloat("LOD dist##mlold", ref s.LodDist, 1.0f, 0, 5000, "%.0f")) Track("Edit LOD dist", "mlo.lod");
            SealHere("mlo.lod");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.DragFloat("HD tex##mlohd", ref s.HdTextureDist, 1.0f, 0, 5000, "%.0f")) Track("Edit HD dist", "mlo.hd");
            SealHere("mlo.hd");
            int fl = unchecked((int)s.Flags), mfl = unchecked((int)s.MloFlags);
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Flags##mlof", ref fl, 0, 0)) { Track("Edit flags", "mlo.flags"); s.Flags = unchecked((uint)Math.Max(fl, 0)); }
            SealHere("mlo.flags");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("MLO flags##mlomf", ref mfl, 0, 0)) { Track("Edit MLO flags", "mlo.mflags"); s.MloFlags = unchecked((uint)Math.Max(mfl, 0)); }
            SealHere("mlo.mflags");

            ImGui.TextDisabled("BOUNDING BOX");
            if (s.ShellBoundsKnown)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(s.BBoxManual ? "(typed - Fit box to scene follows the shell again)" : "(= the shell's bounds, " + s.ShellBoundsSource + ")");
            }
            var mn = V(s.BBMin); var mx = V(s.BBMax);
            ImGui.SetNextItemWidth(-110);
            if (ImGui.DragFloat3("BB min##mlobmin", ref mn, 0.05f)) { Track("Edit box", "mlo.bbmin"); s.BBMin = V(mn); s.BBoxManual = true; }
            SealHere("mlo.bbmin");
            ImGui.SetNextItemWidth(-110);
            if (ImGui.DragFloat3("BB max##mlobmax", ref mx, 0.05f)) { Track("Edit box", "mlo.bbmax"); s.BBMax = V(mx); s.BBoxManual = true; }
            SealHere("mlo.bbmax");
            if (ImGui.SmallButton("Fit box to scene")) { s.PushUndo("Fit box to scene"); s.BBoxManual = false; s.InvalidateShellBounds(); if (!s.SyncToShell(scene)) s.FitBoundsToScene(scene); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The archetype's box (and limbo's) from the shell's geometry, else everything loaded - and it keeps following the shell from then on.");
            ImGui.SameLine();
            if (ImGui.Checkbox("Prop archetypes", ref s.IncludePropArchetypes)) Track("Toggle prop archetypes", null);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Also write a CBaseArchetypeDef for every loose .ydr/.yft the entities name,\nso the ytyp stands on its own. Off when those props have a ytyp already.");

            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Ymap placement (where the interior sits in the world)"))
            {
                ImGui.SetNextItemWidth(-110);
                if (ImGui.InputTextWithHint("Ymap##ymn", s.Name + "_placement", ref s.YmapName, 64)) Track("Edit ymap name", "ym.name");
                SealHere("ym.name");
                var yp = V(s.YmapPosition);
                ImGui.SetNextItemWidth(-110);
                if (ImGui.DragFloat3("Position##ymp", ref yp, 0.1f)) { Track("Move placement", "ym.pos"); s.YmapPosition = V(yp); }
                SealHere("ym.pos");
                ImGui.SetNextItemWidth(110);
                if (ImGui.DragFloat("Heading##ymh", ref s.YmapHeadingDeg, 0.5f, -180, 180, "%.1f deg")) Track("Rotate placement", "ym.head");
                SealHere("ym.head");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60);
                if (ImGui.InputInt("Group##ymg", ref s.YmapGroupId, 0, 0)) Track("Edit group", "ym.grp");
                SealHere("ym.grp");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(60);
                if (ImGui.InputInt("Floor##ymf", ref s.YmapFloorId, 0, 0)) Track("Edit floor", "ym.flr");
                SealHere("ym.flr");
                if (s.EntitySets.Count > 0)
                {
                    ImGui.TextDisabled("default entity sets:");
                    foreach (var sn in s.EntitySets)
                    {
                        ImGui.SameLine();
                        bool on = s.YmapDefaultSets.Contains(sn);
                        if (ImGui.Checkbox(sn + "##yds", ref on)) { Track("Toggle default set", null); if (on) { if (!s.YmapDefaultSets.Contains(sn)) s.YmapDefaultSets.Add(sn); } else s.YmapDefaultSets.Remove(sn); }
                    }
                }
                if (!string.IsNullOrEmpty(s.LastYmapPath)) ImGui.TextDisabled("saved: " + System.IO.Path.GetFileName(s.LastYmapPath));
            }
            ImGui.Spacing();
            if (ImGui.Button("Add to project", new Vector2(-1, 0))) RequestAddToProject = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Build the ytyp and put it in the World Project window - Save All writes it.");
            if (!string.IsNullOrEmpty(s.LastSavedPath)) ImGui.TextDisabled("ytyp saved: " + System.IO.Path.GetFileName(s.LastSavedPath));
        }

        private void DrawRoomPage(Scene scene, TimecycleData timecycle)
        {
            var s = Session; var room = CurrentRoom;
            ImGui.TextDisabled(SelectedRoom == 0 ? "ROOM 0 - LIMBO (required; anything outside every room lands here)" : $"ROOM {SelectedRoom}");
            ImGui.SetNextItemWidth(-90);
            if (ImGui.InputText("Name##rn", ref room.Name, 64)) Track("Rename room", "room.name");
            SealHere("room.name");

            bool limboLocked = SelectedRoom == 0 && s.ShellBoundsKnown;
            ImGui.TextDisabled(limboLocked ? "BOX  (= the shell's bounds, " + s.ShellBoundsSource + " - always; the shell defines limbo)"
                                           : "BOX  (drag the corner handles in the viewport, snap the corners, or edit here)");
            var rmn = V(room.Min); var rmx = V(room.Max);
            if (limboLocked)
            {
                ImGui.BeginDisabled();
                ImGui.SetNextItemWidth(-90); ImGui.DragFloat3("Min##rmin", ref rmn, 0.02f);
                ImGui.SetNextItemWidth(-90); ImGui.DragFloat3("Max##rmax", ref rmx, 0.02f);
                ImGui.EndDisabled();
                ImGui.TextDisabled("Limbo is recomputed when the shell changes; the interior's own box follows it too (Interior page).");
            }
            else
            {
                ImGui.SetNextItemWidth(-90);
                if (ImGui.DragFloat3("Min##rmin", ref rmn, 0.02f)) { Track("Resize room", "room.min"); room.Min = V(rmn); s.AutoAssignRooms(); }
                SealHere("room.min");
                ImGui.Indent(12);
                DrawSnapButton_L4(SnapTargetKind.RoomMin, 0, "##l4rmin", "Snap mode: click a vertex in the viewport - it becomes this room's MIN corner\n(snap MIN then MAX: the room is exactly the box between the two vertices).");
                ImGui.SameLine();
                if (!HasSnapPoint) ImGui.BeginDisabled();
                if (ImGui.SmallButton("min = placement point##rsnmin")) { RequestSnapPointToRoomMin = true; }
                if (!HasSnapPoint) ImGui.EndDisabled();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(HasSnapPoint ? "Set this corner to the placement point (the last clicked / sent vertex)." : "No placement point yet - click a vertex with V held, or send one from 3ds Max.");
                ImGui.Unindent(12);
                ImGui.SetNextItemWidth(-90);
                if (ImGui.DragFloat3("Max##rmax", ref rmx, 0.02f)) { Track("Resize room", "room.max"); room.Max = V(rmx); s.AutoAssignRooms(); }
                SealHere("room.max");
                ImGui.Indent(12);
                DrawSnapButton_L4(SnapTargetKind.RoomMax, 0, "##l4rmax", "Snap mode: click a vertex in the viewport - it becomes this room's MAX corner\n(snap MIN then MAX: the room is exactly the box between the two vertices).");
                ImGui.SameLine();
                if (!HasSnapPoint) ImGui.BeginDisabled();
                if (ImGui.SmallButton("max = placement point##rsnmax")) { RequestSnapPointToRoomMax = true; }
                if (!HasSnapPoint) ImGui.EndDisabled();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(HasSnapPoint ? "Set this corner to the placement point (the last clicked / sent vertex)." : "No placement point yet - click a vertex with V held, or send one from 3ds Max.");
                ImGui.Unindent(12);
                var c = V(room.Centre); var sz = V(room.Size);
                ImGui.SetNextItemWidth(-90);
                if (ImGui.DragFloat3("Centre##rc", ref c, 0.02f)) { Track("Move room", "room.c"); var half = room.Size * 0.5f; room.Min = V(c) - half; room.Max = V(c) + half; s.AutoAssignRooms(); }
                SealHere("room.c");
                ImGui.SetNextItemWidth(-90);
                if (ImGui.DragFloat3("Size##rs", ref sz, 0.02f, 0.05f, 1000.0f)) { Track("Size room", "room.sz"); var cc = room.Centre; var half = V(sz) * 0.5f; room.Min = cc - half; room.Max = cc + half; s.AutoAssignRooms(); }
                SealHere("room.sz");
                DrawRoomCaptureRow_M1();
            }

            ImGui.TextDisabled("TIMECYCLE");
            ImGui.SetNextItemWidth(-90);
            if (ImGui.InputText("Timecycle##rtc", ref room.Timecycle, 64)) Track("Edit timecycle", "room.tc");
            SealHere("room.tc");
            if (timecycle != null && timecycle.Modifiers.Count > 0)
            {
                ImGui.SameLine();
                if (ImGui.BeginCombo("##rtcpick", "", ImGuiComboFlags.NoPreview))
                {
                    ImGui.SetNextItemWidth(160);
                    ImGui.InputTextWithHint("##tcfilter", "filter", ref tcFilter, 32);
                    int shown = 0;
                    foreach (var m in timecycle.Modifiers)
                    {
                        if (tcFilter.Length > 0 && m.Name.IndexOf(tcFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (ImGui.Selectable(m.Name, m.Name == room.Timecycle)) { Track("Set timecycle", null); room.Timecycle = m.Name; }
                        if (++shown > 400) break;
                    }
                    ImGui.EndCombo();
                }
            }
            ImGui.SetNextItemWidth(-90);
            if (ImGui.InputText("Secondary##rtc2", ref room.SecondaryTimecycle, 64)) Track("Edit secondary tc", "room.tc2");
            SealHere("room.tc2");
            ImGui.SetNextItemWidth(100);
            if (ImGui.DragFloat("Blend##rb", ref room.Blend, 0.01f, 0.0f, 1.0f)) Track("Edit blend", "room.blend");
            SealHere("room.blend");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70);
            if (ImGui.InputInt("Floor##rf", ref room.FloorId, 0, 0)) Track("Edit floor", "room.floor");
            SealHere("room.floor");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70);
            if (ImGui.InputInt("Ext. depth##rev", ref room.ExteriorVisibilityDepth, 0, 0)) Track("Edit ext. depth", "room.evd");
            SealHere("room.evd");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("exteriorVisibiltyDepth: -1 = no limit (vanilla rooms carry -1).");

            ImGui.TextDisabled("FLAGS");
            int rfl = unchecked((int)room.Flags);
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("Flags##rflags", ref rfl, 0, 0)) { Track("Edit room flags", "room.flags"); room.Flags = unchecked((uint)Math.Max(rfl, 0)); }
            SealHere("room.flags");
            ImGui.SameLine();
            bool open = roomFlagsOpen == SelectedRoom;
            if (ImGui.SmallButton(open ? "hide bits" : "bits...")) roomFlagsOpen = open ? -1 : SelectedRoom;
            if (roomFlagsOpen == SelectedRoom)
            {
                for (int i = 0; i < RoomFlagNames.Length; i++)
                {
                    bool on = (room.Flags & (1u << i)) != 0;
                    if (ImGui.Checkbox(RoomFlagNames[i] + "##rfb" + i, ref on)) { Track("Toggle room flag", null); room.Flags = on ? (room.Flags | (1u << i)) : (room.Flags & ~(1u << i)); }
                }
            }
            ImGui.Spacing();
            if (ImGui.Button("Frame##rframe")) RequestFrameSelected = true;
            ImGui.SameLine();
            if (ImGui.Button("Selected props here")) RequestAssignSelectedPropsToRoom = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Put the props selected in the viewport into this room (an override).");
            ImGui.Checkbox("Gizmo##rg", ref GizmoEnabled);
            ImGui.SameLine();
            ImGui.RadioButton("Move##rgm", ref GizmoMode, 0);
            ImGui.SameLine();
            ImGui.RadioButton("Size##rgs", ref GizmoMode, 1);
        }

        private void DrawPortalPage()
        {
            var s = Session; var portal = CurrentPortal;
            ImGui.TextDisabled($"PORTAL {SelectedPortal}");
            DrawRoomCombo("From##pfrom", ref portal.RoomFrom);
            ImGui.SameLine();
            DrawRoomCombo("To##pto", ref portal.RoomTo);
            ImGui.SameLine();
            if (ImGui.SmallButton("Flip")) { s.PushUndo("Flip portal"); (portal.RoomFrom, portal.RoomTo) = (portal.RoomTo, portal.RoomFrom); Array.Reverse(portal.Corners); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Swap from/to and reverse the winding (the quad's facing).");
            ImGui.SameLine();
            if (ImGui.SmallButton("Re-wind")) { s.PushUndo("Re-wind portal"); if (!s.OrientPortal(portal)) SetStatus("Winding already matches from -> to."); else SetStatus("Corner order flipped to the game's rule (from -> to)."); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Set the corner order to the game's rule: the quad's normal points from roomFrom into roomTo.");

            ImGui.TextDisabled("FLAGS");
            int pfl = unchecked((int)portal.Flags);
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("Flags##pflags", ref pfl, 0, 0)) { Track("Edit portal flags", "portal.flags"); portal.Flags = unchecked((uint)Math.Max(pfl, 0)); }
            SealHere("portal.flags");
            ImGui.SameLine();
            bool open = portalFlagsOpen == SelectedPortal;
            if (ImGui.SmallButton(open ? "hide bits##p" : "bits...##p")) portalFlagsOpen = open ? -1 : SelectedPortal;
            bool oneWay = (portal.Flags & 1u) != 0, mirror = (portal.Flags & 4u) != 0, link = (portal.Flags & 2u) != 0;
            if (ImGui.Checkbox("One-way##pow", ref oneWay)) { Track("Toggle one-way", null); portal.Flags = oneWay ? portal.Flags | 1u : portal.Flags & ~1u; }
            ImGui.SameLine();
            if (ImGui.Checkbox("Mirror##pmi", ref mirror)) { Track("Toggle mirror", null); portal.Flags = mirror ? portal.Flags | 4u : portal.Flags & ~4u; }
            ImGui.SameLine();
            if (ImGui.Checkbox("Links interiors##pli", ref link)) { Track("Toggle link", null); portal.Flags = link ? portal.Flags | 2u : portal.Flags & ~2u; }
            if (portalFlagsOpen == SelectedPortal)
            {
                for (int i = 0; i < PortalFlagNames.Length; i++)
                {
                    bool on = (portal.Flags & (1u << i)) != 0;
                    if (ImGui.Checkbox(PortalFlagNames[i] + "##pfb" + i, ref on)) { Track("Toggle portal flag", null); portal.Flags = on ? (portal.Flags | (1u << i)) : (portal.Flags & ~(1u << i)); }
                }
            }
            int mp = (int)portal.MirrorPriority, op = (int)portal.Opacity, ao = (int)portal.AudioOcclusion;
            ImGui.SetNextItemWidth(70);
            if (ImGui.InputInt("Mirror pri.##pmp", ref mp, 0, 0)) { Track("Edit mirror pri.", "portal.mp"); portal.MirrorPriority = (uint)Math.Max(mp, 0); }
            SealHere("portal.mp");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70);
            if (ImGui.InputInt("Opacity##pop", ref op, 0, 0)) { Track("Edit opacity", "portal.op"); portal.Opacity = (uint)Math.Clamp(op, 0, 100); }
            SealHere("portal.op");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70);
            if (ImGui.InputInt("Audio occl.##pao", ref ao, 0, 0)) { Track("Edit audio occl.", "portal.ao"); portal.AudioOcclusion = (uint)Math.Max(ao, 0); }
            SealHere("portal.ao");

            ImGui.TextDisabled("CORNERS  (order = the loop the game wants; corner 0's edge is red in the viewport)");
            for (int i = 0; i < portal.Corners.Length; i++)
            {
                var cv = V(portal.Corners[i]);
                ImGui.SetNextItemWidth(-90);
                if (ImGui.DragFloat3($"Corner {i}##pc{i}", ref cv, 0.01f)) { Track("Move corner", "portal.c" + i); portal.Corners[i] = V(cv); }
                SealHere("portal.c" + i);
                ImGui.Indent(12);
                DrawSnapButton_L4(SnapTargetKind.PortalCorner, i, $"##l4pc{i}", "Snap mode: click a vertex in the viewport - it becomes this corner.");
                ImGui.SameLine();
                if (ImGui.SmallButton($"nearest vertex##pcs{i}")) { SelectedCorner = i; FocusKind = 1; CornerMode = true; RequestSnapCornerToNearestVertex = true; RequestSnapCornerIndex = i; }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Snap this corner to the nearest mesh vertex (no click needed).");
                ImGui.Unindent(12);
            }
            DrawSnapAllCornersButton_L4(); ImGui.SameLine();
            if (ImGui.SmallButton("Add corner")) { s.PushUndo("Add corner"); AppendPortalCorner(portal); }
            if (portal.Corners.Length > 3) { ImGui.SameLine(); if (ImGui.SmallButton("Remove last")) { s.PushUndo("Remove corner"); portal.Corners = portal.Corners.Take(portal.Corners.Length - 1).ToArray(); } }
            ImGui.SameLine();
            ImGui.Checkbox("Corner handles##pch", ref CornerMode);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Draggable handle on each corner in the viewport (hold V to land on a vertex).");

            ImGui.TextDisabled($"ATTACHED ENTITIES ({portal.Attached.Count})");
            for (int k = 0; k < portal.Attached.Count; k++)
            {
                int ei = portal.Attached[k];
                string nm = ei >= 0 && ei < s.Entities.Count ? s.Entities[ei].Label : "(missing)";
                ImGui.TextUnformatted($"  {ei}: {nm}");
                ImGui.SameLine();
                if (ImGui.SmallButton($"x##patt{k}")) { s.PushUndo("Detach entity"); portal.Attached.RemoveAt(k); break; }
            }
            if (SelectedEntity >= 0 && SelectedEntity < s.Entities.Count && !portal.Attached.Contains(SelectedEntity))
                if (ImGui.SmallButton($"Attach entity {SelectedEntity} ({s.Entities[SelectedEntity].Label})")) { s.PushUndo("Attach entity"); portal.Attached.Add(SelectedEntity); }
            ImGui.Spacing();
            if (ImGui.Button("Frame##pframe")) RequestFrameSelected = true;
        }

        private static void AppendPortalCorner(MloCreatorPortal p)
        {
            var last = p.Corners.Length > 0 ? p.Corners[p.Corners.Length - 1] : SDX.Vector3.Zero;
            var grown = new SDX.Vector3[p.Corners.Length + 1];
            Array.Copy(p.Corners, grown, p.Corners.Length);
            grown[p.Corners.Length] = last + new SDX.Vector3(0.2f, 0, 0);
            p.Corners = grown;
        }

        private void DrawEntityPage()
        {
            var s = Session; var e = s.Entities[SelectedEntity];
            ImGui.TextDisabled("ENTITY");
            ImGui.SetNextItemWidth(-90);
            if (ImGui.InputText("Archetype##ean", ref e.ArchetypeName, 64)) Track("Rename entity", "ent.name");
            if (ImGui.IsItemDeactivatedAfterEdit()) RequestRenameResolve_V67 = SelectedEntity;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The name IS the model: type another prop's name and its model takes this one's place.");
            SealHere("ent.name");
            var p = V(e.Position);
            ImGui.SetNextItemWidth(-90);
            if (ImGui.DragFloat3("Position##epos", ref p, 0.01f)) { Track("Move entity", "ent.pos"); e.Position = V(p); s.AutoAssignRooms(); EntityMoved = true; }
            SealHere("ent.pos");
            DrawEntityTransform_N3(e);
            DrawSnapButton_L4(SnapTargetKind.Entity, 0, "##l4ent", "Snap mode: click a vertex in the viewport - the entity moves onto it."); ImGui.SameLine(); ImGui.TextDisabled("position to a vertex");
            if (SelectedEntity >= 0 && HasSnapPoint) { if (ImGui.SmallButton("move to placement point")) RequestSnapPointToEntity = true; }

            var scl = V(e.Scale);
            ImGui.SetNextItemWidth(-90);
            if (ImGui.DragFloat3("Scale##escl", ref scl, 0.01f, 0.01f, 100f)) { Track("Scale entity", "ent.scl"); e.Scale = V(scl); EntityMoved = true; }
            SealHere("ent.scl");

            int efl = unchecked((int)e.Flags);
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Flags##efl", ref efl, 0, 0)) { Track("Edit entity flags", "ent.flags"); e.Flags = unchecked((uint)Math.Max(efl, 0)); }
            SealHere("ent.flags");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            if (ImGui.DragFloat("LOD##elod", ref e.LodDist, 1.0f, 0, 5000, "%.0f")) Track("Edit LOD", "ent.lod");
            SealHere("ent.lod");
            if (ImGui.Checkbox("Include in the ytyp##einc", ref e.Include)) Track("Toggle include", null);

            string cur = e.RoomOverride < 0 ? $"auto ({e.AutoRoom}: {NameOnly(e.AutoRoom)})" : $"{e.RoomOverride}: {NameOnly(e.RoomOverride)}";
            ImGui.SetNextItemWidth(-90);
            if (ImGui.BeginCombo("Room##eroom", cur))
            {
                if (ImGui.Selectable($"auto ({e.AutoRoom}: {NameOnly(e.AutoRoom)})", e.RoomOverride < 0)) { Track("Set room", null); e.RoomOverride = -1; }
                for (int r = 0; r < s.Rooms.Count; r++)
                    if (ImGui.Selectable($"{r}: {s.Rooms[r].Name}##er{r}", e.RoomOverride == r)) { Track("Set room", null); e.RoomOverride = r; }
                ImGui.EndCombo();
            }
            string set = string.IsNullOrEmpty(e.EntitySet) ? "(room entity)" : e.EntitySet;
            ImGui.SetNextItemWidth(-90);
            if (ImGui.BeginCombo("Set##eset", set))
            {
                if (ImGui.Selectable("(room entity)", string.IsNullOrEmpty(e.EntitySet))) { Track("Set entity set", null); e.EntitySet = null; }
                foreach (var sn in s.EntitySets)
                    if (ImGui.Selectable(sn, e.EntitySet == sn)) { Track("Set entity set", null); e.EntitySet = sn; }
                ImGui.EndCombo();
            }
            ImGui.TextDisabled(e.FromImportedMlo ? "from the imported interior" : e.SourceFile != null ? "file: " + e.SourceFile.Name : "typed");
            ImGui.Spacing();
            if (ImGui.Button("Frame##eframe")) RequestFrameSelected = true;
            DrawEntityPageExtras_L3(e);
            DrawEntityTools_N3(e);
        }

        private void DrawSetPage()
        {
            var s = Session; var sn = s.EntitySets[SelectedSet];
            ImGui.TextDisabled("ENTITY SET");
            string name = sn;
            ImGui.SetNextItemWidth(-90);
            if (ImGui.InputText("Name##esn", ref name, 48) && !string.IsNullOrWhiteSpace(name) && name != sn)
            {
                Track("Rename set", "set.name");
                var nn = name.Trim().ToLowerInvariant();
                foreach (var e in s.Entities) if (e.EntitySet == sn) e.EntitySet = nn;
                if (s.YmapDefaultSets.Remove(sn)) s.YmapDefaultSets.Add(nn);
                s.EntitySets[SelectedSet] = nn;
                sn = nn;
            }
            SealHere("set.name");
            int n = s.Entities.Count(e => e.Include && e.EntitySet == sn);
            ImGui.TextDisabled($"{n} entit{(n == 1 ? "y" : "ies")} in this set");
            if (SelectedEntity >= 0 && SelectedEntity < s.Entities.Count && s.Entities[SelectedEntity].EntitySet != sn)
                if (ImGui.Button($"Put selected entity ({s.Entities[SelectedEntity].Label}) in this set", new Vector2(-1, 0)))
                { Track("Add entity to set", null); s.Entities[SelectedEntity].EntitySet = sn; }
            foreach (var e in s.Entities.Where(e => e.EntitySet == sn)) ImGui.BulletText(e.Label);
        }

        private void DrawTimecyclesPage()
        {
            var s = Session;
            ImGui.TextDisabled("ROOM TIMECYCLES");
            ImGui.TextWrapped("Each room's timecycle modifier grades the world while the camera is inside it. Edit them on the room's page; this is the overview.");
            ImGui.Separator();
            bool any = false;
            for (int i = 0; i < s.Rooms.Count; i++)
            {
                var r = s.Rooms[i];
                if (string.IsNullOrWhiteSpace(r.Timecycle) && string.IsNullOrWhiteSpace(r.SecondaryTimecycle)) continue;
                any = true;
                if (ImGui.Selectable($"{i}: {r.Name}   {r.Timecycle}{(string.IsNullOrWhiteSpace(r.SecondaryTimecycle) ? "" : " / " + r.SecondaryTimecycle)}   (blend {r.Blend:0.00})##tcrow{i}"))
                    SelectRoom(i);
            }
            if (!any) ImGui.TextDisabled("  no room has a timecycle yet");
        }
    }
}

