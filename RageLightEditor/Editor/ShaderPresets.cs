using System;
using System.Collections.Generic;
using System.Linq;
using CodeWalker.GameFiles;
using SharpDX;

namespace RageLightEditor.Editor
{
    public struct PresetParam
    {
        public uint Hash;
        public bool IsTexture;
        public Vector4 Default;

        public PresetParam(ShaderParamNames n, Vector4 def)
        {
            Hash = (uint)n; IsTexture = false; Default = def;
        }

        public PresetParam(ShaderParamNames n)
        {
            Hash = (uint)n; IsTexture = true; Default = Vector4.Zero;
        }
    }

    public class ShaderTemplate
    {
        public string Name;
        public string Sps;
        public byte Bucket;
        public List<PresetParam> Params = new List<PresetParam>();
        public bool Harvested;
        public string Category = "";

        public bool Declares(uint hash) => Params.Any(p => p.Hash == hash);
    }

    public static partial class ShaderPresets
    {
        private static readonly string[] Megashader =
        {
            "albedo_alpha", "cloth_default", "cloth_normal_spec", "cloth_normal_spec_tnt",
            "cloth_spec_alpha", "cpv_only", "cubemap_reflect", "cutout_fence", "cutout_fence_normal",
            "cutout_hard", "decal", "decal_amb_only", "decal_diff_only_um", "decal_dirt",
            "decal_emissivenight_only", "decal_emissive_only", "decal_glue", "decal_normal_only",
            "decal_normal_spec_um", "decal_shadow_only", "decal_spec_only", "decal_tnt", "default",
            "default_detail", "default_spec", "default_terrain_wet", "default_tnt", "default_um",
            "emissive", "emissivenight", "emissivenight_geomnightonly", "emissivestrong",
            "emissive_additive_alpha", "emissive_additive_uv_alpha", "emissive_clip",
            "emissive_speclum", "emissive_tnt", "minimap", "normal", "normal_cubemap_reflect",
            "normal_decal", "normal_decal_pxm", "normal_decal_pxm_tnt", "normal_decal_tnt",
            "normal_detail", "normal_detail_dpm", "normal_detail_dpm_tnt", "normal_diffspec",
            "normal_diffspec_detail", "normal_diffspec_detail_dpm", "normal_diffspec_detail_dpm_tnt",
            "normal_diffspec_detail_dpm_wind", "normal_diffspec_detail_tnt", "normal_diffspec_tnt",
            "normal_pxm", "normal_pxm_tnt", "normal_reflect", "normal_reflect_decal", "normal_spec",
            "normal_spec_batch", "normal_spec_cubemap_reflect", "normal_spec_decal",
            "normal_spec_decal_detail", "normal_spec_decal_nopuddle", "normal_spec_decal_pxm",
            "normal_spec_decal_tnt", "normal_spec_detail", "normal_spec_detail_dpm",
            "normal_spec_detail_dpm_texdecal_tnt", "normal_spec_detail_dpm_tnt",
            "normal_spec_detail_dpm_vertdecal_tnt", "normal_spec_detail_tnt", "normal_spec_dpm",
            "normal_spec_emissive", "normal_spec_pxm", "normal_spec_pxm_tnt", "normal_spec_reflect",
            "normal_spec_reflect_decal", "normal_spec_reflect_emissivenight", "normal_spec_tnt",
            "normal_spec_twiddle_tnt", "normal_spec_um", "normal_spec_wrinkle", "normal_terrain_wet",
            "normal_terrain_wet_pxm", "normal_tnt", "normal_um", "normal_um_tnt", "normal_wind",
            "parallax", "parallax_specmap", "parallax_steep", "reflect", "reflect_decal", "spec",
            "spec_decal", "spec_reflect", "spec_reflect_decal", "spec_tnt", "spec_twiddle_tnt",
            "weapon_emissivestrong_alpha", "weapon_emissive_tnt", "weapon_normal_spec_alpha",
            "weapon_normal_spec_cutout_palette", "weapon_normal_spec_detail_palette",
            "weapon_normal_spec_detail_tnt", "weapon_normal_spec_palette", "weapon_normal_spec_tnt",
        };

        private static readonly string[] Custom =
        {
            "blend_2lyr", "cable", "decal_normal_blend_2lyr", "distance_map", "glass",
            "glass_breakable", "glass_breakable_crack", "glass_displacement", "glass_emissive",
            "glass_emissivenight", "glass_env", "glass_normal_spec_reflect", "glass_pv",
            "glass_pv_env", "glass_reflect", "glass_spec", "mirror_crack", "mirror_decal",
            "mirror_default", "radar", "rope_default", "silhouettelayer", "skinblend",
        };

        private static readonly string[] Terrain =
        {
            "terrain_cb_4lyr", "terrain_cb_4lyr_2tex", "terrain_cb_4lyr_2tex_blend",
            "terrain_cb_4lyr_2tex_blend_lod", "terrain_cb_4lyr_2tex_pxm", "terrain_cb_4lyr_cm",
            "terrain_cb_4lyr_cm_tnt", "terrain_cb_4lyr_lod", "terrain_cb_4lyr_pxm",
            "terrain_cb_4lyr_spec", "terrain_cb_w_4lyr", "terrain_cb_w_4lyr_2tex",
            "terrain_cb_w_4lyr_2tex_blend", "terrain_cb_w_4lyr_2tex_blend_lod",
            "terrain_cb_w_4lyr_2tex_blend_pxm", "terrain_cb_w_4lyr_2tex_blend_pxm_spm",
            "terrain_cb_w_4lyr_2tex_blend_pxm_tn_spm", "terrain_cb_w_4lyr_2tex_blend_pxm_tt_spm",
            "terrain_cb_w_4lyr_2tex_blend_tt", "terrain_cb_w_4lyr_2tex_blend_ttn",
            "terrain_cb_w_4lyr_2tex_pxm", "terrain_cb_w_4lyr_cm", "terrain_cb_w_4lyr_cm_pxm",
            "terrain_cb_w_4lyr_cm_pxm_tnt", "terrain_cb_w_4lyr_cm_tnt", "terrain_cb_w_4lyr_lod",
            "terrain_cb_w_4lyr_pxm", "terrain_cb_w_4lyr_pxm_spm", "terrain_cb_w_4lyr_spec",
            "terrain_cb_w_4lyr_spec_int", "terrain_cb_w_4lyr_spec_int_pxm",
            "terrain_cb_w_4lyr_spec_pxm", "terrain_uber_4lyr",
        };

        private static readonly string[] Vegetation =
        {
            "billboard_nobump", "grass", "grass_batch", "grass_batch_camera_aligned",
            "grass_batch_camera_facing", "grass_camera_aligned", "grass_camera_facing", "grass_fur",
            "grass_fur_lod", "grass_fur_mask", "grass_fur_tnt", "trees", "trees_camera_aligned",
            "trees_camera_facing", "trees_lod", "trees_lod2", "trees_lod2d", "trees_lod_tnt",
            "trees_normal", "trees_normal_diffspec", "trees_normal_diffspec_tnt", "trees_normal_spec",
            "trees_normal_spec_camera_aligned", "trees_normal_spec_camera_aligned_tnt",
            "trees_normal_spec_camera_facing", "trees_normal_spec_camera_facing_tnt",
            "trees_normal_spec_tnt", "trees_normal_spec_wind", "trees_shadow_proxy", "trees_tnt",
        };

        private static readonly string[] Peds =
        {
            "ped", "ped_alpha", "ped_cloth", "ped_cloth_enveff", "ped_decal", "ped_decal_decoration",
            "ped_decal_exp", "ped_decal_medals", "ped_decal_nodiff", "ped_default",
            "ped_default_cloth", "ped_default_enveff", "ped_default_mp", "ped_default_palette",
            "ped_emissive", "ped_enveff", "ped_fur", "ped_hair_cutout_alpha",
            "ped_hair_cutout_alpha_cloth", "ped_hair_spiked", "ped_hair_spiked_enveff",
            "ped_hair_spiked_mask", "ped_nopeddamagedecals", "ped_palette", "ped_wrinkle",
            "ped_wrinkle_cloth", "ped_wrinkle_cloth_enveff", "ped_wrinkle_cs", "ped_wrinkle_enveff",
        };

        private static readonly string[] Vehicles =
        {
            "vehicle_badges", "vehicle_basic", "vehicle_blurredrotor", "vehicle_blurredrotor_emissive",
            "vehicle_cloth", "vehicle_cloth2", "vehicle_cutout", "vehicle_dash_emissive",
            "vehicle_dash_emissive_opaque", "vehicle_decal", "vehicle_decal2", "vehicle_detail",
            "vehicle_detail2", "vehicle_emissive_alpha", "vehicle_emissive_opaque", "vehicle_generic",
            "vehicle_interior", "vehicle_interior2", "vehicle_licenseplate", "vehicle_lightsemissive",
            "vehicle_lightsemissive_siren", "vehicle_mesh", "vehicle_mesh2_enveff",
            "vehicle_mesh_enveff", "vehicle_paint1", "vehicle_paint1_enveff", "vehicle_paint2",
            "vehicle_paint2_enveff", "vehicle_paint3", "vehicle_paint3_enveff", "vehicle_paint3_lvr",
            "vehicle_paint4", "vehicle_paint4_emissive", "vehicle_paint4_enveff", "vehicle_paint5_enveff",
            "vehicle_paint6", "vehicle_paint6_enveff", "vehicle_paint7", "vehicle_paint7_enveff",
            "vehicle_paint8", "vehicle_paint9", "vehicle_shuts", "vehicle_tire",
            "vehicle_tire_emissive", "vehicle_track", "vehicle_track2", "vehicle_track2_emissive",
            "vehicle_track_ammo", "vehicle_track_emissive", "vehicle_track_siren", "vehicle_vehglass",
            "vehicle_vehglass_crack", "vehicle_vehglass_inner",
        };

        private static readonly string[] Water =
        {
            "water", "waterTex", "water_foam", "water_fountain", "water_poolenv", "water_river",
            "water_riverfoam", "water_riverlod", "water_riverocean", "water_rivershallow",
            "water_shallow", "water_terrainfoam",
        };

        private static readonly HashSet<string> AnimUv = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "spec", "normal_wind", "normal_terrain_wet_pxm", "normal_terrain_wet", "normal_spec_reflect",
            "normal_spec_decal_pxm", "normal_spec_decal", "normal_pxm", "normal_detail_dpm_tnt",
            "normal_detail", "normal_decal_pxm", "normal_decal", "normal", "emissivestrong",
            "emissive_clip", "emissive_additive_uv_alpha", "emissive_additive_alpha", "emissive",
            "default_um", "default_terrain_wet", "default_detail", "default", "cutout_hard",
            "cloth_default",
        };

        public static bool SupportsAnimatedUvs(string shaderName) =>
            !string.IsNullOrEmpty(shaderName) && AnimUv.Contains(StripExt(shaderName));

        public static string[] AnimatedUvPresets => AnimUv.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

        private static string StripExt(string n)
        {
            int dot = n.LastIndexOf('.');
            return dot > 0 ? n.Substring(0, dot) : n;
        }

        public static readonly (string Category, string[] Names)[] Categories =
        {
            ("Map & props", Megashader),
            ("Glass, cable & custom", Custom),
            ("Terrain", Terrain),
            ("Vegetation", Vegetation),
            ("Peds", Peds),
            ("Vehicles", Vehicles),
            ("Water", Water),
        };

        private static readonly Dictionary<uint, string> nameByHash = new Dictionary<uint, string>();
        private static readonly Dictionary<string, ShaderTemplate> templates =
            new Dictionary<string, ShaderTemplate>(StringComparer.OrdinalIgnoreCase);
        private static bool seeded;

        public static void Seed()
        {
            if (seeded) return;
            seeded = true;
            foreach (var (_, names) in Categories)
            {
                foreach (var n in names)
                {
                    JenkIndex.Ensure(n);
                    JenkIndex.Ensure(n + ".sps");
                    nameByHash[JenkHash.GenHash(n)] = n;
                }
            }
            foreach (var v in Enum.GetValues(typeof(ShaderParamNames)))
            {
                JenkIndex.Ensure(v.ToString());
            }
        }

        public static string NameOf(uint hash)
        {
            Seed();
            return nameByHash.TryGetValue(hash, out var n) ? n : null;
        }

        public static IEnumerable<string> AllNames =>
            Categories.SelectMany(c => c.Names);

        public static void Harvest(ShaderFX s)
        {
            if (s?.ParametersList?.Parameters == null) return;
            var name = NameOf(s.Name.Hash) ?? s.Name.ToString();
            if (string.IsNullOrEmpty(name)) return;
            if (templates.TryGetValue(name, out var existing) && existing.Harvested) return;

            var t = new ShaderTemplate
            {
                Name = name,
                Sps = s.FileName.ToString(),
                Bucket = s.RenderBucket,
                Harvested = true,
                Category = CategoryOf(name),
            };
            foreach (var h in MaterialEditing.ParamHashes(s))
            {
                int i = MaterialEditing.IndexOf(s, h);
                var p = s.ParametersList.Parameters[i];
                t.Params.Add(p.DataType == 0
                    ? new PresetParam { Hash = h, IsTexture = true }
                    : new PresetParam { Hash = h, IsTexture = false, Default = p.Data is Vector4 v ? v : Vector4.Zero });
            }
            templates[name] = t;
        }

        public static void HarvestAll(DrawableBase d)
        {
            var shaders = d?.ShaderGroup?.Shaders?.data_items;
            if (shaders == null) return;
            foreach (var s in shaders) Harvest(s);
        }

        public static int HarvestedCount => templates.Values.Count(t => t.Harvested);

        public static ShaderTemplate Template(string name)
        {
            Seed();
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (templates.TryGetValue(name, out var t)) return t;
            t = Derive(name);
            templates[name] = t;
            return t;
        }

        public static string CategoryOf(string name)
        {
            foreach (var (cat, names) in Categories)
            {
                if (names.Contains(name, StringComparer.OrdinalIgnoreCase)) return cat;
            }
            return "Other";
        }

        private static ShaderTemplate Derive(string name)
        {
            var n = name.ToLowerInvariant();
            bool Has(string tok) => n.Contains(tok);

            var t = new ShaderTemplate
            {
                Name = name,
                Sps = name + ".sps",
                Category = CategoryOf(name),
            };

            if (Has("water")) t.Bucket = 6;
            else if (Has("glass") || Has("_alpha") || Has("mirror")) t.Bucket = 1;
            else if (Has("decal") || Has("dirt")) t.Bucket = 2;
            else if (Has("cutout") || Has("fence") || Has("trees") || Has("grass") || Has("_clip")) t.Bucket = 3;
            else t.Bucket = 0;

            if (PedFurParams_V38(n, t)) return t;
            if (GrassFurParams_U20(n, t)) return t;

            bool terrain4 = false; TerrainParams_R4(n, t, ref terrain4);
            if (!terrain4) t.Params.Add(new PresetParam(ShaderParamNames.DiffuseSampler));
            bool normalMap = Has("normal") || Has("bump") || Has("cutout_fence_normal");
            if (normalMap) t.Params.Add(new PresetParam(ShaderParamNames.BumpSampler));

            bool specParams = Has("spec") || Has("reflect") || Has("normal") || Has("default") || Has("emissive");
            bool specMap = Has("spec") && !Has("diffspec") && !Has("speclum");
            if (specMap)
            {
                t.Params.Add(new PresetParam(ShaderParamNames.SpecSampler));
            }

            bool detail = Has("detail") || Has("dpm");
            if (detail) t.Params.Add(new PresetParam(ShaderParamNames.DetailSampler));

            bool tint = n.EndsWith("_tnt") || Has("_tnt_") || Has("palette");
            if (tint) t.Params.Add(new PresetParam(ShaderParamNames.TintPaletteSampler));

            bool vehicle = n.StartsWith("vehicle_");
            bool reflect = Has("reflect") || Has("cubemap") || Has("enveff") || Has("_env") || vehicle;
            if (reflect) t.Params.Add(new PresetParam(ShaderParamNames.EnvironmentSampler));

            if (Has("licenseplate") || Has("plate"))
            {
                t.Params.Add(new PresetParam(ShaderParamNames.PlateBgSampler));
                t.Params.Add(new PresetParam(ShaderParamNames.PlateBgBumpSampler));
            }

            t.Params.Add(new PresetParam(ShaderParamNames.HardAlphaBlend, new Vector4(1, 0, 0, 0)));
            t.Params.Add(new PresetParam(ShaderParamNames.useTessellation, Vector4.Zero));

            if (!Has("shadow_only") && !Has("amb_only") && !Has("cpv_only"))
                t.Params.Add(new PresetParam(ShaderParamNames.wetnessMultiplier, new Vector4(1, 0, 0, 0)));

            if (normalMap)
                t.Params.Add(new PresetParam(ShaderParamNames.bumpiness, new Vector4(1, 0, 0, 0)));

            if (specMap)
                t.Params.Add(new PresetParam(ShaderParamNames.specMapIntMask, new Vector4(1, 0, 0, 0)));

            if (specParams)
            {
                t.Params.Add(new PresetParam(ShaderParamNames.specularIntensityMult, new Vector4(1, 0, 0, 0)));
                t.Params.Add(new PresetParam(ShaderParamNames.specularFalloffMult, new Vector4(32, 0, 0, 0)));
                t.Params.Add(new PresetParam(ShaderParamNames.specularFresnel, new Vector4(0.96f, 0, 0, 0)));
            }

            if (detail)
                t.Params.Add(new PresetParam(ShaderParamNames.detailSettings, new Vector4(0, 0, 24, 24)));

            if (Has("pxm") || Has("parallax") || Has("dpm"))
                t.Params.Add(new PresetParam(ShaderParamNames.parallaxScaleBias, new Vector4(0.03f, 0, 0, 0)));

            if (reflect)
                t.Params.Add(new PresetParam(ShaderParamNames.reflectivePower, new Vector4(0.5f, 0, 0, 0)));

            if (Has("emissive"))
                t.Params.Add(new PresetParam(ShaderParamNames.emissiveMultiplier, new Vector4(1, 0, 0, 0)));

            if (vehicle)
            {
                t.Params.Add(new PresetParam(ShaderParamNames.matDiffuseColor, new Vector4(1, 1, 1, 1)));
                t.Params.Add(new PresetParam(ShaderParamNames.envEffThickness, new Vector4(0.001f, 0, 0, 0)));
                t.Params.Add(new PresetParam(ShaderParamNames.envEffScale, new Vector4(1, 0, 0, 0)));
                t.Params.Add(new PresetParam(ShaderParamNames.dirtLevelMod, new Vector4(0, 0, 0, 0)));
                t.Params.Add(new PresetParam(ShaderParamNames.dirtColor, new Vector4(0.2f, 0.2f, 0.2f, 0)));
                if (Has("licenseplate") || Has("plate"))
                {
                    t.Params.Add(new PresetParam(ShaderParamNames.NumLetters, new Vector4(8, 0, 0, 0)));
                    t.Params.Add(new PresetParam(ShaderParamNames.LetterSize, new Vector4(0.1f, 0.2f, 0, 0)));
                    t.Params.Add(new PresetParam(ShaderParamNames.LetterIndex1, Vector4.Zero));
                    t.Params.Add(new PresetParam(ShaderParamNames.LetterIndex2, Vector4.Zero));
                    t.Params.Add(new PresetParam(ShaderParamNames.LicensePlateFontExtents, new Vector4(0, 0, 1, 1)));
                    t.Params.Add(new PresetParam(ShaderParamNames.LicensePlateFontTint, new Vector4(0, 0, 0, 1)));
                }
            }

            if (AnimUv.Contains(name))
            {
                t.Params.Add(new PresetParam(ShaderParamNames.globalAnimUV0, new Vector4(1, 0, 0, 0)));
                t.Params.Add(new PresetParam(ShaderParamNames.globalAnimUV1, new Vector4(0, 1, 0, 0)));
            }

            return t;
        }

        public static bool Apply(ShaderFX shader, string presetName)
        {
            var t = Template(presetName);
            if (shader == null || t == null) return false;

            MaterialEditing.RememberTextures(shader);

            var oldValues = new Dictionary<uint, Vector4>();
            var oldTextures = new Dictionary<uint, TextureBase>();
            foreach (var h in MaterialEditing.ParamHashes(shader).ToList())
            {
                int i = MaterialEditing.IndexOf(shader, h);
                var p = shader.ParametersList.Parameters[i];
                if (p.DataType == 0) oldTextures[h] = p.Data as TextureBase;
                else if (p.Data is Vector4 v) oldValues[h] = v;
            }

            var ps = new List<ShaderParameter>();
            var hs = new List<MetaName>();
            foreach (var pp in t.Params.Where(p => p.IsTexture).Concat(t.Params.Where(p => !p.IsTexture)))
            {
                var sp = new ShaderParameter { DataType = (byte)(pp.IsTexture ? 0 : 1) };
                if (pp.IsTexture)
                {
                    sp.Data = oldTextures.TryGetValue(pp.Hash, out var tex) && tex != null
                        ? tex
                        : MaterialEditing.RecallTexture(shader, pp.Hash) ?? GameFurTexture_U21(t.Name, pp.Hash);
                }
                else
                {
                    sp.Data = oldValues.TryGetValue(pp.Hash, out var v) ? v : pp.Default;
                }
                ps.Add(sp);
                hs.Add((MetaName)pp.Hash);
            }

            if (shader.ParametersList == null) shader.ParametersList = new ShaderParametersBlock();
            shader.ParametersList.Owner = shader;
            shader.ParametersList.Parameters = ps.ToArray();
            shader.ParametersList.Hashes = hs.ToArray();
            shader.ParametersList.Count = ps.Count;

            shader.Name = new MetaHash(JenkHash.GenHash(t.Name));
            shader.FileName = new MetaHash(JenkHash.GenHash(t.Sps));
            MaterialEditing.SetBucket(shader, t.Bucket);
            MaterialEditing.Normalise(shader);
            return true;
        }

        public static string CompatibilityWarning(string presetName, DrawableGeometry geom)
        {
            var t = Template(presetName);
            if (t == null || geom == null) return null;
            var info = geom.VertexBuffer?.Info;
            if (info == null) return null;

            var warnings = new List<string>();
            bool hasTangent = ((info.Flags >> 14) & 1) != 0;
            bool hasUv1 = ((info.Flags >> 7) & 1) != 0;
            bool hasColour = ((info.Flags >> 4) & 1) != 0;

            if (!hasTangent && t.Declares((uint)ShaderParamNames.BumpSampler))
                warnings.Add("this geometry has no tangents, so a normal map will light incorrectly");
            if (!hasUv1 && presetName.Contains("terrain", StringComparison.OrdinalIgnoreCase))
                warnings.Add("terrain blending expects a second UV set, which this geometry lacks");
            if (!hasColour && (presetName.Contains("blend", StringComparison.OrdinalIgnoreCase) ||
                               presetName.Contains("4lyr", StringComparison.OrdinalIgnoreCase)))
                warnings.Add("layer blending is driven by vertex colours, which this geometry lacks");

            return warnings.Count > 0 ? string.Join("; ", warnings) : null;
        }
    }
}

