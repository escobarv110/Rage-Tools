using System;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public struct SectionViewState
        {
            public bool Valid;
            public int RenderMode;
            public bool ShowGrid, ShowGizmos, ShowAllGizmos, ShowMarkers, EntityPicking;
            public int SelectionMode;
            public bool MouseSelectEnabled;
        }

        private readonly SectionViewState[] sectionView_Q4 = new SectionViewState[Enum.GetValues(typeof(Space)).Length];

        public SectionViewState GetSectionViewState_Q4(Space s) => sectionView_Q4[(int)s];

        internal void EnterWorkspace(Space space)
        {
            if (workspace == space) return;
            var was = workspace;
            WorkspaceSwitching?.Invoke(was, space);
            SaveWorkspaceState(was);
            SaveSectionView_Q4(was);
            ShareViewState_U28(was);
            workspace = space;
            RestoreWorkspaceState(space);
            if (space == Space.Cinematic) EnterCineWorkspace();
            else if (was == Space.Cinematic) LeaveCineWorkspace();
            RestoreSectionView_Q4(space);
            if (space != Space.Cinematic && RenderMode == 7) RenderMode = 0;
        }

        private void SaveSectionView_Q4(Space s)
        {
            sectionView_Q4[(int)s] = new SectionViewState
            {
                Valid = true,
                RenderMode = s == Space.Cinematic ? 7 : RenderMode,
                ShowGrid = ShowGrid,
                ShowGizmos = ShowGizmos,
                ShowAllGizmos = ShowAllGizmos,
                ShowMarkers = ShowMarkers,
                EntityPicking = EntityPicking,
                SelectionMode = SelectionMode,
                MouseSelectEnabled = MouseSelectEnabled,
            };
        }

        private void RestoreSectionView_Q4(Space s)
        {
            var st = sectionView_Q4[(int)s];
            if (!st.Valid) return;
            if (s != Space.Cinematic)
            {
                RenderMode = st.RenderMode;
                ShowGrid = st.ShowGrid;
                ShowGizmos = st.ShowGizmos;
                ShowAllGizmos = st.ShowAllGizmos;
                ShowMarkers = st.ShowMarkers;
                EntityPicking = st.EntityPicking;
            }
            SelectionMode = st.SelectionMode;
            MouseSelectEnabled = st.MouseSelectEnabled;
        }
    }
}

