using System;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool startupSpaceArmed_S4;

        public static LightPanel.Space StartupWorkspace_S4(AppSettings s)
        {
            int last = s?.LastWorkspace ?? -1;
            if (last >= 0 && Enum.IsDefined(typeof(LightPanel.Space), last)) return (LightPanel.Space)last;
            return LightPanel.Space.World;
        }

        private bool startupSpaceScripted_S4 =>
            Environment.GetEnvironmentVariable("RLE_STARTSPACE") != "1" &&
            (IsHeadless || DebugSeqTest || DebugWorldTest || DebugArchiveTest || DebugVideoTest ||
             DebugHourSweep > 0 || DebugModeSweep > 0);

        partial void ApplyStartupWorkspace_S4()
        {
            if (panel == null) return;
            panel.WorkspaceSwitching += (leaving, entering) =>
            {
                if (!startupSpaceArmed_S4 || settings == null) return;
                if (settings.LastWorkspace == (int)entering) return;
                settings.LastWorkspace = (int)entering;
                settings.Save();
            };

            bool named = Mode == AppMode.Material || DebugMlo != null || pendingLoadFiles.Count > 0;
            if (!named && !startupSpaceScripted_S4)
            {
                var space = StartupWorkspace_S4(settings);
                if (panel.Workspace != space) panel.SwitchWorkspace(space);
            }
            startupSpaceArmed_S4 = !startupSpaceScripted_S4;
            Console.WriteLine($"STARTSPACE {SpaceNames.NameOf(panel.Workspace)} (remembered {(settings == null || settings.LastWorkspace < 0 ? "nothing" : SpaceNames.NameOf((LightPanel.Space)settings.LastWorkspace))}" +
                              (named ? ", a flag named the section" : "") + (startupSpaceScripted_S4 ? ", scripted run" : "") + ")");
        }

        private void StartupWorkspaceTest_S4(Action<string, bool, string> check)
        {
            var fresh = new AppSettings();
            check("a fresh install opens in the World",
                  StartupWorkspace_S4(fresh) == LightPanel.Space.World, SpaceNames.NameOf(StartupWorkspace_S4(fresh)));
            check("...and so does a settings file written before this build (no key)",
                  fresh.LastWorkspace == -1 && StartupWorkspace_S4(fresh) == LightPanel.Space.World, fresh.LastWorkspace.ToString());

            var used = new AppSettings { LastWorkspace = (int)LightPanel.Space.Archive };
            check("a settings file left in the RPF explorer opens in the RPF explorer again",
                  StartupWorkspace_S4(used) == LightPanel.Space.Archive, SpaceNames.NameOf(StartupWorkspace_S4(used)));
            used.LastWorkspace = (int)LightPanel.Space.Material;
            check("...and one left in Materials opens in Materials",
                  StartupWorkspace_S4(used) == LightPanel.Space.Material, SpaceNames.NameOf(StartupWorkspace_S4(used)));

            check("a settings file from a newer build lands somewhere real",
                  StartupWorkspace_S4(new AppSettings { LastWorkspace = 99 }) == LightPanel.Space.World, "");
            check("null settings (nothing loaded at all) still opens in the World",
                  StartupWorkspace_S4(null) == LightPanel.Space.World, "");

            var live = panel.Workspace;
            bool armed = startupSpaceArmed_S4;
            int wasLast = settings?.LastWorkspace ?? -1;
            startupSpaceArmed_S4 = true;
            var probe = live == LightPanel.Space.World ? LightPanel.Space.Light : LightPanel.Space.World;
            panel.SwitchWorkspace(probe);
            check("switching section writes it down for the next launch",
                  settings != null && settings.LastWorkspace == (int)probe,
                  settings == null ? "no settings" : SpaceNames.NameOf((LightPanel.Space)settings.LastWorkspace));
            panel.SwitchWorkspace(live);
            startupSpaceArmed_S4 = armed;
            if (settings != null && settings.LastWorkspace != wasLast) { settings.LastWorkspace = wasLast; settings.Save(); }
        }
    }
}

