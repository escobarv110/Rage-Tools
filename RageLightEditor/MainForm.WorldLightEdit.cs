using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using CodeWalker;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {

        private readonly Dictionary<uint, string> worldLightSavedPath = new Dictionary<uint, string>();
        private readonly Dictionary<LightAttributes, uint> worldLightArchOf = new Dictionary<LightAttributes, uint>();
        private readonly HashSet<uint> worldLightUnsaved = new HashSet<uint>();
        private bool worldLightPanelWired;
        private bool worldLightEnvDone, worldLightDumpDone;
        private int worldLightPrintAfterFrames = -1;
        private string worldLightSaveEnvPath;
        private const float WorldLightPickRange = 400.0f;
        private const float WorldLightCandidateRange = 150.0f;

        private const float WorldLightPickPixels = 10.0f;
        private const float WorldLightMarkerPixels = 4.0f;
        private float WorldLightPickRadius(Vector3 lightPos, Vector3 camPos) =>
            Math.Max(0.05f, WorldLightPickPixels * camera.WorldPerPixel(lightPos));
        private float WorldLightMarkerRadius(Vector3 lightPos) =>
            Math.Max(0.03f, WorldLightMarkerPixels * camera.WorldPerPixel(lightPos));

        partial void OnWorldTick_LightEdit()
        {
            if (panel == null) return;
            if (!worldLightPanelWired) { worldLightPanelWired = true; panel.WorldLightSource = worldRender.Lights; }
            if (!panel.WorldMode) return;

            ref var sel = ref WorldEdit.Selection;
            if (sel.Light != null)
            {
                uint hash = sel.LightEntity?.Archetype?.Hash ?? 0;
                if (hash != 0 && worldRender.Lights.TryGetDefs(hash, out var defs) && defs != null &&
                    sel.LightIndex >= 0 && sel.LightIndex < defs.Length && defs[sel.LightIndex].L != null)
                {
                    if (!ReferenceEquals(defs[sel.LightIndex].L, sel.Light))
                    {
                        sel.Light = defs[sel.LightIndex].L;
                        worldLightArchOf[sel.Light] = hash;
                    }
                    sel.LightBone = defs[sel.LightIndex].Bone;
                }
                worldLightSavedPath.TryGetValue(hash, out var saved);
                panel.WorldLightSavedPath = saved;
                panel.WorldLightUnsaved = hash != 0 && worldLightUnsaved.Contains(hash);
            }
            else panel.WorldLightStatus = null;

            if (panel.RequestSelectWorldLight)
            {
                panel.RequestSelectWorldLight = false;
                WorldSelectLight(panel.RequestSelectWorldLightEntity, panel.RequestSelectWorldLightIndex, "inspector");
                panel.RequestSelectWorldLightEntity = null;
            }
            ServiceWorldLightAdd_U18();
            ServiceWorldLightDelete_U25();
            if (panel.WorldLightEdited)
            {
                panel.WorldLightEdited = false;
                var key = panel.WorldLightEditKey; var before = panel.WorldLightEditBefore; var after = panel.WorldLightEditAfter;
                if (key != null && before != null && after != null)
                {
                    uint hash = ArchOfLight(key);
                    worldRender.Lights.Invalidate(hash);
                    if (hash != 0) worldLightUnsaved.Add(hash);
                    WorldEdit.LastStatus = "light edited (" + (worldRender.Lights.GetDrawable(hash) != null ? "live in the world" : "not resident") + ")";
                    WorldHistory.Push(new SnapshotCommand<LightAttributes>("Edit light", key, before, after,
                        st => { WorldLights.CopyAllInto(st, key); worldRender.Lights.Invalidate(ArchOfLight(key)); }));
                }
            }
            if (panel.RequestWorldLightSaveAs) { panel.RequestWorldLightSaveAs = false; WorldLightSaveAs(false); }
            if (panel.RequestWorldLightAddToProject) { panel.RequestWorldLightAddToProject = false; WorldLightSaveAs(true); }

            if (!worldLightDumpDone && worldBuilt && screenshotPath != null && worldWarmup >= 455 && Environment.GetEnvironmentVariable("RLE_DUMPLIGHTS") == "1")
            {
                worldLightDumpDone = true;
                Console.WriteLine($"LIGHTPROPS near {camera.Position}: {worldRender.Lights.ArchetypesWithLights} archetypes with lights built, {World.Visible.Count} visible entities");
                var near = new List<(float d, YmapEntityDef e, int n)>();
                foreach (var e in World.Visible)
                {
                    if (e?.Archetype == null || !worldRender.Lights.TryGetDefs(e.Archetype.Hash, out var ds) || ds == null) continue;
                    near.Add(((e.Position - camera.Position).Length(), e, ds.Length));
                }
                near.Sort((a, b) => a.d.CompareTo(b.d));
                int.TryParse(Environment.GetEnvironmentVariable("RLE_DUMPLIGHTS_N"), out int dumpN);
                for (int i = 0; i < Math.Min(dumpN > 0 ? dumpN : 16, near.Count); i++)
                {
                    var e = near[i].e; string first = "";
                    if (worldRender.Lights.TryGetDefs(e.Archetype.Hash, out var ds2) && ds2 != null && ds2.Length > 0 && ds2[0].L != null)
                    {
                        var sc = e.Scale; if (sc.X <= 0.0f) sc = Vector3.One;
                        first = " light0=" + (e.Orientation.Multiply(ds2[0].Pos * sc) + e.Position);
                    }
                    Console.WriteLine($"LIGHTPROP {e.Archetype.Name} d={near[i].d:0} pos={e.Position} lights={near[i].n} ymap={e.Ymap?.Name ?? "(interior)"}{first}");
                }
            }
            if (!worldLightEnvDone && sel.Light != null && worldBuilt)
            {
                worldLightEnvDone = true;
                var edit = Environment.GetEnvironmentVariable("RLE_LIGHTEDIT");
                if (!string.IsNullOrEmpty(edit)) ApplyLightEditEnv(edit);
                worldLightSaveEnvPath = Environment.GetEnvironmentVariable("RLE_LIGHTSAVE");
                if (!string.IsNullOrEmpty(worldLightSaveEnvPath)) WorldLightSaveEnv(worldLightSaveEnvPath);
                if (!string.IsNullOrEmpty(edit) || !string.IsNullOrEmpty(worldLightSaveEnvPath))
                {
                    worldLightPrintAfterFrames = Math.Max(worldLightPrintAfterFrames, 2);
                    screenshotFrames = Math.Max(screenshotFrames, 8);
                }
            }
            if (worldLightPrintAfterFrames > 0 && --worldLightPrintAfterFrames == 0)
            {
                var L = worldRender.Lights;
                var s2 = WorldEdit.Selection;
                Console.WriteLine($"WORLDLIGHTEDIT after: lit {L.LightsEmitted} inView {L.LightsInView} invalidations {L.Invalidations} archetypesWithLights {L.ArchetypesWithLights} " +
                                  $"undo '{WorldHistory.NextUndoName}' projectDrawablesServed {gameFiles?.ProjectDrawablesServed ?? 0} selection {(s2.Light != null ? s2.LightNameString() + $" colour={s2.Light.ColorR},{s2.Light.ColorG},{s2.Light.ColorB} intensity={s2.Light.Intensity:0.##}" : "none")}");
            }
        }

        private uint ArchOfLight(LightAttributes l)
        {
            if (l != null && worldLightArchOf.TryGetValue(l, out var h)) return h;
            var s = WorldEdit.Selection;
            return ReferenceEquals(s.Light, l) ? (s.LightEntity?.Archetype?.Hash ?? 0) : 0;
        }

        private bool WorldSelectLight(YmapEntityDef e, int index, string via)
        {
            if (e?.Archetype == null) return false;
            if (!worldRender.Lights.TryGetDefs(e.Archetype.Hash, out var defs) || defs == null || index < 0 || index >= defs.Length || defs[index].L == null)
            { WorldEdit.LastStatus = "that light is not resident"; return false; }
            var s = WorldSelection.ForLight(e, defs[index].L, index, defs[index].Bone,
                WorldLightPickRadius(WorldLightMath.WorldPos(e, defs[index].Bone, defs[index].L.Position), camera.Position));
            s.CamRel = s.LightWorldPosition - camera.Position;
            worldLightArchOf[defs[index].L] = e.Archetype.Hash;
            int mi = LightPanel.IndexOfMode(WorldSelectionMode.Light);
            if (mi >= 0 && panel.SelectionModeEnum != WorldSelectionMode.Light) panel.SelectionMode = mi;
            WorldEdit.Select(s);
            WorldEdit.LastStatus = s.GetNameString("") + " (" + via + ")";
            return true;
        }

        partial void PickWorldLights_I6(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            var vis = World.Visible;
            var L = worldRender.Lights;
            float range2 = WorldLightPickRange * WorldLightPickRange;
            bool dump = Environment.GetEnvironmentVariable("RLE_DUMPPICK") == "1";
            float bestAxis = float.MaxValue;
            for (int vi = 0; vi < vis.Count; vi++)
            {
                var e = vis[vi];
                var arch = e?.Archetype;
                if (arch == null) continue;
                if (!L.TryGetDefs(arch.Hash, out var defs) || defs == null) continue;
                float reach = e.BSRadius + 2.0f;
                if (Vector3.DistanceSquared(e.Position, camPos) > range2 + reach * reach) continue;
                var ori = e.Orientation;
                var scale = e.Scale; if (scale.X <= 0.0f) scale = Vector3.One;
                for (int i = 0; i < defs.Length; i++)
                {
                    var la = defs[i].L;
                    if (la == null) continue;
                    var wpos = ori.Multiply(defs[i].Pos * scale) + e.Position;
                    float r = WorldLightPickRadius(wpos, camPos);
                    var rel = wpos - ray.Position;
                    float along = Vector3.Dot(rel, ray.Direction);
                    if (along <= 0) continue;
                    float axis = (rel - ray.Direction * along).Length();
                    if (dump && axis < r * 6) Console.WriteLine($"PICKLIGHT {arch.Name}[{i}] world={wpos} depth={along:0.##} offAxis={axis:0.###} pickR={r:0.###} {(axis <= r ? "HIT" : "miss")}");
                    if (axis > r) continue;
                    float d = Math.Max(along - r, 0.001f);
                    if (d > hit.HitDist + r) continue;
                    if (d >= hit.HitDist - r && axis >= bestAxis) continue;
                    hit = WorldSelection.ForLight(e, la, i, defs[i].Bone, r);
                    hit.HitDist = d;
                    hit.CamRel = wpos - camPos;
                    bestAxis = axis;
                    worldLightArchOf[la] = arch.Hash;
                }
            }
            if (!hit.HasValue)
            {
                var e = WorldPickEntity(ray);
                if (e?.Archetype != null && e.MloInstance == null && (!L.TryGetDefs(e.Archetype.Hash, out var noDefs) || noDefs == null || noDefs.Length == 0))
                {
                    hit = WorldSelection.FromProjectObject(e);
                    hit.HitDist = worldPickEntityDist;
                    if (dump) Console.WriteLine($"PICKLIGHT {e.Archetype.Name} carries no lights - the prop is selected so one can be added");
                }
                else if (e?.Archetype != null && L.TryGetDefs(e.Archetype.Hash, out var edefs) && edefs != null)
                {
                    var ori = e.Orientation;
                    var scale = e.Scale; if (scale.X <= 0.0f) scale = Vector3.One;
                    int bi = -1; float bAxis = float.MaxValue, bAlong = 0; Vector3 bPos = Vector3.Zero;
                    for (int i = 0; i < edefs.Length; i++)
                    {
                        if (edefs[i].L == null) continue;
                        var wpos = ori.Multiply(edefs[i].Pos * scale) + e.Position;
                        var rel = wpos - ray.Position;
                        float along = Vector3.Dot(rel, ray.Direction);
                        if (along <= 0) continue;
                        float axis = (rel - ray.Direction * along).Length();
                        if (axis > 4.0f * WorldLightPickRadius(wpos, camPos) || axis >= bAxis) continue;
                        bi = i; bAxis = axis; bAlong = along; bPos = wpos;
                    }
                    if (bi >= 0)
                    {
                        hit = WorldSelection.ForLight(e, edefs[bi].L, bi, edefs[bi].Bone, WorldLightPickRadius(bPos, camPos));
                        hit.HitDist = bAlong;
                        hit.CamRel = bPos - camPos;
                        worldLightArchOf[edefs[bi].L] = e.Archetype.Hash;
                        if (dump) Console.WriteLine($"PICKLIGHT via the lamp {e.Archetype.Name}: light {bi} offAxis={bAxis:0.###}");
                    }
                }
            }
        }

        partial void WorldLightNearestCandidates_I6(Action<Vector3, float> consider)
        {
            var vis = World.Visible;
            var L = worldRender.Lights;
            var camPos = camera.Position;
            float range2 = WorldLightPickRange * WorldLightPickRange;
            for (int vi = 0; vi < vis.Count; vi++)
            {
                var e = vis[vi];
                var arch = e?.Archetype;
                if (arch == null || Vector3.DistanceSquared(e.Position, camPos) > range2) continue;
                if (!L.TryGetDefs(arch.Hash, out var defs) || defs == null) continue;
                var ori = e.Orientation;
                var scale = e.Scale; if (scale.X <= 0.0f) scale = Vector3.One;
                for (int i = 0; i < defs.Length; i++)
                    if (defs[i].L != null) consider(ori.Multiply(defs[i].Pos * scale) + e.Position, 3.0f);
            }
        }

        partial void DrawWorldLightCandidates_I6()
        {
            var vis = World.Visible;
            var L = worldRender.Lights;
            var camPos = camera.Position;
            float range2 = WorldLightCandidateRange * WorldLightCandidateRange;
            float near2 = WorldLightNearRange * WorldLightNearRange;
            int drawn = 0;
            var selLight = WorldEdit.Selection.Light;
            for (int vi = 0; vi < vis.Count && drawn < 600; vi++)
            {
                var e = vis[vi];
                var arch = e?.Archetype;
                if (arch == null || Vector3.DistanceSquared(e.Position, camPos) > range2) continue;
                if (!L.TryGetDefs(arch.Hash, out var defs) || defs == null) continue;
                var ori = e.Orientation;
                var scale = e.Scale; if (scale.X <= 0.0f) scale = Vector3.One;
                for (int i = 0; i < defs.Length; i++)
                {
                    var la = defs[i].L;
                    if (la == null || ReferenceEquals(la, selLight)) continue;
                    var wpos = ori.Multiply(defs[i].Pos * scale) + e.Position;
                    if (Vector3.DistanceSquared(wpos, camPos) <= near2) continue;
                    lineRenderer.AddSphere(wpos, WorldLightMarkerRadius(wpos), HelperBlue, 10);
                    drawn++;
                }
            }
        }

        partial void DrawSelection_Light(in WorldSelection s, Vector4 col, bool full, ref bool drawBox)
        {
            var camPos = camera.Position;
            var dim = new Vector4(col.X, col.Y, col.Z, col.W * 0.35f);
            if (s.Light != null)
            {
                drawBox = false;
                var e = s.LightEntity;
                var wpos = s.LightWorldPosition;
                float marker = WorldLightPickRadius(wpos, camPos);
                if (!full)
                {
                    lineRenderer.AddSphere(wpos, marker * 1.4f, col, 16);
                    return;
                }
                if (e?.Archetype != null && worldRender.Lights.TryGetDefs(e.Archetype.Hash, out var defs) && defs != null)
                {
                    var ori = e.Orientation;
                    var scale = e.Scale; if (scale.X <= 0.0f) scale = Vector3.One;
                    for (int i = 0; i < defs.Length; i++)
                    {
                        if (defs[i].L == null || i == s.LightIndex) continue;
                        var p = ori.Multiply(defs[i].Pos * scale) + e.Position;
                        lineRenderer.AddSphere(p, WorldLightPickRadius(p, camPos), dim, 12);
                    }
                }
                var l = s.Light;
                lineRenderer.AddSphere(wpos, marker, col, 16);
                var dir = s.LightWorldDirection;
                var tan = s.LightWorldTangent;
                float fall = Math.Max(l.Falloff, 0.05f);
                switch (l.Type)
                {
                    case LightType.Point:
                        lineRenderer.AddSphere(wpos, fall, dim, 32);
                        break;
                    case LightType.Spot:
                        {
                            float outer = Math.Max(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f;
                            float inner = Math.Min(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f;
                            lineRenderer.AddCone(wpos, dir, tan, outer, fall, col);
                            if (inner > 0.001f) lineRenderer.AddCone(wpos, dir, tan, inner, fall, dim);
                            break;
                        }
                    case LightType.Capsule:
                        {
                            var ext = dir * (l.Extent.X * 0.5f);
                            lineRenderer.AddCapsule(wpos + ext, wpos - ext, fall, col);
                            break;
                        }
                }
                lineRenderer.AddLine(wpos, wpos + dir * Math.Min(fall, 2.0f), col);
                return;
            }
            if (full && s.EntityDef != null && s.CollisionBounds == null && s.EntityDef.Archetype != null &&
                worldRender.Lights.TryGetDefs(s.EntityDef.Archetype.Hash, out var edefs) && edefs != null)
            {
                var e = s.EntityDef;
                var ori = e.Orientation;
                var scale = e.Scale; if (scale.X <= 0.0f) scale = Vector3.One;
                for (int i = 0; i < edefs.Length; i++)
                {
                    if (edefs[i].L == null) continue;
                    var p = ori.Multiply(edefs[i].Pos * scale) + e.Position;
                    lineRenderer.AddSphere(p, WorldLightPickRadius(p, camPos), col, 12);
                }
            }
        }

        partial void WorldLightTargetChanged_I6(LightAttributes la, IWorldGizmoTarget t)
        {
            uint hash = ArchOfLight(la);
            if (hash == 0 && t is SelectionGizmoTarget sgt) hash = sgt.Selection.LightEntity?.Archetype?.Hash ?? 0;
            if (hash != 0) { worldRender.Lights.Invalidate(hash); worldLightUnsaved.Add(hash); }
            WorldEdit.LastStatus = "light moved (live in the world)";
        }

        private DrawableBase WorldLightSelectedDrawable(out uint hash, out string archName)
        {
            var s = WorldEdit.Selection;
            hash = s.LightEntity?.Archetype?.Hash ?? 0;
            archName = s.LightEntity?.Archetype?.Name ?? s.LightEntity?._CEntityDef.archetypeName.ToString() ?? "prop";
            return hash != 0 ? worldRender.Lights.GetDrawable(hash) : null;
        }

        private void WorldLightSaveAs(bool addToProject)
        {
            var db = WorldLightSelectedDrawable(out uint hash, out string archName);
            if (db == null) { panel.WorldLightStatus = "the prop's drawable is not resident"; return; }
            if (!WorldLights.CanSave(db)) { panel.WorldLightStatus = "this drawable cannot be written as a loose file"; return; }
            string path = null;
            if (addToProject) worldLightSavedPath.TryGetValue(hash, out path);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                string ext = WorldLights.SaveExtension(db);
                using var dlg = new SaveFileDialog
                {
                    Filter = ext == ".yft" ? "YFT fragment (*.yft)|*.yft" : "YDR drawable (*.ydr)|*.ydr",
                    FileName = archName + ext,
                    Title = (addToProject ? "Save and add to project: " : "Save ") + archName,
                };
                if (!string.IsNullOrEmpty(WorldEdit.OutputFolder) && Directory.Exists(WorldEdit.OutputFolder)) dlg.InitialDirectory = WorldEdit.OutputFolder;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
            }
            try
            {
                path = WorldLights.SaveDrawableAs(db, path);
                worldLightSavedPath[hash] = path;
                worldLightUnsaved.Remove(hash);
                panel.WorldLightSavedPath = path;
                panel.WorldLightStatus = "saved " + Path.GetFileName(path);
                WorldEdit.LastStatus = "saved " + Path.GetFileName(path);
                Console.WriteLine($"WORLDLIGHTSAVE wrote {path}");
            }
            catch (Exception ex)
            {
                panel.WorldLightStatus = "save failed: " + ex.Message;
                return;
            }
            if (addToProject) WorldLightAddToProject(path);
        }

        private void WorldLightAddToProject(string path)
        {
            if (projCtl == null || string.IsNullOrEmpty(path)) return;
            try
            {
                int added = projCtl.AddFilesToProject(new[] { path }, quiet: true);
                RebuildProjectOverrides();
                ProjWin.Visible = true;
                panel.WorldLightStatus = (added > 0 ? "added to project: " : "already in project: ") + Path.GetFileName(path) + " - the world now draws this copy";
                WorldEdit.LastStatus = panel.WorldLightStatus;
                Console.WriteLine($"WORLDLIGHTPROJECT {(added > 0 ? "added" : "present")} {path} project files ydr {ProjWin.Project?.YdrFilenames.Count ?? 0} yft {ProjWin.Project?.YftFilenames.Count ?? 0}");
                if (screenshotPath != null)
                {
                    screenshotFrames = Math.Max(screenshotFrames, 160);
                    worldLightPrintAfterFrames = 150;
                    ProjWin.Minimized = true;
                }
            }
            catch (Exception ex) { panel.WorldLightStatus = "add to project failed: " + ex.Message; }
        }

        partial void WorldSelReport_Light(in WorldSelection s, System.Text.StringBuilder sb)
        {
            var l = s.Light; var e = s.LightEntity;
            var wp = s.LightWorldPosition;
            sb.Append($" | LIGHT idx={s.LightIndex} type={l.Type} colour={l.ColorR},{l.ColorG},{l.ColorB} intensity={l.Intensity:0.##} falloff={l.Falloff:0.##} exp={l.FalloffExponent:0.##} " +
                      $"cone={l.ConeInnerAngle:0.#}/{l.ConeOuterAngle:0.#} flags={l.Flags:X} bone={l.BoneId} local={l.Position} world={wp} " +
                      $"entity={e?.Archetype?.Name} ymap={e?.Ymap?.Name ?? (e?.MloParent != null ? "(interior " + e.MloParent.Archetype?.Name + ")" : "-")} " +
                      $"lightsOnProp={(e?.Archetype != null && worldRender.Lights.TryGetDefs(e.Archetype.Hash, out var d) ? d.Length : 0)} savable={WorldLights.CanSave(worldRender.Lights.GetDrawable(e?.Archetype?.Hash ?? 0))}");
        }

        private void ApplyLightEditEnv(string spec)
        {
            ref var s = ref WorldEdit.Selection;
            var l = s.Light;
            if (l == null) return;
            uint hash = ArchOfLight(l);
            var before = Scene.CloneLight(l);
            Console.WriteLine($"WORLDLIGHTEDIT before: {s.LightNameString()} colour={l.ColorR},{l.ColorG},{l.ColorB} intensity={l.Intensity:0.##} falloff={l.Falloff:0.##} local={l.Position} world={s.LightWorldPosition} dir={l.Direction} lit {worldRender.Lights.LightsEmitted}");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            float F(string v) => float.TryParse(v, System.Globalization.NumberStyles.Float, ci, out var f) ? f : 0f;
            Vector3 V3(string v) { var p = v.Split(','); return new Vector3(p.Length > 0 ? F(p[0]) : 0, p.Length > 1 ? F(p[1]) : 0, p.Length > 2 ? F(p[2]) : 0); }
            bool valueEdit = false, doUndo = false, selectEntity = false, doDelete = false;
            Vector3? move = null, dir = null;
            foreach (var part in spec.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length != 2) continue;
                var k = kv[0].Trim().ToLowerInvariant(); var v = kv[1].Trim();
                switch (k)
                {
                    case "intensity": l.Intensity = F(v); valueEdit = true; break;
                    case "color": case "colour":
                        {
                            var c = V3(v);
                            float m = (c.X > 1.0f || c.Y > 1.0f || c.Z > 1.0f) ? 1.0f : 255.0f;
                            l.ColorR = (byte)Math.Clamp((int)Math.Round(c.X * m), 0, 255);
                            l.ColorG = (byte)Math.Clamp((int)Math.Round(c.Y * m), 0, 255);
                            l.ColorB = (byte)Math.Clamp((int)Math.Round(c.Z * m), 0, 255);
                            valueEdit = true; break;
                        }
                    case "falloff": l.Falloff = F(v); valueEdit = true; break;
                    case "exp": l.FalloffExponent = F(v); valueEdit = true; break;
                    case "cone": { var c = V3(v); l.ConeInnerAngle = c.X; l.ConeOuterAngle = c.Y; valueEdit = true; break; }
                    case "flags": l.Flags = v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToUInt32(v.Substring(2), 16) : uint.Parse(v); valueEdit = true; break;
                    case "type": l.Type = v.StartsWith("s", StringComparison.OrdinalIgnoreCase) ? LightType.Spot : v.StartsWith("c", StringComparison.OrdinalIgnoreCase) ? LightType.Capsule : LightType.Point; valueEdit = true; break;
                    case "move": move = V3(v); break;
                    case "dir": dir = V3(v); break;
                    case "undo": doUndo = v == "1"; break;
                    case "entity": selectEntity = v == "1"; break;
                    case "delete": doDelete = v == "1"; break;
                    case "volume": l.VolumeIntensity = F(v); l.Flags |= LightDefs.FlagDrawVolume; valueEdit = true; break;
                }
            }
            if (valueEdit)
            {
                worldRender.Lights.Invalidate(hash);
                if (hash != 0) worldLightUnsaved.Add(hash);
                WorldHistory.Push(new SnapshotCommand<LightAttributes>("Edit light", l, before, Scene.CloneLight(l),
                    st => { WorldLights.CopyAllInto(st, l); worldRender.Lights.Invalidate(ArchOfLight(l)); }));
            }
            if (move.HasValue || dir.HasValue)
            {
                var t = s.GizmoTarget();
                if (t != null)
                {
                    WorldGizmoDragBegan();
                    if (move.HasValue) t.SetPosition(t.Position + move.Value);
                    if (dir.HasValue) t.SetOrientation(WorldLightMath.Rotation(Vector3.Normalize(dir.Value), s.LightWorldTangent));
                    WorldTargetChanged(t);
                    WorldGizmoDragEnded();
                }
                else Console.WriteLine("WORLDLIGHTEDIT the light selection has no gizmo target");
            }
            Console.WriteLine($"WORLDLIGHTEDIT applied '{spec}': colour={l.ColorR},{l.ColorG},{l.ColorB} intensity={l.Intensity:0.##} falloff={l.Falloff:0.##} local={l.Position} world={s.LightWorldPosition} dir={l.Direction} invalidations {worldRender.Lights.Invalidations} undo '{WorldHistory.NextUndoName}' canUndo {WorldHistory.CanUndo}");
            if (doUndo)
            {
                TryWorldUndo();
                Console.WriteLine($"WORLDLIGHTEDIT after undo: colour={l.ColorR},{l.ColorG},{l.ColorB} intensity={l.Intensity:0.##} local={l.Position} restored {(WorldLights.AllFieldsEqual(before, l) ? "OK" : "MISMATCH")}");
                TryWorldRedo();
                Console.WriteLine($"WORLDLIGHTEDIT after redo: colour={l.ColorR},{l.ColorG},{l.ColorB} intensity={l.Intensity:0.##} local={l.Position}");
            }
            if (doDelete && s.LightEntity?.Archetype != null)
            {
                var de = s.LightEntity;
                int Count() => worldRender.Lights.TryGetDefs(de.Archetype.Hash, out var dd) && dd != null ? dd.Length : 0;
                int n0 = Count();
                WorldDeleteLight_U25();
                int n1 = Count();
                TryWorldUndo();
                int n2 = Count();
                TryWorldRedo();
                int n3 = Count();
                Console.WriteLine($"WORLDLIGHTDELETE {de.Archetype.Name}: {n0} lights, after delete {n1}, after undo {n2}, after redo {n3}, selection {(WorldEdit.Selection.Light != null ? "kept" : "cleared")}");
                return;
            }
            if (selectEntity && s.LightEntity != null)
            {
                var e = s.LightEntity;
                int mi = LightPanel.IndexOfMode(WorldSelectionMode.Entity);
                if (mi >= 0) panel.SelectionMode = mi;
                WorldEdit.Select(e);
                Console.WriteLine($"WORLDLIGHTEDIT entity selected: {e.Archetype?.Name} lights {(worldRender.Lights.TryGetDefs(e.Archetype?.Hash ?? 0, out var ed) ? ed.Length : 0)}");
            }
        }

        private void WorldLightSaveEnv(string path)
        {
            var s = WorldEdit.Selection;
            var db = WorldLightSelectedDrawable(out uint hash, out string archName);
            if (db == null || s.Light == null) { Console.WriteLine("WORLDLIGHTSAVE FAIL: no resident drawable for the selected light"); return; }
            try
            {
                var written = WorldLights.SaveDrawableAs(db, path);
                worldLightSavedPath[hash] = written;
                worldLightUnsaved.Remove(hash);
                var l = s.Light;
                var back = WorldLights.ReloadLight(written, s.LightIndex, out int count);
                bool ok = back != null && back.Intensity == l.Intensity && back.ColorR == l.ColorR && back.ColorG == l.ColorG && back.ColorB == l.ColorB &&
                          (back.Position - l.Position).Length() < 1e-4f && back.Falloff == l.Falloff;
                Console.WriteLine($"WORLDLIGHTSAVE {(ok ? "OK" : "FAIL")} {written} ({new FileInfo(written).Length / 1024} KB) lights={count} reloaded[{s.LightIndex}]=" +
                                  (back == null ? "null" : $"colour={back.ColorR},{back.ColorG},{back.ColorB} intensity={back.Intensity:0.##} falloff={back.Falloff:0.##} local={back.Position}") +
                                  $" expected colour={l.ColorR},{l.ColorG},{l.ColorB} intensity={l.Intensity:0.##}");
                if (Environment.GetEnvironmentVariable("RLE_LIGHTSAVE_PROJECT") == "1") WorldLightAddToProject(written);
            }
            catch (Exception ex) { Console.WriteLine("WORLDLIGHTSAVE FAIL: " + ex.Message); }
        }

        partial void WorldLightEditTest_I6(Action<string, bool, string> check)
        {
            try
            {
                var ent = new YmapEntityDef();
                ent.SetPosition(new Vector3(100, -50, 12));
                ent.SetOrientation(Quaternion.RotationYawPitchRoll(0.7f, 0.2f, -0.1f));
                ent.SetScale(new Vector3(1.5f, 1.5f, 2.0f));
                var bone = Matrix.RotationYawPitchRoll(0.3f, -0.4f, 0.9f) * Matrix.Translation(0.2f, 1.1f, -0.6f);
                var local = new Vector3(0.4f, -0.3f, 2.5f);
                var world = WorldLightMath.WorldPos(ent, bone, local);
                var back = WorldLightMath.LocalPos(ent, bone, world);
                check("world light: entity+bone position round-trips", (back - local).Length() < 1e-3f, $"{local} -> {world} -> {back}");
                var wdir = Vector3.Normalize(new Vector3(0.3f, -0.5f, -0.8f));
                var wtan = WorldLightMath.OrthoTangent(wdir, Vector3.UnitX);
                var q = WorldLightMath.Rotation(wdir, wtan);
                WorldLightMath.Axes(q, out var d2, out var t2);
                check("world light: rotation <-> direction/tangent round-trips", (d2 - wdir).Length() < 1e-3f && (t2 - wtan).Length() < 1e-3f, $"dir {wdir} -> {d2}, tan {wtan} -> {t2}");
                var ldir = WorldLightMath.LocalDir(ent, bone, wdir);
                var wdir2 = WorldLightMath.WorldDir(ent, bone, ldir);
                check("world light: direction local <-> world round-trips", (wdir2 - wdir).Length() < 1e-3f, $"{wdir} -> {ldir} -> {wdir2}");

                string dir = Path.Combine(Path.GetTempPath(), "rle_worldlight_test");
                Directory.CreateDirectory(dir);
                string src = Path.Combine(dir, "light_test_scene.ydr");
                if (!File.Exists(src)) TestSceneGenerator.Run(src);
                var ydr = new YdrFile();
                ydr.Load(File.ReadAllBytes(src));
                var lights = WorldLights.LightsOf(ydr.Drawable);
                check("world light: the test scene drawable carries lights", lights != null && lights.Length > 0, $"{lights?.Length ?? 0} lights");
                if (lights == null || lights.Length == 0) return;
                var defs = WorldLights.Extract(ydr.Drawable);
                var l0 = lights[0];
                var origPos = l0.Position; var origInt = l0.Intensity;
                var sel = WorldSelection.ForLight(ent, l0, 0, defs[0].Bone);
                var tgt = sel.GizmoTarget();
                check("world light: a Light selection has a gizmo target (move + rotate, no scale)", tgt != null && !tgt.CanScale && tgt.RotationAxes == WorldWidgetAxis.XYZ, tgt == null ? "null" : $"{tgt.GetType().Name} scale {tgt.CanScale}");
                var pending = GizmoTransformCommand.Begin("Test move light", new[] { tgt }, null);
                tgt.SetPosition(tgt.Position + new Vector3(0, 0, 1.0f));
                var moved = WorldLightMath.WorldPos(ent, defs[0].Bone, l0.Position);
                check("world light: gizmo SetPosition moves the light 1 m up in the world", Math.Abs((moved - world).Z) > 0 && (moved - WorldLightMath.WorldPos(ent, defs[0].Bone, origPos) - new Vector3(0, 0, 1.0f)).Length() < 1e-3f, $"local {origPos} -> {l0.Position}");
                var cmd = pending.Complete();
                check("world light: the drag is one undo command", cmd != null, cmd?.Name ?? "null");
                cmd?.Undo();
                check("world light: undo puts the light back", (l0.Position - origPos).Length() < 1e-4f, $"{l0.Position}");
                l0.Intensity = 77.5f; l0.ColorR = 255; l0.ColorG = 10; l0.ColorB = 20;
                string outp = Path.Combine(dir, "light_test_scene_edited.ydr");
                var written = WorldLights.SaveDrawableAs(ydr.Drawable, outp);
                var rl = WorldLights.ReloadLight(written, 0, out int count);
                check("world light: SaveDrawableAs writes a .ydr that reloads with the edited light",
                      rl != null && count == lights.Length && rl.Intensity == 77.5f && rl.ColorR == 255 && rl.ColorG == 10 && rl.ColorB == 20,
                      rl == null ? "no light" : $"{count} lights, [0] intensity {rl.Intensity} colour {rl.ColorR},{rl.ColorG},{rl.ColorB}");
                var snap = Scene.CloneLight(l0);
                l0.Intensity = 1; l0.ColorR = 0;
                WorldLights.CopyAllInto(snap, l0);
                check("world light: snapshot restore copies every field back", l0.Intensity == 77.5f && l0.ColorR == 255 && WorldLights.AllFieldsEqual(snap, l0), $"intensity {l0.Intensity}");
                l0.Intensity = origInt;
            }
            catch (Exception ex) { check("world light: test ran", false, ex.Message); }
        }
    }
}

