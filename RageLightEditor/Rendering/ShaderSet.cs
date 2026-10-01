using System;
using System.IO;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace RageLightEditor.Rendering
{
    public class ShaderSet : IDisposable
    {
        public VertexShader VS { get; private set; }
        public PixelShader PS { get; private set; }
        public InputLayout Layout { get; private set; }

        private static string LoadSource(string file)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var names = asm.GetManifestResourceNames();
            string match = Array.Find(names, n => n == file)
                        ?? Array.Find(names, n => n.EndsWith("." + file, StringComparison.OrdinalIgnoreCase))
                        ?? Array.Find(names, n => n.EndsWith(file, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                using var s = asm.GetManifestResourceStream(match);
                if (s != null)
                {
                    using var r = new StreamReader(s);
                    return r.ReadToEnd();
                }
            }
            var path = Path.Combine(AppContext.BaseDirectory, "Shaders", file);
            if (File.Exists(path)) return File.ReadAllText(path);
            throw new FileNotFoundException("Shader not found (embedded or on disk): " + file);
        }

        public ShaderSet(Device device, string file, InputElement[] elements)
            : this(device, LoadSource(file), "VSMain", "PSMain", elements, file)
        {
        }

        public static string Source(string file) => LoadSource(file);

        public ShaderSet(Device device, string source, string vsEntry, string psEntry, InputElement[] elements, string debugName)
        {
            using (var vsb = ShaderCache_U22.Compile(source, vsEntry, "vs_5_0", ShaderFlags.OptimizationLevel3, debugName))
            {
                VS = new VertexShader(device, vsb);
                if (elements != null) Layout = new InputLayout(device, vsb, elements);
            }
            using (var psb = ShaderCache_U22.Compile(source, psEntry, "ps_5_0", ShaderFlags.OptimizationLevel3, debugName))
            {
                PS = new PixelShader(device, psb);
            }
        }

        public void Apply(DeviceContext context)
        {
            context.InputAssembler.InputLayout = Layout;
            context.VertexShader.Set(VS);
            context.PixelShader.Set(PS);
        }

        public void Dispose()
        {
            Layout?.Dispose();
            PS?.Dispose();
            VS?.Dispose();
        }
    }

    public class ConstantBuffer<T> : IDisposable where T : struct
    {
        public Buffer Buffer { get; }
        private readonly int size;

        public ConstantBuffer(Device device)
        {
            size = (System.Runtime.InteropServices.Marshal.SizeOf<T>() + 15) & ~15;
            Buffer = new Buffer(device, size, ResourceUsage.Dynamic, BindFlags.ConstantBuffer,
                CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
        }

        public void Update(DeviceContext context, ref T value)
        {
            var box = context.MapSubresource(Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
            System.Runtime.InteropServices.Marshal.StructureToPtr(value, box.DataPointer, false);
            context.UnmapSubresource(Buffer, 0);
        }

        public void Dispose()
        {
            Buffer?.Dispose();
        }
    }

    public static partial class CommonStates
    {
        public static bool CullingEnabled = true;

        public static bool BackfaceCulling = true;
        public static int BackfaceMode = 0;

        public static DepthStencilState DepthDefault, DepthReadOnly, DepthDisabled, DepthEqual;
        public static DepthStencilState DepthLessDefault;
        public static BlendState BlendOpaque, BlendAlpha, BlendAdditive, BlendAdditiveAlpha, BlendNoColor, BlendMultiply;
        public static BlendState BlendOpaqueA2C, BlendAlphaA2C;
        public static RasterizerState RasterSolid, RasterSolidCullBack;
        public static RasterizerState RasterWireframe;
        public static RasterizerState RasterWireframeCullBack;
        public static RasterizerState RasterDecal, RasterDecalCullBack;
        public static RasterizerState RasterCullBackLH;
        public static SamplerState LinearWrap;
        public static SamplerState PointClamp;
        public static SamplerState LinearClamp;

        public static void Create(Device device)
        {
            DepthDefault = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.GreaterEqual,
            });
            DepthReadOnly = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.GreaterEqual,
            });
            DepthDisabled = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Always,
            });
            DepthEqual = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.GreaterEqual,
            });

            DepthLessDefault = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.LessEqual,
            });

            var opaque = new BlendStateDescription();
            opaque.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = false,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.Zero,
                BlendOperation = BlendOperation.Add,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.Zero,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            };
            BlendOpaque = new BlendState(device, opaque);

            var alpha = new BlendStateDescription();
            alpha.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = true,
                SourceBlend = BlendOption.SourceAlpha,
                DestinationBlend = BlendOption.InverseSourceAlpha,
                BlendOperation = BlendOperation.Add,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.InverseSourceAlpha,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            };
            BlendAlpha = new BlendState(device, alpha);

            // Alpha to coverage: what smooths a cutout's edge under MSAA. A leaf's shape
            // comes from the alpha test, which is a hard per-pixel yes/no, so multisampling
            // alone leaves it stair-stepped no matter how many samples are taken.
            var opaqueA2C = new BlendStateDescription { AlphaToCoverageEnable = true };
            opaqueA2C.RenderTarget[0] = opaque.RenderTarget[0];
            BlendOpaqueA2C = new BlendState(device, opaqueA2C);
            var alphaA2C = new BlendStateDescription { AlphaToCoverageEnable = true };
            alphaA2C.RenderTarget[0] = alpha.RenderTarget[0];
            BlendAlphaA2C = new BlendState(device, alphaA2C);

            var add = new BlendStateDescription();
            add.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = true,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.One,
                BlendOperation = BlendOperation.Add,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.One,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            };
            BlendAdditive = new BlendState(device, add);

            var addA = new BlendStateDescription();
            addA.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = true,
                SourceBlend = BlendOption.SourceAlpha,
                DestinationBlend = BlendOption.One,
                BlendOperation = BlendOperation.Add,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.One,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            };
            BlendAdditiveAlpha = new BlendState(device, addA);

            var mul = new BlendStateDescription();
            mul.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = true,
                SourceBlend = BlendOption.Zero,
                DestinationBlend = BlendOption.SourceColor,
                BlendOperation = BlendOperation.Add,
                SourceAlphaBlend = BlendOption.Zero,
                DestinationAlphaBlend = BlendOption.One,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            };
            BlendMultiply = new BlendState(device, mul);

            var noColor = new BlendStateDescription();
            noColor.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = false,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.Zero,
                BlendOperation = BlendOperation.Add,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.Zero,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = (ColorWriteMaskFlags)0,
            };
            BlendNoColor = new BlendState(device, noColor);

            RasterSolid = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true,
                IsMultisampleEnabled = true,
            });
            RasterWireframe = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.None,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true,
                IsMultisampleEnabled = true,
                IsAntialiasedLineEnabled = true,
            });
            RasterWireframeCullBack = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.Back,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true,
                IsMultisampleEnabled = true,
                IsAntialiasedLineEnabled = true,
            });

            RasterSolidCullBack = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.Back,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true,
                IsMultisampleEnabled = true,
            });

            RasterCullBackLH = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.Back,
                IsFrontCounterClockwise = false,
                IsDepthClipEnabled = true,
                IsMultisampleEnabled = true,
            });

            RasterDecal = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true,
                IsMultisampleEnabled = true,
                DepthBias = 96,
                SlopeScaledDepthBias = 2.0f,
                DepthBiasClamp = DecalBiasClamp,
            });
            RasterDecalCullBack = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.Back,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = true,
                IsMultisampleEnabled = true,
                DepthBias = 96,
                SlopeScaledDepthBias = 2.0f,
                DepthBiasClamp = DecalBiasClamp,
            });

            LinearWrap = new SamplerState(device, new SamplerStateDescription
            {
                Filter = Filter.Anisotropic,
                MaximumAnisotropy = 16,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunction = Comparison.Always,
                MinimumLod = 0,
                MaximumLod = float.MaxValue,
            });

            LinearClamp = new SamplerState(device, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunction = Comparison.Always,
                MinimumLod = 0,
                MaximumLod = float.MaxValue,
            });

            PointClamp = new SamplerState(device, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunction = Comparison.Always,
                MinimumLod = 0,
                MaximumLod = float.MaxValue,
            });
        }

        public static void Destroy()
        {
            LinearClamp?.Dispose();
            LinearWrap?.Dispose();
            RasterCullBackLH?.Dispose();
            RasterDecalCullBack?.Dispose();
            RasterDecal?.Dispose();
            RasterWireframe?.Dispose();
            RasterWireframeCullBack?.Dispose();
            RasterSolidCullBack?.Dispose();
            RasterSolid?.Dispose();
            BlendNoColor?.Dispose();
            DepthEqual?.Dispose();
            BlendAdditiveAlpha?.Dispose();
            BlendAdditive?.Dispose();
            BlendAlpha?.Dispose();
            BlendOpaque?.Dispose();
            DepthDisabled?.Dispose();
            DepthReadOnly?.Dispose();
            DepthDefault?.Dispose();
        }
    }
}

