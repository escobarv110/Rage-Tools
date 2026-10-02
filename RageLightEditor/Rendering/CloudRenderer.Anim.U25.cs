using System;
using System.Collections.Generic;
using CodeWalker.GameFiles;
using SharpDX;
using SharpDX.Direct3D11;
using GameTexture = CodeWalker.GameFiles.Texture;

namespace RageLightEditor.Rendering
{
    public sealed class CloudMeshParams_U25
    {
        public bool Anim;
        public ShaderResourceView Detail1, Detail2;
        public Vector4 Rescale12 = new Vector4(1, 1, 1, 1);
        public Vector4 Rescale3Offset1 = new Vector4(1, 1, 0, 0);
        public Vector4 Offset23 = Vector4.Zero;
        public Vector4 AnimScale12 = new Vector4(1, 1, 1, 1);
        public Vector4 AnimScale3Flags = new Vector4(1, 1, 0, 0);
        public Vector4 AnimCombine = new Vector4(1, 1, 1, 0);
        public Vector4 AnimSculpt = Vector4.Zero;
        public Vector4 AnimBlend = new Vector4(1, 0, 0, 0);
    }

    public partial class CloudRenderer
    {
        private readonly Dictionary<uint, CloudMeshParams_U25[]> meshParams_U25 = new Dictionary<uint, CloudMeshParams_U25[]>();

        private static Vector4 ParamV_U25(ShaderParametersBlock plist, ShaderParamNames name, Vector4 def)
        {
            if (plist?.Parameters == null || plist.Hashes == null) return def;
            int n = Math.Min(plist.Parameters.Length, plist.Hashes.Length);
            for (int k = 0; k < n; k++)
            {
                if ((ShaderParamNames)(uint)plist.Hashes[k] != name) continue;
                var d = plist.Parameters[k].Data;
                if (d is Vector4 v) return v;
                if (d is Vector4[] a && a.Length > 0) return a[0];
                return def;
            }
            return def;
        }

        private ShaderResourceView ParamTex_U25(RenderMesh mesh, ShaderParamNames name)
        {
            var plist = mesh.Shader?.ParametersList;
            if (plist?.Parameters == null || plist.Hashes == null || textures == null) return null;
            int n = Math.Min(plist.Parameters.Length, plist.Hashes.Length);
            for (int k = 0; k < n; k++)
            {
                if ((ShaderParamNames)(uint)plist.Hashes[k] != name) continue;
                if (plist.Parameters[k].Data is TextureBase tb)
                {
                    var gt = tb as GameTexture ?? mesh.EmbeddedDict?.Lookup(tb.NameHash) ?? gameFiles?.FindTexture(tb.NameHash, 0);
                    if (gt?.Data?.FullData != null) return textures.GetSRV(gt, false);
                }
                return null;
            }
            return null;
        }

        private CloudMeshParams_U25 BuildMeshParams_U25(RenderMesh mesh, ShaderResourceView baseDensity)
        {
            var p = new CloudMeshParams_U25();
            var plist = mesh.Shader?.ParametersList;
            string sh = mesh.ShaderName ?? "";
            p.Anim = sh.IndexOf("anim", StringComparison.OrdinalIgnoreCase) >= 0;
            var r1 = ParamV_U25(plist, ShaderParamNames.gRescaleUV1, new Vector4(1, 1, 0, 0));
            var r2 = ParamV_U25(plist, ShaderParamNames.gRescaleUV2, new Vector4(1, 1, 0, 0));
            var r3 = ParamV_U25(plist, ShaderParamNames.gRescaleUV3, new Vector4(1, 1, 0, 0));
            var o1 = ParamV_U25(plist, ShaderParamNames.gUVOffset1, Vector4.Zero);
            var o2 = ParamV_U25(plist, ShaderParamNames.gUVOffset2, Vector4.Zero);
            var o3 = ParamV_U25(plist, ShaderParamNames.gUVOffset3, Vector4.Zero);
            var a1 = ParamV_U25(plist, ShaderParamNames.cloudLayerAnimScale1, new Vector4(1, 1, 0, 0));
            var a2 = ParamV_U25(plist, ShaderParamNames.cloudLayerAnimScale2, new Vector4(1, 1, 0, 0));
            var a3 = ParamV_U25(plist, ShaderParamNames.cloudLayerAnimScale3, new Vector4(1, 1, 0, 0));
            p.Rescale12 = new Vector4(r1.X, r1.Y, r2.X, r2.Y);
            p.Rescale3Offset1 = new Vector4(r3.X, r3.Y, o1.X, o1.Y);
            p.Offset23 = new Vector4(o2.X, o2.Y, o3.X, o3.Y);
            p.AnimScale12 = new Vector4(a1.X, a1.Y, a2.X, a2.Y);
            p.AnimCombine = ParamV_U25(plist, ShaderParamNames.gAnimCombine, new Vector4(1, 1, 1, 0));
            p.AnimSculpt = ParamV_U25(plist, ShaderParamNames.gAnimSculpt, Vector4.Zero);
            p.AnimBlend = ParamV_U25(plist, ShaderParamNames.gAnimBlendWeights, new Vector4(1, 0, 0, 0));
            if (p.Anim)
            {
                p.Detail1 = ParamTex_U25(mesh, ShaderParamNames.DetailDensitySampler);
                p.Detail2 = ParamTex_U25(mesh, ShaderParamNames.DetailDensity2Sampler) ?? p.Detail1;
            }
            p.AnimScale3Flags = new Vector4(a3.X, a3.Y, p.Anim ? 1 : 0, 0);
            return p;
        }

        private static void ApplyMeshParams_U25(ref CloudVars v, CloudMeshParams_U25 p)
        {
            p ??= new CloudMeshParams_U25();
            v.Rescale12 = p.Rescale12;
            v.Rescale3Offset1 = p.Rescale3Offset1;
            v.Offset23 = p.Offset23;
            v.AnimScale12 = p.AnimScale12;
            v.AnimScale3Flags = p.AnimScale3Flags;
            v.AnimCombine = p.AnimCombine;
            v.AnimSculpt = p.AnimSculpt;
            v.AnimBlend = p.AnimBlend;
        }
    }
}
