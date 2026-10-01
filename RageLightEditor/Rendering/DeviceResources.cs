using System;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace RageLightEditor.Rendering
{
    public class DeviceResources : IDisposable
    {
        public static bool EnableDebugLayer = false;
        public Device Device { get; private set; }
        public DeviceContext Context { get; private set; }
        public SwapChain SwapChain { get; private set; }
        public RenderTargetView BackbufferRTV { get; private set; }
        public DepthStencilView DepthDSV { get; private set; }
        public ShaderResourceView DepthSRV { get; private set; }
        public ShaderResourceView SceneDepthRawSRV { get; private set; }
        private DepthStencilView depthDsvReadOnly, depthMSDsvReadOnly;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public int SampleCount { get; private set; } = 1;
        public bool Multisampled => SampleCount > 1;

        public ShaderResourceView DepthMsSRV { get; private set; }
        public RenderTargetView DepthFlatRTV { get; private set; }

        private Texture2D sceneColourMS, depthbufferMS, depthFlat;
        private RenderTargetView sceneMSRtv;
        private DepthStencilView depthMSDsv;

        private Texture2D backbuffer;
        private Texture2D depthbuffer;

        private Texture2D sceneColour;
        public RenderTargetView SceneRTV { get; private set; }
        public ShaderResourceView SceneSRV { get; private set; }

        public DeviceResources(IntPtr windowHandle, int width, int height)
        {
            Width = Math.Max(width, 8);
            Height = Math.Max(height, 8);

            var scd = new SwapChainDescription
            {
                BufferCount = 2,
                ModeDescription = new ModeDescription(Width, Height, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = windowHandle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = FlipModel ? SwapEffect.FlipDiscard : SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput,
                Flags = SwapChainFlags.None,
            };

            var levels = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1 };
            Device dev = null;
            if (EnableDebugLayer)
            {
                try
                {
                    Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.Debug, levels, scd, out dev, out var scdbg);
                    SwapChain = scdbg;
                }
                catch { dev = null; }
            }
            if (dev == null)
            {
                try
                {
                    Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport, levels, scd, out dev, out var sc);
                    SwapChain = sc;
                }
                catch
                {
                    Device.CreateWithSwapChain(DriverType.Warp, DeviceCreationFlags.BgraSupport, levels, scd, out dev, out var sc);
                    SwapChain = sc;
                }
            }
            Device = dev;
            Context = Device.ImmediateContext;

            using (var factory = SwapChain.GetParent<Factory>())
            {
                factory.MakeWindowAssociation(windowHandle, WindowAssociationFlags.IgnoreAltEnter);
            }
            if (FlipModel)
            {
                try { using (var dxgiDev = Device.QueryInterface<SharpDX.DXGI.Device1>()) dxgiDev.MaximumFrameLatency = 2; } catch { }
            }

            CreateSizedResources();
        }

        private void CreateSizedResources()
        {
            backbuffer = Texture2D.FromSwapChain<Texture2D>(SwapChain, 0);
            BackbufferRTV = new RenderTargetView(Device, backbuffer);

            depthbuffer = new Texture2D(Device, new Texture2DDescription
            {
                Width = Width,
                Height = Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R32_Typeless,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
            });
            DepthDSV = new DepthStencilView(Device, depthbuffer, new DepthStencilViewDescription
            {
                Format = Format.D32_Float,
                Dimension = DepthStencilViewDimension.Texture2D,
            });
            DepthSRV = new ShaderResourceView(Device, depthbuffer, new ShaderResourceViewDescription
            {
                Format = Format.R32_Float,
                Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
                Texture2D = new ShaderResourceViewDescription.Texture2DResource { MipLevels = 1 },
            });
            SceneDepthRawSRV = new ShaderResourceView(Device, depthbuffer, new ShaderResourceViewDescription
            {
                Format = Format.R32_Float,
                Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
                Texture2D = new ShaderResourceViewDescription.Texture2DResource { MipLevels = 1 },
            });
            depthDsvReadOnly = new DepthStencilView(Device, depthbuffer, new DepthStencilViewDescription
            {
                Format = Format.D32_Float,
                Dimension = DepthStencilViewDimension.Texture2D,
                Flags = DepthStencilViewFlags.ReadOnlyDepth,
            });

            sceneColour = new Texture2D(Device, new Texture2DDescription
            {
                Width = Width,
                Height = Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R16G16B16A16_Float,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            });
            SceneRTV = new RenderTargetView(Device, sceneColour);
            SceneSRV = new ShaderResourceView(Device, sceneColour);

            if (SampleCount > 1) CreateMultisampledResources();
        }

        private void CreateMultisampledResources()
        {
            var sd = new SampleDescription(SampleCount, 0);

            sceneColourMS = new Texture2D(Device, new Texture2DDescription
            {
                Width = Width, Height = Height, MipLevels = 1, ArraySize = 1,
                Format = Format.R16G16B16A16_Float, SampleDescription = sd,
                Usage = ResourceUsage.Default, BindFlags = BindFlags.RenderTarget,
            });
            sceneMSRtv = new RenderTargetView(Device, sceneColourMS);

            depthbufferMS = new Texture2D(Device, new Texture2DDescription
            {
                Width = Width, Height = Height, MipLevels = 1, ArraySize = 1,
                Format = Format.R32_Typeless, SampleDescription = sd,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
            });
            depthMSDsv = new DepthStencilView(Device, depthbufferMS, new DepthStencilViewDescription
            {
                Format = Format.D32_Float,
                Dimension = DepthStencilViewDimension.Texture2DMultisampled,
            });
            depthMSDsvReadOnly = new DepthStencilView(Device, depthbufferMS, new DepthStencilViewDescription
            {
                Format = Format.D32_Float,
                Dimension = DepthStencilViewDimension.Texture2DMultisampled,
                Flags = DepthStencilViewFlags.ReadOnlyDepth,
            });
            DepthMsSRV = new ShaderResourceView(Device, depthbufferMS, new ShaderResourceViewDescription
            {
                Format = Format.R32_Float,
                Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2DMultisampled,
            });

            depthFlat = new Texture2D(Device, new Texture2DDescription
            {
                Width = Width, Height = Height, MipLevels = 1, ArraySize = 1,
                Format = Format.R32_Float, SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            });
            DepthFlatRTV = new RenderTargetView(Device, depthFlat);

            DepthSRV?.Dispose();
            DepthSRV = new ShaderResourceView(Device, depthFlat);
        }

        private void ReleaseMultisampledResources()
        {
            DepthFlatRTV?.Dispose(); DepthFlatRTV = null;
            DepthMsSRV?.Dispose(); DepthMsSRV = null;
            depthMSDsvReadOnly?.Dispose(); depthMSDsvReadOnly = null;
            depthMSDsv?.Dispose(); depthMSDsv = null;
            sceneMSRtv?.Dispose(); sceneMSRtv = null;
            depthFlat?.Dispose(); depthFlat = null;
            depthbufferMS?.Dispose(); depthbufferMS = null;
            sceneColourMS?.Dispose(); sceneColourMS = null;
        }

        public int SetSampleCount(int samples)
        {
            if (DeviceLost) return SampleCount;
            samples = Math.Max(1, Math.Min(8, samples));
            while (samples > 1 && !SupportsSamples(samples)) samples /= 2;
            if (samples == SampleCount) return SampleCount;

            Context.OutputMerger.SetTargets((DepthStencilView)null, (RenderTargetView)null);
            ReleaseMultisampledResources();
            SampleCount = samples;
            if (samples > 1) CreateMultisampledResources();
            else
            {
                DepthSRV?.Dispose();
                DepthSRV = new ShaderResourceView(Device, depthbuffer, new ShaderResourceViewDescription
                {
                    Format = Format.R32_Float,
                    Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
                    Texture2D = new ShaderResourceViewDescription.Texture2DResource { MipLevels = 1 },
                });
            }
            return SampleCount;
        }

        private bool SupportsSamples(int n)
        {
            try
            {
                return Device.CheckMultisampleQualityLevels(Format.R16G16B16A16_Float, n) > 0
                    && Device.CheckMultisampleQualityLevels(Format.D32_Float, n) > 0;
            }
            catch { return false; }
        }

        public void ResolveFrame(Action<ShaderResourceView, RenderTargetView> depthResolve)
        {
            if (SampleCount <= 1 || sceneColourMS == null) return;
            Context.OutputMerger.SetTargets((DepthStencilView)null, (RenderTargetView)null);
            Context.ResolveSubresource(sceneColourMS, 0, sceneColour, 0, Format.R16G16B16A16_Float);
            depthResolve?.Invoke(DepthMsSRV, DepthFlatRTV);
        }

        public bool DepthDsvValid => SampleCount <= 1 || depthCopiedThisFrame;
        private bool depthCopiedThisFrame;

        public void ResolveDepthIntoDsv(Action<ShaderResourceView, DepthStencilView, int, int> copy)
        {
            if (SampleCount <= 1 || depthFlat == null || DepthDSV == null || copy == null) return;
            copy(DepthSRV, DepthDSV, Width, Height);
            depthCopiedThisFrame = true;
        }

        public void Resize(int width, int height)
        {
            if (DeviceLost) return;
            if (width < 8 || height < 8) return;
            if (width == Width && height == Height) return;
            Width = width;
            Height = height;

            Context.OutputMerger.SetTargets((DepthStencilView)null, (RenderTargetView)null);
            ReleaseMultisampledResources();
            BackbufferRTV?.Dispose();
            DepthSRV?.Dispose();
            SceneDepthRawSRV?.Dispose(); SceneDepthRawSRV = null;
            depthDsvReadOnly?.Dispose(); depthDsvReadOnly = null;
            DepthDSV?.Dispose();
            SceneRTV?.Dispose();
            SceneSRV?.Dispose();
            sceneColour?.Dispose();
            backbuffer?.Dispose();
            depthbuffer?.Dispose();

            SwapChain.ResizeBuffers(2, Width, Height, Format.R8G8B8A8_UNorm, SwapChainFlags.None);
            CreateSizedResources();
        }

        public static float DebugDepthClear = 0.0f;

        public void BeginFrame(Color4 clearColour)
        {
            Context.Rasterizer.SetViewport(0, 0, Width, Height);
            depthCopiedThisFrame = false;
            var rtv = SampleCount > 1 ? sceneMSRtv : SceneRTV;
            var dsv = SampleCount > 1 ? depthMSDsv : DepthDSV;
            Context.OutputMerger.SetTargets(dsv, rtv);
            Context.ClearRenderTargetView(rtv, clearColour);
            Context.ClearDepthStencilView(dsv, DepthStencilClearFlags.Depth, DebugDepthClear, 0);
        }

        public int BeginDepthRead(out ShaderResourceView depth)
        {
            var rtv = SampleCount > 1 ? sceneMSRtv : SceneRTV;
            var dsv = SampleCount > 1 ? depthMSDsvReadOnly : depthDsvReadOnly;
            if (dsv == null) { depth = null; return 0; }
            Context.OutputMerger.SetTargets(dsv, rtv);
            depth = SampleCount > 1 ? DepthMsSRV : SceneDepthRawSRV;
            return SampleCount > 1 ? 2 : 1;
        }

        public void ClearDepthOnly()
        {
            var dsv = SampleCount > 1 ? depthMSDsv : DepthDSV;
            if (dsv == null) return;
            Context.ClearDepthStencilView(dsv, DepthStencilClearFlags.Depth, DebugDepthClear, 0);
        }

        public void EndDepthRead()
        {
            var rtv = SampleCount > 1 ? sceneMSRtv : SceneRTV;
            var dsv = SampleCount > 1 ? depthMSDsv : DepthDSV;
            Context.OutputMerger.SetTargets(dsv, rtv);
        }

        public bool BeginBackbufferWithDepth_U24()
        {
            Context.Rasterizer.SetViewport(0, 0, Width, Height);
            if (!DepthDsvValid || depthDsvReadOnly == null) { Context.OutputMerger.SetTargets((DepthStencilView)null, BackbufferRTV); return false; }
            Context.OutputMerger.SetTargets(depthDsvReadOnly, BackbufferRTV);
            return true;
        }

        public void BeginBackbuffer()
        {
            Context.Rasterizer.SetViewport(0, 0, Width, Height);
            Context.OutputMerger.SetTargets((DepthStencilView)null, BackbufferRTV);
        }

        public int SyncInterval = 1;
        public static readonly bool FlipModel = Environment.GetEnvironmentVariable("RLE_BLITSWAP") != "1";

        public bool DeviceLost { get; private set; }

        public string DeviceLostReason { get; private set; } = "";

        public static bool DebugFakeDeviceLoss;

        public void Present()
        {
            if (DeviceLost) return;
            var r = SwapChain.TryPresent(SyncInterval, PresentFlags.None);
            if (DebugFakeDeviceLoss) r = SharpDX.DXGI.ResultCode.DeviceRemoved.Result;
            if (r.Success) return;
            DeviceLostReason = $"present={Describe(r)} device={Describe(Device.DeviceRemovedReason)}";
            DeviceLost = true;
        }

        private static string Describe(SharpDX.Result r)
        {
            if (SharpDX.DXGI.ResultCode.DeviceRemoved == r) return "DeviceRemoved";
            if (SharpDX.DXGI.ResultCode.DeviceHung == r) return "DeviceHung";
            if (SharpDX.DXGI.ResultCode.DeviceReset == r) return "DeviceReset";
            if (SharpDX.DXGI.ResultCode.DriverInternalError == r) return "DriverInternalError";
            if (SharpDX.DXGI.ResultCode.AccessLost == r) return "AccessLost";
            if (SharpDX.Result.Ok == r) return "Ok";
            return $"0x{r.Code:X8}";
        }

        private Texture2D sceneCopy;
        private int sceneCopyW, sceneCopyH;
        public ShaderResourceView SceneCopySRV { get; private set; }

        public ShaderResourceView CopySceneForRefraction()
        {
            if (DeviceLost || Width < 8 || Height < 8) return null;
            if (sceneCopy == null || sceneCopyW != Width || sceneCopyH != Height)
            {
                SceneCopySRV?.Dispose(); sceneCopy?.Dispose();
                sceneCopy = new Texture2D(Device, new Texture2DDescription
                {
                    Width = Width, Height = Height, MipLevels = 1, ArraySize = 1,
                    Format = Format.R16G16B16A16_Float, SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default, BindFlags = BindFlags.ShaderResource,
                });
                SceneCopySRV = new ShaderResourceView(Device, sceneCopy);
                sceneCopyW = Width; sceneCopyH = Height;
            }
            if (SampleCount > 1 && sceneColourMS != null)
                Context.ResolveSubresource(sceneColourMS, 0, sceneCopy, 0, Format.R16G16B16A16_Float);
            else
                Context.CopyResource(sceneColour, sceneCopy);
            return SceneCopySRV;
        }

        public void Dispose()
        {
            SceneCopySRV?.Dispose(); sceneCopy?.Dispose();
            ReleaseMultisampledResources();
            DepthSRV?.Dispose();
            DepthDSV?.Dispose();
            BackbufferRTV?.Dispose();
            SceneRTV?.Dispose();
            SceneSRV?.Dispose();
            sceneColour?.Dispose();
            depthbuffer?.Dispose();
            backbuffer?.Dispose();
            SwapChain?.Dispose();
            Context?.Dispose();
            Device?.Dispose();
        }
    }
}

