using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private bool projectTabActive_U22;

        private void DrawProjectTab_U22()
        {
            var pw = ProjectWindow;
            if (pw == null) return;
            pw.DockAvailable = Workspace == Space.World;
            if (!pw.ShowsDocked) { projectTabActive_U22 = false; return; }
            if (pw.Visible && !projectTabActive_U22) SelectRightTab_U22("Project");
            else if (!pw.Visible && projectTabActive_U22) SelectRightTab_U22("Inspector");
            bool active = BeginRightTab_J2("Project");
            if (active)
            {
                pw.Minimized = false;
                pw.DrawEmbedded();
                ImGui.EndTabItem();
            }
            projectTabActive_U22 = active;
            pw.Visible = active;
        }
    }
}
