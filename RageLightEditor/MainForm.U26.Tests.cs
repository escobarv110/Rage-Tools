using System;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void SeqTest_U26(Action<string, bool, string> check)
        {
            var white = LightIconColour_U26(2, 3, 1);
            var amber = LightIconColour_U26(255, 100, 10);
            check("u26 light icons: a light's icon takes its colour at full brightness, and a black light shows white",
                  white == new Vector4(1, 1, 1, 1) && Math.Abs(amber.X - 1f) < 1e-5f && Math.Abs(amber.Y - 100f / 255f) < 1e-5f,
                  $"black {white} amber {amber}");

            var quad = new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(0, 0, 1) };
            var n = PortalNormal_U26(quad);
            check("u26 portals: the portal normal is the corner winding's", Math.Abs(Math.Abs(n.X) - 1f) < 1e-5f, n.ToString());
            check("u26 mirror photo: drawn smaller and with soft edges, no frame",
                  MirrorPhotoScale_U26 < 1f && MirrorPhotoFeather_U26 > 0f, $"scale {MirrorPhotoScale_U26} feather {MirrorPhotoFeather_U26}");
        }
    }
}
