using System;
using System.Linq;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool sectionCamsDirty_T2;
        private bool sectionCamsLoaded_T2;

        private bool SectionCamsScripted_T2 =>
            !sectionCamsForced_T2 &&
            (screenshotPath != null || DebugSeqTest || DebugWorldTest || DebugArchiveTest || DebugVideoTest ||
             DebugCam != null || DebugWorldSpace != null || DebugMlo != null || DebugCine != null ||
             DebugHourSweep > 0 || DebugModeSweep > 0 || tabMatrixSpec_T2 != null);

        private static readonly bool sectionCamsForced_T2 = Environment.GetEnvironmentVariable("RLE_T2CAMPERSIST") == "1";

        partial void WireSectionCameras_T2()
        {
            if (panel == null || settings == null) return;
            LoadSectionCameras_T2();
            panel.WorkspaceSwitching += (leaving, entering) => sectionCamsDirty_T2 = true;
            FormClosing += (s, e) => { sectionCamsDirty_T2 = true; FlushSectionCameras_T2(); };
        }

        private void LoadSectionCameras_T2()
        {
            var prefs = settings.SectionCameras;
            int n = 0;
            foreach (var p in prefs ?? new System.Collections.Generic.List<SectionCameraPref>())
            {
                if (p == null || !SpaceNames.TryParse(p.Section, out var sp)) continue;
                if ((int)sp < 0 || (int)sp >= workspaceCam.Length) continue;
                if (!(p.Distance > 0.0f) || !float.IsFinite(p.TargetX)) continue;
                workspaceCam[(int)sp] = new Rendering.Camera.Snapshot
                {
                    Valid = true,
                    Target = new Vector3(p.TargetX, p.TargetY, p.TargetZ),
                    Distance = p.Distance, Yaw = p.Yaw, Pitch = p.Pitch,
                    TargetDistance = p.TargetDistance, TargetYaw = p.TargetYaw, TargetPitch = p.TargetPitch,
                    FieldOfView = p.FieldOfView > 0.0f ? p.FieldOfView : settings.FovDeg * 0.0174533f,
                    NearClip = p.NearClip > 0.0f ? p.NearClip : 0.05f,
                    FarClip = p.FarClip > 0.0f ? p.FarClip : 3000.0f,
                    MaxDistance = p.MaxDistance > 0.0f ? p.MaxDistance : 1000.0f,
                };
                workspaceWalk[(int)sp] = p.Walk;
                n++;
            }
            sectionCamsLoaded_T2 = n > 0;
            var here = workspaceCam[(int)panel.Workspace];
            if (here.Valid && !SectionCamsScripted_T2 && camera != null)
            {
                camera.Restore(here);
                SetWalkMode(workspaceWalk[(int)panel.Workspace]);
                settings.FovDeg = MathUtil.Clamp(here.FieldOfView / 0.0174533f,
                                                 Rendering.Camera.MinFovDeg, Rendering.Camera.MaxFovDeg);
            }
            Console.WriteLine($"SHADERCACHE {Rendering.ShaderCache_U22.Hits} cached, {Rendering.ShaderCache_U22.Misses} compiled in {Rendering.ShaderCache_U22.CompileMs:0} ms");
            Console.WriteLine($"SECTIONCAMS {n} remembered placement(s) read; standing in {panel.Workspace}" +
                              (here.Valid ? $" at {camera?.Position}" : " (never been there)"));
        }

        private void FlushSectionCameras_T2()
        {
            if (!sectionCamsDirty_T2) return;
            sectionCamsDirty_T2 = false;
            if (settings == null || panel == null || camera == null) return;
            if (SectionCamsScripted_T2) return;

            var live = camera.Capture();
            var list = new System.Collections.Generic.List<SectionCameraPref>();
            foreach (LightPanel.Space sp in Enum.GetValues(typeof(LightPanel.Space)))
            {
                var s = sp == panel.Workspace ? live : workspaceCam[(int)sp];
                if (!s.Valid) continue;
                list.Add(new SectionCameraPref
                {
                    Section = SpaceNames.NameOf(sp),
                    TargetX = s.Target.X, TargetY = s.Target.Y, TargetZ = s.Target.Z,
                    Distance = s.Distance, Yaw = s.Yaw, Pitch = s.Pitch,
                    TargetDistance = s.TargetDistance, TargetYaw = s.TargetYaw, TargetPitch = s.TargetPitch,
                    FieldOfView = s.FieldOfView, NearClip = s.NearClip, FarClip = s.FarClip,
                    MaxDistance = s.MaxDistance,
                    Walk = sp == panel.Workspace ? walkMode : workspaceWalk[(int)sp],
                });
            }
            settings.SectionCameras = list;
            settings.Save();
        }

        private void SectionCameraPersistTest_T2(Action<string, bool, string> check)
        {
            var spaces = (LightPanel.Space[])Enum.GetValues(typeof(LightPanel.Space));
            var a = new AppSettings();
            a.SectionCameras = spaces.Select((sp, i) => new SectionCameraPref
            {
                Section = SpaceNames.NameOf(sp),
                TargetX = 10.0f + i, TargetY = -20.0f - i, TargetZ = 3.0f + i,
                Distance = 5.0f + i, Yaw = 0.1f * i, Pitch = 0.02f * i,
                TargetDistance = 5.0f + i, TargetYaw = 0.1f * i, TargetPitch = 0.02f * i,
                FieldOfView = 0.9f, NearClip = 0.05f, FarClip = 3000.0f, MaxDistance = 1000.0f,
                Walk = (i % 2) == 0,
            }).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(a);
            var back = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            check("t2: every section's placement survives the settings file",
                  back?.SectionCameras != null && back.SectionCameras.Count == spaces.Length &&
                  back.SectionCameras.All(p => spaces.Any(s => SpaceNames.NameOf(s) == p.Section)),
                  $"{back?.SectionCameras?.Count ?? 0} of {spaces.Length} | wrote {(json.Length > 160 ? json.Substring(0, 160) : json)}");
            var w = back?.SectionCameras?.FirstOrDefault(p => p.Section == SpaceNames.NameOf(LightPanel.Space.World));
            check("t2: ...with the numbers intact",
                  w != null && Math.Abs(w.TargetX - (10.0f + (int)LightPanel.Space.World)) < 0.001f,
                  w == null ? "no World entry" : $"{w.TargetX:0.###}");
            var stale = new AppSettings();
            stale.SectionCameras = new System.Collections.Generic.List<SectionCameraPref>
            { new SectionCameraPref { Section = "SomethingRemoved", Distance = 5 } };
            check("t2: an unknown section name in the file is ignored, not applied by position",
                  !Enum.TryParse<LightPanel.Space>(stale.SectionCameras[0].Section, out _), "ignored");
            check("t2: a fresh install has no remembered placements, so the World still opens on Maze Bank",
                  new AppSettings().SectionCameras.Count == 0, "empty");
        }
    }
}

