using System;
using System.Collections.Generic;
using CodeWalker;
using CodeWalker.GameFiles;
using CodeWalker.World;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;
using SharpDX.Direct3D11;

namespace RageLightEditor
{
    public partial class MainForm
    {

        private WorldSelection worldHoverSel = WorldSelection.Empty;

        private struct HelperBox
        {
            public Vector3 Pos; public Quaternion Ori; public Vector3 Min, Max; public Vector4 Col;
        }
        private readonly List<HelperBox> selHelperBoxes = new List<HelperBox>();
        private readonly List<HelperBox> selHelperBoxesNoDepth = new List<HelperBox>();
        private readonly List<YmapFile> selYmaps = new List<YmapFile>();
        private readonly List<IWorldGizmoTarget> worldGizmoTargetList = new List<IWorldGizmoTarget>();
        private GizmoTransformCommand.Pending worldDragTargets;
        private Water selWater;
        private bool selWaterTried;
        private bool selCollisionForced;
        private bool selCollisionModeLast;
        private bool selShowCollisionLast, selShowCollisionSeen;
        private WorldSelectionMode selModeBeforeCollisionCheck = WorldSelectionMode.None;
        private int selToggleFrame;
        private YmapFile selLodBvhDirty;
        private bool selEnvModeApplied;
        private WorldSelectionMode selLastMode = WorldSelectionMode.None;
        private WorldSelection selLastForProject = WorldSelection.Empty;

        private const float SelMaxDist = 10000.0f;
        private const int SelMaxHelperBoxes = 6000;

        private const float HelperHdr = 2.2f;
        private static Vector4 T(System.Numerics.Vector4 c, float alpha = 1.0f) => new Vector4(c.X * HelperHdr, c.Y * HelperHdr, c.Z * HelperHdr, alpha);
        private static Vector4 C(float r, float g, float b, float a) => new Vector4(r * HelperHdr, g * HelperHdr, b * HelperHdr, a);
        private const float FillHdr = 1.4f;
        private static Vector4 TF(System.Numerics.Vector4 c, float alpha) => new Vector4(c.X * FillHdr, c.Y * FillHdr, c.Z * FillHdr, alpha);
        private static Vector4 F(float r, float g, float b, float a) => new Vector4(r * FillHdr, g * FillHdr, b * FillHdr, a);
        private static Vector4 HelperBlue => T(UiTheme.Accent, 0.85f);
        private static Vector4 HelperCyan => T(UiTheme.AccentBright, 0.9f);
        private static Vector4 SelGreen => SelColour;
        private static readonly Vector4 SelColour = C(1.0f, 0.78f, 0.30f, 1.0f);
        private static readonly Vector4 SelColourSoft = F(1.0f, 0.78f, 0.30f, 0.18f);
        private static Vector4 SelWhite => T(UiTheme.AccentBright, 0.85f);
        private static Vector4 MouseHitWhite => T(UiTheme.AccentBright, 0.8f);
        private static Vector4 PortalAqua => T(UiTheme.Accent, 1.0f);
        private static Vector4 PortalAquaFill => TF(UiTheme.Accent, 0.20f);
        private static readonly Vector4 PortalLink = C(0.45f, 0.85f, 0.55f, 1.0f);
        private static readonly Vector4 PortalLinkFill = F(0.45f, 0.85f, 0.55f, 0.20f);
        private static readonly Vector4 PortalMirror = C(0.80f, 0.55f, 1.0f, 1.0f);
        private static readonly Vector4 PortalMirrorFill = F(0.80f, 0.55f, 1.0f, 0.22f);
        private static readonly Vector4 PortalWater = C(0.30f, 0.85f, 1.0f, 1.0f);
        private static readonly Vector4 PortalWaterFill = F(0.30f, 0.85f, 1.0f, 0.20f);
        private static Vector4 PortalBlue => PortalMirror;
        private static readonly Vector4 PortalRed = C(1.0f, 0.45f, 0.35f, 1.0f);
        private static readonly Vector4 RoomLabel = new Vector4(1.0f, 1.0f, 1.0f, 0.92f);
        private static Vector4 MloLabel => new Vector4(UiTheme.AccentBright.X, UiTheme.AccentBright.Y, UiTheme.AccentBright.Z, 0.92f);
        private static readonly Vector4 MloLabelSel = new Vector4(1.0f, 0.85f, 0.45f, 1.0f);
        private const float MloLabelDist = 150.0f;

        private static string MloLabelText(YmapEntityDef mlo)
        {
            var name = mlo?.Archetype?.Name;
            if (string.IsNullOrEmpty(name)) name = mlo?._CEntityDef.archetypeName.ToString() ?? "?";
            return name;
        }

        private WorldSelectionMode SelMode => panel?.SelectionModeEnum ?? WorldSelectionMode.Entity;

        private List<IWorldGizmoTarget> WorldGizmoTargets()
        {
            worldGizmoTargetList.Clear();
            var t = WorldEdit.Selection.GizmoTarget();
            if (t != null)
            {
                worldGizmoTargetList.Add(t);
                WorldEdit.PruneExtra_V20();
                foreach (var e in WorldEdit.Extra_V20)
                    if (e != null) worldGizmoTargetList.Add(new EntityGizmoTarget(e));
            }
            else { AreaGizmoTargets_J5(worldGizmoTargetList); NavGizmoTargets_P4(worldGizmoTargetList); }
            return worldGizmoTargetList;
        }

        private List<YmapEntityDef> WorldSelectedEntities()
        {
            worldSelList.Clear();
            foreach (var e in WorldEdit.AllSelected_V20()) worldSelList.Add(e);
            return worldSelList;
        }

        private void WorldTargetChanged(IWorldGizmoTarget t)
        {
            if (t == null) return;
            switch (t.Key)
            {
                case YmapEntityDef e:
                    WorldEntityChanged(e);
                    break;
                case YmapCarGen cg:
                    WorldEdit.MarkDirty(cg.Ymap);
                    break;
                case YmapLODLight l:
                    WorldEdit.MarkDirty(l.Ymap);
                    WorldEdit.MarkDirty(l.DistLodLights?.Ymap);
                    selLodBvhDirty = l.LodLights?.Ymap ?? l.Ymap;
                    break;
                case YmapBoxOccluder bo:
                    bo.UpdateBoxStruct();
                    WorldEdit.MarkDirty(bo.Ymap);
                    break;
                case YmapOccludeModelTriangle ot:
                    if (ot.Model != null) { ot.Model.BuildVertices(); ot.Model.BuildData(); ot.Model.BuildBVH(); }
                    WorldEdit.MarkDirty(ot.Ymap);
                    break;
                case LightAttributes la: WorldLightTargetChanged_I6(la, t); break;
                default:
                    bool areaHandled = false; AreaTargetChanged_J5(t, ref areaHandled); if (areaHandled) break;
                    bool navHandled = false; NavTargetChanged_P4(t, ref navHandled); if (navHandled) break;
                    bool sdHandled = false; WorldTargetChanged_SpaceData(t, ref sdHandled); if (sdHandled) break;
                    WorldEdit.LastStatus = "collision edited in memory only (no ybn save yet)";
                    break;
            }
            ProjectAutoAddForEdit(t.Key);
        }

        private void WorldGizmoDragBegan()
        {
            string what = worldGizmo.Mode == WorldGizmoMode.Rotate ? "Rotate " : worldGizmo.Mode == WorldGizmoMode.Scale ? "Scale " : "Move ";
            if (WorldEdit.Selected != null && WorldEdit.Selection.CollisionBounds == null &&
                WorldEdit.Selection.CollisionPoly == null && WorldEdit.Selection.CollisionVertex == null)
            {
                worldDrag = EntityTransformCommand.Begin(what + "entity", WorldSelectedEntities(), WorldEntityChanged);
                worldDragTargets = null;
            }
            else
            {
                worldDrag = null;
                worldDragTargets = GizmoTransformCommand.Begin(what + (WorldEdit.Selection.HasValue ? WorldEdit.Selection.TypeName : "area corner"), WorldGizmoTargets(), WorldTargetChanged);
            }
        }

        private void WorldGizmoDragEnded()
        {
            var cmd = worldDrag?.Complete();
            worldDrag = null;
            if (cmd != null) WorldHistory.Push(cmd);
            var cmd2 = worldDragTargets?.Complete();
            worldDragTargets = null;
            if (cmd2 != null) WorldHistory.Push(cmd2);
        }

        partial void OnWorldTick_Selection()
        {
            if (panel == null) return;

            if (!selEnvModeApplied && panel.WorldMode)
            {
                selEnvModeApplied = true;
                var env = Environment.GetEnvironmentVariable("RLE_SELMODE");
                if (!string.IsNullOrEmpty(env) && LightPanel.TryParseMode(env, out var m))
                {
                    int idx = LightPanel.IndexOfMode(m);
                    if (idx >= 0) { panel.SelectionMode = idx; Console.WriteLine($"SELMODE {LightPanel.SelectionModeNames[idx]}"); }
                    else Console.WriteLine("SELMODE unknown: " + env);
                }
                if (Environment.GetEnvironmentVariable("RLE_SHOWPICK") == "1") panel.ShowPickDebug = true;
            }

            var mode = SelMode;
            if (Environment.GetEnvironmentVariable("RLE_TOGGLECOLLISION") == "1" && panel.WorldMode && worldBuilt)
            {
                selToggleFrame++;
                if (selToggleFrame == 200) { panel.WorldShowCollision = true; Console.WriteLine($"TOGGLECOLLISION ticked (mode was {mode})"); }
                if (selToggleFrame == 202) Console.WriteLine($"TOGGLECOLLISION after tick: mode {mode} overlay {panel.WorldShowCollision}");
                if (selToggleFrame == 260) { panel.WorldShowCollision = false; Console.WriteLine("TOGGLECOLLISION unticked"); }
                if (selToggleFrame == 262) Console.WriteLine($"TOGGLECOLLISION after untick: mode {mode} overlay {panel.WorldShowCollision}");
            }
            if (panel.WorldMode && panel.WorldShowCollision != selShowCollisionLast && selShowCollisionSeen)
            {
                if (panel.WorldShowCollision && mode != WorldSelectionMode.Collision)
                {
                    int idx = LightPanel.IndexOfMode(WorldSelectionMode.Collision);
                    if (idx >= 0) { selModeBeforeCollisionCheck = mode; panel.SelectionMode = idx; mode = WorldSelectionMode.Collision; }
                    selCollisionForced = false;
                    WorldEdit.LastStatus = "collision overlay on: selection mode Collision";
                }
                else if (!panel.WorldShowCollision && mode == WorldSelectionMode.Collision)
                {
                    var back = selModeBeforeCollisionCheck == WorldSelectionMode.None || selModeBeforeCollisionCheck == WorldSelectionMode.Collision
                        ? WorldSelectionMode.Entity : selModeBeforeCollisionCheck;
                    int idx = LightPanel.IndexOfMode(back);
                    if (idx >= 0) { panel.SelectionMode = idx; mode = back; }
                    selCollisionForced = false;
                    if (WorldEdit.Selection.CollisionBounds != null || WorldEdit.Selection.CollisionPoly != null) WorldEdit.Deselect();
                    WorldEdit.LastStatus = "collision overlay off: selection mode " + LightPanel.SelectionModeNames[Math.Max(idx, 0)];
                }
            }
            selShowCollisionSeen = true;
            bool collisionModeNow = mode == WorldSelectionMode.Collision && panel.WorldMode;
            if (collisionModeNow != selCollisionModeLast)
            {
                selCollisionModeLast = collisionModeNow;
                if (collisionModeNow)
                {
                    if (!panel.WorldShowCollision) { panel.WorldShowCollision = true; selCollisionForced = true; }
                }
                else if (selCollisionForced)
                {
                    panel.WorldShowCollision = false;
                    selCollisionForced = false;
                }
            }
            selShowCollisionLast = panel.WorldShowCollision;
            if (mode != selLastMode)
            {
                selLastMode = mode;
                worldHoverSel = WorldSelection.Empty;
            }

            if (selLodBvhDirty != null && !worldGizmo.Dragging)
            {
                try { selLodBvhDirty.LODLights?.BuildBVH(); } catch { }
                selLodBvhDirty = null;
            }

            if (panel.WorldSelEdited)
            {
                panel.WorldSelEdited = false;
                var key = panel.WorldSelEditKey;
                var before = panel.WorldSelEditBefore;
                var after = panel.WorldSelEditAfter;
                if (key != null && before != null && after != null)
                {
                    WorldEdit.MarkDirty();
                    if (key is YmapLODLight ll) { WorldEdit.MarkDirty(ll.DistLodLights?.Ymap); selLodBvhDirty = ll.LodLights?.Ymap; }
                    ProjectAutoAddForEdit(key);
                    var cmd = new SnapshotCommand<object>(panel.WorldSelEditName ?? "Edit", key, before, after,
                        st => { LightPanel.SelRestoreState(key, st); MarkDirtyFor(key); });
                    WorldHistory.Push(cmd);
                }
            }
            if (panel.RequestSelectCollisionUnderCursor)
            {
                panel.RequestSelectCollisionUnderCursor = false;
                int idx = LightPanel.IndexOfMode(WorldSelectionMode.Collision);
                if (idx >= 0) panel.SelectionMode = idx;
                if (panel.CollisionUnderCursor.HasValue)
                {
                    WorldEdit.Select(panel.CollisionUnderCursor);
                    WorldEdit.LastStatus = panel.CollisionUnderCursor.GetNameString("");
                }
            }
            if (panel.RequestWorldSelectionDelete)
            {
                panel.RequestWorldSelectionDelete = false;
                WorldDeleteSelectionItem();
            }
            if (panel.RequestAddSelectionToProject)
            {
                panel.RequestAddSelectionToProject = false;
                ProjectAddSelection(WorldEdit.Selection);
            }
            if (panel.RequestWorldSelectionFrame)
            {
                panel.RequestWorldSelectionFrame = false;
                if (WorldEdit.Selection.HasValue)
                {
                    var s = WorldEdit.Selection;
                    var at = s.HasValue && s.WidgetPosition != Vector3.Zero ? s.WidgetPosition : (s.AABB.Minimum + s.AABB.Maximum) * 0.5f;
                    var size = s.AABB.Maximum - s.AABB.Minimum;
                    FrameWorldTarget_S5(at, size.Length() * 0.5f);
                }
            }

            if (WorldEdit.Selection.CheckForChanges(selLastForProject))
            {
                selLastForProject = WorldEdit.Selection;
                if (WorldEdit.Selection.HasValue && WorldEdit.Selected == null)
                    WorldEdit.LastStatus = WorldEdit.Selection.GetNameString("");
            }
        }

        private void MarkDirtyFor(object key)
        {
            switch (key)
            {
                case YmapEntityDef e: WorldEntityChanged(e); break;
                case YmapCarGen cg: WorldEdit.MarkDirty(cg.Ymap); break;
                case YmapLODLight l: WorldEdit.MarkDirty(l.Ymap); WorldEdit.MarkDirty(l.DistLodLights?.Ymap); selLodBvhDirty = l.LodLights?.Ymap; break;
                case YmapBoxOccluder bo: WorldEdit.MarkDirty(bo.Ymap); break;
                case YmapOccludeModelTriangle ot: WorldEdit.MarkDirty(ot.Ymap); break;
                case YmapTimeCycleModifier t: WorldEdit.MarkDirty(t.Ymap); break;
                case YmapGrassInstanceBatch g: WorldEdit.MarkDirty(g.Ymap); break;
            }
            ProjectAutoAddForEdit(key);
        }

        private void WorldDeleteSelectionItem()
        {
            var s = WorldEdit.Selection;
            if (s.EntityDef == null) ProjectAutoAddForEdit(s.GetProjectObject());
            if (s.CarGenerator != null)
            {
                var cg = s.CarGenerator; var ymap = cg.Ymap;
                if (ymap == null) return;
                WorldEdit.MarkDirty(ymap);
                ymap.RemoveCarGen(cg);
                WorldEdit.Deselect();
                WorldEdit.LastStatus = "deleted car generator " + cg.NameString();
                WorldHistory.Push(new DelegateCommand("Delete car generator",
                    doIt: () => { ymap.RemoveCarGen(cg); WorldEdit.MarkDirty(ymap); if (ReferenceEquals(WorldEdit.Selection.CarGenerator, cg)) WorldEdit.Deselect(); },
                    undoIt: () => { ymap.AddCarGen(cg); WorldEdit.MarkDirty(ymap); WorldEdit.Select(WorldSelection.FromProjectObject(cg)); }));
            }
            else if (s.LodLight != null)
            {
                var l = s.LodLight; var ymap = l.LodLights?.Ymap ?? l.Ymap;
                if (ymap == null) return;
                WorldEdit.MarkDirty(ymap); WorldEdit.MarkDirty(l.DistLodLights?.Ymap);
                ymap.RemoveLodLight(l);
                ymap.LODLights?.BuildBVH();
                WorldEdit.Deselect();
                WorldEdit.LastStatus = "deleted LOD light " + l.Index;
                WorldHistory.Push(new DelegateCommand("Delete LOD light",
                    doIt: () => { ymap.RemoveLodLight(l); ymap.LODLights?.BuildBVH(); WorldEdit.MarkDirty(ymap); WorldEdit.MarkDirty(l.DistLodLights?.Ymap); if (ReferenceEquals(WorldEdit.Selection.LodLight, l)) WorldEdit.Deselect(); },
                    undoIt: () => { ymap.AddLodLight(l); ymap.LODLights?.BuildBVH(); WorldEdit.MarkDirty(ymap); WorldEdit.MarkDirty(l.DistLodLights?.Ymap); WorldEdit.Select(WorldSelection.FromProjectObject(l)); }));
            }
            else if (s.BoxOccluder != null)
            {
                var bo = s.BoxOccluder; var ymap = bo.Ymap;
                if (ymap == null) return;
                WorldEdit.MarkDirty(ymap);
                ymap.RemoveBoxOccluder(bo);
                WorldEdit.Deselect();
                WorldEdit.LastStatus = "deleted box occluder " + bo.Index;
                WorldHistory.Push(new DelegateCommand("Delete box occluder",
                    doIt: () => { ymap.RemoveBoxOccluder(bo); WorldEdit.MarkDirty(ymap); if (ReferenceEquals(WorldEdit.Selection.BoxOccluder, bo)) WorldEdit.Deselect(); },
                    undoIt: () => { ymap.AddBoxOccluder(bo); WorldEdit.MarkDirty(ymap); WorldEdit.Select(WorldSelection.FromProjectObject(bo)); }));
            }
            else if (s.OccludeModelTri != null)
            {
                var ot = s.OccludeModelTri; var ymap = ot.Ymap;
                if (ymap == null || ot.Model == null) return;
                WorldEdit.MarkDirty(ymap);
                ymap.RemoveOccludeModelTriangle(ot);
                WorldEdit.Deselect();
                WorldEdit.LastStatus = "deleted occlude model triangle " + ot.Index;
                WorldHistory.Push(new DelegateCommand("Delete occlude triangle",
                    doIt: () => { ymap.RemoveOccludeModelTriangle(ot); WorldEdit.MarkDirty(ymap); if (ReferenceEquals(WorldEdit.Selection.OccludeModelTri, ot)) WorldEdit.Deselect(); },
                    undoIt: () => { ymap.AddOccludeModelTriangle(ot); WorldEdit.MarkDirty(ymap); WorldEdit.Select(WorldSelection.FromProjectObject(ot)); }));
            }
            else if (s.EntityDef != null)
            {
                WorldDeleteSelected();
            }
        }

        private WorldSelection WorldPickHit(Ray ray, WorldSelectionMode mode)
        {
            var hit = WorldSelection.Empty;
            var camPos = camera.Position;
            switch (mode)
            {
                case WorldSelectionMode.Entity:
                    {
                        var e = WorldPickEntity(ray);
                        if (e != null) { hit = WorldSelection.FromProjectObject(e); hit.HitDist = worldPickEntityDist; }
                        break;
                    }
                case WorldSelectionMode.EntityPrecision:
                    {
                        var e = WorldPickPrecise(ray, out float d);
                        if (e != null) { hit = WorldSelection.FromProjectObject(e); hit.HitDist = d; }
                        break;
                    }
                case WorldSelectionMode.MloInstance:
                    PickMloInstances(ref ray, camPos, ref hit);
                    if (!hit.HasValue)
                    {
                        var e = WorldPickEntity(ray);
                        var mlo = e;
                        while (mlo != null && mlo.MloInstance == null) mlo = mlo.MloParent;
                        if (mlo?.MloInstance != null)
                        {
                            hit = WorldSelection.FromProjectObject(mlo);
                            hit.MloEntityDef = mlo;
                            hit.AABB = new BoundingBox(new Vector3(-1.5f), new Vector3(1.5f));
                            hit.CamRel = mlo.Position - camPos;
                        }
                    }
                    break;
                case WorldSelectionMode.TimeCycleModifier: PickTimeCycleModifiers(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.CarGenerator: PickCarGenerators(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.Grass: PickGrassBatches(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.LodLights: PickLodLights(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.Occlusion: PickOccluders(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.WaterQuad: PickWaterQuads(ref ray, ref hit, EnsureSelWater()?.WaterQuads); break;
                case WorldSelectionMode.CalmingQuad: PickWaterQuads(ref ray, ref hit, EnsureSelWater()?.CalmingQuads); break;
                case WorldSelectionMode.WaveQuad: PickWaterQuads(ref ray, ref hit, EnsureSelWater()?.WaveQuads); break;
                case WorldSelectionMode.Collision: PickCollision(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.Path: PickPaths(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.NavMesh: PickNavMeshes(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.TrainTrack: PickTrainTracks(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.Scenario: PickScenarios(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.Audio: PickAudioZones(ref ray, camPos, ref hit); break;
                case WorldSelectionMode.Light: PickWorldLights_I6(ref ray, camPos, ref hit); break;
                default:
                    break;
            }
            return hit;
        }

        private void PickMloInstances(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            World.SnapshotWalkedYmaps(selYmaps);
            foreach (var ymap in selYmaps)
            {
                var mlos = ymap.MloEntities;
                if (mlos == null) continue;
                foreach (var ent in mlos)
                {
                    if (ent == null) continue;
                    float mh = MloMarkerHalf_U25(ent.Position);
                    var bbox = new BoundingBox(new Vector3(-mh), new Vector3(mh));
                    var camrel = ent.Position - camPos;
                    var orinv = Quaternion.Invert(ent.Orientation);
                    var mray = new Ray(orinv.Multiply(ray.Position - ent.Position), orinv.Multiply(ray.Direction));
                    if (mray.Intersects(ref bbox, out float d) && d < hit.HitDist && d > 0)
                    {
                        hit.MloEntityDef = ent;
                        hit.EntityDef = ent;
                        hit.Archetype = ent.Archetype;
                        hit.HitDist = d;
                        hit.CamRel = camrel;
                        hit.AABB = bbox;
                    }
                }
            }
        }

        private void PickTimeCycleModifiers(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            World.SnapshotWalkedYmaps(selYmaps);
            foreach (var ymap in selYmaps)
            {
                var tcms = ymap.TimeCycleModifiers;
                if (tcms == null) continue;
                foreach (var tcm in tcms)
                {
                    if (tcm == null) continue;
                    if ((((tcm.BBMin + tcm.BBMax) * 0.5f) - camPos).Length() > SelMaxDist) continue;
                    var bbox = new BoundingBox(tcm.BBMin, tcm.BBMax);
                    if (ray.Intersects(ref bbox, out float d) && d < hit.HitDist && d > 0)
                    {
                        hit.TimeCycleModifier = tcm;
                        hit.HitDist = d;
                        hit.CamRel = -camPos;
                        hit.AABB = bbox;
                    }
                }
            }
        }

        private void PickCarGenerators(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            World.SnapshotWalkedYmaps(selYmaps);
            foreach (var ymap in selYmaps)
            {
                var cgs = ymap.CarGenerators;
                if (cgs == null) continue;
                foreach (var cg in cgs)
                {
                    if (cg == null) continue;
                    var camrel = cg.Position - camPos;
                    if (camrel.Length() > SelMaxDist) continue;
                    var bbox = new BoundingBox(cg.BBMin, cg.BBMax);
                    var orinv = Quaternion.Invert(cg.Orientation);
                    var mray = new Ray(orinv.Multiply(ray.Position - cg.Position), orinv.Multiply(ray.Direction));
                    if (mray.Intersects(ref bbox, out float d) && d < hit.HitDist && d > 0)
                    {
                        hit.CarGenerator = cg;
                        hit.HitDist = d;
                        hit.CamRel = camrel;
                        hit.AABB = bbox;
                    }
                }
            }
        }

        private void PickGrassBatches(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            World.SnapshotWalkedYmaps(selYmaps);
            foreach (var ymap in selYmaps)
            {
                var gbs = ymap.GrassInstanceBatches;
                if (gbs == null) continue;
                foreach (var gb in gbs)
                {
                    if (gb == null) continue;
                    if ((gb.Position - camPos).Length() > SelMaxDist) continue;
                    var bbox = new BoundingBox(gb.AABBMin, gb.AABBMax);
                    if (ray.Intersects(ref bbox, out float d) && d < hit.HitDist && d > 0)
                    {
                        hit.GrassBatch = gb;
                        hit.HitDist = d;
                        hit.CamRel = -camPos;
                        hit.AABB = bbox;
                    }
                }
            }
        }

        private void PickLodLights(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            World.SnapshotWalkedYmaps(selYmaps);
            foreach (var ymap in selYmaps)
            {
                var ll = ymap.LODLights;
                if (ll == null) continue;
                if ((((ll.BBMin + ll.BBMax) * 0.5f) - camPos).Length() > SelMaxDist) continue;
                if (ll.BVH != null) PickPathBvh(ll.BVH, ref ray, camPos, ref hit, scaled: true);
                else if (ll.LodLights != null)
                {
                    var bsph = new BoundingSphere(Vector3.Zero, 0.5f);
                    foreach (var n in ll.LodLights)
                    {
                        if (n == null) continue;
                        bsph.Center = n.Position;
                        bsph.Radius = LodLightPickRadius(n.Position, camPos);
                        if (ray.Intersects(ref bsph, out float d) && d < hit.HitDist && d > 0)
                        {
                            hit.LodLight = n; hit.HitDist = d; hit.CamRel = n.Position - camPos;
                            hit.AABB = new BoundingBox(new Vector3(-bsph.Radius), new Vector3(bsph.Radius));
                        }
                    }
                }
            }
        }

        private static float LodLightPickRadius(Vector3 lightPos, Vector3 camPos)
        {
            return Math.Max(0.5f, (lightPos - camPos).Length() * 0.01f);
        }

        private void PickPathBvh(PathBVHNode pathbvhnode, ref Ray mray, Vector3 camPos, ref WorldSelection hit, bool scaled = false)
        {
            float nrad = 0.5f;
            if (scaled)
            {
                var nearest = Vector3.Clamp(camPos, pathbvhnode.Box.Minimum, pathbvhnode.Box.Maximum);
                float far = (nearest - camPos).Length() + (pathbvhnode.Box.Maximum - pathbvhnode.Box.Minimum).Length();
                nrad = Math.Max(0.5f, far * 0.01f);
            }
            var bsph = new BoundingSphere { Radius = nrad };
            var bbox = new BoundingBox(pathbvhnode.Box.Minimum - nrad, pathbvhnode.Box.Maximum + nrad);
            var nbox = new BoundingBox(new Vector3(-nrad), new Vector3(nrad));
            if (mray.Intersects(ref bbox, out float fhd))
            {
                if (pathbvhnode.Node1 != null && pathbvhnode.Node2 != null)
                {
                    PickPathBvh(pathbvhnode.Node1, ref mray, camPos, ref hit, scaled);
                    PickPathBvh(pathbvhnode.Node2, ref mray, camPos, ref hit, scaled);
                }
                else if (pathbvhnode.Nodes != null)
                {
                    foreach (var n in pathbvhnode.Nodes)
                    {
                        bsph.Center = n.Position;
                        if (scaled) { bsph.Radius = LodLightPickRadius(n.Position, camPos); nbox = new BoundingBox(new Vector3(-bsph.Radius), new Vector3(bsph.Radius)); }
                        if (mray.Intersects(ref bsph, out float hitdist) && hitdist < hit.HitDist && hitdist > 0)
                        {
                            hit.PathNode = n as YndNode;
                            hit.TrainTrackNode = n as TrainTrackNode;
                            hit.ScenarioNode = n as ScenarioNode;
                            hit.LodLight = n as YmapLODLight;
                            hit.NavPoint = n as YnvPoint;
                            hit.NavPortal = n as YnvPortal;
                            hit.NavPoly = null;
                            hit.HitDist = hitdist;
                            hit.CamRel = n.Position - camPos;
                            hit.AABB = nbox;
                        }
                    }
                }
            }
        }

        private void PickOccluders(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            World.SnapshotWalkedYmaps(selYmaps);
            foreach (var ymap in selYmaps)
            {
                var bos = ymap.BoxOccluders;
                if (bos != null)
                {
                    foreach (var bo in bos)
                    {
                        if (bo == null) continue;
                        var camrel = bo.Position - camPos;
                        if (camrel.Length() > SelMaxDist) continue;
                        var bbox = new BoundingBox(bo.BBMin, bo.BBMax);
                        var orinv = Quaternion.Invert(bo.Orientation);
                        var mray = new Ray(orinv.Multiply(ray.Position - bo.Position), orinv.Multiply(ray.Direction));
                        if (mray.Intersects(ref bbox, out float d) && d < hit.HitDist && d > 0)
                        {
                            hit.BoxOccluder = bo;
                            hit.OccludeModelTri = null;
                            hit.HitDist = d;
                            hit.CamRel = camrel;
                            hit.AABB = bbox;
                        }
                    }
                }
                var oms = ymap.OccludeModels;
                if (oms != null)
                {
                    foreach (var om in oms)
                    {
                        if (om == null) continue;
                        float hitdist = float.MaxValue;
                        YmapOccludeModelTriangle hittri = null;
                        try { hittri = om.RayIntersect(ref ray, ref hitdist); } catch { }
                        if (hittri != null && hitdist < hit.HitDist)
                        {
                            hit.BoxOccluder = null;
                            hit.OccludeModelTri = hittri;
                            hit.HitDist = hitdist;
                            hit.CamRel = -camPos;
                            hit.AABB = hittri.Box;
                        }
                    }
                }
            }
        }

        private Water EnsureSelWater()
        {
            if (selWater != null) return selWater;
            if (selWaterTried || gameFiles?.Cache == null || !gameFiles.Ready) return null;
            selWaterTried = true;
            try
            {
                var w = new Water();
                w.Init(gameFiles.Cache, s => { });
                selWater = w;
            }
            catch (Exception ex) { Console.WriteLine("water quads for selection: " + ex.Message); }
            return selWater;
        }

        private void PickWaterQuads<T>(ref Ray ray, ref WorldSelection hit, List<T> quads) where T : BaseWaterQuad
        {
            if (quads == null) return;
            foreach (var quad in quads)
            {
                if (quad == null) continue;
                var bbox = Editor.WorldSelection.QuadBox(quad);
                if (ray.Intersects(ref bbox, out float hitdist) && hitdist > 0 && hitdist <= hit.HitDist)
                {
                    float curSize = hit.AABB.Size.X * hit.AABB.Size.Y;
                    float newSize = bbox.Size.X * bbox.Size.Y;
                    if (curSize == 0 || newSize < curSize)
                    {
                        hit.HitDist = hitdist;
                        hit.CamRel = -camera.Position;
                        hit.AABB = bbox;
                        hit.WaterQuad = quad as WaterQuad;
                        hit.WaveQuad = quad as WaterWaveQuad;
                        hit.CalmingQuad = quad as WaterCalmingQuad;
                    }
                }
            }
        }

        private void PickCollision(ref Ray ray, Vector3 camPos, ref WorldSelection hit, bool allowLoad = true)
        {
            var cache = gameFiles?.Cache;
            if (cache == null || !gameFiles.Ready) { if (allowLoad) WorldEdit.LastStatus = "collision: game files not ready"; return; }
            var res = new SpaceRayIntersectResult { HitDist = float.MaxValue };
            YmapEntityDef hitEnt = null;
            var projectHashes = PickProjectCollision_V28(ref ray, ref res);
            if (ybnIndex != null)
            {
                foreach (var (hash, box) in ybnIndex)
                {
                    if (projectHashes != null && projectHashes.Contains(hash)) continue;
                    var bb = box;
                    if (!ray.Intersects(ref bb, out float bd)) continue;
                    if (bd > res.HitDist) continue;
                    YbnFile ybn = null;
                    try { ybn = cache.GetYbn(hash); } catch { }
                    if (ybn == null) continue;
                    if (!ybn.Loaded)
                    {
                        if (!allowLoad || !gameFiles.EnsureLoaded(ybn)) continue;
                    }
                    var b = ybn.Bounds;
                    if (b == null) continue;
                    var bhit = b.RayIntersect(ref ray, res.HitDist);
                    if (bhit.Hit) { bhit.HitYbn = ybn; if (bhit.HitDist < res.HitDist) { res.TryUpdate(ref bhit); hitEnt = null; } }
                }
            }
            foreach (var mlo in World.Visible)
            {
                if (mlo?.MloInstance == null || mlo.Archetype == null) continue;
                var ebox = new BoundingBox(mlo.BBMin, mlo.BBMax);
                if (!ray.Intersects(ref ebox, out float bd) || bd > res.HitDist) continue;
                YbnFile ybn = null;
                try { ybn = cache.GetYbn(mlo.Archetype.Hash); } catch { }
                if (ybn == null) continue;
                if (!ybn.Loaded && (!allowLoad || !gameFiles.EnsureLoaded(ybn))) continue;
                if (ybn.Bounds == null) continue;
                var iori = mlo.Orientation;
                var iorinv = Quaternion.Invert(iori);
                var iray = new Ray(iorinv.Multiply(ray.Position - mlo.Position), iorinv.Multiply(ray.Direction));
                var ihit = ybn.Bounds.RayIntersect(ref iray, res.HitDist);
                if (ihit.Hit && ihit.HitDist < res.HitDist)
                {
                    ihit.HitYbn = ybn; ihit.HitEntity = mlo;
                    ihit.Position = iori.Multiply(ihit.Position) + mlo.Position;
                    ihit.Normal = iori.Multiply(ihit.Normal);
                    res.TryUpdate(ref ihit);
                    hitEnt = mlo;
                }
            }
            if (!res.Hit) return;
            var ent = res.HitEntity ?? hitEnt;
            var bounds = res.HitBounds;
            hit.CollisionPoly = res.HitPolygon;
            hit.CollisionBounds = bounds;
            hit.EntityDef = ent;
            hit.Archetype = ent?.Archetype;
            hit.HitDist = res.HitDist;
            if (bounds != null)
            {
                var camrel = (ent != null ? ent.Position : Vector3.Zero) - camPos;
                var ori = ent != null ? ent.Orientation : Quaternion.Identity;
                var trans = bounds.Transform.TranslationVector;
                hit.CamRel = camrel + ori.Multiply(trans);
                hit.BBOffset = trans;
                hit.BBOrientation = bounds.Transform.ToQuaternion();
                hit.AABB = new BoundingBox(bounds.BoxMin, bounds.BoxMax);
                const float vertexDist = 0.1f;
                if (res.HitVertex.Distance < vertexDist && bounds is BoundGeometry bgeom)
                    hit.CollisionVertex = bgeom.GetVertexObject(res.HitVertex.Index);
            }
        }

        private void WorldPickCollisionUnderCursor(Ray ray, WorldSelectionMode mode)
        {
            if (panel == null) return;
            if (!panel.WorldShowCollision) { panel.CollisionUnderCursor = WorldSelection.Empty; return; }
            if (mode == WorldSelectionMode.Collision) { panel.CollisionUnderCursor = WorldEdit.Selection; return; }
            var hit = WorldSelection.Empty;
            PickCollision(ref ray, camera.Position, ref hit, allowLoad: true);
            panel.CollisionUnderCursor = hit.CollisionBounds != null ? hit : WorldSelection.Empty;
            if (hit.CollisionBounds != null && !string.IsNullOrEmpty(WorldEdit.LastStatus))
                WorldEdit.LastStatus += "  |  collision: " + hit.GetNameString("");
        }

        private WorldSelection WorldPickCollisionHover(Ray ray)
        {
            var hit = WorldSelection.Empty;
            if (ybnIndex == null) return hit;
            PickCollision(ref ray, camera.Position, ref hit, allowLoad: false);
            return hit.CollisionBounds != null ? hit : WorldSelection.Empty;
        }

        private void ForgetInteriorInstances(MloInstanceData inst)
        {
            if (inst == null) return;
            var ents = inst.Entities;
            if (ents != null) foreach (var ie in ents) if (ie != null) worldRender.Forget(ie);
            var sets = inst.EntitySets;
            if (sets != null)
                foreach (var set in sets)
                {
                    if (set?.Entities == null) continue;
                    foreach (var ie in set.Entities) if (ie != null) worldRender.Forget(ie);
                }
        }

        private void CollectSelectionHelpers(WorldSelectionMode mode)
        {
            selHelperBoxes.Clear();
            selHelperBoxesNoDepth.Clear();
            if (!panel.ShowSelectionHelpers) return;
            var camPos = camera.Position;
            switch (mode)
            {
                case WorldSelectionMode.MloInstance:
                    World.SnapshotWalkedYmaps(selYmaps);
                    foreach (var ymap in selYmaps)
                    {
                        var mlos = ymap.MloEntities; if (mlos == null) continue;
                        foreach (var ent in mlos)
                        {
                            if (ent == null || selHelperBoxesNoDepth.Count >= SelMaxHelperBoxes) continue;
                            float mh = MloMarkerHalf_U25(ent.Position);
                            selHelperBoxesNoDepth.Add(new HelperBox { Pos = ent.Position, Ori = ent.Orientation, Min = new Vector3(-mh), Max = new Vector3(mh), Col = HelperBlue });
                            if ((ent.Position - camPos).LengthSquared() < MloLabelDist * MloLabelDist &&
                                !ReferenceEquals(ent, WorldEdit.Selection.MloEntityDef))
                                DrawWorldLabel(ent.Position + new Vector3(0, 0, 1.6f), MloLabelText(ent), MloLabel);
                        }
                    }
                    break;
                case WorldSelectionMode.TimeCycleModifier:
                    World.SnapshotWalkedYmaps(selYmaps);
                    foreach (var ymap in selYmaps)
                    {
                        var tcms = ymap.TimeCycleModifiers; if (tcms == null) continue;
                        foreach (var tcm in tcms)
                        {
                            if (tcm == null || selHelperBoxes.Count >= SelMaxHelperBoxes) continue;
                            if ((((tcm.BBMin + tcm.BBMax) * 0.5f) - camPos).Length() > SelMaxDist) continue;
                            selHelperBoxes.Add(new HelperBox { Pos = Vector3.Zero, Ori = Quaternion.Identity, Min = tcm.BBMin, Max = tcm.BBMax, Col = HelperBlue });
                        }
                    }
                    break;
                case WorldSelectionMode.CarGenerator:
                    World.SnapshotWalkedYmaps(selYmaps);
                    foreach (var ymap in selYmaps)
                    {
                        var cgs = ymap.CarGenerators; if (cgs == null) continue;
                        foreach (var cg in cgs)
                        {
                            if (cg == null || selHelperBoxes.Count >= SelMaxHelperBoxes) continue;
                            if ((cg.Position - camPos).Length() > SelMaxDist) continue;
                            selHelperBoxes.Add(CarGenHelperBox(cg, camPos));
                        }
                    }
                    break;
                case WorldSelectionMode.Grass:
                    World.SnapshotWalkedYmaps(selYmaps);
                    foreach (var ymap in selYmaps)
                    {
                        var gbs = ymap.GrassInstanceBatches; if (gbs == null) continue;
                        foreach (var gb in gbs)
                        {
                            if (gb == null || selHelperBoxes.Count >= SelMaxHelperBoxes) continue;
                            if ((gb.Position - camPos).Length() > SelMaxDist) continue;
                            selHelperBoxes.Add(new HelperBox { Pos = Vector3.Zero, Ori = Quaternion.Identity, Min = gb.AABBMin, Max = gb.AABBMax, Col = HelperBlue });
                        }
                    }
                    break;
                case WorldSelectionMode.LodLights:
                    World.SnapshotWalkedYmaps(selYmaps);
                    foreach (var ymap in selYmaps)
                    {
                        var ll = ymap.LODLights; if (ll == null) continue;
                        if ((((ll.BBMin + ll.BBMax) * 0.5f) - camPos).Length() > SelMaxDist) continue;
                        selHelperBoxes.Add(new HelperBox { Pos = Vector3.Zero, Ori = Quaternion.Identity, Min = ll.BBMin, Max = ll.BBMax, Col = HelperBlue });
                        var dl = ymap.DistantLODLights;
                        if (dl != null) selHelperBoxes.Add(new HelperBox { Pos = Vector3.Zero, Ori = Quaternion.Identity, Min = dl.BBMin, Max = dl.BBMax, Col = HelperBlue });
                    }
                    break;
                case WorldSelectionMode.WaterQuad: CollectWaterHelpers(EnsureSelWater()?.WaterQuads); break;
                case WorldSelectionMode.CalmingQuad: CollectWaterHelpers(EnsureSelWater()?.CalmingQuads); break;
                case WorldSelectionMode.WaveQuad: CollectWaterHelpers(EnsureSelWater()?.WaveQuads); break;
                case WorldSelectionMode.Light: DrawWorldLightCandidates_I6(); break;
                case WorldSelectionMode.Occlusion:
                    break;
            }
        }

        private void CollectWaterHelpers<T>(List<T> quads) where T : BaseWaterQuad
        {
            if (quads == null) return;
            foreach (var q in quads)
            {
                if (q == null || selHelperBoxesNoDepth.Count >= SelMaxHelperBoxes) continue;
                var b = Editor.WorldSelection.QuadBox(q);
                selHelperBoxesNoDepth.Add(new HelperBox { Pos = Vector3.Zero, Ori = Quaternion.Identity, Min = b.Minimum, Max = b.Maximum, Col = HelperBlue });
            }
        }

        partial void OnAfterWorldDraw_Selection(DeviceContext context)
        {
            if (panel == null || !panel.WorldMode || !worldBuilt) return;
            var mode = SelMode;
            CollectSelectionHelpers(mode);
            foreach (var hb in selHelperBoxes) DrawOrientedBox(hb.Pos, hb.Ori, hb.Min, hb.Max, hb.Col);
            if (mode == WorldSelectionMode.Occlusion && panel.ShowSelectionHelpers) DrawOccluderGeometry(context);
            DrawCarGenPlaceholders(context);
        }

        private void DrawOccluderGeometry(DeviceContext context) => DrawOccluderGeometry_J1(context);

        private void DrawWorldSelectionBox()
        {
            selOverlay_U24.Clear();
            selSegs_U25.Clear();
            if (l4Deferred_U25) { L4Tris.Clear_U25(); l4Deferred_U25 = false; }
            var mode = SelMode;
            int start = lineRenderer.LineCount;
            foreach (var hb in selHelperBoxesNoDepth) DrawOrientedBox(hb.Pos, hb.Ori, hb.Min, hb.Max, hb.Col);
            CaptureLines_U25(start, false, HelperPx_U25);
            DrawWorldLightMarkers_J2();
            if (worldHoverSel.HasValue && worldHoverSel.CheckForChanges(WorldEdit.Selection))
            {
                start = lineRenderer.LineCount;
                DrawSelection(worldHoverSel, MouseHitWhite, false);
                CaptureLines_U25(start, !OnTop_U25(worldHoverSel, mode), HelperPx_U25, HoverWhite_U25);
            }
            if (WorldEdit.Selection.HasValue)
            {
                start = lineRenderer.LineCount;
                DrawSelection(WorldEdit.Selection, SelGreen, true);
                CaptureLines_U25(start, !OnTop_U25(WorldEdit.Selection, mode), SelBoxPx_U24);
            }
            foreach (var extra in WorldEdit.Extra_V20)
                if (extra != null) DrawEntityBox_V19(extra, SelGreen, true);
            DrawPickDebug();
        }

        private void DrawSelection(in WorldSelection s, Vector4 col, bool full)
        {
            var camPos = camera.Position;
            Vector3 bbmin = s.AABB.Minimum, bbmax = s.AABB.Maximum;
            Vector3 pos = Vector3.Zero;
            Vector3 scale = Vector3.One;
            Quaternion ori = Quaternion.Identity;
            bool drawBox = true;

            if (s.Archetype != null) { bbmin = s.Archetype.BBMin; bbmax = s.Archetype.BBMax; }
            if (s.EntityDef != null) { pos = s.EntityDef.Position; scale = s.EntityDef.Scale; ori = s.EntityDef.Orientation; }
            if (s.CarGenerator != null)
            {
                var cg = s.CarGenerator;
                pos = cg.Position; ori = cg.Orientation; bbmin = cg.BBMin; bbmax = cg.BBMax; scale = Vector3.One;
                float arrowlen = cg._CCarGen.perpendicularLength;
                float arrowrad = arrowlen * 0.066f;
                DrawArrowOutline(cg.Position, Vector3.UnitX, Vector3.UnitY, ori, arrowlen, arrowrad, col, fill: full);
                DrawCarGenFootprint(cg, col, full); drawBox = false;
            }
            if (s.WaveQuad != null)
            {
                var quad = s.WaveQuad;
                var quadArrowPos = new Vector3(quad.minX + (quad.maxX - quad.minX) * 0.5f, quad.minY + (quad.maxY - quad.minY) * 0.5f, 5);
                float arrowlen = quad.Amplitude * 50;
                DrawArrowOutline(quadArrowPos, Vector3.UnitX, Vector3.UnitY, quad.WaveOrientation, arrowlen, arrowlen * 0.066f, col, fill: full);
            }
            if (s.LodLight != null)
            {
                if (full)
                {
                    DrawLodLightShape(s.LodLight);
                    if (s.LodLight.LodLights != null) { bbmin = s.LodLight.LodLights.BBMin; bbmax = s.LodLight.LodLights.BBMax; }
                    pos = Vector3.Zero; ori = Quaternion.Identity; scale = Vector3.One;
                }
                else
                {
                    var lp = s.LodLight.Position;
                    float r = LodLightPickRadius(lp, camPos);
                    pos = lp; ori = Quaternion.Identity; scale = Vector3.One;
                    bbmin = new Vector3(-r); bbmax = new Vector3(r);
                    float fall = s.LodLight.Falloff;
                    if (fall > 0.01f && fall < 500.0f) lineRenderer.AddCircle(lp, Vector3.UnitX, Vector3.UnitY, fall, col, 48);
                }
            }
            if (s.MloEntityDef != null && s.CollisionBounds == null)
            {
                bbmin = s.AABB.Minimum; bbmax = s.AABB.Maximum;
                var mlo = s.MloEntityDef;
                var mlop = mlo.Position;
                pos = mlop; ori = mlo.Orientation; scale = Vector3.One;
                DrawWorldLabel(mlop + new Vector3(0, 0, 1.6f), MloLabelText(mlo), full ? MloLabelSel : MloLabel);
                if (mlo.Archetype is MloArchetype mloa && !DrawMloInstanceHelpers_L4(s, mlo, mloa, full, ref bbmin, ref bbmax, ref drawBox))
                {
                    var focusRoom = full ? s.MloRoomDef : null;
                    var focusPortal = full ? s.MloPortalDef : null;
                    bool focused = focusRoom != null || focusPortal != null;
                    if (focused) drawBox = false;
                    if (full && mloa.portals != null)
                    {
                        for (int ip = 0; ip < mloa.portals.Length; ip++)
                        {
                            var portal = mloa.portals[ip];
                            if (portal?.Corners == null) continue;
                            bool isFocus = ReferenceEquals(portal, focusPortal);
                            bool ofRoom = focusRoom != null && (portal._Data.roomFrom == (uint)focusRoom.Index || portal._Data.roomTo == (uint)focusRoom.Index);
                            bool faint = focused && !isFocus && !ofRoom;
                            uint pf = portal._Data.flags;
                            Vector4 pcol = PortalAqua, pfill = PortalAquaFill;
                            if ((pf & 2048u) != 0) { pcol = PortalWater; pfill = PortalWaterFill; }
                            if ((pf & (4u | 16u | 128u | 256u | 512u | 1024u)) != 0) { pcol = PortalMirror; pfill = PortalMirrorFill; }
                            if ((pf & 2u) != 0) { pcol = PortalLink; pfill = PortalLinkFill; }
                            if (isFocus) { pcol = SelColour; pfill = new Vector4(SelColourSoft.X, SelColourSoft.Y, SelColourSoft.Z, 0.30f); }
                            if (faint) { pcol = Faint(pcol); pfill = Faint(pfill, 0.04f); }
                            int pcl = portal.Corners.Length;
                            var wc = new Vector3[pcl];
                            for (int ic = 0; ic < pcl; ic++) wc[ic] = mlop + mlo.Orientation.Multiply(portal.Corners[ic].XYZ());
                            for (int ic = 0; ic < pcl; ic++)
                            {
                                int icn = ic + 1; if (icn >= pcl) icn = 0;
                                var c1 = (ic == 0 && !faint) ? PortalRed : pcol;
                                lineRenderer.AddLine(wc[ic], wc[icn], c1);
                            }
                            if (pcl == 4) triRenderer.AddQuad(wc[0], wc[1], wc[2], wc[3], pfill);
                            else for (int ic = 1; ic + 1 < pcl; ic++) triRenderer.AddTri(wc[0], wc[ic], wc[ic + 1], pfill);
                            if (isFocus)
                            {
                                DrawPortalArrow(mlo, mloa, portal, SelColour);
                                DrawWorldLabel(mlop + mlo.Orientation.Multiply(portal.Center) + new Vector3(0, 0, 0.5f),
                                    $"portal {portal.Index}: {portal._Data.roomFrom} -> {portal._Data.roomTo}", MloLabelSel);
                            }
                        }
                    }
                    if (mloa.rooms != null)
                    {
                        for (int ir = 0; ir < mloa.rooms.Length; ir++)
                        {
                            var room = mloa.rooms[ir];
                            if (room == null) continue;
                            bool isFocus = ReferenceEquals(room, focusRoom);
                            bool ofPortal = focusPortal != null && (focusPortal._Data.roomFrom == (uint)ir || focusPortal._Data.roomTo == (uint)ir);
                            bool faint = focused && !isFocus && !ofPortal;
                            if (ir == 0 || room.RoomName == "limbo")
                            {
                                bbmin = room._Data.bbMin; bbmax = room._Data.bbMax;
                                if (isFocus) DrawWorldLabel(mlop + new Vector3(0, 0, 2.2f), $"{room.Index}: {room.RoomName} (the outside)", MloLabelSel);
                            }
                            else if (full)
                            {
                                var rcol = isFocus ? SelColour : faint ? Faint(SelWhite) : SelWhite;
                                DrawOrientedBox(mlop, mlo.Orientation, room.BBMin_CW, room.BBMax_CW, rcol);
                                if (isFocus)
                                {
                                    var mn = room.BBMin_CW; var mx = room.BBMax_CW;
                                    Vector3 X(float x, float y, float z) => mlop + mlo.Orientation.Multiply(new Vector3(x, y, z));
                                    var fc = new Vector4(SelColourSoft.X, SelColourSoft.Y, SelColourSoft.Z, 0.10f);
                                    var v0 = X(mn.X, mn.Y, mn.Z); var v1 = X(mn.X, mn.Y, mx.Z); var v2 = X(mn.X, mx.Y, mn.Z); var v3 = X(mn.X, mx.Y, mx.Z);
                                    var v4 = X(mx.X, mn.Y, mn.Z); var v5 = X(mx.X, mn.Y, mx.Z); var v6 = X(mx.X, mx.Y, mn.Z); var v7 = X(mx.X, mx.Y, mx.Z);
                                    triRenderer.AddQuad(v0, v1, v3, v2, fc); triRenderer.AddQuad(v4, v6, v7, v5, fc);
                                    triRenderer.AddQuad(v0, v4, v5, v1, fc); triRenderer.AddQuad(v2, v3, v7, v6, fc);
                                    triRenderer.AddQuad(v0, v2, v6, v4, fc); triRenderer.AddQuad(v1, v5, v7, v3, fc);
                                }
                                if (faint) continue;
                                var top = mlop + mlo.Orientation.Multiply(new Vector3(
                                    (room.BBMin_CW.X + room.BBMax_CW.X) * 0.5f, (room.BBMin_CW.Y + room.BBMax_CW.Y) * 0.5f, room.BBMax_CW.Z));
                                DrawWorldLabel(top, $"{room.Index}: {room.RoomName}", isFocus ? MloLabelSel : RoomLabel);
                            }
                        }
                    }
                }
            }
            if (s.MloRoomDef != null && s.MloEntityDef == null)
            {
                pos += ori.Multiply(s.BBOffset); ori = ori * s.BBOrientation;
                bbmin = s.MloRoomDef._Data.bbMin; bbmax = s.MloRoomDef._Data.bbMax;
            }
            if (s.CollisionBounds != null) { bbmin = s.AABB.Minimum; bbmax = s.AABB.Maximum; scale = Vector3.One; }
            if (s.GrassBatch != null) { bbmin = s.GrassBatch.AABBMin; bbmax = s.GrassBatch.AABBMax; scale = Vector3.One; pos = Vector3.Zero; ori = Quaternion.Identity; }
            if (s.TimeCycleModifier != null) { bbmin = s.TimeCycleModifier.BBMin; bbmax = s.TimeCycleModifier.BBMax; pos = Vector3.Zero; ori = Quaternion.Identity; }
            if (s.WaterQuad != null || s.CalmingQuad != null || s.WaveQuad != null) { bbmin = s.AABB.Minimum; bbmax = s.AABB.Maximum; pos = Vector3.Zero; ori = Quaternion.Identity; }
            if (s.BoxOccluder != null)
            {
                var bo = s.BoxOccluder;
                pos = bo.Position; ori = bo.Orientation; bbmin = bo.BBMin; bbmax = bo.BBMax; scale = Vector3.One;
            }
            if (s.OccludeModelTri != null)
            {
                var ot = s.OccludeModelTri;
                var om = ot.Model;
                if (om != null) { bbmin = om._OccludeModel.bmin; bbmax = om._OccludeModel.bmax; }
                pos = Vector3.Zero; ori = Quaternion.Identity; scale = Vector3.One;
                lineRenderer.AddLine(ot.Corner1, ot.Corner2, col);
                lineRenderer.AddLine(ot.Corner2, ot.Corner3, col);
                lineRenderer.AddLine(ot.Corner3, ot.Corner1, col);
            }
            if (s.CollisionVertex != null)
            {
                var vpos = s.CollisionVertex.Position;
                var wpos = pos + ori.Multiply(vpos);
                lineRenderer.AddSphere(wpos, 0.1f, col, 16);
            }
            else if (s.CollisionPoly != null)
            {
                DrawCollisionPolyOutline(s.CollisionPoly, col, s.EntityDef);
                if (!full) drawBox = false;
            }
            if (s.CollisionBounds != null)
            {
                pos += ori.Multiply(s.BBOffset);
                ori = ori * s.BBOrientation;
            }
            if (s.EntityDef != null && s.CollisionBounds == null && s.MloEntityDef == null)
            {
                bool ownedT4 = false;
                PrecisionOwnsBox_T4(s.EntityDef, ref ownedT4);
                if (!ownedT4) DrawEntityBox_V19(s.EntityDef, col, full);
                drawBox = false;
            }
            DrawSelection_SpaceData(in s, col, full, ref pos, ref ori, ref bbmin, ref bbmax, ref drawBox);
            DrawSelection_Light(in s, col, full, ref drawBox);
            if (drawBox)
            {
                if (bbmax.X < bbmin.X) { bbmin = new Vector3(-0.5f); bbmax = new Vector3(0.5f); }
                DrawSelectionBox_V19(pos, ori, bbmin * scale, bbmax * scale, col, full);
            }
        }

        private void DrawOrientedBox(Vector3 pos, Quaternion ori, Vector3 mn, Vector3 mx, Vector4 col)
        {
            Vector3 W(float x, float y, float z) => pos + ori.Multiply(new Vector3(x, y, z));
            var p000 = W(mn.X, mn.Y, mn.Z); var p100 = W(mx.X, mn.Y, mn.Z);
            var p010 = W(mn.X, mx.Y, mn.Z); var p110 = W(mx.X, mx.Y, mn.Z);
            var p001 = W(mn.X, mn.Y, mx.Z); var p101 = W(mx.X, mn.Y, mx.Z);
            var p011 = W(mn.X, mx.Y, mx.Z); var p111 = W(mx.X, mx.Y, mx.Z);
            lineRenderer.AddLine(p000, p100, col); lineRenderer.AddLine(p100, p110, col);
            lineRenderer.AddLine(p110, p010, col); lineRenderer.AddLine(p010, p000, col);
            lineRenderer.AddLine(p001, p101, col); lineRenderer.AddLine(p101, p111, col);
            lineRenderer.AddLine(p111, p011, col); lineRenderer.AddLine(p011, p001, col);
            lineRenderer.AddLine(p000, p001, col); lineRenderer.AddLine(p100, p101, col);
            lineRenderer.AddLine(p110, p111, col); lineRenderer.AddLine(p010, p011, col);
        }

        private void DrawArrowOutline(Vector3 pos, Vector3 dir, Vector3 up, Quaternion ori, float len, float rad, Vector4 colour, bool fill = false)
        {
            Vector3 ax = Vector3.Cross(dir, up);
            Vector3 sx = ax * rad;
            Vector3 sy = up * rad;
            Vector3 sz = dir * len;
            var c = new Vector3[8];
            Vector3 d0 = -sx - sy, d1 = -sx + sy, d2 = +sx - sy, d3 = +sx + sy;
            c[0] = d0; c[1] = d1; c[2] = d2; c[3] = d3;
            c[4] = d0 + sz; c[5] = d1 + sz; c[6] = d2 + sz; c[7] = d3 + sz;
            for (int i = 0; i < 8; i++) c[i] = pos + ori.Multiply(c[i]);
            void L(int a, int b) => lineRenderer.AddLine(c[a], c[b], colour);
            L(0, 1); L(1, 3); L(3, 2); L(2, 0);
            L(4, 5); L(5, 7); L(7, 6); L(6, 4);
            L(0, 4); L(1, 5); L(2, 6); L(3, 7);
            var fc = new Vector4(colour.X, colour.Y, colour.Z, colour.W * 0.28f) * new Vector4(FillHdr / HelperHdr, FillHdr / HelperHdr, FillHdr / HelperHdr, 1.0f);
            if (fill)
            {
                triRenderer.AddQuad(c[0], c[1], c[5], c[4], fc); triRenderer.AddQuad(c[2], c[3], c[7], c[6], fc);
                triRenderer.AddQuad(c[0], c[2], c[6], c[4], fc); triRenderer.AddQuad(c[1], c[3], c[7], c[5], fc);
            }
            var tip = pos + ori.Multiply(dir * (len + rad * 5.0f));
            c[4] += ori.Multiply(d0); c[5] += ori.Multiply(d1); c[6] += ori.Multiply(d2); c[7] += ori.Multiply(d3);
            L(4, 5); L(5, 7); L(7, 6); L(6, 4);
            lineRenderer.AddLine(tip, c[4], colour); lineRenderer.AddLine(tip, c[5], colour);
            lineRenderer.AddLine(tip, c[6], colour); lineRenderer.AddLine(tip, c[7], colour);
            if (fill)
            {
                triRenderer.AddQuad(c[4], c[5], c[7], c[6], fc);
                triRenderer.AddTri(tip, c[4], c[5], fc); triRenderer.AddTri(tip, c[5], c[7], fc);
                triRenderer.AddTri(tip, c[7], c[6], fc); triRenderer.AddTri(tip, c[6], c[4], fc);
            }
        }

        private void DrawLodLightShape(YmapLODLight lodlight)
        {
            var pos = lodlight.Position;
            var dir = lodlight.Direction;
            var tx = lodlight.TangentX;
            var ty = lodlight.TangentY;
            var extent = lodlight.Falloff;
            var innerAngle = lodlight.ConeInnerAngle * 0.012319971f;
            var outerAngle = lodlight.ConeOuterAngleOrCapExt * 0.012319971f;
            if (dir.LengthSquared() < 1e-8f) dir = -Vector3.UnitZ;
            var outerCol = T(UiTheme.Accent, 1.0f);
            switch (lodlight.Type)
            {
                case LightType.Point:
                    lineRenderer.AddCircle(pos, Vector3.UnitX, Vector3.UnitZ, extent, SelWhite, 48);
                    lineRenderer.AddCircle(pos, Vector3.UnitX, Vector3.UnitY, extent, SelWhite, 48);
                    lineRenderer.AddCircle(pos, Vector3.UnitY, Vector3.UnitZ, extent, SelWhite, 48);
                    break;
                case LightType.Spot:
                    {
                        lineRenderer.AddCone(pos, dir, tx, outerAngle, extent, outerCol);
                        lineRenderer.AddCone(pos, dir, tx, innerAngle, extent, SelWhite);
                        if (tx.LengthSquared() < 1e-8f) tx = Vector3.Normalize(dir.GetPerpVec());
                        var tyv = ty.LengthSquared() > 1e-8f ? ty : Vector3.Cross(dir, tx);
                        float ca = Math.Min(outerAngle, 1.45f);
                        float r = (float)Math.Sin(ca) * extent;
                        var baseC = pos + dir * ((float)Math.Cos(ca) * extent);
                        var fillCol = TF(UiTheme.Accent, 1.0f);
                        var apexC = new Vector4(fillCol.X, fillCol.Y, fillCol.Z, 0.10f);
                        var rimC = new Vector4(fillCol.X, fillCol.Y, fillCol.Z, 0.0f);
                        triRenderer.AddCone(pos, baseC, tx, tyv, r, apexC, rimC, 24);
                        break;
                    }
                case LightType.Capsule:
                    {
                        float capExt = lodlight.ConeOuterAngleOrCapExt * 0.25f;
                        var a = pos - dir * (capExt * 0.5f);
                        var b = pos + dir * (capExt * 0.5f);
                        lineRenderer.AddCapsule(a, b, extent, SelWhite);
                        break;
                    }
            }
        }

        private void DrawWorldLabel(Vector3 world, string text, Vector4 col)
        {
            if (string.IsNullOrEmpty(text) || renderingStill) return;
            var clip = Vector4.Transform(new Vector4(world, 1.0f), camera.ViewProjMatrix);
            if (clip.W <= 0.01f) return;
            float nx = clip.X / clip.W, ny = clip.Y / clip.W;
            if (nx < -1.05f || nx > 1.05f || ny < -1.05f || ny > 1.05f) return;
            float sx = (nx + 1.0f) * 0.5f * deviceResources.Width;
            float sy = (1.0f - ny) * 0.5f * deviceResources.Height;
            var dl = ImGuiNET.ImGui.GetForegroundDrawList();
            var size = ImGuiNET.ImGui.CalcTextSize(text);
            var p0 = new System.Numerics.Vector2(sx - size.X * 0.5f - 4, sy - size.Y - 6);
            var p1 = new System.Numerics.Vector2(sx + size.X * 0.5f + 4, sy - 2);
            dl.AddRectFilled(p0, p1, ImGuiNET.ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.06f, 0.08f, 0.12f, 0.72f)), 3.0f);
            dl.AddRect(p0, p1, ImGuiNET.ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(UiTheme.Accent.X, UiTheme.Accent.Y, UiTheme.Accent.Z, 0.6f)), 3.0f);
            dl.AddText(new System.Numerics.Vector2(p0.X + 4, p0.Y + 2), ImGuiNET.ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(col.X, col.Y, col.Z, col.W)), text);
        }

        private void DrawCollisionPolyOutline(BoundPolygon poly, Vector4 col, YmapEntityDef entity)
        {
            if (poly?.Owner == null) return;
            var ori = Quaternion.Identity; var pos = Vector3.Zero; var sca = Vector3.One;
            if (entity != null) { ori = entity.Orientation; pos = entity.Position; sca = entity.Scale; }
            Vector3 W(Vector3 v) => pos + (ori.Multiply(v) * sca);
            if (poly is BoundPolygonTriangle ptri)
            {
                var p1 = W(ptri.Vertex1); var p2 = W(ptri.Vertex2); var p3 = W(ptri.Vertex3);
                lineRenderer.AddLine(p1, p2, col); lineRenderer.AddLine(p2, p3, col); lineRenderer.AddLine(p3, p1, col);
            }
            else if (poly is BoundPolygonSphere psph)
            {
                lineRenderer.AddSphere(W(psph.Position), psph.sphereRadius * 1.03f, col, 24);
            }
            else if (poly is BoundPolygonCapsule pcap)
            {
                lineRenderer.AddCapsule(W(pcap.Vertex1), W(pcap.Vertex2), pcap.capsuleRadius, col);
            }
            else if (poly is BoundPolygonBox pbox)
            {
                var p1 = W(pbox.Vertex1); var p2 = W(pbox.Vertex2); var p3 = W(pbox.Vertex3); var p4 = W(pbox.Vertex4);
                var p5 = (p1 + p2) * 0.5f; var p6 = (p3 + p4) * 0.5f;
                var a1 = (p6 - p5);
                var a2 = (p3 - (p1 + a1)) * 0.5f;
                var a3 = (p4 - (p1 + a1)) * 0.5f;
                DrawBoxAxes(p5, p6, a2, a3, col);
            }
            else if (poly is BoundPolygonCylinder pcyl)
            {
                var p1 = W(pcyl.Vertex1); var p2 = W(pcyl.Vertex2);
                var a1 = Vector3.Normalize(p2 - p1);
                var a2 = Vector3.Normalize(a1.GetPerpVec());
                var a3 = Vector3.Normalize(Vector3.Cross(a1, a2));
                DrawBoxAxes(p1, p2, a2 * pcyl.cylinderRadius, a3 * pcyl.cylinderRadius, col);
            }
        }

        private void DrawBoxAxes(Vector3 p1, Vector3 p2, Vector3 a2, Vector3 a3, Vector4 col)
        {
            var c1 = p1 - a2 - a3; var c2 = p1 - a2 + a3; var c3 = p1 + a2 + a3; var c4 = p1 + a2 - a3;
            var c5 = p2 - a2 - a3; var c6 = p2 - a2 + a3; var c7 = p2 + a2 + a3; var c8 = p2 + a2 - a3;
            lineRenderer.AddLine(c1, c2, col); lineRenderer.AddLine(c2, c3, col); lineRenderer.AddLine(c3, c4, col); lineRenderer.AddLine(c4, c1, col);
            lineRenderer.AddLine(c5, c6, col); lineRenderer.AddLine(c6, c7, col); lineRenderer.AddLine(c7, c8, col); lineRenderer.AddLine(c8, c5, col);
            lineRenderer.AddLine(c1, c5, col); lineRenderer.AddLine(c2, c6, col); lineRenderer.AddLine(c3, c7, col); lineRenderer.AddLine(c4, c8, col);
        }

        private void WorldSelectNearestCandidate(WorldSelectionMode mode)
        {
            var camPos = camera.Position;
            var fwd = camera.GetForward(); if (fwd.LengthSquared() > 1e-8f) fwd.Normalize(); else fwd = Vector3.UnitY;
            Vector3? best = null; float bestD = float.MaxValue; float aboveBy = 40.0f;
            void Consider(Vector3 p, float above = 40.0f)
            {
                var rel = p - camPos;
                float along = Vector3.Dot(rel, fwd);
                if (along < 1.0f || along > 400.0f) return;
                float d = (rel - fwd * along).Length() + along * 0.05f;
                if (d < bestD) { bestD = d; best = p; aboveBy = above; }
            }
            World.SnapshotWalkedYmaps(selYmaps);
            switch (mode)
            {
                case WorldSelectionMode.CarGenerator:
                    foreach (var y in selYmaps) if (y.CarGenerators != null) foreach (var c in y.CarGenerators) if (c != null) Consider(c.Position);
                    break;
                case WorldSelectionMode.MloInstance:
                    foreach (var y in selYmaps) if (y.MloEntities != null) foreach (var m in y.MloEntities) if (m != null) Consider(m.Position);
                    break;
                case WorldSelectionMode.TimeCycleModifier:
                    foreach (var y in selYmaps) if (y.TimeCycleModifiers != null) foreach (var t in y.TimeCycleModifiers) if (t != null) Consider((t.BBMin + t.BBMax) * 0.5f, (t.BBMax.Z - t.BBMin.Z) * 0.5f + 20.0f);
                    break;
                case WorldSelectionMode.Grass:
                    foreach (var y in selYmaps) if (y.GrassInstanceBatches != null) foreach (var g in y.GrassInstanceBatches) if (g != null) Consider((g.AABBMin + g.AABBMax) * 0.5f, (g.AABBMax.Z - g.AABBMin.Z) * 0.5f + 20.0f);
                    break;
                case WorldSelectionMode.LodLights:
                    foreach (var y in selYmaps) if (y.LODLights?.LodLights != null) foreach (var l in y.LODLights.LodLights) if (l != null) Consider(l.Position, 20.0f);
                    break;
                case WorldSelectionMode.Occlusion:
                    foreach (var y in selYmaps)
                    {
                        if (y.BoxOccluders != null) foreach (var b in y.BoxOccluders) if (b != null) Consider(b.Position, b.Size.Z * 0.5f + 20.0f);
                        if (y.OccludeModels != null) foreach (var om in y.OccludeModels) if (om?.Triangles != null && om.Triangles.Length > 0) Consider(om.Triangles[0].Center, 20.0f);
                    }
                    break;
                case WorldSelectionMode.WaterQuad:
                case WorldSelectionMode.CalmingQuad:
                case WorldSelectionMode.WaveQuad:
                    {
                        var w = EnsureSelWater();
                        if (w == null) break;
                        IEnumerable<BaseWaterQuad> qs = mode == WorldSelectionMode.WaterQuad ? (IEnumerable<BaseWaterQuad>)w.WaterQuads
                            : mode == WorldSelectionMode.CalmingQuad ? w.CalmingQuads : w.WaveQuads;
                        foreach (var q in qs) if (q != null) Consider(new Vector3((q.minX + q.maxX) * 0.5f, (q.minY + q.maxY) * 0.5f, q.z ?? 0), 50.0f);
                        break;
                    }
                case WorldSelectionMode.Collision:
                    Consider(new Vector3(camPos.X, camPos.Y, camPos.Z - 1.0f), 0.0f);
                    break;
                case WorldSelectionMode.Light: WorldLightNearestCandidates_I6(Consider); break;
                default: SpaceNearestCandidate(mode, Consider); break;
            }
            if (best == null) { WorldEdit.LastStatus = "no " + mode + " candidates resident"; return; }
            var ray = new Ray(best.Value + new Vector3(0, 0, aboveBy), -Vector3.UnitZ);
            var hit = WorldPickHit(ray, mode);
            if (hit.HasValue) { WorldEdit.Select(hit); WorldEdit.LastStatus = hit.GetNameString("") + " (nearest)"; }
            else WorldEdit.LastStatus = $"nearest {mode} candidate at {bestD:0} m did not pick";
        }

        partial void AfterWorldAutoSelect_Selection()
        {
            var env = Environment.GetEnvironmentVariable("RLE_AUTOMOVE");
            if (string.IsNullOrEmpty(env) || !WorldEdit.Selection.HasValue) return;
            var parts = env.Split(',');
            static float N(string[] a, int i) => i < a.Length && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
            var d = new Vector3(N(parts, 0), N(parts, 1), N(parts, 2));
            var t = WorldEdit.Selection.GizmoTarget();
            if (t == null) { Console.WriteLine("AUTOMOVE: the selection has no gizmo"); return; }
            WorldGizmoDragBegan();
            t.SetPosition(t.Position + d);
            if (t.Key is YmapEntityDef e) WorldEntityChanged(e); else WorldTargetChanged(t);
            WorldGizmoDragEnded();
            screenshotFrames = Math.Max(screenshotFrames, 40);
            if (Environment.GetEnvironmentVariable("RLE_AUTOMOVE_MIN") == "1") ProjWin.Minimized = true;
            Console.WriteLine($"AUTOMOVE {WorldEdit.Selection.TypeName} by {d}: project {(ProjWin.Project != null ? ProjWin.Project.YmapFiles.Count + " ymaps, " + ProjWin.Project.YtypFiles.Count + " ytyps" : "none")} window {(ProjWin.Visible ? "shown" : "hidden")} undo '{WorldHistory.NextUndoName}'");
        }

        private string WorldSelReport()
        {
            var s = WorldEdit.Selection;
            if (!s.HasValue) return "WORLDSEL nothing hit";
            var sb = new System.Text.StringBuilder();
            sb.Append("WORLDSEL ").Append(s.TypeName).Append(' ').Append(s.GetNameString("?"));
            if (s.Light != null) WorldSelReport_Light(in s, sb);
            else if (s.CarGenerator != null)
            {
                var d = s.CarGenerator._CCarGen;
                sb.Append($" | CARGEN model={d.carModel} pop={d.popGroup} len={d.perpendicularLength:0.##} flags={d.flags} pos={s.CarGenerator.Position} drawn={carGenDraw.Count}");
                if (Environment.GetEnvironmentVariable("RLE_DUMPCARGENS") == "1")
                {
                    World.SnapshotWalkedYmaps(selYmaps);
                    var all = new List<YmapCarGen>();
                    foreach (var y in selYmaps) if (y.CarGenerators != null) foreach (var c in y.CarGenerators) if (c != null && c._CCarGen.carModel != 0) all.Add(c);
                    all.Sort((a, b) => (a.Position - camera.Position).LengthSquared().CompareTo((b.Position - camera.Position).LengthSquared()));
                    for (int i = 0; i < Math.Min(8, all.Count); i++)
                        Console.WriteLine($"CARGEN {all[i].Ymap?.Name} model={all[i]._CCarGen.carModel} pos={all[i].Position} d={(all[i].Position - camera.Position).Length():0}");
                }
            }
            else if (s.LodLight != null)
            {
                var l = s.LodLight;
                sb.Append($" | LODLIGHT type={l.Type} pos={l.Position} colour={l.Colour} falloff={l.Falloff:0.##} timeflags={l.TimeFlags:X}");
            }
            else if (s.TimeCycleModifier != null)
            {
                var d = s.TimeCycleModifier.CTimeCycleModifier;
                sb.Append($" | TCM name={d.name} pct={d.percentage} range={d.range} hours={d.startHour}-{d.endHour}");
            }
            else if (s.GrassBatch != null)
            {
                sb.Append($" | GRASS arch={s.GrassBatch.Archetype?.Name} instances={s.GrassBatch.Instances?.Length ?? 0}");
            }
            else if (s.BoxOccluder != null)
            {
                sb.Append($" | BOXOCC pos={s.BoxOccluder.Position} size={s.BoxOccluder.Size}");
            }
            else if (s.OccludeModelTri != null)
            {
                sb.Append($" | OCCTRI model={s.OccludeModelTri.Model?.Index} tri={s.OccludeModelTri.Index}");
            }
            else if (s.WaterQuad != null || s.CalmingQuad != null || s.WaveQuad != null)
            {
                sb.Append($" | WATER box={s.AABB.Minimum}-{s.AABB.Maximum}");
            }
            else if (s.CollisionBounds != null)
            {
                sb.Append($" | COLL bounds={s.CollisionBounds.Type} poly={s.CollisionPoly?.Index ?? -1} mat={(s.CollisionPoly != null ? BoundsMaterialTypes.GetMaterialName(s.CollisionPoly.Material.Type) : "")} dist={s.HitDist:0.##}");
            }
            else if (s.MloEntityDef != null)
            {
                var mloa = s.MloEntityDef.Archetype as MloArchetype;
                sb.Append($" | MLO rooms={mloa?.rooms?.Length ?? 0} portals={mloa?.portals?.Length ?? 0} entities={s.MloEntityDef.MloInstance?.Entities?.Length ?? 0}");
            }
            else if (s.EntityDef != null)
            {
                sb.Append($" | ENTITY arch={s.EntityDef.Archetype?.Name} ymap={s.EntityDef.Ymap?.Name}");
                WorldSelReport_J2(in s, sb);
            }
            else WorldSelReport_SpaceData(in s, sb);
            return sb.ToString();
        }

        partial void RunWorldTestExtras_Selection(Action<string, bool, string> check, Action<Vector3> settle)
        {
            Console.WriteLine("---- selection modes ----");
            var spot = new Vector3(-1150.0f, -1990.0f, 40.0f);
            CameraSequence.ApplyToCamera(camera, spot, 1.4f, 0.45f, settings.FovDeg);
            settle(spot);
            World.SnapshotWalkedYmaps(selYmaps);
            check("the picker sees the walked ymaps", selYmaps.Count > 0, $"{selYmaps.Count} ymaps");

            YmapCarGen cg = null; float cgd = float.MaxValue;
            foreach (var y in selYmaps) if (y.CarGenerators != null) foreach (var c in y.CarGenerators)
            { if (c == null) continue; float d = (c.Position - spot).Length(); if (d < cgd) { cgd = d; cg = c; } }
            check("car generators are resident near the spot", cg != null, cg != null ? $"{cg.NameString()} at {cgd:0} m in {cg.Ymap?.Name}" : "none");
            if (cg != null)
            {
                var ray = new Ray(cg.Position + new Vector3(0, 0, 40.0f), -Vector3.UnitZ);
                var hit = WorldPickHit(ray, WorldSelectionMode.CarGenerator);
                check("CarGenerator mode picks the car generator under the ray", hit.CarGenerator != null,
                      hit.CarGenerator != null ? hit.GetNameString("") + $" at {hit.HitDist:0.#} m" : "nothing");
                if (hit.CarGenerator != null)
                {
                    var t = hit.GizmoTarget();
                    check("a car generator has a gizmo (Z ring, uniform scale)", t != null && t.RotationAxes == WorldWidgetAxis.Z && t.ScaleLockXY, t?.RotationAxes.ToString() ?? "null");
                    var before = GizmoTransform.Capture(t);
                    t.SetOrientation(Quaternion.RotationYawPitchRoll(0, 0, (float)Math.PI * 0.5f));
                    float len = Math.Max(hit.CarGenerator._CCarGen.perpendicularLength * 1.5f, 5.0f);
                    check("CarGen SetOrientation writes orientX/orientY (YmapCarGen rule)",
                          Math.Abs(hit.CarGenerator._CCarGen.orientX) < 0.01f * len + 0.01f && Math.Abs(hit.CarGenerator._CCarGen.orientY - len) < 0.01f * len + 0.01f,
                          $"orientX {hit.CarGenerator._CCarGen.orientX:0.###} orientY {hit.CarGenerator._CCarGen.orientY:0.###} (len {len:0.##})");
                    before.ApplyTo(t);
                }
            }

            YmapEntityDef mlo = null; float mlod = float.MaxValue;
            foreach (var y in selYmaps) if (y.MloEntities != null) foreach (var m in y.MloEntities)
            { if (m == null) continue; float d = (m.Position - spot).Length(); if (d < mlod) { mlod = d; mlo = m; } }
            check("an interior is resident near the spot", mlo != null, mlo != null ? $"{mlo.Archetype?.Name} at {mlod:0} m" : "none");
            if (mlo != null)
            {
                var ray = new Ray(mlo.Position + new Vector3(0, 0, 40.0f), -Vector3.UnitZ);
                var hit = WorldPickHit(ray, WorldSelectionMode.MloInstance);
                check("MloInstance mode picks the interior entity", hit.MloEntityDef != null && hit.EntityDef == hit.MloEntityDef,
                      hit.MloEntityDef != null ? hit.GetNameString("") : "nothing");
                var noray = new Ray(mlo.Position + new Vector3(2.5f, 0, 40.0f), -Vector3.UnitZ);
                var miss = new WorldSelection();
                PickMloInstances(ref noray, noray.Position, ref miss);
                check("MloInstance cube pick is the 3 m box, not the interior's whole extent", !ReferenceEquals(miss.MloEntityDef, mlo),
                      miss.HasValue ? miss.GetNameString("") : "miss (correct)");
            }

            YmapLODLight ll = null; YmapFile lly = null;
            foreach (var y in selYmaps) if (y.LODLights?.LodLights != null && y.LODLights.LodLights.Length > 0) { ll = y.LODLights.LodLights[0]; lly = y; break; }
            check("a _lodlights file is resident near the spot", ll != null, lly?.Name ?? "none");
            if (ll != null)
            {
                var ray = new Ray(ll.Position + new Vector3(0, 0, 30.0f), -Vector3.UnitZ);
                var hit = WorldPickHit(ray, WorldSelectionMode.LodLights);
                check("LodLights mode picks a LOD light under the ray (BVH)", hit.LodLight != null,
                      hit.LodLight != null ? hit.GetNameString("") + $" bvh {(lly.LODLights.BVH != null)}" : "nothing");
            }

            YmapTimeCycleModifier tcm = null;
            foreach (var y in selYmaps) if (y.TimeCycleModifiers != null && y.TimeCycleModifiers.Length > 0) { tcm = y.TimeCycleModifiers[0]; break; }
            if (tcm != null)
            {
                var c = (tcm.BBMin + tcm.BBMax) * 0.5f;
                var ray = new Ray(new Vector3(c.X, c.Y, tcm.BBMax.Z + 20.0f), -Vector3.UnitZ);
                var hit = WorldPickHit(ray, WorldSelectionMode.TimeCycleModifier);
                check("TimeCycleModifier mode picks the modifier box", hit.TimeCycleModifier != null, hit.GetNameString("nothing"));
            }
            else Console.WriteLine("  (no time cycle modifiers resident here - skipped)");

            YmapGrassInstanceBatch gb = null;
            foreach (var y in selYmaps) if (y.GrassInstanceBatches != null && y.GrassInstanceBatches.Length > 0) { gb = y.GrassInstanceBatches[0]; break; }
            if (gb != null)
            {
                var c = (gb.AABBMin + gb.AABBMax) * 0.5f;
                var ray = new Ray(new Vector3(c.X, c.Y, gb.AABBMax.Z + 20.0f), -Vector3.UnitZ);
                var hit = WorldPickHit(ray, WorldSelectionMode.Grass);
                check("Grass mode picks a batch box", hit.GrassBatch != null, hit.GetNameString("nothing"));
            }
            else Console.WriteLine("  (no grass batches resident here - skipped)");

            YmapBoxOccluder bo = null;
            foreach (var y in selYmaps) if (y.BoxOccluders != null && y.BoxOccluders.Length > 0) { bo = y.BoxOccluders[0]; break; }
            if (bo != null)
            {
                var ray = new Ray(bo.Position + new Vector3(0, 0, bo.Size.Z + 20.0f), -Vector3.UnitZ);
                var hit = WorldPickHit(ray, WorldSelectionMode.Occlusion);
                check("Occlusion mode picks an occluder", hit.BoxOccluder != null || hit.OccludeModelTri != null, hit.GetNameString("nothing"));
            }
            else Console.WriteLine("  (no box occluders resident here - skipped)");

            var water = EnsureSelWater();
            check("water.xml quads are available to the picker", water != null && water.WaterQuads.Count > 0, $"{water?.WaterQuads.Count ?? 0} quads");
            if (water != null && water.WaterQuads.Count > 0)
            {
                var q = water.WaterQuads[0];
                var ray = new Ray(new Vector3((q.minX + q.maxX) * 0.5f, (q.minY + q.maxY) * 0.5f, (q.z ?? 0) + 50.0f), -Vector3.UnitZ);
                var hit = WorldPickHit(ray, WorldSelectionMode.WaterQuad);
                check("WaterQuad mode picks a quad", hit.WaterQuad != null, hit.GetNameString("nothing"));
            }

            {
                bool wasOn = panel.WorldShowCollision;
                panel.WorldShowCollision = true;
                var t0 = clock.Elapsed.TotalSeconds;
                while (clock.Elapsed.TotalSeconds - t0 < 30.0)
                {
                    TickWorldCollision();
                    if (ybnIndex != null && collisionDraw.Count > 0) break;
                    System.Threading.Thread.Sleep(50);
                }
                var ray = new Ray(new Vector3(spot.X, spot.Y, 80.0f), -Vector3.UnitZ);
                var hit = WorldPickHit(ray, WorldSelectionMode.Collision);
                check("Collision mode returns real Bounds + polygon under the ray", hit.CollisionBounds != null,
                      hit.CollisionBounds != null ? $"{hit.GetNameString("")} poly {hit.CollisionPoly?.Index} at {hit.HitDist:0.#} m" : $"nothing (index {ybnIndex?.Count ?? 0}, drawn {collisionDraw.Count})");
                panel.CollisionUnderCursor = WorldSelection.Empty;
                WorldPickCollisionUnderCursor(ray, WorldSelectionMode.Entity);
                var cuc = panel.CollisionUnderCursor;
                check("Entity-mode click with the overlay on reports the collision under the cursor", cuc.CollisionBounds != null,
                      cuc.CollisionBounds != null ? $"{cuc.CollisionBounds.GetRootYbn()?.Name ?? cuc.CollisionBounds.GetName()} {cuc.CollisionBounds.Type} poly {cuc.CollisionPoly?.Index} mat {(cuc.CollisionPoly != null ? BoundsMaterialTypes.GetMaterialName(cuc.CollisionPoly.Material.Type) : "-")}" : "nothing");
                var hov = WorldPickCollisionHover(ray);
                check("Collision hover finds the polygon among the files already read", hov.CollisionPoly != null, hov.HasValue ? hov.GetNameString("") : "nothing");
                float streamR = World.EffectiveRadius > 0.0f ? World.EffectiveRadius : World.StreamRadius;
                check("collision overlay range is the LOD ymaps' resident radius", collisionWantedRange >= streamR * WorldStreamer.RangeScaleFor(2u) - 1.0f,
                      $"range {collisionWantedRange:0} m (stream {streamR:0} m x {WorldStreamer.RangeScaleFor(2u):0}), {collisionWantedCount} ybns wanted");
                panel.WorldShowCollision = wasOn;
            }

            if (mlo?.MloInstance?.Entities != null && mlo.MloInstance.Entities.Length > 0)
            {
                var ie = mlo.MloInstance.Entities[0];
                var p0 = mlo.Position; var ip0 = ie.Position;
                var pend = EntityTransformCommand.Begin("Test MLO move", mlo, WorldEntityChanged);
                mlo.SetPosition(p0 + new Vector3(0, 0, 2.0f));
                WorldEntityChanged(mlo);
                var cmd = pend.Complete();
                check("moving an MLO shell moves its interior entities with it", (ie.Position - (ip0 + new Vector3(0, 0, 2.0f))).Length() < 0.01f,
                      $"shell +2 m: entity {ie.Archetype?.Name} moved {(ie.Position - ip0).Z:0.##} m");
                if (cmd != null) { WorldHistory.Push(cmd); TryWorldUndo(); }
                else mlo.SetPosition(p0);
                check("undo puts the interior back", (ie.Position - ip0).Length() < 0.01f && (mlo.Position - p0).Length() < 0.01f,
                      $"entity back at {(ie.Position - ip0).Length():0.###} m, shell {(mlo.Position - p0).Length():0.###} m");
            }

            {
                YmapEntityDef ent = null;
                foreach (var v in World.Visible)
                    if (v?.Ymap != null && v.MloParent == null && v.MloInstance == null && v.Archetype != null &&
                        (ProjWin.Project == null || !ProjWin.Project.ContainsYmap(v.Ymap))) { ent = v; break; }
                if (ent != null)
                {
                    var ymap = ent.Ymap;
                    var hadProject = ProjWin.Project != null;
                    projectAutoAddTestOverride = true;
                    var p0 = ent.Position;
                    var pend = EntityTransformCommand.Begin("Test auto-add", ent, WorldEntityChanged);
                    ent.SetPosition(p0 + new Vector3(0, 0, 0.5f));
                    WorldEntityChanged(ent);
                    var cmd = pend.Complete();
                    check("editing a game entity adds its ymap to the project, unsaved",
                          ProjWin.Project != null && ProjWin.Project.ContainsYmap(ymap) && ymap.HasChanged,
                          $"{ymap.Name} in project {ProjWin.Project?.ContainsYmap(ymap)} changed {ymap.HasChanged}");
                    check("the project window opened on the edited entity", ProjWin.Visible && ReferenceEquals(ProjWin.CurrentEntity, ent),
                          $"visible {ProjWin.Visible} current {ProjWin.CurrentEntity?.Archetype?.Name}");
                    check("the world draws the project's copy (override registered)", projectOverrides.ContainsValue(ymap), $"{projectOverrides.Count} overrides");
                    if (cmd != null) { WorldHistory.Push(cmd); TryWorldUndo(); } else ent.SetPosition(p0);
                    projectAutoAddTestOverride = false;
                    ProjWin.Project?.RemoveYmapFile(ymap);
                    ymap.HasChanged = false;
                    if (!hadProject) { ProjWin.Project = null; ProjWin.Select(null); ProjWin.Visible = false; }
                    RebuildProjectOverrides();
                }
                else Console.WriteLine("  (no game entity to auto-add - skipped)");
            }
        }
    }
}

