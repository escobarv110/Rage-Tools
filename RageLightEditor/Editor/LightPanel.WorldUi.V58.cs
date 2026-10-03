using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private bool worldSectionsInit_V58;

        private bool WorldSection_V58(string label, bool openByDefault)
        {
            if (!worldSectionsInit_V58)
                ImGui.SetNextItemOpen(false, ImGuiCond.FirstUseEver);
            return ImGui.CollapsingHeader(label);
        }

        private void DrawWorldMapTab_V58(float displayHeight)
        {
            DrawWorkspaceLogo(150.0f);

            if (!WorldReady)
            {
                ImGui.TextWrapped(string.IsNullOrEmpty(GameLoadStatus)
                    ? "Waiting for the game archives. Set the GTA V folder in the Lights workspace if this does not finish."
                    : GameLoadStatus);
                return;
            }

            if (WorldTruncated)
            {
                ImGui.TextColored(UiTheme.Warn, "Budget reached");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Some of what is near you was not reached.\n" +
                                     "Raise Budget, or lower Radius so there is less of it to get through.\n" +
                                     "Both are in Options on the right.");
            }

            ImGui.Spacing();
            if (ImGui.Button(WorldFindTotal_V55 > 0 ? $"World search  ({WorldFindTotal_V55:N0} found)" : "World search",
                             new Vector2(-1, 0)))
                WorldSearchOpen_V56 = !WorldSearchOpen_V56;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Find any prop in the loaded world by name - ymap entities and the\n" +
                                 "props inside MLO interiors. Click a hit to select it and fly there.");
            DrawWorldFindWindow_V56();

            ImGui.Spacing();

            if (WorldSection_V58("Time & weather##v58", true))
            {
                ImGui.Spacing();
                UiTheme.PushTimeSlider();
                ImGui.SetNextItemWidth(-46);
                UiSlider_U28.Float("##hourw", ref PreviewHour, 0.0f, 23.99f,
                    $"{(int)PreviewHour:00}:{(int)((PreviewHour % 1.0f) * 60):00}");
                HourScrubbing = ImGui.IsItemActive();
                UiTheme.PopTimeSlider();
                ImGui.SameLine();
                ImGui.TextColored(UiTheme.TimeGrab, "Time");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The hour of day. Hold the RIGHT mouse button in the viewport\nand move to scrub it.");

                ImGui.Checkbox("Run clock", ref AutoTime);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Let the day/night cycle play out on its own.");
                if (AutoTime)
                {
                    ImGui.SameLine();
                    UiTheme.PushTimeSlider();
                    ImGui.SetNextItemWidth(-1);
                    UiSlider_U28.Float("##timespeed", ref TimeSpeed, 1.0f, 600.0f, "%.0f min/s");
                    UiTheme.PopTimeSlider();
                }

                ImGui.SetNextItemWidth(-1);
                var wnames = string.Concat(WeatherSystem.Presets.Select(p => p.Name + "\0"));
                if (ImGui.Combo("##weather", ref WeatherIndex, wnames)) RequestedWeather = WeatherIndex;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("The weather preset: its timecycle, fog and cloud cover.");

                ImGui.Checkbox("Sky", ref ShowSky);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The game's atmosphere gradient with the sun, moon and clouds.");
                ImGui.SameLine(0, 14);
                ImGui.Checkbox("Weather", ref WeatherEnabled);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Applies the preset's fog and cloud cover on top of its timecycle.");

                if (!string.IsNullOrEmpty(WeatherStatus) && WeatherStatus.Contains("->"))
                    ImGui.TextDisabled(WeatherStatus);

                if (ImGui.TreeNodeEx("More##v58weather", ImGuiTreeNodeFlags.SpanAvailWidth))
                {
                    if (ImGui.Checkbox("Drag time", ref ControlTimeOfDay))
                    {
                        settings.ControlTimeOfDay = ControlTimeOfDay;
                        settings.Save();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Hold the RIGHT mouse button in the viewport and move: right or up\n" +
                                         "advances the clock, left or down winds it back. A plain right-click\n" +
                                         "still selects.");
                    float wt = settings.WeatherTransitionSeconds;
                    ImGui.SetNextItemWidth(-96);
                    if (UiSlider_U28.Float("##wtrans", ref wt, 0.0f, 20.0f, wt < 0.05f ? "instant" : "%.1f s"))
                        settings.WeatherTransitionSeconds = wt;
                    if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                    ImGui.SameLine();
                    ImGui.TextDisabled("Transition");
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How long a weather change takes to arrive. Instant by default:\n" +
                                         "mid-fade the scene is a blend of two cycles.");
                    ImGui.TreePop();
                }
                ImGui.Spacing();
            }



            worldSectionsInit_V58 = true;
        }
    }
}
