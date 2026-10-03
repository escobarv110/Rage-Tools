using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ImGuiNET;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {

        private struct View_T2
        {
            public Rendering.Camera.Snapshot Cam;
            public float FovDeg;
            public bool Walk;

            public bool SameAs(in View_T2 o) =>
                Cam.SameAs(o.Cam) && Math.Abs(FovDeg - o.FovDeg) < 0.0005f && Walk == o.Walk;
        }

        private View_T2 CaptureView_T2() => new View_T2
        {
            Cam = camera.Capture(),
            FovDeg = settings.FovDeg,
            Walk = walkMode,
        };

        private static string Diff_T2(in View_T2 want, in View_T2 got)
        {
            var sb = new StringBuilder();
            void F(string n, float a, float b) { if (a != b) sb.Append($" {n} {a:0.####}->{b:0.####}"); }
            var A = want.Cam; var B = got.Cam;
            if (A.Target != B.Target) sb.Append($" target {A.Target}->{B.Target} (moved {(B.Target - A.Target).Length():0.###} m)");
            F("dist", A.Distance, B.Distance);
            F("yaw", A.Yaw, B.Yaw);
            F("pitch", A.Pitch, B.Pitch);
            F("tdist", A.TargetDistance, B.TargetDistance);
            F("tyaw", A.TargetYaw, B.TargetYaw);
            F("tpitch", A.TargetPitch, B.TargetPitch);
            F("fovRad", A.FieldOfView, B.FieldOfView);
            F("near", A.NearClip, B.NearClip);
            F("far", A.FarClip, B.FarClip);
            F("maxdist", A.MaxDistance, B.MaxDistance);
            F("settings.Fov", want.FovDeg, got.FovDeg);
            if (want.Walk != got.Walk) sb.Append($" walk {want.Walk}->{got.Walk}");
            return sb.Length == 0 ? " (nothing)" : sb.ToString();
        }

        private LightPanel.Space gotoSpace_T2;
        private bool gotoSpaceSet_T2;

        partial void NoteGotoSection_T2();
        partial void NoteGotoSection_T2()
        {
            gotoSpace_T2 = panel?.Workspace ?? LightPanel.Space.World;
            gotoSpaceSet_T2 = true;
        }

        private bool GotoLeftItsSection_T2()
        {
            if (!gotoSpaceSet_T2 || panel == null || camera == null) return false;
            if (panel.Workspace == gotoSpace_T2) return false;
            if (SharesTheMap_Q4(gotoSpace_T2, panel.Workspace)) { gotoSpace_T2 = panel.Workspace; return false; }

            var live = camera.Capture();
            LandGoto_O3(gotoTo_O3);
            workspaceCam[(int)gotoSpace_T2] = camera.Capture();
            camera.Restore(live);
            gotoStart_O3 = -1;
            panel.GotoStatus_O3 = $"Landed in the {gotoSpace_T2} section - it was left mid-flight.";
            Console.WriteLine($"GOTO handed back: the flight belonged to {gotoSpace_T2}, landed there; {panel.Workspace} untouched");
            return true;
        }

        private static readonly string tabMatrixSpec_T2 = Environment.GetEnvironmentVariable("RLE_TABMATRIX");
        private int tmFrames_T2 = 4;
        private bool tmPairs_T2 = true, tmScen_T2 = true;
        private int tmWarmup_T2;
        private int tmPair_T2 = -1, tmStep_T2, tmWait_T2;
        private double tmWaitUntil_T2;
        private bool tmDone_T2, tmStarted_T2;
        private readonly List<(LightPanel.Space A, LightPanel.Space B)> tmList_T2 = new List<(LightPanel.Space, LightPanel.Space)>();
        private View_T2 tmViewA_T2, tmViewB_T2;
        private int tmLeaks_T2, tmDrift_T2, tmMisses_T2, tmChecked_T2;
        private readonly List<string> tmReport_T2 = new List<string>();
        private static readonly bool tmTrace_T2 = Environment.GetEnvironmentVariable("RLE_TABTRACE") == "1";

        private void ClickAt_T2(System.Numerics.Vector2 p, bool down)
        {
            var io = ImGui.GetIO();
            io.AddMousePosEvent(p.X, p.Y);
            io.AddMouseButtonEvent(0, down);
            if (tmTrace_T2)
                Console.WriteLine($"TABTRACE queue pos={p} down={down} | io.MousePos={io.MousePos} io.MouseDown0={io.MouseDown[0]} " +
                                  $"wantMouse={io.WantCaptureMouse} anyHovered={ImGui.IsAnyItemHovered()} anyActive={ImGui.IsAnyItemActive()} " +
                                  $"ws={panel.Workspace} display={io.DisplaySize}");
        }

        private void StampView_T2(LightPanel.Space s)
        {
            int i = (int)s + 1;
            camera.Translate(new Vector3(37.0f * i, -23.0f * i, 6.0f * i));
            camera.Orbit(13.0f * i, -7.0f * i);
            camera.Pan(9.0f * i, 5.0f * i);
            camera.Zoom(120.0f * ((i % 3) - 1));
            settings.FovDeg = 30.0f + i * 6.0f;
            SetWalkMode((i % 2) == 0);
        }

        private static LightPanel.Space[] TabOrder_T2 => LightPanel.WorkspaceTabOrder_P1;

        partial void OnWorldTick_T2()
        {
            FlushSectionCameras_T2();

            if (tmDone_T2 || string.IsNullOrEmpty(tabMatrixSpec_T2) || panel == null || camera == null) return;

            if (!tmStarted_T2)
            {
                if (!TabOrder_T2.All(panel.TabDrawn_T2)) return;
                if (++tmWarmup_T2 < 20) return;
                var f = tabMatrixSpec_T2.Split(',');
                if (f.Length > 0 && int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
                    tmFrames_T2 = n;
                if (f.Length > 1)
                {
                    var m = f[1].Trim().ToLowerInvariant();
                    tmPairs_T2 = m != "scen";
                    tmScen_T2 = m != "pairs";
                }
                foreach (var a in TabOrder_T2)
                    foreach (var b in TabOrder_T2)
                        if (a != b) tmList_T2.Add((a, b));
                tmStarted_T2 = true;
                tmPair_T2 = tmPairs_T2 ? 0 : tmList_T2.Count;
                Console.WriteLine($"TABMATRIX start: {tmList_T2.Count} ordered pairs, {tmFrames_T2} frames per step, " +
                                  $"clicking the real tabs (World tab at {panel.TabCentre_T2(LightPanel.Space.World)})");
            }

            if (tmWaitUntil_T2 > 0.0)
            {
                if (clock.Elapsed.TotalSeconds < tmWaitUntil_T2) return;
                tmWaitUntil_T2 = 0.0;
            }
            if (tmWait_T2 > 0) { tmWait_T2--; return; }
            if (tmPair_T2 >= tmList_T2.Count) { TabMatrixScenarios_T2(); return; }

            var (A, B) = tmList_T2[tmPair_T2];
            switch (tmStep_T2)
            {
                case 0: ClickAt_T2(panel.TabCentre_T2(A), true); tmStep_T2++; break;
                case 1: ClickAt_T2(panel.TabCentre_T2(A), false); tmWait_T2 = tmFrames_T2; tmStep_T2++; break;
                case 2:
                    if (panel.Workspace != A)
                    {
                        tmMisses_T2++;
                        tmReport_T2.Add($"the {A} tab did not take a real click (still in {panel.Workspace})");
                        tmPair_T2++; tmStep_T2 = 0;
                        break;
                    }
                    StampView_T2(A);
                    tmWait_T2 = tmFrames_T2; tmStep_T2++;
                    break;
                case 3: tmViewA_T2 = CaptureView_T2(); tmWait_T2 = tmFrames_T2; tmStep_T2++; break;
                case 4:
                    {
                        var again = CaptureView_T2();
                        if (!again.SameAs(tmViewA_T2))
                        {
                            tmDrift_T2++;
                            if (tmReport_T2.Count < 40)
                                tmReport_T2.Add($"DRIFT {A} moved on its own over {tmFrames_T2} frames, nobody left it:{Diff_T2(tmViewA_T2, again)}");
                            tmViewA_T2 = again;
                        }
                    }
                    tmStep_T2++;
                    break;
                case 5: ClickAt_T2(panel.TabCentre_T2(B), true); tmStep_T2++; break;
                case 6: ClickAt_T2(panel.TabCentre_T2(B), false); tmWait_T2 = tmFrames_T2; tmStep_T2++; break;
                case 7:
                    if (panel.Workspace != B)
                    {
                        tmMisses_T2++;
                        tmReport_T2.Add($"the {B} tab did not take a real click (still in {panel.Workspace})");
                        tmPair_T2++; tmStep_T2 = 0;
                        break;
                    }
                    StampView_T2(B);
                    tmWait_T2 = tmFrames_T2; tmStep_T2++;
                    break;
                case 8: tmViewB_T2 = CaptureView_T2(); ClickAt_T2(panel.TabCentre_T2(A), true); tmStep_T2++; break;
                case 9: ClickAt_T2(panel.TabCentre_T2(A), false); tmWait_T2 = tmFrames_T2; tmStep_T2++; break;
                default:
                    {
                        tmChecked_T2++;
                        var now = CaptureView_T2();
                        bool shared = SharesTheMap_Q4(A, B);
                        var want = shared ? tmViewB_T2 : tmViewA_T2;
                        if (!now.SameAs(want))
                        {
                            tmLeaks_T2++;
                            if (tmReport_T2.Count < 40)
                                tmReport_T2.Add($"LEAK {A} after {B}{(shared ? " (shared pair - should carry B's view)" : "")}:{Diff_T2(want, now)}");
                        }
                        tmPair_T2++; tmStep_T2 = 0;
                        if ((tmPair_T2 % 12) == 0)
                            Console.WriteLine($"TABMATRIX {tmPair_T2}/{tmList_T2.Count} pairs, {tmLeaks_T2} leaks, {tmDrift_T2} drifts so far");
                    }
                    break;
            }
        }

        private int tmScenStep_T2, tmScenLeaks_T2;
        private View_T2 tmScenView_T2;

        private struct Scen_T2
        {
            public string Name;
            public LightPanel.Space Home, Away;
            public Action Arm;
            public int Settle;
            public double SettleSeconds;
            public Action PreLeave;
            public Action Disarm;
        }

        private readonly List<Scen_T2> tmScenList_T2 = new List<Scen_T2>();

        private void BuildScenarios_T2()
        {
            var W = LightPanel.Space.World; var L = LightPanel.Space.Light;
            tmScenList_T2.Add(new Scen_T2
            {
                Name = "a Go-to flight still in the air", Home = W, Away = L, Settle = 1,
                Arm = () => StartGoto_O3(new Vector3(-2359.2f, 2834.0f, 26.6f), false),
            });
            tmScenList_T2.Add(new Scen_T2
            {
                Name = "a Go-to flight that has landed", Home = W, Away = L, Settle = 60, SettleSeconds = 4.0,
                Arm = () => StartGoto_O3(new Vector3(1972.0f, 3815.0f, 33.0f), false),
            });
            tmScenList_T2.Add(new Scen_T2
            {
                Name = "Go to selection pressed", Home = W, Away = L, Settle = 1,
                Arm = () => GoToSelection(),
            });
            tmScenList_T2.Add(new Scen_T2
            {
                Name = "a shot playing", Home = LightPanel.Space.Cinematic, Away = L, Settle = 1,
                Arm = () =>
                {
                    var seq = panel.Sequence;
                    if (seq.Shots.Count == 0)
                    {
                        seq.Shots.Add(new CameraShot { Position = new Vector3(20, 30, 12), Yaw = 0.4f, Pitch = 0.1f, Fov = 45, Duration = 0, Hold = 1.0f, Name = "T2a" });
                        seq.Shots.Add(new CameraShot { Position = new Vector3(800, -400, 250), Yaw = 2.2f, Pitch = -0.2f, Fov = 70, Duration = 4.0f, Name = "T2b" });
                    }
                    panel.PlayTime = 0.0f; panel.Playing = true;
                },
                Disarm = () => { panel.Playing = false; },
            });
            tmScenList_T2.Add(new Scen_T2
            {
                Name = "photo mode entered and left", Home = W, Away = L, Settle = 4,
                Arm = () => SetPhotoMode(true),
                PreLeave = () => SetPhotoMode(false),
            });
            tmScenList_T2.Add(new Scen_T2
            {
                Name = "the project window open", Home = W, Away = L, Settle = 3,
                Arm = () => { if (ProjWin != null) ProjWin.Visible = true; },
            });
            tmScenList_T2.Add(new Scen_T2
            {
                Name = "the MLO Creator standing open", Home = LightPanel.Space.Mlo, Away = L, Settle = 3,
                Arm = () => { },
            });
            tmScenList_T2.Add(new Scen_T2
            {
                Name = "a settings save on the way out", Home = W, Away = L, Settle = 2,
                Arm = () => settings.Save(),
            });
            var late = Environment.GetEnvironmentVariable("RLE_T2YTYP");
            if (!string.IsNullOrWhiteSpace(late) && System.IO.File.Exists(late))
                tmScenList_T2.Add(new Scen_T2
                {
                    Name = "a dropped .ytyp landing late", Home = L, Away = W, Settle = 1, SettleSeconds = 0.0,
                    Arm = () => { pendingImports.Add(late); },

                });
        }

        private int tmScenPhase_T2;

        private void TabMatrixScenarios_T2()
        {
            if (!tmScen_T2) { FinishTabMatrix_T2(); return; }
            if (tmScenList_T2.Count == 0) BuildScenarios_T2();
            if (tmScenStep_T2 >= tmScenList_T2.Count) { FinishTabMatrix_T2(); return; }
            var sc = tmScenList_T2[tmScenStep_T2];
            switch (tmScenPhase_T2)
            {
                case 0: ClickAt_T2(panel.TabCentre_T2(sc.Away), true); tmScenPhase_T2++; break;
                case 1: ClickAt_T2(panel.TabCentre_T2(sc.Away), false); tmWait_T2 = tmFrames_T2; tmScenPhase_T2++; break;
                case 2: StampView_T2(sc.Away); tmWait_T2 = tmFrames_T2; tmScenPhase_T2++; break;
                case 3: tmScenView_T2 = CaptureView_T2(); ClickAt_T2(panel.TabCentre_T2(sc.Home), true); tmScenPhase_T2++; break;
                case 4: ClickAt_T2(panel.TabCentre_T2(sc.Home), false); tmWait_T2 = tmFrames_T2; tmScenPhase_T2++; break;
                case 5:
                    if (panel.Workspace != sc.Home) { tmMisses_T2++; tmReport_T2.Add($"scenario '{sc.Name}': could not reach {sc.Home}"); goto done; }
                    sc.Arm();
                    tmWait_T2 = Math.Max(sc.Settle, 1);
                    if (sc.SettleSeconds > 0) tmWaitUntil_T2 = clock.Elapsed.TotalSeconds + sc.SettleSeconds;
                    tmScenPhase_T2++;
                    break;
                case 6:
                    sc.PreLeave?.Invoke(); tmWait_T2 = tmFrames_T2; tmScenPhase_T2++; break;
                case 7: ClickAt_T2(panel.TabCentre_T2(sc.Away), true); tmScenPhase_T2++; break;
                case 8:
                    ClickAt_T2(panel.TabCentre_T2(sc.Away), false);
                    tmWait_T2 = 30;
                    tmWaitUntil_T2 = clock.Elapsed.TotalSeconds + 8.0;
                    tmScenPhase_T2++;
                    break;
                default:
                    {
                        tmChecked_T2++;
                        var now = CaptureView_T2();
                        if (panel.Workspace != sc.Away)
                        {
                            tmMisses_T2++;
                            tmReport_T2.Add($"scenario '{sc.Name}': ended in {panel.Workspace}, not {sc.Away}");
                        }
                        else if (!now.SameAs(tmScenView_T2))
                        {
                            tmScenLeaks_T2++;
                            tmReport_T2.Add($"SCENARIO LEAK {sc.Away} was dragged by {sc.Home} with {sc.Name}:{Diff_T2(tmScenView_T2, now)}");
                        }
                        else Console.WriteLine($"TABMATRIX scenario ok: {sc.Away} survives {sc.Home} with {sc.Name}");
                    }
                    done:
                    sc.Disarm?.Invoke();
                    tmScenStep_T2++; tmScenPhase_T2 = 0;
                    break;
            }
        }

        private void FinishTabMatrix_T2()
        {
            tmDone_T2 = true;
            Console.WriteLine($"TABMATRIX DONE: {tmChecked_T2} checks, {tmLeaks_T2} pair leaks, {tmScenLeaks_T2} scenario leaks, " +
                              $"{tmDrift_T2} self-drifts, {tmMisses_T2} tabs that ignored a click");
            foreach (var line in tmReport_T2) Console.WriteLine("TABMATRIX   " + line);
            Console.WriteLine(tmLeaks_T2 + tmScenLeaks_T2 + tmDrift_T2 + tmMisses_T2 == 0
                ? "TABMATRIX PASS - every section came back exactly as it was left"
                : "TABMATRIX FAIL - see the lines above");
            if (screenshotPath == null) BeginInvoke(new Action(Close));
        }

        private void TabMatrixTest_T2(Action<string, bool, string> check)
        {
            var spaces = (LightPanel.Space[])Enum.GetValues(typeof(LightPanel.Space));
            check("t2: every section in the enum has a tab to click",
                  spaces.All(s => LightPanel.WorkspaceTabOrder_P1.Contains(s)),
                  string.Join(" ", spaces.Where(s => !LightPanel.WorkspaceTabOrder_P1.Contains(s))) is var missing && missing.Length == 0
                      ? $"{spaces.Length} sections" : "no tab for " + missing);
            check("t2: ...and a camera slot behind it",
                  spaces.All(s => { try { var _ = WorkspaceCameraOf(s); return true; } catch { return false; } }),
                  $"{spaces.Length} slots");

            var was = panel.Workspace;
            float wasFov = settings.FovDeg;
            bool wasWalk = walkMode;
            var wasCam = camera.Capture();

            int leaks = 0; string worst = "";
            foreach (var a in spaces)
            {
                foreach (var b in spaces)
                {
                    if (a == b || SharesTheMap_Q4(a, b) || LightPanel.SharesView_U28(a, b)) continue;
                    panel.SwitchWorkspace(a);
                    StampView_T2(a); StepFrameWriters_T2();
                    var va = CaptureView_T2();
                    panel.SwitchWorkspace(b);
                    StampView_T2(b); StepFrameWriters_T2();
                    panel.SwitchWorkspace(a);
                    StepFrameWriters_T2();
                    var now = CaptureView_T2();
                    if (!now.SameAs(va))
                    {
                        leaks++;
                        if (worst.Length == 0) worst = $"{a} after {b}:{Diff_T2(va, now)}";
                    }
                }
            }
            check("t2: a section survives a trip through any other WITH FRAMES RUNNING",
                  leaks == 0, leaks == 0 ? "every pair" : $"{leaks} leaks, e.g. {worst}");

            panel.SwitchWorkspace(was);
            settings.FovDeg = wasFov;
            camera.Restore(wasCam);
            camera.FieldOfView = wasFov * 0.0174533f;
            SetWalkMode(wasWalk);
        }

        private void StepFrameWriters_T2()
        {
            camera.FieldOfView = settings.FovDeg * 0.0174533f;
            camera.Sensitivity = settings.CameraSensitivity;
            camera.Smoothness = settings.CameraSmoothing;
            camera.OrbitAnchorMax = settings.OrbitAnchorMax;
            if (panel.WorldMode)
            {
                camera.FarClip = Math.Max(camera.FarClip, 20000.0f);
                camera.MaxDistance = Math.Max(camera.MaxDistance, 20000.0f);
            }
            camera.Update(1.0f / 60.0f);
        }
    }
}

