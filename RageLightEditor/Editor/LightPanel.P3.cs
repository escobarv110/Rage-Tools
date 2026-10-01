using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool HideBaseUnderProject = System.Environment.GetEnvironmentVariable("RLE_NOPROJHIDE") != "1";
        public int ProjectBaseYmapsHidden, ProjectFootprints, ProjectGrassHidden, ProjectLodLightsHidden;

        partial void DrawRenderExtras_P3()
        {
            OptCheck("Clear under project", ref HideBaseUnderProject,
                     "A project ymap replaces the game's file of the same name COMPLETELY: its grass batches\n" +
                     "and its lodlights / distantlights partners go with it, so a package that ships emptied\n" +
                     "copies of them (a 'vanilla' folder) really does delete them.\n" +
                     "And a project ymap or MLO with a new name clears the base map's grass and LOD lights\n" +
                     "inside its own footprint, so a new building does not have the old one's tufts growing\n" +
                     "through its floor or its street lights floating over the roof.\n\n" +
                     "Off: the base map's grass and LOD lights draw wherever the game put them.");
            if (HideBaseUnderProject && (ProjectBaseYmapsHidden > 0 || ProjectFootprints > 0))
                ImGui.TextDisabled($"{ProjectBaseYmapsHidden:N0} base ymap(s) replaced, {ProjectFootprints} footprint(s): " +
                                   $"{ProjectGrassHidden:N0} grass batches, {ProjectLodLightsHidden:N0} LOD lights hidden");
        }
    }
}

