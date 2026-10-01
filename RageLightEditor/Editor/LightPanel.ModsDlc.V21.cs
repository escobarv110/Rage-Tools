using System.Collections.Generic;
using System.Linq;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool WorldEnableMods;
        public int WorldDlcIndex_V21;
        private bool modsDlcLoaded_V21;
        private string[] dlcLabels_V21 = { "Latest" };
        private List<string> dlcNames_V21 = new List<string>();

        public string SelectedDlcName_V21 =>
            WorldDlcIndex_V21 > 0 && WorldDlcIndex_V21 - 1 < dlcNames_V21.Count ? dlcNames_V21[WorldDlcIndex_V21 - 1] : "";

        public void LoadModsDlcOptions_V21()
        {
            if (modsDlcLoaded_V21 || settings == null) return;
            modsDlcLoaded_V21 = true;
            WorldEnableMods = settings.WorldEnableMods;
        }

        private void SyncDlcList_V21()
        {
            var found = Game?.Cache?.DlcNameList;
            if (found == null || found.Count == 0) return;
            if (dlcNames_V21.Count == found.Count && dlcNames_V21.SequenceEqual(found)) return;

            string was = SelectedDlcName_V21;
            if (string.IsNullOrEmpty(was)) was = settings?.WorldDlc ?? "";
            dlcNames_V21 = new List<string>(found);
            dlcLabels_V21 = new[] { "Latest" }.Concat(dlcNames_V21).ToArray();
            int at = string.IsNullOrEmpty(was) ? -1 : dlcNames_V21.IndexOf(was);
            WorldDlcIndex_V21 = at >= 0 ? at + 1 : 0;
        }

        partial void DrawGeneralExtras_ModsDlc_V21()
        {
            if (!WorldMode) return;
            LoadModsDlcOptions_V21();
            SyncDlcList_V21();

            ImGui.Checkbox("Mods folder", ref WorldEnableMods);
            Tip("Stream what is in GTA V\\mods\\ instead of the stock archives - the OpenIV layout.");
            Info("Mods folder.\n" +
                 "OpenIV installs a mod by copying the archive it changes into a mods\\ folder\n" +
                 "beside the game, so the original is never touched. Both copies are indexed\n" +
                 "either way; this decides which one the world streams.\n" +
                 "On: the mods\\ copy wins wherever there is one, and the stock file is used for\n" +
                 "everything else - so you see the map the way the game loads it with your mods\\n" +
                 "installed. Off: the stock install only.\n" +
                 "Changing this reloads the world; it takes a few seconds and nothing is lost." +
                 (Game?.Cache?.RpfMan?.ModRpfs != null
                     ? $"\n\nFound in mods\\: {Game.Cache.RpfMan.ModRpfs.Count} archive(s)."
                     : ""));

            if (dlcLabels_V21.Length > 1)
            {
                OptWidth();
                ImGui.Combo("DLC level", ref WorldDlcIndex_V21, dlcLabels_V21, dlcLabels_V21.Length);
                Tip("Which DLC pack the map is built at. Newest is what the game loads.");
                Info("DLC level.\n" +
                     "Every online pack replaces and adds map files, so the map is different at\n" +
                     "each one. Newest installed is what the game itself loads and what you want\n" +
                     "for almost anything; picking an older pack shows the map as it was then -\n" +
                     "useful when a prop you are placing came in with a particular update.\n" +
                     "Changing this reloads the world.");
            }
        }
    }
}

