using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool WorldYmapHourFilter = true;
        public bool WorldYmapWeatherFilter = true;
        public bool WorldVariantsIncludeDlc = true;
        public int WorldHourFiltered, WorldTimedYmaps;

        partial void DrawGeneralExtras_World()
        {
            if (!WorldMode) return;
            OptCheck("Hour filter", ref WorldYmapHourFilter,
                     "The manifests schedule some ymaps by hour - a shop's night dressing, a building\n" +
                     "site's two states. On: only the ones the game has loaded at this hour, as\n" +
                     "the game does. Off: all of them, on top of each other." +
                     (WorldTimedYmaps > 0 ? $"\n\n{WorldTimedYmaps} ymaps are scheduled; {WorldHourFiltered} left out right now." : ""));
            SameCol();
            OptCheck("Weather filter", ref WorldYmapWeatherFilter,
                     "Ymaps the manifests tie to a weather type (wet-weather decals) only in that\nweather - the game's weather filter.");
            OptCheck("DLC replaces", ref WorldVariantsIncludeDlc,
                     "A DLC ymap that puts a building on the footprint of the base map's building is that\n" +
                     "building's replacement (the game's dlc_patch does the same): the base copy under it\n" +
                     "is hidden. Off: only scripted ymaps are treated as variants.");
        }
    }
}

