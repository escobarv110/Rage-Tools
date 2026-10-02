using System;
using System.Collections.Generic;
using CodeWalker.GameFiles;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool ytypEntsDone_U22;

        private void YtypEntsProbe_U22()
        {
            if (ytypEntsDone_U22) return;
            var want = Environment.GetEnvironmentVariable("RLE_YTYPENTS");
            if (string.IsNullOrWhiteSpace(want)) { ytypEntsDone_U22 = true; return; }
            var c = gameFiles?.Cache;
            if (c?.RpfMan == null || !gameFiles.Ready) return;
            ytypEntsDone_U22 = true;
            foreach (var rpf in c.RpfMan.AllRpfs)
            {
                if (rpf?.AllEntries == null) continue;
                foreach (var e in rpf.AllEntries)
                {
                    if (!(e is RpfFileEntry fe) || fe.NameLower == null || !fe.NameLower.EndsWith(".ytyp")) continue;
                    bool fileMatch = fe.NameLower.Contains(want.ToLowerInvariant());
                    if (!fileMatch && !fe.NameLower.StartsWith("v_")) continue;
                    YtypFile y = null;
                    try { y = c.RpfMan.GetFile<YtypFile>(fe); } catch { }
                    if (y == null) continue;
                    foreach (var a in y.AllArchetypes ?? Array.Empty<Archetype>())
                    {
                        if (!(a is MloArchetype m)) continue;
                        if (!fileMatch && (m.Name ?? "").IndexOf(want, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var counts = new Dictionary<string, int>();
                        foreach (var me in m.entities ?? Array.Empty<MCEntityDef>())
                        {
                            var nh = me._Data.archetypeName;
                            var arch = c.GetArchetype(nh);
                            string key = nh.ToString() + (arch == null ? " (NO ARCHETYPE)" : "");
                            counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
                        }
                        foreach (var kv in counts) Console.WriteLine($"YTYPENTS {fe.Path} {m.Name}: {kv.Key} x{kv.Value}");
                    }
                    foreach (var ce in y.CompositeEntityTypes ?? Array.Empty<CCompositeEntityType>())
                        Console.WriteLine($"YTYPENTS {fe.Path} composite {ce.Name} start {ce.StartModel} end {ce.EndModel}");
                }
            }
            Console.WriteLine("YTYPENTS done");
        }
    }
}
