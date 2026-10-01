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
        public int EntityTool = 1;
        public static readonly string[] EntityToolNames = { "Select", "Move", "Rotate", "Scale" };

        public bool EntitySnapOn = true;
        public float EntityRotateSnapDeg = 5.0f;

        public int PlaceRoomMode;
        public static readonly string[] PlaceRoomModeNames =
        {
            "Camera room", "Selected room", "Auto",
        };
        public int PlaceRoomIndex = -1;
        public string PlaceRoomLabel = "";

        public readonly List<int> SelectedEntities = new List<int>();
        public readonly List<MloCreatorEntity> EntityClipboard = new List<MloCreatorEntity>();

        public bool RequestDeleteEntities;
        public bool RequestDuplicateEntities;
        public bool RequestCopyEntities, RequestPasteEntities;
        public int RequestAssignEntityRoom = -2;
        public bool RequestPlaceIntoRoom;

        public IEnumerable<int> ActiveEntities()
        {
            if (SelectedEntities.Count > 0)
            {
                foreach (var i in SelectedEntities) yield return i;
                yield break;
            }
            if (SelectedEntity >= 0) yield return SelectedEntity;
        }

        public int ActiveEntityCount => SelectedEntities.Count > 0 ? SelectedEntities.Count : (SelectedEntity >= 0 ? 1 : 0);

        public void SelectEntityMulti(int i, bool ctrl)
        {
            if (Session == null || i < 0 || i >= Session.Entities.Count) return;
            if (!ctrl) { SelectedEntities.Clear(); SelectEntity(i); return; }
            if (SelectedEntities.Count == 0 && SelectedEntity >= 0 && SelectedEntity != i) SelectedEntities.Add(SelectedEntity);
            if (SelectedEntities.Contains(i))
            {
                SelectedEntities.Remove(i);
                if (SelectedEntities.Count > 0) SelectEntity(SelectedEntities[SelectedEntities.Count - 1]);
                return;
            }
            SelectedEntities.Add(i);
            var keep = new List<int>(SelectedEntities);
            SelectEntity(i);
            SelectedEntities.Clear(); SelectedEntities.AddRange(keep);
        }

        public void ForgetEntity(int removed)
        {
            for (int i = SelectedEntities.Count - 1; i >= 0; i--)
            {
                if (SelectedEntities[i] == removed) SelectedEntities.RemoveAt(i);
                else if (SelectedEntities[i] > removed) SelectedEntities[i]--;
            }
            if (SelectedEntity == removed) SelectedEntity = -1;
            else if (SelectedEntity > removed) SelectedEntity--;
        }

        private void DrawEditToolbar_N3()
        {
            void Tool(string label, int mode, string tip)
            {
                bool on = EntityTool == mode;
                if (on) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
                if (ImGui.Button(label)) EntityTool = mode;
                if (on) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
                ImGui.SameLine();
            }
            _ = (Action<string, int, string>)Tool;
            if (ImGui.Checkbox("Rot snap##n3snap", ref EntitySnapOn)) { }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Snap ROTATION to {EntityRotateSnapDeg:0.#} degree steps (E).\n" +
                                 "Position and scale never snap - a prop goes exactly where you drag it.\n" +
                                 "Hold Shift during a rotate to ignore the step; hold V to land a position on a mesh vertex.");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(52);
            if (ImGui.DragFloat("deg##n3rotsnap", ref EntityRotateSnapDeg, 0.5f, 0.0f, 90.0f, "%.0f")) { }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The rotation step, in degrees. 5 by default; 0 = free.");
            ImGui.SameLine();
        }

        private void DrawEntityTransform_N3(MloCreatorEntity e)
        {
            if (e == null) return;
            var eul = V(WorldEditor.ToEulerDegrees(e.Rotation));
            ImGui.SetNextItemWidth(-90);
            if (ImGui.DragFloat3("Rotation##n3erot", ref eul, 0.5f, -360, 360, "%.1f deg"))
            {
                Track("Rotate entity", "ent.rot");
                e.Rotation = WorldEditor.FromEulerDegrees(V(eul));
                EntityMoved = true;
            }
            SealHere("ent.rot");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Yaw / pitch / roll in degrees - the same numbers the world Inspector shows.\nThe gizmo (E) turns it in the viewport.");
        }

        public bool EntityMoved;

        private void DrawEntityTools_N3(MloCreatorEntity e)
        {
            var s = Session;
            if (s == null || e == null) return;
            ImGui.Separator();
            int n = ActiveEntityCount;
            ImGui.TextDisabled(n > 1 ? $"{n} props selected - the tools act on all of them" : "ROOM & TOOLS");
            ImGui.TextDisabled($"in room {e.Room}: {NameOnly(e.Room)}{(e.RoomOverride >= 0 ? "  (pinned)" : "  (by containment)")}");
            if (PlaceRoomIndex >= 0 && PlaceRoomIndex != e.Room)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"put in {PlaceRoomIndex}: {NameOnly(PlaceRoomIndex)}##n3putroom")) RequestAssignEntityRoom = PlaceRoomIndex;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pin this prop to the current room (the room the camera is in / selected in the tree),\nwherever its origin happens to sit.");
            }
            if (e.RoomOverride >= 0)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("unpin##n3unpin")) RequestAssignEntityRoom = -1;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Back to containment: the room whose box the prop's origin is inside.");
            }

            if (ImGui.Button("Duplicate##n3dup")) RequestDuplicateEntities = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A copy beside it, model and all, selected  (Ctrl+D)");
            ImGui.SameLine();
            if (ImGui.Button("Copy##n3copy")) RequestCopyEntities = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy the selected prop(s)  (Ctrl+C)");
            ImGui.SameLine();
            if (EntityClipboard.Count == 0) ImGui.BeginDisabled();
            if (ImGui.Button($"Paste ({EntityClipboard.Count})##n3paste")) RequestPasteEntities = true;
            if (EntityClipboard.Count == 0) ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Put the copied prop(s) down at the placement point / the view  (Ctrl+V)");
            ImGui.SameLine();
            if (LightPanel.DangerButton("Delete##n3del", Vector2.Zero)) RequestDeleteEntities = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove the prop from the interior and from the scene  (Del). Ctrl+Z brings it back.");
            DrawEntityRoom_O3(e);
        }

        public void DrawPlaceRoomRow_N3(bool compact)
        {
            ImGui.TextDisabled("Place into:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(compact ? -1 : 230);
            ImGui.Combo("##n3placeroom", ref PlaceRoomMode, PlaceRoomModeNames, PlaceRoomModeNames.Length);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Which ROOM the placed prop belongs to in the ytyp.\nThe camera's room follows you around the interior; the tree's room is whatever is selected there.");
            if (!compact) ImGui.SameLine();
            if (PlaceRoomIndex >= 0)
                ImGui.TextColored(UiTheme.AccentBright, $"-> room {PlaceRoomIndex}: {PlaceRoomLabel}");
            else
                ImGui.TextDisabled("-> the room the prop lands in");
            if (!string.IsNullOrEmpty(PlaceRoomWhy_O3)) ImGui.TextDisabled(PlaceRoomWhy_O3);
            if (Assets.Selected != null)
            {
                if (ImGui.Button($"Place '{Assets.Selected.Name}' in this room##n3placein", new Vector2(-1, 0))) RequestPlaceIntoRoom = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Load the model, drop it at the placement point / the view, and pin it to the room above.\nMove it with the gizmo (W), turn it (E), delete it (Del).");
            }
        }
    }
}

