using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeWalker;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private WorldGizmo creatorGizmo;
        private readonly List<IWorldGizmoTarget> creatorTargets = new List<IWorldGizmoTarget>();
        private object creatorTargetKey;
        private bool creatorGizmoDragging;
        private bool creatorAutoStarted;

        private static Vector4 CrRoom => T(UiTheme.Accent, 0.9f);
        private static Vector4 CrRoomFill => TF(UiTheme.Accent, 0.05f);
        private static Vector4 CrLimbo => T(UiTheme.AccentDim, 0.55f);
        private static readonly Vector4 CrSel = C(1.0f, 0.78f, 0.30f, 1.0f);
        private static readonly Vector4 CrSelFill = F(1.0f, 0.78f, 0.30f, 0.10f);
        private static readonly Vector4 CrPick = C(0.45f, 0.95f, 0.55f, 1.0f);
        private static readonly Vector4 CrPickFill = F(0.45f, 0.95f, 0.55f, 0.25f);

        private MloCreatorPanel Creator => panel.MloCreator;

        partial void DrawMloCreatorHelpers_H5()
        {
            var ui = Creator;
            if (ui == null) return;
            if (!creatorAutoStarted && ui.Session == null &&
                (panel.MloMode || (LightPanel.ForceOpenHeader != null && "MLO Creator".Contains(LightPanel.ForceOpenHeader, StringComparison.OrdinalIgnoreCase))) &&
                scene.HasModel && (DebugMlo == null || debugMloDone))
            {
                creatorAutoStarted = true;
                ui.RequestStartFromScene = true;
            }
            ServiceMloCreatorRequests(ui);
            if (!panel.MloMode) return;
            DrawMloWorkspaceOverlay_J6(ui);
            var s = ui.Session;
            if (s == null || !ui.ShowHelpers) return;
            bool l4 = DrawMloCreatorHelpers_L4(ui, s);

            for (int i = 0; !l4 && i < s.Rooms.Count; i++)
            {
                var r = s.Rooms[i];
                if (!r.IsValid) continue;
                bool sel = ui.SelectedRoom == i && ui.SelectedPortal < 0;
                if (i == 0 && !sel && !ui.ShowAllRooms) continue;
                var col = sel ? CrSel : (i == 0 ? CrLimbo : CrRoom);
                lineRenderer.AddBox(new BoundingBox(r.Min, r.Max), col);
                if (sel || i > 0) AddBoxFill(r.Min, r.Max, sel ? CrSelFill : CrRoomFill);
                if (ui.LabelFor_V19(sel))
                {
                    int n = s.CountInRoom(i);
                    DrawWorldLabel(new Vector3(r.Centre.X, r.Centre.Y, r.Max.Z),
                        $"{i}: {r.Name}  ({n})", sel ? new Vector4(1.0f, 0.85f, 0.45f, 1.0f) : new Vector4(1, 1, 1, 0.92f));
                }
            }

            for (int i = 0; !l4 && i < s.Portals.Count; i++)
            {
                var p = s.Portals[i];
                bool sel = ui.SelectedPortal == i;
                DrawCreatorPortal(p, sel);
                if (ui.LabelFor_V19(sel))
                {
                    string from = p.RoomFrom >= 0 && p.RoomFrom < s.Rooms.Count ? s.Rooms[p.RoomFrom].Name : "?";
                    string to = p.RoomTo >= 0 && p.RoomTo < s.Rooms.Count ? s.Rooms[p.RoomTo].Name : "?";
                    DrawWorldLabel(p.Centre + new Vector3(0, 0, 0.15f), $"P{i}: {from} -> {to}",
                        sel ? new Vector4(1.0f, 0.85f, 0.45f, 1.0f) : new Vector4(UiTheme.AccentBright.X, UiTheme.AccentBright.Y, UiTheme.AccentBright.Z, 0.92f));
                }
            }

            if (ui.LabelMode_V19 == MloCreatorPanel.MloLabels_V19.All && !l4)
            {
                for (int i = 0; i < s.Entities.Count; i++)
                {
                    var e = s.Entities[i];
                    if (!e.Include) continue;
                    bool sel = ui.SelectedEntity == i;
                    var col = sel ? CrSel : (e.Room == 0 ? CrLimbo : CrRoom);
                    float h = sel ? 0.25f : 0.12f;
                    lineRenderer.AddLine(e.Position - Vector3.UnitX * h, e.Position + Vector3.UnitX * h, col);
                    lineRenderer.AddLine(e.Position - Vector3.UnitY * h, e.Position + Vector3.UnitY * h, col);
                    lineRenderer.AddLine(e.Position - Vector3.UnitZ * h, e.Position + Vector3.UnitZ * h, col);
                    if (sel) DrawWorldLabel(e.Position + new Vector3(0, 0, 0.4f), $"{e.Label}  (room {e.Room})", new Vector4(1.0f, 0.85f, 0.45f, 1.0f));
                }
            }

            if (ui.PickingPortalCorners)
            {
                var pts = ui.PickedPoints;
                foreach (var pt in pts) lineRenderer.AddSphere(pt, 0.06f, CrPick, 16);
                for (int i = 1; i < pts.Count; i++) lineRenderer.AddLine(pts[i - 1], pts[i], CrPick);
                Vector3 hp = Vector3.Zero;
                bool hover = ui.SnapActive && ui.Snap.Valid;
                if (hover) hp = ui.Snap.Position;
                else hover = !ImGuiWantsMouse && MloCreatorSession.RayHitScene(scene,
                        camera.GetPickRay(lastMouse.X, lastMouse.Y, deviceResources.Width, deviceResources.Height), out hp, out _);
                if (hover)
                {
                    lineRenderer.AddSphere(hp, 0.05f, CrPick, 12);
                    if (pts.Count > 0) lineRenderer.AddLine(pts[pts.Count - 1], hp, C(0.45f, 0.95f, 0.55f, 0.5f));
                    var preview = PreviewPortalCorners(ui, hp);
                    if (preview != null)
                    {
                        for (int i = 0; i < preview.Length; i++) lineRenderer.AddLine(preview[i], preview[(i + 1) % preview.Length], CrPick);
                        if (preview.Length == 4) triRenderer.AddQuad(preview[0], preview[1], preview[2], preview[3], CrPickFill);
                    }
                }
            }
        }

        private static Vector3[] PreviewPortalCorners(MloCreatorPanel ui, Vector3 hover)
        {
            var pts = new List<Vector3>(ui.PickedPoints) { hover };
            if (ui.PickShape == 1) return pts.Count >= 3 ? MloCreatorSession.RectangleFromPoints(pts[0], pts[1], pts[2]) : null;
            return pts.Count >= 3 ? pts.Take(4).ToArray() : null;
        }

        private void DrawCreatorPortal(MloCreatorPortal p, bool sel)
        {
            var wc = p.Corners;
            int n = wc.Length;
            if (n < 3) return;
            Vector4 pcol = T(UiTheme.Accent, 1.0f), pfill = TF(UiTheme.Accent, 0.20f);
            uint pf = p.Flags;
            if ((pf & 2048u) != 0) { pcol = C(0.30f, 0.85f, 1.0f, 1.0f); pfill = F(0.30f, 0.85f, 1.0f, 0.20f); }
            if ((pf & (4u | 16u | 128u | 256u | 512u | 1024u)) != 0) { pcol = C(0.80f, 0.55f, 1.0f, 1.0f); pfill = F(0.80f, 0.55f, 1.0f, 0.22f); }
            if ((pf & 2u) != 0) { pcol = C(0.45f, 0.85f, 0.55f, 1.0f); pfill = F(0.45f, 0.85f, 0.55f, 0.20f); }
            if (sel) { pcol = CrSel; pfill = F(1.0f, 0.78f, 0.30f, 0.28f); }
            for (int ic = 0; ic < n; ic++)
            {
                int icn = (ic + 1) % n;
                lineRenderer.AddLine(wc[ic], wc[icn], ic == 0 ? C(1.0f, 0.45f, 0.35f, 1.0f) : pcol);
            }
            if (n == 4) triRenderer.AddQuad(wc[0], wc[1], wc[2], wc[3], pfill);
            else for (int ic = 1; ic + 1 < n; ic++) triRenderer.AddTri(wc[0], wc[ic], wc[ic + 1], pfill);
            var c = p.Centre; var nrm = p.Normal;
            float len = 0.45f;
            var side = Vector3.Cross(nrm, Vector3.UnitZ); if (side.LengthSquared() < 1e-6f) side = Vector3.UnitX; side.Normalize();
            var up = Vector3.Cross(side, nrm);
            void Arrow(Vector3 from, Vector3 dir, float l)
            {
                var tip = from + dir * l;
                lineRenderer.AddLine(from, tip, pcol);
                triRenderer.AddCone(tip, tip - dir * (l * 0.35f), side, up, l * 0.12f, pcol, pfill, 12);
            }
            bool oneWay = (pf & 1u) != 0;
            Arrow(c, nrm, len);
            if (!oneWay) Arrow(c, -nrm, len * 0.6f);
        }

        private void AddBoxFill(Vector3 mn, Vector3 mx, Vector4 col)
        {
            var p000 = new Vector3(mn.X, mn.Y, mn.Z); var p100 = new Vector3(mx.X, mn.Y, mn.Z);
            var p010 = new Vector3(mn.X, mx.Y, mn.Z); var p110 = new Vector3(mx.X, mx.Y, mn.Z);
            var p001 = new Vector3(mn.X, mn.Y, mx.Z); var p101 = new Vector3(mx.X, mn.Y, mx.Z);
            var p011 = new Vector3(mn.X, mx.Y, mx.Z); var p111 = new Vector3(mx.X, mx.Y, mx.Z);
            triRenderer.AddQuad(p000, p010, p110, p100, col);
            triRenderer.AddQuad(p001, p101, p111, p011, col);
            triRenderer.AddQuad(p000, p100, p101, p001, col);
            triRenderer.AddQuad(p010, p011, p111, p110, col);
            triRenderer.AddQuad(p000, p001, p011, p010, col);
            triRenderer.AddQuad(p100, p110, p111, p101, col);
        }

        private sealed class CreatorRoomTarget : IWorldGizmoTarget
        {
            public readonly MloCreatorSession Session; public readonly MloCreatorRoom Room;
            public CreatorRoomTarget(MloCreatorSession s, MloCreatorRoom r) { Session = s; Room = r; }
            public object Key => Room;
            public Vector3 Position => Room.Centre;
            public Quaternion Orientation => Quaternion.Identity;
            public Vector3 Scale => Room.Size;
            public WorldWidgetAxis RotationAxes => WorldWidgetAxis.None;
            public bool ScaleLockXY => false;
            public bool CanScale => true;
            public void SetPosition(Vector3 p) { var h = Room.Size * 0.5f; Room.Min = p - h; Room.Max = p + h; Session.AutoAssignRooms(); }
            public void SetOrientation(Quaternion q) { }
            public void SetScale(Vector3 sz)
            {
                var c = Room.Centre;
                var h = new Vector3(Math.Max(sz.X, 0.05f), Math.Max(sz.Y, 0.05f), Math.Max(sz.Z, 0.05f)) * 0.5f;
                Room.Min = c - h; Room.Max = c + h; Session.AutoAssignRooms();
            }
        }

        private sealed class CreatorPortalTarget : IWorldGizmoTarget
        {
            public readonly MloCreatorPortal Portal;
            private Quaternion ori = Quaternion.Identity;
            public CreatorPortalTarget(MloCreatorPortal p) { Portal = p; }
            public object Key => Portal;
            public Vector3 Position => Portal.Centre;
            public Quaternion Orientation => ori;
            public Vector3 Scale => Vector3.One;
            public WorldWidgetAxis RotationAxes => WorldWidgetAxis.XYZ;
            public bool ScaleLockXY => false;
            public bool CanScale => true;
            public void SetPosition(Vector3 p) { var d = p - Portal.Centre; for (int i = 0; i < Portal.Corners.Length; i++) Portal.Corners[i] += d; }
            public void SetOrientation(Quaternion q)
            {
                var delta = q * Quaternion.Invert(ori);
                var c = Portal.Centre;
                for (int i = 0; i < Portal.Corners.Length; i++) Portal.Corners[i] = c + delta.Multiply(Portal.Corners[i] - c);
                ori = q;
            }
            public void SetScale(Vector3 s)
            {
                var c = Portal.Centre;
                float f = Math.Max((s.X + s.Y + s.Z) / 3.0f, 0.05f);
                for (int i = 0; i < Portal.Corners.Length; i++) Portal.Corners[i] = c + (Portal.Corners[i] - c) * f;
            }
        }

        private IList<IWorldGizmoTarget> CreatorGizmoTargets()
        {
            var ui = Creator;
            var s = ui?.Session;
            creatorTargets.Clear();
            bool assetsUp = ui != null && (ui.Page == MloCreatorPanel.PageKind.Assets || ui.AssetsSectionOpen);
            bool propSelected = s != null && ui.FocusKind == 2 && ui.SelectedEntity >= 0 && ui.SelectedEntity < s.Entities.Count;
            if (s == null || !ui.GizmoEnabled || !panel.MloMode ||
                ui.LightEditingActive ||
                (assetsUp && !propSelected))
            { creatorTargetKey = null; return creatorTargets; }
            object key = null;
            var extra = MloWorkspaceGizmoTarget(ui);
            if (extra != null) key = extra.Key;
            else if (ui.SelectedPortal >= 0 && ui.SelectedPortal < s.Portals.Count) key = s.Portals[ui.SelectedPortal];
            else if (ui.SelectedRoom >= 0 && ui.SelectedRoom < s.Rooms.Count) key = s.Rooms[ui.SelectedRoom];
            if (key == null) { creatorTargetKey = null; return creatorTargets; }
            if (creatorGizmoDragging && !Equals(creatorTargetKey, key)) { creatorTargetKey = null; return creatorTargets; }
            if (creatorGizmo == null)
            {
                creatorGizmo = new WorldGizmo();
                creatorGizmo.DragBegan += () => { creatorGizmoDragging = true; Creator.Session?.PushUndo(); MloDragBegan_N3(); };
                creatorGizmo.DragEnded += () => { creatorGizmoDragging = false; };
                creatorGizmo.TargetChanged += MloTargetChanged_N3;
            }
            creatorGizmo.Mode = key is MloCreatorPortal ? WorldGizmoMode.Translate : (ui.GizmoMode == 1 ? WorldGizmoMode.Scale : WorldGizmoMode.Translate);
            if (key is MloCreatorPortal && ui.GizmoMode == 1) creatorGizmo.Mode = WorldGizmoMode.Rotate;
            if (extra != null && !(key is MloCreatorEntity)) creatorGizmo.Mode = WorldGizmoMode.Translate;
            if (key is MloCreatorEntity) creatorGizmo.Mode = MloEntityGizmoMode_N3(ui);
            if (!Equals(creatorTargetKey, key) || creatorTargets.Count == 0)
            {
                creatorTargetKey = key;
            }
            creatorTargets.Add(extra ?? (key is MloCreatorPortal cp ? new CreatorPortalTarget(cp) : new CreatorRoomTarget(s, (MloCreatorRoom)key)));
            return creatorTargets;
        }

        private IWorldGizmoTarget creatorLiveTarget;
        private IList<IWorldGizmoTarget> CreatorLiveTargets()
        {
            var t = CreatorGizmoTargets();
            if (t.Count == 0) { creatorLiveTarget = null; return t; }
            if (creatorLiveTarget == null || !Equals(creatorLiveTarget.Key, t[0].Key)) creatorLiveTarget = t[0];
            t[0] = creatorLiveTarget;
            return t;
        }

        partial void DrawMloCreatorGizmo_H5(SharpDX.Direct3D11.DeviceContext context)
        {
            if (!panel.MloMode || Creator?.Session == null) return;
            var targets = CreatorLiveTargets();
            if (targets.Count == 0 && !(creatorGizmo?.Dragging ?? false)) return;
            creatorGizmo.Draw(lineRenderer, triRenderer, camera, targets, GizmoStyle.OccludedAlpha);
            lineRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.DepthDisabled);
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthDisabled);
            bool depthOk = deviceResources.SampleCount <= 1 && deviceResources.DepthDSV != null;
            if (depthOk) context.OutputMerger.SetTargets(deviceResources.DepthDSV, deviceResources.BackbufferRTV);
            creatorGizmo.Draw(lineRenderer, triRenderer, camera, targets, 1.0f);
            var ds = depthOk ? CommonStates.DepthReadOnly : CommonStates.DepthDisabled;
            lineRenderer.Flush(context, camera.ViewProjMatrix, ds);
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, ds);
            if (depthOk) deviceResources.BeginBackbuffer();
        }

        partial void MloCreatorMouseDown_H5(int x, int y, bool shift, bool alt, ref bool consumed)
        {
            if (!panel.MloMode || Creator?.Session == null) return;
            if (Creator.PickingPortalCorners || Creator.PickingRoomCorners || Creator.SnapMode || Creator.PickingRoomMesh) return;
            if (Creator.CornerMode && CornerHandleAt(Creator, x, y) is int c && c >= 0 && c != Creator.SelectedCorner) { Creator.SelectedCorner = c; consumed = true; return; }
            var targets = CreatorLiveTargets();
            if (targets.Count == 0) return;
            if (MloGeometryBeatsGizmo_S1(Creator, x, y)) return;
            if (shift && Creator.EntityTool == 1 && creatorGizmo.HitsHandle_V33(camera, targets, x, y, deviceResources.Width, deviceResources.Height))
            {
                Creator.InvokeDuplicate_R1();
                targets = CreatorLiveTargets();
            }
            consumed = creatorGizmo.MouseDown(camera, targets, x, y, deviceResources.Width, deviceResources.Height, shift, alt);
        }

        partial void MloCreatorMouseMove_H5(int x, int y)
        {
            if (creatorGizmo == null || !panel.MloMode) return;
            var targets = CreatorLiveTargets();
            if (!creatorGizmo.Dragging && targets.Count == 0) return;
            creatorGizmo.MouseMove(camera, x, y, deviceResources.Width, deviceResources.Height);
            if (creatorGizmo.Dragging && Creator.SnapActive)
            {
                var ray = camera.GetPickRay(x, y, deviceResources.Width, deviceResources.Height);
                Creator.Snap = MloVertexSnap.Find(scene.AllMeshes, camera, ray, x, y, deviceResources.Width, deviceResources.Height, out var hit) ? hit : default;
                MloSnapGizmoDrag(Creator, targets);
            }
        }

        partial void MloCreatorMouseUp_H5(int x, int y, bool wasClick, ref bool handled)
        {
            if (creatorGizmo != null && creatorGizmo.Dragging) { creatorGizmo.MouseUp(); handled = true; return; }
            if (!wasClick || !panel.MloMode) return;
            var ui = Creator;
            if (ui == null) return;
            if (MloSnapModeClick_L4(ui, x, y)) { handled = true; return; }
            if (ui.Session == null || !ui.PickingPortalCorners) { handled = MloWorkspaceClick(ui, x, y); return; }
            if (!MloClickPoint(ui, x, y, out var hit, out _))
            {
                ui.SetStatus("Click ON the geometry (a wall, a floor) - nothing was under the cursor.", true);
                handled = true;
                return;
            }
            handled = true;
            AddPortalCornerPoint(ui, hit);
        }

        private void AddPortalCornerPoint(MloCreatorPanel ui, Vector3 hit)
        {
            ui.PickedPoints.Add(hit);
            int need = ui.PickShape == 1 ? 3 : 4;
            if (ui.PickedPoints.Count < need)
            {
                ui.SetStatus($"Corner {ui.PickedPoints.Count} of {need} placed.");
                return;
            }
            var s = ui.Session;
            s.PushUndo("Portal from vertices");
            MloCreatorPortal p;
            if (ui.PickShape == 1)
            {
                var corners = MloCreatorSession.RectangleFromPoints(ui.PickedPoints[0], ui.PickedPoints[1], ui.PickedPoints[2]);
                p = s.AddPortal(0, 0, corners);
                var c = p.Centre; var n = p.Normal;
                p.RoomFrom = s.RoomAt(c - n * 0.3f); p.RoomTo = s.RoomAt(c + n * 0.3f);
                if (p.RoomFrom == p.RoomTo) p.RoomTo = p.RoomFrom == 0 ? Math.Min(1, s.Rooms.Count - 1) : 0;
                s.OrientPortal(p);
            }
            else p = s.AddPortalFromPoints(ui.PickedPoints);
            ui.SelectPortal(s.Portals.Count - 1);
            ui.PickedPoints.Clear();
            ui.PickingPortalCorners = false;
            ui.SetStatus($"Portal {ui.SelectedPortal} placed: {s.Rooms[p.RoomFrom].Name} -> {s.Rooms[p.RoomTo].Name}. Set From/To if that guess is wrong.");
        }

        private static int RoomAt(MloCreatorSession s, Vector3 p)
        {
            int best = 0; float bestVol = float.MaxValue;
            for (int i = 1; i < s.Rooms.Count; i++)
            {
                var r = s.Rooms[i];
                if (!r.IsValid || !r.Contains(p)) continue;
                if (r.Volume < bestVol) { bestVol = r.Volume; best = i; }
            }
            return best;
        }

        private void ServiceMloCreatorRequests(MloCreatorPanel ui)
        {
            ui.DoUndoRedo();
            ServiceImportRemoval_R1();
            ServiceShellBuildEnv_R1(ui);
            if (ui.RequestOpenShell) { ui.RequestOpenShell = false; DoOpenDialog(); }
            if (ui.RequestAddProps) { ui.RequestAddProps = false; DoAddFileDialog(); }
            if (ui.RequestImportShellYbn_V34)
            {
                ui.RequestImportShellYbn_V34 = false;
                try
                {
                    var sess = ui.Session;
                    if (sess == null) ui.SetStatus("No interior yet - press New first.", true);
                    else
                    {
                        using var dlg = new OpenFileDialog
                        {
                            Filter = "Collision (*.ybn)|*.ybn|All files|*.*",
                            Title = "The collision for this interior",
                        };
                        var seed = sess.ShellFile?.Path ?? sess.LastSavedPath;
                        if (!string.IsNullOrEmpty(seed)) dlg.InitialDirectory = Path.GetDirectoryName(seed);
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            if (sess.ImportShellYbn_V34(dlg.FileName, out var why))
                            {
                                ui.SetStatus(sess.ShellYbnNote_V34);
                                Console.WriteLine("MLOYBN " + sess.ShellYbnNote_V34);
                            }
                            else ui.SetStatus("Not imported: " + why, true);
                        }
                    }
                }
                catch (Exception ex) { ui.SetStatus("Could not import it: " + ex.Message, true); }
            }
            if (ui.RequestImportYtyp) { ui.RequestImportYtyp = false; DoImportYtyp(); }
            ServiceMloCreatorProject_K1(ui);
            ServiceMloNew_T1(ui);

            if (ui.RequestStartFromScene)
            {
                ui.RequestStartFromScene = false;
                ui.Session = MloCreatorSession.FromScene(scene);
                ui.SelectRoom(ui.Session.Rooms.Count > 1 ? 1 : 0);
                ui.Session.YmapDefaultSets.Clear();
                var s0 = ui.Session;
                ui.SetStatus(s0.SourceArchetype != null
                    ? $"From {s0.SourceArchetype.Name}: {s0.Rooms.Count} rooms, {s0.Portals.Count} portals, {s0.Entities.Count} entities, {s0.EntitySets.Count} sets."
                    : $"{s0.Entities.Count} entit{(s0.Entities.Count == 1 ? "y" : "ies")} from the loaded files" +
                      (s0.ShellFile != null ? $", shell {s0.ShellFile.Name}." : ". Pick the shell if one of them is the interior."));
            }
            var s = ui.Session;
            if (s == null) return;

            ServiceMloEdit_N3(ui);
            ServiceMloAssets_L3(ui);
            ServiceMloAddEntity_V32(ui);
            ServiceMloShell_M1(ui);
            ServiceMloShellBuild_R1(ui);

            if (ui.RequestAddRoomAtView)
            {
                ui.RequestAddRoomAtView = false;
                s.PushUndo();
                var c = camera.Target;
                var r = s.AddRoom(NextRoomName(s), c - new Vector3(2, 2, 1.5f), c + new Vector3(2, 2, 1.5f));
                s.AutoAssignRooms();
                ui.SelectRoom(s.Rooms.Count - 1);
                ui.SetStatus($"Room {ui.SelectedRoom} '{r.Name}' added at the view. Move / size it with the gizmo.");
            }
            if (ui.RequestCaptureFromSelection)
            {
                ui.RequestCaptureFromSelection = false;
                var files = scene.SelectedFiles.Count > 0 ? scene.SelectedFiles.ToList()
                          : (scene.ActiveFile != null ? new List<LoadedFile> { scene.ActiveFile } : new List<LoadedFile>());
                if (files.Count == 0) ui.SetStatus("Select one or more props first (Props list, or click them).", true);
                else
                {
                    s.PushUndo();
                    var r = s.AddRoomAround(files, NextRoomName(s));
                    if (r == null) ui.SetStatus("The selected props have no geometry to bound.", true);
                    else
                    {
                        s.AutoAssignRooms();
                        ui.SelectRoom(s.Rooms.Count - 1);
                        ui.SetStatus($"Room {ui.SelectedRoom} '{r.Name}' around {files.Count} prop{(files.Count == 1 ? "" : "s")}.");
                    }
                }
            }
            if (ui.RequestAssignSelectedPropsToRoom)
            {
                ui.RequestAssignSelectedPropsToRoom = false;
                var room = ui.CurrentRoom;
                if (room == null) ui.SetStatus("Select a room first.", true);
                else
                {
                    s.PushUndo();
                    int n = 0;
                    foreach (var e in s.Entities)
                        if (e.SourceFile != null && scene.IsFileSelected(e.SourceFile)) { e.RoomOverride = ui.SelectedRoom; n++; }
                    ui.SetStatus(n == 0 ? "No selected props among the entities." : $"{n} entit{(n == 1 ? "y" : "ies")} put in room {ui.SelectedRoom}.", n == 0);
                }
            }
            if (ui.RequestRenameResolve_V67 >= 0)
            {
                int ri = ui.RequestRenameResolve_V67;
                ui.RequestRenameResolve_V67 = -1;
                if (s != null && ri >= 0 && ri < s.Entities.Count)
                {
                    var re = s.Entities[ri];
                    var stem = re.SourceFile?.Path != null
                        ? Path.GetFileNameWithoutExtension(re.SourceFile.Path) : null;
                    var want = (re.ArchetypeName ?? "").Trim();
                    if (!re.FromImportedMlo && !re.IsShell_V33 && want.Length > 0 &&
                        !string.Equals(stem, want, StringComparison.OrdinalIgnoreCase))
                    {
                        s.PushUndo("Rename " + (stem ?? "entity") + " to " + want);
                        if (re.SourceFile != null) mloEntityFiles_N3.Add(re.SourceFile);
                        re.SourceFile = null;
                        ResolveTypedEntities_L3(ui);
                        SyncSceneToEntities_N3(ui);
                        ui.SetStatus(re.SourceFile != null
                            ? $"Model swapped to {want}."
                            : $"No model called {want} in the archives or your folders - the name is kept, nothing is drawn.",
                            re.SourceFile == null);
                    }
                }
            }
            if (ui.RequestAddPortalAtView)
            {
                ui.RequestAddPortalAtView = false;
                s.PushUndo();
                var c = camera.Target;
                var toCam = camera.Position - c; toCam.Z = 0;
                var along = Vector3.Cross(Vector3.UnitZ, toCam.LengthSquared() > 1e-6f ? Vector3.Normalize(toCam) : Vector3.UnitY);
                var p = s.AddPortal(0, Math.Min(1, s.Rooms.Count - 1), MloCreatorSession.RectangleAt(c, along, 0.6f, 1.1f));
                p.RoomFrom = RoomAt(s, c - p.Normal * 0.3f); p.RoomTo = RoomAt(s, c + p.Normal * 0.3f);
                if (p.RoomFrom == p.RoomTo) p.RoomTo = p.RoomFrom == 0 ? Math.Min(1, s.Rooms.Count - 1) : 0;
                ui.SelectPortal(s.Portals.Count - 1);
                ui.SetStatus($"Portal {ui.SelectedPortal} added at the view, facing the camera. Move it with the gizmo; set From/To.");
            }
            if (ui.RequestFrameSelected)
            {
                ui.RequestFrameSelected = false;
                if (ui.CurrentPortal != null)
                {
                    var p = ui.CurrentPortal;
                    camera.Target = p.Centre;
                    camera.Distance = Math.Clamp((p.Corners[0] - p.Centre).Length() * 3.0f, 1.5f, 20.0f);
                    camera.SnapSmoothing();
                }
                else if (ui.CurrentRoom != null)
                {
                    var r = ui.CurrentRoom;
                    camera.Target = r.Centre;
                    camera.Distance = Math.Clamp(r.Size.Length() * 0.9f, 1.5f, 60.0f);
                    camera.SnapSmoothing();
                }
            }
            if (ui.RequestExportAll_V31)
            {
                ui.RequestExportAll_V31 = false;
                try
                {
                    var problems = s.Validate();
                    if (problems.Count > 0) throw new InvalidOperationException(problems[0]);
                    using var dlg = new FolderBrowserDialog
                    {
                        Description = "A folder for the interior: its .ytyp, its .ymap and the _manifest.ymf",
                        UseDescriptionForTitle = true,
                    };
                    if (!string.IsNullOrEmpty(s.LastSavedPath)) dlg.SelectedPath = Path.GetDirectoryName(s.LastSavedPath);
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var r = s.ExportForGame_V31(dlg.SelectedPath);
                        ui.LastExport_V31 = r;
                        if (!r.Ok) { ui.SetStatus("Export failed: " + r.Error + " - details in Help > Show log", true); AppLog_U21.Error("MLO export failed: " + (r.ErrorDetail_U21 ?? r.Error)); }
                        else ui.SetStatus($"Wrote {r.Written.Count} file(s) to {Path.GetFileName(dlg.SelectedPath)}" +
                                          (r.Missing.Count > 0 ? $" - {r.Missing.Count} still to add: {r.Missing[0]}" : " - ready to pack."));
                        foreach (var w in r.Written) Console.WriteLine("MLOEXPORT wrote  " + w);
                        foreach (var m in r.Missing) Console.WriteLine("MLOEXPORT needs  " + m);
                    }
                }
                catch (Exception ex) { ui.SetStatus("Export failed: " + ex.Message + " - details in Help > Show log", true); AppLog_U21.Error("MLO export", ex); }
            }
            if (ui.RequestSaveYtyp)
            {
                ui.RequestSaveYtyp = false;
                try
                {
                    using var dlg = new SaveFileDialog
                    {
                        Filter = "Map types (*.ytyp)|*.ytyp",
                        FileName = s.Name.Trim().ToLowerInvariant() + ".ytyp",
                        Title = "Save the interior's .ytyp",
                    };
                    if (!string.IsNullOrEmpty(s.LastSavedPath)) dlg.InitialDirectory = Path.GetDirectoryName(s.LastSavedPath);
                    else if (scene.FilePath != null) dlg.InitialDirectory = Path.GetDirectoryName(scene.FilePath);
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var t = s.SaveYtyp(dlg.FileName);
                        ui.SetStatus($"Saved {Path.GetFileName(dlg.FileName)}: {s.Rooms.Count} rooms, {s.Portals.Count} portals, " +
                                     $"{(t.AllArchetypes.OfType<MloArchetype>().FirstOrDefault()?.entities?.Length ?? 0)} entities.");
                    }
                }
                catch (Exception ex) { ui.SetStatus("Save failed: " + ex.Message + " - details in Help > Show log", true); AppLog_U21.Error("MLO Creator .ytyp save", ex); }
            }
            if (ui.RequestAddToProject)
            {
                ui.RequestAddToProject = false;
                try
                {
                    var problems = s.Validate();
                    if (problems.Count > 0) throw new InvalidOperationException(problems[0]);
                    var t = s.BuildYtyp(s.Name.Trim().ToLowerInvariant() + ".ytyp");
                    var err = MloEditor.Validate(t);
                    if (!string.IsNullOrEmpty(err)) throw new InvalidOperationException(err);
                    t.FilePath = !string.IsNullOrEmpty(s.LastSavedPath) ? s.LastSavedPath : t.Name;
                    if (projCtl != null && projCtl.AddGameFileToProject(t))
                    {
                        ProjectRevealAdded(t.AllArchetypes.FirstOrDefault(), t, t.Name);
                        ui.SetStatus($"{t.Name} added to the project (Save All in the Project window writes it).");
                    }
                    else ui.SetStatus("Could not add to the project (already there?).", true);
                }
                catch (Exception ex) { ui.SetStatus("Add to project failed: " + ex.Message, true); }
            }
            if (ui.RequestExportYmap)
            {
                ui.RequestExportYmap = false;
                try
                {
                    string stem = string.IsNullOrWhiteSpace(s.YmapName) ? s.Name.Trim().ToLowerInvariant() + "_placement" : s.YmapName.Trim();
                    using var dlg = new SaveFileDialog
                    {
                        Filter = "Map placement (*.ymap)|*.ymap",
                        FileName = stem + ".ymap",
                        Title = "Export a .ymap placing the interior",
                    };
                    if (!string.IsNullOrEmpty(s.LastSavedPath)) dlg.InitialDirectory = Path.GetDirectoryName(s.LastSavedPath);
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var rot = Quaternion.RotationAxis(Vector3.UnitZ, MathUtil.DegreesToRadians(s.YmapHeadingDeg));
                        s.SaveYmap(dlg.FileName, new Vector3(s.YmapPosition.X, s.YmapPosition.Y, s.YmapPosition.Z), rot,
                                   (uint)Math.Max(s.YmapGroupId, 0), (uint)Math.Max(s.YmapFloorId, 0), s.YmapDefaultSets);
                        ui.SetStatus($"Saved {Path.GetFileName(dlg.FileName)}: one MLO instance of {s.Name} at {s.YmapPosition.X:0.0}, {s.YmapPosition.Y:0.0}, {s.YmapPosition.Z:0.0}.");
                    }
                }
                catch (Exception ex) { ui.SetStatus("Export failed: " + ex.Message + " - details in Help > Show log", true); AppLog_U21.Error("MLO export", ex); }
            }
        }

        private static string NextRoomName(MloCreatorSession s)
        {
            int n = s.Rooms.Count;
            string name;
            do { name = "room_" + n++; } while (s.Rooms.Any(r => r.Name == name));
            return name;
        }

        partial void MloCreatorTest_H5(Action<string, bool, string> check)
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "rle_mlocreator");
                Directory.CreateDirectory(dir);
                string ydr = Path.Combine(dir, "light_test_scene.ydr");
                if (!File.Exists(ydr)) TestSceneGenerator.Run(ydr);
                check("creator: test scene generated", File.Exists(ydr), ydr);

                string ydr2 = Path.Combine(dir, "prop_rle_second.ydr");
                File.Copy(ydr, ydr2, true);
                scene.CloseAllFiles();
                LoadFile(ydr);
                LoadFile(ydr2);
                bool twoFiles = scene.Files.Count >= 2;
                check("creator: two files open", twoFiles, $"{scene.Files.Count} files");
                if (!twoFiles) return;
                var f2 = scene.Files[1];
                f2.Placement = Matrix.Translation(6.0f, 0.5f, 0.0f); f2.HasPlacement = true;

                var ui = Creator;
                ui.Session = MloCreatorSession.FromScene(scene);
                var s = ui.Session;
                bool hadShell = s.ShellFile != null;
                s.ShellFile = null; s.AssetLess = true;
                s.Entities.Clear();
                s.AddEntityFromFile(scene.Files[0]);
                s.AddEntityFromFile(scene.Files[1]);
                check("creator: the biggest file was offered as the shell", hadShell, hadShell ? "yes" : "no");
                s.Name = "rle_creator_test";
                s.TextureDictionary = "rle_creator_test";
                s.BBMin = new Vector3(-10, -10, -2); s.BBMax = new Vector3(12, 10, 6);
                s.Rooms[0].Min = s.BBMin; s.Rooms[0].Max = s.BBMax;
                check("creator: entities from files", s.Entities.Count == 2, $"{s.Entities.Count} entities: {string.Join(", ", s.Entities.Select(e => e.Label))}");

                var rA = s.AddRoom("hall", new Vector3(-3, -3, -1), new Vector3(3, 3, 3));
                rA.Timecycle = "int_gasstation"; rA.Flags = 4; rA.Blend = 0.75f; rA.FloorId = 1;
                var rB = s.AddRoom("office", new Vector3(3.5f, -3, -1), new Vector3(9, 3, 3));
                rB.SecondaryTimecycle = "int_office_lod"; rB.ExteriorVisibilityDepth = 2;
                var quad = MloCreatorSession.RectangleAt(new Vector3(3.25f, 0, 1.0f), Vector3.UnitY, 0.6f, 1.1f);
                var portal = s.AddPortal(1, 2, quad);
                portal.Flags = 1 | 4; portal.MirrorPriority = 2; portal.Opacity = 50; portal.AudioOcclusion = 3;
                s.AddEntitySet("furnished");
                s.AutoAssignRooms();
                check("creator: entity 0 auto -> hall (1)", s.Entities[0].Room == 1, $"room {s.Entities[0].Room} (auto {s.Entities[0].AutoRoom})");
                check("creator: entity 1 auto -> office (2)", s.Entities[1].Room == 2, $"room {s.Entities[1].Room} (auto {s.Entities[1].AutoRoom})");
                s.Entities.Add(new MloCreatorEntity { ArchetypeName = "prop_chair_01a", Position = new Vector3(6, 0, 0.5f), EntitySet = "furnished" });
                s.AutoAssignRooms();
                s.Entities[1].RoomOverride = 1;
                portal.Attached.Add(1);
                s.PushUndo();
                rA.Max = new Vector3(4, 4, 4);
                s.Undo();
                check("creator: undo restores the room box", (s.Rooms[1].Max - new Vector3(3, 3, 3)).Length() < 1e-5f, $"{s.Rooms[1].Max}");
                rA = s.Rooms[1]; rB = s.Rooms[2]; portal = s.Portals[0];

                var problems = s.Validate();
                check("creator: session validates", problems.Count == 0, string.Join(" | ", problems));

                string ytypPath = Path.Combine(dir, "rle_creator_test.ytyp");
                var written = s.SaveYtyp(ytypPath);
                check("creator: ytyp written", File.Exists(ytypPath) && new FileInfo(ytypPath).Length > 512, $"{new FileInfo(ytypPath).Length} bytes");

                var rt = new YtypFile();
                rt.Load(File.ReadAllBytes(ytypPath));
                var mlo = rt.AllArchetypes?.OfType<MloArchetype>().FirstOrDefault();
                check("creator: MLO archetype reads back", mlo != null, $"{rt.AllArchetypes?.Length ?? 0} archetypes");
                if (mlo == null) return;
                check("creator: archetype name", mlo.Hash == JenkHash.GenHash("rle_creator_test"), mlo.Name ?? "?");
                check("creator: archetype box", (mlo._BaseArchetypeDef.bbMin - s.BBMin).Length() < 1e-4f && (mlo._BaseArchetypeDef.bbMax - s.BBMax).Length() < 1e-4f,
                      $"{mlo._BaseArchetypeDef.bbMin} .. {mlo._BaseArchetypeDef.bbMax}");
                check("creator: assetless", mlo._BaseArchetypeDef.assetType == rage__fwArchetypeDef__eAssetType.ASSET_TYPE_ASSETLESS, mlo._BaseArchetypeDef.assetType.ToString());
                check("creator: 3 rooms", (mlo.rooms?.Length ?? 0) == 3, $"{mlo.rooms?.Length ?? 0}");
                if ((mlo.rooms?.Length ?? 0) == 3)
                {
                    var r0 = mlo.rooms[0]; var r1 = mlo.rooms[1]; var r2 = mlo.rooms[2];
                    check("creator: room names", r0.RoomName == "limbo" && r1.RoomName == "hall" && r2.RoomName == "office", $"{r0.RoomName}, {r1.RoomName}, {r2.RoomName}");
                    check("creator: room 1 box", (r1._Data.bbMin - rA.Min).Length() < 1e-4f && (r1._Data.bbMax - rA.Max).Length() < 1e-4f, $"{r1._Data.bbMin} .. {r1._Data.bbMax}");
                    check("creator: room 1 fields", r1._Data.timecycleName.Hash == JenkHash.GenHash("int_gasstation") && r1._Data.flags == 4 &&
                          Math.Abs(r1._Data.blend - 0.75f) < 1e-5f && r1._Data.floorId == 1,
                          $"tc {r1._Data.timecycleName} flags {r1._Data.flags} blend {r1._Data.blend} floor {r1._Data.floorId}");
                    check("creator: room 2 fields", r2._Data.secondaryTimecycleName.Hash == JenkHash.GenHash("int_office_lod") && r2._Data.exteriorVisibiltyDepth == 2,
                          $"tc2 {r2._Data.secondaryTimecycleName} depth {r2._Data.exteriorVisibiltyDepth}");
                    check("creator: portal counts", r1._Data.portalCount == 1 && r2._Data.portalCount == 1 && r0._Data.portalCount == 0,
                          $"{r0._Data.portalCount}/{r1._Data.portalCount}/{r2._Data.portalCount}");
                    check("creator: 2 room entities", (mlo.entities?.Length ?? 0) == 2, $"{mlo.entities?.Length ?? 0}");
                    check("creator: hall holds both", (r1.AttachedObjects?.Length ?? 0) == 2 && (r2.AttachedObjects?.Length ?? 0) == 0 && (r0.AttachedObjects?.Length ?? 0) == 0,
                          $"limbo {r0.AttachedObjects?.Length ?? 0}, hall {r1.AttachedObjects?.Length ?? 0}, office {r2.AttachedObjects?.Length ?? 0}");
                    if ((mlo.entities?.Length ?? 0) == 2)
                    {
                        var e1 = mlo.entities[1]._Data;
                        check("creator: entity 1 position", (e1.position - new Vector3(6.0f, 0.5f, 0.0f)).Length() < 1e-4f, $"{e1.position}");
                        check("creator: entity 1 archetype", e1.archetypeName.Hash == JenkHash.GenHash("prop_rle_second"), e1.archetypeName.ToString());
                    }
                }
                check("creator: 1 portal", (mlo.portals?.Length ?? 0) == 1, $"{mlo.portals?.Length ?? 0}");
                if ((mlo.portals?.Length ?? 0) == 1)
                {
                    var p = mlo.portals[0];
                    check("creator: portal rooms 1 -> 2", p._Data.roomFrom == 1 && p._Data.roomTo == 2, $"{p._Data.roomFrom} -> {p._Data.roomTo}");
                    check("creator: portal fields", p._Data.flags == 5 && p._Data.mirrorPriority == 2 && p._Data.opacity == 50 && p._Data.audioOcclusion == 3,
                          $"flags {p._Data.flags} mp {p._Data.mirrorPriority} op {p._Data.opacity} ao {p._Data.audioOcclusion}");
                    bool corners = (p.Corners?.Length ?? 0) == 4;
                    if (corners) for (int i = 0; i < 4; i++) corners &= (p.Corners[i].XYZ() - quad[i]).Length() < 1e-4f;
                    check("creator: portal corners", corners, $"{p.Corners?.Length ?? 0} corners, [0] {(p.Corners != null && p.Corners.Length > 0 ? p.Corners[0].XYZ().ToString() : "-")}");
                    check("creator: portal attachment", (p.AttachedObjects?.Length ?? 0) == 1 && p.AttachedObjects[0] == 1, $"{string.Join(",", p.AttachedObjects ?? Array.Empty<uint>())}");
                }
                check("creator: entity set", (mlo.entitySets?.Length ?? 0) == 1 && mlo.entitySets[0]._Data.name.Hash == JenkHash.GenHash("furnished") &&
                      (mlo.entitySets[0].Entities?.Length ?? 0) == 1 && mlo.entitySets[0].Locations?.Length == 1 && mlo.entitySets[0].Locations[0] == 2,
                      $"{mlo.entitySets?.Length ?? 0} sets, {mlo.entitySets?.FirstOrDefault()?.Entities?.Length ?? 0} entities, loc {(mlo.entitySets?.FirstOrDefault()?.Locations?.FirstOrDefault())}");
                int plain = rt.AllArchetypes.Count(a => a != null && !(a is MloArchetype));
                check("creator: prop archetypes written", plain == 2, $"{plain} CBaseArchetypeDefs");
                check("creator: MloEditor.Validate clean", string.IsNullOrEmpty(MloEditor.Validate(rt)), MloEditor.Validate(rt) ?? "");

                string ymapPath = Path.Combine(dir, "rle_creator_test.ymap");
                var rotq = Quaternion.RotationAxis(Vector3.UnitZ, 0.5f);
                s.SaveYmap(ymapPath, new Vector3(100, 200, 30), rotq, 3, 1, new[] { "furnished" });
                var ym = new YmapFile();
                RpfFile.LoadResourceFile(ym, File.ReadAllBytes(ymapPath), 2);
                var inst = ym.CMloInstanceDefs;
                check("creator: ymap has the MLO instance", (inst?.Length ?? 0) == 1, $"{inst?.Length ?? 0} instances, {ym.CEntityDefs?.Length ?? 0} plain entities");
                if ((inst?.Length ?? 0) == 1)
                {
                    var d = inst[0];
                    check("creator: instance archetype + position", d.CEntityDef.archetypeName.Hash == JenkHash.GenHash("rle_creator_test") &&
                          (d.CEntityDef.position - new Vector3(100, 200, 30)).Length() < 1e-3f, $"{d.CEntityDef.archetypeName} at {d.CEntityDef.position}");
                    var q = new Quaternion(d.CEntityDef.rotation.X, d.CEntityDef.rotation.Y, d.CEntityDef.rotation.Z, d.CEntityDef.rotation.W);
                    check("creator: instance rotation raw", Math.Abs(Quaternion.Dot(q, rotq)) > 0.9999f, $"dot {Quaternion.Dot(q, rotq):0.0000}");
                    check("creator: instance group/floor", d.groupId == 3 && d.floorId == 1, $"group {d.groupId} floor {d.floorId}");
                    var sets = ym.MloEntities?.FirstOrDefault()?.MloInstance?.defaultEntitySets;
                    check("creator: instance default sets", sets != null && sets.Length == 1 && sets[0].Hash == JenkHash.GenHash("furnished"), $"{sets?.Length ?? 0} sets");
                }
                check("creator: ymap content flags", (ym.CMapData.contentFlags & 8) != 0, $"contentFlags {ym.CMapData.contentFlags}");

                ui.SelectRoom(1);
                ui.SetStatus("--seqtest: rooms + portal written and read back.");
                Console.WriteLine($"  MLOCREATOR {ytypPath}  {ymapPath}");
            }
            catch (Exception ex)
            {
                check("creator: no exception", false, ex.ToString());
            }
        }
    }
}

