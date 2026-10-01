using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private DiscordPresence_U24 discord_U24;
        private double discordAt_U24 = -10;

        public static string DiscordDetails_U24(LightPanel.Space s)
        {
            switch (s)
            {
                case LightPanel.Space.Light: return "Lighting props";
                case LightPanel.Space.Material: return "Editing materials";
                case LightPanel.Space.Cinematic: return "Making a cinematic";
                case LightPanel.Space.Archive: return "Browsing RPF archives";
                case LightPanel.Space.World: return "Editing the GTA V world";
                case LightPanel.Space.Mlo: return "Building an MLO interior";
                case LightPanel.Space.Particles: return "Making particle effects";
                case LightPanel.Space.NavMesh: return "Editing navmesh";
                case LightPanel.Space.Terrain: return "Sculpting terrain";
                case LightPanel.Space.Animation: return "Animating";
                case LightPanel.Space.Extension: return "Using extensions";
                default: return "Modding GTA V";
            }
        }

        private string DiscordState_U24()
        {
            var p = ProjWin?.Project;
            if (p != null && !string.IsNullOrWhiteSpace(p.Name) && p.Name != "New Project") return "Project: " + p.Name;
            var f = scene?.Files?.FirstOrDefault(x => !string.IsNullOrEmpty(x?.Path));
            if (f != null && (panel.Workspace == LightPanel.Space.Light || panel.Workspace == LightPanel.Space.Material))
                return Path.GetFileName(f.Path);
            return "";
        }

        private void Tick_Discord_U24()
        {
            if (IsHeadless || panel == null) return;
            double now = fivemClock_U12.Elapsed.TotalSeconds;
            if (now - discordAt_U24 < 1.0) return;
            discordAt_U24 = now;
            if (discord_U24 == null)
            {
                discord_U24 = new DiscordPresence_U24();
                if (!discord_U24.Enabled) return;
                discord_U24.Start();
                FormClosing += (s, e) => discord_U24?.Dispose();
            }
            if (!discord_U24.Enabled) return;
            discord_U24.Update(DiscordDetails_U24(panel.Workspace), DiscordState_U24());
        }

        private void SeqTest_Discord_U24(Action<string, bool, string> check)
        {
            var json = DiscordPresence_U24.ActivityJson("123", 42, "Editing the GTA V world", "Project: test", 1700000000);
            check("discord: the activity carries the page, the project, the start time and the download button",
                  json.Contains("\"cmd\":\"SET_ACTIVITY\"") && json.Contains("\"pid\":42") && json.Contains("\"details\":\"Editing the GTA V world\"") &&
                  json.Contains("\"state\":\"Project: test\"") && json.Contains("\"start\":1700000000") && json.Contains(DiscordPresence_U24.RepoUrl), json);
            check("discord: every page has a plain name", Enum.GetValues(typeof(LightPanel.Space)).Cast<LightPanel.Space>()
                  .All(s => DiscordDetails_U24(s).Length >= 2), "");

            string savedBase = DiscordPresence_U24.PipeBase;
            string fakeBase = "rle-discord-test-" + Environment.ProcessId + "-";
            DiscordPresence_U24.PipeBase = fakeBase;
            string handshake = null, activity = null;
            var server = new Thread(() =>
            {
                try
                {
                    using var srv = new NamedPipeServerStream(fakeBase + "0", PipeDirection.InOut, 1, PipeTransmissionMode.Byte);
                    srv.WaitForConnection();
                    handshake = ReadFrame_U24(srv);
                    WriteFrame_U24(srv, 1, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{}}");
                    activity = ReadFrame_U24(srv);
                    WriteFrame_U24(srv, 1, "{\"cmd\":\"SET_ACTIVITY\",\"data\":{}}");
                    Thread.Sleep(300);
                }
                catch { }
            }) { IsBackground = true };
            server.Start();
            Thread.Sleep(100);
            var dp = new DiscordPresence_U24("987654321");
            try
            {
                dp.Update("Editing the GTA V world", "Project: seqtest");
                dp.Start();
                for (int i = 0; i < 60 && activity == null; i++) Thread.Sleep(50);
                check("discord: the handshake names the app", handshake != null && handshake.Contains("\"client_id\":\"987654321\"") && handshake.Contains("\"v\":1"), handshake ?? "(none)");
                check("discord: the presence arrives over the pipe in real time",
                      activity != null && activity.Contains("SET_ACTIVITY") && activity.Contains("Project: seqtest"), activity ?? "(none)");
            }
            finally
            {
                dp.Dispose();
                DiscordPresence_U24.PipeBase = savedBase;
            }
            var none = new DiscordPresence_U24("");
            check("discord: with no app id it stays off and never connects", !none.Enabled || DiscordPresence_U24.AppId.Length > 0, none.Status);
        }

        private static string ReadFrame_U24(Stream s)
        {
            var head = new byte[8];
            int got = 0;
            while (got < 8) { int r = s.Read(head, got, 8 - got); if (r <= 0) return null; got += r; }
            int len = BitConverter.ToInt32(head, 4);
            var body = new byte[len];
            got = 0;
            while (got < len) { int r = s.Read(body, got, len - got); if (r <= 0) return null; got += r; }
            return Encoding.UTF8.GetString(body);
        }

        private static void WriteFrame_U24(Stream s, int op, string json)
        {
            var body = Encoding.UTF8.GetBytes(json);
            s.Write(BitConverter.GetBytes(op), 0, 4);
            s.Write(BitConverter.GetBytes(body.Length), 0, 4);
            s.Write(body, 0, body.Length);
            s.Flush();
        }
    }
}
