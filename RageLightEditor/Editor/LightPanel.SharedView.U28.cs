using System.Collections.Generic;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private static readonly Space[] ViewGroup_U28 = { Space.Light, Space.Material };

        public static bool SharesView_U28(Space a, Space b) => InViewGroup_U28(a) && InViewGroup_U28(b);

        private static bool InViewGroup_U28(Space s) => System.Array.IndexOf(ViewGroup_U28, s) >= 0;

        public static IEnumerable<Space> ViewGroupOf_U28(Space s)
        {
            if (!InViewGroup_U28(s)) yield break;
            foreach (var g in ViewGroup_U28) if (g != s) yield return g;
        }

        private void ShareViewState_U28(Space from)
        {
            foreach (var s in ViewGroupOf_U28(from))
            {
                workspaceTc[(int)s] = workspaceTc[(int)from];
                sectionView_Q4[(int)s] = sectionView_Q4[(int)from];
            }
        }
    }
}
