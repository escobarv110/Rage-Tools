using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private FiveMBridge fivem_U12;
        private bool fivemWired_U12, fivemDemoDone_U12, fivemLiveSent_U12;
        private int fivemFollowSent_U12 = -1;
        private int fivemHoldFrames_U12, fivemHoldSettle_U12;
        private readonly System.Diagnostics.Stopwatch fivemClock_U12 = System.Diagnostics.Stopwatch.StartNew();
        private double fivemCamSentAt_U12, fivemLightsSentAt_U12;
        private string fivemLightsSent_U12 = "", fivemCamSent_U12 = "", fivemTimeSent_U12 = "", fivemWeatherSent_U12 = "", fivemPlayer_U12 = "";
        private Vector3 fivemPlayerPos_U12, fivemPickPos_U12;
        private bool fivemHasPlayer_U12, fivemHasPick_U12;

        private static readonly string[] FiveMResourceFiles_U12 = { "fxmanifest.lua", "client.lua", "server.lua", "html/index.html", "html/bridge.js" };

        private void EnsureFiveMWired_U12()
        {
            if (fivemWired_U12 || panel == null || settings == null) return;
            fivemWired_U12 = true;
            panel.FiveMPort_U12 = settings.FiveMPort > 0 ? settings.FiveMPort : FiveMBridge.DefaultPort;
            panel.FiveMResourcesFolder_U12 = settings.FiveMResourcesFolder ?? "";
            panel.FiveMLive_U12 = settings.FiveMLive;
            panel.FiveMFollow_U12 = Math.Clamp(settings.FiveMFollow, 0, 2);
            panel.FiveMRestartAfterSave_U12 = settings.FiveMRestartAfterSave;
            panel.FiveMAnyInterface_U12 = settings.FiveMAnyInterface;
            panel.FiveMAutoStart_U12 = settings.FiveMAutoStart;
            if (settings.FiveMAutoStart && screenshotPath == null && !DebugSeqTest) StartFiveM_U12();
        }

        private void StoreFiveMSettings_U12()
        {
            if (settings == null) return;
            settings.FiveMPort = panel.FiveMPort_U12;
            settings.FiveMResourcesFolder = panel.FiveMResourcesFolder_U12 ?? "";
            settings.FiveMLive = panel.FiveMLive_U12;
            settings.FiveMFollow = panel.FiveMFollow_U12;
            settings.FiveMRestartAfterSave = panel.FiveMRestartAfterSave_U12;
            settings.FiveMAnyInterface = panel.FiveMAnyInterface_U12;
            settings.FiveMAutoStart = panel.FiveMAutoStart_U12;
        }

        private bool StartFiveM_U12(int port = -1)
        {
            fivem_U12 ??= new FiveMBridge();
            int want = port >= 0 ? port : panel.FiveMPort_U12;
            if (!fivem_U12.Start(want, panel.FiveMAnyInterface_U12))
            {
                panel.FiveMSay_U12("could not listen on port " + want + ": " + fivem_U12.Error);
                return false;
            }
            panel.FiveMPort_U12 = fivem_U12.Port;
            panel.FiveMSay_U12($"listening on {(fivem_U12.AnyInterface ? "every interface" : "127.0.0.1")}:{fivem_U12.Port}");
            Console.WriteLine($"FIVEM listening {(fivem_U12.AnyInterface ? "0.0.0.0" : "127.0.0.1")}:{fivem_U12.Port}");
            ResetFiveMSent_U12();
            return true;
        }

        private void StopFiveM_U12()
        {
            fivem_U12?.Stop();
            panel.FiveMSay_U12("stopped");
        }

        private void ResetFiveMSent_U12()
        {
            fivemLightsSent_U12 = ""; fivemCamSent_U12 = ""; fivemTimeSent_U12 = ""; fivemWeatherSent_U12 = "";
            fivemFollowSent_U12 = -1; fivemLiveSent_U12 = false;
            ResetFiveMSent_U13();
            ResetFiveMSent_U15();
        }

        private int BroadcastFiveM_U12(string json) => fivem_U12?.Broadcast(json) ?? 0;

        private void Tick_FiveM_U12()
        {
            EnsureFiveMWired_U12();
            ServiceFiveMDemo_U12();
            ServiceFiveMRequests_U12();
            StoreFiveMSettings_U12();
            Tick_FiveM_U13();
            Tick_FiveM_U16();
            Tick_Discord_U24();
            var b = fivem_U12;
            if (b == null || !b.Listening)
            {
                panel.FiveMListening_U12 = false;
                panel.FiveMConnected_U12 = false;
                panel.FiveMStatus_U12 = string.IsNullOrEmpty(b?.Error) ? "" : b.Error;
                return;
            }
            panel.FiveMListening_U12 = true;
            foreach (var note in b.DrainNotes()) panel.FiveMSay_U12(note);
            foreach (var m in b.Drain()) HandleFiveMMessage_U12(m);
            int n = b.Clients;
            panel.FiveMConnected_U12 = n > 0;
            panel.FiveMStatus_U12 = n > 0
                ? (string.IsNullOrEmpty(fivemPlayer_U12) ? "" : fivemPlayer_U12 + "  ") + $"port {b.Port}  {b.Received} in / {b.Sent} out"
                : $"on {(b.AnyInterface ? "every interface" : "127.0.0.1")}:{b.Port} - join the server with the resource running";
            HoldCaptureForFiveM_U12();
            if (n > 0) PushFiveMSync_U12();
        }

        private void ServiceFiveMDemo_U12()
        {
            if (fivemDemoDone_U12) return;
            var want = Environment.GetEnvironmentVariable("RLE_FIVEM");
            if (string.IsNullOrEmpty(want)) { fivemDemoDone_U12 = true; return; }
            fivemDemoDone_U12 = true;
            int port = int.TryParse(want, out var p) ? p : FiveMBridge.DefaultPort;
            StartFiveM_U12(port);
            panel.FiveMWindowOpen_U12 = true;
            if (Environment.GetEnvironmentVariable("RLE_FIVEMLIVE") == "1") panel.FiveMLive_U12 = true;
            if (int.TryParse(Environment.GetEnvironmentVariable("RLE_FIVEMFOLLOW"), out var f)) panel.FiveMFollow_U12 = Math.Clamp(f, 0, 2);
            if (screenshotPath != null)
            {
                fivemHoldFrames_U12 = 1800;
                fivemHoldSettle_U12 = int.TryParse(Environment.GetEnvironmentVariable("RLE_FIVEMHOLD"), out var hold) ? hold : 600;
            }
        }

        private void HoldCaptureForFiveM_U12()
        {
            if (fivemHoldFrames_U12 <= 0 || screenshotPath == null) return;
            if (screenshotFrames <= 0) return;
            if ((fivem_U12?.Clients ?? 0) == 0) { fivemHoldFrames_U12--; screenshotFrames = Math.Max(screenshotFrames, 3); }
            else if (fivemHoldSettle_U12-- > 0) screenshotFrames = Math.Max(screenshotFrames, 2);
            else fivemHoldFrames_U12 = 0;
        }

        private void ServiceFiveMRequests_U12()
        {
            if (panel.RequestFiveMToggle_U12)
            {
                panel.RequestFiveMToggle_U12 = false;
                if (fivem_U12 != null && fivem_U12.Listening) StopFiveM_U12();
                else StartFiveM_U12();
            }
            if (panel.RequestFiveMBrowse_U12)
            {
                panel.RequestFiveMBrowse_U12 = false;
                if (screenshotPath == null && !DebugSeqTest)
                {
                    try
                    {
                        using var dlg = new FolderBrowserDialog
                        {
                            Description = "Your FiveM server's resources folder",
                            UseDescriptionForTitle = true,
                            SelectedPath = Directory.Exists(panel.FiveMResourcesFolder_U12) ? panel.FiveMResourcesFolder_U12 : "",
                        };
                        if (dlg.ShowDialog(this) == DialogResult.OK) panel.FiveMResourcesFolder_U12 = dlg.SelectedPath;
                    }
                    catch { }
                }
            }
            if (panel.RequestFiveMInstall_U12)
            {
                panel.RequestFiveMInstall_U12 = false;
                try
                {
                    var dir = InstallFiveMResource_U12(panel.FiveMResourcesFolder_U12, panel.FiveMPort_U12, panel.FiveMAnyInterface_U12 ? LanAddress_U12() : "127.0.0.1");
                    panel.FiveMInstalledPath_U12 = dir;
                    panel.FiveMSay_U12("resource written to " + dir + " - add 'ensure ragetools_linking' and 'set ragetools_dev 1' to server.cfg");
                }
                catch (Exception ex) { panel.FiveMSay_U12("could not write the resource: " + ex.Message); }
            }
            if (!string.IsNullOrEmpty(panel.RequestFiveMReveal_U12))
            {
                var p = panel.RequestFiveMReveal_U12;
                panel.RequestFiveMReveal_U12 = null;
                try { if (Directory.Exists(p)) System.Diagnostics.Process.Start("explorer.exe", "\"" + p + "\""); } catch { }
            }
            if (panel.RequestFiveMRestart_U12)
            {
                panel.RequestFiveMRestart_U12 = false;
                RestartFiveMResource_U12(panel.FiveMRestartName_U12);
            }
            if (panel.RequestFiveMTeleport_U12)
            {
                panel.RequestFiveMTeleport_U12 = false;
                var pos = camera.Position;
                int n = BroadcastFiveM_U12("{\"type\":\"goto\",\"pos\":" + DccBridgeProtocol.V(pos) + "}");
                panel.FiveMSay_U12(n > 0 ? $"player sent to {pos.X:0.#}, {pos.Y:0.#}, {pos.Z:0.#}" : "the game is not connected");
            }
            if (panel.RequestFiveMGoToPlayer_U12)
            {
                panel.RequestFiveMGoToPlayer_U12 = false;
                if (fivemHasPlayer_U12) { SetCameraFrom_P5(fivemPlayerPos_U12 + new Vector3(-4, -4, 2.5f), fivemPlayerPos_U12 + new Vector3(0, 0, 1), 0); panel.FiveMSay_U12("camera moved to the player"); }
                else { BroadcastFiveM_U12("{\"type\":\"where\"}"); panel.FiveMSay_U12("asked the game where the player is"); }
            }
            if (panel.RequestFiveMGoToPick_U12)
            {
                panel.RequestFiveMGoToPick_U12 = false;
                if (fivemHasPick_U12) SetCameraFrom_P5(fivemPickPos_U12 + new Vector3(-5, -5, 3), fivemPickPos_U12, 0);
            }
            if (panel.RequestFiveMResend_U12)
            {
                panel.RequestFiveMResend_U12 = false;
                ResetFiveMSent_U12();
                panel.FiveMSay_U12("resending everything");
            }
        }

        private void PushFiveMSync_U12()
        {
            double now = fivemClock_U12.Elapsed.TotalSeconds;
            bool live = panel.FiveMLive_U12;
            if (live != fivemLiveSent_U12)
            {
                fivemLiveSent_U12 = live;
                if (!live)
                {
                    BroadcastFiveM_U12(ClearLightsJson_U12());
                    BroadcastFiveM_U12("{\"type\":\"camera\",\"off\":true}");
                    fivemLightsSent_U12 = ""; fivemCamSent_U12 = "";
                    PushFiveMOff_U13();
                    PushFiveMOff_U15();
                }
            }
            int follow = live ? panel.FiveMFollow_U12 : 0;
            if (follow != fivemFollowSent_U12)
            {
                if (fivemFollowSent_U12 == 1) BroadcastFiveM_U12("{\"type\":\"camera\",\"off\":true}");
                fivemFollowSent_U12 = follow;
                BroadcastFiveM_U12("{\"type\":\"follow\",\"on\":" + (follow == 2 ? "true" : "false") + "}");
                fivemCamSent_U12 = "";
            }
            if (!live) return;

            if (follow == 1 && now - fivemCamSentAt_U12 > 0.05)
            {
                var body = CameraJson_U12();
                if (body != fivemCamSent_U12) { fivemCamSent_U12 = body; fivemCamSentAt_U12 = now; BroadcastFiveM_U12(body); }
            }

            PushLightsFiveM_U14(now);

            var time = TimeJson_U12(panel.PreviewHour);
            if (time != fivemTimeSent_U12) { fivemTimeSent_U12 = time; BroadcastFiveM_U12(time); }
            var wx = "{\"type\":\"weather\",\"name\":" + DccBridgeProtocol.S(GameWeatherName_U12(weather.TargetPreset.Name)) + "}";
            if (wx != fivemWeatherSent_U12) { fivemWeatherSent_U12 = wx; BroadcastFiveM_U12(wx); }
            PushFiveMSync_U13();
            PushFiveMEntities_U15(now);
        }

        private void HandleFiveMMessage_U12(MloBridgeMessage m)
        {
            bool handled = false;
            ApiMessage_W1(m, ref handled);
            if (handled) return;
            var d = m as DccMessage;
            switch (m.Command)
            {
                case "hello":
                    fivemPlayer_U12 = d?.Text("player", 0, "") ?? "";
                    panel.FiveMPlayerText_U12 = string.IsNullOrEmpty(fivemPlayer_U12) ? "" : "player: " + fivemPlayer_U12;
                    m.From?.Send("{\"type\":\"hello\",\"app\":\"RAGE Tools\",\"version\":" + DccBridgeProtocol.S(AppVersion_P5()) + ",\"protocol\":" + DccBridgeProtocol.Version + "}");
                    panel.FiveMSay_U12("game connected" + (string.IsNullOrEmpty(fivemPlayer_U12) ? "" : " - " + fivemPlayer_U12) + (d != null ? " via " + d.Text("resource", 1, "the resource") : ""));
                    ResetFiveMSent_U12();
                    CheckResourceVersion_U16(d);
                    return;
                case "player":
                    if (d == null) return;
                    fivemPlayerPos_U12 = d.Vec("pos", -1, fivemPlayerPos_U12);
                    fivemHasPlayer_U12 = true;
                    if (panel.FiveMLive_U12 && panel.FiveMFollow_U12 == 2)
                    {
                        var cp = d.Vec("campos", -1, fivemPlayerPos_U12);
                        var rot = d.Vec("camrot", -1, Vector3.Zero);
                        var dir = CamDirFromGameRot_U12(rot.X, rot.Z);
                        SetCameraFrom_P5(cp, cp + dir * 10.0f, d.Num("fov", -1, 0.0f));
                        fivemCamSent_U12 = CameraJson_U12();
                    }
                    return;
                case "pick":
                {
                    if (d == null) return;
                    string name = d.Text("name", 0, "");
                    fivemPickPos_U12 = d.Vec("pos", -1, Vector3.Zero);
                    fivemHasPick_U12 = true;
                    panel.FiveMPickText_U12 = $"picked {(string.IsNullOrEmpty(name) ? "0x" + ((uint)d.Num("hash", -1, 0)).ToString("X8") : name)} at {fivemPickPos_U12.X:0.##}, {fivemPickPos_U12.Y:0.##}, {fivemPickPos_U12.Z:0.##}";
                    panel.FiveMSay_U12(panel.FiveMPickText_U12);
                    return;
                }
                case "say":
                    panel.FiveMSay_U12("game: " + (d?.Text("text", 0, "") ?? ""));
                    return;
                case "pong":
                case "bye":
                    return;
                default:
                    if (!HandleFiveMMessage_U13(m, d)) panel.FiveMSay_U12("unknown message from the game: " + m.Command);
                    return;
            }
        }

        private string CameraJson_U12() =>
            "{\"type\":\"camera\",\"pos\":" + DccBridgeProtocol.V(camera.Position) + ",\"target\":" + DccBridgeProtocol.V(camera.Target) +
            ",\"fov\":" + DccBridgeProtocol.N(MathUtil.RadiansToDegrees(camera.FieldOfView)) + "}";

        public static string TimeJson_U12(float hour)
        {
            float h = ((hour % 24.0f) + 24.0f) % 24.0f;
            int hh = (int)Math.Floor(h);
            int mm = (int)Math.Floor((h - hh) * 60.0f);
            return "{\"type\":\"time\",\"hour\":" + hh + ",\"minute\":" + mm + "}";
        }

        public static string GameWeatherName_U12(string preset)
        {
            switch ((preset ?? "").Trim().ToLowerInvariant())
            {
                case "extra sunny": return "EXTRASUNNY";
                case "cloudy": return "CLOUDS";
                case "overcast": return "OVERCAST";
                case "rain": return "RAIN";
                case "storm": return "THUNDER";
                case "fog": return "FOGGY";
                case "snow": return "SNOW";
                default: return "CLEAR";
            }
        }

        public static Vector3 CamDirFromGameRot_U12(float pitchDeg, float yawDeg)
        {
            double p = MathUtil.DegreesToRadians(pitchDeg), y = MathUtil.DegreesToRadians(yawDeg);
            return new Vector3((float)(-Math.Sin(y) * Math.Cos(p)), (float)(Math.Cos(y) * Math.Cos(p)), (float)Math.Sin(p));
        }

        private static string ClearLightsJson_U12() => "{\"type\":\"lights\",\"props\":[]}";

        private string LightsJson_U12()
        {
            var sb = new StringBuilder("{\"type\":\"lights\",\"spawn\":true,\"props\":[");
            bool first = true;
            int placed = TouchFiveMFiles_U18();
            foreach (var f in scene.Files)
            {
                string model = Path.GetFileNameWithoutExtension(f.Path ?? "").ToLowerInvariant();
                if (model.Length == 0) continue;
                if (!SendPlacedFile_U18(placed, f.HasPlacement, fivemTouched_U18.Contains(f))) continue;
                var inv = f.HasPlacement ? Matrix.Invert(f.Placement) : Matrix.Identity;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"model\":").Append(DccBridgeProtocol.S(model));
                if (f.HasPlacement) sb.Append(",\"place\":").Append(PlaceJson_U14(f.Placement));
                sb.Append(",\"lights\":[");
                bool firstLight = true;
                for (int i = 0; i < scene.Lights.Count; i++)
                {
                    var l = scene.Lights[i];
                    if (!ReferenceEquals(scene.OwnerFile(l), f)) continue;
                    if ((l.Flags & LightDefs.FlagCoronaOnly) != 0) continue;
                    var inst = scene.GetInstance(l);
                    var pos = Vector3.TransformCoordinate(inst.WorldPosition, inv);
                    var dir = Vector3.TransformNormal(inst.WorldDirection, inv);
                    var tan = Vector3.TransformNormal(inst.WorldTangent, inv);
                    if (!firstLight) sb.Append(',');
                    firstLight = false;
                    sb.Append(FiveMLightJson_U12(i, l, pos, dir, tan));
                }
                sb.Append("]}");
            }
            return sb.Append("]}").ToString();
        }

        public static string FiveMLightJson_U12(int index, LightAttributes l, Vector3 pos, Vector3 dir, Vector3 tan)
        {
            const uint shadowFlags = 0x40u | 0x80u | 0x100u | 0x1000000u;
            string kind = l.Type == LightType.Spot ? "spot" : (l.Type == LightType.Capsule ? "capsule" : "point");
            var sb = new StringBuilder("{\"i\":").Append(index)
                .Append(",\"kind\":").Append(DccBridgeProtocol.S(kind))
                .Append(",\"pos\":").Append(DccBridgeProtocol.V(pos))
                .Append(",\"dir\":").Append(DccBridgeProtocol.V(dir))
                .Append(",\"tan\":").Append(DccBridgeProtocol.V(tan))
                .Append(",\"rgb\":[").Append(l.ColorR).Append(',').Append(l.ColorG).Append(',').Append(l.ColorB).Append(']')
                .Append(",\"intensity\":").Append(DccBridgeProtocol.N(l.Intensity))
                .Append(",\"range\":").Append(DccBridgeProtocol.N(l.Falloff))
                .Append(",\"exp\":").Append(DccBridgeProtocol.N(l.FalloffExponent))
                .Append(",\"inner\":").Append(DccBridgeProtocol.N(l.ConeInnerAngle))
                .Append(",\"outer\":").Append(DccBridgeProtocol.N(l.ConeOuterAngle))
                .Append(",\"extent\":").Append(DccBridgeProtocol.N(l.Extent.X))
                .Append(",\"time\":").Append(l.TimeFlags & 0xFFFFFFu)
                .Append(",\"shadow\":").Append((l.Flags & shadowFlags) != 0 ? "true" : "false")
                .Append(",\"flash\":").Append(l.Flashiness)
                .Append('}');
            return sb.ToString();
        }

        public static string ResourceNameForPath_U12(string path)
        {
            try
            {
                var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "");
                for (int depth = 0; dir != null && depth < 12; depth++, dir = dir.Parent)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "fxmanifest.lua")) || File.Exists(Path.Combine(dir.FullName, "__resource.lua")))
                        return dir.Name;
                }
            }
            catch { }
            return null;
        }

        private void AfterSaveFiveM_U12(IEnumerable<LoadedFile> files)
        {
            if (files == null) return;
            AfterSaveFiveM_U12(files.Select(f => f?.Path).Where(p => !string.IsNullOrEmpty(p)).ToArray());
        }

        private void AfterSaveFiveM_U12(params string[] paths)
        {
            if (panel == null || !panel.FiveMRestartAfterSave_U12 || paths == null) return;
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in paths)
            {
                var res = ResourceNameForPath_U12(p);
                if (res == null || !done.Add(res)) continue;
                RestartFiveMResource_U12(res);
            }
        }

        private void RestartFiveMResource_U12(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource)) return;
            panel.FiveMRestartName_U12 = resource;
            int n = BroadcastFiveM_U12("{\"type\":\"restart\",\"resource\":" + DccBridgeProtocol.S(resource) + "}");
            panel.FiveMSay_U12(n > 0 ? "asked the game to restart " + resource : "saved into " + resource + " but the game is not connected");
        }

        public static string LanAddress_U12()
        {
            try
            {
                foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) return a.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        public static string InstallFiveMResource_U12(string resourcesFolder, int port, string host)
        {
            if (string.IsNullOrWhiteSpace(resourcesFolder)) throw new ArgumentException("pick the server's resources folder first");
            var dir = Path.Combine(resourcesFolder, "ragetools_linking");
            Directory.CreateDirectory(Path.Combine(dir, "html"));
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (var name in FiveMResourceFiles_U12)
            {
                using var src = asm.GetManifestResourceStream("fivem/" + name) ?? throw new FileNotFoundException("fivem/" + name + " is not in the build");
                using var dst = File.Create(Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar)));
                src.CopyTo(dst);
            }
            File.WriteAllText(Path.Combine(dir, "config.lua"),
                "Config = { host = \"" + host + "\", port = " + port.ToString(CultureInfo.InvariantCulture) + ", version = " + FiveMResourceVersion_U16.ToString(CultureInfo.InvariantCulture) + " }\n");
            return dir;
        }
    }
}
