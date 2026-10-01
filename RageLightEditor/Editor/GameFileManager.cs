using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public partial class GameFileManager
    {
        public GameFileCache Cache { get; private set; }
        public string Folder { get; private set; }
        public bool IsGen9 { get; private set; }

        public volatile bool Initialising;
        public volatile bool Ready;
        public string Status = "";
        public string Error = "";

        private static bool keysLoaded;

        public static bool IsValidFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;
            return File.Exists(Path.Combine(folder, "gta5.exe")) ||
                   File.Exists(Path.Combine(folder, "GTA5.exe")) ||
                   File.Exists(Path.Combine(folder, "gta5_enhanced.exe"));
        }

        public static string GuessFolder()
        {
            string[] tails =
            {
                @"Rockstar Games\Grand Theft Auto V",
                @"Steam\steamapps\common\Grand Theft Auto V",
                @"SteamLibrary\steamapps\common\Grand Theft Auto V",
                @"steamapps\common\Grand Theft Auto V",
                @"Epic Games\GTAV",
                @"Games\Grand Theft Auto V",
                @"Grand Theft Auto V",
                @"Grand Theft Auto V Legacy",
                @"Grand Theft Auto V Enhanced",
            };
            var roots = new System.Collections.Generic.List<string>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    roots.Add(d.RootDirectory.FullName);
                    roots.Add(Path.Combine(d.RootDirectory.FullName, "Program Files"));
                    roots.Add(Path.Combine(d.RootDirectory.FullName, "Program Files (x86)"));
                }
            }
            catch { }

            foreach (var root in roots)
                foreach (var tail in tails)
                {
                    string p;
                    try { p = Path.Combine(root, tail); } catch { continue; }
                    if (IsValidFolder(p)) return p;
                }
            return null;
        }

        public void BeginInit(string folder)
        {
            if (Initialising) return;
            Folder = folder;
            Ready = false;
            Error = "";
            Initialising = true;
            Status = "Starting...";

            Task.Run(() =>
            {
                try
                {
                    if (!IsValidFolder(folder))
                        throw new Exception("Not a GTA V folder (gta5.exe not found).");

                    IsGen9 = File.Exists(Path.Combine(folder, "gta5_enhanced.exe")) &&
                             !File.Exists(Path.Combine(folder, "gta5.exe"));

                    Status = "Loading keys from your GTA5.exe...";
                    var phase = System.Diagnostics.Stopwatch.StartNew();
                    if (!keysLoaded)
                    {
                        LoadKeys_U22(folder, IsGen9);
                        keysLoaded = true;
                    }
                    Console.WriteLine($"GAMEINIT keys {phase.ElapsedMilliseconds} ms");
                    phase.Restart();

                    Status = "Scanning RPF archives (first run takes a while)...";
                    var cache = new GameFileCache(2L * 1024 * 1024 * 1024, 10.0, folder, IsGen9, SelectedDlc ?? "", EnableMods, "")
                    {
                        LoadVehicles = false,
                        LoadPeds = false,
                        LoadAudio = false,
                        EnableDlc = true,
                        EnableScriptIpls = ScriptIpls,
                    };
                    cache.Init(s => { if (!string.IsNullOrEmpty(s)) Status = s; }, s => { });
                    Cache = cache;
                    Console.WriteLine($"GAMEINIT archives {phase.ElapsedMilliseconds} ms");
                    Ready = cache.IsInited;
                    Status = Ready ? "Game files ready." : "Init finished but cache not ready.";
                    if (Ready) BeginTextureIndex();
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    Status = "Failed: " + ex.Message;
                    Ready = false;
                }
                finally
                {
                    Initialising = false;
                }
            });
        }

        public volatile LocalAssetIndex ProjectAssets;
        public int ProjectDrawablesServed, ProjectTexturesServed;

        public void SetProjectAssets(LocalAssetIndex assets)
        {
            ProjectAssets = assets;
            lock (texCache) texCache.Clear();
        }

        public DrawableBase GetDrawable(uint archetypeHash, out Archetype archetype)
        {
            archetype = null;
            if (!Ready || Cache == null) return null;

            archetype = Cache.GetArchetype(archetypeHash);
            if (archetype == null) return null;

            var pa = ProjectAssets;
            if (pa != null)
            {
                DrawableBase pd = null;
                try { pd = pa.GetDrawable(archetype, archetypeHash); } catch { pd = null; }
                if (pd != null)
                {
                    ProjectDrawablesServed++;
                    PrewarmTextures(pd, archetype);
                    return pd;
                }
            }

            uint drawHash = archetype.Hash;
            DrawableBase found = null;
            if (archetype.DrawableDict != 0)
            {
                var ydd = Cache.GetYdd(archetype.DrawableDict);
                if (ydd != null)
                {
                    if (!EnsureLoaded(ydd) || ydd.Dict == null) return null;
                    ydd.Dict.TryGetValue(drawHash, out var d);
                    found = d;
                    if (found == null) return null;
                }
            }
            if (found == null)
            {
                var ydr = Cache.GetYdr(drawHash);
                if (ydr != null) found = EnsureLoaded(ydr) ? ydr.Drawable : null;
                else
                {
                    var yft = Cache.GetYft(drawHash);
                    if (yft != null) found = EnsureLoaded(yft) ? yft.Fragment?.Drawable : null;
                }
            }
            if (found != null) PrewarmTextures(found, archetype);
            return found;
        }

        public volatile bool PrewarmHd = true;
        public volatile bool PrewarmSd = Environment.GetEnvironmentVariable("RLE_PREWARMSD") == "1";
        public volatile bool PrewarmEnabled = Environment.GetEnvironmentVariable("RLE_NOPREWARM") != "1";
        public int PrewarmCount; public double PrewarmMs; public int PrewarmTextureAsks;
        public volatile string PrewarmPhase = "idle";

        public void PrewarmTextures(DrawableBase drawable, Archetype archetype)
        {
            if (drawable == null || !Ready || Cache == null || !PrewarmEnabled) return;
            uint txd = archetype?.TextureDict ?? 0;
            uint asset = archetype?._BaseArchetypeDef.assetName ?? 0;
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            PrewarmCount++;
            try
            {
                var models = drawable.AllModels;
                if (models == null) return;
                var embedded = drawable.ShaderGroup?.TextureDictionary;
                var seen = new System.Collections.Generic.HashSet<uint>();
                foreach (var m in models)
                {
                    var geoms = m?.Geometries;
                    if (geoms == null) continue;
                    foreach (var g in geoms)
                    {
                        var prms = g?.Shader?.ParametersList?.Parameters;
                        if (prms == null) continue;
                        foreach (var p in prms)
                        {
                            if (!(p?.Data is TextureBase tb) || tb.NameHash == 0) continue;
                            if (!seen.Add(tb.NameHash)) continue;
                            PrewarmTextureAsks++;
                            PrewarmPhase = "hd " + tb.Name;
                            var hd = PrewarmHd ? FindHdTexture(tb.NameHash, txd, asset) : null;
                            if (hd != null || !PrewarmSd) continue;
                            if (tb is Texture gt && gt.Data?.FullData != null) continue;
                            if (embedded?.Lookup(tb.NameHash)?.Data?.FullData != null) continue;
                            {
                                PrewarmPhase = "sd " + tb.Name;
                                FindTexture(tb.NameHash, txd);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { PrewarmPhase = "threw " + ex.Message; }
            PrewarmPhase = "done";
            PrewarmMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        public void Tick()
        {
            if (!Ready || Cache == null) return;
            try { Cache.BeginFrame(); } catch { }
        }

        public string CacheStatus =>
            Cache == null ? "no cache" :
            $"{Cache.MainCacheMemoryUsage / (1024.0 * 1024.0):0} of {Cache.MainCacheMaxMemory / (1024.0 * 1024.0):0} MB, {Cache.MainCacheCount} files{(Cache.MainCacheFull ? ", FULL" : "")}";

        public bool EnsureLoaded<T>(T file) where T : GameFile, PackedFile
        {
            if (file == null) return false;
            if (file.Loaded) return true;
            lock (file)
            {
                if (file.Loaded) return true;
                try { file.Loaded = Cache.LoadFile(file); }
                catch { file.Loaded = false; }
                return file.Loaded;
            }
        }

        private readonly System.Collections.Generic.Dictionary<uint, uint> textureIndex =
            new System.Collections.Generic.Dictionary<uint, uint>();
        private readonly object textureIndexLock = new object();

        public volatile int TextureIndexDone;
        public volatile int TextureIndexTotal;
        public volatile bool TextureIndexReady;
        public volatile bool TextureIndexFromCache;

        private const int TextureIndexVersion = 1;
        private int textureIndexDone;

        private string TextureIndexPath()
        {
            uint h = 2166136261u;
            foreach (var c in (Folder ?? "").ToLowerInvariant()) { h ^= c; h *= 16777619u; }
            return Path.Combine(AppContext.BaseDirectory, "texindex_" + h.ToString("X8") + ".bin");
        }

        private void BeginTextureIndex()
        {
            Task.Run(() =>
            {
                try
                {
                    if (LoadTextureIndex())
                    {
                        TextureIndexFromCache = true;
                        TextureIndexTotal = 1;
                        TextureIndexDone = 1;
                        return;
                    }

                    var entries = new System.Collections.Generic.List<RpfFileEntry>();
                    foreach (var rpf in Cache.AllRpfs)
                    {
                        if (rpf?.AllEntries == null) continue;
                        foreach (var e in rpf.AllEntries)
                        {
                            if (e is RpfFileEntry fe &&
                                fe.NameLower.EndsWith(".ytd", StringComparison.Ordinal))
                                entries.Add(fe);
                        }
                    }
                    TextureIndexTotal = entries.Count;

                    var opts = new System.Threading.Tasks.ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
                    };
                    System.Threading.Tasks.Parallel.ForEach(entries, opts, e =>
                    {
                        try
                        {
                            var ytd = new YtdFile(e);
                            if (Cache.LoadFile(ytd))
                            {
                                var names = ytd.TextureDict?.TextureNameHashes?.data_items;
                                if (names != null && names.Length > 0)
                                {
                                    lock (textureIndexLock)
                                    {
                                        foreach (var h in names)
                                            if (!textureIndex.ContainsKey(h)) textureIndex[h] = e.ShortNameHash;
                                    }
                                }
                            }
                        }
                        catch { }
                        TextureIndexDone = System.Threading.Interlocked.Increment(ref textureIndexDone);
                    });

                    SaveTextureIndex();
                }
                catch { }
                finally { TextureIndexReady = true; }
            });
        }

        private bool LoadTextureIndex()
        {
            try
            {
                var path = TextureIndexPath();
                if (!File.Exists(path)) return false;
                using var fs = File.OpenRead(path);
                using var r = new BinaryReader(fs);
                if (r.ReadInt32() != TextureIndexVersion) return false;
                int n = r.ReadInt32();
                if (n <= 0 || n > 5000000) return false;
                lock (textureIndexLock)
                {
                    for (int i = 0; i < n; i++) textureIndex[r.ReadUInt32()] = r.ReadUInt32();
                }
                return true;
            }
            catch { return false; }
        }

        private void SaveTextureIndex()
        {
            try
            {
                using var fs = File.Create(TextureIndexPath());
                using var w = new BinaryWriter(fs);
                w.Write(TextureIndexVersion);
                lock (textureIndexLock)
                {
                    w.Write(textureIndex.Count);
                    foreach (var kv in textureIndex) { w.Write(kv.Key); w.Write(kv.Value); }
                }
            }
            catch { }
        }

        public void ForgetMissingTextures()
        {
            lock (texCache)
            {
                var dead = new System.Collections.Generic.List<ulong>();
                foreach (var kv in texCache) if (kv.Value == null) dead.Add(kv.Key);
                foreach (var k in dead) texCache.Remove(k);
            }
        }

        private Texture FindIndexedTexture(uint nameHash)
        {
            uint ytdHash;
            lock (textureIndexLock)
            {
                if (!textureIndex.TryGetValue(nameHash, out ytdHash)) return null;
            }
            try
            {
                var ytd = Cache.GetYtd(ytdHash);
                if (!EnsureLoaded(ytd)) return null;
                return ytd?.TextureDict?.Lookup(nameHash);
            }
            catch { return null; }
        }

        private readonly System.Collections.Generic.Dictionary<ulong, Texture> texCache =
            new System.Collections.Generic.Dictionary<ulong, Texture>();

        public Texture FindTexture(uint nameHash, uint txdHash)
        {
            if (!Ready || Cache == null || nameHash == 0) return null;
            ulong key = ((ulong)txdHash << 32) | nameHash;
            lock (texCache)
            {
                if (texCache.TryGetValue(key, out var cached)) return cached;
            }

            Texture found = null;
            try
            {
                var pa = ProjectAssets;
                uint h = txdHash;
                for (int guard = 0; h != 0 && guard < 16; guard++)
                {
                    var t = GetTextureDict(h)?.TextureDict?.Lookup(nameHash);
                    if (t?.Data?.FullData != null) { found = t; break; }
                    h = Cache.TryGetParentYtdHash(h);
                }
                if (found == null && pa != null)
                {
                    var t = pa.FindTexture(nameHash);
                    if (t?.Data?.FullData != null) { found = t; ProjectTexturesServed++; }
                }
                if (found == null)
                {
                    var ytd = Cache.TryGetTextureDictForTexture(nameHash);
                    if (EnsureLoaded(ytd)) found = ytd.TextureDict?.Lookup(nameHash);
                }
                if (found?.Data?.FullData == null) found = FindIndexedTexture(nameHash) ?? found;
            }
            catch { }

            if (found == null && !TextureIndexReady) return null;
            lock (texCache) { texCache[key] = found; }
            return found;
        }

        public string LastYtdStatus = "";

        public Texture FindHdTexture(uint nameHash, uint txdHash, uint assetNameHash)
        {
            if (!Ready || Cache == null || nameHash == 0) return null;
            ulong key = 0x8000000000000000UL | ((ulong)(txdHash ^ (assetNameHash * 2654435761u)) << 32) | nameHash;
            lock (texCache)
            {
                if (texCache.TryGetValue(key, out var cached)) return cached;
            }

            Texture found = null;
            try
            {
                uint h = assetNameHash;
                if (h != 0)
                {
                    uint hd = Cache.TryGetHDTextureHash(h);
                    if (hd != h)
                    {
                        var t = GetHdTextureDict(hd)?.TextureDict?.Lookup(nameHash);
                        if (t?.Data?.FullData != null) found = t;
                    }
                }
                h = txdHash;
                for (int guard = 0; found == null && h != 0 && guard < 16; guard++)
                {
                    uint hd = Cache.TryGetHDTextureHash(h);
                    if (hd != h)
                    {
                        var t = GetHdTextureDict(hd)?.TextureDict?.Lookup(nameHash);
                        if (t?.Data?.FullData != null) { found = t; break; }
                    }
                    h = Cache.TryGetParentYtdHash(h);
                }
            }
            catch { found = null; }

            lock (texCache) { texCache[key] = found; }
            return found;
        }

        private readonly object hdLock = new object();
        private readonly System.Collections.Generic.Dictionary<uint, YtdFile> hdYtds = new System.Collections.Generic.Dictionary<uint, YtdFile>();
        private readonly System.Collections.Generic.Dictionary<uint, long> hdYtdBytes = new System.Collections.Generic.Dictionary<uint, long>();
        private readonly System.Collections.Generic.LinkedList<uint> hdLru = new System.Collections.Generic.LinkedList<uint>();
        private readonly System.Collections.Generic.Dictionary<uint, System.Collections.Generic.LinkedListNode<uint>> hdLruNodes = new System.Collections.Generic.Dictionary<uint, System.Collections.Generic.LinkedListNode<uint>>();
        private readonly System.Collections.Generic.HashSet<uint> hdMissing = new System.Collections.Generic.HashSet<uint>();
        private long hdBytes;
        public long HdTextureBudget = 640L * 1024 * 1024;
        public int HdYtdsResident => hdYtds.Count;
        public long HdYtdBytesResident => hdBytes;
        public int HdYtdsLoaded, HdYtdsReleased;

        private YtdFile GetHdTextureDict(uint hash)
        {
            if (hash == 0) return null;
            lock (hdLock)
            {
                if (hdYtds.TryGetValue(hash, out var y))
                {
                    var node = hdLruNodes[hash];
                    hdLru.Remove(node); hdLru.AddLast(node);
                    return y;
                }
                if (hdMissing.Contains(hash)) return null;
            }
            YtdFile ytd = null;
            try
            {
                var entry = Cache.GetYtdEntry(hash);
                if (entry != null)
                {
                    ytd = new YtdFile(entry);
                    if (!EnsureLoaded(ytd) || ytd.TextureDict == null) ytd = null;
                }
            }
            catch { ytd = null; }
            lock (hdLock)
            {
                if (ytd == null) { hdMissing.Add(hash); return null; }
                if (hdYtds.TryGetValue(hash, out var already)) return already;
                long bytes = 0;
                var texs = ytd.TextureDict.Textures?.data_items;
                if (texs != null) foreach (var t in texs) bytes += t?.Data?.FullData?.Length ?? 0;
                hdYtds[hash] = ytd;
                hdYtdBytes[hash] = bytes;
                hdLruNodes[hash] = hdLru.AddLast(hash);
                hdBytes += bytes;
                HdYtdsLoaded++;
                while (hdBytes > HdTextureBudget && hdLru.Count > 1)
                {
                    uint old = hdLru.First.Value;
                    hdLru.RemoveFirst();
                    hdLruNodes.Remove(old);
                    hdYtds.Remove(old);
                    hdBytes -= hdYtdBytes[old];
                    hdYtdBytes.Remove(old);
                    HdYtdsReleased++;
                }
                return ytd;
            }
        }

        public class GameXmlFile
        {
            public string Name;
            public string Path;
            public override string ToString() => Name;
        }

        private List<GameXmlFile> timecycleFiles;

        public List<GameXmlFile> ListTimecycles()
        {
            if (timecycleFiles != null) return timecycleFiles;
            var list = new List<GameXmlFile>();
            var rpfman = Cache?.RpfMan;
            if (!Ready || rpfman?.EntryDict == null) return list;

            foreach (var kv in rpfman.EntryDict)
            {
                var path = kv.Key;
                if (path == null || !path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
                if (!(kv.Value is RpfFileEntry)) continue;
                var name = System.IO.Path.GetFileNameWithoutExtension(kv.Value.Name);
                if (string.IsNullOrEmpty(name)) continue;
                bool inFolder = path.IndexOf("timecycle", StringComparison.OrdinalIgnoreCase) >= 0;
                bool looksLikeCycle = name.StartsWith("w_", StringComparison.OrdinalIgnoreCase);
                if (!inFolder && !looksLikeCycle) continue;
                list.Add(new GameXmlFile { Name = name, Path = path });
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            timecycleFiles = list;
            return list;
        }

        public IEnumerable<string> FindEntries(string fileName)
        {
            var rpfman = Cache?.RpfMan;
            if (!Ready || rpfman?.EntryDict == null) yield break;
            foreach (var kv in rpfman.EntryDict)
            {
                if (kv.Value is RpfFileEntry &&
                    string.Equals(kv.Value.Name, fileName, StringComparison.OrdinalIgnoreCase))
                    yield return kv.Key;
            }
        }

        public string ReadText(string rpfPath)
        {
            try { return Cache?.RpfMan?.GetFileUTF8Text(rpfPath); }
            catch { return null; }
        }

        public YtdFile GetTextureDict(uint hash)
        {
            if (!Ready || Cache == null || hash == 0) return null;
            var pa = ProjectAssets;
            if (pa != null && pa.HasTextureDict(hash))
            {
                YtdFile pytd = null;
                try { pytd = pa.GetTextureDict(hash); } catch { pytd = null; }
                if (pytd?.TextureDict != null) { ProjectTexturesServed++; LastYtdStatus = "project"; return pytd; }
            }
            var ytd = Cache.GetYtd(hash);
            if (ytd == null) { LastYtdStatus = "no entry"; return null; }
            if (!EnsureLoaded(ytd)) { LastYtdStatus = "load failed"; return null; }
            LastYtdStatus = ytd.TextureDict != null ? "ok" : "loaded, no dict";
            return ytd.TextureDict != null ? ytd : null;
        }
    }
}

