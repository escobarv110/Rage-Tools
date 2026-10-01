using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public static partial class UiTheme
    {
        public static readonly string[] Names = { "Accent tinted", "Dark gray", "Light", "Classic ImGui (blue)", "Graphite" };
        public const int DefaultTheme = 4;

        public static float ClassicPanelAlpha = 0.86f;

        private static void NeutralSections(ImGuiNET.RangeAccessor<Vector4> c)
        {
            void Put(ImGuiCol id, Vector4 v) { int i = (int)id; if (i >= 0 && i < c.Count) c[i] = v; }
            Put(ImGuiCol.Header, new Vector4(1, 1, 1, 0.10f));
            Put(ImGuiCol.HeaderHovered, new Vector4(1, 1, 1, 0.16f));
            Put(ImGuiCol.HeaderActive, new Vector4(1, 1, 1, 0.22f));
        }

        public static void Apply(int theme, Vector3 accent)
        {
            var style = ImGui.GetStyle();
            var c = style.Colors;

            float k = UiScale_V17.Scale;
            style.FrameRounding = 4.0f * k;
            style.GrabRounding = 4.0f * k;
            style.WindowRounding = 4.0f * k;
            style.PopupRounding = 4.0f * k;
            style.ChildRounding = 4.0f * k;
            style.TabRounding = 4.0f * k;
            style.ScrollbarRounding = 6.0f * k;
            style.FramePadding = new Vector2(6 * k, 4 * k);
            style.ButtonTextAlign = new Vector2(0.5f, 0.5f);
            ApplySemantic(theme);
            if (theme == 4) { ApplyGraphite_U22(accent); return; }

            if (theme == 3)
            {
                ImGui.StyleColorsDark();
                float pa = ClassicPanelAlpha;
                c[(int)ImGuiCol.WindowBg] = new Vector4(0.06f, 0.06f, 0.06f, pa);
                c[(int)ImGuiCol.ChildBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
                c[(int)ImGuiCol.PopupBg] = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
                c[(int)ImGuiCol.MenuBarBg] = new Vector4(0.14f, 0.14f, 0.14f, pa);
                c[(int)ImGuiCol.TitleBg] = new Vector4(0.04f, 0.04f, 0.04f, pa);
                c[(int)ImGuiCol.TitleBgActive] = new Vector4(0.16f, 0.29f, 0.48f, pa);
                NeutralSections(c);
                var blue = new Vector4(0.26f, 0.59f, 0.98f, 1f);
                Accent = blue;
                AccentBright = new Vector4(0.52f, 0.73f, 0.99f, 1f);
                AccentDim = new Vector4(0.20f, 0.44f, 0.74f, 1f);
                return;
            }

            Vector4 bg, bgAlt, frame, text, textDim, border;
            switch (theme)
            {
                case 1:
                    bg = new Vector4(0.11f, 0.11f, 0.12f, 1f);
                    bgAlt = new Vector4(0.15f, 0.15f, 0.16f, 1f);
                    frame = new Vector4(0.20f, 0.20f, 0.21f, 1f);
                    text = new Vector4(0.92f, 0.92f, 0.92f, 1f);
                    textDim = new Vector4(0.55f, 0.55f, 0.55f, 1f);
                    border = new Vector4(0.25f, 0.25f, 0.26f, 1f);
                    break;
                case 2:
                    bg = new Vector4(0.93f, 0.93f, 0.94f, 1f);
                    bgAlt = new Vector4(0.87f, 0.87f, 0.89f, 1f);
                    frame = new Vector4(0.80f, 0.80f, 0.83f, 1f);
                    text = new Vector4(0.10f, 0.10f, 0.12f, 1f);
                    textDim = new Vector4(0.45f, 0.45f, 0.48f, 1f);
                    border = new Vector4(0.70f, 0.70f, 0.73f, 1f);
                    break;
                default:
                    bg = Tint(0.085f, accent, 0.10f);
                    bgAlt = Tint(0.120f, accent, 0.14f);
                    frame = Tint(0.165f, accent, 0.18f);
                    text = Tint(0.920f, accent, 0.06f);
                    textDim = Tint(0.540f, accent, 0.20f);
                    border = Tint(0.250f, accent, 0.26f);
                    break;
            }

            var acc = new Vector4(accent.X, accent.Y, accent.Z, 1f);
            var accHover = new Vector4(Lift(accent.X), Lift(accent.Y), Lift(accent.Z), 1f);
            var accActive = new Vector4(accent.X * 0.8f, accent.Y * 0.8f, accent.Z * 0.8f, 1f);
            var accDim = new Vector4(accent.X, accent.Y, accent.Z, theme == 2 ? 0.55f : 0.70f);

            Accent = acc;
            AccentBright = accHover;
            AccentDim = new Vector4(accent.X * 0.75f, accent.Y * 0.75f, accent.Z * 0.75f, 1f);

            Vector4 Sink(Vector4 v, float t) =>
                new Vector4(v.X + (bg.X - v.X) * t, v.Y + (bg.Y - v.Y) * t, v.Z + (bg.Z - v.Z) * t, 1f);
            float lum = accent.X * 0.299f + accent.Y * 0.587f + accent.Z * 0.114f;
            float sink = theme == 2 ? 0.0f : Math.Max(0.0f, (lum - 0.30f)) * 0.9f;
            var accBtn = Sink(acc, sink);
            var accBtnHover = Sink(acc, sink * 0.45f);

            void Set(ImGuiCol id, Vector4 v)
            {
                int i = (int)id;
                if (i >= 0 && i < c.Count) c[i] = v;
            }

            Set(ImGuiCol.Text, text);
            Set(ImGuiCol.TextDisabled, textDim);
            Set(ImGuiCol.WindowBg, bg);
            Set(ImGuiCol.ChildBg, bg);
            Set(ImGuiCol.PopupBg, new Vector4(bg.X, bg.Y, bg.Z, 0.98f));
            Set(ImGuiCol.Border, border);
            Set(ImGuiCol.BorderShadow, new Vector4(0, 0, 0, 0));
            Set(ImGuiCol.FrameBg, frame);
            Set(ImGuiCol.FrameBgHovered, bgAlt);
            Set(ImGuiCol.FrameBgActive, accDim);
            Set(ImGuiCol.TitleBg, bg);
            Set(ImGuiCol.TitleBgActive, bgAlt);
            Set(ImGuiCol.TitleBgCollapsed, bg);
            Set(ImGuiCol.MenuBarBg, bgAlt);
            Set(ImGuiCol.ScrollbarBg, bg);
            Set(ImGuiCol.ScrollbarGrab, frame);
            Set(ImGuiCol.ScrollbarGrabHovered, accDim);
            Set(ImGuiCol.ScrollbarGrabActive, acc);
            Set(ImGuiCol.CheckMark, acc);
            Set(ImGuiCol.SliderGrab, acc);
            Set(ImGuiCol.SliderGrabActive, accHover);
            Set(ImGuiCol.Button, accBtn);
            Set(ImGuiCol.ButtonHovered, accBtnHover);
            Set(ImGuiCol.ButtonActive, acc);
            NeutralSections(c);
            Set(ImGuiCol.Separator, border);
            Set(ImGuiCol.SeparatorHovered, accDim);
            Set(ImGuiCol.SeparatorActive, acc);
            Set(ImGuiCol.ResizeGrip, accDim);
            Set(ImGuiCol.ResizeGripHovered, accHover);
            Set(ImGuiCol.ResizeGripActive, acc);

            Set(ImGuiCol.Tab, bgAlt);
            Set(ImGuiCol.TabHovered, accBtnHover);
            Set(ImGuiCol.TabSelected, accBtn);
            Set(ImGuiCol.TabSelectedOverline, acc);
            Set(ImGuiCol.TabDimmed, bgAlt);
            Set(ImGuiCol.TabDimmedSelected, Sink(acc, 0.55f));
            Set(ImGuiCol.TabDimmedSelectedOverline, accDim);

            Set(ImGuiCol.TextSelectedBg, accDim);
            Set(ImGuiCol.TextLink, accHover);
            Set(ImGuiCol.DragDropTarget, accHover);
            Set(ImGuiCol.NavCursor, acc);
            Set(ImGuiCol.NavWindowingHighlight, acc);
            Set(ImGuiCol.NavWindowingDimBg, new Vector4(0, 0, 0, 0.4f));
            Set(ImGuiCol.PlotLines, acc);
            Set(ImGuiCol.PlotLinesHovered, accHover);
            Set(ImGuiCol.PlotHistogram, acc);
            Set(ImGuiCol.PlotHistogramHovered, accHover);
            Set(ImGuiCol.TableHeaderBg, bgAlt);
            Set(ImGuiCol.TableBorderStrong, border);
            Set(ImGuiCol.TableBorderLight, border);
            Set(ImGuiCol.TableRowBg, new Vector4(0, 0, 0, 0));
            Set(ImGuiCol.TableRowBgAlt, new Vector4(1, 1, 1, 0.03f));
            Set(ImGuiCol.DockingPreview, accDim);
            Set(ImGuiCol.DockingEmptyBg, bg);
            Set(ImGuiCol.ModalWindowDimBg, new Vector4(0, 0, 0, 0.5f));
        }

        private static Vector4 Tint(float level, Vector3 accent, float amount)
        {
            float peak = Math.Max(accent.X, Math.Max(accent.Y, accent.Z));
            if (peak < 0.001f) return new Vector4(level, level, level, 1f);
            var hue = accent / peak;
            return new Vector4(
                level * (1f - amount + amount * hue.X * 1.35f),
                level * (1f - amount + amount * hue.Y * 1.35f),
                level * (1f - amount + amount * hue.Z * 1.35f),
                1f);
        }

        private static float Lift(float v) => v + (1f - v) * 0.35f;

        public static Vector4 Accent { get; private set; } = new Vector4(0.26f, 0.59f, 0.98f, 1f);
        public static Vector4 AccentBright { get; private set; } = new Vector4(0.52f, 0.73f, 0.99f, 1f);
        public static Vector4 AccentDim { get; private set; } = new Vector4(0.20f, 0.44f, 0.74f, 1f);

        public static Vector4 AccentActive(Vector3 accent) =>
            new Vector4(Lift(accent.X), Lift(accent.Y), Lift(accent.Z), 1f);

        public static Vector4 Ok { get; private set; } = new Vector4(0.45f, 0.85f, 0.55f, 1f);
        public static Vector4 Warn { get; private set; } = new Vector4(0.98f, 0.70f, 0.38f, 1f);
        public static Vector4 Danger { get; private set; } = new Vector4(0.90f, 0.35f, 0.32f, 1f);
        public static Vector4 DangerButton { get; private set; } = new Vector4(0.80f, 0.28f, 0.24f, 1f);
        public static Vector4 DangerButtonHi { get; private set; } = new Vector4(0.92f, 0.36f, 0.30f, 1f);
        public static Vector4 Muted => ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        public static Vector4 ButtonOn => ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];
        public static Vector4 ButtonOff => ImGui.GetStyle().Colors[(int)ImGuiCol.Button];
        public static Vector4 ButtonHover => ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered];

        public static Vector4 TimeFrame { get; private set; } = new Vector4(0.34f, 0.24f, 0.10f, 0.95f);
        public static Vector4 TimeFrameHover { get; private set; } = new Vector4(0.44f, 0.31f, 0.12f, 1f);
        public static Vector4 TimeFrameActive { get; private set; } = new Vector4(0.52f, 0.37f, 0.14f, 1f);
        public static Vector4 TimeGrab { get; private set; } = new Vector4(1.00f, 0.72f, 0.20f, 1f);
        public static Vector4 TimeGrabActive { get; private set; } = new Vector4(1.00f, 0.86f, 0.50f, 1f);

        private static void ApplySemantic(int theme)
        {
            bool light = theme == 2;
            float k = light ? 0.72f : 1f;
            Ok = new Vector4(0.45f * k, 0.85f * k, 0.55f * k, 1f);
            Warn = new Vector4(0.98f * k, 0.70f * k, 0.38f * k, 1f);
            Danger = new Vector4(0.90f * k, 0.35f * k, 0.32f * k, 1f);
            DangerButton = new Vector4(0.80f, 0.28f, 0.24f, 1f);
            DangerButtonHi = new Vector4(0.92f, 0.36f, 0.30f, 1f);
            if (light)
            {
                TimeFrame = new Vector4(0.98f, 0.88f, 0.66f, 1f);
                TimeFrameHover = new Vector4(0.99f, 0.84f, 0.55f, 1f);
                TimeFrameActive = new Vector4(0.99f, 0.80f, 0.45f, 1f);
                TimeGrab = new Vector4(0.90f, 0.55f, 0.08f, 1f);
                TimeGrabActive = new Vector4(0.80f, 0.45f, 0.05f, 1f);
            }
            else
            {
                TimeFrame = new Vector4(0.34f, 0.24f, 0.10f, 0.95f);
                TimeFrameHover = new Vector4(0.44f, 0.31f, 0.12f, 1f);
                TimeFrameActive = new Vector4(0.52f, 0.37f, 0.14f, 1f);
                TimeGrab = new Vector4(1.00f, 0.72f, 0.20f, 1f);
                TimeGrabActive = new Vector4(1.00f, 0.86f, 0.50f, 1f);
            }
        }

        public static void PushTimeSlider()
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, TimeFrame);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, TimeFrameHover);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, TimeFrameActive);
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, TimeGrab);
            ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, TimeGrabActive);
        }
        public static void PopTimeSlider() => ImGui.PopStyleColor(5);
    }
}

