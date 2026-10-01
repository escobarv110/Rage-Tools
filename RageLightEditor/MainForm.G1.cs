using System;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void DrawGizmoOverlay(SharpDX.Direct3D11.DeviceContext context)
        {
            bool any = panel.WorldMode ? worldBuilt : gizmo != null;
            if (!any) return;

            GizmoStyle.ApplySettings(settings);
            ApplyGizmoDebugCamera();
            ApplyGizmoDebugPose();

            GizmoStyle.ScreenText = true;
            try { DrawGizmoPass(1.0f); }
            finally { GizmoStyle.ScreenText = false; }
            lineRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.DepthDisabled);
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthDisabled);
        }

        private void ApplyGizmoDebugPose()
        {
            if (gizmoDebugPose == null)
            {
                var env = Environment.GetEnvironmentVariable("RLE_GIZMO");
                gizmoDebugPose = new float[5];
                gizmoDebugPose[4] = -1;
                if (string.IsNullOrEmpty(env)) { gizmoDebugPose[0] = -1; return; }
                var parts = env.Split(',');
                for (int i = 0; i < 5 && i < parts.Length; i++)
                    float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out gizmoDebugPose[i]);
            }
            if (gizmoDebugPose[0] < 0) return;
            if (gizmoDebugPose[4] >= 0) GizmoStyle.SetDebugLook((int)gizmoDebugPose[4]);
            int mode = (int)gizmoDebugPose[0], part = (int)gizmoDebugPose[1];
            bool drag = gizmoDebugPose[2] > 0.5f;
            float amount = gizmoDebugPose[3];
            if (panel.WorldMode)
            {
                if (!worldBuilt) return;
                worldGizmo.Mode = (WorldGizmoMode)Math.Clamp(mode, 0, 3);
                worldGizmo.DebugPose(camera, part, drag, amount);
            }
            else if (gizmo != null)
            {
                gizmo.Mode = (GizmoMode)Math.Clamp(mode, 0, 2);
                gizmo.DebugPose(camera, part, drag, amount);
            }
        }
        private float[] gizmoDebugPose;

        private void ApplyGizmoDebugCamera()
        {
            if (gizmoDebugCamDone) return;
            var env = Environment.GetEnvironmentVariable("RLE_GIZMOCAM");
            if (string.IsNullOrEmpty(env)) { gizmoDebugCamDone = true; return; }
            Vector3 target;
            if (panel.WorldMode)
            {
                if (!worldBuilt || !WorldEdit.Selection.HasValue) return;
                var t = WorldEdit.Selection.GizmoTarget();
                if (t == null) return;
                target = t.Position;
            }
            else
            {
                if (gizmo == null) return;
                var l = scene.SelectedLight;
                if (l == null) return;
                target = scene.GetInstance(l).WorldPosition;
            }
            gizmoDebugCamDone = true;
            var p = env.Split(',');
            static float Num(string[] a, int i, float def) =>
                (i < a.Length && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) ? v : def;
            camera.Target = target;
            camera.Distance = camera.TargetDistance = Num(p, 0, 3f);
            camera.Yaw = camera.TargetYaw = Num(p, 1, 40f) * 0.0174533f;
            camera.Pitch = camera.TargetPitch = Num(p, 2, 25f) * 0.0174533f;
            camera.SnapSmoothing();
            camera.Update();
            screenshotFrames = Math.Max(screenshotFrames, 6);
        }
        private bool gizmoDebugCamDone;

        private void DrawGizmoPass(float alphaMul)
        {
            if (panel.WorldMode)
            {
                if (worldBuilt) worldGizmo.Draw(lineRenderer, triRenderer, camera, WorldGizmoTargets(), alphaMul);
            }
            else if (gizmo != null)
            {
                gizmo.Draw(lineRenderer, triRenderer, camera, alphaMul);
            }
        }

        private void CameraFeelTest_G1(Action<string, bool, string> check)
        {
            var saved = camera.Capture();
            float savedSmooth = camera.Smoothness;
            bool savedWalk = walkMode;

            camera.Target = new Vector3(0, 0, 2); camera.Distance = 1.5f; camera.Yaw = 1.0f; camera.Pitch = 0.1f;
            camera.SnapSmoothing(); camera.Update();

            camera.Smoothness = 0.0f;
            float tenDeg = 10.0f * 0.0174533f;
            camera.Orbit(-tenDeg / camera.Sensitivity, 0);
            camera.Update(1.0f / 60.0f);
            check("smoothing 0: yaw is the target after one frame", Math.Abs(camera.Yaw - camera.TargetYaw) < 1e-6f &&
                  Math.Abs(camera.Yaw - (1.0f + tenDeg)) < 1e-4f, $"yaw {camera.Yaw:0.00000} target {camera.TargetYaw:0.00000}");
            camera.Orbit(0, 30.0f);
            camera.Update(1.0f / 20.0f);
            check("smoothing 0: pitch is the target at 20 fps", Math.Abs(camera.Pitch - camera.TargetPitch) < 1e-6f,
                  $"pitch {camera.Pitch:0.00000} target {camera.TargetPitch:0.00000}");
            camera.Zoom(120);
            camera.Update(1.0f / 60.0f);
            check("smoothing 0: wheel lands in one frame", Math.Abs(camera.Distance - camera.TargetDistance) < 1e-5f,
                  $"dist {camera.Distance:0.0000} target {camera.TargetDistance:0.0000}");

            camera.Smoothness = 10.0f;
            camera.SnapSmoothing();
            float y0 = camera.Yaw;
            camera.Orbit(-tenDeg / camera.Sensitivity, 0);
            camera.Update(1.0f / 60.0f);
            float expect = y0 + tenDeg * Math.Min(10.0f / 60.0f, 1.0f);
            check("smoothing 10 @60fps: closes 1/6 of the gap", Math.Abs(camera.Yaw - expect) < 1e-5f,
                  $"yaw {camera.Yaw:0.00000} expected {expect:0.00000}");
            camera.Update(0.5f);
            check("hitch frame: lands on the target, no overshoot", Math.Abs(camera.Yaw - camera.TargetYaw) < 1e-6f,
                  $"yaw {camera.Yaw:0.00000} target {camera.TargetYaw:0.00000}");
            camera.Update(1.0f / 60.0f);
            check("and stays there", Math.Abs(camera.Yaw - camera.TargetYaw) < 1e-6f, $"yaw {camera.Yaw:0.00000}");

            camera.Smoothness = 0.0f;
            camera.SnapSmoothing(); camera.Update();
            var before = camera.Position;
            walkKeys.Clear(); walkKeys.Add(System.Windows.Forms.Keys.Up);
            float speed = 50.0f * settings.WalkSpeed * Math.Min(camera.TargetDistance, 20.0f);
            ApplyWalkKeys(2.0f);
            camera.Update(2.0f);
            float moved = (camera.Position - before).Length();
            walkKeys.Clear();
            check("walk keys: a 2 s hitch moves at most 0.1 s worth", moved <= speed * 0.1f + 1e-3f && moved > 0,
                  $"moved {moved:0.000} m (0.1 s worth = {speed * 0.1f:0.000} m)");

            camera.FieldOfView = 10.0f * 0.0174533f; camera.Update();
            bool ten = Math.Abs(camera.FieldOfView - 10.0f * 0.0174533f) < 1e-5f;
            camera.FieldOfView = 120.0f * 0.0174533f; camera.Update();
            bool hundredTwenty = Math.Abs(camera.FieldOfView - 120.0f * 0.0174533f) < 1e-5f;
            camera.FieldOfView = 5.0f * 0.0174533f; camera.Update();
            bool five = Math.Abs(camera.FieldOfView - Camera.MinFovDeg * 0.0174533f) < 1e-5f;
            camera.FieldOfView = 150.0f * 0.0174533f; camera.Update();
            bool oneFifty = Math.Abs(camera.FieldOfView - Camera.MaxFovDeg * 0.0174533f) < 1e-5f;
            check("FOV accepts 10..120 and clamps outside", ten && hundredTwenty && five && oneFifty,
                  $"10:{ten} 120:{hundredTwenty} 5->10:{five} 150->120:{oneFifty}");

            camera.ViewportHeight = 900;
            camera.FieldOfView = 54.0f * 0.0174533f; camera.Update();
            float px2 = GizmoStyle.Scale(camera, camera.Position + camera.GetForward() * 2.0f) / camera.WorldPerPixel(camera.Position + camera.GetForward() * 2.0f);
            float px200 = GizmoStyle.Scale(camera, camera.Position + camera.GetForward() * 200.0f) / camera.WorldPerPixel(camera.Position + camera.GetForward() * 200.0f);
            float m54 = GizmoStyle.Scale(camera, camera.Position + camera.GetForward() * 10.0f);
            camera.FieldOfView = 20.0f * 0.0174533f; camera.Update();
            float m20 = GizmoStyle.Scale(camera, camera.Position + camera.GetForward() * 10.0f);
            check("gizmo is a constant pixel size", Math.Abs(px2 - GizmoStyle.AxisPx) < 0.01f && Math.Abs(px200 - GizmoStyle.AxisPx) < 0.01f && m20 < m54 * 0.5f,
                  $"{px2:0.0} px at 2 m, {px200:0.0} px at 200 m; {m54:0.000} m at 54 deg vs {m20:0.000} m at 20 deg (10 m out)");

            camera.Smoothness = savedSmooth;
            camera.Restore(saved);
            SetWalkMode(savedWalk);
        }
    }
}

