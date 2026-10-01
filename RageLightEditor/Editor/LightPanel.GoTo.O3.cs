using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public string GotoText_O3 = "";
        public Vector3? RequestGoto_O3;
        public bool RequestGotoInstant_O3;
        public Vector3 CameraCoords_O3;
        public string GotoStatus_O3 = "";
        private bool gotoStatusBad_O3;

        private static readonly Regex GotoWords_O3 = new Regex(@"[A-Za-z_]+\d*", RegexOptions.Compiled);
        private static readonly Regex GotoNumber_O3 = new Regex(@"[-+]?\d+(?:\.\d+)?", RegexOptions.Compiled);

        public static bool ParseCoords_O3(string text, out Vector3 p)
        {
            p = Vector3.Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string cleaned = GotoWords_O3.Replace(text, " ");
            var m = GotoNumber_O3.Matches(cleaned);
            if (m.Count < 3) return false;
            if (!float.TryParse(m[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(m[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(m[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return false;
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) return false;
            p = new Vector3(x, y, z);
            return true;
        }

        public static string FormatCoords_O3(Vector3 p) =>
            p.X.ToString("0.###", CultureInfo.InvariantCulture) + ", " +
            p.Y.ToString("0.###", CultureInfo.InvariantCulture) + ", " +
            p.Z.ToString("0.###", CultureInfo.InvariantCulture);

        private void GotoFromBox_O3(bool instant)
        {
            if (!ParseCoords_O3(GotoText_O3, out var p))
            {
                GotoStatus_O3 = "That is not a position - paste three numbers (x, y, z) in any shape.";
                gotoStatusBad_O3 = true;
                return;
            }
            RequestGoto_O3 = p;
            RequestGotoInstant_O3 = instant;
            gotoStatusBad_O3 = false;
            GotoStatus_O3 = (instant ? "Jumping to " : "Flying to ") + FormatCoords_O3(p) + "...";
        }

        public void DrawGoToRow_O3()
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("GO TO");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Paste a position and press Enter - the camera goes there and looks at it.\n" +
                                 "Any shape works:  1234.5, -678.9, 30.1   |   1234.5 -678.9 30.1\n" +
                                 "[1234.5, -678.9, 30.1]   |   vector3(1234.5, -678.9, 30.1)   |   X:1234.5 Y:-678.9 Z:30.1\n" +
                                 "Hold Shift to jump instead of flying.  (Ctrl+G puts the cursor here)");
            DrawGoToBody_O3();
            DrawWorldFindEntry_V56();
        }

        public void DrawGoToBody_O3()
        {
            ImGui.SetNextItemWidth(-84);
            bool enter = ImGui.InputTextWithHint("##o3goto", "paste x, y, z", ref GotoText_O3, 160, ImGuiInputTextFlags.EnterReturnsTrue);
            if (GotoFocus_O3) { GotoFocus_O3 = false; ImGui.SetKeyboardFocusHere(-1); }
            bool shift = ImGui.GetIO().KeyShift;
            if (enter) GotoFromBox_O3(shift);
            ImGui.SameLine();
            if (ImGui.Button(shift ? "Jump##o3gotob" : "Go##o3gotob", new Vector2(-1, 0))) GotoFromBox_O3(shift);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(shift ? "Jump straight there (Shift held)." : "Fly there - hold Shift to jump instead.");

            if (ImGui.SmallButton("Copy coords##o3gotocopy"))
            {
                string s = FormatCoords_O3(CameraCoords_O3);
                try { ImGui.SetClipboardText(s); } catch { }
                GotoText_O3 = s;
                gotoStatusBad_O3 = false;
                GotoStatus_O3 = "Copied " + s + " to the clipboard.";
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Put the camera's position ({FormatCoords_O3(CameraCoords_O3)}) on the clipboard\nand in the box, so it can be pasted anywhere - or gone back to later.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Paste##o3gotopaste"))
            {
                try { GotoText_O3 = ImGui.GetClipboardText() ?? ""; } catch { }
                if (!string.IsNullOrWhiteSpace(GotoText_O3)) GotoFromBox_O3(ImGui.GetIO().KeyShift);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Take the coordinates off the clipboard and go there.");
            DrawResetViewRow_U1();
            if (!string.IsNullOrEmpty(GotoStatus_O3))
            {
                if (gotoStatusBad_O3) ImGui.TextColored(UiTheme.Warn, GotoStatus_O3);
                else ImGui.TextDisabled(GotoStatus_O3);
            }
        }

        public bool GotoFocus_O3;

        public static string SafeIndex_O3(string[] a, int i) => a != null && i >= 0 && i < a.Length ? a[i] : i.ToString();
    }
}

