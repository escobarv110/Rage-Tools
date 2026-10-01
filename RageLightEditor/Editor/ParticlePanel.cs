using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;
using SDX = SharpDX;
using GameTexture = CodeWalker.GameFiles.Texture;

namespace RageLightEditor.Editor
{
    public partial class ParticlePanel
    {
        public string Status = "Pick an effect from the library - or open a .ypt of your own.";

        public bool RequestOpenYpt;
        public bool RequestSave;
        public bool RequestSaveAs;
        public string RequestPlayEffect;
        public string RequestOpenGameYpt;
        public bool RequestPlaceAtView;
        public bool RequestAddToYtyp;
        public int YtypTarget, ArchetypeTarget;
        public readonly List<string> YtypTargets = new List<string>();
        public readonly List<string> ArchetypeTargets = new List<string>();

        public PtfxDocument Doc { get; private set; }
        public readonly PtfxSimulator Sim = new PtfxSimulator();

        public List<PtfxGameIndex.Entry> GameYpts;

        private int selEffect = -1;
        private int selEmitter = -1;
        private int blendChoice;
        private readonly Dictionary<int, string> texOverride = new Dictionary<int, string>();

        private string libSearch = "";
        private string assetSearch = "";
        private string effectFilter = "";
        private int libTab;

        public int BlendOverride => blendChoice - 1;
        public string SelectedEffectName =>
            selEffect >= 0 && Doc != null && selEffect < Doc.Effects.Count ? Doc.Effects[selEffect].Name : null;

        public static readonly Dictionary<string, string> EffectAsset =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static int EffectCount { get; private set; }
        public static string LibraryError { get; private set; }

        public static readonly List<(string family, List<string> fx)> Families = LoadFamilies();

        private static List<(string, List<string>)> LoadFamilies()
        {
            var result = new List<(string, List<string>)>();
            try
            {
                using var stream = typeof(ParticlePanel).Assembly.GetManifestResourceStream("particles.json");
                if (stream == null) { LibraryError = "particles.json is not embedded in this build"; return result; }
                using var doc = System.Text.Json.JsonDocument.Parse(stream);
                foreach (var prop in doc.RootElement.EnumerateObject()
                                        .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var fx = prop.Value.EnumerateArray().Select(e => e.GetString())
                                 .Where(x => !string.IsNullOrEmpty(x))
                                 .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    if (fx.Count == 0) continue;
                    result.Add((prop.Name, fx));
                    EffectCount += fx.Count;
                    foreach (var f in fx)
                        if (!EffectAsset.ContainsKey(f)) EffectAsset[f] = prop.Name;
                }
            }
            catch (Exception ex) { LibraryError = ex.Message; }
            return result;
        }

        public void SetDocument(PtfxDocument doc)
        {
            Doc = doc;
            selEffect = doc != null && doc.Effects.Count > 0 ? 0 : -1;
            selEmitter = selEffect >= 0 && doc.Effects[0].Emitters.Count > 0 ? 0 : -1;
            texOverride.Clear();
            Sim.SetEffect(selEffect >= 0 ? doc.Effects[selEffect] : null);
            Sim.Restart();
            Status = doc != null ? $"{doc.Name}: {doc.Effects.Count} effects" : "";
        }

        public void SelectEffect(int idx)
        {
            if (Doc == null || idx < 0 || idx >= Doc.Effects.Count) return;
            selEffect = idx;
            selEmitter = Doc.Effects[idx].Emitters.Count > 0 ? 0 : -1;
            texOverride.Clear();
            Sim.SetEffect(Doc.Effects[idx]);
            Sim.Restart();
        }

        public bool PlayByName(string fx)
        {
            if (Doc == null || string.IsNullOrEmpty(fx)) return false;
            for (int i = 0; i < Doc.Effects.Count; i++)
            {
                if (!string.Equals(Doc.Effects[i].Name, fx, StringComparison.OrdinalIgnoreCase)) continue;
                SelectEffect(i);
                return true;
            }
            return false;
        }

        public GameTexture GetEmitterTexture(int emitterIndex)
        {
            var texDict = Doc?.PtxList?.TextureDictionary;
            var texs = texDict?.Textures?.data_items;
            if (texs == null || texs.Length == 0) return null;

            if (texOverride.TryGetValue(emitterIndex, out var name))
            {
                foreach (var t in texs)
                    if (t?.Name == name) return t;
            }

            var em = Sim.GetSourceEmitter(emitterIndex);
            if (em == null)
            {
                var eff = selEffect >= 0 && selEffect < (Doc?.Effects.Count ?? 0) ? Doc.Effects[selEffect] : null;
                em = eff != null && emitterIndex < eff.Emitters.Count ? eff.Emitters[emitterIndex] : null;
            }

            var pr = em?.ParticleRule;
            if (pr?.ShaderVars?.data_items != null)
            {
                foreach (var svar in pr.ShaderVars.data_items)
                {
                    if (!(svar is ParticleShaderVarTexture svt)) continue;
                    if (svt.Texture != null) return svt.Texture;
                    var wanted = svt.TextureName?.Value;
                    if (string.IsNullOrEmpty(wanted) || svt.TextureNameHash == 0) continue;
                    foreach (var t in texs)
                        if (string.Equals(t?.Name, wanted, StringComparison.OrdinalIgnoreCase))
                            return t;
                    foreach (var t in texs)
                        if (t != null && t.NameHash == svt.TextureNameHash)
                            return t;
                }
            }

            var pname = em?.ParticleName?.ToLowerInvariant() ?? "";
            foreach (var t in texs)
            {
                var tn = t?.Name?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(tn) && (pname.Contains(tn) || tn.Contains(pname)))
                    return t;
            }
            return texs.Length == 1 ? texs[0] : null;
        }

        public void DrawLibrary(float height)
        {
            if (ImGui.Button("New effect of my own", new Vector2(-1, 0))) RequestNewDocument_T6 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A new .ypt holding one effect that already puffs smoke.\n" +
                                 "Shape it in Create on the right, then Save .ypt as...");
            if (ImGui.Button("Open .ypt...", new Vector2(-1, 0))) RequestOpenYpt = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A loose .ypt on disk. Effects from the game's own archives are\n" +
                                 "in the Game library tab and need no file.");

            if (ImGui.BeginTabBar("##ptfxlib"))
            {
                if (ImGui.BeginTabItem("Game library")) { libTab = 0; ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("This asset")) { libTab = 1; ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem(".ypt files")) { libTab = 2; ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }

            float listH = Math.Max(160.0f, height - 210.0f);
            switch (libTab)
            {
                case 1: DrawDocEffects(listH); break;
                case 2: DrawGameYptList(listH); break;
                default: DrawCatalogue(listH); break;
            }
        }

        private void DrawCatalogue(float height)
        {
            if (Families.Count == 0)
            {
                ImGui.TextWrapped("The effect catalogue did not load (" + (LibraryError ?? "no reason given") +
                                  ") - the .ypt files tab still lists every asset in the install.");
                return;
            }
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##ptfxsearch", $"search {EffectCount:N0} effects...", ref libSearch, 64);

            var playing = Sim.Effect?.Name;
            if (ImGui.BeginChild("##ptfxfam", new Vector2(0, height), ImGuiChildFlags.Borders))
            {
                foreach (var (family, fx) in Families)
                {
                    List<string> matches = fx;
                    if (libSearch.Length > 0)
                    {
                        matches = null;
                        foreach (var x in fx)
                        {
                            if (x.IndexOf(libSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            (matches ??= new List<string>()).Add(x);
                        }
                        if (matches == null) continue;
                    }
                    var flags = libSearch.Length > 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
                    if (!ImGui.TreeNodeEx($"{family}  ({matches.Count})##fam{family}", flags)) continue;
                    foreach (var n in matches)
                    {
                        if (ImGui.Selectable("  " + n, string.Equals(n, playing, StringComparison.OrdinalIgnoreCase)))
                            RequestPlayEffect = n;
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip(family + ".ypt");
                    }
                    ImGui.TreePop();
                }
            }
            ImGui.EndChild();
        }

        private void DrawDocEffects(float height)
        {
            if (Doc == null)
            {
                ImGui.TextWrapped("No .ypt open. Pick an effect in the Game library and its asset " +
                                  "loads itself, or open a file of your own.");
                return;
            }
            ImGui.TextDisabled(Doc.Name + (Doc.Dirty ? " *" : ""));
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##efffilter", "filter this asset's effects...", ref effectFilter, 64);
            if (ImGui.BeginChild("##efflist", new Vector2(0, height), ImGuiChildFlags.Borders))
            {
                for (int i = 0; i < Doc.Effects.Count; i++)
                {
                    var eff = Doc.Effects[i];
                    if (effectFilter.Length > 0 &&
                        eff.Name.IndexOf(effectFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (ImGui.Selectable($"{eff.Name}##eff{i}", i == selEffect)) SelectEffect(i);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{eff.Emitters.Count} emitter(s), duration {eff.Rule.DurationMax:0.##}s");
                }
            }
            ImGui.EndChild();
        }

        private void DrawGameYptList(float height)
        {
            if (GameYpts == null)
            {
                ImGui.TextWrapped("Waiting for the game archives. Set the GTA V folder in the Lights " +
                                  "workspace if this does not finish.");
                return;
            }
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##yptsearch", $"search {GameYpts.Count:N0} .ypt assets...", ref assetSearch, 64);
            if (ImGui.BeginChild("##yptlist", new Vector2(0, height), ImGuiChildFlags.Borders))
            {
                int shown = 0;
                foreach (var e in GameYpts)
                {
                    if (assetSearch.Length > 0 &&
                        e.Name.IndexOf(assetSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (++shown > 400) { ImGui.TextDisabled("...narrow the search"); break; }
                    if (ImGui.Selectable(e.Name + "##ypt" + e.Path)) RequestOpenGameYpt = e.Path;
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(e.Path);
                }
            }
            ImGui.EndChild();
        }

        public void DrawPlayback()
        {
            var eff = Sim.Effect;
            if (eff == null)
            {
                ImGui.TextWrapped("Nothing is playing. Pick an effect on the left.");
                var name = SelectedEffectName;
                if (name != null && ImGui.Button("Play " + name, new Vector2(-1, 0))) SelectEffect(selEffect);
                return;
            }
            ImGui.TextColored(new Vector4(0.93f, 0.33f, 0.62f, 1f), eff.Name);
            ImGui.TextDisabled($"{Sim.AliveCount:N0} particles alive   {eff.Emitters.Count} emitter(s)");

            if (ImGui.Button(Sim.Playing ? "Pause" : "Play")) Sim.Playing = !Sim.Playing;
            ImGui.SameLine();
            if (ImGui.Button("Restart")) { Sim.Restart(); Sim.Playing = true; }
            ImGui.SameLine();
            if (ImGui.Button("Stop")) { Sim.SetEffect(null); Status = "stopped"; }
            ImGui.SameLine();
            ImGui.Checkbox("Loop", ref Sim.Loop);
            ImGui.SetNextItemWidth(-1);
            ImGui.SliderFloat("##ptfxspeed", ref Sim.TimeScale, 0.05f, 3.0f, "speed %.2fx");

            var t = Sim.EffectTime;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##ptfxscrub", ref t, 0f, Sim.Duration, "t = %.2f s of " + Sim.Duration.ToString("0.##")))
            {
                Sim.Playing = false;
                Sim.SeekTo(t);
            }

            ImGui.TextDisabled("Position  (x, y, z in metres)");
            var origin = new Vector3(Sim.Origin.X, Sim.Origin.Y, Sim.Origin.Z);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat3("##ptfxorigin", ref origin, 0.05f))
                Sim.Origin = new SDX.Vector3(origin.X, origin.Y, origin.Z);
            if (ImGui.Button("Place where I am looking", new Vector2(-1, 0))) RequestPlaceAtView = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Puts the effect on the surface under the middle of the viewport,\n" +
                                 "or a few metres ahead of the camera when there is nothing there.");

            var scale = Sim.EffectScale;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##ptfxscale", ref scale, 0.05f, 10.0f, "scale %.2f"))
                Sim.EffectScale = Math.Max(0.01f, scale);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The whole-effect scale a particle extension carries: domains,\n" +
                                 "speeds and sprite sizes all grow together, as in the game.");

            ImGui.TextDisabled("Wind  (metres per second)");
            var wind = new Vector3(Sim.WindVelocity.X, Sim.WindVelocity.Y, Sim.WindVelocity.Z);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat3("##ptfxwind", ref wind, 0.05f))
                Sim.WindVelocity = new SDX.Vector3(wind.X, wind.Y, wind.Z);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Only the rules carrying a ptxu_Wind influence curve feel it.");

            var pss = PtfxSimulator.PreviewSizeScale;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##ptfxpreviewsize", ref pss, 0.1f, 4.0f, "sprite size x%.2f"))
                PtfxSimulator.PreviewSizeScale = Math.Max(0.01f, pss);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A zoom on the billboards, for judging a sprite up close.\n" +
                                 "1.00 is the size the asset actually asks for, in metres.");

            ImGui.SetNextItemWidth(-1);
            ImGui.Combo("##ptfxblend", ref blendChoice, "blend: per-rule (auto)\0blend: alpha\0blend: additive\0", 3);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Per-rule reads each ParticleRule's BlendSet. The two overrides are\n" +
                                 "for judging a rule whose blend set we map wrongly.");

            DrawUnsupportedRulesToggle_S6();

            if (!string.IsNullOrEmpty(Sim.DebugInfo)) ImGui.TextDisabled(Sim.DebugInfo);
        }

        public void DrawEffectEditor()
        {
            if (Doc == null || selEffect < 0 || selEffect >= Doc.Effects.Count)
            {
                ImGui.TextDisabled("No effect selected.");
                return;
            }
            var eff = Doc.Effects[selEffect];

            var dmin = eff.Rule.DurationMin;
            var dmax = eff.Rule.DurationMax;
            ImGui.SetNextItemWidth(90);
            if (ImGui.DragFloat("Dur min", ref dmin, 0.05f, 0.05f, 120f)) { eff.Rule.DurationMin = dmin; Touch(); }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            if (ImGui.DragFloat("Dur max", ref dmax, 0.05f, 0.05f, 120f)) { eff.Rule.DurationMax = dmax; Touch(true); }

            ImGui.Separator();
            for (int i = 0; i < eff.Emitters.Count; i++)
            {
                var em = eff.Emitters[i];
                var open = ImGui.CollapsingHeader($"{em.Name}  ->  {em.ParticleName}##em{i}",
                    i == selEmitter ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
                if (ImGui.IsItemClicked()) selEmitter = i;
                if (!open) continue;
                ImGui.PushID(i);
                DrawEmitterEditor(em, i);
                ImGui.PopID();
            }
        }

        private void DrawEmitterEditor(PtfxEmitter em, int index)
        {
            var er = em.EmitterRule;
            var pr = em.ParticleRule;
            if (er == null || pr == null) { ImGui.TextDisabled("(missing rule)"); return; }

            var ev = em.Event;
            if (ev != null)
            {
                var start = ev.StartRatio; var end = ev.EndRatio;
                ImGui.SetNextItemWidth(90);
                if (ImGui.DragFloat("Start", ref start, 0.01f, 0f, 1f)) { ev.StartRatio = start; Touch(true); }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90);
                if (ImGui.DragFloat("End", ref end, 0.01f, 0f, 1f)) { ev.EndRatio = end; Touch(true); }
            }
            ImGui.TextDisabled($"BlendSet {pr.BlendSet}   OneShot {(er.IsOneShot != 0 ? "yes" : "no")}");

            var texDict = Doc?.PtxList?.TextureDictionary?.Textures?.data_items;
            if (texDict != null && texDict.Length > 0)
            {
                var cur = GetEmitterTexture(index)?.Name ?? "(none)";
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##tex", cur))
                {
                    foreach (var tx in texDict)
                    {
                        if (tx?.Name == null) continue;
                        if (ImGui.Selectable(tx.Name, tx.Name == cur))
                        {
                            texOverride[index] = tx.Name;
                            PtfxAuthor.SetSheet(Doc, em, tx);
                            Touch(true);
                        }
                    }
                    ImGui.EndCombo();
                }
            }

            if (ImGui.TreeNode("Emitter curves"))
            {
                DrawKfpList(er.KeyframeProps);
                ImGui.TreePop();
            }
            DrawDomain("Creation domain", er.CreationDomainObj);
            DrawDomain("Target domain", er.TargetDomainObj);

            var behs = pr.AllBehaviours?.data_items;
            if (behs != null && ImGui.TreeNode($"Behaviours ({behs.Length})"))
            {
                foreach (var bh in behs)
                {
                    if (bh == null) continue;
                    var kps = bh.KeyframeProps?.data_items;
                    var label = bh.GetType().Name.Replace("ParticleBehaviour", "");
                    if (!ImGui.TreeNode(label + "##" + bh.GetHashCode())) continue;
                    if (kps != null) DrawKfpList(kps);
                    else ImGui.TextDisabled("(no keyframes)");
                    ImGui.TreePop();
                }
                ImGui.TreePop();
            }
        }

        private void DrawDomain(string label, ParticleDomain d)
        {
            if (d == null) return;
            if (!ImGui.TreeNode($"{label}: {d.DomainType}")) return;
            DrawKfp(d.PositionKFP, "Position");
            DrawKfp(d.RotationKFP, "Rotation");
            DrawKfp(d.SizeOuterKFP, "Size outer");
            DrawKfp(d.SizeInnerKFP, "Size inner");
            ImGui.TreePop();
        }

        private void DrawKfpList(ParticleKeyframeProp[] props)
        {
            if (props == null) return;
            foreach (var p in props)
            {
                if (p == null) continue;
                DrawKfp(p, p.Name.ToString());
            }
        }

        private void DrawKfp(ParticleKeyframeProp p, string label)
        {
            if (p == null) return;
            var vals = p.Values?.data_items;
            var n = vals?.Length ?? 0;
            if (!ImGui.TreeNode($"{label} ({n})##kfp{p.GetHashCode()}")) return;

            if (vals != null)
            {
                for (int i = 0; i < vals.Length; i++)
                {
                    var kv = vals[i];
                    ImGui.PushID(i);
                    var time = kv.KeyframeTime.X;
                    ImGui.SetNextItemWidth(60);
                    if (ImGui.DragFloat("##t", ref time, 0.005f))
                    {
                        kv.KeyframeTime = new SDX.Vector4(time, kv.KeyframeTime.Y, kv.KeyframeTime.Z, kv.KeyframeTime.W);
                        Touch();
                    }
                    ImGui.SameLine();
                    var val = new Vector4(kv.KeyframeValue.X, kv.KeyframeValue.Y, kv.KeyframeValue.Z, kv.KeyframeValue.W);
                    ImGui.SetNextItemWidth(-30);
                    if (ImGui.DragFloat4("##v", ref val, 0.01f))
                    {
                        kv.KeyframeValue = new SDX.Vector4(val.X, val.Y, val.Z, val.W);
                        Touch();
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("x"))
                    {
                        var list = vals.ToList();
                        list.RemoveAt(i);
                        p.Values.data_items = list.ToArray();
                        Touch();
                        ImGui.PopID();
                        break;
                    }
                    ImGui.PopID();
                }
            }
            if (ImGui.SmallButton("+ key"))
            {
                var list = (vals ?? Array.Empty<ParticleKeyframePropValue>()).ToList();
                var lastT = list.Count > 0 ? list[^1].KeyframeTime.X : 0f;
                var lastV = list.Count > 0 ? list[^1].KeyframeValue : new SDX.Vector4(1f, 1f, 1f, 1f);
                list.Add(new ParticleKeyframePropValue
                {
                    KeyframeTime = new SDX.Vector4(Math.Min(1f, lastT + 0.25f), 0f, 0f, 0f),
                    KeyframeValue = lastV,
                });
                p.Values.data_items = list.ToArray();
                Touch();
            }
            ImGui.TreePop();
        }

        private void Touch(bool rebuildSim = false)
        {
            if (Doc != null) Doc.Dirty = true;
            if (!rebuildSim || selEffect < 0) return;
            var keepTime = Sim.EffectTime;
            Sim.SetEffect(Doc.Effects[selEffect]);
            Sim.SeekTo(keepTime);
        }
    }
}

