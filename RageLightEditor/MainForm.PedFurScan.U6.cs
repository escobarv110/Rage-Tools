using System;
using System.Linq;
using CodeWalker.GameFiles;
using RageLightEditor.Rendering;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool pedFurScanDone_U6;

        partial void OnWorldTick_PedFurScan_U6()
        {
            if (pedFurScanDone_U6) return;
            var want = Environment.GetEnvironmentVariable("RLE_PEDFURSCAN");
            if (string.IsNullOrWhiteSpace(want)) return;
            var c = gameFiles?.Cache;
            if (c?.YddDict == null || !gameFiles.Ready) return;
            pedFurScanDone_U6 = true;

            foreach (var raw in want.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = raw.Trim().ToLowerInvariant();
                if (!c.YddDict.TryGetValue(JenkHash.GenHash(name), out var fe) || fe == null)
                {
                    Console.WriteLine($"PEDFURSCAN {name}: not in this install");
                    continue;
                }
                try
                {
                    var ydd = c.RpfMan.GetFile<YddFile>(fe);
                    var drawables = Editor.Scene.DrawablesInYdd_V38(ydd);
                    int furMats = 0;
                    foreach (var d in drawables)
                    {
                        foreach (var sh in d.Drawable?.ShaderGroup?.Shaders?.data_items ?? Array.Empty<ShaderFX>())
                        {
                            if (!ModelRenderer.IsPedFurShader_V38(sh?.Name.ToString())) continue;
                            furMats++;
                            var bits = (sh.ParametersList?.Parameters ?? Array.Empty<ShaderParameter>())
                                .Zip(sh.ParametersList?.Hashes ?? Array.Empty<MetaName>(),
                                     (p, hh) => (Name: ((ShaderParamNames)(uint)hh).ToString(), p.Data))
                                .Where(x => x.Data is SharpDX.Vector4 || x.Data is TextureBase)
                                .Select(x => x.Data is TextureBase tb ? $"{x.Name}='{tb.Name}'" : $"{x.Name}={FmtV4_U6((SharpDX.Vector4)x.Data)}");
                            Console.WriteLine($"PEDFURSCAN {name} / {d.Name}: {string.Join("  ", bits)}");
                        }
                    }
                    if (furMats == 0) Console.WriteLine($"PEDFURSCAN {name}: {drawables.Count} drawable(s), no ped_fur material");
                }
                catch (Exception ex) { Console.WriteLine($"PEDFURSCAN {name}: {ex.Message}"); }
            }
        }

        private bool clipArchDone_U7;

        partial void OnWorldTick_ClipArchScan_U7()
        {
            if (clipArchDone_U7) return;
            var want = Environment.GetEnvironmentVariable("RLE_CLIPARCH");
            if (string.IsNullOrWhiteSpace(want)) return;
            var c = gameFiles?.Cache;
            if (c?.YtypDict == null || !gameFiles.Ready) return;
            clipArchDone_U7 = true;

            var filter = want.Trim() == "1" ? null : want.Trim().ToLowerInvariant();
            int total = 0, shown = 0;
            foreach (var ytyp in c.YtypDict.Values)
            {
                foreach (var a in ytyp?.AllArchetypes ?? Array.Empty<Archetype>())
                {
                    if (a == null || a.ClipDict == 0) continue;
                    total++;
                    var nm = a.Name ?? "";
                    if (filter != null && nm.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (shown++ < 60)
                        Console.WriteLine($"CLIPARCH {nm}  clipDict={a.ClipDict}  ({ytyp.RpfFileEntry?.Name})");
                }
            }
            Console.WriteLine($"CLIPARCH {total:N0} archetype(s) in this install name a clip dictionary" +
                              (filter != null ? $", {shown:N0} matching '{filter}'" : ""));
        }

        private static string FmtV4_U6(SharpDX.Vector4 v) =>
            v.Y == 0 && v.Z == 0 && v.W == 0 ? v.X.ToString("0.####")
                : $"({v.X:0.####},{v.Y:0.####},{v.Z:0.####},{v.W:0.####})";
    }
}
