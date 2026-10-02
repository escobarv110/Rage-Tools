using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RageLightEditor
{
    public static class AppInfo
    {
        public const string Name = "RAGE Tools";
    }

    public enum AppMode
    {
        Light,
        Material,
    }

    public static class AppLauncher
    {
        private static string MloBridgeDefaultPortText =>
            Editor.MloBridge.DefaultPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static int Run(string[] args, AppMode mode)
        {
            if (args.Length >= 2 && string.Equals(args[0], "--convertxml", StringComparison.OrdinalIgnoreCase))
                return Editor.XmlConvertCli_V49.Run(args[1], args.Length > 2 ? args[2] : null);

            if (args.Length >= 2 && string.Equals(args[0], "--converttoxml", StringComparison.OrdinalIgnoreCase))
                return Editor.XmlConvertCli_V49.RunToXml(args[1], args.Length > 2 ? args[2] : null);

            var openFiles = new List<string>();
            string screenshot = null;
            bool selftest = false;
            bool gentest = false;
            bool proxytest = false;
            bool ymaptest = false;
            bool archtest = false;
            bool rpfEditTest = false;
            bool libscan = false;
            string findShader = null;
            bool matdump = false, matidentity = false, mattest = false, matpresets = false;
            bool portalProbe = false;
            string matPreset = null, matPresetFilter = null;
            int matPresetIndex = 0;
            int matSelect = -1;
            bool matIsolate = false, matReport = false;
            bool classReport = false;
            string renderOut = null;
            bool cineSpace = false, seqTest = false, noShadowJitter = false, videoTest = false, archiveTest = false, archiveSpace = false, worldTest = false;
            bool mloSpace = false;
            string worldSpace = null;
            var cineSet = new List<string>();
            var matSet = new List<string>();
            var matTex = new List<string>();
            var matImport = new List<string>();
            int libLimit = 0;
            string placeProp = null;
            string archOut = null;
            int weatherIndex = -1;
            int skyMode = 0;
            int shadowMode = -1;
            bool noDwell = false, pickTest = false, keyTest = false, lightCapTest = false;
            bool matsavetest = false;
            string matExport = null, matExportAll = null;
            uint setFlag = 0, clearFlag = 0;
            bool copyPasteTest = false, gtaGate = false, tcDump = false, moveTest = false, newLightTest = false, multiEditTest = false;
            string ymapOut = null;
            bool dumpmesh = false;
            bool drawTest = false, lodTest = false;
            bool noDepth = false;
            int renderMode = -1;
            int selectLight = -1;
            int hour = -1;
            int gizmoMode = -1;
            bool fpsBench = false;
            bool noShadows = false;
            bool shadowStress = false;
            bool noCull = false;
            string camSpec = null, cineSpec = null;
            bool photoMode = false;
            int hourSweep = 0;
            int modeSweep = 0;
            float hourFocus = 0.0f;
            bool closeup = false;
            string timecyclePath = null;
            string gtaFolder = null;
            string mloPath = null;
            string saveProject = null;
            string modifier = null;
            int lightProbe = 0;
            bool tcEdit = false;
            bool waterDebug = false;
            bool allProps = false;
            string probeShotDir = null;
            int fpsCap = -1;
            bool detachProject = false;
            bool detachMlo = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--screenshot":
                        if (i + 1 < args.Length) screenshot = args[++i];
                        break;
                    case "--selftest":
                        selftest = true;
                        break;
                    case "--gentest":
                        gentest = true;
                        break;
                    case "--proxytest":
                        proxytest = true;
                        break;
                    case "--ymaptest":
                        ymaptest = true;
                        break;
                    case "--rpfedittest":
                        rpfEditTest = true;
                        break;
                    case "--archtest":
                        archtest = true;
                        break;
                    case "--archout":
                        if (i + 1 < args.Length) archOut = args[++i];
                        break;
                    case "--libscan":
                        libscan = true;
                        break;
                    case "--findshader":
                        if (i + 1 < args.Length) findShader = args[++i];
                        break;
                    case "--portalprobe":
                        portalProbe = true;
                        break;
                    case "--matdump":
                        matdump = true;
                        break;
                    case "--matidentity":
                        matidentity = true;
                        break;
                    case "--mattest":
                        mattest = true;
                        break;
                    case "--matpreset":
                        if (i + 2 < args.Length)
                        {
                            int.TryParse(args[++i], out matPresetIndex);
                            matPreset = args[++i];
                        }
                        break;
                    case "--matpresets":
                        matpresets = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) matPresetFilter = args[++i];
                        break;
                    case "--placeprop":
                        if (i + 1 < args.Length) placeProp = args[++i];
                        break;
                    case "--limit":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out libLimit);
                        break;
                    case "--weather":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out weatherIndex);
                        break;
                    case "--skydebug":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out skyMode);
                        break;
                    case "--picktest":
                        pickTest = true;
                        break;
                    case "--keytest":
                        keyTest = true;
                        break;
                    case "--devicelosstest":
                        Rendering.DeviceResources.DebugFakeDeviceLoss = true;
                        break;
                    case "--lightcaptest":
                        lightCapTest = true;
                        break;
                    case "--nodwell":
                        noDwell = true;
                        break;
                    case "--shadowmode":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out shadowMode);
                        break;
                    case "--multiedittest":
                        multiEditTest = true;
                        break;
                    case "--newlighttest":
                        newLightTest = true;
                        break;
                    case "--movetest":
                        moveTest = true;
                        break;
                    case "--tcdump":
                        tcDump = true;
                        break;
                    case "--gtagate":
                        gtaGate = true;
                        break;
                    case "--material":
                        mode = AppMode.Material;
                        break;
                    case "--matsavetest":
                        matsavetest = true;
                        break;
                    case "--matpresetopen":
                        Editor.MaterialPanel.ForcePresetPopup = true;
                        break;
                    case "--matexport":
                        if (i + 1 < args.Length) matExport = args[++i];
                        break;
                    case "--matexportall":
                        if (i + 1 < args.Length) matExportAll = args[++i];
                        break;
                    case "--lights":
                        mode = AppMode.Light;
                        break;
                    case "--copypastetest":
                        copyPasteTest = true;
                        break;
                    case "--setflag":
                        if (i + 1 < args.Length) setFlag = BitMask(args[++i]);
                        break;
                    case "--clearflag":
                        if (i + 1 < args.Length) clearFlag = BitMask(args[++i]);
                        break;
                    case "--ymapout":
                        if (i + 1 < args.Length) ymapOut = args[++i];
                        break;
                    case "--spotonly":
                        TestSceneGenerator.SpotOnly = true;
                        break;
                    case "--cullplane":
                        TestSceneGenerator.CullPlaneDemo = true;
                        break;
                    case "--volumedemo":
                        TestSceneGenerator.VolumeDemo = true;
                        break;
                    case "--projdemo":
                        TestSceneGenerator.ProjTexDemo = true;
                        break;
                    case "--heavy":
                        TestSceneGenerator.HeavyDemo = true;
                        break;
                    case "--gizmomode":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out gizmoMode);
                        break;
                    case "--fpsbench":
                        fpsBench = true;
                        break;
                    case "--noshadows":
                        noShadows = true;
                        break;
                    case "--shadowstress":
                        shadowStress = true;
                        break;
                    case "--nocull":
                        noCull = true;
                        break;
                    case "--closeup":
                        closeup = true;
                        break;
                    case "--cam":
                        if (i + 1 < args.Length) camSpec = args[++i];
                        break;
                    case "--cine":
                        if (i + 1 < args.Length) cineSpec = args[++i];
                        break;
                    case "--cineset":
                        if (i + 1 < args.Length) cineSet.Add(args[++i]);
                        break;
                    case "--photo":
                        photoMode = true;
                        break;
                    case "--modesweep":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out modeSweep);
                        break;
                    case "--hourfocus":
                        if (i + 1 < args.Length) float.TryParse(args[++i],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out hourFocus);
                        break;
                    case "--hoursweep":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out hourSweep);
                        break;
                    case "--timecycle":
                        if (i + 1 < args.Length) timecyclePath = args[++i];
                        break;
                    case "--gta":
                        if (i + 1 < args.Length) gtaFolder = args[++i];
                        break;
                    case "--mlo":
                        if (i + 1 < args.Length) mloPath = args[++i];
                        break;
                    case "--saveproject":
                        if (i + 1 < args.Length) saveProject = args[++i];
                        break;
                    case "--modifier":
                        if (i + 1 < args.Length) modifier = args[++i];
                        break;
                    case "--lightprobe":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out lightProbe);
                        break;
                    case "--probeshots":
                        if (i + 1 < args.Length) probeShotDir = args[++i];
                        break;
                    case "--allprops":
                        allProps = true;
                        break;
                    case "--waterdebug":
                        waterDebug = true;
                        break;
                    case "--tcedit":
                        tcEdit = true;
                        break;
                    case "--openheader":
                        if (i + 1 < args.Length) Editor.LightPanel.ForceOpenHeader = args[++i];
                        break;
                    case "--mattab":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var mt))
                            Editor.MaterialPanel.ForceTab = mt;
                        break;
                    case "--matselect":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out matSelect);
                        break;
                    case "--matisolate":
                        matIsolate = true;
                        break;
                    case "--matset":
                        if (i + 1 < args.Length) matSet.Add(args[++i]);
                        break;
                    case "--mattex":
                        if (i + 1 < args.Length) matTex.Add(args[++i]);
                        break;
                    case "--matimport":
                        if (i + 1 < args.Length) matImport.Add(args[++i]);
                        break;
                    case "--matreport":
                        matReport = true;
                        break;
                    case "--fpscap":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out fpsCap);
                        break;
                    case "--dumpmesh":
                        dumpmesh = true;
                        break;
                    case "--drawtest":
                        drawTest = true;
                        break;
                    case "--renderout":
                        if (i + 1 < args.Length) renderOut = args[++i];
                        break;
                    case "--noshadowjitter":
                        noShadowJitter = true;
                        break;
                    case "--rpfspace":
                    case "--archivespace":
                        archiveSpace = true;
                        break;
                    case "--ptfx":
                        if (i + 1 < args.Length) Environment.SetEnvironmentVariable("RLE_PTFX", args[++i]);
                        break;
                    case "--navspace":
                        Environment.SetEnvironmentVariable("RLE_NAVSPACE", "1");
                        break;
                    case "--terrainspace":
                        Environment.SetEnvironmentVariable("RLE_TERRAINSPACE", "1");
                        break;
                    case "--animspace":
                        Environment.SetEnvironmentVariable("RLE_ANIMSPACE", "1");
                        break;
                    case "--serve":
                    {
                        string port = MloBridgeDefaultPortText;
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out _)) port = args[++i];
                        Environment.SetEnvironmentVariable("RLE_SERVE", "1");
                        Environment.SetEnvironmentVariable("RLE_BRIDGE", port);
                        break;
                    }
                    case "--worldspace":
                        if (i + 1 < args.Length) worldSpace = args[++i];
                        break;
                    case "--worldtest":
                        worldTest = true;
                        break;
                    case "--archivetest":
                        archiveTest = true;
                        break;
                    case "--cinespace":
                        cineSpace = true;
                        break;
                    case "--mlospace":
                        mloSpace = true;
                        break;
                    case "--extspace":
                        Environment.SetEnvironmentVariable("RLE_EXTSPACE", "1");
                        break;
                    case "--videotest":
                        seqTest = true; videoTest = true;
                        break;
                    case "--seqtest":
                        seqTest = true;
                        break;
                    case "--detachproject":
                        detachProject = true;
                        break;
                    case "--detachmlo":
                        detachMlo = true;
                        break;
                    case "--classreport":
                        classReport = true;
                        break;
                    case "--lodtest":
                        lodTest = true;
                        break;
                    case "--rendermode":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out renderMode);
                        break;
                    case "--select":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out selectLight);
                        break;
                    case "--hour":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out hour);
                        break;
                    case "--nodepth":
                        noDepth = true;
                        break;
                    case "--debuglog":
                        DebugLog.Enabled = true;
                        Rendering.DeviceResources.EnableDebugLayer = true;
                        break;
                    case "--depthclear0":
                        Rendering.DeviceResources.DebugDepthClear = 1.0f;
                        break;
                    case "--depthclearmid":
                        Rendering.DeviceResources.DebugDepthClear = 0.00165f;
                        break;
                    default:
                        openFiles.Add(args[i]);
                        break;
                }
            }

            var firstFile = openFiles.Count > 0 ? openFiles[0] : null;
            if (gentest)
            {
                return TestSceneGenerator.Run(firstFile);
            }
            if (dumpmesh)
            {
                return MeshDump.Run(firstFile);
            }
            if (lightCapTest)
            {
                return LightCapTest.Run();
            }
            if (drawTest)
            {
                return DrawTest.Run(firstFile);
            }
            if (lodTest)
            {
                return DrawTest.RunYmap(firstFile);
            }
            if (selftest)
            {
                return SelfTest.Run(firstFile);
            }
            if (proxytest)
            {
                return Editor.LightProxyBuilder.SelfTest(firstFile);
            }
            if (ymaptest)
            {
                return Editor.YmapBuilder.SelfTest(firstFile);
            }
            if (archtest)
            {
                return Editor.ArchetypeBuilder.SelfTest(firstFile);
            }
            if (rpfEditTest)
            {
                return Editor.RpfEditTest.Run();
            }
            if (findShader != null)
            {
                return LibScan.FindShader(gtaFolder, findShader, libLimit > 0 ? libLimit : 5);
            }
            if (libscan)
            {
                return LibScan.Run(gtaFolder, openFiles.ToArray(), libLimit);
            }
            if (matsavetest)
            {
                return MaterialTest.SaveTest(firstFile);
            }
            if (portalProbe)
            {
                return Editor.MloPortalProbe_V67.Run();
            }
            if (matdump)
            {
                return MaterialTest.Dump(firstFile);
            }
            if (matidentity)
            {
                return MaterialTest.Identity(firstFile);
            }
            if (mattest)
            {
                return MaterialTest.EditTest(firstFile);
            }
            if (matpresets)
            {
                return MaterialTest.ListPresets(matPresetFilter);
            }
            if (matPreset != null)
            {
                return MaterialTest.PresetTest(firstFile, matPresetIndex, matPreset);
            }

            ApplicationConfiguration.Initialize();
            InstallCrashLog();

            var form = new MainForm(openFiles, screenshot) { DebugWaterDebug = waterDebug, Mode = mode, DebugRenderMode = renderMode, DebugNoDepth = noDepth, DebugSelectLight = selectLight, DebugHour = hour, DebugGizmoMode = gizmoMode, DebugFpsBench = fpsBench, DebugFpsCap = fpsCap, DebugNoShadows = noShadows, DebugShadowStress = shadowStress, DebugNoCull = noCull, DebugCam = camSpec, DebugCine = cineSpec, DebugPhoto = photoMode, DebugHourSweep = hourSweep, DebugHourFocus = hourFocus, DebugModeSweep = modeSweep, DebugCloseup = closeup, DebugTimecycle = timecyclePath, DebugGtaFolder = gtaFolder, DebugMlo = mloPath, DebugSaveProject = saveProject, DebugYmapOut = ymapOut, DebugArchOut = archOut, DebugWeather = weatherIndex, DebugSkyMode = skyMode, DebugSetFlagMask = setFlag, DebugClearFlagMask = clearFlag, DebugCopyPasteTest = copyPasteTest, DebugGtaGate = gtaGate, DebugTcDump = tcDump, DebugMoveTest = moveTest, DebugNewLightTest = newLightTest, DebugMultiEditTest = multiEditTest, DebugShadowMode = shadowMode, DebugNoDwell = noDwell, DebugPickTest = pickTest, DebugKeyTest = keyTest, DebugModifier = modifier, DebugLightProbe = lightProbe, DebugTcEdit = tcEdit, ProbeShotDir = probeShotDir, DebugAllProps = allProps, DebugPlaceProp = placeProp, DebugMatSelect = matSelect, DebugMatIsolate = matIsolate, DebugMatReport = matReport, DebugClassReport = classReport, DebugRenderOut = renderOut, DebugCineSpace = cineSpace, DebugMloSpace = mloSpace, DebugArchiveTest = archiveTest, DebugWorldTest = worldTest, DebugWorldSpace = worldSpace, DebugArchiveSpace = archiveSpace, DebugNoShadowJitter = noShadowJitter, DebugSeqTest = seqTest, DebugVideoTest = videoTest };
            form.DebugCineSet.AddRange(cineSet);
            form.DebugMatSet.AddRange(matSet);
            form.DebugMatTex.AddRange(matTex);
            form.DebugMatImport.AddRange(matImport);
            form.DebugMatExport = matExport;
            form.DebugMatExportAll = matExportAll;
            form.DebugDetachProject = detachProject;
            form.DebugDetachMlo = detachMlo;
            var newUiEnv = Environment.GetEnvironmentVariable("RLE_NEWUI");
            bool newUi = string.IsNullOrEmpty(newUiEnv) ? Editor.AppSettings.PeekNewUi_U27() : newUiEnv == "1";
            ApplicationContext shellContext = newUi ? Shell.ShellHost_U27.Start(form) : null;
            if (shellContext == null) form.Show();

            Application.Idle += (s, e) =>
            {
                while (form.Created && AppStillIdle)
                {
                    form.RenderFrame();
                }
            };
            if (shellContext != null) Application.Run(shellContext);
            else Application.Run(form);
            FlightRecorder.CleanExit();
            if (form.RestartPending_U27)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath)
                    { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory });
                }
                catch (Exception ex) { Console.WriteLine("RESTART failed: " + ex.Message); }
            }

            var lost = form.DeviceLostReport;
            if (!string.IsNullOrEmpty(lost))
            {
                MessageBox.Show(lost, "Graphics device lost", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return 0;
        }

        private static void InstallCrashLog()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => WriteCrash("UI thread", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => WriteCrash("background", e.ExceptionObject as Exception);
        }

        private static void WriteCrash(string where, Exception ex)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            try
            {
                File.AppendAllText(path,
                    $"---- {DateTime.Now:yyyy-MM-dd HH:mm:ss}  ({where}) ----{Environment.NewLine}" +
                    $"{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }

            try
            {
                MessageBox.Show(
                    $"Something went wrong and the editor had to stop what it was doing.{Environment.NewLine}{Environment.NewLine}" +
                    $"{ex?.GetType().Name}: {ex?.Message}{Environment.NewLine}{Environment.NewLine}" +
                    $"The full details were written to:{Environment.NewLine}{path}{Environment.NewLine}{Environment.NewLine}" +
                    "Send that file along if you report this.",
                    "RAGE Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        private static uint BitMask(string list)
        {
            uint m = 0;
            foreach (var part in list.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), out var b) && b >= 0 && b < 32) m |= 1u << b;
            return m;
        }

        private static bool AppStillIdle
        {
            get
            {
                return !PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParameter;
            public IntPtr LParameter;
            public uint Time;
            public System.Drawing.Point Location;
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);
    }
}

