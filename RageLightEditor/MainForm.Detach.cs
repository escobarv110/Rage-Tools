using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ImGuiNET;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public bool DebugDetachProject;

        private ProjectDetachedForm projDetached;
        private bool detachRestored, detachDebugApplied;
        private bool detachDefaultChecked, detachDefaultToSecondMonitor;
        private bool detachPlacementDirty;
        private double detachPlacementAt;
        private bool detachDebugShotDone;

        partial void RenderDetachedWindows_Detach(bool captureNow, float dt)
        {
            if (deviceResources == null || deviceResources.DeviceLost || settings == null) return;

            if (!detachRestored)
            {
                detachRestored = true;
                if (settings.ProjectDetached && screenshotPath == null) { ProjWin.Detached = true; ProjWin.Docked = false; }
            }
            if (DebugDetachProject && !detachDebugApplied)
            {
                detachDebugApplied = true;
                ProjWin.Visible = true;
                ProjWin.RequestDetach = true;
            }
            if (false && ProjWin.Visible && !detachDefaultChecked && screenshotPath == null && !IsHeadless)
            {
                detachDefaultChecked = true;
                bool neverChosen = !settings.ProjectDetached && settings.ProjectDetachedBounds == null && string.IsNullOrEmpty(settings.ProjectDetachedScreen);
                if (neverChosen && !ProjWin.Detached && Screen.AllScreens.Length > 1)
                {
                    detachDefaultToSecondMonitor = true;
                    ProjWin.RequestDetach = true;
                    Console.WriteLine($"DETACH default: {Screen.AllScreens.Length} monitors, project window opens detached on the second one");
                }
            }

            if (ProjWin.RequestDetach)
            {
                ProjWin.RequestDetach = false;
                ProjWin.Docked = false;
                ProjWin.Detached = true;
                ProjWin.Minimized = false;
                ProjWin.Maximized = false;
                SaveDetachState();
            }
            if (ProjWin.RequestAttach)
            {
                ProjWin.RequestAttach = false;
                ProjWin.Detached = false;
                SaveDetachState();
                if (!IsHeadless) { try { Activate(); } catch { } }
            }

            DetachProbeObserve("after requests");

            bool wantForm = ProjWin.Detached && ProjWin.Visible;
            if (!wantForm)
            {
                if (projDetached != null && projDetached.Visible) projDetached.Hide();
                FlushDetachPlacement();
                return;
            }

            if (projDetached == null) CreateDetachedForm();
            if (projDetached.DeviceLost) return;
            if (!projDetached.Visible)
            {
                projDetached.Show(this);
            }

            float mainScale = DeviceDpi / 96.0f;
            bool worldMode = panel.WorldMode;
            projDetached.RenderFrame(dt, ImGui.GetStyle(), mainScale,
                (w, h, focused) => ProjWin.DrawDetached(w, h, focused, worldMode));
            RunDetachProbe();

            if (captureNow && DebugDetachProject && screenshotPath != null && !detachDebugShotDone)
            {
                detachDebugShotDone = true;
                var path = System.IO.Path.ChangeExtension(screenshotPath, null) + ".detached.png";
                projDetached.PendingScreenshot = path;
                projDetached.RenderFrame(dt, ImGui.GetStyle(), mainScale,
                    (w, h, focused) => ProjWin.DrawDetached(w, h, focused, worldMode));
                Console.WriteLine($"DETACHED PROJECT frames={projDetached.FramesRendered} bounds={projDetached.Bounds} " +
                                  $"dpi={projDetached.DeviceDpi} screen={Screen.FromControl(projDetached).DeviceName}" +
                                  (detachProbeStep >= 5 ? $" probeAttached={detachProbeAttached}" : ""));
            }

            FlushDetachPlacement();
        }

        private void CreateDetachedForm()
        {
            projDetached = new ProjectDetachedForm(deviceResources.Device);
            projDetached.AttachRequested += () => ProjWin.RequestAttach = true;
            projDetached.PlacementChanged += () =>
            {
                if (projDetached == null || !projDetached.Visible) return;
                detachPlacementDirty = true;
                detachPlacementAt = clock.Elapsed.TotalSeconds;
            };
            RestoreDetachPlacement();
        }

        private void RestoreDetachPlacement()
        {
            var b = settings.ProjectDetachedBounds;
            Rectangle rect = Rectangle.Empty;
            if (b != null && b.Length == 4 && b[2] >= 200 && b[3] >= 150)
            {
                rect = new Rectangle(b[0], b[1], b[2], b[3]);
                bool onScreen = Screen.AllScreens.Any(s =>
                {
                    var wa = s.WorkingArea;
                    var i = Rectangle.Intersect(wa, rect);
                    return i.Width >= 120 && i.Height >= 80;
                });
                if (!onScreen) rect = Rectangle.Empty;
            }
            if (rect.IsEmpty)
            {
                var mainScreen = Screen.FromControl(this);
                var wa = mainScreen.WorkingArea;
                int w = Math.Min(1180, wa.Width - 40), h = Math.Min(760, wa.Height - 80);
                var right = Screen.AllScreens.FirstOrDefault(s => s.Bounds.Left >= wa.Right - 8 && s.DeviceName != mainScreen.DeviceName)
                         ?? Screen.AllScreens.FirstOrDefault(s => s.DeviceName != mainScreen.DeviceName && s.Bounds != mainScreen.Bounds);
                var mainRect = Bounds;
                if (right != null)
                {
                    var rw = right.WorkingArea;
                    if (detachDefaultToSecondMonitor)
                    {
                        w = Math.Max(rw.Width - 80, 560); h = Math.Max(rw.Height - 120, 360);
                        rect = new Rectangle(rw.Left + 40, rw.Top + 60, w, h);
                        settings.ProjectDetachedMaximized = true;
                    }
                    else
                    {
                        w = Math.Min(w, rw.Width - 40); h = Math.Min(h, rw.Height - 80);
                        rect = new Rectangle(rw.Left + 20, rw.Top + 40, w, h);
                    }
                }
                else
                {
                    rect = new Rectangle(
                        Math.Max(wa.Left, Math.Min(mainRect.Left + 120, wa.Right - w)),
                        Math.Max(wa.Top, Math.Min(mainRect.Top + 80, wa.Bottom - h)), w, h);
                }
            }
            projDetached.StartPosition = FormStartPosition.Manual;
            projDetached.Bounds = rect;
            if (settings.ProjectDetachedMaximized) projDetached.WindowState = FormWindowState.Maximized;
        }

        private void FlushDetachPlacement()
        {
            if (!detachPlacementDirty || projDetached == null) return;
            if (clock.Elapsed.TotalSeconds - detachPlacementAt < 1.0) return;
            detachPlacementDirty = false;
            SaveDetachState();
        }

        private void SaveDetachState()
        {
            settings.ProjectDetached = ProjWin.Detached;
            if (projDetached != null && projDetached.IsHandleCreated)
            {
                var r = projDetached.WindowState == FormWindowState.Normal ? projDetached.Bounds : projDetached.RestoreBounds;
                if (r.Width >= 200 && r.Height >= 150)
                    settings.ProjectDetachedBounds = new[] { r.X, r.Y, r.Width, r.Height };
                settings.ProjectDetachedMaximized = projDetached.WindowState == FormWindowState.Maximized;
                try { settings.ProjectDetachedScreen = Screen.FromControl(projDetached).DeviceName; } catch { }
            }
            if (screenshotPath == null && !DebugFpsBench) settings.Save();
        }

        private int detachProbeStep = -1;
        private int detachProbeFrame;
        private bool detachProbeAttached;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, MK_LBUTTON = 0x0001;

        private void RunDetachProbe()
        {
            if (detachProbeStep < 0)
            {
                if (Environment.GetEnvironmentVariable("RLE_DETACH_PROBE") != "1") { detachProbeStep = 99; return; }
                detachProbeStep = 0;
                ImGuiBackend.ImGuiInput.SuppressMouseLeave = true;
            }
            if (detachProbeStep >= 99 || projDetached == null || !projDetached.IsHandleCreated) return;
            detachProbeFrame++;
            if (detachProbeFrame < 30) return;
            var mn = ProjWin.AttachButtonMin; var mx = ProjWin.AttachButtonMax;
            int cx = (int)((mn.X + mx.X) * 0.5f), cy = (int)((mn.Y + mx.Y) * 0.5f);
            IntPtr lp = (IntPtr)((cy << 16) | (cx & 0xFFFF));
            switch (detachProbeStep)
            {
                case 0:
                    SendMessage(projDetached.Handle, WM_MOUSEMOVE, IntPtr.Zero, lp);
                    Console.WriteLine($"DETACHPROBE move to Attach button at {cx},{cy} (rect {mn.X:0},{mn.Y:0}-{mx.X:0},{mx.Y:0}) form {projDetached.ClientSize.Width}x{projDetached.ClientSize.Height}");
                    detachProbeStep = 1;
                    break;
                case 1:
                    Console.WriteLine($"DETACHPROBE hover {(ProjWin.AttachButtonHovered ? "OK" : "FAIL")}  hovered={ProjWin.AttachButtonHovered} imguiMouse={projDetached.LastMousePos} capture={projDetached.LastWantCaptureMouse} moves={projDetached.MouseMoveEvents}");
                    SendMessage(projDetached.Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lp);
                    detachProbeStep = 2;
                    break;
                case 2:
                    SendMessage(projDetached.Handle, WM_LBUTTONUP, IntPtr.Zero, lp);
                    detachProbeStep = 3;
                    break;
                case 3:
                    detachProbeStep = 4;
                    break;
                default:
                    break;
            }
        }

        private void DetachProbeObserve(string where)
        {
            if (detachProbeStep != 4) return;
            bool attached = !ProjWin.Detached;
            Console.WriteLine($"DETACHPROBE click {(attached ? "OK" : "FAIL")}  detached={ProjWin.Detached} formVisible={projDetached?.Visible} ({where})");
            detachProbeAttached = attached;
            detachProbeStep = 5;
            if (attached) ProjWin.RequestDetach = true;
        }

        partial void DisposeDetachedWindows_Detach()
        {
            if (projDetached == null) return;
            try
            {
                if (deviceResources != null && deviceResources.DeviceLost) projDetached.AbandonResources();
                else projDetached.ReleaseResources();
                projDetached.Dispose();
            }
            catch { }
            projDetached = null;
        }
    }
}

