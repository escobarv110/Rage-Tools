using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool ShowDistantLights_V47 = true;

        private const string SelectionKeysTip_M3 =
            "F  flies the camera to the selection (also the toolbar's Frame button,\n" +
            "and a double-click in the Project window).\n" +
            "Esc  deselects - or click empty ground.";

        private void DrawEntityClipboardRow_M3()
        {
            var style = ImGui.GetStyle();
            float w = (ImGui.GetContentRegionAvail().X - style.ItemSpacing.X * 3.0f) * 0.25f;
            if (ImGui.Button("Copy##went", new Vector2(w, 0))) RequestWorldCopy = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy the entity  (Ctrl+C)");
            ImGui.SameLine();
            if (ImGui.Button("Duplicate##went", new Vector2(w, 0))) RequestWorldDuplicate = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A copy beside it, selected  (Ctrl+D)");
            ImGui.SameLine();
            if (ImGui.Button("Paste##went", new Vector2(w, 0))) RequestWorldPaste = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Into the SELECTED entity's ymap - the paste needs a target file,\n" +
                                 "and the selection names it. Works across ymaps: copy somewhere,\n" +
                                 "select anything in the destination file, paste.  (Ctrl+V)");
            ImGui.SameLine();
            if (DangerButton("Delete##went", new Vector2(w, 0))) RequestWorldDelete = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("No confirmation - deleting is one Undo away.  (Delete)");
            if (!string.IsNullOrEmpty(WorldClipboardSummary))
                ImGui.TextDisabled("clipboard: " + WorldClipboardSummary);
        }

        public void RequestFrameWorldSelection_M3()
        {
            if (WorldSel != null) RequestWorldEntityGoto = true;
            else if (WorldSelection.HasValue) RequestWorldSelectionFrame = true;
        }

        private const float OptLabelW = 140.0f;
        private static void OptWidth() => ImGui.SetNextItemWidth(-OptLabelW);

        private static void Tip(string tip)
        {
            if (!string.IsNullOrEmpty(tip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(tip);
        }

        private static void Info(string tip)
        {
            if (string.IsNullOrEmpty(tip)) return;
            ImGui.SameLine(0, 6);
            float h = ImGui.GetFrameHeight();
            float r = MathF.Round(ImGui.GetFontSize() * 0.42f);
            var pos = ImGui.GetCursorScreenPos();
            var c = new Vector2(pos.X + r + 1, pos.Y + h * 0.5f);
            ImGui.InvisibleButton("##info" + tip.GetHashCode(), new Vector2(r * 2 + 2, h));
            bool hov = ImGui.IsItemHovered();
            var dl = ImGui.GetWindowDrawList();
            var col = hov ? UiTheme.AccentBright : UiTheme.Muted;
            uint u = ImGui.ColorConvertFloat4ToU32(col);
            dl.AddCircle(c, r, u, 16, 1.2f);
            var ts = ImGui.CalcTextSize("i");
            dl.AddText(new Vector2(c.X - ts.X * 0.5f, c.Y - ts.Y * 0.5f), u, "i");
            if (hov) ImGui.SetTooltip(tip);
        }

        private static bool OptCheck(string label, ref bool v, string tip = null)
        {
            bool ch = ImGui.Checkbox(label, ref v);
            Tip(tip);
            return ch;
        }

        private static bool OptSlider(string label, ref float v, float min, float max, string fmt, string tip = null)
        {
            OptWidth();
            bool ch = ImGui.SliderFloat(label, ref v, min, max, fmt);
            Tip(tip);
            v = Math.Clamp(v, min, max);
            return ch;
        }

        private static bool OptSliderInt(string label, ref int v, int min, int max, string fmt, string tip = null)
        {
            OptWidth();
            bool ch = ImGui.SliderInt(label, ref v, min, max, fmt);
            Tip(tip);
            v = Math.Clamp(v, min, max);
            return ch;
        }

        private static readonly bool advancedOpenByEnv = Environment.GetEnvironmentVariable("RLE_ADVANCED") == "1";
        private static readonly float optScrollByEnv =
            float.TryParse(Environment.GetEnvironmentVariable("RLE_OPTSCROLL"), out var osc) ? osc : 0.0f;
        private static bool Advanced(string id)
        {
            ImGui.Spacing();
            if (advancedOpenByEnv) ImGui.SetNextItemOpen(true, ImGuiCond.Once);
            ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Muted);
            bool open = ImGui.TreeNodeEx("Advanced##adv" + id, ImGuiTreeNodeFlags.SpanAvailWidth);
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The rare and the diagnostic - fine as they are unless you know why not.");
            return open;
        }

        private void DrawViewAdvanced_M3()
        {
            if (!Advanced("view")) return;
            if (!MaterialMode)
            {
                OptSlider("Light boost", ref LightsMultiplier, 0.0f, 4.0f, "%.2fx",
                          "Debug multiplier on every light's contribution. 1.0 = game-accurate; anything\nelse is for finding a light, not for judging one.");
            }
            OptSlider("Ambient", ref AmbientLevel, 0.0f, 0.5f, "%.3f",
                      "The flat preview ambient of a scene with NO timecycle loaded. With a cycle\nloaded the cycle's own ambient is used and this does nothing visible.");
            DrawBackfaceAndVsync(showCount: true);
            ImGui.TreePop();
        }

        partial void DrawInteriorCullRoomsOption_J4();
        partial void DrawRenderExtras_SkyAdvanced();
        partial void DrawLightingExtras_SkyAdvanced();
        partial void DrawHelpersExtras_SelectionAdvanced();
        partial void DrawHelpersExtras_SpaceDataAdvanced();

        private void DrawWorldOptions_M3()
        {
            PersistWorldRenderOptions();
            if (optScrollByEnv > 0) ImGui.SetScrollY(optScrollByEnv);

            if (Header("Map", true))
            {
                DrawGeneralExtras_ScriptIpls();
                DrawGeneralExtras_ModsDlc_V21();
                DrawInteriorCullOption_J4();
                if (WorldRef != null)
                {
                    SameCol();
                    bool ny = WorldRef.ShowNorthYankton;
                    if (ImGui.Checkbox("North Yankton", ref ny)) WorldRef.ShowNorthYankton = ny;
                    Tip("Show North Yankton instead of Cayo Perico - the two share the same place on the map.");
                }
                if (ProjectWindow != null)
                {
                    bool renderGta = !ProjectWindow.HideGtaMap;
                    if (ImGui.Checkbox("GTA V map", ref renderGta)) ProjectWindow.HideGtaMap = !renderGta;
                    Tip("Draw the game's own map. Off: only the project's ymaps are drawn - the mod on an empty stage.");
                    SameCol();
                    ImGui.Checkbox("Project items", ref ProjectWindow.RenderProjectItems);
                    Tip("Draw the project's ymaps and props over the map.");
                }
                ImGui.Spacing();
                OptSlider("Radius", ref WorldStreamRadius, 100.0f, 2000.0f, "%.0f m",
                          "How far out ymaps are opened at all - the memory knob.\nDetail, below, is the quality knob.");
                OptSlider("Detail", ref WorldLodScale, 0.1f, 2.0f, "%.2f",
                          "Scales every entity's LOD distance. Below 1 swaps to coarser stand-ins\nsooner; above 1 holds the detailed version further out.");
                ImGui.Spacing();
                if (ImGui.Button("Reload", new Vector2(-1, 0))) RequestWorldReload = true;
                Tip("Drop every streamed ymap and model and stream the view again\n(also what makes a changed HD textures switch apply to what is already built).");
                if (Advanced("map"))
                {
                    OptCheck("View culling", ref WorldFrustumCull, "Only stream and draw what the camera can see. Off: everything in the radius, all round.");
                    SameCol();
                    DrawInteriorCullRoomsOption_J4();
                    DrawGeneralExtras_World();
                    if (WorldRef != null)
                    {
                        bool sy = WorldRef.ShowScriptedYmaps;
                        if (ImGui.Checkbox("Scripted ymaps", ref sy)) WorldRef.ShowScriptedYmaps = sy;
                        Tip("Ymaps the game loads only when a script asks for them - the island, the carrier,\nthe casino, every DLC addition.");
                        SameCol();
                        bool sv = WorldRef.ShowScriptedVariants;
                        if (ImGui.Checkbox("Every variant", ref sv)) WorldRef.ShowScriptedVariants = sv;
                        Tip("Off: of several scripted ymaps on the same footprint - the four garage states of\n" +
                            "one building, the three layouts of one office - only one is shown, as the game\n" +
                            "would. On: all of them at once, on top of each other." +
                            (WorldRef.HiddenScriptedVariants > 0 ? $"\n\nHiding {WorldRef.HiddenScriptedVariants} right now." : ""));
                    }
                    OptSliderInt("Budget", ref WorldMaxEntities, 5000, 120000, "%d",
                                 "Ceiling on entities picked in one frame. A SAFETY limit, not a quality one - use Detail for that.");
                    ImGui.TreePop();
                }
            }

            if (Header("Look", true))
            {
                DrawShadingCombo();
                ViewGroup("Scene");
                DrawRenderExtras_Materials();
                DrawRenderExtras_Sky();
                DrawLightingExtras_Sky();

                ViewGroup("Map lights");
                OptCheck("Lights", ref WorldLightsEnabled,
                         "The map's own lights - street lamps, signs, floodlights - placed with their\nentities and lit exactly as the light workspace lights are.");
                SameCol();
                OptCheck("LOD lights", ref WorldLodLightsEnabled,
                         "The _lodlights / _distantlights ymaps: the game's stand-in lights and the distant\nsprites that make the city glow at night. Skipped where the real light is streamed in.");
                DrawRenderExtras_P3();

                ViewGroup("Interiors");
                DrawLightingExtras_J3();

                if (Advanced("look"))
                {
                    DrawBackfaceAndVsync(showCount: false);
                    DrawRenderExtras_SkyAdvanced();
                    DrawLightingExtras_SkyAdvanced();
                    OptSlider("Light range", ref WorldLightsRange, 50.0f, 5000.0f, "%.0f m",
                              "How far real lights reach: the props' own lights and the game's LOD lights\n(street and building lights) both light the world out to here, nearest first\nup to the GPU budget. The distant light sprites carry the glow beyond.");
                    OptCheck("Timecycle lighting", ref TimecycleEnabled,
                             "Drive the world with the game's global lighting model\n(directional light + natural/artificial hemisphere ambient). Off: flat.");
                    OptCheck("Distant lights", ref ShowDistantLights_V47,
                             "The game's distant LOD light sprites - the city glow you see from far away\nat night, drawn from the lodlights ymaps the way CodeWalker draws them.");
                    DrawRegionCombo();
                    ImGui.TreePop();
                }
            }

            if (GotoFocus_O3) ImGui.SetNextItemOpen(true, ImGuiCond.Always);
            if (Header("Camera", true))
            {
                DrawGoToBody_O3();
                ImGui.Spacing();
                DrawFovRow(-140);
                DrawCameraKnobs();
            }

            if (Header("Helpers", true))
            {
                OptCheck("Animations", ref WorldAnimations, "Props whose archetype names a clip dictionary play it - fans turn, conveyor textures roll. Off freezes them where they are.");
                SameCol();
                OptCheck("Coronas", ref ShowCoronas, "The glow sprite the map's lights draw at their source.");
                if (WorldShowCollision)
                    OptSlider("Collision opacity", ref WorldCollisionOpacity, 0.2f, 1.0f, "%.2f", "1 = solid, CodeWalker's look; lower sees through to the model.");
                DrawHelpersExtras_H3();
                DrawHelpersExtras_Selection();
                DrawHelpersExtras_I4();
                DrawHelpersExtras_SpaceData();
                if (Advanced("helpers"))
                {
                    DrawHelpersExtras_SpaceDataAdvanced();
                    DrawHelpersExtras_SelectionAdvanced();
                    ImGui.TreePop();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.DangerButton);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UiTheme.DangerButtonHi);
            if (ImGui.Button("Reset all", new Vector2(-1, 0))) openWorldResetConfirm = true;
            ImGui.PopStyleColor(2);
            Tip("Put every option in this tree back to its default: streaming radius and detail,\n" +
                "culling, the ymap filters, collision, map lights, grass and HD textures, sky,\n" +
                "tone map, fog, clouds and sun cascades. The Lights workspace's settings, your\n" +
                "project and the theme are not touched.");
        }
    }
}

