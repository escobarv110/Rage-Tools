using System;
using RageLightEditor.Editor;
using SharpDX;
using SharpDX.Direct3D11;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private Rendering.LightVolumeRenderer_U25 volumeRenderer_U25;
        private string volumeError_U25;

        private void AddVolume_U25(in Scene.VolumeDraw v)
        {
            if (volumeRenderer_U25 == null || !volumeRenderer_U25.Ready) return;
            if (v.Type != 1 && v.Type != 2) return;
            float radius = Math.Max(v.Falloff * v.SizeScale, 0.05f);
            float outerA = MathUtil.Clamp(v.OuterAngleRad, 0.001f, 1.55f);
            float innerA = MathUtil.Clamp(v.InnerAngleRad, 0.0f, outerA);
            float scale = v.Intensity * v.LightIntensity;
            var inner = v.Colour * scale;
            var outer = v.HasOuter ? v.OuterColour * v.OuterIntensity * scale : inner;
            volumeRenderer_U25.Add(v.Pos, v.Dir, radius, (float)Math.Cos(outerA), (float)Math.Cos(innerA),
                v.FalloffExponent, inner, outer, v.HasOuter ? v.OuterExponent : 1.0f, v.Type);
        }

        private void FlushVolumes_U25(DeviceContext context)
        {
            if (volumeRenderer_U25 == null || volumeRenderer_U25.Count == 0) { volumeRenderer_U25?.Clear(); return; }
            try
            {
                int mode = deviceResources.BeginDepthRead(out var depthSrv);
                try
                {
                    volumeRenderer_U25.Intensity = 1.0f;
                    volumeRenderer_U25.Flush(context, camera, depthSrv, mode, deviceResources.Width, deviceResources.Height);
                }
                finally { deviceResources.EndDepthRead(); }
            }
            catch (Exception ex)
            {
                if (volumeError_U25 != ex.Message) { volumeError_U25 = ex.Message; Console.WriteLine("LIGHTVOLUMES draw: " + ex.Message); }
                volumeRenderer_U25.Clear();
            }
        }
    }
}
