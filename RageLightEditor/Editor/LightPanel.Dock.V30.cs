using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private static readonly bool forceDock_V30 =
            Environment.GetEnvironmentVariable("RLE_DOCK") == "1";

        public bool DockedLayout => !ShellChrome_U27 && (forceDock_V30 || (settings != null && settings.DockedLayout));

        public uint DockSpaceId_V30 { get; private set; }

        public bool DockModeChanged_V30 { get; set; }

        public void ApplyDockConfig_V30()
        {
            var io = ImGui.GetIO();
            bool want = DockedLayout;
            bool have = (io.ConfigFlags & ImGuiConfigFlags.DockingEnable) != 0;
            if (want == have) return;
            if (want) io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            else io.ConfigFlags &= ~ImGuiConfigFlags.DockingEnable;
        }

        public void DrawDockSpace_V30(float displayWidth, float displayHeight)
        {
            if (!DockedLayout) { DockSpaceId_V30 = 0; return; }

            float top = TopBarHeight;
            var pos = new Vector2(0, top);
            var size = new Vector2(displayWidth, Math.Max(1.0f, displayHeight - top));

            ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
            ImGui.SetNextWindowViewport(ImGui.GetMainViewport().ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize
                      | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus
                      | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoBackground
                      | ImGuiWindowFlags.NoDocking;
            bool open = ImGui.Begin("##v30dockhost", flags);
            ImGui.PopStyleVar(3);
            if (open)
            {
                DockSpaceId_V30 = ImGui.GetID("##v30dockspace");
                ImGui.DockSpace(DockSpaceId_V30, Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode);
            }
            ImGui.End();
        }

        public ImGuiWindowFlags PanelWindow_V30(float x, float y, float w, float h,
                                                float minW, float maxW, bool forceSize, ref bool open)
        {
            if (!DockedLayout)
            {
                ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(w, h), forceSize ? ImGuiCond.Always : ImGuiCond.Once);
                ImGui.SetNextWindowSizeConstraints(new Vector2(minW, h), new Vector2(maxW, h));
                return ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar;
            }

            ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(minW, 80.0f), new Vector2(maxW, float.MaxValue));
            return ImGuiWindowFlags.NoCollapse;
        }

        public void DrawLayoutMenu_V30()
        {
            ImGui.Separator();
            ImGui.TextDisabled("Layout");

            int mode = settings.DockedLayout ? 1 : 0;
            ImGui.SetNextItemWidth(200.0f);
            if (ImGui.Combo("Panels##v30", ref mode,
                            "Classic - fixed side columns\0Dockable - drag, dock and tab them\0", 2))
            {
                settings.DockedLayout = mode == 1;
                DockModeChanged_V30 = true;
                settings.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Classic is the layout this tool has always had: a column each side, fixed.\n" +
                                 "Dockable gives every panel a title bar you can drag - dock it to any edge,\n" +
                                 "tab it together with another, or close it. Your arrangement is remembered.\n\n" +
                                 "Switching takes effect straight away; nothing is lost either way.");

            if (settings.DockedLayout)
            {
                ImGui.TextDisabled("Drag a panel by its title bar. Panels you close come\nback from View > Panels.");
                if (ImGui.MenuItem("Put the panels back where they started##v30"))
                    RequestResetDockLayout_V30 = true;
            }
        }

        public bool RequestResetDockLayout_V30;

        public string WorkspaceName_V30 => Workspace switch
        {
            Space.World => "World",
            Space.Light => "Lights",
            Space.Material => "Materials",
            Space.Mlo => "MLO",
            Space.Archive => "RPF",
            Space.Particles => "Particles",
            Space.NavMesh => "NavMesh",
            Space.Terrain => "Terrain",
            Space.Animation => "Animations",
            Space.Cinematic => "Cinematic",
            _ => AppInfo.Name,
        };

        public string LeftPanelTitle_V30 => DockedLayout ? WorkspaceName_V30 + " - browser" : "Lights";
        public string RightPanelTitle_V30 => DockedLayout ? WorkspaceName_V30 + " - properties" : AppInfo.Name;
    }
}

