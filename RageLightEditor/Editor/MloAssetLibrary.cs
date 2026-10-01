using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public enum MloAssetSource { Archive, PropFolder, Project, LightProps }

    public sealed class MloAssetItem
    {
        public string Name = "";
        public string FileName = "";
        public string Path = "";
        public bool IsYft;
        public bool FromArchive;
        public MloAssetSource Source;
        public long Size;
        public int LightCount = -1;
        private LightPropEntry entry;

        public LightPropEntry ToEntry() => entry ??= new LightPropEntry
        {
            Name = Name, Path = Path, FromArchive = FromArchive, IsYft = IsYft,
            Hash = JenkHash.GenHash((Path ?? Name).ToLowerInvariant()),
            LightCount = Math.Max(LightCount, 0),
        };

        public string SourceLabel => Source switch
        {
            MloAssetSource.Archive => "game",
            MloAssetSource.PropFolder => "prop folder",
            MloAssetSource.Project => "project",
            _ => "light prop",
        };
    }

    public partial class MloAssetLibrary
    {
        public string Query = "";
        public int SourceFilter;
        public static readonly string[] SourceLabels = { "All sources", "Game archives", "Prop folders", "Project folder", "Light props" };
        public int KindFilter;
        public static readonly string[] KindLabels = { ".ydr + .yft", ".ydr only", ".yft only" };

        public bool Dirty = true;
        public readonly List<MloAssetItem> Results = new List<MloAssetItem>();
        public int ResultTotal;
        public MloAssetItem Selected;
        public string Status = "";
        public bool ArchivesReady;
        public int ArchiveModels, FolderFiles, ProjectFiles, LightPropCount;
        public int GameModels;
        public bool ArchiveIndexing;
        public string ArchiveStatus = "";
        public readonly List<string> ProjectFolders = new List<string>();
        public readonly List<string> PropFolders = new List<string>();

        public int PlaceAt;
        public static readonly string[] PlaceAtLabels = { "Snap point", "View target", "View surface", "Room floor" };
        public bool SelectAfterPlace = true;
        public bool ShowThumbnails = true;
        public bool ClickPlaces = true;
        public readonly List<MloAssetItem> Recent = new List<MloAssetItem>();
        public const int MaxRecent = 24;

        public MloAssetItem RequestPlace;
        public int RequestPlaceCount = 1;
        public string RequestPlaceFolder;
        public bool RequestPickFolderToPlace;
        public bool RequestAddPropFolder;
        public bool RequestRescan;
        public bool RequestResolveTyped;

        public Func<LightPropEntry, IntPtr> ThumbnailOf;
        public Action<LightPropEntry> BeginDrag;

        public void Touch(MloAssetItem it)
        {
            if (it == null) return;
            Recent.RemoveAll(r => string.Equals(r.Path, it.Path, StringComparison.OrdinalIgnoreCase));
            Recent.Insert(0, it);
            if (Recent.Count > MaxRecent) Recent.RemoveRange(MaxRecent, Recent.Count - MaxRecent);
        }

        public static int ScanFolders(IEnumerable<string> folders, string query, MloAssetSource source, List<MloAssetItem> into, int max = 400, int maxDepth = 6, int maxFiles = 40000)
        {
            int total = 0, seen = 0;
            query = (query ?? "").Trim().ToLowerInvariant();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in folders ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                var stack = new Stack<(string dir, int depth)>();
                stack.Push((root, 0));
                while (stack.Count > 0 && seen < maxFiles)
                {
                    var (dir, depth) = stack.Pop();
                    if (!visited.Add(Path.GetFullPath(dir))) continue;
                    IEnumerable<string> files;
                    try { files = Directory.EnumerateFiles(dir); } catch { continue; }
                    foreach (var f in files)
                    {
                        seen++;
                        var ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext != ".ydr" && ext != ".yft") continue;
                        var name = Path.GetFileName(f);
                        if (query.Length > 0 && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        total++;
                        if (into.Count < max)
                        {
                            long size = 0; try { size = new FileInfo(f).Length; } catch { }
                            into.Add(new MloAssetItem
                            {
                                Name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant(), FileName = name, Path = f,
                                IsYft = ext == ".yft", FromArchive = false, Source = source, Size = size,
                            });
                        }
                    }
                    if (depth >= maxDepth) continue;
                    IEnumerable<string> subs;
                    try { subs = Directory.EnumerateDirectories(dir); } catch { continue; }
                    foreach (var s in subs) stack.Push((s, depth + 1));
                }
            }
            return total;
        }

        public static List<MloAssetItem> ModelsInFolder(string folder, MloAssetSource source = MloAssetSource.PropFolder)
        {
            var list = new List<MloAssetItem>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return list;
            ScanFolders(new[] { folder }, "", source, list, max: 2000, maxDepth: 0);
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        public static void FromArchive(IEnumerable<ArchiveBrowser.Entry> hits, List<MloAssetItem> into, int max)
        {
            foreach (var h in hits)
            {
                if (into.Count >= max) return;
                var n = h.File?.Name ?? "";
                into.Add(new MloAssetItem
                {
                    Name = Path.GetFileNameWithoutExtension(n).ToLowerInvariant(), FileName = n, Path = h.Path,
                    IsYft = n.EndsWith(".yft", StringComparison.OrdinalIgnoreCase), FromArchive = true,
                    Source = MloAssetSource.Archive, Size = h.File?.GetFileSize() ?? 0,
                });
            }
        }

        public static void FromLightProps(IEnumerable<LightPropEntry> hits, List<MloAssetItem> into, int max)
        {
            foreach (var e in hits)
            {
                if (into.Count >= max) return;
                into.Add(new MloAssetItem
                {
                    Name = e.Name, FileName = e.Name + (e.IsYft ? ".yft" : ".ydr"), Path = e.Path, IsYft = e.IsYft,
                    FromArchive = e.FromArchive, Source = MloAssetSource.LightProps, LightCount = e.LightCount,
                });
            }
        }

        public static void Sort(List<MloAssetItem> items, string query)
        {
            query = (query ?? "").Trim().ToLowerInvariant();
            items.Sort((a, b) =>
            {
                bool ea = a.Name == query, eb = b.Name == query;
                if (ea != eb) return ea ? -1 : 1;
                int c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return a.FromArchive.CompareTo(b.FromArchive);
            });
        }
    }
}

