using System.Windows.Forms;
using ImGuiNET;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private ImGuiMouseCursor lastImGuiCursor_U29 = ImGuiMouseCursor.Arrow;

        public ImGuiMouseCursor LastImGuiCursor_U29 => lastImGuiCursor_U29;

        private void SyncImGuiCursor_U29()
        {
            var want = ImGui.GetMouseCursor();
            if (want == lastImGuiCursor_U29) return;
            lastImGuiCursor_U29 = want;
            Cursor = want switch
            {
                ImGuiMouseCursor.TextInput => Cursors.IBeam,
                ImGuiMouseCursor.ResizeAll => Cursors.SizeAll,
                ImGuiMouseCursor.ResizeNS => Cursors.SizeNS,
                ImGuiMouseCursor.ResizeEW => Cursors.SizeWE,
                ImGuiMouseCursor.ResizeNESW => Cursors.SizeNESW,
                ImGuiMouseCursor.ResizeNWSE => Cursors.SizeNWSE,
                ImGuiMouseCursor.Hand => Cursors.Hand,
                ImGuiMouseCursor.NotAllowed => Cursors.No,
                _ => Cursors.Default,
            };
        }
    }
}
