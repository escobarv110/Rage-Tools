using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeWalker.GameFiles;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public static partial class PtfxAuthor
    {
        private const uint VftEffectRule = 0x4060BD18, VftTimeline = 0x40610408, VftEvent = 0x40610858;
        private const uint VftEmitterRule = 0x4060C498, VftDomain = 0x4060D968;
        private const uint VftParticleRule = 0x4060CAC8, VftShaderInst = 0x40607C70, VftTechDesc = 0x40607B68;
        private const uint VftSpawner = 0x40610280;
        private const uint VftVarVector = 0x4060FA58, VftVarKeyframe = 0x4060F9E8, VftVarTexture = 0x4060FB08;

        public const string SpriteShader = "ptfx_sprite";
        public const string SpriteTechnique = "RGBA_lit_soft";
        private const uint SpriteTechniqueId = 5;
        private const uint SpriteShaderHash = 0x0EB0D762;
        private const uint VarDiffuseTex = 0x0DF47048;

        private static readonly Dictionary<ParticleBehaviourType, uint> BehaviourVft =
            new Dictionary<ParticleBehaviourType, uint>
            {
                { ParticleBehaviourType.Age,            0x406084D8 },
                { ParticleBehaviourType.Acceleration,   0x40608368 },
                { ParticleBehaviourType.Velocity,       0x4060A348 },
                { ParticleBehaviourType.Rotation,       0x40609E68 },
                { ParticleBehaviourType.Size,           0x4060A198 },
                { ParticleBehaviourType.Dampening,      0x406092B8 },
                { ParticleBehaviourType.Wind,           0x4060A5F8 },
                { ParticleBehaviourType.AnimateTexture, 0x406087E8 },
                { ParticleBehaviourType.Colour,         0x40609018 },
                { ParticleBehaviourType.Noise,          0x40609798 },
                { ParticleBehaviourType.Sprite,         0x4060A9D8 },
            };

        public static readonly ParticleBehaviourType[] AddableBehaviours =
        {
            ParticleBehaviourType.Acceleration,
            ParticleBehaviourType.Dampening,
            ParticleBehaviourType.Rotation,
            ParticleBehaviourType.Size,
            ParticleBehaviourType.Colour,
            ParticleBehaviourType.Wind,
            ParticleBehaviourType.Noise,
            ParticleBehaviourType.AnimateTexture,
        };

        public static PtfxDocument NewDocument(string assetName, string effectName, Texture sheet)
        {
            assetName = Clean(assetName, "my_particles");
            effectName = Clean(effectName, "my_effect");
            sheet ??= MakePuffSheet("rle_puff", 128);

            var list = new ParticleEffectsList
            {
                Name = (string_r)assetName,
                TextureDictionary = new TextureDictionary(),
                ParticleRuleDictionary = new ParticleRuleDictionary(),
                EmitterRuleDictionary = new ParticleEmitterRuleDictionary(),
                EffectRuleDictionary = new ParticleEffectRuleDictionary(),
            };

            var ypt = new YptFile { PtfxList = list, Name = assetName + ".ypt", Loaded = true };
            var doc = new PtfxDocument { Name = assetName + ".ypt", Ypt = ypt, Dirty = true };

            var rule = NewEffectRule(effectName);
            list.EffectRuleDictionary.EffectRules = new ResourcePointerList64<ParticleEffectRule>
            {
                data_items = new[] { rule },
            };
            AddEmitterRule(rule, effectName + "_puff", sheet);
            Rebuild(doc);
            return doc;
        }

        public static PtfxEffect AddEffect(PtfxDocument doc, string effectName, Texture sheet)
        {
            if (doc?.PtxList == null) return null;
            effectName = UniqueName(Clean(effectName, "my_effect"), doc.Effects.Select(e => e.Name));
            sheet ??= FirstSheet(doc) ?? MakePuffSheet("rle_puff", 128);

            var rule = NewEffectRule(effectName);
            AddEmitterRule(rule, effectName + "_puff", sheet);

            var dict = doc.PtxList.EffectRuleDictionary ??= new ParticleEffectRuleDictionary();
            var rules = (dict.EffectRules?.data_items ?? Array.Empty<ParticleEffectRule>()).ToList();
            rules.Add(rule);
            dict.EffectRules = new ResourcePointerList64<ParticleEffectRule> { data_items = rules.ToArray() };
            Rebuild(doc);
            return doc.Effects.FirstOrDefault(e => ReferenceEquals(e.Rule, rule));
        }

        public static bool RemoveEffect(PtfxDocument doc, PtfxEffect eff)
        {
            var dict = doc?.PtxList?.EffectRuleDictionary;
            var rules = dict?.EffectRules?.data_items;
            if (rules == null || eff == null || rules.Length <= 1) return false;
            var keep = rules.Where(r => !ReferenceEquals(r, eff.Rule)).ToArray();
            if (keep.Length == rules.Length) return false;
            dict.EffectRules = new ResourcePointerList64<ParticleEffectRule> { data_items = keep };
            Rebuild(doc);
            return true;
        }

        public static bool AddEmitter(PtfxDocument doc, PtfxEffect eff, string name, Texture sheet)
        {
            if (doc == null || eff?.Rule == null) return false;
            if (eff.Emitters.Count >= 32) return false;
            name = UniqueName(Clean(name, eff.Name + "_puff"), AllRuleNames(doc));
            AddEmitterRule(eff.Rule, name, sheet ?? EmitterSheet(eff) ?? FirstSheet(doc) ?? MakePuffSheet("rle_puff", 128));
            Rebuild(doc);
            return true;
        }

        public static bool RemoveEmitter(PtfxDocument doc, PtfxEffect eff, int index)
        {
            var evs = eff?.Rule?.EventEmitters?.data_items;
            if (doc == null || evs == null) return false;
            var live = evs.Where(e => e != null).ToList();
            if (index < 0 || index >= live.Count || live.Count <= 1) return false;
            live.RemoveAt(index);
            SetEvents(eff.Rule, live);
            Rebuild(doc);
            return true;
        }

        private static void SetEvents(ParticleEffectRule rule, List<ParticleEventEmitter> live)
        {
            var arr = new ParticleEventEmitter[32];
            for (int i = 0; i < live.Count && i < 32; i++) { arr[i] = live[i]; arr[i].Index = (uint)i; }
            rule.EventEmitters = new ResourcePointerArray64<ParticleEventEmitter> { data_items = arr };
            rule.EventEmittersCount = (ushort)Math.Min(live.Count, 32);
            rule.EventEmittersCapacity = 32;
        }

        public static ParticleBehaviour AddBehaviour(ParticleRule pr, ParticleBehaviourType type)
        {
            if (pr == null) return null;
            var have = pr.AllBehaviours?.data_items?.Where(b => b != null).ToList()
                       ?? new List<ParticleBehaviour>();
            if (have.Any(b => b.Type == type)) return null;
            var b2 = MakeBehaviour(type);
            if (b2 == null) return null;
            var at = have.FindIndex(b => b.Type == ParticleBehaviourType.Sprite);
            if (at < 0) have.Add(b2); else have.Insert(at, b2);
            pr.BuildBehaviours(have);
            return b2;
        }

        public static bool RemoveBehaviour(ParticleRule pr, ParticleBehaviour b)
        {
            if (pr == null || b == null) return false;
            if (b.Type == ParticleBehaviourType.Sprite) return false;
            var have = pr.AllBehaviours?.data_items?.Where(x => x != null && !ReferenceEquals(x, b)).ToList();
            if (have == null) return false;
            pr.BuildBehaviours(have);
            return true;
        }

        public static void SetSheet(PtfxDocument doc, PtfxEmitter em, Texture sheet)
        {
            var pr = em?.ParticleRule;
            if (doc?.PtxList == null || pr == null || sheet == null) return;
            var mine = AddTexture(doc, sheet);
            var svs = pr.ShaderVars?.data_items;
            ParticleShaderVarTexture slot = null;
            if (svs != null)
                foreach (var sv in svs)
                    if (sv is ParticleShaderVarTexture t && t.Name.Hash == VarDiffuseTex) { slot = t; break; }
            if (slot == null)
            {
                slot = TexVar(VarDiffuseTex, 4);
                var l = (svs ?? Array.Empty<ParticleShaderVar>()).ToList();
                l.Add(slot);
                pr.ShaderVars = new ResourcePointerList64<ParticleShaderVar> { data_items = l.ToArray() };
            }
            slot.Texture = mine;
            slot.ExternalReference = 0;
            slot.TextureName = (string_r)mine.Name;
            slot.TextureNameHash = JenkHash.GenHash(mine.Name?.ToLowerInvariant() ?? "");
            doc.Dirty = true;
        }

        public static Texture AddTexture(PtfxDocument doc, Texture src)
        {
            var dict = doc?.PtxList?.TextureDictionary;
            if (dict == null || src == null) return null;
            var have = (dict.Textures?.data_items ?? Array.Empty<Texture>()).Where(t => t != null).ToList();
            foreach (var t in have)
                if (string.Equals(t.Name, src.Name, StringComparison.OrdinalIgnoreCase)) return t;
            var copy = CopyTexture(src);
            have.Add(copy);
            dict.BuildFromTextureList(have);
            return copy;
        }

        public static Texture CopyTexture(Texture src)
        {
            if (src == null) return null;
            return new Texture
            {
                Name = src.Name,
                NameHash = src.NameHash,
                Width = src.Width,
                Height = src.Height,
                Depth = src.Depth,
                Stride = src.Stride,
                Format = src.Format,
                Levels = src.Levels,
                Usage = src.Usage,
                UsageFlags = src.UsageFlags,
                ExtraFlags = src.ExtraFlags,
                Data = src.Data == null ? null : new TextureData { FullData = src.Data.FullData },
            };
        }

        public static Texture MakePuffSheet(string name, int size)
        {
            size = Math.Clamp(size, 8, 512);
            var px = new byte[size * size * 4];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float r = MathF.Sqrt(dx * dx + dy * dy);
                    float a = Math.Clamp(1.0f - r, 0f, 1f);
                    a = a * a * (3f - 2f * a);
                    float n = 0.88f + 0.12f * MathF.Sin(x * 0.7f) * MathF.Cos(y * 0.55f);
                    byte v = (byte)Math.Clamp(255f * n, 0f, 255f);
                    int i = (y * size + x) * 4;
                    px[i + 0] = v; px[i + 1] = v; px[i + 2] = v;
                    px[i + 3] = (byte)Math.Clamp(255f * a, 0f, 255f);
                }

            var dds = BuildDds(size, size, px);
            var tex = CodeWalker.Utils.DDSIO.GetTexture(dds);
            if (tex == null) return null;
            tex.Name = name;
            tex.NameHash = JenkHash.GenHash(name.ToLowerInvariant());
            return tex;
        }

        private static byte[] BuildDds(int w, int h, byte[] bgra)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(0x20534444u);
            bw.Write(124u);
            bw.Write(0x0000100Fu);
            bw.Write((uint)h);
            bw.Write((uint)w);
            bw.Write((uint)(w * 4));
            bw.Write(0u);
            bw.Write(1u);
            for (int i = 0; i < 11; i++) bw.Write(0u);
            bw.Write(32u);
            bw.Write(4u);
            bw.Write(0x30315844u);
            for (int i = 0; i < 5; i++) bw.Write(0u);
            bw.Write(0x1000u);
            for (int i = 0; i < 4; i++) bw.Write(0u);
            bw.Write(87u);
            bw.Write(3u);
            bw.Write(0u);
            bw.Write(1u);
            bw.Write(0u);
            bw.Write(bgra);
            bw.Flush();
            return ms.ToArray();
        }

        public static void RenameEffect(PtfxDocument doc, PtfxEffect eff, string name)
        {
            if (doc == null || eff?.Rule == null) return;
            var old = eff.Name;
            name = UniqueName(Clean(name, "my_effect"), doc.Effects.Where(e => !ReferenceEquals(e, eff)).Select(e => e.Name));
            if (string.Equals(old, name, StringComparison.Ordinal)) return;
            eff.Rule.Name = (string_r)name;
            eff.Rule.NameHash = JenkHash.GenHash(name.ToLowerInvariant());
            foreach (var ev in eff.Rule.EventEmitters?.data_items ?? Array.Empty<ParticleEventEmitter>())
            {
                if (ev == null) continue;
                RenameRule(ev, ev.EmitterRuleName?.Value, old, name);
            }
            Rebuild(doc);
        }

        private static void RenameRule(ParticleEventEmitter ev, string ruleName, string oldPrefix, string newPrefix)
        {
            if (string.IsNullOrEmpty(ruleName) || string.IsNullOrEmpty(oldPrefix)) return;
            if (!ruleName.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase)) return;
            var name = newPrefix + ruleName.Substring(oldPrefix.Length);
            SetRuleName(ev, name);
        }

        public static void SetRuleName(ParticleEventEmitter ev, string name)
        {
            if (ev == null) return;
            name = Clean(name, "emitter");
            var hash = JenkHash.GenHash(name.ToLowerInvariant());
            ev.EmitterRuleName = (string_r)name;
            ev.ParticleRuleName = (string_r)name;
            if (ev.EmitterRule != null) { ev.EmitterRule.Name = (string_r)name; ev.EmitterRule.NameHash = hash; }
            if (ev.ParticleRule != null) { ev.ParticleRule.Name = (string_r)name; ev.ParticleRule.NameHash = hash; }
        }

        public static string Clean(string s, string fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            var chars = s.Trim().ToLowerInvariant()
                         .Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
            var r = new string(chars).Trim('_');
            return string.IsNullOrEmpty(r) ? fallback : r;
        }

        private static string UniqueName(string want, IEnumerable<string> taken)
        {
            var set = new HashSet<string>(taken ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!set.Contains(want)) return want;
            for (int i = 2; i < 1000; i++)
                if (!set.Contains(want + "_" + i)) return want + "_" + i;
            return want + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        private static IEnumerable<string> AllRuleNames(PtfxDocument doc)
        {
            foreach (var e in doc.Effects)
                foreach (var em in e.Emitters)
                    yield return em.Name;
        }

        public static void Rebuild(PtfxDocument doc)
        {
            var list = doc?.PtxList;
            var effects = list?.EffectRuleDictionary?.EffectRules?.data_items;
            if (list == null || effects == null) return;

            var emrs = new Dictionary<uint, ParticleEmitterRule>();
            var prs = new Dictionary<uint, ParticleRule>();
            var texs = new List<Texture>();
            var texSeen = new HashSet<uint>();

            foreach (var er in effects)
            {
                if (er == null) continue;
                if (er.Name != null) er.NameHash = JenkHash.GenHash(er.Name.Value?.ToLowerInvariant() ?? "");
                var live = (er.EventEmitters?.data_items ?? Array.Empty<ParticleEventEmitter>())
                           .Where(e => e != null).ToList();
                SetEvents(er, live);
                foreach (var ev in live)
                {
                    if (ev.EmitterRule != null) emrs[RuleHash_U21(ev.EmitterRule)] = ev.EmitterRule;
                    if (ev.ParticleRule == null) continue;
                    prs[RuleHash_U21(ev.ParticleRule)] = ev.ParticleRule;
                    foreach (var sv in ev.ParticleRule.ShaderVars?.data_items ?? Array.Empty<ParticleShaderVar>())
                    {
                        if (!(sv is ParticleShaderVarTexture t) || t.Texture == null) continue;
                        var h = JenkHash.GenHash(t.Texture.Name?.ToLowerInvariant() ?? "");
                        if (texSeen.Add(h)) texs.Add(t.Texture);
                    }
                }
            }

            KeepExistingRules_U21(list, emrs, prs, texs, texSeen);

            var emrList = emrs.Values.OrderBy(r => r.NameHash.Hash).ToArray();
            list.EmitterRuleDictionary ??= new ParticleEmitterRuleDictionary();
            list.EmitterRuleDictionary.EmitterRuleNameHashes = new ResourceSimpleList64_s<MetaHash>
            { data_items = emrList.Select(r => r.NameHash).ToArray() };
            list.EmitterRuleDictionary.EmitterRules = new ResourcePointerList64<ParticleEmitterRule>
            { data_items = emrList };

            var prList = prs.Values.OrderBy(r => r.NameHash.Hash).ToArray();
            list.ParticleRuleDictionary ??= new ParticleRuleDictionary();
            list.ParticleRuleDictionary.ParticleRuleNameHashes = new ResourceSimpleList64_s<MetaHash>
            { data_items = prList.Select(r => r.NameHash).ToArray() };
            list.ParticleRuleDictionary.ParticleRules = new ResourcePointerList64<ParticleRule>
            { data_items = prList };

            var effList = effects.Where(e => e != null).OrderBy(e => e.NameHash.Hash).ToArray();
            list.EffectRuleDictionary.EffectRuleNameHashes = new ResourceSimpleList64_s<MetaHash>
            { data_items = effList.Select(e => e.NameHash).ToArray() };
            list.EffectRuleDictionary.EffectRules = new ResourcePointerList64<ParticleEffectRule>
            { data_items = effList };

            list.TextureDictionary ??= new TextureDictionary();
            list.TextureDictionary.BuildFromTextureList(texs);

            doc.BuildTree();
            doc.Dirty = true;
        }

        private static ParticleEffectRule NewEffectRule(string name)
        {
            var rule = new ParticleEffectRule
            {
                VFT = VftEffectRule,
                VFT2 = VftTimeline,
                RefCount = 1,
                FileVersion = 4.2f,
                EffectList = 0x0000000050000000,
                Name = (string_r)name,
                NameHash = JenkHash.GenHash(name.ToLowerInvariant()),
                NumLoops = -1,
                HasNoShadows = 1,
                PreUpdateTimeInterval = 0.25f,
                DurationMin = 4.0f,
                DurationMax = 4.0f,
                PlaybackRateScalarMin = 1.0f,
                PlaybackRateScalarMax = 1.0f,
                ViewportCullingMode = 3,
                DistanceCullingMode = 4,
                UpdateWhenDistanceCulled = 1,
                ViewportCullingSphereRadius = 4.0f,
                padding02 = 0x7f800001,
                DistanceCullingFadeDist = 500.0f,
                DistanceCullingCullDist = 600.0f,
                LodEvoDistanceMin = 100.0f,
                LodEvoDistanceMax = 250.0f,
                ZoomLevel = 100.0f,
                ColourTintMinKFP = Kfp("ptxEffectRule:m_colourTintMinKFP", (0f, new SDX.Vector4(1, 1, 1, 1))),
                ColourTintMaxKFP = Kfp("ptxEffectRule:m_colourTintMaxKFP", (0f, new SDX.Vector4(1, 1, 1, 1))),
                ZoomScalarKFP = Kfp("ptxEffectRule:m_zoomScalarKFP", (0f, new SDX.Vector4(1, 1, 1, 0))),
                DataSphereKFP = Kfp("ptxEffectRule:m_dataSphereKFP"),
                DataCapsuleKFP = Kfp("ptxEffectRule:m_dataCapsuleKFP"),
                EvolutionList = NewEvolutionList(),
            };
            var kfps = new ParticleKeyframeProp[16];
            kfps[0] = rule.ColourTintMinKFP; kfps[1] = rule.ColourTintMaxKFP; kfps[2] = rule.ZoomScalarKFP;
            kfps[3] = rule.DataSphereKFP; kfps[4] = rule.DataCapsuleKFP;
            rule.KeyframeProps = new ResourcePointerArray64<ParticleKeyframeProp>
            { data_items = kfps, ManualReferenceOverride = true };
            rule.KeyframePropsCount = 5;
            rule.KeyframePropsCapacity = 16;
            SetEvents(rule, new List<ParticleEventEmitter>());
            return rule;
        }

        private static ParticleEvolutionList NewEvolutionList() => new ParticleEvolutionList
        {
            Evolutions = new ResourceSimpleList64<ParticleEvolutions> { data_items = Array.Empty<ParticleEvolutions>() },
            EvolvedKeyframeProps = new ResourceSimpleList64<ParticleEvolvedKeyframeProps> { data_items = Array.Empty<ParticleEvolvedKeyframeProps>() },
            EvolvedKeyframePropMap = new ResourceSimpleList64<ParticleEvolvedKeyframePropMap> { data_items = Array.Empty<ParticleEvolvedKeyframePropMap>() },
        };

        private static void AddEmitterRule(ParticleEffectRule eff, string name, Texture sheet)
        {
            var ev = new ParticleEventEmitter
            {
                VFT = VftEvent,
                EventType = 0,
                StartRatio = 0f,
                EndRatio = 1f,
                PlaybackRateScalarMin = 1f,
                PlaybackRateScalarMax = 1f,
                ZoomScalarMin = 1f,
                ZoomScalarMax = 1f,
                ColourTintMin = 0xFFFFFFFF,
                ColourTintMax = 0xFFFFFFFF,
                EvolutionList = NewEvolutionList(),
                EmitterRule = NewEmitterRule(name),
                ParticleRule = NewParticleRule(name, sheet),
            };
            SetRuleName(ev, name);
            var live = (eff.EventEmitters?.data_items ?? Array.Empty<ParticleEventEmitter>())
                       .Where(e => e != null).ToList();
            live.Add(ev);
            SetEvents(eff, live);
        }

        private static ParticleEmitterRule NewEmitterRule(string name)
        {
            var er = new ParticleEmitterRule
            {
                VFT = VftEmitterRule,
                RefCount = 1,
                FileVersion = 4.1f,
                Name = (string_r)name,
                NameHash = JenkHash.GenHash(name.ToLowerInvariant()),
                IsOneShot = 0,
            };
            er.KeyframeProps = new[]
            {
                Kfp("ptxEmitterRule:m_spawnRateOverTimeKFP", (0f, new SDX.Vector4(14, 18, 0, 0))),
                Kfp("ptxEmitterRule:m_spawnRateOverDistKFP"),
                Kfp("ptxEmitterRule:m_particleLifeKFP", (0f, new SDX.Vector4(1.8f, 2.6f, 0.01f, 0.01f))),
                Kfp("ptxEmitterRule:m_playbackRateScalarKFP", (0f, new SDX.Vector4(1, 0.01f, 0.01f, 0.01f))),
                Kfp("ptxEmitterRule:m_speedScalarKFP", (0f, new SDX.Vector4(0.9f, 1.3f, 0, 0))),
                Kfp("ptxEmitterRule:m_sizeScalarKFP", (0f, new SDX.Vector4(1, 1, 1, 0))),
                Kfp("ptxEmitterRule:m_accnScalarKFP", (0f, new SDX.Vector4(1, 1, 1, 0))),
                Kfp("ptxEmitterRule:m_dampeningScalarKFP", (0f, new SDX.Vector4(1, 1, 1, 0))),
                Kfp("ptxEmitterRule:m_matrixWeightScalarKFP"),
                Kfp("ptxEmitterRule:m_inheritVelocityKFP"),
            };
            er.KeyframePropList = new ResourcePointerArray64<ParticleKeyframeProp>
            { data_items = er.KeyframeProps, ManualReferenceOverride = true };
            er.KeyframePropsCount1 = er.KeyframePropsCount2 = 10;

            er.CreationDomainObj = NewDomain(0, "ptxCreationDomain",
                new SDX.Vector4(0, 0, 0, 0), new SDX.Vector4(0.12f, 0.12f, 0.12f, 0));
            er.TargetDomainObj = NewDomain(1, "ptxTargetDomain",
                new SDX.Vector4(0, 0, 0.5f, 0), new SDX.Vector4(0.25f, 0.25f, 0.25f, 0));
            return er;
        }

        private static ParticleDomain NewDomain(uint index, string owner, SDX.Vector4 pos, SDX.Vector4 outer)
        {
            var d = new ParticleDomainSphere
            {
                VFT = VftDomain,
                Index = index,
                DomainType = ParticleDomainType.Sphere,
                IsWorldSpace = 1,
                FileVersion = 2.1f,
                PositionKFP = Kfp(owner + ":m_positionKFP", (0f, pos)),
                RotationKFP = Kfp(owner + ":m_rotationKFP", (0f, new SDX.Vector4(90, 0, 0, 0))),
                SizeOuterKFP = Kfp(owner + ":m_sizeOuterKFP", (0f, outer)),
                SizeInnerKFP = Kfp(owner + ":m_sizeInnerKFP", (0f, SDX.Vector4.Zero)),
            };
            var items = new ParticleKeyframeProp[16];
            items[0] = d.PositionKFP; items[1] = d.RotationKFP; items[2] = d.SizeInnerKFP; items[3] = d.SizeOuterKFP;
            d.KeyframeProps = new ResourcePointerList64<ParticleKeyframeProp> { data_items = items };
            return d;
        }

        private static ParticleRule NewParticleRule(string name, Texture sheet)
        {
            var pr = new ParticleRule
            {
                VFT = VftParticleRule,
                VFT2 = VftShaderInst,
                VFT3 = VftTechDesc,
                RefCount = 1,
                Name = (string_r)name,
                NameHash = JenkHash.GenHash(name.ToLowerInvariant()),
                FileVersion = 1.0f,
                CullMode = 2,
                BlendSet = 0,
                LightingMode = 2,
                DepthWrite = 0,
                DepthTest = 1,
                AlphaBlend = 1,
                TexFrameIDMin = 0,
                TexFrameIDMax = 0,
                ShaderFile = (string_r)SpriteShader,
                ShaderTechnique = (string_r)SpriteTechnique,
                ShaderTemplateTechniqueID = SpriteTechniqueId,
                ShaderTemplateHashName = SpriteShaderHash,
                IsLit = 1,
                IsSoft = 1,
                IsDataInSync = 1,
                SortType = 3,
                DrawType = 0,
                EffectSpawnerAtRatio = NewSpawner(),
                EffectSpawnerOnCollision = NewSpawner(),
                BiasLinks = new ResourceSimpleList64<ParticleRuleBiasLink> { data_items = Array.Empty<ParticleRuleBiasLink>() },
                Drawables = new ResourceSimpleList64<ParticleDrawable> { data_items = Array.Empty<ParticleDrawable>() },
            };

            pr.BuildBehaviours(new List<ParticleBehaviour>
            {
                MakeBehaviour(ParticleBehaviourType.Age),
                MakeBehaviour(ParticleBehaviourType.Acceleration),
                MakeBehaviour(ParticleBehaviourType.Velocity),
                MakeBehaviour(ParticleBehaviourType.Rotation),
                MakeBehaviour(ParticleBehaviourType.Size),
                MakeBehaviour(ParticleBehaviourType.Dampening),
                MakeBehaviour(ParticleBehaviourType.Colour),
                MakeBehaviour(ParticleBehaviourType.Wind),
                MakeBehaviour(ParticleBehaviourType.Sprite),
            });
            pr.ShaderVars = new ResourcePointerList64<ParticleShaderVar> { data_items = SpriteShaderVars(sheet) };
            return pr;
        }

        private static ParticleEffectSpawner NewSpawner() => new ParticleEffectSpawner
        {
            VFT = VftSpawner,
            DurationScalarMin = 1f,
            DurationScalarMax = 1f,
            PlaybackRateScalarMin = 1f,
            PlaybackRateScalarMax = 1f,
            ZoomScalarMin = 1f,
            ZoomScalarMax = 1f,
            ColourTintScalarMin = 0xFFFFFFFF,
            ColourTintScalarMax = 0xFFFFFFFF,
        };

        private static ParticleShaderVar[] SpriteShaderVars(Texture sheet)
        {
            var diffuse = TexVar(VarDiffuseTex, 4);
            if (sheet != null)
            {
                diffuse.Texture = sheet;
                diffuse.TextureName = (string_r)sheet.Name;
                diffuse.TextureNameHash = JenkHash.GenHash(sheet.Name?.ToLowerInvariant() ?? "");
            }
            return new ParticleShaderVar[]
            {
                VecVar(0xEA057402, 32, ParticleShaderVarType.Vector2, 0.5f, 0, 0, 0),
                KeyVar(0x0B3045BE, 31),
                KeyVar(0x91BF3028, 30),
                VecVar(0x4A8A0A28, 22, ParticleShaderVarType.Vector2, 1, 0, 0, 0),
                VecVar(0xF8338E85, 21, ParticleShaderVarType.Vector2, 1, 0, 0, 0),
                KeyVar(0xBFD98C1D, 20),
                VecVar(0xC6FE034A, 19, ParticleShaderVarType.Vector2, 0, 0, 0, 0),
                VecVar(0xF03ACB8C, 18, ParticleShaderVarType.Vector2, 0.1f, 0, 0, 0),
                KeyVar(0x81634888, 17),
                KeyVar(0xB695F45C, 16),
                KeyVar(0x403390EA, 15),
                VecVar(0x18CA6C12, 14, ParticleShaderVarType.Vector2, 0, 0, 0, 0),
                VecVar(0x1458F27B, 13, ParticleShaderVarType.Vector2, 0, 0, 0, 0),
                VecVar(0xA781A38B, 12, ParticleShaderVarType.Vector2, 0, 0, 0, 0),
                VecVar(0x77B842ED, 11, ParticleShaderVarType.Vector2, 1, 0, 0, 0),
                VecVar(0x7B483BC5, 10, ParticleShaderVarType.Vector4, 0, 1, 1, 0),
                VecVar(0x6A1DBEC3, 9, ParticleShaderVarType.Vector2, 1, 0, 0, 0),
                VecVar(0xBA5AF058, 8, ParticleShaderVarType.Vector2, 1, 0, 0, 0),
                TexVar(0xDF7CC018, 7),
                TexVar(0xB36327D1, 6),
                diffuse,
            };
        }

        private static ParticleShaderVarVector VecVar(uint name, uint id, ParticleShaderVarType type,
                                                     float x, float y, float z, float w) =>
            new ParticleShaderVarVector
            {
                VFT = VftVarVector, Name = name, Type = type, ShaderVarID = id,
                VectorX = x, VectorY = y, VectorZ = z, VectorW = w,
            };

        private static ParticleShaderVarKeyframe KeyVar(uint name, uint id) =>
            new ParticleShaderVarKeyframe
            {
                VFT = VftVarKeyframe, Name = name, Type = ParticleShaderVarType.Keyframe, ShaderVarID = id,
                Items = new ResourceSimpleList64<ParticleShaderVarKeyframeItem>
                { data_items = Array.Empty<ParticleShaderVarKeyframeItem>() },
            };

        private static ParticleShaderVarTexture TexVar(uint name, uint id) =>
            new ParticleShaderVarTexture
            {
                VFT = VftVarTexture, Name = name, Type = ParticleShaderVarType.Texture, ShaderVarID = id,
            };

        private static ParticleBehaviour MakeBehaviour(ParticleBehaviourType type)
        {
            var b = ParticleBehaviour.Create(type);
            if (b == null) return null;
            b.Type = type;
            b.VFT = BehaviourVft.TryGetValue(type, out var vft) ? vft : 0;
            switch (b)
            {
                case ParticleBehaviourAcceleration acc:
                    acc.XYZMinKFP = Kfp("ptxu_Acceleration:m_xyzMinKFP", (0f, new SDX.Vector4(-0.2f, -0.2f, 0.7f, 0)));
                    acc.XYZMaxKFP = Kfp("ptxu_Acceleration:m_xyzMaxKFP", (0f, new SDX.Vector4(0.2f, 0.2f, 1.2f, 0)));
                    acc.CreateKeyframeProps(acc.XYZMinKFP, acc.XYZMaxKFP);
                    break;
                case ParticleBehaviourDampening damp:
                    damp.XYZMinKFP = Kfp("ptxu_Dampening:m_xyzMinKFP", (0f, new SDX.Vector4(0.6f, 0.6f, 0.6f, 0)));
                    damp.XYZMaxKFP = Kfp("ptxu_Dampening:m_xyzMaxKFP", (0f, new SDX.Vector4(0.9f, 0.9f, 0.9f, 0)));
                    damp.CreateKeyframeProps(damp.XYZMinKFP, damp.XYZMaxKFP);
                    break;
                case ParticleBehaviourRotation rot:
                    rot.InitialAngleMinKFP = Kfp("ptxu_Rotation:m_initialAngleMinKFP", (0f, new SDX.Vector4(-180, 0, 0, 0)));
                    rot.InitialAngleMaxKFP = Kfp("ptxu_Rotation:m_initialAngleMaxKFP", (0f, new SDX.Vector4(180, 0, 0, 0)));
                    rot.AngleMinKFP = Kfp("ptxu_Rotation:m_angleMinKFP", (0f, new SDX.Vector4(-25, 0, 0, 0)));
                    rot.AngleMaxKFP = Kfp("ptxu_Rotation:m_angleMaxKFP", (0f, new SDX.Vector4(25, 0, 0, 0)));
                    rot.CreateKeyframeProps(rot.InitialAngleMinKFP, rot.InitialAngleMaxKFP, rot.AngleMinKFP, rot.AngleMaxKFP);
                    break;
                case ParticleBehaviourSize size:
                    size.WhdMinKFP = Kfp("ptxu_Size:m_whdMinKFP",
                        (0f, new SDX.Vector4(0.25f, 0.25f, 0, 0)), (1f, new SDX.Vector4(0.9f, 0.9f, 0, 0)));
                    size.WhdMaxKFP = Kfp("ptxu_Size:m_whdMaxKFP",
                        (0f, new SDX.Vector4(0.35f, 0.35f, 0, 0)), (1f, new SDX.Vector4(1.3f, 1.3f, 0, 0)));
                    size.TblrScalarKFP = Kfp("ptxu_Size:m_tblrScalarKFP", (0f, new SDX.Vector4(1, 1, 1, 1)));
                    size.TblrVelScalarKFP = Kfp("ptxu_Size:m_tblrVelScalarKFP");
                    size.IsProportional = 1;
                    size.CreateKeyframeProps(size.WhdMinKFP, size.WhdMaxKFP, size.TblrScalarKFP, size.TblrVelScalarKFP);
                    break;
                case ParticleBehaviourColour col:
                    col.RGBAMinKFP = Kfp("ptxu_Colour:m_rgbaMinKFP",
                        (0f, new SDX.Vector4(0.55f, 0.55f, 0.55f, 0f)),
                        (0.2f, new SDX.Vector4(0.55f, 0.55f, 0.55f, 0.55f)),
                        (0.6f, new SDX.Vector4(0.55f, 0.55f, 0.55f, 0.45f)),
                        (1f, new SDX.Vector4(0.55f, 0.55f, 0.55f, 0f)));
                    col.RGBAMaxKFP = Kfp("ptxu_Colour:m_rgbaMaxKFP",
                        (0f, new SDX.Vector4(0.8f, 0.8f, 0.8f, 0f)),
                        (0.2f, new SDX.Vector4(0.8f, 0.8f, 0.8f, 0.8f)),
                        (0.6f, new SDX.Vector4(0.8f, 0.8f, 0.8f, 0.6f)),
                        (1f, new SDX.Vector4(0.8f, 0.8f, 0.8f, 0f)));
                    col.EmissiveIntensityKFP = Kfp("ptxu_Colour:m_emissiveIntensityKFP");
                    col.RGBAMaxEnable = 1;
                    col.CreateKeyframeProps(col.RGBAMinKFP, col.RGBAMaxKFP, col.EmissiveIntensityKFP);
                    break;
                case ParticleBehaviourWind wind:
                    wind.InfluenceKFP = Kfp("ptxu_Wind:m_influenceKFP", (0f, new SDX.Vector4(0.1f, 0.1f, 0, 0)));
                    wind.CreateKeyframeProps(wind.InfluenceKFP);
                    break;
                case ParticleBehaviourNoise noise:
                    noise.PosNoiseMinKFP = Kfp("ptxu_Noise:m_posNoiseMinKFP");
                    noise.PosNoiseMaxKFP = Kfp("ptxu_Noise:m_posNoiseMaxKFP");
                    noise.VelNoiseMinKFP = Kfp("ptxu_Noise:m_velNoiseMinKFP", (0f, new SDX.Vector4(-0.3f, -0.3f, -0.1f, 0)));
                    noise.VelNoiseMaxKFP = Kfp("ptxu_Noise:m_velNoiseMaxKFP", (0f, new SDX.Vector4(0.3f, 0.3f, 0.1f, 0)));
                    noise.CreateKeyframeProps(noise.PosNoiseMinKFP, noise.PosNoiseMaxKFP,
                                              noise.VelNoiseMinKFP, noise.VelNoiseMaxKFP);
                    break;
                case ParticleBehaviourAnimateTexture at:
                    at.AnimRateKFP = Kfp("ptxu_AnimateTexture:m_animRateKFP", (0f, new SDX.Vector4(1, 1, 0, 0)));
                    at.LastFrameID = 0;
                    at.IsScaledOverParticleLife = 1;
                    at.CreateKeyframeProps(at.AnimRateKFP);
                    break;
                default:
                    b.CreateKeyframeProps();
                    break;
            }
            return b;
        }

        public static ParticleKeyframeProp Kfp(string name, params (float t, SDX.Vector4 v)[] keys)
        {
            var p = new ParticleKeyframeProp
            {
                Name = name,
                Values = new ResourceSimpleList64<ParticleKeyframePropValue>
                {
                    data_items = (keys ?? Array.Empty<(float, SDX.Vector4)>())
                        .Select(k => new ParticleKeyframePropValue
                        {
                            KeyframeTime = new SDX.Vector4(k.t, 0, 0, 0),
                            KeyframeValue = k.v,
                        }).ToArray(),
                },
            };
            return p;
        }

        public static Texture EmitterSheet(PtfxEmitter em) => PtfxDrawKinds.SheetOf(em?.ParticleRule);

        private static Texture EmitterSheet(PtfxEffect eff) =>
            eff?.Emitters.Select(EmitterSheet).FirstOrDefault(t => t != null);

        private static Texture FirstSheet(PtfxDocument doc) =>
            doc?.PtxList?.TextureDictionary?.Textures?.data_items?.FirstOrDefault(t => t != null);

        public static PtfxEffect CopyEffectInto(PtfxDocument doc, PtfxEffect source, string newName)
        {
            if (doc?.PtxList == null || source?.Rule == null) return null;

            var tmpDoc = new PtfxDocument
            {
                Name = "clone.ypt",
                Ypt = new YptFile
                {
                    PtfxList = new ParticleEffectsList
                    {
                        Name = (string_r)"clone",
                        TextureDictionary = new TextureDictionary(),
                        ParticleRuleDictionary = new ParticleRuleDictionary(),
                        EmitterRuleDictionary = new ParticleEmitterRuleDictionary(),
                        EffectRuleDictionary = new ParticleEffectRuleDictionary
                        {
                            EffectRules = new ResourcePointerList64<ParticleEffectRule> { data_items = new[] { source.Rule } },
                        },
                    },
                    Loaded = true,
                },
            };
            Rebuild(tmpDoc);
            var bytes = tmpDoc.Ypt.Save();
            var back = new YptFile();
            back.Load(bytes);
            var copied = back.PtfxList?.EffectRuleDictionary?.EffectRules?.data_items?.FirstOrDefault();
            if (copied == null) return null;

            var dict = doc.PtxList.EffectRuleDictionary ??= new ParticleEffectRuleDictionary();
            var rules = (dict.EffectRules?.data_items ?? Array.Empty<ParticleEffectRule>()).ToList();
            rules.Add(copied);
            dict.EffectRules = new ResourcePointerList64<ParticleEffectRule> { data_items = rules.ToArray() };
            Rebuild(doc);

            var eff = doc.Effects.FirstOrDefault(e => ReferenceEquals(e.Rule, copied));
            if (eff != null && !string.IsNullOrWhiteSpace(newName)) RenameEffect(doc, eff, newName);
            return doc.Effects.FirstOrDefault(e => ReferenceEquals(e.Rule, copied));
        }
    }
}

