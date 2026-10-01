using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SharpDX.D3DCompiler;

namespace RageLightEditor.Rendering
{
    public static class ShaderCache_U22
    {
        public static int Hits, Misses;
        public static double CompileMs;

        public static string Shipped => Path.Combine(AppContext.BaseDirectory, "shadercache");

        public static string Folder
        {
            get
            {
                var forced = Environment.GetEnvironmentVariable("RLE_SHADERCACHE_DIR");
                if (!string.IsNullOrWhiteSpace(forced)) return forced.Trim();
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(string.IsNullOrEmpty(local) ? Path.GetTempPath() : local, "RAGE Tools", "shadercache");
            }
        }

        public static bool Disabled => Environment.GetEnvironmentVariable("RLE_NOSHADERCACHE") == "1";

        public static string Key(string source, string entry, string profile, ShaderFlags flags)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(source + "\n#entry " + entry + "\n#profile " + profile + "\n#flags " + (int)flags + "\n#v2");
            var h = sha.ComputeHash(bytes);
            var sb = new StringBuilder(40);
            for (int i = 0; i < 20; i++) sb.Append(h[i].ToString("x2"));
            return sb.ToString();
        }

        public static ShaderBytecode Compile(string source, string entry, string profile, ShaderFlags flags, string debugName)
        {
            string path = null;
            if (!Disabled)
            {
                try
                {
                    var key = Key(source, entry, profile, flags) + ".cso";
                    var shipped = Path.Combine(Shipped, key);
                    if (File.Exists(shipped))
                    {
                        var sdata = File.ReadAllBytes(shipped);
                        if (sdata.Length > 0) { Hits++; return new ShaderBytecode(sdata); }
                    }
                    path = Path.Combine(Folder, key);
                    if (File.Exists(path))
                    {
                        var data = File.ReadAllBytes(path);
                        if (data.Length > 0) { Hits++; return new ShaderBytecode(data); }
                    }
                }
                catch { path = null; }
            }
            var sw = Stopwatch.StartNew();
            var result = ShaderBytecode.Compile(source, entry, profile, flags, sourceFileName: debugName);
            sw.Stop();
            Misses++;
            CompileMs += sw.Elapsed.TotalMilliseconds;
            if (result.Bytecode == null) throw new Exception($"{profile} compile failed ({debugName}): {result.Message}");
            var bc = result.Bytecode;
            if (path != null)
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    var tmp = path + "." + Environment.ProcessId + ".tmp";
                    File.WriteAllBytes(tmp, bc.Data);
                    if (File.Exists(path)) File.Delete(tmp); else File.Move(tmp, path);
                }
                catch { }
            }
            return bc;
        }
    }
}
