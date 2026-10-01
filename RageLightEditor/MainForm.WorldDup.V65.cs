using System;
using System.Collections.Generic;
using System.Linq;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool WorldShiftDuplicate_V65()
        {
            if (WorldEdit.Selected == null) return false;
            var s = WorldEdit.Selection;
            if (s.CollisionBounds != null || s.CollisionPoly != null || s.CollisionVertex != null) return false;

            var sources = WorldEdit.AllSelected_V20().ToList();
            if (sources.Count == 0) return false;

            var made = new List<(YmapEntityDef Clone, YmapEntityDef Source)>();
            foreach (var src in sources)
            {
                YmapEntityDef clone = null;
                try
                {
                    clone = src.MloParent != null ? BuildMloChildClone_V65(src) : BuildWorldClone_V65(src);
                }
                catch (Exception ex) { WorldEdit.LastStatus = "duplicate failed: " + ex.Message; }
                if (clone == null) continue;
                WorldEntityChanged(clone);
                made.Add((clone, src));
            }
            if (made.Count == 0) return false;

            SelectClones_V65(made.Select(x => x.Clone).ToList());
            var snap = made.ToList();
            WorldHistory.Push(
                snap.Count == 1
                    ? "Duplicate " + (snap[0].Clone.Archetype?.Name ?? snap[0].Clone._CEntityDef.archetypeName.ToString())
                    : $"Duplicate {snap.Count} entities",
                () =>
                {
                    foreach (var (c, _) in snap) ReattachClone_V65(c);
                    SelectClones_V65(snap.Select(x => x.Clone).ToList());
                },
                () =>
                {
                    foreach (var (c, _) in snap) DetachClone_V65(c);
                    SelectClones_V65(snap.Select(x => x.Source).ToList());
                });
            WorldEdit.LastStatus = snap.Count == 1
                ? "duplicated - the drag places the copy"
                : $"duplicated {snap.Count} - the drag places the copies";
            return true;
        }

        internal static YmapEntityDef BuildWorldClone_V65(YmapEntityDef src)
        {
            var ymap = src?.Ymap;
            if (ymap == null) return null;
            var cent = src._CEntityDef;
            cent.parentIndex = -1;
            cent.numChildren = 0;
            cent.childLodDist = 0;
            cent.lodLevel = rage__eLodType.LODTYPES_DEPTH_ORPHANHD;
            var clone = new YmapEntityDef(ymap, 0, ref cent);
            clone.SetArchetype(src.Archetype);
            ymap.AddEntity(clone);
            return clone;
        }

        private YmapEntityDef BuildMloChildClone_V65(YmapEntityDef src)
        {
            var owner = src.MloParent;
            var inst = owner?.MloInstance;
            var arch = owner?.Archetype as MloArchetype;
            if (inst == null || arch == null) return null;

            int room = 0, portal = -1, entset = -1;
            var srcMc = inst.TryGetArchetypeEntity(src);
            if (srcMc != null)
            {
                room = arch.GetEntityRoom(srcMc)?.Index ?? -1;
                portal = arch.GetEntityPortal(srcMc)?.Index ?? -1;
                entset = arch.GetEntitySet(srcMc)?.Index ?? -1;
                if (room < 0 && portal < 0 && entset < 0) room = 0;
            }

            var cent = src._CEntityDef;
            var ment = new MCEntityDef(ref cent, arch);
            var clone = new YmapEntityDef(owner, ment, arch.entities?.Length ?? 0);
            arch.AddEntity(clone, room, portal, entset);
            inst.AddEntity(clone);
            clone.SetArchetype(src.Archetype ?? gameFiles?.Cache?.GetArchetype(cent.archetypeName));
            inst.UpdateEntity(clone);
            return clone;
        }

        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<YmapEntityDef, int[]> cloneSlot_U21 =
            new System.Runtime.CompilerServices.ConditionalWeakTable<YmapEntityDef, int[]>();

        private void ReattachClone_V65(YmapEntityDef clone)
        {
            try
            {
                if (clone.MloParent != null)
                {
                    var arch = clone.MloParent.Archetype as MloArchetype;
                    var inst = clone.MloParent.MloInstance;
                    var back = inst?.TryGetArchetypeEntity(clone);
                    if (arch != null && back == null)
                    {
                        int[] slot = cloneSlot_U21.TryGetValue(clone, out var s) ? s : new[] { 0, -1, -1 };
                        arch.AddEntity(clone, slot[0], slot[1], slot[2]);
                        inst?.AddEntity(clone);
                        inst?.UpdateEntity(clone);
                    }
                }
                else clone.Ymap?.AddEntity(clone);
                WorldEntityChanged(clone);
            }
            catch (Exception ex) { WorldEdit.LastStatus = "redo failed: " + ex.Message; }
        }

        private void DetachClone_V65(YmapEntityDef clone)
        {
            try
            {
                if (clone.MloParent != null)
                {
                    var spot = CaptureMloChild_U5(clone);
                    if (spot != null) { cloneSlot_U21.Remove(clone); cloneSlot_U21.Add(clone, new[] { spot.Room, spot.Portal, spot.EntSet }); }
                    (clone.MloParent.Archetype as MloArchetype)?.RemoveEntity(clone);
                    try { clone.MloParent.MloInstance?.DeleteEntity(clone); } catch { }
                    WorldEntityChanged(clone.MloParent);
                }
                else if (clone.Ymap != null)
                {
                    var y = clone.Ymap;
                    y.RemoveEntity(clone);
                    worldRender.Forget(clone);
                    WorldEdit.MarkDirty(y);
                    World.Invalidate();
                }
            }
            catch (Exception ex) { WorldEdit.LastStatus = "undo failed: " + ex.Message; }
        }

        private void SelectClones_V65(List<YmapEntityDef> list)
        {
            if (list == null || list.Count == 0) return;
            WorldEdit.Select(list[0]);
            for (int i = 1; i < list.Count; i++) WorldEdit.Extra_V20.Add(list[i]);
        }

        partial void AfterWorldAutoSelect_Dup_V65()
        {
            if (Environment.GetEnvironmentVariable("RLE_AUTODUP") != "1" || !WorldEdit.Selection.HasValue) return;
            var src = WorldEdit.Selected;
            if (src == null) { Console.WriteLine("AUTODUP: the selection is not an entity"); return; }
            var ymap = src.Ymap;
            var inst = src.MloParent?.MloInstance;
            int before = inst?.Entities?.Length ?? ymap?.AllEntities?.Length ?? -1;
            int archBefore = (src.MloParent?.Archetype as MloArchetype)?.entities?.Length ?? -1;

            bool ok = WorldShiftDuplicate_V65();
            var clone = WorldEdit.Selected;
            int after = inst?.Entities?.Length ?? ymap?.AllEntities?.Length ?? -1;
            int archAfter = (src.MloParent?.Archetype as MloArchetype)?.entities?.Length ?? -1;

            if (ok && clone != null && !ReferenceEquals(clone, src))
            {
                WorldGizmoDragBegan();
                var t = WorldEdit.Selection.GizmoTarget();
                if (t != null)
                {
                    t.SetPosition(t.Position + new SharpDX.Vector3(0, 2.0f, 0));
                    WorldEntityChanged(clone);
                }
                WorldGizmoDragEnded();
            }
            Console.WriteLine($"AUTODUP ok={ok} src={src.Archetype?.Name ?? src._CEntityDef.archetypeName.ToString()} " +
                              $"mlo={src.MloParent != null} entities {before}->{after} archEnts {archBefore}->{archAfter} " +
                              $"cloneSelected={clone != null && !ReferenceEquals(clone, src)} " +
                              $"cloneAt={clone?.Position.X:0.##},{clone?.Position.Y:0.##},{clone?.Position.Z:0.##} srcAt={src.Position.X:0.##},{src.Position.Y:0.##},{src.Position.Z:0.##} " +
                              "undoName=" + WorldHistory.NextUndoName);
            WorldHistory.Undo();
            WorldHistory.Undo();
            int undone = inst?.Entities?.Length ?? ymap?.AllEntities?.Length ?? -1;
            WorldHistory.Redo();
            WorldHistory.Redo();
            int redone = inst?.Entities?.Length ?? ymap?.AllEntities?.Length ?? -1;
            Console.WriteLine($"AUTODUP undo-undo {after}->{undone} redo-redo ->{redone}");
            screenshotFrames = Math.Max(screenshotFrames, 40);
        }

        private void SeqTest_WorldDup_V65(Action<string, bool, string> check)
        {
            try
            {
                var ymap = new YmapFile();
                ymap._CMapData.name = JenkHash.GenHash("rle_v65_dup_map");
                ymap.Loaded = true;

                var cent = new CEntityDef
                {
                    archetypeName = JenkHash.GenHash("prop_bench_01a"),
                    position = new SharpDX.Vector3(10, 20, 30),
                    rotation = new SharpDX.Vector4(0, 0, 0.3826834f, 0.9238795f),
                    scaleXY = 1.0f,
                    scaleZ = 1.0f,
                    flags = 32,
                    lodDist = 180.0f,
                    parentIndex = 7,
                    numChildren = 2,
                    lodLevel = rage__eLodType.LODTYPES_DEPTH_HD,
                    priorityLevel = rage__ePriorityLevel.PRI_REQUIRED,
                    ambientOcclusionMultiplier = 255,
                    artificialAmbientOcclusion = 255,
                };
                var src = new YmapEntityDef(ymap, 0, ref cent);
                ymap.AddEntity(src);

                var clone = BuildWorldClone_V65(src);
                check("v65 duplicate: a copy joins the same ymap",
                      clone != null && ymap.AllEntities?.Length == 2,
                      (ymap.AllEntities?.Length ?? 0) + " entities");
                if (clone == null) return;

                check("v65 duplicate: it stands exactly where the original stands",
                      (clone.Position - src.Position).Length() < 0.0001f,
                      clone.Position.ToString());

                check("v65 duplicate: same prop, same draw distance, same flags",
                      clone._CEntityDef.archetypeName == src._CEntityDef.archetypeName &&
                      Math.Abs(clone._CEntityDef.lodDist - src._CEntityDef.lodDist) < 0.001f &&
                      clone._CEntityDef.flags == src._CEntityDef.flags,
                      clone._CEntityDef.archetypeName.ToString());

                check("v65 duplicate: the copy is its own prop, not a link in the LOD family",
                      clone._CEntityDef.parentIndex == -1 && clone._CEntityDef.numChildren == 0 &&
                      clone._CEntityDef.lodLevel == rage__eLodType.LODTYPES_DEPTH_ORPHANHD,
                      $"parent {clone._CEntityDef.parentIndex} children {clone._CEntityDef.numChildren} {clone._CEntityDef.lodLevel}");

                ymap.RemoveEntity(clone);
                check("v65 duplicate: removing it brings the ymap back exactly",
                      ymap.AllEntities?.Length == 1 && ReferenceEquals(ymap.AllEntities[0], src),
                      (ymap.AllEntities?.Length ?? 0) + " entities");
            }
            catch (Exception ex) { check("v65 duplicate", false, ex.Message); }
        }
    }
}
