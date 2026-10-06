using System;
using System.Collections.Generic;
using System.IO;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public static partial class RpfEdit
    {
        private static readonly List<string> xmlNotes_S3 = new List<string>();

        public static void XmlImportBegin_S3() => xmlNotes_S3.Clear();

        public static string XmlImportNote_S3() =>
            xmlNotes_S3.Count == 0 ? "" : " - " + string.Join("; ", xmlNotes_S3);

        public static byte[] ReadForImport_S3(string path, bool raw, ref string name,
                                              ref int converted, out string why)
        {
            why = null;
            var lower = (name ?? "").ToLowerInvariant();

            if (raw || !lower.EndsWith(".xml", StringComparison.Ordinal))
            {
                try { return File.ReadAllBytes(path); }
                catch (Exception ex) { why = ex.Message; return null; }
            }

            if (lower.IndexOf('.') == lower.LastIndexOf('.'))
            {
                Note_S3($"{name} went in as a plain XML file");
                try { return File.ReadAllBytes(path); }
                catch (Exception ex) { why = ex.Message; return null; }
            }

            if (lower.EndsWith(".ymf.xml", StringComparison.Ordinal))
            {
                try
                {
                    var ymf = ManifestWriter_U30.ToBinary(File.ReadAllText(path));
                    var was0 = name;
                    name = name.Substring(0, name.Length - 4);
                    converted++;
                    Note_S3($"{was0} converted back to {name} ({ymf.Length:N0} bytes manifest)");
                    return ymf;
                }
                catch (Exception ex) { why = "the manifest XML could not be converted: " + ex.Message; return null; }
            }

            var mformat = XmlMeta.GetXMLFormat(lower, out _);
            if (mformat == MetaFormat.XML)
            {
                Note_S3($"{name} went in as a plain XML file (no .{SecondExtension_S3(lower)} converter)");
                try { return File.ReadAllBytes(path); }
                catch (Exception ex) { why = ex.Message; return null; }
            }

            var was = name;
            byte[] data;
            try
            {
                if (!TryConvertXml(path, ref name, out data) || data == null || data.Length == 0)
                {
                    name = was;
                    why = $"this is {XmlMeta.GetXMLFormatName(mformat)} that the converter would not " +
                          "take - check it against the XML this explorer shows for a working file of " +
                          "the same type, or use Import raw to put the XML in as it is";
                    return null;
                }
            }
            catch (Exception ex)
            {
                name = was;
                why = $"the {XmlMeta.GetXMLFormatName(mformat)} could not be converted: {ex.Message}";
                return null;
            }

            converted++;
            Note_S3($"{was} converted back to {name} ({data.Length / 1024:N0} KB {XmlMeta.GetXMLFormatName(mformat)})");
            return data;
        }

        private static string SecondExtension_S3(string lower)
        {
            var stem = lower.Substring(0, lower.Length - 4);
            int dot = stem.LastIndexOf('.');
            return dot >= 0 && dot < stem.Length - 1 ? stem.Substring(dot + 1) : "?";
        }

        private static void Note_S3(string text)
        {
            if (xmlNotes_S3.Count < 3) xmlNotes_S3.Add(text);
            else if (xmlNotes_S3.Count == 3) xmlNotes_S3.Add("and more XML files");
        }
    }
}

