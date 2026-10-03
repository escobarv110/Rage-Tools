using System;
using System.Collections.Generic;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private bool ptfxDragHead_R6;
        private int ptfxDragEmitter_R6 = -1, ptfxDragEdge_R6;
        private int ptfxDragKey_R6 = -1;
        private bool ptfxShowCurves_R6 = true;
        private readonly List<(string name, ParticleKeyframeProp kfp)> ptfxCurves_R6 =
            new List<(string, ParticleKeyframeProp)>();

        private void DrawParticlesOverlay_R6(float displayWidth, float displayHeight)
        {
            var p = Particles;
            var sim = p?.Sim;
            var eff = sim?.Effect;
            if (eff == null) return;

            float x0 = ShowLeftPanel ? settings.LeftPanelWidth : 0.0f;
            float x1 = displayWidth - (ShowRightPanel ? settings.RightPanelWidth : 0.0f);
            float w = Math.Max(x1 - x0, 320.0f);

            int rows = Math.Min(eff.Emitters.Count, 8);
            float rowH = ImGui.GetTextLineHeightWithSpacing() + 4.0f;
            bool curves = ptfxShowCurves_R6 && p.SelectedEmitter >= 0 && p.SelectedEmitter < eff.Emitters.Count;
            float curveH = curves ? 74.0f : 0.0f;
            float h = 78.0f + rows * rowH + curveH;

            ImGui.SetNextWindowPos(new Vector2(x0, displayHeight - h), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.88f);
            var wf = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                   | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                   | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBringToFrontOnFocus;
            if (!ImGui.Begin("##ptfxtimeline", wf)) { ImGui.End(); return; }

            DrawPtfxTransport_R6(p, sim, eff);

            float pad = 10.0f;
            float ax0 = ImGui.GetWindowPos().X + pad;
            float axW = Math.Max(ImGui.GetWindowSize().X - 2.0f * pad, 40.0f);
            float dur = Math.Max(sim.Duration, 0.0001f);
            float X(float seconds) => ax0 + axW * Math.Clamp(seconds / dur, 0f, 1f);

            var dl = ImGui.GetWindowDrawList();
            float rulerY = ImGui.GetCursorScreenPos().Y;
            DrawPtfxRuler_R6(dl, sim, ax0, axW, rulerY, dur);

            ImGui.SetCursorScreenPos(new Vector2(ax0, rulerY));
            ImGui.InvisibleButton("##ptfxscrubstrip", new Vector2(axW, 16.0f));
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
            if (ImGui.IsItemActivated()) ptfxDragHead_R6 = true;
            if (ptfxDragHead_R6)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    sim.Playing = false;
                    var frac = Math.Clamp((ImGui.GetIO().MousePos.X - ax0) / axW, 0f, 1f);
                    var want = frac * dur;
                    sim.SeekTo(MathF.Round(want / PtfxSimulator.FixedDt) * PtfxSimulator.FixedDt);
                }
                else ptfxDragHead_R6 = false;
            }

            float rowsY = rulerY + 20.0f;
            DrawPtfxEmitterRows_R6(p, sim, eff, dl, ax0, axW, rowsY, rowH, rows);

            float curveY = rowsY + rows * rowH + 4.0f;
            if (curves) DrawPtfxCurveTrack_R6(p, eff, dl, ax0, axW, curveY, dur);

            float hx = X(sim.EffectTime);
            uint colHead = ImGui.GetColorU32(new Vector4(1f, 0.95f, 0.4f, 0.95f));
            float headBottom = (curves ? curveY + 60.0f : curveY) ;
            dl.AddLine(new Vector2(hx, rulerY), new Vector2(hx, headBottom), colHead, 1.6f);
            dl.AddNgonFilled(new Vector2(hx, rulerY + 2.0f), 5.0f, colHead, 3);

            ImGui.End();
        }

        private void DrawPtfxTransport_R6(ParticlePanel p, PtfxSimulator sim, PtfxEffect eff)
        {
            if (ImGui.Button("|<", new Vector2(28, 0))) { sim.SeekTo(0f); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("back to the start (re-simulates from frame 0)");
            ImGui.SameLine();
            if (ImGui.Button(sim.Playing ? "||" : ">", new Vector2(28, 0))) sim.Playing = !sim.Playing;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(sim.Playing ? "pause - the picture freezes" : "play");
            ImGui.SameLine();
            if (ImGui.Button(">|", new Vector2(28, 0)))
            {
                sim.Playing = false;
                sim.SeekTo(sim.EffectTime + PtfxSimulator.FixedDt);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"step one frame ({PtfxSimulator.FixedDt * 1000f:0} ms)");
            ImGui.SameLine();
            ImGui.Checkbox("Loop", ref sim.Loop);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120.0f);
            UiSlider_U28.Float("##ptfxtlspeed", ref sim.TimeScale, 0.05f, 3.0f, "%.2fx");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("playback speed - the simulation step itself never changes");

            ImGui.SameLine();
            ImGui.TextColored(UiTheme.AccentBright, eff.Name);
            ImGui.SameLine();
            ImGui.TextDisabled($"{sim.EffectTime:0.00} / {sim.Duration:0.00} s   frame {sim.StepCount}   " +
                               $"{sim.AliveCount:N0} particles" + (sim.CulledCount > 0 ? $"   {sim.CulledCount} culled" : ""));
            ImGui.SameLine();
            float right = ImGui.GetWindowSize().X - 120.0f;
            if (right > ImGui.GetCursorPosX()) ImGui.SetCursorPosX(right);
            ImGui.Checkbox("Curves", ref ptfxShowCurves_R6);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show the selected emitter's keyframes on the same time axis.\n" +
                                 "Drag a key sideways to move it in time, up or down to change its value.");
        }

        private static void DrawPtfxRuler_R6(ImDrawListPtr dl, PtfxSimulator sim,
                                             float ax0, float axW, float y, float dur)
        {
            uint colTick = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.30f));
            uint colTickMinor = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.13f));
            uint colBack = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.06f));
            dl.AddRectFilled(new Vector2(ax0, y), new Vector2(ax0 + axW, y + 16.0f), colBack, 2.0f);

            float step = dur <= 1.5f ? 0.1f : dur <= 6f ? 0.25f : dur <= 20f ? 1f : 5f;
            int ticks = (int)(dur / step + 0.5f);
            int every = Math.Max(1, (int)Math.Ceiling(ticks / 8.0));
            for (int i = 0; i <= ticks; i++)
            {
                float t = i * step;
                float x = ax0 + axW * Math.Clamp(t / dur, 0f, 1f);
                bool major = i % every == 0;
                dl.AddLine(new Vector2(x, y + (major ? 3.0f : 9.0f)), new Vector2(x, y + 16.0f),
                           major ? colTick : colTickMinor, 1.0f);
                if (major && i < ticks)
                    dl.AddText(new Vector2(x + 2.0f, y + 1.0f), colTick, t.ToString(step < 1f ? "0.0#" : "0.##"));
            }
        }

        private void DrawPtfxEmitterRows_R6(ParticlePanel p, PtfxSimulator sim, PtfxEffect eff,
                                            ImDrawListPtr dl, float ax0, float axW, float y0, float rowH, int rows)
        {
            uint colBack = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.07f));
            uint colBar = ImGui.GetColorU32(new Vector4(0.93f, 0.33f, 0.62f, 0.50f));
            uint colBarSel = ImGui.GetColorU32(new Vector4(0.99f, 0.55f, 0.78f, 0.80f));
            uint colEdge = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.55f));
            uint colText = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.85f));

            for (int i = 0; i < rows; i++)
            {
                var em = eff.Emitters[i];
                var ev = em.Event;
                float s = Math.Clamp(ev?.StartRatio ?? 0f, 0f, 1f);
                float e = ev?.EndRatio ?? 1f;
                if (e <= s) e = 1f;
                e = Math.Clamp(e, 0f, 1f);

                float y = y0 + i * rowH;
                float bx0 = ax0 + axW * s, bx1 = ax0 + axW * e;
                bool sel = i == p.SelectedEmitter;

                dl.AddRectFilled(new Vector2(ax0, y + 1), new Vector2(ax0 + axW, y + rowH - 3), colBack, 2.0f);
                dl.AddRectFilled(new Vector2(bx0, y + 1), new Vector2(bx1, y + rowH - 3),
                                 sel ? colBarSel : colBar, 2.0f);
                if (sel)
                    dl.AddRect(new Vector2(bx0, y + 1), new Vector2(bx1, y + rowH - 3), colEdge, 2.0f);
                dl.AddText(new Vector2(ax0 + 6.0f, y + 2.0f), colText, em.Name ?? "emitter");

                float grip = 7.0f;
                ImGui.PushID(3000 + i);
                bool changed = false;

                ImGui.SetCursorScreenPos(new Vector2(bx0 - grip, y + 1));
                ImGui.InvisibleButton("##s", new Vector2(grip * 2, rowH - 4));
                if (ImGui.IsItemHovered()) { ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW); ImGui.SetTooltip($"start {s:0.###}"); }
                if (ImGui.IsItemActivated()) { p.SelectedEmitter = i; ptfxDragEmitter_R6 = i; ptfxDragEdge_R6 = -1; }

                ImGui.SetCursorScreenPos(new Vector2(bx1 - grip, y + 1));
                ImGui.InvisibleButton("##e", new Vector2(grip * 2, rowH - 4));
                if (ImGui.IsItemHovered()) { ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW); ImGui.SetTooltip($"end {e:0.###}"); }
                if (ImGui.IsItemActivated()) { p.SelectedEmitter = i; ptfxDragEmitter_R6 = i; ptfxDragEdge_R6 = 1; }

                ImGui.SetCursorScreenPos(new Vector2(bx0 + grip, y + 1));
                ImGui.InvisibleButton("##b", new Vector2(Math.Max(bx1 - bx0 - grip * 2, 1.0f), rowH - 4));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                    ImGui.SetTooltip($"{em.Name}\n{em.ParticleName}\nalive {s * sim.Duration:0.##}..{e * sim.Duration:0.##} s\n\n" +
                                     "drag to slide the window, the ends to resize it, click to pick its curves");
                }
                if (ImGui.IsItemActivated()) { p.SelectedEmitter = i; ptfxDragEmitter_R6 = i; ptfxDragEdge_R6 = 0; }

                if (ptfxDragEmitter_R6 == i && ev != null && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    float d = ImGui.GetIO().MouseDelta.X / Math.Max(axW, 1.0f);
                    if (ptfxDragEdge_R6 < 0) { ev.StartRatio = Math.Clamp(s + d, 0f, e - 0.01f); changed = true; }
                    else if (ptfxDragEdge_R6 > 0) { ev.EndRatio = Math.Clamp(e + d, s + 0.01f, 1f); changed = true; }
                    else
                    {
                        d = Math.Clamp(d, -s, 1f - e);
                        ev.StartRatio = Math.Clamp(s + d, 0f, 1f);
                        ev.EndRatio = Math.Clamp(e + d, 0f, 1f);
                        changed = true;
                    }
                }
                if (ptfxDragEmitter_R6 == i && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) ptfxDragEmitter_R6 = -1;
                ImGui.PopID();

                if (changed) p.TouchFromTimeline(true);
            }
        }

        private void DrawPtfxCurveTrack_R6(ParticlePanel p, PtfxEffect eff, ImDrawListPtr dl,
                                           float ax0, float axW, float y, float dur)
        {
            var em = eff.Emitters[p.SelectedEmitter];
            ParticlePanel.CollectTimelineCurves(em, ptfxCurves_R6);
            if (ptfxCurves_R6.Count == 0)
            {
                ImGui.SetCursorScreenPos(new Vector2(ax0, y + 6.0f));
                ImGui.TextDisabled(em.Name + " has no keyframed curves");
                return;
            }
            p.TimelineCurve = Math.Clamp(p.TimelineCurve, 0, ptfxCurves_R6.Count - 1);

            ImGui.SetCursorScreenPos(new Vector2(ax0, y));
            ImGui.SetNextItemWidth(240.0f);
            var names = new string[ptfxCurves_R6.Count];
            for (int i = 0; i < names.Length; i++) names[i] = ptfxCurves_R6[i].name;
            int cur = p.TimelineCurve;
            if (ImGui.Combo("##ptfxcurvepick", ref cur, names, names.Length)) p.TimelineCurve = cur;
            ImGui.SameLine();
            ImGui.TextDisabled("drag a key: sideways = time, up/down = value");

            var kfp = ptfxCurves_R6[p.TimelineCurve].kfp;
            var vals = kfp?.Values?.data_items;
            if (vals == null || vals.Length == 0) return;

            float trackY = y + 22.0f, trackH = 40.0f;
            uint colBack = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f));
            uint colLine = ImGui.GetColorU32(UiTheme.AccentBright);
            uint colKey = ImGui.GetColorU32(new Vector4(1f, 0.95f, 0.4f, 0.95f));
            dl.AddRectFilled(new Vector2(ax0, trackY), new Vector2(ax0 + axW, trackY + trackH), colBack, 2.0f);

            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var v in vals)
            {
                lo = Math.Min(lo, v.KeyframeValue.X);
                hi = Math.Max(hi, v.KeyframeValue.X);
            }
            if (!(hi > lo)) { hi = lo + 1.0f; }
            float span = hi - lo;
            float KX(float t) => ax0 + axW * Math.Clamp(t, 0f, 1f);
            float KY(float v) => trackY + trackH - 4.0f - (trackH - 8.0f) * Math.Clamp((v - lo) / span, 0f, 1f);

            for (int i = 0; i < vals.Length - 1; i++)
                dl.AddLine(new Vector2(KX(vals[i].KeyframeTime.X), KY(vals[i].KeyframeValue.X)),
                           new Vector2(KX(vals[i + 1].KeyframeTime.X), KY(vals[i + 1].KeyframeValue.X)),
                           colLine, 1.6f);

            for (int i = 0; i < vals.Length; i++)
            {
                var kv = vals[i];
                float kx = KX(kv.KeyframeTime.X), ky = KY(kv.KeyframeValue.X);
                dl.AddNgonFilled(new Vector2(kx, ky), 5.0f, colKey, 4);

                ImGui.PushID(4000 + i);
                ImGui.SetCursorScreenPos(new Vector2(kx - 7.0f, ky - 7.0f));
                ImGui.InvisibleButton("##k", new Vector2(14.0f, 14.0f));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                    ImGui.SetTooltip($"t {kv.KeyframeTime.X:0.###}\n" +
                                     $"({kv.KeyframeValue.X:0.####}, {kv.KeyframeValue.Y:0.####}, " +
                                     $"{kv.KeyframeValue.Z:0.####}, {kv.KeyframeValue.W:0.####})");
                }
                if (ImGui.IsItemActivated()) ptfxDragKey_R6 = i;
                if (ptfxDragKey_R6 == i && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var md = ImGui.GetIO().MouseDelta;
                    float t = Math.Clamp(kv.KeyframeTime.X + md.X / Math.Max(axW, 1.0f), 0f, 1f);
                    if (i > 0) t = Math.Max(t, vals[i - 1].KeyframeTime.X + 0.001f);
                    if (i < vals.Length - 1) t = Math.Min(t, vals[i + 1].KeyframeTime.X - 0.001f);
                    kv.KeyframeTime = new SDX.Vector4(t, kv.KeyframeTime.Y, kv.KeyframeTime.Z, kv.KeyframeTime.W);

                    float v = kv.KeyframeValue.X - md.Y * span / Math.Max(trackH - 8.0f, 1.0f);
                    kv.KeyframeValue = new SDX.Vector4(v, kv.KeyframeValue.Y, kv.KeyframeValue.Z, kv.KeyframeValue.W);
                    p.TouchFromTimeline(false);
                }
                if (ptfxDragKey_R6 == i && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) ptfxDragKey_R6 = -1;
                ImGui.PopID();
            }

            ImGui.SetCursorScreenPos(new Vector2(ax0, trackY + trackH + 2.0f));
            ImGui.TextDisabled($"{ptfxCurves_R6[p.TimelineCurve].name}   {vals.Length} " +
                               (vals.Length == 1 ? $"key, x = {vals[0].KeyframeValue.X:0.###}"
                                                 : $"keys, x from {lo:0.###} to {hi:0.###}"));
        }
    }
}

