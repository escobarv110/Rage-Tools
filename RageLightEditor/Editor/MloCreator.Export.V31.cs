using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public partial class MloCreatorSession
    {
        public sealed class ExportResult_V31
        {
            public readonly List<string> Written = new List<string>();
            public readonly List<string> Missing = new List<string>();
            public string Folder = "";
            public string Error;
            public string ErrorDetail_U21;
            public bool Ok => Error == null;
        }

        public ExportResult_V31 ExportForGame_V31(string folder, string ytypStem = null, string ymapStem = null)
        {
            var r = new ExportResult_V31 { Folder = folder };
            try
            {
                if (string.IsNullOrWhiteSpace(folder)) { r.Error = "no folder"; return r; }
                Directory.CreateDirectory(folder);

                var name = (Name ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(name)) { r.Error = "the interior needs a name"; return r; }
                ytypStem = string.IsNullOrWhiteSpace(ytypStem) ? name : ytypStem.Trim().ToLowerInvariant();
                ymapStem = string.IsNullOrWhiteSpace(ymapStem)
                    ? (string.IsNullOrWhiteSpace(YmapName) ? name + "_placement" : YmapName.Trim().ToLowerInvariant())
                    : ymapStem.Trim().ToLowerInvariant();

                var ytypPath = Path.Combine(folder, ytypStem + ".ytyp");
                SaveYtyp(ytypPath);
                r.Written.Add(Path.GetFileName(ytypPath));

                var ymapPath = Path.Combine(folder, ymapStem + ".ymap");
                var rot = SharpDX.Quaternion.RotationAxis(SharpDX.Vector3.UnitZ,
                              SharpDX.MathUtil.DegreesToRadians(YmapHeadingDeg));
                SaveYmap(ymapPath, new SharpDX.Vector3(YmapPosition.X, YmapPosition.Y, YmapPosition.Z), rot,
                         (uint)Math.Max(YmapGroupId, 0), (uint)Math.Max(YmapFloorId, 0), YmapDefaultSets);
                r.Written.Add(Path.GetFileName(ymapPath));

                var manifestPath = Path.Combine(folder, "_manifest.ymf");
                var manifest = ManifestWriter_U30.Build(ManifestDeps_U30(ymapStem, ytypStem), out var manifestError);
                if (manifest == null) { r.Error = "the _manifest.ymf could not be built: " + manifestError; return r; }
                File.WriteAllBytes(manifestPath, manifest);
                r.Written.Add(Path.GetFileName(manifestPath));

                var shellName = string.IsNullOrWhiteSpace(ShellName) ? name : ShellName.Trim().ToLowerInvariant();
                if (!File.Exists(Path.Combine(folder, name + ".ydr")))
                    r.Missing.Add(name + ".ydr  - the shell model, named after the interior " +
                                  (shellName != name ? "(yours is called " + shellName + ".ydr - RENAME it)" : "(export it from your DCC)"));
                if (!string.IsNullOrWhiteSpace(PhysicsDictionary) && !File.Exists(Path.Combine(folder, PhysicsDictionary + ".ybn")))
                    r.Missing.Add(PhysicsDictionary + ".ybn  - the collision the .ytyp names, or you fall through the floor");
                foreach (var y in CollisionFiles_V31())
                {
                    var dest = Path.Combine(folder, Path.GetFileName(y));
                    if (File.Exists(dest)) continue;
                    try { File.Copy(y, dest); r.Written.Add(Path.GetFileName(dest) + "  (copied from beside your model)"); }
                    catch { }
                }
                return r;
            }
            catch (Exception ex) { r.Error = ex.Message; r.ErrorDetail_U21 = ex.ToString(); return r; }
        }

        public static ManifestWriter_U30.Dep[] ManifestDeps_U30(string ymapStem, string ytypStem)
        {
            var d = new ManifestWriter_U30.Dep { Ymap = ymapStem, Interior = true };
            d.Ytyps.Add(ytypStem);
            return new[] { d };
        }
    }
}

