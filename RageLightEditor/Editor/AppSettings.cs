using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace RageLightEditor.Editor
{
    public partial class AppSettings
    {
        public Dictionary<string, string> Binds { get; set; } = new Dictionary<string, string>();
        public float LeftPanelWidth { get; set; } = 250;
        public float RightPanelWidth { get; set; } = 400;
        public float RotateSnapDeg { get; set; } = 5.0f;
        public float WalkSpeed { get; set; } = 0.25f;
        public int ThemeIndex { get; set; } = UiTheme.DefaultTheme;
        public float UiScaleV17 { get; set; } = 0f;
        public int MloLabelModeV19 { get; set; }
        public float PanelScaleV17 { get; set; } = 1.0f;
        public bool ThemeDefaulted { get; set; }
        public bool ThemeGraphiteU22 { get; set; }

        public static float[] DefaultAccent = { 0.96f, 0.65f, 0.11f };

        public static float[] DefaultMaterialAccent = { 0.694f, 0.196f, 0.914f };

        public float[] Accent { get; set; } = { DefaultAccent[0], DefaultAccent[1], DefaultAccent[2] };

        public float[] AccentMaterial { get; set; } =
            { DefaultMaterialAccent[0], DefaultMaterialAccent[1], DefaultMaterialAccent[2] };

        public float[] AccentFor(bool material) => material ? AccentMaterial : Accent;
        public float FovDeg { get; set; } = 85.0f;
        public bool FovDefaulted { get; set; } = false;
        public float CameraSensitivity { get; set; } = 0.005f;
        public float CameraSmoothing { get; set; } = 0.0f;
        public bool CameraSmoothingDefaulted { get; set; } = false;
        public float OrbitAnchorMax { get; set; } = 1.5f;
        public bool VSync { get; set; } = true;
        public bool BackfaceCulling { get; set; } = true;
        public int BackfaceMode { get; set; } = 0;

        public float WeatherTransitionSeconds { get; set; } = 0.0f;
        public bool ControlTimeOfDay { get; set; } = true;
        public bool ProjectAutoCalcFlags { get; set; } = true;
        public bool ProjectAutoCalcExtents { get; set; } = true;
        public bool ProjectDisplayEntityIndexes { get; set; } = false;
        public bool WorldGrass { get; set; } = true;
        public float WorldGrassDistance { get; set; } = 1.0f;
        public bool WorldHdTextures { get; set; } = true;
        public bool ToneMapCodeWalker { get; set; } = true;
        public bool AutoExposure { get; set; } = true;
        public float RageBloom { get; set; } = 1.0f;
        public bool GameFog { get; set; } = true;
        public float FogScale { get; set; } = 1.0f;
        public bool WaterRefraction { get; set; } = true;
        public float SunShadowDistance { get; set; } = 600.0f;
        public int SunCascadeCount { get; set; } = 4;
        public float SkySaturation_V68 { get; set; } = 1.35f;
        public float ShadowSoftness_V68 { get; set; } = 0.35f;
        public bool CloudsEnabled { get; set; } = true;
        public string CloudFrag { get; set; } = "contrails";
        public bool CloudsFollowWeather { get; set; } = false;
        public bool TimecycleHdr { get; set; } = true;
        public bool WorldRenderOptionsSaved { get; set; } = false;
        public bool WorldScriptIpls { get; set; } = true;
        public int WorldInteriorSets { get; set; } = 1;
        public bool WorldInteriorTimecycle { get; set; } = true;
        public bool WorldEnableMods { get; set; } = false;
        public string WorldDlc { get; set; } = "";
        public string GtaFolder { get; set; } = "";
        public List<string> PropFolders { get; set; } = new List<string>();

        public static readonly (string Id, Keys Default, string Label)[] Actions =
        {
            ("Save",        Keys.Control | Keys.S, "Save"),
            ("Undo",        Keys.Control | Keys.Z, "Undo"),
            ("Redo",        Keys.Control | Keys.Y, "Redo"),
            ("Duplicate",   Keys.Control | Keys.D, "Duplicate selected"),
            ("Delete",      Keys.Delete,           "Delete selected"),
            ("Frame",       Keys.F,                "Frame selection"),
            ("Magnet",      Keys.B,                "Select what is under the mouse and fly to it"),
            ("GizmoSelect", Keys.Q,                "Gizmo: select"),
            ("GizmoMove",   Keys.W,                "Gizmo: move"),
            ("GizmoRotate", Keys.E,                "Gizmo: rotate"),
            ("GizmoScale",  Keys.T,                "Gizmo: scale (world)"),
            ("GizmoSpace",  Keys.G,                "Gizmo: world/local axes"),
            ("ToggleMouseSelect", Keys.C,          "World: toggle mouse selection"),
            ("SelectionMode",     Keys.M,          "World: next selection mode"),
            ("ProjectWindow",     Keys.Control | Keys.U, "Project window"),
            ("ProjectNew",        Keys.Control | Keys.N, "Project: new"),
            ("ProjectOpen",       Keys.Control | Keys.O, "Project: open"),
            ("ProjectSaveAll",    Keys.Control | Keys.Shift | Keys.S, "Project: save all"),
            ("GoToDowntown",      Keys.Home,       "World: back to the tower"),
            ("WalkMode",    Keys.V,                "Toggle walk mode"),
            ("GoToOrigin",  Keys.G,                "Go to selection"),
            ("MoveForward", Keys.W,                "Move forward"),
            ("MoveBack",    Keys.S,                "Move back"),
            ("MoveLeft",    Keys.A,                "Move left"),
            ("MoveRight",   Keys.D,                "Move right"),
            ("MoveUp",      Keys.R,                "Move up"),
            ("MoveDown",    Keys.C,                "Move down"),
            ("SpeedUp",     Keys.X,                "Move faster"),
            ("SpeedDown",   Keys.Z,                "Move slower (in walk mode)"),
            ("Copy",        Keys.Control | Keys.C, "Copy selected light(s)"),
            ("Paste",       Keys.Control | Keys.V, "Paste as new light(s)"),
            ("PasteProps",  Keys.Control | Keys.Shift | Keys.V, "Paste settings onto selection"),
        };

        private static void MigrateBind(AppSettings s, string action, Keys oldDefault, Keys newDefault)
        {
            if (s.Binds.TryGetValue(action, out var cur) &&
                Enum.TryParse<Keys>(cur, out var k) && k == oldDefault)
            {
                s.Binds[action] = newDefault.ToString();
            }
        }

        public static string DetectedLayout { get; private set; } = "QWERTY";

        private static Dictionary<string, Keys> LayoutMovementDefaults()
        {
            var map = new Dictionary<string, Keys>();
            string tag;
            try { tag = System.Windows.Forms.InputLanguage.CurrentInputLanguage.Culture.TwoLetterISOLanguageName; }
            catch { tag = "en"; }

            bool azerty = tag.Equals("fr", StringComparison.OrdinalIgnoreCase);
            DetectedLayout = azerty ? "AZERTY" : "QWERTY";
            if (!azerty) return map;

            map["MoveForward"] = Keys.Z;
            map["MoveLeft"] = Keys.Q;
            map["MoveBack"] = Keys.S;
            map["MoveRight"] = Keys.D;

            map["SpeedDown"] = Keys.W;
            return map;
        }

        public static string LayoutKeyLabel(Keys k) => null;

        public class CinematicPrefs
        {
            [JsonInclude] public int AntiAlias = 3;

            [JsonInclude] public float AoStrength = 0.6f;
            [JsonInclude] public float AoRadius = 0.55f;
            [JsonInclude] public float AoQuality = 0.5f;

            [JsonInclude] public float Bloom = 0.55f;
            [JsonInclude] public float BloomThreshold = 1.05f;
            [JsonInclude] public float BloomSpread = 1.0f;
            [JsonInclude] public float BloomAnamorphic = 0.0f;
            [JsonInclude] public float Halation = 0.0f;

            [JsonInclude] public float Ssr = 0.0f;
            [JsonInclude] public float SsrThickness = 0.35f;
            [JsonInclude] public float SsrDistance = 14.0f;
            [JsonInclude] public float SsrSky = 0.6f;
            [JsonInclude] public float SsrFresnel = 0.65f;
            [JsonInclude] public float SsrBlur = 0.35f;

            [JsonInclude] public float Dof = 0.0f;
            [JsonInclude] public float DofFocus = 6.0f;
            [JsonInclude] public float DofAperture = 0.7f;
            [JsonInclude] public float DofRange = 0.35f;
            [JsonInclude] public float DofMaxRadius = 14.0f;
            [JsonInclude] public float DofBokeh = 1.2f;
            [JsonInclude] public float DofBlades = 0.0f;
            [JsonInclude] public float DofStretch = 1.0f;
            [JsonInclude] public float DofRadial = 0.0f;

            [JsonInclude] public float MotionBlur = 0.0f;
            [JsonInclude] public bool DofAutoFocus = true;

            [JsonInclude] public float Vignette = 0.85f;
            [JsonInclude] public float Grain = 0.0f;
            [JsonInclude] public float ChromAberration = 0.0f;
            [JsonInclude] public float Sharpen = 0.25f;
            [JsonInclude] public float Contrast = 1.0f;
            [JsonInclude] public float Saturation = 1.0f;
            [JsonInclude] public float Temperature = 0.0f;
            [JsonInclude] public float Tint = 0.0f;
            [JsonInclude] public float Letterbox = 0.0f;

            [JsonInclude] public float HalationR = 1.00f;
            [JsonInclude] public float HalationG = 0.32f;
            [JsonInclude] public float HalationB = 0.12f;
            [JsonInclude] public float HalationSpread = 0.4f;
            [JsonInclude] public float HalationThreshold = 0.35f;
            [JsonInclude] public float HalationSaturation = 1.0f;
            [JsonInclude] public float HalationSoftness = 0.5f;

            [JsonInclude] public float GrainSize = 1.5f;
            [JsonInclude] public float GrainColour = 0.25f;
            [JsonInclude] public float GrainShadow = 0.7f;

            [JsonInclude] public float VignetteRoundness = 0.35f;
            [JsonInclude] public float VignetteSoftness = 0.6f;

            [JsonInclude] public float EdgeBlur = 0.0f;
            [JsonInclude] public float EdgeBlurStart = 0.45f;
            [JsonInclude] public float EdgeBlurElongation = 0.6f;

            [JsonInclude] public float Lift = 0.0f;
            [JsonInclude] public float Gain = 1.0f;
            [JsonInclude] public float Bleach = 0.0f;
            [JsonInclude] public float Dither = 0.35f;

            [JsonInclude] public float BevelFlatten = 0.0f;

            [JsonInclude] public int RenderSize = 2;
            [JsonInclude] public int Supersample = 2;
        }

        public CinematicPrefs Cinematic { get; set; } = new CinematicPrefs();

        public static string FileName = "settings.json";

        private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

        public static float PeekUiScale_V17()
        {
            try
            {
                if (!File.Exists(FilePath)) return 0f;
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(FilePath));
                return doc.RootElement.TryGetProperty("UiScaleV17", out var v) && v.TryGetSingle(out float f) ? f : 0f;
            }
            catch { return 0f; }
        }

        public static AppSettings Load()
        {
            AppSettings s = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                }
            }
            catch { }
            s = s ?? new AppSettings();
            s.Cinematic = s.Cinematic ?? new CinematicPrefs();
            MigrateGizmoStyle_J1(s);
            var layout = LayoutMovementDefaults();
            foreach (var (id, def, _) in Actions)
            {
                if (s.Binds.ContainsKey(id)) continue;
                s.Binds[id] = (layout.TryGetValue(id, out var lk) ? lk : def).ToString();
            }
            if (s.Accent != null && s.Accent.Length == 3 &&
                Math.Abs(s.Accent[0] - 0.26f) < 0.001f &&
                Math.Abs(s.Accent[1] - 0.46f) < 0.001f &&
                Math.Abs(s.Accent[2] - 0.75f) < 0.001f)
            {
                s.Accent = new[] { DefaultAccent[0], DefaultAccent[1], DefaultAccent[2] };
            }

            if (s.AccentMaterial != null && s.AccentMaterial.Length == 3 &&
                Math.Abs(s.AccentMaterial[0] - 0.475f) < 0.001f &&
                Math.Abs(s.AccentMaterial[1] - 0.004f) < 0.001f &&
                Math.Abs(s.AccentMaterial[2] - 0.980f) < 0.001f)
            {
                s.AccentMaterial = new[]
                    { DefaultMaterialAccent[0], DefaultMaterialAccent[1], DefaultMaterialAccent[2] };
            }

            if (Math.Abs(s.OrbitAnchorMax - 20.0f) < 0.001f) s.OrbitAnchorMax = 1.5f;

            MigrateBind(s, "GoToOrigin", Keys.Z, Keys.G);
            if ((s.GetBind("MoveForward") & Keys.KeyCode) == Keys.Z)
            {
                MigrateBind(s, "SpeedDown", Keys.Z, Keys.W);
            }

            MigrateBind(s, "GizmoSelect", Keys.D1, Keys.Q);
            MigrateBind(s, "GizmoMove", Keys.D2, Keys.W);
            MigrateBind(s, "GizmoRotate", Keys.D3, Keys.E);

            MigrateBind(s, "Frame", Keys.Home, Keys.F);
            MigrateBind(s, "MoveDown", Keys.F, Keys.C);

            s.LeftPanelWidth = Math.Clamp(s.LeftPanelWidth, 180, 700);
            s.RightPanelWidth = Math.Clamp(s.RightPanelWidth, 260, 800);
            s.RotateSnapDeg = Math.Clamp(s.RotateSnapDeg, 0, 90);
            if (Math.Abs(s.WalkSpeed - 1.0f) < 0.0001f) s.WalkSpeed = 0.25f;
            s.WalkSpeed = Math.Clamp(s.WalkSpeed, 0.02f, 20f);
            s.ThemeIndex = Math.Clamp(s.ThemeIndex, 0, UiTheme.Names.Length - 1);
            if (!s.ThemeDefaulted) { s.ThemeIndex = UiTheme.DefaultTheme; s.ThemeDefaulted = true; }
            if (!s.ThemeGraphiteU22) { s.ThemeIndex = UiTheme.DefaultTheme; s.ThemeGraphiteU22 = true; }
            s.BackfaceMode = Math.Clamp(s.BackfaceMode, 0, 2);
            s.WeatherTransitionSeconds = Math.Clamp(s.WeatherTransitionSeconds, 0f, 30f);
            if (s.Accent == null || s.Accent.Length != 3)
                s.Accent = new[] { DefaultAccent[0], DefaultAccent[1], DefaultAccent[2] };
            if (s.AccentMaterial == null || s.AccentMaterial.Length != 3)
                s.AccentMaterial = new[] { DefaultMaterialAccent[0], DefaultMaterialAccent[1], DefaultMaterialAccent[2] };

            static bool Same(float[] a, float r, float g, float b) =>
                a != null && a.Length == 3 &&
                Math.Abs(a[0] - r) < 0.005f && Math.Abs(a[1] - g) < 0.005f && Math.Abs(a[2] - b) < 0.005f;
            if (Same(s.AccentMaterial, 1.00f, 0.82f, 0.20f) ||
                Same(s.AccentMaterial, 0.96f, 0.65f, 0.11f) ||
                Same(s.AccentMaterial, 0.93f, 0.58f, 0.06f))
            {
                s.AccentMaterial = new[] { DefaultMaterialAccent[0], DefaultMaterialAccent[1], DefaultMaterialAccent[2] };
            }
            s.PropFolders ??= new List<string>();
            if (!s.FovDefaulted)
            {
                if (Math.Abs(s.FovDeg - 54.0f) < 0.001f) s.FovDeg = 85.0f;
                s.FovDefaulted = true;
            }
            s.FovDeg = Math.Clamp(s.FovDeg, Rendering.Camera.MinFovDeg, Rendering.Camera.MaxFovDeg);
            s.CameraSensitivity = Math.Clamp(s.CameraSensitivity, 0.0005f, 0.05f);
            if (!s.CameraSmoothingDefaulted)
            {
                if (Math.Abs(s.CameraSmoothing - 10.0f) < 0.001f ||
                    Math.Abs(s.CameraSmoothing - 25.0f) < 0.001f) s.CameraSmoothing = 0.0f;
                s.CameraSmoothingDefaulted = true;
            }
            s.CameraSmoothing = Math.Clamp(s.CameraSmoothing, 0f, 60f);
            s.OrbitAnchorMax = Math.Clamp(s.OrbitAnchorMax, 0.5f, 500f);
            return s;
        }

        public void ResetToDefaults()
        {
            var keepGta = GtaFolder;
            var keepProps = new List<string>(PropFolders ?? new List<string>());

            var d = new AppSettings();
            Binds.Clear();
            foreach (var (id, def, _) in Actions) Binds[id] = def.ToString();
            LeftPanelWidth = d.LeftPanelWidth;
            RightPanelWidth = d.RightPanelWidth;
            RotateSnapDeg = d.RotateSnapDeg;
            WalkSpeed = d.WalkSpeed;
            ThemeIndex = d.ThemeIndex;
            Accent = new[] { d.Accent[0], d.Accent[1], d.Accent[2] };
            FovDeg = d.FovDeg;
            CameraSensitivity = d.CameraSensitivity;
            CameraSmoothing = d.CameraSmoothing;
            VSync = d.VSync;
            BackfaceCulling = d.BackfaceCulling;
            BackfaceMode = d.BackfaceMode;
            WeatherTransitionSeconds = d.WeatherTransitionSeconds;

            GtaFolder = keepGta;
            PropFolders = keepProps;
            Save();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public Keys GetBind(string action)
        {
            if (Binds.TryGetValue(action, out var s) && Enum.TryParse<Keys>(s, out var k)) return k;
            foreach (var (id, def, _) in Actions) if (id == action) return def;
            return Keys.None;
        }

        public void SetBind(string action, Keys combo)
        {
            Binds[action] = combo.ToString();
            Save();
        }

        public static string KeyLabel(Keys combo)
        {
            var key = combo & Keys.KeyCode;
            var s = "";
            if ((combo & Keys.Control) != 0) s += "Ctrl+";
            if ((combo & Keys.Shift) != 0) s += "Shift+";
            if ((combo & Keys.Alt) != 0) s += "Alt+";
            return s + key;
        }

        public static Keys ComboFrom(KeyEventArgs e)
        {
            var combo = e.KeyCode;
            if (e.Control) combo |= Keys.Control;
            if (e.Shift) combo |= Keys.Shift;
            if (e.Alt) combo |= Keys.Alt;
            return combo;
        }

        public bool ProjectDetached { get; set; }
        public int[] ProjectDetachedBounds { get; set; }
        public bool ProjectDetachedMaximized { get; set; }
        public string ProjectDetachedScreen { get; set; }

        public float GizmoSizePx { get; set; } = 120f;
        public bool GizmoModern { get; set; } = true;
        public int GizmoStyleIndex { get; set; } = -1;
        public bool GizmoCodeWalkerU24 { get; set; }
        public bool MirrorSurprise { get; set; } = true;
    }
}

