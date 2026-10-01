using System;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public static partial class ShaderPresets
    {
        public static TextureBase GameFurTexture_U21(string shaderName, uint hash)
        {
            if (string.IsNullOrEmpty(shaderName) || !shaderName.StartsWith("grass_fur", StringComparison.OrdinalIgnoreCase)) return null;
            if (shaderName.StartsWith("grass_fur_lod", StringComparison.OrdinalIgnoreCase)) return null;
            switch ((ShaderParamNames)hash)
            {
                case ShaderParamNames.ComboHeightSamplerFur01: return MaterialEditing.MakeTextureRef("fur_grass_rgba4_0");
                case ShaderParamNames.ComboHeightSamplerFur23: return MaterialEditing.MakeTextureRef("fur_grass_rgba4_1");
                case ShaderParamNames.ComboHeightSamplerFur45: return MaterialEditing.MakeTextureRef("fur_grass_rgba4_2");
                case ShaderParamNames.ComboHeightSamplerFur67: return MaterialEditing.MakeTextureRef("fur_grass_rgba4_3");
                default: return null;
            }
        }
    }
}
