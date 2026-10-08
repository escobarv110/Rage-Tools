using System;
using CodeWalker.GameFiles;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void WorldDuplicateMloChild_U31(YmapEntityDef src)
        {
            YmapEntityDef clone;
            try { clone = BuildMloChildClone_V65(src); }
            catch (Exception ex) { WorldEdit.LastStatus = "could not duplicate: " + ex.Message; return; }
            if (clone == null) { WorldEdit.LastStatus = "could not duplicate"; return; }
            float step = Math.Max(src.BSRadius * 1.2f, 1.0f);
            clone.SetPosition(src.Position + new SharpDX.Vector3(step, 0, 0));
            WorldEntityChanged(clone);
            WorldEdit.Select(clone);
            string name = clone.Archetype?.Name ?? clone._CEntityDef.archetypeName.ToString();
            WorldEdit.LastStatus = "duplicated " + name;
            WorldHistory.Push("Duplicate " + name,
                () => { ReattachClone_V65(clone); WorldEdit.Select(clone); },
                () => { DetachClone_V65(clone); WorldEdit.Select(src); });
        }
    }
}
