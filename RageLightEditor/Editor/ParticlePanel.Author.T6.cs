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
        public bool RequestNewDocument_T6;
        public bool RequestNewFromPlaying_T6;
        public bool RequestLoadSheets_T6;
        public int RequestImportSheet_T6 = -1;

        public string NewAssetName_T6 = "my_particles";
        public string NewEffectName_T6 = "my_effect";
        public string SheetAsset_T6 = "core";
        public readonly List<GameTexture> SheetLibrary_T6 = new List<GameTexture>();
        public string SheetLibraryNote_T6 = "";

        private string sheetSearch_T6 = "";
        private bool sheetPickerOpen_T6;
        private int renameTarget_T6 = -1;
        private string renameBuf_T6 = "";
        private int addRuleChoice_T6;

        public bool IsAuthoredDoc_T6 => Doc != null && Doc.RpfPath == null;

        public void DrawAuthoring_T6()
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##ptfxasset", "asset name (the .ypt)", ref NewAssetName_T6, 48);
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##ptfxeffname", "effect name", ref NewEffectName_T6, 48);

            if (ImGui.Button("New effect", new Vector2(-1, 0))) RequestNewDocument_T6 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A new .ypt of your own holding one effect that already puffs smoke.\n" +
                                 "Everything below then edits it, and Save .ypt as... writes it out.");

            ImGui.BeginDisabled(Doc == null);
            if (ImGui.Button("Add effect", new Vector2(-1, 0)) && Doc != null)
            {
                var eff = PtfxAuthor.AddEffect(Doc, NewEffectName_T6, null);
                if (eff != null) { SelectByName_T6(eff.Name); Status = "added " + eff.Name; }
            }
            ImGui.EndDisabled();

            ImGui.BeginDisabled(Sim.Effect == null);
            if (ImGui.Button("From playing effect", new Vector2(-1, 0)))
                RequestNewFromPlaying_T6 = true;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Copies the effect that is playing - a shipping one, if that is what you\n" +
                                 "opened - into a new asset of your own, through the same save/load the\n" +
                                 "file goes through. The safest thing to start from if this is going back\n" +
                                 "into the game.");

            var edited = EditedEffect;
            if (edited == null)
            {
                ImGui.Spacing();
                ImGui.TextWrapped("No effect selected. New effect makes one.");
                return;
            }

            ImGui.Separator();
            if (DrawEffectHeader_T6(edited)) return;
            ImGui.Separator();
            DrawEmitters_T6(edited);
        }

        private bool DrawEffectHeader_T6(PtfxEffect eff)
        {
            ImGui.TextDisabled("EFFECT");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.93f, 0.33f, 0.62f, 1f), eff.Name);
            if (ImGui.SmallButton("Rename##eff"))
            {
                renameTarget_T6 = -2;
                renameBuf_T6 = eff.Name;
            }
            ImGui.SameLine();
            ImGui.BeginDisabled((Doc?.Effects.Count ?? 0) <= 1);
            bool wantDelete = ImGui.SmallButton("Delete effect");
            ImGui.EndDisabled();
            if (wantDelete && PtfxAuthor.RemoveEffect(Doc, eff))
            {
                SelectEffect(0);
                Status = "effect deleted";
                return true;
            }

            if (renameTarget_T6 == -2)
            {
                ImGui.SetNextItemWidth(-60);
                ImGui.InputText("##renameeff", ref renameBuf_T6, 48);
                ImGui.SameLine();
                if (ImGui.SmallButton("OK##renameeff"))
                {
                    PtfxAuthor.RenameEffect(Doc, eff, renameBuf_T6);
                    renameTarget_T6 = -1;
                    SelectByName_T6(PtfxAuthor.Clean(renameBuf_T6, eff.Name));
                    return true;
                }
            }

            var dur = eff.Rule.DurationMax;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##ptfxdur", ref dur, 0.25f, 30.0f, "length %.2f s"))
            {
                eff.Rule.DurationMin = eff.Rule.DurationMax = Math.Max(0.25f, dur);
                TouchFromTimeline(true);
            }
            return false;
        }

        private void DrawEmitters_T6(PtfxEffect eff)
        {
            ImGui.TextDisabled($"EMITTERS ({eff.Emitters.Count})");
            ImGui.SameLine();
            ImGui.BeginDisabled(eff.Emitters.Count >= 32);
            bool wantAdd = ImGui.SmallButton("+ emitter");
            ImGui.EndDisabled();
            if (wantAdd && PtfxAuthor.AddEmitter(Doc, eff, eff.Name + "_puff", null))
            {
                SelectByName_T6(eff.Name);
                SelectedEmitter = (EditedEffect?.Emitters.Count ?? 1) - 1;
                Status = "emitter added";
                return;
            }

            for (int i = 0; i < eff.Emitters.Count; i++)
            {
                var em = eff.Emitters[i];
                ImGui.PushID("t6em" + i);
                var open = ImGui.CollapsingHeader(em.Name + "##t6",
                    i == SelectedEmitter ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
                if (ImGui.IsItemClicked()) SelectedEmitter = i;
                if (open && DrawEmitter_T6(eff, em, i)) { ImGui.PopID(); return; }
                ImGui.PopID();
            }
        }

        private bool DrawEmitter_T6(PtfxEffect eff, PtfxEmitter em, int index)
        {
            var er = em.EmitterRule;
            var pr = em.ParticleRule;
            if (er == null || pr == null) { ImGui.TextDisabled("(no rule)"); return false; }

            if (ImGui.SmallButton("Rename")) { renameTarget_T6 = index; renameBuf_T6 = em.Name; }
            ImGui.SameLine();
            ImGui.BeginDisabled(eff.Emitters.Count <= 1);
            bool wantRemove = ImGui.SmallButton("Remove");
            ImGui.EndDisabled();
            if (wantRemove && PtfxAuthor.RemoveEmitter(Doc, eff, index))
            {
                SelectByName_T6(eff.Name);
                SelectedEmitter = 0;
                Status = "emitter removed";
                return true;
            }
            if (renameTarget_T6 == index)
            {
                ImGui.SetNextItemWidth(-60);
                ImGui.InputText("##renameem", ref renameBuf_T6, 48);
                ImGui.SameLine();
                if (ImGui.SmallButton("OK##renameem"))
                {
                    PtfxAuthor.SetRuleName(em.Event, renameBuf_T6);
                    PtfxAuthor.Rebuild(Doc);
                    renameTarget_T6 = -1;
                    SelectByName_T6(eff.Name);
                    return true;
                }
            }

            ImGui.TextDisabled("Shape particles are born in");
            var dom = er.CreationDomainObj;
            if (dom != null)
            {
                int kind = (int)dom.DomainType;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Combo("##t6shape", ref kind, "box\0sphere\0cylinder\0", 3))
                {
                    dom.DomainType = (ParticleDomainType)Math.Clamp(kind, 0, 2);
                    TouchFromTimeline(true);
                }
                if (Vec3Row_T6("size##t6", dom.SizeOuterKFP, 0.01f, 0f, 60f)) TouchFromTimeline(true);
                if (Vec3Row_T6("offset##t6", dom.PositionKFP, 0.01f, -60f, 60f)) TouchFromTimeline(true);
            }
            var tgt = er.TargetDomainObj;
            if (tgt != null && Vec3Row_T6("aimed at##t6", tgt.PositionKFP, 0.01f, -60f, 60f))
                TouchFromTimeline(true);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Particles leave the shape heading for this point, which is what\n" +
                                 "makes a plume go up and a spray go sideways.");

            ImGui.TextDisabled("Emission");
            bool oneShot = er.IsOneShot != 0;
            if (ImGui.Checkbox("Burst##t6", ref oneShot))
            {
                er.IsOneShot = (byte)(oneShot ? 1 : 0);
                TouchFromTimeline(true);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Off: the rate below is particles per second, forever.\n" +
                                 "On: it is how many come out in one go - an explosion, a puff of dust.");

            var rate = PtfxKeyframes.Find(er.KeyframeProps, "ptxemitterrule:m_spawnrateovertimekfp");
            if (MinMaxRow_T6(oneShot ? "count##t6" : "rate /s##t6", rate, 0.1f, 0f, 400f)) TouchFromTimeline(false);
            var life = PtfxKeyframes.Find(er.KeyframeProps, "ptxemitterrule:m_particlelifekfp");
            if (MinMaxRow_T6("life s##t6", life, 0.02f, 0.05f, 60f)) TouchFromTimeline(false);
            var speed = PtfxKeyframes.Find(er.KeyframeProps, "ptxemitterrule:m_speedscalarkfp");
            if (MinMaxRow_T6("speed m/s##t6", speed, 0.02f, -50f, 50f)) TouchFromTimeline(false);

            var ev = em.Event;
            if (ev != null)
            {
                var win = new Vector2(ev.StartRatio, ev.EndRatio <= 0f ? 1f : ev.EndRatio);
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat2("##t6window", ref win, 0.005f, 0f, 1f, "window %.2f"))
                {
                    ev.StartRatio = Math.Clamp(win.X, 0f, 1f);
                    ev.EndRatio = Math.Clamp(Math.Max(win.Y, win.X + 0.01f), 0f, 1f);
                    TouchFromTimeline(true);
                }
            }

            ImGui.TextDisabled("Look");
            DrawSheetPicker_T6(em, index);
            DrawSheetBlock_V29(em, PtfxAuthor.EmitterSheet(em), pr);
            var size = pr.AllBehaviours?.data_items?.OfType<ParticleBehaviourSize>().FirstOrDefault();
            if (size != null)
            {
                if (EndsRow_T6("size m##t6", size.WhdMinKFP, size.WhdMaxKFP, 0.01f, 0.005f, 40f))
                    TouchFromTimeline(false);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The sprite in metres at birth and at death - the two ends of the\n" +
                                     "curve the timeline lets you shape in between.");
            }
            var col = pr.AllBehaviours?.data_items?.OfType<ParticleBehaviourColour>().FirstOrDefault();
            if (col != null) DrawTint_T6(col);

            int blend = pr.BlendSet == 0 ? 0 : 1;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##t6blend", ref blend, "Alpha\0Additive\0", 2))
            {
                pr.BlendSet = blend;
                TouchFromTimeline(false);
            }

            DrawRules_T6(pr);
            return false;
        }

        private void DrawSheetPicker_T6(PtfxEmitter em, int index)
        {
            var cur = PtfxAuthor.EmitterSheet(em);
            if (ImGui.Button((cur?.Name ?? "(no sprite sheet)") + "##t6sheet", new Vector2(-1, 0)))
            {
                sheetPickerOpen_T6 = !sheetPickerOpen_T6;
                if (sheetPickerOpen_T6 && SheetLibrary_T6.Count == 0) RequestLoadSheets_T6 = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The sheet this emitter draws with. A .ypt carries its own textures,\n" +
                                 "so picking one out of a game asset copies it into yours.");
            if (!sheetPickerOpen_T6) return;

            ImGui.SetNextItemWidth(-70);
            ImGui.InputTextWithHint("##t6sheetasset", "a game .ypt (core, weap, veh...)", ref SheetAsset_T6, 48);
            ImGui.SameLine();
            if (ImGui.SmallButton("Load##t6sheets")) RequestLoadSheets_T6 = true;
            if (!string.IsNullOrEmpty(SheetLibraryNote_T6)) ImGui.TextDisabled(SheetLibraryNote_T6);

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##t6sheetsearch", "search sheets...", ref sheetSearch_T6, 48);
            if (ImGui.BeginChild("##t6sheetlist", new Vector2(0, 150.0f), ImGuiChildFlags.Borders))
            {
                if (ImGui.Selectable("Soft puff"))
                {
                    PtfxAuthor.SetSheet(Doc, em, PtfxAuthor.MakePuffSheet("rle_puff", 128));
                    texOverride.Remove(index);
                    TouchFromTimeline(true);
                }
                if (ImGui.Selectable("Import image..."))
                    RequestImportSheet_T6 = index;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(".dds, .png, .jpg or .bmp.\n\n" +
                                     "It is copied INTO this .ypt, so the effect carries its own texture\n" +
                                     "and needs nothing else installed alongside it.\n\n" +
                                     "A .dds keeps whatever compression it was authored with; a .png comes\n" +
                                     "in uncompressed, which looks right but makes a bigger file.\n\n" +
                                     "One image is one frame. For an animated sprite, import the grid and\n" +
                                     "set the frame count under the texture-animation behaviour.");
                foreach (var t in SheetLibrary_T6)
                {
                    if (t?.Name == null) continue;
                    if (sheetSearch_T6.Length > 0 &&
                        t.Name.IndexOf(sheetSearch_T6, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!ImGui.Selectable($"{t.Name}  {t.Width}x{t.Height}##t6s{t.Name}")) continue;
                    PtfxAuthor.SetSheet(Doc, em, t);
                    texOverride.Remove(index);
                    TouchFromTimeline(true);
                    Status = "sheet " + t.Name;
                }
            }
            ImGui.EndChild();
        }

        private void DrawRules_T6(ParticleRule pr)
        {
            var have = pr.AllBehaviours?.data_items?.Where(b => b != null).ToList()
                       ?? new List<ParticleBehaviour>();
            ImGui.TextDisabled($"RULES ({have.Count})");
            foreach (var b in have.ToList())
            {
                var label = b.Type.ToString();
                ImGui.BulletText(label);
                if (b.Type == ParticleBehaviourType.Sprite || b.Type == ParticleBehaviourType.Age ||
                    b.Type == ParticleBehaviourType.Velocity) continue;
                ImGui.SameLine();
                if (ImGui.SmallButton("x##t6r" + label) && PtfxAuthor.RemoveBehaviour(pr, b))
                {
                    TouchFromTimeline(true);
                    return;
                }
            }

            var missing = PtfxAuthor.AddableBehaviours
                .Where(t => !have.Any(b => b.Type == t)).ToArray();
            if (missing.Length == 0) return;
            addRuleChoice_T6 = Math.Clamp(addRuleChoice_T6, 0, missing.Length - 1);
            ImGui.SetNextItemWidth(-60);
            ImGui.Combo("##t6addrule", ref addRuleChoice_T6,
                        string.Join("\0", missing.Select(t => t.ToString())) + "\0", missing.Length);
            ImGui.SameLine();
            if (ImGui.SmallButton("+ rule") && PtfxAuthor.AddBehaviour(pr, missing[addRuleChoice_T6]) != null)
            {
                TouchFromTimeline(true);
                Status = missing[addRuleChoice_T6] + " added";
            }
        }

        private bool MinMaxRow_T6(string label, ParticleKeyframeProp p, float step, float lo, float hi)
        {
            if (p == null) return false;
            EnsureKey_T6(p);
            var kv = p.Values.data_items[0];
            var v = new Vector2(kv.KeyframeValue.X, kv.KeyframeValue.Y);
            ImGui.SetNextItemWidth(-1);
            if (!ImGui.DragFloat2("##" + label, ref v, step, lo, hi,
                                  label.Split(new[] { "##" }, StringSplitOptions.None)[0] + " %.2f")) return false;
            kv.KeyframeValue = new SDX.Vector4(v.X, Math.Max(v.Y, v.X), kv.KeyframeValue.Z, kv.KeyframeValue.W);
            return true;
        }

        private bool Vec3Row_T6(string label, ParticleKeyframeProp p, float step, float lo, float hi)
        {
            if (p == null) return false;
            EnsureKey_T6(p);
            var kv = p.Values.data_items[0];
            var v = new Vector3(kv.KeyframeValue.X, kv.KeyframeValue.Y, kv.KeyframeValue.Z);
            ImGui.SetNextItemWidth(-1);
            if (!ImGui.DragFloat3("##" + label, ref v, step, lo, hi,
                                  label.Split(new[] { "##" }, StringSplitOptions.None)[0] + " %.2f")) return false;
            kv.KeyframeValue = new SDX.Vector4(v.X, v.Y, v.Z, kv.KeyframeValue.W);
            return true;
        }

        private bool EndsRow_T6(string label, ParticleKeyframeProp min, ParticleKeyframeProp max,
                                float step, float lo, float hi)
        {
            if (min == null) return false;
            EnsureKey_T6(min);
            if (max != null) EnsureKey_T6(max);
            var first = min.Values.data_items[0];
            var last = min.Values.data_items[min.Values.data_items.Length - 1];
            var v = new Vector2(first.KeyframeValue.X, last.KeyframeValue.X);
            ImGui.SetNextItemWidth(-1);
            if (!ImGui.DragFloat2("##" + label, ref v, step, lo, hi,
                                  label.Split(new[] { "##" }, StringSplitOptions.None)[0] + " %.3f")) return false;
            SetEnds_T6(min, v.X, v.Y, 1.0f);
            SetEnds_T6(max, v.X, v.Y, 1.3f);
            return true;
        }

        private static void SetEnds_T6(ParticleKeyframeProp p, float start, float end, float spread)
        {
            var vals = p?.Values?.data_items;
            if (vals == null || vals.Length == 0) return;
            for (int i = 0; i < vals.Length; i++)
            {
                float f = vals.Length == 1 ? 0f : i / (float)(vals.Length - 1);
                float w = (start + (end - start) * f) * spread;
                var kv = vals[i].KeyframeValue;
                vals[i].KeyframeValue = new SDX.Vector4(w, w, kv.Z, kv.W);
            }
        }

        private void DrawTint_T6(ParticleBehaviourColour col)
        {
            var vals = col.RGBAMinKFP?.Values?.data_items;
            if (vals == null || vals.Length == 0) return;
            var c0 = vals[0].KeyframeValue;
            var rgb = new Vector3(c0.X, c0.Y, c0.Z);
            if (!ImGui.ColorEdit3("colour##t6", ref rgb, ImGuiColorEditFlags.Float)) return;
            void Apply(ParticleKeyframeProp p, float boost)
            {
                var v = p?.Values?.data_items;
                if (v == null) return;
                for (int i = 0; i < v.Length; i++)
                {
                    var kv = v[i].KeyframeValue;
                    v[i].KeyframeValue = new SDX.Vector4(
                        Math.Min(1f, rgb.X * boost), Math.Min(1f, rgb.Y * boost), Math.Min(1f, rgb.Z * boost), kv.W);
                }
            }
            Apply(col.RGBAMinKFP, 1.0f);
            Apply(col.RGBAMaxKFP, 1.25f);
            TouchFromTimeline(false);
        }

        private static void EnsureKey_T6(ParticleKeyframeProp p)
        {
            p.Values ??= new ResourceSimpleList64<ParticleKeyframePropValue>();
            if (p.Values.data_items != null && p.Values.data_items.Length > 0) return;
            p.Values.data_items = new[]
            {
                new ParticleKeyframePropValue { KeyframeTime = SDX.Vector4.Zero, KeyframeValue = SDX.Vector4.Zero },
            };
        }

        private void SelectByName_T6(string name)
        {
            if (Doc == null || name == null) return;
            for (int i = 0; i < Doc.Effects.Count; i++)
                if (string.Equals(Doc.Effects[i].Name, name, StringComparison.OrdinalIgnoreCase))
                { SelectEffect(i); return; }
        }
    }
}

