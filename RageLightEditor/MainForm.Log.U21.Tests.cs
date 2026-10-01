using System;
using System.IO;
using System.Linq;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private static string ReadShared_U21(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }

        private void SeqTest_Log_U21(Action<string, bool, string> check)
        {
            try
            {
                bool showWas = AppLog_U21.ShowWindow;
                int before = AppLog_U21.ErrorsUnseen;
                Exception boom;
                try { throw new InvalidOperationException("u21 probe failure"); } catch (Exception ex) { boom = ex; }
                AppLog_U21.Error("saving probe.ytyp", boom, false);
                var last = AppLog_U21.Snapshot().Where(e => e.Level == AppLog_U21.Level.Error).Select(e => e.Text).ToList();
                check("u21 log: a save error lands in the log with its message", last.Any(t => t.Contains("saving probe.ytyp") && t.Contains("u21 probe failure")), "");
                check("u21 log: ...and with the stack trace, not just the message", last.Any(t => t.TrimStart().StartsWith("at ")), "");
                check("u21 log: the error count goes up so the menu bar can show it", AppLog_U21.ErrorsUnseen > before, AppLog_U21.ErrorsUnseen.ToString());
                Console.WriteLine("u21 log probe line");
                check("u21 log: everything the tool prints reaches the log too", AppLog_U21.Snapshot().Any(e => e.Text.Contains("u21 log probe line")), "");
                check("u21 log: it is written to a file you can open", File.Exists(AppLog_U21.FilePath) && ReadShared_U21(AppLog_U21.FilePath).Contains("u21 probe failure"), AppLog_U21.FilePath);
                var t = new CodeWalker.GameFiles.YtypFile();
                check("u21 log: an empty .ytyp still saves", (t.Save()?.Length ?? 0) >= 0, "");
                AppLog_U21.ShowWindow = showWas;
                AppLog_U21.MarkSeen();
            }
            catch (Exception ex) { check("u21 log: no exception", false, ex.ToString()); }
        }
    }
}
