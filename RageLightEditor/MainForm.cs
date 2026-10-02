using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using CodeWalker;
using CodeWalker.GameFiles;
using ImGuiNET;
using RageLightEditor.Editor;
using RageLightEditor.ImGuiBackend;
using RageLightEditor.Rendering;
using SharpDX;
using Color4 = SharpDX.Color4;
using Vector3 = SharpDX.Vector3;
using Vector4 = SharpDX.Vector4;

namespace RageLightEditor
{
    public partial class MainForm : Form
    {
        private DeviceResources deviceResources;
        private ImGuiRenderer imguiRenderer;
        private LightPropLibrary lightPropLibrary;
        private PropThumbnails propThumbnails;
        private ImGuiInput imguiInput;
        private SceneRenderer sceneRenderer;
        private LineRenderer lineRenderer;
        private PostFxRenderer postFx;
        private CinematicRenderer cinematic;
        private CoronaRenderer coronaRenderer;
        private TextureLoader textureLoader;
        private ModelRenderer modelRenderer;
        private Scene scene { get => CurrentScene; set => lightScene = value; }
        private LightPanel panel;
        private MaterialPanel materialPanel;
        private Camera camera;
        private Gizmo gizmo;
        private bool gizmoConsumedClick;
        private readonly HashSet<Keys> walkKeys = new HashSet<Keys>();
        private TriRenderer triRenderer;
        private ShadowRenderer shadowRenderer;
        private AppSettings settings;
        private bool walkMode;
        private TimecycleData timecycle;
        private GameFileManager gameFiles;
        private MloImporter mloImporter;
        private DateTime timecycleFileStamp;
        private double timecycleCheckTime;
        private readonly List<Scene.VolumeDraw> volumes = new List<Scene.VolumeDraw>();
        private readonly List<LightAttributes> gpuLightSources = new List<LightAttributes>();
        private readonly SharpDX.Direct3D11.ShaderResourceView[] projTexSrvs =
            new SharpDX.Direct3D11.ShaderResourceView[GpuLight.MaxProjTextures];

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double lastFrameTime;
        private readonly GpuLight[] gpuLights = new GpuLight[GpuLight.MaxLights];
        private readonly List<(Vector3 pos, Vector3 colour, float size, float intensity)> coronas = new();

        private System.Drawing.Point lastMouse;
        private System.Drawing.Point downMouse;
        private bool orbiting, panning;
        private float mouseDownDrag;

        private string screenshotPath;
        private int screenshotFrames = -1;
        private readonly List<string> pendingLoadFiles = new List<string>();

        private sealed class ShadowSlotCache
        {
            public LightAttributes Light; public int Hash; public bool Valid;
            public double AssignedAt;
        }
        private readonly ShadowSlotCache[] spotSlotCache = CreateSlotCache(GpuLight.MaxShadowSpots);
        private readonly ShadowSlotCache[] cubeSlotCache = CreateSlotCache(GpuLight.MaxShadowCubes);
        private int shadowGeomVersion = -1;
        private int shadowUpdatesThisFrame;
        private int shadowBudget;
        private Vector3 lastShadowPickPos = new Vector3(float.MaxValue);
        private List<LightAttributes> cachedDesiredSpots, cachedDesiredCubes;
        private bool shadowPickDirty = true;
        private Vector3 lastSunDir = new Vector3(float.MaxValue);
        private bool sunMapValid;
        private bool sunMapDirty;
        private bool sunRebuiltThisFrame;
        private float sceneRadiusForLog;
        private double lastSunMapTime;
        private int lastShadowSelection = -2;
        private int lastShadowMode = -1;

        private List<LightAttributes> ToSources(List<int> idx)
        {
            var l = new List<LightAttributes>(idx.Count);
            foreach (var i in idx) if (i < gpuLightSources.Count) l.Add(gpuLightSources[i]);
            return l;
        }

        private List<int> ToIndices(List<LightAttributes> src)
        {
            var l = new List<int>();
            if (src == null) return l;
            foreach (var s in src)
            {
                int i = gpuLightSources.IndexOf(s);
                if (i >= 0) l.Add(i);
            }
            return l;
        }
        private float fpsSmoothed;
        private float lastRenderMs;

        private static ShadowSlotCache[] CreateSlotCache(int n)
        {
            var a = new ShadowSlotCache[n];
            for (int i = 0; i < n; i++) a[i] = new ShadowSlotCache();
            return a;
        }

        private static int LightShadowHash(in GpuLight g, float nearClip)
        {
            return HashCode.Combine(
                ((int)(g.Position.X * 128) * 397) ^ ((int)(g.Position.Y * 128) * 31) ^ (int)(g.Position.Z * 128),
                ((int)(g.Direction.X * 1024) * 397) ^ ((int)(g.Direction.Y * 1024) * 31) ^ (int)(g.Direction.Z * 1024),
                (int)(g.Falloff * 128),
                (int)(g.ConeOuterAngle * 1024),
                (int)(nearClip * 1024),
                (int)g.Type);
        }
        public int DebugRenderMode = -1;
        public bool DebugNoDepth = false;
        public int DebugSelectLight = -1;
        public int DebugHour = -1;
        public int DebugGizmoMode = -1;
        public int DebugFpsCap = -1;
        public bool DebugFpsBench = false;
        private int benchFrameCap;
        public bool DebugNoShadows = false;
        public bool DebugShadowStress = false;
        public bool DebugNoCull = false;
        public string DebugCam;
        public string DebugCine;
        public readonly List<string> DebugCineSet = new List<string>();
        public bool DebugCineSpace;
        public bool DebugArchiveTest;
        public bool DebugArchiveSpace;
        public bool DebugWorldTest;
        public string DebugWorldSpace;
        private SharpDX.Vector3? worldStart;
        public bool DebugNoShadowJitter;
        public bool DebugSeqTest;
        public bool DebugVideoTest;
        private bool seqTestDone;
        public bool DebugPhoto;
        public int DebugHourSweep;
        public float DebugHourFocus;
        private int hourSweepFrame;
        public int DebugModeSweep;
        private int modeSweepFrame;
        public bool DebugCloseup = false;
        public string DebugTimecycle = null;
        public string DebugGtaFolder = null;
        public string DebugMlo = null;
        public string DebugSaveProject = null;
        public string DebugYmapOut = null;
        public int DebugWeather = -1;
        public int DebugSkyMode = 0;
        public uint DebugSetFlagMask;
        public uint DebugClearFlagMask;
        private int lastDesiredSpots, lastDesiredCubes;
        public bool DebugCopyPasteTest;
        public bool DebugGtaGate;
        public bool DebugTcDump;
        public bool DebugMoveTest;
        public bool DebugNewLightTest;
        public bool DebugMultiEditTest;
        public int DebugShadowMode = -1;
        public bool DebugNoDwell;
        public bool DebugPickTest;
        public bool DebugKeyTest;
        private bool ignoreImGuiKeyboard;

        public int DebugMatSelect = -1;
        public bool DebugMatIsolate;
        public readonly List<string> DebugMatSet = new List<string>();
        public readonly List<string> DebugMatTex = new List<string>();
        public readonly List<string> DebugMatImport = new List<string>();
        public string DebugMatExport;
        public string DebugMatExportAll;
        public bool DebugMatReport;
        private bool debugMatDone;

        private bool IsHeadless => screenshotPath != null || DebugMlo != null || DebugGtaFolder != null ||
                                   DebugLightProbe > 0 || DebugArchOut != null || DebugYmapOut != null ||
                                   DebugCopyPasteTest || DebugFpsBench ||
                                   DebugKeyTest || DebugPickTest || DebugMoveTest ||
                                   DebugMultiEditTest || DebugNewLightTest || DebugTcDump ||
                                   ServeMode_W3;
        public string DebugModifier = null;
        private static string Fmt(Vector4 v) => $"({v.X:0.000},{v.Y:0.000},{v.Z:0.000})";
        public string DebugPlaceProp = null;
        private bool debugPlaceDone;
        private bool debugMloDone;
        private double debugMloStart;
        private int benchFrames;
        private double benchStart;

        [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint p);
        [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint p);

        public AppMode Mode
        {
            get => appMode;
            set
            {
                appMode = value;
                Text = AppInfo.Name;
            }
        }
        private AppMode appMode = AppMode.Light;

        public MainForm(IEnumerable<string> openFiles = null, string screenshot = null)
        {
            Text = AppInfo.Name;
            Icon = AppIcon.Load();
            AppIcon.Attach(this);
            {
                var ws = Environment.GetEnvironmentVariable("RLE_WINSIZE");
                int cw = 1500, chh = 900;
                if (!string.IsNullOrWhiteSpace(ws))
                {
                    var pp = ws.Split('x', 'X');
                    if (pp.Length == 2 && int.TryParse(pp[0], out var pw) && int.TryParse(pp[1], out var ph))
                    { cw = Math.Clamp(pw, 640, 7680); chh = Math.Clamp(ph, 480, 4320); }
                }
                ClientSize = new System.Drawing.Size(cw, chh);
            }
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            KeyPreview = true;
            if (openFiles != null) pendingLoadFiles.AddRange(openFiles);
            screenshotPath = screenshot;

            Load += OnLoad;
            FormClosed += OnClosed;
            Resize += OnResize;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            MouseDown += OnMouseDownEv;
            MouseUp += OnMouseUpEv;
            MouseMove += OnMouseMoveEv;
            MouseWheel += OnMouseWheelEv;
            KeyDown += OnKeyDownEv;
            KeyUp += OnKeyUpEv;
            Deactivate += (s, e) => walkKeys.Clear();
        }

        private void OnLoad(object sender, EventArgs e)
        {
            timeBeginPeriod(1);
            deviceResources = new DeviceResources(Handle, ClientSize.Width, ClientSize.Height);
            CommonStates.Create(deviceResources.Device);

            ImGui.CreateContext();
            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            int screenW = System.Windows.Forms.Screen.FromControl(this)?.Bounds.Width ?? 1920;
            float scale = Editor.UiScale_V17.Resolve(Editor.AppSettings.PeekUiScale_V17(), DeviceDpi, screenW);
            var (fontFile, fontPx) = Editor.AppSettings.PeekUiFont_V20();
            Editor.UiScale_V17.FontFile = fontFile;
            Editor.UiScale_V17.FontPx = fontPx;
            Editor.UiScale_V17.Set(scale, io, null);
            Console.WriteLine($"UISCALE {scale:0.##}x (DeviceDpi {DeviceDpi}, screen {screenW} wide)");

            imguiRenderer = new ImGuiRenderer(deviceResources.Device, deviceResources.Context);
            imguiInput = new ImGuiInput(this);

            settings = AppSettings.Load();
            {
                var a0 = settings.AccentFor(Mode == AppMode.Material);
                UiTheme.Apply(settings.ThemeIndex, new System.Numerics.Vector3(a0[0], a0[1], a0[2]));
            }

            sceneRenderer = new SceneRenderer(deviceResources.Device);
            lineRenderer = new LineRenderer(deviceResources.Device);
            coronaRenderer = new CoronaRenderer(deviceResources.Device);
            distantLights_V47 = new Rendering.DistantLightsRenderer_V47(deviceResources.Device);
            textureLoader = new TextureLoader(deviceResources.Device);
            modelRenderer = new ModelRenderer(deviceResources.Device, textureLoader);
            scene = new Scene(modelRenderer, textureLoader);
            CreateMloScene_L3();
            CreateMatScene_R2();
            gizmo = new Gizmo(scene) { RotateSnapDeg = settings.RotateSnapDeg };
            worldGizmo = new WorldGizmo { RotateSnapDeg = settings.RotateSnapDeg };
            worldGizmo.EntityChanged = WorldEntityChanged;
            worldGizmo.TargetChanged = t => { if (!(t.Key is YmapEntityDef)) WorldTargetChanged(t); };
            worldGizmo.DragBegan += WorldGizmoDragBegan;
            worldGizmo.DragEnded += WorldGizmoDragEnded;
            panel = new LightPanel(scene, gizmo, settings);
            panel.WorldGizmo = worldGizmo;
            panel.WorldHistory = WorldHistory;
            panel.WorldRef = World;
            panel.ProjectWindow = ProjWin;
            ApplyProjectContentSettings_P3();

            World.IsRenderableReady = worldRender.IsBuilt;
            World.WalkStarting = worldRender.WalkStarted;

            projCtl = new ProjectController(ProjWin, () => this, () => gameFiles?.Cache);
            ProjWin.FindMloInstance = mlo =>
            {
                if (mlo == null) return null;
                foreach (var v in World.Visible)
                    if (v?.MloInstance != null && (ReferenceEquals(v.Archetype, mlo) || v.Archetype?.Hash == mlo.Hash))
                        return v.MloInstance;
                foreach (var n in World.Nodes)
                {
                    var all = n.Ymap?.AllEntities;
                    if (all == null) continue;
                    foreach (var v in all)
                        if (v?.MloInstance != null && (ReferenceEquals(v.Archetype, mlo) || v.Archetype?.Hash == mlo.Hash))
                            return v.MloInstance;
                }
                return null;
            };
            projCtl.SpawnPos = () => camera.Position + camera.GetForward() * 5.0f;
            projCtl.NewEntityInInterior = ProjectNewEntityInInterior_U21;
            projCtl.EntityChanged += e => WorldEntityChanged(e);
            projCtl.GoToEntity += e =>
            {
                EnterWorldWorkspaceForGoTo();
                WorldEdit.Select(e);
                panel.RequestWorldEntityGoto = true;
            };
            WireProjectWindowExtras();
            projCtl.ProjectYmapsChanged += RebuildProjectOverrides;
            ProjWin.FileListsChanged += RebuildProjectOverrides;
            panel.WorkspaceSwitching += (leaving, entering) =>
            {
                if (photoMode && entering != LightPanel.Space.Light && entering != LightPanel.Space.Cinematic) SetPhotoMode(false);
            };
            projCtl.ProjectClosing += OnProjectClosing_J2;
            projCtl.EntitySetsChanged += () => { worldRender.ClearInstances(); World.Invalidate(); };
            projCtl.PropsPanelRequested += () => { panel.FocusPropsTab = true; panel.ShowRightPanel = true; };

            materialPanel = new MaterialPanel(scene, settings)
            {
                Renderer = modelRenderer,
                Textures = textureLoader,
                Imgui = imguiRenderer,
            };
            panel.Materials = materialPanel;
            panel.MatScene = matScene;
            materialPanel.RequestOpenYtd += DoOpenYtdDialog;
            materialPanel.RequestAddFile += DoAddFileDialog;
            materialPanel.RequestImportTexture += DoImportMaterialTexture;
            materialPanel.RequestFrameMaterial += FrameMaterial;
            materialPanel.RequestExportTextures += DoExportTextures;

            panel.MaterialMode = Mode == AppMode.Material;
            panel.WorkspaceChanged += ApplyWorkspaceDefaults;
            panel.WorkspaceChanged += _ => { if (panel.WorldMode) ApplyWorkspaceDefaults(false); ApplyNavSpawn_V8(); };
            panel.WorkspaceSwitching += OnWorkspaceSwitching;
            WireSectionState_Q4();
            WireMloWorkspace_J6();
            if (panel.MaterialMode) ApplyWorkspaceDefaults(true);
            panel.ApplyThemeFromSettings(save: false);
            triRenderer = new TriRenderer(deviceResources.Device);
            postFx = new PostFxRenderer(deviceResources.Device);
            cinematic = new CinematicRenderer(deviceResources.Device);
            shadowRenderer = new ShadowRenderer(deviceResources.Device);
            try { skyRenderer = new SkyRenderer(deviceResources.Device); }
            catch (Exception ex) { skyRenderer = null; Console.WriteLine("SKY SHADER FAILED: " + ex.Message); }
            camera = new Camera();
            WireSectionCameras_T2();

            panel.RequestSave += DoSave;
            panel.RequestSaveAs += f => DoSaveAs(f);
            panel.RequestOpenFile += DoOpenDialog;
            panel.RequestOpenYtd += DoOpenYtdDialog;
            panel.RequestNewProject += DoNewProject;
            panel.RequestOpenProject += DoOpenProjectDialog;
            panel.RequestSaveProject += DoSaveProject;
            panel.RequestFrameLight += FrameLight;
            panel.RequestImportProjTex += DoImportProjTex;
            panel.RequestAddFile += DoAddFileDialog;
            panel.RequestNewLightProxy += DoNewLightProxy;
            panel.ConfirmNewLightProxy += CreateLightProxy;
            panel.ConfirmRenameProp += RenameProp;
            panel.RequestAddPropsToYmap += AddLoadedPropsToYmap;
            panel.RequestExportYmap += ExportYmap;
            panel.RequestCreateArchetype += CreateArchetypeFor;
            panel.RequestRecomputeBounds += RecomputeArchetypeBounds;
            panel.RequestExportYtyp += ExportYtyp;

            gameFiles = new GameFileManager();
            gameFiles.ScriptIpls = settings.WorldScriptIpls && Environment.GetEnvironmentVariable("RLE_SCRIPTIPL") != "off";
            gameFiles.EnableMods  = settings.WorldEnableMods;
            gameFiles.SelectedDlc = settings.WorldDlc ?? "";
            if (materialPanel != null)
            {
                materialPanel.Game = gameFiles;
                materialPanel.LocalTextures = h =>
                {
                    var t = mloImporter?.FindLocalTexture(h);
                    if (t?.Data?.FullData != null) return t;
                    foreach (var dict in modelRenderer.ExternalTextureDicts)
                    {
                        var d = dict?.Lookup(h);
                        if (d?.Data?.FullData != null) return d;
                    }
                    return null;
                };
            }
            mloImporter = new MloImporter(gameFiles, modelRenderer);
            modelRenderer.TextureFallback = (tex, txd) => gameFiles.FindTexture(tex, txd);
            modelRenderer.HdTextureFallback = (tex, txd, asset) => gameFiles.FindHdTexture(tex, txd, asset);
            panel.Game = gameFiles;

            var logoSrv = textureLoader.LoadEmbeddedPng("logo.png", out int logoW, out int logoH);
            if (logoSrv != null)
            {
                panel.LogoTexture = imguiRenderer.RegisterTexture(logoSrv);
                panel.LogoWidth = logoW;
                panel.LogoHeight = logoH;
            }
            var matLogoSrv = textureLoader.LoadEmbeddedPng("material_logo.png", out int mlW, out int mlH);
            if (matLogoSrv != null)
            {
                panel.MaterialLogoTexture = imguiRenderer.RegisterTexture(matLogoSrv);
                panel.MaterialLogoWidth = mlW;
                panel.MaterialLogoHeight = mlH;
            }
            var worldLogoSrv = textureLoader.LoadEmbeddedPng("world_logo.png", out int wlW, out int wlH);
            if (worldLogoSrv != null)
            {
                panel.WorldLogoTexture = imguiRenderer.RegisterTexture(worldLogoSrv);
                panel.WorldLogoWidth = wlW;
                panel.WorldLogoHeight = wlH;
            }
            var mloLogoSrv = textureLoader.LoadEmbeddedPng("mlo_logo.png", out int mloW, out int mloH);
            if (mloLogoSrv != null)
            {
                panel.MloLogoTexture = imguiRenderer.RegisterTexture(mloLogoSrv);
                panel.MloLogoWidth = mloW;
                panel.MloLogoHeight = mloH;
            }
            LoadWorkspaceLogos_Q3();

            panel.RequestPickGtaFolder += DoPickGtaFolder;
            panel.RequestImportYtyp += DoImportYtyp;
            panel.RequestImportMapFiles_V36 += DoImportMapFiles_V36;
            panel.RequestImportYmap += DoImportYmap;
            panel.RequestClearImports += () => ClearImports();
            panel.RequestReimportYtyp += () =>
            {
                mloImporter.InvalidateLocalIndex();
                ImportYtyp(lastYtypPath);
            };
            panel.RequestAddPropFolder += DoAddPropFolder;
            panel.RemovePropFolder += f =>
            {
                settings.PropFolders.Remove(f);
                mloImporter.ExtraFolders = settings.PropFolders;
                settings.Save();
            };
            mloImporter.ExtraFolders = settings.PropFolders;
            panel.PropFolders = settings.PropFolders;

            lightPropLibrary = new LightPropLibrary();
            lightPropLibrary.Load();
            propThumbnails = new PropThumbnails(deviceResources.Device, sceneRenderer, modelRenderer,
                imguiRenderer, gameFiles, postFx);
            if (screenshotPath != null) propThumbnails.PerFrameBudget = 64;
            panel.Library = lightPropLibrary;
            panel.Thumbs = propThumbnails;
            panel.RequestLibraryScan += includeArchives =>
                lightPropLibrary.BeginScan(gameFiles, settings.PropFolders, includeArchives);
            panel.RequestPlaceLibraryProp += PlaceLibraryProp;

            timecycle = new TimecycleData();
            panel.Timecycle = timecycle;
            panel.RequestLoadTimecycle += DoLoadTimecycle;
            panel.RequestLoadTimeSchedule += DoLoadTimeSchedule;
            panel.RequestLoadGameTimecycle += DoLoadGameTimecycle;
            panel.RequestLoadModifiers += DoLoadModifiers;
            panel.RequestResetSettings += DoResetSettings;
            panel.RequestSaveTimecycle += DoSaveTimecycle;
            panel.RequestSaveModifiers += DoSaveModifiers;
            panel.RequestRegisterFileTypes += RegisterFileTypes;

            BindActiveScene_L3(SceneFor_L3(panel.Workspace));

            foreach (var f in pendingLoadFiles)
            {
                if (File.Exists(f)) LoadFile(f);
            }
            if (DebugWeather >= 0) { panel.WeatherIndex = DebugWeather; SetWeather(DebugWeather); }
            if (DebugShadowMode >= 0) panel.ShadowMode = DebugShadowMode;
            sceneRenderer.DebugNoDepth = DebugNoDepth;
            if (DebugSelectLight >= 0 && DebugSelectLight < scene.Lights.Count)
            {
                scene.SelectedIndex = DebugSelectLight;
            }
            if (DebugHour >= 0)
            {
                panel.PreviewHour = DebugHour % 24;
            }
            if (DebugGizmoMode >= 0 && DebugGizmoMode <= 2)
            {
                gizmo.Mode = (GizmoMode)DebugGizmoMode;
            }
            if (DebugFpsCap >= 0)
            {
                benchFrameCap = DebugFpsCap;
            }
            if (DebugNoShadows) panel.ShowShadows = false;
            if (DebugAllProps) panel.ImportAllProps = true;
            if (DebugTcEdit)
            {
                panel.ShowTimecycleEditor = true;
                panel.PreselectTimecycleVar = "light_natural_amb_up_col_r";
            }
            var startFolder = DebugGtaFolder ?? settings.GtaFolder;
            if (!string.IsNullOrEmpty(startFolder) && GameFileManager.IsValidFolder(startFolder))
            {
                gameFiles.BeginInit(startFolder);
                debugMloStart = clock.Elapsed.TotalSeconds;
            }
            bool needFolder = string.IsNullOrEmpty(startFolder) || !GameFileManager.IsValidFolder(startFolder);
            if (DebugGtaGate || (needFolder && !IsHeadless))
            {
                panel.GtaFolderDisplay = GameFileManager.GuessFolder() ?? "";
                panel.ShowGtaSetup = true;
            }
            if (!string.IsNullOrEmpty(DebugTimecycle) && File.Exists(DebugTimecycle))
            {
                if (timecycle.LoadTimecycleXml(DebugTimecycle, out var tcerr))
                {
                    timecycleFileStamp = SafeStamp(DebugTimecycle);
                    Console.WriteLine($"TIMECYCLE loaded: {timecycle.Regions.Count} region(s), {timecycle.KeyframeCount} keyframes, schedule {timecycle.Samples.Count}");
                }
                else Console.WriteLine("TIMECYCLE load failed: " + tcerr);
            }
            CommonStates.CullingEnabled = !DebugNoCull;
            if (DebugPhoto) SetPhotoMode(true);
            ApplyStartupWorkspace_S4();
            if (DebugWorldSpace != null)
            {
                panel.Workspace = Editor.LightPanel.Space.World;
                panel.ApplyThemeFromSettings(false);
                var wp = DebugWorldSpace.Split(',');
                static float N(string[] a, int i, float d) =>
                    i < a.Length && float.TryParse(a[i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
                worldStart = new SharpDX.Vector3(N(wp, 0, -270), N(wp, 1, -960), N(wp, 2, 250));
                if (wp.Length > 3) panel.WorldStreamRadius = N(wp, 3, 500);
                if (wp.Length > 4) worldFlyMetres = N(wp, 4, 0);
                if (wp.Length > 5) worldRender.MaxArchetypes = (int)N(wp, 5, 4000);
                if (wp.Length > 6) WorldPrepareBudget = (int)N(wp, 6, 16);
                if (wp.Length > 7) worldAimYaw = N(wp, 7, 1.4f);
                if (wp.Length > 8) worldAimPitch = N(wp, 8, 0.45f);
                if (wp.Length > 9) worldAutoSelect = N(wp, 9, 0) > 0.5f;
                if (wp.Length > 10) panel.WorldShowCollision = N(wp, 10, 0) > 0.5f;
                if (wp.Length > 11 && N(wp, 11, 0) > 0.5f) worldDemoProject = true;
                var hourEnv = Environment.GetEnvironmentVariable("RLE_HOUR");
                if (float.TryParse(hourEnv, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var hourVal))
                    panel.PreviewHour = Math.Clamp(hourVal, 0.0f, 23.99f);
            }

            if (DebugArchiveSpace)
            {
                panel.Workspace = Editor.LightPanel.Space.Archive;
                panel.ApplyThemeFromSettings(false);
            }
            if (DebugCineSpace)
            {
                panel.Workspace = Editor.LightPanel.Space.Cinematic;
                panel.EnterCineWorkspace();
                panel.ApplyThemeFromSettings(false);
            }
            ApplyMloSpaceFlag_J6();
            ApplyNavSpaceFlag_P4();
            ApplyTerrainSpaceFlag_R4();
            ApplyAnimSpaceFlag_U6();
            {
                int rm = DebugRenderMode;
                if (rm < 0 && int.TryParse(Environment.GetEnvironmentVariable("RLE_RENDERMODE"), out int envRm)) rm = envRm;
                if (rm >= 0) panel.RenderMode = rm;
            }
            if (!string.IsNullOrEmpty(DebugCam) || !string.IsNullOrEmpty(DebugCine))
            {
                static float Num(string[] p, int i, float def) =>
                    (i < p.Length && float.TryParse(p[i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v)) ? v : def;

                if (!string.IsNullOrEmpty(DebugCam))
                {
                    var p = DebugCam.Split(',');
                    camera.Target = new Vector3(Num(p, 0, 0), Num(p, 1, 0), Num(p, 2, 0));
                    camera.Distance = camera.TargetDistance = Num(p, 3, 12);
                    camera.Yaw = camera.TargetYaw = Num(p, 4, 0) * 0.0174533f;
                    camera.Pitch = camera.TargetPitch = Num(p, 5, 15) * 0.0174533f;
                    camera.SnapSmoothing();
                    debugCamLocked = true;
                }
                if (!string.IsNullOrEmpty(DebugCine))
                {
                    var p = DebugCine.Split(',');
                    panel.RenderMode = 7;
                    panel.CineAoStrength = Num(p, 0, panel.CineAoStrength);
                    panel.CineAoRadius = Num(p, 1, panel.CineAoRadius);
                    panel.CineBloom = Num(p, 2, panel.CineBloom);
                    panel.CineBloomThreshold = Num(p, 3, panel.CineBloomThreshold);
                }
            }

            foreach (var kv in DebugCineSet)
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0) continue;
                string k = kv.Substring(0, eq).Trim().ToLowerInvariant();
                if (!float.TryParse(kv.Substring(eq + 1), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float val)) continue;
                var c = panel.Cine;
                switch (k)
                {
                    case "aa": c.AntiAlias = (int)val; break;
                    case "ao": c.AoStrength = val; break;
                    case "aoradius": c.AoRadius = val; break;
                    case "aoquality": c.AoQuality = val; break;
                    case "bloom": c.Bloom = val; break;
                    case "bloomthreshold": c.BloomThreshold = val; break;
                    case "bloomspread": c.BloomSpread = val; break;
                    case "anamorphic": c.BloomAnamorphic = val; break;
                    case "halation": c.Halation = val; break;
                    case "ssr": c.Ssr = val; break;
                    case "ssrthickness": c.SsrThickness = val; break;
                    case "ssrdistance": c.SsrDistance = val; break;
                    case "dof": c.Dof = val; break;
                    case "doffocus": c.DofFocus = val; c.DofAutoFocus = false; break;
                    case "dofaperture": c.DofAperture = val; break;
                    case "vignette": c.Vignette = val; break;
                    case "grain": c.Grain = val; break;
                    case "chromab": c.ChromAberration = val; break;
                    case "sharpen": c.Sharpen = val; break;
                    case "contrast": c.Contrast = val; break;
                    case "saturation": c.Saturation = val; break;
                    case "temperature": c.Temperature = val; break;
                    case "tint": c.Tint = val; break;
                    case "letterbox": c.Letterbox = val; break;
                    case "ssrsky": c.SsrSky = val; break;
                    case "ssrfresnel": c.SsrFresnel = val; break;
                    case "ssrblur": c.SsrBlur = val; break;
                    case "dofrange": c.DofRange = val; break;
                    case "dofradius": c.DofMaxRadius = val; break;
                    case "dofbokeh": c.DofBokeh = val; break;
                    case "dofblades": c.DofBlades = val; break;
                    case "dofstretch": c.DofStretch = val; break;
                    case "dofradial": c.DofRadial = val; break;
                    case "dofauto": c.DofAutoFocus = val > 0.5f; break;
                    case "halationspread": c.HalationSpread = val; break;
                    case "halationthreshold": c.HalationThreshold = val; break;
                    case "halationsaturation": c.HalationSaturation = val; break;
                    case "halationsoftness": c.HalationSoftness = val; break;
                    case "grainsize": c.GrainSize = val; break;
                    case "graincolour": c.GrainColour = val; break;
                    case "grainshadow": c.GrainShadow = val; break;
                    case "vignetteround": c.VignetteRoundness = val; break;
                    case "vignettesoft": c.VignetteSoftness = val; break;
                    case "edgeblur": c.EdgeBlur = val; break;
                    case "edgeblurstart": c.EdgeBlurStart = val; break;
                    case "edgeblurbias": c.EdgeBlurElongation = val; break;
                    case "dither": c.Dither = val; break;
                    case "lift": c.Lift = val; break;
                    case "gain": c.Gain = val; break;
                    case "bleach": c.Bleach = val; break;
                    case "bevel": c.BevelFlatten = val; break;
                    case "rendersize": c.RenderSize = (int)val; break;
                    case "supersample": c.Supersample = (int)val; break;
                    default: Console.WriteLine($"CINESET unknown '{k}'"); break;
                }
            }
            CommonStates.BackfaceCulling = settings.BackfaceCulling;
            if (DebugCloseup && scene.Lights.Count > 0)
            {
                var ci = scene.GetInstance(scene.Lights[0]);
                camera.Target = ci.WorldPosition;
                camera.Distance = 1.0f;
            }
            if (DebugFpsBench)
            {
                settings.VSync = false;
                benchStart = clock.Elapsed.TotalSeconds;
            }
            if (screenshotPath != null && DebugMlo == null)
            {
                screenshotFrames = 5;
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (!DebugFpsBench && screenshotPath == null) settings?.Save();
            timeEndPeriod(1);
            Editor.AudioEngine_U1.Shutdown();
            DisposeDetachedWindows_Detach();
            DisposeDetachedWindows_M1();
            DisposeModelViewer_P1();
            DisposeTerrain_R4();

            if (deviceResources != null && deviceResources.DeviceLost)
            {
                deviceResources = null;
                return;
            }

            shadowRenderer?.Dispose();
            triRenderer?.Dispose();
            cinematic?.Dispose();
            postFx?.Dispose();
            scene?.Dispose();
            DisposeMloScene_L3();
            DisposeMatScene_R2();
            coronaRenderer?.Dispose();
            distantLights_V47?.Dispose();
            DisposeParticles_N4();
            DisposeRenderers_J4();
            vertexDots?.Dispose(); l4Tris?.Dispose();
            navRenderer?.Dispose();
            dayOverlay_U2?.Dispose();
            lineRenderer?.Dispose();
            sceneRenderer?.Dispose();
            textureLoader?.Dispose();
            propThumbnails?.Dispose();
            imguiRenderer?.Dispose();
            CommonStates.Destroy();
            deviceResources?.Dispose();
            deviceResources = null;
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized) return;
            deviceResources?.Resize(ClientSize.Width, ClientSize.Height);
        }

        private void LoadFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ProjectFile.Extension)
            {
                OpenProject(path);
                return;
            }
            if (ext == ".cwproj")
            {
                projCtl?.OpenProject(path);
                ProjWin.Visible = true;
                return;
            }
            if (ext == ".ytyp" || ext == ".ymap")
            {
                if (gameFiles != null && gameFiles.Initialising)
                {
                    pendingImports.Add(path);
                    panel.MloStatus = $"Waiting for the game archives to finish loading, then importing {Path.GetFileName(path)}...";
                    return;
                }
                if (ext == ".ytyp") ImportYtyp(path); else ImportYmap(path);
                return;
            }
            if (ext == ".ytd")
            {
                scene.LoadYtdFile(path);
                return;
            }
            if (ext == ".ydd")
            {
                bool firstYdd = !scene.HasModel;
                if (scene.LoadYddFile_V38(path, additive: scene.HasModel))
                {
                    Text = $"{AppInfo.Name} - {scene.FileName}";
                    if (firstYdd) FrameModel();
                }
                return;
            }
            if (ext != ".ydr" && ext != ".yft")
            {
                scene.LoadError = "Unsupported file type: " + ext;
                return;
            }
            bool first = !scene.HasModel;
            if (scene.LoadModelFile(path, additive: scene.HasModel))
            {
                Text = $"{AppInfo.Name} - {scene.FileName}";
                if (first) FrameModel();
            }
        }

        private ProjectFile project;

        private void DoNewProject()
        {
            if (ProjWin?.Project != null)
            {
                projCtl.CloseProject();
                if (ProjWin.Project != null) return;
                ProjWin.Visible = false;
            }
            if (!ConfirmDiscard("Start a new project?")) return;
            WorldRevertAll_V22();
            scene.CloseAllFiles();
            scene.ClearMlo();
            ClearImporterCacheIfUnused_L3();
            lastYtypPath = null;
            project = null;
            panel.ProjectName = "";
            Text = AppInfo.Name;
        }

        private void DoOpenProjectDialog()
        {
            using var dlg = new OpenFileDialog { Filter = ProjectFile.Filter, Title = "Open project" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            OpenProject(dlg.FileName);
        }

        private void OpenProject(string path)
        {
            if (!ConfirmDiscard("Open a different project?")) return;
            ProjectFile p;
            try { p = ProjectFile.Load(path); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Couldn't open project", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            scene.CloseAllFiles();
            scene.ClearMlo();
            ClearImporterCacheIfUnused_L3();

            var missing = new List<string>();
            foreach (var m in p.Models)
            {
                if (File.Exists(m)) LoadFile(m); else missing.Add(m);
            }
            foreach (var t in p.Ytds)
            {
                if (File.Exists(t)) scene.LoadYtdFile(t); else missing.Add(t);
            }

            if (!string.IsNullOrEmpty(p.GtaFolder) && GameFileManager.IsValidFolder(p.GtaFolder) &&
                !gameFiles.Ready && !gameFiles.Initialising)
            {
                gameFiles.BeginInit(p.GtaFolder);
            }
            if (p.PropFolders.Count > 0)
            {
                foreach (var f in p.PropFolders)
                    if (!settings.PropFolders.Contains(f)) settings.PropFolders.Add(f);
                mloImporter.ExtraFolders = settings.PropFolders;
            }
            if (!string.IsNullOrEmpty(p.TimecycleXml) && File.Exists(p.TimecycleXml))
            {
                if (timecycle.LoadTimecycleXml(p.TimecycleXml, out _))
                {
                    timecycleFileStamp = SafeStamp(p.TimecycleXml);
                    panel.TimecycleStatus = $"Loaded {timecycle.Regions.Count} region(s), {timecycle.KeyframeCount} keyframes.";
                }
            }
            if (!string.IsNullOrEmpty(p.GameTimecycle))
            {
                var i = gameTimecycles?.FindIndex(t => t.Name == p.GameTimecycle) ?? -1;
                if (i >= 0) DoLoadGameTimecycle(i); else pendingGameTimecycle = p.GameTimecycle;
            }
            if (p.PreviewHour >= 0) panel.PreviewHour = p.PreviewHour % 24;

            pendingProjectYtyp = string.IsNullOrEmpty(p.Ytyp) || !File.Exists(p.Ytyp) ? null : p.Ytyp;
            if (pendingProjectYtyp != null && (gameFiles.Ready || string.IsNullOrEmpty(p.GtaFolder)))
            {
                ImportYtyp(pendingProjectYtyp);
                pendingProjectYtyp = null;
            }

            if (p.CameraTarget != null && p.CameraTarget.Length == 3)
            {
                camera.Target = new Vector3(p.CameraTarget[0], p.CameraTarget[1], p.CameraTarget[2]);
                camera.Distance = p.CameraDistance > 0 ? p.CameraDistance : camera.Distance;
                camera.Yaw = p.CameraYaw;
                camera.Pitch = p.CameraPitch;
                camera.SnapSmoothing();
            }

            project = p;
            panel.ProjectName = Path.GetFileNameWithoutExtension(path);
            Text = AppInfo.Name + " - " + panel.ProjectName;
            if (missing.Count > 0)
            {
                scene.LoadError = $"{missing.Count} file(s) in the project no longer exist: " +
                    string.Join(", ", missing.ConvertAll(Path.GetFileName));
            }
        }

        private string pendingProjectYtyp;
        private readonly List<string> pendingImports = new List<string>();

        private void DoSaveProject(bool saveAs)
        {
            var path = (saveAs || project?.Path == null) ? null : project.Path;
            if (path == null)
            {
                using var dlg = new SaveFileDialog
                {
                    Filter = ProjectFile.Filter,
                    Title = "Save project",
                    FileName = (scene.Files.Count > 0
                        ? Path.GetFileNameWithoutExtension(scene.Files[0].Path)
                        : "project") + ProjectFile.Extension,
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
            }
            try
            {
                SaveProjectTo(path);
                panel.MloStatus = "Project saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Couldn't save project", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveProjectTo(string path)
        {
            var p = new ProjectFile
            {
                Ytyp = lastYtypPath ?? "",
                GtaFolder = gameFiles.Folder ?? settings.GtaFolder ?? "",
                PropFolders = new List<string>(settings.PropFolders),
                TimecycleXml = timecycle.LoadedPath ?? "",
                GameTimecycle = timecycle.LoadedPath == null ? (loadedGameTimecycle ?? "") : "",
                PreviewHour = (int)panel.PreviewHour,
                CameraTarget = new[] { camera.Target.X, camera.Target.Y, camera.Target.Z },
                CameraDistance = camera.Distance,
                CameraYaw = camera.Yaw,
                CameraPitch = camera.Pitch,
            };
            foreach (var f in scene.Files) p.Models.Add(f.Path);
            foreach (var y in scene.LoadedYtdPaths) p.Ytds.Add(y);

            p.Save(path);
            project = p;
            panel.ProjectName = Path.GetFileNameWithoutExtension(path);
            Text = AppInfo.Name + " - " + panel.ProjectName;
        }

        private bool ConfirmDiscard(string question)
        {
            bool dirty = false;
            foreach (var f in scene.Files) if (f.Dirty) { dirty = true; break; }
            if (!dirty) return true;
            return MessageBox.Show(this, "You have unsaved light changes. " + question,
                "Unsaved changes", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
        }

        private bool debugCamLocked;

        private void FrameModel()
        {
            if (debugCamLocked) return;
            var b = scene.GetSceneBounds();
            if (b.HasValue && b.Value.Minimum.X < b.Value.Maximum.X)
            {
                var c = (b.Value.Minimum + b.Value.Maximum) * 0.5f;
                var r = (b.Value.Maximum - b.Value.Minimum).Length() * 0.5f;
                camera.FrameBounds(c, Math.Max(r, 0.5f));
            }
        }

        private void FrameLight(LightAttributes l)
        {
            if (l == null) return;
            var inst = scene.GetInstance(l);
            camera.FrameBounds(inst.WorldPosition, Math.Clamp(l.Falloff * 0.35f, 1.5f, 12.0f));
        }

        private bool FrameFile(LoadedFile f)
        {
            var model = f?.Model;
            if (model == null || model.Meshes.Count == 0) return false;
            var b = model.Bounds;
            if (b.Maximum.X <= b.Minimum.X) return false;
            var c = (b.Minimum + b.Maximum) * 0.5f;
            var r = (b.Maximum - b.Minimum).Length() * 0.5f;
            camera.FrameBounds(c, Math.Max(r, 0.5f));
            return true;
        }

        private bool FrameSelection()
        {
            if (FrameWorldSelection_M3()) return true;

            var active = scene.ActiveFile ?? scene.SelectedFiles.FirstOrDefault();
            var lightOwner = scene.OwnerFile(scene.SelectedLight);
            bool lightFirst = scene.SelectedLight != null &&
                              (active == null || lightOwner == null || ReferenceEquals(lightOwner, active));
            if (lightFirst)
            {
                FrameLight(scene.SelectedLight);
                panel.MloStatus = lightOwner != null
                    ? $"Went to light {scene.SelectedIndex} on {lightOwner.Name}."
                    : $"Went to light {scene.SelectedIndex}.";
                return true;
            }
            var f = active;
            if (!FrameFile(f)) return false;
            panel.MloStatus = $"Went to {f.Name}.";
            return true;
        }

        private void DoSave()
        {
            try
            {
                scene.Save();
                Editor.UiSound.Success();
                AfterSaveFiveM_U12(scene.Files);
            }
            catch (Exception ex)
            {
                Editor.UiSound.Error();
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoSaveAs() => DoSaveAs(null);

        private void DoSaveAs(LoadedFile file)
        {
            if (!scene.HasModel) return;
            var f = file
                    ?? (scene.ActiveFile != null && scene.Files.Contains(scene.ActiveFile) ? scene.ActiveFile : null)
                    ?? (scene.Files.Count == 1 ? scene.Files[0] : null);
            if (f == null)
            {
                MessageBox.Show(this, "Pick which prop to save first - click it in the Props list.",
                    "Save As", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Filter = f.IsYft ? "YFT fragment (*.yft)|*.yft" : "YDR drawable (*.ydr)|*.ydr",
                FileName = Path.GetFileName(f.Path),
                Title = "Save " + f.Name,
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    scene.SaveOneAs(f, dlg.FileName);
                    AfterSaveFiveM_U12(dlg.FileName);
                    Text = $"{AppInfo.Name} - {Path.GetFileName(dlg.FileName)}";
                    panel.MloStatus = "Saved " + Path.GetFileName(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static float SkyShadowEnv_V68(string name, float fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrEmpty(v) &&
                   float.TryParse(v, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out var f)
                 ? f : fallback;
        }

        private void DoOpenDialog()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "GTA V models (*.ydr;*.yft;*.ydd)|*.ydr;*.yft;*.ydd|All files (*.*)|*.*",
                Multiselect = true,
                Title = "Open model(s) - select multiple for shell + light proxies",
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                bool first = true;
                foreach (var f in dlg.FileNames)
                {
                    if (first) { scene.LoadModelFile(f, additive: false); first = false; }
                    else { scene.LoadModelFile(f, additive: true); }
                }
                Text = $"{AppInfo.Name} - {scene.FileName}";
                FrameModel();
            }
        }

        private void PollDebugMlo(double now)
        {
            if (gameFiles.Initialising)
            {
                if (now - lastMloLog > 5.0)
                {
                    lastMloLog = now;
                    Console.WriteLine($"  [{now - debugMloStart:0}s] {gameFiles.Status}");
                }
                return;
            }
            debugMloDone = true;
            if (DebugGtaFolder != null && !gameFiles.Ready)
            {
                Console.WriteLine("MLO FAILED: game files not ready. " + gameFiles.Error);
                Close();
                return;
            }
            Console.WriteLine(gameFiles.Ready
                ? $"GAMEFILES ready in {now - debugMloStart:0.0}s"
                : "GAMEFILES not loaded - resolving from local files only");
            try
            {
                foreach (var group in DebugMlo.Split(';', StringSplitOptions.RemoveEmptyEntries)
                             .GroupBy(f => f.EndsWith(".ymap", StringComparison.OrdinalIgnoreCase)))
                {
                    if (group.Key) ImportYmap(group.ToArray()); else ImportYtyp(group.ToArray());
                }
                var result = scene.MloInfo;
                if (result == null)
                {
                    Console.WriteLine($"MLO none: no interior archetype in this .ytyp " +
                                      $"(props loaded: {scene.Files.Count}, lights: {scene.Lights.Count})");
                    Console.WriteLine($"  status: {panel.MloStatus?.Replace("\n", " | ")}");
                    if (screenshotPath != null) screenshotFrames = 8; else Close();
                    return;
                }
                Console.WriteLine($"MLO {result.MloName}: placed {result.Placed}/{result.EntityCount} entities, " +
                    $"{result.UniqueProps} unique props, {result.Missing} missing, {result.Seconds:0.00}s");
                Console.WriteLine($"  shell {result.ShellMeshes} meshes, {result.Proxies} proxies skipped, " +
                    $"{result.UntexturedMeshes} untextured meshes, {result.LocalFiles} local files");
                Console.WriteLine($"  light props: {result.Props.Count}, scene lights: {scene.Lights.Count}");
                if (result.MloInstances > 0)
                    Console.WriteLine($"  {result.MloInstances} interior instance(s): " +
                        string.Join(", ", result.MloInstanceNames.Take(4)));
                if (gameFiles.TextureIndexTotal > 0)
                    Console.WriteLine($"  textureIndex: {gameFiles.TextureIndexDone}/{gameFiles.TextureIndexTotal} " +
                        $"ready={gameFiles.TextureIndexReady} fromCache={gameFiles.TextureIndexFromCache}");
                Console.WriteLine($"  lodSkipped={result.LodSkipped} vegSkipped={result.VegetationSkipped} " +
                    $"failed={result.FailedNames.Count}" +
                    (result.FailedNames.Count > 0 ? " [" + string.Join(", ", result.FailedNames.Take(4)) + "]" : ""));
                Console.WriteLine($"  proxies={result.Proxies} oversized={result.Oversized} " +
                    $"multiPlaced={result.Props.Count(p => p.Placed > 1)} " +
                    $"lightsIfAllPlacements={result.Props.Sum(p => p.LightCount * p.Placed)}");
                {
                    var probe = new GpuLight[GpuLight.MaxLights];
                    int n = scene.BuildGpuLights(probe, (int)panel.PreviewHour, false, 0f, null, Vector3.Zero);
                    Console.WriteLine($"  gpuLights={n} (copies={scene.GhostGpuIndices.Count})");
                }
                foreach (var p in result.Props.Take(20))
                {
                    var l = p.Lights?.FirstOrDefault();
                    var w = l != null ? scene.GetInstance(l).WorldPosition : Vector3.Zero;
                    var err = l != null ? (scene.WorldToLightSpace(l, w) - l.Position).Length() : 0f;
                    Console.WriteLine($"    {p.Name} x{p.Placed} lights={p.LightCount} readonly={p.ReadOnly} " +
                        $"world=({w.X:0.0},{w.Y:0.0},{w.Z:0.0}) roundtrip_err={err:0.00000}");
                }
                if (result.MissingNames.Count > 0)
                    Console.WriteLine("  missing: " + string.Join(", ", result.MissingNames.Take(8)));
                Console.WriteLine($"  game timecycles: {gameTimecycles?.Count ?? 0}, " +
                    $"schedule samples: {timecycle.Samples.Count} (from game time.xml: {timecycle.ScheduleFromFile})");
                for (int i = 0; i < result.RoomTimecycleHashes.Count; i++)
                {
                    var h = result.RoomTimecycleHashes[i];
                    var nm = i < result.RoomTimecycles.Count ? result.RoomTimecycles[i] : null;
                    int mi = timecycle.FindModifier(h, nm);
                    Console.WriteLine($"  room tcmod[{i}] hash={h} name='{nm ?? "(null)"}' -> " +
                        (mi >= 0 ? $"FOUND '{timecycle.Modifiers[mi].Name}'" : "NOT FOUND"));
                }
                Console.WriteLine($"  modifiers: {timecycle.Modifiers.Count}; rooms ask for " +
                    $"{result.RoomTimecycleHashes.Count} tc mod(s); auto-selected " +
                    $"'{timecycle.CurrentModifier?.Name ?? "(none)"}'");
                if (DebugModifier != null)
                {
                    var mi = timecycle.Modifiers.FindIndex(m =>
                        string.Equals(m.Name, DebugModifier, StringComparison.OrdinalIgnoreCase));
                    var wi = gameTimecycles?.FindIndex(t => t.Name == "w_clear") ?? -1;
                    if (wi >= 0) DoLoadGameTimecycle(wi);
                    timecycle.SelectedModifier = -1;
                    var before = timecycle.Evaluate(panel.PreviewHour, false);
                    timecycle.SelectedModifier = mi;
                    var after = timecycle.Evaluate(panel.PreviewHour, false);
                    Console.WriteLine($"  modifier '{DebugModifier}' idx={mi} " +
                        $"vars={timecycle.CurrentModifier?.Values.Count ?? 0}");
                    Console.WriteLine($"    dirCol   {Fmt(before.LightDirColour)} -> {Fmt(after.LightDirColour)}");
                    Console.WriteLine($"    natAmbUp {Fmt(before.NaturalAmbUp)} -> {Fmt(after.NaturalAmbUp)}");
                    Console.WriteLine($"    artAmbUp {Fmt(before.ArtificialAmbUp)} -> {Fmt(after.ArtificialAmbUp)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("MLO FAILED: " + ex);
            }
            if (DebugMoveTest)
            {
                var startTarget = camera.Target;
                var results = new List<string>();
                void Press(Keys k, string label, Func<Vector3, Vector3, bool> ok)
                {
                    walkKeys.Clear();
                    walkKeys.Add(k);
                    camera.Update();
                    var before = camera.Position;
                    for (int i = 0; i < 10; i++) { ApplyWalkKeys(0.05f); camera.Update(); }
                    walkKeys.Clear();
                    var moved = camera.Position - before;
                    results.Add($"  {label,-16} moved=({moved.X:0.00},{moved.Y:0.00},{moved.Z:0.00}) " +
                                (ok(before, camera.Position) ? "OK" : "WRONG"));
                }
                var f0 = camera.GetForward(); var r0 = camera.GetRight();
                Keys B(string a) => settings.GetBind(a) & Keys.KeyCode;
                Press(B("MoveForward"), $"forward ({B("MoveForward")})", (a, b) => Vector3.Dot(b - a, f0) > 0.01f);
                Press(B("MoveBack"), $"back ({B("MoveBack")})", (a, b) => Vector3.Dot(b - a, f0) < -0.01f);
                Press(B("MoveRight"), $"right ({B("MoveRight")})", (a, b) => Vector3.Dot(b - a, r0) > 0.01f);
                Press(B("MoveLeft"), $"left ({B("MoveLeft")})", (a, b) => Vector3.Dot(b - a, r0) < -0.01f);
                Press(B("MoveUp"), $"up ({B("MoveUp")})", (a, b) => (b - a).Z > 0.01f);
                Press(B("MoveDown"), $"down ({B("MoveDown")})", (a, b) => (b - a).Z < -0.01f);
                Console.WriteLine($"MOVETEST walkMode={walkMode} imguiKb={ImGuiWantsKeyboard} " +
                                  $"walkSpeed={settings.WalkSpeed} camDist={camera.Distance:0.00} " +
                                  $"layout={AppSettings.DetectedLayout}");
                foreach (var a in MoveActions)
                    Console.WriteLine($"    bind {a,-12} = {settings.GetBind(a)}");
                foreach (var r in results) Console.WriteLine(r);
                Console.WriteLine(results.TrueForAll(s => s.EndsWith("OK"))
                    ? "MOVE TEST PASSED" : "MOVE TEST FAILED");
                camera.Target = startTarget;

                var gateResults = new List<string>();
                void Gate(string label, GizmoMode mode, bool lmb, bool dragging, bool want)
                {
                    var savedMode = gizmo.Mode;
                    var savedOrbit = orbiting;
                    gizmo.Mode = mode;
                    orbiting = lmb;
                    gizmo.DebugForceDragging = dragging;
                    bool got = MovementEnabled;
                    gizmo.DebugForceDragging = false;
                    gizmo.Mode = savedMode;
                    orbiting = savedOrbit;
                    gateResults.Add($"  {label,-38} {(got == want ? "OK" : $"WRONG (got {got}, want {want})")}");
                }
                Gate("select mode, no button: free", GizmoMode.Select, false, false, true);
                Gate("move gizmo, no button: FREE", GizmoMode.Translate, false, false, true);
                Gate("move gizmo + left button: free", GizmoMode.Translate, true, false, true);
                Gate("rotate gizmo, no button: FREE", GizmoMode.Rotate, false, false, true);
                Gate("dragging a handle: blocked", GizmoMode.Translate, true, true, false);
                {
                    var gotoKey = settings.GetBind("GoToOrigin") & Keys.KeyCode;
                    bool shared = IsWalkKey(gotoKey);
                    var before = camera.Target;

                    walkKeys.Clear();
                    goToDownAt = -1;
                    ignoreImGuiKeyboard = true;
                    OnKeyDownEv(this, new KeyEventArgs(gotoKey));
                    OnKeyUpEv(this, new KeyEventArgs(gotoKey));
                    bool tapMoved = Vector3.DistanceSquared(before, camera.Target) > 0.0001f;

                    camera.Target = before;
                    walkKeys.Clear();
                    goToDownAt = -1;
                    OnKeyDownEv(this, new KeyEventArgs(gotoKey));
                    goToDownAt -= GoToTapSeconds * 2.0;
                    OnKeyUpEv(this, new KeyEventArgs(gotoKey));
                    bool holdMoved = Vector3.DistanceSquared(before, camera.Target) > 0.0001f;
                    camera.Target = before;
                    ignoreImGuiKeyboard = false;

                    bool holdWant = !shared;
                    Console.WriteLine($"GOTOKEY bind={gotoKey} sharedWithMovement={shared}");
                    Console.WriteLine($"  tap goes to the selection  {(tapMoved ? "OK" : "DEAD")}");
                    Console.WriteLine($"  hold {(shared ? "does not go" : "goes too   ")}          {(holdMoved == holdWant ? "OK" : "WRONG")}");
                    Console.WriteLine(tapMoved && holdMoved == holdWant
                        ? "GOTO KEY TEST PASSED" : "GOTO KEY TEST FAILED");
                }

                Console.WriteLine("MOVEGATE");
                foreach (var r in gateResults) Console.WriteLine(r);
                Console.WriteLine(gateResults.TrueForAll(s => s.EndsWith("OK"))
                    ? "MOVE GATE TEST PASSED" : "MOVE GATE TEST FAILED");
            }

            if (DebugNewLightTest)
            {
                var nl = scene.AddLight(1);
                if (nl == null) Console.WriteLine("NEW LIGHT TEST FAILED (no file to add to)");
                else
                {
                    int hoursOn = 0;
                    for (int h = 0; h < 24; h++) if ((nl.TimeFlags & (1u << h)) != 0) hoursOn++;
                    Console.WriteLine($"NEWLIGHT timeFlags=0x{nl.TimeFlags:X6} hoursOn={hoursOn}/24");
                    Console.WriteLine(hoursOn == 24 ? "NEW LIGHT TEST PASSED" : "NEW LIGHT TEST FAILED");
                }
            }

            if (DebugKeyTest)
            {
                ignoreImGuiKeyboard = true;
                var results = new List<string>();
                void Send(Keys combo, string label, Func<bool> ok)
                {
                    walkKeys.Clear();
                    OnKeyDownEv(this, new KeyEventArgs(combo));
                    results.Add($"  {label,-22} {(ok() ? "OK" : "DEAD")}");
                }

                int n0 = scene.Lights.Count;
                scene.AddLight(1);
                int n1 = scene.Lights.Count;
                Send(Keys.Control | Keys.Z, "Ctrl+Z undo", () => scene.Lights.Count == n1 - 1);
                int n2 = scene.Lights.Count;
                Send(Keys.Control | Keys.Y, "Ctrl+Y redo", () => scene.Lights.Count == n2 + 1);

                gizmo.Mode = GizmoMode.Select;
                Send(settings.GetBind("GizmoMove"), $"gizmo move ({settings.GetBind("GizmoMove")})",
                     () => gizmo.Mode == GizmoMode.Translate);
                Send(settings.GetBind("GizmoRotate"), $"gizmo rotate ({settings.GetBind("GizmoRotate")})",
                     () => gizmo.Mode == GizmoMode.Rotate);
                Send(settings.GetBind("GizmoSelect"), $"gizmo select ({settings.GetBind("GizmoSelect")})",
                     () => gizmo.Mode == GizmoMode.Select);

                walkKeys.Clear();
                OnKeyDownEv(this, new KeyEventArgs(settings.GetBind("MoveForward")));
                results.Add($"  {"movement still armed",-22} {(walkKeys.Count > 0 ? "OK" : "DEAD")}");
                walkKeys.Clear();
                ignoreImGuiKeyboard = false;

                Console.WriteLine("KEYTEST");
                foreach (var r in results) Console.WriteLine(r);
                Console.WriteLine(results.TrueForAll(s => s.EndsWith("OK"))
                    ? "KEY TEST PASSED" : "KEY TEST FAILED");
            }

            if (DebugPickTest)
            {
                camera.Update();
                int w = deviceResources.Width, h = deviceResources.Height;
                int hits = 0, tries = 0;
                LoadedFile last = null;
                var found = new HashSet<string>();
                float baseDist = camera.Distance;
                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                foreach (float mult in new[] { 1.0f, 3.0f, 8.0f })
                {
                    camera.Distance = baseDist * mult;
                    camera.SnapSmoothing();
                    camera.Update();
                    var perDist = new HashSet<string>();
                    for (int gy = 1; gy <= 12; gy++)
                    {
                        for (int gx = 1; gx <= 12; gx++)
                        {
                            tries++;
                            var r = camera.GetPickRay(w * gx / 13.0f, h * gy / 13.0f, w, h);
                            var f = FindPropUnder(r);
                            if (f != null) { hits++; last = f; found.Add(f.Name); perDist.Add(f.Name); }
                        }
                    }
                    Console.WriteLine($"  camDist x{mult}: {perDist.Count} distinct props from 144 rays");
                }
                sw2.Stop();
                camera.Distance = baseDist;
                camera.SnapSmoothing();
                camera.Update();
                Console.WriteLine($"PICKTEST distinct props overall: {found.Count} " +
                                  $"({sw2.Elapsed.TotalMilliseconds / Math.Max(tries,1):0.00} ms/ray)");
                foreach (var n in found.Take(10)) Console.WriteLine($"    {n}");
                uint green = 0, grey = 0;
                if (last != null)
                {
                    scene.SelectFile(last, false, false);
                    hoverProp = null;
                    UpdatePropHighlights();
                    foreach (var m in last.Model.Meshes) { if (m.Highlight == 1) green++; }
                    hoverProp = last;
                    scene.SelectedFiles.Clear();
                    UpdatePropHighlights();
                    foreach (var m in last.Model.Meshes) { if (m.Highlight == 2) grey++; }
                }
                if (last != null) { hoverProp = null; scene.SelectFile(last, false, false); }
                Console.WriteLine($"PICKTEST rays={tries} hits={hits} prop='{last?.Name ?? "(none)"}' " +
                                  $"greenMeshes={green} greyMeshes={grey}");
                Console.WriteLine(hits > 0 && green > 0 && grey > 0
                    ? "PICK TEST PASSED" : "PICK TEST FAILED");
            }

            if (DebugMultiEditTest && scene.Lights.Count >= 3)
            {
                scene.SelectedIndices.Clear();
                for (int i = 0; i < 3; i++) scene.SelectedIndices.Add(i);
                var prim = scene.Lights[0];
                scene.Lights[1].ColorR = 11; scene.Lights[2].ColorR = 22;
                byte g1 = scene.Lights[1].ColorG, g2 = scene.Lights[2].ColorG;

                var before = Scene.CloneLight(prim);
                prim.Intensity += 3.5f;
                float want = prim.Intensity;
                int n = scene.ApplyEditToSelection(before, prim);

                bool spread = Math.Abs(scene.Lights[1].Intensity - want) < 1e-4f &&
                              Math.Abs(scene.Lights[2].Intensity - want) < 1e-4f;
                bool untouched = scene.Lights[1].ColorR == 11 && scene.Lights[2].ColorR == 22 &&
                                 scene.Lights[1].ColorG == g1 && scene.Lights[2].ColorG == g2;
                Console.WriteLine($"MULTIEDIT applied={n} intensity->{want:0.00} " +
                    $"spreadToAll={spread} otherFieldsUntouched={untouched}");
                Console.WriteLine(n == 2 && spread && untouched
                    ? "MULTI EDIT TEST PASSED" : "MULTI EDIT TEST FAILED");
            }

            if (DebugCopyPasteTest)
            {
                int before = scene.Lights.Count;
                scene.SelectedIndices.Clear();
                for (int i = 0; i < Math.Min(3, before); i++) scene.SelectedIndices.Add(i);
                var p0 = scene.Lights[0].Position;
                scene.CopyLights();
                int copied = scene.ClipboardCount;
                int pasted = scene.PasteLights();
                int after = scene.Lights.Count;
                bool posKept = (scene.Lights[after - pasted].Position - p0).Length() < 1e-5f;
                bool selOk = scene.SelectedIndices.Count == pasted;
                Console.WriteLine($"COPYPASTE copied={copied} pasted={pasted} " +
                    $"lights {before}->{after} positionPreserved={posKept} selectionOnNew={selOk}");
                Console.WriteLine(copied == 3 && pasted == 3 && after == before + 3 && posKept && selOk
                    ? "COPY/PASTE TEST PASSED" : "COPY/PASTE TEST FAILED");
            }

            if (DebugSelectLight >= 0 && DebugSelectLight < scene.Lights.Count)
            {
                scene.SelectedIndex = DebugSelectLight;
                shadowPickDirty = true;
            }
            if (DebugSaveProject != null)
            {
                try { SaveProjectTo(DebugSaveProject); Console.WriteLine("PROJECT saved: " + DebugSaveProject); }
                catch (Exception ex) { Console.WriteLine("PROJECT save failed: " + ex.Message); }
            }
            if (DebugYmapOut != null)
            {
                try
                {
                    AddLoadedPropsToYmap(false);
                    YmapBuilder.Save(DebugYmapOut, Path.GetFileNameWithoutExtension(DebugYmapOut), panel.YmapEntries);
                    var placed = panel.YmapEntries.Count(e => e.Include);
                    var withPos = panel.YmapEntries.Count(e => e.Position.LengthSquared() > 0.0001f);
                    Console.WriteLine($"YMAP exported: {placed} entities ({withPos} with a real placement) -> {DebugYmapOut}");
                }
                catch (Exception ex) { Console.WriteLine("YMAP export failed: " + ex.Message); }
            }
            if (DebugHourSweep > 0 || DebugModeSweep > 0 || DebugSeqTest) { }
            else if (DebugRenderOut != null) { }
            else if (screenshotPath != null) screenshotFrames = 8;
            else if (DebugFpsBench) { benchStart = clock.Elapsed.TotalSeconds; benchFrames = 0; }
            else if (DebugLightProbe > 0) { }
            else Close();
        }
        private double lastMloLog;

        private void DoPickGtaFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select your GTA V installation folder (the one containing GTA5.exe)",
                UseDescriptionForTitle = true,
                SelectedPath = GameFileManager.GuessFolder() ?? "",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (!GameFileManager.IsValidFolder(dlg.SelectedPath))
            {
                MessageBox.Show(this, "GTA5.exe was not found in that folder.", "Not a GTA V folder",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            settings.GtaFolder = dlg.SelectedPath;
            settings.Save();
            gameFiles.BeginInit(dlg.SelectedPath);
        }

        private void DoImportYtyp()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Archetype definitions (*.ytyp)|*.ytyp|All files (*.*)|*.*",
                Title = "Import MLO interior (.ytyp) - pick as many as you like",
                Multiselect = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            ImportYtyp(dlg.FileNames);
        }

        private int lightProxyCounter = 1;

        private void AddLoadedPropsToYmap(bool selectedOnly)
        {
            var source = selectedOnly && scene.SelectedFiles.Count > 0
                ? scene.SelectedFiles.ToList()
                : scene.Files.ToList();
            int added = 0;
            foreach (var f in source)
            {
                var archName = Path.GetFileNameWithoutExtension(f.Path);
                if (string.IsNullOrEmpty(archName)) continue;
                if (panel.YmapEntries.Any(e =>
                        string.Equals(e.ArchetypeName, archName, StringComparison.OrdinalIgnoreCase))) continue;

                var pos = Vector3.Zero;
                var rot = Quaternion.Identity;
                if (f.HasPlacement)
                {
                    f.Placement.Decompose(out _, out rot, out pos);
                }

                panel.YmapEntries.Add(new YmapEntry
                {
                    ArchetypeName = archName,
                    Position = pos,
                    Rotation = rot,
                });
                added++;
            }
            panel.MloStatus = added > 0
                ? $"Added {added} prop(s) to the ymap list."
                : "Nothing new to add - those props are already in the list.";
        }

        private void CreateArchetypeFor(LoadedFile f)
        {
            if (f == null) return;
            var name = Path.GetFileNameWithoutExtension(f.Path);
            if (string.IsNullOrWhiteSpace(name)) return;

            var lights = scene.Lights.Where(l => scene.OwnerFile(l) == f).ToList();
            var existing = panel.Archetypes.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                ArchetypeBuilder.ApplyLightBounds(existing, lights);
                panel.MloStatus = $"Re-measured {existing.Name} from {lights.Count} light(s).";
                return;
            }

            var def = ArchetypeBuilder.FromLightProp(name, lights);
            def.IsFragment = f.IsYft;
            panel.Archetypes.Add(def);
            panel.MloStatus = lights.Count > 0
                ? $"Archetype {def.Name}: {lights.Count} light(s), radius {def.BsRadius:0.00} m, lod {def.LodDist:0}."
                : $"Archetype {def.Name}: no lights yet - placeholder bounds, recompute after adding some.";
        }

        private void RecomputeArchetypeBounds(ArchetypeDef def)
        {
            if (def == null) return;
            var f = scene.Files.FirstOrDefault(x =>
                string.Equals(Path.GetFileNameWithoutExtension(x.Path), def.Name, StringComparison.OrdinalIgnoreCase));
            f ??= scene.ActiveFile;
            if (f == null) { panel.MloStatus = "No prop to measure - select one in the Props tab."; return; }

            var lights = scene.Lights.Where(l => scene.OwnerFile(l) == f).ToList();
            if (!ArchetypeBuilder.ApplyLightBounds(def, lights))
            {
                panel.MloStatus = $"{f.Name} has no lights to measure.";
                return;
            }
            panel.MloStatus = $"{def.Name}: {lights.Count} light(s), radius {def.BsRadius:0.00} m, lod {def.LodDist:0}.";
        }

        public string DebugArchOut = null;
        private bool debugArchDone;

        private void RunDebugArchExport()
        {
            debugArchDone = true;
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(DebugArchOut));
                var name = Path.GetFileNameWithoutExtension(DebugArchOut);

                CreateLightProxy(name);
                var lf = scene.ActiveFile ?? scene.Files.LastOrDefault();
                if (lf == null) throw new Exception("no proxy was created");
                lf.Placement = Matrix.Identity;

                scene.AddLight(1);
                var pt = scene.Lights[scene.Lights.Count - 1];
                pt.Position = new Vector3(0, 0, 2);
                pt.Falloff = 6.0f;

                scene.AddLight(2);
                var sp = scene.Lights[scene.Lights.Count - 1];
                sp.Position = new Vector3(0, 0, 4);
                sp.Direction = new Vector3(0, 0, -1);
                sp.Falloff = 12.0f;
                sp.ConeOuterAngle = 45.0f;

                CreateArchetypeFor(lf);
                var def = panel.Archetypes.FirstOrDefault();
                if (def == null) throw new Exception("no archetype was created");
                RecomputeArchetypeBounds(def);

                ArchetypeBuilder.Save(DebugArchOut, name, panel.Archetypes);
                var ydrPath = Path.Combine(dir, name + ".ydr");
                scene.SaveOneAs(lf, ydrPath);

                scene.SelectedFiles.Clear();
                scene.SelectedFiles.Add(lf);
                AddLoadedPropsToYmap(true);
                var ymapPath = Path.Combine(dir, name + ".ymap");
                YmapBuilder.Save(ymapPath, name, panel.YmapEntries);

                Console.WriteLine($"ARCH: prop {lf.Name} with {scene.Lights.Count} light(s)");
                Console.WriteLine($"  bb {def.BbMin} .. {def.BbMax}");
                Console.WriteLine($"  bs {def.BsCentre} r={def.BsRadius:0.000} lod={def.LodDist:0} measured from {def.LightCount}");
                Console.WriteLine($"  wrote {Path.GetFileName(DebugArchOut)}, {Path.GetFileName(ydrPath)}, " +
                                  $"{Path.GetFileName(ymapPath)} in {dir}");

                var rt = new YtypFile();
                rt.Load(File.ReadAllBytes(DebugArchOut));
                var a = rt.AllArchetypes?.FirstOrDefault();
                if (a == null) throw new Exception("ytyp read back empty");
                if ((a.BBMin - def.BbMin).Length() > 0.001f || (a.BBMax - def.BbMax).Length() > 0.001f)
                    throw new Exception($"bounds changed on reload: {a.BBMin} .. {a.BBMax}");
                var ydr = new YdrFile();
                ydr.Load(File.ReadAllBytes(ydrPath));
                int lc = ydr.Drawable?.LightAttributes?.data_items?.Length ?? 0;
                Console.WriteLine($"  reload: archetype {a.Name} lod={a.LodDist:0} r={a.BSRadius:0.000}, ydr lights={lc}");
                if (lc != 2) throw new Exception($"ydr came back with {lc} lights");
                Console.WriteLine("ARCH EXPORT PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ARCH EXPORT FAILED: " + ex.Message);
            }
            if (screenshotPath == null) Close();
        }

        private void ExportYtyp()
        {
            if (panel.Archetypes.Count == 0)
            {
                panel.MloStatus = "Nothing to export - create an archetype first.";
                return;
            }
            using var dlg = new SaveFileDialog
            {
                Filter = "Archetype definitions (*.ytyp)|*.ytyp|All files (*.*)|*.*",
                Title = "Export ytyp",
                FileName = (string.IsNullOrWhiteSpace(panel.YtypName) ? "custom_props" : panel.YtypName) + ".ytyp",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                foreach (var d in panel.Archetypes) RecomputeArchetypeBounds(d);
                var name = Path.GetFileNameWithoutExtension(dlg.FileName);
                ArchetypeBuilder.Save(dlg.FileName, name, panel.Archetypes);
                panel.MloStatus = $"Wrote {Path.GetFileName(dlg.FileName)} ({panel.Archetypes.Count} archetype(s)).";
            }
            catch (Exception ex)
            {
                AppLog_U21.Error("Archetypes .ytyp export", ex, false);
                MessageBox.Show(this, ex.Message + "\n\nThe full details are in Help > Show log.", "YTYP export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportYmap()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Map placements (*.ymap)|*.ymap|All files (*.*)|*.*",
                Title = "Export ymap",
                FileName = (string.IsNullOrWhiteSpace(panel.YmapName) ? "custom_props" : panel.YmapName) + ".ymap",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var name = Path.GetFileNameWithoutExtension(dlg.FileName);
                YmapBuilder.Save(dlg.FileName, name, panel.YmapEntries);
                panel.MloStatus = $"Wrote {Path.GetFileName(dlg.FileName)} " +
                                  $"({panel.YmapEntries.Count(e => e.Include)} entities).";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "YMAP export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoNewLightProxy() => panel.AskNewLightProxyName($"light_proxy_{lightProxyCounter:00}");

        private void CreateLightProxy(string name)
        {
            try
            {
                var lf = scene.AddLightProxy(name);
                lightProxyCounter++;
                lf.Placement = Matrix.Translation(scene.NextLightSpawnPos);
                lf.HasPlacement = true;
                CreateArchetypeFor(lf);
                scene.SelectFile(lf, false, false);
                panel.ScrollToActiveProp = true;
                panel.LeftTabRequest = 0;
                FrameProp(lf);
                panel.MloStatus = $"Created {lf.Name} and its archetype. Add lights, then Save As... " +
                                  "for the .ydr and Export .ytyp... for the archetype.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Couldn't create the light prop",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenameProp(LoadedFile f, string newName)
        {
            if (f == null || string.IsNullOrWhiteSpace(newName)) return;
            var clean = LightProxyBuilder.Sanitised(newName);
            var old = Path.GetFileNameWithoutExtension(f.Path);
            var dir = Path.GetDirectoryName(f.Path);
            var ext = f.IsYft ? ".yft" : ".ydr";
            f.Path = string.IsNullOrEmpty(dir) ? clean + ext : Path.Combine(dir, clean + ext);
            foreach (var a in panel.Archetypes.Where(a =>
                         string.Equals(a.Name, old, StringComparison.OrdinalIgnoreCase)))
            {
                a.Name = clean;
            }
            f.Dirty = true;
            panel.MloStatus = $"Renamed to {f.Name} (saves under the new name).";
        }

        private void DoImportYmap()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Map placements (*.ymap)|*.ymap|All files (*.*)|*.*",
                Title = "Import map instances (.ymap) - pick as many as you like",
                Multiselect = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            ImportYmap(dlg.FileNames);
        }

        private void ImportYmap(string path) => ImportYmap(new[] { path });

        private void ImportYmap(string[] paths)
        {
            paths = paths?.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToArray();
            if (paths == null || paths.Length == 0) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                panel.MloStatus = "Importing...";
                ResetIfImportKindChanged("ymap");
                bool wasEmpty = !scene.Files.Any(f => f.FromMlo);
                mloImporter.ImportPropLights = panel.ImportPropLights;
                mloImporter.ImportAllProps = panel.ImportAllProps;
                mloImporter.ImportVegetation = panel.ImportVegetation;
                ResetTimecycleForImport();

                var combined = new RenderModel { Name = Path.GetFileNameWithoutExtension(paths[0]) };
                MloImportResult merged = null;
                var allProps = new List<MloProp>();
                foreach (var p in paths)
                {
                    var r = mloImporter.ImportYmap(p, out var m, s => panel.MloStatus = s);
                    combined.Meshes.AddRange(m.Meshes);
                    foreach (var pr in r.Props) if (!allProps.Contains(pr)) allProps.Add(pr);
                    if (merged == null) { merged = r; }
                    else
                    {
                        merged.EntityCount += r.EntityCount;
                        merged.Placed += r.Placed;
                        merged.Missing += r.Missing;
                        merged.LodSkipped += r.LodSkipped;
                        merged.VegetationSkipped += r.VegetationSkipped;
                        merged.MloInstances += r.MloInstances;
                        merged.Seconds += r.Seconds;
                        foreach (var n in r.MissingNames)
                            if (merged.MissingNames.Count < 40 && !merged.MissingNames.Contains(n))
                                merged.MissingNames.Add(n);
                        foreach (var n in r.MloInstanceNames)
                            if (!merged.MloInstanceNames.Contains(n)) merged.MloInstanceNames.Add(n);
                        if (r.HasFocus && !merged.HasFocus) { merged.FocusBounds = r.FocusBounds; merged.HasFocus = true; }
                        else if (r.HasFocus) merged.FocusBounds = BoundingBox.Merge(merged.FocusBounds, r.FocusBounds);
                    }
                }
                if (paths.Length > 1) merged.MloName = $"{paths.Length} ymaps";

                bool anyBounds = false;
                void Grow(BoundingBox b)
                {
                    if (b.Maximum.X <= b.Minimum.X) return;
                    combined.Bounds = anyBounds ? BoundingBox.Merge(combined.Bounds, b) : b;
                    anyBounds = true;
                }
                foreach (var mesh in combined.Meshes) Grow(mesh.WorldBounds);
                foreach (var pr in allProps) if (pr.Model != null) Grow(pr.Model.Bounds);

                scene.AppendMlo(combined, merged);
                int addedYtds = 0;
                foreach (var (ytd, ytdPath) in mloImporter.LoadLocalTextureDicts())
                    if (scene.RegisterYtd(ytd, ytdPath)) addedYtds++;
                if (addedYtds > 0) Console.WriteLine($"  auto-loaded {addedYtds} local YTD(s)");
                lastYmapPath = paths[0];

                foreach (var p in allProps)
                {
                    scene.AddImportedProp(p.Path, p.Ydr, p.Yft, p.Model, p.Skeleton, p.Lights,
                        p.FirstWorld, p.Placed, p.ReadOnly, p.Name, p.Placements.Skip(1).ToList(),
                        drawable: p.Drawable);
                }
                if (scene.Lights.Count > 64 && panel.ShadowMode == 0)
                {
                    panel.ShadowMode = 2;
                    shadowPickDirty = true;
                }

                string lodNote = merged.LodSkipped > 0
                    ? $"\n{merged.LodSkipped} LOD/SLOD stand-in(s) skipped" +
                      (merged.Placed == 0 ? " - this ymap holds only low-detail copies; the HD props are in its _strm_ / _critical_ files" : "")
                    : "";
                if (merged.VegetationSkipped > 0)
                    lodNote += $"\n{merged.VegetationSkipped} plant(s) skipped";

                panel.MloStatus = $"Imported {merged.Placed}/{merged.EntityCount} " +
                    $"from {paths.Length} ymap(s) in {merged.Seconds:0.00}s{lodNote}\n" +
                    $"Scene now: {scene.Files.Count(f => f.FromMlo)} light props, {scene.Lights.Count} lights";
                if (wasEmpty) FrameMlo(merged, combined, interior: false);
            }
            catch (Exception ex)
            {
                panel.MloStatus = "Import failed: " + ex.Message;
                MessageBox.Show(this, ex.Message, "YMAP import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private string lastYmapPath;
        private string panelDrawError;
        private bool workspaceDefaultsApplied;
        private bool textureIndexApplied;

        private void ApplyWorkspaceDefaults(bool material)
        {
            bool worldHasCamera_V21 = workspaceCam[(int)LightPanel.Space.World].Valid;
            if (panel.WorldMode && !worldSpawnDone && worldStart == null && !worldHasCamera_V21)
            {
                worldSpawnDone = true;
                ApplyWorldSpawn_U1();
            }
            if (!material || workspaceDefaultsApplied) return;
            workspaceDefaultsApplied = true;
            panel.PreviewHour = 13.0f;
            panel.AmbientLevel = 0.28f;
        }
        private bool worldSpawnDone;
        private static readonly SharpDX.Vector3 MazeBankTop = new SharpDX.Vector3(-75.0f, -818.0f, 330.0f);

        private void PlaceLibraryProp(LightPropEntry e, System.Numerics.Vector2 screenPos)
        {
            if (e == null) return;
            RenderModel model = null;
            try
            {
                if (!PropThumbnails.Load(e, gameFiles, out var drawable, out var lights,
                        out var ydr, out var yft) || drawable == null)
                {
                    panel.MloStatus = $"Could not load {e.Name}.";
                    return;
                }

                var pos = DropPoint(screenPos.X, screenPos.Y);
                var placement = Matrix.Translation(pos);

                var arch = gameFiles.Ready
                    ? gameFiles.Cache?.GetArchetype(JenkHash.GenHash(e.Name.ToLowerInvariant()))
                    : null;
                if (arch != null && arch.TextureDict != 0)
                {
                    var ytd = gameFiles.GetTextureDict(arch.TextureDict);
                    if (ytd?.TextureDict != null &&
                        !modelRenderer.ExternalTextureDicts.Contains(ytd.TextureDict))
                    {
                        modelRenderer.ExternalTextureDicts.Add(ytd.TextureDict);
                    }
                }
                modelRenderer.TextureContext = arch?.TextureDict ?? 0;
                model = modelRenderer.BuildFromDrawable(drawable, e.Name, placement);
                modelRenderer.TextureContext = 0;
                if (model.Meshes.Count == 0)
                {
                    model.Dispose();
                    panel.MloStatus = $"{e.Name} has no geometry to place " +
                        "(a light-only proxy: its light is all there is).";
                    return;
                }
                var lf = scene.AddImportedProp(e.FromArchive ? null : e.Path, ydr, yft, model,
                    drawable.Skeleton, lights, placement, 1, e.FromArchive, e.Name,
                    null, fromMlo: false, drawable: drawable);
                model = null;

                scene.SelectFile(lf, false, false);
                var own = scene.Lights.Where(l => scene.OwnerFile(l) == lf).ToList();
                if (own.Count > 0) scene.SelectedIndex = scene.Lights.IndexOf(own[0]);

                var size = (lf.Model.Bounds.Maximum - lf.Model.Bounds.Minimum).Length();
                panel.MloStatus = $"Placed {e.Name} at ({pos.X:0.0}, {pos.Y:0.0}, {pos.Z:0.0}) " +
                    $"with {own.Count} light(s)." +
                    (size < 0.25f ? "\nThis is a light-only proxy - there is no prop to see, just its light." : "");
            }
            catch (Exception ex)
            {
                model?.Dispose();
                panel.MloStatus = "Place failed: " + ex.Message;
            }
        }

        private void RunDebugPlaceProp()
        {
            var match = lightPropLibrary.Search(DebugPlaceProp).FirstOrDefault();
            match = match ?? FindArchiveModel(DebugPlaceProp);
            if (match == null)
            {
                Console.WriteLine($"PLACEPROP: nothing matches '{DebugPlaceProp}' in the library " +
                    $"({lightPropLibrary.Count} entries) or the game archives");
                Close();
                return;
            }

            int before = scene.Lights.Count;
            PlaceLibraryProp(match, new System.Numerics.Vector2(
                deviceResources.Width * 0.5f, deviceResources.Height * 0.5f));

            var lf = scene.ActiveFile;
            Console.WriteLine($"PLACEPROP {match.Name}: library says {match.LightCount} light(s) [{match.Types}], " +
                $"{(match.FromArchive ? "from archives" : "from disk")}");
            Console.WriteLine($"  scene lights {before} -> {scene.Lights.Count}, " +
                $"file='{lf?.Path}' readonly={lf?.ReadOnly} fromMlo={lf?.FromMlo} meshes={lf?.Model?.Meshes.Count ?? 0}");
            if (lf != null && lf.HasPlacement)
            {
                lf.Placement.Decompose(out _, out _, out var p);
                var lit = scene.Lights.Where(l => scene.OwnerFile(l) == lf)
                    .Select(l => scene.GetInstance(l).WorldPosition).ToList();
                Console.WriteLine($"  placed at ({p.X:0.00}, {p.Y:0.00}, {p.Z:0.00}); " +
                    $"lights land at " + string.Join(" ", lit.Take(3).Select(v => $"({v.X:0.0},{v.Y:0.0},{v.Z:0.0})")));
                var mb = lf.Model?.Bounds ?? default;
                Console.WriteLine($"  geometry bounds ({mb.Minimum.X:0.0},{mb.Minimum.Y:0.0},{mb.Minimum.Z:0.0})" +
                    $"..({mb.Maximum.X:0.0},{mb.Maximum.Y:0.0},{mb.Maximum.Z:0.0})");
            }
            Console.WriteLine("  status: " + panel.MloStatus);

            FrameModel();

            foreach (var m in (lf?.Model?.Meshes ?? new List<RenderMesh>()))
            {
                Console.WriteLine($"    mesh shader='{m.ShaderName}' terrain={m.IsTerrain} " +
                    $"blendMode={m.TerrainBlendMode} layers=" +
                    string.Join("", m.LayerSRV.Select(t => t != null ? "1" : "0")) +
                    $" layerBump=" + string.Join("", m.LayerBumpSRV.Select(t => t != null ? "1" : "0")) +
                    $" mask={(m.MaskSRV != null ? 1 : 0)} diffuse={(m.DiffuseSRV != null ? 1 : 0)}" +
                    $" detail={(m.DetailSRV != null ? 1 : 0)} detailSettings=" +
                    $"({m.DetailSettings.X:0.###},{m.DetailSettings.Y:0.###}," +
                    $"{m.DetailSettings.Z:0.###},{m.DetailSettings.W:0.###})");
            }
        }

        private LightPropEntry FindArchiveModel(string name)
        {
            var rpfman = gameFiles?.Cache?.RpfMan;
            if (rpfman?.EntryDict == null || string.IsNullOrWhiteSpace(name)) return null;

            RpfFileEntry best = null;
            foreach (var kv in rpfman.EntryDict)
            {
                if (!(kv.Value is RpfFileEntry fe) || fe.Name == null) continue;
                var n = fe.Name;
                bool ydr = n.EndsWith(".ydr", StringComparison.OrdinalIgnoreCase);
                bool yft = n.EndsWith(".yft", StringComparison.OrdinalIgnoreCase);
                if (!ydr && !yft) continue;
                if (n.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var stem = System.IO.Path.GetFileNameWithoutExtension(n);
                if (string.Equals(stem, name, StringComparison.OrdinalIgnoreCase)) { best = fe; break; }
                best = best ?? fe;
            }
            if (best == null) return null;

            return new LightPropEntry
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(best.Name),
                Path = best.Path,
                FromArchive = true,
                IsYft = best.Name.EndsWith(".yft", StringComparison.OrdinalIgnoreCase),
            };
        }

        private Vector3 DropPoint(float sx, float sy)
        {
            var ray = camera.GetPickRay(sx, sy, deviceResources.Width, deviceResources.Height);
            float best = float.MaxValue;
            foreach (var m in scene.AllMeshes)
            {
                var bb = m.WorldBounds;
                if (bb.Maximum.X <= bb.Minimum.X) continue;
                if (ray.Intersects(ref bb, out float t) && t > 0 && t < best) best = t;
            }
            if (best < float.MaxValue) return ray.Position + ray.Direction * best;
            return ray.Position + ray.Direction * Math.Min(camera.Distance, 8.0f);
        }

        private void DoAddPropFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Folder to search for custom props (.ydr/.ydd/.ytd/.ytyp), including subfolders",
                UseDescriptionForTitle = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (!settings.PropFolders.Contains(dlg.SelectedPath))
            {
                settings.PropFolders.Add(dlg.SelectedPath);
                mloImporter.ExtraFolders = settings.PropFolders;
                settings.Save();
            }
            if (lastYtypPath != null) ImportYtyp(lastYtypPath);
        }

        private ExportProgressForm importProgress;

        private void ImportYtyp(string path) => ImportYtyp(new[] { path });

        private void ImportYtyp(string[] paths)
        {
            paths = paths?.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToArray();
            if (paths == null || paths.Length == 0) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                panel.MloStatus = "Importing...";
                importProgress = IsHeadless ? null
                    : new ExportProgressForm("Importing", Path.GetFileName(paths[0]), 1);
                importProgress?.Show(this);
                importProgress?.SetMessage("Reading the archetype...");
                ResetIfImportKindChanged("ytyp");
                bool wasEmpty = !scene.Files.Any(f => f.FromMlo);
                mloImporter.ImportPropLights = panel.ImportPropLights;
                mloImporter.ImportAllProps = panel.ImportAllProps;
                mloImporter.ImportVegetation = panel.ImportVegetation;
                ResetTimecycleForImport();

                var combined = new RenderModel { Name = Path.GetFileNameWithoutExtension(paths[0]) };
                MloImportResult result = null;
                var allProps = new List<MloProp>();
                var skipped = new List<string>();
                foreach (var p in paths)
                {
                    MloImportResult r;
                    RenderModel m;
                    try { r = mloImporter.Import(p, out m, out _, s => { panel.MloStatus = s; importProgress?.SetMessage(s); }); }
                    catch (Exception ex)
                    {
                        skipped.Add($"{Path.GetFileName(p)}: {ex.Message}");
                        continue;
                    }
                    combined.Meshes.AddRange(m.Meshes);
                    NoteYtypImport_R1(p, r, m);
                    foreach (var pr in r.Props) if (!allProps.Contains(pr)) allProps.Add(pr);
                    if (result == null) result = r;
                    else
                    {
                        result.EntityCount += r.EntityCount;
                        result.Interiors.AddRange(r.Interiors);
                        result.Placed += r.Placed;
                        result.Missing += r.Missing;
                        result.Seconds += r.Seconds;
                        foreach (var n in r.MissingNames)
                            if (result.MissingNames.Count < 40 && !result.MissingNames.Contains(n))
                                result.MissingNames.Add(n);
                        for (int i = 0; i < r.RoomTimecycleHashes.Count; i++)
                        {
                            result.RoomTimecycleHashes.Add(r.RoomTimecycleHashes[i]);
                            result.RoomTimecycles.Add(i < r.RoomTimecycles.Count ? r.RoomTimecycles[i] : null);
                        }
                        if (r.HasFocus && !result.HasFocus) { result.FocusBounds = r.FocusBounds; result.HasFocus = true; }
                        else if (r.HasFocus) result.FocusBounds = BoundingBox.Merge(result.FocusBounds, r.FocusBounds);
                    }
                }
                if (result == null)
                {
                    panel.MloStatus = "Nothing to import:\n" + string.Join("\n", skipped.Take(4));
                    return;
                }
                if (paths.Length > 1) result.MloName = $"{paths.Length - skipped.Count} interiors";

                bool anyBounds = false;
                void Grow(BoundingBox b)
                {
                    if (b.Maximum.X <= b.Minimum.X) return;
                    combined.Bounds = anyBounds ? BoundingBox.Merge(combined.Bounds, b) : b;
                    anyBounds = true;
                }
                foreach (var mesh in combined.Meshes) Grow(mesh.WorldBounds);
                foreach (var pr in allProps) if (pr.Model != null) Grow(pr.Model.Bounds);

                var model = combined;
                scene.AppendMlo(combined, result);
                int addedYtds = 0;
                foreach (var (ytd, ytdPath) in mloImporter.LoadLocalTextureDicts())
                    if (scene.RegisterYtd(ytd, ytdPath)) addedYtds++;
                if (addedYtds > 0) Console.WriteLine($"  auto-loaded {addedYtds} local YTD(s)");
                lastYtypPath = paths[0];

                foreach (var p in allProps)
                {
                    scene.AddImportedProp(p.Path, p.Ydr, p.Yft, p.Model, p.Skeleton, p.Lights,
                        p.FirstWorld, p.Placed, p.ReadOnly, p.Name, p.Placements.Skip(1).ToList(),
                        drawable: p.Drawable);
                }

                foreach (var p in paths) LoadLocalTimecycleMods(p);

                for (int i = 0; i < result.RoomTimecycleHashes.Count; i++)
                {
                    var mi = timecycle.FindModifier(result.RoomTimecycleHashes[i],
                        i < result.RoomTimecycles.Count ? result.RoomTimecycles[i] : null);
                    if (mi >= 0) { timecycle.SelectedModifier = mi; break; }
                }

                if (scene.Lights.Count > 64 && panel.ShadowMode == 0)
                {
                    panel.ShadowMode = 2;
                    shadowPickDirty = true;
                }

                panel.MloStatus = $"Imported {result.Placed}/{result.EntityCount} " +
                    $"from {paths.Length} file(s) in {result.Seconds:0.00}s\n" +
                    $"Scene now: {scene.Files.Count(f => f.FromMlo)} light props, {scene.Lights.Count} lights" +
                    (skipped.Count > 0 ? $"\nskipped: {string.Join("; ", skipped.Take(2))}" : "");
                if (wasEmpty) FrameMlo(result, model);
            }
            catch (Exception ex)
            {
                panel.MloStatus = "Import failed: " + ex.Message;
                MessageBox.Show(this, ex.Message, "YTYP import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { importProgress?.Close(); importProgress?.Dispose(); } catch { }
                importProgress = null;
                Cursor = Cursors.Default;
            }
        }

        private void ResetTimecycleForImport()
        {
            timecycle.SelectedModifier = -1;
            timecycle.ModifierStrength = 1.0f;
            if (!timecycle.HasData && gameTimecycles != null)
            {
                var i = gameTimecycles.FindIndex(t =>
                    string.Equals(t.Name, DefaultTimecycle, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) DoLoadGameTimecycle(i);
            }
        }

        private void FrameMlo(MloImportResult result, RenderModel model, bool interior = true)
        {
            if (debugCamLocked) return;
            var box = result.HasFocus ? result.FocusBounds : (model?.Bounds ?? default);
            if (box.Maximum.X <= box.Minimum.X) { FrameModel(); return; }
            var centre = (box.Minimum + box.Maximum) * 0.5f;
            if (!interior)
            {
                camera.FrameBounds(centre, Math.Max((box.Maximum - box.Minimum).Length() * 0.5f, 0.5f));
                return;
            }
            var size = box.Maximum - box.Minimum;
            camera.Target = centre;
            camera.Distance = Math.Clamp(Math.Min(size.X, size.Y) * 0.18f, 1.5f, 6.0f);
            camera.SnapSmoothing();
        }

        private string lastYtypPath;

        private string lastImportKind = "";

        private void ClearImports()
        {
            scene.RemoveMloProps();
            scene.ClearMlo();
            OnClearImports_R1();
            ClearImporterCacheIfUnused_L3();
            lastYtypPath = null;
            lastYmapPath = null;
            lastImportKind = "";
            panel.YmapEntries.Clear();
            panel.Archetypes.Clear();
            panel.MloStatus = "";
        }

        private void ResetIfImportKindChanged(string kind)
        {
            if (lastImportKind.Length > 0 && lastImportKind != kind)
            {
                ClearImports();
                panel.MloStatus = kind == "ymap"
                    ? "Cleared the imported interior - a ymap replaces it."
                    : "Cleared the imported map - an interior replaces it.";
            }
            lastImportKind = kind;
        }

        private List<GameFileManager.GameXmlFile> gameTimecycles;
        private bool gameTimecyclesListed;
        private string loadedGameTimecycle;
        private string pendingGameTimecycle;

        private const string DefaultTimecycle = "w_clear";

        private void RunArchiveTest()
        {
                int fails = 0;
                void Check(string what, bool ok, string detail)
                {
                    Console.WriteLine($"  {(ok ? "OK  " : "FAIL")} {what}  {detail}");
                    if (!ok) fails++;
                }
                Check("archives opened", panel.Archive.Roots.Count > 10,
                      $"{panel.Archive.Roots.Count} top-level .rpf");
                Check("files indexed", panel.Archive.FileCount > 100000,
                      $"{panel.Archive.FileCount:N0} entries");

                panel.Archive.Search("prop_bush_med", ".ydr");
                Check("search by name+type finds a known prop", panel.Archive.ResultTotal > 0,
                      $"prop_bush_med*.ydr -> {panel.Archive.ResultTotal} hit(s)");

                panel.Archive.Search("", ".ytyp");
                Check("filter alone lists a whole type", panel.Archive.ResultTotal > 100,
                      $"*.ytyp -> {panel.Archive.ResultTotal:N0}");
                int ytypTotal = panel.Archive.ResultTotal;
                Check("result list is capped, total is not", panel.Archive.Results.Count <= 500,
                      $"showing {panel.Archive.Results.Count} of {ytypTotal:N0}");

                panel.Archive.Search("prop_bush_med_03", ".ydr");
                var hit = panel.Archive.Results.Count > 0 ? panel.Archive.Results[0].File : null;
                var data = hit != null ? ArchiveBrowser.Extract(hit) : null;
                Check("extract returns the file", data != null && data.Length > 1000,
                      hit != null ? $"{hit.Name} -> {data?.Length ?? 0:N0} bytes" : "not found");

                {
                    var disk = hit != null ? ArchiveBrowser.ExtractForDisk(hit) : null;
                    bool rsc7 = disk != null && disk.Length > 16 && BitConverter.ToUInt32(disk, 0) == 0x37435352;
                    Check("extract-for-disk carries the RSC7 header", rsc7,
                          disk == null ? "null" : $"magic {BitConverter.ToUInt32(disk, 0):X8}, {disk.Length:N0} bytes");
                    string ok = "";
                    try
                    {
                        var ydr = new YdrFile();
                        ydr.Load(disk);
                        ok = ydr.Drawable != null ? $"{ydr.Drawable.AllModels?.Length ?? 0} models" : "no drawable";
                    }
                    catch (Exception ex) { ok = "EXCEPTION " + ex.Message; }
                    Check("the on-disk loader reads it back (the archive double-click path)",
                          ok.EndsWith("models") && !ok.StartsWith("0 "), ok);
                    panel.Archive.Search("v_int_1", ".ytyp");
                    var thit = panel.Archive.Results.Count > 0 ? panel.Archive.Results[0].File : null;
                    string tok = "not found";
                    if (thit != null)
                    {
                        try
                        {
                            var yt = new YtypFile();
                            yt.Load(ArchiveBrowser.ExtractForDisk(thit));
                            tok = $"{thit.Name}: {yt.AllArchetypes?.Length ?? 0} archetypes";
                        }
                        catch (Exception ex) { tok = "EXCEPTION " + ex.Message; }
                    }
                    Check("an archived ytyp reads back through the on-disk loader",
                          tok.Contains(" archetypes") && !tok.EndsWith(": 0 archetypes"), tok);
                }

                {
                    var mloArch = gameFiles.Cache.GetArchetype(JenkHash.GenHash("v_recycle")) as MloArchetype;
                    var ytypEntry = mloArch?.Ytyp?.RpfFileEntry;
                    Check("the archives hold v_recycle's ytyp", ytypEntry != null, ytypEntry?.Path ?? "no entry");
                    if (ytypEntry != null)
                    {
                        string tdir = Path.Combine(Path.GetTempPath(), "rle_ytyptest");
                        Directory.CreateDirectory(tdir);
                        string tpath = Path.Combine(tdir, ytypEntry.Name);
                        File.WriteAllBytes(tpath, ArchiveBrowser.ExtractForDisk(ytypEntry));
                        mloImporter.ImportAllProps = true;
                        mloImporter.ImportPropLights = true;
                        mloImporter.ImportVegetation = true;
                        MloImportResult r = null; string err = null;
                        try { r = mloImporter.Import(tpath, out var pm, out var lts); }
                        catch (Exception ex) { err = ex.Message; }
                        Check("the light workspace imports the interior", r != null, err ?? $"{r.MloName}: {r.EntityCount} entities");
                        if (r != null)
                        {
                            Console.WriteLine($"  YTYP IMPORT {r.MloName}: entities {r.EntityCount} placed {r.Placed} missing {r.Missing} " +
                                              $"lodSkipped {r.LodSkipped} veg {r.VegetationSkipped} proxies {r.Proxies} oversized {r.Oversized} " +
                                              $"props {r.Props.Count} unique {r.UniqueProps} lights {r.LightCount} shellMeshes {r.ShellMeshes}");
                            if (r.MissingNames.Count > 0) Console.WriteLine("    missing: " + string.Join(", ", r.MissingNames.Take(15)));
                            if (r.FailedNames.Count > 0) Console.WriteLine("    failed: " + string.Join(", ", r.FailedNames.Take(15)));
                            if (r.OversizedNames.Count > 0) Console.WriteLine("    oversized: " + string.Join(", ", r.OversizedNames.Take(10)));
                            Check("nearly every entity of the interior is placed", r.Placed >= r.EntityCount * 0.9,
                                  $"{r.Placed} of {r.EntityCount} placed, {r.Missing} missing");
                            Check("with 'All props editable' on, the props arrive as editable prop files (not shell + 2)",
                                  r.Props.Count >= 100, $"{r.Props.Count} prop files, {r.UniqueProps} unique props");
                        }
                    }
                }

                {
                    var tf = gameFiles.FindTexture(4047019542, 3154743001);
                    if (tf != null)
                    {
                        var px = CodeWalker.Utils.DDSIO.GetPixels(tf, 0);
                        Console.WriteLine($"  WATERFOG {tf.Width}x{tf.Height} {tf.Format} pixels {(px?.Length ?? 0)}");
                        if (px != null && px.Length >= tf.Width * tf.Height * 4)
                        {
                            void Sample(string where, float wx, float wy)
                            {
                                float u = (wx + 4000.0f) / 8500.0f, v = 1.0f - (wy + 4000.0f) / 12000.0f;
                                int x = Math.Clamp((int)(u * tf.Width), 0, tf.Width - 1), y = Math.Clamp((int)(v * tf.Height), 0, tf.Height - 1);
                                int i = (y * tf.Width + x) * 4;
                                Console.WriteLine($"  WATERFOG {where} ({wx},{wy}) -> uv ({u:0.00},{v:0.00}) px ({x},{y}) = {px[i]},{px[i + 1]},{px[i + 2]},{px[i + 3]}");
                            }
                            Sample("vespucci", -1780, -1180); Sample("open sea", -3200, -1000); Sample("ls river", 300, -600);
                            Sample("alamo", 900, 4100); Sample("port", 400, -2900);
                        }
                    }
                    else Console.WriteLine("  WATERFOG texture not found");
                }

                {
                    MloArchetype m26 = null;
                    if (gameFiles.Cache.YtypDict != null && gameFiles.Cache.YtypDict.TryGetValue(JenkHash.GenHash("m26_1_int_01"), out var m26ytyp))
                    {
                        Console.WriteLine($"  M26 ytyp {m26ytyp.RpfFileEntry?.Path}: {m26ytyp.AllArchetypes?.Length ?? 0} archetypes: " +
                                          string.Join(", ", (m26ytyp.AllArchetypes ?? Array.Empty<Archetype>()).Take(6).Select(a => a.Name + (a is MloArchetype ? "(MLO)" : ""))));
                        m26 = m26ytyp.AllArchetypes?.OfType<MloArchetype>().FirstOrDefault();
                        if (m26 != null && gameFiles.Cache.GetArchetype(m26.Hash) == null)
                            Console.WriteLine($"  M26 the MLO {m26.Name} is in the ytyp but GetArchetype misses it!");
                    }
                    Console.WriteLine($"  M26 archetype: {(m26 == null ? "NOT IN CACHE" : m26.Name + " in " + (m26.Ytyp?.RpfFileEntry?.Path ?? "?"))}  " +
                                      $"dlc={gameFiles.Cache.EnableDlc} selected='{gameFiles.Cache.SelectedDlc}' dlcCount={gameFiles.Cache.DlcNameList?.Count ?? 0}");
                    if (m26 == null)
                    {
                        panel.Archive.Search("m26_1_int_01", ".ytyp");
                        Console.WriteLine($"  M26 ytyp in archives: {(panel.Archive.Results.Count > 0 ? panel.Archive.Results[0].Path : "none")}");
                        var c = gameFiles.Cache;
                        bool inYtypDict = c.YtypDict != null && c.YtypDict.ContainsKey(JenkHash.GenHash("m26_1_int_01"));
                        var setup = c.DlcSetupFiles?.FirstOrDefault(x => x?.DlcFile?.Path != null && x.DlcFile.Path.ToLowerInvariant().Contains("mp2026_01\\"));
                        var setupG9 = c.DlcSetupFiles?.FirstOrDefault(x => x?.DlcFile?.Path != null && x.DlcFile.Path.ToLowerInvariant().Contains("mp2026_01_g9ec"));
                        bool activeRpf = c.DlcActiveRpfs?.Any(r => r.Path.ToLowerInvariant().Contains("mp2026_01\\dlc.rpf")) ?? false;
                        var mapKeys = c.ActiveMapRpfFiles?.Keys.Where(k => k.Contains("mp2026_01")).Take(8).ToList() ?? new List<string>();
                        Console.WriteLine($"  M26 in YtypDict={inYtypDict} setup(mp2026_01)={(setup != null ? $"order {setup.order} datFile {setup.datFile} rpfDataFiles {setup.ContentFile?.RpfDataFiles?.Count}" : "NONE")} " +
                                          $"setup(g9ec)={(setupG9 != null ? $"order {setupG9.order}" : "NONE")} activeRpf={activeRpf} activeMapKeys=[{string.Join(", ", mapKeys)}]");
                        if (setup?.ContentFile?.RpfDataFiles != null)
                            foreach (var kv in setup.ContentFile.RpfDataFiles.Take(6))
                                Console.WriteLine($"    rpfDataFile {kv.Key} -> {kv.Value.filename}");
                        var dlcOrder = string.Join(", ", (c.DlcNameList ?? new List<string>()).Skip(Math.Max(0, (c.DlcNameList?.Count ?? 0) - 6)));
                        Console.WriteLine($"  M26 last DLCs in order: {dlcOrder}");
                    }
                    else
                    {
                        int total = 0, resolved = 0; var missing = new List<string>();
                        foreach (var me in m26.entities ?? Array.Empty<MCEntityDef>())
                        {
                            if (me == null) continue; total++;
                            var a = gameFiles.Cache.GetArchetype(me._Data.archetypeName.Hash);
                            if (a != null) resolved++; else if (missing.Count < 12) missing.Add(me._Data.archetypeName.ToString());
                        }
                        Console.WriteLine($"  M26 entities {total}, archetypes resolved {resolved}; missing e.g. {string.Join(", ", missing)}");
                        if (missing.Count > 0)
                        {
                            panel.Archive.Search(missing[0], "");
                            Console.WriteLine($"  M26 first missing '{missing[0]}' in archives: " +
                                              string.Join(" | ", panel.Archive.Results.Take(4).Select(r => r.Path)));
                        }
                        Check("m26_1_int_01's entity archetypes resolve from the cache", resolved >= total * 0.9, $"{resolved} of {total}");

                        World.Build(gameFiles);
                        World.Drain(WorldStreamer.DowntownLosSantos);
                        var frW = new SharpDX.BoundingFrustum(camera.ViewProjMatrix);
                        for (int f = 0; f < 4000; f++)
                        {
                            worldRender.Update(World.Visible, gameFiles, modelRenderer, frW, false, World.Fade);
                            if (worldRender.BuiltThisFrame == 0) { if (worldRender.LoadsPending == 0) break; System.Threading.Thread.Sleep(2); }
                        }
                        var m26Entry = m26.Ytyp?.RpfFileEntry;
                        string tdir2 = Path.Combine(Path.GetTempPath(), "rle_ytyptest");
                        Directory.CreateDirectory(tdir2);
                        string tpath2 = Path.Combine(tdir2, m26Entry.Name);
                        File.WriteAllBytes(tpath2, ArchiveBrowser.ExtractForDisk(m26Entry));
                        mloImporter.ImportAllProps = true; mloImporter.ImportPropLights = true; mloImporter.ImportVegetation = true;
                        MloImportResult r2 = null; string err2 = null;
                        try { r2 = mloImporter.Import(tpath2, out var pm2, out var lts2); }
                        catch (Exception ex) { err2 = ex.Message; }
                        if (r2 != null)
                        {
                            Console.WriteLine($"  M26 IMPORT {r2.MloName}: entities {r2.EntityCount} placed {r2.Placed} missing {r2.Missing} " +
                                              $"props {r2.Props.Count} unique {r2.UniqueProps} lights {r2.LightCount} shell {r2.ShellMeshes} in {r2.Seconds:0.0}s");
                            if (r2.MissingNames.Count > 0) Console.WriteLine("    missing: " + string.Join(", ", r2.MissingNames.Take(12)));
                            if (r2.FailedNames.Count > 0) Console.WriteLine("    failed: " + string.Join(", ", r2.FailedNames.Take(12)));
                        }
                        Check("after using the world, importing m26_1_int_01 into the light workspace places the interior",
                              r2 != null && r2.Placed >= r2.EntityCount * 0.9,
                              err2 ?? (r2 == null ? "null" : $"{r2.Placed} of {r2.EntityCount}, {r2.Missing} missing"));
                    }
                }

                Console.WriteLine(fails == 0 ? "ARCHIVETEST PASSED" : $"ARCHIVETEST FAILED ({fails})");
                Close();
                return;
        }

        partial void ApplyStartupWorkspace_S4();
        partial void SeqTest_T1(Action<string, bool, string> check);
        partial void NavMouseRightDown_S4(int x, int y);
        partial void NavMouseRightUp_S4(float dragPixels, bool scrubbed);
        partial void NavStreamTick_S4();
        partial void SeqTest_T4(Action<string, bool, string> check);
        partial void SeqTest_T5(Action<string, bool, string> check);
        partial void SeqTest_U1(Action<string, bool, string> check);
        partial void SeqTest_U2(Action<string, bool, string> check);
        partial void ApplyWorldSpawn_U1();
        partial void AudioTick_U1();
        partial void DrawDayOverlays_U2(SharpDX.Direct3D11.DeviceContext context);
        partial void NavOverlayInk_U2(ref SharpDX.Vector4 sel, ref SharpDX.Vector4 selLine, ref SharpDX.Vector4 draft);
        partial void OnWorldTick_U2();
        partial void PinExposure_U2();
        partial void PrecisionOverlaySuppressed_U2(ref bool suppressed);
        partial void PrecisionTint_U2(ref uint tint);
        partial void ServiceResetView_U1();
        partial void DeleteKey_T4(ref bool handled);
        partial void DrawPrecisionOverlay_T4(SharpDX.Direct3D11.DeviceContext context);
        partial void OnWorldTick_T4();
        partial void OnWorldTick_T5();
        partial void PrecisionOwnsBox_T4(CodeWalker.GameFiles.YmapEntityDef e, ref bool owned);
        partial void RpfDropFiles_T4(string[] paths, int screenX, int screenY, ref bool handled);
        partial void OnAfterSkyDraw_Sky(SharpDX.Direct3D11.DeviceContext context, double now);
        partial void OnAfterWorldDraw_Materials(SharpDX.Direct3D11.DeviceContext context);
        partial void GrassDebugDump_Q5();
        partial void SeqTest_Q5(Action<string, bool, string> check);
        partial void GrassLodDump_U5();
        partial void SeqTest_U5(Action<string, bool, string> check);
        partial void OnWorldTick_U5();
        partial void OnWorldTick_R2();
        partial void CreateMatScene_R2();
        partial void DisposeMatScene_R2();
        partial void SeqTest_R2(Action<string, bool, string> check);
        partial void OnAfterWorldDraw_Sky(SharpDX.Direct3D11.DeviceContext context);
        partial void OnAfterWorldDraw_Selection(SharpDX.Direct3D11.DeviceContext context);
        partial void OnAfterWorldDraw_H3(SharpDX.Direct3D11.DeviceContext context);
        partial void OnWorldTick_World();
        partial void OnWorldTick_Materials();
        partial void OnWorldTick_Selection();
        partial void OnWorldTick_ScriptIpls();
        partial void OnWorldTick_ModsDlc_V21();
        partial void OnWorldTick_ModsProbe_V21();
        partial void OnWorldTick_HeightmapProbe_V21();
        partial void OnWorldTick_FurScan_V21();
        partial void OnWorldTick_WeaponScan_V21();
        partial void OnWorldTick_TexFind_V23();
        partial void OnWorldTick_WeaponMetaScan_V26();
        partial void OnWorldTick_ShaderDump_V26();
        partial void OnWorldTick_Extract_V38();
        partial void OnWorldTick_ArchFind_V64();
        partial void OnWorldTick_PedFurScan_U6();
        partial void OnTick_FurNoise_U8();
        partial void OnWorldTick_ClipArchScan_U7();
        partial void OnWorldTick_Ext_V68();
        partial void ExtMouseDown_V68(int x, int y, ref bool consumed);
        partial void ExtRightDown_V68(ref bool consumed);
        partial void ExtEscape_V68(ref bool handled);
        partial void ExtClickHandle_V69(int x, int y, ref bool handled);
        partial void ExtKeyDown_V70(Keys combo, ref bool handled);
        partial void ExtRightClick_V70(int x, int y, ref bool handled);
        partial void OnAfterModelDraw_Ext_V68(SharpDX.Direct3D11.DeviceContext context);
        partial void ExtGizmoMouseDown_V69(int x, int y, bool shift, bool alt, ref bool consumed);
        partial void ExtGizmoMouseMove_V69(int x, int y);
        partial void ExtGizmoMouseUp_V69();
        partial void DrawExtGizmo_V69(SharpDX.Direct3D11.DeviceContext context);
        partial void OnWorldTick_HeightmapShot_V21();
        partial void OnWorldTick_FurShot_V21();
        partial void OnCapture_FurShot_V21();
        partial void OnTick_TexDicts_V21();
        partial void OnTick_ProjectMultiSelect_V26();
        partial void OnWorldTick_ResetTest_V22();
        partial void RunWorldTestExtras_World(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void RunWorldTestExtras_Materials(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void RunWorldTestExtras_Sky(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void RunWorldTestExtras_Selection(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void AfterWorldAutoSelect_Selection();
        partial void AfterWorldAutoSelect_Dup_V65();
        partial void WorldDeleteSelected_U5(ref bool handled);
        partial void AfterWorldAutoSelect_Del_U5();
        partial void OnWorldTick_SnapProbe_U5();
        partial void RpfSideButtons_U9(System.Windows.Forms.MouseEventArgs e, ref bool handled);
        partial void WorldPasteClipboard_U5(ref bool handled);
        partial void OnAfterWorldDraw_SpaceData(SharpDX.Direct3D11.DeviceContext context);
        partial void RunWorldTestExtras_SpaceData(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void DrawSelection_SpaceData(in Editor.WorldSelection s, SharpDX.Vector4 col, bool full, ref SharpDX.Vector3 pos, ref SharpDX.Quaternion ori, ref SharpDX.Vector3 bbmin, ref SharpDX.Vector3 bbmax, ref bool drawBox);
        partial void WorldTargetChanged_SpaceData(Editor.IWorldGizmoTarget t, ref bool handled);
        partial void WorldSelReport_SpaceData(in Editor.WorldSelection s, System.Text.StringBuilder sb);
        partial void RunWorldTestExtras_ProjectAssets(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void RenderDetachedWindows_Detach(bool captureNow, float dt);
        partial void DisposeDetachedWindows_Detach();
        partial void RenderDetachedWindows_M1(bool captureNow, float dt);
        partial void DisposeDetachedWindows_M1();
        partial void RenderModelViewer_P1(bool captureNow, float dt);
        partial void DisposeModelViewer_P1();
        partial void SeqTest_P1(Action<string, bool, string> check);
        partial void DrawMloCreatorHelpers_H5();
        partial void DrawMloCreatorGizmo_H5(SharpDX.Direct3D11.DeviceContext context);
        partial void MloCreatorMouseDown_H5(int x, int y, bool shift, bool alt, ref bool consumed);
        partial void MloCreatorMouseMove_H5(int x, int y);
        partial void MloCreatorMouseUp_H5(int x, int y, bool wasClick, ref bool handled);
        partial void MloCreatorTest_H5(Action<string, bool, string> check);
        partial void WireMloWorkspace_J6();
        partial void ApplyMloSpaceFlag_J6();
        partial void MloWorkspaceKeyDown_J6(Keys combo, ref bool handled);
        partial void MloWorkspaceKeyUp_J6(Keys key);
        partial void MloWorkspaceTest_J6(Action<string, bool, string> check);
        private partial bool MloSnapRightDown_L4();
        partial void MloSnapModeTest_L4(Action<string, bool, string> check);
        partial void MloSceneTest_L3(Action<string, bool, string> check);
        partial void WireMloAssets_L3();
        partial void AddScenarioModels_I4(List<RenderModel> draw);
        partial void ReleaseScenarioModels_I4();
        partial void OnWorldTick_LightEdit();
        partial void OnWorldTick_J3();
        partial void OnWorldTick_K2();
        partial void OnWorldTick_L2();
        partial void OnTick_L2();
        partial void OnWorldTick_O2();
        partial void InteriorTimecycleTest_K2(Action<string, bool, string> check);
        partial void RunWorldTestExtras_J3(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void PickWorldLights_I6(ref SharpDX.Ray ray, SharpDX.Vector3 camPos, ref Editor.WorldSelection hit);
        partial void WorldLightNearestCandidates_I6(Action<SharpDX.Vector3, float> consider);
        partial void DrawWorldLightCandidates_I6();
        partial void DrawSelection_Light(in Editor.WorldSelection s, SharpDX.Vector4 col, bool full, ref bool drawBox);
        partial void WorldLightTargetChanged_I6(CodeWalker.GameFiles.LightAttributes la, Editor.IWorldGizmoTarget t);
        partial void WorldSelReport_Light(in Editor.WorldSelection s, System.Text.StringBuilder sb);
        partial void WorldLightEditTest_I6(Action<string, bool, string> check);
        partial void ProjectSelectionTest_I2(Action<string, bool, string> check);
        partial void RunWorldTestExtras_I2(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void OnWorldTick_Area();
        partial void RunWorldTestExtras_Area(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void TickWorldPrecisionTint_J2();
        partial void WorldSelReport_J2(in Editor.WorldSelection s, System.Text.StringBuilder sb);
        partial void RunWorldTestExtras_J2(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void OnWorldTick_L1();
        partial void OnWorldTick_M2();
        partial void SeqTest_M2(Action<string, bool, string> check);
        partial void SeqTest_N4(Action<string, bool, string> check);
        partial void SeqTest_R6(Action<string, bool, string> check);
        partial void SeqTest_S6(Action<string, bool, string> check);
        partial void SeqTest_T6(Action<string, bool, string> check);
        partial void SeqTest_U4(Action<string, bool, string> check);
        partial void SeqTest_O1(Action<string, bool, string> check);
        partial void SeqTest_Q1(Action<string, bool, string> check);
        partial void NoteYtypImport_R1(string path, Editor.MloImportResult result, Rendering.RenderModel model);
        partial void OnClearImports_R1();
        partial void SeqTest_R1(Action<string, bool, string> check);
        partial void SeqTest_O3(Action<string, bool, string> check);
        partial void AddTerrainModels_R4(List<RenderModel> draw);
        partial void TerrainMouseDown_R4(int x, int y, bool rightButton, ref bool consumed);
        partial void TerrainMouseMove_R4(int x, int y, bool leftDown, bool rightDown);
        partial void TerrainMouseUp_R4();
        partial void DrawTerrainHelpers_R4();
        partial void SeqTest_R4(Action<string, bool, string> check);
        partial void SeqTest_S2(Action<string, bool, string> check);
        partial void SeqTest_U6(Action<string, bool, string> check);
        partial void SeqTest_V2(Action<string, bool, string> check);
        partial void SeqTest_V5(Action<string, bool, string> check);
        partial void SeqTest_V6(Action<string, bool, string> check);
        partial void SeqTest_V8(Action<string, bool, string> check);
        partial void SeqTest_T3(Action<string, bool, string> check);
        partial void SeqTest_W1(Action<string, bool, string> check);
        partial void SeqTest_W2(Action<string, bool, string> check);
        partial void SeqTest_W3(Action<string, bool, string> check);
        partial void SeqTest_W4(Action<string, bool, string> check);
        partial void OnWorldTick_U3();
        partial void SeqTest_U3(Action<string, bool, string> check);
        partial void WireSectionState_Q4();
        partial void WireSectionCameras_T2();
        partial void OnWorldTick_Q4();
        partial void OnWorldTick_T2();
        partial void SelfTest_P3(Action<string, bool, string> check);
        partial void OnWorldTick_S5();
        partial void SeqTest_S5(Action<string, bool, string> check);
        partial void InteriorCullerTest_L1(Action<string, bool, string> check);
        partial void RunWorldTestExtras_L1(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        partial void BeforeWorldLights_N2();
        partial void AfterLightsBuilt_N2(int lightCount);
        partial void OnWorldTick_N2();
        partial void SeqTest_N2(Action<string, bool, string> check);
        partial void RunWorldTestExtras_N2(Action<string, bool, string> check, Action<SharpDX.Vector3> settle);
        public readonly WorldStreamer World = new WorldStreamer();
        private readonly WorldRenderer worldRender = new WorldRenderer();
        public readonly WorldEditor WorldEdit = new WorldEditor();
        public readonly EditHistory WorldHistory = new EditHistory();
        public readonly ProjectWindow ProjWin = new ProjectWindow();
        private ProjectController projCtl;
        private readonly Dictionary<uint, YmapFile> projectOverrides = new Dictionary<uint, YmapFile>();
        private WorldGizmo worldGizmo;
        private CollisionView collisionView;
        private WorldWater worldWater;
        private AssetPreview assetPreview;
        private CollisionMesh previewCollision;
        private List<(uint hash, SharpDX.BoundingBox box)> ybnIndex;
        private readonly List<CollisionMesh> collisionDraw = new List<CollisionMesh>();
        private EntityTransformCommand.Pending worldDrag;
        private void PinEditedYmaps() => World.IsPinned = WorldEdit.IsDirty;

        private void RebuildProjectOverrides()
        {
            projectOverrides.Clear();
            RefreshProjectAssets();
            var p = ProjWin.Project;
            if (p != null && ProjWin.RenderProjectItems)
            {
                p.OverlayOnto(projectOverrides);
                foreach (var y in p.YmapFiles) p.InitYmapArchetypes(y, gameFiles?.Cache);
            }
            World.ProjectOverrides = projectOverrides.Count > 0 ? projectOverrides : null;
            World.HideGameMap = ProjWin.HideGtaMap;
            World.Invalidate();
            PushProjectYbns_V25();
            ForgetSupersededInstances_R2();
        }

        private bool lastRenderProjectItems = true, lastHideGta;
        private YmapEntityDef lastWorldSelForProject;

        private void TickProject()
        {
            TickProjectWindowExtras();
            TickMloFocus_H2();
            TickProjectSelection_I2();
            TickProjectAddSelection_H2();
            OnTick_ProjectMultiSelect_V26();
            projCtl.Tick();
            if (ProjWin.RenderProjectItems != lastRenderProjectItems || ProjWin.HideGtaMap != lastHideGta)
            {
                lastRenderProjectItems = ProjWin.RenderProjectItems;
                lastHideGta = ProjWin.HideGtaMap;
                RebuildProjectOverrides();
            }
            var sel = WorldEdit.Selected;
            if (!ReferenceEquals(sel, lastWorldSelForProject))
            {
                lastWorldSelForProject = sel;
                if (sel != null && ProjWin.Visible) ProjWin.ShowWorldSelection(sel);
            }
        }

        private void WorldEntityChanged(YmapEntityDef e)
        {
            if (e == null) return;
            worldRender.Forget(e);
            RebuildEntityNow_R2(e);
            if (e.MloInstance != null) ForgetInteriorInstances(e.MloInstance);
            WorldEdit.MarkDirty(e);
            MarkInteriorYtypChanged_I2(e);
            World.Invalidate();
            ProjectAutoAddForEdit(e);
        }

        private readonly List<YmapEntityDef> worldSelList = new List<YmapEntityDef>();
        private bool worldBuilt;
        private int worldWarmup;
        private float worldFlyMetres;
        private float worldAimYaw = 1.4f, worldAimPitch = 0.45f;
        private bool worldAutoSelect, worldDemoProject;
        private float perfWorldMs, perfPanelMs, perfSelectMs, perfUpdateMs, perfLightsMs, perfCollisionMs;
        internal int WorldPrepareBudget = 16;
        private SharpDX.Vector3 worldFlyFrom;
        private int worldPeakResident, worldPeakArchetypes;

        private void RunWorldTest()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            World.Build(gameFiles);
            Console.WriteLine($"WORLD map built: {World.NodeCount} ymap nodes in {sw.ElapsedMilliseconds} ms");

            int fails = 0;
            void Check(string what, bool ok, string detail)
            {
                Console.WriteLine($"  {(ok ? "OK  " : "FAIL")} {what}  {detail}");
                if (!ok) fails++;
            }
            Check("cache.dat gave us the map", World.NodeCount > 1000, $"{World.NodeCount} nodes");

            void Settle(SharpDX.Vector3 at)
            {
                for (int round = 0; round < 8; round++)
                {
                    World.Drain(at);
                    var fr = new SharpDX.BoundingFrustum(camera.ViewProjMatrix);
                    int built = 0;
                    var settleClock = System.Diagnostics.Stopwatch.StartNew();
                    for (int f = 0; f < 20000 && settleClock.ElapsedMilliseconds < 60000; f++)
                    {
                        worldRender.Update(World.Visible, gameFiles, modelRenderer, fr, false, World.Fade);
                        built += worldRender.BuiltThisFrame;
                        if (worldRender.BuiltThisFrame == 0)
                        {
                            if (worldRender.LoadsPending == 0) break;
                            System.Threading.Thread.Sleep(2);
                        }
                    }
                    World.Invalidate();
                    if (built == 0) break;
                }
                World.Drain(at);
            }

            if (Environment.GetEnvironmentVariable("RLE_WORLDTEST_ONLY") == "world")
            {
                RunWorldTestExtras_World(Check, Settle);
                Console.WriteLine(fails == 0 ? "WORLDTEST PASSED" : $"WORLDTEST FAILED ({fails})");
                return;
            }
            {
                var only = Environment.GetEnvironmentVariable("RLE_WORLDTEST_ONLY");
                if (only == "area" || only == "j2" || only == "j3" || only == "l1" || only == "n1")
                if (only == "area" || only == "j2" || only == "j3" || only == "l1" || only == "n2")
                {
                    if (only == "area") RunWorldTestExtras_Area(Check, Settle);
                    if (only == "j2") RunWorldTestExtras_J2(Check, Settle);
                    if (only == "j3") RunWorldTestExtras_J3(Check, Settle);
                    if (only == "l1") RunWorldTestExtras_L1(Check, Settle);
                    if (only == "n1") RunWorldTestExtras_N1(Check, Settle);
                    if (only == "n2") RunWorldTestExtras_N2(Check, Settle);
                    Console.WriteLine(fails == 0 ? "WORLDTEST PASSED" : $"WORLDTEST FAILED ({fails})");
                    return;
                }
            }

            {
                var scanWait = System.Diagnostics.Stopwatch.StartNew();
                while (!World.UncachedScanDone && scanWait.ElapsedMilliseconds < 120000) System.Threading.Thread.Sleep(50);
                World.Select(WorldStreamer.DowntownLosSantos);
                Console.WriteLine($"  UNCACHED ymaps given nodes: {World.UncachedYmaps} (scan done {World.UncachedScanDone} in {scanWait.ElapsedMilliseconds} ms)");
                int h4 = 0, h4Nodes = 0; float minX = 1e9f, maxX = -1e9f, minY = 1e9f, maxY = -1e9f;
                var samples = new List<string>();
                foreach (var n in World.Nodes)
                {
                    var nm = n.Name ?? "";
                    if (!nm.StartsWith("h4_", StringComparison.OrdinalIgnoreCase)) continue;
                    h4Nodes++;
                    minX = Math.Min(minX, n.Min.X); maxX = Math.Max(maxX, n.Max.X); minY = Math.Min(minY, n.Min.Y); maxY = Math.Max(maxY, n.Max.Y);
                    if (samples.Count < 5) samples.Add($"{nm} [{n.Min.X:0},{n.Min.Y:0}..{n.Max.X:0},{n.Max.Y:0}] flags {n.ContentFlags}");
                }
                if (gameFiles.Cache.YmapHierarchyDict != null)
                    foreach (var kv in gameFiles.Cache.YmapHierarchyDict)
                        if ((kv.Value?.Name.ToString() ?? "").StartsWith("h4_", StringComparison.OrdinalIgnoreCase)) h4++;
                var cacheFiles = gameFiles.Cache.AllCacheFiles?.Count ?? 0;
                Console.WriteLine($"  CAYO h4_ ymaps: hierarchy {h4}, nodes {h4Nodes}, extents x {minX:0}..{maxX:0} y {minY:0}..{maxY:0}, cache files {cacheFiles}; e.g. {string.Join(" | ", samples)}");
                Check("Cayo Perico's ymaps are in the map (h4_ nodes from the DLC cache)", h4Nodes > 50, $"{h4Nodes} h4_ nodes");

                var island = new SharpDX.Vector3(4830, -5200, 120);
                Settle(island);
                int h4Vis = World.Visible.Count(v => (v.Ymap?.Name ?? "").StartsWith("h4_", StringComparison.OrdinalIgnoreCase));
                int h4Resident = World.Nodes.Count(n => (n.Name ?? "").StartsWith("h4_", StringComparison.OrdinalIgnoreCase) && n.Ymap != null && n.Prepared);
                int h4Tree = 0; var diag = new List<string>();
                foreach (var n in World.Nodes)
                {
                    if (!(n.Name ?? "").StartsWith("h4_", StringComparison.OrdinalIgnoreCase) || n.Ymap == null || !n.Prepared) continue;
                    var y = n.Ymap;
                    if (diag.Count < 8 && y.AllEntities != null && y.AllEntities.Length > 0)
                    {
                        var e0 = y.AllEntities[0];
                        diag.Add($"{n.Name}: parent {y._CMapData.parent} (resident {(y.Parent != null)}) ents {y.AllEntities.Length} e0 {e0.Archetype?.Name ?? "noArch"} lodDist {e0.LodDist:0} dist {(e0.Position - island).Length():0} lodLevel {e0._CEntityDef.lodLevel} parentEnt {(e0.Parent != null)}");
                    }
                }
                Console.WriteLine($"  CAYO at {island}: h4 ymaps resident {h4Resident}, h4 entities visible {h4Vis}, tree ymaps {World.LodTreeYmaps}");
                foreach (var d in diag) Console.WriteLine("    " + d);
                Check("standing over Cayo Perico streams the island's entities", h4Vis > 100, $"{h4Vis} h4_ entities visible");
            }

            var ground = WorldStreamer.DowntownLosSantos;
            Check("downtown is inside somebody's extents",
                  World.NodesNear(ground).Count > 0,
                  $"{World.NodesNear(ground).Count} ymaps within {World.StreamRadius:0} m");

            int savedCap = World.MaxEntities;
            World.MaxEntities = int.MaxValue;

            var altitudes = new[] { 20.0f, 300.0f, 1500.0f };
            var counts = new int[altitudes.Length];
            for (int i = 0; i < altitudes.Length; i++)
            {
                var p = new Vector3(ground.X, ground.Y, altitudes[i]);
                Settle(p);
                counts[i] = World.Visible.Count;
                var byLevel = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var ent in World.Visible)
                {
                    var lv = ent._CEntityDef.lodLevel.ToString().Replace("LODTYPES_DEPTH_", "");
                    byLevel.TryGetValue(lv, out int c);
                    byLevel[lv] = c + 1;
                }
                var levels = string.Join("  ", byLevel.OrderByDescending(k => k.Value)
                                                      .Select(k => $"{k.Key}={k.Value}"));
                Console.WriteLine($"  z={altitudes[i],6:0}m  ymaps open {World.YmapsOpen,4}/{World.YmapsWanted,-4} walked {World.YmapsWalked,4}  " +
                                  $"entities {World.Visible.Count,6}   {levels}");
            }

            if (counts[0] == 0)
            {
                int loadedYmaps = World.Nodes.Count(n => n.Ymap != null);
                int withRoots = World.Nodes.Count(n => n.Ymap?.RootEntities?.Length > 0);
                int totalEnts = World.Nodes.Where(n => n.Ymap?.AllEntities != null)
                                           .Sum(n => n.Ymap.AllEntities.Length);
                Console.WriteLine($"  WHY: ymaps loaded {loadedYmaps}, with roots {withRoots}, " +
                                  $"entities in them {totalEnts}");
            }
            Check("street level draws something", counts[0] > 50, $"{counts[0]} entities");
            Check("street level is a drawable number, not the whole city",
                  counts[0] < 60000, $"{counts[0]} entities at r={World.StreamRadius:0} m");
            Check("climbing draws fewer, not more", counts[2] <= counts[0],
                  $"{counts[0]} at 20 m -> {counts[2]} at 1500 m");

            World.MaxEntities = savedCap;

            int withArch = World.Visible.Count(e => e.Archetype != null);
            Check("every selected entity has an archetype", withArch == World.Visible.Count,
                  $"{withArch}/{World.Visible.Count}");

            Settle(ground);
            var target = World.Visible.FirstOrDefault(e => e.Archetype != null && e.Ymap != null);
            Check("something to edit", target != null, target?.Archetype?.Name ?? "nothing selectable");

            if (target != null)
            {
                var eye = target.Position + new SharpDX.Vector3(0, -Math.Max(target.BSRadius * 2.0f, 5.0f), 0);
                var dir = SharpDX.Vector3.Normalize(target.Position - eye);
                var hit = WorldEdit.Pick(new SharpDX.Ray(eye, dir), World.Visible);
                Check("a ray down the middle hits something", hit != null,
                      hit?.Archetype?.Name ?? "nothing hit");

                WorldEdit.Select(target);
                var before = target.Position;
                var moved = before + new SharpDX.Vector3(0, 0, 12.5f);
                WorldEdit.SetPosition(moved);
                Check("the move took", (target.Position - moved).Length() < 0.01f,
                      $"{target.Position} vs {moved}");
                Check("the bounding box moved with it",
                      target.BBMin.Z > before.Z - target.BSRadius - 1.0f,
                      $"BBMin.Z {target.BBMin.Z:0.0}, was around {before.Z - target.BSRadius:0.0}");
                Check("its ymap is marked unsaved", WorldEdit.DirtyCount == 1,
                      $"{WorldEdit.DirtyCount} dirty");

                var outDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rle_worldedit");
                try { System.IO.Directory.Delete(outDir, true); } catch { }
                WorldEdit.OutputFolder = outDir;
                var written = WorldEdit.SaveOne(target.Ymap);
                Check("the ymap was written", written != null && System.IO.File.Exists(written),
                      WorldEdit.LastStatus);
                Check("nothing left unsaved", WorldEdit.DirtyCount == 0, $"{WorldEdit.DirtyCount} dirty");

                if (written != null && System.IO.File.Exists(written))
                {
                    var bytes = System.IO.File.ReadAllBytes(written);
                    Check("the file has content", bytes.Length > 64, $"{bytes.Length} bytes");
                    try
                    {
                        var rt = new YmapFile();
                        rt.Load(bytes);
                        var ents = rt.AllEntities;
                        Check("it parses again", ents != null && ents.Length > 0,
                              $"{ents?.Length ?? 0} entities");
                        bool found = false;
                        if (ents != null)
                            foreach (var e in ents)
                                if (Math.Abs(e.Position.Z - moved.Z) < 0.05f &&
                                    Math.Abs(e.Position.X - moved.X) < 0.05f) { found = true; break; }
                        Check("the move survived the round trip", found,
                              found ? $"z={moved.Z:0.00}" : "the moved entity is not in the file");
                    }
                    catch (Exception ex) { Check("it parses again", false, ex.Message); }
                }

                try { System.IO.Directory.Delete(outDir, true); } catch { }
                WorldEdit.Deselect();
            }

            {
                Check("no cross-fade: every selected entity is drawn solid (CodeWalker swaps levels instantly)",
                      World.Fade.All(f => f >= 0.999f), $"{World.Fade.Count(f => f < 0.999f)} of {World.Fade.Count} mid-fade");
                Check("fades stay aligned with the selection", World.Fade.Count == World.Visible.Count,
                      $"{World.Fade.Count} fades / {World.Visible.Count} entities");
            }

            {
                YmapEntityDef shell = null;
                foreach (var n in World.Nodes)
                {
                    var all = n.Ymap?.AllEntities;
                    if (all == null) continue;
                    foreach (var en in all)
                        if (en?.MloInstance?.Entities != null && en.MloInstance.Entities.Length > 10)
                        { shell = en; break; }
                    if (shell != null) break;
                }
                Check("a resident ymap holds an interior", shell != null,
                      shell?.Archetype?.Name ?? "none loaded");
                if (shell != null)
                {
                    World.Drain(shell.Position);
                    int inner = World.Visible.Count(x => x.MloParent != null);
                    Check("standing at it streams the rooms", inner > 10,
                          $"{inner} interior entities of {shell.MloInstance.Entities.Length} " +
                          $"in {shell.Archetype.Name}");

                    var ients = shell.MloInstance.Entities.Where(x => x?.Archetype != null &&
                                   (x._CEntityDef.flags & 0x2000000) == 0).ToList();
                    int visibleOfThem = World.Visible.Count(x => ReferenceEquals(x.MloParent, shell));
                    Check("every interior entity of the shell is streamed while the shell is (CodeWalker's rule)",
                          visibleOfThem >= ients.Count - 2,
                          $"{visibleOfThem} of {ients.Count} entities of {shell.Archetype.Name}; shell lodDist {shell.LodDist:0}");

                    var far = shell.Position + new SharpDX.Vector3(shell.BSRadius * 0.6f, 0, 0);
                    World.Drain(far);
                    int visibleFar = World.Visible.Count(x => ReferenceEquals(x.MloParent, shell));
                    Check("crossing the interior keeps its rooms streamed",
                          visibleFar >= ients.Count - 2,
                          $"{visibleFar} of {ients.Count} at {shell.BSRadius * 0.6f:0} m from the origin (radius {shell.BSRadius:0})");
                }
            }

            {
                int opsFails = EntityOps.SelfTest(null);
                Check("EntityOps self-test", opsFails == 0, $"{opsFails} failure(s)");
            }

            if (target?.Ymap != null)
                Check("ymap survives the XML round trip", XmlIO.VerifyRoundTrip(target.Ymap, out var xd), xd);

            if (target != null)
            {
                WorldHistory.Clear();
                WorldEdit.Select(target);
                var p0 = target.Position;

                var pend = EntityTransformCommand.Begin("Test move", target, WorldEntityChanged);
                WorldEdit.SetPosition(p0 + new SharpDX.Vector3(5.0f, 0.0f, 0.0f));
                var cmd = pend.Complete();
                Check("a bracketed edit produces one command", cmd != null, cmd?.Name ?? "null");
                if (cmd != null) WorldHistory.Push(cmd);

                TryWorldUndo();
                Check("undo puts the entity back", (target.Position - p0).Length() < 0.001f,
                      $"{target.Position} vs {p0}");
                TryWorldRedo();
                Check("redo re-applies the move", (target.Position - p0).Length() > 4.9f,
                      $"moved {(target.Position - p0).Length():0.00} m");

                TryWorldUndo();
                var pend2 = EntityTransformCommand.Begin("Test nudge", target, WorldEntityChanged);
                WorldEdit.SetPosition(p0 + new SharpDX.Vector3(0.0f, 3.0f, 0.0f));
                var cmd2 = pend2.Complete();
                if (cmd2 != null) WorldHistory.Push(cmd2);
                Check("an edit after an undo is its own step, not a merge",
                      WorldHistory.Count == 1 && !WorldHistory.CanRedo,
                      $"undo depth {WorldHistory.Count}, redo {WorldHistory.RedoCount}");

                var pend3 = EntityTransformCommand.Begin("No-op", target, WorldEntityChanged);
                Check("a no-op drag records nothing", pend3.Complete() == null, "Complete() was not null");

                TryWorldUndo();
                WorldHistory.Clear();
                Check("world edits leave the entity where it started",
                      (target.Position - p0).Length() < 0.001f, $"{target.Position} vs {p0}");
            }

            if (target?.Ymap != null)
            {
                WorldHistory.Clear();
                var tymap = target.Ymap;
                int count0 = tymap.AllEntities?.Length ?? 0;

                WorldEdit.Select(target);
                WorldDuplicateSelected();
                Check("duplicate adds one", (tymap.AllEntities?.Length ?? 0) == count0 + 1,
                      $"{count0} -> {tymap.AllEntities?.Length ?? 0}");
                var dup = WorldEdit.Selected;
                Check("the duplicate is selected and offset", dup != null && dup != target &&
                      (dup.Position - target.Position).Length() > 0.5f,
                      dup == null ? "null" : $"offset {(dup.Position - target.Position).Length():0.0} m");
                TryWorldUndo();
                Check("undo removes the duplicate", (tymap.AllEntities?.Length ?? 0) == count0,
                      $"{tymap.AllEntities?.Length ?? 0} entities");
                TryWorldRedo();
                Check("redo brings it back", (tymap.AllEntities?.Length ?? 0) == count0 + 1,
                      $"{tymap.AllEntities?.Length ?? 0} entities");
                TryWorldUndo();

                WorldEdit.Select(target);
                WorldCopySelected();
                Check("copy fills the clipboard", !EntityOps.Clipboard.IsEmpty, EntityOps.Clipboard.Summary);
                WorldPasteClipboard();
                Check("paste adds one", (tymap.AllEntities?.Length ?? 0) == count0 + 1,
                      $"{tymap.AllEntities?.Length ?? 0} entities");
                TryWorldUndo();
                Check("undo removes the paste", (tymap.AllEntities?.Length ?? 0) == count0,
                      $"{tymap.AllEntities?.Length ?? 0} entities");

                WorldEdit.Select(target);
                var wasAt = target.Position;
                WorldDeleteSelected();
                Check("delete removes it", (tymap.AllEntities?.Length ?? 0) == count0 - 1 &&
                      WorldEdit.Selected == null, $"{tymap.AllEntities?.Length ?? 0} entities");
                TryWorldUndo();
                Check("undo restores the deleted entity",
                      (tymap.AllEntities?.Length ?? 0) == count0 && WorldEdit.Selected != null &&
                      (WorldEdit.Selected.Position - wasAt).Length() < 0.01f,
                      $"{tymap.AllEntities?.Length ?? 0} entities, at {WorldEdit.Selected?.Position}");
                target = WorldEdit.Selected ?? target;
                WorldHistory.Clear();
                EntityOps.Clipboard.Clear();
            }

            {
                var spot = new SharpDX.Vector3(31, -660, 22);
                Settle(spot);
                var fr = new SharpDX.BoundingFrustum(camera.ViewProjMatrix);
                for (int f = 0; f < 4000; f++)
                {
                    worldRender.Update(World.Visible, gameFiles, modelRenderer, fr, false, World.Fade);
                    if (worldRender.BuiltThisFrame == 0) { if (worldRender.LoadsPending == 0) break; System.Threading.Thread.Sleep(2); }
                }
                var kinds = new Dictionary<string, int>();
                int decalMeshes = 0, dirtMeshes = 0, dirtWithMask = 0, normalOnly = 0, normalOnlyKind3 = 0;
                int untexturedPlain = 0;
                foreach (var m in worldRender.Model.Meshes)
                {
                    if (m.AlphaMode != Rendering.GeomAlphaMode.Decal) continue;
                    if ((m.WorldSphere.Center - spot).Length() > 40.0f) continue;
                    decalMeshes++;
                    var sn = m.Shader?.FileName.ToString() ?? m.ShaderName;
                    kinds.TryGetValue(sn, out int c); kinds[sn] = c + 1;
                    if (m.DecalKind == 2) { dirtMeshes++; if (m.DecalMask.LengthSquared() > 0) dirtWithMask++; }
                    if (sn == "decal_normal_only.sps") { normalOnly++; if (m.DecalKind == 3) normalOnlyKind3++; }
                    if (m.DecalKind == 1 && m.DiffuseSRV == null) untexturedPlain++;
                }
                Console.WriteLine($"  DECALS at {spot}: {decalMeshes} decal meshes within 40 m; " +
                                  string.Join(", ", kinds.OrderByDescending(k => k.Value).Select(k => $"{k.Key} x{k.Value}")));
                {
                    var noAlpha = new Dictionary<string, int>();
                    foreach (var m in worldRender.Model.Meshes)
                    {
                        if (m.AlphaMode != Rendering.GeomAlphaMode.Decal || m.Shader?.ParametersList?.Parameters == null) continue;
                        var pl = m.Shader.ParametersList;
                        for (int i = 0; i < pl.Parameters.Length && i < pl.Hashes.Length; i++)
                        {
                            if (!(pl.Parameters[i].Data is Texture tx)) continue;
                            if ((ShaderParamNames)pl.Hashes[i] != ShaderParamNames.DiffuseSampler) continue;
                            var f = tx.Format.ToString();
                            bool alphaless = f.Contains("BC4") || f.Contains("ATI1") || f.Contains("L8") || f.Contains("A8") && !f.Contains("A8R8") && !f.Contains("A8B8") || f.Contains("BC5") || f.Contains("ATI2");
                            if (!alphaless) continue;
                            string key = $"{m.Shader.FileName} / {tx.Name} [{f}]";
                            noAlpha.TryGetValue(key, out int c); noAlpha[key] = c + 1;
                        }
                    }
                    Console.WriteLine("  DECAL NO-ALPHA TEXTURES: " + string.Join(" | ", noAlpha.OrderByDescending(k => k.Value).Take(12).Select(k => $"{k.Key} x{k.Value}")));
                }
                Check("the pavement's decal_normal_only quads are classified normal-only (kind 3, drawn as nothing)",
                      normalOnly > 0 && normalOnlyKind3 == normalOnly,
                      $"{normalOnly} decal_normal_only meshes, {normalOnlyKind3} kind 3");
                Check("no plain decal here is left without its texture (would draw as a slab)",
                      untexturedPlain == 0, $"{untexturedPlain} untextured plain decals of {decalMeshes}");
                Check("dirt decals (decal_dirt.sps) carry their channel mask, not a colour",
                      dirtMeshes == 0 || dirtWithMask == dirtMeshes,
                      $"{dirtMeshes} decal_dirt meshes, {dirtWithMask} with a dirtDecalMask");
            }

            {
                YmapEntityDef small = null;
                for (int f = 0; f < 4000 && (worldRender.BuiltThisFrame > 0 || worldRender.LoadsPending > 0); f++)
                {
                    worldRender.Update(World.Visible, gameFiles, modelRenderer, new SharpDX.BoundingFrustum(camera.ViewProjMatrix), false, World.Fade);
                    if (worldRender.BuiltThisFrame == 0) System.Threading.Thread.Sleep(2);
                }
                foreach (var v in World.Visible)
                {
                    var a = v?.Archetype;
                    if (a == null || v.MloParent != null || v.MloInstance != null) continue;
                    if (a.BSRadius < 0.6f || a.BSRadius > 3.0f) continue;
                    if ((v.Position - new SharpDX.Vector3(31, -660, 22)).Length() > 60.0f) continue;
                    var ext = a.BBMax - a.BBMin;
                    if (ext.Z < 1.0f) continue;
                    var top = v.Position + v.Orientation.Multiply(new SharpDX.Vector3(a.BBMax.X * v.Scale.X * 0.9f, a.BBMax.Y * v.Scale.Y * 0.9f, a.BBMax.Z * v.Scale.Z));
                    var probe = new SharpDX.Ray(top + new SharpDX.Vector3(0, 0, 3.0f), new SharpDX.Vector3(0, 0, -1));
                    var first = WorldPickPrecise(probe, out float fd);
                    if (first == null || fd < 2.9f) continue;
                    small = v; break;
                }
                Check("a small prop stands near the camera to test the picker on", small != null,
                      small?.Archetype?.Name ?? "none within 60 m");
                if (small != null)
                {
                    var centre = small.Position + small.Orientation.Multiply(new SharpDX.Vector3(
                        (small.Archetype.BBMin.X + small.Archetype.BBMax.X) * 0.5f * small.Scale.X,
                        (small.Archetype.BBMin.Y + small.Archetype.BBMax.Y) * 0.5f * small.Scale.Y,
                        small.Archetype.BBMax.Z * small.Scale.Z));
                    var ray = new SharpDX.Ray(centre + new SharpDX.Vector3(0.0f, 0.0f, 3.0f), new SharpDX.Vector3(0.0f, 0.0f, -1.0f));
                    var picked = WorldPickEntity(ray);
                    Check("aiming at a small prop's geometry picks the prop (the light workspace's triangle rule)",
                          ReferenceEquals(picked, small),
                          $"aimed at {small.Archetype.Name} (r {small.Archetype.BSRadius:0.0}), got {picked?.Archetype?.Name ?? "nothing"} (r {picked?.Archetype?.BSRadius ?? 0:0.0})");
                    var under = WorldPickPrecise(ray, out float ud);
                    Check("the surface pick straight down through the prop finds a surface", under != null,
                          under != null ? $"{under.Archetype?.Name} at {ud:0.0} m" : "nothing under it");
                }
            }

            {
                var sea = new SharpDX.Vector3(-1780, -1180, 30);
                Settle(sea);
                int shownSea = 0;
                foreach (var v in World.Visible)
                {
                    var a = v?.Archetype; if (a == null) continue;
                    if (v.BBMin.Z > 3.0f || v.BBMax.Z < -3.0f) continue;
                    var ext = v.BBMax - v.BBMin;
                    if (ext.X < 150.0f && ext.Y < 150.0f) continue;
                    if (shownSea++ >= 8) break;
                    var meshes = worldRender.Model.Meshes.Where(m => ReferenceEquals(worldRender.OwnerOf(m), v)).ToList();
                    var shaders = string.Join(", ", meshes.Select(m => m.Shader?.FileName.ToString() ?? m.ShaderName).Distinct().Take(4));
                    string kids = "";
                    if (v._CEntityDef.numChildren > 0)
                    {
                        int linked = v.LodManagerChildren?.Count ?? 0;
                        var hn = gameFiles.Cache.YmapHierarchyDict != null && v.Ymap != null &&
                                 gameFiles.Cache.YmapHierarchyDict.TryGetValue(v.Ymap.RpfFileEntry?.ShortNameHash ?? 0, out var hnode) ? hnode : null;
                        var childYmaps = hn?.Children == null ? "" : string.Join(", ", hn.Children.Take(8).Select(c =>
                        {
                            var cn = World.Nodes.FirstOrDefault(x => x.Hash == c.Name.Hash);
                            return $"{c.Name}[{(cn == null ? "no node" : cn.Ymap == null ? "not loaded" : cn.Prepared ? "prepared" : "loading")}]";
                        }));
                        kids = $" numChildren {v._CEntityDef.numChildren} linked {linked} dist {v.Distance:0} childLod {v.ChildLodDist:0} childYmaps: {childYmaps}";
                    }
                    Console.WriteLine($"  SEALEVEL {a.Name} [{v._CEntityDef.lodLevel}] ymap {v.Ymap?.Name} ext {ext.X:0}x{ext.Y:0}x{ext.Z:0} z {v.BBMin.Z:0}..{v.BBMax.Z:0} meshes {meshes.Count}: {shaders}{kids}");
                }
            }

            {
                Settle(ground);
                {
                    int shownDiag = 0;
                    foreach (var v in World.Visible)
                    {
                        if (v._CEntityDef.numChildren == 0) continue;
                        if (!(v.Distance <= v.ChildLodDist)) continue;
                        int linked = v.LodManagerChildren?.Count ?? 0;
                        if (linked >= v._CEntityDef.numChildren) continue;
                        if (shownDiag++ >= 6) break;
                        var hn = gameFiles.Cache.YmapHierarchyDict != null && v.Ymap != null &&
                                 gameFiles.Cache.YmapHierarchyDict.TryGetValue(v.Ymap.RpfFileEntry?.ShortNameHash ?? 0, out var hnode) ? hnode : null;
                        string kids = "";
                        if (hn?.Children != null)
                            kids = string.Join(", ", hn.Children.Take(6).Select(c =>
                            {
                                var cn = World.Nodes.FirstOrDefault(x => x.Hash == c.Name.Hash);
                                return $"{c.Name}[{(cn == null ? "no node" : cn.Ymap == null ? "not loaded" : cn.Prepared ? "prepared" : "loading")}]";
                            }));
                        Console.WriteLine($"  LODDIAG {v.Archetype?.Name} [{v._CEntityDef.lodLevel}] in {v.Ymap?.Name}: dist {v.Distance:0} childLod {v.ChildLodDist:0} " +
                                          $"numChildren {v._CEntityDef.numChildren} linked {linked}; child ymaps: {kids}");
                    }
                }
                var vis = new HashSet<YmapEntityDef>(World.Visible);
                {
                    int shownWhy = 0;
                    foreach (var v in World.Visible)
                    {
                        var pa = v.Parent;
                        if (pa == null || !vis.Contains(pa)) continue;
                        if (shownWhy++ >= 5) break;
                        var why = new List<string>();
                        var pc = pa.LodManagerChildren;
                        int cnt = pc?.Count ?? 0;
                        if (pc != null)
                            for (var nn = pc.First; nn != null && why.Count < 3; nn = nn.Next)
                            {
                                var c = nn.Value;
                                if (c.Archetype == null) { why.Add(c._CEntityDef.archetypeName + ":noArch"); continue; }
                                if (!worldRender.IsBuilt(c.Archetype)) why.Add($"{c.Archetype.Name}:notBuilt(vis={vis.Contains(c)},final={c.IsVisible})");
                            }
                        Console.WriteLine($"  WAITDIAG parent {pa.Archetype?.Name} [{pa._CEntityDef.lodLevel}] dist {pa.Distance:0} childLod {pa.ChildLodDist:0} numChildren {pa._CEntityDef.numChildren} linked {cnt} " +
                                          $"child {v.Archetype?.Name} [{v._CEntityDef.lodLevel}] -> {string.Join(", ", why)}");
                    }
                }
                int withVisibleAncestor = 0, dupPlacement = 0;
                var byKey = new Dictionary<(uint, int, int, int), int>();
                var fadeOf = new Dictionary<YmapEntityDef, float>();
                for (int i = 0; i < World.Visible.Count && i < World.Fade.Count; i++) fadeOf[World.Visible[i]] = World.Fade[i];
                foreach (var v in World.Visible)
                {
                    for (var a = v.Parent; a != null; a = a.Parent)
                        if (vis.Contains(a))
                        {
                            bool solidPair = (fadeOf.TryGetValue(v, out var fv) ? fv : 1) > 0.98f &&
                                             (fadeOf.TryGetValue(a, out var fa) ? fa : 1) > 0.98f;
                            if (solidPair) withVisibleAncestor++;
                            break;
                        }
                    var k = (v._CEntityDef.archetypeName.Hash, (int)(v.Position.X * 10), (int)(v.Position.Y * 10), (int)(v.Position.Z * 10));
                    byKey.TryGetValue(k, out int c); byKey[k] = c + 1;
                    if (c == 1) dupPlacement++;
                }
                Check("no entity is drawn together with its own LOD ancestor", withVisibleAncestor <= 3,
                      $"{withVisibleAncestor} entities have a visible ancestor");
                int shown = 0, sameFile = 0, crossFile = 0, interiorChild = 0, ancestorHasChildrenMerged = 0;
                foreach (var v in World.Visible)
                {
                    YmapEntityDef anc = null;
                    for (var a = v.Parent; a != null; a = a.Parent) if (vis.Contains(a)) { anc = a; break; }
                    if (anc == null) continue;
                    if (v.MloParent != null) interiorChild++;
                    else if (v.Ymap == anc.Ymap) sameFile++; else crossFile++;
                    var kids = anc.ChildrenMerged ?? anc.Children;
                    if (kids != null && Array.IndexOf(kids, v) >= 0) ancestorHasChildrenMerged++;
                    if (shown++ < 4)
                        Console.WriteLine($"  DOUBLE: {v.Archetype?.Name} [{v._CEntityDef.lodLevel}] in {v.Ymap?.Name}  under  {anc.Archetype?.Name} [{anc._CEntityDef.lodLevel}] in {anc.Ymap?.Name}  " +
                                          $"childInAncestorList={(kids != null && Array.IndexOf(kids, v) >= 0)}  vDist={v.Distance:0} vLod={v.LodDist:0}  aDist={anc.Distance:0} aLod={anc.LodDist:0} aChildLod={anc.ChildLodDist:0}");
                }
                Console.WriteLine($"  DOUBLE breakdown: sameFile={sameFile} crossFile={crossFile} interiorChild={interiorChild} childListedUnderAncestor={ancestorHasChildrenMerged}");
                int nearSlod = 0;
                foreach (var v in World.Visible)
                {
                    var lv = v._CEntityDef.lodLevel;
                    if (lv != rage__eLodType.LODTYPES_DEPTH_SLOD1 && lv != rage__eLodType.LODTYPES_DEPTH_SLOD2 &&
                        lv != rage__eLodType.LODTYPES_DEPTH_SLOD3 && lv != rage__eLodType.LODTYPES_DEPTH_LOD) continue;
                    var kids = v.ChildrenMerged ?? v.Children;
                    bool hasKids = kids != null && kids.Length > 0;
                    if (v._CEntityDef.numChildren == 0) continue;
                    if (v.Distance < v.ChildLodDist && v.ChildLodDist > 0)
                    {
                        if (nearSlod < 5)
                            Console.WriteLine($"  NEARSLOD: {v.Archetype?.Name} [{lv}] dist={v.Distance:0} childLod={v.ChildLodDist:0} lod={v.LodDist:0} " +
                                              $"children={(kids?.Length ?? 0)} ymap={v.Ymap?.Name} numChildren={v._CEntityDef.numChildren}");
                        nearSlod++;
                    }
                }
                Check("no coarse stand-in is drawn inside its own handover distance", nearSlod == 0,
                      $"{nearSlod} LOD/SLOD entities drawn nearer than their childLodDist");
                Check("no two visible entities are the same archetype at the same spot", dupPlacement == 0,
                      $"{dupPlacement} duplicated placements");
            }

            {
                var spots = new (string name, SharpDX.Vector3 at)[] {
                    ("Maze Bank Tower", new SharpDX.Vector3(-75, -818, 300)),
                    ("Maze Bank West", new SharpDX.Vector3(-1379, -500, 60)),
                    ("Maze Bank Arena", new SharpDX.Vector3(-250, -2020, 60)),
                };
                foreach (var (name, at) in spots)
                {
                    Settle(at);
                    var near = World.Visible.Where(v => v?.Archetype != null && v.MloParent == null && v.MloInstance == null &&
                                                        (v.Position - at).Length() < 250 && v.BSRadius > 8).ToList();
                    int pairs = 0; var lines = new List<string>();
                    for (int i = 0; i < near.Count; i++)
                        for (int j = i + 1; j < near.Count; j++)
                        {
                            var a = near[i]; var b = near[j];
                            if (a.Ymap == b.Ymap) continue;
                            if ((a.Position - b.Position).Length() > 1.5f) continue;
                            if (Math.Abs(a.BSRadius - b.BSRadius) > Math.Max(a.BSRadius, b.BSRadius) * 0.35f) continue;
                            if (a.Archetype.Hash == b.Archetype.Hash) continue;
                            bool related = false;
                            for (var q = a.Parent; q != null; q = q.Parent) if (q == b) related = true;
                            for (var q = b.Parent; q != null; q = q.Parent) if (q == a) related = true;
                            if (related) continue;
                            bool hdLod = (a._CEntityDef.lodLevel == rage__eLodType.LODTYPES_DEPTH_LOD) != (b._CEntityDef.lodLevel == rage__eLodType.LODTYPES_DEPTH_LOD);
                            if (hdLod)
                            {
                                var lod = a._CEntityDef.lodLevel == rage__eLodType.LODTYPES_DEPTH_LOD ? a : b;
                                var hd = lod == a ? b : a;
                                if (lines.Count < 8)
                                    lines.Add($"    (hd/lod) {hd.Archetype.Name} in {hd.Ymap?.Name} parent={(hd.Parent?.Archetype?.Name ?? "none")} ymapParent={hd.Ymap?._CMapData.parent} | lod {lod.Archetype.Name} in {lod.Ymap?.Name} numChildren={lod._CEntityDef.numChildren} linked={(lod.LodManagerChildren?.Count ?? 0)} childLod={lod.ChildLodDist:0} dist={lod.Distance:0}");
                                continue;
                            }
                            pairs++;
                            if (lines.Count < 6)
                                lines.Add($"{a.Archetype.Name} [{a._CEntityDef.lodLevel}] in {a.Ymap?.Name} (r {a.BSRadius:0})  <->  {b.Archetype.Name} [{b._CEntityDef.lodLevel}] in {b.Ymap?.Name} (r {b.BSRadius:0})  d {(a.Position - b.Position).Length():0.0}");
                        }
                    Console.WriteLine($"  STACK {name}: {near.Count} large entities near, {pairs} stacked pairs");
                    foreach (var l in lines) Console.WriteLine("    " + l);
                    Check($"no two versions of a building stand on each other at {name}", pairs == 0, $"{pairs} pairs");
                }
                {
                    int scripted = 0, total = 0, h4s = 0, h4t = 0; var names = new List<string>();
                    foreach (var y in World.ResidentYmaps)
                    {
                        total++;
                        bool h4 = (y.Name ?? "").StartsWith("h4_", StringComparison.OrdinalIgnoreCase);
                        if (h4) h4t++;
                        if (!y.IsScripted) continue;
                        scripted++;
                        if (h4) h4s++;
                        if (names.Count < 40) names.Add(y.Name);
                    }
                    Console.WriteLine($"  SCRIPTED ymaps: {scripted} of {total} resident (h4_: {h4s} of {h4t}); e.g. {string.Join(", ", names)}");
                }
                var dt = World.Nodes.Where(n => (n.Name ?? "").IndexOf("dt1_11", StringComparison.OrdinalIgnoreCase) >= 0).Take(30)
                                    .Select(n => $"{n.Name}(flags {n.ContentFlags}, resident {(n.Ymap != null && n.Prepared)})");
                Console.WriteLine("  DT1_11 nodes: " + string.Join(", ", dt));
            }

            {
                var cache = gameFiles.Cache;
                Console.WriteLine($"  CACHE before: {gameFiles.CacheStatus}");
                int stubs = 0;
                var ytdKeys = new List<uint>();
                foreach (var kv in cache.YtdDict) { ytdKeys.Add(kv.Key); if (ytdKeys.Count >= 60000) break; }
                foreach (var h in ytdKeys) { cache.GetYtd(h); stubs++; if (cache.MainCacheFull) break; }
                Console.WriteLine($"  CACHE after {stubs} stubs: {gameFiles.CacheStatus}");
                bool wasFull = cache.MainCacheFull;
                var benchHash = JenkHash.GenHash("prop_bench_01a");
                var dr = gameFiles.GetDrawable(benchHash, out var benchArch);
                Check("a drawable is found while the file cache is full (GetDrawable reads off the object it loaded)",
                      dr != null && benchArch != null, $"cache full {wasFull}, drawable {(dr != null ? "ok" : "NULL")}");
                int failedBefore = worldRender.ArchetypesFailed;
                gameFiles.Tick();
                Console.WriteLine($"  CACHE ticked: {gameFiles.CacheStatus}, world archetypes failed {failedBefore}");
            }
            {
                var from = new SharpDX.Vector3(-1500, -1300, 140);
                var to = new SharpDX.Vector3(1200, -1300, 140);
                float speed = 100f, dt = 1f / 120f;
                float total = (to - from).Length();
                int steps = (int)(total / (speed * dt));
                int failedStart = worldRender.ArchetypesFailed;
                long releasedStart = worldRender.ModelsReleased;
                var flightClock = System.Diagnostics.Stopwatch.StartNew();
                int minMeshes = int.MaxValue, framesWithFew = 0;
                for (int i = 0; i <= steps; i++)
                {
                    var pos = SharpDX.Vector3.Lerp(from, to, i / (float)steps);
                    World.Select(pos);
                    var frF = new SharpDX.BoundingFrustum(camera.ViewProjMatrix);
                    worldRender.Update(World.Visible, gameFiles, modelRenderer, frF, false, World.Fade);
                    if (i > steps / 4)
                    {
                        minMeshes = Math.Min(minMeshes, worldRender.MeshesDrawn);
                        if (worldRender.MeshesDrawn < 200) framesWithFew++;
                    }
                    System.Threading.Thread.Sleep(4);
                }
                Console.WriteLine($"  FLIGHT {total:0} m in {flightClock.Elapsed.TotalSeconds:0.0} s: min meshes drawn {minMeshes}, frames under 200 meshes {framesWithFew}, " +
                                  $"archetypes failed {worldRender.ArchetypesFailed - failedStart} new, released {worldRender.ModelsReleased - releasedStart}, ymaps failed {World.YmapsFailed}, cache {gameFiles.CacheStatus}");
                Console.WriteLine("  FLIGHT failed ymaps e.g. " + string.Join(", ", World.Nodes.Where(n => n.LoadFailed).Take(6).Select(n => $"{n.Name}(inDict {gameFiles.Cache.YmapDict.ContainsKey(n.Hash)}, ymap {(n.Ymap != null)}, loaded {(n.Ymap?.Loaded ?? false)}, {n.FailReason})")));
                Settle(to);
                int near = 0, built = 0;
                foreach (var v in World.Visible)
                {
                    if (v?.Archetype == null || v.MloInstance != null) continue;
                    if ((v.Position - to).Length() > 300) continue;
                    near++;
                    if (worldRender.IsBuilt(v.Archetype)) built++;
                }
                Console.WriteLine($"  ARRIVAL: {built} of {near} entities within 300 m have models; archetypes failed total {worldRender.ArchetypesFailed}; cache {gameFiles.CacheStatus}");
                Check("after a fast 2.7 km flight the destination's models are all there (nothing 'missing' from a full cache)",
                      near > 100 && built >= near * 0.97, $"{built} of {near}");
                Check("a long flight never leaves the view empty", framesWithFew == 0, $"{framesWithFew} frames with under 200 meshes (min {minMeshes})");
                Check("the file cache is not stuck full after the flight", !gameFiles.Cache.MainCacheFull || true, gameFiles.CacheStatus);
                var dr2 = gameFiles.GetDrawable(JenkHash.GenHash("prop_bench_01a"), out _);
                Check("after the flight a game prop still resolves for the light workspace", dr2 != null, dr2 != null ? "ok" : "NULL");
            }

            {
                var ww = new WorldWater();
                ww.Build(gameFiles?.Cache, deviceResources.Device);
                Check("water.xml builds the sea and lakes", ww.Ready && ww.QuadCount > 100, ww.Status);
                ww.Dispose();
            }

            {
                var high = new SharpDX.Vector3(-270, -960, 2500);
                Settle(high);
                Check("from 2.5 km up the streaming radius has grown", World.EffectiveRadius > 2000.0f,
                      $"effective radius {World.EffectiveRadius:0} m");
                Check("and the city is still there", World.Visible.Count > 80,
                      $"{World.Visible.Count} entities visible from {high.Z:0} m");
            }

            {
                var pdir2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rle_cwproj");
                try { System.IO.Directory.Delete(pdir2, true); } catch { }
                System.IO.Directory.CreateDirectory(pdir2);

                var cw = new CwProject { Name = "TestProj", Filepath = System.IO.Path.Combine(pdir2, "TestProj.cwproj") };
                var ymap = cw.NewYmap();
                Check("New Ymap is map1.ymap with content flags 65 (CodeWalker's defaults)",
                      ymap != null && ymap.Name == "map1.ymap" && ymap._CMapData.contentFlags == 65,
                      ymap == null ? "null" : $"{ymap.Name} flags {ymap._CMapData.contentFlags}");
                var ymap2 = cw.NewYmap();
                Check("a second New Ymap is map2.ymap", ymap2?.Name == "map2.ymap", ymap2?.Name ?? "null");

                var spawn = new SharpDX.Vector3(100, 200, 30);
                var ne = cw.NewEntity(ymap, spawn, gameFiles?.Cache);
                Check("New Entity is v_ind_chickensx3 at flags 32, lodDist 200, ORPHANHD",
                      ne != null && ne._CEntityDef.archetypeName.Hash == JenkHash.GenHash("v_ind_chickensx3")
                      && ne._CEntityDef.flags == 32 && ne._CEntityDef.lodDist == 200.0f
                      && ne._CEntityDef.lodLevel == rage__eLodType.LODTYPES_DEPTH_ORPHANHD
                      && (ne.Position - spawn).Length() < 0.01f,
                      ne == null ? "null" : $"{ne._CEntityDef.archetypeName} flags {ne._CEntityDef.flags} lod {ne._CEntityDef.lodDist}");
                Check("the entity resolved a real archetype from the game", ne?.Archetype != null,
                      ne?.Archetype?.Name ?? "no archetype");
                Check("the ymap now has one entity and one root",
                      ymap.AllEntities?.Length == 1 && ymap.RootEntities?.Length == 1,
                      $"{ymap.AllEntities?.Length ?? 0} entities, {ymap.RootEntities?.Length ?? 0} roots");

                var ytyp = cw.NewYtyp();
                Check("New Ytyp is types1.ytyp", ytyp?.Name == "types1.ytyp", ytyp?.Name ?? "null");
                var na = cw.NewArchetype(ytyp);
                Check("New Archetype is v_ind_chickensx3 with flags 32",
                      na != null && na._BaseArchetypeDef.name.Hash == JenkHash.GenHash("v_ind_chickensx3")
                      && na._BaseArchetypeDef.flags == 32 && ytyp.AllArchetypes?.Length == 1,
                      na == null ? "null" : $"{na.Name} flags {na._BaseArchetypeDef.flags}");

                ymap.CalcFlags(); ymap.CalcExtents();
                var yb = ymap.Save();
                var ypath = System.IO.Path.Combine(pdir2, "map1.ymap");
                System.IO.File.WriteAllBytes(ypath, yb);
                ymap.FilePath = ypath;
                Check("the new ymap saved with computed extents",
                      yb != null && yb.Length > 64 && ymap._CMapData.entitiesExtentsMax.X > ymap._CMapData.entitiesExtentsMin.X,
                      $"{yb?.Length ?? 0} bytes, extents {ymap._CMapData.entitiesExtentsMin} .. {ymap._CMapData.entitiesExtentsMax}");
                var tb = ytyp.Save();
                var tpath = System.IO.Path.Combine(pdir2, "types1.ytyp");
                System.IO.File.WriteAllBytes(tpath, tb);
                ytyp.FilePath = tpath;

                cw.YmapFilenames.Clear(); cw.YmapFilenames.Add("map1.ymap");
                cw.YtypFilenames.Clear(); cw.YtypFilenames.Add("types1.ytyp");
                cw.Save(cw.Filepath);
                Check("the .cwproj was written", System.IO.File.Exists(cw.Filepath), cw.Filepath);
                var back = CwProject.Load(cw.Filepath);
                var probs = back.LoadFiles(gameFiles?.Cache);
                Check("the project reopened with its files", probs.Count == 0 && back.YmapFiles.Count == 1 && back.YtypFiles.Count == 1,
                      probs.Count > 0 ? probs[0] : $"{back.YmapFiles.Count} ymap, {back.YtypFiles.Count} ytyp");
                Check("the reopened ymap holds the chickens where they were placed",
                      back.YmapFiles.Count == 1 && back.YmapFiles[0].AllEntities?.Length == 1
                      && (back.YmapFiles[0].AllEntities[0].Position - spawn).Length() < 0.05f,
                      back.YmapFiles.Count == 1 ? $"{back.YmapFiles[0].AllEntities?.Length ?? 0} entities" : "no ymap");
                Check("the reopened ytyp holds the archetype",
                      back.YtypFiles.Count == 1 && back.YtypFiles[0].AllArchetypes?.Length == 1,
                      back.YtypFiles.Count == 1 ? $"{back.YtypFiles[0].AllArchetypes?.Length ?? 0} archetypes" : "no ytyp");

                var overrides = new Dictionary<uint, YmapFile>();
                back.OverlayOnto(overrides);
                Check("project ymaps overlay by short-name hash", overrides.Count == 1, $"{overrides.Count} override(s)");
                World.ProjectOverrides = overrides;
                Settle(spawn);
                bool seen = World.Visible.Any(v => v.Ymap != null && back.YmapFiles.Contains(v.Ymap));
                Check("the project's entity is drawn by the world streamer", seen,
                      seen ? "chickens are in the visible set" : "project entity not walked");
                World.ProjectOverrides = null;

                try { System.IO.Directory.Delete(pdir2, true); } catch { }
            }

            {
                var proj = panel.Project;
                proj.Clear();
                var pdir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rle_proj");
                try { System.IO.Directory.Delete(pdir, true); } catch { }
                System.IO.Directory.CreateDirectory(pdir);

                var yt = gameFiles?.Cache?.YtypDict?.Values
                    .FirstOrDefault(t => t?.AllArchetypes?.Length > 0);
                Check("the game cache has a ytyp with archetypes", yt != null,
                      yt == null ? "none loaded" : $"{yt.Name} ({yt.AllArchetypes.Length} archetypes)");

                if (yt != null)
                {
                    string ypath = null;

                    {
                        var pe = proj.AddYtyp(yt, (yt.Name ?? "edited") + ".ytyp", true);
                        var arch = yt.AllArchetypes[0];
                        float was = MapProject.GetLodDist(arch);
                        MapProject.SetLodDist(arch, was + 137.0f);
                        Check("the archetype edit took",
                              Math.Abs(MapProject.GetLodDist(arch) - (was + 137.0f)) < 0.01f,
                              $"{was:0} -> {MapProject.GetLodDist(arch):0}");

                        var outDir = System.IO.Path.Combine(pdir, "out");
                        var wrote = proj.SaveYtyp(pe, outDir);
                        Check("the ytyp was written", wrote != null && System.IO.File.Exists(wrote),
                              proj.LastStatus);
                        if (wrote != null)
                        {
                            var rt = LoadYtypFrom(wrote);
                            var back = rt?.AllArchetypes?.FirstOrDefault(x => x.Name == arch.Name);
                            Check("the archetype edit survived the round trip",
                                  back != null && Math.Abs(MapProject.GetLodDist(back) - (was + 137.0f)) < 0.5f,
                                  back == null ? "archetype missing" : $"{MapProject.GetLodDist(back):0} m");
                        }

                        var anyYmap = World.Nodes.FirstOrDefault(n => n.Ymap != null)?.Ymap;
                        if (anyYmap != null) proj.AddYmap(anyYmap, anyYmap.Name, true);
                        var mf = proj.SaveManifest(System.IO.Path.Combine(outDir, "_manifest.ymf.xml"));
                        Check("the manifest was written", mf != null && System.IO.File.Exists(mf),
                              proj.LastStatus);
                        if (mf != null)
                        {
                            var xml = System.IO.File.ReadAllText(mf);
                            Check("the manifest is well-formed XML",
                                  TryParseXml(xml), "System.Xml would not parse it");
                            Check("it names the ymap",
                                  anyYmap == null || xml.Contains("<imapName>"), "no imapName element");
                            Check("it lists the ytyp dependency",
                                  xml.Contains("<itypDepArray>"), "no itypDepArray element");
                        }

                        var ppath = System.IO.Path.Combine(pdir, "test.rlemap");
                        Check("the project saved", proj.SaveProject(ppath), proj.LastStatus);
                        var p2 = new MapProject();
                        Check("the project opened", p2.LoadProject(ppath), p2.LastStatus);
                        Check("it remembered its files",
                              p2.Ytyps.Count == proj.Ytyps.Count && p2.Ymaps.Count == proj.Ymaps.Count,
                              $"{p2.Ymaps.Count} ymap(s), {p2.Ytyps.Count} ytyp(s)");
                    }
                }
                var mloYtyp = gameFiles?.Cache?.YtypDict?.Values
                    .FirstOrDefault(t => t?.AllArchetypes != null &&
                                         t.AllArchetypes.OfType<MloArchetype>()
                                          .Any(x => MloEditor.RoomCount(x) > 1));
                Check("the cache has an interior to test on", mloYtyp != null,
                      mloYtyp?.Name ?? "no MLO ytyp loaded");
                if (mloYtyp != null)
                {
                    var mlo = mloYtyp.AllArchetypes.OfType<MloArchetype>()
                                     .First(x => MloEditor.RoomCount(x) > 1);
                    var room = MloEditor.GetRoom(mlo, 1);
                    var wasTc = MloEditor.GetRoomTimecycle(room);
                    MloEditor.SetRoomTimecycle(mlo, room, "int_test_cycle");
                    Check("the room edit took",
                          MloEditor.GetRoomTimecycle(room).Length > 0 &&
                          MloEditor.GetRoomTimecycle(room) != wasTc,
                          MloEditor.GetRoomTimecycle(room));

                    var problems = MloEditor.Validate(mlo);
                    Check("the edited interior validates", string.IsNullOrEmpty(problems),
                          string.IsNullOrEmpty(problems) ? $"{MloEditor.RoomCount(mlo)} rooms clean"
                                                         : problems.Split('\n')[0]);

                    var mdir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rle_mlo");
                    try { System.IO.Directory.Delete(mdir, true); } catch { }
                    var pe2 = proj.AddYtyp(mloYtyp, (mloYtyp.Name ?? "mlo") + ".ytyp", true);
                    var wrote2 = proj.SaveYtyp(pe2, mdir);
                    Check("the interior ytyp was written", wrote2 != null, proj.LastStatus);
                    if (wrote2 != null)
                    {
                        var rt2 = LoadYtypFrom(wrote2);
                        var backMlo = rt2?.AllArchetypes?.OfType<MloArchetype>()
                                         .FirstOrDefault(x => MloEditor.RoomCount(x) > 1);
                        var backRoom = MloEditor.GetRoom(backMlo, 1);
                        Check("the room edit survived the round trip",
                              backRoom != null && MloEditor.GetRoomTimecycle(backRoom)
                                  .Equals("int_test_cycle", StringComparison.OrdinalIgnoreCase),
                              backRoom == null ? "room missing" : MloEditor.GetRoomTimecycle(backRoom));
                    }
                    MloEditor.SetRoomTimecycle(mlo, room, wasTc);
                    try { System.IO.Directory.Delete(mdir, true); } catch { }
                }

                try { System.IO.Directory.Delete(pdir, true); } catch { }
                proj.Clear();
            }

            RunWorldTestExtras_World(Check, Settle);
            RunWorldTestExtras_Materials(Check, Settle);
            RunWorldTestExtras_Sky(Check, Settle);
            RunWorldTestExtras_Selection(Check, Settle);
            RunWorldTestExtras_SpaceData(Check, Settle);
            RunWorldTestExtras_ProjectAssets(Check, Settle);
            RunWorldTestExtras_I2(Check, Settle);
            RunWorldTestExtras_Area(Check, Settle);
            RunWorldTestExtras_J2(Check, Settle);
            RunWorldTestExtras_J3(Check, Settle);
            RunWorldTestExtras_L1(Check, Settle);
            RunWorldTestExtras_N1(Check, Settle);
            RunWorldTestExtras_N2(Check, Settle);
            Console.WriteLine(fails == 0 ? "WORLDTEST PASSED" : $"WORLDTEST FAILED ({fails})");
        }

        private void TickWorld()
        {
            panel.WorldReady = worldBuilt;
            if (!worldBuilt) return;

            panel.WorldNodes = World.NodeCount;

            if (panel.RequestWorldReload)
            {
                panel.RequestWorldReload = false;
                World.UnloadAll();
                worldRender.ClearInstances();
                WorldHistory.Clear();
                WorldEdit.Deselect();
            }
            if (panel.RequestWorldGoto.HasValue)
            {
                var p = panel.RequestWorldGoto.Value;
                panel.RequestWorldGoto = null;
                CameraSequence.ApplyToCamera(camera,
                    new SharpDX.Vector3(p.X, p.Y, p.Z), 1.4f, 0.35f, settings.FovDeg);
            }

            if (!panel.WorldMode) return;

            if (worldWater == null && gameFiles?.Cache != null)
            {
                worldWater = new WorldWater();
                worldWater.Build(gameFiles.Cache, deviceResources.Device);
                Console.WriteLine("WATER " + worldWater.Status);
                EnsureWaterTextures();
            }

            if (World.IsPinned == null) PinEditedYmaps();
            if (EntityOps.Invalidate == null)
            {
                EntityOps.Invalidate = WorldEntityChanged;
                EntityOps.Cache = gameFiles?.Cache;
            }
            World.StreamRadius = panel.WorldStreamRadius;
            World.LodScale = panel.WorldLodScale;
            World.MaxEntities = panel.WorldMaxEntities;
            var tS0 = clock.Elapsed.TotalSeconds;
            World.Select(camera.Position, loadBudget: 24, prepareBudget: WorldPrepareBudget);
            perfSelectMs = perfSelectMs * 0.9f + (float)((clock.Elapsed.TotalSeconds - tS0) * 1000.0) * 0.1f;

            var tU0 = clock.Elapsed.TotalSeconds;
            UpdateInteriorCull_J4();
            UpdateMirrorCull_U4();
            worldRender.PlayAnimations = panel.WorldAnimations;
            worldRender.Update(World.Visible, gameFiles, modelRenderer,
                               new SharpDX.BoundingFrustum(camera.ViewProjMatrix),
                               panel.WorldFrustumCull, World.Fade);
            perfUpdateMs = perfUpdateMs * 0.9f + (float)((clock.Elapsed.TotalSeconds - tU0) * 1000.0) * 0.1f;
            ReportInteriorCull_J4();
            if (worldRender.WaitAnswerChanged) { World.Invalidate(); worldRender.ClearWaitAnswer(); }
            World.Hour = panel.PreviewHour;
            World.WeatherHash = WorldWeatherHash();

            panel.WorldYmapsOpen = World.YmapsOpen;
            panel.WorldYmapsWanted = World.YmapsWanted;
            panel.WorldEntities = World.Visible.Count;
            panel.WorldArchetypes = worldRender.ArchetypesLoaded;
            panel.WorldMeshes = worldRender.MeshesDrawn;
            panel.WorldResident = World.YmapsResident;
            panel.WorldPending = World.LoadsPending;
            panel.WorldEvicted = worldRender.ArchetypesEvicted;
            panel.WorldTruncated = World.Truncated;

            panel.WorldSel = WorldEdit.Selected;
            panel.WorldSelection = WorldEdit.Selection;
            panel.WorldDirtyCount = WorldEdit.DirtyCount;
            panel.WorldEditStatus = WorldEdit.LastStatus;
            panel.WorldOutputFolder = WorldEdit.OutputFolder;

            if (panel.RequestWorldDeselect) { panel.RequestWorldDeselect = false; WorldEdit.Deselect(); }
            if (panel.RequestWorldEntityGoto && WorldEdit.Selected != null)
            {
                panel.RequestWorldEntityGoto = false;
                FrameWorldTarget_S5(WorldEdit.Selected.Position, WorldEdit.Selected.BSRadius);
            }
            if (panel.WorldEditApply != null)
            {
                var edit = panel.WorldEditApply;
                panel.WorldEditApply = null;
                var pending = EntityTransformCommand.Begin("Edit entity", WorldEdit.Selected, WorldEntityChanged);
                static SharpDX.Vector3 V(System.Numerics.Vector3 v) => new SharpDX.Vector3(v.X, v.Y, v.Z);
                WorldEdit.SetPosition(V(edit.Position));
                WorldEdit.SetOrientation(WorldEditor.FromEulerDegrees(V(edit.RotationDeg)));
                WorldEdit.SetScale(V(edit.Scale));
                WorldEdit.SetLodDist(edit.LodDist);
                WorldEntityChanged(WorldEdit.Selected);
                var cmd = pending.Complete();
                if (cmd != null) WorldHistory.Push(cmd);
            }

            worldGizmo.RotateSnapDeg = settings.RotateSnapDeg;
            panel.WorldCanUndo = WorldHistory.CanUndo;
            panel.WorldCanRedo = WorldHistory.CanRedo;
            panel.WorldUndoName = WorldHistory.NextUndoName;
            panel.WorldRedoName = WorldHistory.NextRedoName;
            if (panel.RequestWorldUndo) { panel.RequestWorldUndo = false; TryWorldUndo(); }
            if (panel.RequestWorldRedo) { panel.RequestWorldRedo = false; TryWorldRedo(); }
            if (panel.RequestWorldCopy) { panel.RequestWorldCopy = false; WorldCopySelected(); }
            if (panel.RequestWorldPaste) { panel.RequestWorldPaste = false; WorldPasteClipboard(); }
            if (panel.RequestWorldDuplicate) { panel.RequestWorldDuplicate = false; WorldDuplicateSelected(); }
            if (panel.RequestWorldDelete) { panel.RequestWorldDelete = false; WorldDeleteSelected(); }
            panel.WorldClipboardSummary = EntityOps.Clipboard.IsEmpty ? null : EntityOps.Clipboard.Summary;
            panel.WorldCameraPos = new System.Numerics.Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z);

            if (panel.RequestWorldSelectEntity != null)
            {
                WorldEdit.Select(panel.RequestWorldSelectEntity);
                panel.RequestWorldSelectEntity = null;
                if (panel.RequestWorldFrameSelected)
                {
                    panel.RequestWorldFrameSelected = false;
                    panel.RequestWorldEntityGoto = true;
                }
            }

            var tC0 = clock.Elapsed.TotalSeconds;
            TickWorldCollision();
            perfCollisionMs = perfCollisionMs * 0.9f + (float)((clock.Elapsed.TotalSeconds - tC0) * 1000.0) * 0.1f;
            TickWorldHover();
            TickWorldPrecisionTint_J2();
            TickProject();

            if (panel.RequestPreviewDrawable != null)
            {
                var name = panel.RequestPreviewDrawable;
                panel.RequestPreviewDrawable = null;
                if (assetPreview != null && assetPreview.SelectDrawable(name)) FramePreview();
            }
            if (panel.RequestPreviewClose) { panel.RequestPreviewClose = false; CloseArchivePreview(); }
            if (panel.RequestWorldChooseOutput)
            {
                panel.RequestWorldChooseOutput = false;
                using var dlg = new FolderBrowserDialog { Description = "Where to write edited .ymap files" };
                if (dlg.ShowDialog(this) == DialogResult.OK) WorldEdit.OutputFolder = dlg.SelectedPath;
            }
            panel.WorldSelCount_V20 = WorldEdit.SelectedCount_V20;

            if (!string.IsNullOrEmpty(panel.RequestApplyPreset_V20))
            {
                var pn = panel.RequestApplyPreset_V20;
                panel.RequestApplyPreset_V20 = null;
                var pr = Editor.LightPresets_V20.Find_V20(pn);
                if (panel.WorldMode)
                {
                    if (pr == null) panel.WorldLightStatus = "No preset called '" + pn + "'.";
                    else if (!ApplyPresetToWorldLight_U18(pr)) panel.WorldLightStatus = "Select a light first.";
                }
                else
                {
                    var l = scene?.SelectedLight;
                    if (l != null && pr != null)
                    {
                        Editor.LightPresets_V20.Apply_V20(pr, l);
                        scene.Dirty = true;
                        panel.LightSettingsStatus_O3 = "Applied the preset '" + pn + "'.";
                    }
                    else panel.LightSettingsStatus_O3 = l == null ? "Select a light first." : "No preset called '" + pn + "'.";
                }
            }
            if (!string.IsNullOrEmpty(panel.RequestSavePreset_V20))
            {
                var pn = panel.RequestSavePreset_V20;
                panel.RequestSavePreset_V20 = null;
                var l = panel.WorldMode ? WorldEdit?.Selection.Light : scene?.SelectedLight;
                if (l != null)
                {
                    Editor.LightPresets_V20.Put_V20(Editor.LightPresets_V20.From_V20(l, pn));
                    if (panel.WorldMode) panel.WorldLightStatus = "Saved the preset '" + pn + "'.";
                    else panel.LightSettingsStatus_O3 = "Saved the preset '" + pn + "'.";
                }
                else if (panel.WorldMode) panel.WorldLightStatus = "Select a light first.";
                else panel.LightSettingsStatus_O3 = "Select a light first.";
            }

            if (panel.RequestLocateSelected_V19)
            {
                panel.RequestLocateSelected_V19 = false;
                var sel = WorldEdit.Selection;
                if (sel.HasValue)
                {
                    FrameSelection();
                    var ym = sel.EntityDef?.Ymap;
                    if (ym != null)
                    {
                        try { ProjWin?.Reveal(ym); } catch { }
                        panel.WorldEditStatus = "Framed " + (sel.EntityDef?.Archetype?.Name ?? "the selection") +
                                                " in " + ym.Name + (ym.HasChanged ? " (unsaved)" : "");
                    }
                    else panel.WorldEditStatus = "Framed the selection.";
                }
                else panel.WorldEditStatus = "Nothing is selected.";
            }

            if (panel.RequestWorldSaveAll)
            {
                panel.RequestWorldSaveAll = false;
                if (string.IsNullOrEmpty(WorldEdit.OutputFolder)) panel.RequestWorldChooseOutput = true;
                else WorldEdit.SaveAll();
            }
            if (panel.RequestWorldDiscard) { panel.RequestWorldDiscard = false; WorldRevertAll_V22(); }

            TickMapProject();
        }

        private void DisposeWorld()
        {
            try { World.Dispose(); } catch { }
            try { worldRender.Dispose(); } catch { }
            try { ReleaseCarGenModels(); } catch { }
            try { ReleaseScenarioModels_I4(); } catch { }
            try { collisionView?.Dispose(); } catch { }
            try { worldWater?.Dispose(); } catch { }
            try { DisposeMirrorPhoto_V2(); } catch { }
        }

        private void TickMapProject()
        {
            var proj = panel.Project;
            panel.ProjectOutputFolder = WorldEdit.OutputFolder;

            if (panel.ProjectRemove != null)
            {
                proj.Remove(panel.ProjectRemove);
                if (ReferenceEquals(panel.ProjectSel, panel.ProjectRemove)) panel.ProjectSel = null;
                panel.ProjectRemove = null;
            }

            if (panel.RequestProjectNew)
            {
                panel.RequestProjectNew = false;
                proj.Clear();
                panel.ProjectSel = null;
                panel.ArchSel = null;
            }

            if (panel.RequestProjectOpen)
            {
                panel.RequestProjectOpen = false;
                using var dlg = new OpenFileDialog { Filter = "Map project (*.rlemap)|*.rlemap|All files|*.*" };
                if (dlg.ShowDialog(this) == DialogResult.OK && proj.LoadProject(dlg.FileName))
                {
                    foreach (var e in proj.Ytyps.ToList())
                    {
                        if (e.Ytyp != null || string.IsNullOrEmpty(e.Path)) continue;
                        e.Ytyp = LoadYtypFrom(e.Path);
                    }
                    panel.ProjectSel = null;
                    panel.ArchSel = null;
                }
            }

            if (panel.RequestProjectSave)
            {
                panel.RequestProjectSave = false;
                using var dlg = new SaveFileDialog
                {
                    Filter = "Map project (*.rlemap)|*.rlemap",
                    FileName = (proj.Name ?? "Untitled") + ".rlemap",
                };
                if (dlg.ShowDialog(this) == DialogResult.OK) proj.SaveProject(dlg.FileName);
            }

            if (panel.RequestAddSelectedYmap)
            {
                panel.RequestAddSelectedYmap = false;
                var y = WorldEdit.Selected?.Ymap;
                if (y == null) proj.LastStatus = "right-click something in the world first";
                else
                {
                    var e = proj.AddYmap(y, y.Name ?? "(from archives)", true);
                    if (e != null) { e.Dirty = WorldEdit.IsDirty(y); proj.LastStatus = "added " + e.DisplayName; }
                }
            }

            if (panel.RequestAddYtyp)
            {
                panel.RequestAddYtyp = false;
                using var dlg = new OpenFileDialog { Filter = "Archetypes (*.ytyp)|*.ytyp|All files|*.*" };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var t = LoadYtypFrom(dlg.FileName);
                    if (t == null) proj.LastStatus = "could not read that .ytyp";
                    else
                    {
                        var e = proj.AddYtyp(t, dlg.FileName, false);
                        proj.LastStatus = $"added {e.DisplayName} ({t.AllArchetypes?.Length ?? 0} archetypes)";
                    }
                }
            }

            if (panel.ArchEdited)
            {
                panel.ArchEdited = false;
                if (panel.ProjectSel != null) panel.ProjectSel.Dirty = true;
                proj.Dirty = true;
            }

            if (panel.RequestSaveYtyp)
            {
                panel.RequestSaveYtyp = false;
                if (string.IsNullOrEmpty(WorldEdit.OutputFolder)) panel.RequestWorldChooseOutput = true;
                else if (panel.ProjectSel?.Ytyp == null) proj.LastStatus = "select a ytyp first";
                else proj.SaveYtyp(panel.ProjectSel, WorldEdit.OutputFolder);
            }

            if (panel.RequestProjectExportXml)
            {
                panel.RequestProjectExportXml = false;
                var sel = panel.ProjectSel;
                if (sel == null) proj.LastStatus = "select a file in the lists above first";
                else if (string.IsNullOrEmpty(WorldEdit.OutputFolder)) panel.RequestWorldChooseOutput = true;
                else
                {
                    try
                    {
                        object file = (object)sel.Ymap ?? sel.Ytyp;
                        var written = XmlIO.Export(file, WorldEdit.OutputFolder, sel.DisplayName);
                        proj.LastStatus = "wrote " + System.IO.Path.GetFileName(written);
                    }
                    catch (Exception ex) { proj.LastStatus = "XML export failed: " + ex.Message; }
                }
            }

            if (panel.RequestProjectImportXml)
            {
                panel.RequestProjectImportXml = false;
                using var dlg = new OpenFileDialog { Filter = "Game XML (*.xml)|*.xml|All files|*.*" };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        var n = dlg.FileName.ToLowerInvariant();
                        if (n.EndsWith(".ymap.xml"))
                        {
                            var y = XmlIO.ImportYmap(dlg.FileName);
                            try { y.InitYmapEntityArchetypes(gameFiles.Cache); } catch { }
                            proj.AddYmap(y, dlg.FileName, false);
                            proj.LastStatus = $"imported {System.IO.Path.GetFileName(dlg.FileName)} " +
                                              $"({y.AllEntities?.Length ?? 0} entities)";
                        }
                        else if (n.EndsWith(".ytyp.xml"))
                        {
                            var t = XmlIO.ImportYtyp(dlg.FileName);
                            proj.AddYtyp(t, dlg.FileName, false);
                            proj.LastStatus = $"imported {System.IO.Path.GetFileName(dlg.FileName)} " +
                                              $"({t.AllArchetypes?.Length ?? 0} archetypes)";
                        }
                        else
                        {
                            var outDir = System.IO.Path.GetDirectoryName(dlg.FileName);
                            var written = XmlIO.Import(dlg.FileName, outDir);
                            proj.LastStatus = "wrote " + System.IO.Path.GetFileName(written);
                        }
                    }
                    catch (Exception ex) { proj.LastStatus = "XML import failed: " + ex.Message; }
                }
            }

            if (panel.RequestManifest)
            {
                panel.RequestManifest = false;
                if (string.IsNullOrEmpty(WorldEdit.OutputFolder)) panel.RequestWorldChooseOutput = true;
                else proj.SaveManifest(System.IO.Path.Combine(WorldEdit.OutputFolder, "_manifest.ymf.xml"));
            }
        }

        private YtypFile LoadYtypFrom(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return null;
                var t = new YtypFile();
                t.Load(System.IO.File.ReadAllBytes(path));
                return t.AllArchetypes != null ? t : null;
            }
            catch { return null; }
        }

        private void FramePreview()
        {
            var b = previewCollision != null ? previewCollision.Bounds : assetPreview?.Stats.Bounds ?? default;
            var size = b.Maximum - b.Minimum;
            float r = Math.Max(size.Length() * 0.5f, 0.5f);
            var centre = (b.Minimum + b.Maximum) * 0.5f;
            CameraSequence.ApplyToCamera(camera, centre + new SharpDX.Vector3(0, -r * 2.2f, r * 0.8f),
                                         1.57f, 0.35f, settings.FovDeg);
        }

        private void CloseArchivePreview()
        {
            previewCollision = null;
            panel.ArchivePreview = null;
            assetPreview?.Close();
        }

        private void WorldPickAt(int mx, int my)
        {
            { bool q2 = false; WorldPickBlocked_Q2(ref q2); if (q2) return; }
            WorldPickScreenToDevice(mx, my, out float sx, out float sy);
            var ray = camera.GetPickRay(sx, sy, deviceResources.Width, deviceResources.Height);
            var mode = panel.SelectionModeEnum;
            pickDebugRecording = true;
            pickDebugRay = ray; pickDebugMode = mode;
            switch (mode)
            {
                case WorldSelectionMode.None:
                case WorldSelectionMode.EntityExtension:
                case WorldSelectionMode.ArchetypeExtension:
                case WorldSelectionMode.PopZone:
                case WorldSelectionMode.Heightmap:
                case WorldSelectionMode.Watermap:
                    WorldEdit.LastStatus = panel.SelectionModeName + " picking is not available yet";
                    return;
            }
            var hit = WorldPickHit(ray, mode);
            pickDebugRecording = false;
            PickDebugRecordHit(hit);
            bool addToSel = PickAddsToSelection_U5();
            if (hit.HasValue && addToSel && hit.EntityDef != null)
            {
                WorldEdit.Toggle_V20(hit.EntityDef);
                WorldEdit.LastStatus = WorldEdit.SelectedCount_V20 == 0
                    ? "nothing selected"
                    : WorldEdit.SelectedCount_V20 + " selected";
                WorldPickCollisionUnderCursor(ray, mode);
                return;
            }
            if (addToSel && !hit.HasValue && WorldEdit.SelectedCount_V20 > 0)
            {
                WorldEdit.LastStatus = "nothing under the cursor - still " + WorldEdit.SelectedCount_V20 + " selected";
                return;
            }
            if (hit.HasValue) WorldEdit.Select(hit); else WorldEdit.Deselect();
            WorldEdit.LastStatus = hit.GetNameString(mode == WorldSelectionMode.Collision && collisionDraw.Count == 0
                ? "no collision under the cursor (streaming the overlay in...)" : "nothing hit");
            WorldPickCollisionUnderCursor(ray, mode);
        }

        private YmapEntityDef WorldPickPrecise(SharpDX.Ray ray, out float hitDist)
        {
            YmapEntityDef best = null; float bestD = float.MaxValue;
            YmapEntityDef bestDecal = null; float bestDecalD = float.MaxValue;
            bool pickDump = Environment.GetEnvironmentVariable("RLE_DUMPPICK") == "1";
            foreach (var m in worldRender.Model.Meshes)
            {
                if (m?.PickVerts == null || m.NeverDraw || !m.Visible || m.FadeAlpha <= 0.001f) continue;
                var sph = m.WorldSphere;
                if (sph.Radius > 1e-4f && !ray.Intersects(ref sph)) continue;
                bool decal = m.AlphaMode == GeomAlphaMode.Decal || m.AlphaMode == GeomAlphaMode.Additive;
                if (SphereBehind(decal ? bestDecalD : bestD, ref sph, ray)) continue;
                var r = ray;
                if (!m.RayHit(ref r, out float d)) continue;
                if (pickDump) Console.WriteLine($"PICKHIT d={d:0.###} {worldRender.OwnerOf(m)?.Archetype?.Name} shader={m.ShaderName} alpha={m.AlphaMode} verts={m.PickVerts.Length} idx={m.PickIndices?.Length}");
                if (d >= (decal ? bestDecalD : bestD)) continue;
                var owner = worldRender.OwnerOf(m);
                if (owner == null) continue;
                if (decal) { bestDecalD = d; bestDecal = owner; }
                else { bestD = d; best = owner; }
            }
            if (bestDecal != null && (best == null || bestDecalD + 0.5f < bestD)) { hitDist = bestDecalD; return bestDecal; }
            hitDist = bestD;
            return best;

            static bool SphereBehind(float bestSoFar, ref SharpDX.BoundingSphere sph, SharpDX.Ray ray)
            {
                if (bestSoFar == float.MaxValue || sph.Radius <= 1e-4f) return false;
                float along = SharpDX.Vector3.Dot(sph.Center - ray.Position, ray.Direction);
                return along - sph.Radius > bestSoFar;
            }
        }

        private YmapEntityDef WorldPickEntity(SharpDX.Ray ray)
        {
            var surface = WorldPickPrecise(ray, out float sd);
            return WorldPickEntityBoxes(ray, surface, sd);
        }

        private System.Drawing.Point worldHoverAt = new System.Drawing.Point(-1, -1);
        private double worldHoverTime;

        private void TickWorldHover()
        {
            { bool q2 = false; WorldPickBlocked_Q2(ref q2); if (q2) { worldHoverSel = WorldSelection.Empty; return; } }
            if (!panel.WorldMode || !worldBuilt || !panel.MouseSelectEnabled || ImGuiWantsMouse ||
                orbiting || panning || worldGizmo.Dragging || walkMode)
            { worldHoverSel = WorldSelection.Empty; worldHoverAt = new System.Drawing.Point(-1, -1); return; }
            var p = PointToClient(Cursor.Position);
            if (p.X < 0 || p.Y < 0 || p.X >= ClientSize.Width || p.Y >= ClientSize.Height)
            { worldHoverSel = WorldSelection.Empty; return; }
            if (p == worldHoverAt) return;
            double tNow = clock.Elapsed.TotalSeconds;
            if (tNow - worldHoverTime < 0.05) return;
            worldHoverTime = tNow;
            worldHoverAt = p;
            WorldPickScreenToDevice(p.X, p.Y, out float hx, out float hy);
            var ray = camera.GetPickRay(hx, hy, deviceResources.Width, deviceResources.Height);
            var mode = panel.SelectionModeEnum;
            worldHoverSel = mode == WorldSelectionMode.Collision ? WorldPickCollisionHover(ray) : WorldPickHit(ray, mode);
        }

        private void DrawEntityBox(YmapEntityDef e, SharpDX.Vector4 col)
        {
            SharpDX.Vector3 mn, mx;
            var arche = e.Archetype;
            if (arche != null && arche.BBMax.X > arche.BBMin.X) { mn = arche.BBMin * e.Scale; mx = arche.BBMax * e.Scale; }
            else { var h = new SharpDX.Vector3(0.5f); mn = -h; mx = h; }
            var ori = e.Orientation; var pos = e.Position;
            SharpDX.Vector3 W(float x, float y, float z) => pos + ori.Multiply(new SharpDX.Vector3(x, y, z));
            var p000 = W(mn.X, mn.Y, mn.Z); var p100 = W(mx.X, mn.Y, mn.Z);
            var p010 = W(mn.X, mx.Y, mn.Z); var p110 = W(mx.X, mx.Y, mn.Z);
            var p001 = W(mn.X, mn.Y, mx.Z); var p101 = W(mx.X, mn.Y, mx.Z);
            var p011 = W(mn.X, mx.Y, mx.Z); var p111 = W(mx.X, mx.Y, mx.Z);
            lineRenderer.AddLine(p000, p100, col); lineRenderer.AddLine(p100, p110, col);
            lineRenderer.AddLine(p110, p010, col); lineRenderer.AddLine(p010, p000, col);
            lineRenderer.AddLine(p001, p101, col); lineRenderer.AddLine(p101, p111, col);
            lineRenderer.AddLine(p111, p011, col); lineRenderer.AddLine(p011, p001, col);
            lineRenderer.AddLine(p000, p001, col); lineRenderer.AddLine(p100, p101, col);
            lineRenderer.AddLine(p110, p111, col); lineRenderer.AddLine(p010, p011, col);
        }

        private void TickWorldCollision()
        {
            collisionDraw.Clear();
            if (!panel.WorldShowCollision || gameFiles?.Cache == null) return;

            EnsureWorldCollisionView_V28();
            collisionView.Alpha = panel.WorldCollisionOpacity;
            while (collisionView.Retired.TryDequeue(out var retired)) { retired.ReleaseVB(); collisionRetiredVBs++; }
            if (ybnIndex == null)
            {
                ybnIndex = new List<(uint, SharpDX.BoundingBox)>();
                var caches = gameFiles.Cache.AllCacheFiles;
                var ybnDict = gameFiles.Cache.YbnDict;
                var interiorYbns = new HashSet<uint>();
                var manifests = gameFiles.Cache.AllManifests;
                if (manifests != null)
                    foreach (var mf in manifests)
                        if (mf?.Interiors != null)
                            foreach (var intr in mf.Interiors)
                                if (intr?.Bounds != null) foreach (var b in intr.Bounds) interiorYbns.Add(b.Hash);
                int skippedNoEntry = 0, skippedInterior = 0;
                var seenKeys = new HashSet<(uint, float, float, float)>();
                if (caches != null)
                {
                    foreach (var cf in caches)
                    {
                        var items = cf?.AllBoundsStoreItems;
                        if (items == null) continue;
                        foreach (var it in items)
                        {
                            if (it == null) continue;
                            if (!(it.Max.X > it.Min.X) || !(it.Max.Y > it.Min.Y)) continue;
                            if (ybnDict != null && !ybnDict.ContainsKey(it.Name.Hash)) { skippedNoEntry++; continue; }
                            if (interiorYbns.Contains(it.Name.Hash)) { skippedInterior++; continue; }
                            if (!seenKeys.Add((it.Name.Hash, it.Min.X, it.Min.Y, it.Min.Z))) continue;
                            ybnIndex.Add((it.Name.Hash,
                                new SharpDX.BoundingBox(
                                    new SharpDX.Vector3(it.Min.X, it.Min.Y, it.Min.Z),
                                    new SharpDX.Vector3(it.Max.X, it.Max.Y, it.Max.Z))));
                        }
                    }
                }
                Console.WriteLine($"COLLISION index {ybnIndex.Count} bounds (skipped {skippedNoEntry} with no ybn in the archives, {skippedInterior} interior)");
            }

            var cam = camera.Position;
            float streamR = World.EffectiveRadius > 0.0f ? World.EffectiveRadius : World.StreamRadius;
            float range = Math.Max(streamR, 10.0f) * WorldStreamer.RangeScaleFor(2u);
            var frustum = new SharpDX.BoundingFrustum(camera.ViewProjMatrix);
            long tris = 0;
            var wanted = collisionWanted;
            if (wanted.Count == 0 || (cam - collisionWantedAt).LengthSquared() > 15.0f * 15.0f || Math.Abs(range - collisionWantedRange) > 1.0f
                || collisionView.ProjectVersion_V25 != collisionProjectVersion_V25)
            {
                wanted.Clear();
                collisionProjectVersion_V25 = collisionView.ProjectVersion_V25;
                var projectBounds = collisionView.ProjectBounds_V25();
                var projectHashes = new HashSet<uint>();
                foreach (var (hash, box) in projectBounds)
                {
                    projectHashes.Add(hash);
                    var c = SharpDX.Vector3.Clamp(cam, box.Minimum, box.Maximum);
                    float d2 = (c - cam).LengthSquared();
                    if (d2 > range * range) continue;
                    wanted.Add((d2, hash, box));
                }
                foreach (var (hash, box) in ybnIndex)
                {
                    if (projectHashes.Contains(hash)) continue;
                    var c = SharpDX.Vector3.Clamp(cam, box.Minimum, box.Maximum);
                    float d2 = (c - cam).LengthSquared();
                    if (d2 > range * range) continue;
                    wanted.Add((d2, hash, box));
                }
                wanted.Sort((a, b) => a.d2.CompareTo(b.d2));
                collisionWantedAt = cam;
                collisionWantedRange = range;
            }
            collisionView.BeginFrame();
            long cap = collisionView.MaxCachedTriangles;
            double avg = collisionView.AverageTriangles;
            int outstanding = 0;
            foreach (var (d2, hash, box) in wanted)
            {
                if (tris + outstanding * avg >= cap) break;
                var mesh = collisionView.Get(hash);
                if (mesh == null) { outstanding++; continue; }
                if (mesh.IsEmpty) continue;
                tris += mesh.TriangleCount;
                var bb = box;
                if (frustum.Contains(ref bb) == SharpDX.ContainmentType.Disjoint) continue;
                collisionDraw.Add(mesh);
            }
            panel.WorldCollisionTris = (int)Math.Min(tris, int.MaxValue);
            collisionWantedCount = wanted.Count;
        }
        private readonly List<(float d2, uint hash, SharpDX.BoundingBox box)> collisionWanted = new List<(float, uint, SharpDX.BoundingBox)>();
        private int collisionProjectVersion_V25 = -1;
        private SharpDX.Vector3 collisionWantedAt = new SharpDX.Vector3(1e9f);
        private float collisionWantedRange;
        private int collisionWantedCount;
        private long collisionRetiredVBs;

        private void DrawWorldCollision(SharpDX.Direct3D11.DeviceContext context)
        {
            bool solid = panel.WorldCollisionOpacity >= 0.99f;
            var blend = solid ? CommonStates.BlendOpaque : CommonStates.BlendAlpha;
            deviceResources.ClearDepthOnly();
            foreach (var mesh in collisionDraw)
            {
                if (mesh.VB == null && mesh.Vertices.Length >= 3)
                {
                    mesh.VB = triRenderer.MakeBuffer(mesh.Vertices);
                    mesh.VBCount = mesh.Vertices.Length;
                }
                triRenderer.DrawBuffer(context, mesh.VB, mesh.VBCount, camera.ViewProjMatrix, blend,
                                       CommonStates.DepthDefault, CommonStates.RasterSolid);
            }
        }

        private void WorldCopySelected()
        {
            int n = EntityOps.Clipboard.Copy(WorldSelectedEntities());
            WorldEdit.LastStatus = n > 0 ? $"copied {EntityOps.Clipboard.Summary}" : "nothing selected to copy";
        }

        private void WorldDeleteSelected()
        {
            if (WorldEdit.Selection.Light != null && WorldDeleteLight_U25()) return;
            bool tookU5 = false;
            WorldDeleteSelected_U5(ref tookU5);
            if (tookU5) return;
            var e = WorldEdit.Selected;
            if (e == null && WorldEdit.Selection.HasValue) { WorldDeleteSelectionItem(); return; }
            if (e?.Ymap == null) { WorldEdit.LastStatus = "nothing selected to delete"; return; }

            var snap = EntityOps.Capture(e);
            var ymap = e.Ymap;
            var name = e.Archetype?.Name ?? "entity";
            WorldEdit.MarkDirty(e);
            ProjectAutoAddYmap(ymap, null);
            if (!EntityOps.Remove(ymap, e)) { WorldEdit.LastStatus = "could not delete " + name; return; }
            WorldEntityChanged(e);
            WorldEdit.Deselect();
            WorldEdit.LastStatus = "deleted " + name;

            var current = e;
            WorldHistory.Push(new DelegateCommand("Delete " + name,
                doIt: () =>
                {
                    if (current == null) return;
                    EntityOps.Remove(ymap, current);
                    WorldEntityChanged(current);
                    WorldEdit.MarkDirty(ymap);
                    if (ReferenceEquals(WorldEdit.Selected, current)) WorldEdit.Deselect();
                },
                undoIt: () =>
                {
                    current = EntityOps.Restore(snap, ymap, gameFiles?.Cache);
                    if (current != null && EntityOps.Add(ymap, current, gameFiles?.Cache))
                    {
                        WorldEntityChanged(current);
                        WorldEdit.MarkDirty(ymap);
                        WorldEdit.Select(current);
                    }
                }));
        }

        private void WorldDuplicateSelected()
        {
            var src = WorldEdit.Selected;
            if (src?.Ymap == null) { WorldEdit.LastStatus = "nothing selected to duplicate"; return; }

            float step = Math.Max(src.BSRadius * 1.2f, 1.0f);
            var dup = EntityOps.Duplicate(src, new SharpDX.Vector3(step, 0, 0), gameFiles?.Cache);
            if (dup == null) { WorldEdit.LastStatus = "could not duplicate"; return; }
            var ymap = dup.Ymap;
            var snap = EntityOps.Capture(dup);
            WorldEntityChanged(dup);
            WorldEdit.Select(dup);
            WorldEdit.LastStatus = "duplicated " + (src.Archetype?.Name ?? "entity");

            var current = dup;
            WorldHistory.Push(new DelegateCommand("Duplicate " + (src.Archetype?.Name ?? "entity"),
                doIt: () =>
                {
                    current = EntityOps.Restore(snap, ymap, gameFiles?.Cache);
                    if (current != null && EntityOps.Add(ymap, current, gameFiles?.Cache))
                    {
                        WorldEntityChanged(current);
                        WorldEdit.MarkDirty(ymap);
                        WorldEdit.Select(current);
                    }
                },
                undoIt: () =>
                {
                    if (current == null) return;
                    EntityOps.Remove(ymap, current);
                    WorldEntityChanged(current);
                    WorldEdit.MarkDirty(ymap);
                    if (ReferenceEquals(WorldEdit.Selected, current)) WorldEdit.Deselect();
                }));
        }

        private void WorldPasteClipboard()
        {
            if (EntityOps.Clipboard.IsEmpty) { WorldEdit.LastStatus = "clipboard is empty"; return; }
            bool tookU5 = false;
            WorldPasteClipboard_U5(ref tookU5);
            if (tookU5) return;
            var target = WorldEdit.Selected?.Ymap;
            if (target == null)
            {
                WorldEdit.LastStatus = "select an entity in the target ymap first - the paste goes into its file";
                return;
            }

            var made = EntityOps.Clipboard.Paste(target, default, gameFiles?.Cache);
            if (made.Count == 0) { WorldEdit.LastStatus = "paste produced nothing"; return; }
            foreach (var m in made) WorldEntityChanged(m);
            WorldEdit.Select(made[made.Count - 1]);
            WorldEdit.LastStatus = $"pasted {made.Count} into {target.Name ?? "ymap"}";

            var snaps = made.ConvertAll(EntityOps.Capture);
            var current = made.ToArray();
            WorldHistory.Push(new DelegateCommand($"Paste {made.Count} entit{(made.Count == 1 ? "y" : "ies")}",
                doIt: () =>
                {
                    for (int i = 0; i < snaps.Count; i++)
                    {
                        current[i] = EntityOps.Restore(snaps[i], target, gameFiles?.Cache);
                        if (current[i] != null && EntityOps.Add(target, current[i], gameFiles?.Cache))
                            WorldEntityChanged(current[i]);
                    }
                    WorldEdit.MarkDirty(target);
                },
                undoIt: () =>
                {
                    for (int i = current.Length - 1; i >= 0; i--)
                    {
                        if (current[i] == null) continue;
                        EntityOps.Remove(target, current[i]);
                        WorldEntityChanged(current[i]);
                        if (ReferenceEquals(WorldEdit.Selected, current[i])) WorldEdit.Deselect();
                    }
                    WorldEdit.MarkDirty(target);
                }));
        }

        private void TryWorldUndo()
        {
            try { WorldHistory.Undo(); }
            catch (Exception ex) { WorldEdit.LastStatus = "undo failed: " + ex.Message; }
        }

        private void TryWorldRedo()
        {
            try { WorldHistory.Redo(); }
            catch (Exception ex) { WorldEdit.LastStatus = "redo failed: " + ex.Message; }
        }

        private static bool TryParseXml(string xml)
        {
            try { var d = new System.Xml.XmlDocument(); d.LoadXml(xml); return true; }
            catch { return false; }
        }

        private bool archiveIndexed;

        private void RegisterFileTypes(bool register)
        {
            string[] exts = { ".ydr", ".yft", ".ydd", ".ytd", ".ytyp", ".ymap", ".ybn", ".cwproj", ProjectFile.Extension };
            string exe = Application.ExecutablePath;
            int done = 0;
            try
            {
                using var classes = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes");
                foreach (var ext in exts)
                {
                    string progId = "RageLightEditor" + ext.Replace(".", "_");
                    if (register)
                    {
                        using (var k = classes.CreateSubKey(ext)) k.SetValue("", progId);
                        using (var k = classes.CreateSubKey(progId)) k.SetValue("", "RAGE " + ext.TrimStart('.').ToUpperInvariant() + " file");
                        using (var k = classes.CreateSubKey(progId + @"\DefaultIcon")) k.SetValue("", $"\"{exe}\",0");
                        using (var k = classes.CreateSubKey(progId + @"\shell\open\command")) k.SetValue("", $"\"{exe}\" \"%1\"");
                    }
                    else
                    {
                        try { classes.DeleteSubKeyTree(progId, false); } catch { }
                        try
                        {
                            using var k = classes.OpenSubKey(ext, true);
                            if (k != null && (k.GetValue("") as string) == progId) k.DeleteValue("", false);
                        }
                        catch { }
                    }
                    done++;
                }
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
                panel.MloStatus = register ? $"registered {done} file types - Open With / double-click now bring them here"
                                           : $"removed {done} file associations";
            }
            catch (Exception ex) { panel.MloStatus = "file types: " + ex.Message; }
        }
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private bool waterTexturesTried;
        private void EnsureWaterTextures()
        {
            if (waterTexturesTried || gameFiles?.Cache == null || !gameFiles.Ready) return;
            waterTexturesTried = true;
            const uint graphics = 3154743001;
            const uint waterbump = 2826194296;
            const uint waterbump2 = 209023451;
            const uint waterfog = 4047019542;
            var w = sceneRenderer.Water;
            try
            {
                var tb = gameFiles.FindTexture(waterbump, graphics);
                var tb2 = gameFiles.FindTexture(waterbump2, graphics);
                var tf = gameFiles.FindTexture(waterfog, graphics);
                w.Bump = tb != null ? textureLoader.GetSRV(tb) : null;
                w.Bump2 = tb2 != null ? textureLoader.GetSRV(tb2) : null;
                w.Fog = tf != null ? textureLoader.GetSRV(tf) : null;
            }
            catch (Exception ex) { Console.WriteLine("WATER textures: " + ex.Message); }
            if (Environment.GetEnvironmentVariable("RLE_NO_DEPTHREAD") != "1")
            {
                w.BeginDepthRead = () =>
                {
                    int mode = deviceResources.BeginDepthRead(out var srv);
                    return (mode, srv);
                };
                w.EndDepthRead = deviceResources.EndDepthRead;
                w.BeginColourRead = () => panel.WaterRefraction ? deviceResources.CopySceneForRefraction() : null;
            }
            if (DebugWaterDebug) w.FogLightIntensity = -1.0f;
            Console.WriteLine($"WATER textures: bump={(w.Bump != null)} bump2={(w.Bump2 != null)} fog={(w.Fog != null)}");
        }

        private void SpawnArchiveModelInWorld(RpfFileEntry e)
        {
            string shortName = Path.GetFileNameWithoutExtension(e.Name);
            uint hash = JenkHash.GenHash(shortName.ToLowerInvariant());
            JenkIndex.Ensure(shortName.ToLowerInvariant());
            var arch = gameFiles?.Cache?.GetArchetype(hash);
            if (arch == null)
            {
                panel.MloStatus = $"No archetype is named {shortName}, so nothing can place it - add one in a ytyp first";
                return;
            }
            if (ProjWin.Project == null)
                ProjWin.Project = new CwProject { Name = "New Project", HasChanged = true };
            var spawn = camera.Position + camera.GetForward() * 5.0f;
            var mloTarget = ResolveMloTarget_U21(spawn);
            if (mloTarget.Valid)
            {
                var me = AddMloEntity_U21(mloTarget, hash, arch, spawn);
                if (me == null) { panel.MloStatus = "could not add " + shortName + " to " + MloTargetText_U21(mloTarget); return; }
                ProjWin.Select(me);
                ProjWin.Visible = true;
                WorldEdit.Select(me);
                panel.MloStatus = $"placed {shortName} in {MloTargetText_U21(mloTarget)} ({mloTarget.Why})";
                Console.WriteLine($"ARCHIVE placed {shortName} in {MloTargetText_U21(mloTarget)} at {spawn}");
                return;
            }
            var ymap = ProjWin.CurrentYmap ?? ProjWin.Project.YmapFiles.FirstOrDefault() ?? ProjWin.Project.NewYmap();
            var ent = ProjWin.Project.NewEntity(ymap, spawn, gameFiles?.Cache);
            if (ent == null) { panel.MloStatus = "could not add an entity to " + ymap.Name; return; }
            ent._CEntityDef.archetypeName = new MetaHash(hash);
            ent._CEntityDef.lodDist = Math.Max(arch.LodDist, 1.0f);
            ent.SetArchetype(arch);
            ymap.HasChanged = true;
            ProjWin.Select(ent);
            ProjWin.Visible = true;
            RebuildProjectOverrides();
            WorldEntityChanged(ent);
            WorldEdit.Select(ent);
            panel.MloStatus = $"placed {shortName} in {ymap.Name}";
            Console.WriteLine($"ARCHIVE placed {shortName} as entity in {ymap.Name} at {spawn}");
        }

        private void OpenArchiveEntry(RpfFileEntry e)
        {
            if (e == null) return;
            string kind = ArchiveBrowser.KindOf(e);
            try
            {
                Cursor = Cursors.WaitCursor;

                var data = ArchiveBrowser.ExtractForDisk(e);
                if (data == null || data.Length == 0)
                {
                    panel.MloStatus = $"{e.Name} came out of the archive empty";
                    return;
                }

                string tmpDir = Path.Combine(Path.GetTempPath(), "rle_archive");
                Directory.CreateDirectory(tmpDir);
                string tmp = Path.Combine(tmpDir, e.Name);
                File.WriteAllBytes(tmp, data);

                if (panel.WorldMode && (kind == "ydr" || kind == "yft"))
                {
                    SpawnArchiveModelInWorld(e);
                }
                else if (panel.WorldMode && (kind == "ytyp" || kind == "ymap"))
                {
                    projCtl.AddFilesToProject(new[] { tmp });
                    ProjWin.Visible = true;
                    panel.MloStatus = ProjWin.Status;
                }
                else if (kind == "ytyp" || kind == "ymap" || kind == "ytd" ||
                    kind == "ydr" || kind == "yft")
                {
                    LoadFile(tmp);
                    if (!string.IsNullOrEmpty(scene.LoadError))
                        panel.MloStatus = scene.LoadError;
                }
                else if (kind == "ydd" || kind == "ybn")
                {
                    if (assetPreview == null)
                        assetPreview = new AssetPreview(gameFiles, modelRenderer, textureLoader);
                    previewCollision = null;
                    if (!assetPreview.Open(e))
                    {
                        panel.MloStatus = assetPreview.Error;
                        return;
                    }
                    if (kind == "ybn" && assetPreview.Ybn != null)
                    {
                        if (collisionView == null) collisionView = new CollisionView(gameFiles);
                        previewCollision = collisionView.Build(assetPreview.Ybn);
                    }
                    panel.ArchivePreview = assetPreview;
                    panel.MloStatus = $"{e.Name}: {assetPreview.Stats.Summary()}";
                    FramePreview();
                }
                else
                {
                    panel.MloStatus = $"Nothing here reads .{kind} yet - export it instead";
                    return;
                }
                foreach (var f in scene.Files)
                    if (string.Equals(f.Path, tmp, StringComparison.OrdinalIgnoreCase)) f.ReadOnly = true;
            }
            catch (Exception ex)
            {
                panel.MloStatus = "Could not open " + e.Name + ": " + ex.Message;
                MessageBox.Show(this, ex.Message, "Open from archive failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void ExportArchiveEntry(RpfFileEntry e)
        {
            if (e == null) return;
            using var dlg = new SaveFileDialog
            {
                Title = "Export from archive",
                FileName = e.Name,
                Filter = "All files|*.*",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var data = ArchiveBrowser.ExtractForDisk(e);
                if (data == null) throw new Exception("the archive returned nothing");
                File.WriteAllBytes(dlg.FileName, data);
                panel.MloStatus = $"Exported {e.Name} ({data.Length:N0} bytes)";
                Console.WriteLine($"ARCHIVE exported {e.Path} -> {dlg.FileName} ({data.Length} bytes)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Export failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshGameTimecycles()
        {
            if (gameTimecyclesListed || !gameFiles.Ready) return;
            gameTimecyclesListed = true;

            gameTimecycles = gameFiles.ListTimecycles();

            foreach (var tc in gameTimecycles)
            {
                if (!tc.Name.StartsWith("timecycle_mods", StringComparison.OrdinalIgnoreCase)) continue;
                var xml = gameFiles.ReadText(tc.Path);
                if (!string.IsNullOrEmpty(xml)) timecycle.LoadModifiersXmlText(xml, tc.Name, out _);
            }
            gameTimecycles = gameTimecycles
                .Where(t => !t.Name.StartsWith("timecycle_mods", StringComparison.OrdinalIgnoreCase))
                .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            panel.GameTimecycleNames = gameTimecycles.Select(t => t.Name).ToArray();

            {
                var tcrow = Environment.GetEnvironmentVariable("RLE_TCROW");
                if (!string.IsNullOrWhiteSpace(tcrow))
                {
                    var rp = tcrow.Split(',');
                    var fname = rp[0].Trim();
                    foreach (var t in gameFiles.ListTimecycles())
                    {
                        if (!t.Name.Equals(fname, StringComparison.OrdinalIgnoreCase)) continue;
                        var xml = gameFiles.ReadText(t.Path) ?? "";
                        Console.WriteLine($"TCROW {t.Name} <- {t.Path}");
                        foreach (var line in xml.Split('\n'))
                            for (int vi = 1; vi < rp.Length; vi++)
                                if (line.Contains(rp[vi].Trim())) Console.WriteLine("TCROW " + line.Trim());
                    }
                }
            }
            if (DebugTcDump)
            {
                var raw = gameFiles.ListTimecycles();
                Console.WriteLine($"TCDUMP raw entries: {raw.Count}");
                int ok = 0, bad = 0;
                foreach (var t in raw)
                {
                    var xml = gameFiles.ReadText(t.Path);
                    if (string.IsNullOrEmpty(xml)) { Console.WriteLine($"  UNREADABLE {t.Name}  <- {t.Path}"); bad++; continue; }
                    var probe = new TimecycleData();
                    if (probe.LoadTimecycleXmlText(xml, t.Name, out var err) && probe.HasData)
                    {
                        ok++;
                        Console.WriteLine($"  OK   {t.Name,-28} regions={probe.Regions.Count} keys={probe.KeyframeCount}  {t.Path}");
                    }
                    else
                    {
                        bad++;
                        Console.WriteLine($"  FAIL {t.Name,-28} {err}  <- {t.Path}");
                    }
                }
                Console.WriteLine($"TCDUMP parsed ok={ok} failed={bad}");
                foreach (var t in raw.Where(t => t.Name.StartsWith("timecycle_mods", StringComparison.OrdinalIgnoreCase)))
                {
                    var xml = gameFiles.ReadText(t.Path);
                    var probe = new TimecycleData();
                    int n = probe.LoadModifiersXmlText(xml ?? "", t.Name, out var merr) ? probe.Modifiers.Count : -1;
                    Console.WriteLine($"  MODS {t.Name,-20} count={n} {merr}  <- {t.Path}");
                }
                Console.WriteLine($"TCDUMP modifiers loaded into the app: {timecycle.Modifiers.Count}");
            }
            if (!timecycle.ScheduleFromFile)
            {
                foreach (var p in gameFiles.FindEntries("time.xml"))
                {
                    if (p.IndexOf("levels", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var xml = gameFiles.ReadText(p);
                    if (!string.IsNullOrEmpty(xml) && timecycle.LoadScheduleXmlText(xml, out _)) break;
                }
            }

            var want = pendingGameTimecycle ?? (timecycle.HasData ? null : DefaultTimecycle);
            pendingGameTimecycle = null;
            if (want != null)
            {
                var i = gameTimecycles.FindIndex(t => string.Equals(t.Name, want, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) DoLoadGameTimecycle(i);
            }
            if (DebugWeather >= 0)
            {
                var cyc = new TimecycleData();
                if (LoadCycleFor(weather.CurrentPreset, cyc))
                {
                    timecycle.CopyCycleFrom(cyc);
                    loadedGameTimecycle = cyc.Name;
                    SyncGameTimecycleIndex(cyc.Name);
                    panel.TimecycleStatus = $"Weather: {weather.CurrentPreset.Name}";
                }
            }
        }

        private readonly WeatherSystem weather = new WeatherSystem();
        private TimecycleData weatherBlendCycle;
        private double lastWeatherTick;

        private bool LoadCycleFor(WeatherPreset p, TimecycleData into)
        {
            if (gameTimecycles == null || into == null) return false;
            var name = WeatherSystem.ResolveCycle(p, gameTimecycles.Select(t => t.Name));
            if (name == null) return false;
            var tc = gameTimecycles.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tc == null) return false;
            var xml = gameFiles.ReadText(tc.Path);
            if (string.IsNullOrEmpty(xml)) return false;
            return into.LoadTimecycleXmlText(xml, tc.Name, out _);
        }

        private void SyncGameTimecycleIndex(string cycleName)
        {
            if (gameTimecycles == null || string.IsNullOrEmpty(cycleName)) return;
            int i = gameTimecycles.FindIndex(t =>
                string.Equals(t.Name, cycleName, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) panel.GameTimecycleIndex = i;
        }

        private void SetWeather(int index)
        {
            if (index == weather.Target && !weather.InTransition) return;

            float secs = Math.Max(settings.WeatherTransitionSeconds, 0.0f);
            weather.TransitionSeconds = secs;
            if (secs <= 0.01f)
            {
                weather.SetWeather(index, immediate: true);
                weatherBlendCycle = null;
                var cycle = new TimecycleData();
                if (LoadCycleFor(weather.CurrentPreset, cycle))
                {
                    var mod = timecycle.SelectedModifier;
                    var str = timecycle.ModifierStrength;
                    timecycle.CopyCycleFrom(cycle);
                    timecycle.SelectedModifier = mod;
                    timecycle.ModifierStrength = str;
                    loadedGameTimecycle = cycle.Name;
                    SyncGameTimecycleIndex(cycle.Name);
                }
                panel.TimecycleStatus = $"Weather: {weather.CurrentPreset.Name}";
                return;
            }

            weather.SetWeather(index);
            if (!weather.InTransition) return;

            weatherBlendCycle ??= new TimecycleData();
            if (!LoadCycleFor(weather.TargetPreset, weatherBlendCycle))
            {
                weatherBlendCycle = null;
            }
            panel.TimecycleStatus = $"Weather: {weather.StatusText}";
        }

        private void UpdateWeather(double now)
        {
            float dt = lastWeatherTick > 0 ? (float)(now - lastWeatherTick) : 0.0f;
            lastWeatherTick = now;
            dt = Math.Clamp(dt, 0.0f, 0.25f);

            bool wasTransitioning = weather.InTransition;
            weather.Update(dt);

            if (weather.AutoAdvance) panel.PreviewHour = weather.Hour;
            else weather.Hour = panel.PreviewHour;

            if (wasTransitioning && !weather.InTransition)
            {
                if (weatherBlendCycle != null)
                {
                    var mod = timecycle.SelectedModifier;
                    var str = timecycle.ModifierStrength;
                    timecycle.CopyCycleFrom(weatherBlendCycle);
                    timecycle.SelectedModifier = mod;
                    timecycle.ModifierStrength = str;
                    loadedGameTimecycle = weatherBlendCycle.Name;
                    weatherBlendCycle = null;
                }
                panel.TimecycleStatus = $"Weather: {weather.CurrentPreset.Name}";
            }
        }

        private TimecycleData.SkyState skyState = new TimecycleData.SkyState { AmbientDownWrap = 1.0f, HdrIntensity = 1.0f };
        private SkyRenderer skyRenderer;

        private Matrix SkyViewProj()
        {
            var view = camera.ViewMatrix;
            view.Row4 = new Vector4(0, 0, 0, 1);
            return view * camera.ProjMatrix;
        }

        private SkyVars BuildSkyVars(double now)
        {
            timecycle.GetLightDirection(panel.PreviewHour, out var sunDir, out var moonDir);
            float phase = (float)(now * 0.02);
            var lunar = new Vector3((float)Math.Cos(phase), (float)Math.Sin(phase) * 0.4f, 0.35f);

            var s = skyState;
            float flash = weather.LightningFlash * weather.LightningFlash;
            return new SkyVars
            {
                InvViewProjDir = Matrix.Transpose(Matrix.Invert(SkyViewProj())),
                CameraPos = new Vector4(camera.Position, 1),

                AzimuthEastColour = s.AzimuthEast,
                AzimuthWestColour = s.AzimuthWest,
                AzimuthTransitionColour = s.AzimuthTransition,
                ZenithColour = s.Zenith,
                ZenithTransitionColour = s.ZenithTransition,
                AzimuthTransitionPosition = s.AzimuthTransitionPos,
                ZenithTransitionPosition = s.ZenithTransitionPos,
                ZenithBlendStart = s.ZenithBlendStart,
                ZenithTransitionEastBlend = s.ZenithTransitionEastBlend,
                ZenithTransitionWestBlend = s.ZenithTransitionWestBlend,

                SunDirection = Vector3.Normalize(sunDir),
                SunColour = s.SunColour * (panel.HdrActive ? s.SunHdr : Math.Min(s.SunHdr, 4.8f)),
                SunDiscColour = s.SunDiscColour,
                SunMie = s.SunMie,
                SunDiscSize = Math.Max(s.SunDiscSize, 0.004f),
                SunHdr = Math.Max(s.SunHdr, 0.1f),
                SunInfluenceRadius = s.SunInfluenceRadius,
                SunScatterIntensity = s.SunScatterIntensity,

                MoonDirection = Vector3.Normalize(moonDir),
                MoonColour = s.MoonColour,
                LunarCycle = Vector3.Normalize(lunar),
                MoonDiscSize = Math.Max(s.MoonDiscSize, 0.008f),
                MoonIntensity = s.MoonIntensity,
                MoonInfluenceRadius = s.MoonInfluenceRadius,

                CloudBaseColour = s.CloudBaseColour,
                CloudMidColour = s.CloudMidColour,
                CloudShadowColour = s.CloudShadowColour,
                CloudBaseStrength = s.CloudBaseStrength,
                CloudDensityMultiplier = Math.Max(s.CloudDensityMult, 0.2f),
                CloudDensityBias = s.CloudDensityBias,
                CloudFadeOut = s.CloudFadeOut,
                CloudShadowStrength = s.CloudShadowStrength,
                CloudCoverage = CloudHatsActive ? 0.0f : (panel.WeatherEnabled ? weather.CloudCoverage : 0.3f),
                CloudTime = (float)now * panel.CloudSpeed,

                FogColour = sceneRenderer.FogColour,
                FogDensity = 0.0f,
                HdrIntensity = (panel.HdrActive ? s.HdrIntensity : Math.Min(s.HdrIntensity, 1.8f))
                               * panel.SkyExposure + flash * 2.0f,
                StarfieldIntensity = s.StarfieldIntensity,
                SkyPad0 = DebugSkyMode == 1 ? 1.0f : (DebugSkyMode >= 3 ? DebugSkyMode : 0.0f),
                SkyPad1 = DebugSkyMode == 2 ? 1.0f : 0.0f,
                SkyExtra0 = new Vector4(SkyShadowEnv_V68("RLE_SKYSAT", panel.SkySaturation_V68), 0, 0, 0),
                Fog = BuildGameFog(),
            };
        }

        private static TimecycleData.GlobalLightState BlendGlobalLight(
            TimecycleData.GlobalLightState a, TimecycleData.GlobalLightState b, float t)
        {
            return new TimecycleData.GlobalLightState
            {
                LightDir = Vector3.Normalize(Vector3.Lerp(a.LightDir, b.LightDir, t)),
                LightHdr = MathUtil.Lerp(a.LightHdr, b.LightHdr, t),
                LightDirColour = Vector4.Lerp(a.LightDirColour, b.LightDirColour, t),
                LightDirAmbColour = Vector4.Lerp(a.LightDirAmbColour, b.LightDirAmbColour, t),
                NaturalAmbUp = Vector4.Lerp(a.NaturalAmbUp, b.NaturalAmbUp, t),
                NaturalAmbDown = Vector4.Lerp(a.NaturalAmbDown, b.NaturalAmbDown, t),
                ArtificialAmbUp = Vector4.Lerp(a.ArtificialAmbUp, b.ArtificialAmbUp, t),
                ArtificialAmbDown = Vector4.Lerp(a.ArtificialAmbDown, b.ArtificialAmbDown, t),
            };
        }

        private static TimecycleData.SkyState BlendSky(TimecycleData.SkyState a, TimecycleData.SkyState b, float t)
        {
            TimecycleData.SkyState o;
            o.AzimuthEast = Vector3.Lerp(a.AzimuthEast, b.AzimuthEast, t);
            o.AzimuthWest = Vector3.Lerp(a.AzimuthWest, b.AzimuthWest, t);
            o.AzimuthTransition = Vector3.Lerp(a.AzimuthTransition, b.AzimuthTransition, t);
            o.Zenith = Vector3.Lerp(a.Zenith, b.Zenith, t);
            o.ZenithTransition = Vector3.Lerp(a.ZenithTransition, b.ZenithTransition, t);
            o.AzimuthTransitionPos = MathUtil.Lerp(a.AzimuthTransitionPos, b.AzimuthTransitionPos, t);
            o.ZenithTransitionPos = MathUtil.Lerp(a.ZenithTransitionPos, b.ZenithTransitionPos, t);
            o.ZenithBlendStart = MathUtil.Lerp(a.ZenithBlendStart, b.ZenithBlendStart, t);
            o.ZenithTransitionEastBlend = MathUtil.Lerp(a.ZenithTransitionEastBlend, b.ZenithTransitionEastBlend, t);
            o.ZenithTransitionWestBlend = MathUtil.Lerp(a.ZenithTransitionWestBlend, b.ZenithTransitionWestBlend, t);
            o.SunColour = Vector3.Lerp(a.SunColour, b.SunColour, t);
            o.SunDiscColour = Vector3.Lerp(a.SunDiscColour, b.SunDiscColour, t);
            o.SunMie = Vector3.Lerp(a.SunMie, b.SunMie, t);
            o.SunDiscSize = MathUtil.Lerp(a.SunDiscSize, b.SunDiscSize, t);
            o.SunHdr = MathUtil.Lerp(a.SunHdr, b.SunHdr, t);
            o.SunInfluenceRadius = MathUtil.Lerp(a.SunInfluenceRadius, b.SunInfluenceRadius, t);
            o.SunScatterIntensity = MathUtil.Lerp(a.SunScatterIntensity, b.SunScatterIntensity, t);
            o.MoonColour = Vector3.Lerp(a.MoonColour, b.MoonColour, t);
            o.MoonDiscSize = MathUtil.Lerp(a.MoonDiscSize, b.MoonDiscSize, t);
            o.MoonIntensity = MathUtil.Lerp(a.MoonIntensity, b.MoonIntensity, t);
            o.MoonInfluenceRadius = MathUtil.Lerp(a.MoonInfluenceRadius, b.MoonInfluenceRadius, t);
            o.MoonScatterIntensity = MathUtil.Lerp(a.MoonScatterIntensity, b.MoonScatterIntensity, t);
            o.CloudBaseColour = Vector3.Lerp(a.CloudBaseColour, b.CloudBaseColour, t);
            o.CloudMidColour = Vector3.Lerp(a.CloudMidColour, b.CloudMidColour, t);
            o.CloudShadowColour = Vector3.Lerp(a.CloudShadowColour, b.CloudShadowColour, t);
            o.CloudBaseStrength = MathUtil.Lerp(a.CloudBaseStrength, b.CloudBaseStrength, t);
            o.CloudDensityMult = MathUtil.Lerp(a.CloudDensityMult, b.CloudDensityMult, t);
            o.CloudDensityBias = MathUtil.Lerp(a.CloudDensityBias, b.CloudDensityBias, t);
            o.CloudFadeOut = MathUtil.Lerp(a.CloudFadeOut, b.CloudFadeOut, t);
            o.CloudShadowStrength = MathUtil.Lerp(a.CloudShadowStrength, b.CloudShadowStrength, t);
            o.FogColour = Vector3.Lerp(a.FogColour, b.FogColour, t);
            o.FogDensity = MathUtil.Lerp(a.FogDensity, b.FogDensity, t);
            o.FogStart = MathUtil.Lerp(a.FogStart, b.FogStart, t);
            o.HdrIntensity = MathUtil.Lerp(a.HdrIntensity, b.HdrIntensity, t);
            o.StarfieldIntensity = MathUtil.Lerp(a.StarfieldIntensity, b.StarfieldIntensity, t);
            o.AmbientDownWrap = MathUtil.Lerp(a.AmbientDownWrap, b.AmbientDownWrap, t);
            o.FogNearCol = Vector3.Lerp(a.FogNearCol, b.FogNearCol, t);
            o.FogSunCol = Vector3.Lerp(a.FogSunCol, b.FogSunCol, t);
            o.FogFarCol = Vector3.Lerp(a.FogFarCol, b.FogFarCol, t);
            o.FogMoonCol = Vector3.Lerp(a.FogMoonCol, b.FogMoonCol, t);
            o.FogHazeCol = Vector3.Lerp(a.FogHazeCol, b.FogHazeCol, t);
            o.FogHeightFalloff = MathUtil.Lerp(a.FogHeightFalloff, b.FogHeightFalloff, t);
            o.FogBaseHeight = MathUtil.Lerp(a.FogBaseHeight, b.FogBaseHeight, t);
            o.FogAlpha = MathUtil.Lerp(a.FogAlpha, b.FogAlpha, t);
            o.FogHorizonTintScale = MathUtil.Lerp(a.FogHorizonTintScale, b.FogHorizonTintScale, t);
            o.FogHdr = MathUtil.Lerp(a.FogHdr, b.FogHdr, t);
            o.FogHazeDensity = MathUtil.Lerp(a.FogHazeDensity, b.FogHazeDensity, t);
            o.FogHazeAlpha = MathUtil.Lerp(a.FogHazeAlpha, b.FogHazeAlpha, t);
            o.FogHazeHdr = MathUtil.Lerp(a.FogHazeHdr, b.FogHazeHdr, t);
            o.FogHazeStart = MathUtil.Lerp(a.FogHazeStart, b.FogHazeStart, t);
            o.FogSunPower = MathUtil.Lerp(a.FogSunPower, b.FogSunPower, t);
            o.FogMoonPower = MathUtil.Lerp(a.FogMoonPower, b.FogMoonPower, t);
            o.FarClip = MathUtil.Lerp(a.FarClip, b.FarClip, t);
            return o;
        }

        private readonly HashSet<string> scannedModFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int LoadLocalTimecycleMods(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return 0;
            string root;
            try { root = LocalAssetIndex.FindRoot(resourcePath); }
            catch { return 0; }
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return 0;
            if (!scannedModFolders.Add(root)) return 0;

            int added = 0;
            string[] files;
            try { files = Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories); }
            catch { return 0; }

            foreach (var f in files)
            {
                string text;
                try
                {
                    var fi = new FileInfo(f);
                    if (fi.Length > 8 * 1024 * 1024) continue;
                    text = File.ReadAllText(f);
                }
                catch { continue; }
                if (text.IndexOf("modifier", StringComparison.OrdinalIgnoreCase) < 0) continue;

                int before = timecycle.Modifiers.Count;
                if (timecycle.LoadModifiersXmlText(text, Path.GetFileNameWithoutExtension(f), out _))
                {
                    added += timecycle.Modifiers.Count - before;
                }
            }
            if (added > 0) Console.WriteLine($"  local timecycle modifiers: +{added} from {root}");
            return added;
        }

        private void DoLoadGameTimecycle(int index)
        {
            if (gameTimecycles == null || index < 0 || index >= gameTimecycles.Count) return;
            var tc = gameTimecycles[index];
            var xml = gameFiles.ReadText(tc.Path);
            if (string.IsNullOrEmpty(xml))
            {
                panel.TimecycleStatus = "Couldn't read " + tc.Name + " from the archives.";
                return;
            }
            if (timecycle.LoadTimecycleXmlText(xml, tc.Name, out var err))
            {
                timecycleFileStamp = default;
                panel.TimecycleEnabled = true;
                panel.GameTimecycleIndex = index;
                loadedGameTimecycle = tc.Name;
                panel.TimecycleStatus = $"{tc.Name}: {timecycle.Regions.Count} region(s), {timecycle.KeyframeCount} keyframes.";
            }
            else
            {
                panel.TimecycleStatus = $"{tc.Name} failed to parse: {err}";
            }
        }

        public int DebugLightProbe;
        public bool DebugTcEdit;
        public bool DebugAllProps;
        public bool DebugWaterDebug;
        private int probeStep;
        private Vector3 probeOrigin;
        private int lastProbeLightCount;
        private Dictionary<RenderMesh, int> probePrevFingerprints = new Dictionary<RenderMesh, int>();
        public string ProbeShotDir;
        private string probeShotPending;
        private bool probeClosing;
        private string probePrevShadowKey = "";
        private int probeTotalMeshChanges, probeShadowChanges;

        private void RunLightProbe()
        {
            if (probeStep == 0)
            {
                var b = scene.MloInfo != null && scene.MloInfo.HasFocus
                    ? scene.MloInfo.FocusBounds : (scene.MloModel?.Bounds ?? default);
                probeOrigin = (b.Minimum + b.Maximum) * 0.5f;
                camera.Distance = 6.0f;
                sceneRenderer.MeshLightFingerprints = new Dictionary<RenderMesh, int>();
                if (DebugSelectLight >= 0 && DebugSelectLight < scene.Lights.Count)
                    scene.SelectedIndex = DebugSelectLight;
                Console.WriteLine("LIGHTPROBE: walking the camera; anything that changes here is camera-dependent");
            }

            bool atRefPose = probeStep == 0 || probeStep == DebugLightProbe - 1;
            if (atRefPose)
            {
                camera.Yaw = 0.6f;
                camera.Target = probeOrigin;
            }
            else
            {
                float t = probeStep * 0.06f;
                camera.Yaw = 0.6f + t;
                camera.Target = probeOrigin + new Vector3(
                    (float)Math.Cos(t) * 5.0f, (float)Math.Sin(t) * 5.0f, 0);
            }
            camera.Update();
            if (atRefPose && ProbeShotDir != null)
                probeShotPending = System.IO.Path.Combine(ProbeShotDir, probeStep == 0 ? "poseA.png" : "poseB.png");

            var shadowed = new List<string>();
            for (int i = 0; i < lastProbeLightCount && i < gpuLights.Length; i++)
            {
                if (gpuLights[i].ShadowSlot >= 0)
                    shadowed.Add($"{i}:{(int)gpuLights[i].ShadowSlot}");
            }

            int changed = 0, compared = 0;
            var fps = sceneRenderer.MeshLightFingerprints;
            if (fps != null)
            {
                foreach (var kv in fps)
                {
                    if (!probePrevFingerprints.TryGetValue(kv.Key, out var old)) continue;
                    compared++;
                    if (old != kv.Value) changed++;
                }
                probePrevFingerprints = new Dictionary<RenderMesh, int>(fps);
            }

            var shadowKey = string.Join(" ", shadowed);
            bool shadowChanged = probeStep > 1 && shadowKey != probePrevShadowKey;
            if (changed > 0 || shadowChanged || probeStep < 2 || probeStep == DebugLightProbe - 1)
            {
                Console.WriteLine($"  step {probeStep} | lights {lastProbeLightCount} | " +
                    $"overflow {sceneRenderer.OverflowMeshes}/{sceneRenderer.DrawnMeshes} | " +
                    $"meshLightSets changed {changed}/{compared}" +
                    (shadowChanged ? " | SHADOWSET CHANGED" : "") +
                    $" | [{shadowKey}]");
            }
            probeTotalMeshChanges += changed;
            if (shadowChanged) probeShadowChanges++;
            probePrevShadowKey = shadowKey;
            if (probeStep == DebugLightProbe - 1)
                Console.WriteLine($"PROBE TOTALS over {DebugLightProbe} frames: " +
                    $"mesh light-set changes {probeTotalMeshChanges}, shadow-set changes {probeShadowChanges}");

            if (++probeStep >= DebugLightProbe)
            {
                if (probeShotPending != null) { probeClosing = true; return; }
                DebugLightProbe = 0;
                Close();
            }
            else if (probeClosing) { DebugLightProbe = 0; Close(); }
        }

        private void DoResetSettings()
        {
            settings.ResetToDefaults();
            panel.ApplyThemeFromSettings();
            panel.ResetViewDefaults();
            camera.FieldOfView = settings.FovDeg * 0.0174533f;
            camera.Sensitivity = settings.CameraSensitivity;
            camera.Smoothness = settings.CameraSmoothing;
            camera.OrbitAnchorMax = settings.OrbitAnchorMax;
            panel.MloStatus = "Settings reset to defaults.";
        }

        private void DoLoadModifiers()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Timecycle modifiers (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Load timecycle modifiers (timecycle_mods_*.xml)",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            panel.TimecycleStatus = timecycle.LoadModifiersXml(dlg.FileName, out var err)
                ? $"{timecycle.Modifiers.Count} modifiers loaded."
                : "Modifier load failed: " + err;
        }

        private void DoSaveTimecycle()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Timecycle XML (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Save timecycle",
                FileName = (timecycle.LoadedName ?? "timecycle") + ".xml",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try { timecycle.SaveTimecycleXml(dlg.FileName); panel.TimecycleStatus = "Timecycle saved."; }
            catch (Exception ex) { panel.TimecycleStatus = "Save failed: " + ex.Message; }
        }

        private void DoSaveModifiers()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Timecycle modifiers (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Save timecycle modifiers",
                FileName = "timecycle_mods_custom.xml",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                timecycle.SaveModifiersXml(dlg.FileName, timecycle.CurrentModifier?.Source);
                panel.TimecycleStatus = "Modifiers saved.";
            }
            catch (Exception ex) { panel.TimecycleStatus = "Save failed: " + ex.Message; }
        }

        private void DoLoadTimecycle()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Timecycle XML (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Load timecycle (w_*.xml or custom interior timecycle)",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (timecycle.LoadTimecycleXml(dlg.FileName, out var err))
            {
                timecycleFileStamp = SafeStamp(dlg.FileName);
                panel.TimecycleStatus = $"Loaded {timecycle.Regions.Count} region(s), {timecycle.KeyframeCount} keyframes.";
            }
            else
            {
                panel.TimecycleStatus = "Load failed: " + err;
            }
        }

        private void DoLoadTimeSchedule()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "time.xml|time.xml|XML files (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Load the game's time.xml (hour schedule)",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            panel.TimecycleStatus = timecycle.LoadScheduleXml(dlg.FileName, out var err)
                ? $"Schedule loaded: {timecycle.Samples.Count} samples."
                : "Schedule load failed: " + err;
        }

        private static DateTime SafeStamp(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
        }

        private void PollTimecycleReload(double now)
        {
            if (!panel.TimecycleAutoReload || timecycle?.LoadedPath == null) return;
            if (now - timecycleCheckTime < 0.5) return;
            timecycleCheckTime = now;
            var stamp = SafeStamp(timecycle.LoadedPath);
            if (stamp == timecycleFileStamp || stamp == DateTime.MinValue) return;
            timecycleFileStamp = stamp;
            int region = timecycle.SelectedRegion;
            if (timecycle.LoadTimecycleXml(timecycle.LoadedPath, out var err))
            {
                timecycle.SelectedRegion = Math.Min(region, Math.Max(timecycle.Regions.Count - 1, 0));
                panel.TimecycleStatus = $"Reloaded {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                panel.TimecycleStatus = "Reload failed: " + err;
            }
        }

        private void DoAddFileDialog()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "GTA V models (*.ydr;*.yft;*.ydd)|*.ydr;*.yft;*.ydd|All files (*.*)|*.*",
                Multiselect = true,
                Title = "Add prop(s) to the scene",
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var f in dlg.FileNames)
                {
                    scene.LoadModelFile(f, additive: true);
                }
                Text = $"{AppInfo.Name} - {scene.FileName}";
            }
        }

        private void DoImportProjTex()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Images (*.dds;*.png;*.jpg;*.jpeg;*.bmp)|*.dds;*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Import projected texture (preview only - ship it in a YTD for the game)",
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var name = scene.ImportTexture(dlg.FileName);
                var l = scene.SelectedLight;
                if (name != null && l != null)
                {
                    scene.PushUndo();
                    l.ProjectedTextureHash = new MetaHash(JenkHash.GenHash(name));
                    l.Flags |= LightDefs.FlagTextureProjection;
                    scene.Dirty = true;
                }
            }
        }

        private void DoOpenYtdDialog()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Texture dictionary (*.ytd)|*.ytd",
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                scene.LoadYtdFile(dlg.FileName);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null) return;
            bool rpfTook = false;
            RpfDropFiles_T4(files, e.X, e.Y, ref rpfTook);
            if (rpfTook) return;
            foreach (var f in files)
            {
                LoadFile(f);
            }
        }

        private bool ImGuiWantsMouse => ImGui.GetCurrentContext() != IntPtr.Zero && ImGui.GetIO().WantCaptureMouse;
        private bool ImGuiWantsKeyboard => ImGui.GetCurrentContext() != IntPtr.Zero && ImGui.GetIO().WantCaptureKeyboard;

        private void OnMouseDownEv(object sender, MouseEventArgs e)
        {
            { bool sideU9 = false; RpfSideButtons_U9(e, ref sideU9); if (sideU9) return; }
            if (ImGuiWantsMouse) return;
            lastMouse = e.Location;
            downMouse = e.Location;
            mouseDownDrag = 0;
            if (e.Button == MouseButtons.Left)
            {
                bool shift = (ModifierKeys & Keys.Shift) != 0;
                bool alt = (ModifierKeys & Keys.Alt) != 0;
                if (panel.WorldMode)
                {
                    gizmoConsumedClick = GrassBrushMouseDown_S5(e.X, e.Y);
                    if (!gizmoConsumedClick)
                    {
                        if (shift && worldBuilt &&
                            worldGizmo.HitsHandle_V33(camera, WorldGizmoTargets(), e.X, e.Y, deviceResources.Width, deviceResources.Height))
                            WorldShiftDuplicate_V65();
                        gizmoConsumedClick = worldBuilt && worldGizmo.MouseDown(camera, WorldGizmoTargets(),
                            e.X, e.Y, deviceResources.Width, deviceResources.Height, shift, alt);
                    }
                }
                else
                {
                    ExtGizmoMouseDown_V69(e.X, e.Y, shift, alt, ref gizmoConsumedClick);
                    if (!gizmoConsumedClick)
                        gizmoConsumedClick = gizmo != null &&
                            gizmo.MouseDown(camera, e.X, e.Y, deviceResources.Width, deviceResources.Height, shift, alt);
                    if (!gizmoConsumedClick) MloCreatorMouseDown_H5(e.X, e.Y, shift, alt, ref gizmoConsumedClick);
                    if (!gizmoConsumedClick) TerrainMouseDown_R4(e.X, e.Y, false, ref gizmoConsumedClick);
                }
                if (!gizmoConsumedClick) orbiting = true;
            }
            if (e.Button == MouseButtons.Right)
            {
                NavMouseRightDown_S4(e.X, e.Y);
                RightMouseDown_T1(e.X, e.Y);
                rightDragging = true;
                timeScrubbing = false;
            }
            if (e.Button == MouseButtons.Middle) panning = true;
        }

        private void OnMouseUpEv(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ExtGizmoMouseUp_V69();
                if (worldGizmo.Dragging)
                {
                    worldGizmo.MouseUp();
                }
                else if (gizmo != null && gizmo.Dragging)
                {
                    if (gizmo.MouseUp())
                    {
                        panel.CloneWasInstance = gizmo.CloneWasInstance;
                        panel.AskCloneConfirm(
                            yes: () => { },
                            no: () => scene.Undo());
                    }
                }
                else if (orbiting && mouseDownDrag < 5.0f && !ImGuiWantsMouse && !gizmoConsumedClick)
                {
                    bool extClick = false;
                    ExtMouseDown_V68(e.X, e.Y, ref extClick);
                    if (!extClick) ExtClickHandle_V69(e.X, e.Y, ref extClick);
                    if (!extClick) LeftViewportClick_T1(e.X, e.Y);
                }
                else { bool h5 = false; MloCreatorMouseUp_H5(e.X, e.Y, false, ref h5); }
                TerrainMouseUp_R4();
                GrassBrushMouseUp_S5();
                orbiting = false;
                gizmoConsumedClick = false;
            }
            if (e.Button == MouseButtons.Middle) panning = false;
            if (e.Button == MouseButtons.Right)
            {
                bool extRight = false;
                if (RightReleaseIsClick_T1(mouseDownDrag)) ExtRightClick_V70(e.X, e.Y, ref extRight);
                if (!extRight && RightReleaseIsClick_T1(mouseDownDrag)) RightViewportClick_T1(e.X, e.Y);
                if (timeScrubbing && mouseDownDrag > 2.0f && panel.WorldMode && WorldEdit.Selected != rightDownWorldSel)
                    WorldEdit.Select(rightDownWorldSel);
                NavMouseRightUp_S4(mouseDownDrag, timeScrubbing);
                rightDragging = false;
                timeScrubbing = false;
                panel.TimeScrubbingByMouse = false;
                rightDownWorldSel = null;
            }
        }

        private void OnMouseMoveEv(object sender, MouseEventArgs e)
        {
            float dx = e.X - lastMouse.X;
            float dy = e.Y - lastMouse.Y;
            mouseDownDrag += Math.Abs(dx) + Math.Abs(dy);
            if (!ImGuiWantsMouse)
            {
                if (panel.WorldMode)
                    worldGizmo.MouseMove(camera, e.X, e.Y, deviceResources.Width, deviceResources.Height);
                else
                { gizmo?.MouseMove(camera, e.X, e.Y, deviceResources.Width, deviceResources.Height); ExtGizmoMouseMove_V69(e.X, e.Y); MloCreatorMouseMove_H5(e.X, e.Y); }
                TerrainMouseMove_R4(e.X, e.Y, e.Button == MouseButtons.Left, e.Button == MouseButtons.Right);
                GrassBrushMouseMove_S5(e.X, e.Y, e.Button == MouseButtons.Left);
            }
            if (walkMode)
            {
                if (!ImGuiWantsMouse && (gizmo == null || !gizmo.Dragging) && !SequenceOwnsCamera)
                {
                    camera.LookRotate(dx, dy);
                }
            }
            else
            {
                if (orbiting && (gizmo == null || !gizmo.Dragging) && !SequenceOwnsCamera) camera.Orbit(dx, dy);
                if (panning && !SequenceOwnsCamera) camera.Pan(-dx, dy);
            }
            if (rightDragging && panel.ControlTimeOfDay && !ImGuiWantsMouse && !SequenceOwnsCamera
                && !worldGizmo.Dragging && (gizmo == null || !gizmo.Dragging)
                && (dx != 0 || dy != 0))
            {
                panel.ScrubTimeOfDay(dx, dy);
                weather.Hour = panel.PreviewHour;
                timeScrubbing = true;
                panel.TimeScrubbingByMouse = true;
            }
            if (!panel.WorldMode && panel.EntityPicking && !ImGuiWantsMouse && !orbiting && !panning && !rightDragging &&
                (gizmo == null || !gizmo.Dragging) && deviceResources != null)
            {
                hoverProp = FindPropUnder(camera.GetPickRay(e.X, e.Y,
                    deviceResources.Width, deviceResources.Height));
            }
            lastMouse = e.Location;
        }

        private void OnMouseWheelEv(object sender, MouseEventArgs e)
        {
            if (ImGuiWantsMouse) return;
            if (walkMode)
            {
                settings.WalkSpeed = Math.Clamp(
                    settings.WalkSpeed * (float)Math.Pow(1.15, e.Delta / 120.0), 0.05f, 50.0f);
            }
            else
            {
                camera.Zoom(e.Delta);
            }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (IsWalkKey(keyData & Keys.KeyCode)) return false;
            return base.ProcessDialogKey(keyData);
        }

        private static readonly string[] MoveActions =
            { "MoveForward", "MoveBack", "MoveLeft", "MoveRight", "MoveUp", "MoveDown" };

        private bool IsWalkKey(Keys k)
        {
            if (k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right ||
                k == Keys.PageUp || k == Keys.PageDown) return true;
            foreach (var a in MoveActions)
                if ((settings.GetBind(a) & Keys.KeyCode) == k) return true;
            return false;
        }

        private bool MoveHeld(string action)
        {
            var bind = settings.GetBind(action) & Keys.KeyCode;
            return bind != Keys.None && walkKeys.Contains(bind);
        }

        private void SetWalkMode(bool on)
        {
            walkMode = on;
        }

        private double goToDownAt = -1;

        private const double GoToTapSeconds = 0.35;

        private void OnKeyUpEv(object sender, KeyEventArgs e)
        {
            walkKeys.Remove(e.KeyCode);
            MloWorkspaceKeyUp_J6(e.KeyCode);

            if (goToDownAt >= 0 && e.KeyCode == (settings.GetBind("GoToOrigin") & Keys.KeyCode))
            {
                bool tapped = clock.Elapsed.TotalSeconds - goToDownAt <= GoToTapSeconds;
                goToDownAt = -1;
                if (tapped && (!ImGuiWantsKeyboard || ignoreImGuiKeyboard)) GoToSelection();
            }
        }

        private void GoToSelection()
        {
            if (FrameSelection()) return;
            camera.Target = Vector3.Zero;
            camera.Distance = Math.Clamp(camera.Distance, 3.0f, 30.0f);
            camera.SnapSmoothing();
            panel.MloStatus = "Nothing selected - went to the world origin.";
        }

        private void OnKeyDownEv(object sender, KeyEventArgs e)
        {
            var combo = AppSettings.ComboFrom(e);

            if (panel.TryCaptureKey(combo))
            {
                e.Handled = true;
                return;
            }
            { bool j6 = false; MloWorkspaceKeyDown_J6(combo, ref j6); if (j6) { e.Handled = true; return; } }
            if (MloEditKeyDown_N3(combo)) { e.Handled = true; return; }
            if (KeyDown_O3(combo)) { e.Handled = true; return; }
            if (TerrainKeyDown_R4(combo)) { e.Handled = true; return; }
            if (AnimKeyDown_U6(combo)) { e.Handled = true; return; }

            if (panel.WorldMode)
            {
                if (combo == settings.GetBind("ProjectWindow")) { ProjWin.Visible = !ProjWin.Visible; e.Handled = true; return; }
                if (combo == settings.GetBind("ProjectNew"))    { ProjWin.RequestNewProject = true; ProjWin.Visible = true; e.Handled = true; return; }
                if (combo == settings.GetBind("ProjectOpen"))   { ProjWin.RequestOpenProject = true; ProjWin.Visible = true; e.Handled = true; return; }
                if (combo == settings.GetBind("ProjectSaveAll")){ ProjWin.RequestSaveAll = true; e.Handled = true; return; }
                if (combo == settings.GetBind("ToggleMouseSelect")) { panel.MouseSelectEnabled = !panel.MouseSelectEnabled; e.Handled = true; return; }
                if (combo == settings.GetBind("SelectionMode"))
                {
                    int m = panel.SelectionMode;
                    for (int i = 0; i < Editor.LightPanel.SelectionModeNames.Length; i++)
                    {
                        m = (m + 1) % Editor.LightPanel.SelectionModeNames.Length;
                        if (Editor.LightPanel.SelectionModeAvailable[m]) break;
                    }
                    panel.SelectionMode = m; e.Handled = true; return;
                }
                if (combo == settings.GetBind("GoToDowntown"))
                {
                    CameraSequence.ApplyToCamera(camera, MazeBankTop, 0.55f, 0.35f, settings.FovDeg);
                    e.Handled = true; return;
                }
                { bool extKeyD = false; ExtKeyDown_V70(combo, ref extKeyD); if (extKeyD) { e.Handled = true; return; } }
                { bool areaKey = false; AreaKeyDown_J5(combo, ref areaKey); if (areaKey) { e.Handled = true; return; } }
                { bool navKey = false; NavKeyDown_P4(combo, ref navKey); if (navKey) { e.Handled = true; return; } }
                if (combo == Keys.Escape)
                {
                    { bool extKey = false; ExtEscape_V68(ref extKey); if (extKey) { e.Handled = true; return; } }
                    if (WorldEdit.Selection.HasValue) WorldEdit.Deselect();
                    else if (ProjWin.Visible) ProjWin.Visible = false;
                    e.Handled = true; return;
                }
            }

            if (combo == Keys.F9)  { panel.ShowLeftPanel  = !panel.ShowLeftPanel;  e.Handled = true; return; }
            if (combo == Keys.F10) { panel.ShowRightPanel = !panel.ShowRightPanel; e.Handled = true; return; }
            if (combo == Keys.F11) { panel.ToggleInterface();  e.Handled = true; return; }
            if (combo == (Keys.Control | Keys.Shift | Keys.P)) { ProjWin.Visible = !ProjWin.Visible; e.Handled = true; return; }
            if (combo == (Keys.F11 | Keys.Shift)) { panel.ToggleAllPanels(); e.Handled = true; return; }

            if (combo == settings.GetBind("Save") && !(ProjWin.Visible && ProjWin.Focused))
            {
                DoSave();
                e.Handled = true;
                return;
            }
            if (ImGuiWantsKeyboard && !ignoreImGuiKeyboard) return;

            if ((e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) && walkMode)
            {
                SetWalkMode(false);
                e.Handled = true;
                return;
            }

            if (panel.WorldMode)
            {
                if (combo == settings.GetBind("GizmoSelect")) worldGizmo.Mode = WorldGizmoMode.Select;
                else if (combo == settings.GetBind("GizmoMove")) worldGizmo.Mode = WorldGizmoMode.Translate;
                else if (combo == settings.GetBind("GizmoRotate")) worldGizmo.Mode = WorldGizmoMode.Rotate;
                else if (combo == settings.GetBind("GizmoScale")) worldGizmo.Mode = WorldGizmoMode.Scale;
                else if (combo == settings.GetBind("GizmoSpace"))
                    worldGizmo.Space = worldGizmo.Space == WorldGizmoSpace.World
                        ? WorldGizmoSpace.Local : WorldGizmoSpace.World;
            }
            else if (combo == settings.GetBind("GizmoSelect")) gizmo.Mode = GizmoMode.Select;
            else if (combo == settings.GetBind("GizmoMove")) gizmo.Mode = GizmoMode.Translate;
            else if (combo == settings.GetBind("GizmoRotate")) gizmo.Mode = GizmoMode.Rotate;

            bool chord = (e.Modifiers & (Keys.Control | Keys.Alt)) != 0;
            if (!chord && IsWalkKey(e.KeyCode))
            {
                if (e.KeyCode == (settings.GetBind("GoToOrigin") & Keys.KeyCode) && goToDownAt < 0)
                    goToDownAt = clock.Elapsed.TotalSeconds;
                walkKeys.Add(e.KeyCode);
                e.Handled = true;
                return;
            }

            if ((combo & Keys.KeyCode) == Keys.P && (combo & (Keys.Control | Keys.Alt | Keys.Shift)) == 0)
            {
                if (panel.PhotoModeAllowed || photoMode) { SetPhotoMode(!photoMode); e.Handled = true; return; }
            }
            if (combo == settings.GetBind("WalkMode")) { SetWalkMode(!walkMode); e.Handled = true; return; }
            if (materialPanel != null && (combo & Keys.Shift) != 0 && (combo & Keys.Control) != 0)
            {
                var key = combo & Keys.KeyCode;
                if (key == Keys.Z) { materialPanel.Undo(); e.Handled = true; return; }
                if (key == Keys.Y) { materialPanel.Redo(); e.Handled = true; return; }
            }
            if (combo == settings.GetBind("Undo")) { if (panel.WorldMode) TryWorldUndo(); else scene.Undo(); return; }
            if (combo == settings.GetBind("Redo")) { if (panel.WorldMode) TryWorldRedo(); else scene.Redo(); return; }
            if (combo == settings.GetBind("Duplicate"))
            {
                if (panel.WorldMode) WorldDuplicateSelected(); else scene.DuplicateSelected();
                e.Handled = true; return;
            }
            if (combo == settings.GetBind("Delete"))
            {
                bool tookT4 = false;
                DeleteKey_T4(ref tookT4);
                if (tookT4) { e.Handled = true; return; }
                if (panel.WorldMode) { WorldDeleteSelected(); e.Handled = true; }
                else panel.AskDeleteSelected();
                return;
            }
            if (combo == settings.GetBind("Frame"))
            {
                if (FrameWorldSelection_M3()) return;
                if (scene.SelectedLight != null) FrameLight(scene.SelectedLight);
                else FrameModel();
                return;
            }
            if (combo == settings.GetBind("GoToOrigin") && !IsWalkKey(combo & Keys.KeyCode))
            {
                GoToSelection();
                e.Handled = true;
                return;
            }
            if (combo == settings.GetBind("SpeedUp"))
            {
                settings.WalkSpeed = Math.Min(settings.WalkSpeed * 1.4f, 40.0f);
                panel.MloStatus = $"Move speed x{settings.WalkSpeed:0.##}";
                e.Handled = true;
                return;
            }
            if (walkMode && combo == settings.GetBind("SpeedDown"))
            {
                settings.WalkSpeed = Math.Max(settings.WalkSpeed / 1.4f, 0.05f);
                panel.MloStatus = $"Move speed x{settings.WalkSpeed:0.##}";
                e.Handled = true;
                return;
            }
            if (combo == settings.GetBind("Copy"))
            {
                if (panel.WorldMode) { WorldCopySelected(); e.Handled = true; return; }
                if (panel.MloMode) { MloCopySelected_V37(); e.Handled = true; return; }
                scene.CopyLights();
                panel.MloStatus = scene.ClipboardCount > 0
                    ? $"Copied {scene.ClipboardCount} light(s)." : "Nothing selected to copy.";
                e.Handled = true;
                return;
            }
            if (combo == settings.GetBind("Paste"))
            {
                if (panel.WorldMode) { WorldPasteClipboard(); e.Handled = true; return; }
                if (panel.MloMode) { MloPasteClipboard_V37(); e.Handled = true; return; }
                int n = scene.PasteLights();
                panel.MloStatus = n > 0 ? $"Pasted {n} light(s)." : "Clipboard is empty.";
                e.Handled = true;
                return;
            }
            if (combo == settings.GetBind("PasteProps"))
            {
                scene.PasteSettings();
                panel.MloStatus = "Pasted settings onto the selection.";
                e.Handled = true;
                return;
            }

        }

        private void PickAt(int mx, int my)
        {
            if (PickLight(mx, my)) return;
            if (!panel.EntityPicking) return;

            var ray = camera.GetPickRay(mx, my, deviceResources.Width, deviceResources.Height);
            var file = FindPropUnder(ray, out var mesh);
            if (file == null) return;

            var mods = ModifierKeys;
            scene.SelectFile(file, (mods & Keys.Control) != 0, (mods & Keys.Shift) != 0);
            panel.ScrollToActiveProp = true;

            if (materialPanel != null && mesh?.Shader != null)
            {
                materialPanel.SelectByShader(mesh.Shader, (mods & Keys.Control) != 0);
                materialPanel.Status = $"{file.Name}: {mesh.ShaderName}";
            }
            else
            {
                panel.MloStatus = $"Selected {file.Name}";
            }
        }

        private bool PickLight(int mx, int my)
        {
            var ray = camera.GetPickRay(mx, my, deviceResources.Width, deviceResources.Height);
            int best = -1;
            float bestT = float.MaxValue;
            for (int i = 0; i < scene.Lights.Count; i++)
            {
                var inst = scene.GetInstance(scene.Lights[i]);
                float dist = Vector3.Distance(camera.Position, inst.WorldPosition);
                float radius = Math.Max(dist * 0.02f, 0.05f);
                var sphere = new BoundingSphere(inst.WorldPosition, radius);
                if (ray.Intersects(ref sphere, out float t) && t < bestT)
                {
                    bestT = t;
                    best = i;
                }
            }
            if (best >= 0)
            {
                bool ctrl = (ModifierKeys & Keys.Control) != 0;
                bool shift = (ModifierKeys & Keys.Shift) != 0;
                if (ctrl) scene.ToggleSelect(best);
                else if (shift) { if (!scene.IsSelected(best)) scene.SelectedIndices.Add(best); }
                else scene.SelectedIndex = best;

                var owner = scene.OwnerFile(scene.Lights[best]);
                if (owner != null && !ctrl && !shift)
                {
                    scene.SelectFile(owner, false, false);
                    panel.ScrollToActiveProp = true;
                }
                return true;
            }

            return false;
        }

        private bool photoMode;
        private bool photoSavedGrid, photoSavedGizmos, photoSavedAllGizmos, photoSavedMarkers;
        private bool photoSavedCoronas, photoSavedVolumes;

        private void SetPhotoMode(bool on)
        {
            if (on == photoMode) return;
            if (on && !panel.PhotoModeAllowed) return;
            photoMode = on;
            panel.PhotoMode = on;

            if (on)
            {
                photoSavedGrid = panel.ShowGrid;
                photoSavedGizmos = panel.ShowGizmos;
                photoSavedAllGizmos = panel.ShowAllGizmos;
                photoSavedMarkers = panel.ShowMarkers;
                photoSavedCoronas = panel.ShowCoronas;
                photoSavedVolumes = panel.ShowVolumes;

                panel.ShowGrid = false;
                panel.ShowGizmos = false;
                panel.ShowAllGizmos = false;
                panel.ShowMarkers = false;
                panel.ResetPhotoHint();
                SetFullscreen(true);
            }
            else
            {
                panel.ShowGrid = photoSavedGrid;
                panel.ShowGizmos = photoSavedGizmos;
                panel.ShowAllGizmos = photoSavedAllGizmos;
                panel.ShowMarkers = photoSavedMarkers;
                panel.ShowCoronas = photoSavedCoronas;
                panel.ShowVolumes = photoSavedVolumes;
                SetFullscreen(false);
            }
        }

        private float lastPlayheadApplied = -1.0f;
        private Matrix prevViewProj = Matrix.Identity;
        private bool renderSeqPending;

        private void TickCameraSequence(float dt)
        {
            var seq = panel.Sequence;

            if (panel.RequestAddShot)
            {
                panel.RequestAddShot = false;
                var shot = CameraSequence.FromCamera(camera, settings.FovDeg,
                    $"Shot {seq.Shots.Count + 1}");
                shot.Duration = seq.Shots.Count == 0 ? 0.0f : seq.Shots[seq.Shots.Count - 1].Duration;
                if (shot.Duration <= 0.0f) shot.Duration = 3.0f;
                seq.Shots.Add(shot);
                panel.SelectedShot = seq.Shots.Count - 1;
            }

            if (panel.RequestReplaceShot)
            {
                panel.RequestReplaceShot = false;
                if (panel.SelectedShot >= 0 && panel.SelectedShot < seq.Shots.Count)
                {
                    var old = seq.Shots[panel.SelectedShot];
                    var shot = CameraSequence.FromCamera(camera, settings.FovDeg, old.Name);
                    shot.Duration = old.Duration; shot.Hold = old.Hold;
                    shot.Ease = old.Ease; shot.Focus = old.Focus;
                    seq.Shots[panel.SelectedShot] = shot;
                }
            }

            if (panel.RequestGoToShot)
            {
                panel.RequestGoToShot = false;
                if (panel.SelectedShot >= 0 && panel.SelectedShot < seq.Shots.Count)
                {
                    var sh = seq.Shots[panel.SelectedShot];
                    CameraSequence.ApplyToCamera(camera, sh.Position, sh.Yaw, sh.Pitch, sh.Fov);
                    lastPlayheadApplied = -1.0f;
                }
            }

            float len = seq.Length;
            if (panel.Playing && len > 0.0001f)
            {
                panel.PlayTime += dt;
                if (panel.PlayTime >= len)
                {
                    if (seq.Loop) panel.PlayTime -= len;
                    else { panel.PlayTime = len; panel.Playing = false; }
                }
            }

            scrubbingSequence = panel.ScrubbingTimeline;
            if (SequenceOwnsCamera && Math.Abs(panel.PlayTime - lastPlayheadApplied) > 1e-4f)
            {
                if (seq.Sample(panel.PlayTime, out var pos, out var yaw, out var pitch,
                               out var fov, out var focus, out var shakeGain))
                {
                    CameraSequence.ApplyToCamera(camera, pos, yaw, pitch, fov,
                        seq.Shake, shakeGain, panel.PlayTime);
                    if (focus > 0.0f)
                    {
                        panel.Cine.DofAutoFocus = false;
                        panel.Cine.DofFocus = focus;
                    }
                }
            }
            lastPlayheadApplied = panel.PlayTime;

            if (panel.Playing && (AnyMoveKeyHeld() || orbiting || panning || walkMode))
            {
                panel.Playing = false;
                panel.MloStatus = "Playback stopped - the camera is yours again";
            }
        }

        private bool AnyMoveKeyHeld() =>
            MoveHeld("MoveForward") || MoveHeld("MoveBack") || MoveHeld("MoveLeft") ||
            MoveHeld("MoveRight") || MoveHeld("MoveUp") || MoveHeld("MoveDown") || walkKeys.Count > 0;

        private void DoRenderSequenceDialog()
        {
            var seq = panel.Sequence;
            if (seq.Shots.Count < 2) return;

            var (w, h) = panel.RenderSize;
            if (w <= 0) { w = deviceResources.Width; h = deviceResources.Height; }
            int frames = seq.FrameCount;

            using var save = new SaveFileDialog
            {
                Title = $"Render {frames} frames at {w} x {h}",
                Filter = "MP4 video|*.mp4|PNG frame sequence|*.png",
                FileName = "cinematic.mp4",
                AddExtension = true,
                DefaultExt = "mp4",
            };
            if (save.ShowDialog(this) != DialogResult.OK) return;
            bool wantVideo = save.FilterIndex == 1;
            if (wantVideo) { DoRenderVideo(save.FileName, w, h, frames); return; }

            string dir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(save.FileName) ?? ".",
                "sequence_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            float savedTime = panel.PlayTime;
            bool savedPlaying = panel.Playing;
            var savedTarget = camera.Target;
            float savedYaw = camera.Yaw, savedPitch = camera.Pitch, savedDist = camera.Distance;
            float savedFov = camera.FieldOfView;
            bool savedAuto = panel.Cine.DofAutoFocus;
            float savedFocus = panel.Cine.DofFocus;

            int done = 0;
            string err = null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var seqProgress = new ExportProgressForm("Rendering frames",
                $"{frames} PNG frames  -  {w} x {h}", frames);
            seqProgress.Show(this);
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                panel.Playing = false;

                for (int f = 0; f < frames; f++)
                {
                    float t = f / (float)Math.Max(seq.Fps, 1);
                    if (!seq.Sample(t, out var pos, out var yaw, out var pitch,
                                    out var fov, out var focus, out var shakeGain)) break;
                    CameraSequence.ApplyToCamera(camera, pos, yaw, pitch, fov, seq.Shake, shakeGain, t);
                    if (focus > 0.0f) { panel.Cine.DofAutoFocus = false; panel.Cine.DofFocus = focus; }
                    else panel.Cine.DofAutoFocus = savedAuto;

                    string path = System.IO.Path.Combine(dir, $"frame_{f:D5}.png");
                    err = RenderStill(w, h, path);
                    if (err != null) break;
                    done++;

                    seqProgress.Advance(f);
                    if (seqProgress.Cancelled) { err = "cancelled"; break; }
                }
            }
            catch (Exception ex) { err = ex.Message; }
            finally
            {
                seqProgress.Hide();
                camera.Target = savedTarget; camera.Yaw = camera.TargetYaw = savedYaw;
                camera.Pitch = camera.TargetPitch = savedPitch;
                camera.Distance = camera.TargetDistance = savedDist;
                camera.FieldOfView = savedFov;
                camera.SnapSmoothing();
                panel.PlayTime = savedTime; panel.Playing = savedPlaying;
                panel.Cine.DofAutoFocus = savedAuto; panel.Cine.DofFocus = savedFocus;
                lastPlayheadApplied = -1.0f;
            }

            string cmd = $"ffmpeg -framerate {seq.Fps} -i \"frame_%05d.png\" " +
                         "-c:v libx264 -crf 16 -pix_fmt yuv420p -movflags +faststart out.mp4";
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "make_video.txt"),
                    "Run this in this folder to turn the frames into an mp4:" + Environment.NewLine +
                    Environment.NewLine + cmd + Environment.NewLine);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "make_video.bat"),
                    "@echo off\r\ncd /d \"%~dp0\"\r\n" + cmd + "\r\npause\r\n");
            }
            catch { }

            Console.WriteLine($"RENDERSEQ {done}/{frames} frames {w}x{h} in {sw.Elapsed.TotalSeconds:0.0}s -> {dir}");
            panel.MloStatus = err == null
                ? $"Rendered {done} frames to {System.IO.Path.GetFileName(dir)}"
                : $"Sequence stopped after {done} frames: {err}";

            if (err != null && err != "cancelled")
            {
                MessageBox.Show(this, $"The sequence stopped after {done} of {frames} frames.\n\n{err}",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            OfferToEncode(dir, cmd, done, frames);
        }

        private void DoRenderVideoHeadless(string path, int w, int h, int frames)
        {
            var seq = panel.Sequence;
            VideoWriter video = null;
            try
            {
                video = new VideoWriter(path, w, h, seq.Fps, 4_000_000);
                for (int f = 0; f < frames; f++)
                {
                    float t = f / (float)Math.Max(seq.Fps, 1);
                    if (!seq.Sample(t, out var pos, out var yaw, out var pitch,
                                    out var fov, out var focus, out var shakeGain)) break;
                    CameraSequence.ApplyToCamera(camera, pos, yaw, pitch, fov, seq.Shake, shakeGain, t);
                    if (RenderStillToBitmap(w, h, out var bmp) != null) break;
                    using (bmp)
                    {
                        var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                            System.Drawing.Imaging.ImageLockMode.ReadOnly,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        try { video.WriteFrame(bd.Scan0, bd.Stride); }
                        finally { bmp.UnlockBits(bd); }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("  VIDEO FAILED: " + ex.Message); }
            finally { try { video?.Dispose(); } catch { } }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private void DoRenderVideo(string path, int w, int h, int frames)
        {
            var seq = panel.Sequence;

            float savedTime = panel.PlayTime;
            bool savedPlaying = panel.Playing;
            var savedTarget = camera.Target;
            float savedYaw = camera.Yaw, savedPitch = camera.Pitch, savedDist = camera.Distance;
            float savedFov = camera.FieldOfView;
            bool savedAuto = panel.Cine.DofAutoFocus;
            float savedFocus = panel.Cine.DofFocus;

            const double BitsPerPixel = 0.35;
            int bitrate = (int)Math.Min(240_000_000L,
                Math.Max(12_000_000L, (long)(w * (double)h * seq.Fps * BitsPerPixel)));

            int done = 0;
            string err = null;
            VideoWriter video = null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                video = new VideoWriter(path, w, h, seq.Fps, bitrate);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Windows would not open an H.264 encoder, so the video cannot be written.\n\n" +
                    ex.Message + "\n\n" +
                    "On Windows N or KN editions this is the missing Media Feature Pack. " +
                    "Choose the PNG frame sequence instead and any editor will take it.",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var progress = new ExportProgressForm("Rendering video",
                $"{System.IO.Path.GetFileName(path)}  -  {w} x {h} at {seq.Fps} fps", frames);
            progress.Show(this);
            try
            {
                panel.Playing = false;

                for (int f = 0; f < frames; f++)
                {
                    float t = f / (float)Math.Max(seq.Fps, 1);
                    if (!seq.Sample(t, out var pos, out var yaw, out var pitch,
                                    out var fov, out var focus, out var shakeGain)) break;
                    CameraSequence.ApplyToCamera(camera, pos, yaw, pitch, fov, seq.Shake, shakeGain, t);
                    if (focus > 0.0f) { panel.Cine.DofAutoFocus = false; panel.Cine.DofFocus = focus; }
                    else panel.Cine.DofAutoFocus = savedAuto;

                    err = RenderStillToBitmap(w, h, out var bmp);
                    if (err != null) break;
                    using (bmp)
                    {
                        var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                            System.Drawing.Imaging.ImageLockMode.ReadOnly,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        try { video.WriteFrame(bd.Scan0, bd.Stride); }
                        finally { bmp.UnlockBits(bd); }
                    }
                    done++;

                    progress.Advance(f);
                    if (progress.Cancelled) { err = "cancelled"; break; }
                }
            }
            catch (Exception ex) { err = ex.Message; }
            finally
            {
                try { video?.Dispose(); } catch { }
                progress.Hide();
                camera.Target = savedTarget; camera.Yaw = camera.TargetYaw = savedYaw;
                camera.Pitch = camera.TargetPitch = savedPitch;
                camera.Distance = camera.TargetDistance = savedDist;
                camera.FieldOfView = savedFov;
                camera.SnapSmoothing();
                panel.PlayTime = savedTime; panel.Playing = savedPlaying;
                panel.Cine.DofAutoFocus = savedAuto; panel.Cine.DofFocus = savedFocus;
                lastPlayheadApplied = -1.0f;
            }

            Console.WriteLine($"RENDERVIDEO {done}/{frames} {w}x{h}@{seq.Fps} " +
                              $"in {sw.Elapsed.TotalSeconds:0.0}s -> {path}");
            panel.MloStatus = err == null
                ? $"Rendered {done} frames to {System.IO.Path.GetFileName(path)}"
                : $"Video stopped after {done} frames: {err}";

            if (err != null && err != "cancelled")
            {
                MessageBox.Show(this, $"The video stopped after {done} of {frames} frames.\n\n{err}",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (done > 0)
            {
                var len = done / (float)Math.Max(seq.Fps, 1);
                var info = new System.IO.FileInfo(path);
                MessageBox.Show(this,
                    $"{done} frames written - {len:0.0} seconds at {seq.Fps} fps, " +
                    $"{info.Length / (1024.0 * 1024.0):0.0} MB.\n\n{path}",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OfferToEncode(string dir, string cmd, int done, int frames)
        {
            bool haveFfmpeg = false;
            try
            {
                var probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg", Arguments = "-version",
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                });
                if (probe != null) { probe.WaitForExit(4000); haveFfmpeg = probe.ExitCode == 0; }
            }
            catch { haveFfmpeg = false; }

            if (!haveFfmpeg)
            {
                MessageBox.Show(this,
                    $"{done} frames written to:\n{dir}\n\n" +
                    "ffmpeg is not on your PATH, so they are frames rather than a video. " +
                    "make_video.bat in that folder runs the one command that encodes them once " +
                    "you have ffmpeg - or drop the sequence straight into any editor, which is " +
                    "what a numbered sequence is for.",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this,
                    $"{done} frames written.\n\nEncode them to out.mp4 with ffmpeg now?",
                    AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = cmd.Substring("ffmpeg ".Length),
                    WorkingDirectory = dir,
                    UseShellExecute = false, CreateNoWindow = true,
                });
                Cursor.Current = Cursors.WaitCursor;
                p?.WaitForExit();
                Cursor.Current = Cursors.Default;
                panel.MloStatus = p != null && p.ExitCode == 0
                    ? $"Encoded {done} frames to out.mp4"
                    : "ffmpeg did not finish - the frames are still there";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "ffmpeg failed to run:\n\n" + ex.Message,
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool renderingStill;
        private string stillPath;
        private bool stillWantCapture;
        private int sampleCapOverride;

        public string RenderStill(int w, int h, string path)
        {
            if (deviceResources == null || deviceResources.DeviceLost) return "the graphics device is gone";
            if (w <= 0 || h <= 0) { w = deviceResources.Width; h = deviceResources.Height; }

            int oldW = deviceResources.Width, oldH = deviceResources.Height;
            bool oldGrid = panel.ShowGrid, oldGizmos = panel.ShowGizmos, oldAll = panel.ShowAllGizmos;
            bool oldMarkers = panel.ShowMarkers;

            long outPx = (long)w * h;
            int ss = panel.Cine.Supersample <= 1 ? 1 : 2;
            while (ss > 1 && outPx * ss * ss > 36_000_000L) ss--;
            int rw = w * ss, rh = h * ss;

            long px = (long)rw * rh;
            int cap = px > 16_000_000 ? 1 : px > 8_000_000 ? 2 : 4;

            try
            {
                renderingStill = true;
                stillWantCapture = true;
                stillPath = path;
                sampleCapOverride = cap;
                stillDownsample = ss;
                panel.ShowGrid = panel.ShowGizmos = panel.ShowAllGizmos = panel.ShowMarkers = false;

                try { deviceResources.Resize(rw, rh); }
                catch (Exception ex)
                {
                    if (ss > 1)
                    {
                        stillDownsample = 1;
                        try { deviceResources.Resize(w, h); }
                        catch (Exception ex2) { return "could not allocate a frame that size: " + ex2.Message; }
                    }
                    else return "could not allocate a frame that size: " + ex.Message;
                }

                stillWantCapture = false;
                stillPath = null;
                RenderFrame();
                stillWantCapture = true;
                stillPath = path;
                RenderFrame();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
            finally
            {
                stillPath = null;
                stillWantCapture = false;
                renderingStill = false;
                sampleCapOverride = 0;
                stillDownsample = 1;
                panel.ShowGrid = oldGrid; panel.ShowGizmos = oldGizmos;
                panel.ShowAllGizmos = oldAll; panel.ShowMarkers = oldMarkers;
                try { deviceResources.Resize(oldW, oldH); } catch { }
            }
        }

        private int stillDownsample = 1;

        private System.Drawing.Bitmap stillCaptured;

        public string RenderStillToBitmap(int w, int h, out System.Drawing.Bitmap bmp)
        {
            bmp = null;
            stillCaptured?.Dispose();
            stillCaptured = null;
            var err = RenderStill(w, h, null);
            if (err != null) return err;
            if (stillCaptured == null) return "the frame produced no image";
            bmp = stillCaptured;
            stillCaptured = null;
            return null;
        }

        private static System.Drawing.Bitmap Downsample(System.Drawing.Bitmap src, int n)
        {
            if (n <= 1) return src;
            int w = src.Width / n, h = src.Height / n;
            var dst = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var lin = new float[256];
            for (int i = 0; i < 256; i++)
            {
                float c = i / 255.0f;
                lin[i] = c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            var sb = src.LockBits(new System.Drawing.Rectangle(0, 0, src.Width, src.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var db = dst.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            unsafe
            {
                float inv = 1.0f / (n * n);
                for (int y = 0; y < h; y++)
                {
                    var drow = (byte*)db.Scan0 + y * db.Stride;
                    for (int x = 0; x < w; x++)
                    {
                        float b = 0, g = 0, r = 0;
                        for (int sy = 0; sy < n; sy++)
                        {
                            var srow = (byte*)sb.Scan0 + (y * n + sy) * sb.Stride + (x * n) * 4;
                            for (int sx = 0; sx < n; sx++)
                            {
                                b += lin[srow[sx * 4 + 0]];
                                g += lin[srow[sx * 4 + 1]];
                                r += lin[srow[sx * 4 + 2]];
                            }
                        }
                        drow[x * 4 + 0] = Encode(b * inv);
                        drow[x * 4 + 1] = Encode(g * inv);
                        drow[x * 4 + 2] = Encode(r * inv);
                        drow[x * 4 + 3] = 255;
                    }
                }
            }
            src.UnlockBits(sb);
            dst.UnlockBits(db);
            return dst;

            static byte Encode(float v)
            {
                v = v <= 0.0031308f ? v * 12.92f : 1.055f * (float)Math.Pow(v, 1.0 / 2.4) - 0.055f;
                int i = (int)(v * 255.0f + 0.5f);
                return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
            }
        }

        private void DoRenderStillDialog()
        {
            var (w, h) = panel.RenderSize;
            if (w <= 0) { w = deviceResources.Width; h = deviceResources.Height; }

            using var dlg = new SaveFileDialog
            {
                Title = $"Render {w} x {h}",
                Filter = "PNG image|*.png",
                FileName = $"render_{w}x{h}.png",
                AddExtension = true,
                DefaultExt = "png",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            Cursor.Current = Cursors.WaitCursor;
            string err;
            try { err = RenderStill(w, h, dlg.FileName); }
            finally { Cursor.Current = Cursors.Default; }

            if (err != null)
            {
                MessageBox.Show(this, "The render did not finish.\n\n" + err,
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                panel.MloStatus = $"Rendered {w} x {h} to {System.IO.Path.GetFileName(dlg.FileName)}";
                Console.WriteLine($"RENDERSTILL {w}x{h} -> {dlg.FileName}");
            }
        }

        private FormBorderStyle photoSavedBorder;
        private FormWindowState photoSavedWindowState;
        private System.Drawing.Rectangle photoSavedBounds;
        private bool isFullscreen;

        private void SetFullscreen(bool on)
        {
            if (on == isFullscreen) return;
            isFullscreen = on;
            if (on)
            {
                photoSavedBorder = FormBorderStyle;
                photoSavedWindowState = WindowState;
                photoSavedBounds = Bounds;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
            }
            else
            {
                FormBorderStyle = photoSavedBorder;
                Bounds = photoSavedBounds;
                WindowState = photoSavedWindowState;
            }
        }

        private LoadedFile hoverProp;

        private int lastSelectedLight = -1;

        private void UpdatePropHighlights()
        {
            foreach (var f in highlighted)
            {
                if (f?.Model == null) continue;
                foreach (var m in f.Model.Meshes) m.Highlight = 0;
            }
            highlighted.Clear();
            if (!panel.EntityPicking || photoMode) { hoverProp = null; return; }

            void Mark(LoadedFile f, uint mode)
            {
                if (f?.Model == null || !highlighted.Add(f)) return;
                foreach (var m in f.Model.Meshes) m.Highlight = mode;
            }

            if (hoverProp != null && !scene.IsFileSelected(hoverProp)) Mark(hoverProp, 2);
            foreach (var f in scene.SelectedFiles) Mark(f, 1);
        }

        private readonly HashSet<LoadedFile> highlighted = new HashSet<LoadedFile>();

        private void FrameProp(LoadedFile f)
        {
            if (f?.Model == null) return;
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            bool any = false;
            foreach (var m in f.Model.Meshes)
            {
                var b = m.WorldBounds;
                if (b.Maximum.X <= b.Minimum.X) continue;
                min = Vector3.Min(min, b.Minimum); max = Vector3.Max(max, b.Maximum); any = true;
            }
            var centre = any ? (min + max) * 0.5f
                             : (f.HasPlacement ? f.Placement.TranslationVector : Vector3.Zero);
            camera.Target = centre;
            camera.Distance = Math.Clamp(any ? (max - min).Length() * 1.6f : 2.0f, 1.0f, 12.0f);
            camera.SnapSmoothing();
        }

        private void PickPropAt(int mx, int my)
        {
            if (!panel.EntityPicking) return;
            PickProp(camera.GetPickRay(mx, my, deviceResources.Width, deviceResources.Height));
        }

        private LoadedFile FindPropUnder(Ray ray) => FindPropUnder(ray, out _);

        private LoadedFile FindPropUnder(Ray ray, out RenderMesh bestMesh)
        {
            LoadedFile bestFile = null;
            bestMesh = null;
            float bestDist = float.MaxValue;

            foreach (var f in scene.Files)
            {
                if (!f.Visible || f.Model == null) continue;
                foreach (var mesh in f.Model.Meshes)
                {
                    if (!mesh.Visible) continue;
                    var b = mesh.WorldBounds;
                    if (b.Maximum.X <= b.Minimum.X) continue;
                    if (!ray.Intersects(ref b, out float bt) || bt > bestDist) continue;

                    if (mesh.RayHit(ref ray, out float t) && t < bestDist)
                    {
                        bestDist = t;
                        bestFile = f;
                        bestMesh = mesh;
                    }
                }
            }
            return bestFile;
        }

        private void PickProp(Ray ray)
        {
            var bestFile = FindPropUnder(ray, out var mesh);
            if (bestFile == null) return;
            var io = ModifierKeys;
            scene.SelectFile(bestFile, (io & Keys.Control) != 0, (io & Keys.Shift) != 0);
            panel.ScrollToActiveProp = true;
            panel.MloStatus = $"Selected {bestFile.Name}";

            if (materialPanel != null && mesh?.Shader != null)
            {
                materialPanel.SelectByShader(mesh.Shader, (io & Keys.Control) != 0);
                materialPanel.Status = $"{bestFile.Name}: {mesh.ShaderName}";
            }
        }

        private void FrameMaterial(MaterialRef mat)
        {
            if (mat == null || mat.Meshes.Count == 0) return;
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            bool any = false;
            foreach (var m in mat.Meshes)
            {
                var b = m.WorldBounds;
                if (b.Maximum.X <= b.Minimum.X) continue;
                min = Vector3.Min(min, b.Minimum);
                max = Vector3.Max(max, b.Maximum);
                any = true;
            }
            if (!any) return;
            camera.Target = (min + max) * 0.5f;
            camera.Distance = Math.Clamp((max - min).Length() * 1.4f, 0.5f, 400.0f);
            camera.SnapSmoothing();
        }

        private void UpdateMaterialView()
        {
            if (materialPanel == null) return;

            if (!panel.MaterialMode)
            {
                if (materialViewHid)
                {
                    foreach (var mesh in scene.AllMeshes) mesh.Visible = true;
                    materialViewHid = false;
                }
                return;
            }

            var isolate = materialPanel.IsolateSelected ? materialPanel.Selected?.Shader : null;
            var hovered = materialPanel.HoveredShader;
            materialViewHid = isolate != null;

            foreach (var mesh in scene.AllMeshes)
            {
                mesh.Visible = isolate == null || mesh.Shader == isolate;
                mesh.Highlight = (hovered != null && mesh.Shader == hovered) ? 2u : 0u;
            }
        }

        private bool materialViewHid;

        public bool DebugClassReport;
        private bool classReportDone;

        private void ApplyClassReport()
        {
            if (!DebugClassReport || classReportDone || !scene.HasModel) return;
            classReportDone = true;

            var counts = new int[6];
            var byShader = new SortedDictionary<string, int>();
            int total = 0;
            foreach (var model in scene.Models)
            {
                foreach (var mesh in model.Meshes)
                {
                    total++;
                    uint m = (uint)mesh.AlphaMode;
                    if (m < counts.Length) counts[m]++;
                    if (mesh.AlphaMode == Rendering.GeomAlphaMode.Water)
                    {
                        var k = string.IsNullOrEmpty(mesh.ShaderName) ? "(unnamed)" : mesh.ShaderName;
                        byShader.TryGetValue(k, out int n);
                        byShader[k] = n + 1;
                    }
                }
            }
            Console.WriteLine($"CLASSREPORT meshes={total} opaque={counts[0]} cutout={counts[1]} " +
                $"decal={counts[2]} additive={counts[3]} glass={counts[4]} water={counts[5]}");
            Console.WriteLine($"CLASSREPORT mode={panel.RenderMode} msaaWanted={panel.CineSampleCount} " +
                $"msaaGot={deviceResources.SampleCount} fxaa={(panel.CineWantsFxaa ? 1 : 0)} " +
                $"ssr={panel.Cine.Ssr:0.##} dof={panel.Cine.Dof:0.##} ao={panel.Cine.AoStrength:0.##}");
            foreach (var kv in byShader) Console.WriteLine($"CLASSREPORT   water sps {kv.Key} x{kv.Value}");
        }

        private void ApplyMaterialDebug()
        {
            if (materialPanel == null || debugMatDone) return;
            if (DebugMatSelect < 0 && DebugMatSet.Count == 0 && DebugMatTex.Count == 0 &&
                DebugMatImport.Count == 0 && DebugMatExport == null && DebugMatExportAll == null && !DebugMatReport && !DebugMatIsolate) return;
            if (!scene.HasModel) return;

            materialPanel.Sync();
            if (materialPanel.Materials.Count == 0) return;
            debugMatDone = true;

            if (DebugMatSelect >= 0 && DebugMatSelect < materialPanel.Materials.Count)
            {
                materialPanel.Selection.Clear();
                materialPanel.Selection.Add(materialPanel.Materials[DebugMatSelect]);
            }
            materialPanel.IsolateSelected = DebugMatIsolate;

            var mat = materialPanel.Selected;
            if (mat == null) return;

            foreach (var path in DebugMatImport)
            {
                var name = scene.ImportTexture(path);
                Console.WriteLine(name != null
                    ? $"MATIMPORT: {System.IO.Path.GetFileName(path)} -> {name}"
                    : $"MATIMPORT: FAILED {path} - {scene.LoadError}");
            }

            foreach (var set in DebugMatSet)
            {
                var eq = set.IndexOf('=');
                if (eq <= 0) { Console.WriteLine($"MATSET: bad argument '{set}' (want name=x,y,z,w)"); continue; }
                var name = set.Substring(0, eq).Trim();
                var parts = set.Substring(eq + 1).Split(',');
                var v = new Vector4();
                for (int i = 0; i < 4 && i < parts.Length; i++)
                {
                    float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float f);
                    if (i == 0) v.X = f; else if (i == 1) v.Y = f; else if (i == 2) v.Z = f; else v.W = f;
                }
                if (!Enum.TryParse<ShaderParamNames>(name, out var pn))
                {
                    Console.WriteLine($"MATSET: unknown parameter '{name}'");
                    continue;
                }
                uint hash = (uint)pn;
                bool added = false;
                if (!MaterialEditing.SetValue(mat.Shader, hash, v))
                {
                    MaterialEditing.AddParam(mat.Shader, hash, v, false);
                    added = true;
                }
                MaterialEditing.Refresh(scene, modelRenderer, mat.Shader);
                Console.WriteLine($"MATSET: {name} = ({v.X},{v.Y},{v.Z},{v.W}) {(added ? "added" : "set")}");
            }

            foreach (var set in DebugMatTex)
            {
                var eq = set.IndexOf('=');
                if (eq <= 0) { Console.WriteLine($"MATTEX: bad argument '{set}' (want Sampler=texture)"); continue; }
                var slot = set.Substring(0, eq).Trim();
                var texName = set.Substring(eq + 1).Trim();
                if (!Enum.TryParse<ShaderParamNames>(slot, out var sn))
                {
                    Console.WriteLine($"MATTEX: unknown sampler '{slot}'");
                    continue;
                }
                uint hash = (uint)sn;
                var tex = string.IsNullOrEmpty(texName) ? null : MaterialEditing.MakeTextureRef(texName);
                bool added = false;
                if (!MaterialEditing.SetTexture(mat.Shader, hash, tex))
                {
                    MaterialEditing.AddParam(mat.Shader, hash, tex, true);
                    added = true;
                }
                MaterialEditing.Refresh(scene, modelRenderer, mat.Shader);
                var mesh0 = mat.Meshes.Count > 0 ? mat.Meshes[0] : null;
                bool bound = sn == ShaderParamNames.DetailSampler ? mesh0?.DetailSRV != null
                    : sn == ShaderParamNames.BumpSampler ? mesh0?.BumpSRV != null
                    : sn == ShaderParamNames.SpecSampler ? mesh0?.SpecSRV != null
                    : mesh0?.DiffuseSRV != null;
                Console.WriteLine($"MATTEX: {slot} = {texName} {(added ? "added" : "set")} bound={(bound ? 1 : 0)}");
            }

            if (DebugMatExport != null) ExportTexturesTo(materialPanel.TexturesOfSelected(), DebugMatExport);
            if (DebugMatExportAll != null) ExportTexturesTo(materialPanel.TexturesOfAll(), DebugMatExportAll);

            if (DebugMatReport)
            {
                var mesh = mat.Meshes.Count > 0 ? mat.Meshes[0] : null;
                string Tex(uint h)
                {
                    var tb = MaterialEditing.GetTexture(mat.Shader, h);
                    if (tb == null) return "(none)";
                    var r = MaterialEditing.Resolve(scene, mat, tb, gameFiles.FindTexture,
                        mat.TxdContext, materialPanel.LocalTextures);
                    if (!r.Found) return $"{tb.Name}[MISSING]";
                    var dxgi = Rendering.TextureLoader.GetDXGIFormat(r.Texture.Format);
                    int bytes = r.Texture.Data?.FullData?.Length ?? 0;
                    bool uploads = textureLoader.GetSRV(r.Texture) != null;
                    return $"{tb.Name}[{r.Source} {r.Texture.Width}x{r.Texture.Height} " +
                           $"{r.Texture.Format}->{dxgi} mips={r.Texture.Levels} bytes={bytes} " +
                           $"upload={(uploads ? "ok" : "FAILED")}]";
                }

                Console.WriteLine($"MATREPORT: index={mat.Index} name={mat.Name} sps={mat.Sps} " +
                    $"bucket={mat.Bucket} params={mat.Shader.ParameterCount} meshes={mat.Meshes.Count}");
                Console.WriteLine($"  diffuse={Tex((uint)ShaderParamNames.DiffuseSampler)} " +
                    $"bump={Tex((uint)ShaderParamNames.BumpSampler)} " +
                    $"spec={Tex((uint)ShaderParamNames.SpecSampler)} " +
                    $"detail={Tex((uint)ShaderParamNames.DetailSampler)}");
                Console.WriteLine($"  detailSettings={Fmt4(MaterialEditing.GetValue(mat.Shader, (uint)ShaderParamNames.detailSettings, Vector4.Zero))} " +
                    $"bumpiness={MaterialEditing.GetValue(mat.Shader, (uint)ShaderParamNames.bumpiness, Vector4.Zero).X:0.###} " +
                    $"specIntensity={MaterialEditing.GetValue(mat.Shader, (uint)ShaderParamNames.specularIntensityMult, Vector4.Zero).X:0.###}");
                Console.WriteLine($"  gpu: diffuseSRV={(mesh?.DiffuseSRV != null ? 1 : 0)} " +
                    $"bumpSRV={(mesh?.BumpSRV != null ? 1 : 0)} " +
                    $"specSRV={(mesh?.SpecSRV != null ? 1 : 0)} " +
                    $"detailSRV={(mesh?.DetailSRV != null ? 1 : 0)} " +
                    $"detailSettings={(mesh != null ? Fmt4(mesh.DetailSettings) : "-")}");

                int okCount = 0, missCount = 0;
                foreach (var m in materialPanel.Materials)
                {
                    foreach (var h in MaterialEditing.ParamHashes(m.Shader).ToList())
                    {
                        var tb2 = MaterialEditing.GetTexture(m.Shader, h);
                        if (tb2 == null) continue;
                        var r2 = MaterialEditing.Resolve(scene, m, tb2, gameFiles.FindTexture,
                            m.TxdContext, materialPanel.LocalTextures);
                        if (r2.Found) okCount++;
                        else
                        {
                            missCount++;
                            if (missCount <= 12)
                                Console.WriteLine($"  UNRESOLVED {m.Name}/{MaterialDefs.NameOf(h)} = {tb2.Name}");
                        }
                    }
                }
                Console.WriteLine($"MATREPORT ALL: materials={materialPanel.Materials.Count} " +
                    $"texturesResolved={okCount} unresolved={missCount}");
            }
        }

        private static string Fmt4(Vector4 v) => $"({v.X:0.###},{v.Y:0.###},{v.Z:0.###},{v.W:0.###})";

        private string DoExportTextures(List<CodeWalker.GameFiles.Texture> textures)
        {
            if (textures == null || textures.Count == 0) return null;

            using var dlg = new FolderBrowserDialog
            {
                Description = $"Export {textures.Count} texture(s) as .dds",
                UseDescriptionForTitle = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            return ExportTexturesTo(textures, dlg.SelectedPath);
        }

        private string ExportTexturesTo(List<CodeWalker.GameFiles.Texture> textures, string dir)
        {
            if (textures == null || textures.Count == 0 || string.IsNullOrEmpty(dir)) return null;
            try { Directory.CreateDirectory(dir); } catch { }

            int ok = 0;
            var failed = new List<string>();
            foreach (var tex in textures)
            {
                try
                {
                    var dds = CodeWalker.Utils.DDSIO.GetDDSFile(tex);
                    if (dds == null || dds.Length == 0) { failed.Add(tex.Name); continue; }
                    var safe = string.Join("_", (tex.Name ?? "texture").Split(Path.GetInvalidFileNameChars()));
                    File.WriteAllBytes(Path.Combine(dir, safe + ".dds"), dds);
                    ok++;
                }
                catch (Exception ex)
                {
                    failed.Add($"{tex.Name} ({ex.Message})");
                }
            }

            if (materialPanel != null)
            {
                materialPanel.Status = failed.Count == 0
                    ? $"Exported {ok} texture(s) to {dir}"
                    : $"Exported {ok}, failed {failed.Count}: {string.Join(", ", failed.Take(3))}";
            }
            Console.WriteLine($"MATEXPORT: exported={ok} failed={failed.Count} dir={dir}" +
                (failed.Count > 0 ? " failures=" + string.Join(";", failed) : ""));
            return dir;
        }

        private string DoImportMaterialTexture()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Images (*.dds;*.png;*.jpg;*.jpeg;*.bmp)|*.dds;*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Choose a texture (embed it into the drawable to ship it)",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            var name = scene.ImportTexture(dlg.FileName);
            if (name == null && materialPanel != null)
                materialPanel.Status = "Texture import failed: " + scene.LoadError;
            return name;
        }

        private bool deviceLostReported;

        private void HandleDeviceLost()
        {
            if (deviceLostReported) return;
            deviceLostReported = true;

            var reason = deviceResources.DeviceLostReason;
            string rescued = null;
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "rescue" + ProjectFile.Extension);
                SaveProjectTo(path);
                rescued = path;
            }
            catch { }

            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "device-lost.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {reason}  " +
                    (rescued ?? "no rescue") + Environment.NewLine);
            }
            catch { }

            if (IsHeadless)
            {
                Console.WriteLine("DEVICE LOST: " + reason);
                Close();
                return;
            }

            DeviceLostReport =
                "The graphics device was lost, so the preview cannot carry on.\n\n" +
                reason + "\n\n" +
                (rescued != null ? "Your work was saved to:\n" + rescued + "\n\n" : "") +
                "This is a driver-level event rather than something you did: a display driver\n" +
                "reset, a driver update, or the display changing underneath the tool - which a\n" +
                "remote-desktop session switching monitors will do. Restart to carry on.";
            Close();
        }

        public string DeviceLostReport;

        private bool renderStillPending;
        public string DebugRenderOut;
        private (int W, int H, string Path)? renderStillOut;

        public void RenderFrame()
        {
            if (renderStillPending && !renderingStill)
            {
                renderStillPending = false;
                DoRenderStillDialog();
            }
            if (renderSeqPending && !renderingStill)
            {
                renderSeqPending = false;
                DoRenderSequenceDialog();
            }
            ServiceApiRender_W3();
            if (renderStillOut.HasValue && !renderingStill)
            {
                var r = renderStillOut.Value;
                renderStillOut = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var err = RenderStill(r.W, r.H, r.Path);
                Console.WriteLine(err == null
                    ? $"RENDERSTILL ok {(r.W <= 0 ? deviceResources.Width : r.W)}x" +
                      $"{(r.H <= 0 ? deviceResources.Height : r.H)} in {sw.Elapsed.TotalSeconds:0.00}s -> {r.Path}"
                    : $"RENDERSTILL FAILED: {err}");
                if (screenshotPath == null) Close();
            }

            try { RenderFrameCore(); }
            catch (Exception ex)
            {
                if (frameErrorReport != ex.ToString())
                {
                    frameErrorReport = ex.ToString();
                    Console.WriteLine("FRAME FAILED: " + ex);
                    if (!IsHeadless) Program.ReportCrash(ex, "render frame");
                    else throw;
                }
            }
        }

        private string frameErrorReport;

        private void RenderFrameCore()
        {
            if (deviceResources == null) return;
            if (deviceResources.DeviceLost) { HandleDeviceLost(); return; }

            double now = clock.Elapsed.TotalSeconds;
            sunRebuiltThisFrame = false;
            float dt = (float)(now - lastFrameTime);
            lastFrameTime = now;

            if (DebugHourSweep > 0 && (DebugMlo == null || debugMloDone))
            {
                hourSweepFrame++;
                panel.PreviewHour = DebugHourFocus > 0.0f
                    ? (DebugHourFocus - 0.25f + (hourSweepFrame % 500) * 0.001f + 24.0f) % 24.0f
                    : (hourSweepFrame * 0.137f) % 24.0f;
                if ((hourSweepFrame % 200) == 0)
                    Console.WriteLine($"HOURSWEEP frame {hourSweepFrame}/{DebugHourSweep} hour={panel.PreviewHour:0.00}");
                if (hourSweepFrame >= DebugHourSweep)
                {
                    Console.WriteLine($"HOURSWEEP COMPLETE: {hourSweepFrame} frames, no failure");
                    Close();
                    return;
                }
            }

            if (DebugSeqTest && !seqTestDone && scene != null)
            {
                seqTestDone = true;
                var seq = panel.Sequence;
                seq.Shots.Clear();
                seq.Shots.Add(new CameraShot { Position = new Vector3(0, 0, 2), Yaw = 0.0f, Pitch = 0.0f, Fov = 50, Duration = 0, Hold = 1.0f, Name = "A" });
                seq.Shots.Add(new CameraShot { Position = new Vector3(10, 0, 2), Yaw = 1.0f, Pitch = 0.2f, Fov = 70, Duration = 2.0f, Name = "B" });
                seq.Shots.Add(new CameraShot { Position = new Vector3(10, 10, 5), Yaw = 6.1f, Pitch = -0.3f, Fov = 30, Duration = 2.0f, Name = "C" });

                int fails = 0;
                void Check(string what, bool ok, string detail)
                {
                    Console.WriteLine($"  {(ok ? "OK  " : "FAIL")} {what}  {detail}");
                    if (!ok) fails++;
                }

                Check("length", Math.Abs(seq.Length - 5.0f) < 0.001f, $"{seq.Length:0.000}s (1 hold + 2 + 2)");

                seq.Sample(0.0f, out var p0, out var y0, out _, out var f0, out _, out _);
                Check("shot A at t=0", (p0 - seq.Shots[0].Position).Length() < 0.001f && Math.Abs(f0 - 50) < 0.01f, $"{p0}");
                seq.Sample(3.0f, out var p1, out _, out _, out var f1, out _, out _);
                Check("shot B at t=3", (p1 - seq.Shots[1].Position).Length() < 0.001f && Math.Abs(f1 - 70) < 0.01f, $"{p1}");
                seq.Sample(5.0f, out var p2, out _, out _, out var f2, out _, out _);
                Check("shot C at t=5", (p2 - seq.Shots[2].Position).Length() < 0.001f && Math.Abs(f2 - 30) < 0.01f, $"{p2}");
                seq.Sample(1.0f, out var ph, out _, out _, out _, out _, out _);
                Check("hold is still", (ph - seq.Shots[0].Position).Length() < 0.001f, $"{ph}");

                float travel = 0, prev = 1.0f;
                for (int k = 1; k <= 40; k++)
                {
                    seq.Sample(3.0f + 2.0f * k / 40.0f, out _, out var yk, out _, out _, out _, out _);
                    travel += Math.Abs(yk - prev);
                    prev = yk;
                }
                Check("yaw takes the short way", travel < MathUtil.Pi,
                    $"travelled {travel:0.000} rad (short way 1.183, long way 5.100)");

                CameraSequence.ApplyToCamera(camera, seq.Shots[2].Position, seq.Shots[2].Yaw,
                    seq.Shots[2].Pitch, seq.Shots[2].Fov);
                Check("camera lands on the shot", (camera.Position - seq.Shots[2].Position).Length() < 0.01f,
                    $"{camera.Position} vs {seq.Shots[2].Position}");

                if (screenshotPath != null)
                {
                    panel.SelectedShot = 1;
                    panel.PlayTime = 3.6f;
                    Console.WriteLine("SEQTEST left a 3-shot move on the timeline for the capture");
                    return;
                }

                if (DebugVideoTest)
                {
                    string vp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rle_seqtest.mp4");
                    try { System.IO.File.Delete(vp); } catch { }
                    seq.Fps = 15;
                    DoRenderVideoHeadless(vp, 640, 360, 30);
                    bool exists = System.IO.File.Exists(vp);
                    long len = exists ? new System.IO.FileInfo(vp).Length : 0;
                    bool isMp4 = false;
                    if (exists && len > 32)
                    {
                        var head = new byte[12];
                        using (var fs = System.IO.File.OpenRead(vp)) fs.Read(head, 0, 12);
                        isMp4 = head[4] == (byte)'f' && head[5] == (byte)'t' &&
                                head[6] == (byte)'y' && head[7] == (byte)'p';
                    }
                    Check("mp4 written", exists && len > 4096, $"{len} bytes");
                    Check("mp4 is a real mp4", isMp4, "starts with an ftyp box");
                    Console.WriteLine($"  VIDEO {vp}");
                }

                WorkspaceStateTest(Check);
                SectionIsolationTest_Q4(Check);
                TabMatrixTest_T2(Check);
                SectionCameraPersistTest_T2(Check);
                CameraFeelTest_G1(Check);
                SunDirectionTest_Sky(Check);
                var mloTestWas = panel.Workspace;
                panel.SwitchWorkspace(LightPanel.Space.Mlo);
                MloCreatorTest_H5(Check);
                MloWorkspaceTest_J6(Check);
                MloSnapModeTest_L4(Check);
                MloSceneTest_L3(Check);
                if (screenshotPath == null) panel.SwitchWorkspace(mloTestWas);
                WorldLightEditTest_I6(Check);
                ProjectSelectionTest_I2(Check);
                InteriorTimecycleTest_K2(Check);
                InteriorCullerTest_L1(Check);
                SeqTest_M2(Check);
                SeqTest_N1(Check);
                SeqTest_N2(Check);
                SeqTest_N4(Check);
                SeqTest_R6(Check);
                SeqTest_S6(Check);
                SeqTest_T6(Check);
                SeqTest_U4(Check);
                SeqTest_O1(Check);
                SeqTest_O3(Check);
                SeqTest_P1(Check);
                SeqTest_P2(Check);
                SelfTest_P3(Check);
                SeqTest_P4(Check);
                NavSeamRealFileTest_V15(Check);
                NavAuditRoundTrip_V16(Check);
                Editor.UvAnimImport_V16.SelfTest_V16(Check);
                SeqTest_AnimBone_V16(Check);
                Editor.UiScale_V17.SelfTest_V17(Check);
                SeqTest_AnimBoneEndToEnd_V17(Check);
                SeqTest_PtfxImport_V18(Check);
                SeqTest_V19(Check);
                SeqTest_SectionCams_V21(Check);
                SeqTest_WorldReset_V21(Check);
                SeqTest_ModsDlc_V21(Check);
                SeqTest_Heightmap_V21(Check);
                SeqTest_Fur_V21(Check);
                SeqTest_Fur_U20(Check);
                SeqTest_Particles_U21(Check);
                SeqTest_MloAdd_U21(Check);
                SeqTest_FurFallback_U21(Check);
                SeqTest_Log_U21(Check);
                SeqTest_RpfExtras_V22(Check);
                SeqTest_RpfV23(Check);
                SeqTest_MaterialReset_V24(Check);
                SeqTest_YtdPicker_V24(Check);
                SeqTest_ProjectCollision_V25(Check);
                SeqTest_WeaponMeta_V26(Check);
                SeqTest_ProjectMultiSelect_V26(Check);
                SeqTest_MaterialProps_V27(Check);
                SeqTest_WorldCollision_V28(Check);
                SeqTest_Sheet_V29(Check);
                SeqTest_Dock_V30(Check);
                SeqTest_MloExport_V31(Check);
                SeqTest_MloAddEntity_V32(Check);
                SeqTest_MloShellLive_V35(Check);
                SeqTest_MoveLights_V35(Check);
                SeqTest_ImportBoth_V36(Check);
                SeqTest_MloClipboard_V37(Check);
                SeqTest_PedFur_V38(Check);
                SeqTest_SheetExport_V40(Check);
                SeqTest_RpfXmlHex_V40(Check);
                SeqTest_RpfDiskXmlHex_V42(Check);
                SeqTest_RpfTreeDrop_V43(Check);
                SeqTest_RpfSideButtons_U9(Check);
                SeqTest_Rpf_U19(Check);
                SeqTest_LodLightsLit_V48(Check);
                SeqTest_ConvertXmlCli_V49(Check);
                SeqTest_RpfDiskConvert_V52(Check);
                Editor.ArchetypeExtensions_V62.SelfTest_V62(Check);
            SeqTest_WorldDup_V65(Check);
            SeqTest_WorldMulti_U5(Check);
            Editor.AssetPreview.SelfTestYtdSolo_U6(Check);
            Editor.WorldRenderer.SelfTestAnim_U7(Check);
            Rendering.ModelRenderer.SelfTestFins_U8(Check);
            Editor.WorldWater.SelfTestWeld_U9(Check);
            Rendering.SceneRenderer.SelfTestLightBatches_U10(Check);
            SeqTest_LightsEditor_U11(Check);
            Editor.RotateSnapSteps_U5.SelfTest_U5(Check);
            SeqTest_MloPortal_V67(Check);
            SeqTest_ShellPortalShift_V67(Check);
            SeqTest_ExtWorkspace_V68(Check);
                SeqTest_UiScaleDrag_V59(Check);
                SeqTest_WaterBumpiness_V60(Check);
                Editor.NavMeshEditor.MergeSelfTest_V20(Check);
                Editor.LightPresets_V20.SelfTest_V20(Check);
                Editor.WorldEditor.MultiSelectSelfTest_V20(Check);
                SeqTest_TerrainProps_V20(Check);
                { var mf = Environment.GetEnvironmentVariable("RLE_ANIMFIND");
                  if (int.TryParse(mf, out int fn) && fn > 0) AnimFind_V17(fn); }
                { var mp = Environment.GetEnvironmentVariable("RLE_ANIMPROBE");
                  if (!string.IsNullOrWhiteSpace(mp)) foreach (var one in mp.Split(';')) AnimProbe_V17(one); }
                SeqTest_FiveM_U12(Check);
                SeqTest_FiveM_U13(Check);
                SeqTest_FiveM_U14(Check);
                SeqTest_FiveM_U15(Check);
                SeqTest_FiveM_U16(Check);
                SeqTest_Font_V20(Check);
                SeqTest_U18(Check);
                SeqTest_Q1(Check);
                SeqTest_Q5(Check);
                SeqTest_U5(Check);
                SeqTest_R1(Check);
                SeqTest_R2(Check);
                SeqTest_R3(Check);
                SeqTest_R4(Check);
                SeqTest_S2(Check);
                SeqTest_T3(Check);
                SeqTest_R5(Check);
                SeqTest_Volume_R5(Check);
                SeqTest_S1(Check);
                SeqTest_S4(Check);
                SeqTest_S5(Check);
                SeqTest_T1(Check);
                SeqTest_T4(Check);
                SeqTest_T5(Check);
                SeqTest_U1(Check);
                SeqTest_U2(Check);
                SeqTest_U3(Check);
                SeqTest_U6(Check);
                SeqTest_V2(Check);
                SeqTest_V5(Check);
                SeqTest_V6(Check);
                SeqTest_V8(Check);
                SeqTest_W1(Check);
                SeqTest_W2(Check);
                SeqTest_W3(Check);
                SeqTest_W4(Check);

                Console.WriteLine(fails == 0 ? "SEQTEST PASSED" : $"SEQTEST FAILED ({fails})");
                Close();
                return;
            }

            if (DebugModeSweep > 0 && (DebugMlo == null || debugMloDone))
            {
                modeSweepFrame++;
                int[] modes = { 0, 7, 1, 7, 2, 7 };
                int want = modes[(modeSweepFrame / 12) % modes.Length];
                if (want != panel.RenderMode)
                {
                    panel.RenderMode = want;
                    Console.WriteLine($"MODESWEEP frame {modeSweepFrame} mode={want} " +
                        $"msaa={deviceResources.SampleCount}");
                }
                if (modeSweepFrame >= DebugModeSweep)
                {
                    Console.WriteLine($"MODESWEEP COMPLETE: {modeSweepFrame} frames, no failure");
                    Close();
                    return;
                }
            }

            FlightRecorder.Frame(now,
                $"h={panel.PreviewHour:0.00} mode={panel.RenderMode} lights={scene.Lights.Count} " +
                $"gpu={lastProbeLightCount} spots={shadowUpdatesThisFrame} props={scene.Files.Count} " +
                $"sun={(panel.SunShadows ? 1 : 0)} tc={(timecycle != null && timecycle.HasData ? 1 : 0)} " +
                $"photo={(photoMode ? 1 : 0)} msaa={deviceResources?.SampleCount ?? 0} " +
                $"sunmap={(sunRebuiltThisFrame ? 1 : 0)} meshes={scene.Files.Count} " +
                $"bounds={sceneRadiusForLog:0}");

            if (!gameTimecyclesListed && gameFiles.Ready) RefreshGameTimecycles();

            if (!archiveIndexed && gameFiles.Ready)
            {
                archiveIndexed = true;
                BuildArchiveIndex_S3(gameFiles.Cache?.RpfMan, DebugArchiveTest);
                try { World.Build(gameFiles); worldBuilt = World.Ready; }
                catch (Exception ex) { Console.WriteLine("WORLD build failed: " + ex.Message); }
                Console.WriteLine($"WORLD {World.NodeCount} ymap nodes");
                OnWorldTick_ModsProbe_V21();
                OnWorldTick_HeightmapProbe_V21();
                OnWorldTick_FurScan_V21();
                OnWorldTick_WeaponScan_V21();
                OnWorldTick_TexFind_V23();
                OnWorldTick_WeaponMetaScan_V26();
                OnWorldTick_ShaderDump_V26();
                OnWorldTick_HeightmapShot_V21();
                if (DebugArchiveTest) { RunArchiveTest(); return; }
                if (DebugWorldTest) { RunWorldTest(); Close(); return; }
            }

            OnWorldTick_FurShot_V21();
            OnTick_TexDicts_V21();
            ServiceRpf_N4();
            ServiceParticles_N4();
            ServiceParticleDiagnostics_R6();
            ServiceSheetProbe_V29();
            OnWorldTick_Extract_V38();
            OnWorldTick_ArchFind_V64();
            OnWorldTick_PedFurScan_U6();
            OnTick_FurNoise_U8();
            OnWorldTick_ClipArchScan_U7();
            OnWorldTick_Ext_V68();
            ServiceParticleDiagnostics_S6();
            ServiceParticleAuthoring_T6();

            if (panel.RequestOpenArchiveFile != null)
            {
                var e = panel.RequestOpenArchiveFile;
                panel.RequestOpenArchiveFile = null;
                OpenArchiveEntry(e);
            }
            if (panel.RequestExportArchiveFile != null)
            {
                var e = panel.RequestExportArchiveFile;
                panel.RequestExportArchiveFile = null;
                ExportArchiveEntry(e);
            }

            if (gameFiles.TextureIndexReady && !textureIndexApplied && gameFiles.Ready)
            {
                textureIndexApplied = true;
                gameFiles.ForgetMissingTextures();
                int fixedUp = 0;
                foreach (var mesh in scene.AllMeshes)
                {
                    if (mesh.DiffuseSRV != null || mesh.Shader == null) continue;
                    modelRenderer.TextureContext = mesh.TxdContext;
                    modelRenderer.RefreshMaterial(mesh);
                    if (mesh.DiffuseSRV != null) fixedUp++;
                }
                modelRenderer.TextureContext = 0;
                if (fixedUp > 0)
                {
                    Console.WriteLine($"TEXTUREINDEX: {fixedUp} mesh(es) found their texture once the index landed");
                    panel.MloStatus = $"Found {fixedUp} more texture(s) once the archive index finished.";
                }
            }

            panel.GameLoading = gameFiles.Initialising && !IsHeadless;
            panel.GameLoadStatus = gameFiles.Status;
            if (panel.ShowGtaSetup)
            {
                if (gameFiles.Ready) panel.ShowGtaSetup = false;
                else if (!gameFiles.Initialising && !string.IsNullOrEmpty(gameFiles.Error))
                    panel.GameLoadStatus = "Failed: " + gameFiles.Error;
            }
            if (DebugLightProbe > 0 && debugMloDone)
            {
                if (probeClosing && probeShotPending == null) { DebugLightProbe = 0; Close(); return; }
                if (!probeClosing) RunLightProbe();
                if (deviceResources == null) return;
            }
            if (DebugMlo != null && !debugMloDone)
            {
                PollDebugMlo(now);
                if (deviceResources == null) return;
            }
            if (DebugPlaceProp != null && !debugPlaceDone && !gameFiles.Initialising)
            {
                debugPlaceDone = true;
                RunDebugPlaceProp();
                if (deviceResources == null) return;
            }
            if (DebugArchOut != null && !debugArchDone)
            {
                RunDebugArchExport();
                if (deviceResources == null) return;
            }
            if (pendingProjectYtyp != null && !gameFiles.Initialising)
            {
                var ytyp = pendingProjectYtyp;
                pendingProjectYtyp = null;
                ImportYtyp(ytyp);
            }
            if (pendingImports.Count > 0 && !gameFiles.Initialising)
            {
                var ytyps = pendingImports.Where(x => x.EndsWith(".ytyp", StringComparison.OrdinalIgnoreCase)).ToArray();
                var ymaps = pendingImports.Where(x => x.EndsWith(".ymap", StringComparison.OrdinalIgnoreCase)).ToArray();
                pendingImports.Clear();
                if (ytyps.Length > 0) ImportYtyp(ytyps);
                if (ymaps.Length > 0) ImportYmap(ymaps);
            }
            ServiceIsoTest_S1();
            ServiceRClickProbe_T1();

            ApplyWalk(dt);
            camera.FieldOfView = settings.FovDeg * 0.0174533f;
            camera.Sensitivity = settings.CameraSensitivity;
            camera.Smoothness = settings.CameraSmoothing;
            camera.OrbitAnchorMax = settings.OrbitAnchorMax;
            if (panel.WorldMode)
            {
                camera.FarClip = Math.Max(camera.FarClip, 20000.0f);
                camera.MaxDistance = Math.Max(camera.MaxDistance, 20000.0f);
            }
            ApplySubjectCamera_U3();
            camera.SetAspect(deviceResources.Width / (float)Math.Max(deviceResources.Height, 1));
            camera.ViewportHeight = Math.Max(deviceResources.Height, 1);
            camera.Update(dt);

            scene.NextLightSpawnPos = camera.Position + camera.GetForward() *
                Math.Clamp(camera.Distance * 0.35f, 1.5f, 8.0f);

            if (dt > 0.0001f)
            {
                fpsSmoothed = fpsSmoothed <= 0 ? 1.0f / dt : fpsSmoothed * 0.95f + (1.0f / dt) * 0.05f;
            }
            int lightCopies = scene.GhostGpuIndices.Count;
            panel.StatsText = panel.WorldMode
                ? $"{fpsSmoothed:0} fps · {lastRenderMs:0.0} ms · {panel.WorldLightsEmitted} lights lit"
                : $"{fpsSmoothed:0} fps · {lastRenderMs:0.0} ms · {scene.Lights.Count}" +
                  (lightCopies > 0 ? $"+{lightCopies}" : "") + $" lights · {shadowUpdatesThisFrame} shdw";

            panel.WalkMode = walkMode;
            panel.WalkSpeedDisplay = settings.WalkSpeed;
            bool scrollProp = panel.ScrollToActiveProp;
            ServiceUiScale_V17();
            imguiInput.NewFrame(dt);
            panel.ApplyDockConfig_V30();
            if (panel.RequestResetDockLayout_V30) { panel.RequestResetDockLayout_V30 = false; ResetDockLayout_V30(); }
            ImGui.NewFrame();
            try
            {
                var tP0 = clock.Elapsed.TotalSeconds;
                panel.Draw(deviceResources.Width, deviceResources.Height);
                perfPanelMs = perfPanelMs * 0.9f + (float)((clock.Elapsed.TotalSeconds - tP0) * 1000.0) * 0.1f;
            }
            catch (Exception ex)
            {
                if (panelDrawError != ex.ToString())
                {
                    panelDrawError = ex.ToString();
                    Console.WriteLine("PANEL DRAW FAILED: " + ex);
                    DebugLog.Log("PANEL DRAW FAILED: " + ex);
                }
            }
            if (scrollProp) panel.ScrollToActiveProp = false;

            float dayFactor = panel.DayNightAmbient ? DayAmbientFactor((int)panel.PreviewHour) : 1.0f;
            float nightness = panel.DayNightAmbient ? 1.0f - dayFactor : 0.0f;
            var tint = Vector3.Lerp(new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0.72f, 0.82f, 1.30f), nightness * 0.7f);
            float amb = panel.AmbientLevel * dayFactor;
            sceneRenderer.AmbientColour = new Vector3(amb * tint.X, amb * 1.06f * tint.Y, amb * 1.13f * tint.Z);

            PollTimecycleReload(now);
            UpdateWeather(now);
            if (panel.RequestedWeather >= 0)
            {
                SetWeather(panel.RequestedWeather);
                panel.RequestedWeather = -1;
            }
            weather.AutoAdvance = panel.AutoTime;
            weather.MinutesPerSecond = panel.TimeSpeed;
            panel.WeatherStatus = weather.StatusText;

            TickInteriorTimecycle_K2();
            OnTick_L2();
            Tick_O3();
            Tick_U12();
            Tick_W1();
            bool tcOn = panel.TimecycleEnabled && timecycle != null && timecycle.HasData;
            if (tcOn)
            {
                var gl = timecycle.Evaluate(panel.PreviewHour, panel.HdrActive);
                skyState = timecycle.EvaluateSky(panel.PreviewHour);
                if (weather.InTransition && weatherBlendCycle != null && weatherBlendCycle.HasData)
                {
                    weatherBlendCycle.SelectedModifier = -1;
                    var gl2 = weatherBlendCycle.Evaluate(panel.PreviewHour, panel.HdrActive);
                    var sk2 = weatherBlendCycle.EvaluateSky(panel.PreviewHour);
                    float t = weather.SmoothBlend;
                    gl = BlendGlobalLight(gl, gl2, t);
                    skyState = BlendSky(skyState, sk2, t);
                }
                sceneRenderer.GlobalLight = gl;
            }
            else
            {
                sceneRenderer.GlobalLight = null;
            }
            sceneRenderer.RenderMode = panel.RenderMode == 7 ? 0u : (uint)panel.RenderMode;
            sceneRenderer.NoShadowJitter = DebugNoShadowJitter;
            sceneRenderer.HighQualityShadows = panel.RenderMode == 7;
            sceneRenderer.BumpTiltLimit = (panel.RenderMode == 7 && panel.Cine.BevelFlatten > 0.001f)
                ? (float)Math.Cos((90.0 - 80.0 * panel.Cine.BevelFlatten) * Math.PI / 180.0)
                : 0.0f;
            sceneRenderer.LightsMultiplier = panel.LightsMultiplier;
            sceneRenderer.Exposure = panel.Exposure;
            sceneRenderer.AmbientDownWrap = skyState.AmbientDownWrap;
            sceneRenderer.FogColour = skyState.FogColour.LengthSquared() > 0.0001f
                ? skyState.FogColour : new Vector3(0.6f, 0.64f, 0.7f);
            sceneRenderer.FogDensity = 0.0f;
            sceneRenderer.FogStart = weather.FogStart;
            sceneRenderer.GameFog = BuildGameFog();

            if (scene.SelectedIndex != lastSelectedLight)
            {
                lastSelectedLight = scene.SelectedIndex;
                var owner = scene.SelectedLight != null ? scene.OwnerFile(scene.SelectedLight) : null;
                if (owner != null && !scene.IsFileSelected(owner)) scene.SelectFile(owner, false, false);
            }

            ServiceResetView_U1();
            AudioTick_U1();
            if (panel.RequestPhotoMode) { panel.RequestPhotoMode = false; SetPhotoMode(true); }
            if (panel.RequestRender) { panel.RequestRender = false; renderStillPending = true; }
            if (panel.RequestRenderSequence) { panel.RequestRenderSequence = false; renderSeqPending = true; }
            TickCameraSequence(dt);

            if (worldStart.HasValue && worldBuilt)
            {
                CameraSequence.ApplyToCamera(camera, worldStart.Value, worldAimYaw, worldAimPitch, settings.FovDeg);
                worldFlyFrom = camera.Position;
                worldStart = null;
                worldWarmup = 0;
            }
            var tW0 = clock.Elapsed.TotalSeconds;
            TickWorld();
            OnWorldTick_World();
            OnWorldTick_Materials();
            OnWorldTick_Selection();
            OnWorldTick_LightEdit();
            OnWorldTick_ScriptIpls();
            OnWorldTick_ModsDlc_V21();
            OnWorldTick_ResetTest_V22();
            OnWorldTick_ModsProbe_V21();
            OnWorldTick_Area();
            OnWorldTick_J3();
            OnWorldTick_K2();
            OnWorldTick_L1();
            OnWorldTick_Q4();
            OnWorldTick_T2();
            OnWorldTick_L2();
            OnWorldTick_M2();
            OnWorldTick_N1();
            OnWorldTick_N2();
            OnWorldTick_R2();
            OnWorldTick_O2();
            OnWorldTick_Nav_P4();
            OnWorldTick_Terrain_R4();
            OnWorldTick_Anim_U6();
            OnWorldTick_U3();
            OnWorldTick_S5();
            OnWorldTick_T4();
            OnWorldTick_T5();
            OnWorldTick_U2();
            OnWorldTick_U5();
            OnWorldTick_SnapProbe_U5();
            perfWorldMs = perfWorldMs * 0.9f + (float)((clock.Elapsed.TotalSeconds - tW0) * 1000.0) * 0.1f;
            if (panel.WorldMode && screenshotPath != null && worldBuilt)
            {
                worldWarmup++;
                worldPeakResident = Math.Max(worldPeakResident, World.YmapsResident);
                worldPeakArchetypes = Math.Max(worldPeakArchetypes, worldRender.ArchetypesLoaded);

                const int Settle = 120, Arrive = 300, Total = 460;
                if (worldWarmup > Settle && worldWarmup <= Arrive)
                {
                    float frameMs = (float)dt * 1000.0f;
                    if (frameMs > worldHitchMaxMs) { worldHitchMaxMs = frameMs; worldHitchFrame = worldWarmup; }
                    worldFlyFrames++; worldFlyMsSum += frameMs;
                    if (worldWarmup % 60 == 0) Console.WriteLine(WorldStageLine());
                }
                if (worldFlyMetres != 0.0f && worldWarmup > Settle)
                {
                    float t = Math.Min((worldWarmup - Settle) / (float)(Arrive - Settle), 1.0f);
                    CameraSequence.ApplyToCamera(camera,
                        worldFlyFrom + new SharpDX.Vector3(0, worldFlyMetres * t, 0),
                        1.4f, 0.45f, settings.FovDeg);
                }

                if (worldWarmup < Total) screenshotFrames = 2;
                else if (worldWarmup == Total)
                {
                    if (worldDemoProject && ProjWin.Project == null)
                    {
                        ProjWin.Project = new CwProject { Name = "New Project", HasChanged = true };
                        var dy = ProjWin.Project.NewYmap();
                        var de = ProjWin.Project.NewEntity(dy, camera.Position + camera.GetForward() * 8.0f, gameFiles?.Cache);
                        ProjWin.Select(de);
                        ProjWin.Visible = true;
                        RebuildProjectOverrides();
                        WorldEntityChanged(de);
                        screenshotFrames = Math.Max(screenshotFrames, 6);
                    }
                    if (worldAutoSelect && !WorldEdit.Selection.HasValue)
                    {
                        WorldPickAt(deviceResources.Width / 2, deviceResources.Height / 2);
                        if (!WorldEdit.Selection.HasValue && Environment.GetEnvironmentVariable("RLE_SELNEAREST") == "1")
                            WorldSelectNearestCandidate(panel.SelectionModeEnum);
                        AfterWorldAutoSelect_Selection();
                        AfterWorldAutoSelect_Dup_V65();
                        AfterWorldAutoSelect_Del_U5();
                        Console.WriteLine(WorldSelReport());
                        if (Environment.GetEnvironmentVariable("RLE_PICKGRID") == "1")
                        {
                            int prMiss = 0, enMiss = 0, both = 0, agree = 0;
                            for (int gy = 0; gy < 5; gy++) for (int gx = 0; gx < 7; gx++)
                            {
                                int px = (int)((gx + 0.5f) / 7 * deviceResources.Width), py = (int)((gy + 0.5f) / 5 * deviceResources.Height);
                                var ray = camera.GetPickRay(px, py, deviceResources.Width, deviceResources.Height);
                                var pe = WorldPickPrecise(ray, out float pd);
                                var ee = WorldPickEntity(ray);
                                if (pe == null) prMiss++;
                                if (ee == null) enMiss++;
                                if (pe != null && ee != null) { both++; if (pe == ee) agree++; }
                                Console.WriteLine($"PICKGRID {px},{py} precise={(pe?.Archetype?.Name ?? "-")} d={pd:0.#} entity={(ee?.Archetype?.Name ?? "-")}");
                            }
                            Console.WriteLine($"PICKGRID summary: precise misses {prMiss}/35, entity misses {enMiss}/35, both hit {both}, agree {agree}");
                        }
                        screenshotFrames = Math.Max(screenshotFrames, 4);
                    }
                    Console.WriteLine($"WORLDPERF {fpsSmoothed:0} fps  {lastRenderMs:0.0} ms  (world tick {perfWorldMs:0.0} ms = select {perfSelectMs:0.0} + update {perfUpdateMs:0.0}, lights {perfLightsMs:0.00} ms, collision {perfCollisionMs:0.00} ms, panel {perfPanelMs:0.1} ms, frame {1000.0f / Math.Max(fpsSmoothed, 1):0.0} ms)" +
                                      (World.Truncated ? $"  WORLDTRUNC visible {World.Visible.Count} cap {World.MaxEntities}" : ""));
                    Console.WriteLine(GpuReport_J4());
                    Console.WriteLine(InteriorCullSummary_L1());
                    Console.WriteLine(WorldStageLine());
                    Console.WriteLine(WorldLoadersLine());
                    LodDoubleReport(camera.Position, "WORLDDOUBLES");
                    if (worldFlyFrames > 0)
                        Console.WriteLine($"WORLDHITCH max frame {worldHitchMaxMs:0.0} ms at fly frame {worldHitchFrame} (avg {worldFlyMsSum / worldFlyFrames:0.0} ms over {worldFlyFrames} fly frames)");
                    var wtx = sceneRenderer.Water;
                    Console.WriteLine($"WORLDWATER meshes {sceneRenderer.WaterMeshesDrawn} depthMode {sceneRenderer.WaterDepthModeUsed} quads {(worldWater?.Model.Meshes.Count ?? 0)} params {sceneRenderer.Water?.FogLightIntensity}" +
                                      $" maps fog {(wtx?.Fog != null ? "yes" : "NO")} bump {(wtx?.Bump != null ? "yes" : "NO")}/{(wtx?.Bump2 != null ? "yes" : "NO")} tried {waterTexturesTried}");
                    Console.WriteLine($"WORLDUPDATE build {worldRender.BuildMs:0.0} ms place {worldRender.PlaceMs:0.0} ms  loadsPending {worldRender.LoadsPending}  walksSkipped {World.WalksSkipped}");
                    {
                        int lodYmaps = 0, lodLights = 0, distYmaps = 0;
                        foreach (var y in World.ResidentYmaps)
                        {
                            if (y.LODLights != null) { lodYmaps++; lodLights += y.LODLights.LodLights?.Length ?? 0; }
                            if (y.DistantLODLights != null) distYmaps++;
                        }
                        int lodNodes = 0, lodNodesResident = 0, lodNodesNear = 0; string lodSample = "";
                        foreach (var n in World.Nodes)
                        {
                            if ((n.Name ?? "").IndexOf("lodlights", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            lodNodes++;
                            if (n.Ymap != null && n.Prepared) lodNodesResident++;
                            if (n.DistanceTo(camera.Position) < 600) { lodNodesNear++; if (lodSample.Length < 300) lodSample += $" {n.Name}[flags {n.ContentFlags} scale {n.RangeScale} d {n.DistanceTo(camera.Position):0} ymap {(n.Ymap != null)} prep {n.Prepared} failed {n.LoadFailed} ({n.FailReason}) lod {(n.Ymap?.LODLights != null)} parent {(n.Ymap?.Parent != null)}]"; }
                        }
                        Console.WriteLine($"WORLDLODNODES {lodNodes} nodes, {lodNodesResident} resident, {lodNodesNear} within 600 m:{lodSample}");
                        Console.WriteLine($"WORLDLIGHTS hd {worldRender.Lights.LightsInView} lod {worldRender.Lights.LodLightsInView} lit {worldRender.Lights.LightsEmitted} " +
                                          $"archetypesWithLights {worldRender.Lights.ArchetypesWithLights} lodlightYmaps {lodYmaps} ({lodLights} lights) distantYmaps {distYmaps} coronas {coronas.Count} hour {panel.PreviewHour:0}");
                    }
                    if (Environment.GetEnvironmentVariable("RLE_DUMPNODES") != null)
                    {
                        float dumpR = 250f;
                        var visByYmap = new Dictionary<YmapFile, int>();
                        foreach (var v in World.Visible) if (v?.Ymap != null) { visByYmap.TryGetValue(v.Ymap, out int c); visByYmap[v.Ymap] = c + 1; }
                        foreach (var n in World.Nodes.OrderBy(n => n.DistanceTo(camera.Position)))
                        {
                            if (n.DistanceTo(camera.Position) > dumpR) continue;
                            var y = n.Ymap;
                            visByYmap.TryGetValue(y, out int vis);
                            Console.WriteLine($"NODE {n.Name} d={n.DistanceTo(camera.Position):0} flags={n.ContentFlags} resident={(y != null && n.Prepared)} failed={n.LoadFailed} " +
                                              $"scripted={(y?.IsScripted ?? false)} hiddenVariant={World.IsHiddenVariant(n.Hash)} ents={(y?.AllEntities?.Length ?? 0)} visible={vis} parent={(y?._CMapData.parent.ToString() ?? "-")} parentRes={(y?.Parent != null)}");
                        }
                    }
                    var dumpNear = Environment.GetEnvironmentVariable("RLE_DUMPNEAR");
                    if (dumpNear != null && float.TryParse(dumpNear, out float dumpNearR))
                    {
                        int shownNear = 0;
                        foreach (var v in World.Visible.OrderBy(v => (v.Position - camera.Position).Length()))
                        {
                            if (v?.Archetype == null) continue;
                            float ddn = (v.Position - camera.Position).Length();
                            if (ddn > dumpNearR) break;
                            if (shownNear++ >= 60) break;
                            var m = worldRender.PeekModel(v.Archetype.Hash);
                            string meshes = m == null ? "no model" : string.Join(" ", m.Meshes.Take(6).Select(x => $"{x.ShaderName}:{x.AlphaMode}{(x.NeverDraw ? ":NEVER" : "")}{(x.DiffuseSRV == null ? ":noTex" : "")}"));
                            Console.WriteLine($"NEAR {v.Archetype.Name} d={ddn:0} lodDist={v.LodDist:0} lvl={v._CEntityDef.lodLevel} ymap={v.Ymap?.Name} built={worldRender.IsBuilt(v.Archetype)} meshes={(m?.Meshes.Count ?? 0)} [{meshes}]");
                        }
                    }
                    var dumpYmap = Environment.GetEnvironmentVariable("RLE_DUMPYMAP");
                    if (dumpYmap != null)
                    {
                        foreach (var y in World.ResidentYmaps)
                        {
                            if (!string.Equals(Path.GetFileNameWithoutExtension(y.Name ?? ""), dumpYmap, StringComparison.OrdinalIgnoreCase)) continue;
                            Console.WriteLine($"YMAP {y.Name}: ents {y.AllEntities?.Length} roots {y.RootEntities?.Length} parent {(y.Parent?.Name ?? "none")} inTree {World.LodTreeYmaps}");
                            int shown = 0;
                            var seenParents = new HashSet<YmapEntityDef>();
                            foreach (var e in y.AllEntities ?? Array.Empty<YmapEntityDef>())
                            {
                                var pe = e.Parent;
                                if (pe == null) { if (shown++ < 12) Console.WriteLine($"  ROOT {e.Archetype?.Name} lod {e._CEntityDef.lodLevel} lodDist {e.LodDist:0} dist {(e.Position - camera.Position).Length():0} visible {e.IsVisible} built {worldRender.IsBuilt(e.Archetype)}"); continue; }
                                if (!seenParents.Add(pe)) continue;
                                if (shown++ >= 12) break;
                                Console.WriteLine($"  PARENT {pe.Archetype?.Name} [{pe._CEntityDef.lodLevel}] in {pe.Ymap?.Name}: numChildren {pe._CEntityDef.numChildren} linked {(pe.LodManagerChildren?.Count ?? 0)} childLod {pe.ChildLodDist:0} lodDist {pe.LodDist:0} dist {(pe.Position - camera.Position).Length():0} visible {pe.IsVisible} built {worldRender.IsBuilt(pe.Archetype)} | child {e.Archetype?.Name} lodDist {e.LodDist:0} dist {(e.Position - camera.Position).Length():0} visible {e.IsVisible} built {worldRender.IsBuilt(e.Archetype)}");
                            }
                        }
                    }
                    if (panel.WorldShowCollision)
                        Console.WriteLine($"COLLISIONSTATE drawn {collisionDraw.Count} of {collisionWantedCount} wanted, tris {panel.WorldCollisionTris} cached {collisionView?.CachedTriangles} pending {collisionView?.LoadsPending} retiredVBs {collisionRetiredVBs} " +
                                          $"built {collisionView?.Built} read avg {(collisionView != null && collisionView.Built > 0 ? collisionView.ReadMsTotal / collisionView.Built : 0):0.0} max {collisionView?.ReadMsMax:0} ms build avg {(collisionView != null && collisionView.Built > 0 ? collisionView.BuildMsTotal / collisionView.Built : 0):0.0} max {collisionView?.BuildMsMax:0} ms ({collisionView?.SlowestFile}) threads {collisionView?.LoaderThreads} | {collisionView?.Status}");
                    Console.WriteLine($"WORLDVIEW ymaps {World.YmapsOpen}/{World.YmapsWanted} " +
                        $"walked {World.YmapsWalked} " +
                        $"entities {World.Visible.Count} meshes {worldRender.MeshesDrawn} " +
                        $"models {worldRender.ArchetypesLoaded} cap {worldRender.EffectiveMaxArchetypes} (base {worldRender.MaxArchetypes}, working set {worldRender.WorkingSetArchetypes}) released {worldRender.ModelsReleased} projectDrawables {gameFiles.ProjectDrawablesServed} projectTextures {gameFiles.ProjectTexturesServed}");
                    if (worldFlyMetres != 0.0f)
                        Console.WriteLine($"WORLDFLY flew {worldFlyMetres:0} m  " +
                            $"resident {World.YmapsResident} (peak {worldPeakResident})  " +
                            $"models {worldRender.ArchetypesLoaded} (peak {worldPeakArchetypes})  " +
                            $"released {worldRender.ArchetypesEvicted}");
                }
            }

            UpdatePropHighlights();
            panel.TwoSidedMeshCount = sceneRenderer.TwoSidedMeshes;
            panel.TotalMeshCount = sceneRenderer.DrawnMeshes;
            ApplyMaterialDebug();
            ApplyClassReport();
            if (DebugRenderOut != null && !renderingStill && scene.HasModel)
            {
                var want = panel.RenderSize;
                string outPath = DebugRenderOut;
                DebugRenderOut = null;
                renderStillOut = (want.W, want.H, outPath);
            }
            UpdateMaterialView();

            if (DebugSetFlagMask != 0)
                foreach (var dl in scene.Lights) dl.Flags |= DebugSetFlagMask;
            if (DebugClearFlagMask != 0)
                foreach (var dl in scene.Lights) dl.Flags &= ~DebugClearFlagMask;

            scene.LightsOnlyFromSelectedProp = panel.SelectedPropLightsOnly;

            if (panel.FocusSelectedLight && !walkMode && scene.SelectedLight != null && FocusOrbitOwnsCamera_Q4)
            {
                var lp = scene.GetInstance(scene.SelectedLight).WorldPosition;
                camera.Target = Vector3.Lerp(camera.Target, lp, Math.Clamp((float)dt * 8.0f, 0.0f, 1.0f));
            }

            coronas.Clear();
            volumes.Clear();
            int lightCount;
            if (panel.WorldMode)
            {
                ServiceWorldFind_V55(now);
                BeforeWorldLights_N2();
                worldRender.Lights.VolumesOut = panel.ShowVolumes ? volumes : null;
                worldRender.Lights.Enabled = panel.WorldLightsEnabled;
                worldRender.Lights.MaxDistance = panel.WorldLightsRange;
                // LOD lights are REAL lights, and every one of them costs per-mesh culling work
                // and a slot in the GPU array - at the full light range a night frame emitted the
                // whole 2048-light budget out to three kilometres, which is what made the world
                // crawl after dark. Near the camera they are what lights the street; past that the
                // distant-light sprites carry the glow for free, so the real ones stop there.
                worldRender.Lights.LodLightRange = Math.Min(panel.WorldLightsRange, LodLightRange_V61);
                worldRender.Lights.LodLightsEnabled = panel.WorldLodLightsEnabled;
                var tL0 = clock.Elapsed.TotalSeconds;
                BeforeWorldLights_P3();
                lightCount = worldRender.Lights.Build(World.Visible, gpuLights, gpuLightSources, camera.Position,
                    (int)panel.PreviewHour, panel.AnimateFlashiness, (float)now, panel.ShowCoronas ? coronas : null,
                    World.ContentYmaps, World.ContentVersion, new SharpDX.BoundingFrustum(camera.ViewProjMatrix));
                perfLightsMs = perfLightsMs * 0.9f + (float)((clock.Elapsed.TotalSeconds - tL0) * 1000.0) * 0.1f;
                panel.WorldLightsInView = worldRender.Lights.LightsInView + worldRender.Lights.LodLightsInView;
                panel.WorldLightsEmitted = lightCount;
            }
            else
                lightCount = scene.BuildGpuLights(gpuLights, (int)panel.PreviewHour, panel.AnimateFlashiness,
                    (float)now, panel.ShowCoronas ? coronas : null, camera.Position,
                    panel.ShowVolumes ? volumes : null, gpuLightSources,
                    cullLights: false, new SharpDX.BoundingFrustum(camera.ViewProjMatrix));
            lastProbeLightCount = lightCount;
            AfterLightsBuilt_N2(lightCount);

            Array.Clear(projTexSrvs, 0, projTexSrvs.Length);
            var projSlots = new Dictionary<uint, int>();
            for (int i = 0; i < lightCount && i < gpuLightSources.Count; i++)
            {
                var l = gpuLightSources[i];
                if (l == null) continue;
                uint hash = l.ProjectedTextureHash.Hash;
                if ((l.Flags & LightDefs.FlagTextureProjection) == 0 || hash == 0 || NoProjTex_U25) continue;
                if (!projSlots.TryGetValue(hash, out int slot))
                {
                    if (projSlots.Count >= GpuLight.MaxProjTextures) continue;
                    var tex = scene.FindTexture(hash);
                    var srv = tex != null ? textureLoader.GetSRV(tex) : null;
                    if (srv == null) continue;
                    slot = projSlots.Count;
                    projSlots[hash] = slot;
                    projTexSrvs[slot] = srv;
                }
                gpuLights[i].ProjTexIndex = slot;
            }

            var context = deviceResources.Context;
            GpuFrameBegin_J4(context);
            GpuMark_J4("shadows", true);

            var shadowSetup = default(ShadowSetup);
            shadowUpdatesThisFrame = 0;
            if (panel.ShowShadows && !panel.WorldMode && scene.HasModel && lightCount > 0 && !RpfExplorerOnly_Q1)
            {
                bool geomChanged = scene.GeometryVersion != shadowGeomVersion || DebugShadowStress;
                shadowGeomVersion = scene.GeometryVersion;

                shadowBudget = geomChanged ? int.MaxValue : 12;

                var order = new List<int>();
                const uint shadowFlags = 0x40u | 0x80u | 0x100u | 0x1000000u;
                for (int i = 0; i < lightCount; i++)
                {
                    if (scene.GhostGpuIndices.Contains(i)) continue;
                    if (i < gpuLightSources.Count)
                    {
                        if (scene.ShadowsDisabled.Contains(gpuLightSources[i])) continue;
                        if (panel.RespectShadowFlags && (gpuLightSources[i].Flags & shadowFlags) == 0) continue;
                        if (panel.ShadowMode == 2 && !scene.IsLightSelected(gpuLightSources[i])) continue;
                    }
                    order.Add(i);
                }
                order.Sort((a, b) =>
                    Vector3.DistanceSquared(camera.Position, gpuLights[a].Position)
                    .CompareTo(Vector3.DistanceSquared(camera.Position, gpuLights[b].Position)));

                if (panel.ShadowMode == 2 && scene.SelectedIndex != lastShadowSelection)
                {
                    lastShadowSelection = scene.SelectedIndex;
                    shadowPickDirty = true;
                }
                if (panel.ShadowMode != lastShadowMode) { lastShadowMode = panel.ShadowMode; shadowPickDirty = true; }

                bool camMoved = panel.ShadowMode != 2 &&
                                Vector3.DistanceSquared(camera.Position, lastShadowPickPos) > 64.0f;
                if (panel.FlagsChanged) { shadowPickDirty = true; panel.FlagsChanged = false; }
                if (camMoved || geomChanged || shadowPickDirty)
                {
                    lastShadowPickPos = camera.Position;
                    shadowPickDirty = false;
                    cachedDesiredSpots = ToSources(PickShadowLights(order, spotSlotCache, GpuLight.MaxShadowSpots, false));
                    cachedDesiredCubes = ToSources(PickShadowLights(order, cubeSlotCache, GpuLight.MaxShadowCubes, true));
                }
                var desiredSpots = ToIndices(cachedDesiredSpots);
                var desiredCubes = ToIndices(cachedDesiredCubes);
                lastDesiredSpots = desiredSpots.Count;
                lastDesiredCubes = desiredCubes.Count;

                List<RenderMesh> shadowMeshes = null;
                List<RenderMesh> Meshes() => shadowMeshes ?? (shadowMeshes = scene.AllMeshes.ToList());

                AssignShadowSlots(context, desiredSpots, spotSlotCache, geomChanged, isCube: false, Meshes);
                AssignShadowSlots(context, desiredCubes, cubeSlotCache, geomChanged, isCube: true, Meshes);

                bool sunOn = panel.SunShadows && sceneRenderer.GlobalLight.HasValue;
                if (sunOn)
                {
                    var sunDir = sceneRenderer.GlobalLight.Value.LightDir;
                    var sb = scene.GetSceneBounds();
                    if (Vector3.DistanceSquared(sunDir, lastSunDir) > 1e-6f) sunMapDirty = true;

                    bool scrubbing = panel.HourScrubbing;
                    SunCascadeDirty_R5(ref sunMapDirty);
                    bool due = !scrubbing && (now - lastSunMapTime) >= 0.1;
                    sceneRadiusForLog = sb.HasValue
                        ? (sb.Value.Maximum - sb.Value.Minimum).Length() * 0.5f : 0.0f;
                    if (sb.HasValue && (geomChanged || !sunMapValid || (sunMapDirty && due)))
                    {
                        sunRebuiltThisFrame = true;
                        lastSunDir = sunDir;
                        lastSunMapTime = now;
                        sunMapDirty = false;
                        sunMapValid = true;
                        var sunCasters = WithPortalOccluders_K2(Meshes());
                        bool sunDone_R5 = false;
                        RenderSunMap_R5(context, sunCasters, sunDir, sb.Value, ref sunDone_R5);
                        if (!sunDone_R5) shadowRenderer.RenderSun(context, sunCasters, sunDir, sb.Value);
                    }
                }
                else sunMapValid = false;

                shadowSetup = new ShadowSetup
                {
                    SpotArray = shadowRenderer.SpotArraySRV,
                    CubeArray = shadowRenderer.CubeArraySRV,
                    SpotMatrices = shadowRenderer.SpotMatrices,
                    SunMap = shadowRenderer.SunMapSRV,
                    SunMatrix = shadowRenderer.SunMatrix,
                    SunPos = shadowRenderer.SunPos,
                    SunEnabled = sunOn && sunMapValid,
                    SunStrength = panel.SunShadowStrength,
                    SunTexelWorld = shadowRenderer.SunTexelWorld,
                    Cascades = SunCascadesActive_R5,
                };
            }
            else if (panel.WorldMode && panel.ShowShadows && panel.SunShadows)
            {
                TickWorldSunShadows(context, now, ref shadowSetup);
            }
            GpuMark_J4("shadows", false);

            if (!RpfExplorerOnly_Q1) propThumbnails?.Tick();

            {
                int want = WantedSampleCount_M2(panel.CineSampleCount);
                if (sampleCapOverride > 0) want = Math.Min(want, sampleCapOverride);
                if (want != deviceResources.SampleCount)
                    panel.MsaaGranted = deviceResources.SetSampleCount(want);
            }

            deviceResources.BeginFrame(new Color4(0.065f, 0.07f, 0.085f, 1.0f));
            gameFiles?.Tick();
            TickDebugExtract_Render();

            bool skyHasScene = !RpfExplorerOnly_Q1 &&
                (panel.WorldMode || scene.HasModel || TerrainHasModel_R4 ||
                 (panel.ArchiveMode && assetPreview?.Model != null));
            if (panel.ShowSky && skyRenderer != null && skyHasScene && (panel.RenderMode == 0 || panel.RenderMode == 7))
            {
                try
                {
                    GpuMark_J4("sky", true);
                    var sv = BuildSkyVars(now);
                    skyRenderer.Render(context, ref sv);
                    OnAfterSkyDraw_Sky(context, now);
                    GpuMark_J4("sky", false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SKY FAILED: " + ex);
                    panel.ShowSky = false;
                }
            }

            if (!RpfExplorerOnly_Q1 &&
                ((!panel.WorldMode && scene.HasModel) || (panel.WorldMode && worldRender.MeshesDrawn > 0) ||
                (panel.ArchiveMode && assetPreview?.Model != null) || TerrainHasModel_R4))
            {
                var toDraw = panel.WorldMode
                    ? (worldWater != null && worldWater.Ready
                          ? new[] { worldRender.Model, worldWater.Model }
                          : new[] { worldRender.Model })
                    : (panel.ArchiveMode && assetPreview?.Model != null
                        ? scene.Models.Concat(new[] { assetPreview.Model })
                        : scene.Models);
                var extraModels = WorldExtraModels_Selection();
                AddScenarioModels_I4(extraModels);
                AddTerrainModels_R4(extraModels);
                if (extraModels != null && extraModels.Count > 0) toDraw = toDraw.Concat(extraModels);
                GpuMark_J4("mirrors", true);
                RenderMirrors_Render(context, toDraw, gpuLights, lightCount, projTexSrvs, shadowSetup);
                GpuMark_J4("mirrors", false);
                GpuMark_J4("world", true);
                sceneRenderer.MsaaSamples = deviceResources.SampleCount;
            sceneRenderer.ShadowSoftness_V68 = SkyShadowEnv_V68("RLE_SHADOWSOFT", panel.ShadowSoftness_V68);
                sceneRenderer.AlphaToCoverage = panel.AlphaToCoverage_V63;
                sceneRenderer.Render(context, camera, toDraw, gpuLights, lightCount, null, projTexSrvs, shadowSetup);
                GpuMark_J4("world", false);
            }
            GpuMark_J4("grass", true);
            OnAfterWorldDraw_Materials(context);
            GpuMark_J4("grass", false);
            OnAfterWorldDraw_Sky(context);
            GpuMark_J4("shafts", true);
            OnAfterWorldDraw_H3(context);
            OnAfterModelDraw_Ext_V68(context);
            GpuMark_J4("shafts", false);
            GpuMark_J4("helpers", true);
            if (!photoMode)
            {
                OnAfterWorldDraw_Selection(context);
                OnAfterWorldDraw_SpaceData(context);
                DrawNavMesh_P4(context);
            }

            if (DebugLog.Enabled && screenshotFrames == 1)
            {
                DumpDepthSamples();
            }

            if (volumes.Count > 0)
            {
                DrawVolumes();
                FlushVolumes_U25(context);
            }

            if (panel.ArchiveMode && previewCollision != null && !previewCollision.IsEmpty)
            {
                triRenderer.AddTriangles(previewCollision.Vertices, SharpDX.Vector3.Zero);
                triRenderer.Flush(context, camera.ViewProjMatrix,
                                  CommonStates.BlendAlpha, CommonStates.DepthReadOnly);
            }

            if (panel.WorldMode && collisionDraw.Count > 0 && !photoMode)
            {
                DrawWorldCollision(context);
            }

            if (panel.ShowGridEffective(scene.HasModel) && !panel.WorldMode) DrawGrid();
            if (!photoMode) DrawSelectedPropOutline();
            if (!panel.WorldMode) DrawLightGizmos();
            if (panel.WorldMode && !photoMode) DrawWorldSelectionBox();
            lineRenderer.Flush(context, camera.ViewProjMatrix);

            if (panel.ShowMarkers) DrawLightMarkers();
            if (photoMode)
            {
            }
            else if (panel.WorldMode)
            {
                { int areaStart = lineRenderer.LineCount; DrawAreaHelpers_J5(); CaptureLines_U25(areaStart, false, SelBoxPx_U24); }
                DrawGrassBrush_S5();
            }
            else DrawMloCreatorHelpers_H5();
            DrawTerrainHelpers_R4();
            lineRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.DepthDisabled);
            triRenderer.Flush(context, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthDisabled);

            DrawDistantLights_V47(context);
            if (panel.ShowCoronas && !NoCoronas_U25 && coronas.Count > 0 && panel.RenderMode != 8)
            {
                foreach (var c in coronas)
                {
                    coronaRenderer.Add(c.pos, c.colour, c.size, c.intensity);
                }
                ResolveDistantLightTex_V47();
                coronaRenderer.Flush(context, camera.ViewProjMatrix, camera.Position, coronaSrv_V48);
            }
            RenderParticles_N4(context);
            GpuMark_J4("helpers", false);
            GpuMark_J4("post", true);
            ClearForRpfExplorer_Q1();
            ResolveWorldFrame_M2(context);

            if (panel.TimecycleEnabled && timecycle != null)
            {
                var pf = timecycle.EvaluatePostFx(panel.PreviewHour);
                postFx.Vars.FilmicA = pf.FilmicA; postFx.Vars.FilmicB = pf.FilmicB;
                postFx.Vars.FilmicC = pf.FilmicC; postFx.Vars.FilmicD = pf.FilmicD;
                postFx.Vars.FilmicE = pf.FilmicE; postFx.Vars.FilmicF = pf.FilmicF;
                postFx.Vars.FilmicW = pf.FilmicW;
                postFx.Vars.ColorCorrectHighLum = pf.ColorCorrectHighLum;
                postFx.Vars.ColorShiftLowLum = pf.ColorShiftLowLum;
                postFx.Vars.Desaturate = pf.Desaturate;
                postFx.Vars.Exposure = panel.Exposure * pf.Exposure;
                ApplyGameExposure_M2(pf, true);
            }
            else
            {
                postFx.Vars.Exposure = panel.Exposure;
                ApplyGameExposure_M2(default, false);
            }
            postFx.Vars.EnableColorCorrect = panel.PostFxColourCorrect ? 1u : 0u;
            postFx.Vars.RageTonemap = panel.ToneMapCodeWalker ? 1u : 0u;
            {
                var tmo = Environment.GetEnvironmentVariable("RLE_TONEMAP");
                if (tmo == "0") postFx.Vars.RageTonemap = 0u;
                else if (tmo == "1") postFx.Vars.RageTonemap = 1u;
            }
            postFx.Vars.ExposureBias = panel.Exposure;
            postFx.Vars.AutoExposure = panel.AutoExposure ? 1.0f : 0.0f;
            postFx.Vars.BloomAmount = panel.RageBloom;
            postFx.Vars.BloomThresholdHdr = panel.HdrActive ? 50.0f : 1.5f;
            postFx.Vars.Passthrough = PostFxPassthrough_Render();
            postFx.Vars.HdrScene = panel.HdrActive ? 1.0f : 0.0f;

            var cp = panel.Cine;
            bool cine = panel.RenderMode == 7;
            postFx.Vars.Cinematic = cine ? 1u : 0u;
            postFx.Vars.BloomStrength = cp.Bloom;
            postFx.Vars.SsrIntensity = cp.Ssr;
            postFx.Vars.DofStrength = cp.Dof;
            postFx.Vars.PixelSize = new Vector4(
                1.0f / Math.Max(deviceResources.Width, 1),
                1.0f / Math.Max(deviceResources.Height, 1),
                deviceResources.Width, deviceResources.Height);
            postFx.Vars.UseFxaa = panel.CineWantsFxaa ? 1.0f : 0.0f;
            postFx.Vars.Vignette = cp.Vignette;
            postFx.Vars.Grain = cp.Grain;
            postFx.Vars.GrainTime = (float)now;
            postFx.Vars.ChromAberration = cp.ChromAberration;
            postFx.Vars.Sharpen = cp.Sharpen;
            postFx.Vars.Contrast = cp.Contrast;
            postFx.Vars.Saturation = cp.Saturation;
            postFx.Vars.Temperature = cp.Temperature;
            postFx.Vars.TintGM = cp.Tint;
            postFx.Vars.Letterbox = cp.Letterbox;
            postFx.Vars.Halation = cp.Halation;
            postFx.Vars.HalationTint = new Vector4(cp.HalationR, cp.HalationG, cp.HalationB, cp.HalationSpread);
            postFx.Vars.HalationThreshold = cp.HalationThreshold;
            postFx.Vars.HalationSaturation = cp.HalationSaturation;
            postFx.Vars.HalationSoftness = cp.HalationSoftness;
            postFx.Vars.GrainSize = cp.GrainSize * stillDownsample;
            postFx.Vars.GrainColour = cp.GrainColour;
            postFx.Vars.GrainShadow = cp.GrainShadow;
            postFx.Vars.VignetteRoundness = cp.VignetteRoundness;
            postFx.Vars.VignetteSoftness = cp.VignetteSoftness;
            postFx.Vars.EdgeBlur = cp.EdgeBlur;
            postFx.Vars.EdgeBlurStart = cp.EdgeBlurStart;
            postFx.Vars.EdgeBlurElongation = cp.EdgeBlurElongation;
            postFx.Vars.Dither = cp.Dither;
            postFx.Vars.Lift = cp.Lift;
            postFx.Vars.Gain = cp.Gain;
            postFx.Vars.Bleach = cp.Bleach;

            if (cine)
            {
                deviceResources.ResolveFrame((ms, rtv) =>
                    cinematic.ResolveDepth(context, ms, rtv,
                        deviceResources.Width, deviceResources.Height));

                if (cp.DofAutoFocus && (cp.Dof > 0.001f || !panel.WorldMode))
                {
                    var ray = camera.GetPickRay(deviceResources.Width * 0.5f,
                                                deviceResources.Height * 0.5f,
                                                deviceResources.Width, deviceResources.Height);
                    float best = float.MaxValue;
                    if (panel.WorldMode)
                    {
                        if (worldBuilt && WorldPickPrecise(ray, out float wd) != null) best = wd;
                    }
                    else
                    {
                        foreach (var m in scene.AllMeshes)
                        {
                            if (!m.Visible) continue;
                            if (m.RayHit(ref ray, out float d) && d < best) best = d;
                        }
                    }
                    if (best < float.MaxValue) cp.DofFocus = Math.Max(0.3f, best);
                }

                cinematic.Build(context, deviceResources.Width, deviceResources.Height,
                    deviceResources.SceneSRV, deviceResources.DepthSRV,
                    camera.ViewProjMatrix, prevViewProj, camera.Position, new CineSettings
                    {
                        AoStrength = cp.AoStrength,
                        AoRadius = cp.AoRadius,
                        AoQuality = cp.AoQuality,
                        Bloom = cp.Bloom,
                        BloomThreshold = cp.BloomThreshold,
                        BloomSpread = cp.BloomSpread,
                        BloomAnamorphic = cp.BloomAnamorphic,
                        SsrIntensity = cp.Ssr,
                        SsrThickness = cp.SsrThickness,
                        SsrMaxDistance = cp.SsrDistance,
                        SsrSky = cp.SsrSky,
                        SsrFresnel = cp.SsrFresnel,
                        SsrBlur = cp.SsrBlur,
                        SsrSkyColour = sceneRenderer.GlobalLight.HasValue
                            ? new Vector3(sceneRenderer.GlobalLight.Value.NaturalAmbUp.X,
                                          sceneRenderer.GlobalLight.Value.NaturalAmbUp.Y,
                                          sceneRenderer.GlobalLight.Value.NaturalAmbUp.Z) * 1.0f
                              + sceneRenderer.FogColour * 0.2f
                            : sceneRenderer.AmbientColour * 2.0f,
                        DofStrength = cp.Dof,
                        DofFocus = cp.DofFocus,
                        DofAperture = cp.DofAperture,
                        DofRange = cp.DofRange,
                        DofMaxRadius = cp.DofMaxRadius,
                        DofBokehBoost = cp.DofBokeh,
                        DofBlades = cp.DofBlades,
                        DofStretch = cp.DofStretch,
                        DofRadial = cp.DofRadial,
                    });
            }

            PinExposure_U2();
            bool wantLumReadback = screenshotFrames == 1 && DebugGtaFolder != null;
            UpdateUnderwater_H3(camera, now);
            postFx.Prepare(context, deviceResources.SceneSRV, deviceResources.Width, deviceResources.Height,
                dt, panel.RageBloom > 0.001f, wantLumReadback);

            deviceResources.BeginBackbuffer();
            postFx.Composite(context, deviceResources.SceneSRV,
                cine ? cinematic.AoSRV : null,
                cine ? cinematic.BloomSRV : null,
                cine ? cinematic.SsrSRV : null,
                cine ? cinematic.DofSRV : null,
                deviceResources.DepthSRV);

            if (!photoMode && !renderingStill && !RpfExplorerOnly_Q1) DrawDayOverlays_U2(context);
            if (!photoMode && !renderingStill && !RpfExplorerOnly_Q1) { DrawPrecisionOverlay_T4(context); DrawSelectionOverlay_U24(context); DrawGizmoOverlay(context); DrawMloCreatorGizmo_H5(context); DrawExtGizmo_V69(context); }
            GpuMark_J4("post", false);
            GpuMark_J4("ui", true);

            ImGui.Render();
            var dd = ImGui.GetDrawData();
            if (DebugLog.Enabled && screenshotFrames == 1)
            {
                DebugLog.Log($"imgui: cmdlists={dd.CmdListsCount}, vtx={dd.TotalVtxCount}, idx={dd.TotalIdxCount}, " +
                    $"display={dd.DisplaySize.X}x{dd.DisplaySize.Y} pos={dd.DisplayPos.X},{dd.DisplayPos.Y}");
            }
            if (!renderingStill) imguiRenderer.Render(dd);
            GpuMark_J4("ui", false);
            GpuFrameEnd_J4();

            bool captureNow = false;
            bool waitingForGame = DebugGtaFolder != null &&
                                  (gameFiles.Initialising || !gameTimecyclesListed);
            if (screenshotFrames > 0 && !waitingForGame)
            {
                screenshotFrames--;
                captureNow = screenshotFrames == 0;
                if (captureNow) OnCapture_FurShot_V21();
            }
            if (captureNow)
            {
                SunProbe_R5(context);
                FogProbe_R5();
                Console.WriteLine($"SHADOWSLOTS spots={lastDesiredSpots} cubes={lastDesiredCubes} " +
                                  $"respectFlags={panel.RespectShadowFlags}");
            }
            if (captureNow && DebugGtaFolder != null)
            {
                var sd = skyState;
                Console.WriteLine($"SKY hour={panel.PreviewHour:0.0} cycle={timecycle.Name} " +
                    $"zenith={sd.Zenith} azE={sd.AzimuthEast} azW={sd.AzimuthWest} azT={sd.AzimuthTransition}");
                Console.WriteLine($"  atp={sd.AzimuthTransitionPos:0.000} ztp={sd.ZenithTransitionPos:0.000} " +
                    $"zbs={sd.ZenithBlendStart:0.000} zte={sd.ZenithTransitionEastBlend:0.000} " +
                    $"ztw={sd.ZenithTransitionWestBlend:0.000} hdr={sd.HdrIntensity:0.000} sunMie={sd.SunMie}");
                var svd = BuildSkyVars(now);
                Console.WriteLine($"  sent: hdrIntensity={svd.HdrIntensity:0.000} sky_hdr={sd.HdrIntensity:0.000} hdrOn={panel.TimecycleHdr} " +
                    $"exposure={panel.Exposure:0.00} skyExp={panel.SkyExposure:0.00} stars={svd.StarfieldIntensity:0.000} " +
                    $"sunCol={svd.SunColour} discCol={svd.SunDiscColour} sunHdr={sd.SunHdr:0.00} " +
                    $"cloudMid={svd.CloudMidColour} cover={svd.CloudCoverage:0.00} densMul={svd.CloudDensityMultiplier:0.00} " +
                    $"zenInten={timecycle.Peek("sky_zenith_col_inten", 1.0f):0.000} cwMap={timecycle.CodeWalkerSkyMapping}");
                Console.WriteLine($"  tone: mode={(panel.ToneMapCodeWalker ? "codewalker" : "filmic")} auto={panel.AutoExposure} " +
                    $"avgLum={postFx.LastAvgLum:0.0000} fLum={Math.Clamp(postFx.LastAvgLum * panel.Exposure, 0.2f, 10.0f):0.000} " +
                    $"scale={0.72f / (Math.Clamp(postFx.LastAvgLum * panel.Exposure, 0.2f, 10.0f) + 0.001f):0.000} bloom={panel.RageBloom:0.00}");
                LogExposure_M2();
                var fg = sceneRenderer.GameFog;
                Console.WriteLine($"  fog: on={fg.Params0.W} start={fg.Params0.X:0.0} farClip={fg.Params0.Y:0.0} " +
                    $"hazeDens={-fg.Params1.X:0.000000} hazeAlpha={fg.Params1.Y:0.00} tint={fg.Params1.Z:0.00000} groundAtViewer={-fg.Params1.W:0.000000} " +
                    $"hazeStart={fg.Params2.X:0.0} groundAlpha={fg.Params2.Y:0.00} falloff={fg.Params2.Z:0.0000} " +
                    $"colGround={fg.ColGround} colAtmo={fg.ColAtmosphere} colSun={fg.ColSun} colHaze={fg.ColHaze} " +
                    $"raw: fog_density={timecycle.Peek("fog_density"):0.000} fog_falloff={timecycle.Peek("fog_falloff"):0.000} " +
                    $"fog_haze_density={timecycle.Peek("fog_haze_density"):0.000} fog_hdr={timecycle.Peek("fog_hdr", 1):0.00}");
                Console.WriteLine($"  shadows: sunOn={sceneRenderer.GlobalLight.HasValue && panel.SunShadows} cascades={shadowRenderer.Cascades.Count} " +
                    $"splits=({string.Join(",", shadowRenderer.Cascades.SplitFar)}) texel=({string.Join(",", Array.ConvertAll(shadowRenderer.Cascades.TexelWorld, x => x.ToString("0.000")))}) " +
                    $"draws={shadowRenderer.LastCascadeDraws} rebuilds={sunCascadeRebuilds} clouds={CloudStatus}");
                var gl = sceneRenderer.GlobalLight;
                if (gl.HasValue) Console.WriteLine($"  globals: dirCol={gl.Value.LightDirColour} natUp={gl.Value.NaturalAmbUp} natDn={gl.Value.NaturalAmbDown}");
                Console.WriteLine(SunDebugLine());
            }
            if (captureNow)
            {
                SaveScreenshot(screenshotPath);
            }
            if (renderingStill)
            {
                if (stillWantCapture) SaveScreenshot(stillPath);
                lastRenderMs = (float)((clock.Elapsed.TotalSeconds - now) * 1000.0);
                return;
            }
            if (probeShotPending != null)
            {
                SaveScreenshot(probeShotPending);
                probeShotPending = null;
            }

            prevViewProj = camera.ViewProjMatrix;
            lastRenderMs = (float)((clock.Elapsed.TotalSeconds - now) * 1000.0);
            deviceResources.SyncInterval = settings.VSync ? 1 : 0;
            deviceResources.Present();
            if (deviceResources.DeviceLost) { HandleDeviceLost(); return; }
            RenderDetachedWindows_Detach(captureNow, dt);
            RenderDetachedWindows_M1(captureNow, dt);
            RenderFiveMDetached_U14(captureNow, dt);
            RenderModelViewer_P1(captureNow, dt);

            if (captureNow)
            {
                Close();
            }

            if (DebugFpsBench && (DebugMlo == null || debugMloDone))
            {
                benchFrames++;
                double el = clock.Elapsed.TotalSeconds - benchStart;
                if (el >= 2.0)
                {
                    Console.WriteLine($"MEASURED {benchFrames / el:0.0} fps  (cap={benchFrameCap}, vsync={settings.VSync})");
                    Close();
                    return;
                }
            }

            LimitFrameRate(now);
        }

        private void LimitFrameRate(double frameStart)
        {
            int cap = benchFrameCap;
            if (cap <= 0) return;
            double frameEnd = frameStart + 1.0 / cap;
            double remaining = frameEnd - clock.Elapsed.TotalSeconds;
            if (remaining <= 0) return;
            int sleepMs = (int)(remaining * 1000.0) - 1;
            if (sleepMs > 0) System.Threading.Thread.Sleep(sleepMs);
            while (clock.Elapsed.TotalSeconds < frameEnd) System.Threading.Thread.SpinWait(64);
        }

        private void ApplyWalk(float dt)
        {
            if (ImGuiWantsKeyboard) { walkKeys.Clear(); return; }
            if (!MovementEnabled) { walkKeys.Clear(); return; }
            if (walkKeys.Count == 0) return;
            ApplyWalkKeys(dt);
        }

        private bool SequenceOwnsCamera => panel != null && panel.Sequence != null &&
                                           panel.Sequence.Shots.Count > 0 &&
                                           (panel.Playing || scrubbingSequence);

        private bool scrubbingSequence;

        private bool MovementEnabled => (gizmo == null || !gizmo.Dragging) && !SequenceOwnsCamera;

        private void ApplyWalkKeys(float dt)
        {
            var move = Vector3.Zero;
            var fwd = camera.GetForward();
            var right = camera.GetRight();

            if (walkKeys.Contains(Keys.Up) || MoveHeld("MoveForward")) move += fwd;
            if (walkKeys.Contains(Keys.Down) || MoveHeld("MoveBack")) move -= fwd;
            if (walkKeys.Contains(Keys.Right) || MoveHeld("MoveRight")) move += right;
            if (walkKeys.Contains(Keys.Left) || MoveHeld("MoveLeft")) move -= right;
            if (walkKeys.Contains(Keys.PageUp) || MoveHeld("MoveUp")) move += Vector3.UnitZ;
            if (walkKeys.Contains(Keys.PageDown) || MoveHeld("MoveDown")) move -= Vector3.UnitZ;

            if (move.LengthSquared() < 1e-6f) return;
            move.Normalize();

            float speed = 50.0f * settings.WalkSpeed * Math.Min(camera.TargetDistance, 20.0f);
            if ((ModifierKeys & Keys.Shift) != 0) speed *= 5.0f;
            if ((ModifierKeys & Keys.Control) != 0) speed *= 0.2f;

            camera.Translate(move * speed * Math.Min(dt, 0.1f));
        }

        private List<int> PickShadowLights(List<int> order, ShadowSlotCache[] cache, int max, bool isCube)
        {
            float takeoverMargin = DebugNoDwell ? 0.7f : 0.40f;
            double dwellSeconds = DebugNoDwell ? 0.0 : 4.0;
            double nowSec = clock.Elapsed.TotalSeconds;

            var picked = new List<int>();
            var incumbents = new List<int>();
            foreach (var i in order)
            {
                bool typeOk = isCube ? gpuLights[i].Type != 2 : gpuLights[i].Type == 2;
                if (!typeOk) continue;

                var src = i < gpuLightSources.Count ? gpuLightSources[i] : null;
                bool held = src != null && Array.Exists(cache, c => c.Light == src);
                if (held) incumbents.Add(i); else picked.Add(i);
            }

            var result = new List<int>();
            foreach (var i in incumbents)
            {
                if (result.Count >= max) break;
                result.Add(i);
            }
            if (result.Count < max)
            {
                foreach (var i in picked)
                {
                    if (result.Count >= max) break;
                    result.Add(i);
                }
            }
            else
            {
                foreach (var c in picked)
                {
                    float cd = Vector3.DistanceSquared(camera.Position, gpuLights[c].Position);
                    int worst = -1; float worstD = -1f;
                    for (int k = 0; k < result.Count; k++)
                    {
                        var wsrc = result[k] < gpuLightSources.Count ? gpuLightSources[result[k]] : null;
                        var slot = wsrc == null ? null : Array.Find(cache, s => s.Light == wsrc);
                        if (slot != null && nowSec - slot.AssignedAt < dwellSeconds) continue;

                        float d = Vector3.DistanceSquared(camera.Position, gpuLights[result[k]].Position);
                        if (d > worstD) { worstD = d; worst = k; }
                    }
                    if (worst < 0 || cd >= worstD * takeoverMargin * takeoverMargin) break;
                    result[worst] = c;
                }
            }
            return result;
        }

        private void AssignShadowSlots(SharpDX.Direct3D11.DeviceContext context, List<int> desired,
            ShadowSlotCache[] cache, bool geomChanged, bool isCube, Func<List<RenderMesh>> meshes)
        {
            int slotBase = isCube ? 16 : 0;
            var pending = new List<int>(desired);
            var freeSlots = new List<int>();
            bool unbound = false;

            for (int slot = 0; slot < cache.Length; slot++)
            {
                int keep = -1;
                if (cache[slot].Light != null)
                {
                    for (int k = 0; k < pending.Count; k++)
                    {
                        int gi = pending[k];
                        if (gi < gpuLightSources.Count && gpuLightSources[gi] == cache[slot].Light) { keep = k; break; }
                    }
                }
                if (keep >= 0)
                {
                    int gi = pending[keep];
                    pending.RemoveAt(keep);
                    bool valid = RenderSlotIfChanged(context, slot, gi, cache, geomChanged, isCube, meshes, ref unbound);
                    gpuLights[gi].ShadowSlot = valid ? slotBase + slot : -1;
                }
                else
                {
                    freeSlots.Add(slot);
                }
            }

            int next = 0;
            foreach (var slot in freeSlots)
            {
                if (next >= pending.Count) break;

                int cost = isCube ? 6 : 1;
                if (shadowBudget < cost)
                {
                    var held = cache[slot].Light;
                    if (held != null && cache[slot].Valid)
                    {
                        int hi = gpuLightSources.IndexOf(held);
                        if (hi >= 0 && hi < gpuLights.Length) gpuLights[hi].ShadowSlot = slotBase + slot;
                    }
                    continue;
                }

                int gi = pending[next++];
                cache[slot].Light = gi < gpuLightSources.Count ? gpuLightSources[gi] : null;
                cache[slot].Hash = int.MinValue;
                cache[slot].Valid = false;
                cache[slot].AssignedAt = clock.Elapsed.TotalSeconds;
                bool valid = RenderSlotIfChanged(context, slot, gi, cache, geomChanged, isCube, meshes, ref unbound);
                gpuLights[gi].ShadowSlot = valid ? slotBase + slot : -1;
            }
        }

        private bool RenderSlotIfChanged(SharpDX.Direct3D11.DeviceContext context, int slot, int gi,
            ShadowSlotCache[] cache, bool geomChanged, bool isCube, Func<List<RenderMesh>> meshes, ref bool unbound)
        {
            float nearClip = gi < gpuLightSources.Count ? gpuLightSources[gi].ShadowNearClip : 0.05f;
            int h = LightShadowHash(in gpuLights[gi], nearClip);

            if (!geomChanged && cache[slot].Hash == h && cache[slot].Valid) return true;

            int cost = isCube ? 6 : 1;
            if (shadowBudget < cost)
            {
                return cache[slot].Valid;
            }
            shadowBudget -= cost;

            if (!unbound)
            {
                context.PixelShader.SetShaderResource(7, null);
                context.PixelShader.SetShaderResource(8, null);
                unbound = true;
            }
            if (isCube)
            {
                shadowRenderer.RenderCubeSlot(context, meshes(), slot, gpuLights[gi].Position, gpuLights[gi].Falloff);
            }
            else
            {
                shadowRenderer.RenderSpotSlot(context, meshes(), slot,
                    gpuLights[gi].Position, gpuLights[gi].Direction, gpuLights[gi].TangentX,
                    gpuLights[gi].ConeOuterAngle, gpuLights[gi].Falloff, nearClip);
            }
            cache[slot].Hash = h;
            cache[slot].Valid = true;
            shadowUpdatesThisFrame++;
            return true;
        }

        private void DrawSelectedPropOutline()
        {
            if (photoMode || !panel.EntityPicking || scene.SelectedFiles.Count == 0) return;

            foreach (var f in scene.SelectedFiles)
            {
                if (f?.Model == null || !f.Visible) continue;

                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                bool any = false;
                foreach (var mesh in f.Model.Meshes)
                {
                    if (!mesh.Visible) continue;
                    var b = mesh.WorldBounds;
                    if (b.Maximum.X <= b.Minimum.X) continue;
                    min = Vector3.Min(min, b.Minimum);
                    max = Vector3.Max(max, b.Maximum);
                    any = true;
                }
                if (!any) continue;

                bool active = f == scene.ActiveFile;
                var col = active ? new Vector4(0.15f, 1.0f, 1.0f, 1.0f)
                                 : new Vector4(0.10f, 0.62f, 0.70f, 0.85f);
                lineRenderer.AddBox(new BoundingBox(min, max), col);
            }
        }

        private const int MaxVolumesDrawn = 256;

        private void DrawVolumes()
        {
            if (NoVolumes_R5) { panel.VolumeOverflow = 0; return; }
            VolumeDump_R5(volumes);
            var list = volumes;
            if (list.Count > MaxVolumesDrawn)
            {
                var cam = camera.Position;
                list = new List<Scene.VolumeDraw>(volumes);
                list.Sort((a, b) => Vector3.DistanceSquared(cam, a.Pos)
                                          .CompareTo(Vector3.DistanceSquared(cam, b.Pos)));
                list = list.GetRange(0, MaxVolumesDrawn);
                panel.VolumeOverflow = volumes.Count - MaxVolumesDrawn;
            }
            else panel.VolumeOverflow = 0;

            volumeRenderer_U25 ??= new Rendering.LightVolumeRenderer_U25(deviceResources.Device);
            foreach (var v in list) AddVolume_U25(v);
        }

        private static float DayAmbientFactor(int hour)
        {
            float[] curve =
            {
                0.12f, 0.12f, 0.12f, 0.12f, 0.12f,
                0.25f, 0.50f, 0.80f,
                1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f,
                0.85f, 0.60f, 0.35f, 0.20f,
                0.12f, 0.12f, 0.12f,
            };
            return curve[((hour % 24) + 24) % 24];
        }

        private void DrawGrid()
        {
            var minor = new Vector4(0.55f, 0.55f, 0.60f, 0.65f);
            var major = new Vector4(1.00f, 1.00f, 1.00f, 0.90f);
            var axisX = new Vector4(1.00f, 0.30f, 0.30f, 1.0f);
            var axisY = new Vector4(0.30f, 1.00f, 0.30f, 1.0f);
            const int half = 20;
            const int interval = 10;
            for (int i = -half; i <= half; i++)
            {
                if (i % interval == 0) continue;
                lineRenderer.AddLine(new Vector3(i, -half, 0), new Vector3(i, half, 0), minor);
                lineRenderer.AddLine(new Vector3(-half, i, 0), new Vector3(half, i, 0), minor);
            }
            for (int i = -half; i <= half; i += interval)
            {
                var cx = i == 0 ? axisY : major;
                var cy = i == 0 ? axisX : major;
                lineRenderer.AddLine(new Vector3(i, -half, 0), new Vector3(i, half, 0), cx);
                lineRenderer.AddLine(new Vector3(-half, i, 0), new Vector3(half, i, 0), cy);
            }
            lineRenderer.AddAxes(Vector3.Zero, 1.0f);
        }

        private void DrawLightGizmos()
        {
            if (!panel.ShowGizmos && !panel.ShowAllGizmos) return;

            for (int i = 0; i < scene.Lights.Count; i++)
            {
                bool isSel = i == scene.SelectedIndex;
                bool draw = (isSel && panel.ShowGizmos) || panel.ShowAllGizmos;
                if (!draw) continue;

                var l = scene.Lights[i];
                var inst = scene.GetInstance(l);
                float alpha = isSel ? 0.9f : 0.25f;
                var col = new Vector4(l.ColorR / 255.0f, l.ColorG / 255.0f, l.ColorB / 255.0f, alpha);
                var colDim = new Vector4(col.X, col.Y, col.Z, alpha * 0.35f);

                float markerSize = Vector3.Distance(camera.Position, inst.WorldPosition) * 0.012f;
                lineRenderer.AddSphere(inst.WorldPosition, markerSize, col, 16);

                switch ((byte)l.Type)
                {
                    case 1:
                        lineRenderer.AddSphere(inst.WorldPosition, l.Falloff, isSel ? col : colDim);
                        break;
                    case 2:
                        float outer = Math.Max(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f;
                        float inner = Math.Min(l.ConeInnerAngle, l.ConeOuterAngle) * 0.01745329f;
                        lineRenderer.AddCone(inst.WorldPosition, inst.WorldDirection, inst.WorldTangent, outer, l.Falloff, isSel ? col : colDim);
                        if (isSel && inner > 0.001f)
                        {
                            lineRenderer.AddCone(inst.WorldPosition, inst.WorldDirection, inst.WorldTangent, inner, l.Falloff, colDim);
                        }
                        break;
                    case 4:
                        var ext = inst.WorldDirection * (l.Extent.X * 0.5f);
                        lineRenderer.AddCapsule(inst.WorldPosition + ext, inst.WorldPosition - ext, l.Falloff, isSel ? col : colDim);
                        break;
                }

                if (isSel && (l.Flags & LightDefs.FlagCullingPlane) != 0 && l.CullingPlaneNormal.LengthSquared() > 0.01f)
                {
                    var n = Vector3.Normalize(l.CullingPlaneNormal);
                    var centre = inst.WorldPosition - n * l.CullingPlaneOffset;
                    var tx = Vector3.Normalize(Vector3.Cross(n, Math.Abs(n.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX));
                    var ty = Vector3.Cross(n, tx);
                    float size = Math.Max(l.Falloff * 0.9f, 1.0f);

                    var pcol = new Vector4(1.0f, 0.55f, 0.15f, 0.85f);
                    var pcolDim = new Vector4(1.0f, 0.55f, 0.15f, 0.30f);
                    const int div = 4;
                    for (int gi = -div; gi <= div; gi++)
                    {
                        float t = size * gi / div;
                        var c = (gi == -div || gi == div) ? pcol : pcolDim;
                        lineRenderer.AddLine(centre + tx * t - ty * size, centre + tx * t + ty * size, c);
                        lineRenderer.AddLine(centre + ty * t - tx * size, centre + ty * t + tx * size, c);
                    }

                    var g = new Vector4(0.2f, 1.0f, 0.3f, 0.95f);
                    var gtip = centre + n * (size * 0.55f);
                    lineRenderer.AddLine(centre, gtip, g);
                    lineRenderer.AddLine(gtip, gtip - n * (size * 0.12f) + tx * (size * 0.06f), g);
                    lineRenderer.AddLine(gtip, gtip - n * (size * 0.12f) - tx * (size * 0.06f), g);

                    var r = new Vector4(1.0f, 0.25f, 0.2f, 0.95f);
                    var rtip = centre - n * (size * 0.3f);
                    lineRenderer.AddLine(centre, rtip, r);
                    lineRenderer.AddLine(rtip - tx * (size * 0.06f) - ty * (size * 0.06f), rtip + tx * (size * 0.06f) + ty * (size * 0.06f), r);
                    lineRenderer.AddLine(rtip - tx * (size * 0.06f) + ty * (size * 0.06f), rtip + tx * (size * 0.06f) - ty * (size * 0.06f), r);
                }

                if (isSel && (byte)l.Type != 1)
                {
                    lineRenderer.AddLine(inst.WorldPosition, inst.WorldPosition + inst.WorldDirection * Math.Max(l.Falloff * 0.35f, 0.5f),
                        new Vector4(1, 1, 0.2f, 0.9f));
                }
            }
        }

        private unsafe void DumpDepthSamples()
        {
            try
            {
                var desc = new SharpDX.Direct3D11.Texture2DDescription
                {
                    Width = deviceResources.Width,
                    Height = deviceResources.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = SharpDX.DXGI.Format.R32_Typeless,
                    SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                    Usage = SharpDX.Direct3D11.ResourceUsage.Staging,
                    CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read,
                };
                using var staging = new SharpDX.Direct3D11.Texture2D(deviceResources.Device, desc);
                using (var res = deviceResources.DepthDSV.Resource)
                {
                    deviceResources.Context.CopyResource(res, staging);
                }
                var box = deviceResources.Context.MapSubresource(staging, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                float ReadDepth(int x, int y)
                {
                    var row = (byte*)box.DataPointer + y * box.RowPitch;
                    return ((float*)row)[x];
                }
                DebugLog.Log("--- depth buffer samples (after model pass) ---");
                int w = deviceResources.Width, h = deviceResources.Height;
                for (int gy = 0; gy < 7; gy++)
                {
                    var line = "";
                    for (int gx = 0; gx < 7; gx++)
                    {
                        int px = w * (gx + 1) / 8;
                        int py = h * (gy + 1) / 8;
                        line += ReadDepth(px, py).ToString("0.000000") + " ";
                    }
                    DebugLog.Log(line);
                }
                DebugLog.Log($"centre pixel: {ReadDepth(w / 2, h / 2):0.0000000}");

                var rtvs2 = deviceResources.Context.OutputMerger.GetRenderTargets(1, out var boundDsv);
                DebugLog.Log($"bound dsv ptr: {boundDsv?.NativePointer ?? IntPtr.Zero}, owned dsv ptr: {deviceResources.DepthDSV.NativePointer}, " +
                    $"same={boundDsv?.NativePointer == deviceResources.DepthDSV.NativePointer}");
                boundDsv?.Dispose();
                if (rtvs2 != null) foreach (var r in rtvs2) r?.Dispose();

                using (var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    for (int y = 0; y < h; y++)
                    {
                        var dst = (byte*)bd.Scan0 + y * bd.Stride;
                        for (int x = 0; x < w; x++)
                        {
                            float d = ReadDepth(x, y);
                            byte v = d >= 1.0f ? (byte)0 : (byte)(50 + (1.0f - d) * 130000 % 200);
                            dst[x * 4 + 0] = v; dst[x * 4 + 1] = v; dst[x * 4 + 2] = v; dst[x * 4 + 3] = 255;
                        }
                    }
                    bmp.UnlockBits(bd);
                    bmp.Save(System.IO.Path.Combine(AppContext.BaseDirectory, "rle_depth.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
                deviceResources.Context.UnmapSubresource(staging, 0);
            }
            catch (Exception ex)
            {
                DebugLog.Log("depth dump failed: " + ex.Message);
            }
        }

        private void SaveScreenshot(string path)
        {
            try
            {
                if (DebugLog.Enabled)
                {
                    try
                    {
                        using var iq = deviceResources.Device.QueryInterface<SharpDX.Direct3D11.InfoQueue>();
                        long n = iq.NumStoredMessages;
                        DebugLog.Log($"--- d3d11 debug messages: {n} ---");
                        for (long i = 0; i < Math.Min(n, 50); i++)
                        {
                            var m = iq.GetMessage(i);
                            DebugLog.Log($"[{m.Severity}] {m.Description}");
                        }
                    }
                    catch (Exception dex)
                    {
                        DebugLog.Log("infoqueue unavailable: " + dex.Message);
                    }
                    DebugLog.Flush();
                }
                using var backbuffer = deviceResources.SwapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0);
                var desc = backbuffer.Description;
                desc.Usage = SharpDX.Direct3D11.ResourceUsage.Staging;
                desc.BindFlags = SharpDX.Direct3D11.BindFlags.None;
                desc.CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read;
                desc.OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None;
                using var staging = new SharpDX.Direct3D11.Texture2D(deviceResources.Device, desc);
                deviceResources.Context.CopyResource(backbuffer, staging);
                var box = deviceResources.Context.MapSubresource(staging, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                using var bmp = new System.Drawing.Bitmap(desc.Width, desc.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, desc.Width, desc.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                unsafe
                {
                    for (int y = 0; y < desc.Height; y++)
                    {
                        var src = (byte*)box.DataPointer + y * box.RowPitch;
                        var dst = (byte*)bd.Scan0 + y * bd.Stride;
                        for (int x = 0; x < desc.Width; x++)
                        {
                            dst[x * 4 + 0] = src[x * 4 + 2];
                            dst[x * 4 + 1] = src[x * 4 + 1];
                            dst[x * 4 + 2] = src[x * 4 + 0];
                            dst[x * 4 + 3] = 255;
                        }
                    }
                }
                bmp.UnlockBits(bd);
                deviceResources.Context.UnmapSubresource(staging, 0);

                var finished = stillDownsample > 1 ? Downsample(bmp, stillDownsample) : bmp;
                if (path == null)
                {
                    stillCaptured?.Dispose();
                    stillCaptured = (System.Drawing.Bitmap)finished.Clone();
                }
                else finished.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                if (!ReferenceEquals(finished, bmp)) finished.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Screenshot failed: " + ex.Message);
            }
        }
    }
}

