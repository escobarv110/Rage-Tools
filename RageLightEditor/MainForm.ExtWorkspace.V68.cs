using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private MloVertexDots extDots_V68;
        private VertexSnapHit extSnap_V68;
        private string extLastDir_V68;

        private bool ExtActive_V68 => panel != null && panel.ExtensionMode;

        private WorldGizmo extGizmo_V69;
        private readonly List<IWorldGizmoTarget> extGizmoTargets_V69 = new List<IWorldGizmoTarget>();

        private IList<IWorldGizmoTarget> ExtGizmoTargets_V69()
        {
            extGizmoTargets_V69.Clear();
            if (!ExtActive_V68) return extGizmoTargets_V69;
            var t = panel.Ext.GizmoTarget_V69();
            if (t != null) extGizmoTargets_V69.Add(t);
            return extGizmoTargets_V69;
        }

        private WorldGizmo ExtGizmo_V69()
        {
            if (extGizmo_V69 == null)
            {
                extGizmo_V69 = new WorldGizmo { Mode = WorldGizmoMode.Translate };
                extGizmo_V69.TargetChanged = _ => ExtNoteEdited_V68();
            }
            extGizmo_V69.Mode = WorldGizmoMode.Translate;
            return extGizmo_V69;
        }

        partial void ExtGizmoMouseDown_V69(int x, int y, bool shift, bool alt, ref bool consumed)
        {
            if (!ExtActive_V68 || consumed) return;
            var targets = ExtGizmoTargets_V69();
            if (targets.Count == 0) return;
            var g = ExtGizmo_V69();
            if (g.HitsHandle_V33(camera, targets, x, y, deviceResources.Width, deviceResources.Height))
                panel.Ext.PushUndo_V70(shift ? "Duplicate and move" : "Move with the gizmo");
            if (shift && g.HitsHandle_V33(camera, targets, x, y, deviceResources.Width, deviceResources.Height))
            {
                var ws = panel.Ext;
                ws.DuplicateExtension_V69(ws.CurrentArchetype, ws.SelectedExtension);
                ExtNoteEdited_V68();
                ExtNoteEdited_V68();
                targets = ExtGizmoTargets_V69();
            }
            consumed = g.MouseDown(camera, targets, x, y, deviceResources.Width, deviceResources.Height, shift, alt);
        }

        partial void ExtGizmoMouseMove_V69(int x, int y)
        {
            if (!ExtActive_V68 || extGizmo_V69 == null) return;
            var targets = ExtGizmoTargets_V69();
            if (!extGizmo_V69.Dragging && targets.Count == 0) return;
            extGizmo_V69.MouseMove(camera, x, y, deviceResources.Width, deviceResources.Height);
        }

        partial void ExtGizmoMouseUp_V69()
        {
            if (extGizmo_V69 == null || !extGizmo_V69.Dragging) return;
            extGizmo_V69.MouseUp();
            ExtNoteEdited_V68();
        }

        partial void DrawExtGizmo_V69(SharpDX.Direct3D11.DeviceContext context)
        {
            if (!ExtActive_V68) return;
            var targets = ExtGizmoTargets_V69();
            if (targets.Count == 0 && !(extGizmo_V69?.Dragging ?? false)) return;
            var g = ExtGizmo_V69();
            g.Draw(lineRenderer, triRenderer, camera, targets, GizmoStyle.OccludedAlpha);
            lineRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.DepthDisabled);
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthDisabled);
            g.Draw(lineRenderer, triRenderer, camera, targets, 1.0f);
            lineRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.DepthDisabled);
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthDisabled);
        }

        partial void ExtClickHandle_V69(int x, int y, ref bool handled)
        {
            if (!ExtActive_V68 || handled) return;
            handled = ExtPickHandle_V69(x, y);
        }

        private bool ExtPickHandle_V69(int x, int y)
        {
            var ws = panel.Ext;
            var t = ws.Current;
            var arch = t?.Archetype;
            if (arch == null) return false;
            var list = ArchetypeExtensions_V62.Get(arch);
            if (ws.SelectedExtension < 0 || ws.SelectedExtension >= list.Length) return false;
            var w = list[ws.SelectedExtension];
            var fields = ExtensionWorkspace_V68.MovableFields_V69(w);
            if (fields.Count == 0) return false;

            var vp = camera.ViewProjMatrix;
            float vw = deviceResources.Width, vh = deviceResources.Height;
            string best = null;
            float bestD = 18.0f;
            foreach (var f in fields)
            {
                if (!(ArchetypeExtensions_V62.GetValue(w, f) is Vector3 local)) continue;
                var world = t.Placement + Vector3.Transform(local, t.Orientation);
                var clip = Vector4.Transform(new Vector4(world, 1.0f), vp);
                if (clip.W <= 0.0001f) continue;
                float sx = (clip.X / clip.W * 0.5f + 0.5f) * vw;
                float sy = (0.5f - clip.Y / clip.W * 0.5f) * vh;
                float d = (float)Math.Sqrt((sx - x) * (sx - x) + (sy - y) * (sy - y));
                if (d < bestD) { bestD = d; best = f.Prop.Name; }
            }
            if (best == null)
            {
                if (ws.SelectedPointField == null) return false;
                ws.SelectedPointField = null;
                ws.Say("the gizmo moves the whole extension again");
                return true;
            }
            ws.SelectedPointField = best;
            ws.Say(ArchetypeExtensions_V62.Spaced(best) + " picked - drag it, or click away for the whole extension");
            return true;
        }

        private string extFxPlaying_V73;
        private int extFxSelection_V73 = -1;

        private void ExtServiceParticles_V73()
        {
            if (panel == null) return;
            if (!ExtActive_V68)
            {
                if (extFxPlaying_V73 != null)
                {
                    extFxPlaying_V73 = null;
                    extFxSelection_V73 = -1;
                    ReleaseParticles_U3();
                }
                return;
            }

            RequestParticles_U3(Editor.LightPanel.Space.Extension);

            var ws = panel.Ext;
            var t = ws.Current;
            var arch = t?.Archetype;
            string want = null;
            Vector3 at = Vector3.Zero;
            if (arch != null && panel.ExtPlayEffects_V73)
            {
                var list = ArchetypeExtensions_V62.Get(arch);
                if (ws.SelectedExtension >= 0 && ws.SelectedExtension < list.Length &&
                    list[ws.SelectedExtension] is MCExtensionDefParticleEffect pe)
                {
                    want = pe.fxName;
                    at = t.Placement + Vector3.Transform(pe.Data.offsetPosition, t.Orientation);
                }
            }

            if (string.IsNullOrWhiteSpace(want))
            {
                if (extFxPlaying_V73 != null)
                {
                    extFxPlaying_V73 = null;
                    extFxSelection_V73 = -1;
                    Ptfx?.Sim.SetEffect(null);
                }
                return;
            }

            if (Ptfx != null) Ptfx.Sim.Origin = at;

            bool changed = !string.Equals(want, extFxPlaying_V73, StringComparison.OrdinalIgnoreCase) ||
                           extFxSelection_V73 != ws.SelectedExtension;
            if (!changed) return;
            extFxPlaying_V73 = want;
            extFxSelection_V73 = ws.SelectedExtension;
            if (gameFiles == null || !gameFiles.Ready) return;

            EnsureParticles_N4();
            PlayEffectByName_N4(want);
            if (Ptfx != null)
            {
                Ptfx.Sim.Origin = at;
                WarmParticles_N4(0.8f);
                ws.Say(Ptfx.Sim.Effect != null
                    ? "playing " + want
                    : want + ": that effect is not in the game's .ypt files", Ptfx.Sim.Effect == null);
            }
        }

        private bool extSpaceApplied_V68;
        private int extProbeFrames_V68 = -1;
        private int extProbeSelect_V69 = -1;
        private bool extSampleWanted_V70, extSampleDone_V70;
        private bool extMarkersForced_V71;
        private bool extYptScanDone_V73;
        private static readonly string extSampleFilter_V72 = Environment.GetEnvironmentVariable("RLE_EXTTIME");
        private int extSampleHits_V72;

        private void ExtYptScan_V73()
        {
            if (screenshotPath != null) screenshotFrames = -1;
            var list = Editor.PtfxGameIndex.ListYpts(gameFiles);
            Console.WriteLine($"YPTSCAN {list.Count} .ypt files in the install");
            var want = Environment.GetEnvironmentVariable("RLE_YPTFIND");
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int scanned = 0, effects = 0;
            foreach (var e in list)
            {
                try
                {
                    var doc = Editor.PtfxDocument.FromGame(gameFiles, e.Name, e.Path);
                    if (doc?.Effects == null) continue;
                    scanned++;
                    effects += doc.Effects.Count;
                    foreach (var fx in doc.Effects)
                    {
                        if (fx.Name == null) continue;
                        have.Add(fx.Name);
                        if (!string.IsNullOrWhiteSpace(want) &&
                            fx.Name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                            Console.WriteLine($"YPTFIND {fx.Name} is in {e.Name} ({e.Path})");
                    }
                }
                catch { }
            }
            Console.WriteLine($"YPTSCAN read {scanned} assets, {effects} effects, {have.Count} distinct names");

            int used = 0, hit = 0;
            var misses = new List<string>();
            var hits = new List<string>();
            foreach (var ytyp in gameFiles.Cache.YtypDict.Values)
            {
                if (ytyp?.AllArchetypes == null) continue;
                foreach (var a2 in ytyp.AllArchetypes)
                    foreach (var w in ArchetypeExtensions_V62.Get(a2))
                        if (w is MCExtensionDefParticleEffect pe && !string.IsNullOrEmpty(pe.fxName))
                        {
                            used++;
                            if (have.Contains(pe.fxName))
                            {
                                hit++;
                                if (hits.Count < 8 && !hits.Any(x => x.StartsWith(a2.Name + " ")))
                                    hits.Add(a2.Name + "  ->  " + pe.fxName);
                            }
                            else if (misses.Count < 8 && !misses.Contains(pe.fxName)) misses.Add(pe.fxName);
                        }
            }
            Console.WriteLine($"FXMATCH {hit} of {used} particle extensions name an effect that exists in a .ypt");
            if (misses.Count > 0) Console.WriteLine("FXMATCH misses: " + string.Join(", ", misses));
            foreach (var h in hits) Console.WriteLine("FXHIT " + h);
            if (screenshotPath != null) screenshotFrames = 6;
        }

        private void ExtSampleTypes_V70()
        {
            extSampleDone_V70 = true;
            var cache = gameFiles?.Cache;
            if (cache?.YtypDict == null) { Console.WriteLine("EXTSAMPLE no ytyps"); return; }
            var want = new HashSet<string>(ArchetypeExtensions_V62.Types.Select(t => t.Wrapper));
            var seen = new Dictionary<string, int>();
            foreach (var ytyp in cache.YtypDict.Values)
            {
                if (ytyp?.AllArchetypes == null) continue;
                foreach (var a in ytyp.AllArchetypes)
                {
                    foreach (var w in ArchetypeExtensions_V62.Get(a))
                    {
                        var wn = w.GetType().Name;
                        if (!want.Contains(wn)) continue;
                        if (extSampleFilter_V72 != null)
                        {
                            if (wn != "MCExtensionDefLightShaft") continue;
                            var fi = ArchetypeExtensions_V62.Fields(w);
                            float fis = Convert.ToSingle(ArchetypeExtensions_V62.GetValue(w, fi.First(x => x.Prop.Name == "fadeInTimeStart")));
                            float fie = Convert.ToSingle(ArchetypeExtensions_V62.GetValue(w, fi.First(x => x.Prop.Name == "fadeInTimeEnd")));
                            float fos = Convert.ToSingle(ArchetypeExtensions_V62.GetValue(w, fi.First(x => x.Prop.Name == "fadeOutTimeStart")));
                            float fds = Convert.ToSingle(ArchetypeExtensions_V62.GetValue(w, fi.First(x => x.Prop.Name == "fadeDistanceStart")));
                            float fde = Convert.ToSingle(ArchetypeExtensions_V62.GetValue(w, fi.First(x => x.Prop.Name == "fadeDistanceEnd")));
                            float inten = Convert.ToSingle(ArchetypeExtensions_V62.GetValue(w, fi.First(x => x.Prop.Name == "intensity")));
                            if (fis != 0 || fie != 0 || fos != 0 || fds != 0 || fde != 0)
                            {
                                extSampleHits_V72++;
                                if (extSampleHits_V72 <= 14)
                                    Console.WriteLine($"EXTTIME {a.Name}: fadeIn {fis}..{fie} fadeOut {fos}.. dist {fds}..{fde} intensity {inten}");
                            }
                            continue;
                        }
                        seen.TryGetValue(wn, out int n);
                        if (n >= 2) continue;
                        seen[wn] = n + 1;
                        Console.WriteLine($"EXTSAMPLE {wn} on {a.Name} ({ytyp.Name})");
                        foreach (var f in ArchetypeExtensions_V62.Fields(w))
                            Console.WriteLine($"EXTSAMPLE     {f.Prop.Name} = {ArchetypeExtensions_V62.GetValue(w, f)}");
                    }
                }
            }
            if (extSampleFilter_V72 != null) Console.WriteLine($"EXTTIME {extSampleHits_V72} shafts carry non-zero fades");
            Console.WriteLine($"EXTSAMPLE done: {seen.Count} of {want.Count} types found");
        }


        partial void OnWorldTick_Ext_V68()
        {
            if (panel == null) return;
            var ws = panel.Ext;

            if (!extSpaceApplied_V68 && Environment.GetEnvironmentVariable("RLE_EXTSPACE") == "1")
            {
                extSpaceApplied_V68 = true;
                panel.SwitchWorkspace(Editor.LightPanel.Space.Extension);
                var probe = Environment.GetEnvironmentVariable("RLE_EXTOPEN");
                if (!string.IsNullOrWhiteSpace(probe))
                {
                    foreach (var raw in probe.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var f = raw.Trim();
                        try
                        {
                            var ext = Path.GetExtension(f).ToLowerInvariant();
                            if (ext == ".ytyp") ws.AddYtyp(f);
                            else if (ext == ".ymap") ws.AddYmap(f, ExtLookupArchetype_V68);
                            else { scene.LoadModelFile(f, additive: false); FrameModel(); }
                        }
                        catch (Exception ex) { Console.WriteLine("EXTSPACE open failed: " + ex.Message); }
                    }
                    Console.WriteLine($"EXTSPACE sources {ws.Sources.Count} archetypes {ws.Targets.Count} selected " +
                                      (ws.Current?.Name ?? "none") + " extensions " +
                                      ArchetypeExtensions_V62.Get(ws.CurrentArchetype).Length);
                }
                if (int.TryParse(Environment.GetEnvironmentVariable("RLE_EXTSEL"), out var wantSel))
                    extProbeSelect_V69 = wantSel;
                if (Environment.GetEnvironmentVariable("RLE_EXTSAMPLE") == "1") extSampleWanted_V70 = true;
                var wantGame = Environment.GetEnvironmentVariable("RLE_EXTGAME");
                if (!string.IsNullOrWhiteSpace(wantGame)) panel.RequestExtGameArchetype_V68 = wantGame;
                extProbeFrames_V68 = 20;
                panel.ShowExtensions = true;
                if (screenshotPath != null) screenshotFrames = -1;
            }

            if (panel.RequestExtOpenYtyp_V68)
            {
                panel.RequestExtOpenYtyp_V68 = false;
                var f = ExtPickFile_V68("Archetypes (*.ytyp)|*.ytyp|All files|*.*");
                if (f != null)
                {
                    try { ws.AddYtyp(f); }
                    catch (Exception ex) { ws.Say("could not open it: " + ex.Message, true); }
                }
            }
            if (panel.RequestExtOpenYmap_V68)
            {
                panel.RequestExtOpenYmap_V68 = false;
                var f = ExtPickFile_V68("Placements (*.ymap)|*.ymap|All files|*.*");
                if (f != null)
                {
                    try { ws.AddYmap(f, ExtLookupArchetype_V68); }
                    catch (Exception ex) { ws.Say("could not open it: " + ex.Message, true); }
                }
            }
            if (panel.RequestExtOpenModel_V68)
            {
                panel.RequestExtOpenModel_V68 = false;
                var f = ExtPickFile_V68("Models (*.ydr;*.yft;*.ydd)|*.ydr;*.yft;*.ydd|All files|*.*");
                if (f != null)
                {
                    scene.LoadModelFile(f, additive: false);
                    extLastDir_V68 = Path.GetDirectoryName(f);
                    FrameModel();
                    var stem = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    var have = ws.Targets.FirstOrDefault(t => string.Equals(t.Name, stem, StringComparison.OrdinalIgnoreCase));
                    if (have != null) ws.Selected = ws.Targets.IndexOf(have);
                    else
                    {
                        var fromGame = ExtLookupArchetype_V68(JenkHash.GenHash(stem));
                        if (fromGame != null) ws.AddGameArchetype(fromGame);
                    }
                    ws.Say(ws.Current != null
                        ? $"{stem} - editing {ws.Current.Name}"
                        : $"{stem} loaded. It has no archetype yet: New archetype for the open model.");
                }
            }
            if (panel.RequestExtNewArchetype_V68)
            {
                panel.RequestExtNewArchetype_V68 = false;
                var name = scene.FileName != null ? Path.GetFileNameWithoutExtension(scene.FileName) : null;
                if (string.IsNullOrWhiteSpace(name)) ws.Say("open a model first - the archetype is named after it", true);
                else
                {
                    var bb = scene.GetSceneBounds();
                    var mn = bb.HasValue ? bb.Value.Minimum : new Vector3(-2, -2, 0);
                    var mx = bb.HasValue ? bb.Value.Maximum : new Vector3(2, 2, 3);
                    ws.NewArchetypeFor(name, mn, mx);
                }
            }
            if (panel.RequestExtSaveYtyp_V68 || panel.RequestExtSaveYtypAs_V68)
            {
                bool saveAs = panel.RequestExtSaveYtypAs_V68;
                panel.RequestExtSaveYtyp_V68 = false;
                panel.RequestExtSaveYtypAs_V68 = false;
                ExtSaveYtyp_V68(saveAs);
            }
            if (panel.RequestExtFromWorld_V68)
            {
                panel.RequestExtFromWorld_V68 = false;
                var sel = WorldEdit.Selected;
                var arch = sel?.Archetype;
                if (arch == null) ws.Say("select a prop in the World workspace first", true);
                else
                {
                    var t = ws.AddGameArchetype(arch);
                    if (t != null) { t.Placement = Vector3.Zero; t.Orientation = Quaternion.Identity; }
                    ws.Say($"{arch.Name} taken from the World");
                    panel.ExtShownFor_V68 = -1;
                }
            }
            if (panel.RequestExtPickProp_V69 >= 0)
            {
                int pi = panel.RequestExtPickProp_V69;
                panel.RequestExtPickProp_V69 = -1;
                if (ws.PickProp(pi, ExtLookupArchetype_V68) != null) panel.ExtShownFor_V68 = -1;
            }
            if (panel.RequestExtFromMlo_V68)
            {
                panel.RequestExtFromMlo_V68 = false;
                ExtTakeMloSession_V68();
            }
            if (!string.IsNullOrWhiteSpace(panel.RequestExtGameArchetype_V68))
            {
                var want = panel.RequestExtGameArchetype_V68.Trim().ToLowerInvariant();
                if (gameFiles == null || !gameFiles.Ready)
                {
                    ws.Say("the game archives are still loading - it will come in on its own");
                    goto afterGameLookup;
                }
                panel.RequestExtGameArchetype_V68 = null;
                JenkIndex.Ensure(want);
                var arch = ExtLookupArchetype_V68(JenkHash.GenHash(want));
                if (arch == null) ws.Say($"the game has no archetype called {want}", true);
                else
                {
                    ws.AddGameArchetype(arch);
                    ws.Say($"{want} found - {ArchetypeExtensions_V62.Get(arch).Length} extension(s)");
                    panel.ExtShownFor_V68 = -1;
                }
            }
            afterGameLookup:
            if (panel.RequestExtUndo_V70) { panel.RequestExtUndo_V70 = false; ws.Undo_V70(); ExtNoteEdited_V68(); }
            if (panel.RequestExtRedo_V70) { panel.RequestExtRedo_V70 = false; ws.Redo_V70(); ExtNoteEdited_V68(); }
            if (panel.RequestExtFrame_V68)
            {
                panel.RequestExtFrame_V68 = false;
                if (scene.HasModel) FrameModel();
            }

            if (extProbeFrames_V68 > 0 && gameFiles != null && gameFiles.Ready &&
                string.IsNullOrEmpty(panel.RequestExtGameArchetype_V68))
                extProbeFrames_V68--;
            if (extSampleWanted_V70 && !extSampleDone_V70 && gameFiles != null && gameFiles.Ready)
                ExtSampleTypes_V70();
            if (!extYptScanDone_V73 && Environment.GetEnvironmentVariable("RLE_YPTSCAN") == "1" &&
                gameFiles != null && gameFiles.Ready)
            { extYptScanDone_V73 = true; ExtYptScan_V73(); }
            if (extProbeFrames_V68 == 0)
            {
                extProbeFrames_V68 = -1;
                if (extProbeSelect_V69 >= 0) { ws.SelectedExtension = extProbeSelect_V69; extProbeSelect_V69 = -1; }
                Console.WriteLine($"EXTSPACE sources {ws.Sources.Count} archetypes {ws.Targets.Count} " +
                                  $"selected {(ws.Current?.Name ?? "none")} extensions " +
                                  ArchetypeExtensions_V62.Get(ws.CurrentArchetype).Length +
                                  $" sceneMeshes {scene.AllMeshes.Count()} status \"{ws.Status}\"");
                var dumpAll = Environment.GetEnvironmentVariable("RLE_EXTDUMP") == "1";
                if (dumpAll)
                {
                    var exts = ArchetypeExtensions_V62.Get(ws.CurrentArchetype);
                    for (int ei = 0; ei < exts.Length; ei++)
                    {
                        Console.WriteLine($"EXTDUMP [{ei}] {ArchetypeExtensions_V62.TypeLabel(exts[ei])}");
                        foreach (var fld in ArchetypeExtensions_V62.Fields(exts[ei]))
                            Console.WriteLine($"EXTDUMP     {fld.Prop.Name} = {ArchetypeExtensions_V62.GetValue(exts[ei], fld)}");
                    }
                }
                screenshotFrames = 6;
            }
            if (panel.ExtensionMode && !extMarkersForced_V71) { extMarkersForced_V71 = true; panel.ShowExtensions = true; }
            ExtServiceParticles_V73();
            panel.ExtWorldSelName_V68 = WorldEdit.Selected?.Archetype?.Name;
            panel.ExtMloSessionName_V68 = Creator?.Session?.Name;
            if (panel.ExtensionMode && panel.ExtAutoModel_V68 && panel.ExtShownFor_V68 != ws.Selected)
            {
                panel.ExtShownFor_V68 = ws.Selected;
                ExtShowModelFor_V68(ws.Current);
            }
        }

        private void ExtTakeMloSession_V68()
        {
            var ws = panel.Ext;
            var s = Creator?.Session;
            if (s == null) { ws.Say("start an interior in the MLO Creator first", true); return; }
            YtypFile ytyp = null;
            try { ytyp = s.BuildYtyp(); }
            catch (Exception ex) { ws.Say("could not read that interior: " + ex.Message, true); return; }
            var mlo = ytyp?.AllArchetypes?.FirstOrDefault(a => a is MloArchetype);
            if (mlo == null) { ws.Say("that interior has no archetype yet", true); return; }
            var src = new ExtensionSource_V68 { Name = s.Name + ".ytyp (MLO Creator)", Ytyp = ytyp };
            ws.Sources.Add(src);
            ws.Targets.Add(new ExtensionTarget_V68 { Archetype = mlo, Source = src });
            ws.Selected = ws.Targets.Count - 1;
            ws.SelectedExtension = -1;
            panel.ExtShownFor_V68 = -1;
            ws.Say($"{s.Name} taken from the MLO Creator - add extensions, then Save .ytyp");
        }

        private void ExtShowModelFor_V68(ExtensionTarget_V68 t)
        {
            var name = t?.Archetype?.Name;
            if (string.IsNullOrWhiteSpace(name)) return;
            var already = scene.Files.FirstOrDefault(f => f?.Path != null &&
                string.Equals(Path.GetFileNameWithoutExtension(f.Path), name, StringComparison.OrdinalIgnoreCase));
            if (already != null) return;
            try
            {
                var it = FindMloAsset_L3(name);
                if (it == null)
                {
                    panel.Ext.Say($"{name}: no model of that name in the archives or your folders - " +
                                  "open one with Open a model, or work from the numbers", false);
                    return;
                }
                var entry = it.ToEntry();
                if (!PropThumbnails.Load(entry, gameFiles, out var drawable, out var lights, out var ydr, out var yft) ||
                    drawable == null)
                {
                    panel.Ext.Say($"{name}: its model would not load", true);
                    return;
                }
                foreach (var f in scene.Files.ToList()) if (f != null) scene.RemoveFile(f);
                var arch = gameFiles != null && gameFiles.Ready
                    ? gameFiles.Cache?.GetArchetype(JenkHash.GenHash(name.ToLowerInvariant())) : null;
                if (arch != null && arch.TextureDict != 0)
                {
                    var ytd = gameFiles.GetTextureDict(arch.TextureDict);
                    if (ytd?.TextureDict != null && !modelRenderer.ExternalTextureDicts.Contains(ytd.TextureDict))
                        modelRenderer.ExternalTextureDicts.Add(ytd.TextureDict);
                }
                modelRenderer.TextureContext = arch?.TextureDict ?? 0;
                var model = modelRenderer.BuildFromDrawable(drawable, name, Matrix.Identity);
                modelRenderer.TextureContext = 0;
                if (model == null || model.Meshes.Count == 0)
                {
                    model?.Dispose();
                    panel.Ext.Say($"{name}: its model has nothing to draw", true);
                    return;
                }
                scene.AddImportedProp(it.FromArchive ? null : it.Path, ydr, yft, model, drawable.Skeleton,
                                      lights, Matrix.Identity, 1, it.FromArchive, name, null,
                                      fromMlo: false, drawable: drawable);
                panel.RequestExtFrame_V68 = true;
            }
            catch (Exception ex) { panel.Ext.Say("could not show that model: " + ex.Message, true); }
        }

        private Archetype ExtLookupArchetype_V68(uint hash)
        {
            foreach (var s in panel.Ext.Sources)
            {
                var a = s.Ytyp?.AllArchetypes?.FirstOrDefault(x => x != null && x.Hash == hash);
                if (a != null) return a;
            }
            return gameFiles?.Cache?.GetArchetype(hash);
        }

        private string ExtPickFile_V68(string filter)
        {
            if (IsHeadless) return null;
            using var dlg = new OpenFileDialog { Filter = filter };
            if (!string.IsNullOrEmpty(extLastDir_V68) && Directory.Exists(extLastDir_V68))
                dlg.InitialDirectory = extLastDir_V68;
            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            extLastDir_V68 = Path.GetDirectoryName(dlg.FileName);
            return dlg.FileName;
        }

        private void ExtSaveYtyp_V68(bool saveAs)
        {
            var ws = panel.Ext;
            var cur = ws.Current;
            if (cur == null) { ws.Say("pick an archetype first", true); return; }

            if (cur.Source != null && cur.Source.FromGame)
            {
                if (IsHeadless) { ws.Say("a game archetype needs Save as... to a file of your own", true); return; }
                var made = ExtAdoptGameArchetype_V68(cur);
                if (made == null) return;
                cur = made;
            }

            string path = cur.Source?.Path;
            if (saveAs || string.IsNullOrWhiteSpace(path))
            {
                if (IsHeadless) { ws.Say("no path to save to", true); return; }
                using var dlg = new SaveFileDialog
                {
                    Filter = "Archetypes (*.ytyp)|*.ytyp",
                    FileName = cur.Source?.Name ?? (cur.Name + ".ytyp"),
                };
                if (!string.IsNullOrEmpty(extLastDir_V68) && Directory.Exists(extLastDir_V68))
                    dlg.InitialDirectory = extLastDir_V68;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
                extLastDir_V68 = Path.GetDirectoryName(path);
            }
            string err;
            try { err = ws.SaveCurrentYtyp(path); }
            catch (Exception ex) { err = ex.Message + " - details in Help > Show log"; AppLog_U21.Error("Extensions .ytyp save", ex); }
            if (err != null && !err.Contains("Help > Show log")) AppLog_U21.Error(Path.GetFileName(path) + " was not saved: " + err);
            ws.Say(err ?? ("saved " + Path.GetFileName(path)), err != null);
        }

        private ExtensionTarget_V68 ExtAdoptGameArchetype_V68(ExtensionTarget_V68 cur)
        {
            var ws = panel.Ext;
            var b = cur.Archetype._BaseArchetypeDef;
            var made = ws.NewArchetypeFor(cur.Name, b.bbMin, b.bbMax);
            if (made == null) { ws.Say("could not make a .ytyp for it", true); return null; }
            var copy = ArchetypeExtensions_V62.Get(cur.Archetype);
            foreach (var w in copy) ArchetypeExtensions_V62.Add(made.Archetype, w);
            made.Placement = cur.Placement;
            made.Orientation = cur.Orientation;
            ws.Say($"made your own .ytyp for {cur.Name} - it carries its {copy.Length} extension(s)");
            return made;
        }

        partial void ExtMouseDown_V68(int x, int y, ref bool consumed)
        {
            if (!ExtActive_V68 || consumed) return;
            var ws = panel.Ext;
            if (!ws.SnapArmed) return;
            var arch = ws.CurrentArchetype;
            if (arch == null) { ws.CancelSnap(); return; }

            float w = deviceResources.Width, h = deviceResources.Height;
            var ray = camera.GetPickRay(x, y, w, h);
            if (!MloVertexSnap.Find(scene.AllMeshes, camera, ray, x, y, w, h, out var hit))
            {
                ws.Say("no vertex under the cursor - move closer, or click nearer a corner", true);
                consumed = true;
                return;
            }
            var t = ws.Current;
            ws.PushUndo_V70("Snap " + ArchetypeExtensions_V62.Spaced(ws.SnapFieldName ?? "point"));
            var inv = t?.Orientation ?? Quaternion.Identity; inv.Conjugate();
            var local = Vector3.Transform(hit.Position - (t?.Placement ?? Vector3.Zero), inv);
            ws.ApplySnap(arch, local);
            ExtNoteEdited_V68();
            consumed = true;
        }

        partial void ExtRightDown_V68(ref bool consumed)
        {
            if (!ExtActive_V68 || consumed) return;
            if (!panel.Ext.SnapArmed) return;
            panel.Ext.CancelSnap();
            panel.Ext.Say("snapping cancelled");
            consumed = true;
        }

        partial void ExtKeyDown_V70(System.Windows.Forms.Keys combo, ref bool handled)
        {
            if (!ExtActive_V68 || handled) return;
            var ws = panel.Ext;
            if (combo == (System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z))
            { ws.Undo_V70(); ExtNoteEdited_V68(); handled = true; return; }
            if (combo == (System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y) ||
                combo == (System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.Z))
            { ws.Redo_V70(); ExtNoteEdited_V68(); handled = true; }
        }

        partial void ExtRightClick_V70(int x, int y, ref bool handled)
        {
            if (!ExtActive_V68 || handled) return;
            var ws = panel.Ext;
            var t = ws.Current;
            if (t?.Archetype == null) return;

            float vw = deviceResources.Width, vh = deviceResources.Height;
            var vp = camera.ViewProjMatrix;
            (bool, float, float) Project(Vector3 world)
            {
                var clip = Vector4.Transform(new Vector4(world, 1.0f), vp);
                if (clip.W <= 0.0001f) return (false, 0, 0);
                return (true, (clip.X / clip.W * 0.5f + 0.5f) * vw, (0.5f - clip.Y / clip.W * 0.5f) * vh);
            }

            int hit = ws.PickExtensionAt_V70(Project, x, y, 22.0f);
            if (hit < 0) return;
            ws.SelectedExtension = hit;
            ws.SelectedPointField = null;
            ws.CancelSnap();
            var list = ArchetypeExtensions_V62.Get(t.Archetype);
            ws.Say(ArchetypeExtensions_V62.TypeLabel(list[hit]) + " picked in the viewport");
            handled = true;
        }

        partial void ExtEscape_V68(ref bool handled)
        {
            if (!ExtActive_V68 || handled) return;
            if (!panel.Ext.SnapArmed) return;
            panel.Ext.CancelSnap();
            panel.Ext.Say("snapping cancelled");
            handled = true;
        }

        private void ExtNoteEdited_V68()
        {
            var src = panel.Ext.Current?.Source;
            if (src?.Ytyp != null) src.Ytyp.HasChanged = true;
        }

        partial void OnAfterModelDraw_Ext_V68(SharpDX.Direct3D11.DeviceContext context)
        {
            if (!ExtActive_V68) return;
            var ws = panel.Ext;
            var t = ws.Current;
            if (t?.Archetype == null) return;

            ExtDrawPoints_V69(context, ws, t);

            if (ws.SnapArmed && scene.HasModel)
            {
                float w = deviceResources.Width, h = deviceResources.Height;
                var mp = PointToClient(Cursor.Position);
                var ray = camera.GetPickRay(mp.X, mp.Y, w, h);
                extSnap_V68 = MloVertexSnap.Find(scene.AllMeshes, camera, ray, mp.X, mp.Y, w, h, out var hit) ? hit : default;
                ExtDrawDots_V68(context);
            }
            else extSnap_V68 = default;
        }

        private void ExtDrawPoints_V69(SharpDX.Direct3D11.DeviceContext context,
                                      ExtensionWorkspace_V68 ws, ExtensionTarget_V68 t)
        {
            var list = ArchetypeExtensions_V62.Get(t.Archetype);
            int si = ws.SelectedExtension;
            bool anyOther = false;
            for (int oi = 0; oi < list.Length; oi++)
            {
                if (oi == si) continue;
                anyOther |= ExtDrawOne_V71(list[oi], t, 0.45f, false);
            }
            if (anyOther)
                triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthReadOnly);

            if (si < 0 || si >= list.Length) return;
            var sel = list[si];
            var fields = ExtensionWorkspace_V68.PointFields_V68(sel);
            if (fields.Count == 0) return;

            var baseCol = ExtensionHelpers.MarkerColour(sel);
            var col = new Vector4(baseCol.X, baseCol.Y, baseCol.Z, 1.0f);
            ExtDrawOne_V71(sel, t, 1.0f, true);
            var hot = new Vector4(1, 1, 1, 1);
            var fwd = camera.GetForward();
            var right = camera.GetRight(); right.Normalize();
            var up = Vector3.Cross(right, fwd); up.Normalize();

            var corners = new List<Vector3>();
            Vector3? bottom = null, top = null;
            bool any = false;

            foreach (var f in fields)
            {
                if (!(ArchetypeExtensions_V62.GetValue(sel, f) is Vector3 local)) continue;
                var n = f.Prop.Name;
                if (n == "direction" || n == "normal") continue;
                var world = t.Placement + Vector3.Transform(local, t.Orientation);
                bool armed = ws.SnapArmed && ws.SnapExtension == si && ws.SnapFieldName == n;
                float wpp = camera.WorldPerPixel(world);
                float rad = (armed ? 9.0f : 6.0f) * wpp;
                triRenderer.AddDiscAA(world, right, up, rad, 1.2f * wpp, armed ? hot : col, 20);
                triRenderer.AddThickCircleAA(world, right, up, rad + 2.0f * wpp, camera.Position,
                                             0.7f * wpp, 1.2f * wpp, armed ? hot : col, 24);
                any = true;
                if (n.StartsWith("corner", StringComparison.OrdinalIgnoreCase)) corners.Add(world);
                else if (n == "bottom") bottom = world;
                else if (n == "top") top = world;
            }

            if (corners.Count >= 3)
            {
                for (int i = 0; i < corners.Count; i++)
                {
                    var a2 = corners[i];
                    var b2 = corners[(i + 1) % corners.Count];
                    float wpp = camera.WorldPerPixel((a2 + b2) * 0.5f);
                    triRenderer.AddThickLineAA(a2, b2, camera.Position, 0.9f * wpp, 1.2f * wpp, col);
                }
                any = true;
            }
            if (bottom.HasValue && top.HasValue)
            {
                float wpp = camera.WorldPerPixel((bottom.Value + top.Value) * 0.5f);
                triRenderer.AddThickLineAA(bottom.Value, top.Value, camera.Position, 0.9f * wpp, 1.2f * wpp, col);
                any = true;
            }
            if (any)
                triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthReadOnly);
        }

        private void ExtGlyph_V72(MetaWrapper w, Vector3 at, Vector4 col, float alpha, bool selected)
        {
            float wpp = camera.WorldPerPixel(at);
            var fwd = camera.GetForward();
            var right = camera.GetRight(); right.Normalize();
            var up = Vector3.Cross(right, fwd); up.Normalize();
            var c = new Vector4(col.X, col.Y, col.Z, alpha);
            float px = wpp;

            switch (w)
            {
                case MCExtensionDefParticleEffect pe:
                {
                    float scale = Math.Max(pe.Data.scale, 0.15f);
                    for (int r = 0; r < 3; r++)
                    {
                        float rr = (0.10f + r * 0.11f) * scale;
                        var lift = at + new Vector3(0, 0, rr * 1.15f);
                        triRenderer.AddThickCircleAA(lift, right, up, rr, camera.Position,
                                                     0.9f * px, 1.2f * px, ExtFade_V72(c, 1.0f - r * 0.22f), 20);
                    }
                    triRenderer.AddThickLineAA(at, at + new Vector3(0, 0, 0.42f * scale), camera.Position,
                                               0.8f * px, 1.2f * px, ExtFade_V72(c, 0.7f));
                    break;
                }
                case MCExtensionDefSpawnPoint sp:
                {
                    var q = sp.Data.offsetRotation;
                    var rot = new Quaternion(q.X, q.Y, q.Z, q.W);
                    if (rot.LengthSquared() < 1e-6f) rot = Quaternion.Identity;
                    var face = Vector3.Transform(new Vector3(0, 1, 0), rot);
                    triRenderer.AddThickCircleAA(at, Vector3.UnitX, Vector3.UnitY, 0.35f, camera.Position,
                                                 1.0f * px, 1.2f * px, c, 28);
                    triRenderer.AddThickLineAA(at, at + new Vector3(0, 0, 1.0f), camera.Position, 1.0f * px, 1.2f * px, c);
                    triRenderer.AddThickLineAA(at + new Vector3(0, 0, 0.05f), at + face * 0.6f + new Vector3(0, 0, 0.05f),
                                               camera.Position, 1.2f * px, 1.2f * px, ExtFade_V72(c, 1.0f));
                    break;
                }
                case MCExtensionDefLadder _:
                    break;
                case MCExtensionDefAudioEmitter _:
                case MCExtensionDefAudioCollisionSettings _:
                {
                    for (int r = 1; r <= 3; r++)
                        triRenderer.AddThickCircleAA(at, right, up, 0.18f * r, camera.Position,
                                                     0.8f * px, 1.2f * px, ExtFade_V72(c, 1.0f - (r - 1) * 0.25f), 24);
                    break;
                }
                case MCExtensionDefExplosionEffect _:
                {
                    for (int i = 0; i < 8; i++)
                    {
                        double ang = i * Math.PI / 4.0;
                        var dir = right * (float)Math.Cos(ang) + up * (float)Math.Sin(ang);
                        triRenderer.AddThickLineAA(at + dir * 0.10f, at + dir * 0.42f, camera.Position,
                                                   0.9f * px, 1.2f * px, c);
                    }
                    break;
                }
                case MCExtensionDefWindDisturbance _:
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        var off = up * (i * 0.14f);
                        triRenderer.AddThickLineAA(at + off - right * 0.3f, at + off + right * 0.3f,
                                                   camera.Position, 0.8f * px, 1.2f * px, c);
                    }
                    triRenderer.AddThickLineAA(at + right * 0.3f, at + right * 0.16f + up * 0.12f,
                                               camera.Position, 0.8f * px, 1.2f * px, c);
                    triRenderer.AddThickLineAA(at + right * 0.3f, at + right * 0.16f - up * 0.12f,
                                               camera.Position, 0.8f * px, 1.2f * px, c);
                    break;
                }
                case MCExtensionDefProcObject po:
                {
                    triRenderer.AddThickCircleAA(at, Vector3.UnitX, Vector3.UnitY, Math.Max(po.Data.radiusInner, 0.05f),
                                                 camera.Position, 0.8f * px, 1.2f * px, ExtFade_V72(c, 0.8f), 28);
                    triRenderer.AddThickCircleAA(at, Vector3.UnitX, Vector3.UnitY, Math.Max(po.Data.radiusOuter, 0.1f),
                                                 camera.Position, 0.9f * px, 1.2f * px, c, 32);
                    break;
                }
                case MCExtensionDefBuoyancy _:
                {
                    triRenderer.AddThickCircleAA(at, Vector3.UnitX, Vector3.UnitY, 0.3f, camera.Position,
                                                 0.9f * px, 1.2f * px, c, 28);
                    break;
                }
                case MCExtensionDefDoor _:
                {
                    triRenderer.AddThickCircleAA(at, Vector3.UnitX, Vector3.UnitY, 0.5f, camera.Position,
                                                 0.9f * px, 1.2f * px, c, 8);
                    break;
                }
                default:
                    triRenderer.AddThickCircleAA(at, right, up, 0.16f, camera.Position, 0.8f * px, 1.2f * px, c, 16);
                    break;
            }

            if (selected || alpha > 0.6f)
            {
                var label = ArchetypeExtensions_V62.TypeLabel(w);
                var nm = ArchetypeExtensions_V62.NameOf(w);
                if (w is MCExtensionDefParticleEffect fx && !string.IsNullOrEmpty(fx.fxName)) nm = fx.fxName;
                DrawWorldLabel(at + up * (18.0f * px), string.IsNullOrEmpty(nm) ? label : label + "  " + nm,
                               new Vector4(col.X, col.Y, col.Z, selected ? 1.0f : 0.75f));
            }
        }

        private static Vector4 ExtFade_V72(Vector4 c, float k) => new Vector4(c.X, c.Y, c.Z, c.W * k);

        private bool ExtDrawOne_V71(MetaWrapper w, ExtensionTarget_V68 t, float alpha, bool selected)
        {
            var fields = ExtensionWorkspace_V68.PointFields_V68(w);
            if (fields.Count == 0) return false;
            var mc = ExtensionHelpers.MarkerColour(w);
            var col = new Vector4(mc.X, mc.Y, mc.Z, alpha);
            var fwd = camera.GetForward();
            var right = camera.GetRight(); right.Normalize();
            var up = Vector3.Cross(right, fwd); up.Normalize();

            var corners = new List<Vector3>();
            Vector3? bottom = null, top = null;
            bool any = false;
            foreach (var f in fields)
            {
                var n = f.Prop.Name;
                if (n == "direction" || n == "normal") continue;
                if (!(ArchetypeExtensions_V62.GetValue(w, f) is Vector3 local)) continue;
                var world = t.Placement + Vector3.Transform(local, t.Orientation);
                float wpp = camera.WorldPerPixel(world);
                triRenderer.AddDiscAA(world, right, up, 4.0f * wpp, 1.2f * wpp, col, 16);
                triRenderer.AddThickCircleAA(world, right, up, 6.0f * wpp, camera.Position,
                                             0.6f * wpp, 1.2f * wpp, col, 20);
                if (n == "offsetPosition" || fields.Count == 1)
                    ExtGlyph_V72(w, world, col, alpha, selected);
                any = true;
                if (n.StartsWith("corner", StringComparison.OrdinalIgnoreCase)) corners.Add(world);
                else if (n == "bottom") bottom = world;
                else if (n == "top") top = world;
            }
            if (corners.Count >= 3)
                for (int i = 0; i < corners.Count; i++)
                {
                    var a2 = corners[i]; var b2 = corners[(i + 1) % corners.Count];
                    float wpp = camera.WorldPerPixel((a2 + b2) * 0.5f);
                    triRenderer.AddThickLineAA(a2, b2, camera.Position, 0.7f * wpp, 1.2f * wpp, col);
                }
            if (bottom.HasValue && top.HasValue)
            {
                float wpp = camera.WorldPerPixel((bottom.Value + top.Value) * 0.5f);
                triRenderer.AddThickLineAA(bottom.Value, top.Value, camera.Position, 0.7f * wpp, 1.2f * wpp, col);
            }
            return any;
        }

        private void ExtDrawDots_V68(SharpDX.Direct3D11.DeviceContext context)
        {
            if (!extSnap_V68.Valid) return;
            extDots_V68 ??= new MloVertexDots(deviceResources.Device);
            extDots_V68.BeginFrame();
            float vw = deviceResources.Width, vh = deviceResources.Height;
            var centre = extSnap_V68.Triangle >= 0 ? extSnap_V68.SurfacePoint : extSnap_V68.Position;
            var hitMesh = extSnap_V68.Mesh;
            var dotCol = new Vector4(UiTheme.Accent.X, UiTheme.Accent.Y, UiTheme.Accent.Z, 0.85f);
            var hitCol = new Vector4(UiTheme.AccentBright.X, UiTheme.AccentBright.Y, UiTheme.AccentBright.Z, 0.95f);
            int budget = 200000;
            foreach (var m in scene.AllMeshes)
            {
                if (budget <= 0) break;
                if (m == null || !m.Visible || m.NeverDraw || m.PickVerts == null || m.PickVerts.Length == 0) continue;
                var b = m.WorldBounds;
                if (b.Maximum.X <= b.Minimum.X) continue;
                bool isHit = ReferenceEquals(m, hitMesh);
                if (!isHit)
                {
                    var sphere = new BoundingSphere(centre, 1.5f);
                    if (b.Contains(ref sphere) == ContainmentType.Disjoint) continue;
                }
                extDots_V68.Draw(context, camera, vw, vh, m, centre, 1.5f, 5.0f, isHit ? hitCol : dotCol);
                budget -= m.PickVerts.Length;
            }

            var p = extSnap_V68.Position;
            float wpp = camera.WorldPerPixel(p);
            var fwd = camera.GetForward();
            var right = camera.GetRight(); right.Normalize();
            var up = Vector3.Cross(right, fwd); up.Normalize();
            float rad = 10.0f * wpp;
            triRenderer.AddDiscAA(p, right, up, rad, 1.2f * wpp, new Vector4(1, 1, 1, 1), 24);
            triRenderer.AddThickCircleAA(p, right, up, rad + 2.5f * wpp, camera.Position, 0.7f * wpp, 1.2f * wpp, hitCol, 32);
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthReadOnly);
        }

        private void SeqTest_ExtWorkspace_V68(Action<string, bool, string> check)
        {
            ExtensionWorkspace_V68.SelfTest_V68(check);
            ExtensionWorkspace_V68.SelfTestGizmo_V69(check);
            ExtensionWorkspace_V68.SelfTestHistory_V70(check);
            ExtensionPresets_V70.SelfTest_V70(check);
            ParticleEffectNames_V71.SelfTest_V71(check);
            ExtensionLimits_V72.SelfTest_V72(check);
            try
            {
                var was = panel.Workspace;
                panel.SwitchWorkspace(Editor.LightPanel.Space.Extension);
                check("v68 extensions: the workspace has its own tab and opens",
                      panel.ExtensionMode && panel.Workspace == Editor.LightPanel.Space.Extension,
                      panel.Workspace.ToString());
                check("v68 extensions: it is in the workspace tab order",
                      Array.IndexOf(Editor.LightPanel.WorkspaceTabOrder_P1, Editor.LightPanel.Space.Extension) >= 0,
                      string.Join(" ", Editor.LightPanel.WorkspaceTabOrder_P1));
                panel.SwitchWorkspace(was);
            }
            catch (Exception ex) { check("v68 extensions workspace", false, ex.Message); }
        }
    }
}
