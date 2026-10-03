using System.IO;

namespace RageLightEditor.Editor
{
    public partial class AppSettings
    {
        public bool NewUiU27 { get; set; }
        public float ShellRightWidthU29 { get; set; }

        public static bool PeekNewUi_U27()
        {
            try
            {
                if (!File.Exists(FilePath)) return false;
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(FilePath));
                return doc.RootElement.TryGetProperty("NewUiU27", out var v) &&
                       v.ValueKind == System.Text.Json.JsonValueKind.True;
            }
            catch { return false; }
        }
    }
}
