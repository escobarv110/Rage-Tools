using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace RageLightEditor.Editor
{
    public sealed class DiscordPresence_U24 : IDisposable
    {
        public const string AppId = "";
        public const string RepoUrl = "https://github.com/escobarv110/Rage-Tools";

        public static string PipeBase = "discord-ipc-";

        private readonly string appId;
        private readonly long startUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private readonly object sync = new object();
        private string wantDetails = "", wantState = "";
        private string sentKey;
        private Thread thread;
        private volatile bool stop;
        private NamedPipeClientStream pipe;

        public bool Connected { get; private set; }
        public string Status { get; private set; } = "off";
        public int Sent { get; private set; }

        public DiscordPresence_U24(string appId = null)
        {
            var env = Environment.GetEnvironmentVariable("RLE_DISCORD_APPID");
            this.appId = !string.IsNullOrWhiteSpace(appId) ? appId.Trim() : !string.IsNullOrWhiteSpace(env) ? env.Trim() : AppId;
        }

        public bool Enabled => !string.IsNullOrEmpty(appId) && Environment.GetEnvironmentVariable("RLE_NODISCORD") != "1";

        public void Start()
        {
            if (!Enabled || thread != null) return;
            thread = new Thread(Run) { IsBackground = true, Name = "DiscordPresence" };
            thread.Start();
        }

        public void Update(string details, string state)
        {
            lock (sync)
            {
                wantDetails = details ?? "";
                wantState = state ?? "";
            }
        }

        private void Run()
        {
            while (!stop)
            {
                try
                {
                    if (!Connected && !Connect()) { Wait(15000); continue; }
                    string d, s;
                    lock (sync) { d = wantDetails; s = wantState; }
                    string key = d + "\n" + s;
                    if (key != sentKey)
                    {
                        Send(1, ActivityJson(appId, Environment.ProcessId, d, s, startUnix));
                        ReadFrame(out _, out _);
                        sentKey = key;
                        Sent++;
                        Status = "showing: " + (string.IsNullOrEmpty(d) ? "RAGE Tools" : d);
                    }
                    Wait(4000);
                }
                catch (Exception ex)
                {
                    Drop("lost: " + ex.Message);
                    Wait(15000);
                }
            }
            try { if (Connected) Send(1, ClearJson(Environment.ProcessId)); } catch { }
            Drop("off");
        }

        private void Wait(int ms)
        {
            for (int t = 0; t < ms && !stop; t += 100) Thread.Sleep(100);
        }

        private bool Connect()
        {
            for (int i = 0; i < 10 && !stop; i++)
            {
                var p = new NamedPipeClientStream(".", PipeBase + i, PipeDirection.InOut, PipeOptions.None);
                try { p.Connect(200); }
                catch { p.Dispose(); continue; }
                pipe = p;
                try
                {
                    Send(0, "{\"v\":1,\"client_id\":" + JsonSerializer.Serialize(appId) + "}");
                    ReadFrame(out int op, out string body);
                    if (op != 1 || body.IndexOf("\"READY\"", StringComparison.Ordinal) < 0) { Drop("discord refused: " + body); return false; }
                    Connected = true;
                    sentKey = null;
                    Status = "connected";
                    return true;
                }
                catch (Exception ex) { Drop("handshake failed: " + ex.Message); }
            }
            if (!Connected) Status = "Discord is not running";
            return false;
        }

        private void Drop(string why)
        {
            Connected = false;
            Status = why;
            try { pipe?.Dispose(); } catch { }
            pipe = null;
        }

        private void Send(int op, string json)
        {
            var body = Encoding.UTF8.GetBytes(json);
            var head = new byte[8];
            BitConverter.GetBytes(op).CopyTo(head, 0);
            BitConverter.GetBytes(body.Length).CopyTo(head, 4);
            pipe.Write(head, 0, 8);
            pipe.Write(body, 0, body.Length);
            pipe.Flush();
        }

        private void ReadFrame(out int op, out string body)
        {
            var head = ReadExact(8);
            op = BitConverter.ToInt32(head, 0);
            int len = BitConverter.ToInt32(head, 4);
            if (len < 0 || len > 1 << 20) throw new IOException("bad frame");
            body = Encoding.UTF8.GetString(ReadExact(len));
            if (op == 2) throw new IOException("discord closed the link: " + body);
        }

        private byte[] ReadExact(int n)
        {
            var buf = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = pipe.Read(buf, got, n - got);
                if (r <= 0) throw new IOException("pipe closed");
                got += r;
            }
            return buf;
        }

        public static string ActivityJson(string appId, int pid, string details, string state, long startUnix)
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject();
                w.WriteString("cmd", "SET_ACTIVITY");
                w.WriteStartObject("args");
                w.WriteNumber("pid", pid);
                w.WriteStartObject("activity");
                if (!string.IsNullOrEmpty(details)) w.WriteString("details", Clip(details));
                if (!string.IsNullOrEmpty(state)) w.WriteString("state", Clip(state));
                w.WriteStartObject("timestamps");
                w.WriteNumber("start", startUnix);
                w.WriteEndObject();
                w.WriteStartObject("assets");
                w.WriteString("large_image", "rage_tools");
                w.WriteString("large_text", "RAGE Tools - GTA V / FiveM modding");
                w.WriteEndObject();
                w.WriteStartArray("buttons");
                w.WriteStartObject();
                w.WriteString("label", "Get RAGE Tools");
                w.WriteString("url", RepoUrl);
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteEndObject();
                w.WriteEndObject();
                w.WriteString("nonce", Guid.NewGuid().ToString());
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        public static string ClearJson(int pid) =>
            "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + pid + "},\"nonce\":\"" + Guid.NewGuid() + "\"}";

        private static string Clip(string s) => s.Length <= 128 ? s : s.Substring(0, 127) + "…";

        public void Dispose()
        {
            stop = true;
            try { thread?.Join(1500); } catch { }
        }
    }
}
