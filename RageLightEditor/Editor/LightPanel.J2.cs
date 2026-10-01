using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private static readonly Vector4 EditLightOn = new Vector4(0.96f, 0.65f, 0.11f, 1f);
        private static readonly Vector4 EditLightOnHover = new Vector4(1.00f, 0.74f, 0.25f, 1f);
        private static readonly Vector4 EditLightOff = new Vector4(0.42f, 0.30f, 0.10f, 0.85f);
        private static readonly Vector4 EditLightOffHover = new Vector4(0.62f, 0.44f, 0.14f, 1f);

        private string rightTabRequest = Environment.GetEnvironmentVariable("RLE_RIGHTTAB");
        private int rightTabRequestFrames;
        public void SelectRightTab_U22(string name)
        {
            rightTabRequest = name;
            rightTabRequestFrames = 0;
        }

        private bool BeginRightTab_J2(string name)
        {
            if (rightTabRequest == null || !string.Equals(rightTabRequest, name, StringComparison.OrdinalIgnoreCase))
                return ImGui.BeginTabItem(name);
            if (++rightTabRequestFrames > 30) rightTabRequest = null;
            bool dummy = true;
            return ImGui.BeginTabItem(name, ref dummy, ImGuiTabItemFlags.SetSelected);
        }

        public bool EditLightActive => SelectionModeEnum == WorldSelectionMode.Light;

        public void SetEditLight(bool on)
        {
            int idx = IndexOfMode(on ? WorldSelectionMode.Light : WorldSelectionMode.Entity);
            if (idx >= 0) SelectionMode = idx;
        }

        partial void DrawEditLightButton_J2()
        {
            bool on = EditLightActive;
            ImGui.PushStyleColor(ImGuiCol.Button, on ? EditLightOn : EditLightOff);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, on ? EditLightOnHover : EditLightOffHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, EditLightOnHover);
            if (on) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.06f, 0.06f, 0.07f, 1f));
            if (ImGui.Button(on ? "Edit Light  *" : "Edit Light")) SetEditLight(!on);
            if (on) ImGui.PopStyleColor(1);
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(on
                    ? "Light editing is ON: a click on a light marker (or on the lamp that carries it) selects that\n" +
                      "light and the Inspector edits it in place - colour, intensity, cone, position. Click again to\n" +
                      "go back to selecting entities."
                    : "Edit the props' lights where they stand: click to switch the selection mode to Light - every\n" +
                      "lamp within 60 m shows a small marker, click one (or the lamp itself) to edit its light.");
            ImGui.SameLine();
        }

        partial void DrawWorldNoSelectionHint_J2(ref bool handled)
        {
            if (!EditLightActive) return;
            handled = true;
            ImGui.PushStyleColor(ImGuiCol.Text, EditLightOn);
            ImGui.TextWrapped("Edit Light is on.");
            ImGui.PopStyleColor();
            ImGui.TextWrapped("Click a light marker - the small ring on every lamp within 60 m - to select that light and " +
                              "edit it here. Clicking the lamp itself picks its nearest light. Hover shows which light a click would take.");
            ImGui.TextDisabled("A prop with no lights? Click it, then + Point / + Spot / + Capsule adds one.");
            ImGui.TextDisabled("Edit Light again (or the mode combo) goes back to selecting entities.");
        }
    }
}

