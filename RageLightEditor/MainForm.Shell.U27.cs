using System;
using System.Runtime.InteropServices;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public Action<bool> FullscreenHook_U27;
        public bool RestartPending_U27 { get; private set; }

        internal LightPanel Panel_U27 => panel;
        internal float Fps_U27 => fpsSmoothed;

        [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetFocus();

        internal void AttachToShell_U27()
        {
            MouseDown += (s, e) => FocusViewport_U27();
        }

        internal void FocusViewport_U27()
        {
            if (IsHandleCreated && GetFocus() != Handle) SetFocus(Handle);
        }

        internal void ShellDeactivated_U27() => walkKeys.Clear();

        private bool ServiceRestart_U27()
        {
            if (panel == null || !panel.RequestRestart_U27) return false;
            panel.RequestRestart_U27 = false;
            RestartPending_U27 = true;
            Close();
            return true;
        }
    }
}
