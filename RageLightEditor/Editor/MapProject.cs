using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public class MapProject
    {

        public class Entry
        {
            [JsonPropertyName("path")] public string Path { get; set; }
            [JsonPropertyName("fromGame")] public bool FromGame { get; set; }
            [JsonIgnore] public YmapFile Ymap;
            [JsonIgnore] public YtypFile Ytyp;
            [JsonIgnore] public bool Dirty;

            public string DisplayName => System.IO.Path.GetFileName(Path ?? "") is { Length: > 0 } n
                ? n : (Path ?? "(unnamed)");
        }

        public string Name = "Untitled";
        public string ProjectPath;
        public readonly List<Entry> Ymaps = new List<Entry>();
        public readonly List<Entry> Ytyps = new List<Entry>();

        public string LastStatus = "";
        public bool Dirty;

        private class Saved
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("ymaps")] public List<Entry> Ymaps { get; set; } = new List<Entry>();
            [JsonPropertyName("ytyps")] public List<Entry> Ytyps { get; set; } = new List<Entry>();
        }

        public void Clear()
        {
            Name = "Untitled";
            ProjectPath = null;
            Ymaps.Clear();
            Ytyps.Clear();
            Dirty = false;
            LastStatus = "new project";
        }

        public bool SaveProject(string path)
        {
            try
            {
                var s = new Saved { Name = Name };
                s.Ymaps.AddRange(Ymaps);
                s.Ytyps.AddRange(Ytyps);
                var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                ProjectPath = path;
                Name = System.IO.Path.GetFileNameWithoutExtension(path);
                Dirty = false;
                LastStatus = "saved " + System.IO.Path.GetFileName(path);
                return true;
            }
            catch (Exception ex) { LastStatus = "save failed: " + ex.Message; return false; }
        }

        public bool LoadProject(string path)
        {
            try
            {
                var s = JsonSerializer.Deserialize<Saved>(File.ReadAllText(path));
                if (s == null) { LastStatus = "not a project file"; return false; }
                Clear();
                Name = s.Name ?? System.IO.Path.GetFileNameWithoutExtension(path);
                ProjectPath = path;
                if (s.Ymaps != null) Ymaps.AddRange(s.Ymaps);
                if (s.Ytyps != null) Ytyps.AddRange(s.Ytyps);
                LastStatus = $"opened {Ymaps.Count} ymap(s), {Ytyps.Count} ytyp(s)";
                return true;
            }
            catch (Exception ex) { LastStatus = "open failed: " + ex.Message; return false; }
        }

        public Entry AddYmap(YmapFile y, string path, bool fromGame)
        {
            if (y == null) return null;
            var existing = Ymaps.FirstOrDefault(e => ReferenceEquals(e.Ymap, y));
            if (existing != null) return existing;
            var e = new Entry { Path = path, FromGame = fromGame, Ymap = y };
            Ymaps.Add(e);
            Dirty = true;
            return e;
        }

        public Entry AddYtyp(YtypFile t, string path, bool fromGame)
        {
            if (t == null) return null;
            var existing = Ytyps.FirstOrDefault(e => ReferenceEquals(e.Ytyp, t));
            if (existing != null) return existing;
            var e = new Entry { Path = path, FromGame = fromGame, Ytyp = t };
            Ytyps.Add(e);
            Dirty = true;
            return e;
        }

        public void Remove(Entry e)
        {
            if (e == null) return;
            Ymaps.Remove(e);
            Ytyps.Remove(e);
            Dirty = true;
        }

        public string SaveManifest(string path)
        {
            try
            {
                var deps = ManifestDeps_U30(out int skipped, out int ytypCount);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
                if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(path, ManifestWriter_U30.BuildXml(deps), new UTF8Encoding(false));
                else
                {
                    var data = ManifestWriter_U30.Build(deps, out var error);
                    if (data == null) { LastStatus = "manifest failed: " + error; AppLog_U21.Error("_manifest.ymf: " + error); return null; }
                    File.WriteAllBytes(path, data);
                }
                LastStatus = $"wrote {System.IO.Path.GetFileName(path)} " +
                             $"({deps.Count} ymap(s), {ytypCount} ytyp(s), {deps.Count(d => d.Interior)} interior)" +
                             (skipped > 0 ? $" - {skipped} unnamed ymap(s) LEFT OUT" : "");
                return path;
            }
            catch (Exception ex) { LastStatus = "manifest failed: " + ex.Message; return null; }
        }

        public string BuildManifest(out int skipped, out int ytypCount) =>
            ManifestWriter_U30.BuildXml(ManifestDeps_U30(out skipped, out ytypCount));

        public List<ManifestWriter_U30.Dep> ManifestDeps_U30(out int skipped, out int ytypCount)
        {
            var ytyps = new List<(string name, HashSet<uint> archetypes)>();
            foreach (var e in Ytyps)
            {
                if (e.Ytyp == null) continue;
                var nm = NameOfYtyp(e);
                if (string.IsNullOrEmpty(nm)) continue;
                var set = new HashSet<uint>();
                foreach (var a in e.Ytyp.AllArchetypes ?? System.Array.Empty<CodeWalker.GameFiles.Archetype>())
                    if (a != null) set.Add(a.Hash);
                ytyps.Add((nm, set));
            }
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deps = new List<ManifestWriter_U30.Dep>();
            skipped = 0;
            foreach (var e in Ymaps)
            {
                var nm = NameOfYmap(e);
                if (string.IsNullOrEmpty(nm)) { skipped++; continue; }
                var y = e.Ymap;
                var dep = new ManifestWriter_U30.Dep
                {
                    Ymap = nm,
                    Interior = y != null && ((y.CMloInstanceDefs?.Length ?? 0) > 0 || (y.MloEntities?.Length ?? 0) > 0),
                };
                var hashes = new HashSet<uint>();
                if (y?.AllEntities != null) foreach (var en in y.AllEntities) if (en != null) hashes.Add(en._CEntityDef.archetypeName.Hash);
                if (y?.CMloInstanceDefs != null) foreach (var m in y.CMloInstanceDefs) hashes.Add(m.CEntityDef.archetypeName.Hash);
                foreach (var (tn, set) in ytyps)
                    if (set.Overlaps(hashes)) { dep.Ytyps.Add(tn); used.Add(tn); }
                deps.Add(dep);
            }
            ytypCount = used.Count;
            return deps;
        }

        private static string NameOfYmap(Entry e)
        {
            var n = e.Ymap?.Name;
            if (string.IsNullOrEmpty(n)) n = e.Ymap?.RpfFileEntry?.Name;
            if (string.IsNullOrEmpty(n)) n = System.IO.Path.GetFileNameWithoutExtension(e.Path ?? "");
            return StripExt(n, ".ymap");
        }

        private static string NameOfYtyp(Entry e)
        {
            var n = e.Ytyp?.Name;
            if (string.IsNullOrEmpty(n)) n = e.Ytyp?.RpfFileEntry?.Name;
            if (string.IsNullOrEmpty(n)) n = System.IO.Path.GetFileNameWithoutExtension(e.Path ?? "");
            return StripExt(n, ".ytyp");
        }

        private static string StripExt(string n, string ext) =>
            string.IsNullOrEmpty(n) ? n
            : (n.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - ext.Length) : n);

        private static string Esc(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        public string SaveYtyp(Entry e, string folder)
        {
            if (e?.Ytyp == null) { LastStatus = "no ytyp"; return null; }
            try
            {
                Directory.CreateDirectory(folder);
                var name = NameOfYtyp(e);
                if (string.IsNullOrEmpty(name)) name = "edited";
                var p = System.IO.Path.Combine(folder, name + ".ytyp");
                foreach (var a in e.Ytyp.AllArchetypes ?? System.Array.Empty<CodeWalker.GameFiles.Archetype>())
                    if (a is CodeWalker.GameFiles.MloArchetype m)
                    {
                        var v = MloEditor.Validate(m);
                        if (!string.IsNullOrEmpty(v)) AppLog_U21.Warn($"{name}.ytyp: interior {a.Name} has problems the game may not like:\n{v}");
                    }
                var data = e.Ytyp.Save();
                if (data == null || data.Length == 0) { LastStatus = name + ": nothing written"; AppLog_U21.Error(name + ".ytyp: the save produced no data"); return null; }
                if (e.Ytyp.SaveWarnings != null && e.Ytyp.SaveWarnings.Count > 0)
                    AppLog_U21.Warn(name + ".ytyp saved with warnings:\n" + string.Join("\n", e.Ytyp.SaveWarnings));
                File.WriteAllBytes(p, data);
                e.Dirty = false;
                LastStatus = $"wrote {name}.ytyp ({data.Length / 1024} KB)";
                return p;
            }
            catch (Exception ex) { LastStatus = "ytyp save failed: " + ex.Message + " - details in Help > Show log"; AppLog_U21.Error("saving " + NameOfYtyp(e) + ".ytyp", ex); return null; }
        }

        public static string DescribeArchetype(Archetype a)
        {
            if (a == null) return "";
            var kind = a is MloArchetype ? "interior (MLO)"
                     : a is TimeArchetype ? "time-based"
                     : "base";
            return $"{kind}  ·  {a.DrawableDict.ToCleanString()}";
        }

        public static float GetLodDist(Archetype a) => a?._BaseArchetypeDef.lodDist ?? 0.0f;

        public static void SetLodDist(Archetype a, float v)
        {
            if (a == null) return;
            var d = a._BaseArchetypeDef;
            d.lodDist = Math.Max(v, 0.0f);
            a._BaseArchetypeDef = d;
            a.LodDist = d.lodDist;
        }

        public static uint GetFlags(Archetype a) => a?._BaseArchetypeDef.flags ?? 0u;

        public static void SetFlags(Archetype a, uint v)
        {
            if (a == null) return;
            var d = a._BaseArchetypeDef;
            d.flags = v;
            a._BaseArchetypeDef = d;
        }

        public static float GetHdTextureDist(Archetype a) => a?._BaseArchetypeDef.hdTextureDist ?? 0.0f;

        public static void SetHdTextureDist(Archetype a, float v)
        {
            if (a == null) return;
            var d = a._BaseArchetypeDef;
            d.hdTextureDist = Math.Max(v, 0.0f);
            a._BaseArchetypeDef = d;
        }

        public static string DescribeMlo(MloArchetype m)
        {
            if (m == null) return "";
            int rooms = m.rooms?.Length ?? 0;
            int portals = m.portals?.Length ?? 0;
            int sets = m.entitySets?.Length ?? 0;
            int ents = m.entities?.Length ?? 0;
            return $"{rooms} room(s), {portals} portal(s), {sets} entity set(s), {ents} entities";
        }
    }
}

