using ImGuiNET;

namespace RageLightEditor.Editor
{
    public static class UiSlider_U28
    {
        private static uint typingId, focusId, armedId;
        private static double armedAt;
        private static float armedFloat;
        private static int armedInt;

        public static bool Typing => typingId != 0 || focusId != 0;

        private static bool Rearm(uint id) => armedId == id && ImGui.GetTime() - armedAt <= ImGui.GetIO().MouseDoubleClickTime;

        private static void Before(uint id)
        {
            if (focusId != id) return;
            ImGui.SetKeyboardFocusHere();
            focusId = 0;
            typingId = id;
        }

        private static bool After(uint id)
        {
            if (typingId == id && ImGui.IsItemDeactivated()) typingId = 0;
            return ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && typingId != id;
        }

        public static bool Float(string label, ref float v, float min, float max) => Float(label, ref v, min, max, "%.3f", ImGuiSliderFlags.None);

        public static bool Float(string label, ref float v, float min, float max, string format) => Float(label, ref v, min, max, format, ImGuiSliderFlags.None);

        public static bool Float(string label, ref float v, float min, float max, string format, ImGuiSliderFlags flags)
        {
            uint id = ImGui.GetID(label);
            Before(id);
            float before = v;
            bool changed = ImGui.SliderFloat(label, ref v, min, max, format, flags | ImGuiSliderFlags.AlwaysClamp);
            if (ImGui.IsItemActivated() && !Rearm(id)) { armedId = id; armedFloat = before; armedAt = ImGui.GetTime(); }
            if (After(id))
            {
                if (armedId == id && v != armedFloat) { v = armedFloat; changed = true; }
                focusId = id;
            }
            return changed;
        }

        public static bool Int(string label, ref int v, int min, int max) => Int(label, ref v, min, max, "%d", ImGuiSliderFlags.None);

        public static bool Int(string label, ref int v, int min, int max, string format) => Int(label, ref v, min, max, format, ImGuiSliderFlags.None);

        public static bool Int(string label, ref int v, int min, int max, string format, ImGuiSliderFlags flags)
        {
            uint id = ImGui.GetID(label);
            Before(id);
            int before = v;
            bool changed = ImGui.SliderInt(label, ref v, min, max, format, flags | ImGuiSliderFlags.AlwaysClamp);
            if (ImGui.IsItemActivated() && !Rearm(id)) { armedId = id; armedInt = before; armedAt = ImGui.GetTime(); }
            if (After(id))
            {
                if (armedId == id && v != armedInt) { v = armedInt; changed = true; }
                focusId = id;
            }
            return changed;
        }
    }
}
