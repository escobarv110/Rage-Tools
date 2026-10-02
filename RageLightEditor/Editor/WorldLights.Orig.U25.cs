using System.Collections.Generic;
using CodeWalker;
using CodeWalker.GameFiles;
using SharpDX;

namespace RageLightEditor.Editor
{
    public partial class WorldLights
    {
        private readonly Dictionary<uint, Vector3[]> originalPos_U25 = new Dictionary<uint, Vector3[]>();

        private void RememberOriginal_U25(uint archetypeHash, LightDef[] defs)
        {
            if (defs == null || originalPos_U25.ContainsKey(archetypeHash)) return;
            var p = new Vector3[defs.Length];
            for (int i = 0; i < defs.Length; i++) p[i] = defs[i].Pos;
            originalPos_U25[archetypeHash] = p;
        }

        private void AddOriginalCells_U25(YmapEntityDef e, uint archetypeHash)
        {
            if (!originalPos_U25.TryGetValue(archetypeHash, out var p)) return;
            var ori = e.Orientation;
            var scale = SaneScale(e.Scale);
            for (int i = 0; i < p.Length; i++) hdCells.Add(Cell(ori.Multiply(p[i] * scale) + e.Position));
        }

        public bool HidesLodLightAt_U25(Vector3 p) => hdCells.Contains(Cell(p));
    }
}
