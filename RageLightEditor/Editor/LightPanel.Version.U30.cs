using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private void DrawVersionItems_U30()
        {
            ImGui.TextDisabled(AppVersion_U30.Title);
            if (UpdateCheck_U30.UpdateAvailable)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Ok);
                if (ImGui.MenuItem($"Download version {UpdateCheck_U30.Latest}")) UpdateCheck_U30.OpenDownloadPage();
                ImGui.PopStyleColor();
            }
            if (ImGui.MenuItem(UpdateCheck_U30.Checking ? "Checking for updates..." : "Check for updates", null, false, !UpdateCheck_U30.Checking))
                UpdateCheck_U30.Start();
            var s = UpdateCheck_U30.Summary;
            if (!string.IsNullOrEmpty(s) && !UpdateCheck_U30.Checking) ImGui.TextDisabled(s);
            ImGui.Separator();
        }

        private void DrawUpdateBadge_U30()
        {
            if (!UpdateCheck_U30.UpdateAvailable) return;
            ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Ok);
            if (ImGui.MenuItem($"Update {UpdateCheck_U30.Latest}")) UpdateCheck_U30.OpenDownloadPage();
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"You have {AppVersion_U30.Version}. Click to open the download page for {UpdateCheck_U30.Latest}.");
        }
    }
}
