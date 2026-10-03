using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool rightPressConsumed_T1;

        internal const bool SelectByRightClick_T1 = true;

        private void RightMouseDown_T1(int x, int y)
        {
            rightPressConsumed_T1 = false;
            if (panel == null) return;
            if (panel.WorldMode && worldBuilt) rightDownWorldSel = WorldEdit.Selected;
            if (TerrainRightDown_R4(x, y)) { rightPressConsumed_T1 = true; return; }
            if (MloSnapRightDown_L4()) { rightPressConsumed_T1 = true; return; }
        }

        private bool RightReleaseIsClick_T1(float dragPixels)
            => rightDragging && !timeScrubbing && !rightPressConsumed_T1 && dragPixels < 5.0f && !ImGuiWantsMouse;

        private void RightViewportClick_T1(int x, int y)
        {
            if (panel == null) return;

            if (panel.WorldMode)
            {
                if (panel.NavMode) return;
                if (!worldBuilt) return;
                bool handled = false;
                if (!AreaTool.Drawing) AreaMouseClick_J5(x, y, ref handled);
                if (!handled && panel.MouseSelectEnabled) WorldPickAt(x, y);
                return;
            }

            if (panel.MloMode)
            {
                if (MloPickerArmed_T1(Creator)) return;
                bool h5 = false;
                MloCreatorMouseUp_H5(x, y, true, ref h5);
                if (!h5) PickAt(x, y);
                return;
            }

            PickAt(x, y);
        }

        private void LeftViewportClick_T1(int x, int y)
        {
            if (panel == null) return;
            if (panel.WorldMode)
            {
                if (worldBuilt && AreaTool.Drawing) { bool h = false; AreaMouseClick_J5(x, y, ref h); return; }
                if (panel.NavMode && NavEd != null && NavEd.PlacingPoly) { bool h = false; NavMouseClick_P4(x, y, ref h); }
                return;
            }
            if (panel.MloMode && MloPickerArmed_T1(Creator))
            {
                bool h5 = false;
                MloCreatorMouseUp_H5(x, y, true, ref h5);
            }
        }

        private static bool MloPickerArmed_T1(MloCreatorPanel ui)
            => ui != null && (ui.SnapMode || ui.PickingPortalCorners || ui.PickingRoomCorners || ui.PickingRoomMesh);

        private string SelectionSignature_T1()
        {
            var s = new StringBuilder();
            s.Append("world=").Append(WorldEdit.Selection.HasValue ? WorldEdit.Selection.GetNameString("-") : "-");
            var sc = CurrentScene;
            s.Append(" light=").Append(sc?.SelectedIndex ?? -1);
            s.Append(" file=").Append(sc?.ActiveFile?.Name ?? "-");
            var ui = Creator;
            s.Append(" ent=").Append(ui?.SelectedEntity ?? -1)
             .Append(" room=").Append(ui?.SelectedRoom ?? -1)
             .Append(" portal=").Append(ui?.SelectedPortal ?? -1)
             .Append(" corner=").Append(ui?.SelectedCorner ?? -1);
            s.Append(" nav=").Append(NavEd?.SelectedPoly?.Index.ToString() ?? "-")
             .Append('/').Append(NavEd?.SelectedPoint?.Index.ToString() ?? "-")
             .Append('/').Append(NavEd?.SelectedPortal?.Index.ToString() ?? "-");
            s.Append(" mat=").Append(materialPanel?.Selected?.Name ?? "-");
            return s.ToString();
        }

        private bool rclickDone_T1;
        private int rclickWait_T1;

        private void ServiceRClickProbe_T1()
        {
            if (rclickDone_T1) return;
            var spec = Environment.GetEnvironmentVariable("RLE_RCLICK");
            if (string.IsNullOrEmpty(spec) || panel == null || deviceResources == null) return;
            if (screenshotPath != null) screenshotFrames = Math.Max(screenshotFrames, 12);
            rclickWait_T1++;
            bool anything = worldBuilt || (mloScene?.HasModel ?? false) || (lightScene?.HasModel ?? false);
            bool worldReady = !worldBuilt || worldWarmup >= 380 || screenshotPath == null;
            if ((!anything || !worldReady) && rclickWait_T1 < 3000) return;
            rclickDone_T1 = true;
            Console.WriteLine($"T1RCLICK [{spec}] after {rclickWait_T1} frames: world={worldBuilt} mloScene={(mloScene?.Files.Count ?? 0)} file(s) lightScene={(lightScene?.Files.Count ?? 0)} file(s)");
            try { RunRClickProbe_T1(spec); }
            catch (Exception ex) { Console.WriteLine("T1RCLICK threw: " + ex); }
        }

        private static readonly LightPanel.Space[] RClickSpaces_T1 =
        {
            LightPanel.Space.World, LightPanel.Space.Light, LightPanel.Space.Material,
            LightPanel.Space.Mlo, LightPanel.Space.NavMesh, LightPanel.Space.Terrain,
            LightPanel.Space.Particles, LightPanel.Space.Archive,
        };

        private void RunRClickProbe_T1(string spec)
        {
            var wanted = new List<LightPanel.Space>();
            if (spec.Equals("all", StringComparison.OrdinalIgnoreCase) || spec == "1") wanted.AddRange(RClickSpaces_T1);
            else
                foreach (var bit in spec.Split(','))
                    if (SpaceNames.TryParse(bit, out var sp)) wanted.Add(sp);
            if (wanted.Count == 0) { Console.WriteLine("T1RCLICK nothing to probe: " + spec); return; }

            var was = panel.Workspace;
            panel.EntityPicking = true;
            panel.MouseSelectEnabled = true;
            int leftBad = 0, rightGot = 0, dragBad = 0;
            foreach (var sp in wanted)
            {
                panel.SwitchWorkspace(sp);
                int cx = deviceResources.Width / 2, cy = deviceResources.Height / 2;
                AimForProbe_T1(sp, ref cx, ref cy);
                string before = SelectionSignature_T1();

                Click_T1(MouseButtons.Left, cx, cy);
                string afterLeft = SelectionSignature_T1();
                bool leftOk = afterLeft == before;
                if (!leftOk) leftBad++;

                Click_T1(MouseButtons.Right, cx, cy);
                string afterRight = SelectionSignature_T1();
                bool rightSelected = afterRight != afterLeft;
                if (rightSelected) rightGot++;

                Drag_T1(MouseButtons.Right, cx, cy, cx + 60, cy - 30);
                string afterDrag = SelectionSignature_T1();
                bool dragOk = afterDrag == afterRight;
                if (!dragOk) dragBad++;

                Console.WriteLine($"T1RCLICK {sp,-9} left={(leftOk ? "no change OK" : "*** SELECTED SOMETHING")} " +
                                  $"right={(rightSelected ? "selected" : "nothing under the cursor")} " +
                                  $"rightDrag={(dragOk ? "no change OK" : "*** CHANGED THE SELECTION")}");
                if (!leftOk) Console.WriteLine($"T1RCLICK   before [{before}]\nT1RCLICK   afterLeft [{afterLeft}]");
                if (!dragOk) Console.WriteLine($"T1RCLICK   afterRight [{afterRight}]\nT1RCLICK   afterDrag [{afterDrag}]");
            }
            if (screenshotPath == null) panel.SwitchWorkspace(was);
            Console.WriteLine($"T1RCLICK RESULT: {wanted.Count} workspace(s); left selected in {leftBad}; " +
                              $"right selected in {rightGot}; right-drag changed the selection in {dragBad}");
            Console.WriteLine(leftBad == 0 && dragBad == 0 ? "T1RCLICK PASSED" : "T1RCLICK FAILED");
        }

        private void AimForProbe_T1(LightPanel.Space sp, ref int px, ref int py)
        {
            if (sp == LightPanel.Space.World || sp == LightPanel.Space.NavMesh) return;
            var ui = Creator;
            if (sp == LightPanel.Space.Mlo && ui?.Session != null)
            {
                for (int i = 0; i < ui.Session.Entities.Count; i++)
                    if (AimAt_P2(ui, i, out px, out py)) return;
            }
            var sc = CurrentScene;
            if (sc == null || !sc.HasModel) return;
            FrameModel();
            camera.SnapSmoothing();
            camera.Update();
        }

        private void Click_T1(MouseButtons button, int x, int y)
        {
            ClearImGuiMouse_R2();
            orbiting = false; gizmoConsumedClick = false; mouseDownDrag = 0; timeScrubbing = false;
            lastMouse = new System.Drawing.Point(x, y);
            OnMouseDownEv(this, new MouseEventArgs(button, 1, x, y, 0));
            ClearImGuiMouse_R2();
            OnMouseUpEv(this, new MouseEventArgs(button, 1, x, y, 0));
        }

        private void Drag_T1(MouseButtons button, int x0, int y0, int x1, int y1)
        {
            ClearImGuiMouse_R2();
            orbiting = false; gizmoConsumedClick = false; mouseDownDrag = 0; timeScrubbing = false;
            lastMouse = new System.Drawing.Point(x0, y0);
            OnMouseDownEv(this, new MouseEventArgs(button, 1, x0, y0, 0));
            ClearImGuiMouse_R2();
            OnMouseMoveEv(this, new MouseEventArgs(button, 0, (x0 + x1) / 2, (y0 + y1) / 2, 0));
            ClearImGuiMouse_R2();
            OnMouseMoveEv(this, new MouseEventArgs(button, 0, x1, y1, 0));
            ClearImGuiMouse_R2();
            OnMouseUpEv(this, new MouseEventArgs(button, 1, x1, y1, 0));
        }

        partial void SeqTest_T1(Action<string, bool, string> check)
        {
            RightSelectTest_T1(check);
            MloNewProjectTest_T1(check);
        }

        private void RightSelectTest_T1(Action<string, bool, string> check)
        {
            var wasSpace = panel.Workspace;
            try
            {
                panel.SwitchWorkspace(LightPanel.Space.Light);
                panel.EntityPicking = true;
                var sc = CurrentScene;
                if (sc != null && sc.Files.Count > 0 && deviceResources != null)
                {
                    camera.Yaw = camera.TargetYaw = 0.6f;
                    camera.Pitch = camera.TargetPitch = 0.35f;
                    FrameModel();
                    camera.SnapSmoothing();
                    camera.Update();
                    sc.SelectedFiles.Clear(); sc.ActiveFile = null;
                    int cx = deviceResources.Width / 2, cy = deviceResources.Height / 2;
                    Click_T1(MouseButtons.Left, cx, cy);
                    bool leftPicked = sc.ActiveFile != null;
                    check("right-click select: a LEFT click in the light workspace picks nothing",
                          !leftPicked, leftPicked ? "it selected " + sc.ActiveFile.Name : "");
                    Click_T1(MouseButtons.Right, cx, cy);
                    check("right-click select: a RIGHT click in the light workspace picks the prop",
                          sc.ActiveFile != null, sc.ActiveFile?.Name ?? "nothing under the crosshair");
                }

                panel.SwitchWorkspace(LightPanel.Space.Mlo);
                var ui = Creator;
                var msc = CurrentScene;
                if (ui?.Session != null && ui.Session.Entities.Count > 0 && msc != null && deviceResources != null)
                {
                    int ei = -1, px = 0, py = 0;
                    for (int i = 0; i < ui.Session.Entities.Count && ei < 0; i++)
                        if (AimAt_P2(ui, i, out px, out py)) ei = i;
                    if (ei < 0) check("right-click select: an MLO entity a click can land on", false, "none of the entities could be aimed at");
                    else
                    {
                        ui.SelectedEntity = -1; ui.SelectedEntities.Clear();
                        msc.SelectedFiles.Clear(); msc.ActiveFile = null;
                        string before = SelectionSignature_T1();
                        Click_T1(MouseButtons.Left, px, py);
                        check("right-click select: a LEFT click in the MLO Creator picks nothing",
                              SelectionSignature_T1() == before, SelectionSignature_T1());
                        Click_T1(MouseButtons.Right, px, py);
                        check("right-click select: a RIGHT click in the MLO Creator selects the prop under the cursor",
                              ui.SelectedEntity == ei, $"aimed at entity {ei}, selected {ui.SelectedEntity} ({SelectionSignature_T1()})");
                    }
                }

                foreach (var sp in RClickSpaces_T1)
                {
                    panel.SwitchWorkspace(sp);
                    if (deviceResources == null) break;
                    string before = SelectionSignature_T1();
                    LeftViewportClick_T1(deviceResources.Width / 2, deviceResources.Height / 2);
                    check($"right-click select: the left button selects nothing in {sp}",
                          SelectionSignature_T1() == before, SelectionSignature_T1());
                }

                check("right-click select: a right press that moved is not a click",
                      !RightReleaseIsClick_T1(40.0f), "");
                rightDragging = true; timeScrubbing = false; rightPressConsumed_T1 = true;
                check("right-click select: a right press a tool claimed is not a click",
                      !RightReleaseIsClick_T1(0.0f), "");
                rightPressConsumed_T1 = false; rightDragging = false;
            }
            catch (Exception ex)
            {
                check("right-click select: no exception", false, ex.ToString());
            }
            finally
            {
                if (screenshotPath == null) panel.SwitchWorkspace(wasSpace);
            }
        }
    }
}

