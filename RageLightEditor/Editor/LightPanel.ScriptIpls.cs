using System.Linq;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool WorldScriptIpls = true;
        public int WorldInteriorSets = 1;
        private bool scriptIplOptionLoaded;
        private static readonly string[] InteriorSetsLabels = { "Default", "Auto", "All" };

        public void LoadScriptIplOptions()
        {
            if (scriptIplOptionLoaded || settings == null) return;
            scriptIplOptionLoaded = true;
            WorldScriptIpls = settings.WorldScriptIpls;
            WorldInteriorSets = System.Math.Clamp(settings.WorldInteriorSets, 0, 2);
        }

        partial void DrawGeneralExtras_ScriptIpls()
        {
            if (!WorldMode) return;
            LoadScriptIplOptions();
            ImGui.Checkbox("Script IPLs", ref WorldScriptIpls);
            Tip("Bring back the interiors the online DLCs' change sets dropped (PDM showroom, FIB lobby, bank vault) - what every FiveM server RequestIpl()s.");
            {
                var c = Game?.Cache;
                string list = "";
                if (c?.ScriptIpls != null && c.ScriptIpls.Count > 0)
                    list = "\n\nBrought back right now: " + string.Join(", ", c.ScriptIpls.Where(i => i.Applied).Select(i => i.Name).Take(24)) +
                           (c.ScriptIpls.Count > 24 ? $" (+{c.ScriptIpls.Count - 24} more)" : "");
                Info("The online DLCs' map change sets drop whole base rpfs and re-supply most of their\n" +
                     "ymaps under a DLC prefix - but a few interiors never come back: the Premium Deluxe\n" +
                     "Motorsport showroom (shr_int), the FIB lobby, the bank vault... In the game they are\n" +
                     "script-requested IPLs, and every FiveM server RequestIpl()s them. On: the ymaps the\n" +
                     "change sets dropped WITHOUT a replacement come back when they place an interior\n" +
                     "the game's cache knows and no active interior already stands at that pivot. Off:\n" +
                     "the map's stock active set alone (the PDM site is a bare slab)." + list);
            }
            OptWidth();
            ImGui.Combo("Interior sets", ref WorldInteriorSets, InteriorSetsLabels, InteriorSetsLabels.Length);
            Tip("Which of an online interior's switchable entity sets are drawn.\nAs placed: the ymap's defaults. Auto: one theme per interior. All: every set at once.");
            Info("Interior entity sets. The online interiors keep their walls and furniture in\n" +
                 "switchable sets - the auto shop's style_1..9, the nightclub's style01..03, the\n" +
                 "arcade's constant_geometry - that a script turns on when you walk in, so drawn\n" +
                 "as placed they are bare boxes.\n" +
                 "As placed: only the ymap's defaultEntitySets (the map's default).\n" +
                 "Auto: ONE theme per interior - the sets are grouped into families (style_1..9,\n" +
                 "style01..03, wpaper_1..9, office_basic / modern, ceiling_beams / flat, X / no_X)\n" +
                 "and exactly one member of each is on (the placed one, else the first / plainest),\n" +
                 "plus the constant / default / shell sets; everything else stays off, so two\n" +
                 "themes never stand in each other. The log's INTSETS lines say why per set.\n" +
                 "All: every set at once, on top of each other." +
                 (WorldRef != null && WorldRef.InteriorSetsAutoOn > 0 ? $"\n\nAuto has turned on {WorldRef.InteriorSetsAutoOn} sets so far." : ""));
        }
    }
}

