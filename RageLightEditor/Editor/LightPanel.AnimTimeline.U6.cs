using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private bool animDragHead_U6;
        private UvAnimTrack animCurvePicked_U6;
        private int animDragKey_U6 = -1;
        private bool animShowCurve_U6 = true;

        partial void WorkspaceOverlay_U6(float displayWidth, float displayHeight)
        {
            if (!AnimMode || Anim == null || Anim.Clip == null) return;
            DrawAnimTimeline_U6(displayWidth, displayHeight);
        }

        private void DrawAnimTimeline_U6(float displayWidth, float displayHeight)
        {
            var a = Anim;
            var clip = a.Clip;

            float x0 = ShowLeftPanel ? settings.LeftPanelWidth : 0.0f;
            float x1 = displayWidth - (ShowRightPanel ? settings.RightPanelWidth : 0.0f);
            float w = Math.Max(x1 - x0, 320.0f);

            int rows = Math.Min(clip.Tracks.Count, 8);
            float rowH = ImGui.GetTextLineHeightWithSpacing() + 4.0f;
            bool curves = animShowCurve_U6 && a.SelectedTrack != null;
            float curveH = curves ? 92.0f : 0.0f;
            float h = 78.0f + rows * rowH + curveH;

            ImGui.SetNextWindowPos(new Vector2(x0, displayHeight - h), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.88f);
            var wf = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                   | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                   | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBringToFrontOnFocus;
            if (!ImGui.Begin("##animtimeline", wf)) { ImGui.End(); return; }

            DrawAnimTransport_U6(a, clip);

            float pad = 10.0f;
            float ax0 = ImGui.GetWindowPos().X + pad;
            float axW = Math.Max(ImGui.GetWindowSize().X - 2.0f * pad, 40.0f);
            float dur = Math.Max(clip.Duration, 0.0001f);
            float X(float seconds) => ax0 + axW * Math.Clamp(seconds / dur, 0f, 1f);

            var dl = ImGui.GetWindowDrawList();
            float rulerY = ImGui.GetCursorScreenPos().Y;
            DrawAnimRuler_U6(dl, ax0, axW, rulerY, dur, clip.Fps);

            ImGui.SetCursorScreenPos(new Vector2(ax0, rulerY));
            ImGui.InvisibleButton("##animscrub", new Vector2(axW, 16.0f));
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
            if (ImGui.IsItemActivated()) animDragHead_U6 = true;
            if (animDragHead_U6)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    a.Playing = false;
                    float frac = Math.Clamp((ImGui.GetIO().MousePos.X - ax0) / axW, 0f, 1f);
                    a.SeekFrame((int)Math.Round(frac * dur * Math.Max(clip.Fps, 1)));
                }
                else animDragHead_U6 = false;
            }

            float rowsY = rulerY + 20.0f;
            DrawAnimTrackRows_U6(a, clip, dl, ax0, axW, rowsY, rowH, rows, dur);

            float curveY = rowsY + rows * rowH + 4.0f;
            if (curves) DrawAnimCurveTrack_U6(a, dl, ax0, axW, curveY, dur);

            float hx = X(a.Time);
            uint colHead = ImGui.GetColorU32(new Vector4(1f, 0.95f, 0.4f, 0.95f));
            float headBottom = curves ? curveY + 78.0f : curveY;
            dl.AddLine(new Vector2(hx, rulerY), new Vector2(hx, headBottom), colHead, 1.6f);
            dl.AddNgonFilled(new Vector2(hx, rulerY + 2.0f), 5.0f, colHead, 3);

            ImGui.End();
        }

        private void DrawAnimTransport_U6(AnimEditor a, UvAnimClip clip)
        {
            int fps = Math.Max(clip.Fps, 1);
            if (ImGui.Button("|<", new Vector2(28, 0))) { a.Playing = false; a.SeekTo(0f); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("back to the start (Home)");
            ImGui.SameLine();
            if (ImGui.Button("<|", new Vector2(28, 0))) { a.Playing = false; a.SeekFrame(a.FrameOf(a.Time) - 1); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"back one frame ({1000.0f / fps:0} ms)");
            ImGui.SameLine();
            if (ImGui.Button(a.Playing ? "||" : ">", new Vector2(28, 0))) a.Playing = !a.Playing;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(a.Playing ? "pause (Space)" : "play (Space)");
            ImGui.SameLine();
            if (ImGui.Button("|>", new Vector2(28, 0))) { a.Playing = false; a.SeekFrame(a.FrameOf(a.Time) + 1); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("forward one frame");
            ImGui.SameLine();
            if (ImGui.Button(">|", new Vector2(28, 0))) { a.Playing = false; a.SeekTo(clip.Duration); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("to the end (End)");
            ImGui.SameLine();
            bool loop = clip.Loop;
            if (ImGui.Checkbox("Loop", ref loop)) clip.Loop = loop;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120.0f);
            UiSlider_U28.Float("##animtlspeed", ref a.TimeScale, 0.05f, 3.0f, "%.2fx");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("playback speed - a view control. The clip's own frame rate never moves with it.");

            ImGui.SameLine();
            ImGui.TextColored(AnimWorkspaceColour, clip.Name ?? "uv_anim");
            ImGui.SameLine();
            ImGui.TextDisabled($"{a.Time:0.000} / {clip.Duration:0.00} s   frame {a.FrameOf(a.Time)}/{clip.FrameCount - 1}   " +
                               $"{a.LiveMeshes} mesh(es) animating");

            ImGui.SameLine();
            float right = ImGui.GetWindowSize().X - 130.0f;
            if (right > ImGui.GetCursorPosX()) ImGui.SetCursorPosX(right);
            ImGui.Checkbox("Curve", ref animShowCurve_U6);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show the selected material's channel as keys on the same time axis.\n" +
                                 "Drag a key sideways to move it in time, up or down to change its value.");
        }

        private static void DrawAnimRuler_U6(ImDrawListPtr dl, float ax0, float axW, float y, float dur, int fps)
        {
            uint colTick = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.30f));
            uint colTickMinor = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.13f));
            uint colBack = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.06f));
            dl.AddRectFilled(new Vector2(ax0, y), new Vector2(ax0 + axW, y + 16.0f), colBack, 2.0f);

            float framePx = axW / Math.Max(dur * Math.Max(fps, 1), 1f);
            if (framePx >= 4.0f)
            {
                int frames = (int)(dur * fps + 0.5f);
                for (int f = 0; f <= frames; f++)
                {
                    float x = ax0 + axW * Math.Clamp(f / (float)Math.Max(frames, 1), 0f, 1f);
                    dl.AddLine(new Vector2(x, y + 12.0f), new Vector2(x, y + 16.0f), colTickMinor, 1.0f);
                }
            }

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

        private void DrawAnimTrackRows_U6(AnimEditor a, UvAnimClip clip, ImDrawListPtr dl,
                                          float ax0, float axW, float y0, float rowH, int rows, float dur)
        {
            uint colBack = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.07f));
            uint colBar = ImGui.GetColorU32(new Vector4(AnimWorkspaceColour.X, AnimWorkspaceColour.Y, AnimWorkspaceColour.Z, 0.22f));
            uint colBarSel = ImGui.GetColorU32(new Vector4(AnimWorkspaceColour.X, AnimWorkspaceColour.Y, AnimWorkspaceColour.Z, 0.42f));
            uint colLine = ImGui.GetColorU32(AnimWorkspaceColour);
            uint colOff = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.18f));
            uint colText = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.85f));

            var sel = a.SelectedTrack;
            for (int i = 0; i < rows; i++)
            {
                var tr = clip.Tracks[i];
                if (tr == null) continue;
                float y = y0 + i * rowH;
                bool isSel = ReferenceEquals(tr, sel);

                dl.AddRectFilled(new Vector2(ax0, y + 1), new Vector2(ax0 + axW, y + rowH - 3),
                                 tr.Enabled ? (isSel ? colBarSel : colBar) : colBack, 2.0f);

                float lo = float.MaxValue, hi = float.MinValue;
                const int N = 48;
                var vals = new float[N + 1];
                for (int s = 0; s <= N; s++)
                {
                    tr.Evaluate(dur * s / N, out var r0, out var r1);
                    vals[s] = r0.Z + r1.Z;
                    lo = Math.Min(lo, vals[s]); hi = Math.Max(hi, vals[s]);
                }
                if (hi - lo < 1e-5f) { lo -= 0.5f; hi += 0.5f; }
                for (int s = 0; s < N; s++)
                {
                    float xa = ax0 + axW * s / N, xb = ax0 + axW * (s + 1) / N;
                    float ya = y + rowH - 4.0f - (rowH - 8.0f) * (vals[s] - lo) / (hi - lo);
                    float yb = y + rowH - 4.0f - (rowH - 8.0f) * (vals[s + 1] - lo) / (hi - lo);
                    dl.AddLine(new Vector2(xa, ya), new Vector2(xb, yb), tr.Enabled ? colLine : colOff, 1.3f);
                }

                dl.AddText(new Vector2(ax0 + 6.0f, y + 2.0f), colText,
                           $"[{tr.MaterialIndex}] {tr.MaterialName}" + (tr.Enabled ? "" : "  (off)"));

                ImGui.PushID(7100 + i);
                ImGui.SetCursorScreenPos(new Vector2(ax0, y + 1));
                ImGui.InvisibleButton("##row", new Vector2(axW, rowH - 4));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{tr.MaterialName}\n{(tr.Raw ? "raw 2x3" : "scale / rotation / offset")}" +
                                     (string.IsNullOrEmpty(tr.Preset) ? "" : $"\n{tr.Preset}") +
                                     "\n\nclick to edit it; double-click to turn it on and off");
                if (ImGui.IsItemClicked())
                {
                    int row = a.Materials.FindIndex(m => m.Index == tr.MaterialIndex);
                    if (row >= 0) a.SelectedMaterial = row;
                }
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && ImGui.IsItemHovered())
                    tr.Enabled = !tr.Enabled;
                ImGui.PopID();
            }

            if (clip.Tracks.Count > rows)
            {
                ImGui.SetCursorScreenPos(new Vector2(ax0, y0 + rows * rowH));
                ImGui.TextDisabled($"...and {clip.Tracks.Count - rows} more track(s)");
            }
        }

        private void DrawAnimCurveTrack_U6(AnimEditor a, ImDrawListPtr dl, float ax0, float axW, float y, float dur)
        {
            var tr = a.SelectedTrack;
            if (tr == null) return;
            var chans = tr.ActiveChannels;
            a.TimelineChannel = Math.Clamp(a.TimelineChannel, 0, chans.Length - 1);
            if (animCurvePicked_U6 != tr && !tr.Curve(chans[a.TimelineChannel]).Animated)
            {
                int keyed = Array.FindIndex(chans, c => tr.Curve(c).Animated);
                if (keyed >= 0) a.TimelineChannel = keyed;
            }
            animCurvePicked_U6 = tr;

            var names = new string[chans.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var c = tr.Curve(chans[i]);
                names[i] = UvAnimTrack.ChannelLabel(chans[i]) + (c.Animated ? $"  ({c.Keys.Count})" : "");
            }

            ImGui.SetCursorScreenPos(new Vector2(ax0, y));
            ImGui.SetNextItemWidth(240.0f);
            int cur = a.TimelineChannel;
            if (ImGui.Combo("##animchanpick", ref cur, names, names.Length)) a.TimelineChannel = cur;
            ImGui.SameLine();
            var chan = chans[a.TimelineChannel];
            var curve = tr.Curve(chan);
            if (ImGui.SmallButton("Key here")) curve.SetKey(a.Clip.Wrap(a.Time), curve.Evaluate(a.Clip.Wrap(a.Time)));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add a key on this channel at the playhead.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Key all")) KeyAllChannels_U6(tr, a.Clip.Wrap(a.Time));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Key every channel of this track at the playhead (K).");
            ImGui.SameLine();
            ImGui.TextDisabled("drag a key: sideways = time, up/down = value; right-click deletes");

            float trackY = y + 22.0f, trackH = 52.0f;
            uint colBack = ImGui.GetColorU32(new Vector4(1, 1, 1, 0.05f));
            uint colLine = ImGui.GetColorU32(AnimWorkspaceColour);
            uint colKey = ImGui.GetColorU32(new Vector4(1f, 0.95f, 0.4f, 0.95f));
            dl.AddRectFilled(new Vector2(ax0, trackY), new Vector2(ax0 + axW, trackY + trackH), colBack, 2.0f);

            float lo = float.MaxValue, hi = float.MinValue;
            const int N = 64;
            for (int i = 0; i <= N; i++)
            {
                float v = curve.Evaluate(dur * i / N);
                lo = Math.Min(lo, v); hi = Math.Max(hi, v);
            }
            if (hi - lo < 1e-5f) { lo -= 0.5f; hi += 0.5f; }
            float span = hi - lo;
            float KX(float t) => ax0 + axW * Math.Clamp(t / dur, 0f, 1f);
            float KY(float v) => trackY + trackH - 5.0f - (trackH - 10.0f) * Math.Clamp((v - lo) / span, 0f, 1f);

            for (int i = 0; i < N; i++)
                dl.AddLine(new Vector2(KX(dur * i / N), KY(curve.Evaluate(dur * i / N))),
                           new Vector2(KX(dur * (i + 1) / N), KY(curve.Evaluate(dur * (i + 1) / N))),
                           colLine, 1.6f);

            var keys = curve.Keys;
            for (int i = 0; keys != null && i < keys.Count; i++)
            {
                var k = keys[i];
                float kx = KX(k.Time), ky = KY(k.Value);
                dl.AddNgonFilled(new Vector2(kx, ky), 5.0f, colKey, 4);

                ImGui.PushID(7300 + i);
                ImGui.SetCursorScreenPos(new Vector2(kx - 7.0f, ky - 7.0f));
                ImGui.InvisibleButton("##k", new Vector2(14.0f, 14.0f), ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                    ImGui.SetTooltip($"t {k.Time:0.###} s (frame {a.FrameOf(k.Time)})\nvalue {k.Value:0.#####}");
                }
                if (ImGui.IsItemActivated() && ImGui.IsMouseDown(ImGuiMouseButton.Left)) animDragKey_U6 = i;
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) { curve.RemoveKeyAt(i); ImGui.PopID(); break; }
                if (animDragKey_U6 == i && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var md = ImGui.GetIO().MouseDelta;
                    float t = Math.Clamp(k.Time + md.X * dur / Math.Max(axW, 1.0f), 0f, dur);
                    if (i > 0) t = Math.Max(t, keys[i - 1].Time + 1e-3f);
                    if (i < keys.Count - 1) t = Math.Min(t, keys[i + 1].Time - 1e-3f);
                    k.Time = t;
                    k.Value -= md.Y * span / Math.Max(trackH - 10.0f, 1.0f);
                }
                if (animDragKey_U6 == i && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) animDragKey_U6 = -1;
                ImGui.PopID();
            }

            ImGui.SetCursorScreenPos(new Vector2(ax0, trackY + trackH + 2.0f));
            ImGui.TextDisabled(UvAnimTrack.ChannelLabel(chan) + "   " +
                               (curve.Animated
                                   ? $"{curve.Keys.Count} key(s), {lo:0.###} to {hi:0.###}"
                                   : $"constant {curve.Constant:0.####} - press K on the slider to key it"));
        }

        public void KeyAllChannels_U6(UvAnimTrack tr, float t)
        {
            if (tr == null) return;
            foreach (var ch in tr.ActiveChannels)
            {
                var c = tr.Curve(ch);
                c.SetKey(t, c.Evaluate(t));
            }
        }
    }
}

