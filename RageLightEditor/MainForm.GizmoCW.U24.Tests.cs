using System;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void SeqTest_GizmoCW_U24(Action<string, bool, string> check)
        {
            var axes = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            var pivot = new Vector3(10, 20, 5);
            var eye = pivot + new Vector3(30, 40, 50);
            Ray At(Vector3 p) => new Ray(eye, Vector3.Normalize(p - eye));
            float scale = 2.0f;
            float move = scale * GizmoStyle.CwMoveSize;

            int ax = GizmoStyle.HitTranslateCW(At(pivot + axes[0] * (0.7f * move)), pivot, axes, scale);
            int az = GizmoStyle.HitTranslateCW(At(pivot + axes[2] * (1.2f * move)), pivot, axes, scale);
            int pl = GizmoStyle.HitTranslateCW(At(pivot + (axes[0] + axes[1]) * (0.37f * move)), pivot, axes, scale);
            int ce = GizmoStyle.HitTranslateCW(At(pivot), pivot, axes, scale);
            int no = GizmoStyle.HitTranslateCW(At(pivot + new Vector3(0, 0, -3 * move)), pivot, axes, scale);
            check("cw gizmo move: the X line, the Z arrow, the XY square, the centre and empty space pick what is drawn",
                  ax == 0 && az == 2 && pl == GizmoStyle.PartPlane && ce == GizmoStyle.PartCentre && no == -1,
                  $"x {ax} z {az} xy {pl} centre {ce} empty {no}");

            float rot = scale * GizmoStyle.CwRotateSize;
            var toEye = Vector3.Normalize(new Vector3(eye.X - pivot.X, eye.Y - pivot.Y, 0));
            int rz = GizmoStyle.HitRotateCW(At(pivot + toEye * (0.75f * rot)), eye, pivot, axes, scale, new[] { true, true, true }, true);
            int rback = GizmoStyle.HitRotateCW(At(pivot - toEye * (0.75f * rot)), eye, pivot, axes, scale, new[] { false, false, true }, false);
            check("cw gizmo rotate: the near side of the Z ring picks Z and its hidden back half does not",
                  rz == 2 && rback == -1, $"near {rz} back {rback}");

            int si = GizmoStyle.HitScaleCW(At(pivot + (axes[0] + axes[1]) * (0.2f * move)), pivot, axes, scale, false);
            int so = GizmoStyle.HitScaleCW(At(pivot + (axes[0] + axes[1]) * (0.42f * move)), pivot, axes, scale, false);
            check("cw gizmo scale: the inner triangle scales all axes and the outer band scales two",
                  si == GizmoStyle.PartCentre && so == GizmoStyle.PartPlane, $"inner {si} band {so}");
            check("cw gizmo: style 3 is the CodeWalker look", LookFromSettingsDefault_U24() == GizmoStyle.GizmoLook.CodeWalker, "");
        }

        private static GizmoStyle.GizmoLook LookFromSettingsDefault_U24()
        {
            var s = new AppSettings();
            s.GizmoStyleIndex = 3;
            return GizmoStyle.LookFromSettings(s);
        }
    }
}
