using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void SeqTest_FiveM_U12(Action<string, bool, string> check)
        {
            try
            {
                string accept = FiveMBridge.AcceptKey("dGhlIHNhbXBsZSBub25jZQ==");
                check("fivem: the WebSocket accept key matches the RFC example", accept == "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", accept);

                var small = FiveMBridge.EncodeFrame("hi");
                bool ok = FiveMBridge.TryDecodeFrame(small, small.Length, out int op, out bool fin, out var pl, out int used);
                check("fivem: a short frame round-trips", ok && op == 1 && fin && used == small.Length && Encoding.UTF8.GetString(pl) == "hi", $"op {op} used {used}/{small.Length}");

                var mid = FiveMBridge.EncodeFrame(new string('m', 300));
                ok = FiveMBridge.TryDecodeFrame(mid, mid.Length, out op, out fin, out pl, out used);
                check("fivem: a 300 byte frame uses the 16-bit length", ok && mid[1] == 126 && pl.Length == 300 && used == 304, $"len byte {mid[1]} payload {pl?.Length}");

                var big = FiveMBridge.EncodeFrame(new string('b', 70000));
                ok = FiveMBridge.TryDecodeFrame(big, big.Length, out op, out fin, out pl, out used);
                check("fivem: a 70000 byte frame uses the 64-bit length", ok && big[1] == 127 && pl.Length == 70000 && used == 70010, $"len byte {big[1]} payload {pl?.Length}");

                var masked = FiveMBridge.MaskedFrame("{\"type\":\"player\"}", new byte[] { 1, 2, 3, 4 });
                ok = FiveMBridge.TryDecodeFrame(masked, masked.Length, out op, out fin, out pl, out used);
                check("fivem: a masked client frame decodes to the text", ok && Encoding.UTF8.GetString(pl) == "{\"type\":\"player\"}", ok ? Encoding.UTF8.GetString(pl) : "no");
                ok = FiveMBridge.TryDecodeFrame(masked, masked.Length - 3, out _, out _, out _, out _);
                check("fivem: a frame that is still arriving is left alone", !ok, "");

                var m = FiveMBridge.Parse("{\"type\":\"player\",\"pos\":[1,2,3],\"fov\":50}") as DccMessage;
                check("fivem: a game message parses by its type",
                      m != null && m.Command == "player" && m.Vec("pos", -1, Vector3.Zero) == new Vector3(1, 2, 3) && Math.Abs(m.Num("fov", -1, 0) - 50) < 1e-5f,
                      m?.Command ?? "null");
                check("fivem: an api verb from the game keeps its name", FiveMBridge.Parse("{\"type\":\"api.status\"}")?.Command == "api.status", "");
                check("fivem: junk is refused", FiveMBridge.Parse("hello") == null && FiveMBridge.Parse("{\"x\":1}") == null, "");

                var l = new LightAttributes
                {
                    Type = LightType.Spot, ColorR = 255, ColorG = 128, ColorB = 0, Intensity = 12.5f, Falloff = 8f, FalloffExponent = 32f,
                    ConeInnerAngle = 10f, ConeOuterAngle = 35f, TimeFlags = Scene.AllHoursTimeFlags, Flags = 0x40, Flashiness = 3,
                };
                var lj = FiveMLightJson_U12(4, l, new Vector3(0, 1, 2), new Vector3(0, 0, -1), new Vector3(1, 0, 0));
                check("fivem: a spot light's JSON carries what the game needs",
                      lj.Contains("\"kind\":\"spot\"") && lj.Contains("\"rgb\":[255,128,0]") && lj.Contains("\"intensity\":12.5") && lj.Contains("\"outer\":35") &&
                      lj.Contains("\"time\":16777215") && lj.Contains("\"shadow\":true") && lj.Contains("\"pos\":[0,1,2]"), lj);
                l.Flags = 0;
                check("fivem: no shadow flag means shadow false", FiveMLightJson_U12(0, l, Vector3.Zero, Vector3.Zero, Vector3.Zero).Contains("\"shadow\":false"), "");

                check("fivem: tool weather names map to game weather",
                      GameWeatherName_U12("Extra sunny") == "EXTRASUNNY" && GameWeatherName_U12("Storm") == "THUNDER" && GameWeatherName_U12("Fog") == "FOGGY" && GameWeatherName_U12("nothing") == "CLEAR", "");
                check("fivem: the clock message splits hours and minutes", TimeJson_U12(21.5f) == "{\"type\":\"time\",\"hour\":21,\"minute\":30}", TimeJson_U12(21.5f));

                var d0 = CamDirFromGameRot_U12(0, 0);
                var d90 = CamDirFromGameRot_U12(0, 90);
                var dUp = CamDirFromGameRot_U12(90, 0);
                check("fivem: game camera rotation turns into a direction",
                      (d0 - Vector3.UnitY).Length() < 1e-4f && (d90 + Vector3.UnitX).Length() < 1e-4f && (dUp - Vector3.UnitZ).Length() < 1e-4f, $"{d0} {d90} {dUp}");

                string tmp = Path.Combine(Path.GetTempPath(), "rle_u12_fivem_" + Environment.ProcessId);
                try { Directory.Delete(tmp, true); } catch { }
                var resDir = Path.Combine(tmp, "resources", "[maps]", "my_mlo", "stream");
                Directory.CreateDirectory(resDir);
                File.WriteAllText(Path.Combine(tmp, "resources", "[maps]", "my_mlo", "fxmanifest.lua"), "fx_version 'cerulean'\n");
                var ydr = Path.Combine(resDir, "thing.ydr");
                File.WriteAllText(ydr, "x");
                check("fivem: a saved file finds the resource it lives in", ResourceNameForPath_U12(ydr) == "my_mlo", ResourceNameForPath_U12(ydr) ?? "null");
                var loose = Path.Combine(tmp, "loose.ydr");
                File.WriteAllText(loose, "x");
                check("fivem: a file outside any resource restarts nothing", ResourceNameForPath_U12(loose) == null, ResourceNameForPath_U12(loose) ?? "null");

                var installed = InstallFiveMResource_U12(Path.Combine(tmp, "resources"), 27018, "127.0.0.1");
                bool allThere = FiveMResourceFiles_U12.All(f => File.Exists(Path.Combine(installed, f.Replace('/', Path.DirectorySeparatorChar)))) && File.Exists(Path.Combine(installed, "config.lua"));
                check("fivem: installing writes the whole resource", allThere && installed.EndsWith("ragetools_linking"), installed);
                var cfg = File.ReadAllText(Path.Combine(installed, "config.lua"));
                var manifest = File.ReadAllText(Path.Combine(installed, "fxmanifest.lua"));
                check("fivem: the resource carries the port and a NUI page",
                      cfg.Contains("port = 27018") && manifest.Contains("ui_page") && manifest.Contains("client.lua") && manifest.Contains("server.lua"), cfg.Trim());
                var lua = File.ReadAllText(Path.Combine(installed, "client.lua"));
                check("fivem: the client draws lights with the game's own light natives", lua.Contains("DrawSpotLight") && lua.Contains("DrawLightWithRange") && lua.Contains("RegisterKeyMapping"), "");
                check("fivem: inside an interior the client never spawns a copy over the real model",
                      lua.Contains("GetInteriorAtCoords") && lua.Contains("and inInterior(p) then") && lua.Contains("and e.orig and inInterior(e.orig.pos) then"), "");
                try { Directory.Delete(tmp, true); } catch { }

                SeqTest_FiveMSocket_U12(check);
            }
            catch (Exception ex) { check("fivem: no exception", false, ex.ToString()); }
        }

        private void SeqTest_FiveMSocket_U12(Action<string, bool, string> check)
        {
            var b = new FiveMBridge();
            try
            {
                check("fivem: the link listens on a free port", b.Start(0) && b.Port > 0, b.Error);
                using var tcp = new TcpClient();
                tcp.Connect(IPAddress.Loopback, b.Port);
                var ns = tcp.GetStream();
                ns.ReadTimeout = 4000;
                string key = Convert.ToBase64String(Enumerable.Range(0, 16).Select(i => (byte)(i * 7)).ToArray());
                var req = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: " + key + "\r\nSec-WebSocket-Version: 13\r\n\r\n");
                ns.Write(req, 0, req.Length);
                var head = new StringBuilder();
                var one = new byte[1];
                while (!head.ToString().EndsWith("\r\n\r\n") && head.Length < 4096)
                {
                    if (ns.Read(one, 0, 1) <= 0) break;
                    head.Append((char)one[0]);
                }
                var reply = head.ToString();
                check("fivem: the handshake is answered with 101 and the right accept key",
                      reply.StartsWith("HTTP/1.1 101") && reply.Contains("Sec-WebSocket-Accept: " + FiveMBridge.AcceptKey(key)), reply.Split('\r')[0]);

                var frame = FiveMBridge.MaskedFrame("{\"type\":\"hello\",\"player\":\"tester\"}", new byte[] { 9, 8, 7, 6 });
                ns.Write(frame, 0, frame.Length);
                MloBridgeMessage got = null;
                for (int i = 0; i < 400 && got == null; i++)
                {
                    got = b.Drain().FirstOrDefault();
                    if (got == null) Thread.Sleep(10);
                }
                check("fivem: a masked hello from the game reaches the inbox",
                      got != null && got.Command == "hello" && (got as DccMessage)?.Text("player", 0, "") == "tester" && b.Clients == 1, got?.Command ?? "nothing");

                got?.From?.Send("{\"type\":\"probe\",\"n\":1}");
                var buf = new byte[256];
                int have = 0;
                string text = null;
                for (int i = 0; i < 400 && text == null; i++)
                {
                    if (ns.DataAvailable) have += ns.Read(buf, have, buf.Length - have);
                    if (FiveMBridge.TryDecodeFrame(buf, have, out _, out _, out var pl, out _)) text = Encoding.UTF8.GetString(pl);
                    else Thread.Sleep(10);
                }
                check("fivem: a reply to that client arrives as one text frame", text == "{\"type\":\"probe\",\"n\":1}", text ?? "nothing");

                int n = b.Broadcast("{\"type\":\"lights\",\"props\":[]}");
                check("fivem: a broadcast reaches the connected game", n == 1, n.ToString());
                tcp.Close();
                for (int i = 0; i < 200 && b.Clients > 0; i++) Thread.Sleep(10);
                check("fivem: a game that leaves is forgotten", b.Clients == 0, b.Clients.ToString());
            }
            catch (Exception ex) { check("fivem socket: no exception", false, ex.ToString()); }
            finally { b.Stop(); }
        }
    }
}
