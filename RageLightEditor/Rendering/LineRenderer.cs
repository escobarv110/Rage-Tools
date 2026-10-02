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
    public struct LineVertex
    {
        public Vector3 Position;
        public Vector4 Colour;
        public const int Stride = 28;
        public LineVertex(Vector3 p, Vector4 c) { Position = p; Colour = c; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LineVars
    {
        public Matrix ViewProj;
        public Vector4 CamPull;
        public Vector4 PullClamp;
    }

    public class LineRenderer : IDisposable
    {
        private readonly Device device;
        private readonly ShaderSet shader;
        private readonly ConstantBuffer<LineVars> cbuffer;
        private Buffer vbuffer;
        private int vbufferCapacity;
        private readonly List<LineVertex> verts = new List<LineVertex>(4096);

        public LineRenderer(Device device)
        {
            this.device = device;
            shader = new ShaderSet(device, "lines.hlsl", new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
            });
            cbuffer = new ConstantBuffer<LineVars>(device);
        }

        public int LineCount => verts.Count / 2;

        public void TakeSince(int startLine, List<(Vector3 a, Vector3 b, Vector4 c)> into)
        {
            int s = Math.Max(startLine, 0) * 2;
            for (int i = s; i + 1 < verts.Count; i += 2) into.Add((verts[i].Position, verts[i + 1].Position, verts[i].Colour));
            if (s < verts.Count) verts.RemoveRange(s, verts.Count - s);
        }

        public void AddLine(Vector3 a, Vector3 b, Vector4 colour)
        {
            verts.Add(new LineVertex(a, colour));
            verts.Add(new LineVertex(b, colour));
        }

        public void AddCircle(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, Vector4 colour, int segments = 48)
        {
            Vector3 prev = centre + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)(i * Math.PI * 2.0 / segments);
                Vector3 p = centre + (axisA * (float)Math.Cos(t) + axisB * (float)Math.Sin(t)) * radius;
                AddLine(prev, p, colour);
                prev = p;
            }
        }

        public void AddSphere(Vector3 centre, float radius, Vector4 colour, int segments = 48)
        {
            AddCircle(centre, Vector3.UnitX, Vector3.UnitY, radius, colour, segments);
            AddCircle(centre, Vector3.UnitX, Vector3.UnitZ, radius, colour, segments);
            AddCircle(centre, Vector3.UnitY, Vector3.UnitZ, radius, colour, segments);
        }

        public void AddCone(Vector3 pos, Vector3 dir, Vector3 tangent, float halfAngle, float range, Vector4 colour)
        {
            dir = Vector3.Normalize(dir);
            var tx = Vector3.Normalize(tangent - dir * Vector3.Dot(tangent, dir));
            if (float.IsNaN(tx.X) || tx.LengthSquared() < 0.5f)
            {
                tx = Vector3.Normalize(Vector3.Cross(dir, Math.Abs(dir.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX));
            }
            var ty = Vector3.Cross(dir, tx);

            float sinA = (float)Math.Sin(halfAngle);
            float cosA = (float)Math.Cos(halfAngle);
            float cradius = range * sinA;
            Vector3 ccentre = pos + dir * (range * cosA);

            AddCircle(ccentre, tx, ty, cradius, colour);
            for (int i = 0; i < 4; i++)
            {
                float t = (float)(i * Math.PI / 2.0);
                Vector3 rim = ccentre + (tx * (float)Math.Cos(t) + ty * (float)Math.Sin(t)) * cradius;
                AddLine(pos, rim, colour);
            }
            int segs = 24;
            Vector3 prev = pos + dir * range;
            for (int i = 1; i <= segs; i++)
            {
                float a = halfAngle * i / segs;
                Vector3 p = pos + (dir * (float)Math.Cos(a) + tx * (float)Math.Sin(a)) * range;
                AddLine(prev, p, colour);
                prev = p;
            }
            prev = pos + dir * range;
            for (int i = 1; i <= segs; i++)
            {
                float a = halfAngle * i / segs;
                Vector3 p = pos + (dir * (float)Math.Cos(a) - tx * (float)Math.Sin(a)) * range;
                AddLine(prev, p, colour);
                prev = p;
            }
        }

        public void AddCapsule(Vector3 a, Vector3 b, float radius, Vector4 colour)
        {
            var axis = b - a;
            float len = axis.Length();
            var dir = len > 1e-6f ? axis / len : Vector3.UnitZ;
            var tx = Vector3.Normalize(Vector3.Cross(dir, Math.Abs(dir.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX));
            var ty = Vector3.Cross(dir, tx);

            AddCircle(a, tx, ty, radius, colour);
            AddCircle(b, tx, ty, radius, colour);
            for (int i = 0; i < 4; i++)
            {
                float t = (float)(i * Math.PI / 2.0);
                Vector3 off = (tx * (float)Math.Cos(t) + ty * (float)Math.Sin(t)) * radius;
                AddLine(a + off, b + off, colour);
            }
            int segs = 16;
            for (int h = 0; h < 2; h++)
            {
                Vector3 c = h == 0 ? a : b;
                Vector3 d = h == 0 ? -dir : dir;
                foreach (var ax in new[] { tx, ty })
                {
                    Vector3 prev = c + ax * radius;
                    for (int i = 1; i <= segs; i++)
                    {
                        float t = (float)(i * Math.PI / 2.0 / segs);
                        Vector3 p = c + ax * ((float)Math.Cos(t) * radius) + d * ((float)Math.Sin(t) * radius);
                        AddLine(prev, p, colour);
                        prev = p;
                    }
                }
            }
        }

        public void AddBox(BoundingBox b, Vector4 colour)
        {
            var c = b.GetCorners();
            for (int i = 0; i < 4; i++)
            {
                AddLine(c[i], c[(i + 1) % 4], colour);
                AddLine(c[i + 4], c[((i + 1) % 4) + 4], colour);
                AddLine(c[i], c[i + 4], colour);
            }
        }

        public void AddAxes(Vector3 pos, float size)
        {
            AddLine(pos, pos + Vector3.UnitX * size, new Vector4(1, 0.2f, 0.2f, 1));
            AddLine(pos, pos + Vector3.UnitY * size, new Vector4(0.2f, 1, 0.2f, 1));
            AddLine(pos, pos + Vector3.UnitZ * size, new Vector4(0.3f, 0.5f, 1, 1));
        }

        public void Flush(DeviceContext context, Matrix viewProj, DepthStencilState depthState = null)
        {
            if (verts.Count == 0) return;

            if (vbuffer == null || vbufferCapacity < verts.Count)
            {
                vbuffer?.Dispose();
                vbufferCapacity = Math.Max(verts.Count + 2048, 8192);
                vbuffer = new Buffer(device, vbufferCapacity * LineVertex.Stride, ResourceUsage.Dynamic,
                    BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
            }

            var box = context.MapSubresource(vbuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            unsafe
            {
                var dst = (LineVertex*)box.DataPointer;
                for (int i = 0; i < verts.Count; i++) dst[i] = verts[i];
            }
            context.UnmapSubresource(vbuffer, 0);

            var vars = new LineVars { ViewProj = Matrix.Transpose(viewProj) };
            cbuffer.Update(context, ref vars);

            shader.Apply(context);
            context.VertexShader.SetConstantBuffer(0, cbuffer.Buffer);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.LineList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vbuffer, LineVertex.Stride, 0));
            context.OutputMerger.SetBlendState(CommonStates.BlendAlpha);
            context.OutputMerger.SetDepthStencilState(depthState ?? CommonStates.DepthReadOnly);
            context.Rasterizer.State = CommonStates.RasterSolid;
            context.Draw(verts.Count, 0);

            verts.Clear();
        }

        public void Dispose()
        {
            vbuffer?.Dispose();
            cbuffer?.Dispose();
            shader?.Dispose();
        }
    }
}

