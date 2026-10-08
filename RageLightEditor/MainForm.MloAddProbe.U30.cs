using System;
using System.Linq;
using CodeWalker.GameFiles;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private int mloAddProbeStage_U30, mloAddProbeFrames_U30;
        private YmapEntityDef mloAddProbeEnt_U30;

        private void ServiceMloAddProbe_U30()
        {
            var spec = Environment.GetEnvironmentVariable("RLE_MLOADDPROBE");
            if (string.IsNullOrEmpty(spec) || mloAddProbeStage_U30 < 0 || panel == null || !panel.WorldMode || !worldBuilt) return;
            mloAddProbeFrames_U30++;
            screenshotFrames = Math.Max(screenshotFrames, 3);
            if (mloAddProbeStage_U30 == 0 && mloAddProbeFrames_U30 > 90)
            {
                var owner = World.Visible.Where(v => v?.MloInstance != null && v.Archetype is MloArchetype m && (m.rooms?.Length ?? 0) > 1)
                    .OrderBy(v => Vector3.Distance(v.Position, camera.Position)).FirstOrDefault();
                if (owner == null && mloAddProbeFrames_U30 < 600)
                {
                    var far = World.Nodes.Where(n => n.Ymap?.MloEntities != null).SelectMany(n => n.Ymap.MloEntities)
                        .Where(v => v?.MloInstance != null && v.Archetype is MloArchetype m && (m.rooms?.Length ?? 0) > 1)
                        .OrderBy(v => Vector3.Distance(v.Position, camera.Position)).FirstOrDefault();
                    if (far != null && mloAddProbeFrames_U30 == 91)
                    {
                        Console.WriteLine($"MLOADDPROBE flying to {far.Archetype.Name} at {far.Position}");
                        camera.Target = far.Position + new Vector3(0, 0, 2); camera.Distance = 25; camera.SnapSmoothing(); camera.Update();
                    }
                    if (far != null && mloAddProbeFrames_U30 < 300) return;
                    owner = far;
                }
                if (owner == null) { Console.WriteLine("MLOADDPROBE no interior in view"); mloAddProbeStage_U30 = -1; return; }
                var mlo = (MloArchetype)owner.Archetype;
                int room = 1;
                var r = mlo.rooms[room];
                var localCentre = (r._Data.bbMin + r._Data.bbMax) * 0.5f;
                var world = owner.Position + Vector3.Transform(localCentre, owner.Orientation);
                uint hash = JenkHash.GenHash(spec == "1" || spec == "custom" ? "prop_chair_01a" : spec.ToLowerInvariant());
                var arch = gameFiles?.Cache?.GetArchetype(hash);
                if (spec == "custom")
                {
                    var def = new Editor.ArchetypeDef { Name = "rle_u30_custom_prop", TextureDict = "rle_u30_custom_prop", BbMin = new Vector3(-0.5f), BbMax = new Vector3(0.5f), BsRadius = 0.9f };
                    var y = Editor.ArchetypeBuilder.BuildYtyp("rle_u30_custom", new[] { def });
                    if (ProjWin.Project == null) ProjWin.Project = new Editor.CwProject { Name = "probe" };
                    ProjWin.Project.AddYtypFile(y);
                    hash = JenkHash.GenHash("rle_u30_custom_prop");
                    arch = null;
                    Console.WriteLine($"MLOADDPROBE custom archetype in project, game cache knows it: {gameFiles?.Cache?.GetArchetype(hash) != null}");
                }
                var t = new MloTarget_U21 { Owner = owner, Room = room, Portal = -1, EntSet = -1, Why = "probe" };
                mloAddProbeEnt_U30 = AddMloEntity_U21(t, hash, arch, world);
                Console.WriteLine($"MLOADDPROBE interior {mlo.Name} room {room} {r.RoomName} at {world}, archetype {(arch == null ? "NOT FOUND" : arch.Name)}, added {(mloAddProbeEnt_U30 != null)}, entities now {mlo.entities?.Length}, inst entities {owner.MloInstance.Entities?.Length}");
                if (mloAddProbeEnt_U30 != null) { WorldEdit.Select(mloAddProbeEnt_U30); panel.RequestWorldEntityGoto = true; }
                mloAddProbeStage_U30 = 1; mloAddProbeFrames_U30 = 0;
                return;
            }
            if (mloAddProbeStage_U30 == 1 && mloAddProbeFrames_U30 > 120)
            {
                var e = mloAddProbeEnt_U30;
                bool visible = e != null && World.Visible.Contains(e);
                bool drawn = e != null && worldRender.LiveInstances.Any(kv => ReferenceEquals(kv.Key, e) && kv.Value != null && kv.Value.Count > 0);
                Console.WriteLine($"MLOADDPROBE result: archetype {(e?.Archetype?.Name ?? "null")}, in visible list {visible}, drawn {drawn}, position {e?.Position}, warning {LastMloAddWarning_U30 ?? "none"}");
                mloAddProbeStage_U30 = -1;
            }
        }
    }
}
