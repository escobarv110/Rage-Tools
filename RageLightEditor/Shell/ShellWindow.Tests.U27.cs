using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using CodeWalker.GameFiles;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using RageLightEditor.Editor;
using Space = RageLightEditor.Editor.LightPanel.Space;

namespace RageLightEditor.Shell
{
    public partial class ShellWindow_U27
    {
        [DllImport("user32.dll")] private static extern IntPtr GetFocus();
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }

        private void Snap_U27(string path)
        {
            try
            {
                var h = Window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (h == IntPtr.Zero || !GetWindowRect(h, out var r)) return;
                using var bmp = new System.Drawing.Bitmap(r.R - r.L, r.B - r.T);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    var hdc = g.GetHdc();
                    PrintWindow(h, hdc, 2);
                    g.ReleaseHdc(hdc);
                }
                bmp.Save(path);
            }
            catch { }
        }

        private void StartSelfTest_U27()
        {
            var outPath = Environment.GetEnvironmentVariable("RLE_SHELLTEST");
            if (string.IsNullOrEmpty(outPath)) return;
            _ = RunSelfTest_U27(outPath);
        }

        private async Task RunSelfTest_U27(string outPath)
        {
            var lines = new List<string>();
            int fails = 0;
            void Check(string what, bool ok, string detail = "")
            {
                if (!ok) fails++;
                lines.Add((ok ? "PASS " : "FAIL ") + what + (detail.Length > 0 ? "  (" + detail + ")" : ""));
            }
            async Task Frames(int ms = 400) { await Task.Delay(ms); Sync(); }

            try
            {
                for (int i = 0; i < 240 && (P == null || form.Fps_U27 <= 0); i++) await Task.Delay(250);
                await Frames(1500);
                var p = P;
                Check("panel ready", p != null);
                if (p == null) throw new Exception("no panel");

                Check("imgui top bar hidden", p.TopBarHeight == 0.0f, p.TopBarHeight.ToString());
                Check("viewport has keyboard focus", GetFocus() == form.Handle);
                Check("form is a child window", !form.TopLevel);

                Space Ws(string label) { foreach (var t in TabNames) if (t.label == label) return t.space; return Space.World; }
                ShellButton_U27 Tab(Space s) { foreach (var t in tabs) if (t.space == s) return t.btn; return null; }
                Tab(Ws("Lights")).PerformClick(); await Frames();
                Check("tab switches to Lights", p.Workspace == Space.Light, p.Workspace.ToString());
                Check("world tools hidden outside World", !worldTools.IsVisible && !paletteArea.IsVisible);
                Tab(Space.World).PerformClick(); await Frames();
                Check("tab switches back to World", p.Workspace == Space.World, p.Workspace.ToString());
                Check("world tools shown in World", worldTools.IsVisible && paletteArea.IsVisible);
                Check("World tab is lit", Tab(Space.World).IsOn && !Tab(Space.Light).IsOn);

                var g = p.WorldGizmo;
                Check("gizmo exists", g != null);
                if (g != null)
                {
                    bRotate.PerformClick(); await Frames();
                    Check("Rotate tool", g.Mode == WorldGizmoMode.Rotate && bRotate.IsOn && !bMove.IsOn);
                    bScale.PerformClick(); await Frames();
                    Check("Scale tool", g.Mode == WorldGizmoMode.Scale && bScale.IsOn);
                    bSelect.PerformClick(); await Frames();
                    Check("Select tool", g.Mode == WorldGizmoMode.Select && bSelect.IsOn);
                    bMove.PerformClick(); await Frames();
                    Check("Move tool", g.Mode == WorldGizmoMode.Translate && bMove.IsOn);

                    var sp0 = g.Space;
                    bAxes.PerformClick(); await Frames();
                    Check("axes toggle", g.Space != sp0 && bAxes.Text == (g.Space == WorldGizmoSpace.Local ? "Local" : "World"), bAxes.Text);
                    bAxes.PerformClick(); await Frames();
                    Check("axes toggle back", g.Space == sp0);

                    float s0 = g.RotateSnapDeg;
                    bSnapUp.PerformClick(); await Frames();
                    Check("snap up", g.RotateSnapDeg > s0, s0 + " -> " + g.RotateSnapDeg);
                    Check("snap label follows", bSnap.Text == RotateSnapSteps_U5.Label(g.RotateSnapDeg), bSnap.Text);
                    bSnapDown.PerformClick(); await Frames();
                    Check("snap down", Math.Abs(g.RotateSnapDeg - s0) < 0.001f, g.RotateSnapDeg.ToString());
                }

                bool pick0 = p.MouseSelectEnabled;
                bPick.PerformClick(); await Frames();
                Check("picking toggle", p.MouseSelectEnabled != pick0 && bPick.IsOn == p.MouseSelectEnabled);
                bPick.PerformClick(); await Frames();
                Check("picking toggle back", p.MouseSelectEnabled == pick0);

                ShellButton_U27 Tile(int mode) { foreach (var t in tiles) if (t.mode == mode) return t.btn; return null; }
                int lightMode = Array.IndexOf(LightPanel.SelectionModeNames, "Light");
                int mloMode = Array.IndexOf(LightPanel.SelectionModeNames, "Mlo Instance");
                Tile(mloMode).PerformClick(); await Frames();
                Check("palette picks MLO", p.SelectionMode == mloMode && Tile(mloMode).IsOn && !Tile(0).IsOn);
                Tile(lightMode).PerformClick(); await Frames();
                Check("palette picks Light", p.SelectionMode == lightMode && p.EditLightActive && bEditLight.IsOn);
                Tile(0).PerformClick(); await Frames();
                Check("palette picks Entity", p.SelectionMode == 0 && !bEditLight.IsOn);
                Check("unavailable modes not offered", Tile(Array.IndexOf(LightPanel.SelectionModeNames, "Archetype Extension")) == null);

                int refreshed = 0;
                foreach (var r in menuRefresh) { r(); refreshed++; }
                Check("every menu item refreshes", refreshed == menuRefresh.Count && refreshed > 30, refreshed.ToString());

                p.ShellPopup_U27 = "appearance"; await Frames();
                Check("appearance popup opened by the imgui frame", p.ShellPopup_U27 == null);

                AppLog_U21.Info("SHELLTEST marker line");
                await Frames();
                string shown = outputText.Inlines?.Text ?? "";
                Check("output panel shows new log lines", shown.Contains("SHELLTEST marker line"));
                outTabErrors.PerformClick(); await Frames();
                Check("problems tab hides info lines", !(outputText.Inlines?.Text ?? "").Contains("SHELLTEST marker line"));
                outTabAll.PerformClick(); await Frames();

                bOutput.PerformClick(); await Frames();
                Check("output panel hides", !outputArea.IsVisible);
                bOutput.PerformClick(); await Frames();
                Check("output panel shows", outputArea.IsVisible);

                SetFullscreen(true); await Frames(800);
                Check("fullscreen hides the chrome", Window.WindowState == WindowState.FullScreen && !topArea.IsVisible && !paletteArea.IsVisible);
                SetFullscreen(false); await Frames(800);
                Check("fullscreen restores the chrome", Window.WindowState != WindowState.FullScreen && topArea.IsVisible);

                Check("status bar names the workspace", (statusLeft.Inlines?.Text ?? "").StartsWith("World"), statusLeft.Inlines?.Text ?? "");
                Check("status bar shows frame stats", (statusRight.Text ?? "").Contains("fps"), statusRight.Text ?? "");
                Check("still rendering", form.Fps_U27 > 1, form.Fps_U27.ToString("0"));

                string snapDir = Path.GetDirectoryName(outPath);
                Check("right column shown in World", rightHeader.IsVisible && rightColumn.Width.Value > 0);
                p.RequestWorldDeselect = true; await Frames(600);
                Check("inspector is native with nothing selected", rightBody.IsVisible && !p.ShellRightImGui_U27 && noSelNote.IsVisible);
                for (int i = 0; i < 40 && tree.Items.Count == 0; i++) { treeTick = 0; await Frames(500); }
                Check("map tree lists nearby ymaps", tree.Items.Count > 0, tree.Items.Count.ToString());
                TreeViewItem ent = null;
                foreach (var it in tree.Items.OfType<TreeViewItem>())
                {
                    it.IsExpanded = true; await Frames(200);
                    ent = it.Items.OfType<TreeViewItem>().FirstOrDefault(c => c.Tag is YmapEntityDef d && d.Archetype != null);
                    if (ent != null) break;
                    it.IsExpanded = false;
                }
                Check("a ymap expands into entities", ent != null);
                if (ent != null)
                {
                    tree.SelectedItem = ent; await Frames(800);
                    var en = (YmapEntityDef)ent.Tag;
                    Check("picking in the tree selects the entity", ReferenceEquals(p.WorldSel, en));
                    Check("inspector stays native for an entity", rightBody.IsVisible && selBar.IsVisible && !p.ShellRightImGui_U27);
                    Check("selected bar names it", (selName.Text ?? "").StartsWith(en.Archetype.Name), selName.Text);
                    Check("position field shows X", fields[0].box.Text == en.Position.X.ToString("0.###", CultureInfo.InvariantCulture), fields[0].box.Text);
                    Snap_U27(Path.Combine(snapDir, "shell_inspector.png"));
                    float x0 = en.Position.X;
                    Apply(fields[0].set, x0 + 1.0f); await Frames(600);
                    Check("editing X moves the entity", Math.Abs(p.WorldSel.Position.X - (x0 + 1.0f)) < 0.01f, p.WorldSel.Position.X.ToString());
                    Check("the edit can be undone", p.ShellCanUndo_U27);
                    p.ShellCommand_U27("undo"); await Frames(800);
                    Check("undo puts it back", Math.Abs(p.WorldSel.Position.X - x0) < 0.01f, p.WorldSel.Position.X.ToString());
                    Check("an edit opens the Project page, as in the classic panel", p.ShellRightPage_U27 == LightPanel.ShellPageProject_U27 || p.ProjectWindow == null);
                    pageTabs[LightPanel.ShellPageInspector_U27].PerformClick(); await Frames();
                    detailTab.PerformClick(); await Frames();
                    Check("details tab lists the entity", detailRows.Children.Count > 10 && detailsPane.IsVisible, detailRows.Children.Count.ToString());
                    propTab.PerformClick(); await Frames();
                    subLights.PerformClick(); await Frames();
                    Check("lights tab lists something", lightsList.Children.Count > 0 && lightsPane.IsVisible);
                    subMap.PerformClick(); await Frames();
                    p.RequestWorldDiscard = true; await Frames(600);
                }
                pageTabs[LightPanel.ShellPageOptions_U27].PerformClick(); await Frames(600);
                Check("options page is drawn in the column by imgui", p.ShellRightImGui_U27 && p.ShellRightPage_U27 == LightPanel.ShellPageOptions_U27 && !rightBody.IsVisible && Grid.GetColumnSpan(viewportArea) == 3);
                Snap_U27(Path.Combine(snapDir, "shell_options.png"));
                pageTabs[LightPanel.ShellPageInspector_U27].PerformClick(); await Frames(600);
                Check("inspector page comes back", p.ShellRightPage_U27 == 0 && Grid.GetColumnSpan(viewportArea) == (p.ShellInspectorNative_U27 ? 1 : 3));
                if (p.ProjectWindow != null)
                {
                    bProject.PerformClick(); await Frames(600);
                    Check("project button opens the Project page", p.ShellRightPage_U27 == LightPanel.ShellPageProject_U27 && pageTabs[1].IsOn);
                    Snap_U27(Path.Combine(snapDir, "shell_project.png"));
                    bProject.PerformClick(); await Frames(600);
                    Check("project button closes it again", p.ShellRightPage_U27 == 0);
                }

                bool was = p.NewUi_U27;
                p.NewUi_U27 = false;
                Check("choosing Classic is saved", !AppSettings.PeekNewUi_U27() && p.InterfaceChangePending_U27);
                p.NewUi_U27 = true;
                Check("choosing New is saved", AppSettings.PeekNewUi_U27() && !p.InterfaceChangePending_U27);
                p.NewUi_U27 = was;

                if (Environment.GetEnvironmentVariable("RLE_SHELLRESTART") == "1")
                {
                    Environment.SetEnvironmentVariable("RLE_SHELLRESTART", null);
                    lines.Add(fails == 0 ? "SHELLTEST PASSED (restarting)" : $"SHELLTEST FAILED ({fails})");
                    try { File.WriteAllLines(outPath + ".first", lines); } catch { }
                    p.RequestRestart_U27 = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                fails++;
                lines.Add("FAIL exception: " + ex);
            }
            lines.Add(fails == 0 ? "SHELLTEST PASSED" : $"SHELLTEST FAILED ({fails})");
            try { File.WriteAllLines(outPath, lines); } catch { }
            form.Close();
        }
    }
}
