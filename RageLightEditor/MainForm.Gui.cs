using System;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool rightDragging;
        private bool timeScrubbing;
        private CodeWalker.GameFiles.YmapEntityDef rightDownWorldSel;

        private bool projOptsSynced;

        private void WireProjectWindowExtras()
        {
            projCtl.GoToPosition += (c, r) =>
            {
                EnterWorldWorkspaceForGoTo();
                camera.FrameBounds(c, r);
            };
            projCtl.UndoRequested += () => TryWorldUndo();
            projCtl.RedoRequested += () => TryWorldRedo();
            projCtl.CopyRequested += () => { SelectProjectEntityInWorld(); WorldCopySelected(); };
            projCtl.CutRequested += () => { SelectProjectEntityInWorld(); WorldCopySelected(); WorldDeleteSelected(); };
            projCtl.PasteRequested += () => WorldPasteClipboard();
            projCtl.CloneRequested += () => { SelectProjectEntityInWorld(); WorldDuplicateSelected(); };
            projCtl.DeleteRequested += () => { SelectProjectEntityInWorld(); WorldDeleteSelected(); };
            projCtl.ThemeRequested += t =>
            {
                settings.ThemeIndex = t;
                settings.ThemeDefaulted = true;
                panel.ApplyThemeFromSettings();
            };
            ProjWin.AutoCalcFlags = settings.ProjectAutoCalcFlags;
            ProjWin.AutoCalcExtents = settings.ProjectAutoCalcExtents;
            ProjWin.DisplayEntityIndexes = settings.ProjectDisplayEntityIndexes;
            projOptsSynced = true;
        }

        private void TickProjectWindowExtras()
        {
            ProjWin.CanUndo = WorldHistory.CanUndo;
            ProjWin.CanRedo = WorldHistory.CanRedo;
            ProjWin.CanPaste = !EntityOps.Clipboard.IsEmpty;
            ProjWin.ThemeIndex = settings.ThemeIndex;
            if (projOptsSynced && ProjWin.OptionsChanged)
            {
                ProjWin.OptionsChanged = false;
                settings.ProjectAutoCalcFlags = ProjWin.AutoCalcFlags;
                settings.ProjectAutoCalcExtents = ProjWin.AutoCalcExtents;
                settings.ProjectDisplayEntityIndexes = ProjWin.DisplayEntityIndexes;
                settings.Save();
            }
        }

        private void SelectProjectEntityInWorld()
        {
            if (ProjWin.CurrentEntity != null) WorldEdit.Select(ProjWin.CurrentEntity);
        }

        private readonly Camera.Snapshot[] workspaceCam = new Camera.Snapshot[Enum.GetValues(typeof(LightPanel.Space)).Length];
        private readonly bool[] workspaceWalk = new bool[Enum.GetValues(typeof(LightPanel.Space)).Length];

        public Camera.Snapshot WorkspaceCameraOf(LightPanel.Space s) => workspaceCam[(int)s];
        public Camera.Snapshot CurrentCamera => camera.Capture();

        private void OnWorkspaceSwitching(LightPanel.Space leaving, LightPanel.Space entering)
        {
            workspaceCam[(int)leaving] = camera.Capture();
            workspaceWalk[(int)leaving] = walkMode;
            foreach (var s in LightPanel.ViewGroupOf_U28(leaving))
            {
                workspaceCam[(int)s] = workspaceCam[(int)leaving];
                workspaceWalk[(int)s] = walkMode;
            }
            bool sameMap = (leaving == LightPanel.Space.Cinematic && entering == LightPanel.Space.World) ||
                           (leaving == LightPanel.Space.World && entering == LightPanel.Space.Cinematic);
            if (sameMap) return;
            var next = workspaceCam[(int)entering];
            if (!next.Valid) return;
            camera.Restore(next);
            SetWalkMode(workspaceWalk[(int)entering]);
        }

        private void WorkspaceStateTest(Action<string, bool, string> check)
        {
            var S = LightPanel.Space.Light; var W = LightPanel.Space.World;
            var tc = panel.Timecycle;
            panel.SwitchWorkspace(S);
            if (tc != null) { tc.SelectedModifier = 3; tc.ModifierStrength = 0.6f; }
            panel.PreviewHour = 21.5f;
            camera.Target = new Vector3(12, -7, 3); camera.Distance = 4; camera.Yaw = 1.1f; camera.Pitch = 0.2f;
            camera.SnapSmoothing(); camera.Update();
            var lightsCam = camera.Capture();
            SetWalkMode(true);

            panel.SwitchWorkspace(W);
            check("world starts with no interior modifier", tc == null || tc.SelectedModifier == -1,
                $"modifier {(tc?.SelectedModifier ?? -1)}");
            check("world's first entry keeps the clock", Math.Abs(panel.PreviewHour - 21.5f) < 0.001f, $"{panel.PreviewHour:0.00}");
            panel.PreviewHour = 8.25f;
            camera.Target = new Vector3(-75, -818, 330); camera.Distance = 1.5f; camera.Yaw = 4.71f; camera.Pitch = -0.3f;
            camera.SnapSmoothing(); camera.Update();
            var worldCam = camera.Capture();
            SetWalkMode(false);

            panel.SwitchWorkspace(S);
            check("lights camera restored", camera.Capture().SameAs(lightsCam), $"{camera.Position} yaw {camera.Yaw:0.000}");
            check("lights walk mode restored", walkMode, walkMode ? "walking" : "not walking");
            check("lights modifier restored", tc == null || (tc.SelectedModifier == 3 && Math.Abs(tc.ModifierStrength - 0.6f) < 0.001f),
                $"modifier {(tc?.SelectedModifier ?? -1)} x{tc?.ModifierStrength:0.00}");
            check("lights hour restored", Math.Abs(panel.PreviewHour - 21.5f) < 0.001f, $"{panel.PreviewHour:0.00}");

            panel.SwitchWorkspace(W);
            check("world camera restored", camera.Capture().SameAs(worldCam), $"{camera.Position} yaw {camera.Yaw:0.000}");
            check("world walk mode restored", !walkMode, walkMode ? "walking" : "not walking");
            check("world modifier still off", tc == null || tc.SelectedModifier == -1, $"modifier {(tc?.SelectedModifier ?? -1)}");
            check("world hour restored", Math.Abs(panel.PreviewHour - 8.25f) < 0.001f, $"{panel.PreviewHour:0.00}");

            panel.SwitchWorkspace(LightPanel.Space.Archive);
            check("rpf workspace is reachable", panel.Workspace == LightPanel.Space.Archive, panel.Workspace.ToString());
            panel.SwitchWorkspace(LightPanel.Space.Particles);
            check("particles workspace is reachable", panel.Workspace == LightPanel.Space.Particles, panel.Workspace.ToString());
            panel.SwitchWorkspace(W);
            check("world camera survives the new workspaces", camera.Capture().SameAs(worldCam), $"{camera.Position} yaw {camera.Yaw:0.000}");
            check("world hour survives the new workspaces", Math.Abs(panel.PreviewHour - 8.25f) < 0.001f, $"{panel.PreviewHour:0.00}");
            panel.SwitchWorkspace(S);
            if (tc != null) tc.SelectedModifier = -1;
        }

        private void EnterWorldWorkspaceForGoTo()
        {
            if (panel.WorldMode) return;
            panel.SwitchWorkspace(LightPanel.Space.World);
        }
    }
}

