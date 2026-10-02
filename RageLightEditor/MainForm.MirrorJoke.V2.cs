using System;
using System.IO;
using SharpDX;
using SharpDX.Direct3D11;
using RageLightEditor.Rendering;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public const string MirrorPhotoFile_V2 = "mirror_photo.png";
        public const float MirrorPhotoScale_U26 = 0.6f;
        public const float MirrorPhotoBright_U26 = 1.4f;
        public const float MirrorPhotoFeather_U26 = 0.7f;

        private MirrorPhotoRenderer_V2 mirrorPhotoRenderer_V2;
        private ShaderResourceView mirrorPhotoSrv_V2;
        private Device mirrorPhotoDevice_V2;
        private bool mirrorPhotoTried_V2;
        private int mirrorPhotoW_V2, mirrorPhotoH_V2;
        private bool mirrorPhotoLogged_V2;

        public bool HaveMirrorPhoto_V2 => mirrorPhotoSrv_V2 != null && !mirrorPhotoSrv_V2.IsDisposed;

        public static string MirrorPhotoPath_V2()
        {
            try { return Path.Combine(AppContext.BaseDirectory ?? ".", MirrorPhotoFile_V2); }
            catch { return MirrorPhotoFile_V2; }
        }

        private void EnsureMirrorPhoto_V2(Device device)
        {
            if (device == null) return;
            if (mirrorPhotoDevice_V2 != null && !ReferenceEquals(mirrorPhotoDevice_V2, device))
            {
                mirrorPhotoRenderer_V2?.Dispose(); mirrorPhotoRenderer_V2 = null;
                mirrorPhotoSrv_V2?.Dispose(); mirrorPhotoSrv_V2 = null;
                mirrorPhotoTried_V2 = false;
            }
            if (mirrorPhotoTried_V2) return;
            mirrorPhotoTried_V2 = true;
            mirrorPhotoDevice_V2 = device;

            try
            {
                var path = MirrorPhotoPath_V2();
                if (File.Exists(path))
                    mirrorPhotoSrv_V2 = textureLoader?.LoadPngMipped_U26(File.ReadAllBytes(path), out mirrorPhotoW_V2, out mirrorPhotoH_V2);
                if (mirrorPhotoSrv_V2 == null)
                    mirrorPhotoSrv_V2 = textureLoader?.LoadEmbeddedPngMipped_U26(MirrorPhotoFile_V2, out mirrorPhotoW_V2, out mirrorPhotoH_V2);
                if (mirrorPhotoSrv_V2 != null) mirrorPhotoRenderer_V2 = new MirrorPhotoRenderer_V2(device);
            }
            catch (Exception e)
            {
                mirrorPhotoSrv_V2 = null;
                mirrorPhotoRenderer_V2 = null;
                Console.WriteLine("MIRRORPHOTO load failed: " + e.Message);
            }

            if (!mirrorPhotoLogged_V2)
            {
                mirrorPhotoLogged_V2 = true;
                Console.WriteLine(HaveMirrorPhoto_V2
                    ? $"MIRRORPHOTO {mirrorPhotoW_V2}x{mirrorPhotoH_V2} from {(File.Exists(MirrorPhotoPath_V2()) ? MirrorPhotoPath_V2() : "the embedded resources")}"
                    : $"MIRRORPHOTO none - drop {MirrorPhotoFile_V2} beside the exe; the silhouette is drawn meanwhile");
            }
        }

        public float MirrorPhotoAspect_V2 =>
            mirrorPhotoW_V2 > 0 && mirrorPhotoH_V2 > 0 ? (float)mirrorPhotoW_V2 / mirrorPhotoH_V2 : 4f / 3f;

        public static void PhotoQuad_V2(JokePlacement_T6 fit, float aspect, float maxWidth,
                                        out Vector3 right, out Vector3 up)
        {
            float h = Math.Max(fit.Height, 0.01f);
            float a = aspect > 0.01f && aspect < 100f ? aspect : 4f / 3f;
            float w = h * a;
            if (maxWidth > 0.01f && w > maxWidth) { float k = maxWidth / w; w *= k; h *= k; }
            right = fit.Right * (w * 0.5f);
            up = fit.Up * (h * 0.5f);
        }

        private void DisposeMirrorPhoto_V2()
        {
            mirrorPhotoRenderer_V2?.Dispose(); mirrorPhotoRenderer_V2 = null;
            mirrorPhotoSrv_V2?.Dispose(); mirrorPhotoSrv_V2 = null;
            mirrorPhotoDevice_V2 = null;
        }

        partial void SeqTest_V2(Action<string, bool, string> check) => MirrorPhotoTest_V2(check);

        private void MirrorPhotoTest_V2(Action<string, bool, string> check)
        {
            var wall = new Vector3(0, 1, 0);
            var at = new Vector3(0, -1, 0);

            check("standing right against the glass still shows the picture",
                  FacingAMirror_S6(wall, 0.06f, at), "6 cm");
            check("...and at arm's length",
                  FacingAMirror_S6(wall, 0.60f, at), "60 cm");
            check("...and across a small room",
                  FacingAMirror_S6(wall, 2.50f, at), "2.5 m");
            check("but not from the far side of a big one",
                  !FacingAMirror_S6(wall, 6.0f, at), "6 m");
            check("and not while walking past it",
                  !FacingAMirror_S6(wall, 1.2f, new Vector3(1, 0, 0)), "");

            foreach (float d in new[] { 0.06f, 0.12f, 0.30f, 0.80f, 3.0f })
            {
                var fit = JokeFitCamera_U4(default, Vector3.Zero, at,
                                           new Vector3(1, 0, 0), Vector3.UnitZ, wall, d);
                float ahead = Vector3.Dot(fit.Centre, at);
                check($"at {d:0.00} m the picture is between the eye and the glass",
                      ahead > 0.0f && ahead < d, $"{ahead:0.###} of {d:0.00} m");
            }

            var f = new JokePlacement_T6
            {
                Centre = Vector3.Zero, Right = new Vector3(1, 0, 0), Up = Vector3.UnitZ,
                Fwd = new Vector3(0, -1, 0), Height = 0.40f, Fill = 0.72f,
            };
            PhotoQuad_V2(f, 1.5f, 0f, out var r, out var u);
            check("a landscape picture is drawn 3:2",
                  Math.Abs(r.Length() * 2f / (u.Length() * 2f) - 1.5f) < 0.001f,
                  $"{r.Length() * 2f:0.###} x {u.Length() * 2f:0.###} m");
            PhotoQuad_V2(f, 1.5f, 0.30f, out r, out u);
            check("...and shrinks, shape intact, rather than spilling past a narrow mirror",
                  r.Length() * 2f <= 0.3001f && Math.Abs(r.Length() / u.Length() - 1.5f) < 0.001f,
                  $"{r.Length() * 2f:0.###} x {u.Length() * 2f:0.###} m");
            PhotoQuad_V2(f, 0f, 0f, out r, out u);
            check("an unreadable size falls back to a 4:3 print instead of a zero-area quad",
                  r.Length() > 0.001f && u.Length() > 0.001f, $"{r.Length() * 2f:0.###} x {u.Length() * 2f:0.###} m");

            check("photo mode, a still and Cinematic still refuse it",
                  MirrorJokeBlocked_T6(true, false, false) && MirrorJokeBlocked_T6(false, true, false) &&
                  MirrorJokeBlocked_T6(false, false, true) && !MirrorJokeBlocked_T6(false, false, false), "");
        }
    }
}

