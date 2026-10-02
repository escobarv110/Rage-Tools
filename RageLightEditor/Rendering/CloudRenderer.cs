using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CodeWalker.GameFiles;
using CodeWalker.World;
using RageLightEditor.Editor;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using GameTexture = CodeWalker.GameFiles.Texture;

namespace RageLightEditor.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CloudVars
    {
        public Matrix ViewProjNoTrans;
        public Vector4 Scale;
        public Vector4 SunColour;
        public Vector4 UvOffset;
        public Vector4 CamPos;
        public Vector4 SunDir;
        public Vector4 CloudColour;
        public Vector4 LightColour;
        public Vector4 AmbientColour;
        public Vector4 SkyColour;
        public Vector4 BounceColour;
        public Vector4 EastMinusWestColour;
        public Vector4 WestColour;
        public Vector4 DensityShiftScale;
        public Vector4 ScaleDiffuseFillAmbientWrap;
        public Vector4 Piercing;
        public Vector4 Rescale12;
        public Vector4 Rescale3Offset1;
        public Vector4 Offset23;
        public Vector4 AnimScale12;
        public Vector4 AnimScale3Flags;
        public Vector4 AnimCombine;
        public Vector4 AnimSculpt;
        public Vector4 AnimBlend;
        public GameFogVars Fog;
    }

    public struct CloudLighting
    {
        public bool Keyframed;
        public Vector3 SunDir;
        public float HdrScale;
        public CloudKeyframeState Kf;
    }

    public partial class CloudRenderer : IDisposable
    {
        private readonly Device device;
        private readonly ShaderSet shader;
        private readonly ConstantBuffer<CloudVars> cb;
        private GameFileManager gameFiles;
        private ModelRenderer modelRenderer;
        private TextureLoader textures;
        private readonly CloudHats hats = new CloudHats();
        private readonly Dictionary<uint, RenderModel> models = new Dictionary<uint, RenderModel>();
        private readonly Dictionary<uint, ShaderResourceView[]> densities = new Dictionary<uint, ShaderResourceView[]>();
        private readonly HashSet<uint> missing = new HashSet<uint>();
        private readonly HashSet<uint> warned = new HashSet<uint>();
        private readonly Dictionary<uint, int> retryAt = new Dictionary<uint, int>();
        private readonly HashSet<uint> retryWarned = new HashSet<uint>();
        private int frame;
        public int LayersWaiting { get; private set; }

        public bool Ready => hats.Loaded;
        public string Status { get; private set; } = "not loaded";
        public string[] FragNames { get; private set; } = Array.Empty<string>();
        public int LayersDrawn { get; private set; }
        public static readonly bool DebugDump = Environment.GetEnvironmentVariable("RLE_DUMPCLOUDS") == "1";

        public CloudRenderer(Device device)
        {
            this.device = device;
            shader = new ShaderSet(device, "clouds.hlsl", new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                new InputElement("TANGENT", 0, Format.R32G32B32A32_Float, 24, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 40, 0),
                new InputElement("COLOR", 1, Format.R32G32B32A32_Float, 56, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 72, 0),
                new InputElement("TEXCOORD", 1, Format.R32G32_Float, 80, 0),
            });
            cb = new ConstantBuffer<CloudVars>(device);
        }

        public void Init(GameFileManager files, ModelRenderer renderer, TextureLoader textureLoader)
        {
            gameFiles = files;
            modelRenderer = renderer;
            textures = textureLoader;
            if (hats.Load(files))
            {
                FragNames = hats.FragNames();
                Status = $"{FragNames.Length} hats";
                Console.WriteLine($"CLOUDS: clouds.xml read - {FragNames.Length} hat frags [{string.Join(",", FragNames)}], keyframes={(hats.SettingsMap != null)}");
            }
            else
            {
                Status = "clouds.xml: " + hats.Error;
                Console.WriteLine("CLOUDS: " + Status);
            }
        }

        public string FragForWeather(string[] cycles)
        {
            if (!Ready || hats.SettingsMap?.SettingsMap == null || cycles == null) return null;
            foreach (var c in cycles)
            {
                if (string.IsNullOrEmpty(c)) continue;
                var sn = hats.SettingsNameFor(c);
                if (sn != null)
                {
                    var f0 = hats.PickFrag(sn);
                    if (f0 != null) return f0;
                }
                string bare = c.StartsWith("w_", StringComparison.OrdinalIgnoreCase) ? c.Substring(2) : c;
                foreach (var key in hats.SettingsMap.SettingsMap.Keys)
                {
                    if (string.Equals(key, bare, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(key, c, StringComparison.OrdinalIgnoreCase))
                    {
                        var f = hats.PickFrag(key);
                        if (f != null) return f;
                    }
                }
            }
            return null;
        }

        public string SettingsNameFor(string cycleOrWeather) => Ready ? hats.SettingsNameFor(cycleOrWeather) : null;
        public CloudKeyframeState EvaluateKeyframes(string settingsName, float hour) => Ready ? hats.Evaluate(settingsName, hour) : default;

        private CloudHatFrag FindFrag(string name)
        {
            if (string.IsNullOrEmpty(name) || hats.HatManager?.CloudHatFrags == null) return null;
            foreach (var f in hats.HatManager.CloudHatFrags)
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        private RenderModel GetLayerModel(CloudHatFragLayer layer, out ShaderResourceView[] density)
        {
            density = null;
            if (string.IsNullOrEmpty(layer.Filename)) return null;
            uint hash = JenkHash.GenHash(layer.Filename.ToLowerInvariant());
            if (models.TryGetValue(hash, out var m)) { densities.TryGetValue(hash, out density); return m; }
            if (missing.Contains(hash)) return null;
            var drw = gameFiles.GetDrawable(hash, out var arch);
            if (drw == null)
            {
                if (arch == null)
                {
                    missing.Add(hash);
                    if (warned.Add(hash)) Console.WriteLine($"CLOUDS: cloud hat archetype missing: {layer.Filename}");
                }
                return null;
            }
            if (retryAt.TryGetValue(hash, out int at) && frame < at) return null;
            m = modelRenderer.BuildFromDrawable(drw, layer.Filename);
            if (m == null || m.Meshes.Count == 0) { missing.Add(hash); return null; }
            density = new ShaderResourceView[m.Meshes.Count];
            var mparams = new CloudMeshParams_U25[m.Meshes.Count];
            for (int i = 0; i < m.Meshes.Count; i++)
            {
                var mesh = m.Meshes[i];
                ShaderResourceView srv = null;
                var plist = mesh.Shader?.ParametersList;
                if (plist?.Parameters != null && plist.Hashes != null)
                {
                    int n = Math.Min(plist.Parameters.Length, plist.Hashes.Length);
                    for (int k = 0; k < n; k++)
                    {
                        if ((ShaderParamNames)(uint)plist.Hashes[k] != ShaderParamNames.DensitySampler) continue;
                        if (plist.Parameters[k].Data is TextureBase tb)
                        {
                            var gt = tb as GameTexture ?? mesh.EmbeddedDict?.Lookup(tb.NameHash);
                            if (gt?.Data?.FullData != null && textures != null)
                                srv = textures.GetSRV(gt, false);
                        }
                        break;
                    }
                }
                density[i] = srv ?? mesh.DiffuseSRV;
                mparams[i] = BuildMeshParams_U25(mesh, density[i]);
                if (density[i] == null || (mparams[i].Anim && mparams[i].Detail1 == null))
                {
                    m.Dispose();
                    retryAt[hash] = frame + 30;
                    if (retryWarned.Add(hash)) Console.WriteLine($"CLOUDS: hat layer {layer.Filename} has no density texture yet - waiting");
                    return null;
                }
            }
            models[hash] = m;
            meshParams_U25[hash] = mparams;
            densities[hash] = density;
            if (DebugDump)
            {
                for (int i = 0; i < m.Meshes.Count; i++)
                {
                    var mesh = m.Meshes[i];
                    var sb = new System.Text.StringBuilder();
                    var plist = mesh.Shader?.ParametersList;
                    if (plist?.Parameters != null && plist.Hashes != null)
                    {
                        int n = Math.Min(plist.Parameters.Length, plist.Hashes.Length);
                        for (int k = 0; k < n; k++)
                        {
                            var pd = plist.Parameters[k].Data;
                            if (pd is TextureBase tb) sb.Append($" {(ShaderParamNames)(uint)plist.Hashes[k]}={tb.Name}({(tb is GameTexture ? "embedded" : "ref")})");
                        }
                    }
                    Console.WriteLine($"CLOUDS:   mesh {i}: shader={mesh.ShaderName} tris={mesh.IndexCount / 3} diffuse={(mesh.DiffuseSRV != null)} embeddedDict={(mesh.EmbeddedDict != null)}{sb}");
                }
            }
            Console.WriteLine($"CLOUDS: built hat layer {layer.Filename}: {m.Meshes.Count} meshes, density={(density[0] != null)} diffuse={(m.Meshes[0].DiffuseSRV != null)}");
            return m;
        }

        public void Render(DeviceContext context, Matrix viewProjNoTrans, Vector3 camPos, Vector3 sunColour,
            float time, string fragName, GameFogVars fog, CloudLighting lighting = default)
        {
            LayersDrawn = 0;
            LayersWaiting = 0;
            frame++;
            if (!Ready) return;
            var frag = FindFrag(fragName) ?? FindFrag("contrails");
            if (frag == null || frag.Layers == null) { Status = "no frag " + fragName; return; }

            bool applied = false;
            for (int li = 0; li < frag.Layers.Length; li++)
            {
                var layer = frag.Layers[li];
                if (frag.ShowLayer != null && li < frag.ShowLayer.Length && !frag.ShowLayer[li]) continue;
                if (Math.Max(camPos.Z, 0.0f) < layer.HeightTigger) continue;
                var model = GetLayerModel(layer, out var density);
                if (model == null) { LayersWaiting++; continue; }

                if (!applied)
                {
                    shader.Apply(context);
                    context.VertexShader.SetConstantBuffer(0, cb.Buffer);
                    context.PixelShader.SetConstantBuffer(0, cb.Buffer);
                    context.PixelShader.SetSampler(0, CommonStates.LinearWrap);
                    context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
                    context.OutputMerger.SetDepthStencilState(CommonStates.DepthDisabled);
                    context.OutputMerger.SetBlendState(CommonStates.BlendAlpha);
                    context.Rasterizer.State = CommonStates.RasterSolid;
                    applied = true;
                }
                var vel = (frag.UVVelocity != null && li < frag.UVVelocity.Length) ? frag.UVVelocity[li] : Vector2.Zero;
                var v = new CloudVars
                {
                    ViewProjNoTrans = Matrix.Transpose(viewProjNoTrans),
                    Scale = new Vector4(frag.Scale * 0.05f, 1.0f),
                    SunColour = new Vector4(sunColour, 1.0f),
                    UvOffset = new Vector4(vel.X * time * 0.001f, vel.Y * time * 0.001f, 0, 0),
                    CamPos = new Vector4(camPos, 1.0f),
                    Fog = fog,
                };
                if (lighting.Keyframed && lighting.Kf.Valid)
                {
                    var k = lighting.Kf;
                    v.SunDir = new Vector4(lighting.SunDir, 1.0f);
                    v.CloudColour = new Vector4(k.CloudColor, lighting.HdrScale);
                    v.LightColour = new Vector4(k.LightColor, 1.0f);
                    v.AmbientColour = new Vector4(k.AmbientColor, 1.0f);
                    v.SkyColour = new Vector4(k.SkyColor * k.ScaleFillColors.X, 1.0f);
                    v.BounceColour = new Vector4(k.BounceColor * k.ScaleFillColors.Y, 1.0f);
                    v.EastMinusWestColour = new Vector4((k.EastColor - k.WestColor) * k.ScaleFillColors.Z, 1.0f);
                    v.WestColour = new Vector4(k.WestColor * k.ScaleFillColors.Z, 1.0f);
                    v.DensityShiftScale = k.DensityShift_Scale_ScatteringConst_Scale;
                    v.ScaleDiffuseFillAmbientWrap = k.ScaleDiffuseFillAmbient_WrapAmount;
                    v.Piercing = k.PiercingLightPower_Strength_NormalStrength_Thickness;
                }
                else
                {
                    v.SunDir = new Vector4(lighting.SunDir, 0.0f);
                    v.CloudColour = new Vector4(1, 1, 1, 1);
                    v.DensityShiftScale = new Vector4(0, 1, 0, 0);
                }
                meshParams_U25.TryGetValue(JenkHash.GenHash(layer.Filename.ToLowerInvariant()), out var mps);
                for (int mi = 0; mi < model.Meshes.Count; mi++)
                {
                    var mesh = model.Meshes[mi];
                    if (mesh.VB == null || mesh.IB == null) continue;
                    var mp = mps != null && mi < mps.Length ? mps[mi] : null;
                    ApplyMeshParams_U25(ref v, mp);
                    cb.Update(context, ref v);
                    context.PixelShader.SetShaderResource(1, mp?.Detail1);
                    context.PixelShader.SetShaderResource(2, mp?.Detail2);
                    context.PixelShader.SetShaderResource(0, density != null && mi < density.Length ? density[mi] : mesh.DiffuseSRV);
                    context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(mesh.VB, MeshVertex.Stride, 0));
                    context.InputAssembler.SetIndexBuffer(mesh.IB, Format.R16_UInt, 0);
                    context.DrawIndexed(mesh.IndexCount, 0, 0);
                }
                LayersDrawn++;
            }
            if (applied)
            {
                context.PixelShader.SetShaderResource(0, null);
                context.PixelShader.SetShaderResource(1, null);
                context.PixelShader.SetShaderResource(2, null);
                context.OutputMerger.SetBlendState(CommonStates.BlendOpaque);
                context.OutputMerger.SetDepthStencilState(CommonStates.DepthDefault);
                context.Rasterizer.State = CommonStates.RasterSolid;
            }
            Status = $"{fragName}: {LayersDrawn}/{frag.Layers.Length} layers" +
                     (LayersWaiting > 0 ? $" ({LayersWaiting} waiting for textures)" : "") +
                     (lighting.Keyframed && lighting.Kf.Valid ? $" [{lighting.Kf.SettingsName}]" : " [flat]");
        }

        public void Dispose()
        {
            foreach (var m in models.Values) m.Dispose();
            models.Clear();
            cb?.Dispose();
            shader?.Dispose();
        }
    }
}

