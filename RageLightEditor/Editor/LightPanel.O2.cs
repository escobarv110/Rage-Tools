using System;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public const int VertexColourModeFirst = 9, VertexColourModeLast = 13;

        public bool VertexColourMode => RenderMode >= VertexColourModeFirst && RenderMode <= VertexColourModeLast;

        public static int ShadingComboMode_O2(int renderMode) =>
            (renderMode >= VertexColourModeFirst && renderMode <= VertexColourModeLast) ? VertexColourModeFirst : renderMode;

        private static readonly string[] vcChannelLabels_O2 =
        {
            "RGB", "R natural", "G artificial", "B tint", "A alpha",
        };

        private void DrawVertexColourChannel_O2()
        {
            if (!VertexColourMode) return;
            ImGui.SetNextItemWidth(-140);
            int ch = RenderMode - VertexColourModeFirst;
            if (ImGui.Combo("Channel", ref ch, vcChannelLabels_O2, vcChannelLabels_O2.Length))
                RenderMode = VertexColourModeFirst + Math.Clamp(ch, 0, vcChannelLabels_O2.Length - 1);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "GTA bakes lighting into vertex COLOR0 and the shader uses each channel differently:\n" +
                    "  R  how much SKY (natural) ambient this vertex lets through\n" +
                    "  G  how much INTERIOR (artificial) ambient it lets through\n" +
                    "  B  the tint palette column on a _tnt preset (and the terrain blend elsewhere)\n" +
                    "  A  decal / blend weight\n" +
                    "Black R with a lit G is a room interior; white in both is a prop built for the street.\n" +
                    "Use it on an MLO to see whether its ambient is baked at all.");
        }
    }
}

