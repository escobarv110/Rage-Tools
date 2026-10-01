using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D11;
using Format = SharpDX.DXGI.Format;
using SampleDescription = SharpDX.DXGI.SampleDescription;
using Device = SharpDX.Direct3D11.Device;

namespace RageLightEditor.Rendering
{
    public partial class ModelRenderer
    {
        private static ShaderResourceView[] fallbackCombo_U21;
        private static ShaderResourceView fallbackNoise_U21;
        private static Device fallbackDevice_U21;

        public const int FallbackFurSize_U21 = 256;

        private static uint Hash_U21(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
            return x;
        }

        private static float Rand_U21(int x, int y, uint seed) =>
            (Hash_U21((uint)x * 73856093U ^ (uint)y * 19349663U ^ seed) & 0xFFFFFF) / 16777216.0f;

        public static float FallbackStrandTop_U21(int x, int y)
        {
            int n = FallbackFurSize_U21;
            x = ((x % n) + n) % n; y = ((y % n) + n) % n;
            int cx = x >> 1, cy = y >> 1;
            if (Rand_U21(cx, cy, 0x51ED27u) > 0.42f) return 0.0f;
            return 0.25f + 0.75f * Rand_U21(cx, cy, 0xA2C3u);
        }

        public static byte FallbackComboValue_U21(int x, int y, int layer)
        {
            float top = FallbackStrandTop_U21(x, y);
            if (top <= 0.0f) return 0;
            float at = layer / (float)FurMath_U20.MaxLayers;
            if (top < at) return 0;
            float v = 0.35f + 0.65f * (1.0f - at / Math.Max(top, 0.001f));
            return (byte)Math.Clamp((int)(v * 255.0f + 0.5f), 1, 255);
        }

        public static byte FallbackNoiseValue_U21(int x, int y)
        {
            int n = FallbackFurSize_U21;
            x = ((x % n) + n) % n; y = ((y % n) + n) % n;
            if (Rand_U21(x, y, 0x9E37u) > 0.55f) return 0;
            return (byte)(96 + (int)(159 * Rand_U21(x, y, 0x3C6Fu)));
        }

        private ShaderResourceView MakeFallbackSrv_U21(Func<int, int, uint> pixel)
        {
            int n = FallbackFurSize_U21;
            var px = new uint[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++) px[y * n + x] = pixel(x, y);
            var desc = new Texture2DDescription
            {
                Width = n, Height = n, MipLevels = 1, ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
            };
            var h = GCHandle.Alloc(px, GCHandleType.Pinned);
            try
            {
                using var tex = new Texture2D(device, desc, new[] { new DataRectangle(h.AddrOfPinnedObject(), n * 4) });
                return new ShaderResourceView(device, tex);
            }
            finally { h.Free(); }
        }

        private void EnsureFurFallback_U21()
        {
            if (fallbackCombo_U21 != null && ReferenceEquals(fallbackDevice_U21, device)) return;
            fallbackDevice_U21 = device;
            fallbackCombo_U21 = new ShaderResourceView[4];
            for (int k = 0; k < 4; k++)
            {
                int slot = k;
                fallbackCombo_U21[k] = MakeFallbackSrv_U21((x, y) =>
                {
                    uint g = FallbackComboValue_U21(x, y, slot * 2);
                    uint a = FallbackComboValue_U21(x, y, slot * 2 + 1);
                    return g | (g << 8) | (g << 16) | (a << 24);
                });
            }
            fallbackNoise_U21 = MakeFallbackSrv_U21((x, y) =>
            {
                uint v = FallbackNoiseValue_U21(x, y);
                return v | (v << 8) | (v << 16) | 0xFF000000u;
            });
        }

        private int UseGrassFurFallback_U21(RenderMesh mesh)
        {
            EnsureFurFallback_U21();
            int filled = 0;
            for (int k = 0; k < 4; k++)
            {
                if (mesh.FurComboSRV[k] != null) continue;
                mesh.FurComboSRV[k] = fallbackCombo_U21[k];
                filled++;
            }
            mesh.FurPreviewTextures_U21 = filled > 0;
            return filled;
        }

        private void UsePedFurFallback_U21(RenderMesh mesh)
        {
            if (mesh.FurComboSRV[0] != null) return;
            EnsureFurFallback_U21();
            mesh.FurComboSRV[0] = fallbackNoise_U21;
            mesh.FurPreviewTextures_U21 = true;
        }
    }

    public partial class RenderMesh
    {
        public bool FurPreviewTextures_U21;
    }
}
