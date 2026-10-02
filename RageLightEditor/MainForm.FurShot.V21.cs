using System;
using System.Linq;
using CodeWalker.GameFiles;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private int furShotStage_V21;
        private int furShotSettle_V21;

        partial void OnWorldTick_FurShot_V21()
        {
            FindFilesProbe_U22();
            YtypEntsProbe_U22();
            if (furShotStage_V21 >= 2) return;
            var name = Environment.GetEnvironmentVariable("RLE_FURSHOT");
            if (string.IsNullOrEmpty(name)) return;

            if (furShotStage_V21 == 1)
            {
                if (Environment.GetEnvironmentVariable("RLE_FURSHOT_FRAME") == "1") { furShotStage_V21 = 2; return; }
                if (scene == null || scene.Files.Count == 0) return;
                if (++furShotSettle_V21 < 10) return;
                furShotStage_V21 = 2;
                var preset = Environment.GetEnvironmentVariable("RLE_FURSHOT_PRESET");
                if (!string.IsNullOrWhiteSpace(preset))
                {
                    var done = new System.Collections.Generic.HashSet<CodeWalker.GameFiles.ShaderFX>();
                    foreach (var pm in scene.AllMeshes)
                    {
                        if (pm?.Shader == null || !done.Add(pm.Shader)) continue;
                        Editor.ShaderPresets.Apply(pm.Shader, preset.Trim());
                    }
                    foreach (var pm in scene.AllMeshes) if (pm?.Shader != null) modelRenderer.RefreshMaterial(pm);
                    Console.WriteLine($"FURSHOT switched {done.Count} material(s) to {preset.Trim()}; fur on {scene.AllMeshes.Count(x => x.IsFur)} mesh(es), preview textures on {scene.AllMeshes.Count(x => x.FurPreviewTextures_U21)}");
                }
                if (Environment.GetEnvironmentVariable("RLE_MATPROBE") == "1")
                    foreach (var pm in scene.AllMeshes)
                    {
                        if (pm?.Shader == null) continue;
                        var sh = pm.Shader;
                        var texs = new System.Collections.Generic.List<string>();
                        var vals = new System.Collections.Generic.List<string>();
                        var ps = sh.ParametersList?.Parameters; var hs = sh.ParametersList?.Hashes;
                        for (int i = 0; ps != null && hs != null && i < ps.Length && i < hs.Length; i++)
                        {
                            if (ps[i].Data is CodeWalker.GameFiles.TextureBase tb) texs.Add(((CodeWalker.GameFiles.ShaderParamNames)(uint)hs[i]) + "=" + tb.Name);
                            else if (ps[i].Data is SharpDX.Vector4 v) vals.Add(((CodeWalker.GameFiles.ShaderParamNames)(uint)hs[i]) + "=" + v.X.ToString("0.###") + "," + v.Y.ToString("0.###") + "," + v.Z.ToString("0.###") + "," + v.W.ToString("0.###"));
                        }
                        Console.WriteLine($"MATPROBE {sh.Name} ({sh.FileName} #{sh.FileName.Hash}) bucket {sh.RenderBucket} -> {pm.AlphaMode} never={pm.NeverDraw} visible={pm.Visible} decal={pm.DecalKind} diffuse='{pm.DiffuseName}' srv={(pm.DiffuseSRV != null)} idx={pm.IndexCount} tex[{string.Join(" ", texs)}] val[{string.Join(" ", vals)}]");
                    }
                var target = new SharpDX.Vector3(0, 0, 0.3f);
                foreach (var fm in scene.AllMeshes)
                {
                    if (fm == null || !fm.IsFur) continue;
                    target = fm.WorldSphere.Center + new SharpDX.Vector3(0, 0, 0.3f);
                    break;
                }
                float dist = 6.0f;
                var distEnv = Environment.GetEnvironmentVariable("RLE_FURSHOT_DIST");
                if (!string.IsNullOrWhiteSpace(distEnv) && float.TryParse(distEnv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fd) && fd > 0.0f) dist = fd;
                camera.Target = target;
                camera.Distance = dist;
                camera.Pitch = 0.22f;
                camera.Yaw = 0.8f;
                if (float.TryParse(Environment.GetEnvironmentVariable("RLE_FURSHOT_YAW"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fy)) camera.Yaw = fy;
                if (float.TryParse(Environment.GetEnvironmentVariable("RLE_FURSHOT_PITCH"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fp)) camera.Pitch = fp;
                camera.SnapSmoothing();
                camera.Update();
                screenshotFrames = Math.Max(screenshotFrames, 12);
                Console.WriteLine($"FURSHOT camera moved onto the lawn ({dist:0.#} m from {target}, grazing)");
                return;
            }
            var c = gameFiles?.Cache;
            if (c?.YdrDict == null || !gameFiles.Ready) return;

            uint h = JenkHash.GenHash(System.IO.Path.GetFileNameWithoutExtension(name).ToLowerInvariant());
            CodeWalker.GameFiles.RpfFileEntry fe = null;
            if (c.YdrDict.TryGetValue(h, out var feYdr) && feYdr != null) fe = feYdr;
            else if (c.YftDict != null && c.YftDict.TryGetValue(h, out var feYft) && feYft != null) fe = feYft;
            if (fe == null)
            {
                furShotStage_V21 = 2;
                Console.WriteLine($"FURSHOT no .ydr called '{name}' in the archives");
                return;
            }

            if (!gameFiles.TextureIndexReady) return;

            furShotStage_V21 = 1;
            panel.RequestOpenArchiveFile = fe;
            screenshotFrames = Math.Max(screenshotFrames, 150);
            Console.WriteLine($"FURSHOT opening {fe.Name} from {fe.Path}");
        }

        partial void OnCapture_FurShot_V21()
        {
            if (Environment.GetEnvironmentVariable("RLE_FURDBG") != "1") return;
            Console.WriteLine($"FURDBG at capture: {sceneRenderer.FurShellsDrawn_V21} shell draw(s) over {sceneRenderer.FurMeshesDrawn_V21} fur mesh pass(es), {sceneRenderer.FurFinsDrawn_V21} fin pass(es) this run");
            Console.WriteLine($"FURDBG at capture: camera pos {camera.Position} target {camera.Target} dist {camera.Distance:0.0}");
        }
    }
}

