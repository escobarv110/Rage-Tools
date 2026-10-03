using System;
using System.Numerics;
using ImGuiNET;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class MloCreatorPanel
    {
        public enum SnapTargetKind
        {
            None,
            Picker,
            PortalCorner,
            RoomMin,
            RoomMax,
            Entity,
            PlacementPoint,
        }

        public bool SnapMode;
        public SnapTargetKind SnapTarget = SnapTargetKind.None;
        public int SnapTargetIndex;
        public int SnapTargetPortal = -1, SnapTargetRoom = -1, SnapTargetEntity = -1;
        public bool SnapChainCorners;
        public bool ShowAllVertices;
        public float VertexSizePx = 2.5f;
        public float VertexRadius = 5.0f;
        public int SnapDotsDrawn, SnapMeshesDrawn;
        public string SnapHint = "";

        public void BeginSnap(SnapTargetKind kind, int index = 0, bool chain = false)
        {
            SnapMode = true; SnapTarget = kind; SnapTargetIndex = index; SnapChainCorners = chain;
            SnapTargetPortal = SelectedPortal; SnapTargetRoom = SelectedRoom; SnapTargetEntity = SelectedEntity;
            SetStatus(SnapPrompt() + "  (Esc or right click cancels)");
        }

        public void EndSnap()
        {
            SnapMode = false; SnapTarget = SnapTargetKind.None; SnapChainCorners = false; SnapHint = "";
        }

        public string SnapPrompt()
        {
            var s = Session;
            switch (SnapTarget)
            {
                case SnapTargetKind.Picker:
                    if (PickingPortalCorners)
                    {
                        int need = PickShape == 1 ? 3 : 4;
                        return $"Snap: click a vertex for corner {Math.Min(PickedPoints.Count + 1, need)} of {need}";
                    }
                    if (PickingRoomCorners)
                        return RoomPickAll
                            ? $"Snap: click a vertex to bound the room ({PickedRoomPoints.Count} so far, Finish when done)"
                            : PickedRoomPoints.Count == 0 ? "Snap: click a vertex for the room's MIN corner (1 of 2)"
                                                          : "Snap: click a vertex for the room's MAX corner (2 of 2)";
                    return "Snap: click a vertex";
                case SnapTargetKind.PortalCorner:
                {
                    var p = s != null && SnapTargetPortal >= 0 && SnapTargetPortal < s.Portals.Count ? s.Portals[SnapTargetPortal] : null;
                    int n = p?.Corners.Length ?? 4;
                    return $"Snap: click a vertex for portal {SnapTargetPortal} corner {SnapTargetIndex + 1} of {n}";
                }
                case SnapTargetKind.RoomMin: return $"Snap: click a vertex for the MIN corner of room {SnapTargetRoom} ({RoomNameOnly(SnapTargetRoom)})";
                case SnapTargetKind.RoomMax: return $"Snap: click a vertex for the MAX corner of room {SnapTargetRoom} ({RoomNameOnly(SnapTargetRoom)})";
                case SnapTargetKind.Entity:
                {
                    string nm = s != null && SnapTargetEntity >= 0 && SnapTargetEntity < s.Entities.Count ? s.Entities[SnapTargetEntity].Label : "?";
                    return $"Snap: click a vertex to place entity {SnapTargetEntity} ({nm})";
                }
                case SnapTargetKind.PlacementPoint: return "Snap: click a vertex - it becomes the placement point";
                default: return "Snap: click a vertex";
            }
        }

        private string RoomNameOnly(int i) => Session != null && i >= 0 && i < Session.Rooms.Count ? Session.Rooms[i].Name : "?";

        public bool TickSnapMode()
        {
            bool picking = PickingPortalCorners || PickingRoomCorners;
            if (picking && (!SnapMode || SnapTarget == SnapTargetKind.None)) BeginSnap(SnapTargetKind.Picker);
            else if (picking && SnapTarget != SnapTargetKind.Picker) { SnapTarget = SnapTargetKind.Picker; SnapChainCorners = false; }
            else if (!picking && SnapMode && SnapTarget == SnapTargetKind.Picker) EndSnap();
            var s = Session;
            if (SnapMode && s != null)
            {
                if (SnapTarget == SnapTargetKind.PortalCorner && (SnapTargetPortal < 0 || SnapTargetPortal >= s.Portals.Count || SnapTargetIndex >= s.Portals[SnapTargetPortal].Corners.Length)) EndSnap();
                if ((SnapTarget == SnapTargetKind.RoomMin || SnapTarget == SnapTargetKind.RoomMax) && (SnapTargetRoom < 0 || SnapTargetRoom >= s.Rooms.Count)) EndSnap();
                if (SnapTarget == SnapTargetKind.Entity && (SnapTargetEntity < 0 || SnapTargetEntity >= s.Entities.Count)) EndSnap();
            }
            if (SnapMode && s == null) EndSnap();
            return SnapMode && SnapTarget == SnapTargetKind.Picker;
        }

        public void DrawSnapButton_L4(SnapTargetKind kind, int index, string id, string tooltip)
        {
            bool armed = SnapMode && SnapTarget == kind && (kind != SnapTargetKind.PortalCorner || SnapTargetIndex == index)
                         && (kind != SnapTargetKind.PortalCorner || SnapTargetPortal == SelectedPortal)
                         && ((kind != SnapTargetKind.RoomMin && kind != SnapTargetKind.RoomMax) || SnapTargetRoom == SelectedRoom)
                         && (kind != SnapTargetKind.Entity || SnapTargetEntity == SelectedEntity);
            if (armed) ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ButtonOn);
            if (ImGui.SmallButton((armed ? "Snap..." : "Snap") + id))
            {
                if (armed) EndSnap();
                else BeginSnap(kind, index);
            }
            if (armed) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(armed ? "Snap is on for this - click a vertex in the viewport (Esc cancels)." : tooltip);
        }

        public void DrawSnapAllCornersButton_L4()
        {
            var p = CurrentPortal;
            if (p == null) return;
            bool armed = SnapMode && SnapTarget == SnapTargetKind.PortalCorner && SnapChainCorners && SnapTargetPortal == SelectedPortal;
            bool clicked;
            if (armed)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ButtonOn);
                clicked = ImGui.SmallButton($"Snapping corner {SnapTargetIndex + 1} of {p.Corners.Length}... (Esc stops)");
                ImGui.PopStyleColor();
            }
            else clicked = LightPanel.ColourButton_V33("Snap all corners", LightPanel.ColSnap_V33, Vector2.Zero);
            if (clicked)
            {
                if (armed) EndSnap();
                else BeginSnap(SnapTargetKind.PortalCorner, 0, chain: true);
            }
            if (ImGui.IsItemHovered() && !armed) ImGui.SetTooltip("Click one vertex per corner, in order: the portal is re-drawn onto the doorway's own vertices.");
        }

        public void DrawSnapModeControls_L4()
        {
            var s = Session;
            if (SnapMode)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ButtonOn);
                if (ImGui.Button("Snap is ON - cancel (Esc)", new Vector2(-1, 0))) { EndSnap(); if (PickingPortalCorners || PickingRoomCorners) CancelPicking(); }
                ImGui.PopStyleColor();
                ImGui.TextColored(UiTheme.AccentBright, SnapPrompt());
            }
            else
            {
                if (s == null) ImGui.BeginDisabled();
                if (ImGui.Button("Snap", new Vector2(-1, 0))) BeginSnap(SnapTargetKind.PlacementPoint);
                if (s == null) ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Snap mode: the vertices of the meshes around the cursor are shown as dots; the nearest\n" +
                                     "one is highlighted with its coordinates and a LEFT CLICK takes it (Esc / right click cancel).\n" +
                                     "This button just notes the vertex as the placement point; the Snap buttons on a room's, a\n" +
                                     "portal's or an entity's page put it straight into that corner / position, and 'Portal from\n" +
                                     "4 vertices' / 'Room from vertices' below enter it by themselves.");
            }
            ImGui.Checkbox("Show all vertices", ref ShowAllVertices);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Every vertex of the mesh under the cursor, not only those within the radius.");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(110);
            UiSlider_U28.Float("Vertex size##l4vs", ref VertexSizePx, 1.5f, 8.0f, "%.1f px");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            UiSlider_U28.Float("Radius##l4vr", ref VertexRadius, 1.0f, 30.0f, "%.0f m");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("How far around the cursor's hit the vertices are shown (every mesh within it).");
            if (SnapActive) ImGui.TextDisabled($"{SnapDotsDrawn} vertices of {SnapMeshesDrawn} mesh{(SnapMeshesDrawn == 1 ? "" : "es")} in view");
            ImGui.Separator();
        }
    }
}

