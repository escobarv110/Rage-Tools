using System;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool WorldGrass = true;
        public float WorldGrassDistance = 1.0f;
        public bool WorldHdTextures = true;

        public int WorldGrassBatches, WorldGrassInstances, WorldHdTextureHits, WorldLightTests;
        public float WorldGrassMs;

        public bool ShowLightShafts = true;
        public float LightShaftIntensity = 1.0f;
        public bool ShowExtensions;
        public int WorldLightShaftsDrawn, WorldExtensionsDrawn;

        partial void DrawHelpersExtras_H3()
        {
            OptCheck("Light shafts", ref ShowLightShafts,
                     "The CExtensionDefLightShaft extensions of the archetypes and entities in view,\n" +
                     "drawn as the beams they are (a quad extruded along its direction, fading out).\n" +
                     $"Now: {WorldLightShaftsDrawn:N0} shafts");
            SameCol();
            OptCheck("Extensions", ref ShowExtensions,
                     "The other extension types as small boxes at their offset: orange particles, green spawn\n" +
                     "points, blue audio, yellow doors, violet ladders, cyan buoyancy, lime proc objects.\n" +
                     $"Now: {WorldExtensionsDrawn:N0} markers");
            if (ShowLightShafts)
                OptSlider("Shaft glow", ref LightShaftIntensity, 0.0f, 3.0f, "%.2fx", "Scales every shaft's brightness. 1 = the extension's own intensity.", def: 1.0f);
        }

        partial void DrawRenderExtras_Materials()
        {
            OptCheck("Grass", ref WorldGrass,
                     "The map's grass batches, drawn instanced as the game draws them.\n" +
                     $"Now: {WorldGrassBatches:N0} batches, {WorldGrassInstances:N0} blades, {WorldGrassMs:0.00} ms");
            SameCol();
            bool hd = WorldHdTextures;
            if (OptCheck("HD textures", ref hd,
                         "Use the '+hi' texture dictionaries the manifests bind for an archetype\n" +
                         "(the game streams them in near the player). Takes effect on models built\n" +
                         $"from now on; 'Unload everything' rebuilds all. HD hits so far: {WorldHdTextureHits:N0}"))
                WorldHdTextures = hd;
            if (WorldGrass)
                OptSlider("Grass distance", ref WorldGrassDistance, 0.25f, 2.0f, "%.2fx", "Scales how far out grass batches are drawn (on top of Detail).", def: 1.0f);
        }
    }
}

