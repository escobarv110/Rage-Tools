using System;
using System.Collections.Generic;
using System.Linq;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private static bool SharesView_V21(LightPanel.Space a, LightPanel.Space b) =>
            (a == LightPanel.Space.Cinematic && b == LightPanel.Space.World) ||
            (a == LightPanel.Space.World && b == LightPanel.Space.Cinematic) ||
            LightPanel.SharesView_U28(a, b);

        private void SeqTest_SectionCams_V21(Action<string, bool, string> check)
        {
            if (panel == null) { Console.WriteLine("  v21: (skipped - no panel)"); return; }

            var spaces = (LightPanel.Space[])Enum.GetValues(typeof(LightPanel.Space));
            var was = panel.Workspace;

            var parked = new Dictionary<LightPanel.Space, Rendering.Camera.Snapshot>();

            foreach (var s in spaces)
            {
                panel.SwitchWorkspace(s);
                int i = (int)s;
                camera.Target = new Vector3(1000 + i * 100, 2000 + i * 10, 30 + i);
                camera.Distance = 5.0f + i;
                camera.Yaw = 0.1f * i;
                camera.Pitch = 0.05f * i;
                camera.SnapSmoothing();
                camera.Update();
                parked[s] = camera.Capture();
                foreach (var g in LightPanel.ViewGroupOf_U28(s)) parked[g] = parked[s];
            }

            var moved = new List<string>();
            var shared = new List<string>();
            foreach (var s in spaces)
            {
                var from = panel.Workspace;
                panel.SwitchWorkspace(s);
                var now = camera.Capture();
                var want = parked[s];
                float d = (now.Target - want.Target).Length();

                if (SharesView_V21(from, s))
                {
                    shared.Add(SpaceNames.NameOf(from) + "->" + SpaceNames.NameOf(s));
                    continue;
                }
                if (d > 0.5f) moved.Add($"{SpaceNames.NameOf(s)} (off by {d:0.#} m)");
            }

            check("v21 cameras: every section comes back to the camera it was left on",
                  moved.Count == 0,
                  moved.Count == 0 ? $"all {spaces.Length} sections held their view"
                                   : "moved: " + string.Join(", ", moved));

            panel.SwitchWorkspace(LightPanel.Space.World);
            camera.Target = new Vector3(777, -888, 99);
            camera.SnapSmoothing(); camera.Update();
            var worldCam = camera.Capture();
            panel.SwitchWorkspace(LightPanel.Space.Cinematic);
            var cineCam = camera.Capture();
            check("v21 cameras: ...except Cinematic and World, which share one on purpose",
                  (cineCam.Target - worldCam.Target).Length() < 0.5f,
                  $"world {worldCam.Target} / cinematic {cineCam.Target}");

            panel.SwitchWorkspace(LightPanel.Space.Light);
            camera.Target = new Vector3(10, 20, 30); camera.SnapSmoothing(); camera.Update();
            panel.SwitchWorkspace(LightPanel.Space.Material);
            camera.Target = new Vector3(-40, -50, -60); camera.SnapSmoothing(); camera.Update();
            var matWant = camera.Capture();
            panel.SwitchWorkspace(LightPanel.Space.Light);
            var lightBack = camera.Capture();
            panel.SwitchWorkspace(LightPanel.Space.Material);
            var matBack = camera.Capture();
            check("v21 cameras: Lights and Materials share one view",
                  (lightBack.Target - matWant.Target).Length() < 0.5f &&
                  (matBack.Target - matWant.Target).Length() < 0.5f,
                  $"lights {lightBack.Target}, materials {matBack.Target}");

            var saved = settings?.SectionCameras;
            int named = saved?.Count(p => p != null && SpaceNames.TryParse(p.Section, out _)) ?? 0;
            check("v21 cameras: the placements are stored by NAME, so a new section cannot shift them",
                  saved == null || named == saved.Count,
                  $"{named} of {saved?.Count ?? 0} rows readable");

            {
                var saveSpawnDone = worldSpawnDone;
                worldSpawnDone = false;
                panel.SwitchWorkspace(LightPanel.Space.World);
                camera.Target = new Vector3(4242, -1234, 55);
                camera.SnapSmoothing(); camera.Update();
                var mine = camera.Capture();
                panel.SwitchWorkspace(LightPanel.Space.Light);
                worldSpawnDone = false;
                panel.SwitchWorkspace(LightPanel.Space.World);
                var back = camera.Capture();
                check("v21 cameras: the World keeps its camera too - the spawn no longer overrides it",
                      (back.Target - mine.Target).Length() < 0.5f,
                      $"left at {mine.Target}, came back to {back.Target}");
                worldSpawnDone = saveSpawnDone;
            }

            panel.SwitchWorkspace(was);
            if (shared.Count > 0)
                Console.WriteLine("  v21 cameras: shared-view hops skipped: " + string.Join(", ", shared));
        }
    }
}

