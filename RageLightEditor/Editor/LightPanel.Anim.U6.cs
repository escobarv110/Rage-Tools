using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool AnimMode => Workspace == Space.Animation;

        private static readonly Vector4 AnimWorkspaceColour = new Vector4(0.659f, 0.878f, 0.251f, 1f);

        private const string AnimWorkspaceTooltip =
            "Animations workspace: UV animation, the way GTA V does it.\n" +
            "A shader whose preset declares USE_ANIMATED_UVS carries globalAnimUV0 and\n" +
            "globalAnimUV1 - two rows of a 2x3 matrix applied to the texture coordinates - and a\n" +
            ".ycd clip dictionary animates them over time. That is a conveyor belt, a waterfall,\n" +
            "a flowing river, a scrolling sign.\n" +
            "Open a model, pick a material the game will honour, scroll / rotate / scale / offset\n" +
            "it, key it on the timeline, and watch it move in the viewport for real - the preview\n" +
            "is the renderer's own globalAnimUV path, not an approximation.\n" +
            "Export writes a .ycd (and the .ycd.xml beside it) and reads it straight back.\n" +
            "Space plays and pauses, Home and End jump to the ends, K keys every channel.";

        public AnimEditor Anim;

        partial void WorkspaceLeft_U6(float displayHeight, ref bool handled);
        partial void WorkspaceRight_U6(ref bool handled);
        partial void WorkspaceTheme_U6(ref bool handled);
        partial void WorkspaceTabs_U6();
        partial void WorkspaceTabColour_U6(Space space, ref Vector4 col);
        partial void WorkspaceTabTip_U6(Space space, ref string tip);
        partial void WorkspaceOverlay_U6(float displayWidth, float displayHeight);
        partial void WorkspaceLogo_U6(ref IntPtr tex, ref float w, ref float h);

        partial void WorkspaceLeft_U6(float displayHeight, ref bool handled)
        {
            if (!AnimMode || Anim == null) return;
            DrawAnimLeft_U6(displayHeight);
            handled = true;
        }

        partial void WorkspaceRight_U6(ref bool handled)
        {
            if (!AnimMode || Anim == null) return;
            DrawAnimRight_U6();
            handled = true;
        }

        partial void WorkspaceTheme_U6(ref bool handled)
        {
            if (!AnimMode) return;
            UiTheme.Apply(settings.ThemeIndex, new Vector3(
                AnimWorkspaceColour.X, AnimWorkspaceColour.Y, AnimWorkspaceColour.Z));
            handled = true;
        }

        partial void WorkspaceTabs_U6()
        {
            ImGui.SameLine(0, 2);
            DrawWorkspaceTab("Animations", Space.Animation);
        }

        partial void WorkspaceTabColour_U6(Space space, ref Vector4 col)
        {
            if (space == Space.Animation) col = AnimWorkspaceColour;
        }

        partial void WorkspaceTabTip_U6(Space space, ref string tip)
        {
            if (space == Space.Animation) tip = AnimWorkspaceTooltip;
        }

        public IntPtr AnimLogoTexture = IntPtr.Zero;
        public int AnimLogoWidth, AnimLogoHeight;

        partial void WorkspaceLogo_U6(ref IntPtr tex, ref float w, ref float h)
        {
            if (!AnimMode || AnimLogoTexture == IntPtr.Zero) return;
            tex = AnimLogoTexture; w = AnimLogoWidth; h = AnimLogoHeight;
        }

        private string animMatFilter_U6 = "";
        private string animArchiveName_U6 = "";

        private void DrawAnimLeft_U6(float displayHeight)
        {
            var a = Anim;
            DrawWorkspaceLogo(ImGui.GetContentRegionAvail().X);
            ImGui.Spacing();

            if (ImGui.Button("Open model...", new Vector2(-1, 0))) a.RequestOpenFile = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A .ydr or .yft off disk - what GIMS, Sollumz or this tool's own\n" +
                                 "exports produce. Its materials are what you can animate.");
            if (ImGui.Button("Use Lights model", new Vector2(-1, 0))) a.RequestTakeOpenModel = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Copy whatever is open in the Lights workspace into this one,\n" +
                                 "without going back to disk. Each section owns its own scene.");

            ImGui.SetNextItemWidth(-64);
            ImGui.InputTextWithHint("##animarch", "archetype name from the archives", ref animArchiveName_U6, 96);
            ImGui.SameLine();
            if (ImGui.Button("Find", new Vector2(-1, 0)) && !string.IsNullOrWhiteSpace(animArchiveName_U6))
                a.RequestOpenArchive = animArchiveName_U6.Trim();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Pull a prop straight out of the game archives by its archetype name\n" +
                                 "(prop_conveyor_01, prop_sign_led_01a ...) instead of finding the file.");

            DrawYcdSection_V6();

            if (!a.HasModel)
            {
                ImGui.Spacing();
                ImGui.TextWrapped("Nothing open yet.");
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                ImGui.TextWrapped("UV animation moves a material's texture coordinates over time: a belt, " +
                                  "a waterfall, a river, a scrolling sign. Open the model whose material " +
                                  "you want to move.");
                ImGui.PopStyleColor();
                return;
            }

            ImGui.Spacing();
            ImGui.TextWrapped(a.ModelName);
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(a.ModelPath)) ImGui.SetTooltip(a.ModelPath);
            if (ImGui.SmallButton("Frame it")) a.RequestFrame = true;
            ImGui.SameLine();
            if (ImGui.SmallButton("Close##animmodel")) a.RequestClose = true;

            ImGui.Separator();
            if (a.Materials.Count == 0)
            {
                ImGui.TextWrapped("This drawable has no materials the editor can read.");
                return;
            }
            if (a.AnimUvCount == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.98f, 0.62f, 0.35f, 1f));
                ImGui.TextWrapped($"None of this model's {a.Materials.Count} material(s) use a preset that " +
                                  "declares USE_ANIMATED_UVS, so the game will not animate any of them.");
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The presets that do:\n" + WrapList_U6(ShaderPresets.AnimatedUvPresets, 6) +
                                     "\n\nSwitch the material to one of them in the Materials workspace,\n" +
                                     "then come back - the list here follows the preset.");
                ImGui.TextDisabled("(they are still listed below, so you can see what they are)");
            }
            else
            {
                ImGui.TextDisabled($"{a.AnimUvCount} of {a.Materials.Count} material(s) can be animated");
            }

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##animmatfilter", "filter materials", ref animMatFilter_U6, 64);

            float listH = Math.Max(displayHeight - ImGui.GetCursorPosY() - 90.0f, 120.0f);
            if (ImGui.BeginChild("##animmats", new Vector2(0, listH), ImGuiChildFlags.Borders))
            {
                for (int i = 0; i < a.Materials.Count; i++)
                {
                    var m = a.Materials[i];
                    if (animMatFilter_U6.Length > 0 &&
                        m.Name.IndexOf(animMatFilter_U6, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    bool animated = a.IsAnimated(m);
                    var col = !m.AnimUv ? new Vector4(0.55f, 0.55f, 0.58f, 1f)
                            : animated ? AnimWorkspaceColour
                            : new Vector4(0.85f, 0.87f, 0.90f, 1f);
                    ImGui.PushStyleColor(ImGuiCol.Text, col);
                    string mark = animated ? "*" : m.AnimUv ? "+" : "-";
                    if (ImGui.Selectable($"{mark} {m.Label}##animmat{i}", a.SelectedMaterial == i))
                        a.SelectedMaterial = i;
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(
                            $"{m.Name}\n{m.Sps}\n{m.MeshCount} mesh(es) draw with it\n\n" +
                            (m.AnimUv
                                ? "This preset declares USE_ANIMATED_UVS: globalAnimUV0/1 are real on it and\n" +
                                  "the game will play a .ycd that animates them." +
                                  (m.HasParams ? "\nThe material already carries both parameters."
                                               : "\nThe material does not carry the two parameters yet - the export\n" +
                                                 "still works, but add them in the Materials workspace if you want the\n" +
                                                 "file itself to hold a starting transform.")
                                : "This preset does NOT declare USE_ANIMATED_UVS. You can still preview a\n" +
                                  "transform here, but the game will ignore it: pick a preset that does."));
                    }
                }
            }
            ImGui.EndChild();

            ImGui.TextDisabled("*  animated    +  can be    -  cannot");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("*  this material has a UV animation track\n" +
                                 "+  its preset declares USE_ANIMATED_UVS, so it could have one\n" +
                                 "-  its preset has no animated UVs - the game would ignore one");
        }

        private static string WrapList_U6(string[] names, int perLine)
        {
            if (names == null || names.Length == 0) return "(none)";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                sb.Append(names[i]);
                if (i == names.Length - 1) break;
                sb.Append(((i + 1) % perLine == 0) ? ",\n" : ", ");
            }
            return sb.ToString();
        }

        private int animPreset_U6;

        private void DrawAnimRight_U6()
        {
            var a = Anim;
            var clip = a.Clip;

            if (YcdSelected_V6)
            {
                ImGui.TextColored(AnimWorkspaceColour, "CLIP DICTIONARY");
                ImGui.Separator();
                DrawYcdInspector_V6();
                return;
            }

            ImGui.TextColored(AnimWorkspaceColour, "UV ANIMATION");
            ImGui.Separator();

            if (ImGui.CollapsingHeader("Clip"))
            {
                string name = clip.Name ?? "";
                ImGui.SetNextItemWidth(-110);
                if (ImGui.InputText("Name", ref name, 64)) clip.Name = name;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The clip dictionary's base name. Each material's clip is named after\n" +
                                     "this plus the material - a .ycd keys its clips by hash, so two clips\n" +
                                     "with one name is one clip.");

                float dur = clip.Duration;
                ImGui.SetNextItemWidth(-110);
                if (ImGui.DragFloat("Length", ref dur, 0.05f, 0.1f, 120.0f, "%.2f"))
                {
                    float scale = Math.Max(dur, 0.1f) / Math.Max(clip.Duration, 1e-4f);
                    clip.Duration = Math.Max(dur, 0.1f);
                    foreach (var t in clip.Tracks) foreach (var c in t.Curves) foreach (var k in c.Keys) k.Time *= scale;
                    a.Time = Math.Min(a.Time * scale, clip.Duration);
                }
                int fps = clip.Fps;
                ImGui.SetNextItemWidth(-110);
                if (UiSlider_U28.Int("Frame rate", ref fps, 5, 60)) clip.Fps = Math.Max(fps, 1);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How densely the export samples the curves. The game's own clips are 30.\n" +
                                     "It changes the FILE, not the animation: the curves are the truth.");
                bool loop = clip.Loop;
                if (ImGui.Checkbox("Loop", ref loop)) clip.Loop = loop;
                ImGui.SameLine();
                ImGui.Checkbox("Live preview", ref a.PreviewEnabled);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Off, the materials sit at whatever their file says - which is how you\n" +
                                     "see what you started from.");
                ImGui.TextDisabled($"{clip.FrameCount} frames  -  {clip.Tracks.Count} track(s)");
            }

            var row = a.SelectedRow;
            if (row == null)
            {
                ImGui.Spacing();
                ImGui.TextWrapped("Pick a material on the left to animate it.");
                DrawAnimExport_U6();
                return;
            }

            ImGui.Spacing();
            ImGui.TextColored(UiTheme.AccentBright, row.Label);
            ImGui.TextDisabled(row.Sps);

            var track = a.SelectedTrack;
            if (!row.AnimUv)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.98f, 0.62f, 0.35f, 1f));
                ImGui.TextWrapped($"'{row.Name}' does not declare USE_ANIMATED_UVS. A .ycd that animates it " +
                                  "will load and do nothing in game.");
                ImGui.PopStyleColor();
                ImGui.TextDisabled("You can still add a track and watch it here, to find the numbers you want.");
            }

            if (track == null)
            {
                if (ImGui.Button("Add track", new Vector2(-1, 0)))
                    a.RequestAddTrack = a.SelectedMaterial;
                DrawAnimExport_U6();
                return;
            }

            bool en = track.Enabled;
            if (ImGui.Checkbox("Enabled", ref en)) track.Enabled = en;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Off: not previewed and not exported.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove track")) a.RequestRemoveTrack = a.SelectedMaterial;

            if (ImGui.CollapsingHeader("Preset"))
            {
                ImGui.SetNextItemWidth(-70);
                ImGui.Combo("##animpreset", ref animPreset_U6, UvAnimPresets.Names, UvAnimPresets.Names.Length);
                if (ImGui.IsItemHovered() && animPreset_U6 >= 0 && animPreset_U6 < UvAnimPresets.All.Length)
                    ImGui.SetTooltip(UvAnimPresets.All[animPreset_U6].Blurb);
                ImGui.SameLine();
                if (ImGui.Button("Apply", new Vector2(-1, 0))) a.RequestPreset = animPreset_U6;
                if (!string.IsNullOrEmpty(track.Preset)) ImGui.TextDisabled("last applied: " + track.Preset);
            }

            if (track.Raw) DrawAnimRawRows_U6(track);
            else DrawAnimComposed_U6(track);

            ImGui.Separator();
            ImGui.TextDisabled("globalAnimUV0  " + Row_U6(a.LiveUv0));
            ImGui.TextDisabled("globalAnimUV1  " + Row_U6(a.LiveUv1));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The two rows the preview is writing onto this material's meshes at\n" +
                                 "t = " + a.Time.ToString("0.000") + " s. U' = u*x + v*y + z, same for V'.\n" +
                                 "This is exactly what the exported .ycd carries for this frame.");
            if (a.PreviewEnabled && a.LiveMeshes == 0)
                ImGui.TextColored(new Vector4(0.98f, 0.62f, 0.35f, 1f),
                                  "nothing on screen is drawing with this material");

            DrawAnimExport_U6();
        }

        private static string Row_U6(SharpDX.Vector4 v) =>
            $"({v.X,7:0.0000}, {v.Y,7:0.0000}, {v.Z,7:0.0000})";

        private void DrawAnimComposed_U6(UvAnimTrack track)
        {
            var a = Anim;
            if (!ImGui.CollapsingHeader("Transform")) return;
            float t = a.Clip.Wrap(a.Time);

            DrawAnimChannel_U6(track, UvAnimChannel.ScaleU, t, 0.01f, 0.01f, 32f, "%.3f",
                "How many times the texture repeats across U. 2 tiles it twice.");
            DrawAnimChannel_U6(track, UvAnimChannel.ScaleV, t, 0.01f, 0.01f, 32f, "%.3f",
                "The same down V.");
            DrawAnimChannel_U6(track, UvAnimChannel.Rotation, t, 1.0f, -3600f, 3600f, "%.1f deg",
                "Turns the texture about the pivot below. Key 0 and 360 for a full spin.");
            DrawAnimChannel_U6(track, UvAnimChannel.OffsetU, t, 0.005f, -64f, 64f, "%.4f",
                "Slides the texture along U. A whole number is one full tile, so an\n" +
                "offset that ends on a whole number loops seamlessly.");
            DrawAnimChannel_U6(track, UvAnimChannel.OffsetV, t, 0.005f, -64f, 64f, "%.4f",
                "Slides it along V - the belt / waterfall direction.");

            ImGui.Spacing();
            var pivot = new Vector2(track.PivotU, track.PivotV);
            ImGui.SetNextItemWidth(-110);
            if (ImGui.DragFloat2("Pivot", ref pivot, 0.01f, -8f, 8f, "%.3f"))
            {
                track.PivotU = pivot.X; track.PivotV = pivot.Y;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("What the rotation and the scale happen about, in UV.\n" +
                                 "(0.5, 0.5) is the middle of the texture - a sign spins about its\n" +
                                 "centre rather than swinging round its corner.");

            ImGui.Spacing();
            ImGui.TextDisabled("Scroll (writes an offset ramp over the whole clip)");
            ImGui.SetNextItemWidth(-110);
            ImGui.DragFloat2("UV per second", ref animScroll_U6, 0.01f, -16f, 16f, "%.3f");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A conveyor is about 0.5, a waterfall about 1.2, a river about 0.15.\n" +
                                 "The travel is rounded to a whole number of tiles so the loop is seamless.");
            if (ImGui.Button("Apply scroll", new Vector2(-1, 0)))
            {
                UvAnimPresets.Scroll(track, a.Clip.Duration, animScroll_U6.X, animScroll_U6.Y);
                track.Preset = $"scroll {animScroll_U6.X:0.###}, {animScroll_U6.Y:0.###} /s";
                a.Status = "Scroll applied: " + track.Preset;
            }

            ImGui.Spacing();
            if (ImGui.Button("Bake to the raw 2x3", new Vector2(-1, 0))) a.RequestBake = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Sample this track onto the six shader numbers, one key per frame -\n" +
                                 "exactly what the export writes. Do it when you want to hand-edit a\n" +
                                 "curve the controls above cannot express (shear, for instance).");
        }

        private Vector2 animScroll_U6 = new Vector2(0.0f, 0.5f);

        private void DrawAnimRawRows_U6(UvAnimTrack track)
        {
            var a = Anim;
            if (!ImGui.CollapsingHeader("Raw 2x3 (globalAnimUV0 / 1)")) return;
            float t = a.Clip.Wrap(a.Time);
            ImGui.TextDisabled("U' = u*x + v*y + z");
            DrawAnimChannel_U6(track, UvAnimChannel.Row0X, t, 0.005f, -32f, 32f, "%.4f", "globalAnimUV0.x");
            DrawAnimChannel_U6(track, UvAnimChannel.Row0Y, t, 0.005f, -32f, 32f, "%.4f", "globalAnimUV0.y");
            DrawAnimChannel_U6(track, UvAnimChannel.Row0Z, t, 0.005f, -64f, 64f, "%.4f", "globalAnimUV0.z");
            ImGui.TextDisabled("V' = u*x + v*y + z");
            DrawAnimChannel_U6(track, UvAnimChannel.Row1X, t, 0.005f, -32f, 32f, "%.4f", "globalAnimUV1.x");
            DrawAnimChannel_U6(track, UvAnimChannel.Row1Y, t, 0.005f, -32f, 32f, "%.4f", "globalAnimUV1.y");
            DrawAnimChannel_U6(track, UvAnimChannel.Row1Z, t, 0.005f, -64f, 64f, "%.4f", "globalAnimUV1.z");

            ImGui.Spacing();
            if (ImGui.Button("Back", new Vector2(-1, 0)))
            {
                track.Raw = false;
                a.Status = "Editing scale / rotation / offset again (the raw keys are still there).";
            }
        }

        private void DrawAnimChannel_U6(UvAnimTrack track, UvAnimChannel ch, float t,
                                        float speed, float lo, float hi, string fmt, string tip)
        {
            var a = Anim;
            var curve = track.Curve(ch);
            float v = curve.Evaluate(t);
            string label = UvAnimTrack.ChannelLabel(ch);

            ImGui.PushID((int)ch + 900);
            ImGui.SetNextItemWidth(-110);
            if (ImGui.DragFloat(label, ref v, speed, lo, hi, fmt))
            {
                if (curve.Animated) curve.SetKey(t, v);
                else curve.Constant = v;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tip + (curve.Animated
                    ? $"\n\n{curve.Keys.Count} key(s) - dragging moves the key at the playhead."
                    : "\n\nNo keys: this is a constant. Press K to key it."));
            ImGui.SameLine();
            if (ImGui.SmallButton(curve.Animated ? "K*" : "K")) curve.SetKey(t, v);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Key this value at the playhead.");
            ImGui.SameLine();
            if (ImGui.SmallButton("x"))
            {
                curve.Constant = v;
                curve.Clear();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Drop this channel's keys (it keeps the value it has now).");
            ImGui.PopID();
        }

        private void DrawAnimExport_U6()
        {
            var a = Anim;
            ImGui.Separator();
            if (!ImGui.CollapsingHeader("Export")) return;

            int live = a.Clip?.Tracks?.Count(t => t != null && t.Enabled) ?? 0;
            if (live == 0) ImGui.TextDisabled("No enabled track to write.");

            if (ImGui.Button("Export .ycd...", new Vector2(-1, 0))) a.RequestExport = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Writes a real clip dictionary: one clip per animated material, its\n" +
                                 "animation carrying tracks 17 and 18 - globalAnimUV0 and globalAnimUV1 -\n" +
                                 "sampled at the clip's frame rate. The .ycd.xml goes beside it, and the\n" +
                                 "file is read straight back so a broken write is visible now, not later.");
            if (ImGui.Button("Save project...", new Vector2(-1, 0))) a.RequestSaveProject = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The curves themselves (.rleuv), to come back to.");
            if (ImGui.Button("Open project...", new Vector2(-1, 0))) a.RequestOpenProject = true;

            if (!string.IsNullOrEmpty(a.ExportStatus))
            {
                ImGui.Spacing();
                ImGui.TextWrapped(a.ExportStatus);
            }
        }
    }
}

