using System;
using SharpDX;

namespace RageLightEditor.Rendering
{
    public partial class TriRenderer
    {
        public int VertexCount_U25 => verts.Count;

        public SharpDX.Direct3D11.RasterizerState RasterOverride_U26;

        public void Clear_U25() => verts.Clear();

        public void MapColours_U25(Func<Vector4, Vector4> f)
        {
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                v.Colour = f(v.Colour);
                verts[i] = v;
            }
        }
    }
}
