using System.Windows.Forms;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public int MagnetCount_U28 { get; private set; }

        private bool Magnet_U28()
        {
            if (panel == null || ImGuiWantsMouse) return false;
            var p = PointToClient(Cursor.Position);
            if (!ClientRectangle.Contains(p)) return false;
            return MagnetAt_U28(p.X, p.Y);
        }

        private bool MagnetAt_U28(int x, int y)
        {
            if (panel.WorldMode)
            {
                if (panel.NavMode || !worldBuilt) return false;
                WorldPickAt(x, y);
                bool hit = WorldEdit.Selected != null || WorldEdit.Selection.HasValue;
                if (!hit) { WorldEdit.LastStatus = "Nothing under the mouse to fly to"; return false; }
                FrameWorldSelection_M3();
                MagnetCount_U28++;
                return true;
            }
            var sc = scene;
            if (sc == null) return false;
            if (panel.MloMode)
            {
                if (MloPickerArmed_T1(Creator)) return false;
                string before = SelectionSignature_T1();
                PickAt(x, y);
                if (SelectionSignature_T1() == before) { panel.MloStatus = "Nothing under the mouse to fly to"; return false; }
            }
            else if (!PickLight(x, y))
            {
                var ray = camera.GetPickRay(x, y, deviceResources.Width, deviceResources.Height);
                var file = FindPropUnder(ray, out var mesh);
                if (file == null) { panel.MloStatus = "Nothing under the mouse to fly to"; return false; }
                sc.SelectFile(file, false, false);
                panel.ScrollToActiveProp = true;
                if (panel.MaterialMode && materialPanel != null && mesh?.Shader != null) materialPanel.SelectByShader(mesh.Shader, false);
                panel.MloStatus = $"Selected {file.Name}";
            }
            FrameSelection();
            MagnetCount_U28++;
            return true;
        }
    }
}
