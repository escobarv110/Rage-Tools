using System;
using ImGuiNET;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool uiScaleSynced_V17;

        private void ServiceUiScale_V17()
        {
            if (panel == null) return;

            if (panel.ForcePanelSize_V17 > 0) panel.ForcePanelSize_V17--;

            if (!uiScaleSynced_V17)
            {
                uiScaleSynced_V17 = true;
                if (Math.Abs(settings.PanelScaleV17 - Editor.UiScale_V17.Scale) > 0.005f)
                {
                    ScalePanelWidths_V17(Editor.UiScale_V17.Scale);
                    panel.ForcePanelSize_V17 = 3;
                    settings.Save();
                }
            }
            int screenW = System.Windows.Forms.Screen.FromControl(this)?.Bounds.Width ?? 1920;
            panel.UiScaleNote_V17 = Editor.UiScale_V17.Describe(settings.UiScaleV17, DeviceDpi, screenW);
            panel.UiScaleAuto_U29 = Editor.UiScale_V17.Auto(DeviceDpi, screenW);

            bool commit = panel.RequestUiScaleCommit_V45;
            panel.RequestUiScaleCommit_V45 = false;
            float want = 0f;
            if (panel.RequestUiScaleAuto_V17)
            {
                panel.RequestUiScaleAuto_V17 = false;
                settings.UiScaleV17 = 0f;
                want = Editor.UiScale_V17.Auto(DeviceDpi, screenW);
                commit = true;
            }
            else if (panel.RequestUiScale_V17 > 0.01f)
            {
                want = panel.RequestUiScale_V17;
                settings.UiScaleV17 = want;
            }
            panel.RequestUiScale_V17 = 0f;

            if (want > 0.01f && Math.Abs(want - Editor.UiScale_V17.Scale) >= 0.005f)
            {
                ScalePanelWidths_V17(want);
                Editor.UiScale_V17.Set(want, ImGui.GetIO(), imguiRenderer);
                panel.ApplyThemeFromSettings();
            }
            if (commit)
            {
                settings.Save();
                Console.WriteLine($"UISCALE -> {Editor.UiScale_V17.Scale:0.##}x");
            }
            if (panel.RequestUiFontRebuild_V20)
            {
                panel.RequestUiFontRebuild_V20 = false;
                Editor.UiScale_V17.FontFile = settings.UiFontV20 ?? "";
                Editor.UiScale_V17.FontPx = settings.UiFontPxV20;
                Editor.UiScale_V17.Rebuild(ImGui.GetIO(), imguiRenderer);
                settings.Save();
                Console.WriteLine($"UIFONT -> {(string.IsNullOrEmpty(settings.UiFontV20) ? "built-in" : System.IO.Path.GetFileName(settings.UiFontV20))} {settings.UiFontPxV20:0}px");
            }
        }

        private void ScalePanelWidths_V17(float want)
        {
            float from = settings.PanelScaleV17 > 0.01f ? settings.PanelScaleV17 : 1.0f;
            if (Math.Abs(want - from) < 0.005f) return;
            float k = want / from;
            settings.LeftPanelWidth = Math.Clamp(settings.LeftPanelWidth * k, 120f, 1200f);
            settings.RightPanelWidth = Math.Clamp(settings.RightPanelWidth * k, 200f, 1600f);
            settings.PanelScaleV17 = want;
            if (panel != null) panel.ForcePanelSize_V17 = 3;
            Console.WriteLine($"UISCALE panels x{k:0.###} -> left {settings.LeftPanelWidth:0}, right {settings.RightPanelWidth:0}");
        }
    }
}

