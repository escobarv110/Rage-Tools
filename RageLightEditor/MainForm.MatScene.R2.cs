using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void BindMatPanel_R2(Scene target)
        {
            if (materialPanel != null && target != null) materialPanel.Scene = target;
        }
    }
}
