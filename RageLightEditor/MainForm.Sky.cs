using System;
using System.Collections.Generic;
using SharpDX;
using SharpDX.Direct3D11;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;

namespace RageLightEditor
{
    public partial class MainForm
    {

        private GameFogVars BuildGameFog()
        {
            bool tcOn = panel.TimecycleEnabled && timecycle != null && timecycle.HasData;
            if (!panel.GameFog || !tcOn || NoGameFog_R5) return default;
            timecycle.GetLightDirection(panel.PreviewHour, out var sunDir, out var moonDir);
            var s = skyState;
            if (!timecycle.Has("fog_density"))
            {
                s.FogDensity = panel.WeatherEnabled ? weather.FogDensity : 0.0f;
                s.FogHazeDensity = 0.0f;
                s.FogHeightFalloff = 0.0f;
                s.FogStart = weather.FogStart;
                s.FogNearCol = s.FogFarCol = s.FogSunCol = s.FogColour;
                s.FogAlpha = 1.0f; s.FogHdr = 1.0f;
            }
            float indoors = InteriorFogScale_U18();
            if (indoors < 1.0f)
            {
                s.FogDensity *= indoors;
                s.FogHazeDensity *= indoors;
                s.FogStart = Math.Max(s.FogStart, 40.0f);
            }
            return GameFogVars.Build(s, camera.Position.Z, Vector3.Normalize(sunDir), Vector3.Normalize(moonDir),
                                     panel.HdrActive, panel.FogScale, true);
        }

        private int sunCascadeRebuilds;
        private double lastCascadeTime;
        private Vector3 lastCascadeCam;
        private Vector3 lastCascadeSun;
        private float lastCascadeDist;
        private int lastCascadeCount;
        private bool cascadesValid;
        private static readonly float[] cascadeScratch = new float[SunCascades.MaxCascades];

        private void TickWorldSunShadows(DeviceContext context, double now, ref ShadowSetup setup)
        {
            if (!sceneRenderer.GlobalLight.HasValue || worldRender.Model.Meshes.Count == 0)
            {
                cascadesValid = false;
                return;
            }
            var sunDir = sceneRenderer.GlobalLight.Value.LightDir;
            int count = Math.Clamp(panel.SunCascadeCount, 1, SunCascades.MaxCascades);
            float dist = Math.Max(panel.SunShadowDistance, 20.0f);

            bool scrubbing = panel.HourScrubbing;
            bool sunMoved = Vector3.DistanceSquared(sunDir, lastCascadeSun) > 1e-6f;
            bool camMoved = Vector3.DistanceSquared(camera.Position, lastCascadeCam) > 1.0f;
            bool turned = shadowRenderer.Cascades.Count > 0 &&
                          Vector3.DistanceSquared(camera.GetForward(), lastCascadeForward) > 0.0004f;
            bool settingsChanged = count != lastCascadeCount || Math.Abs(dist - lastCascadeDist) > 0.5f;
            bool worldChanged = worldRender.BuiltThisFrame > 0;
            bool due = (now - lastCascadeTime) >= 0.1;
            bool dirty = !cascadesValid || settingsChanged || worldChanged || sunMoved || camMoved || turned;
            if (dirty && (due || !cascadesValid) && !(scrubbing && cascadesValid))
            {
                float scale = dist / SunCascades.DefaultIntervals[count - 1];
                for (int i = 0; i < count; i++) cascadeScratch[i] = SunCascades.DefaultIntervals[i] * scale;
                float r = Math.Max(panel.WorldStreamRadius, dist) + 100.0f;
                var cp = camera.Position;
                var sceneMin = new Vector3(cp.X - r, cp.Y - r, Math.Min(cp.Z - 1500.0f, -200.0f));
                var sceneMax = new Vector3(cp.X + r, cp.Y + r, Math.Max(cp.Z + 1500.0f, 1200.0f));
                var cascadeClock = System.Diagnostics.Stopwatch.StartNew();
                shadowRenderer.RenderSunCascades(context, worldRender.SunCasters(), camera, sunDir,
                                                 cascadeScratch, count, sceneMin, sceneMax);
                perfCascadeMs = (float)cascadeClock.Elapsed.TotalMilliseconds;
                if (perfCascadeMs > perfCascadeMaxMs) perfCascadeMaxMs = perfCascadeMs;
                perfCascadeMsTotal += perfCascadeMs;
                sunCascadeRebuilds++;
                lastCascadeTime = now;
                lastCascadeCam = camera.Position;
                lastCascadeForward = camera.GetForward();
                lastCascadeSun = sunDir;
                lastCascadeDist = dist;
                lastCascadeCount = count;
                cascadesValid = true;
            }
            if (!cascadesValid) return;
            setup.SunMap = shadowRenderer.SunMapSRV;
            setup.SunMatrix = shadowRenderer.SunMatrix;
            setup.SunPos = shadowRenderer.SunPos;
            setup.SunEnabled = true;
            setup.SunStrength = panel.SunShadowStrength;
            setup.Cascades = shadowRenderer.Cascades;
        }
        private Vector3 lastCascadeForward;

        private static void CodeWalkerSunMoon(float timeofday, float sunRollDeg, float moonRollDeg, out Vector3 sundir, out Vector3 moondir)
        {
            float sunroll = sunRollDeg * (float)Math.PI / 180.0f;
            float moonroll = moonRollDeg * (float)Math.PI / 180.0f;
            float dayval = (0.5f + (timeofday - 6.0f) / 14.0f);
            float nightval = (((timeofday > 12.0f) ? (timeofday - 7.0f) : (timeofday + 17.0f)) / 9.0f);
            float daycyc = (float)Math.PI * dayval;
            float nightcyc = (float)Math.PI * nightval;
            Vector3 sdir = new Vector3((float)Math.Sin(daycyc), -(float)Math.Cos(daycyc), 0.0f);
            Vector3 mdir = new Vector3(-(float)Math.Sin(nightcyc), 0.0f, -(float)Math.Cos(nightcyc));
            Quaternion saxis = Quaternion.RotationYawPitchRoll(0.0f, sunroll, 0.0f);
            Quaternion maxis = Quaternion.RotationYawPitchRoll(0.0f, -moonroll, 0.0f);
            sundir = Vector3.Normalize(CodeWalker.QuaternionExtension.Multiply(saxis, sdir));
            moondir = Vector3.Normalize(CodeWalker.QuaternionExtension.Multiply(maxis, mdir));
        }

        private static Vector3 CodeWalkerLightDir(float hour, float sunRollDeg, float moonRollDeg)
        {
            CodeWalkerSunMoon(hour, sunRollDeg, moonRollDeg, out var sd, out var md);
            var d = (hour < 5.0f || hour > 21.0f) ? md : sd;
            if (d.Z < 0) d.Z = 0;
            return d.LengthSquared() > 1e-8f ? Vector3.Normalize(d) : Vector3.UnitZ;
        }

        private void SunDirectionTest_Sky(Action<string, bool, string> check)
        {
            var tc = timecycle ?? new TimecycleData();
            float worst = 0; float worstH = -1;
            for (float h = 0; h <= 24.0f; h += 0.5f)
            {
                tc.GetLightDirection(h, out var s, out var m);
                CodeWalkerSunMoon(h, tc.SunRoll, tc.MoonRoll, out var cs, out var cm);
                float e = Math.Max((s - cs).Length(), (m - cm).Length());
                if (e > worst) { worst = e; worstH = h; }
            }
            check("sun/moon direction is CodeWalker's function at every hour", worst < 1e-4f,
                  $"max diff {worst:0.000000} at {worstH:0.0}h (sun_roll {tc.SunRoll:0}, moon_roll {tc.MoonRoll:0})");
            tc.GetLightDirection(6.0f, out var s6, out _);
            tc.GetLightDirection(12.0f, out var s12, out _);
            tc.GetLightDirection(13.0f, out var s13, out _);
            tc.GetLightDirection(18.0f, out var s18, out _);
            check("sun rises in the east at 6h, low", s6.X > 0.9f && Math.Abs(s6.Z) < 0.15f, $"6h {s6}");
            check("sun is high at noon and highest at 13h", s12.Z > 0.75f && s13.Z >= s12.Z && s13.Z > 0.8f, $"12h {s12} 13h {s13}");
            check("sun sets in the west at 18h, low", s18.X < -0.7f && s18.Z < 0.5f && s18.Z < s12.Z, $"18h {s18}");
            tc.GetLightDirection(22.0f, out _, out var m22);
            var e22 = tc.Evaluate(22.0f, panel.HdrActive);
            var cw22 = CodeWalkerLightDir(22.0f, tc.SunRoll, tc.MoonRoll);
            var m22up = Vector3.Normalize(new Vector3(m22.X, m22.Y, Math.Max(m22.Z, 0)));
            check("22h lights with the moon, above the horizon", (e22.LightDir - cw22).Length() < 1e-4f && e22.LightDir.Z >= 0 &&
                  Vector3.Dot(e22.LightDir, m22up) > 0.999f,
                  $"LightDir {e22.LightDir} moon {m22}");
            var e12 = tc.Evaluate(12.0f, panel.HdrActive);
            check("12h lights with the sun", (e12.LightDir - Vector3.Normalize(s12)).Length() < 1e-4f, $"LightDir {e12.LightDir} sun {s12}");
            if (timecycle != null && timecycle.HasData && panel != null)
            {
                int savedMod = timecycle.SelectedModifier; float savedStr = timecycle.ModifierStrength;
                var was = panel.Workspace;
                float savedHour = panel.PreviewHour;
                var S = LightPanel.Space.Light; var W = LightPanel.Space.World;
                panel.SwitchWorkspace(S);
                timecycle.SelectedModifier = -1;
                float hourS = 9.5f; panel.PreviewHour = hourS;
                var glS = timecycle.Evaluate(panel.PreviewHour, panel.HdrActive);
                timecycle.GetLightDirection(panel.PreviewHour, out var sunS, out _);
                panel.SwitchWorkspace(W);
                timecycle.SelectedModifier = -1;
                panel.PreviewHour = hourS;
                var glW = timecycle.Evaluate(panel.PreviewHour, panel.HdrActive);
                timecycle.GetLightDirection(panel.PreviewHour, out var sunW, out _);
                check("light workspace sun direction equals the world's at the same hour",
                      (glS.LightDir - glW.LightDir).Length() < 1e-5f && (sunS - sunW).Length() < 1e-5f && (glS.LightDir - Vector3.Normalize(sunS)).Length() < 1e-4f,
                      $"lights {glS.LightDir} world {glW.LightDir} @ {hourS}h");
                check("light workspace sun colour equals the world's at the same hour",
                      (glS.LightDirColour - glW.LightDirColour).Length() < 1e-5f && glS.LightDirColour.LengthSquared() > 0.001f,
                      $"lights {glS.LightDirColour} world {glW.LightDirColour}");
                panel.SwitchWorkspace(was);
                panel.PreviewHour = savedHour;
                timecycle.SelectedModifier = savedMod; timecycle.ModifierStrength = savedStr;
            }
        }

        private string SunDebugLine()
        {
            if (timecycle == null) return "  sun: no cycle";
            timecycle.GetLightDirection(panel.PreviewHour, out var sd, out var md);
            var gl = sceneRenderer.GlobalLight;
            var cw = CodeWalkerLightDir(panel.PreviewHour, timecycle.SunRoll, timecycle.MoonRoll);
            string lit = gl.HasValue ? $"{gl.Value.LightDir}" : "none";
            bool moon = panel.PreviewHour < 5.0f || panel.PreviewHour > 21.0f;
            float alt = (float)(Math.Asin(Math.Clamp(sd.Z, -1, 1)) * 180.0 / Math.PI);
            float az = (float)(Math.Atan2(sd.X, sd.Y) * 180.0 / Math.PI);
            string clouds = lastCloudKf.Valid
                ? $"{lastCloudKf.SettingsName} col={lastCloudKf.CloudColor} light={lastCloudKf.LightColor} amb={lastCloudKf.AmbientColor} sdfa={lastCloudKf.ScaleDiffuseFillAmbient_WrapAmount}"
                : "flat";
            return $"  sun: dir={sd} (alt {alt:0.0} deg, az {az:0.0} deg from +Y) moon={md} lightDir={lit} ({(moon ? "moon" : "sun")}) " +
                   $"cw={cw} match={(gl.HasValue && (gl.Value.LightDir - cw).Length() < 1e-4f)} " +
                   $"dirCol={(gl.HasValue ? gl.Value.LightDirColour.ToString() : "-")} clouds={clouds}";
        }

        partial void RunWorldTestExtras_Sky(Action<string, bool, string> check, Action<Vector3> settle)
        {
            check("the cycle carries the game's fog block", timecycle != null && timecycle.Has("fog_density"),
                  timecycle == null ? "no cycle" : $"fog_density={timecycle.Peek("fog_density"):0.0} fog_falloff={timecycle.Peek("fog_falloff"):0.0}");
            if (timecycle != null && timecycle.HasData)
            {
                var sk = timecycle.EvaluateSky(14.0f);
                timecycle.GetLightDirection(14.0f, out var sd, out var md);
                var fg = GameFogVars.Build(sk, 60.0f, Vector3.Normalize(sd), Vector3.Normalize(md), true, 1.0f, true);
                check("the game fog constants pack like TimeCycle.cpp",
                      fg.Params0.W > 0.5f && fg.Params1.W < 0.0f && fg.Params1.X <= 0.0f && fg.Params0.Y > fg.Params0.X && fg.ColGround.X >= 0,
                      $"groundAtViewer={-fg.Params1.W:0.000000} haze={-fg.Params1.X:0.000000} far={fg.Params0.Y:0} colGround={fg.ColGround}");
                check("the sky is on the HDR scale with the CodeWalker zenith mapping",
                      timecycle.CodeWalkerSkyMapping && sk.HdrIntensity > 1.0f && sk.ZenithBlendStart > 0.5f,
                      $"sky_hdr={sk.HdrIntensity:0.0} zbs(remapped, = 1 - ztp)={sk.ZenithBlendStart:0.000}");
            }
            var cs = new SunCascades();
            cs.Fit(camera, Vector3.Normalize(new Vector3(0.3f, 0.4f, 0.8f)), SunCascades.DefaultIntervals, 4, ShadowRenderer.SunSize,
                   camera.Position - new Vector3(1000, 1000, 500), camera.Position + new Vector3(1000, 1000, 500));
            check("sun cascades fit the camera frustum", cs.Count == 4 && cs.SplitFar[3] == 160.0f && cs.TexelWorld[0] > 0 && cs.TexelWorld[3] > cs.TexelWorld[0],
                  $"splits=({string.Join(",", cs.SplitFar)}) texel=({string.Join(",", Array.ConvertAll(cs.TexelWorld, x => x.ToString("0.000")))})");
            var hats = new CloudHats();
            bool loaded = gameFiles != null && gameFiles.Ready && hats.Load(gameFiles);
            check("clouds.xml reads through the archives", loaded && hats.FragNames().Length > 0,
                  loaded ? $"{hats.FragNames().Length} hats, contrails={Array.Exists(hats.FragNames(), n => n.Equals("contrails", StringComparison.OrdinalIgnoreCase))}" : hats.Error);
            if (loaded)
            {
                string sn = hats.SettingsNameFor("w_clear");
                var k12 = hats.Evaluate(sn, 12.0f);
                var k22 = hats.Evaluate(sn, 22.0f);
                var k2 = hats.Evaluate(sn, 2.0f);
                float L(Vector3 v) => v.X * 0.299f + v.Y * 0.587f + v.Z * 0.114f;
                check("cloudkeyframes: w_clear -> LIGHTclouds through weather.xml", sn == "LIGHTclouds" && k12.Valid, $"{sn ?? "null"} valid={k12.Valid}");
                check("cloudkeyframes: night clouds are dark (CloudColor 22h/2h a fraction of noon)",
                      k12.Valid && L(k22.CloudColor) < L(k12.CloudColor) * 0.1f && L(k2.CloudColor) < L(k12.CloudColor) * 0.1f
                      && k22.ScaleDiffuseFillAmbient_WrapAmount.X < k12.ScaleDiffuseFillAmbient_WrapAmount.X * 0.2f,
                      $"12h col={k12.CloudColor} sdfa={k12.ScaleDiffuseFillAmbient_WrapAmount} 22h col={k22.CloudColor} sdfa={k22.ScaleDiffuseFillAmbient_WrapAmount} 2h col={k2.CloudColor}");
                check("cloudkeyframes: hat picked for the clear weather", hats.PickFrag(sn) != null, hats.PickFrag(sn) ?? "null");
            }
            SunDirectionTest_Sky(check);
            Console.WriteLine($"  TONE selected: {(panel.ToneMapCodeWalker ? "RAGE (auto exposure + Reinhard)" : "Game filmic")}, auto exposure {panel.AutoExposure}");
        }

        private CloudRenderer cloudRenderer;
        private bool CloudHatsActive => panel.CloudsEnabled && cloudRenderer != null && cloudRenderer.Ready;
        private string CloudStatus => cloudRenderer == null ? "none" : cloudRenderer.Status;

        private static readonly bool EnvCloudsOff = Environment.GetEnvironmentVariable("RLE_CLOUDS") == "0";
        private static readonly string EnvCloudFrag_U25 = Environment.GetEnvironmentVariable("RLE_CLOUDFRAG");

        private CloudLighting BuildCloudLighting()
        {
            var gl = sceneRenderer.GlobalLight;
            var lit = new CloudLighting
            {
                Keyframed = false,
                SunDir = gl.HasValue ? gl.Value.LightDir : Vector3.UnitZ,
                HdrScale = 1.0f,
            };
            if (!panel.CloudsGameLighting || cloudRenderer == null || !cloudRenderer.Ready) return lit;
            bool tcOn = panel.TimecycleEnabled && timecycle != null && timecycle.HasData;
            if (!tcOn) return lit;
            string settings = cloudRenderer.SettingsNameFor(timecycle.Name);
            if (settings == null && weather?.CurrentPreset?.Cycles != null)
                foreach (var c in weather.CurrentPreset.Cycles) { settings = cloudRenderer.SettingsNameFor(c); if (settings != null) break; }
            var kf = cloudRenderer.EvaluateKeyframes(settings ?? "default", panel.PreviewHour);
            if (!kf.Valid) return lit;
            float skyHdr = Math.Max(skyState.HdrIntensity, 0.001f);
            float sent = panel.HdrActive ? skyHdr : Math.Min(skyHdr, 1.8f);
            lit.HdrScale = (sent / skyHdr) * panel.SkyExposure;
            lit.Keyframed = true;
            lit.Kf = kf;
            lastCloudKf = kf;
            return lit;
        }
        private CloudKeyframeState lastCloudKf;

        partial void OnAfterSkyDraw_Sky(DeviceContext context, double now)
        {
            if (!panel.CloudsEnabled || EnvCloudsOff || gameFiles == null || !gameFiles.Ready) return;
            try
            {
                if (cloudRenderer == null)
                {
                    cloudRenderer = new CloudRenderer(deviceResources.Device);
                    cloudRenderer.Init(gameFiles, modelRenderer, textureLoader);
                    panel.CloudFragNames = cloudRenderer.FragNames;
                }
                if (!cloudRenderer.Ready) return;
                var gl = sceneRenderer.GlobalLight;
                var sunCol = gl.HasValue ? new Vector3(gl.Value.LightDirColour.X, gl.Value.LightDirColour.Y, gl.Value.LightDirColour.Z)
                                         : new Vector3(1, 1, 1);
                string frag = panel.CloudFrag;
                if (panel.CloudsFollowWeather) frag = cloudRenderer.FragForWeather(weather.CurrentPreset.Cycles) ?? frag;
                frag = EnvCloudFrag_U25 ?? frag;
                cloudRenderer.Render(context, SkyViewProj(), camera.Position, sunCol, (float)now * panel.CloudSpeed, frag,
                                     sceneRenderer.GameFog, BuildCloudLighting());
            }
            catch (Exception ex)
            {
                Console.WriteLine("CLOUDS FAILED: " + ex.Message);
                panel.CloudsEnabled = false;
            }
        }
    }
}

