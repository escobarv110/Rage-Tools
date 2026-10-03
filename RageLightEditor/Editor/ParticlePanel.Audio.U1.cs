using System;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class ParticlePanel
    {
        public int RequestPickEmitterWav_U1 = -1;
        public int RequestEmitterAtView_U1 = -1;
        public bool RequestPickBedWav_U1;

        private int selAudio_U1 = -1;
        private bool? focusAudio_U1;

        public void DrawAudio_U1()
        {
            var doc = Doc;
            if (doc == null)
            {
                ImGui.TextDisabled("Open or make an asset first - audio is saved with it.");
                return;
            }
            var audio = doc.Audio_U1;
            if (audio == null) return;

            if (focusAudio_U1 == null) focusAudio_U1 = Environment.GetEnvironmentVariable("RLE_U1AUDIOFOCUS") == "1";
            if (focusAudio_U1 == true) ImGui.SetScrollY(Math.Max(0.0f, ImGui.GetCursorPosY() - 56.0f));

            if (!AudioEngine_U1.Available)
            {
                ImGui.TextColored(UiTheme.Warn, "No audio device: " + AudioEngine_U1.Trouble);
                ImGui.TextWrapped("Emitters can still be authored and saved with the effect - " +
                                  "there is simply nothing here to play them through.");
                ImGui.Spacing();
            }
            else if (!UiSound.Enabled)
            {
                ImGui.TextColored(UiTheme.Warn, "UI sounds are off (Help > UI sounds) - nothing will play.");
                ImGui.Spacing();
            }

            DrawAmbientBed_U1(audio.Bed);
            ImGui.Spacing();
            ImGui.Separator();
            DrawEmitters_U1(audio);
        }

        private void DrawAmbientBed_U1(PtfxAudioBed_U1 bed)
        {
            if (bed == null) return;
            ImGui.TextDisabled("AMBIENT BED");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A loop with no position: the room tone under the whole effect.\n" +
                                 "It is not attenuated and not panned - it plays while the timeline\n" +
                                 "runs and stops the moment it is paused.");

            bool on = bed.Enabled;
            if (ImGui.Checkbox("On##u1bedon", ref on)) { bed.Enabled = on; MarkDirty_U1(); }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            float v = bed.Volume;
            if (UiSlider_U28.Float("##u1bedvol", ref v, 0.0f, 1.0f, "volume %.2f"))
            { bed.Volume = v; MarkDirty_U1(); }

            string shown = string.IsNullOrEmpty(bed.File) ? "(no file)" : Path.GetFileName(bed.File);
            if (ImGui.Button(shown + "##u1bedpick", new Vector2(-60, 0))) RequestPickBedWav_U1 = true;
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(bed.File)) ImGui.SetTooltip(bed.File);
            ImGui.SameLine();
            ImGui.BeginDisabled(string.IsNullOrEmpty(bed.File));
            if (ImGui.Button("Clear##u1bedclr", new Vector2(-1, 0)))
            {
                if (bed.Voice != 0) { AudioEngine_U1.Stop(bed.Voice); bed.Voice = 0; }
                bed.File = "";
                MarkDirty_U1();
            }
            ImGui.EndDisabled();
            if (!string.IsNullOrEmpty(bed.Trouble)) ImGui.TextColored(UiTheme.Warn, bed.Trouble);
        }

        private void DrawEmitters_U1(PtfxAudioDoc_U1 audio)
        {
            ImGui.TextDisabled("AUDIO EMITTERS");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A sound at a point on the effect. Distance attenuation and left/right\n" +
                                 "panning from the camera - stereo, not HRTF: a source behind you and\n" +
                                 "one in front sound the same. Delay is measured on the EFFECT's own\n" +
                                 "timeline, so 0 fires with the burst and a looping effect re-fires it\n" +
                                 "on every pass.");

            if (ImGui.Button("Add emitter", new Vector2(-1, 0)))
            {
                audio.Emitters.Add(new PtfxAudioEmitter_U1
                {
                    Name = "emitter " + (audio.Emitters.Count + 1),
                    OffsetZ = 0.5f,
                });
                selAudio_U1 = audio.Emitters.Count - 1;
                MarkDirty_U1();
            }

            for (int i = 0; i < audio.Emitters.Count; i++)
            {
                var em = audio.Emitters[i];
                if (em == null) continue;
                ImGui.PushID("u1em" + i);

                bool live = em.Voice != 0 && AudioEngine_U1.Playing(em.Voice);
                if (live) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Accent);
                bool open = ImGui.CollapsingHeader((live ? "> " : "") + em.Name + "##u1emh",
                                                   ImGuiTreeNodeFlags.DefaultOpen);
                if (live) ImGui.PopStyleColor();
                if (!open) { ImGui.PopID(); continue; }

                bool on = em.Enabled;
                if (ImGui.Checkbox("On", ref on)) { em.Enabled = on; MarkDirty_U1(); }
                ImGui.SameLine();
                bool loop = em.Loop;
                if (ImGui.Checkbox("Loop", ref loop)) { em.Loop = loop; MarkDirty_U1(); }
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    if (em.Voice != 0) AudioEngine_U1.Stop(em.Voice);
                    audio.Emitters.RemoveAt(i);
                    MarkDirty_U1();
                    ImGui.PopID();
                    break;
                }

                var name = em.Name ?? "";
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##u1emname", "name", ref name, 48)) { em.Name = name; MarkDirty_U1(); }

                string shown = !string.IsNullOrEmpty(em.File) ? Path.GetFileName(em.File) : "(pick a .wav)";
                if (ImGui.Button(shown + "##u1empick", new Vector2(-1, 0))) RequestPickEmitterWav_U1 = i;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.IsNullOrEmpty(em.File)
                        ? "A .wav from disk. (.ogg needs a decoder this build does not ship - convert it.)"
                        : em.File);

                var game = em.GameSound ?? "";
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##u1emgame", "or a game audio name", ref game, 64))
                { em.GameSound = game; MarkDirty_U1(); }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The name the effect should trigger in game. It is saved with the effect.\n" +
                                     "It cannot be played here - the game's audio lives in .awc containers in\n" +
                                     "codecs this editor does not decode - so with no .wav set, the timeline\n" +
                                     "fires a neutral pip at this emitter's delay instead. That gets the TIMING\n" +
                                     "right, which is the part you are authoring.");

                var off = new Vector3(em.OffsetX, em.OffsetY, em.OffsetZ);
                ImGui.SetNextItemWidth(-58);
                if (ImGui.DragFloat3("##u1emoff", ref off, 0.05f))
                { em.OffsetX = off.X; em.OffsetY = off.Y; em.OffsetZ = off.Z; MarkDirty_U1(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Offset from the effect's origin, in metres.");
                ImGui.SameLine();
                if (ImGui.Button("View##u1emview", new Vector2(-1, 0))) RequestEmitterAtView_U1 = i;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Put it where the camera is looking.");

                float vol = em.Volume, rad = em.Radius, del = em.Delay;
                ImGui.SetNextItemWidth(-1);
                if (UiSlider_U28.Float("##u1emvol", ref vol, 0.0f, 1.0f, "volume %.2f")) { em.Volume = vol; MarkDirty_U1(); }
                ImGui.SetNextItemWidth(-1);
                if (UiSlider_U28.Float("##u1emrad", ref rad, 1.0f, 200.0f, "falloff %.1f m")) { em.Radius = rad; MarkDirty_U1(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Silent past this distance; loudest at the emitter.");
                ImGui.SetNextItemWidth(-1);
                float dmax = Math.Max(1.0f, Sim?.Duration ?? 5.0f);
                if (UiSlider_U28.Float("##u1emdel", ref del, 0.0f, dmax, "delay %.2f s"))
                { em.Delay = Math.Clamp(del, 0.0f, dmax); em.Fired = false; MarkDirty_U1(); }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Seconds into the effect's own {dmax:0.##} s timeline. 0 = with the burst.");

                if (!string.IsNullOrEmpty(em.Trouble)) ImGui.TextColored(UiTheme.Warn, em.Trouble);
                ImGui.PopID();
            }

            if (audio.Emitters.Count == 0)
                ImGui.TextDisabled("No emitters. Add one and give it a .wav or a game audio name.");
        }

        private void MarkDirty_U1()
        {
            if (Doc != null) Doc.Dirty = true;
        }
    }
}

