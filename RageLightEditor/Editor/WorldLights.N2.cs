using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CodeWalker;
using CodeWalker.GameFiles;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public partial class WorldLights
    {
        public static readonly string CompareArch = Environment.GetEnvironmentVariable("RLE_LIGHTCOMPARE") ?? "";
        public static readonly bool InteriorDump = Environment.GetEnvironmentVariable("RLE_INTLIGHTDUMP") == "1";
        public static readonly bool InteriorPriorityDisabled = Environment.GetEnvironmentVariable("RLE_NOINTLIGHTPRIO") == "1";
        public static readonly bool InteriorFlagsDisabled = Environment.GetEnvironmentVariable("RLE_NOLIGHTINTFLAG") == "1";

        public YmapEntityDef CameraInterior;
        public int CameraRoom = -1;
        public Func<YmapEntityDef, int> RoomOfEntity;

        internal byte PrioOf(YmapEntityDef e)
        {
            if (InteriorPriorityDisabled || CameraInterior == null || e == null) return 2;
            if (ReferenceEquals(e, CameraInterior)) return 1;
            if (!ReferenceEquals(e.MloParent, CameraInterior)) return 2;
            if (CameraRoom < 0) return 1;
            int r = RoomOfEntity?.Invoke(e) ?? -1;
            return r == CameraRoom ? (byte)0 : (byte)1;
        }

        internal bool AllowedByInteriorFlags(uint flags)
        {
            if (InteriorFlagsDisabled) return true;
            const uint interiorOnly = 1u << 0, exteriorOnly = 1u << 1, both = 1u << 14;
            if ((flags & both) != 0) return true;
            bool inside = CameraInterior != null;
            if ((flags & interiorOnly) != 0) return inside;
            if ((flags & exteriorOnly) != 0) return !inside;
            return true;
        }

        internal static Vector3 SaneScale(Vector3 s)
        {
            if (!(s.X > 0.0f)) s.X = 1.0f;
            if (!(s.Y > 0.0f)) s.Y = s.X;
            if (!(s.Z > 0.0f)) s.Z = 1.0f;
            return s;
        }

        public List<Scene.VolumeDraw> VolumesOut;
        public int VolumesEmitted { get; private set; }

        internal void EmitVolume_N2(LightAttributes la, in Vector3 wpos, in Quaternion ori, in Vector3 localDir, float flash)
        {
            var outList = VolumesOut;
            if (outList == null || la == null) return;
            if ((la.Flags & LightDefs.FlagDrawVolume) == 0 || la.VolumeIntensity <= 0.001f) return;
            var dir = ori.Multiply(localDir);
            if (dir.LengthSquared() > 1e-9f) dir.Normalize();
            outList.Add(new Scene.VolumeDraw
            {
                Pos = wpos,
                Dir = dir,
                Colour = new Vector3(la.ColorR, la.ColorG, la.ColorB) / 255.0f * flash,
                OuterColour = new Vector3(la.VolumeOuterColorR, la.VolumeOuterColorG, la.VolumeOuterColorB) / 255.0f,
                OuterAngleRad = Math.Max(la.ConeInnerAngle, la.ConeOuterAngle) * 0.01745329f,
                Falloff = la.Falloff,
                SizeScale = Math.Clamp(la.VolumeSizeScale, 0.05f, 10.0f),
                Intensity = la.VolumeIntensity,
                Type = (byte)la.Type,
                ExtentX = la.Extent.X,
                HasOuter = (la.Flags & LightDefs.FlagVolumeOuterColour) != 0,
                InnerAngleRad = Math.Min(la.ConeInnerAngle, la.ConeOuterAngle) * 0.01745329f,
                FalloffExponent = la.FalloffExponent,
                LightIntensity = la.Intensity,
                OuterIntensity = la.VolumeOuterIntensity,
                OuterExponent = la.VolumeOuterExponent,
            });
            VolumesEmitted++;
        }

        public int RoomCandidates, InteriorCandidates;
        public int RoomEmitted, InteriorEmitted;
        public int FlagSkipped;

        internal void ResetFrameStats_N2()
        {
            RoomCandidates = 0; InteriorCandidates = 0; RoomEmitted = 0; InteriorEmitted = 0; FlagSkipped = 0;
            VolumesEmitted = 0;
        }

        internal void CountEmitted_N2(byte prio)
        {
            if (prio == 0) { RoomEmitted++; InteriorEmitted++; }
            else if (prio == 1) InteriorEmitted++;
        }

        public string InteriorLightStatus_N2 =>
            CameraInterior == null
                ? $"outside · {LightsEmitted} lit of {LitCandidates} (flags skipped {FlagSkipped})"
                : $"{CameraInterior.Archetype?.Name} room {CameraRoom} · room {RoomEmitted}/{RoomCandidates} · interior {InteriorEmitted}/{InteriorCandidates} · total {LightsEmitted}/{LitCandidates} (flags skipped {FlagSkipped})";

        private static bool CompareMatches(string archName, uint hash)
        {
            if (CompareArch.Length == 0) return false;
            if (CompareArch == "*") return true;
            if (!string.IsNullOrEmpty(archName) && archName.IndexOf(CompareArch, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return uint.TryParse(CompareArch, out uint h) && h == hash;
        }

        private readonly HashSet<uint> compared = new HashSet<uint>();

        internal void CompareDump_N2(YmapEntityDef e, LightDef[] defs, int hour, Vector3 cameraPos)
        {
            var arch = e?.Archetype;
            if (arch == null || defs == null) return;
            if (!CompareMatches(arch.Name, arch.Hash)) return;
            if (!compared.Add(arch.Hash)) return;
            Console.WriteLine($"LIGHTCOMPARE world {arch.Name} #{arch.Hash} entity @ {Fmt(e.Position)} ori {e.Orientation} scale {e.Scale} " +
                              $"interior {(e.MloParent?.Archetype?.Name ?? "-")} room {(RoomOfEntity?.Invoke(e) ?? -1)} lights {defs.Length}");
            var scale = SaneScale(e.Scale);
            for (int i = 0; i < defs.Length; i++)
            {
                var la = defs[i].L;
                if (la == null) { Console.WriteLine($"  [{i}] null"); continue; }
                var wpos = e.Orientation.Multiply(defs[i].Pos * scale) + e.Position;
                Console.WriteLine("  " + DescribeLight(i, la, defs[i].Pos, wpos, hour, Vector3.Distance(wpos, cameraPos)));
            }
        }

        public static string DescribeLight(int index, LightAttributes la, Vector3 localPos, Vector3 worldPos, int hour, float camDist)
        {
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $"[{index}] type {(byte)la.Type} bone {la.BoneId} local {Fmt(localPos)} world {Fmt(worldPos)} camDist {camDist:0.0}");
            sb.Append(CultureInfo.InvariantCulture, $" rgb {la.ColorR},{la.ColorG},{la.ColorB} intensity {la.Intensity:0.###} falloff {la.Falloff:0.###} exp {la.FalloffExponent:0.###}");
            sb.Append(CultureInfo.InvariantCulture, $" cone {la.ConeInnerAngle:0.##}/{la.ConeOuterAngle:0.##} extent {Fmt(la.Extent)}");
            sb.Append(CultureInfo.InvariantCulture, $" flags 0x{la.Flags:X} flashiness {la.Flashiness} timeFlags 0x{la.TimeFlags:X} on@{hour} {LightDefs.IsActiveAtHour(la.TimeFlags, hour)}");
            sb.Append(CultureInfo.InvariantCulture, $" shadowBlur {la.ShadowBlur} corona {la.CoronaSize:0.##}/{la.CoronaIntensity:0.##} zbias {la.CoronaZBias:0.##}");
            sb.Append(CultureInfo.InvariantCulture, $" volume {la.VolumeIntensity:0.###}x{la.VolumeSizeScale:0.###} cullPlane {((la.Flags & LightDefs.FlagCullingPlane) != 0 ? 1 : 0)} proj {la.ProjectedTextureHash}");
            return sb.ToString();
        }

        public static string DescribeGpuLight(int index, in GpuLight g)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"  GPU[{index}] pos {Fmt(g.Position)} colour {Fmt(g.Colour)} intensity {g.Intensity:0.###} falloff {g.Falloff:0.###} exp {g.FalloffExponent:0.###} " +
                $"dir {Fmt(g.Direction)} tanX {Fmt(g.TangentX)} cone {g.ConeInnerAngle:0.####}/{g.ConeOuterAngle:0.####} type {g.Type} " +
                $"capsule {Fmt(g.CapsuleExtent)} flags 0x{g.Flags:X} shadowSlot {g.ShadowSlot:0} blur {g.ShadowBlur:0.###} projTex {g.ProjTexIndex:0} cullPlane {g.CullingPlaneEnable}");
        }

        private static string Fmt(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"({v.X:0.###},{v.Y:0.###},{v.Z:0.###})");

        public void CompareDumpRows_N2(GpuLight[] outLights, int count, List<LightAttributes> sources)
        {
            if (CompareArch.Length == 0 || comparedRows || sources == null || compareRowLights.Count == 0) return;
            int printed = 0;
            for (int i = 0; i < count && i < sources.Count; i++)
            {
                var la = sources[i];
                if (la == null) continue;
                if (!compareRowLights.Contains(la)) continue;
                Console.WriteLine(DescribeGpuLight(i, in outLights[i]));
                printed++;
            }
            if (printed > 0) comparedRows = true;
        }
        private bool comparedRows;
        private readonly HashSet<LightAttributes> compareRowLights = new HashSet<LightAttributes>();
        internal void NoteCompareRowLight_N2(YmapEntityDef e, LightAttributes la)
        {
            if (CompareArch.Length == 0 || la == null) return;
            var arch = e?.Archetype;
            if (arch == null || !CompareMatches(arch.Name, arch.Hash)) return;
            compareRowLights.Add(la);
        }

        private int intDumpFrame;

        public void InteriorDump_N2(IReadOnlyList<YmapEntityDef> visible, int hour, Vector3 cameraPos)
        {
            if (!InteriorDump || CameraInterior == null) return;
            if (intDumpFrame++ % 60 != 0) return;
            int ents = 0, withArch = 0, registered = 0, notAsked = 0, lightsTotal = 0;
            int offAtHour = 0, coronaOnly = 0, tooFar = 0, entityFar = 0, live = 0;
            var missing = new List<string>();
            float maxD2 = MaxDistance * MaxDistance;
            for (int i = 0; visible != null && i < visible.Count; i++)
            {
                var e = visible[i];
                if (e == null || !ReferenceEquals(e.MloParent, CameraInterior)) continue;
                ents++;
                var a = e.Archetype;
                if (a == null) continue;
                withArch++;
                if (byArchetype.TryGetValue(a.Hash, out var defs))
                {
                    registered++;
                    float reach = MaxDistance + e.BSRadius;
                    bool entityInRange = Vector3.DistanceSquared(e.Position, cameraPos) <= reach * reach;
                    var scale = SaneScale(e.Scale);
                    for (int k = 0; k < defs.Length; k++)
                    {
                        var la = defs[k].L; if (la == null) continue;
                        lightsTotal++;
                        if (!entityInRange) { entityFar++; continue; }
                        if (!LightDefs.IsActiveAtHour(la.TimeFlags, hour)) { offAtHour++; continue; }
                        var wpos = e.Orientation.Multiply(defs[k].Pos * scale) + e.Position;
                        if (Vector3.DistanceSquared(wpos, cameraPos) > maxD2) { tooFar++; continue; }
                        if ((la.Flags & LightDefs.FlagCoronaOnly) != 0) { coronaOnly++; continue; }
                        live++;
                    }
                }
                else if (!asked.Contains(a.Hash))
                {
                    notAsked++;
                    if (missing.Count < 12) missing.Add(a.Name ?? a.Hash.ToString());
                }
            }
            Console.WriteLine($"INTLIGHTS {CameraInterior.Archetype?.Name} room {CameraRoom}: entities {ents} ({withArch} with an archetype), " +
                              $"archetypes with lights {registered}, never asked for a drawable {notAsked}{(missing.Count > 0 ? " [" + string.Join(", ", missing) + "]" : "")}, " +
                              $"lights on them {lightsTotal} = {live} live + {offAtHour} off at hour {hour} + {coronaOnly} corona-only + {tooFar} past the light range + {entityFar} on a far entity | {InteriorLightStatus_N2}");
        }

        public static void InteriorPriorityTest_N2(Action<string, bool, string> check)
        {
            var wl = new WorldLights();
            check("n2 lights: outside, every light is one band", wl.PrioOf(null) == 2, wl.PrioOf(null).ToString());
            check("n2 lights: outside, an exterior-only light is lit", wl.AllowedByInteriorFlags(1u << 1), "bit 1");
            check("n2 lights: outside, an interior-only light is not", !wl.AllowedByInteriorFlags(1u << 0), "bit 0");

            var shell = new YmapEntityDef();
            var inRoom = new YmapEntityDef { MloParent = shell };
            var otherRoom = new YmapEntityDef { MloParent = shell };
            var street = new YmapEntityDef();
            wl.CameraInterior = shell; wl.CameraRoom = 3;
            wl.RoomOfEntity = e => ReferenceEquals(e, inRoom) ? 3 : ReferenceEquals(e, otherRoom) ? 5 : -1;
            check("n2 lights: the camera's own room is band 0", wl.PrioOf(inRoom) == 0, wl.PrioOf(inRoom).ToString());
            check("n2 lights: another room of the same interior is band 1", wl.PrioOf(otherRoom) == 1, wl.PrioOf(otherRoom).ToString());
            check("n2 lights: the shell itself is band 1", wl.PrioOf(shell) == 1, wl.PrioOf(shell).ToString());
            check("n2 lights: the street is band 2", wl.PrioOf(street) == 2, wl.PrioOf(street).ToString());
            check("n2 lights: inside, an exterior-only light is not lit", !wl.AllowedByInteriorFlags(1u << 1), "bit 1");
            check("n2 lights: inside, an interior-only light is lit", wl.AllowedByInteriorFlags(1u << 0), "bit 0");
            check("n2 lights: 'both' beats either", wl.AllowedByInteriorFlags((1u << 1) | (1u << 14)), "bit 14");
            check("n2 lights: an unflagged light is always lit", wl.AllowedByInteriorFlags(0), "0");

            var s = SaneScale(new Vector3(1, 1, 0));
            check("n2 lights: a zero scaleZ is 1, not a flattened light", s.Z == 1.0f && s.X == 1.0f, s.ToString());
            s = SaneScale(new Vector3(0, 0, 0));
            check("n2 lights: an unauthored scale is identity", s == Vector3.One, s.ToString());
            s = SaneScale(new Vector3(2, 2, 3));
            check("n2 lights: a real scale is kept", s == new Vector3(2, 2, 3), s.ToString());

            var wl2 = new WorldLights { MaxLights = 4 };
            wl2.CameraInterior = shell; wl2.CameraRoom = 1;
            wl2.RoomOfEntity = e => ReferenceEquals(e, inRoom) ? 1 : -1;
            wl2.ResetLitSet();
            wl2.candidates.Clear();
            for (int i = 0; i < 4; i++)
                wl2.candidates.Add(new Candidate { Dist = 1.0f + i, Key = KeyOf(new Vector3(i, 0, 0), i), Prio = 2, G = new GpuLight { Position = new Vector3(i, 0, 0) } });
            for (int i = 0; i < 4; i++)
                wl2.candidates.Add(new Candidate { Dist = 400.0f + i, Key = KeyOf(new Vector3(0, 20 + i, 0), i), Prio = 0, G = new GpuLight { Position = new Vector3(0, 20 + i, 0) } });
            wl2.SelectLit(4, 0.0f);
            int roomLit = 0;
            foreach (var k in wl2.emitIdx) if (wl2.candidates[k].Prio == 0) roomLit++;
            check("n2 lights: the room's own lamps take the cap before the street's", roomLit == 4, $"{roomLit} of 4 room lights lit");
        }
    }
}

