using System;
using System.Linq;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private Scene[] sectionScenes_S1;

        private static bool SectionOwnsScene_S1(LightPanel.Space sp) =>
            sp == LightPanel.Space.Terrain || sp == LightPanel.Space.Particles ||
            sp == LightPanel.Space.NavMesh || sp == LightPanel.Space.Archive ||
            sp == LightPanel.Space.Animation;

        private Scene SectionScene_S1(LightPanel.Space sp)
        {
            if (!SectionOwnsScene_S1(sp) || modelRenderer == null || lightScene == null) return null;
            sectionScenes_S1 ??= new Scene[32];
            int i = (int)sp;
            if (i < 0 || i >= sectionScenes_S1.Length) return null;
            if (sectionScenes_S1[i] == null)
            {
                var wasImported = modelRenderer.ImportedTextures;
                sectionScenes_S1[i] = new Scene(modelRenderer, null);
                modelRenderer.ImportedTextures = wasImported;
                Console.WriteLine($"SECTIONSCENE created the {sp} workspace's own scene");
            }
            return sectionScenes_S1[i];
        }

        private System.Collections.Generic.IEnumerable<Scene> SectionScenes_S1 =>
            sectionScenes_S1?.Where(s => s != null) ?? Enumerable.Empty<Scene>();

        private System.Collections.Generic.IEnumerable<Scene> AllScenes_S1()
        {
            if (lightScene != null) yield return lightScene;
            if (mloScene != null) yield return mloScene;
            foreach (var s in SectionScenes_S1) yield return s;
        }

        private void DisposeSectionScenes_S1()
        {
            if (sectionScenes_S1 == null) return;
            foreach (var s in sectionScenes_S1) { try { s?.Dispose(); } catch { } }
        }

        private void SeqTest_S1(Action<string, bool, string> check)
        {
            SectionSceneTest_S1(check);
            MloClickSelectTest_S1(check);
            MloRotSnapTest_S1(check);
        }

        private void SectionSceneTest_S1(Action<string, bool, string> check)
        {
            try
            {
                var owners = new[] { LightPanel.Space.Terrain, LightPanel.Space.Particles, LightPanel.Space.NavMesh, LightPanel.Space.Archive };
                foreach (var sp in owners)
                {
                    var sc = SceneFor_L3(sp);
                    check($"section scenes: {sp} has its own scene",
                          sc != null && !ReferenceEquals(sc, lightScene) && !ReferenceEquals(sc, mloScene),
                          $"{sp} -> {(ReferenceEquals(sc, lightScene) ? "lightScene" : ReferenceEquals(sc, mloScene) ? "mloScene" : "own")}");
                }
                var all = new[] { LightPanel.Space.Light, LightPanel.Space.Mlo,
                                  LightPanel.Space.Terrain, LightPanel.Space.Particles, LightPanel.Space.NavMesh, LightPanel.Space.Archive };
                var seen = all.Select(SceneFor_L3).ToList();
                check("section scenes: Materials shares the Lights scene", ReferenceEquals(SceneFor_L3(LightPanel.Space.Material), lightScene), "");
                check("section scenes: the six owning sections have six distinct scenes",
                      seen.Distinct().Count() == all.Length, string.Join(", ", all.Zip(seen, (a, b) => $"{a}={b?.GetHashCode()}")));
                check("section scenes: Cinematic films the Lights scene on purpose",
                      ReferenceEquals(SceneFor_L3(LightPanel.Space.Cinematic), lightScene), "");

                string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rle_mlocreator");
                System.IO.Directory.CreateDirectory(dir);
                string ydr = System.IO.Path.Combine(dir, "light_test_scene.ydr");
                if (!System.IO.File.Exists(ydr)) TestSceneGenerator.Run(ydr);
                string mine = System.IO.Path.Combine(dir, "prop_rle_sectioniso.ydr");
                System.IO.File.Copy(ydr, mine, true);
                var before = all.ToDictionary(sp => sp, sp => SceneFor_L3(sp).Files.Count);

                panel.SwitchWorkspace(LightPanel.Space.Terrain);
                BindActiveScene_L3(SceneFor_L3(LightPanel.Space.Terrain));
                check("section scenes: the Terrain workspace draws its own scene", ReferenceEquals(scene, SceneFor_L3(LightPanel.Space.Terrain)) && ReferenceEquals(panel.ActiveScene, scene), "");
                LoadFile(mine);
                var terr = SceneFor_L3(LightPanel.Space.Terrain);
                check("section scenes: the load landed in the Terrain scene", terr.Files.Any(f => f.Path == mine), $"{terr.Files.Count} files");
                var leaked = all.Where(sp => sp != LightPanel.Space.Terrain && SceneFor_L3(sp).Files.Any(f => f.Path == mine)).ToList();
                check("section scenes: no other section has it", leaked.Count == 0, leaked.Count == 0 ? "" : "leaked into " + string.Join(", ", leaked));
                var moved = all.Where(sp => sp != LightPanel.Space.Terrain && SceneFor_L3(sp).Files.Count != before[sp]).ToList();
                check("section scenes: no other section's prop count moved", moved.Count == 0, moved.Count == 0 ? "" : string.Join(", ", moved.Select(sp => $"{sp} {before[sp]}->{SceneFor_L3(sp).Files.Count}")));

                panel.SwitchWorkspace(LightPanel.Space.Light);
                BindActiveScene_L3(lightScene);
                check("section scenes: back in Lights the light scene draws", ReferenceEquals(scene, lightScene), "");
                check("section scenes: Lights never saw the Terrain prop", !lightScene.Files.Any(f => f.Path == mine), $"{lightScene.Files.Count} files");

                var f2 = terr.Files.FirstOrDefault(f => f.Path == mine);
                if (f2 != null) terr.RemoveFile(f2);
            }
            catch (Exception ex)
            {
                check("section scenes: no exception", false, ex.ToString());
            }
        }
    }
}

