using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public void DrawAppearanceMenu_V19()
        {
            if (!ImGui.BeginMenu("Appearance")) return;
            DrawAppearanceBody_V19();
            ImGui.EndMenu();
        }

        private void DrawAppearanceBody_V19()
        {
            ImGui.TextDisabled("How the whole tool looks");
            ImGui.Separator();

            int theme = settings.ThemeIndex;
            ImGui.SetNextItemWidth(200.0f);
            if (ImGui.Combo("Colours##v19", ref theme, UiTheme.Names, UiTheme.Names.Length))
            {
                settings.ThemeIndex = theme;
                ApplyThemeFromSettings();
            }

            if (theme == 3)
                ImGui.TextDisabled("The classic theme uses ImGui's own blue.");
            else
            {
                var target = settings.Accent;
                var acc = new Vector3(target[0], target[1], target[2]);
                ImGui.SetNextItemWidth(200.0f);
                if (ImGui.ColorEdit3("Accent##v19", ref acc))
                {
                    target[0] = acc.X; target[1] = acc.Y; target[2] = acc.Z;
                    var m = settings.AccentMaterial;
                    m[0] = acc.X; m[1] = acc.Y; m[2] = acc.Z;
                    ApplyThemeFromSettings();
                }
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            }

            DrawLayoutMenu_V30();

            ImGui.Separator();
            ImGui.TextDisabled("Size");

            float uiScale = settings.UiScaleV17 <= 0.01f ? UiScale_V17.Scale : settings.UiScaleV17;
            ImGui.SetNextItemWidth(200.0f);
            if (UiSlider_U28.Float("Interface size##v19", ref uiScale, UiScale_V17.Min, UiScale_V17.Max, "%.2fx"))
                RequestUiScale_V17 = uiScale;
            if (ImGui.IsItemDeactivatedAfterEdit()) RequestUiScaleCommit_V45 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Scales everything - text, padding, every panel.\n" +
                                 "Applied straight away, no restart.");
            if (ImGui.MenuItem("Work it out from my display")) RequestUiScaleAuto_V17 = true;
            ImGui.TextDisabled(UiScaleNote_V17 ?? "");

            DrawFontSection_V20();
            DrawInterfaceSection_U27();
        }
    }
}

