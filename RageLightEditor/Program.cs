using System;

namespace RageLightEditor
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            AppIcon.SetAppUserModelId();
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                ReportCrash(e.ExceptionObject as Exception, "unhandled");
            System.Windows.Forms.Application.ThreadException += (s, e) =>
                ReportCrash(e.Exception, "ui thread");

            Editor.AppLog_U21.Install();
            FlightRecorder.Start();
            if (FlightRecorder.PreviousCrashReport != null)
            {
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [previous session did not exit cleanly]{Environment.NewLine}" +
                        FlightRecorder.PreviousCrashReport + Environment.NewLine);
                }
                catch { }
            }

            DataFiles.EnsureExtracted();
            try { return AppLauncher.Run(args, AppMode.Light); }
            finally { FlightRecorder.CleanExit(); }
        }

        internal static void ReportCrash(Exception ex, string where)
        {
            Editor.AppLog_U21.Error("crash (" + where + ")", ex);
            string path = null;
            try
            {
                path = System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log");
                System.IO.File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{where}] {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { path = null; }

            try
            {
                System.Windows.Forms.MessageBox.Show(
                    (ex?.Message ?? "unknown error") +
                    (path != null ? $"\n\nWritten to:\n{path}" : "") +
                    "\n\nSend that file along and the cause can be found from it.",
                    $"{AppInfo.Name} - something went wrong",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}

