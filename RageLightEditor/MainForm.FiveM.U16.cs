using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public const int FiveMResourceVersion_U16 = 5;

        private AsiClient_U16 asi_U16;
        private readonly Dictionary<string, string> asiSent_U16 = new Dictionary<string, string>();
        private double fivemPingAt_U16, asiPushAt_U16, asiInstalledCheckAt_U16;
        private bool fivemExitHooked_U16, gameViewRestored_U16, gameViewPlacementDirty_U16, gameViewRestoredPlacement_U16, asiAutoInstalled_U16;
        private double gameViewPlacementAt_U16;
        private GameViewForm_U16 gameViewForm_U16;

        public static string AsiPluginPath_U16() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveM", "FiveM.app", "plugins", "RageToolsLive.asi");

        private void Tick_FiveM_U16()
        {
            EnsureExitHook_U16();
            if (settings != null && !gameViewRestored_U16)
            {
                gameViewRestored_U16 = true;
                panel.FiveMAsiEnabled_U16 = settings.FiveMAsiEnabled;
                if (settings.FiveMGameViewDetached && screenshotPath == null) panel.GameViewDetached_U16 = true;
            }
            double now = fivemClock_U12.Elapsed.TotalSeconds;
            var b = fivem_U12;
            if (b != null && b.Listening && b.Clients > 0 && now - fivemPingAt_U16 > 2.0)
            {
                fivemPingAt_U16 = now;
                BroadcastFiveM_U12("{\"type\":\"ping\"}");
            }
            TickAsi_U16(now);
            TickGameViewDetach_U16(now);
            if (settings != null)
            {
                settings.FiveMAsiEnabled = panel.FiveMAsiEnabled_U16;
                settings.FiveMGameViewDetached = panel.GameViewDetached_U16;
            }
        }

        private void EnsureExitHook_U16()
        {
            if (fivemExitHooked_U16) return;
            fivemExitHooked_U16 = true;
            FormClosing += (s, e) =>
            {
                try
                {
                    int n = BroadcastFiveM_U12("{\"type\":\"bye\"}");
                    asi_U16?.SendNow("reset");
                    if (n > 0) Thread.Sleep(150);
                }
                catch { }
            };
        }

        private void TickAsi_U16(double now)
        {
            asi_U16 ??= new AsiClient_U16();
            asi_U16.Start();
            bool headlessOk = Environment.GetEnvironmentVariable("RLE_ASI") == "1";
            asi_U16.Wanted = panel.FiveMLive_U12 && panel.FiveMAsiEnabled_U16 && (screenshotPath == null || headlessOk);
            panel.FiveMAsiConnected_U16 = asi_U16.Connected;
            if (now - asiInstalledCheckAt_U16 > 2.0)
            {
                asiInstalledCheckAt_U16 = now;
                panel.FiveMAsiPath_U16 = AsiPluginPath_U16();
                panel.FiveMAsiInstalled_U16 = File.Exists(panel.FiveMAsiPath_U16);
                if (asi_U16.Wanted && !panel.FiveMAsiInstalled_U16 && !asiAutoInstalled_U16 && Directory.Exists(Path.GetDirectoryName(panel.FiveMAsiPath_U16)))
                {
                    asiAutoInstalled_U16 = true;
                    InstallAsi_U16();
                }
            }
            panel.FiveMAsiStatus_U16 = !asi_U16.Wanted ? "off (turn Live on)"
                : asi_U16.Connected ? asi_U16.Status
                : !panel.FiveMAsiInstalled_U16 ? "plugin not installed yet - press Install below"
                : "plugin installed - restart FiveM so it loads, then it connects on its own";
            while (asi_U16.Notes.TryDequeue(out var note)) panel.FiveMSay_U12(note);
            if (panel.RequestInstallAsi_U16)
            {
                panel.RequestInstallAsi_U16 = false;
                InstallAsi_U16();
            }
            if (panel.RequestAsiReset_U16)
            {
                panel.RequestAsiReset_U16 = false;
                asi_U16.Send("reset");
                asiSent_U16.Clear();
                panel.FiveMSay_U12("asked the plugin to restore the game's materials");
            }
            if (!asi_U16.Connected) { asiSent_U16.Clear(); return; }
            if (panel.Workspace != LightPanel.Space.Material || materialPanel == null) return;
            if (now - asiPushAt_U16 < 0.1) return;
            asiPushAt_U16 = now;
            var mat = materialPanel.Selected;
            if (mat?.Shader?.ParametersList == null || mat.File == null) return;
            ModelAndDict_U16(mat, out var model, out var dict);
            foreach (var (key, line) in AsiLines_U16(model, dict, mat.Index, mat.Shader))
            {
                if (asiSent_U16.TryGetValue(key, out var prev) && prev == line) continue;
                asiSent_U16[key] = line;
                asi_U16.Send(line);
            }
        }

        private static void ModelAndDict_U16(MaterialRef mat, out string model, out string dict)
        {
            string stem = Path.GetFileNameWithoutExtension(mat.File?.Path ?? "").ToLowerInvariant().Replace(' ', '_');
            string ext = Path.GetExtension(mat.File?.Path ?? "").ToLowerInvariant();
            dict = "";
            model = stem;
            if (ext == ".ydd")
            {
                dict = stem;
                var name = (mat.Drawable as Drawable)?.Name;
                if (!string.IsNullOrEmpty(name)) model = Path.GetFileNameWithoutExtension(name).ToLowerInvariant().Replace(' ', '_');
            }
        }

        private static string F4_U16(Vector4 v) =>
            v.X.ToString("0.######", CultureInfo.InvariantCulture) + " " + v.Y.ToString("0.######", CultureInfo.InvariantCulture) + " " +
            v.Z.ToString("0.######", CultureInfo.InvariantCulture) + " " + v.W.ToString("0.######", CultureInfo.InvariantCulture);

        public static IEnumerable<(string key, string line)> AsiLines_U16(string model, string dict, int shaderIndex, ShaderFX s)
        {
            var pl = s?.ParametersList;
            if (pl?.Parameters == null || pl.Hashes == null || string.IsNullOrEmpty(model)) yield break;
            for (int i = 0; i < pl.Parameters.Length && i < pl.Hashes.Length; i++)
            {
                var p = pl.Parameters[i];
                if (p == null || p.DataType == 0) continue;
                uint hash = (uint)pl.Hashes[i];
                var sb = new StringBuilder("set ").Append(model).Append(' ').Append(string.IsNullOrEmpty(dict) ? "-" : dict)
                    .Append(' ').Append(shaderIndex).Append(' ').Append(hash).Append(' ');
                if (p.Data is Vector4 v) sb.Append("1 ").Append(F4_U16(v));
                else if (p.Data is Vector4[] arr && arr.Length > 0)
                {
                    sb.Append(arr.Length);
                    foreach (var a in arr) sb.Append(' ').Append(F4_U16(a));
                }
                else continue;
                yield return (model + "/" + shaderIndex + "/" + hash, sb.ToString());
            }
        }

        private void InstallAsi_U16()
        {
            try
            {
                var path = AsiPluginPath_U16();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using var src = asm.GetManifestResourceStream("fivem/RageToolsLive.asi") ?? throw new FileNotFoundException("the plugin is not in this build");
                using (var dst = File.Create(path)) src.CopyTo(dst);
                panel.FiveMAsiInstalled_U16 = true;
                panel.FiveMSay_U12("plugin written to " + path + " - start FiveM (or restart it) so it loads");
            }
            catch (IOException ex) { panel.FiveMSay_U12("could not write the plugin (close FiveM first?): " + ex.Message); }
            catch (Exception ex) { panel.FiveMSay_U12("could not write the plugin: " + ex.Message); }
        }

        private void CheckResourceVersion_U16(DccMessage d)
        {
            int got = d?.Int("version", -1, 0) ?? 0;
            if (got == FiveMResourceVersion_U16) { panel.FiveMWarning_U16 = ""; return; }
            var folder = panel.FiveMResourcesFolder_U12;
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(Path.Combine(folder, "ragetools_linking")))
            {
                try
                {
                    InstallFiveMResource_U12(folder, panel.FiveMPort_U12, panel.FiveMAnyInterface_U12 ? LanAddress_U12() : "127.0.0.1");
                    RestartFiveMResource_U12("ragetools_linking");
                    panel.FiveMSay_U12($"the game's resource was version {got}, rewritten as version {FiveMResourceVersion_U16} and restarted");
                    panel.FiveMWarning_U16 = "";
                    return;
                }
                catch (Exception ex) { panel.FiveMSay_U12("could not rewrite the resource: " + ex.Message); }
            }
            panel.FiveMWarning_U16 = $"The ragetools_linking resource in your server is version {got}; this tool needs version {FiveMResourceVersion_U16}. " +
                                     "Set the resources folder below, press Install / update the resource, then restart it (or the server).";
        }

        private string WorldLightsJson_U16()
        {
            if (!panel.WorldMode || worldRender == null || WorldEdit == null) return null;
            var sel = WorldEdit.Selection;
            var e = sel.LightEntity;
            if (sel.Light == null || e?.Archetype == null) return null;
            uint hash = e.Archetype.Hash;
            if (!worldRender.Lights.TryGetDefs(hash, out var defs) || defs == null) return null;
            var places = new List<(Vector3 pos, Quaternion rot, Vector3 scale)> { (e.Position, e.Orientation, e.Scale) };
            var others = new List<(float d, YmapEntityDef x)>();
            var visible = World?.Visible;
            if (visible != null)
                foreach (var x in visible)
                {
                    if (x == null || ReferenceEquals(x, e) || x.Archetype == null || x.Archetype.Hash != hash) continue;
                    others.Add(((x.Position - e.Position).LengthSquared(), x));
                }
            others.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var o in others.Take(47)) places.Add((o.x.Position, o.x.Orientation, o.x.Scale));
            return WorldLightsJsonFor_U16(e.Archetype.Name.ToString(), places, defs);
        }

        public static string WorldLightsJsonFor_U16(string model, IList<(Vector3 pos, Quaternion rot, Vector3 scale)> places, WorldLights.LightDef[] defs)
        {
            var sb = new StringBuilder("{\"type\":\"lights\",\"spawn\":false,\"props\":[");
            for (int i = 0; i < places.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var p = places[i];
                var one = WorldPropJson_U14(model, p.pos, p.rot, p.scale, defs);
                sb.Append("{\"key\":").Append(DccBridgeProtocol.S(model + "#" + i)).Append(',').Append(one, 1, one.Length - 1);
            }
            return sb.Append("]}").ToString();
        }

        public static void FitRect_U16(int availW, int availH, float aspect, out int w, out int h)
        {
            aspect = Math.Max(aspect, 0.01f);
            w = Math.Max(availW, 1);
            h = (int)Math.Round(w * aspect);
            if (h > availH)
            {
                h = Math.Max(availH, 1);
                w = Math.Max((int)Math.Round(h / aspect), 1);
            }
        }

        private void TickGameViewDetach_U16(double now)
        {
            if (panel.RequestGameViewDetach_U16)
            {
                panel.RequestGameViewDetach_U16 = false;
                panel.GameViewDetached_U16 = true;
                panel.GameViewOpen_U13 = true;
                gameThumb_U14?.Unregister();
            }
            if (panel.RequestGameViewAttach_U16)
            {
                panel.RequestGameViewAttach_U16 = false;
                panel.GameViewDetached_U16 = false;
                gameThumb_U14?.Unregister();
                if (!IsHeadless) { try { Activate(); } catch { } }
            }
            bool want = panel.GameViewDetached_U16 && panel.GameViewOpen_U13 && screenshotPath == null;
            if (!want)
            {
                if (gameViewForm_U16 != null && gameViewForm_U16.Visible) gameViewForm_U16.Hide();
                FlushGameViewPlacement_U16(now, true);
                return;
            }
            if (gameViewForm_U16 == null) CreateGameViewForm_U16();
            if (!gameViewForm_U16.Visible) gameViewForm_U16.Show(this);
            gameViewForm_U16.ShowDebug = panel.GameDebug_U13;
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(panel.GameViewStatus_U13)) lines.Add(panel.GameViewStatus_U13);
            lines.AddRange(panel.GameDebugLines_U13);
            gameViewForm_U16.SetDebug(string.Join(Environment.NewLine, lines));
            FlushGameViewPlacement_U16(now, false);
        }

        private void CreateGameViewForm_U16()
        {
            gameViewForm_U16 = new GameViewForm_U16();
            gameViewForm_U16.AttachRequested += () => panel.RequestGameViewAttach_U16 = true;
            gameViewForm_U16.PlacementChanged += () =>
            {
                if (gameViewForm_U16 == null || !gameViewForm_U16.Visible) return;
                gameViewPlacementDirty_U16 = true;
                gameViewPlacementAt_U16 = fivemClock_U12.Elapsed.TotalSeconds;
            };
            var b = settings?.FiveMGameViewBounds;
            if (b != null && b.Length == 4 && b[2] >= 320 && b[3] >= 220)
            {
                var rect = new System.Drawing.Rectangle(b[0], b[1], b[2], b[3]);
                if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect))) gameViewForm_U16.Bounds = rect;
                else gameViewForm_U16.StartPosition = FormStartPosition.CenterParent;
            }
            else gameViewForm_U16.StartPosition = FormStartPosition.CenterParent;
        }

        private void FlushGameViewPlacement_U16(double now, bool force)
        {
            if (!gameViewPlacementDirty_U16 || gameViewForm_U16 == null || settings == null) return;
            if (!force && now - gameViewPlacementAt_U16 < 1.0) return;
            gameViewPlacementDirty_U16 = false;
            var r = gameViewForm_U16.WindowState == FormWindowState.Normal ? gameViewForm_U16.Bounds : gameViewForm_U16.RestoreBounds;
            settings.FiveMGameViewBounds = new[] { r.X, r.Y, r.Width, r.Height };
        }

        internal IntPtr GameViewDestination_U16() =>
            panel.GameViewDetached_U16 && gameViewForm_U16 != null && gameViewForm_U16.IsHandleCreated ? gameViewForm_U16.Handle : Handle;

        internal bool GameViewDetachedRect_U16(out int x, out int y, out int w, out int h)
        {
            x = y = w = h = 0;
            if (!panel.GameViewDetached_U16 || gameViewForm_U16 == null || !gameViewForm_U16.Visible) return false;
            var area = gameViewForm_U16.PictureArea();
            FitRect_U16(area.Width, area.Height, panel.GameViewAspect_U14, out int fw, out int fh);
            x = area.X + (area.Width - fw) / 2;
            y = area.Y + (area.Height - fh) / 2;
            w = fw;
            h = fh;
            return true;
        }
    }
}
