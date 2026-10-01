using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImGuiNET;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace RageLightEditor.ImGuiBackend
{
    public class ImGuiRenderer : IDisposable
    {
        private readonly Device device;
        private readonly DeviceContext context;

        private Buffer vertexBuffer;
        private Buffer indexBuffer;
        private int vertexBufferSize = 5000;
        private int indexBufferSize = 10000;

        private VertexShader vs;
        private PixelShader ps;
        private InputLayout layout;
        private Buffer constantBuffer;
        private BlendState blendState;
        private RasterizerState rasterizerState;
        private DepthStencilState depthState;
        private SamplerState fontSampler;
        private ShaderResourceView fontSRV;

        private readonly Dictionary<IntPtr, ShaderResourceView> textureMap = new Dictionary<IntPtr, ShaderResourceView>();
        private int nextTextureId = 1;

        private const string ShaderSource = @"
cbuffer vertexBuffer : register(b0)
{
    float4x4 ProjectionMatrix;
};
struct VS_INPUT
{
    float2 pos : POSITION;
    float2 uv  : TEXCOORD0;
    float4 col : COLOR0;
};
struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};
PS_INPUT VSMain(VS_INPUT input)
{
    PS_INPUT output;
    output.pos = mul(ProjectionMatrix, float4(input.pos.xy, 0.f, 1.f));
    output.col = input.col;
    output.uv = input.uv;
    return output;
}
sampler sampler0 : register(s0);
Texture2D texture0 : register(t0);
float4 PSMain(PS_INPUT input) : SV_Target
{
    return input.col * texture0.Sample(sampler0, input.uv);
}
";

        public ImGuiRenderer(Device device, DeviceContext context)
        {
            this.device = device;
            this.context = context;
            CreateDeviceObjects();
        }

        private void CreateDeviceObjects()
        {
            using (var vsBlob = RageLightEditor.Rendering.ShaderCache_U22.Compile(ShaderSource, "VSMain", "vs_4_0", ShaderFlags.None, "imgui"))
            {
                vs = new VertexShader(device, vsBlob);
                layout = new InputLayout(device, vsBlob, new[]
                {
                    new InputElement("POSITION", 0, Format.R32G32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 8, 0),
                    new InputElement("COLOR", 0, Format.R8G8B8A8_UNorm, 16, 0),
                });
            }
            using (var psBlob = RageLightEditor.Rendering.ShaderCache_U22.Compile(ShaderSource, "PSMain", "ps_4_0", ShaderFlags.None, "imgui"))
            {
                ps = new PixelShader(device, psBlob);
            }

            constantBuffer = new Buffer(device, 64, ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);

            var blendDesc = new BlendStateDescription();
            blendDesc.RenderTarget[0] = new RenderTargetBlendDescription
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
            blendState = new BlendState(device, blendDesc);

            rasterizerState = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsScissorEnabled = true,
                IsDepthClipEnabled = true,
            });

            depthState = new DepthStencilState(device, new DepthStencilStateDescription
            {
                IsDepthEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Always,
                IsStencilEnabled = false,
            });

            fontSampler = new SamplerState(device, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunction = Comparison.Always,
                MinimumLod = 0,
                MaximumLod = 0,
            });

            RecreateFontTexture();
        }

        public unsafe void RecreateFontTexture()
        {
            var io = ImGui.GetIO();
            io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);

            fontSRV?.Dispose();
            using (var tex = new Texture2D(device, new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
            }, new DataRectangle((IntPtr)pixels, width * 4)))
            {
                fontSRV = new ShaderResourceView(device, tex);
            }

            io.Fonts.SetTexID(RegisterTexture(fontSRV));
            io.Fonts.ClearTexData();
        }

        public IntPtr RegisterTexture(ShaderResourceView srv)
        {
            var id = (IntPtr)nextTextureId++;
            textureMap[id] = srv;
            return id;
        }

        public void UnregisterTexture(IntPtr id)
        {
            textureMap.Remove(id);
        }

        public void Render(ImDrawDataPtr drawData)
        {
            if (drawData.CmdListsCount == 0) return;
            if (drawData.DisplaySize.X <= 0 || drawData.DisplaySize.Y <= 0) return;

            if (vertexBuffer == null || vertexBufferSize < drawData.TotalVtxCount)
            {
                vertexBuffer?.Dispose();
                vertexBufferSize = drawData.TotalVtxCount + 5000;
                vertexBuffer = new Buffer(device, vertexBufferSize * Unsafe.SizeOfImDrawVert, ResourceUsage.Dynamic,
                    BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
            }
            if (indexBuffer == null || indexBufferSize < drawData.TotalIdxCount)
            {
                indexBuffer?.Dispose();
                indexBufferSize = drawData.TotalIdxCount + 10000;
                indexBuffer = new Buffer(device, indexBufferSize * sizeof(ushort), ResourceUsage.Dynamic,
                    BindFlags.IndexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
            }

            var vtxData = context.MapSubresource(vertexBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            var idxData = context.MapSubresource(indexBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            unsafe
            {
                var vtxDst = (byte*)vtxData.DataPointer;
                var idxDst = (byte*)idxData.DataPointer;
                for (int n = 0; n < drawData.CmdListsCount; n++)
                {
                    var cmdList = drawData.CmdLists[n];
                    int vtxBytes = cmdList.VtxBuffer.Size * Unsafe.SizeOfImDrawVert;
                    int idxBytes = cmdList.IdxBuffer.Size * sizeof(ushort);
                    System.Buffer.MemoryCopy((void*)cmdList.VtxBuffer.Data, vtxDst, vtxBytes, vtxBytes);
                    System.Buffer.MemoryCopy((void*)cmdList.IdxBuffer.Data, idxDst, idxBytes, idxBytes);
                    vtxDst += vtxBytes;
                    idxDst += idxBytes;
                }
            }
            context.UnmapSubresource(vertexBuffer, 0);
            context.UnmapSubresource(indexBuffer, 0);

            var L = drawData.DisplayPos.X;
            var R = drawData.DisplayPos.X + drawData.DisplaySize.X;
            var T = drawData.DisplayPos.Y;
            var B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;
            var mvp = new Matrix(
                2.0f / (R - L), 0, 0, 0,
                0, 2.0f / (T - B), 0, 0,
                0, 0, 0.5f, 0,
                (R + L) / (L - R), (T + B) / (B - T), 0.5f, 1.0f);
            var cbData = context.MapSubresource(constantBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            unsafe { *(Matrix*)cbData.DataPointer = mvp; }
            context.UnmapSubresource(constantBuffer, 0);

            SetupRenderState(drawData);

            int vtxOffset = 0;
            int idxOffset = 0;
            var clipOff = drawData.DisplayPos;
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];
                for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
                {
                    var cmd = cmdList.CmdBuffer[i];
                    if (cmd.UserCallback != IntPtr.Zero)
                    {
                        continue;
                    }
                    context.Rasterizer.SetScissorRectangle(
                        (int)(cmd.ClipRect.X - clipOff.X), (int)(cmd.ClipRect.Y - clipOff.Y),
                        (int)(cmd.ClipRect.Z - clipOff.X), (int)(cmd.ClipRect.W - clipOff.Y));

                    if (textureMap.TryGetValue(cmd.TextureId, out var srv))
                    {
                        context.PixelShader.SetShaderResource(0, srv);
                    }
                    context.DrawIndexed((int)cmd.ElemCount, (int)(cmd.IdxOffset + idxOffset), (int)(cmd.VtxOffset + vtxOffset));
                }
                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }
        }

        private void SetupRenderState(ImDrawDataPtr drawData)
        {
            context.InputAssembler.InputLayout = layout;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Unsafe.SizeOfImDrawVert, 0));
            context.InputAssembler.SetIndexBuffer(indexBuffer, Format.R16_UInt, 0);
            context.VertexShader.Set(vs);
            context.VertexShader.SetConstantBuffer(0, constantBuffer);
            context.PixelShader.Set(ps);
            context.PixelShader.SetSampler(0, fontSampler);
            context.GeometryShader.Set(null);
            context.HullShader.Set(null);
            context.DomainShader.Set(null);
            context.OutputMerger.SetBlendState(blendState, new RawColor4(0, 0, 0, 0), -1);
            context.OutputMerger.SetDepthStencilState(depthState);
            context.Rasterizer.State = rasterizerState;
        }

        public void Dispose()
        {
            fontSRV?.Dispose();
            fontSampler?.Dispose();
            depthState?.Dispose();
            rasterizerState?.Dispose();
            blendState?.Dispose();
            constantBuffer?.Dispose();
            layout?.Dispose();
            ps?.Dispose();
            vs?.Dispose();
            indexBuffer?.Dispose();
            vertexBuffer?.Dispose();
        }

        private static class Unsafe
        {
            public static readonly int SizeOfImDrawVert = Marshal.SizeOf<ImDrawVert>();
        }
    }

    public struct RawColor4
    {
        public float R, G, B, A;
        public RawColor4(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }
        public static implicit operator SharpDX.Mathematics.Interop.RawColor4(RawColor4 c)
            => new SharpDX.Mathematics.Interop.RawColor4(c.R, c.G, c.B, c.A);
    }
}

