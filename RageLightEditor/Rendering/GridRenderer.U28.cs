using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace RageLightEditor.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GridVars_U28
    {
        public Matrix ViewProj;
        public Vector4 CamPos;
        public Vector4 Spacing;
        public Vector4 Fade;
        public Vector4 MinorCol;
        public Vector4 MajorCol;
        public Vector4 AxisXCol;
        public Vector4 AxisYCol;
    }

    public sealed class GridRenderer_U28 : IDisposable
    {
        private readonly ShaderSet shader;
        private readonly ConstantBuffer<GridVars_U28> cbuffer;
        public string LastError;
        public GridVars_U28 Vars;

        public GridRenderer_U28(Device device)
        {
            try { shader = new ShaderSet(device, "grid_u28.hlsl", null); }
            catch (Exception ex) { LastError = ex.Message; shader = null; }
            cbuffer = new ConstantBuffer<GridVars_U28>(device);
        }

        public bool Ready => shader != null;

        public void Draw(DeviceContext context, DepthStencilState depth)
        {
            if (shader == null) return;
            var v = Vars;
            v.ViewProj = Matrix.Transpose(Vars.ViewProj);
            cbuffer.Update(context, ref v);
            shader.Apply(context);
            context.VertexShader.SetConstantBuffer(0, cbuffer.Buffer);
            context.PixelShader.SetConstantBuffer(0, cbuffer.Buffer);
            context.OutputMerger.SetBlendState(CommonStates.BlendAlpha);
            context.OutputMerger.SetDepthStencilState(depth);
            context.Rasterizer.State = CommonStates.RasterSolid;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.Draw(6, 0);
        }

        public void Dispose()
        {
            shader?.Dispose();
            cbuffer?.Dispose();
        }
    }
}
