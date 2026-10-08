using System;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public struct MloTarget_U21
        {
            public YmapEntityDef Owner;
            public int Room, Portal, EntSet;
            public string Why;
            public bool Valid => Owner?.MloInstance != null && Owner.Archetype is MloArchetype && (Room >= 0 || Portal >= 0 || EntSet >= 0);
        }

        private YmapEntityDef OwnerOf_U21(MloArchetype mlo)
        {
            if (mlo == null) return null;
            var shell = WorldEdit.Selection.MloEntityDef;
            if (shell?.MloInstance != null && (ReferenceEquals(shell.Archetype, mlo) || shell.Archetype?.Hash == mlo.Hash)) return shell;
            return ProjWin?.FindMloInstance?.Invoke(mlo)?.Owner;
        }

        public MloTarget_U21 ResolveMloTarget_U21(Vector3 spawnWorld)
        {
            var t = new MloTarget_U21 { Room = -1, Portal = -1, EntSet = -1 };
            if (ProjWin?.CurrentRoom != null) { t.Owner = OwnerOf_U21(ProjWin.CurrentRoom.OwnerMlo); t.Room = ProjWin.CurrentRoom.Index; t.Why = "room selected in the project"; if (t.Valid) return t; }
            if (ProjWin?.CurrentPortal != null) { t = new MloTarget_U21 { Room = -1, EntSet = -1, Owner = OwnerOf_U21(ProjWin.CurrentPortal.OwnerMlo), Portal = ProjWin.CurrentPortal.Index, Why = "portal selected in the project" }; if (t.Valid) return t; }
            if (ProjWin?.CurrentEntitySet != null) { t = new MloTarget_U21 { Room = -1, Portal = -1, Owner = OwnerOf_U21(ProjWin.CurrentEntitySet.OwnerMlo), EntSet = ProjWin.CurrentEntitySet.Index, Why = "entity set selected in the project" }; if (t.Valid) return t; }

            var sel = WorldEdit.Selection;
            if (sel.MloRoomDef != null && sel.MloEntityDef != null) { t = new MloTarget_U21 { Owner = sel.MloEntityDef, Room = sel.MloRoomDef.Index, Portal = -1, EntSet = -1, Why = "room selected" }; if (t.Valid) return t; }
            if (sel.MloPortalDef != null && sel.MloEntityDef != null) { t = new MloTarget_U21 { Owner = sel.MloEntityDef, Room = -1, Portal = sel.MloPortalDef.Index, EntSet = -1, Why = "portal selected" }; if (t.Valid) return t; }

            var child = sel.EntityDef;
            if (IsMloChild_U5(child))
            {
                var spot = CaptureMloChild_U5(child);
                if (spot != null)
                {
                    t = new MloTarget_U21 { Owner = spot.Owner, Room = spot.Room, Portal = spot.Portal, EntSet = spot.EntSet, Why = "next to the selected interior prop" };
                    if (t.Valid) return t;
                }
            }

            var room = FindCameraRoom(spawnWorld, out var inst, out int roomIndex);
            if (room == null) room = FindCameraRoom(camera.Position, out inst, out roomIndex);
            if (room != null && inst?.Owner != null)
            {
                t = new MloTarget_U21 { Owner = inst.Owner, Room = roomIndex, Portal = -1, EntSet = -1, Why = "you are inside room " + (room.RoomName ?? roomIndex.ToString()) };
                if (t.Valid) return t;
            }
            return new MloTarget_U21 { Room = -1, Portal = -1, EntSet = -1 };
        }

        public static CEntityDef NewMloEntityDef_U21(uint archetypeHash, float lodDist, Vector3 local, Quaternion localRotation)
        {
            var cent = new CEntityDef
            {
                archetypeName = new MetaHash(archetypeHash),
                position = local,
                rotation = new Vector4(localRotation.X, localRotation.Y, localRotation.Z, localRotation.W),
                scaleXY = 1.0f,
                scaleZ = 1.0f,
                flags = 1572865,
                parentIndex = -1,
                lodDist = lodDist > 0 ? lodDist : 100.0f,
                childLodDist = 0,
                lodLevel = rage__eLodType.LODTYPES_DEPTH_HD,
                priorityLevel = rage__ePriorityLevel.PRI_REQUIRED,
                ambientOcclusionMultiplier = 255,
                artificialAmbientOcclusion = 255,
            };
            return cent;
        }

        public static Vector3 WorldToInterior_U21(YmapEntityDef owner, Vector3 world) =>
            Vector3.Transform(world - owner.Position, Quaternion.Invert(owner.Orientation));

        public YmapEntityDef AddMloEntity_U21(MloTarget_U21 t, uint archetypeHash, Archetype archetype, Vector3 world, CEntityDef? template = null)
        {
            if (!t.Valid) return null;
            var mlo = (MloArchetype)t.Owner.Archetype;
            var inst = t.Owner.MloInstance;
            var local = WorldToInterior_U21(t.Owner, world);
            CEntityDef cent;
            if (template.HasValue)
            {
                cent = template.Value;
                cent.position = local;
                cent.parentIndex = -1;
                if (archetypeHash != 0) cent.archetypeName = new MetaHash(archetypeHash);
            }
            else cent = NewMloEntityDef_U21(archetypeHash, archetype?.LodDist ?? 100.0f, local, Quaternion.Identity);

            var resolved = archetype ?? ProjWin?.Project?.FindArchetype(cent.archetypeName, gameFiles?.Cache) ?? gameFiles?.Cache?.GetArchetype(cent.archetypeName);
            LastMloAddWarning_U30 = resolved == null
                ? $"{(uint.TryParse(cent.archetypeName.ToString(), out _) ? "This prop (#" + cent.archetypeName.Hash + ")" : cent.archetypeName.ToString())} is not in the game or in your project's .ytyp files, so it can't be drawn - add the .ytyp that defines it to the project"
                : null;
            if (LastMloAddWarning_U30 != null) AppLog_U21.Warn(LastMloAddWarning_U30);
            var ment = new MCEntityDef(ref cent, mlo);
            var e = new YmapEntityDef(t.Owner, ment, mlo.entities?.Length ?? 0);
            if (!mlo.AddEntity(e, t.Room, t.Portal, t.EntSet)) return null;
            inst.AddEntity(e);
            e.SetArchetype(resolved);
            inst.UpdateEntity(e);
            if (mlo.Ytyp != null) mlo.Ytyp.HasChanged = true;
            WorldEntityChanged(e);

            var spot = CaptureMloChild_U5(e);
            var live = e;
            if (spot != null)
                WorldHistory.Push(new DelegateCommand("Add interior entity",
                    () => { var back = RestoreMloChild_U5(spot); if (back != null) { live = back; spot.Live = back; } },
                    () => { if (live != null) { RemoveMloChild_U5(live); if (ReferenceEquals(WorldEdit.Selected, live)) WorldEdit.Deselect(); } }));
            return e;
        }

        public string LastMloAddWarning_U30 { get; private set; }

        public static string MloTargetText_U21(MloTarget_U21 t)
        {
            if (!t.Valid) return "";
            var mlo = t.Owner.Archetype as MloArchetype;
            string where = t.Room >= 0 ? "room " + (mlo?.rooms != null && t.Room < mlo.rooms.Length ? mlo.rooms[t.Room]?.RoomName ?? t.Room.ToString() : t.Room.ToString())
                         : t.Portal >= 0 ? "portal " + t.Portal
                         : "entity set " + (mlo?.entitySets != null && t.EntSet < mlo.entitySets.Length ? MloEditor.GetEntitySetName(mlo.entitySets[t.EntSet]) : t.EntSet.ToString());
            return (t.Owner.Archetype?.Name ?? "interior") + " " + where;
        }

        private bool ProjectNewEntityInInterior_U21(YmapEntityDef copy)
        {
            var spawn = camera.Position + camera.GetForward() * 5.0f;
            var t = ResolveMloTarget_U21(spawn);
            if (!t.Valid) return false;
            uint hash = copy?.Archetype?.Hash ?? copy?._CEntityDef.archetypeName.Hash ?? JenkHash.GenHash("v_ind_chickensx3");
            var arch = copy?.Archetype ?? ProjWin?.Project?.FindArchetype(new MetaHash(hash), gameFiles?.Cache) ?? gameFiles?.Cache?.GetArchetype(hash);
            CEntityDef? tpl = copy != null ? copy._CEntityDef : (CEntityDef?)null;
            if (copy != null && copy.Ymap != null) tpl = null;
            var e = AddMloEntity_U21(t, hash, arch, spawn, tpl);
            if (e == null) { ProjWin.Status = "could not add an entity to " + MloTargetText_U21(t); return true; }
            ProjWin.Select(e);
            WorldEdit.Select(e);
            ProjWin.Status = LastMloAddWarning_U30 ?? ("new entity in " + MloTargetText_U21(t) + " (" + t.Why + ")");
            WorldEdit.LastStatus = ProjWin.Status;
            return true;
        }
    }
}
