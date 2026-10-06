using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace RageLightEditor.Editor
{
    public static class UpdateCheck_U30
    {
        public const string Repo = "escobarv110/Rage-Tools";
        public static readonly string ReleasesUrl = $"https://github.com/{Repo}/releases/latest";

        public static string Latest { get; private set; }
        public static string LatestUrl { get; private set; }
        public static string Error { get; private set; }
        public static volatile bool Checking;
        public static volatile bool Checked;

        public static bool UpdateAvailable => !string.IsNullOrEmpty(Latest) && Compare(Latest, AppVersion_U30.Version) > 0;

        public static string Summary =>
            Checking ? "Checking for updates..." :
            UpdateAvailable ? $"Version {Latest} is out - you have {AppVersion_U30.Version}" :
            !string.IsNullOrEmpty(Error) ? "Could not check for updates: " + Error :
            Checked ? $"You have the latest version ({AppVersion_U30.Version})" : "";

        public static void Start()
        {
            if (Checking) return;
            Checking = true;
            Error = null;
            Task.Run(async () =>
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("RAGE-Tools/" + AppVersion_U30.Version);
                    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                    var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest").ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                    LatestUrl = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : ReleasesUrl;
                    Latest = (tag ?? "").Trim().TrimStart('v', 'V');
                    Console.WriteLine($"UPDATECHECK latest {Latest}, running {AppVersion_U30.Version}{(UpdateAvailable ? " - an update is available" : "")}");
                }
                catch (Exception ex) { Error = ex.Message; }
                finally { Checked = true; Checking = false; }
            });
        }

        public static void OpenDownloadPage()
        {
            try { Process.Start(new ProcessStartInfo(string.IsNullOrEmpty(LatestUrl) ? ReleasesUrl : LatestUrl) { UseShellExecute = true }); }
            catch (Exception ex) { AppLog_U21.Warn("Could not open the download page: " + ex.Message); }
        }

        public static int Compare(string a, string b)
        {
            static int[] Parts(string s)
            {
                var head = (s ?? "").Trim().TrimStart('v', 'V').Split('+', '-')[0];
                var bits = head.Split('.');
                var n = new int[4];
                for (int i = 0; i < n.Length && i < bits.Length; i++) int.TryParse(bits[i], out n[i]);
                return n;
            }
            var x = Parts(a); var y = Parts(b);
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return x[i].CompareTo(y[i]);
            return 0;
        }
    }

    public static class AppVersion_U30
    {
        public static readonly string Version = Read();

        public static string Title => AppInfo.Name + " " + Version;

        private static string Read()
        {
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrEmpty(info)) return info.Split('+')[0];
                var v = asm.GetName().Version;
                return v == null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}";
            }
            catch { return "?"; }
        }
    }
}
