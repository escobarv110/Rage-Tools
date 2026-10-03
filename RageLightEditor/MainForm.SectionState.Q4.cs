using System;
using System.Linq;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private static readonly int SpaceCount_Q4 = Enum.GetValues(typeof(LightPanel.Space)).Length;

        private readonly int[] sectionWorldGizmo_Q4 = Enumerable.Repeat(-1, SpaceCount_Q4).ToArray();
        private readonly int[] sectionLightGizmo_Q4 = Enumerable.Repeat(-1, SpaceCount_Q4).ToArray();

        public float SectionFovOf_Q4(LightPanel.Space s)
        {
            var cam = WorkspaceCameraOf(s);
            return cam.Valid ? cam.FieldOfView / 0.0174533f : 0.0f;
        }

        partial void WireSectionState_Q4()
        {
            if (panel == null) return;
            panel.WorkspaceSwitching += OnSectionSwitching_Q4;
        }

        private void OnSectionSwitching_Q4(LightPanel.Space leaving, LightPanel.Space entering)
        {
            if (worldGizmo != null) sectionWorldGizmo_Q4[(int)leaving] = (int)worldGizmo.Mode;
            if (gizmo != null) sectionLightGizmo_Q4[(int)leaving] = (int)gizmo.Mode;

            int wg = sectionWorldGizmo_Q4[(int)entering];
            if (wg >= 0 && worldGizmo != null) worldGizmo.Mode = (WorldGizmoMode)wg;
            int lg = sectionLightGizmo_Q4[(int)entering];
            if (lg >= 0 && gizmo != null) gizmo.Mode = (GizmoMode)lg;

            if (SharesTheMap_Q4(leaving, entering)) return;

            if (panel != null && panel.Playing)
            {
                panel.Playing = false;
                panel.MloStatus = "Playback stopped - the camera is yours again";
            }

            var next = WorkspaceCameraOf(entering);
            if (!next.Valid || camera == null) return;
            settings.FovDeg = MathUtil.Clamp(camera.FieldOfView / 0.0174533f,
                                             Rendering.Camera.MinFovDeg, Rendering.Camera.MaxFovDeg);
        }

        private bool WorldHasNoCameraYet_Q4 => !WorkspaceCameraOf(LightPanel.Space.World).Valid;

        private bool FocusOrbitOwnsCamera_Q4 =>
            panel != null && (panel.Workspace == LightPanel.Space.Light ||
                              panel.Workspace == LightPanel.Space.Material ||
                              panel.Workspace == LightPanel.Space.Mlo);

        internal static bool SharesTheMap_Q4(LightPanel.Space a, LightPanel.Space b) =>
            (a == LightPanel.Space.Cinematic && b == LightPanel.Space.World) ||
            (a == LightPanel.Space.World && b == LightPanel.Space.Cinematic);

        private static readonly string roundTripSpec_Q4 = Environment.GetEnvironmentVariable("RLE_Q4ROUNDTRIP");
        private int roundTripTick_Q4;
        private bool roundTripDone_Q4;

        partial void OnWorldTick_Q4()
        {
            if (roundTripDone_Q4 || string.IsNullOrEmpty(roundTripSpec_Q4) || panel == null) return;
            if (!worldBuilt) return;
            if (!int.TryParse(roundTripSpec_Q4, out int at) || at < 1) at = 30;
            if (++roundTripTick_Q4 < at) { screenshotFrames = Math.Max(screenshotFrames, 2); return; }
            roundTripDone_Q4 = true;

            var before = camera.Capture();
            float fovBefore = settings.FovDeg;
            Console.WriteLine($"Q4ROUNDTRIP before  space={panel.Workspace} pos={camera.Position} target={before.Target} " +
                              $"dist={before.Distance:0.###} yaw={before.Yaw:0.#####} pitch={before.Pitch:0.#####} fov={fovBefore:0.##}");

            panel.SwitchWorkspace(LightPanel.Space.Material);
            camera.Target = new Vector3(3, 4, 5); camera.Distance = 2.0f; camera.Yaw = 0.5f; camera.Pitch = 0.1f;
            camera.SnapSmoothing(); camera.Update();
            settings.FovDeg = 30.0f;
            Console.WriteLine($"Q4ROUNDTRIP materials pos={camera.Position} fov={settings.FovDeg:0.##}");

            panel.SwitchWorkspace(LightPanel.Space.Light);
            camera.Target = new Vector3(-8, 1, 2); camera.Distance = 7.0f; camera.Yaw = 2.6f; camera.Pitch = -0.2f;
            camera.SnapSmoothing(); camera.Update();
            settings.FovDeg = 110.0f;
            Console.WriteLine($"Q4ROUNDTRIP lights    pos={camera.Position} fov={settings.FovDeg:0.##}");

            panel.SwitchWorkspace(LightPanel.Space.World);
            var after = camera.Capture();
            bool same = after.SameAs(before) && Math.Abs(settings.FovDeg - fovBefore) < 0.001f;
            Console.WriteLine($"Q4ROUNDTRIP after   space={panel.Workspace} pos={camera.Position} target={after.Target} " +
                              $"dist={after.Distance:0.###} yaw={after.Yaw:0.#####} pitch={after.Pitch:0.#####} fov={settings.FovDeg:0.##}");
            if (!same)
                Console.WriteLine($"Q4ROUNDTRIP diff target={before.Target != after.Target} dist={before.Distance != after.Distance} " +
                                  $"yaw={before.Yaw != after.Yaw} pitch={before.Pitch != after.Pitch} " +
                                  $"tdist={before.TargetDistance != after.TargetDistance}({before.TargetDistance:0.####}->{after.TargetDistance:0.####}) " +
                                  $"tyaw={before.TargetYaw != after.TargetYaw}({before.TargetYaw:0.####}->{after.TargetYaw:0.####}) " +
                                  $"tpitch={before.TargetPitch != after.TargetPitch}({before.TargetPitch:0.####}->{after.TargetPitch:0.####}) " +
                                  $"fovc={before.FieldOfView != after.FieldOfView}({before.FieldOfView:0.######}->{after.FieldOfView:0.######}) " +
                                  $"near={before.NearClip != after.NearClip} far={before.FarClip != after.FarClip}({before.FarClip:0}->{after.FarClip:0}) " +
                                  $"maxd={before.MaxDistance != after.MaxDistance}({before.MaxDistance:0}->{after.MaxDistance:0}) " +
                                  $"fovset={Math.Abs(settings.FovDeg - fovBefore):0.######}");
            Console.WriteLine(same ? "Q4ROUNDTRIP MATCH - the world camera did not move"
                                   : "Q4ROUNDTRIP MISMATCH - the world camera was dragged along");
            worldWarmup = 0;
            screenshotFrames = Math.Max(screenshotFrames, 2);
        }

        private struct SectionStamp_Q4
        {
            public Vector3 Target;
            public float Distance, Yaw, Pitch, Fov, Hour;
            public bool Walk, Grid, Markers;
            public int RenderMode, SelectionMode, WorldGizmoMode;
        }

        private static SectionStamp_Q4 StampFor_Q4(LightPanel.Space s)
        {
            int i = (int)s;
            return new SectionStamp_Q4
            {
                Target = new Vector3(100.0f + i * 13.0f, -200.0f - i * 17.0f, 20.0f + i * 7.0f),
                Distance = 5.0f + i * 3.0f,
                Yaw = 0.31f * (i + 1),
                Pitch = 0.05f * (i + 1),
                Fov = 35.0f + i * 6.0f,
                Hour = 1.0f + i * 2.25f,
                Walk = (i % 2) == 0,
                Grid = (i % 2) == 1,
                Markers = (i % 3) != 0,
                RenderMode = i % 3,
                SelectionMode = i % 5,
                WorldGizmoMode = i % 4,
            };
        }

        private void ApplyStamp_Q4(in SectionStamp_Q4 st)
        {
            camera.Target = st.Target; camera.Distance = st.Distance;
            camera.Yaw = st.Yaw; camera.Pitch = st.Pitch;
            camera.SnapSmoothing(); camera.Update();
            SetWalkMode(st.Walk);
            settings.FovDeg = st.Fov;
            camera.FieldOfView = st.Fov * 0.0174533f;
            panel.PreviewHour = st.Hour;
            panel.RenderMode = st.RenderMode;
            panel.ShowGrid = st.Grid;
            panel.ShowMarkers = st.Markers;
            panel.SelectionMode = st.SelectionMode;
            if (worldGizmo != null) worldGizmo.Mode = (WorldGizmoMode)st.WorldGizmoMode;
        }

        private void SectionIsolationTest_Q4(Action<string, bool, string> check)
        {
            var spaces = (LightPanel.Space[])Enum.GetValues(typeof(LightPanel.Space));
            var wasSpace = panel.Workspace;
            float wasFov = settings.FovDeg;
            bool wasWalk = walkMode;
            var wasCam = camera.Capture();

            int pairs = 0, camFails = 0, walkFails = 0, fovFails = 0, hourFails = 0,
                renderFails = 0, gridFails = 0, selFails = 0, gizmoFails = 0, sharedFails = 0;
            var worstCam = "";
            var worstOther = "";

            foreach (var a in spaces)
            {
                foreach (var b in spaces)
                {
                    if (a == b) continue;
                    pairs++;
                    var sa = StampFor_Q4(a);
                    var sb = StampFor_Q4(b);

                    panel.SwitchWorkspace(a);
                    ApplyStamp_Q4(sa);
                    var camA = camera.Capture();

                    panel.SwitchWorkspace(b);
                    ApplyStamp_Q4(sb);
                    var camB = camera.Capture();

                    panel.SwitchWorkspace(a);

                    bool viewPair = LightPanel.SharesView_U28(a, b);
                    bool shared = SharesTheMap_Q4(a, b) || viewPair;
                    if (viewPair && (Math.Abs(panel.PreviewHour - sb.Hour) > 0.001f || panel.RenderMode != sb.RenderMode ||
                                     panel.ShowGrid != sb.Grid || panel.SelectionMode != sb.SelectionMode))
                    { sharedFails++; worstOther = $"{a}<->{b} did not carry the clock / shading / grid / picks"; }
                    if (shared)
                    {
                        if (!camera.Capture().SameAs(camB)) { sharedFails++; worstOther = $"{a}<->{b} did not carry the shot"; }
                    }
                    else if (!camera.Capture().SameAs(camA))
                    {
                        camFails++;
                        if (worstCam.Length == 0)
                            worstCam = $"{a} after {b}: {camera.Position} want {camA.Target} d{camA.Distance:0.##} yaw {camA.Yaw:0.###}";
                    }

                    if (walkMode != sa.Walk && !shared) { walkFails++; if (worstOther.Length == 0) worstOther = $"{a} walk after {b}"; }
                    if (!shared && Math.Abs(settings.FovDeg - sa.Fov) > 0.001f)
                    { fovFails++; if (worstOther.Length == 0) worstOther = $"{a} fov {settings.FovDeg:0.#} want {sa.Fov:0.#} after {b}"; }
                    if (!viewPair && Math.Abs(panel.PreviewHour - sa.Hour) > 0.001f)
                    { hourFails++; if (worstOther.Length == 0) worstOther = $"{a} hour {panel.PreviewHour:0.00} want {sa.Hour:0.00} after {b}"; }
                    if (a != LightPanel.Space.Cinematic && !viewPair)
                    {
                        if (panel.RenderMode != sa.RenderMode)
                        { renderFails++; if (worstOther.Length == 0) worstOther = $"{a} render {panel.RenderMode} want {sa.RenderMode} after {b}"; }
                        if (panel.ShowGrid != sa.Grid || panel.ShowMarkers != sa.Markers)
                        { gridFails++; if (worstOther.Length == 0) worstOther = $"{a} grid/markers after {b}"; }
                    }
                    if (!viewPair && panel.SelectionMode != sa.SelectionMode)
                    { selFails++; if (worstOther.Length == 0) worstOther = $"{a} selection mode {panel.SelectionMode} want {sa.SelectionMode} after {b}"; }
                    if (worldGizmo != null && (int)worldGizmo.Mode != sa.WorldGizmoMode)
                    { gizmoFails++; if (worstOther.Length == 0) worstOther = $"{a} gizmo {worldGizmo.Mode} want {(WorldGizmoMode)sa.WorldGizmoMode} after {b}"; }
                }
            }

            check($"q4: every ordered pair of sections was exercised", pairs == spaces.Length * (spaces.Length - 1), $"{pairs} pairs");
            check("q4: a section's camera survives a trip through any other", camFails == 0, camFails == 0 ? $"{pairs} pairs" : $"{camFails} leaks, e.g. {worstCam}");
            check("q4: and its walk mode", walkFails == 0, walkFails == 0 ? "ok" : $"{walkFails} leaks, e.g. {worstOther}");
            check("q4: and its field of view", fovFails == 0, fovFails == 0 ? "ok" : $"{fovFails} leaks, e.g. {worstOther}");
            check("q4: and its clock", hourFails == 0, hourFails == 0 ? "ok" : $"{hourFails} leaks, e.g. {worstOther}");
            check("q4: and its shading mode", renderFails == 0, renderFails == 0 ? "ok" : $"{renderFails} leaks, e.g. {worstOther}");
            check("q4: and its grid / markers", gridFails == 0, gridFails == 0 ? "ok" : $"{gridFails} leaks, e.g. {worstOther}");
            check("q4: and its selection mode", selFails == 0, selFails == 0 ? "ok" : $"{selFails} leaks, e.g. {worstOther}");
            check("q4: and its gizmo tool", gizmoFails == 0, gizmoFails == 0 ? "ok" : $"{gizmoFails} leaks, e.g. {worstOther}");
            check("q4: Cinematic and World share the map, and Lights and Materials share one view, on purpose", sharedFails == 0, sharedFails == 0 ? "the shot travels" : worstOther);

            {
                var A = LightPanel.Space.World; var B = LightPanel.Space.Material;
                var sa = StampFor_Q4(A); var sb = StampFor_Q4(B);
                panel.Workspace = A; ApplyStamp_Q4(sa); var camA = camera.Capture();
                panel.Workspace = B; ApplyStamp_Q4(sb);
                panel.Workspace = A;
                check("q4: a plain Workspace assignment keeps each section's camera",
                      camera.Capture().SameAs(camA), $"{camera.Position} want {camA.Target}");
                check("q4: ...and its clock and lens", Math.Abs(panel.PreviewHour - sa.Hour) < 0.001f && Math.Abs(settings.FovDeg - sa.Fov) < 0.001f,
                      $"{panel.PreviewHour:0.00}h {settings.FovDeg:0.#} deg");
            }
            {
                var sl = StampFor_Q4(LightPanel.Space.Light);
                panel.SwitchWorkspace(LightPanel.Space.Light);
                ApplyStamp_Q4(sl);
                var camL = camera.Capture();
                panel.MaterialMode = true;
                ApplyStamp_Q4(StampFor_Q4(LightPanel.Space.Material));
                var camM = camera.Capture();
                panel.MaterialMode = false;
                check("q4: the MaterialMode setter goes through the same door",
                      panel.Workspace == LightPanel.Space.Light && camera.Capture().SameAs(camM),
                      $"{panel.Workspace} {camera.Position}");
            }
            if (panel.Sequence != null && panel.Sequence.Shots.Count > 0)
            {
                panel.SwitchWorkspace(LightPanel.Space.Cinematic);
                panel.Playing = true;
                panel.SwitchWorkspace(LightPanel.Space.Light);
                check("q4: a playing shot does not fly the camera in another section",
                      !panel.Playing && MovementEnabled, panel.Playing ? "still playing" : "handed back");
                panel.SwitchWorkspace(LightPanel.Space.Cinematic);
                panel.Playing = true;
                panel.SwitchWorkspace(LightPanel.Space.World);
                check("q4: ...but the World preview of the shot still runs", panel.Playing, panel.Playing ? "playing" : "stopped");
                panel.Playing = false;
            }
            {
                panel.SwitchWorkspace(LightPanel.Space.Light);
                bool wasFocus = panel.FocusSelectedLight;
                panel.FocusSelectedLight = true;
                bool inLight = FocusOrbitOwnsCamera_Q4;
                panel.SwitchWorkspace(LightPanel.Space.World);
                bool inWorld = FocusOrbitOwnsCamera_Q4;
                panel.SwitchWorkspace(LightPanel.Space.Cinematic);
                bool inCine = FocusOrbitOwnsCamera_Q4;
                panel.FocusSelectedLight = wasFocus;
                check("q4: only the light editor orbits its own selected light",
                      inLight && !inWorld && !inCine, $"light {inLight} world {inWorld} cine {inCine}");
            }

            check("q4: the World home-camera default cannot fire twice", !WorldHasNoCameraYet_Q4,
                  WorldHasNoCameraYet_Q4 ? "still armed" : "spent");

            {
                panel.SwitchWorkspace(LightPanel.Space.Light);
                camera.Target = new Vector3(-11, 22, 3); camera.Distance = 9; camera.Yaw = 2.0f; camera.Pitch = 0.4f;
                camera.SnapSmoothing(); camera.Update();
                var live = camera.Capture();
                panel.SwitchWorkspace(LightPanel.Space.Particles);
                check("q4: a section that has been visited never inherits again",
                      !camera.Capture().SameAs(live), $"{camera.Position}");
            }

            panel.SwitchWorkspace(wasSpace);
            settings.FovDeg = wasFov;
            camera.FieldOfView = wasFov * 0.0174533f;
            camera.Restore(wasCam);
            SetWalkMode(wasWalk);
        }
    }
}

