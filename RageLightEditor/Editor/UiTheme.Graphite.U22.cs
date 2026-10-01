using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public static partial class UiTheme
    {
        private static Vector4 G(float r, float g, float b, float a = 1f) => new Vector4(r, g, b, a);

        private static Vector4 Mix(Vector4 a, Vector4 b, float t) =>
            new Vector4(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);

        private static void ApplyGraphite_U22(Vector3 accent)
        {
            var c = ImGui.GetStyle().Colors;
            void Set(ImGuiCol id, Vector4 v) { int i = (int)id; if (i >= 0 && i < c.Count) c[i] = v; }

            var acc = new Vector4(accent.X, accent.Y, accent.Z, 1f);
            var accHi = new Vector4(Lift(accent.X), Lift(accent.Y), Lift(accent.Z), 1f);
            Accent = acc;
            AccentBright = accHi;
            AccentDim = new Vector4(accent.X * 0.75f, accent.Y * 0.75f, accent.Z * 0.75f, 1f);

            var bg = G(0.110f, 0.114f, 0.125f, 0.96f);
            var bgAlt = G(0.145f, 0.150f, 0.165f, 1f);
            var frame = G(0.180f, 0.186f, 0.204f, 1f);
            var frameHi = G(0.225f, 0.232f, 0.255f, 1f);
            var frameAct = G(0.265f, 0.274f, 0.300f, 1f);
            var button = G(0.205f, 0.212f, 0.232f, 1f);
            var buttonHi = G(0.275f, 0.284f, 0.310f, 1f);
            var buttonAct = Mix(G(0.275f, 0.284f, 0.310f, 1f), acc, 0.45f);
            var border = G(0.235f, 0.243f, 0.265f, 0.85f);
            var accSoft = new Vector4(accent.X, accent.Y, accent.Z, 0.35f);
            var accFaint = new Vector4(accent.X, accent.Y, accent.Z, 0.22f);

            Set(ImGuiCol.Text, G(0.910f, 0.918f, 0.935f));
            Set(ImGuiCol.TextDisabled, G(0.530f, 0.550f, 0.590f));
            Set(ImGuiCol.WindowBg, bg);
            Set(ImGuiCol.ChildBg, G(0, 0, 0, 0));
            Set(ImGuiCol.PopupBg, G(0.120f, 0.125f, 0.137f, 0.98f));
            Set(ImGuiCol.Border, border);
            Set(ImGuiCol.BorderShadow, G(0, 0, 0, 0));
            Set(ImGuiCol.FrameBg, frame);
            Set(ImGuiCol.FrameBgHovered, frameHi);
            Set(ImGuiCol.FrameBgActive, frameAct);
            Set(ImGuiCol.TitleBg, G(0.090f, 0.093f, 0.102f, 1f));
            Set(ImGuiCol.TitleBgActive, G(0.140f, 0.145f, 0.160f, 1f));
            Set(ImGuiCol.TitleBgCollapsed, G(0.090f, 0.093f, 0.102f, 0.9f));
            Set(ImGuiCol.MenuBarBg, G(0.130f, 0.135f, 0.148f, 1f));
            Set(ImGuiCol.ScrollbarBg, G(0, 0, 0, 0.10f));
            Set(ImGuiCol.ScrollbarGrab, G(0.300f, 0.310f, 0.335f, 1f));
            Set(ImGuiCol.ScrollbarGrabHovered, G(0.380f, 0.392f, 0.420f, 1f));
            Set(ImGuiCol.ScrollbarGrabActive, acc);
            Set(ImGuiCol.CheckMark, acc);
            Set(ImGuiCol.SliderGrab, Mix(acc, G(0.6f, 0.6f, 0.6f, 1f), 0.25f));
            Set(ImGuiCol.SliderGrabActive, accHi);
            Set(ImGuiCol.Button, button);
            Set(ImGuiCol.ButtonHovered, buttonHi);
            Set(ImGuiCol.ButtonActive, buttonAct);
            Set(ImGuiCol.Header, G(0.185f, 0.191f, 0.210f, 1f));
            Set(ImGuiCol.HeaderHovered, G(0.235f, 0.243f, 0.266f, 1f));
            Set(ImGuiCol.HeaderActive, Mix(G(0.235f, 0.243f, 0.266f, 1f), acc, 0.35f));
            Set(ImGuiCol.Separator, border);
            Set(ImGuiCol.SeparatorHovered, accSoft);
            Set(ImGuiCol.SeparatorActive, acc);
            Set(ImGuiCol.ResizeGrip, G(1, 1, 1, 0.06f));
            Set(ImGuiCol.ResizeGripHovered, accSoft);
            Set(ImGuiCol.ResizeGripActive, acc);
            Set(ImGuiCol.Tab, G(0.130f, 0.135f, 0.148f, 1f));
            Set(ImGuiCol.TabHovered, buttonHi);
            Set(ImGuiCol.TabSelected, G(0.215f, 0.222f, 0.244f, 1f));
            Set(ImGuiCol.TabSelectedOverline, acc);
            Set(ImGuiCol.TabDimmed, G(0.130f, 0.135f, 0.148f, 1f));
            Set(ImGuiCol.TabDimmedSelected, G(0.180f, 0.186f, 0.204f, 1f));
            Set(ImGuiCol.TabDimmedSelectedOverline, accSoft);
            Set(ImGuiCol.PlotLines, acc);
            Set(ImGuiCol.PlotLinesHovered, accHi);
            Set(ImGuiCol.PlotHistogram, acc);
            Set(ImGuiCol.PlotHistogramHovered, accHi);
            Set(ImGuiCol.TableHeaderBg, bgAlt);
            Set(ImGuiCol.TableBorderStrong, border);
            Set(ImGuiCol.TableBorderLight, G(0.200f, 0.207f, 0.226f, 1f));
            Set(ImGuiCol.TableRowBg, G(0, 0, 0, 0));
            Set(ImGuiCol.TableRowBgAlt, G(1, 1, 1, 0.025f));
            Set(ImGuiCol.TextLink, accHi);
            Set(ImGuiCol.TextSelectedBg, accFaint);
            Set(ImGuiCol.DragDropTarget, accHi);
            Set(ImGuiCol.NavCursor, acc);
            Set(ImGuiCol.NavWindowingHighlight, acc);
            Set(ImGuiCol.NavWindowingDimBg, G(0, 0, 0, 0.4f));
            Set(ImGuiCol.DockingPreview, accSoft);
            Set(ImGuiCol.DockingEmptyBg, bg);
            Set(ImGuiCol.ModalWindowDimBg, G(0, 0, 0, 0.5f));
        }
    }
}
