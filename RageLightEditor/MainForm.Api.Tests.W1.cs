using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        partial void SeqTest_W1(Action<string, bool, string> check)
        {
            var ui = Creator;
            var was = panel.Workspace;
            TcpClient tcp = null;
            try
            {
                var api = Api_W1();

                check("api: the plain spelling parses", DccBridgeProtocol.Parse("api.ping hi") is DccMessage, "api.ping hi");
                check("api: the JSON spelling parses", DccBridgeProtocol.Parse("{\"cmd\":\"api.ping\",\"id\":3}") is DccMessage, "cmd api.ping");
                check("api: a bare 'api.' is nobody's", DccBridgeProtocol.Parse("api.") == null, "null");
                check("api: 'apix' is not the family", DccBridgeProtocol.Parse("apix 1 2") == null, "null");

                {
                    var m = DccBridgeProtocol.Parse("{\"cmd\":\"api.ping\",\"id\":41}") as DccMessage;
                    check("api: a numeric id is echoed raw", ApiVerbs.IdOf(m) == "41", ApiVerbs.IdOf(m) ?? "(none)");
                    var ms = DccBridgeProtocol.Parse("{\"cmd\":\"api.ping\",\"id\":\"a-1\"}") as DccMessage;
                    check("api: a string id keeps its quotes", ApiVerbs.IdOf(ms) == "\"a-1\"", ApiVerbs.IdOf(ms) ?? "(none)");
                    var mp = DccBridgeProtocol.Parse("api.ping") as DccMessage;
                    check("api: the plain spelling has no id", ApiVerbs.IdOf(mp) == null, "(none)");
                    var ok = ApiVerbs.Reply("7", "api.x", "{\"a\":1}");
                    check("api: the reply envelope", ok == "{\"type\":\"api\",\"verb\":\"api.x\",\"id\":7,\"ok\":true,\"result\":{\"a\":1}}", ok);
                    var no = ApiVerbs.Fail(null, "api.x", "why");
                    check("api: the error envelope", no == "{\"type\":\"api\",\"verb\":\"api.x\",\"ok\":false,\"error\":\"why\"}", no);
                }

                int port = StartMloBridge(ui, 0);
                check("api: listening on a free port", port > 0, port > 0 ? $"port {port}" : ui.BridgeStatus);
                if (port <= 0) return;
                tcp = new TcpClient();
                tcp.Connect("127.0.0.1", port);
                tcp.ReceiveTimeout = 3000;
                var stream = tcp.GetStream();
                var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                var inbox = new StringBuilder();
                var rawIn = new byte[65536];
                var charsIn = new char[Encoding.UTF8.GetMaxCharCount(rawIn.Length)];
                var decoder = Encoding.UTF8.GetDecoder();
                bool Fill()
                {
                    int n = stream.Read(rawIn, 0, rawIn.Length);
                    if (n <= 0) return false;
                    inbox.Append(charsIn, 0, decoder.GetChars(rawIn, 0, n, charsIn, 0));
                    return true;
                }
                string TakeLine()
                {
                    var s = inbox.ToString();
                    int nl = s.IndexOf('\n');
                    if (nl < 0) return null;
                    inbox.Remove(0, nl + 1);
                    return s.Substring(0, nl).TrimEnd('\r');
                }
                string ReadLine()
                {
                    while (true)
                    {
                        var l = TakeLine();
                        if (l != null) return l;
                        if (!Fill()) return null;
                    }
                }

                void Pump(Func<bool> until)
                {
                    for (int i = 0; i < 2400 && !until(); i++) { TickMloBridge(ui); Tick_W1(); Thread.Sleep(5); }
                }
                var strays = new List<string>();
                string Ask(string line)
                {
                    int n = mloBridge.Received;
                    writer.WriteLine(line);
                    Pump(() => mloBridge.Received > n);
                    try
                    {
                        for (int i = 0; i < 32; i++)
                        {
                            var l = ReadLine();
                            if (l == null) return null;
                            if (l.Contains("\"type\":\"event\"")) { strays.Add(l); continue; }
                            return l;
                        }
                    }
                    catch { }
                    return null;
                }
                List<string> Pending()
                {
                    var got = new List<string>();
                    tcp.ReceiveTimeout = 250;
                    try { while (stream.DataAvailable) if (!Fill()) break; } catch { }
                    for (var l = TakeLine(); l != null; l = TakeLine()) got.Add(l);
                    tcp.ReceiveTimeout = 3000;
                    return got;
                }
                JsonElement Result(string reply)
                {
                    using var doc = JsonDocument.Parse(reply);
                    return doc.RootElement.GetProperty("result").Clone();
                }

                var hello = Ask("{\"cmd\":\"hello\",\"app\":\"test\"}");
                check("api: hello still answers protocol 1", hello != null && hello.Contains("\"protocol\":1"), hello ?? "(no reply)");
                check("api: hello carries the api family's version", hello != null && hello.Contains("\"api\":1"), hello ?? "(no reply)");

                var pong = Ask("{\"cmd\":\"api.ping\",\"id\":41,\"echo\":\"hi\"}");
                check("api: ping answers in the envelope with the id",
                      pong != null && pong.Contains("\"type\":\"api\"") && pong.Contains("\"id\":41") && pong.Contains("\"ok\":true") && pong.Contains("\"echo\":\"hi\""),
                      pong ?? "(no reply)");
                var plain = Ask("api.ping hello");
                check("api: the plain spelling gets the same JSON reply, without an id",
                      plain != null && plain.Contains("\"ok\":true") && plain.Contains("\"echo\":\"hello\"") && !plain.Contains("\"id\""),
                      plain ?? "(no reply)");

                var described = Ask("{\"cmd\":\"api.describe\",\"id\":1}");
                check("api: describe answers", described != null && described.Contains("\"ok\":true"), described == null ? "(no reply)" : described.Substring(0, Math.Min(120, described.Length)));
                if (described != null)
                {
                    var result = Result(described);
                    var names = new List<string>();
                    bool schemas = true;
                    foreach (var v in result.GetProperty("verbs").EnumerateArray())
                    {
                        names.Add(v.GetProperty("name").GetString());
                        if (v.GetProperty("params").ValueKind != JsonValueKind.Object) schemas = false;
                    }
                    check("api: describe lists the system verbs",
                          names.Contains("api.ping") && names.Contains("api.status") && names.Contains("api.workspace") && names.Contains("api.jobs"),
                          string.Join(",", names));
                    check("api: every verb is in the family and named once",
                          names.All(n => n.StartsWith("api.")) && names.Distinct().Count() == names.Count, $"{names.Count} verbs");
                    check("api: every verb carries a params schema an MCP tool can declare", schemas, schemas ? "all objects" : "a non-object schema");
                    check("api: describe names the app and workspace",
                          result.GetProperty("app").GetString() == "RAGE Tools" && result.GetProperty("workspace").GetString() == SpaceNames.NameOf(panel.Workspace),
                          result.GetProperty("workspace").GetString());
                }

                var status = Ask("{\"cmd\":\"api.status\",\"id\":2}");
                check("api: status reports workspace, camera and files",
                      status != null && status.Contains("\"workspace\"") && status.Contains("\"camera\"") && status.Contains("\"files\"") && status.Contains("\"gta\""),
                      status == null ? "(no reply)" : status.Substring(0, Math.Min(160, status.Length)));

                var sw = Ask("{\"cmd\":\"api.workspace\",\"space\":\"materials\",\"id\":3}");
                check("api: workspace switches by its written name",
                      panel.Workspace == LightPanel.Space.Material && sw != null && sw.Contains("\"workspace\":\"Material\""), sw ?? "(no reply)");
                Ask("{\"cmd\":\"api.workspace\",\"space\":\"" + SpaceNames.NameOf(was) + "\",\"id\":4}");
                check("api: and back", panel.Workspace == was, SpaceNames.NameOf(panel.Workspace));
                var bad = Ask("{\"cmd\":\"api.workspace\",\"space\":\"attic\",\"id\":5}");
                check("api: a workspace that does not exist is refused with the reason",
                      bad != null && bad.Contains("\"ok\":false") && bad.Contains("attic"), bad ?? "(no reply)");

                var unknown = Ask("{\"cmd\":\"api.wibble\",\"id\":6}");
                check("api: an unknown verb fails cleanly and points at describe",
                      unknown != null && unknown.Contains("\"ok\":false") && unknown.Contains("api.describe"), unknown ?? "(no reply)");

                api.Add("api.test_count", "test only", ApiVerbs.Schema(), "", false,
                    m => ApiVerbs.StartedJson(api.StartJob(m, "api.test_count", job =>
                    {
                        for (int i = 0; i <= 10; i++) { job.Percent = i * 10; Thread.Sleep(2); }
                        job.Finish("{\"n\":10}");
                    })));
                api.Add("api.test_wait", "test only", ApiVerbs.Schema(), "", false,
                    m => ApiVerbs.StartedJson(api.StartJob(m, "api.test_wait", job =>
                    {
                        for (int i = 0; i < 600 && !job.CancelRequested; i++) Thread.Sleep(5);
                        if (job.CancelRequested) job.Fail("cancelled");
                        else job.Finish("{\"waited\":true}");
                    })));
                try
                {
                    var started = Ask("{\"cmd\":\"api.test_count\",\"id\":7}");
                    int jobId = started != null && started.Contains("\"job\":") ? Result(started).GetProperty("job").GetInt32() : -1;
                    check("api: a long verb answers at once with its job id", jobId > 0, started ?? "(no reply)");

                    var events = new List<string>();
                    void Collect(List<string> into) { into.AddRange(strays); strays.Clear(); into.AddRange(Pending()); }
                    Pump(() => { Collect(events); return events.Any(l => l.Contains("\"event\":\"job-done\"")); });
                    Collect(events);
                    var doneLine = events.FirstOrDefault(l => l.Contains("\"event\":\"job-done\"") && l.Contains("\"job\":" + jobId));
                    check("api: the job's progress arrives as events", events.Any(l => l.Contains("\"event\":\"job\"") && l.Contains("\"percent\"")),
                          $"{events.Count} events");
                    check("api: job-done carries the result", doneLine != null && doneLine.Contains("\"ok\":true") && doneLine.Contains("\"n\":10"),
                          doneLine ?? "(no done event)");

                    var after = Ask("{\"cmd\":\"api.job_status\",\"job\":" + jobId + ",\"id\":8}");
                    check("api: a finished job still answers job_status with its result",
                          after != null && after.Contains("\"done\":true") && after.Contains("\"n\":10"), after ?? "(no reply)");

                    var w = Ask("{\"cmd\":\"api.test_wait\",\"id\":9}");
                    int waitId = w != null && w.Contains("\"job\":") ? Result(w).GetProperty("job").GetInt32() : -1;
                    var cancel = Ask("{\"cmd\":\"api.job_cancel\",\"job\":" + waitId + ",\"id\":10}");
                    check("api: job_cancel acknowledges", cancel != null && cancel.Contains("\"cancelling\":true"), cancel ?? "(no reply)");
                    var got = new List<string>();
                    Pump(() => { Collect(got); return got.Any(l => l.Contains("\"event\":\"job-done\"") && l.Contains("\"job\":" + waitId)); });
                    Collect(got);
                    var cancelled = got.FirstOrDefault(l => l.Contains("\"event\":\"job-done\"") && l.Contains("\"job\":" + waitId));
                    check("api: a cancelled job ends ok:false with the reason",
                          cancelled != null && cancelled.Contains("\"ok\":false") && cancelled.Contains("cancelled"), cancelled ?? "(no done event)");

                    var list = Ask("{\"cmd\":\"api.jobs\",\"id\":11}");
                    check("api: api.jobs lists what ran", list != null && list.Contains("\"job\":" + jobId) && list.Contains("\"job\":" + waitId),
                          list == null ? "(no reply)" : list.Substring(0, Math.Min(160, list.Length)));
                }
                finally
                {
                    api.Remove("api.test_count");
                    api.Remove("api.test_wait");
                }
                var refused = Ask("{\"cmd\":\"api.test_count\",\"id\":12}");
                check("api: a removed verb is gone", refused != null && refused.Contains("\"ok\":false"), refused ?? "(no reply)");
            }
            catch (Exception ex)
            {
                check("api: no exception", false, ex.ToString());
            }
            finally
            {
                try { tcp?.Close(); } catch { }
                try { StopMloBridge(ui); } catch { }
                if (screenshotPath == null) panel.SwitchWorkspace(was);
            }
        }
    }
}

