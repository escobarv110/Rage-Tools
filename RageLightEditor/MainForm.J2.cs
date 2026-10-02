using System;
using System.Collections.Generic;
using CodeWalker;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private const float WorldLightNearRange = 60.0f;
        private const int WorldLightNearMarkerCap = 800;

        private void DrawWorldLightMarkers_J2() => CollectLightIcons_U26();

        private void OnProjectClosing_J2(CwProject p)
        {
            if (p == null) return;
            var s = WorldEdit.Selection;
            var ownerYmap = s.OwnerYmap ?? s.LightEntity?.Ymap ?? s.MloEntityDef?.Ymap;
            bool inProject = ownerYmap != null && p.YmapFiles.Contains(ownerYmap);
            if (!inProject && s.EntityDef?.MloParent?.Ymap != null) inProject = p.YmapFiles.Contains(s.EntityDef.MloParent.Ymap);
            if (inProject) { WorldEdit.Deselect(); worldHoverSel = WorldSelection.Empty; }
            int forgotten = 0;
            foreach (var y in p.YmapFiles) if (WorldEdit.ForgetDirty(y)) forgotten++;
            WorldEdit.LastStatus = $"closed {p.Name}" + (inProject ? " (the selection was in it)" : "") + (forgotten > 0 ? $", {forgotten} unsaved ymap{(forgotten == 1 ? "" : "s")} left to the project" : "");
            Console.WriteLine($"PROJECTCLOSE {p.Name}: selectionCleared={inProject} dirtyForgotten={forgotten} ymaps={p.YmapFiles.Count} ytyps={p.YtypFiles.Count}");
            WorldRevertFiles_V22(p.YmapFiles.Where(y => y != null && y.RpfFileEntry != null).Concat(WorldEdit.Dirty), "project closed");
        }

        private bool closeProjectEnvDone;
        private void TickCloseProjectEnv_J2()
        {
            if (closeProjectEnvDone || !worldBuilt || screenshotPath == null) return;
            var env = Environment.GetEnvironmentVariable("RLE_CLOSEPROJECT");
            if (string.IsNullOrEmpty(env)) { closeProjectEnvDone = true; return; }
            if (!int.TryParse(env, out int at) || at <= 1) at = 462;
            if (worldWarmup < at) return;
            closeProjectEnvDone = true;
            var p = ProjWin.Project;
            bool unsaved = p != null && p.AnyUnsaved;
            Console.WriteLine($"CLOSEPROJECT before: project={(p?.Name ?? "none")} ymaps={p?.YmapFiles.Count ?? 0} overrides={(World.ProjectOverrides?.Count ?? 0)} selection={WorldEdit.Selection.TypeName}/{WorldEdit.Selection.EntityDef?.Archetype?.Name ?? "-"} dirty={WorldEdit.DirtyCount} unsaved={unsaved} (the prompt {(unsaved ? "WOULD ask Save / Don't save / Cancel" : "is skipped")})");
            if (unsaved)
            {
                p.HasChanged = false;
                foreach (var y in p.YmapFiles) if (y != null) y.HasChanged = false;
                foreach (var t in p.YtypFiles) if (t != null) t.HasChanged = false;
                foreach (var f in p.YndFiles) if (f != null) f.HasChanged = false;
                foreach (var f in p.YnvFiles) if (f != null) f.HasChanged = false;
                foreach (var f in p.TrainsFiles) if (f != null) f.HasChanged = false;
                foreach (var f in p.ScenarioFiles) if (f != null) f.HasChanged = false;
                foreach (var f in p.AudioRelFiles) if (f != null) f.HasChanged = false;
            }
            ProjWin.RequestCloseProject = true;
            projCtl.Tick();
            Console.WriteLine($"CLOSEPROJECT after: project={(ProjWin.Project?.Name ?? "none")} overrides={(World.ProjectOverrides?.Count ?? 0)} status='{ProjWin.Status}' page={(ProjWin.CurrentEntity != null || ProjWin.CurrentYmap != null || ProjWin.CurrentYtyp != null ? "still shows an item" : "empty")} selection={WorldEdit.Selection.TypeName}/{WorldEdit.Selection.EntityDef?.Archetype?.Name ?? "-"} dirty={WorldEdit.DirtyCount} worldStatus='{WorldEdit.LastStatus}'");
            screenshotFrames = Math.Max(screenshotFrames, 8);
        }

        private readonly List<RenderMesh> precTinted = new List<RenderMesh>();
        private const uint PrecHoverTint = 5u;
        private const uint PrecSelectTint = 6u;

        partial void TickWorldPrecisionTint_J2()
        {
            TickCloseProjectEnv_J2();
            foreach (var m in precTinted) if (m != null) m.Highlight = 0;
            precTinted.Clear();
            if (panel == null || !panel.WorldMode || !worldBuilt || photoMode || SelMode != WorldSelectionMode.EntityPrecision) return;
            var s = WorldEdit.Selection;
            var sel = (s.CollisionBounds == null && s.MloEntityDef == null) ? s.EntityDef : null;
            var hov = worldHoverSel.EntityDef;
            if (hov != null && !ReferenceEquals(hov, sel)) MarkPrecision(hov, PrecHoverTint);
            if (sel != null) MarkPrecision(sel, PrecSelectTint);
        }

        private void MarkPrecision(YmapEntityDef e, uint tint)
        {
            PrecisionTint_U2(ref tint);
            if (tint == 0u) return;
            var list = worldRender.InstancesOf(e);
            if (list == null) return;
            foreach (var m in list)
            {
                if (m == null) continue;
                m.Highlight = tint;
                precTinted.Add(m);
            }
        }

        private bool PrecisionTriangleOf(YmapEntityDef e, Ray ray, out float dist, out RenderMesh mesh, out int tri)
        {
            dist = float.MaxValue; mesh = null; tri = -1;
            var list = worldRender.InstancesOf(e);
            if (list == null) return false;
            foreach (var m in list)
            {
                if (m?.PickVerts == null || m.PickIndices == null || m.NeverDraw || !m.Visible) continue;
                var inv = m.Transform; inv.Invert();
                var o = Vector3.TransformCoordinate(ray.Position, inv);
                var d = Vector3.TransformNormal(ray.Direction, inv);
                float dl = d.Length(); if (dl < 1e-9f) continue;
                d /= dl;
                var local = new Ray(o, d);
                var pv = m.PickVerts; var pi = m.PickIndices;
                for (int i = 0; i + 2 < pi.Length; i += 3)
                {
                    if (pi[i] >= pv.Length || pi[i + 1] >= pv.Length || pi[i + 2] >= pv.Length) continue;
                    if (!local.Intersects(ref pv[pi[i]], ref pv[pi[i + 1]], ref pv[pi[i + 2]], out float t)) continue;
                    t /= dl;
                    if (t < dist) { dist = t; mesh = m; tri = i / 3; }
                }
            }
            return mesh != null;
        }

        partial void WorldSelReport_J2(in WorldSelection s, System.Text.StringBuilder sb)
        {
            if (SelMode != WorldSelectionMode.EntityPrecision || s.EntityDef == null) return;
            var ray = camera.GetPickRay(deviceResources.Width / 2, deviceResources.Height / 2, deviceResources.Width, deviceResources.Height);
            if (PrecisionTriangleOf(s.EntityDef, ray, out float d, out var m, out int tri))
                sb.Append($" | PRECISE dist={d:0.##} mesh={m.ShaderName} tri={tri} of {(m.PickIndices?.Length ?? 0) / 3} hitDist={s.HitDist:0.##}");
            else sb.Append($" | PRECISE hitDist={s.HitDist:0.##} (no triangle of its own under the centre ray now)");
        }

        partial void RunWorldTestExtras_J2(Action<string, bool, string> check, Action<Vector3> settle)
        {
            Console.WriteLine("---- WS-J2: entity precision + light pick ----");
            var spot = new Vector3(-270.0f, -960.0f, 40.0f);
            CameraSequence.ApplyToCamera(camera, spot, 1.4f, 0.25f, settings.FovDeg);
            settle(spot);
            var ray = camera.GetPickRay(deviceResources.Width / 2, deviceResources.Height / 2, deviceResources.Width, deviceResources.Height);
            var pe = WorldPickPrecise(ray, out float pd);
            check("entity precision: the crosshair over the street strikes a triangle of an entity", pe != null && pd > 0 && pd < 1000,
                  pe != null ? $"{pe.Archetype?.Name} at {pd:0.##} m" : "nothing");
            if (pe != null)
            {
                bool tri = PrecisionTriangleOf(pe, ray, out float td, out var tm, out int ti);
                check("entity precision: the hit entity has a triangle of its own under the ray", tri && Math.Abs(td - pd) < 0.05f,
                      tri ? $"tri {ti} of {tm.ShaderName} at {td:0.##} m (pick said {pd:0.##})" : "no triangle");
                var sel = WorldSelection.FromProjectObject(pe); sel.HitDist = pd;
                check("entity precision: the selection is an entity selection (box + gizmo, like Entity mode)", sel.HasValue && sel.EntityDef == pe && sel.Archetype != null, sel.TypeName);
                var ee = WorldPickEntity(ray);
                check("entity mode: the same ray picks an entity too", ee != null, ee?.Archetype?.Name ?? "nothing");
            }
            YmapEntityDef le = null; int li = -1; Vector3 lpos = Vector3.Zero; float best = float.MaxValue;
            var fwd = camera.GetForward();
            foreach (var e in World.Visible)
            {
                if (e?.Archetype == null || !worldRender.Lights.TryGetDefs(e.Archetype.Hash, out var defs) || defs == null) continue;
                var sc = e.Scale; if (sc.X <= 0.0f) sc = Vector3.One;
                for (int i = 0; i < defs.Length; i++)
                {
                    if (defs[i].L == null) continue;
                    var wp = e.Orientation.Multiply(defs[i].Pos * sc) + e.Position;
                    var rel = wp - camera.Position;
                    float dd = rel.Length();
                    if (dd > 5.0f && dd < 80.0f && wp.Z - e.Position.Z > 3.0f && Vector3.Dot(rel, fwd) > 0.65f * dd && dd < best) { best = dd; le = e; li = i; lpos = wp; }
                }
            }
            check("light mode: a prop light hangs above its entity's origin within 80 m, in shot", le != null, le != null ? $"{le.Archetype.Name} light {li} at {best:0.#} m, {lpos.Z - le.Position.Z:0.#} m over the origin, {camera.WorldPerPixel(lpos) * 1000:0.#} mm per pixel" : "none in view");
            if (le != null)
            {
                var lray = new Ray(camera.Position, Vector3.Normalize(lpos - camera.Position));
                var hit = WorldSelection.Empty;
                PickWorldLights_I6(ref lray, camera.Position, ref hit);
                check("light mode: a ray aimed at that light selects it", hit.Light != null && hit.LightEntity == le && hit.LightIndex == li,
                      hit.Light != null ? hit.LightNameString() : "nothing");
                float wpp = camera.WorldPerPixel(lpos);
                var side = Vector3.Normalize(Vector3.Cross(lray.Direction, Vector3.UnitZ));
                var lray2 = new Ray(camera.Position, Vector3.Normalize(lpos + side * (5.0f * wpp) - camera.Position));
                var hit2 = WorldSelection.Empty;
                PickWorldLights_I6(ref lray2, camera.Position, ref hit2);
                check("light mode: five pixels beside the light still selects it", hit2.Light != null && hit2.LightEntity == le && hit2.LightIndex == li,
                      hit2.Light != null ? hit2.LightNameString() : "nothing");
                var lray3 = new Ray(camera.Position, Vector3.Normalize(lpos + side * (20.0f * wpp) - camera.Position));
                var under = WorldPickEntity(lray3);
                var hit3 = WorldSelection.Empty;
                PickWorldLights_I6(ref lray3, camera.Position, ref hit3);
                bool viaLamp = under == le;
                check("light mode: twenty pixels beside the light, a click that lands on the lamp itself gives the lamp's light",
                      !viaLamp || (hit3.Light != null && hit3.LightEntity == le),
                      $"entity under the ray {(under?.Archetype?.Name ?? "none")}{(viaLamp ? " (the lamp)" : "")}, picked {(hit3.Light != null ? hit3.LightNameString() : "nothing")}");
            }
        }
    }
}

