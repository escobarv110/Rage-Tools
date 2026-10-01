using System;
using System.Linq;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void SeqTest_Particles_U21(Action<string, bool, string> check)
        {
            try
            {
                if (gameFiles?.Cache == null || !gameFiles.Ready) { Console.WriteLine("  u21 particles: (skipped - no game files)"); return; }
                var list = Ptfx?.GameYpts ?? PtfxGameIndex.ListYpts(gameFiles);
                PtfxDocument doc = null;
                foreach (var e in list)
                {
                    var d = PtfxDocument.FromGame(gameFiles, e.Name, e.Path);
                    int prn = d?.PtxList?.ParticleRuleDictionary?.ParticleRules?.data_items?.Length ?? 0;
                    int emn = d?.PtxList?.EmitterRuleDictionary?.EmitterRules?.data_items?.Length ?? 0;
                    if (prn >= 4 && emn >= 4 && d.Effects.Count >= 3) { doc = d; break; }
                }
                if (doc == null) { check("u21 particles: a game .ypt with several rules was found", false, "none"); return; }

                var pl = doc.PtxList;
                int prBefore = pl.ParticleRuleDictionary.ParticleRules.data_items.Length;
                int emBefore = pl.EmitterRuleDictionary.EmitterRules.data_items.Length;
                int txBefore = pl.TextureDictionary?.Textures?.data_items?.Length ?? 0;
                var prNames = pl.ParticleRuleDictionary.ParticleRules.data_items.Select(r => r?.Name?.Value).OrderBy(x => x).ToList();
                var prHashes = pl.ParticleRuleDictionary.ParticleRuleNameHashes.data_items.Select(h => h.Hash).ToList();

                check("u21 particles: a loaded rule carries its name hash",
                      pl.ParticleRuleDictionary.ParticleRules.data_items.All(r => r == null || r.NameHash.Hash != 0) &&
                      pl.EmitterRuleDictionary.EmitterRules.data_items.All(r => r == null || r.NameHash.Hash != 0), doc.Name);
                var rules = pl.ParticleRuleDictionary.ParticleRules.data_items;
                bool sameKeys = true;
                for (int i = 0; i < rules.Length; i++) if (rules[i] != null && rules[i].NameHash.Hash != prHashes[i]) sameKeys = false;
                check("u21 particles: the hash is the one the file's dictionary uses", sameKeys, doc.Name);

                var eff = doc.Effects[0];
                PtfxAuthor.RenameEffect(doc, eff, (eff.Rule?.Name?.Value ?? "fx") + "_u21");
                PtfxAuthor.Rebuild(doc);
                var bytes = doc.Ypt.Save();
                var back = new YptFile();
                back.Load(bytes);
                var bl = back.PtfxList;
                int prAfter = bl?.ParticleRuleDictionary?.ParticleRules?.data_items?.Length ?? 0;
                int emAfter = bl?.EmitterRuleDictionary?.EmitterRules?.data_items?.Length ?? 0;
                int txAfter = bl?.TextureDictionary?.Textures?.data_items?.Length ?? 0;
                var namesAfter = (bl?.ParticleRuleDictionary?.ParticleRules?.data_items ?? Array.Empty<ParticleRule>()).Select(r => r?.Name?.Value).OrderBy(x => x).ToList();
                check("u21 particles: after an edit and a save every particle rule is still in the file",
                      prAfter == prBefore && namesAfter.SequenceEqual(prNames), $"{doc.Name}: {prBefore} -> {prAfter}");
                check("u21 particles: ...and every emitter rule", emAfter == emBefore, $"{emBefore} -> {emAfter}");
                check("u21 particles: ...and every texture", txAfter == txBefore, $"{txBefore} -> {txAfter}");
                check("u21 particles: no rule is filed under a zero hash",
                      (bl?.ParticleRuleDictionary?.ParticleRuleNameHashes?.data_items ?? Array.Empty<MetaHash>()).All(h => h.Hash != 0) &&
                      (bl?.EmitterRuleDictionary?.EmitterRuleNameHashes?.data_items ?? Array.Empty<MetaHash>()).All(h => h.Hash != 0), "");
                check("u21 particles: the renamed effect is saved under its new name",
                      (bl?.EffectRuleDictionary?.EffectRules?.data_items ?? Array.Empty<ParticleEffectRule>()).Any(r => r?.Name?.Value?.EndsWith("_u21") == true), "");
            }
            catch (Exception ex) { check("u21 particles: no exception", false, ex.ToString()); }
        }
    }
}
