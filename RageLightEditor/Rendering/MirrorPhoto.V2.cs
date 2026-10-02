using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace RageLightEditor.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PhotoVertex_V2
    {
        public Vector3 Pos;
        public Vector2 Uv;
        public const int Stride = 20;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PhotoVars_V2
    {
        public Matrix ViewProj;
        public Vector4 Tint;
        public Vector4 Frame;
    }

    public sealed class MirrorPhotoRenderer_V2 : IDisposable
    {
        private readonly Device device;
        private readonly ShaderSet shader;
        private readonly ConstantBuffer<PhotoVars_V2> cbuffer;
        private Buffer vbuffer;
        private readonly PhotoVertex_V2[] verts = new PhotoVertex_V2[6];

        private static readonly Vector2[] Corners =
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1),
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
        };

        public MirrorPhotoRenderer_V2(Device device)
        {
            this.device = device;
            shader = new ShaderSet(device, "mirrorphoto.hlsl", new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
            });
            cbuffer = new ConstantBuffer<PhotoVars_V2>(device);
            vbuffer = new Buffer(device, 6 * PhotoVertex_V2.Stride, ResourceUsage.Dynamic,
                BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
        }

        public void Draw(DeviceContext context, Matrix viewProj, ShaderResourceView srv,
                         Vector3 centre, Vector3 right, Vector3 up, Vector4 tint, float border, float feather = 0f)
        {
            if (srv == null || context == null) return;

            for (int i = 0; i < 6; i++)
            {
                var c = Corners[i];
                verts[i].Pos = centre + right * (c.X * 2f - 1f) - up * (c.Y * 2f - 1f);
                verts[i].Uv = c;
            }

            var box = context.MapSubresource(vbuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            unsafe
            {
                var dst = (PhotoVertex_V2*)box.DataPointer;
                for (int i = 0; i < 6; i++) dst[i] = verts[i];
            }
            context.UnmapSubresource(vbuffer, 0);

            var vars = new PhotoVars_V2
            {
                ViewProj = Matrix.Transpose(viewProj),
                Tint = tint,
                Frame = new Vector4(Math.Clamp(border, 0f, 0.25f), border > 0f ? 1f : 0f, Math.Clamp(feather, 0f, 1f), 0),
            };
            cbuffer.Update(context, ref vars);

            shader.Apply(context);
            context.VertexShader.SetConstantBuffer(0, cbuffer.Buffer);
            context.PixelShader.SetConstantBuffer(0, cbuffer.Buffer);
            context.PixelShader.SetShaderResource(0, srv);
            context.PixelShader.SetSampler(0, CommonStates.LinearClamp);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vbuffer, PhotoVertex_V2.Stride, 0));
            context.OutputMerger.SetBlendState(CommonStates.BlendAlpha);
            context.OutputMerger.SetDepthStencilState(CommonStates.DepthDisabled);
            context.Rasterizer.State = CommonStates.RasterSolid;
            context.Draw(6, 0);

            context.PixelShader.SetShaderResource(0, null);
        }

        public void Dispose()
        {
            vbuffer?.Dispose();
            cbuffer?.Dispose();
            shader?.Dispose();
        }
    }
}

