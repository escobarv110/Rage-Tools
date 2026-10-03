using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RageLightEditor.Editor
{
    public static class AppLog_U21
    {
        public enum Level { Info, Warning, Error }

        public readonly struct Entry
        {
            public readonly DateTime Time;
            public readonly Level Level;
            public readonly string Text;
            public Entry(DateTime time, Level level, string text) { Time = time; Level = level; Text = text ?? ""; }
        }

        public const int MaxEntries = 4000;
        private static readonly object gate = new object();
        private static readonly List<Entry> entries = new List<Entry>();
        private static StreamWriter file;
        private static bool installed;

        public static int ErrorsUnseen { get; private set; }
        public static int Version { get; private set; }
        public static bool ShowWindow;
        public static string FilePath { get; private set; } = "";

        public static string Folder
        {
            get
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(string.IsNullOrEmpty(local) ? Path.GetTempPath() : local, "RAGE Tools", "logs");
            }
        }

        public static void Install()
        {
            if (installed) return;
            installed = true;
            try
            {
                Directory.CreateDirectory(Folder);
                FilePath = Path.Combine(Folder, "rage_tools.log");
                var previous = Path.Combine(Folder, "rage_tools.previous.log");
                FileStream stream = null;
                try
                {
                    if (File.Exists(FilePath))
                    {
                        if (File.Exists(previous)) File.Delete(previous);
                        File.Move(FilePath, previous);
                    }
                    stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                    foreach (var stale in Directory.GetFiles(Folder, "rage_tools.*.log"))
                    {
                        var mid = Path.GetFileNameWithoutExtension(stale).Substring("rage_tools.".Length);
                        if (int.TryParse(mid, out _)) try { File.Delete(stale); } catch { }
                    }
                }
                catch (IOException)
                {
                    FilePath = Path.Combine(Folder, $"rage_tools.{Environment.ProcessId}.log");
                    stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                }
                catch (UnauthorizedAccessException)
                {
                    FilePath = Path.Combine(Folder, $"rage_tools.{Environment.ProcessId}.log");
                    stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                }
                file = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                file.WriteLine($"RAGE Tools log started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch { file = null; }
            var outWas = Console.Out;
            var errWas = Console.Error;
            Console.SetOut(new Tee(outWas, Level.Info));
            Console.SetError(new Tee(errWas, Level.Error));
        }

        public static void Info(string text) => Add(Level.Info, text);
        public static void Warn(string text) => Add(Level.Warning, text);

        public static void Error(string text, bool open = true)
        {
            Add(Level.Error, text);
            if (open) ShowWindow = true;
        }

        public static void Error(string context, Exception ex, bool open = true) =>
            Error((string.IsNullOrEmpty(context) ? "" : context + ": ") + (ex?.ToString() ?? "(no exception)"), open);

        public static void Add(Level level, string text)
        {
            if (text == null) return;
            var now = DateTime.Now;
            lock (gate)
            {
                foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
                {
                    if (line.Length == 0) continue;
                    entries.Add(new Entry(now, level, line));
                }
                if (entries.Count > MaxEntries) entries.RemoveRange(0, entries.Count - MaxEntries);
                if (level == Level.Error) ErrorsUnseen++;
                Version++;
                try { file?.WriteLine($"{now:HH:mm:ss.fff} {(level == Level.Error ? "ERROR " : level == Level.Warning ? "WARN  " : "")}{text}"); } catch { }
            }
        }

        public static List<Entry> Snapshot()
        {
            lock (gate) return new List<Entry>(entries);
        }

        public static void Clear()
        {
            lock (gate) { entries.Clear(); ErrorsUnseen = 0; Version++; }
        }

        public static void MarkSeen() { ErrorsUnseen = 0; }

        public static string Text(bool errorsOnly)
        {
            var sb = new StringBuilder();
            foreach (var e in Snapshot())
            {
                if (errorsOnly && e.Level != Level.Error) continue;
                sb.Append(e.Time.ToString("HH:mm:ss")).Append(' ').Append(e.Text).Append('\n');
            }
            return sb.ToString();
        }

        private sealed class Tee : TextWriter
        {
            private readonly TextWriter inner;
            private readonly Level level;
            private readonly StringBuilder pending = new StringBuilder();
            public Tee(TextWriter inner, Level level) { this.inner = inner; this.level = level; }
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                try { inner?.Write(value); } catch { }
                lock (pending)
                {
                    if (value == '\n') { Flush(); return; }
                    if (value != '\r') pending.Append(value);
                }
            }

            public override void Write(string value)
            {
                if (value == null) return;
                try { inner?.Write(value); } catch { }
                lock (pending)
                {
                    foreach (var c in value)
                    {
                        if (c == '\n') Flush();
                        else if (c != '\r') pending.Append(c);
                    }
                }
            }

            public override void WriteLine(string value)
            {
                try { inner?.WriteLine(value); } catch { }
                lock (pending)
                {
                    pending.Append(value);
                    Flush();
                }
            }

            public override void Flush()
            {
                if (pending.Length == 0) return;
                var s = pending.ToString();
                pending.Clear();
                Add(level == Level.Error ? Level.Error : Classify(s), s);
            }

            private static Level Classify(string s)
            {
                if (s.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf(" failed", StringComparison.OrdinalIgnoreCase) >= 0) return Level.Warning;
                return Level.Info;
            }
        }
    }
}
