using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool WorldInteriorTimecycle = true;
        private bool interiorTcOptionLoaded;
        public string InteriorTimecycleStatus = "";
        public float InteriorTimecycleStrength;

        public void LoadInteriorTimecycleOption()
        {
            if (interiorTcOptionLoaded || settings == null) return;
            interiorTcOptionLoaded = true;
            WorldInteriorTimecycle = settings.WorldInteriorTimecycle;
        }

        partial void DrawLightingExtras_J3()
        {
            if (!WorldMode) return;
            LoadInteriorTimecycleOption();
            ImGui.Checkbox("Timecycle", ref WorldInteriorTimecycle);
            Tip("Inside an interior, grade the world with that room's own timecycle modifier, as the game does.\nOff: the weather's cycle everywhere.");
            Info("Inside an interior, grade the world with that ROOM's timecycle modifier - the\n" +
                 "one the room's ytyp names (timecycleName) - the way the game does when the\n" +
                 "player walks in: the showroom's, the bank vault's, the casino floor's own\n" +
                 "look, blended in over half a second and out again at the door. Which room\n" +
                 "you are in is the smallest room box the camera is inside; the modifiers are\n" +
                 "the game's timecycle_mods files.\n\n" +
                 "Off: the weather's cycle everywhere, as CodeWalker draws it.");
            DrawInteriorTimecycleReadout();
        }

        partial void DrawWorldMapTabExtras_J3()
        {
            if (!WorldInteriorTimecycle) return;
            if (string.IsNullOrEmpty(InteriorTimecycleStatus)) return;
            ImGui.Spacing();
            DrawInteriorTimecycleReadout();
        }

        private void DrawInteriorTimecycleReadout()
        {
            if (!WorldInteriorTimecycle) return;
            if (string.IsNullOrEmpty(InteriorTimecycleStatus))
            {
                ImGui.TextDisabled("Outside");
                return;
            }
            ImGui.TextColored(UiTheme.Accent, "Interior");
            ImGui.SameLine();
            ImGui.TextWrapped(InteriorTimecycleStatus.Replace("%", "%%"));
            ImGui.ProgressBar(System.Math.Clamp(InteriorTimecycleStrength, 0.0f, 1.0f), new System.Numerics.Vector2(-1, 6), "");
        }
    }
}

