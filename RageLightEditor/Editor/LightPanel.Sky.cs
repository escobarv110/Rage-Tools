using System;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool ToneMapCodeWalker = true;
        public bool AutoExposure = true;
        public float RageBloom = 1.0f;

        public bool GameFog = true;
        public float FogScale = 1.0f;
        public bool WaterRefraction = true;

        public float SunShadowDistance = 600.0f;
        public int SunCascadeCount = 4;
        public float SkySaturation_V68 = 1.35f;
        public float ShadowSoftness_V68 = 0.35f;

        public bool CloudsEnabled = true;
        public string CloudFrag = "contrails";
        public string[] CloudFragNames = Array.Empty<string>();
        public bool CloudsFollowWeather = false;
        public bool CloudsGameLighting = true;

        partial void DrawRenderExtras_Sky()
        {
            OptCheck("Clouds", ref CloudsEnabled, "The game's cloud hat meshes (clouds.xml), drawn over the sky the way RAGE draws them.");
            if (CloudsEnabled && CloudFragNames.Length > 0)
            {
                int idx = Array.IndexOf(CloudFragNames, CloudFrag);
                if (idx < 0) idx = 0;
                if (CloudsFollowWeather) ImGui.BeginDisabled();
                OptWidth();
                if (ImGui.Combo("Cloud hat", ref idx, CloudFragNames, CloudFragNames.Length))
                    CloudFrag = CloudFragNames[Math.Clamp(idx, 0, CloudFragNames.Length - 1)];
                Tip(CloudsFollowWeather
                    ? "The weather picks the hat (Advanced > Clouds follow weather)."
                    : "Which cloud hat fragment to draw. 'contrails' is the default.");
                if (CloudsFollowWeather) ImGui.EndDisabled();
            }
        }

        partial void DrawRenderExtras_SkyAdvanced()
        {
            if (CloudsEnabled)
            {
                OptSlider("Cloud speed", ref CloudSpeed, 0.0f, 5.0f, "%.2f", "How fast the hat drifts. 1 = the game's.", def: 1.0f);
                OptCheck("Weather clouds", ref CloudsFollowWeather,
                         "Pick the hat from the weather's cloud settings (weather.xml -> cloudkeyframes.xml CloudList)\ninstead of the Cloud hat combo.");
                SameCol();
                OptCheck("Cloud lighting", ref CloudsGameLighting,
                         "Light the hats with the game's cloudkeyframes.xml colours for the hour and weather\n" +
                         "(sun/moon, ambient, sky fill, scattering) - dark at night, bright at noon.\n" +
                         "Off: a flat directional-light colour, the classic map-viewer look.");
            }
            OptCheck("Refraction", ref WaterRefraction,
                     "Read the scene behind the water surface through the ripples (the game's refraction\n" +
                     "target). Off: the water alpha-blends over what was drawn, as before.");
        }

        partial void DrawLightingExtras_Sky()
        {
            ViewGroup("Tone");
            int tm = ToneMapCodeWalker ? 0 : 1;
            OptWidth();
            if (ImGui.Combo("Tone map", ref tm, "RAGE\0Filmic\0")) ToneMapCodeWalker = tm == 0;
            Tip("RAGE: average-luminance auto exposure, Reinhard with a white point, 0.6 x bloom.\n" +
                "Filmic: the timecycle's own filmic curve and colour correction at a fixed exposure.");
            if (ToneMapCodeWalker)
            {
                OptCheck("Auto exposure", ref AutoExposure, "Adapt the exposure to the frame's average brightness, as the game does.\nOff: the Exposure slider alone.");
                OptSlider("Bloom", ref RageBloom, 0.0f, 2.0f, "%.2f", "Scale on the glow around bright things. 0 = off, 1 = the reference look.", def: 1.0f);
            }
            float defW = ImGui.CalcTextSize("Default").X + ImGui.GetStyle().FramePadding.X * 2.0f;
            float sp = ImGui.GetStyle().ItemInnerSpacing.X;
            ImGui.SetNextItemWidth(Math.Max(40.0f, ImGui.GetContentRegionAvail().X - OptLabelW - defW - sp));
            ImGui.SliderFloat("##exposure", ref Exposure, 0.05f, 4.0f, "%.2f");
            Tip("1.0 = the automatic result. Below 1 brightens, above 1 darkens (it scales the\nadapted luminance the operator divides by).");
            Exposure = Math.Clamp(Exposure, 0.05f, 4.0f);
            ImGui.SameLine(0, sp);
            if (ImGui.Button("Default##exposure")) Exposure = DefaultExposure_U25;
            Tip("Back to " + DefaultExposure_U25.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".");
            ImGui.SameLine(0, sp);
            ImGui.TextUnformatted("Exposure");

            ViewGroup("Fog");
            OptCheck("Enabled", ref GameFog,
                     "The game's fog, driven by the timecycle's fog_* values - ground fog\n" +
                     "with height falloff, distance haze, sun/moon tinted atmosphere. Thick in w_foggy, a\n" +
                     "faint blue haze in w_clear, and it thins by itself as the camera climbs.");
            if (GameFog)
                OptSlider("Density", ref FogScale, 0.0f, 4.0f, "%.2f", "Multiplies the cycle's fog and haze densities. 1 = the game's.", def: 1.0f);

            if (WorldMode)
            {
                ViewGroup("Sun shadows");
                OptSlider("Distance", ref SunShadowDistance, 50.0f, 3000.0f, "%.0f m",
                          "How far from the camera the sun's cascaded shadow maps reach (the game's\nintervals 7/20/65/160/600 m scale to it). Further costs more per frame.", def: 600.0f);
            }
        }

        partial void DrawLightingExtras_SkyAdvanced()
        {
            OptCheck("HDR", ref TimecycleHdr,
                     "HDR: the timecycle's full light and sky intensities (light_dir_mult, sky_hdr),\n" +
                     "divided back out by the auto exposure. Off: the LDR clamps (sun to 1,\n" +
                     "ambient to 0.5, sky_hdr to 1.8).");
            OptSlider("Sky exposure", ref SkyExposure, 0.25f, 4.0f, "%.2f", "Bias on the sky's sky_hdr scale alone. 1.0 = the game's.", def: 1.0f);
            OptSlider("Sky blue", ref SkySaturation_V68, 0.5f, 2.5f, "%.2f",
                      "How much colour the sky keeps overhead. 1.0 is the timecycle's own value, " +
                      "which the tone mapping washes towards grey; above that the zenith goes " +
                      "deeper blue and the horizon is left alone.", def: 1.35f);
            OptSlider("Shadow softness", ref ShadowSoftness_V68, 0.05f, 1.5f, "%.2f m",
                      "How wide the sun's shadow edge is, in metres of world - the same width in " +
                      "every cascade, so it no longer jumps as you fly away.", def: 0.35f);
            if (WorldMode)
                OptSliderInt("Shadow cascades", ref SunCascadeCount, 1, 4, "%d", "How many cascades the sun's shadow map is split into. 4 = the game's; fewer is faster and coarser.", def: 4);
        }
    }
}

