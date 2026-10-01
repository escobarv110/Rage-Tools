using System;
using System.Globalization;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public YcdDocument_V6.Outline_V6 YcdOutline_V6;
        public string YcdName_V6 = "";
        public string YcdStatus_V6 = "";
        public bool YcdEdited_V6;

        public bool RequestYtdOpen_V16;
        public float RequestUiScale_V17;
        public bool RequestUiScaleAuto_V17;
        public bool RequestUiScaleCommit_V45;
        public string UiScaleNote_V17;
        public int ForcePanelSize_V17;
        public bool RequestYcdOpen_V6;
        public bool RequestYcdSave_V6;
        public bool RequestYcdSaveXml_V6;
        public bool RequestYcdClose_V6;
        public bool RequestYcdNewBone_V6;
        public string RequestYcdRename_V6;
        public string RequestYcdDelete_V6;
        public string RequestYcdRetime_V6;
        public string RequestYcdFlags_V6;

        private int ycdClipSel_V6 = -1, ycdAnimSel_V6 = -1;
        private string ycdRenameTo_V6 = "";
        private float ycdStart_V6, ycdEnd_V6, ycdRate_V6 = 1f;
        private int ycdFlags_V6;
        private int ycdNewFrames_V6 = 30;
        private float ycdNewFps_V6 = 30f;
        private string ycdNewName_V6 = "my_animation";
        public int YcdNewFrames_V6 => ycdNewFrames_V6;
        public float YcdNewFps_V6 => ycdNewFps_V6;
        public string YcdNewName_V6 => ycdNewName_V6;
        private bool ycdEditorLoaded_V6;

        public void DrawYcdSection_V6()
        {
            ImGui.Separator();
            if (!ImGui.CollapsingHeader("Clip dictionary")) return;

            if (ImGui.Button("Open...", new Vector2(-1, 0))) RequestYcdOpen_V6 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Opens a clip dictionary AND plays what is in it: UV tracks land on\n" +
                                 "the materials they animate, bone tracks pose the model.");
            if (ImGui.Button("Open .ytd...", new Vector2(-1, 0))) RequestYtdOpen_V16 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Load texture dictionaries and rebuild the model against them.\n" +
                                 "A model opened from disk has no archetype to find its .ytd through,\n" +
                                 "and a scroll you cannot see on the texture looks like nothing playing.");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Any clip dictionary - one of the game's own, one Sollumz exported,\n" +
                                 "or one this tool wrote. The XML form opens too.");

            if (YcdOutline_V6 == null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                ImGui.TextWrapped("A .ycd holds clips and animations: what plays, and the frames it plays. " +
                                  "Open one to see and edit what is in it, or start a bone animation below.");
                ImGui.PopStyleColor();
                DrawYcdNew_V6();
                DrawYcdStatus_V6();
                return;
            }

            ImGui.TextWrapped(YcdName_V6);
            ImGui.TextDisabled($"{YcdOutline_V6.Clips.Count} clip(s), {YcdOutline_V6.Animations.Count} animation(s), " +
                               $"{YcdOutline_V6.TrackCount} track(s), {YcdOutline_V6.KeyframeCount:N0} keyframe(s)");

            if (ImGui.Button("Export...", new Vector2(-1, 0))) RequestYcdSave_V6 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(YcdEdited_V6
                    ? "Writes the .ycd and its .ycd.xml. It has been edited, so the file is\n" +
                      "rebuilt - quantised channels are re-encoded by the writer, which moves\n" +
                      "their values by a fraction of a step. Authored tracks are exact."
                    : "Writes the .ycd and its .ycd.xml. Nothing has been edited, so the .ycd is\n" +
                      "byte for byte the file that was opened - no re-encoding at all.");
            if (ImGui.Button("Export XML only", new Vector2(-1, 0))) RequestYcdSaveXml_V6 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The CodeWalker / Sollumz XML on its own - what you hand to Blender.");
            if (ImGui.Button("Close##ycd", new Vector2(-1, 0))) RequestYcdClose_V6 = true;

            ImGui.Separator();
            ImGui.TextDisabled("CLIPS");
            for (int i = 0; i < YcdOutline_V6.Clips.Count; i++)
            {
                var c = YcdOutline_V6.Clips[i];
                if (ImGui.Selectable(ShortClip_V6(c.Name) + "##ycdclip" + i, ycdClipSel_V6 == i))
                { ycdClipSel_V6 = i; ycdAnimSel_V6 = -1; ycdEditorLoaded_V6 = false; }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{c.Name}\n{c.Kind}, {c.StartTime:0.###} - {c.EndTime:0.###} s at {c.Rate:0.##}x\n" +
                                     $"{c.TagCount} tag(s), {c.PropertyCount} propert(ies)");
            }

            ImGui.Spacing();
            ImGui.TextDisabled("ANIMATIONS");
            for (int i = 0; i < YcdOutline_V6.Animations.Count; i++)
            {
                var a = YcdOutline_V6.Animations[i];
                string label = a.Hash + (a.IsUv ? "  (UV)" : a.IsBone ? "  (bones)" : "");
                if (ImGui.Selectable(label + "##ycdanim" + i, ycdAnimSel_V6 == i))
                { ycdAnimSel_V6 = i; ycdClipSel_V6 = -1; }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{a.FrameCount} frame(s) over {a.Duration:0.###} s ({a.Fps:0.#} fps)\n" +
                                     $"{a.Tracks.Count} track(s), {a.SequenceCount} sequence(s)");
            }

            DrawYcdNew_V6();
            DrawYcdStatus_V6();
        }

        private void DrawYcdNew_V6()
        {
            ImGui.Spacing();
            if (!ImGui.TreeNode("New bone animation")) return;

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##ycdnewname", "name", ref ycdNewName_V6, 64);
            ImGui.SetNextItemWidth(-90);
            ImGui.DragInt("frames##ycdnf", ref ycdNewFrames_V6, 1f, 2, 4096);
            ImGui.SetNextItemWidth(-90);
            ImGui.DragFloat("fps##ycdfps", ref ycdNewFps_V6, 0.5f, 1f, 240f, "%.0f");
            if (ImGui.Button("Create", new Vector2(-1, 0))) RequestYcdNewBone_V6 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A clip dictionary with one animation and one clip, holding a\n" +
                                 "position and a rotation track on bone 0 that you can edit and\n" +
                                 "export. Written as raw floats, so nothing is quantised away.");
            ImGui.TreePop();
        }

        private void DrawYcdStatus_V6()
        {
            if (string.IsNullOrEmpty(YcdStatus_V6)) return;
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextWrapped(YcdStatus_V6);
            ImGui.PopStyleColor();
        }

        public bool YcdSelected_V6 =>
            YcdOutline_V6 != null &&
            ((ycdClipSel_V6 >= 0 && ycdClipSel_V6 < YcdOutline_V6.Clips.Count) ||
             (ycdAnimSel_V6 >= 0 && ycdAnimSel_V6 < YcdOutline_V6.Animations.Count));

        public void DrawYcdInspector_V6()
        {
            if (YcdOutline_V6 == null) return;

            if (ycdClipSel_V6 >= 0 && ycdClipSel_V6 < YcdOutline_V6.Clips.Count)
            {
                var c = YcdOutline_V6.Clips[ycdClipSel_V6];
                if (!ycdEditorLoaded_V6)
                {
                    ycdEditorLoaded_V6 = true;
                    ycdStart_V6 = c.StartTime; ycdEnd_V6 = c.EndTime; ycdRate_V6 = c.Rate;
                    ycdFlags_V6 = (int)c.Unknown30; ycdRenameTo_V6 = c.Name;
                }

                if (ImGui.CollapsingHeader("Clip"))
                {
                    ImGui.TextWrapped(c.Name);
                    ImGui.TextDisabled(c.Kind + "  -  " + c.Hash);
                    ImGui.Spacing();

                    ImGui.SetNextItemWidth(-70);
                    ImGui.InputText("name##ycdrn", ref ycdRenameTo_V6, 128);
                    ImGui.SameLine();
                    if (ImGui.Button("Rename##ycd", new Vector2(-1, 0)) && ycdRenameTo_V6 != c.Name)
                        RequestYcdRename_V6 = c.Name + "\n" + ycdRenameTo_V6;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("The dictionary is keyed by the hash of the name, so the key\n" +
                                         "moves with it - a renamed clip nothing can find is worse than none.");
                }

                if (ImGui.CollapsingHeader("Playback"))
                {
                    bool changed = false;
                    ImGui.SetNextItemWidth(-90);
                    changed |= ImGui.DragFloat("start (s)##ycd", ref ycdStart_V6, 0.01f, 0f, 10000f, "%.3f");
                    ImGui.SetNextItemWidth(-90);
                    changed |= ImGui.DragFloat("end (s)##ycd", ref ycdEnd_V6, 0.01f, 0f, 10000f, "%.3f");
                    ImGui.SetNextItemWidth(-90);
                    changed |= ImGui.DragFloat("rate##ycd", ref ycdRate_V6, 0.01f, 0.01f, 100f, "%.3f");
                    ImGui.TextDisabled($"plays {Math.Max(ycdEnd_V6 - ycdStart_V6, 0f) / Math.Max(ycdRate_V6, 0.0001f):0.###} s at this rate");
                    if (changed && ycdEnd_V6 >= ycdStart_V6 && ycdRate_V6 > 0f)
                        RequestYcdRetime_V6 = string.Join("\n", c.Name,
                            ycdStart_V6.ToString(CultureInfo.InvariantCulture),
                            ycdEnd_V6.ToString(CultureInfo.InvariantCulture),
                            ycdRate_V6.ToString(CultureInfo.InvariantCulture));
                }

                if (ImGui.CollapsingHeader("Flags"))
                {
                    ImGui.SetNextItemWidth(-90);
                    if (ImGui.InputInt("flags##ycd", ref ycdFlags_V6))
                        RequestYcdFlags_V6 = c.Name + "\n" + Math.Max(ycdFlags_V6, 0).ToString(CultureInfo.InvariantCulture);
                    ImGui.TextDisabled("0 on nearly every clip the game ships; 1 on a few.");
                }

                if (c.Properties.Count > 0 && ImGui.CollapsingHeader($"Properties ({c.Properties.Count})"))
                    foreach (var p in c.Properties) ImGui.TextWrapped(p);

                if (c.Tags.Count > 0 && ImGui.CollapsingHeader($"Tags ({c.Tags.Count})"))
                    foreach (var t in c.Tags) ImGui.TextWrapped(t);

                ImGui.Separator();
                if (ImGui.Button("Delete this clip", new Vector2(-1, 0))) RequestYcdDelete_V6 = c.Name;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The animation it points at stays - another clip may be playing it.");
                return;
            }

            if (ycdAnimSel_V6 >= 0 && ycdAnimSel_V6 < YcdOutline_V6.Animations.Count)
            {
                var a = YcdOutline_V6.Animations[ycdAnimSel_V6];
                if (ImGui.CollapsingHeader("Animation"))
                {
                    ImGui.TextWrapped(a.Hash);
                    ImGui.TextDisabled($"{a.FrameCount} frames over {a.Duration:0.###} s  ({a.Fps:0.#} fps)");
                    ImGui.TextDisabled($"{a.SequenceCount} sequence(s), limit {a.SequenceFrameLimit}");
                }
                if (ImGui.CollapsingHeader($"Tracks ({a.Tracks.Count})"))
                {
                    if (ImGui.BeginTable("##ycdtracks", 3,
                        ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY,
                        new Vector2(0, Math.Min(360f, 24f * a.Tracks.Count + 28f))))
                    {
                        ImGui.TableSetupColumn("bone", ImGuiTableColumnFlags.WidthFixed, 56);
                        ImGui.TableSetupColumn("track");
                        ImGui.TableSetupColumn("stored as");
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableHeadersRow();
                        foreach (var t in a.Tracks)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn(); ImGui.TextUnformatted(t.BoneId.ToString(CultureInfo.InvariantCulture));
                            ImGui.TableNextColumn(); ImGui.TextUnformatted(t.TrackName);
                            ImGui.TableNextColumn(); ImGui.TextUnformatted(t.Kind);
                            if (ImGui.IsItemHovered() && t.Sample.Length > 0)
                                ImGui.SetTooltip("first frames: " + t.Sample);
                        }
                        ImGui.EndTable();
                    }
                }
            }
        }

        private static string ShortClip_V6(string name)
        {
            var s = name ?? "";
            if (s.StartsWith("pack:/", StringComparison.OrdinalIgnoreCase)) s = s.Substring(6);
            if (s.EndsWith(".clip", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 5);
            return s.Length == 0 ? "(unnamed)" : s;
        }
    }
}

