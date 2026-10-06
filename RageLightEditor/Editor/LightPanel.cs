using System;
using System.Linq;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;
using RageLightEditor.Rendering;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public float PreviewHour = 12.0f;
        public bool HourScrubbing { get => hourSliderActive || TimeScrubbingByMouse; set => hourSliderActive = value; }
        private bool hourSliderActive;
        public bool DayNightAmbient = true;
        public bool AnimateFlashiness = true;
        public bool ShowGizmos = true;
        public bool ShowAllGizmos = false;
        public bool ShowCoronas = true;
        public bool ShowVolumes = true;
        public bool ShowGrid = true;
        public bool ShowMarkers = true;
        public bool ShowShadows = true;
        public int ShadowMode = 1;
        private bool openTutorial;
        public float AmbientLevel = 0.12f;
        private int renderModeLight = 0;
        private int renderModeMaterial = 0;
        private int renderModeWorld = 0;

        public int RenderMode
        {
            get => Workspace == Space.Cinematic ? 7
                 : WorldMode ? renderModeWorld
                 : MaterialMode ? renderModeMaterial : renderModeLight;
            set
            {
                if (Workspace == Space.Cinematic) return;
                if (WorldMode) renderModeWorld = value;
                else if (MaterialMode) renderModeMaterial = value;
                else renderModeLight = value;
            }
        }

        public bool HdrActive => TimecycleHdr && RenderMode != 7;

        public AppSettings.CinematicPrefs Cine => settings.Cinematic;

        public float CineAoStrength { get => Cine.AoStrength; set => Cine.AoStrength = value; }
        public float CineAoRadius { get => Cine.AoRadius; set => Cine.AoRadius = value; }
        public float CineBloom { get => Cine.Bloom; set => Cine.Bloom = value; }
        public float CineBloomThreshold { get => Cine.BloomThreshold; set => Cine.BloomThreshold = value; }

        public static readonly string[] CineAaLabels =
            { "Off", "FXAA", "MSAA 2x", "MSAA 4x", "MSAA 8x", "MSAA 4x + FXAA", "MSAA 8x + FXAA" };
        private static readonly int[] cineAaSamples = { 1, 1, 2, 4, 8, 4, 8 };
        private static readonly bool[] cineAaFxaa = { false, true, false, false, false, true, true };

        private int CineAaIndex => Math.Max(0, Math.Min(CineAaLabels.Length - 1, Cine.AntiAlias));
        public int CineSampleCount => cineAaSamples[CineAaIndex];
        public bool CineWantsFxaa => cineAaFxaa[CineAaIndex];

        private const string ShadingTooltip =
            "RAGE: the game's own lighting and composite. What a player would see.\n\n" +
            "Cinematic: the same lighting, rendered and finished the way a camera would\n" +
            "see it - multisampled edges, traced contact occlusion, screen-space\n" +
            "reflections, depth of field, bloom with halation, and a full grade. For\n" +
            "stills, and for judging a scene as a picture. It costs more per frame and\n" +
            "is NOT what the game draws. Its own section below has the controls.\n\n" +
            "Unlit: albedo only.  Normals: the shading normal, bump included.\n" +
            "Wireframe: every edge, unlit - for reading topology and LOD density.";
        public float LightsMultiplier = 1.0f;

        public bool RespectShadowFlags = true;
        public bool FlagsChanged;
        public bool ShowSky = true;
        public bool WeatherEnabled = true;
        public int WeatherIndex = 0;
        public int RequestedWeather = -1;
        public string WeatherStatus = "";
        public bool AutoTime = false;
        public float TimeSpeed = 30.0f;
        public float CloudSpeed = 1.0f;
        public float SkyExposure = 1.0f;
        public bool SelectedPropLightsOnly = false;
        public bool FocusSelectedLight = false;
        public bool EntityPicking = false;
        public int TwoSidedMeshCount, TotalMeshCount;
        public int VolumeOverflow;
        public bool ScrollToActiveProp;
        public int LeftTabRequest = -1;

        public bool WalkMode = false;
        public float WalkSpeedDisplay = 1.0f;
        public string StatsText = "";

        public event Action<LoadedFile> RequestSaveAs;
        public event Action RequestSave;
        public event Action RequestOpenFile;
        public event Action<bool> RequestRegisterFileTypes;
        public event Action RequestOpenYtd;
        public event Action RequestNewProject;
        public event Action RequestOpenProject;
        public event Action<bool> RequestSaveProject;
        public string ProjectName = "";
        public event Action<LightAttributes> RequestFrameLight;
        public event Action RequestImportProjTex;
        public event Action RequestAddFile;
        public event Action RequestLoadTimecycle;
        public event Action RequestLoadTimeSchedule;
        public event Action<int> RequestLoadGameTimecycle;
        public event Action RequestLoadModifiers;
        private string[] modifierNames;
        public string[] GameTimecycleNames;
        public int GameTimecycleIndex = -1;
        public event Action RequestPickGtaFolder;
        public event Action RequestImportYtyp;
        public event Action RequestImportMapFiles_V36;
        public event Action RequestImportYmap;
        public event Action RequestClearImports;
        public event Action RequestReimportYtyp;
        public event Action RequestAddPropFolder;
        public event Action<string> RemovePropFolder;
        public List<string> PropFolders;
        public bool ImportPropLights = true;
        public bool ImportAllProps = true;
        public bool ImportVegetation = true;
        public const float DefaultExposure_U25 = 1.0f;
        public float Exposure = DefaultExposure_U25;
        public bool PostFxColourCorrect = true;
        private string propFilter = "";
        public float VolumeFeather = 0.85f;
        public bool SunShadows = true;
        public float SunShadowStrength = 0.85f;
        public event Action RequestNewLightProxy;

        public LightPropLibrary Library;
        public PropThumbnails Thumbs;
        public event Action<bool> RequestLibraryScan;
        public event Action<LightPropEntry, Vector2> RequestPlaceLibraryProp;
        public LightPropEntry DraggedLibraryProp;
        private string libFilter = "";

        public GameFileManager Game;
        public string MloStatus = "";

        public TimecycleData Timecycle;
        public bool TimecycleEnabled = true;
        public bool TimecycleHdr = true;
        public bool TimecycleAutoReload = true;
        public string TimecycleStatus = "";

        private Scene scene => SectionScene_S1() ?? (MloMode && mloScene != null ? mloScene : lightScene);
        private readonly Scene lightScene;
        private readonly Gizmo gizmo;
        private readonly AppSettings settings;

        public MaterialPanel Materials;

        public enum Space { Light, Material, Cinematic, Archive, World, Mlo, Particles, NavMesh, Terrain, Animation, Extension }
        public Space Workspace
        {
            get => workspace;
            set => EnterWorkspace(value);
        }
        private Space workspace = Space.Light;

        public bool MaterialMode
        {
            get => Workspace == Space.Material;
            set => Workspace = value ? Space.Material : Space.Light;
        }
        public bool CineMode => Workspace == Space.Cinematic;
        public bool PhotoModeAllowed => Workspace == Space.Light || Workspace == Space.Cinematic;
        public bool ArchiveMode => Workspace == Space.Archive;
        public bool WorldMode => Workspace == Space.World || NavMode;

        private bool cineSavedGrid, cineSavedGizmos, cineSavedAllGizmos, cineSavedMarkers, cineSavedPicking;
        private bool cineSaved;

        public void EnterCineWorkspace()
        {
            if (!cineSaved)
            {
                cineSavedGrid = ShowGrid; cineSavedGizmos = ShowGizmos;
                cineSavedAllGizmos = ShowAllGizmos; cineSavedMarkers = ShowMarkers;
                cineSavedPicking = EntityPicking;
                cineSaved = true;
            }
            ShowGrid = ShowGizmos = ShowAllGizmos = ShowMarkers = false;
            EntityPicking = false;
        }

        public void LeaveCineWorkspace()
        {
            if (!cineSaved) return;
            ShowGrid = cineSavedGrid; ShowGizmos = cineSavedGizmos;
            ShowAllGizmos = cineSavedAllGizmos; ShowMarkers = cineSavedMarkers;
            EntityPicking = cineSavedPicking;
            cineSaved = false;
        }

        public event Action<bool> WorkspaceChanged;

        private bool openDeleteConfirm;
        private bool openCloneConfirm;
        private Action cloneYes, cloneNo;
        public bool CloneWasInstance;

        public string CaptureBindingAction { get; private set; }

        public float LeftWidth => settings.LeftPanelWidth;
        public float RightWidth => settings.RightPanelWidth;

        public LightPanel(Scene scene, Gizmo gizmo, AppSettings settings)
        {
            this.lightScene = scene;
            this.gizmo = gizmo;
            this.settings = settings;
            ControlTimeOfDay = settings.ControlTimeOfDay;
            LoadWorldRenderOptions();
        }

        public void AskDeleteSelected()
        {
            if (scene.SelectedIndices.Count > 0) openDeleteConfirm = true;
        }

        public void AskCloneConfirm(Action yes, Action no)
        {
            cloneYes = yes;
            cloneNo = no;
            openCloneConfirm = true;
        }

        public bool TryCaptureKey(System.Windows.Forms.Keys combo)
        {
            if (CaptureBindingAction == null) return false;
            var key = combo & System.Windows.Forms.Keys.KeyCode;
            if (key == System.Windows.Forms.Keys.Escape)
            {
                CaptureBindingAction = null;
                return true;
            }
            if (key == System.Windows.Forms.Keys.ControlKey || key == System.Windows.Forms.Keys.ShiftKey ||
                key == System.Windows.Forms.Keys.Menu) return true;
            settings.SetBind(CaptureBindingAction, combo);
            CaptureBindingAction = null;
            return true;
        }

        private static Vector3 ToNum(SDX.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        private static SDX.Vector3 ToDx(Vector3 v) => new SDX.Vector3(v.X, v.Y, v.Z);

        public IntPtr LogoTexture = IntPtr.Zero;
        public int LogoWidth, LogoHeight;
        public IntPtr MaterialLogoTexture = IntPtr.Zero;
        public int MaterialLogoWidth, MaterialLogoHeight;
        public IntPtr WorldLogoTexture = IntPtr.Zero;
        public int WorldLogoWidth, WorldLogoHeight;
        public IntPtr MloLogoTexture = IntPtr.Zero;
        public int MloLogoWidth, MloLogoHeight;

        private void DrawWorkspaceLogo(float maxWidth)
        {
            var tex = LogoTexture;
            float w = LogoWidth, h = LogoHeight;
            if (MaterialMode && MaterialLogoTexture != IntPtr.Zero)
            {
                tex = MaterialLogoTexture;
                w = MaterialLogoWidth;
                h = MaterialLogoHeight;
            }
            else if (WorldMode && WorldLogoTexture != IntPtr.Zero)
            {
                tex = WorldLogoTexture;
                w = WorldLogoWidth;
                h = WorldLogoHeight;
            }
            else if (MloMode && MloLogoTexture != IntPtr.Zero)
            {
                tex = MloLogoTexture;
                w = MloLogoWidth;
                h = MloLogoHeight;
            }
            WorkspaceLogo_Q3(ref tex, ref w, ref h);
            WorkspaceLogo_R4(ref tex, ref w, ref h);
            WorkspaceLogo_U6(ref tex, ref w, ref h);
            if (tex == IntPtr.Zero || w <= 0 || h <= 0) return;
            float lw = Math.Min(ImGui.GetContentRegionAvail().X, maxWidth);
            ImGui.Image(tex, new Vector2(lw, lw * h / w));
        }

        public static string ForceOpenHeader;

        private static bool Header(string label, bool defaultOpen = false)
        {
            var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.SpanFullWidth;
            if (ForceOpenHeader != null)
                ImGui.SetNextItemOpen(label.Contains(ForceOpenHeader, StringComparison.OrdinalIgnoreCase),
                    ImGuiCond.Always);
            return ImGui.CollapsingHeader(label, flags);
        }

        public void Draw(float displayWidth, float displayHeight)
        {
            if (PhotoMode)
            {
                DrawPhotoModeHint(displayWidth, displayHeight);
                return;
            }

            if (!ShowInterface)
            {
                TopBarHeight = 0.0f;
                DrawInterfaceHiddenHint(displayWidth, displayHeight);
                DrawConfirmations();
                DrawLoadingOverlay();
                return;
            }

            DrawTopBar();
            if (ShellChrome_U27) DrawShellPopups_U27();
            DrawDockSpace_V30(displayWidth, displayHeight);
            if (WorldMode) DrawWorldToolbar(displayWidth);
            if (ShowLeftPanel) DrawLeftPanel(displayWidth, displayHeight);
            if (ShellOwnsRight_U27) DrawShellRightPage_U27(displayWidth, displayHeight);
            else if (ShowRightPanel) DrawRightPanel(displayWidth, displayHeight);
            if (WorldMode && !ShellChrome_U27) DrawStatusBar(displayWidth, displayHeight);
            DrawPanelHandles(displayWidth, displayHeight);
            WorkspaceOverlay_N4(displayWidth, displayHeight);
            WorkspaceOverlay_P4(displayWidth, displayHeight);
            WorkspaceOverlay_R4(displayWidth, displayHeight);
            WorkspaceOverlay_U6(displayWidth, displayHeight);
            if (TimelineVisible) DrawCineTimeline(displayWidth, displayHeight);
            if (WorldMode)
            {
                ProjectWindow?.Draw(displayWidth, displayHeight);
                ProjectWindow?.DrawTaskbar(displayWidth, displayHeight, StatusH);
                DrawPropsPanelWindow(displayWidth, displayHeight);
                GrassBrushWindow_S5(displayWidth, displayHeight);
            }
            if (MloMode) { MloCreator.RightPanelWidth_S1 = ShowRightPanel ? RightWidth : 0.0f; MloCreator.DrawWindow(scene, Timecycle, displayWidth, displayHeight); }
            DrawFiveMWindow_U12(displayWidth, displayHeight);
            DrawGameView_U13(displayWidth, displayHeight);
            DrawLogWindow_U21(displayWidth, displayHeight);
            DrawConfirmations();
            DrawTutorial();
            DrawTimecycleEditor();
            DrawGtaSetupDialog();
            DrawLoadingOverlay();
            UpdateLibraryDrag();
        }

        public bool PhotoMode;
        public bool RequestPhotoMode;

        private void DrawPhotoModeHint(float w, float h)
        {
            photoHintAge += ImGui.GetIO().DeltaTime;
            var io = ImGui.GetIO();
            if (io.MouseDown[0] || io.MouseDown[1] || io.MouseWheel != 0.0f || io.WantCaptureKeyboard)
                photoHintAge = Math.Max(photoHintAge, 1.25f);
            float a = 1.0f - Math.Clamp((photoHintAge - 1.25f) / 0.5f, 0.0f, 1.0f);
            if (a <= 0.001f) return;

            ImGui.SetNextWindowPos(new Vector2(w * 0.5f, h - 42), ImGuiCond.Always, new Vector2(0.5f, 1.0f));
            ImGui.SetNextWindowBgAlpha(0.35f * a);
            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize |
                        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing |
                        ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoInputs;
            if (ImGui.Begin("##photohint", flags))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 0.75f * a));
                ImGui.Text("Photo mode - press P to leave");
                ImGui.PopStyleColor();
            }
            ImGui.End();
        }

        private float photoHintAge;

        public void ResetPhotoHint() => photoHintAge = 0.0f;

        public void ApplyThemeFromSettings(bool save = true)
        {
            { bool h4 = false; WorkspaceTheme_N4(ref h4); if (h4) { if (save) settings.Save(); return; } }
            { bool hp = false; WorkspaceTheme_P4(ref hp); if (hp) { if (save) settings.Save(); return; } }
            { bool hr = false; WorkspaceTheme_R4(ref hr); if (hr) { if (save) settings.Save(); return; } }
            { bool hx = false; WorkspaceTheme_V68(ref hx); if (hx) { if (save) settings.Save(); return; } }
            { bool hu = false; WorkspaceTheme_U6(ref hu); if (hu) { if (save) settings.Save(); return; } }
            if (CineMode)
            {
                UiTheme.Apply(settings.ThemeIndex,
                    new Vector3(CineWorkspaceColour.X, CineWorkspaceColour.Y, CineWorkspaceColour.Z));
            }
            else if (ArchiveMode)
            {
                UiTheme.Apply(settings.ThemeIndex, new Vector3(
                    ArchiveWorkspaceColour.X, ArchiveWorkspaceColour.Y, ArchiveWorkspaceColour.Z));
            }
            else if (WorldMode)
            {
                UiTheme.Apply(settings.ThemeIndex, new Vector3(
                    WorldWorkspaceColour.X, WorldWorkspaceColour.Y, WorldWorkspaceColour.Z));
            }
            else if (MloMode)
            {
                UiTheme.Apply(settings.ThemeIndex, new Vector3(
                    MloWorkspaceColour.X, MloWorkspaceColour.Y, MloWorkspaceColour.Z));
            }
            else
            {
                var a = settings.AccentFor(MaterialMode);
                UiTheme.Apply(settings.ThemeIndex, new Vector3(a[0], a[1], a[2]));
            }
            if (save) settings.Save();
        }

        public bool ShowTimecycleEditor;
        public event Action RequestSaveTimecycle;
        public event Action RequestSaveModifiers;
        private string tcFilter = "";
        public string PreselectTimecycleVar;
        private string tcSelectedVar;
        private int tcTab;

        private void DrawTimecycleEditor()
        {
            if (!ShowTimecycleEditor) return;
            if (Timecycle == null) { ShowTimecycleEditor = false; return; }

            var io = ImGui.GetIO();
            ImGui.SetNextWindowSize(new Vector2(720, 560), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                ImGuiCond.FirstUseEver, new Vector2(0.5f, 0.5f));

            bool open = ShowTimecycleEditor;
            if (!ImGui.Begin("Timecycle editor", ref open))
            {
                ShowTimecycleEditor = open;
                ImGui.End();
                return;
            }
            ShowTimecycleEditor = open;

            if (ImGui.BeginTabBar("tctabs"))
            {
                if (ImGui.BeginTabItem("Cycle")) { tcTab = 0; DrawCycleTab(); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Interior modifier")) { tcTab = 1; DrawModifierTab(); ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }
            ImGui.End();
        }

        private void DrawCycleTab()
        {
            var r = Timecycle.Current;
            if (r == null)
            {
                ImGui.TextWrapped("No timecycle loaded. Pick one in the Weather dropdown, or load an XML.");
                return;
            }

            if (tcSelectedVar == null && PreselectTimecycleVar != null)
            {
                tcSelectedVar = PreselectTimecycleVar;
                PreselectTimecycleVar = null;
            }
            ImGui.Text($"{Timecycle.LoadedName}  ·  region {r.Name}  ·  {r.Values.Count} variables");
            ImGui.SameLine();
            if (ImGui.SmallButton("Save as XML...")) RequestSaveTimecycle?.Invoke();
            ImGui.Separator();

            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##tcfilter", "filter variables (e.g. light_)", ref tcFilter, 64);
            ImGui.SameLine();
            ImGui.TextDisabled($"hour {Timecycle.CurrentHour:0.0} -> sample {Timecycle.CurrentSampleIndex}");

            ImGui.BeginChild("tcvars", new Vector2(250, -1), ImGuiChildFlags.Borders);
            foreach (var name in r.Values.Keys)
            {
                if (tcFilter.Length > 0 && name.IndexOf(tcFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool sel = name == tcSelectedVar;
                if (ImGui.Selectable(name, sel)) tcSelectedVar = name;
            }
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("tcvals", new Vector2(0, -1), ImGuiChildFlags.Borders);
            if (tcSelectedVar != null && r.Values.TryGetValue(tcSelectedVar, out var vals))
            {
                ImGui.Text(tcSelectedVar);
                ImGui.TextDisabled($"value now: {r.Get(tcSelectedVar, Timecycle.CurrentSampleIndex, Timecycle.CurrentSampleBlend):0.0000}");
                ImGui.Separator();

                for (int i = 0; i < vals.Length; i++)
                {
                    float hour = i < Timecycle.Samples.Count ? Timecycle.Samples[i].Hour : i;
                    bool isCurrent = i == Timecycle.CurrentSampleIndex;
                    if (isCurrent) ImGui.PushStyleColor(ImGuiCol.Text, AccentText());
                    ImGui.Text($"{hour,5:00}:00");
                    if (isCurrent) ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(-1);
                    ImGui.DragFloat($"##kf{i}", ref vals[i], 0.005f);
                }

                ImGui.Separator();
                if (ImGui.SmallButton("Set all keys"))
                {
                    float v = r.Get(tcSelectedVar, Timecycle.CurrentSampleIndex, Timecycle.CurrentSampleBlend);
                    for (int i = 0; i < vals.Length; i++) vals[i] = v;
                }
            }
            else
            {
                ImGui.TextDisabled("Pick a variable on the left.");
            }
            ImGui.EndChild();
        }

        private void DrawModifierTab()
        {
            if (Timecycle.Modifiers.Count == 0)
            {
                ImGui.TextWrapped("No modifiers loaded. Select a GTA V folder, or use Load modifiers XML...");
                return;
            }

            var m = Timecycle.CurrentModifier;
            ImGui.Text(m == null ? "No modifier selected" : $"{m.Name}  ·  {m.Values.Count} overrides  ·  {m.Source}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Save as XML...")) RequestSaveModifiers?.Invoke();

            ImGui.SetNextItemWidth(-110);
            UiSlider_U28.Float("##mstr", ref Timecycle.ModifierStrength, 0.0f, 1.0f, "%.2f");
            ImGui.SameLine();
            ImGui.TextDisabled("Strength");
            ImGui.Separator();

            if (m == null)
            {
                ImGui.TextDisabled("Pick one in the Interior mod dropdown.");
                return;
            }

            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##mfilter", "filter overrides", ref tcFilter, 64);
            ImGui.Separator();

            ImGui.BeginChild("modvals", new Vector2(0, -30), ImGuiChildFlags.Borders);
            string toRemove = null;
            foreach (var key in new List<string>(m.Values.Keys))
            {
                if (tcFilter.Length > 0 && key.IndexOf(tcFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                ImGui.PushID(key);
                if (ImGui.SmallButton("x")) toRemove = key;
                ImGui.PopID();
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                float v = m.Values[key];
                if (ImGui.DragFloat($"##v{key}", ref v, 0.005f)) m.Values[key] = v;
                ImGui.SameLine();
                ImGui.TextUnformatted(key);
            }
            if (toRemove != null) m.Values.Remove(toRemove);
            ImGui.EndChild();

            var cur = Timecycle.Current;
            if (cur != null)
            {
                ImGui.SetNextItemWidth(220);
                ImGui.InputTextWithHint("##addvar", "variable name to override", ref tcAddVar, 64);
                ImGui.SameLine();
                if (ImGui.SmallButton("Add") && tcAddVar.Length > 0 && !m.Values.ContainsKey(tcAddVar))
                {
                    m.Values[tcAddVar] = cur.Get(tcAddVar, Timecycle.CurrentSampleIndex, Timecycle.CurrentSampleBlend);
                }
            }
        }

        private string tcAddVar = "";

        private void DrawTutorial()
        {
            if (openTutorial)
            {
                ImGui.OpenPopup("Tutorial - RAGE Tools");
                openTutorial = false;
            }
            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(620, 540), ImGuiCond.Appearing);
            if (ImGui.BeginPopupModal("Tutorial - RAGE Tools"))
            {
                ImGui.BeginChild("tutscroll", new Vector2(0, -36));

                void H(string t) { ImGui.Spacing(); ImGui.TextColored(AccentText(), t); ImGui.Separator(); }
                void T(string t) { ImGui.TextWrapped(t); ImGui.Spacing(); }

                H("1. Open a model");
                T("Drag & drop a .ydr or .yft anywhere on the window (or use Open...). Embedded textures load " +
                  "automatically; if the model uses a shared texture dictionary, drop the matching .ytd too. " +
                  "Interiors with no lights look dark - raise Ambient or switch Shading to Unlit to inspect geometry.");

                H("2. Camera");
                T("Left-drag orbits, middle-drag pans, wheel zooms. Right-click selects. F frames the selected " +
                  "light (or the model). WASD flies, R/C go up and down, Shift is fast and Ctrl is slow, and " +
                  "speed scales with how far out you're zoomed.");
                T("The fly keys are GATED. In Select mode (Q) they fly freely; the " +
                  "moment a Move or Rotate gizmo is active they go quiet unless you HOLD THE LEFT BUTTON. That is " +
                  "what lets W be both 'gizmo: move' and 'fly forward' without the two fighting each other - reach " +
                  "for the gizmo and the camera stays put, hold left and W flies. Press V for walk mode, where " +
                  "movement is always live: the mouse steers without any button, the wheel changes speed, and V or " +
                  "Esc exits.");

                H("3. Selecting lights");
                T("Every light shows a small coloured marker in the viewport - RIGHT-click it to select. Right click is " +
                  "the select gesture everywhere in the tool; the left button is the camera, the gizmo and the brushes. In the left list " +
                  "Ctrl+click adds/removes from the selection, Shift+click selects a range, double-click frames the " +
                  "light. Duplicate and Delete act on the whole selection (Delete asks for confirmation). " +
                  "Z takes the camera straight to the selected light (with nothing selected it jumps to 0,0,0).");

                H("4. Moving and rotating (gizmo)");
                T("Q/W/E switch Select / Move / Rotate, like 3ds Max. Move: drag an axis arrow, a corner square " +
                  "(plane move) or the centre box (screen move). Rotate: drag a ring - rotation snaps to the angle " +
                  "set in View > Rotate snap. Hold Shift and drag the move gizmo to CLONE the light; hold ALT " +
                  "instead and the clone is a linked INSTANCE - its parameters stay in sync with the original " +
                  "and only its transform is its own. On drop you confirm keep or cancel.");

                H("5. Light parameters");
                T("The right panel edits everything the game stores: colour, intensity, falloff + exponent, cone " +
                  "half-angles, capsule extent, corona, volume, shadows, fades, culling plane, flags and time flags. " +
                  "Every edit updates the viewport instantly using GTA V's real light math.");

                H("6. Time flags and day/night");
                T("Time flags choose the game hours a light is on (0 or all 24 = always). The Hour slider previews " +
                  "any time of day; lights outside their hours go dark and the scene ambient follows a day/night curve. " +
                  "With a GTA V folder selected, the Timecycle section's Weather dropdown lists every cycle the " +
                  "game ships with (w_clear, w_thunder, w_foggy, ...); w_clear loads by itself so the scene is " +
                  "lit by the game's global lighting from the start, on its own hour schedule. Under it, Interior mod picks " +
                  "a timecycle modifier: the overrides a room applies on top of the weather, which is what " +
                  "makes an interior look like itself. Importing an MLO selects the modifier its rooms ask " +
                  "for automatically; Strength blends between the plain cycle and the full interior look. " +
                  "Edit timecycle... opens the full editor: every variable at every keyframe, and the " +
                  "modifier's overrides, all live, with Save as XML for both.");

                H("7. Projected texture (IES / gobo)");
                T("Spot lights can project a texture - this is how GTA does IES-style patterns. In Attachment & IDs, " +
                  "pick a texture from the loaded dictionaries (or enter its hash); the projection flag is set " +
                  "automatically and the pattern shows live on lit surfaces. Load the .ytd containing the texture first.");

                H("8. Culling plane");
                T("Enable flag 18 to clip a light with a plane: the orange grid shows the plane, the green arrow " +
                  "marks the side that keeps light, the red X the culled side.");

                H("9. Saving");
                T("Ctrl+S saves back into the same file (a one-time .bak backup is kept next to it). Save As... " +
                  "writes a copy. Files are standard RSC7 resources - import into your RPF with OpenIV.");

                H("10. Loading a whole interior (YTYP / MLO)");
                T("Under GTA V / MLO import, point Select GTA V folder... at your install (the folder with " +
                  "GTA5.exe) - the key is derived from your own copy of the game, nothing ships with this tool. " +
                  "Then Import YTYP (MLO)... and every entity in the interior is placed at its exact position, " +
                  "with base-game props pulled straight from the archives and your custom props read from the " +
                  "folder the .ytyp lives in. Each unique prop is built once and instanced, so hundreds of " +
                  "entities cost almost nothing. If some props aren't found, use Add prop folder... to point at " +
                  "where they live and hit Reload.");
                T("Any prop that carries lights joins the Props list (tinted blue) with its lights fully " +
                  "editable at their real positions in the interior - move, retune and save them like a prop " +
                  "you opened by hand. Props that live in the game archives are editable for preview only; " +
                  "their Save button is greyed out because there is no loose file to write to. When the MLO " +
                  "places the same prop more than once you'll see (xN): it lights the room at EVERY " +
                  "placement, but appears in the lists once, because it's one file with one set of local " +
                  "coordinates. The other placements are copies - they light and glow but aren't listed, " +
                  "picked or saved, and editing the original moves all of them, exactly like in game. The " +
                  "stats line reads 3+12 lights when twelve of them are copies.");
                T("Double-click a prop in the list to rename it - that renames what it SAVES TO, so the file " +
                  "already on disk is left alone and the next save writes the new name beside it. " +
                  "+ New light prop asks for a name first and creates an empty light-only prop to build in.");
                T("The bottom of the left panel has four tabs. Props is the list above; Archetype has " +
                  "Create (your own archetypes) and Imported (what the loaded .ytyp declares - every " +
                  "archetype with its asset, flags, LOD distances and bounds, plus a Placement tab for " +
                  "where the selected one sits, interiors tinted); Library is the light prop browser " +
                  "(below); YMAP export builds a .ymap out of them.");
                T("Library is a catalogue of every prop that has lights inside it - your own prop folders, " +
                  "plus your whole GTA V install once you hit Scan game archives. That sweep has to open " +
                  "every model in the game to find out which ones carry a light, so it takes a few minutes; " +
                  "it only happens once and is cached next to the exe. Search by name, then DRAG A TILE " +
                  "INTO THE VIEWPORT to place it - it lands on whatever is under the cursor with its lights " +
                  "ready to edit, and goes out through YMAP export like any other prop. Each thumbnail is " +
                  "a real render of the prop lit by its own lights.");
                T("Archetype > Create is how a light prop becomes something the game can stream. A prop " +
                  "needs an archetype, and an archetype needs bounds - but a light proxy's drawable is a " +
                  "4 cm quad, so measuring the geometry would give the game a box far smaller than the " +
                  "light it carries and the prop would stream out while its light should still be lit. " +
                  "The bounds here are measured from the LIGHTS instead: a point light contributes a " +
                  "sphere of its falloff, a spot the cone it actually throws, a capsule its tube plus " +
                  "falloff. The draw distance is set from that radius to match.");
                T("Creating a light prop makes its archetype automatically. Otherwise select props in the " +
                  "Props tab and hit From selected. Name, texture/physics dictionary, LOD and HD " +
                  "distances and flags are all editable; Recompute from lights re-measures after you " +
                  "move a light or change its falloff (Export re-measures everything anyway, so what " +
                  "you ship always matches what you see). Export .ytyp... writes them all out - pair it " +
                  "with Save As... for the .ydr and YMAP export for the placement, and you have a " +
                  "complete streamable set.");

                H("11. Maps (YMAP)");
                T("Import YMAP... places a map's entities at their world positions, and you can pick " +
                  "several .ymap files at once - they merge into one scene. A ymap that places an " +
                  "interior brings the whole interior in with it. Map coordinates sit thousands of " +
                  "units from the grid, so press Z (with nothing selected) to jump the camera back to 0,0,0.");
                T("YMAP export goes the other way. Pick the props you want in the Props tab (click, " +
                  "Ctrl+click, Shift+click - same as the lights list), switch to the YMAP export tab and " +
                  "hit Add selected; Add all takes everything instead. Each prop keeps the placement it " +
                  "was imported at, and a light prop you made yourself lands wherever you put it. " +
                  "Export .ymap... writes a real .ymap you can stream. Untick an entry to leave it out.");
                T("Interiors and maps don't mix: importing a .ymap clears an imported .ytyp and the other " +
                  "way round, because one is an interior in its own space and the other is a chunk of the " +
                  "world. Several files of the SAME kind still merge into one scene.");

                H("12. Projects");
                T("Save project writes a .rlep next to your work holding everything: which models are open, the " +
                  "imported MLO, your GTA and prop folders, the timecycle and the current view. Open project (or " +
                  "just drag the .rlep onto the window) puts the whole session back exactly as you left it. " +
                  "Paths inside the project folder are stored relative, so the folder can be zipped and shared.");

                H("13. The menu bar");
                T("Project, File, Edit, Shortcuts and Help live across the top. Project holds the whole " +
                  "workspace (.rlep); File opens models, adds props, makes a new light prop, loads a .ytd " +
                  "and saves; Edit is undo/redo; Shortcuts rebinds every key right there in the menu - " +
                  "click a key, press the new one, Esc cancels.");

                H("14. Customising");
                T("The Theme section changes the UI colours. Both panels resize by dragging their inner " +
                  "edge. Everything persists in settings.json next to the exe.");

                H("Credits");
                T("Escobar - Escobar Tools: tool concept and direction.\n" +
                  "Claude AI (Anthropic) - implementation of this editor.\n" +
                  "dexyfex & CodeWalker contributors - RAGE formats and lighting math (CodeWalker.Core).\n" +
                  "Sollumz team & GIMS Evo (3Doomer) - light parameter semantics references.");

                ImGui.EndChild();
                if (ImGui.Button("Close", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        public bool ShowLeftPanel = true, ShowRightPanel = true;

        public bool ShowInterface = true;
        private double interfaceHiddenAt = -1.0;

        private void DrawInterfaceHiddenHint(float displayWidth, float displayHeight)
        {
            var io = ImGui.GetIO();
            double now = ImGui.GetTime();
            if (interfaceHiddenAt < 0) interfaceHiddenAt = now;
            if (Math.Abs(io.MouseDelta.X) + Math.Abs(io.MouseDelta.Y) > 0.5f) interfaceHiddenAt = now;

            const double Hold = 2.5, Fade = 1.0;
            double age = now - interfaceHiddenAt;
            if (age > Hold + Fade) return;
            float alpha = age <= Hold ? 1.0f : 1.0f - (float)((age - Hold) / Fade);

            ImGui.SetNextWindowPos(new Vector2(14, displayHeight - 14), ImGuiCond.Always, new Vector2(0, 1));
            ImGui.SetNextWindowBgAlpha(0.0f);
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings |
                        ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing |
                        ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
            ImGui.Begin("##hiddenhint", flags);
            ImGui.TextColored(new Vector4(1, 1, 1, 0.55f * alpha), "F11  show interface");
            ImGui.End();
        }

        public void ToggleInterface()
        {
            ShowInterface = !ShowInterface;
            interfaceHiddenAt = -1.0;
            if (ShowInterface) ShowLeftPanel = ShowRightPanel = true;
        }

        public const float HandleStripW = 22.0f;

        private void DrawPanelHandles(float displayWidth, float displayHeight)
        {
            const float TabW = 18.0f, TabH = 46.0f;
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar |
                        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav |
                        ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize;
            float y = TopBarHeight + (displayHeight - TopBarHeight) * 0.5f - TabH * 0.5f;
            if (DockedLayout) return;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2, 2));

            float lx = ShowLeftPanel ? settings.LeftPanelWidth : 0.0f;
            ImGui.SetNextWindowPos(new Vector2(lx, y), ImGuiCond.Always);
            ImGui.Begin("##lefthandle", flags);
            if (ImGui.Button(ShowLeftPanel ? "<##hideleft" : ">##showleft", new Vector2(TabW, TabH)))
                ShowLeftPanel = !ShowLeftPanel;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(ShowLeftPanel ? "Hide the left panel  (F9)" : "Show the left panel  (F9)");
            ImGui.End();

            if (!ShellOwnsRight_U27)
            {
                float rx = displayWidth - TabW - 4.0f - (ShowRightPanel ? settings.RightPanelWidth : 0.0f);
                ImGui.SetNextWindowPos(new Vector2(rx, y), ImGuiCond.Always);
                ImGui.Begin("##righthandle", flags);
                if (ImGui.Button(ShowRightPanel ? ">##hideright" : "<##showright", new Vector2(TabW, TabH)))
                    ShowRightPanel = !ShowRightPanel;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(ShowRightPanel ? "Hide the right panel  (F10)" : "Show the right panel  (F10)");
                ImGui.End();
            }

            ImGui.PopStyleVar();
        }

        public void ToggleAllPanels()
        {
            bool anyShown = ShowLeftPanel || ShowRightPanel;
            ShowLeftPanel = ShowRightPanel = !anyShown;
        }

        private void DrawLeftPanel(float displayWidth, float displayHeight)
        {
            float shellTop = TopBarHeight + (WorldMode ? ToolbarHNow_U5 : 0.0f);
            displayHeight -= shellTop + (WorldMode ? StatusH : 0.0f);
            bool leftOpen = true;
            var wflags = PanelWindow_V30(0, shellTop, settings.LeftPanelWidth, displayHeight,
                                         180 * UiScale_V17.Scale, 700 * UiScale_V17.Scale,
                                         ForcePanelSize_V17 > 0, ref leftOpen);
            ImGui.Begin(LeftPanelTitle_V30 + "###leftpanel", wflags);

            float w = ImGui.GetWindowSize().X;
            if (!DockedLayout && Math.Abs(w - settings.LeftPanelWidth) > 0.5f) settings.LeftPanelWidth = w;

            { bool h4 = false; WorkspaceLeft_N4(displayHeight, ref h4); if (h4) { ImGui.End(); return; } }
            { bool hp = false; WorkspaceLeft_P4(displayHeight, ref hp); if (hp) { ImGui.End(); return; } }
            { bool hr = false; WorkspaceLeft_R4(displayHeight, ref hr); if (hr) { ImGui.End(); return; } }
            { bool hx = false; WorkspaceLeft_V68(displayHeight, ref hx); if (hx) { ImGui.End(); return; } }
            { bool hu = false; WorkspaceLeft_U6(displayHeight, ref hu); if (hu) { ImGui.End(); return; } }

            if (MaterialMode)
            {
                DrawMaterialModeLeft(displayHeight);
                ImGui.End();
                return;
            }
            if (CineMode)
            {
                DrawCineWorkspaceLeft(displayHeight);
                ImGui.End();
                return;
            }
            if (ArchiveMode)
            {
                DrawArchiveLeft(displayHeight);
                ImGui.End();
                return;
            }
            if (WorldMode)
            {
                DrawExplorerLeft(displayHeight);
                ImGui.End();
                return;
            }
            if (MloMode)
            {
                DrawMloWorkspaceLeft(displayHeight);
                ImGui.End();
                return;
            }

            DrawWorkspaceLogo(150.0f);

            ImGui.TextDisabled("LIGHTS");
            ImGui.SameLine();
            ImGui.Text($"({scene.Lights.Count})");
            if (!string.IsNullOrEmpty(StatsText))
            {
                ImGui.TextDisabled(StatsText);
            }
            if (scene.SelectedIndices.Count > 1)
            {
                ImGui.SameLine();
                ImGui.TextColored(AccentText(), $"{scene.SelectedIndices.Count} selected");
            }
            ImGui.Separator();

            if (WalkMode)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Ok);
                ImGui.TextWrapped($"WALK MODE  (V or Esc to exit)");
                ImGui.PopStyleColor();
                ImGui.TextDisabled($"speed x{WalkSpeedDisplay:0.##} (scroll or X/Z)");
                ImGui.TextDisabled("WASD fly  R/C up-down  Shift fast  Ctrl slow");
                ImGui.TextDisabled("Mouse steers without holding a button");
            }
            else
            {
                DrawModeButton("Select (Q)", GizmoMode.Select);
                ImGui.SameLine();
                DrawModeButton("Move (W)", GizmoMode.Translate);
                ImGui.SameLine();
                DrawModeButton("Rotate (E)", GizmoMode.Rotate);
                DrawGizmoExtras_U11();
                ImGui.TextDisabled("WASD fly  R/C up-down  Shift fast  Ctrl slow");
                ImGui.TextDisabled($"X/Z: speed   V: mouse-look   {AppSettings.KeyLabel(settings.GetBind("GoToOrigin"))}: go to light");
                ImGui.TextDisabled("Shift+drag gizmo: copy   Alt+drag: instance");
            }
            ImGui.Separator();

            if (!scene.HasModel)
            {
                ImGui.TextWrapped("Drag & drop a .ydr or .yft, or use + Prop / Open below.");
                ImGui.Separator();
                DrawSceneTabs();
                ImGui.End();
                return;
            }

            if (ImGui.Button("+ Point")) scene.AddLight(1);
            ImGui.SameLine();
            if (ImGui.Button("+ Spot")) scene.AddLight(2);
            ImGui.SameLine();
            if (ImGui.Button("+ Capsule")) scene.AddLight(4);

            bool selAny = scene.SelectedIndices.Count > 0;
            if (!selAny) ImGui.BeginDisabled();
            if (ImGui.Button("Duplicate")) scene.DuplicateSelected(false);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Independent copies");
            ImGui.SameLine();
            if (ImGui.Button("Instance")) scene.DuplicateSelected(true);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Linked duplicates: parameters stay in sync\n(position/rotation stay independent). Editor-only link.");
            DrawInstanceLinkButtons_U11();
            ImGui.SameLine();
            if (ImGui.Button("Delete")) AskDeleteSelected();
            ImGui.SameLine();
            if (ImGui.Button("Copy")) scene.CopySettings();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy the selected light's settings");
            if (!selAny) ImGui.EndDisabled();
            ImGui.SameLine();
            bool canPaste = selAny && scene.HasClipboard;
            if (!canPaste) ImGui.BeginDisabled();
            if (ImGui.Button("Paste")) scene.PasteSettings();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Paste copied settings onto every selected light\n(keeps each light's position and bone)");
            if (!canPaste) ImGui.EndDisabled();

            ImGui.Checkbox("This prop only", ref SelectedPropLightsOnly);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show only the lights belonging to the selected prop - the rest go dark\n" +
                                 "and drop out of the list. Makes one prop workable inside a 300-light interior.");
            ImGui.SameLine();
            ImGui.Checkbox("Orbit light", ref FocusSelectedLight);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Orbit the camera around the selected light instead of the scene centre,\n" +
                                 "so dragging left/right circles it. Selecting another light moves the pivot.");
            ImGui.Separator();

            float bottomH = Math.Min(Math.Max(260f, displayHeight * 0.44f), displayHeight - 280f);

            if (ImGui.BeginListBox("##lightlist", new Vector2(-1, -(bottomH + 26))))
            {
                var onlyFile = SelectedPropLightsOnly
                    ? (scene.ActiveFile ?? scene.OwnerFile(scene.SelectedLight)) : null;
                for (int i = 0; i < scene.Lights.Count; i++)
                {
                    var l = scene.Lights[i];
                    if (onlyFile != null && scene.OwnerFile(l) != onlyFile) continue;
                    string typeName = LightDefs.TypeNames[LightDefs.TypeToIndex((byte)l.Type)];
                    bool active = LightDefs.IsActiveAtHour(l.TimeFlags, (int)PreviewHour);
                    int ig = scene.InstanceGroup(l);
                    string fileTag = scene.Files.Count > 1 ? $"  [{scene.OwnerFile(l)?.Name}]" : "";
                    string label = $"{i}: {typeName}{(ig > 0 ? $" (i{ig})" : "")}{fileTag}##light{i}";
                    var col = new Vector4(
                        0.35f + 0.65f * l.ColorR / 255.0f,
                        0.35f + 0.65f * l.ColorG / 255.0f,
                        0.35f + 0.65f * l.ColorB / 255.0f, 1);
                    ImGui.PushStyleColor(ImGuiCol.Text, active ? col : UiTheme.Muted);
                    if (ImGui.Selectable(label, scene.IsSelected(i)))
                    {
                        var io = ImGui.GetIO();
                        if (io.KeyCtrl) scene.ToggleSelect(i);
                        else if (io.KeyShift && scene.SelectedIndices.Count > 0) scene.RangeSelectTo(i);
                        else scene.SelectedIndex = i;
                    }
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        RequestFrameLight?.Invoke(l);
                    }
                }

                if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                    && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.A, false))
                {
                    scene.SelectedIndices.Clear();
                    for (int i = 0; i < scene.Lights.Count; i++)
                    {
                        if (onlyFile != null && scene.OwnerFile(scene.Lights[i]) != onlyFile) continue;
                        scene.SelectedIndices.Add(i);
                    }
                    if (scene.SelectedIndices.Count > 0)
                        MloStatus = $"Selected {scene.SelectedIndices.Count} light(s).";
                }
                ImGui.EndListBox();
            }

            if (scene.LastDroppedLights > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Warn);
                ImGui.TextWrapped($"GPU light cap: {scene.LastEmittedLights + scene.LastDroppedLights} wanted, " +
                                  $"{GpuLight.MaxLights} shown. All are saved.");
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Placement copies count towards this: a prop placed more than once\n" +
                                     "emits one light per placement.\n\n" +
                                     "Editable lights are filled in first, so a light you just made\n" +
                                     "always renders - only copies are turned away.");
            }
            else
            {
                ImGui.TextDisabled("Ctrl/Shift+click: multi-select");
            }

            DrawMoveLightsButton_V35();

            ImGui.Separator();
            DrawSceneTabs();

            ImGui.End();
        }

        private void DrawMaterialModeLeft(float displayHeight)
        {
            DrawWorkspaceLogo(150.0f);
            ImGui.Separator();

            if (!WalkMode)
            {
                DrawModeButton("Select (Q)", GizmoMode.Select);
                ImGui.SameLine();
                DrawModeButton("Move (W)", GizmoMode.Translate);
                ImGui.SameLine();
                DrawModeButton("Rotate (E)", GizmoMode.Rotate);
                ImGui.TextDisabled("WASD fly  R/C up-down  V mouse-look");
                ImGui.TextDisabled("Click a surface to pick its material");
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Ok);
                ImGui.TextWrapped("WALK MODE  (V or Esc to exit)");
                ImGui.PopStyleColor();
                ImGui.TextDisabled($"speed x{WalkSpeedDisplay:0.##} (scroll or X/Z)");
            }

            ImGui.TextDisabled("MATERIALS");
            if (!string.IsNullOrEmpty(Materials.Status)) ImGui.TextDisabled(Materials.Status);

            float bottomH = Math.Min(Math.Max(240f, displayHeight * 0.38f), displayHeight - 260f);
            if (ImGui.BeginChild("matlisthost", new Vector2(0, -bottomH), ImGuiChildFlags.None))
            {
                Materials.DrawList();
            }
            ImGui.EndChild();

            ImGui.Separator();
            DrawSceneTabs();
        }

        private void DrawMaterialModeLights()
        {
            if (!scene.HasModel)
            {
                ImGui.TextWrapped("Load a prop first.");
                return;
            }

            ImGui.TextWrapped("Add a light to judge specular and normal maps against.");
            if (ImGui.Button("+ Point")) scene.AddLight(1);
            ImGui.SameLine();
            if (ImGui.Button("+ Spot")) scene.AddLight(2);
            ImGui.SameLine();
            if (ImGui.Button("+ Capsule")) scene.AddLight(4);

            bool selAny = scene.SelectedIndices.Count > 0;
            if (!selAny) ImGui.BeginDisabled();
            if (ImGui.Button("Duplicate")) scene.DuplicateSelected(false);
            ImGui.SameLine();
            if (ImGui.Button("Delete")) AskDeleteSelected();
            if (!selAny) ImGui.EndDisabled();

            ImGui.Separator();
            if (ImGui.BeginListBox("##matlightlist", new Vector2(-1, 150)))
            {
                for (int i = 0; i < scene.Lights.Count; i++)
                {
                    var l = scene.Lights[i];
                    string typeName = LightDefs.TypeNames[LightDefs.TypeToIndex((byte)l.Type)];
                    var col = new Vector4(
                        0.35f + 0.65f * l.ColorR / 255.0f,
                        0.35f + 0.65f * l.ColorG / 255.0f,
                        0.35f + 0.65f * l.ColorB / 255.0f, 1);
                    ImGui.PushStyleColor(ImGuiCol.Text, col);
                    if (ImGui.Selectable($"{i}: {typeName}##mlight{i}", scene.IsSelected(i)))
                    {
                        var io = ImGui.GetIO();
                        if (io.KeyCtrl) scene.ToggleSelect(i);
                        else scene.SelectedIndex = i;
                    }
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        RequestFrameLight?.Invoke(l);
                }
                ImGui.EndListBox();
            }

            var sel = scene.SelectedLight;
            if (sel != null) DrawLightEditor(sel);
            else ImGui.TextDisabled("Select a light above, or click one in the viewport.");
            DrawLightPresets_V20(sel);
        }

        private static void SameLineIfFits(string nextLabel)
        {
            var st = ImGui.GetStyle();
            float w = ImGui.CalcTextSize(nextLabel).X + st.FramePadding.X * 2.0f;
            float right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
            if (ImGui.GetItemRectMax().X + st.ItemSpacing.X + w <= right) ImGui.SameLine();
        }

        private void DrawSceneTabs()
        {
            ImGui.BeginChild("leftbottom", new Vector2(0, 0), ImGuiChildFlags.None);

            if (ImGui.SmallButton("+ Prop")) RequestAddFile?.Invoke();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add a .ydr / .yft to the scene");
            SameLineIfFits("+ Light prop");
            if (ImGui.SmallButton("+ Light prop")) RequestNewLightProxy?.Invoke();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Asks for a name, then creates an empty light-only prop");
            SameLineIfFits("Open...");
            if (ImGui.SmallButton("Open...")) RequestOpenFile?.Invoke();
            if (ImGui.SmallButton("Load YTD...")) RequestOpenYtd?.Invoke();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Load a texture dictionary");

            bool canSave = scene.HasModel;
            string saveLabel = scene.Files.Count > 1 ? "Save all" : "Save";
            SameLineIfFits(saveLabel);
            if (!canSave) ImGui.BeginDisabled();
            if (ImGui.SmallButton(saveLabel)) RequestSave?.Invoke();
            if (!canSave) ImGui.EndDisabled();
            SameLineIfFits("Save As...");
            if (!canSave) ImGui.BeginDisabled();
            if (ImGui.SmallButton("Save As...")) RequestSaveAs?.Invoke(null);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Write the active prop (highlighted in Props) to a new file");
            if (!canSave) ImGui.EndDisabled();

            SameLineIfFits("Project");
            if (ImGui.SmallButton("Project")) ImGui.OpenPopup("projectmenu");
            if (ImGui.BeginPopup("projectmenu"))
            {
                if (ImGui.MenuItem("New project")) RequestNewProject?.Invoke();
                if (ImGui.MenuItem("Open project...")) RequestOpenProject?.Invoke();
                if (ImGui.MenuItem("Save project")) RequestSaveProject?.Invoke(false);
                if (ImGui.MenuItem("Save project as...")) RequestSaveProject?.Invoke(true);
                if (!string.IsNullOrEmpty(ProjectName))
                {
                    ImGui.Separator();
                    ImGui.TextDisabled(ProjectName);
                }
                ImGui.EndPopup();
            }
            if (!MaterialMode) DrawGoToRow_O3();
            ImGui.Separator();

            if (ImGui.BeginTabBar("scenetabs"))
            {
                var tabs = new (string Name, Action Draw)[]
                {
                    ("Props", DrawFileSection),
                    ("Archetype", DrawArchetypeSection),
                    ("Library", DrawLibrarySection),
                    ("Ymap", DrawYmapSection),
                };
                int first = ForceOpenHeader == null ? 0 : Array.FindIndex(tabs,
                    t => t.Name.Contains(ForceOpenHeader, StringComparison.OrdinalIgnoreCase));
                if (first < 0) first = 0;
                for (int k = 0; k < tabs.Length; k++)
                {
                    int i = (first + k) % tabs.Length;
                    var tflags = (LeftTabRequest == i) ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                    bool dummy = true;
                    bool open = tflags == ImGuiTabItemFlags.None
                        ? ImGui.BeginTabItem(tabs[i].Name)
                        : ImGui.BeginTabItem(tabs[i].Name, ref dummy, tflags);
                    if (open) { leftTab = i; tabs[i].Draw(); ImGui.EndTabItem(); }
                }
                LeftTabRequest = -1;
                ImGui.EndTabBar();
            }
            ImGui.EndChild();
        }

        public bool openResetConfirm;
        public event Action RequestResetSettings;

        public void ResetViewDefaults()
        {
            ShowCoronas = true;
            ShowVolumes = true;
            ShowGrid = true;
            ShowMarkers = true;
            ShowShadows = true;
            ShadowMode = scene.Lights.Count > 64 ? 2 : 1;
            ShowGizmos = true;
            ShowAllGizmos = false;
            AmbientLevel = 0.12f;
            RenderMode = 0;
            LightsMultiplier = 1.0f;
            ShowSky = true;
            WeatherEnabled = true;
            WeatherIndex = 0;
            AutoTime = false;
            TimeSpeed = 30.0f;
            CloudSpeed = 1.0f;
            SkyExposure = 1.0f;
            SelectedPropLightsOnly = false;
            FocusSelectedLight = false;
            DayNightAmbient = true;
            AnimateFlashiness = true;
            ImportPropLights = true;
            ImportAllProps = true;
            ImportVegetation = true;
            Exposure = 1.0f;
            VolumeFeather = 0.6f;
            propFilter = "";
        }

        private LoadedFile renameTarget;
        private string renameText = "";
        private bool openRename;
        private bool openNewProxyName;
        private string newProxyName = "light_proxy_01";

        public void AskNewLightProxyName(string suggested)
        {
            newProxyName = suggested;
            openNewProxyName = true;
        }

        public event Action<string> ConfirmNewLightProxy;
        public event Action<LoadedFile, string> ConfirmRenameProp;

        private void DrawLoadingOverlay()
        {
            if (!GameLoading || ShowGtaSetup) return;

            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
            ImGui.SetNextWindowSize(io.DisplaySize, ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.82f);
            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav |
                        ImGuiWindowFlags.NoBringToFrontOnFocus;
            if (ImGui.Begin("##loadingoverlay", flags))
            {
                io.WantCaptureMouse = true;
                io.WantCaptureKeyboard = true;

                float boxW = 520, boxH = 150;
                var p = new Vector2((io.DisplaySize.X - boxW) * 0.5f, (io.DisplaySize.Y - boxH) * 0.5f);
                ImGui.SetCursorPos(p);
                ImGui.BeginChild("##loadbox", new Vector2(boxW, boxH), ImGuiChildFlags.Borders);

                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.AccentBright);
                ImGui.TextWrapped("Loading GTA V assets...");
                ImGui.PopStyleColor();
                ImGui.Spacing();
                ImGui.TextWrapped("The editor is locked until the game archives are open.");
                ImGui.Spacing();

                float t = (float)(ImGui.GetTime() % 1.5) / 1.5f;
                var dl = ImGui.GetWindowDrawList();
                var bp = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X - 8, h = 12.0f;
                dl.AddRectFilled(bp, new Vector2(bp.X + w, bp.Y + h), ImGui.GetColorU32(ImGuiCol.FrameBg), 3.0f);
                float bw = w * 0.28f;
                float bx = bp.X + (w + bw) * t - bw;
                dl.AddRectFilled(new Vector2(Math.Max(bx, bp.X), bp.Y),
                    new Vector2(Math.Min(bx + bw, bp.X + w), bp.Y + h),
                    ImGui.GetColorU32(ImGuiCol.ButtonActive), 3.0f);
                ImGui.Dummy(new Vector2(w, h + 6));

                ImGui.TextDisabled(string.IsNullOrEmpty(GameLoadStatus)
                    ? "Opening RPF archives..." : GameLoadStatus);
                ImGui.EndChild();
            }
            ImGui.End();
        }

        public bool ShowGtaSetup;
        private bool gtaSetupOpened;

        private void DrawGtaSetupDialog()
        {
            if (ShowGtaSetup && !gtaSetupOpened)
            {
                ImGui.OpenPopup("Select your GTA V folder");
                gtaSetupOpened = true;
            }
            if (!ShowGtaSetup) { gtaSetupOpened = false; return; }

            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(620, 0), ImGuiCond.Always);
            if (!ImGui.BeginPopupModal("Select your GTA V folder",
                    ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize))
                return;

            ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.AccentBright);
            ImGui.TextWrapped("RAGE Tools needs your GTA V installation.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.TextWrapped(
                "Point it at the folder containing GTA5.exe. It reads stock props, textures and " +
                "timecycles straight out of your own copy of the game - nothing is bundled with " +
                "this tool, and no game files are ever written back.");
            ImGui.Spacing();
            ImGui.TextDisabled("Without it: interiors import with missing props, materials show " +
                               "untextured, and the weather/timecycle list stays empty.");
            ImGui.Spacing();
            ImGui.Separator();

            if (!string.IsNullOrEmpty(GtaFolderDisplay))
            {
                ImGui.TextDisabled("Found: " + GtaFolderDisplay);
                ImGui.Spacing();
            }

            if (GameLoading)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, AccentText());
                ImGui.TextWrapped("Loading game archives...");
                ImGui.PopStyleColor();
                ImGui.Spacing();

                float t = (float)(ImGui.GetTime() % 1.5) / 1.5f;
                var dl = ImGui.GetWindowDrawList();
                var p = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X, h = 12.0f;
                dl.AddRectFilled(p, new Vector2(p.X + w, p.Y + h), ImGui.GetColorU32(ImGuiCol.FrameBg), 3.0f);
                float bw = w * 0.28f;
                float bx = p.X + (w + bw) * t - bw;
                dl.AddRectFilled(new Vector2(Math.Max(bx, p.X), p.Y),
                    new Vector2(Math.Min(bx + bw, p.X + w), p.Y + h),
                    ImGui.GetColorU32(ImGuiCol.ButtonActive), 3.0f);
                ImGui.Dummy(new Vector2(w, h + 6));

                ImGui.TextDisabled(string.IsNullOrEmpty(GameLoadStatus)
                    ? "Opening RPF archives..." : GameLoadStatus);
                ImGui.Spacing();
                ImGui.TextDisabled("This runs once per launch and takes a few seconds.");
                ImGui.EndPopup();
                return;
            }

            if (ImGui.Button("Select GTA V folder...", new Vector2(260, 36)))
            {
                RequestPickGtaFolder?.Invoke();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("required - close the window to quit");

            ImGui.EndPopup();
        }

        public string GtaFolderDisplay = "";
        public bool GameLoading;
        public string GameLoadStatus = "";

        private void DrawNameDialogs()
        {
            var io = ImGui.GetIO();
            var centre = new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f);

            if (openRename) { ImGui.OpenPopup("Rename prop"); openRename = false; }
            ImGui.SetNextWindowPos(centre, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("Rename prop", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextDisabled("Renames the prop and the file it saves to.");
                ImGui.SetNextItemWidth(320);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##rn", ref renameText, 96,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
                ImGui.Spacing();
                if (ImGui.Button("Rename", new Vector2(120, 0)) || enter)
                {
                    if (renameTarget != null && renameText.Trim().Length > 0)
                        ConfirmRenameProp?.Invoke(renameTarget, renameText.Trim());
                    renameTarget = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    renameTarget = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (openNewProxyName) { ImGui.OpenPopup("Name the light prop"); openNewProxyName = false; }
            ImGui.SetNextWindowPos(centre, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("Name the light prop", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextDisabled("This becomes the .ydr name when you save it.");
                ImGui.SetNextItemWidth(320);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##np", ref newProxyName, 96,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
                ImGui.Spacing();
                if (ImGui.Button("Create", new Vector2(120, 0)) || enter)
                {
                    if (newProxyName.Trim().Length > 0) ConfirmNewLightProxy?.Invoke(newProxyName.Trim());
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)) || ImGui.IsKeyPressed(ImGuiKey.Escape))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DrawResetConfirm()
        {
            if (openResetConfirm)
            {
                ImGui.OpenPopup("Reset settings?");
                openResetConfirm = false;
            }
            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("Reset settings?", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped("Put every setting back to its default?\n\n" +
                                  "Theme, shortcuts, panel widths, FOV, rotate snap, walk speed and the\n" +
                                  "view toggles all go back to how the tool shipped.\n\n" +
                                  "Loaded models and lights are left alone.");
                ImGui.Spacing();
                if (ImGui.Button("Reset", new Vector2(120, 0)))
                {
                    RequestResetSettings?.Invoke();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DrawWorldResetConfirm()
        {
            if (openWorldResetConfirm)
            {
                ImGui.OpenPopup("Reset world settings?");
                openWorldResetConfirm = false;
            }
            var io = ImGui.GetIO();
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
                ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("Reset world settings?", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped("Put every world option back to its default?\n\n" +
                                  "Streaming radius, detail and budget, culling, the ymap filters, collision,\n" +
                                  "map lights, grass, HD textures, sky, tone map, fog, clouds and cascades\n" +
                                  "all go back to how the tool shipped.\n\n" +
                                  "Your project, the theme and the Lights workspace are left alone.");
                ImGui.Spacing();
                if (ImGui.Button("Reset", new Vector2(120, 0)))
                {
                    ResetWorldDefaults();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DrawConfirmations()
        {
            DrawResetConfirm();
            DrawWorldResetConfirm();
            DrawNameDialogs();
            if (openDeleteConfirm)
            {
                ImGui.OpenPopup("Delete lights?");
                openDeleteConfirm = false;
            }
            if (openCloneConfirm)
            {
                ImGui.OpenPopup("Keep cloned light?");
                openCloneConfirm = false;
            }

            var centre = new Vector2(ImGui.GetIO().DisplaySize.X * 0.5f, ImGui.GetIO().DisplaySize.Y * 0.5f);
            ImGui.SetNextWindowPos(centre, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("Delete lights?", ImGuiWindowFlags.AlwaysAutoResize))
            {
                int n = scene.SelectedIndices.Count;
                ImGui.Text($"Delete {n} light{(n == 1 ? "" : "s")}?");
                ImGui.TextDisabled("Enter = yes    Backspace = cancel");
                ImGui.Separator();
                bool yes = ImGui.Button("Yes", new Vector2(120, 0)) ||
                           ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter);
                ImGui.SameLine();
                bool no = ImGui.Button("Cancel", new Vector2(120, 0)) ||
                          ImGui.IsKeyPressed(ImGuiKey.Backspace) || ImGui.IsKeyPressed(ImGuiKey.Escape);
                if (yes)
                {
                    scene.DeleteSelected();
                    ImGui.CloseCurrentPopup();
                }
                else if (no)
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            ImGui.SetNextWindowPos(centre, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("Keep cloned light?", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text(CloneWasInstance
                    ? "Keep this linked instance at its new position?"
                    : "Keep the cloned light at this position?");
                if (CloneWasInstance)
                    ImGui.TextDisabled("Instances share every setting except their transform.");
                ImGui.TextDisabled("Enter = yes    Backspace = cancel");
                ImGui.Separator();
                bool yes = ImGui.Button("Yes", new Vector2(120, 0)) ||
                           ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter);
                ImGui.SameLine();
                bool no = ImGui.Button("Cancel", new Vector2(120, 0)) ||
                          ImGui.IsKeyPressed(ImGuiKey.Backspace) || ImGui.IsKeyPressed(ImGuiKey.Escape);
                if (yes)
                {
                    cloneYes?.Invoke();
                    cloneYes = cloneNo = null;
                    ImGui.CloseCurrentPopup();
                }
                else if (no)
                {
                    cloneNo?.Invoke();
                    cloneYes = cloneNo = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private void DrawModeButton(string label, GizmoMode mode)
        {
            bool on = gizmo.Mode == mode;
            if (on)
            {
                var lit = UiTheme.ButtonOn;
                ImGui.PushStyleColor(ImGuiCol.Button, lit);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, lit);
            }
            if (ImGui.Button(label)) gizmo.Mode = mode;
            if (on) ImGui.PopStyleColor(2);
        }

        private void DrawRightPanel(float displayWidth, float displayHeight)
        {
            float rShellTop = TopBarHeight + (WorldMode ? ToolbarHNow_U5 : 0.0f);
            displayHeight -= rShellTop + (WorldMode ? StatusH : 0.0f);
            bool rightOpen = true;
            var wflags = PanelWindow_V30(displayWidth - settings.RightPanelWidth, rShellTop,
                                         settings.RightPanelWidth, displayHeight,
                                         260 * UiScale_V17.Scale, 800 * UiScale_V17.Scale,
                                         ForcePanelSize_V17 > 0, ref rightOpen);
            ImGui.Begin(RightPanelTitle_V30 + "###rightpanel", wflags);

            float w = ImGui.GetWindowSize().X;
            if (!DockedLayout && Math.Abs(w - settings.RightPanelWidth) > 0.5f) settings.RightPanelWidth = w;

            { bool h4 = false; WorkspaceRight_N4(ref h4); if (h4) { DrawPanelFooter(); ImGui.End(); return; } }
            { bool hp = false; WorkspaceRight_P4(ref hp); if (hp) { DrawPanelFooter(); ImGui.End(); return; } }
            { bool hr = false; WorkspaceRight_R4(ref hr); if (hr) { DrawPanelFooter(); ImGui.End(); return; } }
            { bool hu = false; WorkspaceRight_U6(ref hu); if (hu) { DrawPanelFooter(); ImGui.End(); return; } }
            { bool hx = false; WorkspaceRight_V68(ref hx); if (hx) { DrawPanelFooter(); ImGui.End(); return; } }

            if (MaterialMode)
            {
                if (Header("Lights")) DrawMaterialModeLights();
                if (Header("Material", true)) Materials.DrawEditor();
            }

            if (WorldMode)
            {
                if (ImGui.BeginTabBar("##inspectortabs"))
                {
                    if (ImGui.BeginTabItem("Inspector"))
                    {
                        ImGui.BeginChild("##insp", new Vector2(0, 0));
                        DrawInspectorTab();
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                    DrawProjectTab_U22();
                    if (BeginRightTab_J2("Assets"))
                    {
                        ImGui.BeginChild("##inspassets", new Vector2(0, 0));
                        DrawAssetsTab();
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                    DrawAreaTabItem_J5();
                    if (BeginRightTab_J2("Options"))
                    {
                        ImGui.BeginChild("##inspview", new Vector2(0, 0));
                        DrawWorldOptions();
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.End();
                return;
            }
            if (ArchiveMode)
            {
                DrawArchiveRight();
                DrawPanelFooter();
                ImGui.End();
                return;
            }
            if (MloMode)
            {
                DrawMloWorkspaceRight();
                DrawPanelFooter();
                ImGui.End();
                return;
            }
            if (CineMode)
            {
                DrawCinematicSection();
                if (Header("View")) DrawViewSection();
                if (Header("Timecycle")) DrawTimecycleSection();
                DrawPanelFooter();
                ImGui.End();
                return;
            }

            if (Header("View", true)) DrawViewSection();
            DrawMloCreatorSection_H5();
            if (Header("Import from GTA V")) DrawGameSection();
            if (Header("Timecycle")) DrawTimecycleSection();

            if (!MaterialMode)
            {
                var sel = scene.SelectedLight;
                string title =
                    scene.SelectedIndices.Count > 1
                        ? $"Light parameters - {scene.SelectedIndices.Count} lights selected"
                        : sel != null
                            ? $"Light parameters - {sel.Type} #{scene.SelectedIndex}"
                            : "Light parameters";
                if (Header(title + "###lightparams", true))
                {
                    if (sel != null) DrawLightEditor(sel);
                    else if (scene.HasModel)
                    {
                        ImGui.TextDisabled(scene.Lights.Count == 0
                            ? "No lights in this file. Use Add in the left panel."
                            : "Select a light to edit (left list or click in viewport).");
                    }
                    else ImGui.TextDisabled("Open or import something to light.");
                }
            }

            DrawPanelFooter();

            ImGui.End();
        }

        private void DrawPanelFooter()
        {
            ImGui.Spacing();
            ImGui.Separator();
            if (PhotoModeAllowed)
            {
                if (ImGui.Button("Photo mode", new Vector2(-1, 0))) RequestPhotoMode = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(
                        "Everything but the picture goes away: both panels, the grid, the gizmos,\n" +
                        "the light markers and the selection tint, and the window goes borderless\n" +
                        "fullscreen. The camera stays yours, so an interior can be framed and\n" +
                        "captured at full quality. Coronas and volume shafts stay - they are the\n" +
                        "light's own glow, which the game draws too.\n\n" +
                        "P again brings it all back exactly as it was.");
            }

            {
                float need = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y * 3.0f;
                float spare = ImGui.GetContentRegionAvail().Y - need;
                if (spare > 0) ImGui.Dummy(new Vector2(0, spare));
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.DangerButton);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UiTheme.DangerButtonHi);
            if (ImGui.Button("Reset all", new Vector2(-1, 0))) openResetConfirm = true;
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Puts every setting back to how it shipped: theme, shortcuts, panel\n" +
                                 "widths, FOV, snaps, walk speed and the view toggles.\n" +
                                 "Your loaded models and lights are not touched.");
        }

        public bool WorldReady;
        public int WorldNodes, WorldYmapsOpen, WorldYmapsWanted;
        public int WorldEntities, WorldArchetypes, WorldMeshes;
        public int WorldResident, WorldPending, WorldEvicted;
        public int WorldMaxEntities = 40000;

        public WorldGizmo WorldGizmo;
        public EditHistory WorldHistory;
        public bool WorldCanUndo, WorldCanRedo;
        public string WorldUndoName, WorldRedoName;
        public bool RequestWorldUndo, RequestWorldRedo;
        public bool RequestWorldCopy, RequestWorldPaste, RequestWorldDuplicate, RequestWorldDelete;
        public string WorldClipboardSummary;

        public ProjectWindow ProjectWindow;

        public static readonly string[] SelectionModeNames =
        {
            "Entity", "Entity Precision", "Entity Extension", "Archetype Extension", "Time Cycle Modifier",
            "Car Generator", "Grass", "Water Quad", "Water Calming Quad", "Water Wave Quad", "Collision",
            "Nav Mesh", "Path", "Train Track", "Lod Lights", "Mlo Instance", "Scenario", "Audio", "Occlusion",
            "Light",
        };
        public static readonly bool[] SelectionModeAvailable =
        {
            true, true, false, false, true,
            true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true,
        };
        public int SelectionMode;
        private bool lastCollisionMode_U22, collisionShownBefore_U22;
        public bool MouseSelectEnabled = true;
        public string SelectionModeName => SelectionModeNames[Math.Clamp(SelectionMode, 0, SelectionModeNames.Length - 1)];

        public bool FocusPropsTab;
        public bool ShowPropsPanel;
        private void DrawPropsPanelWindow(float displayW, float displayH)
        {
            if (FocusPropsTab) { ShowPropsPanel = true; FocusPropsTab = false; }
            if (!ShowPropsPanel) return;
            ImGui.SetNextWindowSize(new Vector2(420, 560), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(displayW - 460, 90), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Props Panel###PropsPanel", ref ShowPropsPanel))
            {
                ImGui.BeginChild("##propspanelbody", new Vector2(0, 0));
                DrawLibrarySection();
                ImGui.EndChild();
            }
            ImGui.End();
        }

        internal static Vector4 ColUnsaved => UiTheme.Warn;
        internal static Vector4 ColDanger => UiTheme.DangerButton;
        internal static Vector4 ColDangerHi => UiTheme.DangerButtonHi;

        internal static bool AccentButton_V31(string label, Vector2 size)
        {
            var a = UiTheme.AccentBright;
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(a.X * 0.55f, a.Y * 0.55f, a.Z * 0.55f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(a.X * 0.8f, a.Y * 0.8f, a.Z * 0.8f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, a);
            bool r = ImGui.Button(label, size);
            ImGui.PopStyleColor(3);
            return r;
        }

        internal static bool ColourButton_V33(string label, Vector3 rgb, Vector2 size)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(rgb.X * 0.62f, rgb.Y * 0.62f, rgb.Z * 0.62f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(rgb.X * 0.85f, rgb.Y * 0.85f, rgb.Z * 0.85f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(rgb.X, rgb.Y, rgb.Z, 1f));
            bool r = ImGui.Button(label, size);
            ImGui.PopStyleColor(3);
            return r;
        }

        internal static readonly Vector3 ColRoom_V33 = new Vector3(1.00f, 0.66f, 0.22f);
        internal static readonly Vector3 ColPortal_V33 = new Vector3(1.00f, 0.66f, 0.22f);
        internal static readonly Vector3 ColSnap_V33 = new Vector3(0.42f, 0.88f, 0.50f);

        internal static bool DangerButton(string label, Vector2 size)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ColDanger);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColDangerHi);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColDangerHi);
            bool r = ImGui.Button(label, size);
            ImGui.PopStyleColor(3);
            return r;
        }

        public WorldStreamer WorldRef;
        public System.Numerics.Vector3 WorldCameraPos;
        public CodeWalker.GameFiles.YmapEntityDef RequestWorldSelectEntity;
        public bool RequestWorldFrameSelected;
        private string explorerFilter = "";
        private const float ToolbarH = 34.0f, StatusHBase_U27 = 24.0f;
        private static float StatusH => ShellChrome_U27 ? 0.0f : StatusHBase_U27;

        private bool worldToolbarWraps_U5;

        private float ToolbarHNow_U5 => ShellChrome_U27 && !NavMode ? 0.0f : ToolbarH +
            (worldToolbarWraps_U5 && !NavMode
                ? ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y
                : 0.0f);

        public AssetPreview ArchivePreview;
        public string RequestPreviewDrawable;
        public bool RequestPreviewClose;

        public bool WorldShowCollision;
        public bool WorldAnimations = true;
        public float WorldCollisionRange = 500.0f;
        public float WorldCollisionOpacity = 1.0f;
        public int WorldCollisionTris;

        public CodeWalker.GameFiles.YmapEntityDef WorldSel;
        public int WorldDirtyCount;
        public int WorldSelCount_V20 = 1;
        public string WorldEditStatus = "", WorldOutputFolder;
        public bool RequestWorldDeselect, RequestWorldEntityGoto;
        public bool RequestWorldSaveAll, RequestWorldDiscard, RequestWorldChooseOutput;

        public class EntityEdit
        {
            public Vector3 Position;
            public Vector3 RotationDeg;
            public Vector3 Scale;
            public float LodDist;
        }
        public EntityEdit WorldEditApply;

        public MapProject Project = new MapProject();
        public bool RequestProjectNew, RequestProjectOpen, RequestProjectSave;
        public bool RequestProjectExportXml, RequestProjectImportXml;
        public bool RequestAddSelectedYmap, RequestAddYtyp, RequestSaveYtyp, RequestManifest;
        public MapProject.Entry ProjectSel, ProjectRemove;
        public CodeWalker.GameFiles.Archetype ArchSel;
        public string ProjectOutputFolder;
        public bool ArchEdited;
        private string archetypeFilter = "";
        private int mloRoomSel = -1, mloPortalSel = -1;
        private string mloValidation;

        private CodeWalker.GameFiles.YmapEntityDef worldFieldsFor;
        private System.Numerics.Vector3 wePos, weRot, weScale;
        private float weLod;
        public bool WorldTruncated;
        public float WorldStreamRadius = 500.0f;
        public float WorldLodScale = 1.0f;
        public bool WorldFrustumCull = true;
        public bool WorldLightsEnabled = true;
        public bool WorldLodLightsEnabled = true;
        public float WorldLightsRange = 3000.0f;
        public int WorldLightsInView, WorldLightsEmitted;
        public Vector3? RequestWorldGoto;
        public bool RequestWorldReload;

        private static readonly (string Name, Vector3 Pos)[] WorldPlaces =
        {
            ("Maze Bank Tower (top)", new Vector3(-75, -818, 330)),
            ("Downtown Los Santos", new Vector3(-270, -960, 60)),
            ("Vinewood Hills",      new Vector3(-680, 600, 190)),
            ("Los Santos Airport",  new Vector3(-1050, -2900, 30)),
            ("Sandy Shores",        new Vector3(1900, 3700, 40)),
            ("Paleto Bay",          new Vector3(-260, 6350, 40)),
            ("Mount Chiliad",       new Vector3(450, 5600, 800)),
            ("Del Perro Pier",      new Vector3(-1850, -1250, 20)),
            ("Above the whole map", new Vector3(0, 0, 4000)),
        };

        private void DrawWorldLeft(float displayHeight)
        {
            if (ImGui.BeginTabBar("##worldtabs"))
            {
                if (ImGui.BeginTabItem("Map"))
                {
                    ImGui.BeginChild("##maptab", new Vector2(0, 0), ImGuiChildFlags.None);
                    DrawWorldMapTab(displayHeight);
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Project"))
                {
                    ImGui.BeginChild("##projtab", new Vector2(0, 0), ImGuiChildFlags.None);
                    DrawProjectTab();
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Archetypes"))
                {
                    ImGui.BeginChild("##ytyptab", new Vector2(0, 0), ImGuiChildFlags.None);
                    DrawYtypTab();
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Manifest"))
                {
                    ImGui.BeginChild("##mftab", new Vector2(0, 0), ImGuiChildFlags.None);
                    DrawManifestTab();
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }

        private void DrawWorldMapTab(float displayHeight) => DrawWorldMapTab_V58(displayHeight);

        private void DrawWorldTools()
        {
            if (WorldGizmo == null) return;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("TOOLS");

            void ModeButton(string label, WorldGizmoMode m, string tip)
            {
                bool on = WorldGizmo.Mode == m;
                if (on) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
                if (ImGui.SmallButton(label)) WorldGizmo.Mode = m;
                if (on) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
                ImGui.SameLine();
            }
            ModeButton("Sel##wgm", WorldGizmoMode.Select, "Select only  (Q)");
            ModeButton("Move##wgm", WorldGizmoMode.Translate, "Move  (W)");
            ModeButton("Rot##wgm", WorldGizmoMode.Rotate, "Rotate  (E)");
            ModeButton("Scale##wgm", WorldGizmoMode.Scale, "Scale  (T)\n\nThe ymap format stores one\n" +
                       "number for X and Y together, plus Z - so the horizontal axes scale as a pair.");

            bool local = WorldGizmo.Space == WorldGizmoSpace.Local;
            if (ImGui.SmallButton(local ? "Local##wgs" : "World##wgs"))
                WorldGizmo.Space = local ? WorldGizmoSpace.World : WorldGizmoSpace.Local;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Which way the handles point  (G)\n\nWorld: the map's axes.\n" +
                                 "Local: the entity's own, which is what you want for nudging\n" +
                                 "something along a wall that is not axis-aligned.");

            ImGui.SetNextItemWidth(-96);
            float tsnap = WorldGizmo.TranslateSnap;
            if (UiSlider_U28.Float("Move snap##wg", ref tsnap, 0.0f, 5.0f, tsnap <= 0.001f ? "off" : "%.2f m"))
                WorldGizmo.TranslateSnap = tsnap;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Round each drag to this grid. Hold Shift while dragging to\n" +
                                 "suppress snapping for that one drag.");

            bool cu = WorldCanUndo, cr = WorldCanRedo;
            if (!cu) ImGui.BeginDisabled();
            if (ImGui.Button((cu ? "Undo " + (WorldUndoName ?? "") : "Undo") + "##wundo", new Vector2(-1, 0)))
                RequestWorldUndo = true;
            if (!cu) ImGui.EndDisabled();
            if (!cr) ImGui.BeginDisabled();
            if (ImGui.Button((cr ? "Redo " + (WorldRedoName ?? "") : "Redo") + "##wredo", new Vector2(-1, 0)))
                RequestWorldRedo = true;
            if (!cr) ImGui.EndDisabled();
        }

        private void DrawWorldSelection()
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("SELECTION");

            bool selHandled = false;
            DrawWorldSelectionExtras_SpaceData(ref selHandled);
            if (!selHandled) DrawWorldSelectionExtras_Selection(ref selHandled);
            if (selHandled) return;

            if (WorldSel == null)
            {
                bool hinted = false;
                DrawWorldNoSelectionHint_J2(ref hinted);
                if (!hinted) ImGui.TextWrapped("Right-click anything in the world to select it.");
                DrawCollisionUnderCursor_Selection();
                if (WorldDirtyCount > 0) DrawWorldSaveRow();
                return;
            }

            if (!ReferenceEquals(worldFieldsFor, WorldSel))
            {
                worldFieldsFor = WorldSel;
                var p = WorldSel.Position;
                var r = WorldEditor.ToEulerDegrees(WorldSel.Orientation);
                var sc = WorldSel.Scale;
                wePos = new System.Numerics.Vector3(p.X, p.Y, p.Z);
                weRot = new System.Numerics.Vector3(r.X, r.Y, r.Z);
                weScale = new System.Numerics.Vector3(sc.X, sc.Y, sc.Z);
                weLod = WorldSel.LodDist;
            }

            DrawWorldSelHeader_V19(WorldSel);

            bool changed = false;
            ImGui.SetNextItemWidth(-80);
            changed |= ImGui.DragFloat3("Position##wepos", ref wePos, 0.05f);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Position  (X Y Z, world metres)");
            ImGui.SetNextItemWidth(-80);
            changed |= ImGui.DragFloat3("Rotation##werot", ref weRot, 0.5f);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Rotation  (roll pitch yaw, degrees)");
            ImGui.SetNextItemWidth(-80);
            changed |= ImGui.DragFloat3("Scale##wescale", ref weScale, 0.01f);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Scale  (X Y Z)");
            ImGui.SetNextItemWidth(-80);
            changed |= ImGui.DragFloat("Draw dist", ref weLod, 1.0f, 0.0f, 20000.0f, "%.0f m");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How far away this placement is still drawn. Zero means take the\n" +
                                 "archetype's own distance.");

            if (changed)
            {
                WorldEditApply = new EntityEdit
                {
                    Position = wePos,
                    RotationDeg = weRot,
                    Scale = weScale,
                    LodDist = weLod,
                };
            }

            DrawEntityClipboardRow_M3();
            DrawAddToProjectButton("ent");
            DrawEntityLightsList_WorldLight(WorldSel);
            DrawWorldEntityAddLight_U18(WorldSel);

            DrawCollisionUnderCursor_Selection();
            DrawWorldSaveRow();
        }

        private void DrawWorldSaveRow()
        {
            ImGui.Spacing();
            if (WorldDirtyCount > 0)
                ImGui.TextColored(UiTheme.Warn,
                    $"{WorldDirtyCount} ymap{(WorldDirtyCount == 1 ? "" : "s")} edited");

            ImGui.TextDisabled(string.IsNullOrEmpty(WorldOutputFolder)
                ? "No folder"
                : System.IO.Path.GetFileName(WorldOutputFolder.TrimEnd('\\')) + "\\");
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(WorldOutputFolder))
                ImGui.SetTooltip(WorldOutputFolder);
            if (ImGui.Button("Output folder...", new Vector2(-1, 0))) RequestWorldChooseOutput = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Edited .ymap files are written here.\n\n" +
                                 "Never back into the .rpf archives - those are your game install,\n" +
                                 "and a half-written one is a reinstall. A folder is also how a map\n" +
                                 "mod actually ships.");

            if (WorldDirtyCount > 0)
            {
                if (ImGui.Button("Save ymaps", new Vector2(-1, 0))) RequestWorldSaveAll = true;
                if (ImGui.Button("Discard edits", new Vector2(-1, 0))) RequestWorldDiscard = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Throws the changes away. The world reloads them from the\n" +
                                     "archives as you fly back through.");
            }
            if (!string.IsNullOrEmpty(WorldEditStatus)) ImGui.TextWrapped(WorldEditStatus);
        }

        private void DrawProjectTab()
        {
            ImGui.TextDisabled("PROJECT");
            ImGui.TextWrapped(Project.Name + (Project.Dirty ? " *" : ""));
            if (ImGui.Button("New", new Vector2(-1, 0))) RequestProjectNew = true;
            if (ImGui.Button("Open...", new Vector2(-1, 0))) RequestProjectOpen = true;
            if (ImGui.Button("Save as...", new Vector2(-1, 0))) RequestProjectSave = true;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("YMAPS");
            if (ImGui.Button("Add the selected entity's ymap", new Vector2(-1, 0)))
                RequestAddSelectedYmap = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Right-click something in the world first. Its ymap is the file\n" +
                                 "that placement lives in, and the one you would ship.");
            DrawEntryList(Project.Ymaps, "##ymaplist");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("YTYPS");
            if (ImGui.Button("Add a .ytyp...", new Vector2(-1, 0))) RequestAddYtyp = true;
            DrawEntryList(Project.Ytyps, "##ytyplist");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("XML");
            bool haveSel = ProjectSel != null;
            if (!haveSel) ImGui.BeginDisabled();
            if (ImGui.Button("Export selected as XML...", new Vector2(-1, 0))) RequestProjectExportXml = true;
            if (!haveSel) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The game's XML interchange format (the one OpenIV reads and writes) - the file as text you\n" +
                                 "can diff, hand-edit, or feed to any other tool. Embedded textures\n" +
                                 "are written as .dds beside it.");
            if (ImGui.Button("Import XML...", new Vector2(-1, 0))) RequestProjectImportXml = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A .ymap.xml or .ytyp.xml joins the project as a live file. Any\n" +
                                 "other supported type is converted back to its game format on\n" +
                                 "disk, next to the XML.");

            if (!string.IsNullOrEmpty(Project.LastStatus))
            {
                ImGui.Spacing();
                ImGui.TextWrapped(Project.LastStatus);
            }
        }

        private void DrawEntryList(System.Collections.Generic.List<MapProject.Entry> list, string id)
        {
            if (list.Count == 0) { ImGui.TextDisabled("  (none)"); return; }
            ImGui.BeginChild(id, new Vector2(0, Math.Min(list.Count * 22 + 8, 150)), ImGuiChildFlags.Borders);
            foreach (var e in list)
            {
                bool sel = ReferenceEquals(e, ProjectSel);
                if (ImGui.Selectable(e.DisplayName + (e.Dirty ? " *" : "") + "##" + e.GetHashCode(), sel))
                    ProjectSel = e;
                if (ImGui.BeginPopupContextItem("##ctx" + e.GetHashCode()))
                {
                    if (ImGui.MenuItem("Remove from project")) ProjectRemove = e;
                    ImGui.EndPopup();
                }
                if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(e.Path)) ImGui.SetTooltip(e.Path);
            }
            ImGui.EndChild();
        }

        private void DrawYtypTab()
        {
            var ytyps = Project.Ytyps.Where(e => e.Ytyp?.AllArchetypes != null).ToList();
            if (ytyps.Count == 0)
            {
                ImGui.TextWrapped("No .ytyp in the project yet. Add one on the Project tab.");
                ImGui.Spacing();
                ImGui.TextDisabled("A ytyp holds ARCHETYPES: the definition of a prop, as opposed to " +
                                   "a placement of one. Changing a draw distance here changes it " +
                                   "everywhere that prop appears, rather than in one ymap.");
                return;
            }

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##archfilter", "filter by name", ref archetypeFilter, 128);

            ImGui.BeginChild("##archlist", new Vector2(0, 220), ImGuiChildFlags.Borders);
            int shown = 0;
            foreach (var e in ytyps)
            {
                foreach (var a in e.Ytyp.AllArchetypes)
                {
                    if (a == null) continue;
                    var nm = a.Name ?? "";
                    if (archetypeFilter.Length > 0 &&
                        nm.IndexOf(archetypeFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (shown >= 400) continue;
                    shown++;
                    if (ImGui.Selectable(nm + "##a" + a.GetHashCode(), ReferenceEquals(a, ArchSel)))
                    {
                        ArchSel = a;
                        ProjectSel = e;
                    }
                }
            }
            ImGui.EndChild();
            int total = ytyps.Sum(e => e.Ytyp.AllArchetypes.Length);
            ImGui.TextDisabled(shown >= 400 ? $"showing 400 of {total:N0}" : $"{shown:N0} of {total:N0}");

            if (ArchSel == null) { ImGui.TextDisabled("select an archetype"); return; }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextWrapped(ArchSel.Name ?? "(unnamed)");
            ImGui.TextDisabled(MapProject.DescribeArchetype(ArchSel));

            float lod = MapProject.GetLodDist(ArchSel);
            ImGui.SetNextItemWidth(-90);
            if (ImGui.DragFloat("Draw dist##arch", ref lod, 1.0f, 0.0f, 20000.0f, "%.0f m"))
            { MapProject.SetLodDist(ArchSel, lod); ArchEdited = true; }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How far away this prop is drawn, everywhere it appears. A ymap\n" +
                                 "placement can override it; this is the default it falls back to.");

            float hd = MapProject.GetHdTextureDist(ArchSel);
            ImGui.SetNextItemWidth(-90);
            if (ImGui.DragFloat("HD tex dist##arch", ref hd, 1.0f, 0.0f, 20000.0f, "%.0f m"))
            { MapProject.SetHdTextureDist(ArchSel, hd); ArchEdited = true; }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Where the high-resolution textures stop being used. Below the\n" +
                                 "draw distance, or the prop keeps its big textures out to the\n" +
                                 "horizon for nothing.");

            int flags = unchecked((int)MapProject.GetFlags(ArchSel));
            ImGui.SetNextItemWidth(-90);
            if (ImGui.InputInt("Flags##arch", ref flags, 0, 0))
            { MapProject.SetFlags(ArchSel, unchecked((uint)flags)); ArchEdited = true; }

            if (ArchSel is CodeWalker.GameFiles.MloArchetype mlo) DrawMloEditor(mlo);

            DrawArchetypeExtensions_V62(ArchSel);

            ImGui.Spacing();
            if (ImGui.Button("Save this .ytyp", new Vector2(-1, 0))) RequestSaveYtyp = true;
        }

        private void DrawMloEditor(CodeWalker.GameFiles.MloArchetype mlo)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("INTERIOR");
            ImGui.TextDisabled(MapProject.DescribeMlo(mlo));

            int rooms = MloEditor.RoomCount(mlo);
            if (rooms > 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("ROOMS");
                ImGui.BeginChild("##mlorooms", new Vector2(0, Math.Min(rooms * 21 + 8, 130)),
                                 ImGuiChildFlags.Borders);
                for (int i = 0; i < rooms; i++)
                {
                    var r = MloEditor.GetRoom(mlo, i);
                    if (r == null) continue;
                    if (ImGui.Selectable($"{i}: {MloEditor.GetRoomName(r)}##mr{i}", mloRoomSel == i))
                        mloRoomSel = i;
                }
                ImGui.EndChild();

                var room = MloEditor.GetRoom(mlo, mloRoomSel);
                if (room != null)
                {
                    var name = MloEditor.GetRoomName(room);
                    ImGui.SetNextItemWidth(-70);
                    if (ImGui.InputText("Name##mr", ref name, 64))
                    { MloEditor.SetRoomName(mlo, room, name); ArchEdited = true; }

                    var tc = MloEditor.GetRoomTimecycle(room);
                    ImGui.SetNextItemWidth(-70);
                    if (ImGui.InputText("Cycle##mr", ref tc, 64))
                    { MloEditor.SetRoomTimecycle(mlo, room, tc); ArchEdited = true; }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("The interior timecycle modifier this room uses - the same\n" +
                                         "name the Timecycle section's Interior mod list shows.");

                    int flags = unchecked((int)MloEditor.GetRoomFlags(room));
                    ImGui.SetNextItemWidth(-70);
                    if (ImGui.InputInt("Flags##mr", ref flags, 0, 0))
                    { MloEditor.SetRoomFlags(mlo, room, unchecked((uint)flags)); ArchEdited = true; }

                    ImGui.TextDisabled($"{MloEditor.GetRoomPortalCount(room)} portal(s), " +
                                       $"{MloEditor.GetRoomAttachedObjects(room).Length} attached object(s)");
                }
            }

            int portals = MloEditor.PortalCount(mlo);
            if (portals > 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("PORTALS");
                ImGui.BeginChild("##mloportals", new Vector2(0, Math.Min(portals * 21 + 8, 130)),
                                 ImGuiChildFlags.Borders);
                for (int i = 0; i < portals; i++)
                {
                    var pd = MloEditor.GetPortal(mlo, i);
                    if (pd == null) continue;
                    if (ImGui.Selectable($"{i}: {MloEditor.DescribePortal(mlo, pd)}##mp{i}", mloPortalSel == i))
                        mloPortalSel = i;
                }
                ImGui.EndChild();

                var portal = MloEditor.GetPortal(mlo, mloPortalSel);
                if (portal != null)
                {
                    int from = (int)MloEditor.GetPortalRoomFrom(portal);
                    int to = (int)MloEditor.GetPortalRoomTo(portal);
                    ImGui.SetNextItemWidth(90);
                    if (ImGui.InputInt("##mpfrom", ref from, 0, 0))
                    { if (MloEditor.SetPortalRoomFrom(mlo, portal, (uint)Math.Max(from, 0))) ArchEdited = true; }
                    ImGui.SameLine(); ImGui.Text("to"); ImGui.SameLine();
                    ImGui.SetNextItemWidth(90);
                    if (ImGui.InputInt("##mpto", ref to, 0, 0))
                    { if (MloEditor.SetPortalRoomTo(mlo, portal, (uint)Math.Max(to, 0))) ArchEdited = true; }
                    ImGui.SameLine(); ImGui.TextDisabled("rooms");

                    int opacity = (int)MloEditor.GetPortalOpacity(portal);
                    ImGui.SetNextItemWidth(-70);
                    if (UiSlider_U28.Int("Opacity##mp", ref opacity, 0, 100))
                    { MloEditor.SetPortalOpacity(mlo, portal, (uint)opacity); ArchEdited = true; }

                    int mirror = (int)MloEditor.GetPortalMirrorPriority(portal);
                    ImGui.SetNextItemWidth(-70);
                    if (ImGui.InputInt("Mirror##mp", ref mirror, 0, 0))
                    { MloEditor.SetPortalMirrorPriority(mlo, portal, (uint)Math.Max(mirror, 0)); ArchEdited = true; }

                    int pflags = unchecked((int)MloEditor.GetPortalFlags(portal));
                    ImGui.SetNextItemWidth(-70);
                    if (ImGui.InputInt("Flags##mp", ref pflags, 0, 0))
                    { MloEditor.SetPortalFlags(mlo, portal, unchecked((uint)pflags)); ArchEdited = true; }

                    ImGui.TextDisabled($"{MloEditor.GetPortalCornerCount(portal)} corner(s)");
                }
            }

            int sets = MloEditor.EntitySetCount(mlo);
            if (sets > 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled($"{sets} ENTITY SET(S)");
                for (int i = 0; i < sets && i < 12; i++)
                {
                    var es = MloEditor.GetEntitySet(mlo, i);
                    if (es != null) ImGui.TextDisabled("  " + MloEditor.DescribeEntitySet(es));
                }
            }

            ImGui.Spacing();
            if (ImGui.Button("Validate interior", new Vector2(-1, 0)))
                mloValidation = MloEditor.Validate(mlo) is { Length: > 0 } v ? v : "no problems found";
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("YtypFile.Save swallows its own errors - a broken interior can\n" +
                                 "'save successfully' as a silently truncated file. This is the\n" +
                                 "check that catches it first.");
            if (!string.IsNullOrEmpty(mloValidation)) ImGui.TextWrapped(mloValidation);
        }

        private void DrawManifestTab()
        {
            ImGui.TextDisabled("MANIFEST");
            ImGui.TextWrapped("_manifest.ymf tells the game which ytyps a ymap needs, so the " +
                              "archetypes exist before it tries to place them. Without it a map mod " +
                              "loads, draws nothing, and says nothing about why.");
            ImGui.Spacing();
            ImGui.Text($"{Project.Ymaps.Count} ymap(s), {Project.Ytyps.Count} ytyp(s) in the project");

            if (Project.Ymaps.Count == 0)
                ImGui.TextDisabled("Add at least one ymap on the Project tab.");

            ImGui.Spacing();
            ImGui.TextDisabled(string.IsNullOrEmpty(ProjectOutputFolder)
                ? "No folder" : ProjectOutputFolder);
            if (ImGui.Button("Output folder...", new Vector2(-1, 0))) RequestWorldChooseOutput = true;

            if (Project.Ymaps.Count > 0 && ImGui.Button("Generate manifest", new Vector2(-1, 0)))
                RequestManifest = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Writes a ready-to-use binary _manifest.ymf - put it straight into your RPF.\n" +
                                 "Ymaps that place an interior are marked INTERIOR_DATA, as in the game's own manifests.");

            if (!string.IsNullOrEmpty(Project.LastStatus))
            {
                ImGui.Spacing();
                ImGui.TextWrapped(Project.LastStatus);
            }
        }

        private void DrawWorldToolbar(float displayWidth)
        {
            { bool q2 = false; WorldToolbar_Q2(displayWidth, ref q2); if (q2) return; }
            if (ShellChrome_U27) { ShellToolbarLogic_U27(); return; }
            worldToolbarWraps_U5 = WorldToolbarWidth_U5() > displayWidth - 16.0f;
            ImGui.SetNextWindowPos(new Vector2(0, TopBarHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(displayWidth, ToolbarHNow_U5), ImGuiCond.Always);
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 5));
            ImGui.Begin("##worldtoolbar", flags);

            float btnH = ImGui.GetFrameHeight();
            float pad = ImGui.GetStyle().FramePadding.X * 2.0f + 8.0f;
            float W(string t) => ImGui.CalcTextSize(t).X + pad;

            void Gap() { ImGui.SameLine(0, 4); }
            void Sep()
            {
                ImGui.SameLine(0, 10);
                ImGui.TextDisabled("|");
                ImGui.SameLine(0, 10);
            }
            bool Toggle(string label, bool on, string tip, float w)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, on ? UiTheme.ButtonOn : UiTheme.ButtonOff);
                bool hit = ImGui.Button(label, new Vector2(w, 0));
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
                return hit;
            }

            if (WorldGizmo != null)
            {
                float tw = Math.Max(Math.Max(W("Select"), W("Rotate")), Math.Max(W("Move"), W("Scale")));
                if (Toggle("Select##tbtool", WorldGizmo.Mode == WorldGizmoMode.Select,
                           "Select - click things without moving them\nShortcut: Q", tw))
                    WorldGizmo.Mode = WorldGizmoMode.Select;
                Gap();
                if (Toggle("Move##tbtool", WorldGizmo.Mode == WorldGizmoMode.Translate,
                           "Move - drag the arrows to shift what is selected\nShortcut: W", tw))
                    WorldGizmo.Mode = WorldGizmoMode.Translate;
                Gap();
                if (Toggle("Rotate##tbtool", WorldGizmo.Mode == WorldGizmoMode.Rotate,
                           "Rotate - drag a ring to turn what is selected\nShortcut: E", tw))
                    WorldGizmo.Mode = WorldGizmoMode.Rotate;
                Gap();
                if (Toggle("Scale##tbtool", WorldGizmo.Mode == WorldGizmoMode.Scale,
                           "Scale - drag to resize what is selected\nShortcut: T", tw))
                    WorldGizmo.Mode = WorldGizmoMode.Scale;

                Sep();

                bool local = WorldGizmo.Space == WorldGizmoSpace.Local;
                if (Toggle(local ? "Axes: Local##tbaxes" : "Axes: World##tbaxes", local,
                           "Which way the handles point.\nWorld: along the map's own north, east and up.\n" +
                           "Local: along the prop's own front, side and up.\nShortcut: G",
                           Math.Max(W("Axes: World"), W("Axes: Local"))))
                    WorldGizmo.Space = local ? WorldGizmoSpace.World : WorldGizmoSpace.Local;

                Sep();

                float rsnap = RotateSnapSteps_U5.Clamp(WorldGizmo.RotateSnapDeg);
                string snapTip = "Turning snaps to steps this big.\n" +
                                 "Click - and + to change it a degree at a time,\n" +
                                 "or drag the number to move faster.\n" +
                                 "Hold Shift while dragging a ring to turn freely.";

                ImGui.BeginDisabled(rsnap <= 0.001f);
                if (ImGui.Button("-##tbsnapdown", new Vector2(btnH, 0)))
                    rsnap = RotateSnapSteps_U5.Down(rsnap);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(rsnap <= 0.001f ? "Snapping is already off" : "Smaller steps");
                SnapMinusMin_U5 = ImGui.GetItemRectMin(); SnapMinusMax_U5 = ImGui.GetItemRectMax();
                ImGui.SameLine(0, 3);

                ImGui.SetNextItemWidth(Math.Max(W("Snap: 22.5 deg"), W("Snap: off")));
                if (ImGui.DragFloat("##tbsnapval", ref rsnap, 0.5f, 0.0f, 90.0f,
                                    RotateSnapSteps_U5.Label(rsnap)))
                    rsnap = RotateSnapSteps_U5.Clamp(rsnap);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(snapTip);
                ImGui.SameLine(0, 3);

                ImGui.BeginDisabled(rsnap >= 89.999f);
                if (ImGui.Button("+##tbsnapup", new Vector2(btnH, 0)))
                    rsnap = RotateSnapSteps_U5.Up(rsnap);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(rsnap >= 89.999f ? "A quarter turn is as big as it goes" : "Bigger steps");
                SnapPlusMin_U5 = ImGui.GetItemRectMin(); SnapPlusMax_U5 = ImGui.GetItemRectMax();

                float snapNow = RotateSnapSteps_U5.Clamp(rsnap);
                WorldGizmo.RotateSnapDeg = snapNow;
                if (settings != null && Math.Abs(settings.RotateSnapDeg - snapNow) > 0.0001f)
                    settings.RotateSnapDeg = snapNow;
                SnapLabelDrawn_U5 = RotateSnapSteps_U5.Label(snapNow);
            }

            Sep();

            float hw = Math.Max(W("Undo"), W("Redo"));
            ImGui.BeginDisabled(!WorldCanUndo);
            if (ImGui.Button("Undo##tbhist", new Vector2(hw, 0))) RequestWorldUndo = true;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(WorldCanUndo ? "Undo " + (WorldUndoName ?? "") + "\nShortcut: Ctrl+Z"
                                              : "Nothing to undo yet");
            Gap();
            ImGui.BeginDisabled(!WorldCanRedo);
            if (ImGui.Button("Redo##tbhist", new Vector2(hw, 0))) RequestWorldRedo = true;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(WorldCanRedo ? "Redo " + (WorldRedoName ?? "") + "\nShortcut: Ctrl+Y"
                                              : "Nothing to redo");

            if (!worldToolbarWraps_U5) Sep();

            bool collMode = SelectionModeEnum == WorldSelectionMode.Collision;
            if (collMode != lastCollisionMode_U22)
            {
                if (collMode) { collisionShownBefore_U22 = WorldShowCollision; WorldShowCollision = true; }
                else WorldShowCollision = collisionShownBefore_U22;
                lastCollisionMode_U22 = collMode;
            }

            float cw = W("Picks: Entity");
            for (int i = 0; i < SelectionModeNames.Length; i++)
                cw = Math.Max(cw, Math.Min(W("Picks: " + SelectionModeNames[i]), 230.0f));
            ImGui.SetNextItemWidth(cw + ImGui.GetFrameHeight());
            if (ImGui.BeginCombo("##selmode", "Picks: " + SelectionModeName))
            {
                for (int i = 0; i < SelectionModeNames.Length; i++)
                {
                    if (!SelectionModeAvailable[i]) continue;
                    if (ImGui.Selectable(SelectionModeNames[i], SelectionMode == i)) SelectionMode = i;
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("What a click in the world picks up.\n" +
                                 "Right-click picks; hold Shift or Ctrl to pick several.\nShortcut: M cycles");
            Gap();
            if (Toggle(MouseSelectEnabled ? "Picking on##tbpick" : "Picking off##tbpick", MouseSelectEnabled,
                       "On, clicking in the world changes the selection.\nOff, the selection is left alone.\n" +
                       "Shortcut: C", Math.Max(W("Picking on"), W("Picking off"))))
                MouseSelectEnabled = !MouseSelectEnabled;

            Sep();

            if (WorldSel != null || WorldSelection.HasValue)
            {
                if (ImGui.Button("Frame##tbview", new Vector2(W("Frame"), 0)))
                    RequestFrameWorldSelection_M3();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fly the camera to what is selected\nShortcut: F");
                Sep();
            }

            if (ProjectWindow != null)
            {
                bool pv = ProjectWindow.Visible;
                if (DrawProjectButton_U24(pv))
                    ProjectWindow.Visible = !pv;
                ImGui.SameLine(0, 4);
            }
            DrawEditLightButton_J2();

            ImGui.End();
            ImGui.PopStyleVar();
        }

        private float WorldToolbarWidth_U5()
        {
            float pad = ImGui.GetStyle().FramePadding.X * 2.0f + 8.0f;
            float W(string t) => ImGui.CalcTextSize(t).X + pad;
            float h = ImGui.GetFrameHeight();

            float w = 16.0f;
            if (WorldGizmo != null)
            {
                float tw = Math.Max(Math.Max(W("Select"), W("Rotate")), Math.Max(W("Move"), W("Scale")));
                w += tw * 4.0f + 12.0f;
                w += 20.0f + W("|") + Math.Max(W("Axes: World"), W("Axes: Local"));
                w += 20.0f + W("|") + h * 2.0f + 6.0f + Math.Max(W("Snap: 22.5 deg"), W("Snap: off"));
            }
            w += 20.0f + W("|") + Math.Max(W("Undo"), W("Redo")) * 2.0f + 4.0f;

            float cw = W("Picks: Entity");
            for (int i = 0; i < SelectionModeNames.Length; i++)
                cw = Math.Max(cw, Math.Min(W("Picks: " + SelectionModeNames[i]), 230.0f));
            w += 20.0f + W("|") + cw + h + 4.0f + Math.Max(W("Picking on"), W("Picking off"));

            w += 20.0f + W("|");
            if (WorldSel != null || WorldSelection.HasValue) w += 4.0f + W("Frame") + 20.0f + W("|");
            if (ProjectWindow != null) w += ProjectButtonWidth_U24(ProjectUnsaved_U24()) + 4.0f;
            w += W("Edit Light") + 8.0f;
            w += ImGui.CalcTextSize(StatsText ?? "").X + 12.0f;
            return w;
        }

        private void DrawStatusBar(float displayWidth, float displayHeight)
        {
            ImGui.SetNextWindowPos(new Vector2(0, displayHeight - StatusH), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(displayWidth, StatusH), ImGuiCond.Always);
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 3));
            ImGui.Begin("##worldstatus", flags);

            bool first = true;
            void Dot() { if (!first) { ImGui.SameLine(); ImGui.Text("·"); ImGui.SameLine(); } first = false; }
            if (WorldSel != null)
            {
                Dot();
                ImGui.TextDisabled((WorldSel.Archetype?.Name ?? "?") + DirtyMark_V19(WorldSel));
            }
            if (WorldDirtyCount > 0)
            {
                Dot();
                ImGui.TextColored(UiTheme.Warn,
                    $"{WorldDirtyCount} unsaved ymap{(WorldDirtyCount == 1 ? "" : "s")}");
            }
            if (!string.IsNullOrEmpty(WorldClipboardSummary))
            {
                Dot();
                ImGui.TextDisabled("copied: " + WorldClipboardSummary);
            }
            if (WorldTruncated)
            {
                Dot();
                ImGui.TextColored(UiTheme.Warn, "budget reached");
            }
            if (first) ImGui.TextDisabled(WorldYmapsOpen < WorldYmapsWanted ? "loading the map..." : "ready");

            ImGui.End();
            ImGui.PopStyleVar();
        }

        private void DrawExplorerLeft(float displayHeight)
        {
            DrawWorldMapTab(displayHeight);
        }

        private void DrawExplorerTree()
        {
            var world = WorldRef;
            if (world == null || !world.Ready)
            {
                ImGui.TextWrapped("The world is still opening.");
                return;
            }

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##expfilter", "filter entities by archetype", ref explorerFilter, 96);

            var cam = new SharpDX.Vector3(WorldCameraPos.X, WorldCameraPos.Y, WorldCameraPos.Z);
            var near = new List<(float d, WorldStreamer.MapNode n)>();
            foreach (var n in world.Nodes)
            {
                if (n.Ymap?.AllEntities == null) continue;
                near.Add((n.DistanceTo(cam), n));
            }
            near.Sort((a, b) => a.d.CompareTo(b.d));

            int shownYmaps = 0;
            bool filtering = explorerFilter.Length > 0;
            foreach (var (d, n) in near)
            {
                if (shownYmaps >= 40) { ImGui.TextDisabled($"... {near.Count - 40:N0} more ymaps beyond"); break; }
                shownYmaps++;

                var ents = n.Ymap.AllEntities;
                if (!ImGui.TreeNode($"{n.Name}  ({ents.Length})  {d:0}m##ym{n.Hash}")) continue;

                int shown = 0, hidden = 0;
                for (int i = 0; i < ents.Length; i++)
                {
                    var en = ents[i];
                    var nm = en?.Archetype?.Name ?? en?.Name;
                    if (string.IsNullOrEmpty(nm)) continue;
                    if (filtering && nm.IndexOf(explorerFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (shown >= 250) { hidden++; continue; }
                    shown++;
                    bool selHere = ReferenceEquals(WorldSel, en);
                    if (ImGui.Selectable($"{nm}##e{n.Hash}_{i}", selHere))
                        RequestWorldSelectEntity = en;
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        RequestWorldSelectEntity = en;
                        RequestWorldFrameSelected = true;
                    }
                }
                if (hidden > 0) ImGui.TextDisabled($"  ... {hidden:N0} more (narrow the filter)");
                ImGui.TreePop();
            }
            if (shownYmaps == 0)
                ImGui.TextWrapped("Nothing loaded near the camera yet - fly somewhere, or use a " +
                                  "Go To on the World tab.");
        }

        private void DrawInspectorTab()
        {
            rowSeq = 0;
            DrawWorldSelection();
            var e = WorldSel;
            if (e == null) return;

            var d = e._CEntityDef;
            if (e.Archetype == null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.65f, 0.3f, 1.0f));
                ImGui.TextWrapped("This prop's archetype is not in the game or the project, so only its " +
                                  "placement box can be drawn. Open the .ytyp that defines " +
                                  d.archetypeName.ToString() + " (Project window > Open, or drop it in with the ymap).");
                ImGui.PopStyleColor();
            }
            ImGui.Spacing();
            if (!ImGui.CollapsingHeader("Details##wseldetails")) return;
            ImGui.Indent();
            ImGui.TextDisabled("ENTITY");
            Row("Archetype", d.archetypeName.ToString() + "   # " + d.archetypeName.Hash);
            Row("Position", $"{d.position.X:0.###}, {d.position.Y:0.###}, {d.position.Z:0.###}");
            Row("Rotation", $"{d.rotation.X:0.####}, {d.rotation.Y:0.####}, {d.rotation.Z:0.####}, {d.rotation.W:0.####}");
            Row("Scale", $"{d.scaleXY:0.###} xy, {d.scaleZ:0.###} z");
            Row("GUID", d.guid.ToString());
            Row("Flags", d.flags.ToString());
            Row("Lod dist", $"{d.lodDist:0.#}   child {d.childLodDist:0.#}");
            Row("Lod level", d.lodLevel.ToString().Replace("LODTYPES_DEPTH_", ""));
            Row("Priority", d.priorityLevel.ToString());
            Row("Parent index", d.parentIndex + (e.Parent != null ? "   (" + (e.Parent.Archetype?.Name ?? "?") + ")" : ""));
            Row("Children", d.numChildren.ToString());
            Row("AO / artificial", $"{d.ambientOcclusionMultiplier} / {d.artificialAmbientOcclusion}");
            Row("Tint", d.tintValue.ToString());
            Row("Distance", $"{e.Distance:0.#} m");
            if (e.MloParent != null) Row("Interior", e.MloParent.Archetype?.Name ?? "(mlo)");
            ImGui.Unindent();
            if (e.MloInstance != null) Row("MLO instance", $"{e.MloInstance.Entities?.Length ?? 0} entities, {e.MloInstance.EntitySets?.Length ?? 0} sets");

            ImGui.Indent();
            var a = e.Archetype;
            if (a != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextDisabled("ARCHETYPE");
                var b = a._BaseArchetypeDef;
                Row("Name", a.Name + "   # " + a.Hash);
                Row("Type", a is MloArchetype ? "MLO (interior)" : a is TimeArchetype ? "Time" : "Base");
                Row("Asset", b.assetName.ToString() + "   " + b.assetType.ToString().Replace("ASSET_TYPE_", ""));
                Row("Texture dict", b.textureDictionary.ToString());
                Row("Drawable dict", b.drawableDictionary.ToString());
                Row("Physics dict", b.physicsDictionary.ToString());
                Row("Clip dict", b.clipDictionary.ToString());
                Row("Lod dist", $"{b.lodDist:0.#}   hd tex {b.hdTextureDist:0.#}");
                Row("Flags", b.flags.ToString());
                Row("BB min", $"{b.bbMin.X:0.##}, {b.bbMin.Y:0.##}, {b.bbMin.Z:0.##}");
                Row("BB max", $"{b.bbMax.X:0.##}, {b.bbMax.Y:0.##}, {b.bbMax.Z:0.##}");
                Row("BS", $"r {b.bsRadius:0.##} @ {b.bsCentre.X:0.##}, {b.bsCentre.Y:0.##}, {b.bsCentre.Z:0.##}");
                Row("Special attr", b.specialAttribute.ToString());
                if (a.Ytyp != null) Row("Ytyp", a.Ytyp.Name ?? "?");
                if (a is MloArchetype mlo) Row("Interior", MapProject.DescribeMlo(mlo));
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("SOURCE");
            Row("Ymap", e.Ymap?.Name ?? "(interior)");
            Row("Path", e.Ymap?.RpfFileEntry?.Path ?? e.Ymap?.FilePath ?? "");
            Row("Index", e.Index.ToString());
            ImGui.Unindent();
        }

        private static int rowSeq;
        private static void Row(string k, string v)
        {
            ImGui.TextDisabled(k);
            ImGui.SameLine(120);
            string val = v ?? "";
            ImGui.SetNextItemWidth(-1);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0, 0, 0, 0));
            ImGui.InputText($"##row{k}{rowSeq++}", ref val, 512, ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AutoSelectAll);
            ImGui.PopStyleColor();
            if (ImGui.BeginPopupContextItem())
            {
                if (ImGui.MenuItem("Copy value")) ImGui.SetClipboardText(val);
                if (ImGui.MenuItem("Copy \"" + k + ": \" + value")) ImGui.SetClipboardText(k + ": " + val);
                ImGui.EndPopup();
            }
        }

        private void DrawAssetsTab()
        {
            if (Archive == null || !Archive.Ready)
            {
                ImGui.TextWrapped("The archive index is still building.");
                return;
            }

            ImGui.SetNextItemWidth(-1);
            bool changed = ImGui.InputTextWithHint("##assetsearch", "search the game's files",
                                                   ref archiveSearch, 96);
            ImGui.SetNextItemWidth(-1);
            changed |= ImGui.Combo("##assetfilter", ref archiveFilter,
                                   ArchiveBrowser.FilterLabels, ArchiveBrowser.FilterLabels.Length);
            if (changed)
                Archive.Search(archiveSearch, ArchiveBrowser.ExtensionFor(archiveFilter));

            var results = Archive.Results;
            if (results.Count > 0)
            {
                ImGui.TextDisabled(Archive.ResultTotal > results.Count
                    ? $"showing {results.Count:N0} of {Archive.ResultTotal:N0}"
                    : $"{results.Count:N0} hit(s)");
                ImGui.BeginChild("##assetresults", new Vector2(0, 260), ImGuiChildFlags.Borders);
                foreach (var r in results)
                {
                    if (ImGui.Selectable(r.File.Name + "##a" + r.File.GetHashCode(),
                                         Archive.Selected == r.File))
                        Archive.Selected = r.File;
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(r.Path);
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            RequestOpenArchiveFile = r.File;
                    }
                }
                ImGui.EndChild();
                if (Archive.Selected != null &&
                    ImGui.Button("Open " + Archive.Selected.Name, new Vector2(-1, 0)))
                    RequestOpenArchiveFile = Archive.Selected;
            }

            if (ArchivePreview != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                DrawArchivePreviewPanel();
            }
        }

        public ArchiveBrowser Archive = new ArchiveBrowser();

        private string archiveSearch = "";
        private int archiveFilter;
        private string lastArchiveSearch = "~";
        private int lastArchiveFilter = -1;
        private readonly List<RpfDirectoryEntry> archiveCrumbs = new List<RpfDirectoryEntry>();

        public RpfFileEntry RequestOpenArchiveFile;
        public RpfFileEntry RequestExportArchiveFile;

        private void DrawArchiveLeft(float displayHeight)
        {
            DrawWorkspaceLogo(150.0f);

            ImGui.TextDisabled("ARCHIVES");
            if (!Archive.Ready)
            {
                ImGui.TextWrapped(string.IsNullOrEmpty(GameLoadStatus)
                    ? "Waiting for the game archives to open. Set the GTA V folder in the Lights workspace if this does not finish."
                    : GameLoadStatus);
                return;
            }
            ImGui.SameLine();
            ImGui.Text($"({Archive.FileCount:N0} files)");

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##arcsearch", "Search every file by name...", ref archiveSearch, 128);
            ImGui.SetNextItemWidth(-1);
            ImGui.Combo("##arcfilter", ref archiveFilter,
                ArchiveBrowser.FilterLabels, ArchiveBrowser.FilterLabels.Length);

            if (archiveSearch != lastArchiveSearch || archiveFilter != lastArchiveFilter)
            {
                lastArchiveSearch = archiveSearch;
                lastArchiveFilter = archiveFilter;
                Archive.Search(archiveSearch, ArchiveBrowser.ExtensionFor(archiveFilter));
            }

            bool searching = Archive.Results.Count > 0 ||
                             (archiveSearch.Trim().Length > 0 || archiveFilter > 0);

            if (searching)
            {
                if (ImGui.Button("Back", new Vector2(-1, 0)))
                {
                    archiveSearch = "";
                    archiveFilter = 0;
                }
                ImGui.TextDisabled(Archive.ResultTotal > Archive.Results.Count
                    ? $"{Archive.Results.Count} of {Archive.ResultTotal:N0} matches"
                    : $"{Archive.ResultTotal:N0} match{(Archive.ResultTotal == 1 ? "" : "es")}");

                if (ImGui.BeginChild("##arcresults", new Vector2(0, displayHeight - 210),
                        ImGuiChildFlags.Borders))
                {
                    for (int i = 0; i < Archive.Results.Count; i++)
                    {
                        var r = Archive.Results[i];
                        bool sel = ReferenceEquals(Archive.Selected, r.File);
                        if (ImGui.Selectable($"{r.File.Name}##res{i}", sel))
                            Archive.Selected = r.File;
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip(r.Path);
                        if (ImGui.IsMouseDoubleClicked(0) && ImGui.IsItemHovered())
                            RequestOpenArchiveFile = r.File;
                        var szTxt = ArchiveBrowser.SizeOf(r.File);
                        ImGui.SameLine(UiFit_V32.LeadingWidth(UiFit_V32.Reserve(UiFit_V32.TextW(szTxt))));
                        ImGui.TextDisabled(szTxt);
                    }
                    if (Archive.Results.Count == 0) ImGui.TextDisabled("Nothing matches.");
                }
                ImGui.EndChild();
                return;
            }

            ImGui.Spacing();
            if (archiveCrumbs.Count == 0)
            {
                ImGui.TextDisabled("Pick an archive:");
            }
            else
            {
                if (ImGui.SmallButton("root")) archiveCrumbs.Clear();
                for (int i = 0; i < archiveCrumbs.Count; i++)
                {
                    ImGui.SameLine(0, 3);
                    ImGui.TextDisabled("/");
                    ImGui.SameLine(0, 3);
                    if (ImGui.SmallButton(archiveCrumbs[i].Name + "##crumb" + i))
                    {
                        archiveCrumbs.RemoveRange(i + 1, archiveCrumbs.Count - i - 1);
                        break;
                    }
                }
            }

            if (ImGui.BeginChild("##arctree", new Vector2(0, displayHeight - 190), ImGuiChildFlags.Borders))
            {
                if (archiveCrumbs.Count == 0)
                {
                    foreach (var rpf in Archive.Roots)
                    {
                        if (ImGui.Selectable(ArchiveBrowser.RootLabel(rpf) + "##rpf" + rpf.Path)
                            && rpf.Root != null)
                            archiveCrumbs.Add(rpf.Root);
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip(rpf.Path);
                    }
                }
                else
                {
                    var dir = archiveCrumbs[archiveCrumbs.Count - 1];
                    if (dir.Directories != null)
                    {
                        foreach (var d in dir.Directories.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            if (ImGui.Selectable("[ " + d.Name + " ]##d" + d.Path)) archiveCrumbs.Add(d);
                        }
                    }
                    if (dir.Files != null)
                    {
                        foreach (var f in dir.Files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            bool sel = ReferenceEquals(Archive.Selected, f);
                            if (ImGui.Selectable(f.Name + "##f" + f.Path, sel)) Archive.Selected = f;
                            if (ImGui.IsMouseDoubleClicked(0) && ImGui.IsItemHovered())
                            {
                                var inner = f.File?.Children?.FirstOrDefault(
                                    c => string.Equals(c.Path, f.Path, StringComparison.OrdinalIgnoreCase));
                                if (inner?.Root != null) archiveCrumbs.Add(inner.Root);
                                else RequestOpenArchiveFile = f;
                            }
                            var szTxt2 = ArchiveBrowser.SizeOf(f);
                            ImGui.SameLine(UiFit_V32.LeadingWidth(UiFit_V32.Reserve(UiFit_V32.TextW(szTxt2))));
                            ImGui.TextDisabled(szTxt2);
                        }
                    }
                }
            }
            ImGui.EndChild();
        }

        private void DrawArchivePreviewPanel()
        {
            var pv = ArchivePreview;
            ImGui.TextDisabled("PREVIEW");
            ImGui.TextWrapped(pv.Entry?.Name ?? "(asset)");
            ImGui.TextDisabled(pv.Stats.Summary());
            if (!string.IsNullOrEmpty(pv.Error))
                ImGui.TextColored(UiTheme.Danger, pv.Error);

            if (pv.DrawableNames.Count > 1)
            {
                ImGui.Spacing();
                ImGui.TextDisabled($"{pv.DrawableNames.Count} DRAWABLES");
                ImGui.BeginChild("##ydditems", new Vector2(0, Math.Min(pv.DrawableNames.Count * 21 + 8, 150)),
                                 ImGuiChildFlags.Borders);
                for (int i = 0; i < pv.DrawableNames.Count; i++)
                {
                    bool cur = i == pv.SelectedDrawable;
                    if (ImGui.Selectable(pv.DrawableNames[i] + "##pd" + i, cur) && !cur)
                        RequestPreviewDrawable = pv.DrawableNames[i];
                }
                ImGui.EndChild();
            }

            if (pv.Textures.Count > 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled($"{pv.Textures.Count} TEXTURES");
                ImGui.BeginChild("##pvtex", new Vector2(0, Math.Min(pv.Textures.Count * 21 + 8, 170)),
                                 ImGuiChildFlags.Borders);
                foreach (var t in pv.Textures)
                {
                    ImGui.Text(t.Name);
                    ImGui.SameLine();
                    ImGui.TextDisabled($"{t.Width}x{t.Height} {t.Format} {t.DataBytes / 1024}KB");
                }
                ImGui.EndChild();
            }

            if (ImGui.Button("Close preview", new Vector2(-1, 0))) RequestPreviewClose = true;
        }

        private void DrawArchiveRight()
        {
            if (ArchivePreview != null)
            {
                DrawArchivePreviewPanel();
                ImGui.Spacing();
                ImGui.Separator();
            }

            ImGui.TextDisabled("SELECTED FILE");
            var e = Archive.Selected;
            if (e == null)
            {
                ImGui.TextWrapped("Nothing selected. Walk the tree or search on the left, then " +
                                  "click a file - or double-click it to open it straight away.");
                return;
            }

            ImGui.Spacing();
            ImGui.Text(e.Name);
            ImGui.TextDisabled(ArchiveBrowser.KindOf(e).ToUpperInvariant() + "   " +
                               ArchiveBrowser.SizeOf(e));
            ImGui.Spacing();
            ImGui.TextDisabled("Where it lives");
            ImGui.TextWrapped(e.Path ?? "");

            ImGui.Spacing();
            ImGui.Separator();

            bool drawable = ArchiveBrowser.IsDrawable(e);
            string kind = ArchiveBrowser.KindOf(e);
            bool openable = drawable || kind == "ytyp" || kind == "ymap" || kind == "ytd";

            ImGui.BeginDisabled(!openable);
            if (ImGui.Button("Open", new Vector2(-1, 0))) RequestOpenArchiveFile = e;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(openable
                    ? "Models open in the viewport, .ytyp and .ymap go through the importers,\n" +
                      ".ytd loads as textures for the material workspace."
                    : "Nothing in this tool reads that format yet - export it instead.");

            if (ImGui.Button("Export...", new Vector2(-1, 0))) RequestExportArchiveFile = e;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Write the file out of the archive, exactly as it is stored,\n" +
                                 "so it can be edited elsewhere and put back.");
        }

        public const float TimelineHeight = 108.0f;

        private int dragKey = -1;
        private bool draggingHead;

        public bool ScrubbingTimeline => draggingHead;

        private void DrawCineTimeline(float displayWidth, float displayHeight)
        {
            float x0 = settings.LeftPanelWidth;
            float w = Math.Max(displayWidth - settings.LeftPanelWidth - settings.RightPanelWidth, 120.0f);
            float y0 = displayHeight - TimelineHeight;

            ImGui.SetNextWindowPos(new Vector2(x0, y0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, TimelineHeight), ImGuiCond.Always);
            var flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                      | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                      | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBringToFrontOnFocus;
            if (!ImGui.Begin("##cinetimeline", flags)) { ImGui.End(); return; }

            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            float trackW = ImGui.GetContentRegionAvail().X;
            float trackY = p.Y + 22.0f;
            float trackH = 34.0f;
            float len = Math.Max(Sequence.Length, 0.001f);

            uint colBg = ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.11f, 1f));
            uint colTick = ImGui.GetColorU32(new Vector4(0.35f, 0.35f, 0.37f, 1f));
            uint colText = ImGui.GetColorU32(new Vector4(0.62f, 0.62f, 0.64f, 1f));
            var accent = UiTheme.Accent;
            uint colShot = ImGui.GetColorU32(new Vector4(accent.X * 0.45f, accent.Y * 0.30f, accent.Z * 0.28f, 1f));
            uint colShotSel = ImGui.GetColorU32(accent);
            uint colHold = ImGui.GetColorU32(new Vector4(accent.X * 0.75f, accent.Y * 0.45f, accent.Z * 0.40f, 1f));
            uint colHead = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f));

            dl.AddRectFilled(new Vector2(p.X, trackY), new Vector2(p.X + trackW, trackY + trackH), colBg, 3.0f);

            float X(float t) => p.X + (t / len) * trackW;

            int labelEvery = len > 30 ? 10 : len > 12 ? 5 : 1;
            for (int sTick = 0; sTick <= (int)Math.Ceiling(len); sTick++)
            {
                float tx = X(sTick);
                bool major = (sTick % labelEvery) == 0;
                dl.AddLine(new Vector2(tx, trackY - (major ? 8.0f : 4.0f)), new Vector2(tx, trackY), colTick);
                if (major) dl.AddText(new Vector2(tx + 2, trackY - 20.0f), colText, $"{sTick}s");
            }

            float cursor = 0.0f;
            for (int i = 0; i < Sequence.Shots.Count; i++)
            {
                var sh = Sequence.Shots[i];
                float travel = i == 0 ? 0.0f : Math.Max(sh.Duration, 0.0f);
                float hold = Math.Max(sh.Hold, 0.0f);
                float a = cursor, b = cursor + travel, c = b + hold;
                bool sel = i == SelectedShot;

                if (travel > 0.0001f)
                {
                    dl.AddRectFilled(new Vector2(X(a), trackY + 10), new Vector2(X(b), trackY + trackH - 10),
                        sel ? colShotSel : colShot, 2.0f);
                }
                if (hold > 0.0001f)
                {
                    dl.AddRectFilled(new Vector2(X(b), trackY + 2), new Vector2(X(c), trackY + trackH - 2),
                        sel ? colShotSel : colHold, 3.0f);
                }

                float kx = X(b);
                dl.AddNgonFilled(new Vector2(kx, trackY + trackH * 0.5f), 7.0f,
                    sel ? colHead : colShotSel, 4);

                ImGui.SetCursorScreenPos(new Vector2(kx - 8.0f, trackY));
                ImGui.InvisibleButton($"##key{i}", new Vector2(16.0f, trackH));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
                    ImGui.SetTooltip($"{i + 1}. {sh.Name}\n" +
                        (i == 0 ? "start of the move" : $"travel {travel:0.00}s") +
                        (hold > 0.0001f ? $"\nhold {hold:0.00}s" : "") +
                        "\n\ndrag to change the travel, right-click to select");
                }
                if (ImGui.IsItemActivated()) { SelectedShot = i; dragKey = i; }
                if (dragKey == i && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    float dx = ImGui.GetIO().MouseDelta.X;
                    if (i > 0)
                    {
                        sh.Duration = Math.Max(0.05f, sh.Duration + dx * (len / Math.Max(trackW, 1.0f)));
                    }
                }
                if (dragKey == i && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) dragKey = -1;

                cursor = c;
            }

            float hx = X(Math.Min(PlayTime, len));
            dl.AddLine(new Vector2(hx, trackY - 10), new Vector2(hx, trackY + trackH + 4), colHead, 2.0f);
            dl.AddNgonFilled(new Vector2(hx, trackY - 10), 5.0f, colHead, 3);

            ImGui.SetCursorScreenPos(new Vector2(p.X, trackY - 12));
            ImGui.InvisibleButton("##scrub", new Vector2(trackW, trackH + 20));
            if (ImGui.IsItemActivated() && dragKey < 0) draggingHead = true;
            if (draggingHead)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    float mx = ImGui.GetIO().MousePos.X;
                    PlayTime = Math.Clamp((mx - p.X) / Math.Max(trackW, 1.0f), 0.0f, 1.0f) * len;
                    Playing = false;
                }
                else draggingHead = false;
            }

            ImGui.SetCursorScreenPos(new Vector2(p.X, trackY + trackH + 8));
            ImGui.TextDisabled(
                $"{PlayTime:0.00} / {len:0.00} s     {Sequence.Shots.Count} shots     " +
                $"{Sequence.FrameCount} frames at {Sequence.Fps} fps" +
                (Sequence.Shots.Count < 2 ? "     - add a second shot to make a move" : ""));

            ImGui.End();
        }

        public bool TimelineVisible => CineMode && !PhotoMode;

        public static readonly string[] BlendLabels =
            { "Cut", "Linear", "Ease in/out", "Ease in", "Ease out", "Hard in", "Hard out" };

        public CameraSequence Sequence = new CameraSequence();
        public int SelectedShot = -1;

        public float PlayTime;
        public bool Playing;
        public bool RequestAddShot;
        public bool RequestGoToShot;
        public bool RequestReplaceShot;
        public bool RequestRenderSequence;

        private void DrawCineWorkspaceLeft(float displayHeight)
        {
            DrawWorkspaceLogo(150.0f);

            ImGui.TextDisabled("CAMERA");
            ImGui.SameLine();
            ImGui.Text($"({Sequence.Shots.Count} shot{(Sequence.Shots.Count == 1 ? "" : "s")})");
            if (!string.IsNullOrEmpty(StatsText)) ImGui.TextDisabled(StatsText);

            float len = Sequence.Length;
            ImGui.Spacing();
            if (ImGui.Button(Playing ? "Pause" : "Play", new Vector2(70, 0)))
            {
                Playing = !Playing;
                if (Playing && PlayTime >= len - 0.001f) PlayTime = 0;
            }
            ImGui.SameLine();
            if (ImGui.Button("Stop", new Vector2(60, 0))) { Playing = false; PlayTime = 0; }
            ImGui.SameLine();
            bool loop = Sequence.Loop;
            if (ImGui.Checkbox("Loop", ref loop)) Sequence.Loop = loop;

            ImGui.SetNextItemWidth(-1);
            float t = PlayTime;
            if (UiSlider_U28.Float("##playhead", ref t, 0.0f, Math.Max(len, 0.01f),
                    $"{t:0.00} / {len:0.00} s"))
            {
                PlayTime = t;
                Playing = false;
            }
            if (ImGui.IsItemActive()) draggingHead = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Drag to scrub. The camera follows the playhead whether or not\n" +
                                 "it is playing, so a move can be judged a frame at a time.");

            ImGui.Spacing();
            ImGui.Separator();

            if (ImGui.Button("Add shot here", new Vector2(-1, 0))) RequestAddShot = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Records the camera exactly where it is now, at the end of the\n" +
                                 "list. Fly to the next position and press it again.");

            bool has = SelectedShot >= 0 && SelectedShot < Sequence.Shots.Count;
            ImGui.BeginDisabled(!has);
            if (ImGui.Button("Go to", new Vector2(78, 0))) RequestGoToShot = true;
            ImGui.SameLine();
            if (ImGui.Button("Re-record", new Vector2(90, 0))) RequestReplaceShot = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Overwrite the selected shot with where the camera is now.");
            ImGui.SameLine();
            if (ImGui.Button("Delete", new Vector2(-1, 0)) && has)
            {
                Sequence.Shots.RemoveAt(SelectedShot);
                SelectedShot = Math.Min(SelectedShot, Sequence.Shots.Count - 1);
            }
            ImGui.EndDisabled();

            float listH = Math.Max(90.0f, displayHeight * 0.26f);
            if (ImGui.BeginChild("##shots", new Vector2(0, listH), ImGuiChildFlags.Borders))
            {
                for (int i = 0; i < Sequence.Shots.Count; i++)
                {
                    var sh = Sequence.Shots[i];
                    bool sel = i == SelectedShot;
                    if (ImGui.Selectable($"{i + 1,2}. {sh.Name}##shot{i}", sel))
                    {
                        SelectedShot = i;
                        RequestGoToShot = true;
                    }
                    if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                    {
                        int j = i + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
                        if (j >= 0 && j < Sequence.Shots.Count)
                        {
                            Sequence.Shots[i] = Sequence.Shots[j];
                            Sequence.Shots[j] = sh;
                            SelectedShot = j;
                            ImGui.ResetMouseDragDelta();
                        }
                    }
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - 52);
                    ImGui.TextDisabled(i == 0 ? "start" : $"{sh.Duration:0.0}s");
                }
                if (Sequence.Shots.Count == 0)
                    ImGui.TextDisabled("No shots yet.\nFly the camera somewhere\nand press Add shot here.");
            }
            ImGui.EndChild();

            if (has)
            {
                var sh = Sequence.Shots[SelectedShot];
                ImGui.Spacing();
                ImGui.TextDisabled($"SHOT {SelectedShot + 1}");

                var name = sh.Name ?? "";
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##shotname", ref name, 48)) sh.Name = name;

                ImGui.SetNextItemWidth(-70);
                if (SelectedShot > 0)
                {
                    UiSlider_U28.Float("Travel", ref sh.Duration, 0.1f, 30.0f, "%.1f s");
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Seconds spent moving here from the shot before it.");
                    ImGui.SetNextItemWidth(-70);
                }
                UiSlider_U28.Float("Hold", ref sh.Hold, 0.0f, 20.0f, "%.1f s");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Seconds held still on this shot before moving off it.");
                ImGui.SetNextItemWidth(-70);
                int bs = (int)sh.Blend;
                if (ImGui.Combo("Blend", ref bs, BlendLabels, BlendLabels.Length))
                    sh.Blend = (BlendStyle)bs;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(
                        "The shape of the move into this shot - the seven a sequencer offers,\n" +
                        "and the ones Cinemachine defines:\n\n" +
                        "Cut         no move at all, the camera is simply here\n" +
                        "Linear      constant speed. Mechanical, occasionally right\n" +
                        "Ease in/out accelerate away, decelerate in. Most moves want this\n" +
                        "Ease in     leaves at speed, settles gently\n" +
                        "Ease out    starts gently, arrives at speed\n" +
                        "Hard in     creeps away, arrives hard\n" +
                        "Hard out    snaps away, drifts in");
                ImGui.SetNextItemWidth(-70);
                UiSlider_U28.Float("Ease", ref sh.Ease, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How much of the blend shape above to apply. 0 is a straight\n" +
                                     "constant-speed move whatever the style says; 1 is the style\n" +
                                     "in full. It is there so a shot can be given a bit of one.");
                ImGui.SetNextItemWidth(-70);
                UiSlider_U28.Float("Shake", ref sh.Shake, 0.0f, 3.0f, "%.2f");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Handheld sway at this shot, blended along the move like\n" +
                                     "everything else - so a camera can settle as it arrives, or\n" +
                                     "pick up as it moves in. The profile is under SHAKE below.");
                ImGui.SetNextItemWidth(-70);
                UiSlider_U28.Float("Lens", ref sh.Fov, 12.0f, 110.0f, "%.0f deg");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Each shot carries its own field of view, so a move can zoom.");
                ImGui.SetNextItemWidth(-70);
                UiSlider_U28.Float("Focus", ref sh.Focus, 0.0f, 60.0f,
                    sh.Focus < 0.05f ? "auto" : "%.1f m");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Depth-of-field focus for this shot. At zero the autofocus\n" +
                                     "decides, which is right for a move and wrong for a rack focus.");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("SHAKE");
            ImGui.SetNextItemWidth(-70);
            UiSlider_U28.Float("Sway", ref Sequence.Shake.PositionAmplitude, 0.0f, 0.3f, "%.3f m");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How far the camera drifts, in metres, at a shot shake of 1.");
            ImGui.SetNextItemWidth(-70);
            UiSlider_U28.Float("Tilt", ref Sequence.Shake.RotationAmplitude, 0.0f, 4.0f, "%.2f deg");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How far it rocks, in degrees, at a shot shake of 1. A little of\n" +
                                 "this reads as handheld far more than sway does.");
            ImGui.SetNextItemWidth(-70);
            UiSlider_U28.Float("Speed", ref Sequence.Shake.Frequency, 0.1f, 6.0f, "%.2f");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Slow is a shoulder, fast is an engine. Four noise channels at\n" +
                                 "unrelated frequencies are summed, so it never falls into a\n" +
                                 "rhythm - anything that lines up reads as a machine.\n\n" +
                                 "Driven by the timeline, never by wall clock, so the render is\n" +
                                 "frame-for-frame what the preview showed.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("EXPORT");
            int fps = Sequence.Fps;
            ImGui.SetNextItemWidth(-70);
            if (UiSlider_U28.Int("FPS", ref fps, 12, 60)) Sequence.Fps = Math.Max(1, fps);
            ImGui.TextDisabled($"{Sequence.FrameCount} frames at {Sequence.Fps} fps");

            ImGui.BeginDisabled(Sequence.Shots.Count < 2);
            if (ImGui.Button("Render sequence...", new Vector2(-1, 0))) RequestRenderSequence = true;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "Renders every frame of the move at the resolution and quality set in the\n" +
                    "panel opposite, as a numbered PNG sequence.\n\n" +
                    "Frames rather than a video file, because a video file means an encoder and\n" +
                    "this tool does not ship one. If ffmpeg is on your PATH it is offered at the\n" +
                    "end; either way a ready-to-run command is written next to the frames.");
            if (Sequence.Shots.Count < 2)
                ImGui.TextDisabled("Two shots or more to render a move.");
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return "..." + s.Substring(s.Length - (max - 3));
        }

        private void DrawGameSection()
        {
            if (Game == null) { ImGui.TextDisabled("unavailable"); return; }

            if (Game.Initialising)
            {
                ImGui.TextColored(UiTheme.AccentBright, "Loading game files...");
                ImGui.TextWrapped(Game.Status);
                ImGui.TextDisabled("First run scans every RPF - this takes a while.");
            }
            else if (Game.Ready)
            {
                ImGui.TextColored(UiTheme.Ok, "Game files ready");
                if (Game.TextureIndexTotal > 0 && !Game.TextureIndexReady)
                {
                    float pc = 100.0f * Game.TextureIndexDone / Math.Max(Game.TextureIndexTotal, 1);
                    ImGui.TextDisabled($"Indexing textures across the archives... {pc:0}% " +
                                       $"({Game.TextureIndexDone}/{Game.TextureIndexTotal} .ytd)");
                }
                ImGui.TextDisabled(Game.Folder ?? "");
            }
            else
            {
                if (ImGui.Button("Select GTA V folder...")) RequestPickGtaFolder?.Invoke();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Point at your GTA V install (the folder with GTA5.exe).\nNeeded to auto-load base-game props for MLO import.");
                if (!string.IsNullOrEmpty(Game.Error))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Danger);
                    ImGui.TextWrapped(Game.Error);
                    ImGui.PopStyleColor();
                }
            }

            ImGui.Separator();

            if (ImGui.Button("Import map...", new Vector2(-1, 0))) RequestImportMapFiles_V36?.Invoke();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Pick any mix of .ytyp and .ymap files at once. The archetypes are loaded first,\n"
                                 + "then the placements - which is the order that works.");
                        if (ImGui.Button("Import ytyp...")) RequestImportYtyp?.Invoke();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Load an interior .ytyp: every entity is placed at its exact position.\nCustom props are read from the folder the .ytyp lives in;\nbase-game props come from your GTA V install.");
            ImGui.SameLine();
            if (ImGui.Button("Import YMAP...")) RequestImportYmap?.Invoke();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Load a .ymap: every map instance is placed at its world position.\nSame prop resolution as the MLO import, and props with lights\nbecome editable entries too.");
            if (scene.MloModel != null)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Clear")) RequestClearImports?.Invoke();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove everything that was imported (imports add to the scene).");
                ImGui.SameLine();
                if (ImGui.SmallButton("Reload")) RequestReimportYtyp?.Invoke();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Import the same .ytyp again - use after adding a prop folder.");
                ImGui.Checkbox("Show MLO", ref scene.MloVisible);
            }
            DrawImportedYtyps_R1(scene);
            if (!Game.Ready && !Game.Initialising)
                ImGui.TextDisabled("(no GTA folder: custom props only)");
            if (ImGui.Checkbox("Import prop lights", ref ImportPropLights)) { }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Props that carry lights join the Props list, with their lights editable\n" +
                                 "at their real positions in the interior. Off = geometry only.\n" +
                                 "Takes effect on the next import (use Reload).");
            ImGui.SameLine();
            if (ImGui.Checkbox("All props editable", ref ImportAllProps)) { }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Bring in EVERY prop as an editable entry, not just the ones that already\n" +
                                 "have lights - use this to add lights to an object that has none.\n" +
                                 "Big interiors list hundreds of props. Takes effect on the next import.");

            if (ImGui.Checkbox("Import vegetation", ref ImportVegetation)) { }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Bring in trees, bushes, grass and hedges. They are alpha-tested and\n" +
                                 "two-sided, so a street's worth of planting costs far more to draw\n" +
                                 "than the building you came to light - but leaving them out makes an\n" +
                                 "exterior look wrong, which is why this is on. Takes effect on the\n" +
                                 "next import (use Reload).");

            var info = scene.MloInfo;
            if (info != null)
            {
                ImGui.TextDisabled($"{info.MloName}");
                ImGui.Text($"{info.Placed}/{info.EntityCount} entities placed");
                ImGui.TextDisabled($"{info.UniqueProps} unique props · {info.Seconds:0.00}s");
                if (info.Missing > 0)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Warn);
                    ImGui.Text($"{info.Missing} not found");
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered() && info.MissingNames.Count > 0)
                        ImGui.SetTooltip("Missing (first few):\n" + string.Join("\n", info.MissingNames) +
                                         "\n\nAdd the folder holding these props below, then Reload.");
                }
            }

            ImGui.Separator();
            if (ImGui.Button("Add prop folder...")) RequestAddPropFolder?.Invoke();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Extra folders to search for .ydr/.ydd/.ytd/.ytyp files\nwhen a prop isn't next to the .ytyp or in the game files.");
            if (PropFolders != null)
            {
                for (int i = 0; i < PropFolders.Count; i++)
                {
                    ImGui.PushID(9000 + i);
                    if (ImGui.SmallButton("x")) { RemovePropFolder?.Invoke(PropFolders[i]); ImGui.PopID(); break; }
                    ImGui.PopID();
                    ImGui.SameLine();
                    ImGui.TextDisabled(Shorten(PropFolders[i], 34));
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(PropFolders[i]);
                }
            }

            if (!string.IsNullOrEmpty(MloStatus)) ImGui.TextWrapped(MloStatus);
        }

        private Vector4 AccentText()
        {
            return UiTheme.AccentBright;
        }

        private readonly Dictionary<string, string> comboFilters = new Dictionary<string, string>();
        private string comboJustOpened;

        private bool SearchCombo(string id, string[] all, ref int index, bool keepFirst = false)
        {
            if (all == null || all.Length == 0) return false;
            if (index < 0 || index >= all.Length) index = 0;

            comboFilters.TryGetValue(id, out var filter);
            filter ??= "";

            bool changed = false;
            if (ImGui.BeginCombo(id, all[index] ?? "", ImGuiComboFlags.HeightLarge))
            {
                if (comboJustOpened != id)
                {
                    comboJustOpened = id;
                    filter = "";
                    ImGui.SetKeyboardFocusHere();
                }

                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint(id + "##search", "type to filter", ref filter, 64))
                    comboFilters[id] = filter;

                ImGui.Separator();

                bool filtering = filter.Length > 0;
                int shown = 0;
                if (ImGui.BeginChild(id + "##rows", new Vector2(0, 260), ImGuiChildFlags.None))
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        var name = all[i] ?? "";
                        if (filtering && !(keepFirst && i == 0) &&
                            name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        shown++;
                        if (ImGui.Selectable(name + "##" + id + i, i == index))
                        {
                            if (i != index) { index = i; changed = true; }
                            ImGui.CloseCurrentPopup();
                        }
                        if (i == index) ImGui.SetItemDefaultFocus();
                    }
                    if (shown == 0) ImGui.TextDisabled("nothing matches");
                }
                ImGui.EndChild();
                ImGui.EndCombo();
            }
            else if (comboJustOpened == id)
            {
                comboJustOpened = null;
            }
            return changed;
        }

        private void DrawSkyWeatherRow()
        {
            ImGui.Checkbox("Sky", ref ShowSky);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Dynamic sky: the game's atmosphere gradient with the sun, moon and clouds.\n" +
                                 "Off leaves the flat background, which is easier for close-up light work.");
            if (!WorldMode) SameCol();
            ImGui.Checkbox("Weather", ref WeatherEnabled);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Applies the preset's fog and cloud cover on top of its timecycle.");

            ImGui.SetNextItemWidth(-110);
            var wnames = string.Concat(WeatherSystem.Presets.Select(p => p.Name + "\0"));
            if (ImGui.Combo("##weather", ref WeatherIndex, wnames)) RequestedWeather = WeatherIndex;
            ImGui.SameLine();
            ImGui.TextDisabled("Weather");
            float wt = settings.WeatherTransitionSeconds;
            ImGui.SetNextItemWidth(-110);
            if (UiSlider_U28.Float("##wtrans", ref wt, 0.0f, 20.0f, wt < 0.05f ? "instant" : "%.1f s"))
                settings.WeatherTransitionSeconds = wt;
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            ImGui.SameLine();
            ImGui.TextDisabled("Transition");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How long a weather change takes to arrive. INSTANT by default:\n" +
                                 "mid-fade the scene is a blend of two cycles, so what you are\n" +
                                 "looking at is not the weather you picked. Raise it to watch the\n" +
                                 "change happen the way the game does it.");
            if (!string.IsNullOrEmpty(WeatherStatus)) ImGui.TextDisabled(WeatherStatus);
        }

        private void DrawTimeRow()
        {
            UiTheme.PushTimeSlider();
            ImGui.SetNextItemWidth(-110);
            UiSlider_U28.Float("##hourw", ref PreviewHour, 0.0f, 23.99f,
                $"{(int)PreviewHour:00}:{(int)((PreviewHour % 1.0f) * 60):00}");
            HourScrubbing = ImGui.IsItemActive();
            UiTheme.PopTimeSlider();
            ImGui.SameLine();
            ImGui.TextColored(UiTheme.TimeGrab, "Time");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Drives the timecycle, filters each light's TimeFlags\nand scales the day/night ambient.\n" +
                                 "Hold the RIGHT mouse button in the viewport and move to scrub it.");

            ImGui.Checkbox("Run clock", ref AutoTime);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Let the day/night cycle play out on its own.");
            if (AutoTime)
            {
                ImGui.SameLine();
                UiTheme.PushTimeSlider();
                ImGui.SetNextItemWidth(-1);
                UiSlider_U28.Float("##timespeed", ref TimeSpeed, 1.0f, 600.0f, "%.0f min/s");
                UiTheme.PopTimeSlider();
            }
            if (ImGui.Checkbox("Drag time", ref ControlTimeOfDay))
            {
                settings.ControlTimeOfDay = ControlTimeOfDay;
                settings.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Hold the RIGHT mouse button in the viewport and move.\n" +
                                 "Right or up advances the clock, left or down winds it back - 2 minutes per\n" +
                                 "pixel, wrapping at midnight. A plain right-click still selects.");
        }

        private void DrawExposureRow()
        {
            ImGui.SetNextItemWidth(-110);
            UiSlider_U28.Float("##exposure", ref Exposure, 0.05f, 2.0f, "%.2f");
            ImGui.SameLine();
            ImGui.TextDisabled("Exposure");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The game tone-maps its final image; this stands in for that.\n" +
                                 "Turn it down if daylight looks blown out, up for a brighter preview.\n" +
                                 "1.00 is the raw timecycle result.");
        }

        private void DrawRegionCombo()
        {
            if (Timecycle == null || Timecycle.Regions.Count <= 1) return;
            var names = new string[Timecycle.Regions.Count];
            for (int i = 0; i < names.Length; i++) names[i] = Timecycle.Regions[i].Name;
            int sel = Timecycle.SelectedRegion;
            ImGui.SetNextItemWidth(-110);
            if (ImGui.Combo("Region", ref sel, names, names.Length)) Timecycle.SelectedRegion = sel;
        }

        private void DrawTimecycleSection()
        {

            DrawSkyWeatherRow();

            DrawTimeRow();

            ImGui.Separator();
            ImGui.Checkbox("Sun shadows", ref SunShadows);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Stop the timecycle's sun shining through walls. The scene is rendered\n" +
                                 "once from the sun's direction; glass and other see-through materials\n" +
                                 "aren't included, so daylight still comes in through windows and openings.\n" +
                                 "Needs a timecycle loaded.");
            if (SunShadows)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(110);
                UiSlider_U28.Float("##sunstr", ref SunShadowStrength, 0.0f, 1.0f, "strength %.2f");
            }

            DrawExposureRow();
            ImGui.Separator();

            if (Timecycle == null) { ImGui.TextDisabled("unavailable"); return; }

            if (GameTimecycleNames != null && GameTimecycleNames.Length > 0)
            {
                ImGui.SetNextItemWidth(-110);
                if (SearchCombo("##stocktc", GameTimecycleNames, ref GameTimecycleIndex))
                    RequestLoadGameTimecycle?.Invoke(GameTimecycleIndex);
                ImGui.SameLine();
                ImGui.TextDisabled("Cycle");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Every weather timecycle shipped with the game (w_clear, w_thunder, ...),\n" +
                                     "read from your install. Open it and type to filter.\n" +
                                     "The hour schedule comes from the game's own time.xml.");
            }
            else if (Game != null && !Game.Ready)
            {
                ImGui.TextDisabled(Game.Initialising
                    ? "Loading the game's timecycles..."
                    : "Select a GTA V folder above to use the stock timecycles.");
            }

            if (ImGui.Button("Edit timecycle...")) ShowTimecycleEditor = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open the full editor: every variable of the cycle at every keyframe,\nand the interior modifier's overrides. Changes show live.");
            ImGui.SameLine();
            if (ImGui.Button("Load XML...")) RequestLoadTimecycle?.Invoke();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Load a timecycle file from disk (w_*.xml or a custom interior timecycle).\nEdits to the file are picked up automatically while it's loaded.");

            ImGui.Separator();
            DrawInteriorTimecycleRow_K2();
            if (Timecycle.Modifiers.Count > 0)
            {
                if (modifierNames == null || modifierNames.Length != Timecycle.Modifiers.Count + 1)
                {
                    modifierNames = new string[Timecycle.Modifiers.Count + 1];
                    modifierNames[0] = "(none)";
                    for (int i = 0; i < Timecycle.Modifiers.Count; i++)
                        modifierNames[i + 1] = Timecycle.Modifiers[i].Name;
                }
                int sel = Timecycle.SelectedModifier + 1;
                ImGui.SetNextItemWidth(-110);
                if (SearchCombo("##tcmod", modifierNames, ref sel, keepFirst: true))
                    Timecycle.SelectedModifier = sel - 1;
                ImGui.SameLine();
                ImGui.TextDisabled("Interior mod");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Timecycle modifier applied on top of the weather cycle -\n" +
                                     "this is what an MLO room actually looks like in game.\n" +
                                     "The room's modifier name is in its .ytyp (timecycleName).");
                if (Timecycle.SelectedModifier >= 0)
                {
                    ImGui.SetNextItemWidth(-110);
                    UiSlider_U28.Float("##tcmodstr", ref Timecycle.ModifierStrength, 0.0f, 1.0f, "%.2f");
                    ImGui.SameLine();
                    ImGui.TextDisabled("Strength");
                    var m = Timecycle.CurrentModifier;
                    if (m != null) ImGui.TextDisabled($"{m.Values.Count} overrides · {m.Source}");
                    if (!Timecycle.HasData)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Warn);
                        ImGui.TextWrapped("Pick a cycle above too - a modifier changes a cycle, it isn't one by itself.");
                        ImGui.PopStyleColor();
                    }
                }
            }
            if (ImGui.SmallButton("Load modifiers XML...")) RequestLoadModifiers?.Invoke();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Load a timecycle_mods_*.xml (yours or the game's).\nEntries merge with the ones already loaded.");
            if (Timecycle.Modifiers.Count > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"{Timecycle.Modifiers.Count} modifiers");
            }
            ImGui.Separator();

            if (!Timecycle.HasData)
            {
                ImGui.TextDisabled("No timecycle loaded - using the flat preview ambient.");
            }
            else
            {
                ImGui.Text(Timecycle.LoadedName ?? "");
                ImGui.Checkbox("Use timecycle lighting", ref TimecycleEnabled);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Drive the scene with the game's global lighting model\n(directional light + natural/artificial hemisphere ambient).");

                DrawRegionCombo();

                if (!Timecycle.ScheduleMatchesData)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Warn);
                    ImGui.TextWrapped("Keyframe count doesn't match the hour schedule - load the game's time.xml for correct hour mapping.");
                    ImGui.PopStyleColor();
                }
            }

            if (!Timecycle.ScheduleFromFile)
            {
                if (ImGui.SmallButton("Load time.xml...")) RequestLoadTimeSchedule?.Invoke();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Only needed without a GTA V folder: time.xml defines which\nhour each keyframe sits on. A standard schedule is assumed otherwise.");
            }
            if (!string.IsNullOrEmpty(TimecycleStatus))
            {
                ImGui.TextWrapped(TimecycleStatus);
            }
        }

        private void DrawLibrarySection()
        {
            if (Library == null) { ImGui.TextDisabled("Library not available."); return; }

            ImGui.SetNextItemWidth(-60);
            ImGui.InputTextWithHint("##libsearch", "search light props", ref libFilter, 64);
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear##lib")) libFilter = "";

            if (Library.Scanning)
            {
                float frac = Library.Total > 0 ? Library.Scanned / (float)Library.Total : 0f;
                ImGui.ProgressBar(frac, new Vector2(-70, 0), $"{Library.Scanned}/{Library.Total}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Stop")) Library.Cancel();
                ImGui.TextDisabled(Library.Status);
            }
            else
            {
                if (ImGui.SmallButton("Rescan folders")) RequestLibraryScan?.Invoke(false);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Re-read your prop folders (Import from GTA V > Add prop folder...).\nFast.");
                ImGui.SameLine();
                bool canArchive = Game != null && Game.Ready;
                if (!canArchive) ImGui.BeginDisabled();
                if (ImGui.SmallButton(Library.ArchiveScanned ? "Rescan game" : "Scan game archives"))
                    RequestLibraryScan?.Invoke(true);
                if (!canArchive) ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(canArchive
                        ? "Sweep every model in your GTA V install for embedded lights.\n" +
                          "~150,000 files, a few minutes; the result is cached so it only happens once."
                        : "Select your GTA V folder first (Import from GTA V).");
                ImGui.TextDisabled(Library.Status);
            }

            var shown = Library.Search(libFilter);
            ImGui.TextDisabled($"{shown.Count} shown - drag one into the viewport to place it");
            ImGui.Separator();

            DrawLibraryGrid(shown);
        }

        private void DrawLibraryGrid(System.Collections.Generic.List<LightPropEntry> shown)
        {
            const float thumb = PropThumbnails.Size;
            const float pad = 6.0f;
            float tileW = thumb + pad * 2;
            float tileH = thumb + pad * 2 + ImGui.GetTextLineHeight() + 2;

            ImGui.BeginChild("libgrid", new Vector2(0, 0), ImGuiChildFlags.Borders);
            float avail = ImGui.GetContentRegionAvail().X;
            int cols = Math.Max(1, (int)(avail / (tileW + 4)));

            var dl = ImGui.GetWindowDrawList();
            uint colBg = ImGui.GetColorU32(ImGuiCol.FrameBg);
            uint colHi = ImGui.GetColorU32(ImGuiCol.ButtonHovered);
            uint colText = ImGui.GetColorU32(ImGuiCol.Text);
            uint colDim = ImGui.GetColorU32(ImGuiCol.TextDisabled);

            int rows = (shown.Count + cols - 1) / cols;
            float rowH = tileH + 4;
            float scrollY = ImGui.GetScrollY();
            float viewH = ImGui.GetWindowHeight();
            int firstRow = Math.Max(0, (int)(scrollY / rowH) - 1);
            int lastRow = Math.Min(rows - 1, (int)((scrollY + viewH) / rowH) + 1);

            if (firstRow > 0) ImGui.Dummy(new Vector2(1, firstRow * rowH));
            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int i = row * cols + c;
                    if (i >= shown.Count) break;
                    if (c > 0) ImGui.SameLine();
                    DrawLibraryTile(shown[i], i, dl, tileW, tileH, thumb, pad,
                        colBg, colHi, colText, colDim);
                }
            }
            int below = rows - 1 - lastRow;
            if (below > 0) ImGui.Dummy(new Vector2(1, below * rowH));
            ImGui.EndChild();
        }

        private void DrawLibraryTile(LightPropEntry e, int i, ImDrawListPtr dl,
            float tileW, float tileH, float thumb, float pad,
            uint colBg, uint colHi, uint colText, uint colDim)
        {
            ImGui.PushID(i);
            var p0 = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton("tile", new Vector2(tileW, tileH));
            bool hovered = ImGui.IsItemHovered();
            bool active = ImGui.IsItemActive();
            var p1 = new Vector2(p0.X + tileW, p0.Y + tileH);

            dl.AddRectFilled(p0, p1, hovered ? colHi : colBg, 4.0f);

            var tex = Thumbs?.Get(e) ?? IntPtr.Zero;
            var ip0 = new Vector2(p0.X + pad, p0.Y + pad);
            var ip1 = new Vector2(ip0.X + thumb, ip0.Y + thumb);
            if (tex != IntPtr.Zero) dl.AddImage(tex, ip0, ip1);
            else dl.AddText(new Vector2(ip0.X + thumb * 0.35f, ip0.Y + thumb * 0.45f), colDim, "...");

            var label = Ellipsise(e.Name, tileW - pad);
            dl.AddText(new Vector2(p0.X + pad, ip1.Y + 2), e.FromArchive ? colDim : colText, label);
            ImGui.PopID();

            if (hovered)
            {
                ImGui.SetTooltip($"{e.Name}\n{e.LightCount} light(s): {e.Types}\n" +
                    (e.FromArchive ? "from the game archives" : e.Path) +
                    "\n\nDrag into the viewport to place it.");
            }
            if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 6.0f)) DraggedLibraryProp = e;
        }

        private static string Ellipsise(string s, float maxWidth)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (ImGui.CalcTextSize(s).X <= maxWidth) return s;
            for (int n = s.Length - 1; n > 1; n--)
            {
                var t = s.Substring(0, n) + "..";
                if (ImGui.CalcTextSize(t).X <= maxWidth) return t;
            }
            return s.Substring(0, 1);
        }

        private void UpdateLibraryDrag()
        {
            if (DraggedLibraryProp == null) return;

            var mouse = ImGui.GetMousePos();
            var dl = ImGui.GetForegroundDrawList();
            var size = ImGui.CalcTextSize(DraggedLibraryProp.Name);
            var p0 = new Vector2(mouse.X + 14, mouse.Y + 6);
            var p1 = new Vector2(p0.X + size.X + 10, p0.Y + size.Y + 6);
            dl.AddRectFilled(p0, p1, ImGui.GetColorU32(ImGuiCol.PopupBg), 3.0f);
            dl.AddText(new Vector2(p0.X + 5, p0.Y + 3),
                ImGui.GetColorU32(ImGuiCol.Text), DraggedLibraryProp.Name);

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                bool overPanel = ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow);
                if (!overPanel) RequestPlaceLibraryProp?.Invoke(DraggedLibraryProp, mouse);
                DraggedLibraryProp = null;
            }
        }

        public readonly List<YmapEntry> YmapEntries = new List<YmapEntry>();
        public string YmapName = "custom_props";
        public event Action RequestExportYmap;
        public event Action<bool> RequestAddPropsToYmap;
        private int selYmapEntry;

        private void DrawYmapSection()
        {
            ImGui.TextWrapped("Collect prop placements and write them out as a .ymap.");
            ImGui.SetNextItemWidth(-70);
            ImGui.InputTextWithHint("##ymapname", "ymap name", ref YmapName, 64);
            ImGui.SameLine();
            ImGui.TextDisabled(".ymap");

            int selCount = scene.SelectedFiles.Count;
            if (selCount == 0) ImGui.BeginDisabled();
            if (ImGui.Button($"Add selected ({selCount})")) RequestAddPropsToYmap?.Invoke(true);
            if (selCount == 0) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add the props selected in the Props tab, at the position each sits now.\n" +
                                 "Select there with click / Ctrl+click / Shift+click.");
            ImGui.SameLine();
            if (ImGui.Button("Add all")) RequestAddPropsToYmap?.Invoke(false);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add every prop currently in the Props list.\n" +
                                 "Imported props keep their placement; hand-opened ones land at the origin.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear##ymap")) { YmapEntries.Clear(); selYmapEntry = 0; }

            if (YmapEntries.Count > 0)
            {
                ImGui.BeginChild("ymaplist", new Vector2(0, 120), ImGuiChildFlags.Borders);
                for (int i = 0; i < YmapEntries.Count; i++)
                {
                    var en = YmapEntries[i];
                    ImGui.PushID(7000 + i);
                    ImGui.Checkbox("##inc", ref en.Include);
                    ImGui.PopID();
                    ImGui.SameLine();
                    if (ImGui.Selectable(en.Label + $"##ye{i}", i == selYmapEntry)) selYmapEntry = i;
                }
                ImGui.EndChild();

                selYmapEntry = Math.Clamp(selYmapEntry, 0, YmapEntries.Count - 1);
                var sel = YmapEntries[selYmapEntry];
                ImGui.SetNextItemWidth(-90);
                ImGui.InputText("##yarch", ref sel.ArchetypeName, 96);
                ImGui.SameLine();
                ImGui.TextDisabled("archetype");

                var p = new System.Numerics.Vector3(sel.Position.X, sel.Position.Y, sel.Position.Z);
                ImGui.SetNextItemWidth(-90);
                if (ImGui.DragFloat3("##ypos", ref p, 0.05f))
                    sel.Position = new SDX.Vector3(p.X, p.Y, p.Z);
                ImGui.SameLine();
                ImGui.TextDisabled("position");

                ImGui.SetNextItemWidth(-90);
                ImGui.DragFloat("##ylod", ref sel.LodDist, 1.0f, 10.0f, 3000.0f, "%.0f");
                ImGui.SameLine();
                ImGui.TextDisabled("lod dist");

                if (ImGui.SmallButton("Remove")) { YmapEntries.RemoveAt(selYmapEntry); return; }
                ImGui.SameLine();
                ImGui.TextDisabled($"{YmapEntries.Count(e => e.Include)} of {YmapEntries.Count} included");
            }

            ImGui.Spacing();
            bool can = YmapEntries.Any(e => e.Include);
            if (!can) ImGui.BeginDisabled();
            if (ImGui.Button("Export .ymap...", new Vector2(-1, 0))) RequestExportYmap?.Invoke();
            if (!can) ImGui.EndDisabled();
        }

        private int selYtyp, selArch;
        private string archFilter = "";
        private int archTab;

        private void DrawArchetypeSection()
        {
            if (ImGui.BeginTabBar("archsections"))
            {
                if (ImGui.BeginTabItem("Create")) { DrawArchetypeCreate(); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Imported")) { DrawImportedArchetypes(); ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }
        }

        public readonly List<ArchetypeDef> Archetypes = new List<ArchetypeDef>();
        public string YtypName = "custom_props";
        private int selNewArch;

        public event Action<LoadedFile> RequestCreateArchetype;
        public event Action RequestExportYtyp;
        public event Action<ArchetypeDef> RequestRecomputeBounds;

        private void DrawArchetypeCreate()
        {
            ImGui.TextDisabled("Bounds are measured from the prop's lights.");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A light proxy's drawable is a 4 cm quad, so its geometry says nothing about\n" +
                                 "how far the light reaches. The box here covers every light's actual reach,\n" +
                                 "and the draw distance is set to match so the prop can't stream out while\n" +
                                 "its light should still be lit.");

            int selCount = scene.SelectedFiles.Count;
            if (selCount == 0) ImGui.BeginDisabled();
            if (ImGui.Button($"From selected prop{(selCount == 1 ? "" : "s")} ({selCount})"))
            {
                foreach (var f in scene.SelectedFiles.ToList()) RequestCreateArchetype?.Invoke(f);
            }
            if (selCount == 0) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Builds an archetype per selected prop: name, asset and a bounding box\n" +
                                 "around everything its lights reach, plus a draw distance to match.\n" +
                                 "Select props in the Props tab.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear##arch")) { Archetypes.Clear(); selNewArch = 0; }

            if (Archetypes.Count == 0)
            {
                ImGui.TextDisabled("No archetypes yet.");
                return;
            }

            float rows = Math.Clamp(Archetypes.Count, 1, 4);
            ImGui.BeginChild("newarchlist", new Vector2(0, rows * ImGui.GetTextLineHeightWithSpacing() + 8),
                ImGuiChildFlags.Borders);
            for (int i = 0; i < Archetypes.Count; i++)
            {
                if (ImGui.Selectable($"{Archetypes[i].Label}##na{i}", i == selNewArch)) selNewArch = i;
            }
            ImGui.EndChild();

            selNewArch = Math.Clamp(selNewArch, 0, Archetypes.Count - 1);
            var d = Archetypes[selNewArch];

            ImGui.SetNextItemWidth(-110);
            ImGui.InputText("##aname", ref d.Name, 96);
            ImGui.SameLine(); ImGui.TextDisabled("name / asset");

            ImGui.SetNextItemWidth(-110);
            ImGui.InputTextWithHint("##atxd", "(none)", ref d.TextureDict, 96);
            ImGui.SameLine(); ImGui.TextDisabled("texture dict");

            ImGui.SetNextItemWidth(-110);
            ImGui.InputTextWithHint("##aphys", "(none)", ref d.PhysicsDict, 96);
            ImGui.SameLine(); ImGui.TextDisabled("physics dict");

            ImGui.SetNextItemWidth(-110);
            ImGui.DragFloat("##alod", ref d.LodDist, 5.0f, 10.0f, 3000.0f, "%.0f");
            ImGui.SameLine(); ImGui.TextDisabled("lod dist");

            ImGui.SetNextItemWidth(-110);
            ImGui.DragFloat("##ahd", ref d.HdTextureDist, 1.0f, 0.0f, 500.0f, "%.0f");
            ImGui.SameLine(); ImGui.TextDisabled("hd tex dist");

            int flags = (int)d.Flags;
            ImGui.SetNextItemWidth(-110);
            if (ImGui.InputInt("##aflags", ref flags)) d.Flags = (uint)Math.Max(flags, 0);
            ImGui.SameLine(); ImGui.TextDisabled("flags");

            ImGui.Checkbox("Fragment (.yft)", ref d.IsFragment);

            ImGui.Separator();
            Field("Bounds min", $"{d.BbMin.X:0.00}, {d.BbMin.Y:0.00}, {d.BbMin.Z:0.00}");
            Field("Bounds max", $"{d.BbMax.X:0.00}, {d.BbMax.Y:0.00}, {d.BbMax.Z:0.00}");
            Field("Sphere", $"({d.BsCentre.X:0.00}, {d.BsCentre.Y:0.00}, {d.BsCentre.Z:0.00})  r {d.BsRadius:0.00}");
            Field("Measured from", d.LightCount > 0 ? $"{d.LightCount} light(s)" : "set by hand");

            if (ImGui.SmallButton("Recompute from lights")) RequestRecomputeBounds?.Invoke(d);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Re-measure the box after moving lights or changing their falloff.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove##arch")) { Archetypes.RemoveAt(selNewArch); return; }

            ImGui.Spacing();
            ImGui.SetNextItemWidth(-70);
            ImGui.InputTextWithHint("##ytypname", "ytyp name", ref YtypName, 64);
            ImGui.SameLine();
            ImGui.TextDisabled(".ytyp");
            if (ImGui.Button("Export .ytyp...", new Vector2(-1, 0))) RequestExportYtyp?.Invoke();
        }

        private void DrawImportedArchetypes()
        {
            var info = scene.MloInfo;
            if (info == null || info.Ytyps.Count == 0)
            {
                ImGui.TextWrapped("Import a .ytyp (Import from GTA V) to see its archetypes here.");
                return;
            }

            ImGui.TextDisabled("YTYPS");
            var ytypNames = info.Ytyps.Select(y => y.Name).ToArray();
            selYtyp = Math.Clamp(selYtyp, 0, ytypNames.Length - 1);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.ListBox("##ytyps", ref selYtyp, ytypNames, ytypNames.Length, Math.Min(4, ytypNames.Length)))
                selArch = 0;

            var ytyp = info.Ytyps[selYtyp];
            ImGui.TextDisabled($"Archetypes  ({ytyp.Archetypes.Count})");

            ImGui.SetNextItemWidth(-46);
            ImGui.InputTextWithHint("##archsearch", "Search", ref archFilter, 64);
            ImGui.SameLine();
            if (ImGui.SmallButton("clr")) archFilter = "";

            var shown = ytyp.Archetypes
                .Where(a => archFilter.Length == 0 ||
                            (a.Name ?? "").IndexOf(archFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            ImGui.BeginChild("archlist", new Vector2(0, 120), ImGuiChildFlags.Borders);
            for (int i = 0; i < shown.Count; i++)
            {
                var a = shown[i];
                bool isMlo = a is MloArchetype;
                if (isMlo) ImGui.PushStyleColor(ImGuiCol.Text, AccentText());
                if (ImGui.Selectable($"{a.Name}##arch{i}", i == selArch)) selArch = i;
                if (isMlo) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered() && isMlo) ImGui.SetTooltip("MLO interior archetype");
            }
            ImGui.EndChild();

            if (shown.Count == 0) { ImGui.TextDisabled("nothing matches"); return; }
            selArch = Math.Clamp(selArch, 0, shown.Count - 1);
            var arch = shown[selArch];

            if (ImGui.BeginTabBar("archtabs"))
            {
                if (ImGui.BeginTabItem("Archetype")) { archTab = 0; DrawArchetypeFields(arch); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Placement")) { archTab = 1; DrawArchetypePlacement(arch); ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }
        }

        private static void Field(string label, string value)
        {
            ImGui.TextDisabled(label);
            ImGui.SameLine(150);
            ImGui.TextUnformatted(string.IsNullOrEmpty(value) ? "-" : value);
        }

        private void DrawArchetypeFields(Archetype a)
        {
            Field("Type", a is MloArchetype ? "MLO" : "Base");
            Field("Name", a.Name);
            Field("Asset Name", a.AssetName);
            Field("Texture Dictionary", a.TextureDict != 0 ? a.TextureDict.ToString() : null);
            Field("Drawable Dictionary", a.DrawableDict != 0 ? a.DrawableDict.ToString() : null);
            Field("Clip Dictionary", a.ClipDict != 0 ? a.ClipDict.ToString() : null);
            Field("LOD Distance", a.LodDist.ToString("0.00"));
            Field("Bounds min", $"{a.BBMin.X:0.0}, {a.BBMin.Y:0.0}, {a.BBMin.Z:0.0}");
            Field("Bounds max", $"{a.BBMax.X:0.0}, {a.BBMax.Y:0.0}, {a.BBMax.Z:0.0}");

            if (a is MloArchetype mlo)
            {
                ImGui.Separator();
                Field("Rooms", (mlo.rooms?.Length ?? 0).ToString());
                Field("Entities", (mlo.entities?.Length ?? 0).ToString());
                Field("Entity sets", (mlo.entitySets?.Length ?? 0).ToString());
                Field("Portals", (mlo.portals?.Length ?? 0).ToString());
            }
        }

        private void DrawArchetypePlacement(Archetype a)
        {
            var info = scene.MloInfo;
            int placed = info?.Entities.Count(e => e.ArchetypeHash == a.Hash) ?? 0;
            Field("Placed in scene", placed.ToString());

            var lf = scene.Files.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f.Path), a.Name, StringComparison.OrdinalIgnoreCase));
            Field("Editable prop", lf != null ? lf.Label : "not loaded as a prop");
            if (lf != null)
            {
                Field("Lights", scene.Lights.Count(l => scene.OwnerFile(l) == lf).ToString());
                if (ImGui.Button("Make active")) scene.ActiveFile = lf;
            }

            var first = info?.Entities.FirstOrDefault(e => e.ArchetypeHash == a.Hash);
            if (first != null)
                Field("First position", $"{first.Position.X:0.0}, {first.Position.Y:0.0}, {first.Position.Z:0.0}");
        }

        private void DrawShortcutsSection()
        {
            ImGui.TextDisabled("Click a key to rebind, then press the new key (Esc cancels).");
            ImGui.TextDisabled($"Keyboard detected: {AppSettings.DetectedLayout}" +
                (AppSettings.DetectedLayout == "AZERTY" ? " - movement shown as printed (Z Q S D)" : ""));
            foreach (var (id, _, label) in AppSettings.Actions)
            {
                ImGui.Text(label);
                SameCol(200.0f);
                bool capturing = CaptureBindingAction == id;
                var bind = settings.GetBind(id);
                string printed = AppSettings.LayoutKeyLabel(bind);
                string btn = capturing ? "press a key..."
                    : (printed ?? AppSettings.KeyLabel(bind));
                if (capturing) ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ButtonOn);
                if (ImGui.Button($"{btn}##bind{id}", new Vector2(140, 0)))
                {
                    CaptureBindingAction = capturing ? null : id;
                }
                if (capturing) ImGui.PopStyleColor();
            }
            if (ImGui.SmallButton("Reset to defaults"))
            {
                foreach (var (id, def, _) in AppSettings.Actions) settings.Binds[id] = def.ToString();
                settings.Save();
            }
        }

        private int leftTab;

        private void DrawFileSection()
        {
            if (!scene.HasModel)
            {
                ImGui.TextWrapped("Drag & drop .ydr / .yft files here (drop more to add: shell + light proxies).");
            }
            else
            {
                LoadedFile toClose = null;
                if (scene.Files.Count > 6)
                {
                    ImGui.SetNextItemWidth(-70);
                    ImGui.InputTextWithHint("##propfilter", "search props...", ref propFilter, 64);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("clear##pf")) propFilter = "";
                }

                int shown = 0;
                bool scroll = scene.Files.Count > 6;
                if (scroll) ImGui.BeginChild("proplist", new Vector2(0, -58), ImGuiChildFlags.Borders);
                foreach (var f in scene.Files)
                {
                    if (propFilter.Length > 0 &&
                        f.Name.IndexOf(propFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    shown++;
                    int lc = scene.Lights.Count(l => scene.OwnerFile(l) == f);
                    bool isActive = scene.IsFileSelected(f) || scene.ActiveFile == f ||
                                    (scene.ActiveFile == null && scene.OwnerFile(scene.SelectedLight) == f);
                    if (f.FromMlo) ImGui.PushStyleColor(ImGuiCol.Text, AccentText());
                    float rowReserve = UiFit_V32.Reserve(
                        UiFit_V32.TextW($"({lc})"),
                        UiFit_V32.SmallButtonW(f.Visible ? "hide" : "show"),
                        UiFit_V32.SmallButtonW("Save As"));
                    if (ImGui.Selectable($"{(f.Visible ? "" : "[hidden] ")}{f.Label}{(f.Dirty ? " *" : "")}##prop{shown}_{f.Path}",
                        isActive, ImGuiSelectableFlags.None, new Vector2(UiFit_V32.LeadingWidth(rowReserve), 0)))
                    {
                        var pio = ImGui.GetIO();
                        scene.SelectFile(f, pio.KeyCtrl, pio.KeyShift);
                    }
                    if (f.FromMlo) ImGui.PopStyleColor();
                    if (ScrollToActiveProp && isActive) ImGui.SetScrollHereY(0.5f);
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        renameTarget = f;
                        renameText = Path.GetFileNameWithoutExtension(f.Path) ?? "";
                        openRename = true;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        var note = f.FromMlo ? "\nPlaced by the imported MLO." : "";
                        if (f.InstanceCount > 1) note += $"\nPlaced {f.InstanceCount}x - lights are shown at the first placement.";
                        if (f.ReadOnly)
                            note += Scene.HasWritableResource(f)
                                ? "\nFrom the game archives: edit it, then use Save As to write a loose .ydr."
                                : "\nFrom the game archives, and there is nothing here that can be written out.";
                        ImGui.SetTooltip($"{f.Path}\n{lc} light(s). Click to make active: new lights are added here." + note);
                    }
                    ImGui.SameLine();
                    ImGui.TextDisabled($"({lc})");
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"{(f.Visible ? "hide" : "show")}##vis{shown}_{f.Path}"))
                    {
                        f.Visible = !f.Visible;
                    }
                    ImGui.SameLine();
                    bool canWrite = Scene.HasWritableResource(f);
                    if (!canWrite) ImGui.BeginDisabled();
                    if (ImGui.SmallButton($"Save As##saveas{shown}_{f.Path}")) RequestSaveAs?.Invoke(f);
                    if (!canWrite) ImGui.EndDisabled();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(canWrite
                            ? (f.ReadOnly
                                ? "Save As... - this prop came from the game archives; this writes\nit out as a new loose .ydr you can edit and ship."
                                : "Save As... - write this prop to a new .ydr")
                            : "Nothing here can be written to a .ydr.");

                    ImGui.SameLine();
                    if (DangerButton($"del##close{shown}_{f.Path}", Vector2.Zero)) toClose = f;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Scene.IsUnsaved(f)
                            ? "Take this prop out of the scene. IT HAS UNSAVED CHANGES - save it first if you want them."
                            : "Take this prop out of the scene, with its lights. The file on disk is not touched.");

                    if (ImGui.BeginPopupContextItem($"propmenu{shown}_{f.Path}"))
                    {
                        bool unsaved = Scene.IsUnsaved(f);
                        if (ImGui.MenuItem(unsaved ? "Save As..." : "Save", null, false, !f.ReadOnly))
                        {
                            if (unsaved) RequestSaveAs?.Invoke(f);
                            else
                            {
                                try { scene.SaveOne(f); scene.LoadError = null; }
                                catch (Exception ex) { scene.LoadError = ex.Message; }
                            }
                        }
                        if (ImGui.MenuItem("Save As...")) RequestSaveAs?.Invoke(f);
                        if (ImGui.MenuItem(f.Visible ? "Hide" : "Show")) f.Visible = !f.Visible;
                        ImGui.Separator();
                        if (ImGui.MenuItem("Close")) toClose = f;
                        ImGui.EndPopup();
                    }
                }
                if (scroll)
                {
                    ImGui.EndChild();
                    var selNote = scene.SelectedFiles.Count > 1 ? $"  -  {scene.SelectedFiles.Count} selected" : "";
                    ImGui.TextDisabled((propFilter.Length > 0
                        ? $"{shown} of {scene.Files.Count} props match"
                        : $"{scene.Files.Count} props loaded") + selNote);
                    ImGui.TextDisabled("Ctrl/Shift+click: multi-select");
                }
                if (toClose != null)
                {
                    if (scene.ActiveFile == toClose) scene.ActiveFile = null;
                    scene.RemoveFile(toClose);
                }
                ImGui.TextDisabled("Add props and light props from the File menu.");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Double-click any prop in the list to rename it.");
            }

            if (!string.IsNullOrEmpty(scene.LoadError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Danger);
                ImGui.TextWrapped(scene.LoadError);
                ImGui.PopStyleColor();
            }
        }

        public float TopBarHeight { get; private set; }

        private static readonly Vector4 LightWorkspaceColour = new Vector4(0.96f, 0.65f, 0.11f, 1f);
        private static readonly Vector4 MaterialWorkspaceColour = new Vector4(0.694f, 0.196f, 0.914f, 1f);
        private static readonly Vector4 CineWorkspaceColour = new Vector4(0.878f, 0.282f, 0.235f, 1f);
        private static readonly Vector4 ArchiveWorkspaceColour = new Vector4(0.086f, 0.871f, 0.451f, 1f);
        private static readonly Vector4 WorldWorkspaceColour = new Vector4(0.220f, 0.490f, 0.980f, 1f);

        private void DrawWorkspaceTab(string label, Space space)
        {
            NoteWorkspaceTab_P1(space);
            bool on = Workspace == space;
            var col = space == Space.Material ? MaterialWorkspaceColour
                    : space == Space.Cinematic ? CineWorkspaceColour
                    : space == Space.Archive ? ArchiveWorkspaceColour
                    : space == Space.Mlo ? MloWorkspaceColour
                    : space == Space.World ? WorldWorkspaceColour : LightWorkspaceColour;
            WorkspaceTabColour_N4(space, ref col);
            WorkspaceTabColour_P4(space, ref col);
            WorkspaceTabColour_R4(space, ref col);
            WorkspaceTabColour_U6(space, ref col);
            WorkspaceTabColour_V68(space, ref col);

            if (on)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 1, 1, 0.16f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.20f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, 0.24f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1));
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.10f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, 0.16f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.78f, 0.79f, 0.81f, 1f));
            }
            if (ImGui.Button(label) && !on) SwitchWorkspace(space);
            NoteWorkspaceTabRect_T2(space);
            ImGui.PopStyleColor(4);
            { string t4 = null; WorkspaceTabTip_N4(space, ref t4); if (t4 != null) { if (ImGui.IsItemHovered()) ImGui.SetTooltip(t4); return; } }
            { string tp = null; WorkspaceTabTip_P4(space, ref tp); if (tp != null) { if (ImGui.IsItemHovered()) ImGui.SetTooltip(tp); return; } }
            { string tr = null; WorkspaceTabTip_R4(space, ref tr); if (tr != null) { if (ImGui.IsItemHovered()) ImGui.SetTooltip(tr); return; } }
            { string tx = null; WorkspaceTabTip_V68(space, ref tx); if (tx != null) { if (ImGui.IsItemHovered()) ImGui.SetTooltip(tx); return; } }
            { string tu = null; WorkspaceTabTip_U6(space, ref tu); if (tu != null) { if (ImGui.IsItemHovered()) ImGui.SetTooltip(tu); return; } }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    space == Space.Material
                    ? "Material workspace: shaders, textures and material parameters.\n" +
                      "Same scene, same open files - nothing is unloaded. Each workspace\n" +
                      "keeps its own camera, clock and weather."
                    : space == Space.World
                    ? "World workspace: the whole of Los Santos, streamed around the camera.\n" +
                      "Fly anywhere and the map loads in around you at the level of detail the\n" +
                      "game itself would use. Its camera, clock, weather and interior modifier\n" +
                      "are its own - leaving and coming back finds them where you left them."
                    : space == Space.Mlo ? MloWorkspaceTooltip
                    : space == Space.Cinematic
                    ? "Cinematic workspace: the camera, the shot list and every quality and grade\n" +
                      "control, with the Cinematic composite always on. This is where a still or\n" +
                      "a move gets made.\n" +
                      "Same scene, same open files - nothing is unloaded."
                    : "Light workspace: light parameters, MLO import, prop library, YMAP export.\n" +
                      "Same scene, same open files - nothing is unloaded.");
        }

        private void DrawTopBar()
        {
            if (ShellChrome_U27) { TopBarHeight = 0; return; }
            if (!ImGui.BeginMainMenuBar()) { TopBarHeight = 0; return; }
            TopBarHeight = ImGui.GetWindowSize().Y;

            DrawAppearanceMenu_V19();
            ImGui.SameLine(0, 8);
            DrawWorkspaceTab("World", Space.World);
            ImGui.SameLine(0, 2);
            DrawWorkspaceTab("Lights", Space.Light);
            ImGui.SameLine(0, 2);
            DrawWorkspaceTab("Materials", Space.Material);
            ImGui.SameLine(0, 2);
            DrawWorkspaceTab("MLO", Space.Mlo);
            WorkspaceTabs_N4();
            WorkspaceTabs_P4();
            WorkspaceTabs_R4();
            WorkspaceTabs_U6();
            WorkspaceTabs_V68();
            WorkspaceTabsEnd_P1();
            ImGui.SameLine(0, 10);
            ImGui.TextDisabled("|");
            ImGui.SameLine(0, 10);

            if (ImGui.BeginMenu("Project"))
            {
                if (ProjectWindow != null)
                {
                    bool vis = ProjectWindow.Visible;
                    if (ImGui.MenuItem("Project Window", "Ctrl+Shift+P", ref vis)) ProjectWindow.Visible = vis;
                    if (ImGui.MenuItem("New Project")) { ProjectWindow.RequestNewProject = true; ProjectWindow.Visible = true; }
                    if (ImGui.MenuItem("Open Project...")) { ProjectWindow.RequestOpenProject = true; ProjectWindow.Visible = true; }
                    if (ImGui.MenuItem("Save All", null, false, ProjectWindow.Project != null)) ProjectWindow.RequestSaveAll = true;
                    if (ImGui.MenuItem("Close Project", null, false, ProjectWindow.Project != null)) ProjectWindow.RequestCloseProject = true;
                    ImGui.Separator();
                    if (ImGui.MenuItem("New Ymap")) { ProjectWindow.RequestNewYmap = true; ProjectWindow.Visible = true; }
                    if (ImGui.MenuItem("New Ytyp")) { ProjectWindow.RequestNewYtyp = true; ProjectWindow.Visible = true; }
                    if (ImGui.MenuItem("Open Files...")) { ProjectWindow.RequestOpenAny = true; ProjectWindow.Visible = true; }
                    ImGui.Separator();
                    ImGui.TextDisabled(ProjectWindow.Project == null ? "(no map project)"
                        : (ProjectWindow.Project.AnyUnsaved ? "*" : "") + ProjectWindow.Project.Name);
                    ImGui.Separator();
                }
                if (ImGui.BeginMenu("Lighting session (.rlep)"))
                {
                    if (ImGui.MenuItem("New session")) RequestNewProject?.Invoke();
                    if (ImGui.MenuItem("Open session...")) RequestOpenProject?.Invoke();
                    if (ImGui.MenuItem("Save session")) RequestSaveProject?.Invoke(false);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Save every open model, the imported MLO, your folders,\n" +
                                         "the timecycle and the current view into one .rlep file.");
                    if (ImGui.MenuItem("Save session as...")) RequestSaveProject?.Invoke(true);
                    ImGui.Separator();
                    ImGui.TextDisabled(string.IsNullOrEmpty(ProjectName) ? "(no session)" : ProjectName);
                    ImGui.EndMenu();
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Open...")) RequestOpenFile?.Invoke();
                if (ImGui.MenuItem("Add prop...")) RequestAddFile?.Invoke();
                if (ImGui.MenuItem("New light prop...")) RequestNewLightProxy?.Invoke();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Asks for a name, then creates an empty light-only prop: a drawable that\n" +
                                     "carries nothing but lights, plus its archetype.");
                if (ImGui.MenuItem("Load YTD...")) RequestOpenYtd?.Invoke();
                ImGui.Separator();
                bool canSave = scene.HasModel;
                if (ImGui.MenuItem(scene.Files.Count > 1 ? "Save all" : "Save", "Ctrl+S", false, canSave))
                    RequestSave?.Invoke();
                if (ImGui.MenuItem("Save As...", null, false, canSave)) RequestSaveAs?.Invoke(null);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Write the active prop (highlighted in the Props tab) to a new file.");
                ImGui.Separator();
                if (ImGui.MenuItem("Open files with this editor (register file types)")) RequestRegisterFileTypes?.Invoke(true);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Associates .ydr .yft .ydd .ytd .ytyp .ymap .ybn .cwproj .rlep with this exe for your\n" +
                                     "Windows account, so Open With / double-click brings them here.");
                if (ImGui.MenuItem("Remove those file associations")) RequestRegisterFileTypes?.Invoke(false);
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Edit"))
            {
                if (WorldMode)
                {
                    if (ImGui.MenuItem("Undo", "Ctrl+Z", false, WorldHistory?.CanUndo == true)) RequestWorldUndo = true;
                    if (ImGui.MenuItem("Redo", "Ctrl+Y", false, WorldHistory?.CanRedo == true)) RequestWorldRedo = true;
                }
                else
                {
                    bool canUndo = scene.CanUndo, canRedo = scene.CanRedo;
                    if (ImGui.MenuItem("Undo", "Ctrl+Z", false, canUndo)) scene.Undo();
                    if (ImGui.MenuItem("Redo", "Ctrl+Y", false, canRedo)) scene.Redo();
                }
                if (MaterialMode)
                {
                    ImGui.Separator();
                    bool mu = Materials.CanUndo, mr = Materials.CanRedo;
                    if (ImGui.MenuItem(mu ? $"Undo material: {Materials.UndoLabel}" : "Undo material",
                        "Ctrl+Shift+Z", false, mu)) Materials.Undo();
                    if (ImGui.MenuItem("Redo material", "Ctrl+Shift+Y", false, mr)) Materials.Redo();
                }
                ImGui.EndMenu();
            }

            if (ForceOpenHeader != null &&
                "Shortcuts".Contains(ForceOpenHeader, StringComparison.OrdinalIgnoreCase))
                ImGui.OpenPopup("Shortcuts");
            if (ImGui.BeginMenu("Shortcuts"))
            {
                DrawShortcutsSection();
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Help"))
            {
                DrawVersionItems_U30();
                if (ImGui.MenuItem("Tutorial")) openTutorial = true;
                DrawMirrorJokeMenuItem_S6();
                DrawHelpLogItems_U21();
                ImGui.EndMenu();
            }
            DrawLogBadge_U21();
            DrawUpdateBadge_U30();

            DrawBridgeMenu_U12();

            if (!string.IsNullOrEmpty(ProjectName) || scene.HasModel)
            {
                var label = string.IsNullOrEmpty(ProjectName)
                    ? scene.FileName
                    : ProjectName + (scene.Dirty ? " *" : "");
                float w = ImGui.CalcTextSize(label).X;
                float x = ImGui.GetWindowWidth() - w - 16;
                if (x > ImGui.GetCursorPosX() + 24)
                {
                    ImGui.SameLine(x);
                    ImGui.TextDisabled(label);
                }
            }
            ImGui.EndMainMenuBar();
        }

        private static void ViewGroup(string label)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(label.ToUpperInvariant());
            ImGui.Separator();
        }

        private static readonly (string Name, Action<AppSettings.CinematicPrefs> Apply)[] CinePresets =
        {
            ("Neutral", c =>
            {
                c.Bloom = 0.55f; c.BloomThreshold = 1.05f; c.BloomSpread = 1.0f; c.BloomAnamorphic = 0f;
                c.Halation = 0f; c.Vignette = 0.85f; c.Grain = 0f; c.ChromAberration = 0f;
                c.Sharpen = 0.25f; c.Contrast = 1.0f; c.Saturation = 1.0f;
                c.Temperature = 0f; c.Tint = 0f; c.Letterbox = 0f;
            }),
            ("Filmic", c =>
            {
                c.Bloom = 0.7f; c.BloomThreshold = 0.95f; c.BloomSpread = 1.3f; c.BloomAnamorphic = 0.25f;
                c.Halation = 0.18f; c.Vignette = 1.0f; c.Grain = 0.35f; c.ChromAberration = 0.35f;
                c.Sharpen = 0.2f; c.Contrast = 1.08f; c.Saturation = 0.95f;
                c.Temperature = 0.06f; c.Tint = 0.02f; c.Letterbox = 0.105f;
            }),
            ("Anamorphic", c =>
            {
                c.Bloom = 0.9f; c.BloomThreshold = 1.1f; c.BloomSpread = 1.6f; c.BloomAnamorphic = 0.85f;
                c.Halation = 0.22f; c.Vignette = 1.0f; c.Grain = 0.25f; c.ChromAberration = 0.6f;
                c.Sharpen = 0.15f; c.Contrast = 1.05f; c.Saturation = 1.05f;
                c.Temperature = -0.08f; c.Tint = 0.04f; c.Letterbox = 0.115f;
            }),
            ("Noir", c =>
            {
                c.Bloom = 0.45f; c.BloomThreshold = 1.3f; c.BloomSpread = 0.9f; c.BloomAnamorphic = 0f;
                c.Halation = 0f; c.Vignette = 1.0f; c.Grain = 0.6f; c.ChromAberration = 0f;
                c.Sharpen = 0.35f; c.Contrast = 1.25f; c.Saturation = 0.05f;
                c.Temperature = -0.05f; c.Tint = 0f; c.Letterbox = 0.12f;
            }),
            ("Warm interior", c =>
            {
                c.Bloom = 0.8f; c.BloomThreshold = 0.85f; c.BloomSpread = 1.4f; c.BloomAnamorphic = 0.1f;
                c.Halation = 0.3f; c.Vignette = 0.9f; c.Grain = 0.15f; c.ChromAberration = 0.2f;
                c.Sharpen = 0.2f; c.Contrast = 1.04f; c.Saturation = 1.12f;
                c.Temperature = 0.3f; c.Tint = 0.06f; c.Letterbox = 0f;
            }),
            ("Clean showcase", c =>
            {
                c.Bloom = 0.4f; c.BloomThreshold = 1.25f; c.BloomSpread = 1.0f; c.BloomAnamorphic = 0f;
                c.Halation = 0.05f; c.Vignette = 0.4f; c.Grain = 0f; c.ChromAberration = 0f;
                c.Sharpen = 0.45f; c.Contrast = 1.02f; c.Saturation = 1.05f;
                c.Temperature = 0f; c.Tint = 0f; c.Letterbox = 0f;
            }),
        };

        private int cinePreset;

        private void DrawCinematicSection()
        {
            var c = Cine;

            if (RenderMode != 7)
            {
                ImGui.TextDisabled("Cinematic shading is off - none of this is being drawn.");
                if (ImGui.Button("Turn Cinematic on", new Vector2(-1, 0))) RenderMode = 7;
                ImGui.Spacing();
            }

            ViewGroup("Look");
            ImGui.SetNextItemWidth(-140);
            var presetNames = new string[CinePresets.Length];
            for (int i = 0; i < CinePresets.Length; i++) presetNames[i] = CinePresets[i].Name;
            if (ImGui.Combo("Preset", ref cinePreset, presetNames, presetNames.Length))
            {
                CinePresets[Math.Clamp(cinePreset, 0, CinePresets.Length - 1)].Apply(c);
                settings.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A whole grade at once. Everything below stays editable\n" +
                                 "afterwards - a preset is a starting point, not a lock.");

            ViewGroup("Edges");
            ImGui.SetNextItemWidth(-140);
            int aa = c.AntiAlias;
            if (ImGui.Combo("Anti-aliasing", ref aa, CineAaLabels, CineAaLabels.Length))
            {
                c.AntiAlias = aa;
                settings.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "MSAA keeps coverage per sample while the triangle is being drawn, so a\n" +
                    "silhouette is never turned into a staircase in the first place. It is the\n" +
                    "only one of the two that helps a railing, a wire or the corner of a doorway.\n\n" +
                    "FXAA works on the finished image and guesses where the edge was. It costs\n" +
                    "almost nothing and it also smooths edges MSAA cannot see - ones that came\n" +
                    "from a texture or a shader - at the price of softening fine detail.\n\n" +
                    "8x is roughly twice the memory of 4x for a difference you have to look for.");
            if (MsaaGranted > 0 && MsaaGranted != CineSampleCount)
                ImGui.TextDisabled($"This GPU granted {MsaaGranted}x.");
            ImGui.SetNextItemWidth(-140);
            if (UiSlider_U28.Float("Sharpen", ref c.Sharpen, 0.0f, 1.0f, "%.2f")) { }
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Unsharp mask over the finished frame. A resolve and the\n" +
                                 "reduced-resolution buffers both soften an image slightly;\n" +
                                 "this puts the crispness back without more samples anywhere.");

            ViewGroup("Surfaces");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Flatten bevels", ref c.BevelFlatten, 0.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "GTA fakes the chamfer along a wall panel's edge in the BUMP MAP rather\n" +
                    "than in the geometry. At the distance a player sees it that is a good\n" +
                    "trick; with a camera close to the wall it is a painted-on groove that\n" +
                    "catches light from a shape that is not there.\n\n" +
                    "This limits how far a bumped normal is allowed to tilt away from the\n" +
                    "surface it sits on. A chamfer tilts it a long way and gets pulled flat;\n" +
                    "brick, plaster and grain tilt it barely at all and are left alone. Turn\n" +
                    "it up until the fake edges go and the texture is still there.");

            ViewGroup("Occlusion");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Strength##ao", ref c.AoStrength, 0.0f, 1.5f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How dark the ray-marched contact shadows go. 0 turns them off.");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Reach##ao", ref c.AoRadius, 0.05f, 3.0f, "%.2f m");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How far the rays travel before giving up, in metres.\n" +
                                 "Small values catch only where surfaces meet; large ones\n" +
                                 "darken whole corners of a room.");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Quality##ao", ref c.AoQuality, 0.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Rays per pixel, and steps along each. The result is blurred\n" +
                                 "afterwards either way, so this mostly decides how much\n" +
                                 "structure survives that blur - and how much the frame costs.");

            ViewGroup("Reflections");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Screen-space", ref c.Ssr, 0.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "Walks each surface's reflection ray through the depth buffer and shows\n" +
                    "what is already on screen where it lands. A polished floor picks up the\n" +
                    "lights above it; wet ground picks up the room.\n\n" +
                    "It can only reflect what is IN the frame, so a reflection fades out at\n" +
                    "the edges of the screen and behind anything in the way. That is the\n" +
                    "technique, not a bug - and Ambient fallback below is what keeps it from\n" +
                    "simply disappearing where it runs out.");
            if (c.Ssr > 0.001f)
            {
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Spread over angle", ref c.SsrFresnel, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("0 = every surface in the room is a mirror, whatever angle it\n" +
                                     "is seen from. 1 = only the grazing angles reflect, the way\n" +
                                     "an unpolished surface behaves.\n\n" +
                                     "There is no roughness channel anywhere in a .ydr, so nothing\n" +
                                     "in the file can decide this - turn it down to make the props\n" +
                                     "reflect properly.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Roughness##ssr", ref c.SsrBlur, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How soft the reflection is. Zero is a mirror; anything above\n" +
                                     "it is a polish, and it also cleans up the ray march's noise.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Ambient fallback", ref c.SsrSky, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("What a ray that finds nothing shows instead of nothing - the\n" +
                                     "room's own ambient, from the timecycle. A floor that mirrors\n" +
                                     "the room in the middle of the frame and goes flat matte at\n" +
                                     "the edges looks broken; this is what stops that.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Ray distance", ref c.SsrDistance, 1.0f, 60.0f, "%.0f m");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Thickness", ref c.SsrThickness, 0.02f, 2.0f, "%.2f m");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The depth buffer says where surfaces are, not how solid they\n" +
                                     "are. This is how far behind one a ray may pass and still\n" +
                                     "count as having hit it. Too small and reflections break up;\n" +
                                     "too large and they smear behind objects.");
            }

            ViewGroup("Depth of field");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Amount##dof", ref c.Dof, 0.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("One plane in focus, everything else spread into a disc - the\n" +
                                 "single strongest cue that a picture was taken rather than\n" +
                                 "generated.");
            if (c.Dof > 0.001f)
            {
                bool autoFocus = c.DofAutoFocus;
                if (ImGui.Checkbox("Auto focus", ref autoFocus))
                {
                    c.DofAutoFocus = autoFocus;
                    settings.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Casts a ray down the middle of the screen and focuses on\n" +
                                     "whatever it hits, every frame. Off, the distance below is\n" +
                                     "used as it stands.");
                if (!autoFocus)
                {
                    ImGui.SetNextItemWidth(-140);
                    UiSlider_U28.Float("Focus distance", ref c.DofFocus, 0.3f, 120.0f, "%.1f m");
                    if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                }
                else ImGui.TextDisabled($"Focused at {c.DofFocus:0.0} m");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("In-focus depth", ref c.DofRange, 0.0f, 8.0f, "%.2f m");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("A band either side of the focus that stays completely sharp.\n" +
                                     "Without it exactly one distance in the picture is sharp and\n" +
                                     "framing a subject against it is guesswork.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Aperture", ref c.DofAperture, 0.05f, 3.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How fast everything off the focus plane falls apart.\n" +
                                     "Wide open on a real lens, the depth in focus is inches.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Bokeh size", ref c.DofMaxRadius, 2.0f, 32.0f, "%.0f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How wide a fully defocused point of light spreads.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Bokeh highlights", ref c.DofBokeh, 0.0f, 6.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How hard a bright point is pushed into a disc of its own\n" +
                                     "rather than averaged into a haze. This is the difference\n" +
                                     "between blur and bokeh - turn it up and the lamps behind\n" +
                                     "your subject become circles.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Bokeh stretch", ref c.DofStretch, 0.3f, 3.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Above 1 the bokeh is an OVAL rather than a disc. A spherical\n" +
                                     "anamorphic squeezes the image on the way in and the print\n" +
                                     "stretches it back, so a circle in the lens comes out an oval -\n" +
                                     "which is why anamorphic highlights are the shape they are.\n\n" +
                                     "Below 1 squeezes the other way, which no lens does.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Bokeh swirl", ref c.DofRadial, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Rolls the bokeh round towards the corners, so the discs become\n" +
                                     "arcs the further they are from the middle. It is what a fast\n" +
                                     "vintage lens does with its field curvature, and the reason\n" +
                                     "old portrait glass sells for what it does.\n\n" +
                                     "Nothing happens at the centre of the frame - there is no\n" +
                                     "direction to be radial about there.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Iris blades", ref c.DofBlades, 0.0f, 9.0f,
                    c.DofBlades < 3.0f ? "round" : "%.0f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("A real lens stopped down leaves the SHAPE of its aperture in\n" +
                                     "every out-of-focus highlight. Six blades gives the hexagons\n" +
                                     "you see in most photographs; round is a lens wide open.");
            }

            ViewGroup("Bloom");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Intensity##bloom", ref c.Bloom, 0.0f, 2.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Threshold##bloom", ref c.BloomThreshold, 0.2f, 4.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Brightness a pixel has to reach before it glows.\n" +
                                 "Lower it and the whole image blooms; raise it and only\n" +
                                 "genuine light sources do.");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Spread##bloom", ref c.BloomSpread, 0.2f, 3.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Anamorphic", ref c.BloomAnamorphic, 0.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Stretches the glow sideways, the way a spherical lens does.\n" +
                                 "The wide streak across a bright light is the most\n" +
                                 "recognisable thing about how film renders one.");
            ViewGroup("Halation");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Strength##halation", ref c.Halation, 0.0f, 1.5f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The warm ring film puts around a bright window: light that got\n" +
                                 "through the emulsion, scattered off the backing and came back -\n" +
                                 "reddest, because the red-sensitive layer sits deepest.");
            if (c.Halation > 0.001f)
            {
                var htint = new Vector3(c.HalationR, c.HalationG, c.HalationB);
                ImGui.SetNextItemWidth(-140);
                if (ImGui.ColorEdit3("Colour##halation", ref htint))
                {
                    c.HalationR = htint.X; c.HalationG = htint.Y; c.HalationB = htint.Z;
                }
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Red-orange is what colour film does. Cyan is what a bleached\n" +
                                     "print does. Anything else is yours.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Threshold##halation", ref c.HalationThreshold, 0.0f, 3.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How bright a thing has to be before it halos at all. Its own,\n" +
                                     "separate from the bloom threshold: halation is what the very\n" +
                                     "brightest parts of a frame do, and letting everything that\n" +
                                     "blooms also halo turns the warm ring into a warm cast.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Falloff##halation", ref c.HalationSoftness, 0.01f, 2.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How gradually it comes on across that threshold. A hard cut\n" +
                                     "draws a visible outline around whatever crossed the line.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Tint amount##halation", ref c.HalationSaturation, 0.0f, 2.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("0 leaves the halo white - a plain second bloom. 1 is the\n" +
                                     "colour above as set. Past 1 pushes beyond it, which no film\n" +
                                     "ever did and which occasionally looks right anyway.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Spread##halation", ref c.HalationSpread, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How far past the bloom the halo reaches. It is WIDER than the\n" +
                                     "glow that made it - the light has crossed the film base twice\n" +
                                     "by the time it comes back out.");
            }

            ViewGroup("Grade");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Contrast", ref c.Contrast, 0.5f, 2.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Saturation", ref c.Saturation, 0.0f, 2.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Temperature", ref c.Temperature, -1.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Cool to warm, along the blue-orange axis.");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Tint##grade", ref c.Tint, -1.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Green to magenta - what corrects the cast\n" +
                                                        "fluorescent light leaves on a scene.");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Lift", ref c.Lift, -0.25f, 0.25f, "%.3f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The shadows moved without touching the highlights. Lifting them\n" +
                                 "slightly is what gives a print its milky black.");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Gain", ref c.Gain, 0.5f, 2.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Bleach bypass", ref c.Bleach, 0.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The print developed with the silver left in: hard contrast,\n" +
                                 "colour mostly gone, blacks that go all the way. Half the war\n" +
                                 "films of the last thirty years, in one slider.");

            ViewGroup("Vignette");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Amount##vignette", ref c.Vignette, 0.0f, 1.5f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (c.Vignette > 0.001f)
            {
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Roundness##vignette", ref c.VignetteRoundness, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("0 follows the shape of the frame, which on a wide window\n" +
                                     "means an ellipse. 1 stays a circle, which is what the lens\n" +
                                     "itself actually does.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Softness##vignette", ref c.VignetteSoftness, 0.1f, 2.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How abruptly the corners give way. Low is a wide gentle\n" +
                                     "shade over most of the frame; high is dark corners only.");
            }

            ViewGroup("Grain");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Amount##grain", ref c.Grain, 0.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (c.Grain > 0.001f)
            {
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Size##grain", ref c.GrainSize, 0.5f, 8.0f, "%.1f px");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Screen pixels per grain. Grain belongs to the film stock, not\n" +
                                     "to the resolution - which is why this is in pixels and why a\n" +
                                     "one-pixel grain vanishes entirely in an 8K render.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Colour##grain", ref c.GrainColour, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("0 = monochrome silver, as black and white stock. 1 = each\n" +
                                     "channel drawn separately, as colour dye clouds.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("In shadows", ref c.GrainShadow, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How much more grain there is in the dark than in the light.\n" +
                                     "Film puts most of it in the shadows; an even layer across\n" +
                                     "the frame reads as a dirty screen instead.");
            }

            ViewGroup("Lens & frame");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Chromatic aberration", ref c.ChromAberration, 0.0f, 2.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A lens does not bring every wavelength to the same point, and\n" +
                                 "the error grows towards the corners. Radial, not a flat shift.");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Edge smear", ref c.EdgeBlur, 0.0f, 1.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("An anamorphic lens does not resolve its corners the way it\n" +
                                 "resolves its centre, and the softening runs ALONG the radius -\n" +
                                 "which is why it reads as a lens rather than as a blur.");
            if (c.EdgeBlur > 0.001f)
            {
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Smear starts at", ref c.EdgeBlurStart, 0.0f, 0.95f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Distance from the centre, 0..1. Nothing inside it is touched,\n" +
                                     "so the subject stays sharp and only the frame gives way.");
                ImGui.SetNextItemWidth(-140);
                UiSlider_U28.Float("Horizontal bias", ref c.EdgeBlurElongation, 0.0f, 1.0f, "%.2f");
                if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("0 is purely radial. 1 drags the smear towards horizontal,\n" +
                                     "which is the asymmetry a spherical anamorphic actually has.");
            }
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Letterbox", ref c.Letterbox, 0.0f, 0.2f,
                c.Letterbox < 0.0005f ? "off" : "%.3f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Bar height as a fraction of the frame. 0.105 is about 2.39:1\n" +
                                 "on a 16:9 window - composition, not an effect.");
            ImGui.SetNextItemWidth(-140);
            UiSlider_U28.Float("Dither", ref c.Dither, 0.0f, 2.0f, "%.2f");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Under a single code value of noise, added last. An 8-bit image\n" +
                                 "cannot hold a smooth gradient across a dark wall - it holds flat\n" +
                                 "steps with visible edges - and this turns them back into a\n" +
                                 "gradient. Below the threshold of being seen; the banding is not.");

            ViewGroup("Render to file");
            ImGui.SetNextItemWidth(-140);
            int rs = Math.Clamp(c.RenderSize, 0, RenderSizeLabels.Length - 1);
            if (ImGui.Combo("Resolution", ref rs, RenderSizeLabels, RenderSizeLabels.Length))
            {
                c.RenderSize = rs;
                settings.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "Renders the frame OFF SCREEN at this size, not a grab of the window. The\n" +
                    "whole Cinematic pipeline runs at the full resolution - the occlusion, the\n" +
                    "reflections, the bokeh - so a 4K render holds four times the detail of a\n" +
                    "1080p window rather than the same picture scaled up.\n\n" +
                    "Nothing from the interface is in it. Memory grows with the square of the\n" +
                    "size; if the card cannot allocate at the sample count you asked for, the\n" +
                    "render steps down rather than failing.");
            bool ss = c.Supersample > 1;
            if (ImGui.Checkbox("Supersample", ref ss))
            {
                c.Supersample = ss ? 2 : 1;
                settings.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "Renders at twice the width and height and averages four samples into every\n" +
                    "output pixel. Unlike MSAA it does not anti-alias EDGES, it anti-aliases\n" +
                    "EVERYTHING - a specular glint too small to land on a pixel, a normal map\n" +
                    "shimmering at distance, whatever noise the occlusion and reflection marches\n" +
                    "left behind. The noise falls with the square root of the sample count and the\n" +
                    "picture does not.\n\n" +
                    "It is four times the work and four times the memory, so it is dropped\n" +
                    "automatically when the internal frame would not fit - an 8K output already\n" +
                    "renders at 8K.");
            if (ImGui.Button("Render image...", new Vector2(-1, 0))) RequestRender = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{RenderSizeLabels[rs]}, at the current camera and settings.");

            ImGui.Spacing();
            if (ImGui.Button("Reset Cinematic", new Vector2(-1, 0)))
            {
                settings.Cinematic = new AppSettings.CinematicPrefs();
                settings.Save();
            }
        }

        public static readonly string[] RenderSizeLabels =
        {
            "Window size", "1080p", "1440p", "4K",
            "5K", "8K",
        };
        private static readonly int[] renderSizeW = { 0, 1920, 2560, 3840, 5120, 7680 };
        private static readonly int[] renderSizeH = { 0, 1080, 1440, 2160, 2880, 4320 };

        public (int W, int H) RenderSize
        {
            get
            {
                int i = Math.Clamp(Cine.RenderSize, 0, renderSizeW.Length - 1);
                return (renderSizeW[i], renderSizeH[i]);
            }
        }

        public bool RequestRender;

        public int MsaaGranted;

        private void SameCol(float column = 180.0f)
        {
            float prevEnd = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetScrollX() + 18.0f;
            ImGui.SameLine(Math.Max(column, prevEnd));
        }

        private void DrawWorldOptions()
        {
            DrawWorldOptions_M3();
        }

        partial void DrawGeneralExtras_World();
        partial void DrawGeneralExtras_ScriptIpls();
        partial void DrawGeneralExtras_ModsDlc_V21();

        partial void DrawRenderExtras_Materials();
        partial void DrawRenderExtras_Sky();
        partial void DrawRenderExtras_World();
        partial void DrawRenderExtras_P3();
        partial void DrawHelpersExtras_World();
        partial void DrawHelpersExtras_Selection();
        partial void DrawLightingExtras_Sky();
        partial void DrawLightingExtras_World();
        partial void DrawWorldSelectionExtras_Selection(ref bool handled);
        partial void DrawCollisionUnderCursor_Selection();
        partial void DrawHelpersExtras_SpaceData();
        partial void DrawHelpersExtras_H3();
        partial void DrawWorldSelectionExtras_SpaceData(ref bool handled);
        partial void DrawMloCreatorSection_H5();
        partial void DrawCameraExtras_Gizmo();
        partial void DrawHelpersExtras_I4();
        partial void DrawEditLightButton_J2();
        partial void DrawWorldNoSelectionHint_J2(ref bool handled);
        partial void DrawLightingExtras_J3();
        partial void DrawInteriorTimecycleRow_K2();
        partial void DrawWorldMapTabExtras_J3();

        private void DrawViewSection()
        {
            DrawViewLights();
            DrawViewShadows();
            DrawViewOverlays();
            DrawViewCamera();
            DrawViewAdvanced_M3();
        }

        private void DrawViewLights()
        {

            if (!MaterialMode)
            {
                ViewGroup("Lights");
                ImGui.Checkbox("Flashiness", ref AnimateFlashiness);
                SameCol();
                ImGui.Checkbox("Day/night ambient", ref DayNightAmbient);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Scale scene ambient with the preview hour (bright day / dark night)");
            }

        }

        private void DrawViewShadows()
        {
            ViewGroup("Shadows");
            ImGui.Checkbox("Shadows", ref ShowShadows);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Shadow casting for a few lights at a time (8 spots + 4 point/capsule).\nSoftness follows each light's Shadow blur value.");
            if (!MaterialMode)
            {
                SameCol();
                ImGui.Checkbox("Use flags", ref RespectShadowFlags);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("On: a light with no Cast Shadows flag never casts, whatever the\n" +
                                     "shadow mode says. No Specular and Don't Light Alpha are always live.\n" +
                                     "Off: ignore the flags and shadow whatever the mode picks.");
                if (ShowShadows)
                {
                    ImGui.SetNextItemWidth(-140);
                    ImGui.Combo("Cast from", ref ShadowMode, "Nearest\0Flagged\0Selected\0");
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(
                            "Only a few lights can cast shadows at once (8 spot + 4 point).\n\n" +
                            "Nearest: whichever are closest to you.\n" +
                            "Flagged: just lights with a Cast Shadows flag (game behaviour).\n" +
                            "Selected: only the light(s) you have selected.\n\n" +
                            "All three hold their slots long enough not to flicker as you walk.");
                    if (ShadowMode == 2 && scene.SelectedIndex < 0)
                        ImGui.TextDisabled("select a light to see its shadows");
                }
            }

        }

        private void DrawViewOverlays()
        {
            ViewGroup("Overlays");
            ImGui.Checkbox("Gizmo", ref ShowGizmos);
            SameCol();
            ImGui.Checkbox("All gizmos", ref ShowAllGizmos);
            ImGui.Checkbox("Grid", ref ShowGrid);
            SameCol();
            ImGui.Checkbox("Markers", ref ShowMarkers);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Clickable dot at every light position");
            ImGui.Checkbox("Coronas", ref ShowCoronas);
            SameCol();
            ImGui.Checkbox("Volumes", ref ShowVolumes);
            if (ShowVolumes && VolumeOverflow > 0)
                ImGui.TextDisabled($"{VolumeOverflow} more volumes than can be drawn - showing the nearest");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Approximate volumetric glow for lights with flag 12 (Draw Volume)");
            ImGui.Checkbox(MaterialMode ? "Material picking" : "Entity picking", ref EntityPicking);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(MaterialMode
                    ? "RIGHT-click a surface in the viewport to select the material on it.\n" +
                      "Lights still take priority under the cursor, so a light marker\n" +
                      "can always be grabbed."
                    : "RIGHT-click a prop in the viewport to select it, instead of finding\n" +
                      "it in the Props list. The selected prop is outlined in the viewport.\n" +
                      "Lights still take priority under the cursor.");

        }

        private void DrawViewCamera()
        {
            ViewGroup("Camera");
            DrawShadingCombo();
            DrawCameraKnobs();
        }

        private void DrawShadingCombo()
        {
            ImGui.SetNextItemWidth(-140);
            if (MaterialMode)
            {
                var modes = new[] { 0, 1, 2, 5, 6, 8, VertexColourModeFirst };
                var labels = new[] { "RAGE", "Unlit", "Normals", "Lighting only", "Specular only", "Wireframe", "Vertex colours" };
                int sel = Array.IndexOf(modes, ShadingComboMode_O2(RenderMode));
                if (sel < 0) sel = 0;
                if (ImGui.Combo("Shading", ref sel, labels, labels.Length)) RenderMode = modes[sel];
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(ShadingTooltip);
                DrawVertexColourChannel_O2();
            }
            else
            {
                var (modes, labels) = ShadingChoices_U28();
                int sel = Array.IndexOf(modes, ShadingComboMode_O2(RenderMode));
                if (sel < 0) sel = 0;
                if (ImGui.Combo("Shading", ref sel, labels, labels.Length)) RenderMode = modes[sel];
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(ShadingTooltip);
                DrawVertexColourChannel_O2();
            }
        }

        private void DrawCameraKnobs()
        {
            if (!WorldMode)
            {
                float snap = settings.RotateSnapDeg;
                ImGui.SetNextItemWidth(-140);
                if (UiSlider_U28.Float("Rotate snap", ref snap, 0.0f, 45.0f, snap < 0.01f ? "off" : "%.0f deg"))
                {
                    settings.RotateSnapDeg = snap;
                    gizmo.RotateSnapDeg = snap;
                }
                DrawFovRow(-140);
            }

            float sens = settings.CameraSensitivity;
            ImGui.SetNextItemWidth(-140);
            if (UiSlider_U28.Float("Sensitivity", ref sens, 0.001f, 0.02f, "%.4f"))
                settings.CameraSensitivity = sens;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Radians of turn per pixel of mouse movement.\nThe default is 0.005.");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();

            float smooth = settings.CameraSmoothing;
            ImGui.SetNextItemWidth(-140);
            if (UiSlider_U28.Float("Smoothing", ref smooth, 0.0f, 60.0f, smooth < 0.5f ? "off" : "%.0f /s"))
                settings.CameraSmoothing = smooth < 0.5f ? 0.0f : smooth;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How fast the view catches up with the mouse and the wheel.\n" +
                                 "Off (0): immediate - the view is exactly where the mouse put it, every frame.\n" +
                                 "10 is the default; the higher the value the less the camera trails.");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();

            DrawCameraExtras_Gizmo();
        }

        private void DrawFovRow(float width)
        {
            float fov = settings.FovDeg;
            ImGui.SetNextItemWidth(width);
            if (UiSlider_U28.Float("FOV", ref fov, Rendering.Camera.MinFovDeg, Rendering.Camera.MaxFovDeg, "%.0f deg"))
                settings.FovDeg = Math.Clamp(fov, Rendering.Camera.MinFovDeg, Rendering.Camera.MaxFovDeg);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Vertical field of view. Lower zooms in like a long lens (10 = 6x closer\n" +
                                 "than 54), higher pulls wide. The game's own is about 50-55.");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();
        }

        private void DrawBackfaceAndVsync(bool showCount)
        {
            bool backface = settings.BackfaceCulling;
            if (ImGui.Checkbox("Backface culling", ref backface))
            {
                settings.BackfaceCulling = backface;
                Rendering.CommonStates.BackfaceCulling = backface;
                settings.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Drop triangles facing away from you, the way the game draws.\n" +
                                 "Turn this off to see inside a shell, or to check a model whose\n" +
                                 "winding is inside out.");
            if (backface)
            {
                ImGui.SetNextItemWidth(-140);
                int mode = settings.BackfaceMode;
                if (ImGui.Combo("Two-sided", ref mode,
                        "Game\0Flat only\0None\0"))
                {
                    settings.BackfaceMode = mode;
                    Rendering.CommonStates.BackfaceMode = mode;
                    settings.Save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(
                        "What still keeps BOTH faces while culling is on.\n\n" +
                        "Materials the game draws two-sided: foliage, cloth, glass and the alpha\n" +
                        "presets, plus anything modelled as a flat sheet. This is how the game\n" +
                        "renders and the right answer for judging a scene.\n\n" +
                        "Only flat sheets: ignore the material and go by the geometry alone -\n" +
                        "useful when a converted asset claims a two-sided preset it doesn't need.\n\n" +
                        "Nothing: cull every back face without exception. The strictest view of\n" +
                        "how a mesh is actually wound; foliage will lose half its leaves.");
                if (showCount) ImGui.TextDisabled($"{TwoSidedMeshCount} of {TotalMeshCount} meshes are two-sided");
            }

            bool vsync = settings.VSync;
            if (ImGui.Checkbox("VSync", ref vsync))
            {
                settings.VSync = vsync;
                settings.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Sync to the monitor refresh (removes tearing, paces the frame rate).\nOff = uncapped.");
        }

        private void UndoOnActivate()
        {
            if (ImGui.IsItemActivated()) scene.PushUndo();
        }

        private void MarkDirty(bool changed)
        {
            if (changed) scene.Dirty = true;
        }

        private void DrawLightEditor(LightAttributes l)
        {
            var inst = scene.GetInstance(l);

            if (scene.SelectedIndices.Count > 1 && !lightEditorExternal)
            {
                ImGui.TextColored(AccentText(),
                    $"Editing {scene.SelectedIndices.Count} lights together - every change applies to all of them");
            }
            int igroup = scene.InstanceGroup(l);
            if (igroup > 0)
            {
                ImGui.TextColored(UiTheme.Ok,
                    $"Instance group i{igroup}: edits sync to all its members");
                ImGui.SameLine();
                if (ImGui.SmallButton("Make unique"))
                {
                    scene.PushUndo();
                    scene.MakeSelectedUnique();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Unlink the selected light(s) from their instance group");
            }

            var paramsBefore = Scene.CloneLight(l);

            DrawLightSettingsClipboardRow_O3(l);

            ImGui.Spacing();
            if (!ImGui.BeginTabBar("lighttabs")) return;

            if (ImGui.BeginTabItem("Main"))
            {

            if (Header("Core", true))
            {
                int typeIdx = LightDefs.TypeToIndex((byte)l.Type);
                if (ImGui.Combo("Type", ref typeIdx, "Point\0Spot\0Capsule\0"))
                {
                    scene.PushUndo();
                    l.Type = (LightType)LightDefs.IndexToType(typeIdx);
                    if (l.Type == LightType.Spot && l.ConeOuterAngle <= 0.01f)
                    {
                        l.ConeInnerAngle = 10; l.ConeOuterAngle = 35;
                    }
                    scene.Dirty = true;
                }

                var pos = ToNum(l.Position);
                bool ch = ImGui.DragFloat3("Position", ref pos, 0.005f);
                UndoOnActivate();
                if (ch) { l.Position = ToDx(pos); MarkDirty(true); }
                if (l.BoneId != 0)
                {
                    ImGui.SameLine(); ImGui.TextDisabled("(bone-local)");
                }

                var col = new Vector3(l.ColorR / 255.0f, l.ColorG / 255.0f, l.ColorB / 255.0f);
                ch = ImGui.ColorEdit3("Colour", ref col);
                UndoOnActivate();
                if (ch)
                {
                    l.ColorR = (byte)Math.Clamp((int)Math.Round(col.X * 255), 0, 255);
                    l.ColorG = (byte)Math.Clamp((int)Math.Round(col.Y * 255), 0, 255);
                    l.ColorB = (byte)Math.Clamp((int)Math.Round(col.Z * 255), 0, 255);
                    MarkDirty(true);
                }

                float intensity = l.Intensity;
                ch = ImGui.DragFloat("Intensity", ref intensity, 0.05f, 0.0f, 1000.0f);
                UndoOnActivate();
                if (ch) { l.Intensity = Math.Max(intensity, 0); MarkDirty(true); }

                float falloff = l.Falloff;
                ch = ImGui.DragFloat("Falloff", ref falloff, 0.02f, 0.01f, 500.0f);
                UndoOnActivate();
                if (ch) { l.Falloff = Math.Max(falloff, 0.01f); MarkDirty(true); }

                float fexp = l.FalloffExponent;
                ch = ImGui.DragFloat("Falloff exponent", ref fexp, 0.25f, 0.0f, 512.0f);
                UndoOnActivate();
                if (ch) { l.FalloffExponent = Math.Max(fexp, 0); MarkDirty(true); }

                int flash = l.Flashiness;
                if (ImGui.Combo("Flashiness", ref flash, LightDefs.FlashinessNames, LightDefs.FlashinessNames.Length))
                {
                    scene.PushUndo();
                    l.Flashiness = (byte)flash;
                    scene.Dirty = true;
                }

                bool prevSh = !scene.ShadowsDisabled.Contains(l);
                if (ImGui.Checkbox("Cast shadows", ref prevSh))
                {
                    scene.PushUndo();
                    if (prevSh)
                    {
                        scene.ShadowsDisabled.Remove(l);
                        l.Flags |= 0x180u;
                    }
                    else
                    {
                        scene.ShadowsDisabled.Add(l);
                        l.Flags &= ~0x1C0u;
                    }
                    scene.Dirty = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Off: this light ignores walls/objects entirely (no shadows).\nAlso sets/clears the game's shadow flags (bits 7+8).");
            }

            if (Header("Direction", true))
            {
                var dir = ToNum(l.Direction);
                bool ch = ImGui.DragFloat3("Direction", ref dir, 0.005f, -1.0f, 1.0f);
                UndoOnActivate();
                if (ch)
                {
                    var d = ToDx(dir);
                    if (d.LengthSquared() > 1e-6f) d.Normalize();
                    l.Direction = d;
                    l.Tangent = MakeOrthoTangent(d, l.Tangent);
                    MarkDirty(true);
                }

                var tan = ToNum(l.Tangent);
                ch = ImGui.DragFloat3("Tangent", ref tan, 0.005f, -1.0f, 1.0f);
                UndoOnActivate();
                if (ch)
                {
                    var t = ToDx(tan);
                    if (t.LengthSquared() > 1e-6f) t.Normalize();
                    l.Tangent = t;
                    MarkDirty(true);
                }

                if (ImGui.Button("Point down"))
                {
                    scene.PushUndo();
                    l.Direction = new SDX.Vector3(0, 0, -1);
                    l.Tangent = new SDX.Vector3(-1, 0, 0);
                    scene.Dirty = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Ortho tangent"))
                {
                    scene.PushUndo();
                    l.Tangent = MakeOrthoTangent(l.Direction, l.Tangent);
                    scene.Dirty = true;
                }
                ImGui.SameLine();
                ImGui.TextDisabled("or use Rotate (E)");
            }

            if (l.Type == LightType.Spot && Header("Cone", true))
            {
                float inner = l.ConeInnerAngle;
                bool ch = UiSlider_U28.Float("Inner angle", ref inner, 0.0f, 90.0f, "%.2f");
                UndoOnActivate();
                if (ch) { l.ConeInnerAngle = inner; MarkDirty(true); }

                float outer = l.ConeOuterAngle;
                ch = UiSlider_U28.Float("Outer angle", ref outer, 0.0f, 90.0f, "%.2f");
                UndoOnActivate();
                if (ch) { l.ConeOuterAngle = outer; MarkDirty(true); }
                ImGui.TextDisabled("Half-angles; game clamps ~90.");
            }

            if (l.Type == LightType.Capsule && Header("Capsule", true))
            {
                var ext = ToNum(l.Extent);
                bool ch = ImGui.DragFloat3("Extent", ref ext, 0.01f);
                UndoOnActivate();
                if (ch) { l.Extent = ToDx(ext); MarkDirty(true); }
                ImGui.TextDisabled("Only X (length along direction) is used by the game.");
            }

            ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Effects"))
            {

            if (Header("Corona", true))
            {
                float cs = l.CoronaSize;
                bool ch = ImGui.DragFloat("Corona size", ref cs, 0.02f, 0.0f, 100.0f);
                UndoOnActivate();
                if (ch) { l.CoronaSize = Math.Max(cs, 0); MarkDirty(true); }

                float ci = l.CoronaIntensity;
                ch = ImGui.DragFloat("Corona intensity", ref ci, 0.02f, 0.0f, 100.0f);
                UndoOnActivate();
                if (ch) { l.CoronaIntensity = Math.Max(ci, 0); MarkDirty(true); }

                float cz = l.CoronaZBias;
                ch = ImGui.DragFloat("Corona z-bias", ref cz, 0.005f, 0.0f, 10.0f);
                UndoOnActivate();
                if (ch) { l.CoronaZBias = Math.Max(cz, 0); MarkDirty(true); }
            }

            if (Header("Volume", true))
            {
                bool drawVol = (l.Flags & LightDefs.FlagDrawVolume) != 0;
                if (ImGui.Checkbox("Draw volume", ref drawVol))
                {
                    scene.PushUndo();
                    if (drawVol) l.Flags |= LightDefs.FlagDrawVolume; else l.Flags &= ~LightDefs.FlagDrawVolume;
                    scene.Dirty = true;
                }

                float vi = l.VolumeIntensity;
                bool ch = ImGui.DragFloat("Volume intensity", ref vi, 0.01f, 0.0f, 50.0f);
                UndoOnActivate();
                if (ch) { l.VolumeIntensity = vi; MarkDirty(true); }

                float vs = l.VolumeSizeScale;
                ch = ImGui.DragFloat("Volume size scale", ref vs, 0.01f, 0.0f, 50.0f);
                UndoOnActivate();
                if (ch) { l.VolumeSizeScale = vs; MarkDirty(true); }

                bool outerCol = (l.Flags & LightDefs.FlagVolumeOuterColour) != 0;
                if (ImGui.Checkbox("Outer colour", ref outerCol))
                {
                    scene.PushUndo();
                    if (outerCol) l.Flags |= LightDefs.FlagVolumeOuterColour; else l.Flags &= ~LightDefs.FlagVolumeOuterColour;
                    scene.Dirty = true;
                }

                if (!outerCol) ImGui.BeginDisabled();
                var vcol = new Vector3(l.VolumeOuterColorR / 255.0f, l.VolumeOuterColorG / 255.0f, l.VolumeOuterColorB / 255.0f);
                ch = ImGui.ColorEdit3("Outer colour", ref vcol);
                UndoOnActivate();
                if (ch)
                {
                    l.VolumeOuterColorR = (byte)Math.Clamp((int)Math.Round(vcol.X * 255), 0, 255);
                    l.VolumeOuterColorG = (byte)Math.Clamp((int)Math.Round(vcol.Y * 255), 0, 255);
                    l.VolumeOuterColorB = (byte)Math.Clamp((int)Math.Round(vcol.Z * 255), 0, 255);
                    MarkDirty(true);
                }

                float voi = l.VolumeOuterIntensity;
                ch = ImGui.DragFloat("Outer intensity", ref voi, 0.01f, 0.0f, 10.0f);
                UndoOnActivate();
                if (ch) { l.VolumeOuterIntensity = voi; MarkDirty(true); }

                float voe = l.VolumeOuterExponent;
                ch = ImGui.DragFloat("Outer exponent", ref voe, 0.05f, 0.0f, 512.0f);
                UndoOnActivate();
                if (ch) { l.VolumeOuterExponent = voe; MarkDirty(true); }
                if (!outerCol) ImGui.EndDisabled();
            }

            ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Advanced"))
            {

            if (Header("Shadows & fades", true))
            {
                int sb = l.ShadowBlur;
                bool ch = UiSlider_U28.Int("Shadow blur", ref sb, 0, 255);
                UndoOnActivate();
                if (ch) { l.ShadowBlur = (byte)sb; MarkDirty(true); }

                float snc = l.ShadowNearClip;
                ch = ImGui.DragFloat("Shadow near clip", ref snc, 0.005f, 0.0f, 10.0f);
                UndoOnActivate();
                if (ch) { l.ShadowNearClip = snc; MarkDirty(true); }

                int lfd = l.LightFadeDistance;
                ch = UiSlider_U28.Int("Light fade", ref lfd, 0, 255, l.LightFadeDistance == 0 ? "0 (default)" : "%d");
                UndoOnActivate();
                if (ch) { l.LightFadeDistance = (byte)lfd; MarkDirty(true); }

                int sfd = l.ShadowFadeDistance;
                ch = UiSlider_U28.Int("Shadow fade", ref sfd, 0, 255, l.ShadowFadeDistance == 0 ? "0 (default)" : "%d");
                UndoOnActivate();
                if (ch) { l.ShadowFadeDistance = (byte)sfd; MarkDirty(true); }

                int spfd = l.SpecularFadeDistance;
                ch = UiSlider_U28.Int("Specular fade", ref spfd, 0, 255, l.SpecularFadeDistance == 0 ? "0 (default)" : "%d");
                UndoOnActivate();
                if (ch) { l.SpecularFadeDistance = (byte)spfd; MarkDirty(true); }

                int vfd = l.VolumetricFadeDistance;
                ch = UiSlider_U28.Int("Volume fade", ref vfd, 0, 255, l.VolumetricFadeDistance == 0 ? "0 (default)" : "%d");
                UndoOnActivate();
                if (ch) { l.VolumetricFadeDistance = (byte)vfd; MarkDirty(true); }
            }

            if (Header("Culling plane", true))
            {
                bool en = (l.Flags & LightDefs.FlagCullingPlane) != 0;
                if (ImGui.Checkbox("Culling plane", ref en))
                {
                    scene.PushUndo();
                    if (en) l.Flags |= LightDefs.FlagCullingPlane; else l.Flags &= ~LightDefs.FlagCullingPlane;
                    scene.Dirty = true;
                }
                if (!en) ImGui.BeginDisabled();
                var n = ToNum(l.CullingPlaneNormal);
                bool ch = ImGui.DragFloat3("Plane normal", ref n, 0.005f, -1.0f, 1.0f);
                UndoOnActivate();
                if (ch)
                {
                    var nn = ToDx(n);
                    if (nn.LengthSquared() > 1e-6f) nn.Normalize();
                    l.CullingPlaneNormal = nn;
                    MarkDirty(true);
                }
                float off = l.CullingPlaneOffset;
                ch = ImGui.DragFloat("Plane offset", ref off, 0.01f);
                UndoOnActivate();
                if (ch) { l.CullingPlaneOffset = off; MarkDirty(true); }
                ImGui.TextDisabled("Green side keeps light, red side is culled.");
                if (!en) ImGui.EndDisabled();
            }

            if (Header("Attachment & IDs", true))
            {
                int boneId = l.BoneId;
                bool ch = ImGui.InputInt("Bone tag", ref boneId);
                UndoOnActivate();
                if (ch) { l.BoneId = (ushort)Math.Clamp(boneId, 0, 65535); MarkDirty(true); }
                if (l.BoneId != 0)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled(LightEditorBoneFound(inst) ? LightEditorBoneName(l.BoneId) : "(not found!)");
                }

                var tags = LightEditorBoneTags().ToArray();
                if (tags.Length > 0 && ImGui.BeginCombo("Attach to bone", l.BoneId == 0 ? "(none)" : l.BoneId.ToString()))
                {
                    if (ImGui.Selectable("(none)", l.BoneId == 0))
                    {
                        scene.PushUndo(); l.BoneId = 0; scene.Dirty = true;
                    }
                    foreach (var t in tags)
                    {
                        var name = LightEditorBoneName(t);
                        if (ImGui.Selectable($"{t}  {name}", l.BoneId == t))
                        {
                            scene.PushUndo(); l.BoneId = t; scene.Dirty = true;
                        }
                    }
                    ImGui.EndCombo();
                }

                int groupId = l.GroupId;
                ch = ImGui.InputInt("Group id", ref groupId);
                UndoOnActivate();
                if (ch) { l.GroupId = (byte)Math.Clamp(groupId, 0, 255); MarkDirty(true); }

                int lightHash = l.LightHash;
                ch = ImGui.InputInt("Light hash (id)", ref lightHash);
                UndoOnActivate();
                if (ch) { l.LightHash = (byte)Math.Clamp(lightHash, 0, 255); MarkDirty(true); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Small 0-255 id linking this light to CExtensionDefLightEffect overrides");

                uint pth = l.ProjectedTextureHash.Hash;
                var texNames = scene.GetTextureNames();
                string current = "(none)";
                if (pth != 0)
                {
                    current = $"hash {pth:X8}";
                    foreach (var n in texNames)
                    {
                        if (JenkHash.GenHash(n.ToLowerInvariant()) == pth) { current = n; break; }
                    }
                }
                if (ImGui.BeginCombo("Projected texture", current))
                {
                    if (ImGui.Selectable("(none)", pth == 0))
                    {
                        scene.PushUndo();
                        l.ProjectedTextureHash = new MetaHash(0);
                        l.Flags &= ~LightDefs.FlagTextureProjection;
                        scene.Dirty = true;
                    }
                    foreach (var n in texNames)
                    {
                        uint h = JenkHash.GenHash(n.ToLowerInvariant());
                        if (ImGui.Selectable(n, h == pth))
                        {
                            scene.PushUndo();
                            l.ProjectedTextureHash = new MetaHash(h);
                            l.Flags |= LightDefs.FlagTextureProjection;
                            scene.Dirty = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Gobo/IES pattern projected by spot lights (sets flag 5).\nLoad the YTD that contains the texture to see it and to pick by name.");
                ImGui.SameLine();
                if (ImGui.SmallButton("Import..."))
                {
                    RequestImportProjTex?.Invoke();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Import a loose .dds/.png/.jpg for preview.\nFor the game, the texture must be shipped inside a YTD.");

                string hex = pth.ToString("X8");
                if (ImGui.InputText("Projected tex hash", ref hex, 16, ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var nh))
                    {
                        scene.PushUndo();
                        l.ProjectedTextureHash = new MetaHash(nh);
                        if (nh != 0) l.Flags |= LightDefs.FlagTextureProjection;
                        scene.Dirty = true;
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("JOAAT hash (hex) if the texture isn't in a loaded dictionary");
            }

            ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Flags"))
            {
                uint flags = l.Flags;
                string fnum = flags.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (ImGui.InputText("Flags", ref fnum, 16, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (uint.TryParse(fnum, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var nf))
                    {
                        scene.PushUndo();
                        l.Flags = nf;
                        scene.Dirty = true;
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("The light's flags as one number, the way CodeWalker shows them (for example 900).\nPress Enter to apply. The boxes below are the same flags one by one.");

                ImGui.Columns(2, "flagcols", false);
                foreach (var fd in LightDefs.Flags)
                {
                    bool set = (l.Flags & (1u << fd.Bit)) != 0;
                    if (ImGui.Checkbox($"{fd.Name}##flag{fd.Bit}", ref set))
                    {
                        scene.PushUndo();
                        if (set) l.Flags |= (1u << fd.Bit); else l.Flags &= ~(1u << fd.Bit);
                        scene.Dirty = true;
                        FlagsChanged = true;
                    }
                    if (!string.IsNullOrEmpty(fd.Tooltip) && ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"bit {fd.Bit} (0x{1u << fd.Bit:X}): {fd.Tooltip}");
                    }
                    ImGui.NextColumn();
                }
                ImGui.Columns(1);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Time"))
            {
                uint tf = l.TimeFlags;
                ImGui.Text(tf == 0 ? "0 = always on" : (tf == 0xFFFFFF ? "always on (all hours)" : $"0x{tf:X6}"));
                ImGui.SameLine();
                bool allOn = (l.TimeFlags & 0xFFFFFF) == 0xFFFFFF;
                if (ImGui.SmallButton(allOn ? "All Off" : "All On"))
                {
                    scene.PushUndo();
                    l.TimeFlags = allOn ? 0u : 0xFFFFFFu;
                    scene.Dirty = true;
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Night"))
                {
                    scene.PushUndo(); l.TimeFlags = 0xF0007F; scene.Dirty = true;
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Day"))
                {
                    scene.PushUndo(); l.TimeFlags = 0x0FFF80; scene.Dirty = true;
                }

                ImGui.Columns(4, "tfcols", false);
                for (int bit = 0; bit < 24; bit++)
                {
                    bool set = (l.TimeFlags & (1u << bit)) != 0;
                    if (ImGui.Checkbox($"{LightDefs.HourLabel(bit)}##tf{bit}", ref set))
                    {
                        scene.PushUndo();
                        if (set) l.TimeFlags |= (1u << bit); else l.TimeFlags &= ~(1u << bit);
                        scene.Dirty = true;
                    }
                    ImGui.NextColumn();
                }
                ImGui.Columns(1);
            ImGui.EndTabItem();
            }

            ImGui.EndTabBar();

            if (!Scene.LightParamsEqual(paramsBefore, l) && !lightEditorExternal)
            {
                scene.PropagateInstances(l);
                scene.ApplyEditToSelection(paramsBefore, l);
            }
        }

        private static SDX.Vector3 MakeOrthoTangent(SDX.Vector3 dir, SDX.Vector3 currentTangent)
        {
            var t = currentTangent - dir * SDX.Vector3.Dot(currentTangent, dir);
            if (t.LengthSquared() < 1e-6f)
            {
                var reference = Math.Abs(dir.Z) < 0.9f ? SDX.Vector3.UnitZ : SDX.Vector3.UnitX;
                t = SDX.Vector3.Cross(dir, reference);
            }
            t.Normalize();
            return t;
        }
    }
}

