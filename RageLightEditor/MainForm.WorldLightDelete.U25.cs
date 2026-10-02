using System;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private static readonly bool NoProjTex_U25 = Environment.GetEnvironmentVariable("RLE_NOPROJTEX") == "1";
        private static readonly bool NoCoronas_U25 = Environment.GetEnvironmentVariable("RLE_NOCORONAS") == "1";

        private bool WorldDeleteLight_U25()
        {
            var s = WorldEdit.Selection;
            var la = s.Light;
            if (la == null) return false;
            uint hash = s.LightEntity?.Archetype?.Hash ?? ArchOfLight(la);
            var db = hash != 0 ? worldRender.Lights.GetDrawable(hash) : null;
            if (db == null) { WorldEdit.LastStatus = "that light's model is not loaded - move closer and try again"; return true; }
            var items = WorldLights.LightsOf(db);
            int index = items == null ? -1 : Array.IndexOf(items, la);
            if (index < 0 || !WorldLights.RemoveLight_U18(db, la)) { WorldEdit.LastStatus = "could not delete that light"; return true; }
            string owner = s.LightEntity?.Archetype?.Name.ToString() ?? "the prop";
            worldLightUnsaved.Add(hash);
            worldRender.Lights.Invalidate(hash);
            WorldEdit.Deselect();
            WorldEdit.LastStatus = $"light deleted from {owner} - Save as... or Add to project keeps it";
            WorldHistory.Push(new DelegateCommand("Delete light",
                () =>
                {
                    if (WorldLights.RemoveLight_U18(db, la)) { worldLightUnsaved.Add(hash); worldRender.Lights.Invalidate(hash); }
                    if (ReferenceEquals(WorldEdit.Selection.Light, la)) WorldEdit.Deselect();
                },
                () =>
                {
                    if (WorldLights.InsertLightAt_U18(db, la, index) >= 0) { worldLightUnsaved.Add(hash); worldRender.Lights.Invalidate(hash); }
                }));
            return true;
        }

        private void ServiceWorldLightDelete_U25()
        {
            if (panel == null || !panel.RequestWorldLightDelete_U25) return;
            panel.RequestWorldLightDelete_U25 = false;
            WorldDeleteLight_U25();
        }
    }
}
