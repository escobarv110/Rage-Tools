using System;
using System.Collections.Generic;
using System.Linq;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public partial class ArchiveBrowser
    {
        public readonly struct Entry
        {
            public readonly RpfFileEntry File;
            public readonly string NameLower;
            public readonly string Path;

            public Entry(RpfFileEntry f)
            {
                File = f;
                NameLower = f.NameLower ?? f.Name?.ToLowerInvariant() ?? "";
                Path = f.Path ?? "";
            }
        }

        private readonly List<Entry> index = new List<Entry>();
        private readonly List<Entry> results = new List<Entry>();

        public readonly List<RpfFile> Roots = new List<RpfFile>();

        public bool Ready { get; private set; }
        public int FileCount => index.Count;

        public IReadOnlyList<Entry> Results => results;
        public int ResultTotal { get; private set; }

        public RpfDirectoryEntry CurrentDir;
        public RpfFileEntry Selected;

        public void Build(RpfManager rpfMan)
        {
            index.Clear();
            Roots.Clear();
            Ready = false;
            if (rpfMan?.AllRpfs == null) return;

            foreach (var rpf in rpfMan.AllRpfs)
            {
                if (rpf == null) continue;
                if (rpf.Parent == null) Roots.Add(rpf);
                if (rpf.AllEntries == null) continue;
                foreach (var e in rpf.AllEntries)
                    if (e is RpfFileEntry fe) index.Add(new Entry(fe));
            }

            Roots.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
            Ready = true;
        }

        public void Search(string query, string extension, int max = 500)
        {
            results.Clear();
            ResultTotal = 0;
            if (!Ready) return;

            query = (query ?? "").Trim().ToLowerInvariant();
            extension = (extension ?? "").Trim().ToLowerInvariant();
            bool anyExt = string.IsNullOrEmpty(extension) || extension == "*";
            if (query.Length == 0 && anyExt) return;

            foreach (var e in index)
            {
                if (!anyExt && !e.NameLower.EndsWith(extension, StringComparison.Ordinal)) continue;
                if (query.Length > 0 && e.NameLower.IndexOf(query, StringComparison.Ordinal) < 0) continue;
                ResultTotal++;
                if (results.Count < max) results.Add(e);
            }
        }

        public int Find(string query, IReadOnlyList<string> extensions, List<Entry> into, int max = 400)
            => Find(query, extensions, into, max, null);

        public int Find(string query, IReadOnlyList<string> extensions, List<Entry> into, int max, string pathPrefix)
        {
            into.Clear();
            if (!Ready) return 0;
            query = (query ?? "").Trim().ToLowerInvariant();
            string prefix = string.IsNullOrEmpty(pathPrefix) ? null : pathPrefix.TrimEnd('\\', '/') + "\\";
            int total = 0;
            foreach (var e in index)
            {
                if (prefix != null && !e.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                bool extOk = extensions == null || extensions.Count == 0;
                if (!extOk)
                    for (int i = 0; i < extensions.Count; i++)
                        if (e.NameLower.EndsWith(extensions[i], StringComparison.Ordinal)) { extOk = true; break; }
                if (!extOk) continue;
                if (query.Length > 0 && e.NameLower.IndexOf(query, StringComparison.Ordinal) < 0) continue;
                total++;
                if (into.Count < max) into.Add(e);
            }
            return total;
        }

        public static readonly string[] FilterLabels =
        {
            "All files", "Models", "Fragments", "Drawable dicts",
            "Textures", "Archetypes", "Ymaps",
            "Collision", "Meta", "Audio", "Archives",
        };
        private static readonly string[] FilterExts =
        {
            "", ".ydr", ".yft", ".ydd", ".ytd", ".ytyp", ".ymap", ".ybn", ".ymt", ".awc", ".rpf",
        };
        public static string ExtensionFor(int filterIndex) =>
            filterIndex >= 0 && filterIndex < FilterExts.Length ? FilterExts[filterIndex] : "";

        public static string KindOf(RpfEntry e)
        {
            if (e is RpfDirectoryEntry) return "dir";
            var n = e?.NameLower ?? "";
            int dot = n.LastIndexOf('.');
            return dot >= 0 && dot < n.Length - 1 ? n.Substring(dot + 1) : "";
        }

        public static string SizeOf(RpfEntry e)
        {
            long b = e is RpfFileEntry fe ? fe.GetFileSize() : 0;
            if (b <= 0) return "";
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0.#") + " KB";
            return (b / (1024.0 * 1024.0)).ToString("0.#") + " MB";
        }

        public static bool IsDrawable(RpfEntry e)
        {
            var k = KindOf(e);
            return k == "ydr" || k == "yft" || k == "ydd";
        }

        public static string RootLabel(RpfFile rpf)
        {
            var path = rpf?.Path ?? "";
            var name = rpf?.Name ?? path;
            int slash = path.LastIndexOf('\\');
            if (slash <= 0) return name;
            int prev = path.LastIndexOf('\\', slash - 1);
            var folder = prev >= 0 ? path.Substring(prev + 1, slash - prev - 1) : path.Substring(0, slash);
            return string.IsNullOrEmpty(folder) ? name : folder + " / " + name;
        }

        public static byte[] Extract(RpfFileEntry e)
        {
            if (e?.File == null) return null;
            return e.File.ExtractFile(e);
        }

        public static byte[] ExtractForDisk(RpfFileEntry e)
        {
            var data = Extract(e);
            if (data == null) return null;
            if (e is RpfResourceFileEntry rrfe)
            {
                data = ResourceBuilder.Compress(data);
                data = ResourceBuilder.AddResourceHeader(rrfe, data);
            }
            return data;
        }
    }
}

