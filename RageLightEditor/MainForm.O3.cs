using System;
using System.Windows.Forms;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private Vector3 gotoFrom_O3, gotoTo_O3;
        private double gotoStart_O3 = -1, gotoDuration_O3;
        private float gotoYaw0_O3, gotoPitch0_O3, gotoDist0_O3;
        private const float GotoYaw_O3 = 1.4f, GotoPitch_O3 = 0.35f, GotoDistance_O3 = 4.0f;
        private readonly System.Diagnostics.Stopwatch gotoClock_O3 = System.Diagnostics.Stopwatch.StartNew();
        public Vector3 LastGoto_O3;

        private int lightClipKeyFrame_O3 = -1;
        private int frameCounter_O3;

        private bool KeyDown_O3(Keys combo)
        {
            if (panel == null) return false;
            const Keys cs = Keys.Control | Keys.Shift;
            if (combo == (cs | Keys.C))
            {
                panel.RequestCopyLightSettings_O3 = true;
                lightClipKeyFrame_O3 = frameCounter_O3;
                return true;
            }
            if (combo == (cs | Keys.V))
            {
                panel.RequestPasteLightSettings_O3 = true;
                lightClipKeyFrame_O3 = frameCounter_O3;
                return true;
            }
            if (combo == (Keys.Control | Keys.G))
            {
                panel.GotoFocus_O3 = true;
                if (panel.WorldMode) panel.SelectRightTab_U22("Options");
                return true;
            }
            return false;
        }

        private void Tick_O3()
        {
            if (panel == null || camera == null) return;
            frameCounter_O3++;
            panel.CameraCoords_O3 = new System.Numerics.Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z);

            if (lightClipKeyFrame_O3 >= 0 && frameCounter_O3 > lightClipKeyFrame_O3 + 1)
            {
                lightClipKeyFrame_O3 = -1;
                panel.RequestCopyLightSettings_O3 = false;
                panel.RequestPasteLightSettings_O3 = false;
            }

            if (panel.RequestGoto_O3.HasValue)
            {
                var v = panel.RequestGoto_O3.Value;
                bool instant = panel.RequestGotoInstant_O3;
                panel.RequestGoto_O3 = null;
                panel.RequestGotoInstant_O3 = false;
                StartGoto_O3(new Vector3(v.X, v.Y, v.Z), instant);
            }
            AdvanceGoto_O3();
            ServiceGotoEnv_O3();
            ServiceLightClipEnv_O3();
        }

        private bool gotoEnvDone_O3, lightClipEnvDone_O3;

        private void ServiceGotoEnv_O3()
        {
            if (gotoEnvDone_O3) return;
            string g = Environment.GetEnvironmentVariable("RLE_GOTO");
            if (string.IsNullOrEmpty(g)) { gotoEnvDone_O3 = true; return; }
            if (panel.WorldMode && !worldBuilt) return;
            gotoEnvDone_O3 = true;
            bool jump = g.EndsWith("jump", StringComparison.OrdinalIgnoreCase);
            panel.GotoText_O3 = g;
            if (!LightPanel.ParseCoords_O3(g, out var p)) { Console.WriteLine("GOTOENV could not read '" + g + "'"); return; }
            Console.WriteLine($"GOTOENV read '{g}' as {LightPanel.FormatCoords_O3(p)} ({(jump ? "jump" : "fly")})");
            StartGoto_O3(new Vector3(p.X, p.Y, p.Z), jump);
        }

        private void ServiceLightClipEnv_O3()
        {
            if (lightClipEnvDone_O3) return;
            string c = Environment.GetEnvironmentVariable("RLE_LIGHTCOPY");
            string v = Environment.GetEnvironmentVariable("RLE_LIGHTPASTE");
            if (string.IsNullOrEmpty(c) && string.IsNullOrEmpty(v)) { lightClipEnvDone_O3 = true; return; }
            var sc = panel.ActiveScene;
            if (sc == null || sc.Lights.Count < 2) return;
            lightClipEnvDone_O3 = true;
            if (!string.IsNullOrEmpty(c) && int.TryParse(c, out int ci) && ci >= 0 && ci < sc.Lights.Count)
            {
                sc.SelectedIndex = ci;
                panel.CopyLightSettings_O3(sc.Lights[ci]);
                Console.WriteLine($"LIGHTCLIP copied light {ci}: {LightSettings_O3.Describe()}");
            }
            if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int vi) && vi >= 0 && vi < sc.Lights.Count)
            {
                var before = Scene.CloneLight(sc.Lights[vi]);
                sc.SelectedIndex = vi;
                panel.PasteLightSettings_O3(sc.Lights[vi]);
                var after = sc.Lights[vi];
                Console.WriteLine($"LIGHTCLIP pasted onto light {vi}: intensity {before.Intensity:0.##} -> {after.Intensity:0.##}, " +
                                  $"colour {before.ColorR},{before.ColorG},{before.ColorB} -> {after.ColorR},{after.ColorG},{after.ColorB}, " +
                                  $"type {before.Type} -> {after.Type}; position kept={(before.Position - after.Position).Length() < 1e-6f}, " +
                                  $"direction kept={(before.Direction - after.Direction).Length() < 1e-6f}; status: {panel.LightSettingsStatus_O3}");
            }
        }

        public void StartGoto_O3(Vector3 to, bool instant)
        {
            LastGoto_O3 = to;
            NoteGotoSection_T2();
            gotoFrom_O3 = camera.Target;
            gotoTo_O3 = to;
            gotoYaw0_O3 = camera.Yaw; gotoPitch0_O3 = camera.Pitch; gotoDist0_O3 = camera.Distance;
            float dist = (to - camera.Target).Length();
            if (instant || dist < 1.0f)
            {
                gotoStart_O3 = -1;
                LandGoto_O3(to);
                ReportGotoArrival_O3(to, dist, true);
                return;
            }
            gotoDuration_O3 = MathUtil.Clamp(0.6f + dist / 3000.0f * 1.9f, 0.6f, 2.5f);
            gotoStart_O3 = gotoClock_O3.Elapsed.TotalSeconds;
            panel.GotoStatus_O3 = $"Flying {dist:0} m to {LightPanel.FormatCoords_O3(new System.Numerics.Vector3(to.X, to.Y, to.Z))}...";
        }

        private void AdvanceGoto_O3()
        {
            if (gotoStart_O3 < 0) return;
            if (GotoLeftItsSection_T2()) return;
            double t = (gotoClock_O3.Elapsed.TotalSeconds - gotoStart_O3) / Math.Max(gotoDuration_O3, 0.001);
            bool done = t >= 1.0;
            float s = MathUtil.Clamp((float)t, 0.0f, 1.0f);
            s = s * s * (3.0f - 2.0f * s);
            camera.Target = Vector3.Lerp(gotoFrom_O3, gotoTo_O3, s);
            camera.Yaw = MathUtil.Lerp(gotoYaw0_O3, GotoYaw_O3, s);
            camera.Pitch = MathUtil.Lerp(gotoPitch0_O3, GotoPitch_O3, s);
            camera.Distance = MathUtil.Lerp(gotoDist0_O3, GotoDistance_O3, s);
            camera.SnapSmoothing();
            camera.Update();
            if (!done) return;
            gotoStart_O3 = -1;
            LandGoto_O3(gotoTo_O3);
            ReportGotoArrival_O3(gotoTo_O3, (gotoTo_O3 - gotoFrom_O3).Length(), false);
        }

        private void LandGoto_O3(Vector3 to)
        {
            camera.Target = to;
            camera.Yaw = GotoYaw_O3;
            camera.Pitch = GotoPitch_O3;
            camera.Distance = GotoDistance_O3;
            camera.FieldOfView = MathUtil.DegreesToRadians(MathUtil.Clamp(settings.FovDeg, Rendering.Camera.MinFovDeg, Rendering.Camera.MaxFovDeg));
            camera.SnapSmoothing();
            camera.Update();
        }

        private void ReportGotoArrival_O3(Vector3 to, float travelled, bool jumped)
        {
            string where = LightPanel.FormatCoords_O3(new System.Numerics.Vector3(to.X, to.Y, to.Z));
            string s = (jumped ? "Jumped to " : "Flew to ") + where + $"  ({travelled:0} m)";
            if (panel.WorldMode)
            {
                s += $" - {World.YmapsWanted:N0} ymaps wanted here, {World.YmapsResident:N0} resident.";
            }
            panel.GotoStatus_O3 = s;
            Console.WriteLine("GOTO " + s);
        }

        partial void SeqTest_O3(Action<string, bool, string> check)
        {
            GoToParseTest_O3(check);
            LightSettingsTest_O3(check);
            MloEntityGeomTest_O3(check);
            AssetViewTest_O3(check);
        }

        private void GoToParseTest_O3(Action<string, bool, string> check)
        {
            var want = new System.Numerics.Vector3(1234.5f, -678.9f, 30.125f);
            string[] shapes =
            {
                "1234.5, -678.9, 30.125",
                "1234.5 -678.9 30.125",
                "[1234.5, -678.9, 30.125]",
                "(1234.5,-678.9,30.125)",
                "vector3(1234.5, -678.9, 30.125)",
                "vec3(1234.5,-678.9,30.125)",
                "X:1234.5 Y:-678.9 Z:30.125",
                "  1234.5;-678.9;30.125  ",
                "pos = 1234.5 / -678.9 / 30.125",
            };
            int ok = 0; string bad = "";
            foreach (var s in shapes)
            {
                if (LightPanel.ParseCoords_O3(s, out var p) && (p - want).Length() < 0.002f) ok++;
                else bad += " | " + s;
            }
            check("goto: every pasted shape reads as the same point", ok == shapes.Length, $"{ok} of {shapes.Length}{bad}");
            check("goto: extra numbers after z are ignored",
                  LightPanel.ParseCoords_O3("1234.5, -678.9, 30.125, 90, 500", out var q) && (q - want).Length() < 0.002f, q.ToString());
            check("goto: text with fewer than three numbers is refused",
                  !LightPanel.ParseCoords_O3("go to the bank", out _) && !LightPanel.ParseCoords_O3("12, 34", out _), "");
            check("goto: our own format round-trips",
                  LightPanel.ParseCoords_O3(LightPanel.FormatCoords_O3(want), out var r) && (r - want).Length() < 0.002f, r.ToString());

            var before = camera.Target;
            StartGoto_O3(new Vector3(100, 200, 300), true);
            check("goto: a jump puts the camera at the point", (camera.Target - new Vector3(100, 200, 300)).Length() < 0.01f, camera.Target.ToString());
            StartGoto_O3(new Vector3(1100, 200, 300), false);
            bool flying = gotoStart_O3 >= 0;
            gotoStart_O3 = gotoClock_O3.Elapsed.TotalSeconds - gotoDuration_O3 - 1.0;
            AdvanceGoto_O3();
            check("goto: a fly arrives at the point", flying && (camera.Target - new Vector3(1100, 200, 300)).Length() < 0.01f,
                  $"flying {flying}, {camera.Target}");
            camera.Target = before; camera.SnapSmoothing(); camera.Update();
        }

        private void AssetViewTest_O3(Action<string, bool, string> check)
        {
            var ui = Creator;
            if (ui == null) { check("assetview: the creator exists", false, "none"); return; }
            int view0 = ui.AssetView_O3, cols0 = ui.AssetColumns_O3;

            check("assetview: the default is four tiles a line", cols0 >= 2 && cols0 <= 8 && ui.AssetColumns_O3 == cols0, cols0.ToString());
            ui.AssetView_O3 = 1;
            ui.AssetColumns_O3 = 4;
            float avail = 640, spacing = 8;
            float cell = (avail - spacing * 3) / 4;
            float img = Math.Min(cell - 8, 160.0f);
            check("assetview: four to a line gives a big picture, not a row thumbnail", img >= 128 && img <= 160, $"{img:0} px tiles from a {avail:0} px page");
            float cellC = (380 - spacing * 3) / 4;
            check("assetview: the narrow panel still fits four a line", cellC > 48, $"{cellC:0} px cells");
            check("assetview: thumbnails are rendered at the tile's size", PropThumbnails.Size >= 128, PropThumbnails.Size.ToString());
            double mb = propThumbnails != null ? propThumbnails.MaxCached * (double)PropThumbnails.Size * PropThumbnails.Size * 4 / (1024 * 1024) : 0;
            check("assetview: the thumbnail cache is still bounded", propThumbnails == null || (mb > 0 && mb < 48), $"{mb:0.#} MB ceiling");

            int n = 20000, colsN = 4;
            float cellH = img + 8 + 16 + 4, stride = cellH + spacing, viewH = 400;
            int rows = (n + colsN - 1) / colsN;
            int firstRow = Math.Max(0, (int)(0 / stride) - 1);
            int lastRow = Math.Min(rows - 1, (int)((0 + viewH) / stride) + 1);
            int drawn = (lastRow - firstRow + 1) * colsN;
            check("assetview: only what is on screen is drawn (and asks for a thumbnail)", drawn < 40 && rows == 5000, $"{drawn} tiles of {n}, {rows} rows");

            ui.AssetView_O3 = view0; ui.AssetColumns_O3 = cols0;
        }

        private bool assetViewEnvDone_O3;
        private void ApplyAssetViewEnv_O3(MloCreatorPanel ui)
        {
            if (assetViewEnvDone_O3) return;
            string v = Environment.GetEnvironmentVariable("RLE_MLOVIEW");
            string c = Environment.GetEnvironmentVariable("RLE_MLOCOLS");
            if (string.IsNullOrEmpty(v) && string.IsNullOrEmpty(c)) return;
            assetViewEnvDone_O3 = true;
            if (!string.IsNullOrEmpty(v)) ui.AssetView_O3 = v.StartsWith("g", StringComparison.OrdinalIgnoreCase) || v == "1" ? 1 : 0;
            if (!string.IsNullOrEmpty(c) && int.TryParse(c, out int cols)) ui.AssetColumns_O3 = Math.Clamp(cols, 2, 8);
            Console.WriteLine($"MLOVIEW assets library view={LightPanel.SafeIndex_O3(MloCreatorPanel.AssetViewNames_O3, ui.AssetView_O3)} columns={ui.AssetColumns_O3} thumbSize={PropThumbnails.Size}");
        }

        private void LightSettingsTest_O3(Action<string, bool, string> check)
        {
            var a = new CodeWalker.GameFiles.LightAttributes
            {
                Position = new Vector3(1, 2, 3),
                Direction = new Vector3(0, 0, -1),
                Tangent = new Vector3(1, 0, 0),
                BoneId = 17,
                ColorR = 10, ColorG = 20, ColorB = 30,
                Intensity = 4.5f, Falloff = 9.0f, FalloffExponent = 3.0f,
                Type = CodeWalker.GameFiles.LightType.Spot,
                ConeInnerAngle = 12, ConeOuterAngle = 44,
                Flags = 0x1234, TimeFlags = 0xF0007F, Flashiness = 5,
                CoronaSize = 1.5f, CoronaIntensity = 2.5f, CoronaZBias = 0.25f,
                VolumeIntensity = 0.75f, VolumeSizeScale = 1.25f,
                ShadowBlur = 3, ShadowNearClip = 0.5f,
                CullingPlaneNormal = new Vector3(0, 1, 0), CullingPlaneOffset = 2.0f,
                ProjectedTextureHash = 0xABCD,
                Extent = new Vector3(0.5f, 0.25f, 0.75f),
            };
            var b = new CodeWalker.GameFiles.LightAttributes
            {
                Position = new Vector3(9, 8, 7),
                Direction = new Vector3(1, 0, 0),
                Tangent = new Vector3(0, 1, 0),
                BoneId = 42,
                ColorR = 200, ColorG = 100, ColorB = 50,
                Intensity = 1.0f, Falloff = 2.0f,
                Type = CodeWalker.GameFiles.LightType.Point,
            };
            LightSettings_O3.Copy(a, "test light");
            var diffs = LightSettings_O3.Differences(b);
            check("light clipboard: the differences are named before the paste",
                  diffs.Contains("colour") && diffs.Contains("intensity") && diffs.Contains("type") && diffs.Contains("corona"),
                  string.Join(",", diffs));
            LightSettings_O3.Apply(b);
            check("light clipboard: every parameter crossed over",
                  b.ColorR == 10 && b.ColorG == 20 && b.ColorB == 30 &&
                  Math.Abs(b.Intensity - 4.5f) < 1e-6f && Math.Abs(b.Falloff - 9.0f) < 1e-6f &&
                  Math.Abs(b.FalloffExponent - 3.0f) < 1e-6f &&
                  b.Type == CodeWalker.GameFiles.LightType.Spot &&
                  Math.Abs(b.ConeOuterAngle - 44) < 1e-6f && b.Flags == 0x1234 && b.TimeFlags == 0xF0007F &&
                  b.Flashiness == 5 && Math.Abs(b.CoronaSize - 1.5f) < 1e-6f && Math.Abs(b.VolumeIntensity - 0.75f) < 1e-6f &&
                  b.ShadowBlur == 3 && Math.Abs(b.CullingPlaneOffset - 2.0f) < 1e-6f && b.ProjectedTextureHash == 0xABCD &&
                  (b.Extent - a.Extent).Length() < 1e-6f,
                  $"colour {b.ColorR},{b.ColorG},{b.ColorB} intensity {b.Intensity} type {b.Type} flags 0x{b.Flags:X}");
            check("light clipboard: where it is and what it is on are untouched",
                  (b.Position - new Vector3(9, 8, 7)).Length() < 1e-6f &&
                  (b.Direction - new Vector3(1, 0, 0)).Length() < 1e-6f &&
                  (b.Tangent - new Vector3(0, 1, 0)).Length() < 1e-6f && b.BoneId == 42,
                  $"{b.Position} dir {b.Direction} bone {b.BoneId}");
            check("light clipboard: nothing is left to paste afterwards", LightSettings_O3.Differences(b).Count == 0,
                  string.Join(",", LightSettings_O3.Differences(b)));

            var sc = panel.ActiveScene ?? lightScene;
            LoadedFile made = null;
            if (sc != null && sc.Lights.Count < 2)
            {
                made = sc.AddLightProxy("o3_clip_test");
                sc.AddLight(1); sc.AddLight(1);
            }
            if (sc != null && sc.Lights.Count >= 2)
            {
                var l0 = sc.Lights[0]; var l1 = sc.Lights[1];
                var p1 = l1.Position;
                l0.Intensity = 12.25f; l0.ColorR = 3;
                sc.SelectedIndex = 0;
                panel.CopyLightSettings_O3(l0);
                sc.SelectedIndex = 1;
                panel.PasteLightSettings_O3(l1);
                check("light clipboard: the panel's paste reaches the scene light",
                      Math.Abs(l1.Intensity - 12.25f) < 1e-4f && l1.ColorR == 3 && (l1.Position - p1).Length() < 1e-6f,
                      $"intensity {l1.Intensity}, colour {l1.ColorR}, moved {(l1.Position - p1).Length():0.###}");
                sc.Undo();
                check("light clipboard: one Ctrl+Z puts it back", Math.Abs(sc.Lights[1].Intensity - 12.25f) > 1e-4f || sc.Lights[1].ColorR != 3,
                      $"intensity {sc.Lights[1].Intensity}, colour {sc.Lights[1].ColorR}");

                if (sc.Lights.Count >= 2)
                {
                    var a2 = sc.Lights[0];
                    a2.Intensity = 7.75f; a2.ColorG = 111;
                    sc.SelectedIndex = 0;
                    panel.CopyLightSettings_O3(a2);
                    sc.SelectedIndices.Clear();
                    for (int i = 1; i < sc.Lights.Count; i++) sc.SelectedIndices.Add(i);
                    panel.PasteLightSettings_O3(sc.Lights[1]);
                    bool all = true;
                    for (int i = 1; i < sc.Lights.Count; i++)
                        if (Math.Abs(sc.Lights[i].Intensity - 7.75f) > 1e-4f || sc.Lights[i].ColorG != 111) all = false;
                    check("light clipboard: a paste reaches the whole multi-selection", all, $"{sc.SelectedIndices.Count} selected");
                    sc.SelectedIndices.Clear();
                }
            }
            if (made != null) sc.RemoveFile(made);
        }
    }
}

