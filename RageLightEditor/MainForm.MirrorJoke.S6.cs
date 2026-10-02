using System;
using System.Diagnostics;
using SharpDX;
using SharpDX.Direct3D11;
using RageLightEditor.Rendering;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private readonly Stopwatch mirrorJokeClock_S6 = Stopwatch.StartNew();
        private readonly Random mirrorJokeRng_S6 = new Random(20260818);
        private double mirrorJokeDwellFrom_S6 = -1;
        private double mirrorJokeShowFrom_S6 = -1;
        private double mirrorJokeReadyAt_S6;
        private bool mirrorJokeLogged_S6;
        private readonly System.Collections.Generic.List<(Vector3 A, Vector3 B, Vector3 C, Vector4 CA, Vector4 CB, Vector4 CC)> jokeTris_S6 = new();

        private const float JokeGrow = 0.25f;
        private const float JokeHold = JokeVisibleSeconds_T6 - JokeGrow - JokeFall;
        private const float JokeFall = 0.35f;
        private const float JokeDwell = 0.35f;
        private const float JokeNear = 3.0f;
        private const float JokeFar = 0.02f;

        private static readonly int MirrorJokeForce_S6 =
            Environment.GetEnvironmentVariable("RLE_MIRRORJOKE") == "0" ? 0 :
            Environment.GetEnvironmentVariable("RLE_MIRRORJOKE") == "1" ? 1 : -1;

        private void WireMirrorJoke_S6()
        {
            if (sceneRenderer != null && sceneRenderer.ReflectionExtras_S6 == null)
                sceneRenderer.ReflectionExtras_S6 = DrawMirrorJoke_S6;
        }

        private void DrawMirrorJoke_S6(DeviceContext context, Matrix reflViewProj, Plane plane, int pass)
        {
            if (pass != 0 || triRenderer == null || camera == null) return;
            if (MirrorJokeForce_S6 == 0) return;
            if (MirrorJokeBlocked_T6(photoMode, renderingStill, panel?.CineMode ?? false))
            {
                NoteJokeBlocked_T6(photoMode, renderingStill, panel?.CineMode ?? false);
                return;
            }
            if (MirrorJokeForce_S6 != 1 && !(panel?.MirrorSurprise_S6 ?? true)) return;

            var n = plane.Normal;
            var eye = camera.Position;
            float d = Vector3.Dot(n, eye) + plane.D;
            var fwd = camera.GetForward();
            bool facing = FacingAMirror_S6(n, d, fwd);

            if (!facing) return;
            const float k = 1f;

            var mirrorBox = sceneRenderer?.ReflectionMirrorBounds_T6 ?? default;
            EnsureMirrorPhoto_V2(context.Device);
            var fit = JokeFitCamera_U4(mirrorBox, plane,
                                       HaveMirrorPhoto_V2 ? MirrorPhotoAspect_V2 : JokeAspect_T6);

            if (HaveMirrorPhoto_V2 && mirrorPhotoRenderer_V2 != null)
            {
                PhotoQuad_V2(fit, MirrorPhotoAspect_V2, 0f, out var qr, out var qu);
                mirrorPhotoRenderer_V2.Draw(context, reflViewProj, mirrorPhotoSrv_V2,
                                            fit.Centre, qr * MirrorPhotoScale_U26, qu * MirrorPhotoScale_U26, new Vector4(MirrorPhotoBright_U26, MirrorPhotoBright_U26, MirrorPhotoBright_U26, k), 0f, MirrorPhotoFeather_U26);
            }
            else DrawBoldFinger_T6(context, reflViewProj, fit, k);
            if (screenshotPath != null && !mirrorJokeLogged_S6)
            {
                mirrorJokeLogged_S6 = true;
                var mb = sceneRenderer?.ReflectionMirrorBounds_T6 ?? default;
                Console.WriteLine($"MIRRORJOKE at {fit.Centre.X:0.00},{fit.Centre.Y:0.00},{fit.Centre.Z:0.00} " +
                                  $"mirror {d:0.00} m, height {fit.Height:0.00} m, {jokeTris_S6.Count} tris, " +
                                  $"glass {(mb.Maximum.X > mb.Minimum.X ? $"{mb.Maximum.X - mb.Minimum.X:0.##}x{mb.Maximum.Y - mb.Minimum.Y:0.##}x{mb.Maximum.Z - mb.Minimum.Z:0.##} m" : "(unknown)")}");
            }
        }

        public static bool FacingAMirror_S6(Vector3 planeNormal, float distToPlane, Vector3 forward)
        {
            if (Math.Abs(planeNormal.Z) > 0.5f) return false;
            if (!(distToPlane > JokeFar) || !(distToPlane < JokeNear)) return false;
            return Vector3.Dot(forward, -planeNormal) > 0.80f;
        }

    }
}

