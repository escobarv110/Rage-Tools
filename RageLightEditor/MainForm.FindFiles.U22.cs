using System;
using CodeWalker.GameFiles;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool findFilesDone_U22;

        private void FindFilesProbe_U22()
        {
            if (findFilesDone_U22) return;
            var want = Environment.GetEnvironmentVariable("RLE_FINDFILES");
            if (string.IsNullOrWhiteSpace(want)) { findFilesDone_U22 = true; return; }
            var rm = gameFiles?.Cache?.RpfMan;
            if (rm == null || !gameFiles.Ready) return;
            findFilesDone_U22 = true;
            int n = 0;
            foreach (var rpf in rm.AllRpfs)
            {
                if (rpf?.AllEntries == null) continue;
                foreach (var e in rpf.AllEntries)
                {
                    if (!(e is RpfFileEntry fe)) continue;
                    foreach (var w in want.Split(','))
                        if (fe.NameLower != null && fe.NameLower.Contains(w.Trim().ToLowerInvariant()))
                        {
                            Console.WriteLine("FINDFILE " + fe.Path);
                            var dumpDir = Environment.GetEnvironmentVariable("RLE_FINDFILES_DUMP");
                            if (!string.IsNullOrWhiteSpace(dumpDir))
                            {
                                try
                                {
                                    var data = rm.GetFileData(fe.Path);
                                    if (data != null) System.IO.File.WriteAllBytes(System.IO.Path.Combine(dumpDir, n + "_" + fe.Name), data);
                                }
                                catch (Exception ex) { Console.WriteLine("FINDFILE dump failed: " + ex.Message); }
                            }
                            n++;
                            break;
                        }
                    if (n > 400) break;
                }
            }
            Console.WriteLine($"FINDFILE done, {n} match(es) for '{want}'");
        }
    }
}
