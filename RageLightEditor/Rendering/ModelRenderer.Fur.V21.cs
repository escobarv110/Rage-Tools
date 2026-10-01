using System;
using CodeWalker.GameFiles;
using SharpDX;
using SharpDX.Direct3D11;
using GameTexture = CodeWalker.GameFiles.Texture;

namespace RageLightEditor.Rendering
{
    public partial class ModelRenderer
    {
        private static readonly bool furDbg_V21 = Environment.GetEnvironmentVariable("RLE_FURDBG") == "1";
        private static readonly System.Collections.Generic.HashSet<uint> furDbgSaid_V21 = new System.Collections.Generic.HashSet<uint>();

        public static bool IsFurShader_V21(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith("grass_fur", StringComparison.OrdinalIgnoreCase);

        private void ReadFurParams_V21(RenderMesh mesh, ShaderFX shader, TextureBase[] combo,
                                       TextureDictionary embeddedDict)
        {
            if (!IsFurShader_V21(shader?.Name.ToString())) return;

            if (mesh.FurLayerParams == Vector4.Zero) mesh.FurLayerParams = FurMath_U20.DefaultLayerParams;
            if (mesh.FurAlphaDistance == Vector2.Zero) mesh.FurAlphaDistance = FurMath_U20.DefaultAlphaDistance;
            if (mesh.FurUvScales == Vector4.Zero) mesh.FurUvScales = FurMath_U20.DefaultUvScales;
            if (mesh.FurShadow03 == Vector4.Zero && mesh.FurShadow47 == Vector4.Zero) { mesh.FurShadow03 = FurMath_U20.DefaultShadow03; mesh.FurShadow47 = FurMath_U20.DefaultShadow47; }
            if (mesh.FurAlphaClip03 == Vector4.Zero && mesh.FurAlphaClip47 == Vector4.Zero) { mesh.FurAlphaClip03 = FurMath_U20.DefaultAlphaClip03; mesh.FurAlphaClip47 = FurMath_U20.DefaultAlphaClip47; }
            ReadFurTextures_U20(mesh, embeddedDict);
            if (FurMath_U20.IsLodShader(shader.Name.ToString())) { mesh.IsFur = false; return; }

            int found = 0;
            var foundTex = furDbg_V21 ? new GameTexture[4] : null;
            for (int k = 0; k < 4; k++)
            {
                mesh.FurComboSRV[k] = ResolveTexture(combo[k], embeddedDict, false, out var ft);
                if (foundTex != null) foundTex[k] = ft;
                if (mesh.FurComboSRV[k] != null) found++;
            }

            if (furDbg_V21 && furDbgSaid_V21.Add(shader.Name.Hash ^ (uint)(mesh.DiffuseName ?? "").GetHashCode()))
            {
                var ps = shader.ParametersList?.Parameters;
                var hs = shader.ParametersList?.Hashes;
                if (ps != null && hs != null)
                    for (int pi = 0; pi < ps.Length && pi < hs.Length; pi++)
                    {
                        if (ps[pi].DataType == 0) continue;
                        var pv = ps[pi].Data is SharpDX.Vector4 v4 ? v4 : default;
                        Console.WriteLine($"FURDBG   param {(ShaderParamNames)hs[pi]} = {pv.X:0.####}, {pv.Y:0.####}, {pv.Z:0.####}, {pv.W:0.####}");
                    }
                Console.WriteLine($"FURDBG {shader.Name} diffuse '{mesh.DiffuseName}' comb {found}/4 " +
                                  $"[{string.Join(", ", System.Linq.Enumerable.Select(combo, x => x?.Name ?? "(none)"))}] " +
                                  $"len {mesh.FurLayerParams.X:0.###} m, layers {mesh.FurLayers}, " +
                                  $"fade {mesh.FurAlphaDistance.X:0}..{mesh.FurAlphaDistance.Y:0} m");
                if (foundTex != null)
                    foreach (var ft in foundTex)
                    {
                        if (ft?.Data?.FullData == null) continue;
                        try
                        {
                            var px = CodeWalker.Utils.DDSIO.GetPixels(ft, 0);
                            if (px == null || px.Length < 4) continue;
                            int n2 = px.Length / 4;
                            var lo = new int[4] { 255, 255, 255, 255 }; var hi = new int[4]; var sum = new long[4];
                            for (int i2 = 0; i2 < n2; i2++)
                                for (int ch = 0; ch < 4; ch++)
                                {
                                    int v2 = px[i2 * 4 + ch];
                                    if (v2 < lo[ch]) lo[ch] = v2;
                                    if (v2 > hi[ch]) hi[ch] = v2;
                                    sum[ch] += v2;
                                }
                            Console.WriteLine($"FURDBG   {ft.Name} {ft.Width}x{ft.Height} {ft.Format}: " +
                                              $"B {lo[0]}..{hi[0]} ~{sum[0] / n2}  G {lo[1]}..{hi[1]} ~{sum[1] / n2}  " +
                                              $"R {lo[2]}..{hi[2]} ~{sum[2] / n2}  A {lo[3]}..{hi[3]} ~{sum[3] / n2}");
                        }
                        catch (Exception ex) { Console.WriteLine("FURDBG   decode failed: " + ex.Message); }
                    }
            }

            if (found < 4) found += UseGrassFurFallback_U21(mesh);
            mesh.IsFur = found > 0;
            if (!mesh.IsFur) return;
            mesh.FurLayerParams = new Vector4(Math.Max(mesh.FurLayerParams.X, 0.0f), mesh.FurLayerParams.Y, mesh.FurLayerParams.Z, mesh.FurLayerParams.W);

            mesh.FurLayers = Math.Max(1, Math.Min(8, mesh.FurLayers == 0 ? 8 : mesh.FurLayers));
        }

        private static bool ReadFurVector_V21(RenderMesh mesh, ShaderParamNames name, Vector4 v)
        {
            switch (name)
            {
                case ShaderParamNames.furShadow03: mesh.FurShadow03 = v; return true;
                case ShaderParamNames.furShadow47: mesh.FurShadow47 = v; return true;
                case ShaderParamNames.furAlphaClip03: mesh.FurAlphaClip03 = v; return true;
                case ShaderParamNames.furAlphaClip47: mesh.FurAlphaClip47 = v; return true;
                case ShaderParamNames.furAlphaDistance: mesh.FurAlphaDistance = new Vector2(v.X, v.Y); return true;
                case ShaderParamNames.furUvScales: mesh.FurUvScales = v; return true;
                case ShaderParamNames.furLayerParams: mesh.FurLayerParams = v; return true;
                case ShaderParamNames.furLength:
                    mesh.FurLayerParams = new Vector4(v.X, mesh.FurLayerParams.Y, mesh.FurLayerParams.Z, mesh.FurLayerParams.W);
                    return true;
                case ShaderParamNames.furNumLayers: mesh.FurLayers = (int)Math.Round(v.X); return true;
                default: return false;
            }
        }

        public static float FurLadderAverage_V21(RenderMesh mesh)
        {
            int layers = Math.Max(1, Math.Min(8, mesh.FurLayers == 0 ? 8 : mesh.FurLayers));
            float sum = 0;
            int n = 0;
            for (int i = 0; i < layers; i++)
            {
                FurLayer_V21(mesh, i, out _, out var sh);
                if (sh > 0.0f) { sum += sh; n++; }
            }
            return n > 0 ? sum / n : 1.0f;
        }

        public static void FurLayer_V21(RenderMesh mesh, int layer, out float clip, out float shadow)
        {
            layer = Math.Max(0, Math.Min(7, layer));
            var clips = layer < 4 ? mesh.FurAlphaClip03 : mesh.FurAlphaClip47;
            var shad = layer < 4 ? mesh.FurShadow03 : mesh.FurShadow47;
            int i = layer & 3;
            clip = i == 0 ? clips.X : i == 1 ? clips.Y : i == 2 ? clips.Z : clips.W;
            shadow = i == 0 ? shad.X : i == 1 ? shad.Y : i == 2 ? shad.Z : shad.W;
        }
    }
}

