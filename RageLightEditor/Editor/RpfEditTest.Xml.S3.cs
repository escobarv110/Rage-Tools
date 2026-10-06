using System;
using System.IO;
using System.Linq;
using System.Text;
using CodeWalker.GameFiles;
using SharpDX;

namespace RageLightEditor.Editor
{
    public static partial class RpfEditTest
    {
        private static int RunXmlImportChecks_S3(string root, Action<string, bool, string> check)
        {
            var fails = new Counter(check);
            var ex = new RpfExplorer();

            var def = new ArchetypeDef
            {
                Name = "wss3_xml_probe",
                TextureDict = "wss3_xml_probe",
                LodDist = 137.0f,
                BbMin = new Vector3(-2, -3, -1),
                BbMax = new Vector3(2, 3, 4),
                BsCentre = Vector3.Zero,
                BsRadius = 5.5f,
            };
            byte[] ytypBytes;
            try
            {
                ytypBytes = ArchetypeBuilder.BuildYtyp("wss3_xml", new[] { def }).Save();
            }
            catch (Exception e)
            {
                fails.Check("XML import: build a .ytyp to convert", false, e.Message);
                return fails.Count;
            }
            var ytypPath = Path.Combine(root, "wss3_xml.ytyp");
            File.WriteAllBytes(ytypPath, ytypBytes);

            var arcPath = Path.Combine(root, "wss3_xml_test.rpf");
            var rpf = RpfFile.CreateNew(root, arcPath, RpfEncryption.OPEN);
            var target = new RpfEdit.Target(rpf.Root, null);

            var res = RpfEdit.ImportFiles(true, ex, target, new[] { ytypPath }, true);
            var binEntry = rpf.Root.Files.FirstOrDefault(f => f.NameLower == "wss3_xml.ytyp");
            fails.Check("XML import: the binary .ytyp imports first",
                        res.Ok && binEntry is RpfResourceFileEntry, res.Message);
            if (binEntry == null) return fails.Count;

            var tmp = Path.Combine(root, "xmlout");
            Directory.CreateDirectory(tmp);
            var xml = MetaXml.GetXml(binEntry, ArchiveBrowser.Extract(binEntry), out _, tmp);
            fails.Check("XML import: the .ytyp converts to XML",
                        !string.IsNullOrEmpty(xml) && xml.Contains("CMapTypes"),
                        (xml?.Length ?? 0) + " chars");
            if (string.IsNullOrEmpty(xml)) return fails.Count;

            var xmlPath = Path.Combine(root, "wss3_from_xml.ytyp.xml");
            File.WriteAllText(xmlPath, xml, new UTF8Encoding(false));

            RpfEdit.NewFolder(true, ex, target, "xml");
            var xmlDir = rpf.Root.Directories.First(d => d.NameLower == "xml");
            var xmlTarget = new RpfEdit.Target(xmlDir, null);
            res = RpfEdit.ImportFiles(true, ex, xmlTarget, new[] { xmlPath }, false);

            var made = xmlDir.Files.FirstOrDefault(f => f.NameLower == "wss3_from_xml.ytyp");
            fails.Check("XML import: a .ytyp.xml lands as a .ytyp resource",
                        res.Ok && made is RpfResourceFileEntry,
                        res.Message + " | " + string.Join(",", xmlDir.Files.Select(f => f.Name)));
            fails.Check("XML import: the status line says it converted",
                        res.Message.IndexOf("converted back to wss3_from_xml.ytyp",
                                            StringComparison.OrdinalIgnoreCase) >= 0,
                        res.Message);
            fails.Check("XML import: the .xml itself is NOT left in the archive",
                        !xmlDir.Files.Any(f => f.NameLower.EndsWith(".xml")), "");

            Archetype back = null;
            string why = "no entry";
            if (made != null)
            {
                try
                {
                    var ytyp = new YtypFile();
                    ytyp.Load(ArchiveBrowser.ExtractForDisk(made));
                    back = ytyp.AllArchetypes?.FirstOrDefault();
                    why = ytyp.AllArchetypes?.Length.ToString() ?? "null";
                }
                catch (Exception e) { why = e.Message; }
            }
            fails.Check("XML import: the imported .ytyp reads back as a .ytyp",
                        back != null && back.Hash == JenkHash.GenHash("wss3_xml_probe"),
                        back != null ? back.Name.ToString() : why);
            fails.Check("XML import: the archetype survived the round trip",
                        back != null &&
                        Math.Abs(back.LodDist - 137.0f) < 0.01f &&
                        (back.BBMax - def.BbMax).Length() < 0.01f &&
                        Math.Abs(back.BSRadius - def.BsRadius) < 0.01f,
                        back != null ? $"lod={back.LodDist} bb={back.BBMin}..{back.BBMax} r={back.BSRadius}" : "-");

            var plainPath = Path.Combine(root, "readme.xml");
            var plainBody = "<notes><line>an ordinary xml file</line></notes>";
            File.WriteAllText(plainPath, plainBody);
            res = RpfEdit.ImportFiles(true, ex, xmlTarget, new[] { plainPath }, false);
            var plain = xmlDir.Files.FirstOrDefault(f => f.NameLower == "readme.xml");
            var plainBack = plain != null ? Encoding.UTF8.GetString(ArchiveBrowser.Extract(plain) ?? Array.Empty<byte>()) : "";
            fails.Check("XML import: a plain .xml goes in as itself, and says so",
                        res.Ok && plain != null && plainBack == plainBody &&
                        res.Message.IndexOf("plain XML", StringComparison.OrdinalIgnoreCase) >= 0,
                        res.Message);

            var brokenPath = Path.Combine(root, "broken.ytyp.xml");
            File.WriteAllText(brokenPath, "<CMapTypes><archetypes><Item></CMapTypes>");
            res = RpfEdit.ImportFiles(true, ex, xmlTarget, new[] { brokenPath }, false);
            fails.Check("XML import: XML that does not parse is refused, not written as text",
                        !res.Ok && res.Message.IndexOf("broken.ytyp.xml", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        !xmlDir.Files.Any(f => f.NameLower == "broken.ytyp") &&
                        !xmlDir.Files.Any(f => f.NameLower == "broken.ytyp.xml"),
                        res.Message);

            var noConvPath = Path.Combine(root, "nothing.ynv.xml");
            File.WriteAllText(noConvPath, "<Nonsense><a>1</a></Nonsense>");
            res = RpfEdit.ImportFiles(true, ex, xmlTarget, new[] { noConvPath }, false);
            fails.Check("XML import: a converter that produces nothing is a failure, not a silent raw import",
                        !res.Ok &&
                        !xmlDir.Files.Any(f => f.NameLower == "nothing.ynv") &&
                        !xmlDir.Files.Any(f => f.NameLower == "nothing.ynv.xml"),
                        res.Message);

            res = RpfEdit.ImportFiles(true, ex, xmlTarget, new[] { xmlPath }, true);
            fails.Check("XML import: Import raw puts the .xml in unconverted",
                        res.Ok && xmlDir.Files.Any(f => f.NameLower == "wss3_from_xml.ytyp.xml"),
                        res.Message);

            var dep = new ManifestWriter_U30.Dep { Ymap = "u30_int_placement", Interior = true };
            dep.Ytyps.Add("u30_int");
            var mfDir = Path.Combine(root, "u30mf");
            Directory.CreateDirectory(mfDir);
            var mfXmlPath = Path.Combine(mfDir, "_manifest.ymf.xml");
            File.WriteAllText(mfXmlPath, ManifestWriter_U30.BuildXml(new[] { dep }), new UTF8Encoding(false));
            RpfEdit.NewFolder(true, ex, target, "mf");
            var mfRpfDir = rpf.Root.Directories.First(d => d.NameLower == "mf");
            res = RpfEdit.ImportFiles(true, ex, new RpfEdit.Target(mfRpfDir, null), new[] { mfXmlPath }, false);
            var mfEntry = mfRpfDir.Files.FirstOrDefault(f => f.NameLower == "_manifest.ymf");
            var mfData = mfEntry != null ? ArchiveBrowser.Extract(mfEntry) : null;
            fails.Check("XML import: a _manifest.ymf.xml goes into the RPF as a real _manifest.ymf",
                        res.Ok && mfData != null && mfData.Length > 4 && mfData[0] == (byte)'P' && mfData[1] == (byte)'S',
                        res.Message + " | " + string.Join(",", mfRpfDir.Files.Select(f => f.Name)));
            var mfBack = mfData != null ? ManifestWriter_U30.Read(mfData) : null;
            fails.Check("XML import: ...that keeps INTERIOR_DATA and its ytyp",
                        mfBack != null && ManifestWriter_U30.InteriorFlags(mfBack).FirstOrDefault() &&
                        (mfBack.imapDependencies2?.FirstOrDefault()?.itypDepArray?.Any(h => h.Hash == JenkHash.GenHash("u30_int")) ?? false),
                        mfBack?.imapDependencies2?.FirstOrDefault()?.ToString() ?? "unreadable");
            var mfBinPath = Path.Combine(mfDir, "_manifest.ymf");
            File.WriteAllBytes(mfBinPath, ManifestWriter_U30.Build(new[] { dep }, out _));
            RpfEdit.NewFolder(true, ex, target, "mf2");
            var mf2 = rpf.Root.Directories.First(d => d.NameLower == "mf2");
            res = RpfEdit.ImportFiles(true, ex, new RpfEdit.Target(mf2, null), new[] { mfBinPath }, false);
            fails.Check("XML import: a binary _manifest.ymf goes in as it is",
                        res.Ok && mf2.Files.Any(f => f.NameLower == "_manifest.ymf"), res.Message);

            var reopened = new RpfFile(arcPath, "wss3_xml_test.rpf");
            reopened.ScanStructure(null, null);
            var rdir = reopened.Root?.Directories?.FirstOrDefault(d => d.NameLower == "xml");
            var rmade = rdir?.Files?.FirstOrDefault(f => f.NameLower == "wss3_from_xml.ytyp");
            Archetype reread = null;
            try
            {
                if (rmade != null)
                {
                    var ytyp = new YtypFile();
                    ytyp.Load(ArchiveBrowser.ExtractForDisk(rmade));
                    reread = ytyp.AllArchetypes?.FirstOrDefault();
                }
            }
            catch { }
            fails.Check("XML import: re-opened from disk, the converted .ytyp still parses",
                        reread != null && reread.Hash == JenkHash.GenHash("wss3_xml_probe"),
                        rmade != null ? rmade.GetFileSize() + " bytes" : "missing");

            return fails.Count;
        }
    }
}

