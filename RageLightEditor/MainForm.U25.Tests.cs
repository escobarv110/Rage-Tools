using System;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void SeqTest_U25(Action<string, bool, string> check)
        {
            var spot = new LightAttributes { Type = LightType.Spot, ConeInnerAngle = 20, ConeOuterAngle = 40 };
            var pos = new Vector3(0, 0, 10);
            var down = -Vector3.UnitZ;
            float under = LightDefs.CoronaView_U25(spot, pos, down, new Vector3(0, 0, 0));
            float beside = LightDefs.CoronaView_U25(spot, pos, down, new Vector3(20, 0, 10));
            float above = LightDefs.CoronaView_U25(spot, pos, down, new Vector3(0, 0, 30));
            var point = new LightAttributes { Type = LightType.Point };
            float pt = LightDefs.CoronaView_U25(point, pos, down, new Vector3(0, 0, 30));
            check("u25 corona: a spot's corona shows inside its beam and not from beside or behind it",
                  under > 0.99f && beside < 0.001f && above < 0.001f && pt > 0.99f, $"under {under:0.00} beside {beside:0.00} above {above:0.00} point {pt:0.00}");

            var amber = Ldr_U25(new Vector4(2.2f, 1.716f, 0.66f, 1f));
            var blue = Ldr_U25(new Vector4(0.0f, 0.5f, 2.0f, 0.4f));
            check("u25 outlines: the selection colour turns CodeWalker green and other colours keep their hue at full brightness",
                  amber == new Vector4(0, 1, 0, 1) && Math.Abs(blue.Z - 1f) < 1e-4f && Math.Abs(blue.Y - 0.25f) < 1e-4f && Math.Abs(blue.W - 0.4f) < 1e-4f,
                  $"amber {amber} blue {blue}");

            check("u25 exposure: Default puts it back to 1.0", Math.Abs(LightPanel.DefaultExposure_U25 - 1.0f) < 1e-6f, LightPanel.DefaultExposure_U25.ToString());
        }
    }
}
