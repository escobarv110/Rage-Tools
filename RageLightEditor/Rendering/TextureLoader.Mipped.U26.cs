using System;
using System.IO;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace RageLightEditor.Rendering
{
    public partial class TextureLoader
    {
        public ShaderResourceView LoadEmbeddedPngMipped_U26(string resourceName, out int width, out int height)
        {
            width = height = 0;
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var name = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) return null;
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return LoadPngMipped_U26(ms.ToArray(), out width, out height);
            }
            catch { return null; }
        }

        public ShaderResourceView LoadPngMipped_U26(byte[] png, out int width, out int height)
        {
            width = height = 0;
            if (png == null || png.Length == 0) return null;
            try
            {
                using var ms = new MemoryStream(png);
                using var bmp = new System.Drawing.Bitmap(ms);
                width = bmp.Width; height = bmp.Height;
                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var desc = new Texture2DDescription
                    {
                        Width = bmp.Width,
                        Height = bmp.Height,
                        MipLevels = 0,
                        ArraySize = 1,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                        OptionFlags = ResourceOptionFlags.GenerateMipMaps,
                    };
                    using var tex = new Texture2D(device, desc);
                    var ctx = device.ImmediateContext;
                    ctx.UpdateSubresource(new SharpDX.DataBox(data.Scan0, data.Stride, 0), tex, 0);
                    var srv = new ShaderResourceView(device, tex);
                    ctx.GenerateMips(srv);
                    return srv;
                }
                finally { bmp.UnlockBits(data); }
            }
            catch { return null; }
        }
    }
}
