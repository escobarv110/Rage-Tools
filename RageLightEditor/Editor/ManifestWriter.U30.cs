using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public static class ManifestWriter_U30
    {
        public sealed class Dep
        {
            public string Ymap;
            public bool Interior;
            public readonly List<string> Ytyps = new List<string>();
        }

        private static string Esc(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        public static string BuildXml(IEnumerable<Dep> deps)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<CPackFileMetaData>");
            sb.AppendLine(" <MapDataGroups itemType=\"CMapDataGroup\" />");
            sb.AppendLine(" <HDTxdBindingArray itemType=\"CHDTxdAssetBinding\" />");
            sb.AppendLine(" <imapDependencies itemType=\"CImapDependency\" />");
            sb.AppendLine(" <imapDependencies_2 itemType=\"CImapDependencies\">");
            foreach (var d in deps)
            {
                if (string.IsNullOrWhiteSpace(d?.Ymap)) continue;
                sb.AppendLine("  <Item>");
                sb.AppendLine($"   <imapName>{Esc(d.Ymap.Trim().ToLowerInvariant())}</imapName>");
                sb.AppendLine(d.Interior ? "   <manifestFlags>INTERIOR_DATA</manifestFlags>" : "   <manifestFlags />");
                var ytyps = d.Ytyps.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList();
                if (ytyps.Count == 0) sb.AppendLine("   <itypDepArray />");
                else
                {
                    sb.AppendLine("   <itypDepArray>");
                    foreach (var t in ytyps) sb.AppendLine($"    <Item>{Esc(t)}</Item>");
                    sb.AppendLine("   </itypDepArray>");
                }
                sb.AppendLine("  </Item>");
            }
            sb.AppendLine(" </imapDependencies_2>");
            sb.AppendLine(" <itypDependencies_2 itemType=\"CItypDependencies\" />");
            sb.AppendLine(" <Interiors itemType=\"CInteriorBoundsFiles\" />");
            sb.AppendLine("</CPackFileMetaData>");
            return sb.ToString();
        }

        public static byte[] ToBinary(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var data = XmlMeta.GetData(doc, MetaFormat.PSO, "");
            if (data == null || data.Length == 0) throw new InvalidOperationException("the manifest XML did not convert to a .ymf");
            return data;
        }

        public static YmfFile Read(byte[] data)
        {
            var ymf = new YmfFile();
            ymf.Load(data, new RpfBinaryFileEntry { Name = "_manifest.ymf", NameLower = "_manifest.ymf" });
            return ymf;
        }

        public static string Verify(byte[] data, IEnumerable<Dep> deps)
        {
            YmfFile ymf;
            try { ymf = Read(data); }
            catch (Exception ex) { return "the written .ymf does not read back: " + ex.Message; }
            if (ymf.Pso == null) return "the written .ymf is not a PSO file";
            var got = ymf.imapDependencies2 ?? Array.Empty<YmfImapDependency2>();
            var want = deps.Where(d => !string.IsNullOrWhiteSpace(d?.Ymap)).ToList();
            if (got.Length != want.Count) return $"the .ymf lists {got.Length} ymap(s), expected {want.Count}";
            var flags = InteriorFlags(ymf);
            if (flags.Count != want.Count) return "the .ymf's dependency list does not read back as XML";
            for (int i = 0; i < want.Count; i++)
            {
                var g = got[i]; var w = want[i];
                if (g.Dep.imapName.Hash != JenkHash.GenHash(w.Ymap.Trim().ToLowerInvariant()))
                    return $"ymap {i} came back as {g.Dep.imapName}, expected {w.Ymap}";
                bool interior = flags[i];
                if (interior != w.Interior) return $"{w.Ymap}: INTERIOR_DATA came back {(interior ? "on" : "off")}";
                var wantTypes = w.Ytyps.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => JenkHash.GenHash(t.Trim().ToLowerInvariant())).Distinct().ToList();
                var gotTypes = (g.itypDepArray ?? Array.Empty<MetaHash>()).Select(h => h.Hash).ToList();
                if (!wantTypes.All(gotTypes.Contains) || gotTypes.Count != wantTypes.Count)
                    return $"{w.Ymap}: its ytyp list came back wrong ({gotTypes.Count} of {wantTypes.Count})";
            }
            return null;
        }

        public static List<bool> InteriorFlags(YmfFile ymf)
        {
            var list = new List<bool>();
            var xml = MetaXml.GetXml(ymf, out _);
            if (string.IsNullOrEmpty(xml)) return list;
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var items = doc.SelectNodes("/CPackFileMetaData/imapDependencies_2/Item");
            if (items == null) return list;
            foreach (XmlNode item in items)
                list.Add((item.SelectSingleNode("manifestFlags")?.InnerText ?? "").Contains("INTERIOR_DATA"));
            return list;
        }

        public static byte[] Build(IEnumerable<Dep> deps, out string error)
        {
            var list = deps.ToList();
            error = null;
            byte[] data;
            try { data = ToBinary(BuildXml(list)); }
            catch (Exception ex) { error = ex.Message; return null; }
            error = Verify(data, list);
            return error == null ? data : null;
        }
    }
}
