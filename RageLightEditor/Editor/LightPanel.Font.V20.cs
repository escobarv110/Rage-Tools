using System;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool RequestUiFontRebuild_V20;
        private string fontFilter_V20 = "";

        private void DrawFontSection_V20()
        {
            ImGui.Separator();
            ImGui.TextDisabled("Text");
            var fonts = FontCatalog_V20.All();
            string current = settings.UiFontV20 ?? "";
            string label = "Built-in (ProggyClean)";
            if (!string.IsNullOrEmpty(current))
            {
                var hit = fonts.Find(f => string.Equals(f.Path, current, StringComparison.OrdinalIgnoreCase));
                label = hit != null ? hit.Label : Path.GetFileNameWithoutExtension(current);
            }
            ImGui.SetNextItemWidth(200.0f);
            if (ImGui.BeginCombo("Font##v20", label))
            {
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##fontfilter", "type to filter", ref fontFilter_V20, 64);
                if (ImGui.Selectable("Built-in (ProggyClean)", string.IsNullOrEmpty(current)))
                {
                    settings.UiFontV20 = "";
                    RequestUiFontRebuild_V20 = true;
                }
                ImGui.BeginChild("##fontlist", new Vector2(0, 240), ImGuiChildFlags.None);
                foreach (var f in fonts)
                {
                    if (fontFilter_V20.Length > 0 && f.Label.IndexOf(fontFilter_V20, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (ImGui.Selectable(f.Label + "##" + f.Path, string.Equals(f.Path, current, StringComparison.OrdinalIgnoreCase)))
                    {
                        settings.UiFontV20 = f.Path;
                        RequestUiFontRebuild_V20 = true;
                    }
                }
                ImGui.EndChild();
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Any TrueType font installed on this PC. Applied straight away.");

            float px = settings.UiFontPxV20 > 0.5f ? settings.UiFontPxV20 : (string.IsNullOrEmpty(current) ? UiScale_V17.ProggyPx : UiScale_V17.BaseFontPx);
            ImGui.SetNextItemWidth(200.0f);
            if (UiSlider_U28.Float("Text size##v20", ref px, 9.0f, 32.0f, "%.0f px")) settings.UiFontPxV20 = px;
            if (ImGui.IsItemDeactivatedAfterEdit()) RequestUiFontRebuild_V20 = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Text size at 100% interface size. Rebuilt when you let go of the slider.");
            if (ImGui.MenuItem("Default text##v20"))
            {
                settings.UiFontV20 = "";
                settings.UiFontPxV20 = 0f;
                RequestUiFontRebuild_V20 = true;
            }
        }
    }
}
