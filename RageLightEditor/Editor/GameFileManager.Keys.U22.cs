using System;
using System.IO;
using CodeWalker.GameFiles;

namespace RageLightEditor.Editor
{
    public partial class GameFileManager
    {
        public static string KeyCachePath_U22
        {
            get
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(string.IsNullOrEmpty(local) ? Path.GetTempPath() : local, "RAGE Tools", "keycache.txt");
            }
        }

        public static string KeyStamp_U22(string folder, bool gen9)
        {
            var exe = Path.Combine(folder, gen9 ? "gta5_enhanced.exe" : "gta5.exe");
            var fi = new FileInfo(exe);
            return fi.FullName.ToLowerInvariant() + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
        }

        public static bool KeysFromCache_U22;

        private static void LoadKeys_U22(string folder, bool gen9)
        {
            KeysFromCache_U22 = false;
            string stamp = null;
            try
            {
                stamp = KeyStamp_U22(folder, gen9);
                if (Environment.GetEnvironmentVariable("RLE_NOKEYCACHE") != "1" && File.Exists(KeyCachePath_U22))
                {
                    foreach (var line in File.ReadAllLines(KeyCachePath_U22))
                    {
                        int bar = line.LastIndexOf('|');
                        if (bar <= 0 || line.Substring(0, bar) != stamp) continue;
                        GTA5Keys.LoadFromPath(folder, gen9, line.Substring(bar + 1));
                        KeysFromCache_U22 = true;
                        return;
                    }
                }
            }
            catch { KeysFromCache_U22 = false; }

            GTA5Keys.LoadFromPath(folder, gen9, null);
            try
            {
                if (stamp != null && GTA5Keys.PC_AES_KEY != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(KeyCachePath_U22));
                    File.WriteAllText(KeyCachePath_U22, stamp + "|" + Convert.ToBase64String(GTA5Keys.PC_AES_KEY) + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
