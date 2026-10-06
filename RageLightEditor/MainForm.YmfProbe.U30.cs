using System;
using CodeWalker.GameFiles;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool ymfProbeDone_U30;

        private void ServiceYmfProbe_U30()
        {
            if (ymfProbeDone_U30 || Environment.GetEnvironmentVariable("RLE_YMFPROBE") != "1") return;
            if (gameFiles == null || !gameFiles.Ready) return;
            ymfProbeDone_U30 = true;
            YmfProbe_U30((w, ok, d) => Console.WriteLine($"  {(ok ? "OK  " : "FAIL")} {w}  {d}"));
            BeginInvoke((Action)Close);
        }

        private void YmfProbe_U30(Action<string, bool, string> check)
        {
            var cache = gameFiles?.Cache;
            if (cache?.AllRpfs == null) { check("ymf probe: game archives", false, "no game cache"); return; }
            int total = 0, interiorKind = 0, printed = 0, withInteriors = 0, roundTrips = 0, roundTripBad = 0;
            foreach (var rpf in cache.AllRpfs)
            {
                if (rpf?.AllEntries == null) continue;
                foreach (var e in rpf.AllEntries)
                {
                    if (!(e is RpfFileEntry fe) || fe.NameLower != "_manifest.ymf") continue;
                    total++;
                    byte[] data;
                    try { data = rpf.ExtractFile(fe); } catch { continue; }
                    if (data == null) continue;
                    string xml;
                    try { xml = MetaXml.GetXml(fe, data, out _); } catch { continue; }
                    if (xml == null || !xml.Contains("INTERIOR_DATA")) continue;
                    interiorKind++;
                    if (roundTrips < 40)
                    {
                        roundTrips++;
                        try
                        {
                            var orig = new YmfFile(); orig.Load(data, fe);
                            var back = Editor.ManifestWriter_U30.Read(Editor.ManifestWriter_U30.ToBinary(xml));
                            var a2 = orig.imapDependencies2 ?? Array.Empty<YmfImapDependency2>();
                            var b2 = back.imapDependencies2 ?? Array.Empty<YmfImapDependency2>();
                            bool same = a2.Length == b2.Length;
                            for (int k = 0; same && k < a2.Length; k++)
                                same = a2[k].Dep.imapName.Hash == b2[k].Dep.imapName.Hash && a2[k].Dep.manifestFlags == b2[k].Dep.manifestFlags &&
                                       (a2[k].itypDepArray?.Length ?? 0) == (b2[k].itypDepArray?.Length ?? 0);
                            if (!same) { roundTripBad++; Console.WriteLine("YMFPROBE round trip differs: " + fe.Path); }
                        }
                        catch (Exception ex) { roundTripBad++; Console.WriteLine("YMFPROBE round trip threw on " + fe.Path + ": " + ex.Message); }
                    }
                    bool hasInteriors = xml.Contains("<Interiors>") && !xml.Contains("<Interiors/>");
                    if (hasInteriors) withInteriors++;
                    if (printed < 3 && (hasInteriors || printed == 0) && xml.Length < 20000)
                    {
                        printed++;
                        Console.WriteLine("YMFPROBE ==== " + fe.Path + " (" + xml.Length + " chars)");
                        Console.WriteLine(xml.Length > 3500 ? xml.Substring(0, 3500) + " ...(cut)" : xml);
                    }
                }
            }
            check("ymf probe: the game's interior manifests convert XML -> binary unchanged", roundTripBad == 0 && roundTrips > 0, $"{roundTrips - roundTripBad} of {roundTrips}");
            check("ymf probe: manifests scanned", total > 0, $"{total} manifests, {interiorKind} with INTERIOR_DATA, {withInteriors} with an Interiors list, {printed} printed");
        }
    }
}
