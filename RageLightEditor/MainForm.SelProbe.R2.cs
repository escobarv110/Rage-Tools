using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeWalker;
using CodeWalker.GameFiles;
using ImGuiNET;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool selProbeDone_R2;

        partial void ServiceAreaGrass_R2();

        partial void OnWorldTick_R2()
        {
            ServiceSelProbe_R2();
            StepSelProbe_R2();
            ServiceAreaGrass_R2();
        }

        private void ServiceSelProbe_R2()
        {
            if (selProbeDone_R2) return;
            var spec = Environment.GetEnvironmentVariable("RLE_SELPROBE");
            if (string.IsNullOrEmpty(spec) || !worldBuilt || deviceResources == null || panel == null) return;
            if (screenshotPath != null && worldWarmup < 380) return;
            selProbeDone_R2 = true;
            try { RunSelProbe_R2(spec); }
            catch (Exception ex) { Console.WriteLine("R2PROBE threw: " + ex); }
        }

        private void ClearImGuiMouse_R2()
        {
            if (ImGui.GetCurrentContext() == IntPtr.Zero) return;
            var io = ImGui.GetIO();
            io.WantCaptureMouse = false;
        }

        private void MouseDown_R2(int x, int y)
        {
            ClearImGuiMouse_R2();
            OnMouseDownEv(this, new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
        }

        private void MouseMove_R2(int x, int y)
        {
            ClearImGuiMouse_R2();
            OnMouseMoveEv(this, new MouseEventArgs(MouseButtons.Left, 0, x, y, 0));
        }

        private void MouseUp_R2(int x, int y)
        {
            ClearImGuiMouse_R2();
            OnMouseUpEv(this, new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
        }

        private void MouseRightClick_R2(int x, int y)
        {
            ClearImGuiMouse_R2();
            timeScrubbing = false;
            OnMouseDownEv(this, new MouseEventArgs(MouseButtons.Right, 1, x, y, 0));
            ClearImGuiMouse_R2();
            OnMouseUpEv(this, new MouseEventArgs(MouseButtons.Right, 1, x, y, 0));
        }

        private bool Project_R2(Vector3 p, out int x, out int y)
        {
            x = y = 0;
            if (deviceResources == null) return false;
            var v = Vector4.Transform(new Vector4(p, 1.0f), camera.ViewProjMatrix);
            if (v.W <= 1e-4f) return false;
            x = (int)((v.X / v.W * 0.5f + 0.5f) * deviceResources.Width);
            y = (int)((-v.Y / v.W * 0.5f + 0.5f) * deviceResources.Height);
            return x >= 0 && y >= 0 && x < deviceResources.Width && y < deviceResources.Height;
        }

        private bool FindPixelFor_R2(YmapEntityDef e, out int px, out int py)
        {
            px = py = 0;
            if (e == null) return false;
            var mode = panel.SelectionModeEnum;
            bool Hits(int x, int y)
            {
                if (x < 0 || y < 0 || x >= deviceResources.Width || y >= deviceResources.Height) return false;
                var ray = camera.GetPickRay(x, y, deviceResources.Width, deviceResources.Height);
                var h = WorldPickHit(ray, mode);
                return ReferenceEquals(h.EntityDef, e);
            }
            var starts = new List<Vector3> { e.Position };
            if (e.Archetype != null) starts.Add(e.Position + e.Orientation.Multiply(e.Archetype.BSCenter * e.Scale));
            foreach (var s in starts)
            {
                if (!Project_R2(s, out int cx, out int cy)) continue;
                for (int r = 0; r <= 120; r += 4)
                {
                    for (int a = 0; a < (r == 0 ? 1 : 24); a++)
                    {
                        double th = a * Math.PI * 2 / 24.0;
                        int x = cx + (int)(Math.Cos(th) * r), y = cy + (int)(Math.Sin(th) * r);
                        if (!Hits(x, y)) continue;
                        px = x; py = y; return true;
                    }
                }
            }
            int mx = deviceResources.Width / 2, my = deviceResources.Height / 2;
            var grid = new List<(int x, int y, int d)>();
            for (int y = 8; y < deviceResources.Height; y += 24)
                for (int x = 8; x < deviceResources.Width; x += 24)
                    grid.Add((x, y, (x - mx) * (x - mx) + (y - my) * (y - my)));
            grid.Sort((p, q) => p.d.CompareTo(q.d));
            foreach (var g in grid)
            {
                if (!Hits(g.x, g.y)) continue;
                px = g.x; py = g.y; return true;
            }
            return false;
        }

        private YmapEntityDef SelProbeTarget_R2()
        {
            int mx = deviceResources.Width / 2, my = deviceResources.Height / 2;
            var mode = panel.SelectionModeEnum;
            YmapEntityDef best = null; int bestD = int.MaxValue;
            for (int y = 16; y < deviceResources.Height; y += 20)
                for (int x = 16; x < deviceResources.Width; x += 20)
                {
                    int d = (x - mx) * (x - mx) + (y - my) * (y - my);
                    if (d >= bestD) continue;
                    var ray = camera.GetPickRay(x, y, deviceResources.Width, deviceResources.Height);
                    var e = WorldPickHit(ray, mode).EntityDef;
                    if (e?.Ymap == null || e.MloInstance != null) continue;
                    float r = e.Archetype?.BSRadius ?? 999.0f;
                    if (r > 12.0f) continue;
                    best = e; bestD = d;
                }
            return best;
        }

        private enum SelProbeStage { Idle, Click, AfterClick, Drag, AfterDrag, Done }
        private SelProbeStage selProbeStage = SelProbeStage.Idle;
        private int selProbeWait, selProbeGap = 8, selProbeRound, selProbeRounds = 4, selProbeFails;
        private int selProbeDx = 70, selProbeDy = -35;
        private YmapEntityDef selProbeEntity;
        private Vector3 selProbeStart, selProbeBefore;
        private int selProbePx, selProbePy;

        private void RunSelProbe_R2(string spec)
        {
            bool precision = false, withProject = false, down = false;
            foreach (var bit in spec.Split(','))
            {
                var b = bit.Trim();
                if (int.TryParse(b, out int n) && n > 0) selProbeRounds = Math.Min(n, 12);
                else if (b.StartsWith("gap", StringComparison.OrdinalIgnoreCase) && int.TryParse(b.Substring(3), out int g)) selProbeGap = Math.Clamp(g, 0, 120);
                else if (b.Equals("precision", StringComparison.OrdinalIgnoreCase)) precision = true;
                else if (b.Equals("proj", StringComparison.OrdinalIgnoreCase)) withProject = true;
                else if (b.Equals("down", StringComparison.OrdinalIgnoreCase)) down = true;
            }
            if (down) { selProbeDx = -70; selProbeDy = 35; }
            if (!panel.WorldMode) panel.SwitchWorkspace(LightPanel.Space.World);
            int mi = LightPanel.IndexOfMode(precision ? WorldSelectionMode.EntityPrecision : WorldSelectionMode.Entity);
            if (mi >= 0) panel.SelectionMode = mi;
            worldGizmo.Mode = WorldGizmoMode.Translate;
            worldGizmo.Enabled = true;
            panel.MouseSelectEnabled = true;
            if (withProject) { ProjWin.Visible = true; ProjWin.Minimized = false; }

            int cx = deviceResources.Width / 2, cy = deviceResources.Height / 2;
            var ray0 = camera.GetPickRay(cx, cy, deviceResources.Width, deviceResources.Height);
            var target = SelProbeTarget_R2() ?? WorldPickHit(ray0, panel.SelectionModeEnum).EntityDef ?? WorldPickEntity(ray0);
            if (target == null)
            {
                Console.WriteLine("R2PROBE no entity under the crosshair - nothing to probe");
                Console.WriteLine("R2PROBE FAILED (no target)");
                return;
            }
            selProbeEntity = target;
            selProbeStart = target.Position;
            selProbeRound = 0;
            selProbeFails = 0;
            selProbeStage = SelProbeStage.Click;
            selProbeWait = 0;
            Console.WriteLine($"R2PROBE mode={panel.SelectionModeName} project={(withProject ? "open" : "closed")} rounds={selProbeRounds} " +
                              $"gap={selProbeGap} frames drag=({selProbeDx},{selProbeDy}) px " +
                              $"target '{target.Archetype?.Name ?? target._CEntityDef.archetypeName.ToString()}' ymap={target.Ymap?.Name ?? "-"} " +
                              $"pos=({target.Position.X:0.00},{target.Position.Y:0.00},{target.Position.Z:0.00}) visible={World.Visible.Contains(target)} " +
                              $"worldVisible={World.Visible.Count}");
        }

        private void StepSelProbe_R2()
        {
            if (selProbeStage == SelProbeStage.Idle || selProbeStage == SelProbeStage.Done) return;
            if (screenshotPath != null && worldWarmup > 380) worldWarmup = 380;
            if (selProbeWait > 0) { selProbeWait--; return; }
            var live = selProbeEntity;
            int r = selProbeRound + 1;
            switch (selProbeStage)
            {
                case SelProbeStage.Click:
                    {
                        WorldEdit.Deselect();
                        bool aimed = FindPixelFor_R2(live, out selProbePx, out selProbePy);
                        if (!aimed)
                        {
                            Console.WriteLine($"R2PROBE round {r}: FAIL - no pixel on screen picks the entity any more " +
                                              $"(pos {live.Position}, in Visible {World.Visible.Contains(live)}, instances {SelProbeMeshCount_R2(live)}, camera {camera.Position})");
                            selProbeFails++;
                            SelProbeExplain_R2(live);
                            FinishSelProbe_R2();
                            return;
                        }
                        var rayC = camera.GetPickRay(selProbePx, selProbePy, deviceResources.Width, deviceResources.Height);
                        var wouldHit = WorldPickHit(rayC, panel.SelectionModeEnum);
                        MouseDown_R2(selProbePx, selProbePy);
                        MouseUp_R2(selProbePx, selProbePy);
                        bool leftSelected = WorldEdit.Selection.HasValue;
                        MouseRightClick_R2(selProbePx, selProbePy);
                        var selNow = WorldEdit.Selection;
                        if (leftSelected) { selProbeFails++; Console.WriteLine($"R2PROBE round {r}: FAIL - the LEFT button selected something (WS-T1: right click only)"); }
                        bool selectedIt = ReferenceEquals(selNow.EntityDef, live);
                        var tgt = selNow.GizmoTarget();
                        var tlist = WorldGizmoTargets();
                        Console.WriteLine($"R2PROBE round {r} click ({selProbePx},{selProbePy}): rayHit={(wouldHit.EntityDef?.Archetype?.Name ?? wouldHit.GetNameString("nothing"))} " +
                                          $"consumedByGizmo={gizmoConsumedClick} -> selected={(selNow.HasValue ? selNow.GetNameString("?") : "nothing")} " +
                                          $"SAME={selectedIt} gizmoTarget={(tgt == null ? "NONE" : tgt.GetType().Name)} targets={tlist.Count} " +
                                          $"instances={SelProbeMeshCount_R2(live)} dragging={worldGizmo.Dragging}");
                        if (!selectedIt)
                        {
                            selProbeFails++;
                            Console.WriteLine($"R2PROBE round {r}: FAIL - the click did not re-select the entity");
                            SelProbeExplain_R2(live);
                            FinishSelProbe_R2();
                            return;
                        }
                        selProbeStage = SelProbeStage.AfterClick;
                        selProbeWait = selProbeGap;
                        return;
                    }
                case SelProbeStage.AfterClick:
                    selProbeStage = SelProbeStage.Drag;
                    return;
                case SelProbeStage.Drag:
                    {
                        selProbeBefore = live.Position;
                        bool projected = Project_R2(selProbeBefore, out int gx, out int gy);
                        MouseDown_R2(gx, gy);
                        bool grabbed = worldGizmo.Dragging;
                        int tx = gx + selProbeDx, ty = gy + selProbeDy;
                        MouseMove_R2(tx, ty);
                        MouseUp_R2(tx, ty);
                        var after = live.Position;
                        float moved = (after - selProbeBefore).Length();
                        bool stillSel = ReferenceEquals(WorldEdit.Selection.EntityDef, live);
                        Console.WriteLine($"R2PROBE round {r} drag  ({gx},{gy})->({tx},{ty}): onGizmo={projected} grabbed={grabbed} " +
                                          $"dragging-after-up={worldGizmo.Dragging} moved={moved:0.000} m " +
                                          $"({selProbeBefore.X:0.00},{selProbeBefore.Y:0.00},{selProbeBefore.Z:0.00})->({after.X:0.00},{after.Y:0.00},{after.Z:0.00}) " +
                                          $"stillSelected={stillSel} undo='{WorldHistory.NextUndoName}' history={WorldHistory.Count}");
                        if (!grabbed || moved < 0.01f)
                        {
                            selProbeFails++;
                            Console.WriteLine($"R2PROBE round {r}: FAIL - the gizmo did not move the entity ({(grabbed ? "grabbed but nothing moved" : "the handle was not grabbed")})");
                        }
                        selProbeStage = SelProbeStage.AfterDrag;
                        selProbeWait = selProbeGap;
                        return;
                    }
                case SelProbeStage.AfterDrag:
                    {
                        var again = WorldPickHit(camera.GetPickRay(selProbePx, selProbePy, deviceResources.Width, deviceResources.Height), panel.SelectionModeEnum).EntityDef;
                        if (again != null && !ReferenceEquals(again, live) && again.Archetype == live.Archetype)
                            Console.WriteLine($"R2PROBE round {r}: NOTE a DIFFERENT instance of the same archetype is under that pixel now (project / undo swap)");
                        selProbeRound++;
                        if (selProbeRound >= selProbeRounds) { FinishSelProbe_R2(); return; }
                        selProbeStage = SelProbeStage.Click;
                        return;
                    }
            }
        }

        private int SelProbeMeshCount_R2(YmapEntityDef e) => worldRender?.InstanceCountOf_R2(e) ?? -1;

        private void SelProbeExplain_R2(YmapEntityDef e)
        {
            if (e == null) return;
            var a = e.Archetype;
            var ymap = e.Ymap;
            bool inYmap = ymap?.AllEntities != null && Array.IndexOf(ymap.AllEntities, e) >= 0;
            YmapFile over = null;
            if (World.ProjectOverrides != null && ymap != null) World.ProjectOverrides.TryGetValue(JenkHash.GenHash(System.IO.Path.GetFileNameWithoutExtension(ymap.Name ?? "")), out over);
            Console.WriteLine($"R2WHY entity '{a?.Name ?? "?"}' hash={a?.Hash} bsRadius={a?.BSRadius:0.00} scale={e.Scale} " +
                              $"visible={World.Visible.Contains(e)}/{World.Visible.Count} instances={SelProbeMeshCount_R2(e)} " +
                              $"entitiesLive={worldRender.EntitiesLive} meshesInPickList={worldRender.Model.Meshes.Count} " +
                              $"ymap={ymap?.Name ?? "-"} stillInYmap={inYmap} projectOverride={(over == null ? "none" : ReferenceEquals(over, ymap) ? "same file" : "A DIFFERENT FILE " + over.Name)} " +
                              $"projectYmaps={ProjWin.Project?.YmapFiles.Count ?? 0}");
            if (!Project_R2(e.Position, out int px, out int py)) { Console.WriteLine("R2WHY   its pivot is off screen"); return; }
            var ray = camera.GetPickRay(px, py, deviceResources.Width, deviceResources.Height);
            var surface = WorldPickPrecise(ray, out float sd);
            float limit = surface != null ? sd + 0.5f : float.MaxValue;
            bool boxHit = EntityBoxHit(ray, e, a, out float d);
            var centre = a != null ? e.Position + e.Orientation.Multiply(a.BSCenter * e.Scale) : e.Position;
            float centreAlong = Vector3.Dot(centre - ray.Position, ray.Direction);
            var winner = WorldPickEntity(ray);
            Console.WriteLine($"R2WHY   at its own pivot pixel ({px},{py}): surface={(surface?.Archetype?.Name ?? "none")}@{sd:0.0} limit={limit:0.0} " +
                              $"boxHit={boxHit} boxDist={d:0.0} centreAlong={centreAlong:0.0} " +
                              $"rejectedByLimit={(boxHit && d > limit)} rejectedByCentre={(boxHit && limit < float.MaxValue && centreAlong > limit)} " +
                              $"winner={(winner?.Archetype?.Name ?? "none")} winnerRadius={winner?.Archetype?.BSRadius:0.00}");
        }

        private void FinishSelProbe_R2()
        {
            selProbeStage = SelProbeStage.Done;
            if (Environment.GetEnvironmentVariable("RLE_SELPROBEKEEP") != "1")
            {
                int guard = 0;
                while (WorldHistory.CanUndo && guard++ < 64) TryWorldUndo();
            }
            var endPos = selProbeEntity?.Position ?? Vector3.Zero;
            Console.WriteLine($"R2PROBE end: travel from start {(endPos - selProbeStart).Length():0.00} m after {selProbeRound} completed round(s); " +
                              $"back at start after undo={(endPos - selProbeStart).Length() < 0.01f}");
            Console.WriteLine(selProbeFails == 0 ? "R2PROBE PASSED" : $"R2PROBE FAILED ({selProbeFails})");
        }
    }
}

