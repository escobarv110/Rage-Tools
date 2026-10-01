using System;
using System.Collections.Generic;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public static partial class PtfxAuthor
    {
        public static uint RuleHash_U21(ParticleRule r)
        {
            if (r == null) return 0;
            if (r.NameHash.Hash == 0 && !string.IsNullOrEmpty(r.Name?.Value)) r.NameHash = JenkHash.GenHash(r.Name.Value);
            return r.NameHash.Hash;
        }

        public static uint RuleHash_U21(ParticleEmitterRule r)
        {
            if (r == null) return 0;
            if (r.NameHash.Hash == 0 && !string.IsNullOrEmpty(r.Name?.Value)) r.NameHash = JenkHash.GenHash(r.Name.Value);
            return r.NameHash.Hash;
        }

        private static void KeepExistingRules_U21(ParticleEffectsList list, Dictionary<uint, ParticleEmitterRule> emrs,
                                                  Dictionary<uint, ParticleRule> prs, List<Texture> texs, HashSet<uint> texSeen)
        {
            var oldEm = list.EmitterRuleDictionary?.EmitterRules?.data_items;
            var oldEmH = list.EmitterRuleDictionary?.EmitterRuleNameHashes?.data_items;
            if (oldEm != null)
                for (int i = 0; i < oldEm.Length; i++)
                {
                    var r = oldEm[i];
                    if (r == null) continue;
                    if (r.NameHash.Hash == 0 && oldEmH != null && i < oldEmH.Length && oldEmH[i].Hash != 0) r.NameHash = oldEmH[i];
                    uint h = RuleHash_U21(r);
                    if (h != 0 && !emrs.ContainsKey(h) && !emrs.ContainsValue(r)) emrs[h] = r;
                }
            var oldPr = list.ParticleRuleDictionary?.ParticleRules?.data_items;
            var oldPrH = list.ParticleRuleDictionary?.ParticleRuleNameHashes?.data_items;
            if (oldPr != null)
                for (int i = 0; i < oldPr.Length; i++)
                {
                    var r = oldPr[i];
                    if (r == null) continue;
                    if (r.NameHash.Hash == 0 && oldPrH != null && i < oldPrH.Length && oldPrH[i].Hash != 0) r.NameHash = oldPrH[i];
                    uint h = RuleHash_U21(r);
                    if (h != 0 && !prs.ContainsKey(h) && !prs.ContainsValue(r)) prs[h] = r;
                }
            var oldTex = list.TextureDictionary?.Textures?.data_items;
            if (oldTex != null)
                foreach (var t in oldTex)
                {
                    if (t == null) continue;
                    var h = JenkHash.GenHash(t.Name?.ToLowerInvariant() ?? "");
                    if (texSeen.Add(h)) texs.Add(t);
                }
        }
    }
}
