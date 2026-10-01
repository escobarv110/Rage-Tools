using System;
using CodeWalker.GameFiles;
using SharpDX;

namespace RageLightEditor.Rendering
{
    public partial class ModelRenderer
    {
        public static bool IsPedFurShader_V38(string name) =>
            string.Equals(name, "ped_fur", StringComparison.OrdinalIgnoreCase);

        public static int PedFurLayers_V38(int minLayers, int maxLayers, float distance,
                                           float nearDist = 3.0f, float farDist = 25.0f)
        {
            int lo = Math.Max(1, Math.Min(minLayers, maxLayers));
            int hi = Math.Max(lo, Math.Max(minLayers, maxLayers));
            if (hi <= lo) return lo;
            if (distance <= nearDist) return hi;
            if (distance >= farDist) return lo;
            float t = (distance - nearDist) / (farDist - nearDist);
            return Math.Max(lo, Math.Min(hi, (int)Math.Round(hi + (lo - hi) * t)));
        }

        public static float PedFurShellOffset_U6(float furLength, int layer, int layers)
        {
            float t = (layer + 1) / (float)Math.Max(1, layers);
            return furLength * (t - 1.0f);
        }

        public static float PedFurClip_V38(Vector4 attenCoef, int layer, int layers)
        {
            float t = layers <= 1 ? 1.0f : (layer + 1) / (float)layers;
            float a = attenCoef.X == 0.0f && attenCoef.Y == 0.0f ? 1.21f : attenCoef.X;
            float b = attenCoef.X == 0.0f && attenCoef.Y == 0.0f ? -0.22f : attenCoef.Y;
            return MathUtil.Clamp(t * a + b, 0.0f, 0.995f);
        }

        public static float PedFurShadow_V38(float selfShadowMin, float aoBlend, int layer, int layers)
        {
            float t = layers <= 1 ? 1.0f : (layer + 1) / (float)layers;
            float min = selfShadowMin <= 0.0f ? 0.45f : MathUtil.Clamp(selfShadowMin, 0.0f, 1.0f);
            float lit = min + (1.0f - min) * t;
            float blend = MathUtil.Clamp(aoBlend, 0.0f, 1.0f);
            return 1.0f + (lit - 1.0f) * blend;
        }

        private void ReadPedFurParams_V38(RenderMesh mesh, ShaderFX shader, TextureDictionary embeddedDict)
        {
            if (mesh == null || !IsPedFurShader_V38(shader?.Name.ToString())) return;

            float length = 0.15f, noiseScale = 15.0f, stiffness = 0.5f, selfShadow = 0.45f, aoBlend = 1.0f;
            bool sawAoBlend = false;
            int maxLayers = 15, minLayers = 2;
            var atten = new Vector4(1.21f, -0.22f, 0, 0);
            var bend = Vector4.Zero;
            TextureBase noise = null;

            var ps = shader?.ParametersList?.Parameters;
            var hs = shader?.ParametersList?.Hashes;
            if (ps != null && hs != null)
            {
                for (int i = 0; i < ps.Length && i < hs.Length; i++)
                {
                    var v = ps[i].Data as Vector4? ?? Vector4.Zero;
                    switch ((ShaderParamNames)(uint)hs[i])
                    {
                        case ShaderParamNames.furLength: if (v.X > 0) length = v.X; break;
                        case ShaderParamNames.furMaxLayers: if (v.X > 0) maxLayers = (int)Math.Round(v.X); break;
                        case ShaderParamNames.furMinLayers: if (v.X > 0) minLayers = (int)Math.Round(v.X); break;
                        case ShaderParamNames.furNoiseUVScale: if (v.X > 0) noiseScale = v.X; break;
                        case ShaderParamNames.furAttenCoef: if (v.X != 0 || v.Y != 0) atten = v; break;
                        case ShaderParamNames.furSelfShadowMin: if (v.X > 0) selfShadow = v.X; break;
                        case ShaderParamNames.furAOBlend: aoBlend = v.X; sawAoBlend = true; break;
                        case ShaderParamNames.furStiffness: stiffness = v.X; break;
                        case ShaderParamNames.furBendParams: bend = v; break;
                        case ShaderParamNames.NoiseSampler: noise = ps[i].Data as TextureBase; break;
                    }
                }
            }

            var lenOvr = Environment.GetEnvironmentVariable("RLE_FURLEN");
            if (!string.IsNullOrWhiteSpace(lenOvr) &&
                float.TryParse(lenOvr, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var fo) && fo > 0.0f)
                length = fo;

            mesh.IsPedFur_V38 = true;
            mesh.IsFur = true;
            mesh.PedFurMinLayers_V38 = minLayers;
            mesh.PedFurMaxLayers_V38 = maxLayers;
            mesh.PedFurAtten_V38 = atten;
            mesh.PedFurSelfShadowMin_V38 = selfShadow;
            mesh.PedFurAOBlend_V38 = sawAoBlend ? aoBlend : 1.0f;
            mesh.PedFurStiffness_V38 = stiffness;
            mesh.PedFurBend_V38 = bend;
            mesh.FurLayerParams = new Vector4(length, 0, 0, 0);
            mesh.FurUvScales = new Vector4(noiseScale, noiseScale, 0, 0);
            mesh.FurLayers = maxLayers;
            if (mesh.FurAlphaDistance == Vector2.Zero) mesh.FurAlphaDistance = new Vector2(25f, 40f);

            mesh.FurComboSRV[0] = ResolveTexture(noise, embeddedDict, false, out _);
            UsePedFurFallback_U21(mesh);

            if (furDbg_V21)
                Console.WriteLine($"PEDFUR len {length:0.###} m, shells sink from the modelled coat down to the skin, layers {minLayers}..{maxLayers}, " +
                                  $"noise x{noiseScale:0.#} '{noise?.Name ?? "(none)"}', atten {atten.X:0.##}/{atten.Y:0.##}, " +
                                  $"selfShadow {selfShadow:0.##}, stiffness {stiffness:0.##}");
        }
    }
}

