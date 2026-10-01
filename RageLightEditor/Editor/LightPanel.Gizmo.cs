using System;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        partial void DrawCameraExtras_Gizmo()
        {
            ViewGroup("Gizmo");

            float size = settings.GizmoSizePx;
            ImGui.SetNextItemWidth(-140);
            if (ImGui.SliderFloat("Size", ref size, 40f, 400f, "%.0f px"))
            {
                settings.GizmoSizePx = Math.Clamp(size, 40f, 400f);
                GizmoStyle.ApplySettings(settings);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Length of a move arrow / radius of a rotate ring on screen, in pixels.\n" +
                                 "The gizmo keeps this size at any distance and any lens. 120 is the default;\n" +
                                 "at 4K with no display scaling try 180-240.");
            if (ImGui.IsItemDeactivatedAfterEdit()) settings.Save();

            int style = Math.Clamp(settings.GizmoStyleIndex, 0, 2);
            ImGui.SetNextItemWidth(-140);
            if (ImGui.Combo("Style", ref style, "Modern\0Shaded\0Classic\0"))
            {
                settings.GizmoStyleIndex = style;
                settings.GizmoModern = style != 2;
                GizmoStyle.ApplySettings(settings);
                settings.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Modern: flat, crisp 2D shapes like Blender 4 / Unity 6 - thin anti-aliased axis\n" +
                                 "lines with flat arrow tips, translucent plane squares, a hollow centre circle,\n" +
                                 "thin rotate rings (back half dimmed) plus an outer view ring, square scale tips;\n" +
                                 "hover turns a part yellow-white, a drag dims the rest.\n" +
                                 "Shaded: lit 3D cones and cubes with silhouettes, drop shadows and glow halos.\n" +
                                 "Classic: the plain axes, cones and corner brackets of the first release.");
        }
    }
}

