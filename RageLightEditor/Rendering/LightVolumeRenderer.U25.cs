using System;
using System.Collections.Generic;
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
    public struct LightVolumeVertex_U25
    {
        public Vector3 Pos;
        public Vector4 LightPR;
        public Vector4 DirCosO;
        public Vector4 InCosI;
        public Vector4 OutExp;
        public Vector4 Misc;
        public const int Stride = 12 + 16 * 5;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LightVolumeVars_U25
    {
        public Matrix ViewProj;
        public Vector4 CameraPos;
        public Vector4 CameraFwd;
        public Vector4 ProjParams;
        public Vector4 Params;
    }

    public sealed class LightVolumeRenderer_U25 : IDisposable
    {
        private readonly Device device;
        private readonly ShaderSet shader;
        private readonly ConstantBuffer<LightVolumeVars_U25> cbuffer;
        private readonly RasterizerState rasterBackFaces;
        private readonly List<LightVolumeVertex_U25> verts = new List<LightVolumeVertex_U25>(36 * 64);
        private Buffer vbuffer;
        private int capacity;

        public float Intensity = 1.0f;
        public string LastError;
        public int Count => verts.Count / 36;

        public LightVolumeRenderer_U25(Device device)
        {
            this.device = device;
            try
            {
                shader = new ShaderSet(device, "lightvolume.hlsl", new[]
                {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32B32A32_Float, 12, 0),
                    new InputElement("TEXCOORD", 1, Format.R32G32B32A32_Float, 28, 0),
                    new InputElement("TEXCOORD", 2, Format.R32G32B32A32_Float, 44, 0),
                    new InputElement("TEXCOORD", 3, Format.R32G32B32A32_Float, 60, 0),
                    new InputElement("TEXCOORD", 4, Format.R32G32B32A32_Float, 76, 0),
                });
            }
            catch (Exception ex) { LastError = ex.Message; shader = null; }
            cbuffer = new ConstantBuffer<LightVolumeVars_U25>(device);
            rasterBackFaces = new RasterizerState(device, new RasterizerStateDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.Front,
                IsFrontCounterClockwise = true,
                IsDepthClipEnabled = false,
                IsMultisampleEnabled = true,
            });
        }

        public bool Ready => shader != null;

        public void Add(Vector3 pos, Vector3 dir, float radius, float cosOuter, float cosInner, float falloffExponent,
                        Vector3 innerColour, Vector3 outerColour, float outerExponent, int type)
        {
            if (radius <= 0.01f) return;
            var v = new LightVolumeVertex_U25
            {
                LightPR = new Vector4(pos, radius),
                DirCosO = new Vector4(dir.LengthSquared() > 1e-8f ? Vector3.Normalize(dir) : -Vector3.UnitZ, cosOuter),
                InCosI = new Vector4(innerColour, cosInner),
                OutExp = new Vector4(outerColour, outerExponent),
                Misc = new Vector4(falloffExponent, type, 0, 0),
            };
            Vector3 C(int x, int y, int z) => pos + new Vector3(x * radius, y * radius, z * radius);
            var c = new[] { C(-1, -1, -1), C(1, -1, -1), C(1, 1, -1), C(-1, 1, -1), C(-1, -1, 1), C(1, -1, 1), C(1, 1, 1), C(-1, 1, 1) };
            void Quad(int a, int b, int d, int e)
            {
                var p0 = c[a]; var p1 = c[b]; var p2 = c[d]; var p3 = c[e];
                var nrm = Vector3.Cross(p1 - p0, p2 - p0);
                if (Vector3.Dot(nrm, p0 - pos) < 0) { var t = p1; p1 = p3; p3 = t; }
                v.Pos = p0; verts.Add(v); v.Pos = p1; verts.Add(v); v.Pos = p2; verts.Add(v);
                v.Pos = p0; verts.Add(v); v.Pos = p2; verts.Add(v); v.Pos = p3; verts.Add(v);
            }
            Quad(0, 1, 2, 3); Quad(4, 5, 6, 7); Quad(0, 1, 5, 4); Quad(1, 2, 6, 5); Quad(2, 3, 7, 6); Quad(3, 0, 4, 7);
        }

        public void Clear() => verts.Clear();

        public void Flush(DeviceContext context, Camera camera, ShaderResourceView depthSrv, int depthMode, int viewportWidth, int viewportHeight)
        {
            if (verts.Count == 0 || shader == null) { verts.Clear(); return; }
            if (vbuffer == null || capacity < verts.Count)
            {
                vbuffer?.Dispose();
                capacity = Math.Max(verts.Count + 36 * 16, 36 * 64);
                vbuffer = new Buffer(device, capacity * LightVolumeVertex_U25.Stride, ResourceUsage.Dynamic,
                    BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
            }
            var box = context.MapSubresource(vbuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            unsafe
            {
                var dst = (LightVolumeVertex_U25*)box.DataPointer;
                for (int i = 0; i < verts.Count; i++) dst[i] = verts[i];
            }
            context.UnmapSubresource(vbuffer, 0);

            var pm = camera.ProjMatrix;
            var vars = new LightVolumeVars_U25
            {
                ViewProj = Matrix.Transpose(camera.ViewProjMatrix),
                CameraPos = new Vector4(camera.Position, 0),
                CameraFwd = new Vector4(Vector3.Normalize(camera.GetForward()), depthSrv != null ? depthMode : 0),
                ProjParams = new Vector4(pm.M33, pm.M43, 1.0f / Math.Max(viewportWidth, 1), 1.0f / Math.Max(viewportHeight, 1)),
                Params = new Vector4(Intensity, 0, 0, 0),
            };
            cbuffer.Update(context, ref vars);

            shader.Apply(context);
            context.VertexShader.SetConstantBuffer(0, cbuffer.Buffer);
            context.PixelShader.SetConstantBuffer(0, cbuffer.Buffer);
            if (depthMode == 2) context.PixelShader.SetShaderResource(26, depthSrv);
            else context.PixelShader.SetShaderResource(25, depthSrv);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vbuffer, LightVolumeVertex_U25.Stride, 0));
            context.OutputMerger.SetBlendState(CommonStates.BlendAdditive);
            context.OutputMerger.SetDepthStencilState(CommonStates.DepthDisabled);
            context.Rasterizer.State = rasterBackFaces;
            context.Draw(verts.Count, 0);
            context.PixelShader.SetShaderResource(25, null);
            context.PixelShader.SetShaderResource(26, null);
            context.Rasterizer.State = CommonStates.RasterSolid;
            verts.Clear();
        }

        public void Dispose()
        {
            vbuffer?.Dispose();
            cbuffer?.Dispose();
            shader?.Dispose();
            rasterBackFaces?.Dispose();
        }
    }
}
