using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public partial class ProjectController
    {
        public LocalAssetIndex AssetIndex { get; private set; }
        public readonly List<string> AssetFolders = new List<string>();
        public int RegisteredArchetypes { get; private set; }
        public int ReresolvedEntities { get; private set; }
        public int ReresolvedInteriors { get; private set; }

        public LocalAssetIndex BuildAssetIndex()
        {
            var p = win.Project;
            AssetFolders.Clear();
            if (p == null) { AssetIndex = null; return null; }
            var idx = new LocalAssetIndex();

            foreach (var rel in p.YdrFilenames.Concat(p.YddFilenames).Concat(p.YftFilenames).Concat(p.YtdFilenames))
            {
                string full = null;
                try { full = p.GetFullFilePath(rel); } catch { }
                if (!string.IsNullOrEmpty(full)) idx.AddFile(full);
            }

            var folders = new List<string>();
            void Folder(string dir)
            {
                if (string.IsNullOrEmpty(dir)) return;
                try { dir = Path.GetFullPath(dir); } catch { return; }
                if (!Directory.Exists(dir)) return;
                if (folders.Any(f => string.Equals(f, dir, StringComparison.OrdinalIgnoreCase))) return;
                folders.Add(dir);
            }
            void FileFolders(string file)
            {
                if (string.IsNullOrEmpty(file) || !Path.IsPathRooted(file)) return;
                string dir = null;
                try { dir = Path.GetDirectoryName(Path.GetFullPath(file)); } catch { }
                Folder(dir);
                string root = null;
                try { root = LocalAssetIndex.FindRoot(file); } catch { }
                Folder(root);
            }
            foreach (var y in p.YmapFiles) FileFolders(y?.FilePath);
            foreach (var t in p.YtypFiles) FileFolders(t?.FilePath);
            Folder(p.Directory);
            var roots = folders.Where(f => !folders.Any(o => !ReferenceEquals(o, f) && !string.Equals(o, f, StringComparison.OrdinalIgnoreCase) && LocalAssetIndex.IsUnder(f, o))).ToList();
            foreach (var d in roots)
            {
                idx.AddFolder(d);
                AssetFolders.Add(d);
            }

            AssetIndex = idx.FileCount + idx.TextureDictCount > 0 ? idx : null;
            return AssetIndex;
        }

        public int RegisterProjectArchetypes(bool render)
        {
            var c = cache();
            RegisteredArchetypes = 0;
            ReresolvedEntities = 0; ReresolvedInteriors = 0;
            if (c == null) return 0;
            c.ClearProjectArchetypes();
            var p = win.Project;
            int n = 0;
            if (p != null && render)
            {
                foreach (var t in p.YtypFiles)
                {
                    var all = t?.AllArchetypes;
                    if (all == null) continue;
                    foreach (var a in all)
                    {
                        if (a == null || a.Hash == 0) continue;
                        c.AddProjectArchetype(a);
                        n++;
                    }
                }
            }
            RegisteredArchetypes = n;
            if (p != null && render)
                foreach (var y in p.YmapFiles)
                {
                    var r = ReresolveArchetypes(y, c);
                    ReresolvedEntities += r.entities; ReresolvedInteriors += r.interiors;
                }
            return n;
        }

        public static (int entities, int interiors) ReresolveArchetypes(YmapFile y, GameFileCache c)
        {
            int swapped = 0, interiors = 0;
            var all = y?.AllEntities;
            if (all == null || c == null) return (0, 0);
            foreach (var e in all)
            {
                if (e == null) continue;
                var arch = c.GetArchetype(e._CEntityDef.archetypeName);
                if (arch != null && !ReferenceEquals(e.Archetype, arch))
                {
                    e.SetArchetype(arch);
                    swapped++;
                    if (e.MloInstance != null) { e.MloInstance.InitYmapEntityArchetypes(c); interiors++; }
                }
                else if (e.MloInstance != null && InteriorNeedsReresolve(e.MloInstance, c))
                {
                    e.MloInstance.InitYmapEntityArchetypes(c);
                    interiors++;
                }
            }
            return (swapped, interiors);
        }

        public static bool InteriorNeedsReresolve(MloInstanceData inst, GameFileCache c)
        {
            if (inst == null) return false;
            var ents = inst.Entities;
            if (ents != null)
                foreach (var ie in ents)
                    if (ie != null && !ReferenceEquals(ie.Archetype, c.GetArchetype(ie._CEntityDef.archetypeName))) return true;
            var sets = inst.EntitySets;
            if (sets != null)
                foreach (var s in sets)
                {
                    if (s?.Entities == null) continue;
                    foreach (var ie in s.Entities)
                        if (ie != null && !ReferenceEquals(ie.Archetype, c.GetArchetype(ie._CEntityDef.archetypeName))) return true;
                }
            return false;
        }
    }
}

