using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private static readonly Vector4 ProjBtnOff_U24 = new Vector4(0.13f, 0.42f, 0.38f, 1f);
        private static readonly Vector4 ProjBtnOffHover_U24 = new Vector4(0.17f, 0.52f, 0.47f, 1f);
        private static readonly Vector4 ProjBtnOn_U24 = new Vector4(0.20f, 0.62f, 0.55f, 1f);
        private static readonly Vector4 ProjBtnOnHover_U24 = new Vector4(0.26f, 0.72f, 0.64f, 1f);

        public static float ProjectButtonWidth_U24(int unsaved)
        {
            float h = ImGui.GetFrameHeight();
            float pad = ImGui.GetStyle().FramePadding.X;
            float w = pad + h * 0.8f + pad * 0.6f + ImGui.CalcTextSize("Project").X + pad;
            if (unsaved > 0) w += BadgeWidth_U24(unsaved) + pad * 0.6f;
            return w;
        }

        private static string BadgeText_U24(int unsaved) => unsaved > 99 ? "99+" : unsaved.ToString();

        private static float BadgeWidth_U24(int unsaved) =>
            Math.Max(ImGui.GetFrameHeight() * 0.62f, ImGui.CalcTextSize(BadgeText_U24(unsaved)).X + 8.0f);

        public int ProjectUnsaved_U24()
        {
            int n = WorldDirtyCount;
            var p = ProjectWindow?.Project;
            if (p != null && p.AnyUnsaved) n = Math.Max(n, 1);
            return n;
        }

        private bool DrawProjectButton_U24(bool open)
        {
            float h = ImGui.GetFrameHeight();
            float pad = ImGui.GetStyle().FramePadding.X;
            int unsaved = ProjectUnsaved_U24();
            float w = ProjectButtonWidth_U24(unsaved);

            ImGui.PushStyleColor(ImGuiCol.Button, open ? ProjBtnOn_U24 : ProjBtnOff_U24);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, open ? ProjBtnOnHover_U24 : ProjBtnOffHover_U24);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ProjBtnOnHover_U24);
            bool hit = ImGui.Button("##tbproj", new Vector2(w, 0));
            ImGui.PopStyleColor(3);
            bool hovered = ImGui.IsItemHovered();

            var min = ImGui.GetItemRectMin();
            var dl = ImGui.GetWindowDrawList();
            float iw = h * 0.8f, ih = h * 0.56f;
            float fx = min.X + pad, fy = min.Y + (h - ih) * 0.5f;
            float r = Math.Max(1.0f, h * 0.07f);
            uint tab = ImGui.ColorConvertFloat4ToU32(new Vector4(0.90f, 0.70f, 0.26f, 1f));
            uint body = ImGui.ColorConvertFloat4ToU32(new Vector4(1.00f, 0.84f, 0.40f, 1f));
            dl.AddRectFilled(new Vector2(fx, fy), new Vector2(fx + iw * 0.46f, fy + ih * 0.34f), tab, r);
            dl.AddRectFilled(new Vector2(fx, fy + ih * 0.20f), new Vector2(fx + iw, fy + ih), body, r);

            var ts = ImGui.CalcTextSize("Project");
            float tx = fx + iw + pad * 0.6f;
            dl.AddText(new Vector2(tx, min.Y + (h - ts.Y) * 0.5f), ImGui.ColorConvertFloat4ToU32(new Vector4(0.97f, 0.99f, 0.98f, 1f)), "Project");

            if (unsaved > 0)
            {
                string t = BadgeText_U24(unsaved);
                var bs = ImGui.CalcTextSize(t);
                float bw = BadgeWidth_U24(unsaved), bh = h * 0.62f;
                float bx = tx + ts.X + pad * 0.6f, by = min.Y + (h - bh) * 0.5f;
                dl.AddRectFilled(new Vector2(bx, by), new Vector2(bx + bw, by + bh),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.93f, 0.36f, 0.20f, 1f)), bh * 0.5f);
                dl.AddText(new Vector2(bx + (bw - bs.X) * 0.5f, by + (bh - bs.Y) * 0.5f), ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), t);
            }

            if (hovered)
                ImGui.SetTooltip((open ? "Hide your project" : "Open your project - the map files you change") +
                                 (unsaved > 0 ? $"\n{unsaved} unsaved change{(unsaved == 1 ? "" : "s")}" : "") +
                                 "\nShortcut: Ctrl+Shift+P");
            return hit;
        }
    }
}
