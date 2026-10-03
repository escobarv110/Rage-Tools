using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public ParticlePanel Particles;

        private void DrawParticlesLeft_N4(float displayHeight)
        {
            DrawWorkspaceLogo(150.0f);
            ImGui.TextDisabled("PARTICLE EFFECTS");
            if (Particles == null)
            {
                ImGui.TextWrapped("The particle editor has not started yet.");
                return;
            }
            ImGui.SameLine();
            ImGui.Text($"({ParticlePanel.EffectCount:N0})");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{ParticlePanel.EffectCount:N0} effects across " +
                                 $"{ParticlePanel.Families.Count:N0} assets, from the shipped catalogue.\n" +
                                 "The .ypt files tab lists what is actually in YOUR install.");
            ImGui.Spacing();
            Particles.DrawLibrary(displayHeight);
        }

        private void DrawParticlesRight_N4()
        {
            if (Particles == null) { ImGui.TextDisabled("starting..."); return; }

            if (Header("Create", true)) Particles.DrawAuthoring_T6();
            if (Header("Playback", true)) Particles.DrawPlayback();
            if (Header("Place")) DrawParticlePlacement_N4();
            if (Header("Audio", true)) Particles.DrawAudio_U1();
            if (Header("Effect", true))
            {
                if (ImGui.BeginChild("##ptfxedit", new Vector2(0, Math.Max(140.0f, ImGui.GetContentRegionAvail().Y - 120.0f))))
                    Particles.DrawEffectEditor();
                ImGui.EndChild();
            }

            ImGui.Separator();
            var doc = Particles.Doc;
            ImGui.BeginDisabled(doc == null || doc.FilePath == null);
            if (ImGui.Button(doc != null && doc.Dirty ? "Save .ypt *" : "Save .ypt", new Vector2(-1, 0)))
                Particles.RequestSave = true;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(doc?.FilePath == null
                    ? "This asset came out of the game archives, which are never written back.\nSave As writes your copy."
                    : "Writes " + doc.FilePath + " (a .bak is kept the first time).");
            ImGui.BeginDisabled(doc == null);
            if (ImGui.Button("Save .ypt as...", new Vector2(-1, 0))) Particles.RequestSaveAs = true;
            ImGui.EndDisabled();

            if (!string.IsNullOrEmpty(Particles.Status))
            {
                ImGui.Spacing();
                ImGui.TextWrapped(Particles.Status);
            }
        }

        private void DrawParticlePlacement_N4()
        {
            var p = Particles;
            if (p.Sim.Effect == null)
            {
                ImGui.TextWrapped("Play an effect first.");
                return;
            }
            if (p.YtypTargets.Count == 0)
            {
                ImGui.TextWrapped("No .ytyp is open. Import one in the Lights workspace, or build one " +
                                  "in the MLO Creator, and it appears here to write the effect into.");
                return;
            }
            ImGui.TextDisabled("Write into");
            ImGui.SetNextItemWidth(-1);
            var files = p.YtypTargets.ToArray();
            if (ImGui.Combo("##ptfxytyp", ref p.YtypTarget, files, files.Length)) p.ArchetypeTarget = 0;
            p.YtypTarget = Math.Clamp(p.YtypTarget, 0, files.Length - 1);

            if (p.ArchetypeTargets.Count > 0)
            {
                var archs = p.ArchetypeTargets.ToArray();
                p.ArchetypeTarget = Math.Clamp(p.ArchetypeTarget, 0, archs.Length - 1);
                ImGui.SetNextItemWidth(-1);
                ImGui.Combo("##ptfxarch", ref p.ArchetypeTarget, archs, archs.Length);
            }
            if (ImGui.Button("Add this effect to the ytyp", new Vector2(-1, 0))) p.RequestAddToYtyp = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Appends a CExtensionDefParticleEffect naming this effect, at the\n" +
                                 "position and scale it is playing with, and saves the .ytyp\n" +
                                 "(a .bak is kept the first time).");
        }

        private void DrawParticlesOverlay_N4Legacy(float displayWidth, float displayHeight)
        {
            var p = Particles;
            var eff = p?.Sim.Effect;
            if (eff == null) return;

            float x0 = ShowLeftPanel ? settings.LeftPanelWidth : 0.0f;
            float x1 = displayWidth - (ShowRightPanel ? settings.RightPanelWidth : 0.0f);
            float w = Math.Max(x1 - x0, 240.0f);
            int rows = Math.Min(eff.Emitters.Count, 6);
            float rowH = ImGui.GetTextLineHeightWithSpacing();
            float h = 58.0f + rows * rowH;

            ImGui.SetNextWindowPos(new Vector2(x0, displayHeight - h), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.85f);
            var wf = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                   | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                   | ImGuiWindowFlags.NoBringToFrontOnFocus;
            if (!ImGui.Begin("##ptfxtimeline", wf)) { ImGui.End(); return; }

            var sim = p.Sim;
            if (ImGui.Button(sim.Playing ? "||" : ">")) sim.Playing = !sim.Playing;
            ImGui.SameLine();
            if (ImGui.Button("|<")) sim.Restart();
            ImGui.SameLine();
            ImGui.TextDisabled($"{eff.Name}   {sim.EffectTime:0.00} / {sim.Duration:0.00} s   " +
                               $"{sim.AliveCount:N0} particles");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            var t = sim.EffectTime;
            if (UiSlider_U28.Float("##tlscrub", ref t, 0f, sim.Duration, "")) { sim.EffectTime = t; sim.Playing = false; }

            var dl = ImGui.GetWindowDrawList();
            float bx0 = ImGui.GetWindowPos().X + 8;
            float bw = ImGui.GetWindowSize().X - 16;
            uint colBar = ImGui.GetColorU32(new Vector4(0.93f, 0.33f, 0.62f, 0.55f));
            uint colBack = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.08f));
            uint colHead = ImGui.GetColorU32(new Vector4(1f, 0.95f, 0.4f, 0.95f));
            float frac = sim.Duration > 0f ? Math.Clamp(sim.EffectTime / sim.Duration, 0f, 1f) : 0f;

            for (int i = 0; i < rows; i++)
            {
                var em = eff.Emitters[i];
                float s = Math.Clamp(em.Event?.StartRatio ?? 0f, 0f, 1f);
                float e = em.Event?.EndRatio ?? 1f;
                if (e <= s) e = 1f;
                e = Math.Clamp(e, 0f, 1f);

                var y = ImGui.GetCursorScreenPos().Y;
                dl.AddRectFilled(new Vector2(bx0, y + 2), new Vector2(bx0 + bw, y + rowH - 4), colBack, 2f);
                dl.AddRectFilled(new Vector2(bx0 + bw * s, y + 2), new Vector2(bx0 + bw * e, y + rowH - 4), colBar, 2f);
                ImGui.SetCursorPosX(12);
                ImGui.TextDisabled(em.Name);
                ImGui.Dummy(new Vector2(1, 0));
            }
            float hy0 = ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y - rows * rowH - 4;
            dl.AddLine(new Vector2(bx0 + bw * frac, hy0),
                       new Vector2(bx0 + bw * frac, ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y - 4), colHead, 1.5f);
            ImGui.End();
        }
    }
}

