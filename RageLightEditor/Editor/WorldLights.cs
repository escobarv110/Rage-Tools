using System;
using System.Collections.Generic;
using CodeWalker;
using CodeWalker.GameFiles;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public partial class WorldLights
    {
        public struct LightDef
        {
            public LightAttributes L;
            public Vector3 Pos, Dir, Tan;
            public Matrix Bone;
        }

        private readonly Dictionary<uint, LightDef[]> byArchetype = new Dictionary<uint, LightDef[]>();
        private readonly HashSet<uint> asked = new HashSet<uint>();

        public int MaxLights = 8192;
        public float MaxDistance = 1000.0f;
        public bool Enabled = true;

        public int ArchetypesWithLights => byArchetype.Count;
        public int LightsInView { get; private set; }
        public int LightsEmitted { get; private set; }

        public void Register(uint archetypeHash, DrawableBase drawable)
        {
            if (!asked.Add(archetypeHash)) return;
            var defs = Extract(drawable);
            if (defs != null && defs.Length > 0) { byArchetype[archetypeHash] = defs; RememberDrawable(archetypeHash, drawable); RememberOriginal_U25(archetypeHash, defs); }
        }

        public bool Has(uint archetypeHash) => byArchetype.ContainsKey(archetypeHash);

        public void Clear()
        {
            byArchetype.Clear();
            asked.Clear();
            ForgetDrawables();
            ResetLitSet();
        }

        public static LightDef[] Extract(DrawableBase db)
        {
            if (db == null) return null;
            var dd = db as Drawable;
            var fd = db as FragDrawable;
            var skel = db.Skeleton;
            LightAttributes[] attrs = null;
            if (dd != null) attrs = dd.LightAttributes?.data_items;
            else if (fd != null)
            {
                var frag = fd.OwnerFragment;
                skel = skel ?? frag?.Drawable?.Skeleton;
                attrs = frag?.LightAttributes?.data_items;
            }
            if (attrs == null || attrs.Length == 0) return null;

            var bones = skel?.BonesMap;
            var defs = new LightDef[attrs.Length];
            for (int i = 0; i < attrs.Length; i++)
            {
                var la = attrs[i];
                var xform = Matrix.Identity;
                if (bones != null && la != null && bones.TryGetValue(la.BoneId, out Bone bone) && bone != null)
                    xform = bone.AbsTransform;
                if (la == null) continue;
                defs[i] = new LightDef
                {
                    L = la,
                    Pos = xform.Multiply(la.Position),
                    Dir = xform.MultiplyRot(la.Direction),
                    Tan = xform.MultiplyRot(la.Tangent),
                    Bone = xform,
                };
            }
            return defs;
        }

        private struct Candidate
        {
            public GpuLight G;
            public LightAttributes L;
            public float Dist;
            public ulong Key;
            public byte Prio;
        }
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly HashSet<long> hdCells = new HashSet<long>();

        public bool LodLightsEnabled = true;
        public float LodLightRange = 0.0f;
        public int LodLightsInView { get; private set; }

        private static long Cell(Vector3 p) =>
            ((long)Math.Floor(p.X * 0.5f) & 0xFFFFF) | (((long)Math.Floor(p.Y * 0.5f) & 0xFFFFF) << 20) | (((long)Math.Floor(p.Z * 0.5f) & 0xFFFFF) << 40);

        public int Build(IReadOnlyList<YmapEntityDef> visible, GpuLight[] outLights, List<LightAttributes> sourcesOut,
                         Vector3 cameraPos, int hour, bool animateFlash, float time,
                         List<(Vector3 pos, Vector3 colour, float size, float intensity)> coronasOut,
                         IEnumerable<YmapFile> residentYmaps = null, int residentVersion = -1, BoundingFrustum? frustum = null)
        {
            buildClock.Restart();
            sourcesOut?.Clear();
            LightsInView = 0;
            LightsEmitted = 0;
            LodLightsInView = 0;
            if (!Enabled || outLights == null) return 0;

            candidates.Clear();
            hdCells.Clear();
            ResetFrameStats_N2();
            float maxD2 = MaxDistance * MaxDistance;
            for (int vi = 0; visible != null && vi < visible.Count; vi++)
            {
                var e = visible[vi];
                var arch = e?.Archetype;
                if (arch == null) continue;
                if (Vector3.DistanceSquared(e.Position, cameraPos) <= maxD2) AddOriginalCells_U25(e, arch.Hash);
                if (!byArchetype.TryGetValue(arch.Hash, out var defs)) continue;
                float ed2 = Vector3.DistanceSquared(e.Position, cameraPos);
                float reach = MaxDistance + e.BSRadius;
                if (ed2 > reach * reach) continue;
                byte prio = PrioOf(e);
                CompareDump_N2(e, defs, hour, cameraPos);

                var ori = e.Orientation;
                var scale = SaneScale(e.Scale);
                for (int i = 0; i < defs.Length; i++)
                {
                    var d = defs[i];
                    var la = d.L;
                    if (la == null) continue;
                    if (!LightDefs.IsActiveAtHour(la.TimeFlags, hour)) continue;
                    if (!AllowedByInteriorFlags(la.Flags)) { FlagSkipped++; continue; }
                    float flash = animateFlash ? LightDefs.GetFlashMultiplier(la.Flashiness, time, (int)(arch.Hash % 9973) + i * 977) : 1.0f;
                    if (flash <= 0.0001f) continue;
                    var wpos = ori.Multiply(d.Pos * scale) + e.Position;
                    float dist2 = Vector3.DistanceSquared(wpos, cameraPos);
                    LightsInView++;
                    if (dist2 > maxD2) continue;
                    hdCells.Add(Cell(wpos));

                    EmitVolume_N2(la, in wpos, in ori, in d.Dir, flash);
                    if (coronasOut != null && la.CoronaSize > 0.001f && la.CoronaIntensity > 0.001f)
                    {
                        var cpos = wpos;
                        var toCam = cameraPos - cpos;
                        var tlen = toCam.Length();
                        if (tlen > 0.001f) cpos += toCam * (Math.Min(la.CoronaZBias, tlen * 0.5f) / tlen);
                        float cview = LightDefs.CoronaView_U25(la, wpos, ori.Multiply(d.Dir), cameraPos);
                        if (cview > 0.001f) coronasOut.Add((cpos, new Vector3(la.ColorR, la.ColorG, la.ColorB) / 255.0f, la.CoronaSize * 0.1f, la.CoronaIntensity * flash * cview));
                    }
                    if ((la.Flags & LightDefs.FlagCoronaOnly) != 0) continue;

                    var dir = ori.Multiply(d.Dir); if (dir.LengthSquared() > 1e-9f) dir.Normalize();
                    var tan = ori.Multiply(d.Tan); if (tan.LengthSquared() > 1e-9f) tan.Normalize();
                    var tangentY = Vector3.Cross(dir, tan);
                    if (tangentY.LengthSquared() > 1e-9f) tangentY.Normalize();
                    var inner = Math.Min(la.ConeInnerAngle, la.ConeOuterAngle) * 0.01745329f;
                    var outer = Math.Max(la.ConeInnerAngle, la.ConeOuterAngle) * 0.01745329f;
                    if (prio == 0) { RoomCandidates++; InteriorCandidates++; } else if (prio == 1) InteriorCandidates++;
                    NoteCompareRowLight_N2(e, la);
                    candidates.Add(new Candidate
                    {
                        L = la,
                        Dist = dist2,
                        Prio = prio,
                        Key = KeyOf(wpos, i),
                        G = new GpuLight
                        {
                            ProjTexIndex = -1.0f,
                            ShadowSlot = -1.0f,
                            ShadowBlur = la.ShadowBlur / 255.0f,
                            Position = wpos,
                            Intensity = la.Intensity,
                            Colour = new Vector3(la.ColorR, la.ColorG, la.ColorB) * (2.0f * la.Intensity / 255.0f) * flash,
                            Falloff = la.Falloff,
                            Direction = dir,
                            FalloffExponent = la.FalloffExponent,
                            TangentX = tan,
                            ConeInnerAngle = inner,
                            TangentY = tangentY,
                            ConeOuterAngle = outer,
                            CapsuleExtent = la.Extent,
                            Type = (uint)la.Type,
                            CullingPlaneNormal = la.CullingPlaneNormal,
                            CullingPlaneOffset = la.CullingPlaneOffset,
                            CullingPlaneEnable = (la.Flags & LightDefs.FlagCullingPlane) != 0 ? 1u : 0u,
                            Flags = la.Flags,
                        },
                    });
                }
            }

            if (LodLightsEnabled && residentYmaps != null)
            {
                float lodRange = Math.Min(LodLightRange, MaxDistance);
                float lodLitD2 = lodRange * lodRange;
                bool rebuild = lodTable == null || residentVersion != lodTableVersion || residentVersion < 0 ||
                               hour != lodTableHour || lodRange != lodTableRange ||
                               Vector3.DistanceSquared(cameraPos, lodTableCam) > LodTableMove * LodTableMove;
                if (rebuild && lodLitD2 > 0)
                {
                    lodTable ??= new List<LodLite>(4096);
                    lodTable.Clear();
                    LodTableRebuilds++;
                    float slack = lodRange + LodTableMove;
                    float slackD2 = slack * slack;
                    foreach (var ymap in residentYmaps)
                    {
                        var ll = ymap?.LODLights?.LodLights;
                        if (ll == null || ll.Length == 0) continue;
                        var bmin = ymap.LODLights.BBMin; var bmax = ymap.LODLights.BBMax;
                        if (bmax.X >= bmin.X)
                        {
                            var cl = Vector3.Clamp(cameraPos, bmin, bmax);
                            if (Vector3.DistanceSquared(cl, cameraPos) > slackD2) continue;
                        }
                        for (int i = 0; i < ll.Length; i++)
                        {
                            var l = ll[i];
                            if (l == null || !l.Enabled) continue;
                            if (!LightDefs.IsActiveAtHour(l.TimeFlags, hour)) continue;
                            if (Vector3.DistanceSquared(l.Position, cameraPos) > slackD2) continue;
                            if (LodLightHidden != null && LodLightHidden(l.Position)) continue;
                            var col = l.Colour;
                            lodTable.Add(new LodLite
                            {
                                L = l,
                                Pos = l.Position,
                                Rgb = new Vector3(col.R, col.G, col.B) / 255.0f,
                                A01 = col.A / 255.0f,
                            });
                        }
                    }
                    lodTableVersion = residentVersion;
                    lodTableHour = hour;
                    lodTableRange = lodRange;
                    lodTableCam = cameraPos;
                }
                for (int ti = 0; lodTable != null && ti < lodTable.Count; ti++)
                {
                    var t = lodTable[ti];
                    float d2 = Vector3.DistanceSquared(t.Pos, cameraPos);
                    if (d2 > lodLitD2) continue;
                    LodLightsInView++;
                    var rgb = t.Rgb;
                    float a01 = t.A01;
                    if (hdCells.Contains(Cell(t.Pos))) continue;
                    var l = t.L;
                        var dir = l.Direction; if (dir.LengthSquared() > 1e-9f) dir.Normalize(); else dir = -Vector3.UnitZ;
                        var tan = l.TangentX; if (tan.LengthSquared() > 1e-9f) tan.Normalize(); else tan = Vector3.UnitX;
                        var tanY = l.TangentY; if (tanY.LengthSquared() > 1e-9f) tanY.Normalize(); else tanY = Vector3.Cross(dir, tan);
                        float inner = l.ConeInnerAngle * 0.01745329f, outer = l.ConeOuterAngleOrCapExt * 0.01745329f;
                        uint type = (uint)l.Type;
                        if (type != 1 && type != 2 && type != 4) type = 1;
                        candidates.Add(new Candidate
                        {
                            L = null,
                            Dist = d2,
                            Prio = 2,
                            Key = KeyOf(l.Position, -1),
                            G = new GpuLight
                            {
                                ProjTexIndex = -1.0f,
                                ShadowSlot = -1.0f,
                                Position = l.Position,
                                Intensity = a01 * 96.0f,
                                Colour = rgb * (a01 * 96.0f * 2.0f),
                                Falloff = l.Falloff,
                                Direction = dir,
                                FalloffExponent = l.FalloffExponent,
                                TangentX = tan,
                                ConeInnerAngle = Math.Min(inner, outer),
                                TangentY = tanY,
                                ConeOuterAngle = Math.Max(inner, outer),
                                CapsuleExtent = type == 4 ? new Vector3(l.ConeOuterAngleOrCapExt, 0, 0) : Vector3.Zero,
                                Type = type,
                                Flags = 0,
                            },
                        });
                }
            }

            int chosen = SelectLit(Math.Min(MaxLights, outLights.Length), time);
            int count = 0;
            for (int k = 0; k < chosen && count < outLights.Length; k++)
            {
                var c = candidates[emitIdx[k]];
                sourcesOut?.Add(c.L);
                CountEmitted_N2(c.Prio);
                outLights[count++] = c.G;
            }
            LightsEmitted = count;
            CompareDumpRows_N2(outLights, count, sourcesOut);
            InteriorDump_N2(visible, hour, cameraPos);
            LastBuildMs = buildClock.Elapsed.TotalMilliseconds;
            return count;
        }

        private struct LodLite { public YmapLODLight L; public Vector3 Pos; public Vector3 Rgb; public float A01; }
        private List<LodLite> lodTable;
        private int lodTableVersion = -2, lodTableHour = -1;
        private float lodTableRange = -1.0f;
        private Vector3 lodTableCam = new Vector3(float.MaxValue);
        public float LodTableMove = 25.0f;
        public int LodTableRebuilds { get; private set; }
        public int LodTableSize => lodTable?.Count ?? 0;
        public int CoronasCulled { get; private set; }
        private readonly System.Diagnostics.Stopwatch buildClock = new System.Diagnostics.Stopwatch();
        public double LastBuildMs { get; private set; }
    }
}

