using System;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private string logFilter_U21 = "";
        private bool logErrorsOnly_U21;
        private bool logFollow_U21 = true;
        private int logSeenVersion_U21 = -1;

        private void DrawHelpLogItems_U21()
        {
            ImGui.Separator();
            int n = AppLog_U21.ErrorsUnseen;
            if (ImGui.MenuItem(n > 0 ? $"Show log ({n} error{(n == 1 ? "" : "s")})" : "Show log", "Ctrl+L")) AppLog_U21.ShowWindow = true;
            if (ImGui.MenuItem("Open log folder")) OpenLogFolder_U21();
        }

        private static void OpenLogFolder_U21()
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppLog_U21.Folder);
                if (System.IO.File.Exists(AppLog_U21.FilePath)) Process.Start("explorer", "/select, \"" + AppLog_U21.FilePath + "\"");
                else Process.Start("explorer", "\"" + AppLog_U21.Folder + "\"");
            }
            catch { }
        }

        private void DrawLogBadge_U21()
        {
            int n = AppLog_U21.ErrorsUnseen;
            if (n <= 0) return;
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.42f, 0.36f, 1.0f));
            if (ImGui.MenuItem($"{n} error{(n == 1 ? "" : "s")}")) AppLog_U21.ShowWindow = true;
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Something went wrong - click to see what, with the full details.");
        }

        private void DrawLogWindow_U21(float displayWidth, float displayHeight)
        {
            var io = ImGui.GetIO();
            if (io.KeyCtrl && !io.WantTextInput && ImGui.IsKeyPressed(ImGuiKey.L, false)) AppLog_U21.ShowWindow = !AppLog_U21.ShowWindow;
            if (!AppLog_U21.ShowWindow) return;
            ImGui.SetNextWindowSize(new Vector2(Math.Min(900, displayWidth * 0.6f), Math.Min(420, displayHeight * 0.45f)), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(displayWidth * 0.2f, displayHeight * 0.5f), ImGuiCond.FirstUseEver);
            bool open = true;
            if (!ImGui.Begin("Log##rle_log", ref open)) { ImGui.End(); AppLog_U21.ShowWindow = open; return; }
            AppLog_U21.ShowWindow = open;
            AppLog_U21.MarkSeen();

            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##logfilter", "filter...", ref logFilter_U21, 128);
            ImGui.SameLine();
            ImGui.Checkbox("Errors only", ref logErrorsOnly_U21);
            ImGui.SameLine();
            ImGui.Checkbox("Follow", ref logFollow_U21);
            ImGui.SameLine();
            if (ImGui.Button("Copy")) ImGui.SetClipboardText(AppLog_U21.Text(logErrorsOnly_U21));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copies the log (or only the errors) - paste it when you report a problem.");
            ImGui.SameLine();
            if (ImGui.Button("Clear")) AppLog_U21.Clear();
            ImGui.SameLine();
            if (ImGui.Button("Open folder")) OpenLogFolder_U21();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(AppLog_U21.FilePath + "\nThe last run is kept beside it as rage_tools.previous.log.");
            ImGui.Separator();

            if (ImGui.BeginChild("##logbody", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar))
            {
                var errCol = new Vector4(1.0f, 0.45f, 0.40f, 1.0f);
                var warnCol = new Vector4(1.0f, 0.78f, 0.35f, 1.0f);
                var f = logFilter_U21.Trim();
                foreach (var e in AppLog_U21.Snapshot())
                {
                    if (logErrorsOnly_U21 && e.Level != AppLog_U21.Level.Error) continue;
                    if (f.Length > 0 && e.Text.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    ImGui.TextDisabled(e.Time.ToString("HH:mm:ss"));
                    ImGui.SameLine();
                    if (e.Level == AppLog_U21.Level.Error) ImGui.TextColored(errCol, e.Text);
                    else if (e.Level == AppLog_U21.Level.Warning) ImGui.TextColored(warnCol, e.Text);
                    else ImGui.TextUnformatted(e.Text);
                }
                if (logFollow_U21 && logSeenVersion_U21 != AppLog_U21.Version)
                {
                    ImGui.SetScrollHereY(1.0f);
                    logSeenVersion_U21 = AppLog_U21.Version;
                }
            }
            ImGui.EndChild();
            ImGui.End();
        }
    }
}
