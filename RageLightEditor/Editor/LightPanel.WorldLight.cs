using System;
using System.Collections.Generic;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {

        public WorldLights WorldLightSource;
        public string WorldLightSavedPath;
        public string WorldLightStatus;
        public bool WorldLightUnsaved;

        public bool RequestSelectWorldLight;
        public YmapEntityDef RequestSelectWorldLightEntity;
        public int RequestSelectWorldLightIndex;
        public bool RequestWorldLightSaveAs, RequestWorldLightAddToProject, RequestWorldLightDelete_U25;

        public bool WorldLightEdited;
        public LightAttributes WorldLightEditKey, WorldLightEditBefore, WorldLightEditAfter;

        private bool lightEditorExternal;
        private Skeleton lightEditorSkeleton;

        private bool LightEditorBoneFound(LightInstance inst)
        {
            if (!lightEditorExternal) return inst.BoneFound;
            var l = inst?.Light;
            if (l == null || l.BoneId == 0) return true;
            var bm = lightEditorSkeleton?.BonesMap;
            return bm != null && bm.ContainsKey(l.BoneId);
        }

        private string LightEditorBoneName(ushort tag)
        {
            if (!lightEditorExternal) return scene.GetBoneName(tag);
            var bm = lightEditorSkeleton?.BonesMap;
            return bm != null && bm.TryGetValue(tag, out var b) ? (b?.Name ?? "") : "";
        }

        private IEnumerable<ushort> LightEditorBoneTags()
        {
            if (!lightEditorExternal) return scene.GetBoneTags();
            var list = new List<ushort>();
            var bones = lightEditorSkeleton?.Bones?.Items;
            if (bones != null) foreach (var b in bones) if (b != null) list.Add(b.Tag);
            return list;
        }

        private void DrawWorldLightPage(in WorldSelection s)
        {
            var l = s.Light;
            var e = s.LightEntity;
            if (l == null) return;
            rowSeq = 0;
            ImGui.TextWrapped(s.LightNameString());
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(SelectionKeysTip_M3);
            var ymapName = e?.Ymap?.Name;
            if (!string.IsNullOrEmpty(ymapName)) ImGui.TextDisabled("in " + ymapName);
            else if (e?.MloParent != null) ImGui.TextDisabled("in interior " + (e.MloParent.Archetype?.Name ?? ""));

            uint archHash = e?.Archetype?.Hash ?? 0;
            var drawable = archHash != 0 ? WorldLightSource?.GetDrawable(archHash) : null;
            var wp = s.LightWorldPosition;
            Row("Prop", e?.Archetype?.Name ?? "?");
            Row("Light", $"{s.LightIndex}  ({l.Type})");
            Row("World pos", $"{wp.X:0.###}, {wp.Y:0.###}, {wp.Z:0.###}");
            if (l.BoneId != 0) Row("Bone", l.BoneId.ToString());
            if (drawable != null && !WorldLights.CanSave(drawable))
                ImGui.TextColored(UiTheme.Warn, "This drawable cannot be written out as a loose file (edits stay in memory).");
            else if (drawable == null)
                ImGui.TextColored(UiTheme.Warn, "The prop's drawable is not resident right now - fly closer to edit.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("LIGHT");
            var before = Scene.CloneLight(l);
            lightEditorExternal = true;
            lightEditorSkeleton = WorldLights.SkeletonOf(drawable);
            scene.ExternalEditing = true;
            try { DrawLightEditor(l); }
            finally
            {
                scene.ExternalEditing = false;
                lightEditorExternal = false;
                lightEditorSkeleton = null;
            }
            if (!WorldLights.AllFieldsEqual(before, l))
            {
                WorldLightEdited = true;
                WorldLightEditKey = l;
                WorldLightEditBefore = before;
                WorldLightEditAfter = Scene.CloneLight(l);
            }

            DrawEntityLightsList_WorldLight(e, s.LightIndex);
            ImGui.Spacing();
            ImGui.Separator();
            DrawWorldLightAddRow_U18(e?.Archetype?.Name);
            DrawWorldLightPresets_U18(l);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("FILE");
            if (!string.IsNullOrEmpty(WorldLightSavedPath))
            {
                ImGui.TextDisabled(System.IO.Path.GetFileName(WorldLightSavedPath));
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(WorldLightSavedPath);
            }
            if (WorldLightUnsaved)
            {
                ImGui.TextColored(UiTheme.Warn, "Unsaved");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The prop's lights are changed in the world's copy of the drawable only.\n" +
                                     "Save as writes them to a loose .ydr/.yft; Add to project makes that file\n" +
                                     "the one the world draws.");
            }
            bool canSave = drawable != null && WorldLights.CanSave(drawable);
            if (!canSave) ImGui.BeginDisabled();
            if (ImGui.Button("Save as...##wlight", new Vector2(-1, 0))) RequestWorldLightSaveAs = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Write this prop, with its edited lights, to a loose " + (drawable != null ? WorldLights.SaveExtension(drawable) : ".ydr") + "\n" +
                                 "(the same writer the light workspace's Save As uses). Never back into an .rpf.");
            if (ImGui.Button("Add to project##wlight", new Vector2(-1, 0))) RequestWorldLightAddToProject = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Save the edited prop (asks where, the first time) and add that file to the project:\n" +
                                 "the project's copy replaces the game's, so the world draws and lights from it.");
            if (!canSave) ImGui.EndDisabled();
            ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.DangerButton);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UiTheme.DangerButtonHi);
            if (ImGui.Button("Delete light##wlight", new Vector2(-1, 0))) RequestWorldLightDelete_U25 = true;
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove this light from the prop. Undo brings it back.\nShortcut: Delete");
            if (!string.IsNullOrEmpty(WorldLightStatus)) ImGui.TextWrapped(WorldLightStatus);

            DrawCollisionUnderCursor_Selection();
            if (WorldDirtyCount > 0) DrawWorldSaveRow();
        }

        private void DrawEntityLightsList_WorldLight(YmapEntityDef e, int selectedIndex = -1)
        {
            if (e?.Archetype == null || WorldLightSource == null) return;
            if (!WorldLightSource.TryGetDefs(e.Archetype.Hash, out var defs) || defs == null || defs.Length == 0) return;
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled($"LIGHTS ({defs.Length})");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The lights this prop's drawable carries. Click one to select and edit it.");
            const int maxRows = 10;
            bool scrolls = defs.Length > maxRows;
            if (scrolls) ImGui.BeginChild("##wllist", new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * maxRows + 4), ImGuiChildFlags.None);
            for (int i = 0; i < defs.Length; i++)
            {
                var la = defs[i].L;
                if (la == null) continue;
                var col = new Vector4(la.ColorR / 255.0f, la.ColorG / 255.0f, la.ColorB / 255.0f, 1.0f);
                ImGui.ColorButton($"##wlsw{i}", col, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.NoDragDrop, new Vector2(14, 14));
                ImGui.SameLine();
                bool sel = i == selectedIndex;
                if (sel) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.AccentBright);
                string label = $"{i}: {la.Type}   {la.Intensity:0.#}   {la.Falloff:0.#} m##wlrow{i}";
                if (ImGui.Selectable(label, sel))
                {
                    RequestSelectWorldLight = true;
                    RequestSelectWorldLightEntity = e;
                    RequestSelectWorldLightIndex = i;
                }
                if (sel) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"type {la.Type}, colour {la.ColorR},{la.ColorG},{la.ColorB}, intensity {la.Intensity:0.##}, falloff {la.Falloff:0.##} m" +
                                     (la.Type == LightType.Spot ? $", cone {la.ConeInnerAngle:0.#}/{la.ConeOuterAngle:0.#} deg" : "") +
                                     (la.BoneId != 0 ? $", bone {la.BoneId}" : "") + "\nClick to select this light.");
            }
            if (scrolls) ImGui.EndChild();
        }
    }
}

