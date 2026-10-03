using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool isoTestDone_S1;

        private static readonly LightPanel.Space[] IsoSpaces_S1 =
        {
            LightPanel.Space.World, LightPanel.Space.Light, LightPanel.Space.Material, LightPanel.Space.Mlo,
            LightPanel.Space.Archive, LightPanel.Space.Particles, LightPanel.Space.NavMesh,
            LightPanel.Space.Terrain, LightPanel.Space.Animation, LightPanel.Space.Cinematic,
        };

        private void ServiceIsoTest_S1()
        {
            if (isoTestDone_S1) return;
            string spec = Environment.GetEnvironmentVariable("RLE_ISOTEST");
            if (string.IsNullOrEmpty(spec)) return;
            if (gameFiles != null && gameFiles.Initialising) return;
            if (DebugMlo != null && !debugMloDone) return;
            isoTestDone_S1 = true;
            try { RunIsoTest_S1(spec); }
            catch (Exception ex) { Console.WriteLine("ISOTEST threw: " + ex); }
        }

        private void RunIsoTest_S1(string spec)
        {
            var parts = spec.Split(',');
            string path = parts[0].Trim();
            var into = LightPanel.Space.Mlo;
            if (parts.Length > 1 && Enum.TryParse(parts[1].Trim(), true, out LightPanel.Space s2)) into = s2;
            if (!File.Exists(path)) { Console.WriteLine("ISOTEST: no such file " + path); return; }

            Console.WriteLine($"ISOTEST importing {Path.GetFileName(path)} into the {into} workspace ONLY");
            panel.SwitchWorkspace(into);
            BindActiveScene_L3(SceneFor_L3(into));
            ImportYtyp(path);

            var mine = new HashSet<RenderMesh>();
            var sc = SceneFor_L3(into);
            foreach (var e in sc.Imports_R1.Entries)
                if (e.Model?.Meshes != null) foreach (var m in e.Model.Meshes) mine.Add(m);
            foreach (var f in sc.Files) if (f.FromMlo && f.Model != null) foreach (var m in f.Model.Meshes) mine.Add(m);
            var mineFiles = new HashSet<LoadedFile>(sc.Files.Where(f => f.FromMlo));
            Console.WriteLine($"ISOTEST the import brought {mine.Count} meshes and {mineFiles.Count} prop files into the {into} scene");

            int leaks = 0;
            foreach (var sp in IsoSpaces_S1)
            {
                panel.SwitchWorkspace(sp);
                BindActiveScene_L3(SceneFor_L3(sp));
                var line = IsoCount_S1(sp, mine, mineFiles, out int mineHere);
                Console.WriteLine("ISOTEST " + line);
                bool shares = sp == LightPanel.Space.Cinematic && into == LightPanel.Space.Light;
                if (sp != into && mineHere > 0 && !shares) { leaks++; Console.WriteLine($"ISOTEST   *** LEAK: {mineHere} of the import's meshes are drawn in {sp}"); }
                else if (shares && mineHere > 0) Console.WriteLine("ISOTEST   (Cinematic films the Lights scene on purpose - not a leak)");
            }
            Console.WriteLine($"ISOTEST RESULT: {(leaks == 0 ? "isolated - the import draws in " + into + " and nowhere else" : leaks + " workspace(s) leak the import")}");
            panel.SwitchWorkspace(into);
            BindActiveScene_L3(SceneFor_L3(into));
        }

        private string IsoCount_S1(LightPanel.Space sp, HashSet<RenderMesh> mine, HashSet<LoadedFile> mineFiles, out int mineHere)
        {
            var sc = CurrentScene;
            string which = ReferenceEquals(sc, lightScene) ? "lightScene" : ReferenceEquals(sc, mloScene) ? "mloScene" : "own";
            var draw = IsoDrawList_S1(sp).ToList();
            int meshes = draw.Sum(m => m?.Meshes?.Count ?? 0);
            mineHere = draw.Sum(m => m?.Meshes?.Count(x => mine.Contains(x)) ?? 0);
            int props = sc.Files.Count, fromMlo = sc.Files.Count(f => f.FromMlo), mineProps = sc.Files.Count(f => mineFiles.Contains(f));
            int lights = sc.Lights.Count;
            int shell = sc.MloModel?.Meshes.Count ?? 0;
            int ytyps = sc.MloInfo?.Ytyps?.Count ?? 0, ents = sc.MloInfo?.Entities?.Count ?? 0;
            int imports = sc.Imports_R1?.Count ?? 0;
            int creator = Creator?.Session?.Entities.Count ?? 0;
            int panelArch = panel.Archetypes.Count, panelYmap = panel.YmapEntries.Count;
            return $"{sp,-10} scene={which,-10} draws {draw.Count} models / {meshes} meshes (of the import: {mineHere}) | " +
                   $"props {props} ({fromMlo} from MLO, {mineProps} the import's), lights {lights}, MloModel {shell} meshes, " +
                   $"MloInfo {ytyps} ytyps / {ents} entities, imports {imports} | creator session {creator} entities, " +
                   $"panel lists {panelArch} archetypes / {panelYmap} ymap entries";
        }

        private IEnumerable<RenderModel> IsoDrawList_S1(LightPanel.Space sp)
        {
            var sc = CurrentScene;
            IEnumerable<RenderModel> toDraw;
            if (RpfExplorerOnly_Q1) return Enumerable.Empty<RenderModel>();
            if (sp == LightPanel.Space.World)
                toDraw = worldRender?.Model != null ? new[] { worldRender.Model } : Enumerable.Empty<RenderModel>();
            else if (sp == LightPanel.Space.Archive && assetPreview?.Model != null)
                toDraw = sc.Models.Concat(new[] { assetPreview.Model });
            else toDraw = sc.Models;
            var extra = WorldExtraModels_Selection() ?? new List<RenderModel>();
            AddTerrainModels_R4(extra);
            return toDraw.Concat(extra).Where(m => m != null);
        }
    }
}

