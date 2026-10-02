using System;
using System.Collections.Generic;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace RageLightEditor.Rendering
{
    public partial class TriRenderer : IDisposable
    {
        private readonly Device device;
        private readonly ShaderSet shader;
        private readonly ConstantBuffer<LineVars> cbuffer;
        private Buffer vbuffer;
        private int capacity;
        private readonly List<LineVertex> verts = new List<LineVertex>(4096);

        public TriRenderer(Device device)
        {
            this.device = device;
            shader = new ShaderSet(device, "lines.hlsl", new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0),
            });
            cbuffer = new ConstantBuffer<LineVars>(device);
        }

        public void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector4 ca, Vector4 cb, Vector4 cc)
        {
            verts.Add(new LineVertex(a, ca));
            verts.Add(new LineVertex(b, cb));
            verts.Add(new LineVertex(c, cc));
        }

        public void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector4 col) => AddTri(a, b, c, col, col, col);

        public void AddTriangles(LineVertex[] tris, Vector3 offset)
        {
            if (tris == null || tris.Length < 3) return;
            int n = tris.Length - (tris.Length % 3);
            if (verts.Capacity < verts.Count + n) verts.Capacity = verts.Count + n;
            for (int i = 0; i < n; i++)
                verts.Add(new LineVertex(tris[i].Position + offset, tris[i].Colour));
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 col)
        {
            AddTri(a, b, c, col);
            AddTri(a, c, d, col);
        }

        public void AddCone(Vector3 apex, Vector3 baseCentre, Vector3 axisA, Vector3 axisB, float radius,
            Vector4 apexCol, Vector4 rimCol, int segments = 16)
        {
            Vector3 prev = baseCentre + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)(i * Math.PI * 2.0 / segments);
                Vector3 p = baseCentre + (axisA * (float)Math.Cos(t) + axisB * (float)Math.Sin(t)) * radius;
                AddTri(apex, prev, p, apexCol, rimCol, rimCol);
                prev = p;
            }
        }

        private static float FeatherProfile(float t, float feather)
        {
            t = Math.Clamp(t, 0f, 1f);
            float g = 1.0f - t * t;
            g *= g;
            return 1.0f - feather * (1.0f - g);
        }

        private static bool FeatherWeights(Span<float> w, float feather)
        {
            int n = w.Length;
            float total = 0;
            for (int k = 0; k < n; k++)
            {
                float t0 = k / (float)n, t1 = (k + 1) / (float)n;
                w[k] = Math.Max(FeatherProfile(t0, feather) - FeatherProfile(t1, feather), 0f);
                total += w[k];
            }
            if (total <= 1e-6f) return false;
            for (int k = 0; k < n; k++) w[k] /= total;
            return true;
        }

        public void AddConeFeathered(Vector3 apex, Vector3 baseCentre, Vector3 axisA, Vector3 axisB,
            float radius, Vector4 apexCol, Vector4 rimCol, int segments, float feather, int rings = 10)
        {
            feather = Math.Clamp(feather, 0f, 1f);
            Span<float> w = stackalloc float[Math.Max(rings, 1)];
            if (feather <= 0.001f || rings <= 1 || !FeatherWeights(w, feather))
            {
                AddCone(apex, baseCentre, axisA, axisB, radius, apexCol, rimCol, segments);
                return;
            }

            for (int k = 0; k < rings; k++)
            {
                if (w[k] <= 0.0005f) continue;
                float f = (k + 1) / (float)rings;
                var a = apexCol; a.W *= w[k];
                var b = rimCol; b.W *= w[k];
                AddCone(apex, baseCentre, axisA, axisB, radius * f, a, b, segments);
            }
        }

        public void AddSphereFeathered(Vector3 centre, float radius, Vector4 col,
            int rings, int segments, float feather, int shells = 8)
        {
            feather = Math.Clamp(feather, 0f, 1f);
            Span<float> w = stackalloc float[Math.Max(shells, 1)];
            if (feather <= 0.001f || shells <= 1 || !FeatherWeights(w, feather))
            {
                AddSphere(centre, radius, col, rings, segments);
                return;
            }
            for (int k = 0; k < shells; k++)
            {
                if (w[k] <= 0.0005f) continue;
                float f = (k + 1) / (float)shells;
                var c = col; c.W *= w[k];
                AddSphere(centre, radius * f, c, rings, segments);
            }
        }

        public void AddCylinderFeathered(Vector3 c0, Vector3 c1, Vector3 axisA, Vector3 axisB,
            float radius, Vector4 col, int segments, float feather, int shells = 8)
        {
            feather = Math.Clamp(feather, 0f, 1f);
            Span<float> w = stackalloc float[Math.Max(shells, 1)];
            if (feather <= 0.001f || shells <= 1 || !FeatherWeights(w, feather))
            {
                AddCylinder(c0, c1, axisA, axisB, radius, col, col, segments);
                return;
            }
            for (int k = 0; k < shells; k++)
            {
                if (w[k] <= 0.0005f) continue;
                float f = (k + 1) / (float)shells;
                var c = col; c.W *= w[k];
                AddCylinder(c0, c1, axisA, axisB, radius * f, c, c, segments);
            }
        }

        public void AddDisc(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, Vector4 centreCol, Vector4 rimCol, int segments = 16)
        {
            Vector3 prev = centre + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)(i * Math.PI * 2.0 / segments);
                Vector3 p = centre + (axisA * (float)Math.Cos(t) + axisB * (float)Math.Sin(t)) * radius;
                AddTri(centre, prev, p, centreCol, rimCol, rimCol);
                prev = p;
            }
        }

        public void AddCylinder(Vector3 c0, Vector3 c1, Vector3 axisA, Vector3 axisB, float radius,
            Vector4 col0, Vector4 col1, int segments = 16)
        {
            for (int i = 0; i < segments; i++)
            {
                float t0 = (float)(i * Math.PI * 2.0 / segments);
                float t1 = (float)((i + 1) * Math.PI * 2.0 / segments);
                var o0 = (axisA * (float)Math.Cos(t0) + axisB * (float)Math.Sin(t0)) * radius;
                var o1 = (axisA * (float)Math.Cos(t1) + axisB * (float)Math.Sin(t1)) * radius;
                AddTri(c0 + o0, c1 + o0, c1 + o1, col0, col1, col1);
                AddTri(c0 + o0, c1 + o1, c0 + o1, col0, col1, col0);
            }
        }

        public void AddSphere(Vector3 centre, float radius, Vector4 col, int rings = 8, int segments = 12)
        {
            for (int r = 0; r < rings; r++)
            {
                float p0 = (float)(Math.PI * r / rings - Math.PI / 2);
                float p1 = (float)(Math.PI * (r + 1) / rings - Math.PI / 2);
                for (int s = 0; s < segments; s++)
                {
                    float t0 = (float)(s * Math.PI * 2.0 / segments);
                    float t1 = (float)((s + 1) * Math.PI * 2.0 / segments);
                    Vector3 P(float pit, float th) => centre + new Vector3(
                        (float)(Math.Cos(pit) * Math.Cos(th)),
                        (float)(Math.Cos(pit) * Math.Sin(th)),
                        (float)Math.Sin(pit)) * radius;
                    var a = P(p0, t0); var b = P(p0, t1); var c = P(p1, t1); var d = P(p1, t0);
                    AddTri(a, b, c, col);
                    AddTri(a, c, d, col);
                }
            }
        }

        public void AddThickLine(Vector3 a, Vector3 b, Vector3 camPos, float halfWidth, Vector4 col)
        {
            var d = b - a;
            if (d.LengthSquared() < 1e-12f) return;
            var side = SideVector(d, camPos - (a + b) * 0.5f) * halfWidth;
            AddQuad(a - side, b - side, b + side, a + side, col);
        }

        public void AddThickCircle(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius,
            Vector3 camPos, float halfWidth, Vector4 col, int segments = 64)
        {
            AddThickArc(centre, axisA, axisB, radius, 0.0f, (float)(Math.PI * 2.0), camPos, halfWidth, col, segments);
        }

        public void AddThickArc(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, float a0, float a1,
            Vector3 camPos, float halfWidth, Vector4 col, int segments = 64)
        {
            float span = a1 - a0;
            int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(span) / (Math.PI * 2.0) * segments));
            Vector3 prevP = Vector3.Zero, prevS = Vector3.Zero;
            for (int i = 0; i <= n; i++)
            {
                float ang = a0 + span * i / n;
                float c = (float)Math.Cos(ang), s = (float)Math.Sin(ang);
                var p = centre + (axisA * c + axisB * s) * radius;
                var tangent = (-axisA * s + axisB * c) * Math.Sign(span);
                var sideV = SideVector(tangent, camPos - p) * halfWidth;
                if (i > 0) AddQuad(prevP - prevS, p - sideV, p + sideV, prevP + prevS, col);
                prevP = p; prevS = sideV;
            }
        }

        public void AddThickPolyline(IList<Vector3> pts, Vector3 camPos, float halfWidth, Vector4 col, bool closed = false)
        {
            if (pts == null || pts.Count < 2) return;
            int n = pts.Count;
            int segs = closed ? n : n - 1;
            Vector3 prevP = Vector3.Zero, prevS = Vector3.Zero;
            for (int i = 0; i <= segs; i++)
            {
                var p = pts[i % n];
                var before = pts[((i - 1) % n + n) % n];
                var after = pts[(i + 1) % n];
                Vector3 tangent;
                if (closed) tangent = after - before;
                else if (i == 0) tangent = after - p;
                else if (i >= n - 1) tangent = p - before;
                else tangent = after - before;
                var sideV = SideVector(tangent, camPos - p) * halfWidth;
                if (i > 0) AddQuad(prevP - prevS, p - sideV, p + sideV, prevP + prevS, col);
                prevP = p; prevS = sideV;
            }
        }

        private static Vector3 SideVector(Vector3 dir, Vector3 toCam)
        {
            var side = Vector3.Cross(dir, toCam);
            if (side.LengthSquared() < 1e-12f) side = Vector3.Cross(dir, Vector3.UnitZ);
            if (side.LengthSquared() < 1e-12f) side = Vector3.Cross(dir, Vector3.UnitX);
            if (side.LengthSquared() < 1e-12f) return Vector3.UnitY;
            side.Normalize();
            return side;
        }

        public void AddThickLineAA(Vector3 a, Vector3 b, Vector3 camPos, float halfWidth, float feather, Vector4 col)
        {
            var d = b - a;
            if (d.LengthSquared() < 1e-12f) return;
            var side = SideVector(d, camPos - (a + b) * 0.5f);
            AddRibbonAA(a, b, side, side, halfWidth, feather, col, col);
        }

        public void AddThickLineAA(Vector3 a, Vector3 b, Vector3 camPos, float halfWidth, float feather, Vector4 colA, Vector4 colB)
        {
            var d = b - a;
            if (d.LengthSquared() < 1e-12f) return;
            var side = SideVector(d, camPos - (a + b) * 0.5f);
            AddRibbonAA(a, b, side, side, halfWidth, feather, colA, colB);
        }

        private void AddRibbonAA(Vector3 a, Vector3 b, Vector3 sideA, Vector3 sideB, float halfWidth, float feather,
                                 Vector4 colA, Vector4 colB)
        {
            float f = Math.Max(feather, 0f);
            float hi = Math.Max(halfWidth - f * 0.5f, 0f);
            float ho = halfWidth + f * 0.5f;
            var zA = new Vector4(colA.X, colA.Y, colA.Z, 0f);
            var zB = new Vector4(colB.X, colB.Y, colB.Z, 0f);
            if (hi <= 1e-7f)
            {
                float k = Math.Clamp(halfWidth / Math.Max(f * 0.5f, 1e-6f), 0f, 1f);
                var cA = colA; cA.W *= k; var cB = colB; cB.W *= k;
                AddTri(a - sideA * ho, b - sideB * ho, b, zA, zB, cB);
                AddTri(a - sideA * ho, b, a, zA, cB, cA);
                AddTri(a, b, b + sideB * ho, cA, cB, zB);
                AddTri(a, b + sideB * ho, a + sideA * ho, cA, zB, zA);
                return;
            }
            AddTri(a - sideA * hi, b - sideB * hi, b + sideB * hi, colA, colB, colB);
            AddTri(a - sideA * hi, b + sideB * hi, a + sideA * hi, colA, colB, colA);
            AddTri(a - sideA * ho, b - sideB * ho, b - sideB * hi, zA, zB, colB);
            AddTri(a - sideA * ho, b - sideB * hi, a - sideA * hi, zA, colB, colA);
            AddTri(a + sideA * hi, b + sideB * hi, b + sideB * ho, colA, colB, zB);
            AddTri(a + sideA * hi, b + sideB * ho, a + sideA * ho, colA, zB, zA);
        }

        public void AddThickArcAA(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, float a0, float a1,
            Vector3 camPos, float halfWidth, float feather, Func<float, Vector4> colourAt, int segments = 64)
        {
            float span = a1 - a0;
            if (Math.Abs(span) < 1e-6f) return;
            int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(span) / (Math.PI * 2.0) * segments));
            Vector3 prevP = Vector3.Zero, prevS = Vector3.Zero;
            Vector4 prevC = Vector4.Zero;
            for (int i = 0; i <= n; i++)
            {
                float ang = a0 + span * i / n;
                float c = (float)Math.Cos(ang), s = (float)Math.Sin(ang);
                var p = centre + (axisA * c + axisB * s) * radius;
                var tangent = (-axisA * s + axisB * c) * Math.Sign(span);
                var sideV = SideVector(tangent, camPos - p);
                var col = colourAt(ang);
                if (i > 0) AddRibbonAA(prevP, p, prevS, sideV, halfWidth, feather, prevC, col);
                prevP = p; prevS = sideV; prevC = col;
            }
        }

        public void AddThickArcAA(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, float a0, float a1,
            Vector3 camPos, float halfWidth, float feather, Vector4 col, int segments = 64)
        {
            AddThickArcAA(centre, axisA, axisB, radius, a0, a1, camPos, halfWidth, feather, _ => col, segments);
        }

        public void AddThickCircleAA(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, Vector3 camPos,
            float halfWidth, float feather, Vector4 col, int segments = 64)
        {
            AddThickArcAA(centre, axisA, axisB, radius, 0f, (float)(Math.PI * 2.0), camPos, halfWidth, feather, _ => col, segments);
        }

        public void AddDiscAA(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, float feather, Vector4 col, int segments = 32)
        {
            float ri = Math.Max(radius - feather * 0.5f, 0f), ro = radius + feather * 0.5f;
            var z = new Vector4(col.X, col.Y, col.Z, 0f);
            Vector3 P(float ang, float r) => centre + (axisA * (float)Math.Cos(ang) + axisB * (float)Math.Sin(ang)) * r;
            for (int i = 0; i < segments; i++)
            {
                float t0 = (float)(i * Math.PI * 2.0 / segments), t1 = (float)((i + 1) * Math.PI * 2.0 / segments);
                if (ri > 1e-7f) AddTri(centre, P(t0, ri), P(t1, ri), col, col, col);
                AddTri(P(t0, ri), P(t0, ro), P(t1, ro), col, z, z);
                AddTri(P(t0, ri), P(t1, ro), P(t1, ri), col, z, col);
            }
        }

        public void AddQuadAA(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float feather, Vector4 col)
        {
            AddQuad(a, b, c, d, col);
            if (feather <= 0f) return;
            var z = new Vector4(col.X, col.Y, col.Z, 0f);
            var n = Vector3.Cross(b - a, d - a);
            if (n.LengthSquared() < 1e-14f) return;
            n.Normalize();
            Vector3[] q = { a, b, c, d };
            for (int i = 0; i < 4; i++)
            {
                var p0 = q[i]; var p1 = q[(i + 1) % 4];
                var e = p1 - p0;
                if (e.LengthSquared() < 1e-14f) continue;
                var outw = Vector3.Cross(e, n); outw.Normalize();
                var mid = (p0 + p1) * 0.5f;
                var opp = q[(i + 2) % 4];
                if (Vector3.Dot(outw, opp - mid) > 0) outw = -outw;
                var o = outw * feather;
                AddTri(p0, p1, p1 + o, col, col, z);
                AddTri(p0, p1 + o, p0 + o, col, z, z);
            }
        }

        public void AddConeShaded(Vector3 apex, Vector3 baseCentre, Vector3 axisA, Vector3 axisB, float radius,
            Vector4 col, Vector3 lightDir, float ambient = 0.55f, int segments = 24)
        {
            var axis = apex - baseCentre;
            float h = axis.Length();
            if (h < 1e-9f) return;
            var n = axis / h;
            Vector3 prev = baseCentre + axisA * radius;
            Vector4 prevC = col;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)(i * Math.PI * 2.0 / segments);
                var radial = axisA * (float)Math.Cos(t) + axisB * (float)Math.Sin(t);
                Vector3 p = baseCentre + radial * radius;
                var fn = Vector3.Normalize(radial * h + n * radius);
                float k = ambient + (1f - ambient) * Math.Max(0f, Vector3.Dot(fn, lightDir));
                var c = new Vector4(col.X * k, col.Y * k, col.Z * k, col.W);
                if (i > 0) AddTri(apex, prev, p, c, prevC, c);
                prev = p; prevC = c;
            }
            float kb = ambient + (1f - ambient) * Math.Max(0f, Vector3.Dot(-n, lightDir));
            var bc = new Vector4(col.X * kb, col.Y * kb, col.Z * kb, col.W);
            AddDisc(baseCentre, axisA, axisB, radius, bc, bc, segments);
        }

        public void AddArcFan(Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, float a0, float a1, Vector4 col, int segments = 32)
        {
            float span = a1 - a0;
            int n = Math.Max(2, (int)(Math.Abs(span) / (Math.PI * 2) * segments));
            Vector3 P(float ang) => centre + (axisA * (float)Math.Cos(ang) + axisB * (float)Math.Sin(ang)) * radius;
            var prev = P(a0);
            for (int i = 1; i <= n; i++)
            {
                var p = P(a0 + span * i / n);
                AddTri(centre, prev, p, col);
                prev = p;
            }
        }

        public void Flush(DeviceContext context, Matrix viewProj, BlendState blend = null, DepthStencilState depth = null)
        {
            if (verts.Count == 0) return;

            if (vbuffer == null || capacity < verts.Count)
            {
                vbuffer?.Dispose();
                capacity = Math.Max(verts.Count + 4096, 8192);
                vbuffer = new Buffer(device, capacity * LineVertex.Stride, ResourceUsage.Dynamic,
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
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vbuffer, LineVertex.Stride, 0));
            context.OutputMerger.SetBlendState(blend ?? CommonStates.BlendAlpha);
            context.OutputMerger.SetDepthStencilState(depth ?? CommonStates.DepthReadOnly);
            context.Rasterizer.State = RasterOverride_U26 ?? CommonStates.RasterSolid;
            context.Draw(verts.Count, 0);

            verts.Clear();
        }

        public void DrawBuffer(DeviceContext context, Buffer vb, int vertexCount, Matrix viewProj,
                               BlendState blend, DepthStencilState depth, RasterizerState raster,
                               Vector3 cameraPos = default, float pullToCamera = 0.0f)
        {
            if (vb == null || vertexCount < 3) return;
            var vars = new LineVars { ViewProj = Matrix.Transpose(viewProj), CamPull = new Vector4(cameraPos, pullToCamera) };
            cbuffer.Update(context, ref vars);
            shader.Apply(context);
            context.VertexShader.SetConstantBuffer(0, cbuffer.Buffer);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vb, LineVertex.Stride, 0));
            context.OutputMerger.SetBlendState(blend ?? CommonStates.BlendOpaque);
            context.OutputMerger.SetDepthStencilState(depth ?? CommonStates.DepthDefault);
            context.Rasterizer.State = raster ?? CommonStates.RasterSolid;
            context.Draw(vertexCount, 0);
        }

        public Buffer MakeBuffer(LineVertex[] tris)
        {
            if (tris == null || tris.Length < 3) return null;
            return Buffer.Create(device, BindFlags.VertexBuffer, tris);
        }

        public void Dispose()
        {
            vbuffer?.Dispose();
            cbuffer?.Dispose();
            shader?.Dispose();
        }
    }
}

