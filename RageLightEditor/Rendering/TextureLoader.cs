using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CodeWalker.GameFiles;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using DXTexture2D = SharpDX.Direct3D11.Texture2D;
using GameTexture = CodeWalker.GameFiles.Texture;

namespace RageLightEditor.Rendering
{
    public partial class TextureLoader : IDisposable
    {
        private readonly Device device;
        private readonly Dictionary<(GameTexture, bool), ShaderResourceView> cache =
            new Dictionary<(GameTexture, bool), ShaderResourceView>();
        private readonly List<IDisposable> resources = new List<IDisposable>();

        public TextureLoader(Device device)
        {
            this.device = device;
        }

        public ShaderResourceView LoadPngFile_V2(string path, out int width, out int height)
        {
            width = height = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
                var bytes = System.IO.File.ReadAllBytes(path);
                using var stream = new System.IO.MemoryStream(bytes);
                using var bmp = new System.Drawing.Bitmap(stream);
                return UploadBitmap_V2(bmp, out width, out height);
            }
            catch { return null; }
        }

        private ShaderResourceView UploadBitmap_V2(System.Drawing.Bitmap bmp, out int width, out int height)
        {
            width = bmp.Width; height = bmp.Height;
            var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                var desc = new Texture2DDescription
                {
                    Width = bmp.Width,
                    Height = bmp.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Immutable,
                    BindFlags = BindFlags.ShaderResource,
                };
                using var tex = new Texture2D(device, desc, new DataRectangle(data.Scan0, data.Stride));
                return new ShaderResourceView(device, tex);
            }
            finally { bmp.UnlockBits(data); }
        }

        public ShaderResourceView LoadEmbeddedPng(string resourceName, out int width, out int height)
        {
            width = height = 0;
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var name = Array.Find(asm.GetManifestResourceNames(),
                    n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;

                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) return null;
                using var src = new System.Drawing.Bitmap(stream);

                using var bmp = TrimTransparent(src);
                width = bmp.Width; height = bmp.Height;

                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var desc = new Texture2DDescription
                    {
                        Width = bmp.Width,
                        Height = bmp.Height,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Immutable,
                        BindFlags = BindFlags.ShaderResource,
                    };
                    using var tex = new Texture2D(device, desc,
                        new DataRectangle(data.Scan0, data.Stride));
                    return new ShaderResourceView(device, tex);
                }
                finally { bmp.UnlockBits(data); }
            }
            catch { return null; }
        }

        private static System.Drawing.Bitmap TrimTransparent(System.Drawing.Bitmap src)
        {
            int w = src.Width, h = src.Height;
            int minX = w, minY = h, maxX = -1, maxY = -1;

            var d = src.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = (byte*)d.Scan0 + y * d.Stride;
                        for (int x = 0; x < w; x++)
                        {
                            if (row[x * 4 + 3] <= 8) continue;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }
            finally { src.UnlockBits(d); }

            if (maxX < minX || maxY < minY) return (System.Drawing.Bitmap)src.Clone();
            if (minX == 0 && minY == 0 && maxX == w - 1 && maxY == h - 1)
                return (System.Drawing.Bitmap)src.Clone();

            var rect = new System.Drawing.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return src.Clone(rect, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }

        public ShaderResourceView GetSRV(GameTexture tex, bool srgb = false)
        {
            if (tex?.Data?.FullData == null) return null;
            var key = (tex, srgb);
            if (cache.TryGetValue(key, out var srv)) return srv;
            srv = Upload(tex, srgb);
            cache[key] = srv;
            return srv;
        }

        private ShaderResourceView Upload(GameTexture tex, bool srgb)
        {
            var fmt = GetDXGIFormat(tex.Format, srgb);
            if (fmt == Format.Unknown) return null;

            var data = tex.Data.FullData;
            int width = tex.Width;
            int height = tex.Height;
            int levels = Math.Max((int)tex.Levels, 1);
            var srcFormat = tex.Format;

            if (srcFormat == TextureFormat.D3DFMT_L8)
            {
                int total = 0;
                for (int i = 0; i < levels; i++) total += Math.Max(width >> i, 1) * Math.Max(height >> i, 1);
                var rgba = new byte[total * 4];
                int si = 0, di = 0;
                for (int i = 0; i < levels && si < data.Length; i++)
                {
                    int n = Math.Max(width >> i, 1) * Math.Max(height >> i, 1);
                    for (int k = 0; k < n && si < data.Length; k++, si++)
                    {
                        byte l = data[si];
                        rgba[di++] = l; rgba[di++] = l; rgba[di++] = l; rgba[di++] = 255;
                    }
                }
                data = rgba;
                srcFormat = TextureFormat.D3DFMT_A8B8G8R8;
            }

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                var boxes = new List<DataBox>();
                int offset = 0;
                for (int i = 0; i < levels; i++)
                {
                    int mipw = Math.Max(width >> i, 1);
                    int miph = Math.Max(height >> i, 1);
                    ComputePitch(srcFormat, mipw, miph, out int rowPitch, out int slicePitch);
                    if (offset + slicePitch > data.Length) break;
                    boxes.Add(new DataBox(IntPtr.Add(ptr, offset), rowPitch, slicePitch));
                    offset += slicePitch;
                }
                if (boxes.Count == 0) return null;

                var desc = new Texture2DDescription
                {
                    Width = width,
                    Height = height,
                    MipLevels = boxes.Count,
                    ArraySize = 1,
                    Format = fmt,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None,
                };

                var tex2d = new DXTexture2D(device, desc, boxes.ToArray());
                resources.Add(tex2d);
                var srv = new ShaderResourceView(device, tex2d);
                resources.Add(srv);
                return srv;
            }
            catch
            {
                return null;
            }
            finally
            {
                handle.Free();
            }
        }

        public static bool HasSrgbFormat(TextureFormat f)
        {
            var a = GetDXGIFormat(f, false);
            var b = GetDXGIFormat(f, true);
            return a != Format.Unknown && a != b;
        }

        public static Format GetDXGIFormat(TextureFormat f, bool srgb = false)
        {
            switch ((uint)f)
            {
                case 0x31545844: return srgb ? Format.BC1_UNorm_SRgb : Format.BC1_UNorm;
                case 0x33545844: return srgb ? Format.BC2_UNorm_SRgb : Format.BC2_UNorm;
                case 0x35545844: return srgb ? Format.BC3_UNorm_SRgb : Format.BC3_UNorm;
                case 0x31495441: return Format.BC4_UNorm;
                case 0x32495441: return Format.BC5_UNorm;
                case 0x20374342: return srgb ? Format.BC7_UNorm_SRgb : Format.BC7_UNorm;
                case 21: return srgb ? Format.B8G8R8A8_UNorm_SRgb : Format.B8G8R8A8_UNorm;
                case 22: return srgb ? Format.B8G8R8X8_UNorm_SRgb : Format.B8G8R8X8_UNorm;
                case 25: return Format.B5G5R5A1_UNorm;
                case 28: return Format.A8_UNorm;
                case 32: return srgb ? Format.R8G8B8A8_UNorm_SRgb : Format.R8G8B8A8_UNorm;
                case 50: return srgb ? Format.R8G8B8A8_UNorm_SRgb : Format.R8G8B8A8_UNorm;
                default: return Format.Unknown;
            }
        }

        public static int ExpectedDataSize(TextureFormat f, int width, int height, int levels)
        {
            int total = 0;
            for (int i = 0; i < Math.Max(levels, 1); i++)
            {
                int w = Math.Max(width >> i, 1);
                int h = Math.Max(height >> i, 1);
                ComputePitch(f, w, h, out _, out int slice);
                total += slice;
            }
            return total;
        }

        public static int Mip0Size(TextureFormat f, int width, int height)
        {
            ComputePitch(f, Math.Max(width, 1), Math.Max(height, 1), out _, out int slice);
            return slice;
        }

        private static void ComputePitch(TextureFormat f, int w, int h, out int rowPitch, out int slicePitch)
        {
            uint fu = (uint)f;
            switch (fu)
            {
                case 0x31545844:
                case 0x31495441:
                    rowPitch = Math.Max(1, (w + 3) / 4) * 8;
                    slicePitch = rowPitch * Math.Max(1, (h + 3) / 4);
                    return;
                case 0x33545844:
                case 0x35545844:
                case 0x32495441:
                case 0x20374342:
                    rowPitch = Math.Max(1, (w + 3) / 4) * 16;
                    slicePitch = rowPitch * Math.Max(1, (h + 3) / 4);
                    return;
                default:
                    int bpp = GetBitsPerPixel(fu);
                    rowPitch = (w * bpp + 7) / 8;
                    slicePitch = rowPitch * h;
                    return;
            }
        }

        private static int GetBitsPerPixel(uint f)
        {
            switch (f)
            {
                case 21: case 22: case 32: return 32;
                case 25: return 16;
                case 28: case 50: return 8;
                default: return 32;
            }
        }

        public void Clear()
        {
            foreach (var r in resources) r?.Dispose();
            resources.Clear();
            cache.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}

