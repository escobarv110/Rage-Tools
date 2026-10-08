using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public (int[] modes, string[] labels) ShadingChoices_U28()
        {
            if (MaterialMode)
                return (new[] { 0, 7, 1, 2, 5, 6, 8, VertexColourModeFirst },
                        new[] { "RAGE", "Cinematic", "Unlit", "Normals", "Lighting only", "Specular only", "Wireframe", "Vertex colours" });
            return (new[] { 0, 7, 1, 2, 8, VertexColourModeFirst }, new[] { "RAGE", "Cinematic", "Unlit", "Normals", "Wireframe", "Vertex colours" });
        }

        public string ShadingName_U28()
        {
            var (modes, labels) = ShadingChoices_U28();
            int i = Array.IndexOf(modes, ShadingComboMode_O2(RenderMode));
            if (i <= 0) return "Default Shading";
            return labels[i];
        }

        public void DrawShadingMenuItems_U28()
        {
            var (modes, labels) = ShadingChoices_U28();
            int cur = ShadingComboMode_O2(RenderMode);
            for (int i = 0; i < modes.Length; i++)
                if (ImGui.MenuItem(i == 0 ? "Default Shading" : labels[i], null, cur == modes[i])) RenderMode = modes[i];
        }

        public Vector4 ViewRect_U28(float dw, float dh)
        {
            float top = TopBarHeight + (WorldMode ? ToolbarHNow_U5 : 0.0f);
            float left = !DockedLayout && ShowLeftPanel ? settings.LeftPanelWidth : 0.0f;
            float right = dw;
            if (ShellOwnsRight_U27) { if (ShellRightImGui_U27) right -= ShellRightPx_U27; }
            else if (!DockedLayout && ShowRightPanel) right -= settings.RightPanelWidth;
            float bottom = dh - (WorldMode ? StatusH : 0.0f);
            return new Vector4(left, top, Math.Max(right, left + 1), Math.Max(bottom, top + 1));
        }
    }
}
