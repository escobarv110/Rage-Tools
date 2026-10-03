using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace RageLightEditor.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PostFxVars
    {
        public float FilmicA, FilmicB, FilmicC, FilmicD;
        public float FilmicE, FilmicF, FilmicW, Exposure;
        public Vector4 ColorCorrectHighLum;
        public Vector4 ColorShiftLowLum;
        public float Desaturate;
        public float Gamma;
        public uint EnableColorCorrect;

        public uint Cinematic;
        public float BloomStrength;
        public float SsrIntensity;
        public float DofStrength;
        public float CinePad0;
        public Vector4 PixelSize;

        public float UseFxaa;
        public float Vignette;
        public float Grain;
        public float GrainTime;
        public float ChromAberration;
        public float Sharpen;
        public float Contrast;
        public float Saturation;
        public float Temperature;
        public float TintGM;
        public float Letterbox;
        public float Halation;

        public Vector4 HalationTint;

        public float GrainSize;
        public float GrainColour;
        public float GrainShadow;
        public float VignetteRoundness;

        public float VignetteSoftness;
        public float EdgeBlur;
        public float EdgeBlurStart;
        public float EdgeBlurElongation;

        public float Dither;
        public float Lift;
        public float Gain;
        public float Bleach;

        public float HalationThreshold;
        public float HalationSaturation;
        public float HalationSoftness;
        public float HdrScene;

        public uint PostPass;
        public uint RageTonemap;
        public float LumBlend;
        public float BloomAmount;
        public Vector4 PostTexel;
        public float ExposureBias;
        public float AutoExposure;
        public float BloomThresholdHdr;
        public float Passthrough;

        public Vector4 UnderwaterParams;
        public Vector4 UnderwaterColour;
        public Vector4 UnderwaterProj;
        public Vector4 UnderwaterSun;
        public Vector4 UnderwaterUp;

        public Vector4 ExposureGame;
        public Vector4 MaxBackground;
        public Vector4 ClearColour;
    }

    public class PostFxRenderer : IDisposable
    {
        private readonly Device device;
        private readonly ShaderSet shader;
        private readonly ConstantBuffer<PostFxVars> cb;

        public PostFxVars Vars = new PostFxVars
        {
            FilmicA = 0.22f, FilmicB = 0.30f, FilmicC = 0.10f, FilmicD = 0.20f,
            FilmicE = 0.01f, FilmicF = 0.30f, FilmicW = 4.0f, Exposure = 1.0f,
            ColorCorrectHighLum = new Vector4(0.451f, 0.478f, 0.443f, 0.0f),
            ColorShiftLowLum = new Vector4(0.0f, 0.0f, 0.0f, 0.0f),
            Desaturate = 0.70f,
            Gamma = 1.0f / 2.2f,
            EnableColorCorrect = 1,
            BloomStrength = 0.55f,
            Contrast = 1.0f,
            Saturation = 1.0f,
            Gain = 1.0f,
            RageTonemap = 1,
            BloomAmount = 1.0f,
            ExposureBias = 1.0f,
            AutoExposure = 1.0f,
            BloomThresholdHdr = 50.0f,
        };

        private const int LumSize = 128;
        private const int LumMips = 8;
        private Texture2D lumSmall, lumA, lumB, lumStaging, bloomA, bloomB;
        private RenderTargetView lumSmallRtv, lumARtv, lumBRtv, bloomARtv, bloomBRtv;
        private ShaderResourceView lumSmallSrv, lumASrv, lumBSrv, bloomASrv, bloomBSrv;
        private bool lumInA;
        private bool lumInitialised;
        private int bloomW, bloomH;
        public float LastAvgLum { get; private set; } = -1.0f;
        public ShaderResourceView RageBloomSRV => bloomReady ? bloomASrv : null;
        private bool bloomReady;

        public PostFxRenderer(Device device)
        {
            this.device = device;
            shader = new ShaderSet(device, "postfx.hlsl", null);
            cb = new ConstantBuffer<PostFxVars>(device);
            CreateLumTargets();
        }

        private void CreateLumTargets()
        {
            lumSmall = new Texture2D(device, new Texture2DDescription
            {
                Width = LumSize, Height = LumSize, MipLevels = LumMips, ArraySize = 1,
                Format = Format.R16_Float, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default, BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                OptionFlags = ResourceOptionFlags.GenerateMipMaps,
            });
            lumSmallRtv = new RenderTargetView(device, lumSmall, new RenderTargetViewDescription
            {
                Format = Format.R16_Float, Dimension = RenderTargetViewDimension.Texture2D,
                Texture2D = new RenderTargetViewDescription.Texture2DResource { MipSlice = 0 },
            });
            lumSmallSrv = new ShaderResourceView(device, lumSmall);
            Texture2D One(out RenderTargetView rtv, out ShaderResourceView srv)
            {
                var t = new Texture2D(device, new Texture2DDescription
                {
                    Width = 1, Height = 1, MipLevels = 1, ArraySize = 1,
                    Format = Format.R32_Float, SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default, BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                });
                rtv = new RenderTargetView(device, t);
                srv = new ShaderResourceView(device, t);
                return t;
            }
            lumA = One(out lumARtv, out lumASrv);
            lumB = One(out lumBRtv, out lumBSrv);
            lumStaging = new Texture2D(device, new Texture2DDescription
            {
                Width = 1, Height = 1, MipLevels = 1, ArraySize = 1,
                Format = Format.R32_Float, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging, BindFlags = BindFlags.None, CpuAccessFlags = CpuAccessFlags.Read,
            });
        }

        private void EnsureBloomTargets(int w, int h)
        {
            int bw = Math.Max(w / 4, 1), bh = Math.Max(h / 4, 1);
            if (bloomA != null && bw == bloomW && bh == bloomH) return;
            bloomASrv?.Dispose(); bloomARtv?.Dispose(); bloomA?.Dispose();
            bloomBSrv?.Dispose(); bloomBRtv?.Dispose(); bloomB?.Dispose();
            Texture2D Make(out RenderTargetView rtv, out ShaderResourceView srv)
            {
                var t = new Texture2D(device, new Texture2DDescription
                {
                    Width = bw, Height = bh, MipLevels = 1, ArraySize = 1,
                    Format = Format.R16G16B16A16_Float, SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default, BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                });
                rtv = new RenderTargetView(device, t);
                srv = new ShaderResourceView(device, t);
                return t;
            }
            bloomA = Make(out bloomARtv, out bloomASrv);
            bloomB = Make(out bloomBRtv, out bloomBSrv);
            bloomW = bw; bloomH = bh;
        }

        private void SetPipeline(DeviceContext context)
        {
            shader.Apply(context);
            context.VertexShader.SetConstantBuffer(0, cb.Buffer);
            context.PixelShader.SetConstantBuffer(0, cb.Buffer);
            context.PixelShader.SetSampler(0, CommonStates.PointClamp);
            context.PixelShader.SetSampler(1, CommonStates.LinearClamp);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding());
            context.InputAssembler.SetIndexBuffer(null, Format.Unknown, 0);
            context.OutputMerger.SetDepthStencilState(CommonStates.DepthDisabled);
            context.OutputMerger.SetBlendState(CommonStates.BlendOpaque);
            context.Rasterizer.State = CommonStates.RasterSolid;
        }

        private void RunPass(DeviceContext context, uint pass, RenderTargetView target, int w, int h,
            ShaderResourceView src, ref PostFxVars v)
        {
            v.PostPass = pass;
            v.PostTexel = new Vector4(1.0f / Math.Max(w, 1), 1.0f / Math.Max(h, 1), w, h);
            cb.Update(context, ref v);
            context.OutputMerger.SetTargets((DepthStencilView)null, target);
            context.Rasterizer.SetViewport(0, 0, w, h);
            context.PixelShader.SetShaderResource(0, src);
            context.Draw(3, 0);
            context.PixelShader.SetShaderResource(0, null);
        }

        public void Prepare(DeviceContext context, ShaderResourceView sceneSrv, int width, int height,
            float dt, bool bloom, bool readback = false)
        {
            bloomReady = false;
            if (Vars.Cinematic != 0 || (Vars.RageTonemap == 0 && Vars.HdrScene < 0.5f) || sceneSrv == null) return;
            var v = Vars;
            SetPipeline(context);
            for (int i = 5; i <= 8; i++) context.PixelShader.SetShaderResource(i, null);

            RunPass(context, 1, lumSmallRtv, LumSize, LumSize, sceneSrv, ref v);
            context.OutputMerger.SetTargets((DepthStencilView)null, (RenderTargetView)null);
            context.GenerateMips(lumSmallSrv);

            float adaptRate = Vars.ExposureGame.X > 0.5f ? 9.0f : 2.0f;
            v.LumBlend = lumInitialised ? Math.Min(Math.Max(dt, 0.0f) * adaptRate, 1.0f) : 1.0f;
            var prevSrv = lumInA ? lumASrv : lumBSrv;
            var nextRtv = lumInA ? lumBRtv : lumARtv;
            context.PixelShader.SetShaderResource(5, lumSmallSrv);
            context.PixelShader.SetShaderResource(6, prevSrv);
            RunPass(context, 2, nextRtv, 1, 1, null, ref v);
            context.PixelShader.SetShaderResource(5, null);
            context.PixelShader.SetShaderResource(6, null);
            lumInA = !lumInA;
            lumInitialised = true;
            var curSrv = lumInA ? lumASrv : lumBSrv;
            var curTex = lumInA ? lumA : lumB;

            if (bloom && Vars.BloomAmount > 0.001f)
            {
                EnsureBloomTargets(width, height);
                context.PixelShader.SetShaderResource(7, curSrv);
                RunPass(context, 3, bloomARtv, bloomW, bloomH, sceneSrv, ref v);
                context.PixelShader.SetShaderResource(7, null);
                RunPass(context, 4, bloomBRtv, bloomW, bloomH, bloomASrv, ref v);
                RunPass(context, 5, bloomARtv, bloomW, bloomH, bloomBSrv, ref v);
                bloomReady = true;
            }
            context.OutputMerger.SetTargets((DepthStencilView)null, (RenderTargetView)null);

            if (readback)
            {
                try
                {
                    context.CopyResource(curTex, lumStaging);
                    var box = context.MapSubresource(lumStaging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                    LastAvgLum = SharpDX.Utilities.Read<float>(box.DataPointer);
                    context.UnmapSubresource(lumStaging, 0);
                }
                catch { LastAvgLum = -1.0f; }
            }
        }

        public void Composite(DeviceContext context, ShaderResourceView sceneSrv,
            ShaderResourceView aoSrv = null, ShaderResourceView bloomSrv = null,
            ShaderResourceView ssrSrv = null, ShaderResourceView dofSrv = null,
            ShaderResourceView depthSrv = null)
        {
            var v = Vars;
            v.PostPass = 0;
            cb.Update(context, ref v);
            SetPipeline(context);
            context.PixelShader.SetShaderResource(0, sceneSrv);
            context.PixelShader.SetShaderResource(1, aoSrv);
            context.PixelShader.SetShaderResource(2, bloomSrv);
            context.PixelShader.SetShaderResource(3, ssrSrv);
            context.PixelShader.SetShaderResource(4, dofSrv);
            context.PixelShader.SetShaderResource(7, lumInA ? lumASrv : lumBSrv);
            context.PixelShader.SetShaderResource(8, bloomReady ? bloomASrv : null);
            context.PixelShader.SetShaderResource(9, Vars.UnderwaterParams.X > 0.5f || Vars.MaxBackground.W > 0.5f ? depthSrv : null);
            context.Draw(3, 0);
            for (int i = 0; i <= 9; i++) context.PixelShader.SetShaderResource(i, null);
        }

        public void Dispose()
        {
            bloomASrv?.Dispose(); bloomARtv?.Dispose(); bloomA?.Dispose();
            bloomBSrv?.Dispose(); bloomBRtv?.Dispose(); bloomB?.Dispose();
            lumStaging?.Dispose();
            lumASrv?.Dispose(); lumARtv?.Dispose(); lumA?.Dispose();
            lumBSrv?.Dispose(); lumBRtv?.Dispose(); lumB?.Dispose();
            lumSmallSrv?.Dispose(); lumSmallRtv?.Dispose(); lumSmall?.Dispose();
            cb?.Dispose();
            shader?.Dispose();
        }
    }
}

