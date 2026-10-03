using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RageLightEditor.Editor;
using Space = RageLightEditor.Editor.LightPanel.Space;

using Control = Avalonia.Controls.Control;
using Color = Avalonia.Media.Color;
using FontFamily = Avalonia.Media.FontFamily;
using Brushes = Avalonia.Media.Brushes;
using Cursor = Avalonia.Input.Cursor;
using ToolTip = Avalonia.Controls.ToolTip;
using HorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using Orientation = Avalonia.Layout.Orientation;
using Path = Avalonia.Controls.Shapes.Path;


namespace RageLightEditor.Shell
{
    public partial class ShellWindow_U27
    {
        public readonly Window Window;
        private readonly MainForm form;
        private bool closingFromForm;
        private readonly DispatcherTimer timer;

        private readonly List<(Space space, ShellButton_U27 btn)> tabs = new List<(Space, ShellButton_U27)>();
        private readonly List<Action> menuRefresh = new List<Action>();
        private readonly List<(int mode, ShellButton_U27 btn)> tiles = new List<(int, ShellButton_U27)>();
        private ShellButton_U27 bSelect, bMove, bRotate, bScale, bAxes, bSnapDown, bSnapUp, bSnap, bUndo, bRedo,
            bPick, bFrame, bProject, bEditLight, bSave, bOutput;
        private Control viewportArea, worldTools, topArea, toolbarArea, paletteArea, outputArea, statusArea;
        private TextBlock docName, statusLeft, statusRight, outputCount;
        private SelectableTextBlock outputText;
        private ScrollViewer outputScroll;
        private ShellButton_U27 outTabAll, outTabErrors;
        private bool outputErrorsOnly;
        private string statusKey;
        private int logVersion = -1;
        private WindowState preFullscreen;
        private bool fullscreen;

        private static readonly (Space space, string label)[] TabNames =
        {
            (Space.World, "World"), (Space.Light, "Lights"), (Space.Material, "Materials"), (Space.Mlo, "MLO"),
            (Space.Archive, "RPF"), (Space.Particles, "Particles"), (Space.NavMesh, "NavMesh"), (Space.Terrain, "Terrain"),
            (Space.Animation, "Animations"), (Space.Extension, "Extensions"), (Space.Cinematic, "Cinematic"),
        };

        private LightPanel P => form.Panel_U27;

        public ShellWindow_U27(MainForm form)
        {
            this.form = form;
            Window = new Window
            {
                Title = form.Text,
                Width = 1600,
                Height = 1000,
                MinWidth = 900,
                MinHeight = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = ShellTheme_U27.Window,
                FontFamily = ShellTheme_U27.Font,
                FontSize = 13,
                Foreground = ShellTheme_U27.Text,
            };
            try
            {
                using var ms = new MemoryStream();
                AppIcon.Load()?.Save(ms);
                ms.Position = 0;
                if (ms.Length > 0) Window.Icon = new WindowIcon(ms);
            }
            catch { }

            topArea = BuildTop();
            toolbarArea = BuildToolbar();
            paletteArea = BuildPalette();
            outputArea = BuildOutput();
            statusArea = BuildStatus();

            viewportArea = new Avalonia.Controls.Border
            {
                Background = Brushes.Black,
                Child = new ViewportHost_U27(form),
            };
            BuildRight_U27();

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,340"),
                RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,150,Auto"),
            };
            rightColumn = grid.ColumnDefinitions[3];
            outputRow = grid.RowDefinitions[4];
            outputSplitter = new GridSplitter { Height = 4, Background = ShellTheme_U27.Window, ResizeDirection = GridResizeDirection.Rows };
            void Put(Control c, int row, int col, int colSpan = 1)
            {
                Grid.SetRow(c, row); Grid.SetColumn(c, col); Grid.SetColumnSpan(c, colSpan);
                grid.Children.Add(c);
            }
            Put(topArea, 0, 0, 4);
            Put(toolbarArea, 1, 0, 2);
            Put(rightHeader, 1, 3);
            Put(rightSplitter, 1, 2);
            Grid.SetRowSpan(rightSplitter, 2);
            Put(paletteArea, 2, 0);
            Put(viewportArea, 2, 1);
            Put(rightBody, 2, 3);
            Put(outputSplitter, 3, 0, 4);
            Put(outputArea, 4, 0, 4);
            Put(statusArea, 5, 0, 4);
            Window.Content = grid;

            Window.Closing += (s, e) =>
            {
                if (closingFromForm) return;
                e.Cancel = true;
                Dispatcher.UIThread.Post(() => { if (!form.IsDisposed) form.Close(); });
            };
            Window.Opened += (s, e) => form.FocusViewport_U27();
            Window.Activated += (s, e) => form.FocusViewport_U27();
            Window.Deactivated += (s, e) => form.ShellDeactivated_U27();

            form.FullscreenHook_U27 = SetFullscreen;

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (s, e) => Sync();
            timer.Start();
            Sync();
            StartSelfTest_U27();
        }

        public void CloseFromForm()
        {
            closingFromForm = true;
            timer.Stop();
            Window.Close();
        }

        private void Act(Action a)
        {
            if (P == null) return;
            a();
            form.FocusViewport_U27();
        }

        private void SetFullscreen(bool on)
        {
            fullscreen = on;
            topArea.IsVisible = toolbarArea.IsVisible = statusArea.IsVisible = !on;
            if (on) paletteArea.IsVisible = false;
            if (on) { preFullscreen = Window.WindowState; Window.WindowState = WindowState.FullScreen; }
            else Window.WindowState = preFullscreen == WindowState.FullScreen ? WindowState.Normal : preFullscreen;
            ApplyOutput();
            SyncRight();
        }

        private Control BuildTop()
        {
            var menu = new Menu { VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
            menu.Items.Add(BuildProjectMenu());
            menu.Items.Add(BuildFileMenu());
            menu.Items.Add(BuildEditMenu());
            menu.Items.Add(BuildViewMenu());
            menu.Items.Add(BuildFiveMMenu());
            menu.Items.Add(BuildHelpMenu());

            var brand = new TextBlock
            {
                Text = "RAGE Tools",
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 10, 0),
            };

            var tabStrip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Margin = new Thickness(12, 0, 0, 0) };
            foreach (var (space, label) in TabNames)
            {
                var b = new ShellButton_U27(null, label, null, underline: true) { Padding = new Thickness(12, 0), MinHeight = 38 };
                var sp = space;
                b.Click += () => Act(() => P.SwitchWorkspace(sp));
                tabs.Add((space, b));
                tabStrip.Children.Add(b);
            }

            docName = new TextBlock
            {
                Foreground = ShellTheme_U27.Dim,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 14, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(brand, Dock.Left);
            DockPanel.SetDock(menu, Dock.Left);
            DockPanel.SetDock(docName, Dock.Right);
            row.Children.Add(brand);
            row.Children.Add(menu);
            row.Children.Add(docName);
            var tabScroll = new ScrollViewer
            {
                Content = tabStrip,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            };
            row.Children.Add(tabScroll);

            return new Avalonia.Controls.Border
            {
                Background = ShellTheme_U27.Window,
                BorderBrush = ShellTheme_U27.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 40,
                Child = row,
            };
        }

        private ShellButton_U27 Tool(string icon, string tip, Action click, string text = null)
        {
            var b = new ShellButton_U27(icon, text, tip, 18) { Margin = new Thickness(1, 0), MinWidth = 32, Height = 32 };
            b.Click += () => Act(click);
            return b;
        }

        private Control BuildToolbar()
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center };
            bar.Children.Add(Tool(ShellIcons_U27.Open, "Open...", () => P.ShellCommand_U27("open")));
            bar.Children.Add(bSave = Tool(ShellIcons_U27.Save, "Save  (Ctrl+S)", () => P.ShellCommand_U27("save")));
            bar.Children.Add(ShellTheme_U27.Divider(true));
            bar.Children.Add(bUndo = Tool(ShellIcons_U27.Undo, "Undo  (Ctrl+Z)", () => P.ShellCommand_U27("undo")));
            bar.Children.Add(bRedo = Tool(ShellIcons_U27.Redo, "Redo  (Ctrl+Y)", () => P.ShellCommand_U27("redo")));

            var world = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            world.Children.Add(ShellTheme_U27.Divider(true));
            world.Children.Add(bPicks = Tool(ShellIcons_U27.Pick, "Show or hide the pick palette  (M cycles what a click picks)", () => paletteWanted = !paletteWanted, "Picks: Entity"));
            world.Children.Add(ShellTheme_U27.Divider(true));
            world.Children.Add(bSelect = Tool(ShellIcons_U27.Select, "Select  (Q)", () => SetGizmo(WorldGizmoMode.Select)));
            world.Children.Add(bMove = Tool(ShellIcons_U27.Move, "Move  (W)", () => SetGizmo(WorldGizmoMode.Translate)));
            world.Children.Add(bRotate = Tool(ShellIcons_U27.Rotate, "Rotate  (E)", () => SetGizmo(WorldGizmoMode.Rotate)));
            world.Children.Add(bScale = Tool(ShellIcons_U27.Scale, "Scale", () => SetGizmo(WorldGizmoMode.Scale)));
            world.Children.Add(ShellTheme_U27.Divider(true));
            world.Children.Add(bAxes = Tool(ShellIcons_U27.World, "Which way the handles point: World or Local  (G)", () =>
            {
                var g = P.WorldGizmo; if (g == null) return;
                g.Space = g.Space == WorldGizmoSpace.Local ? WorldGizmoSpace.World : WorldGizmoSpace.Local;
            }, "World"));
            world.Children.Add(bSnapDown = Tool(ShellIcons_U27.Minus, "Smaller rotate steps", () =>
            {
                var g = P.WorldGizmo; if (g != null) g.RotateSnapDeg = RotateSnapSteps_U5.Down(g.RotateSnapDeg);
            }));
            world.Children.Add(bSnap = new ShellButton_U27(ShellIcons_U27.Magnet, "Snap: off", "Turning snaps to steps this big.\nHold Shift while dragging a ring to turn freely.") { Height = 32, Cursor = Cursor.Default });
            world.Children.Add(bSnapUp = Tool(ShellIcons_U27.Plus, "Bigger rotate steps", () =>
            {
                var g = P.WorldGizmo; if (g != null) g.RotateSnapDeg = RotateSnapSteps_U5.Up(g.RotateSnapDeg);
            }));
            world.Children.Add(ShellTheme_U27.Divider(true));
            world.Children.Add(bPick = Tool(ShellIcons_U27.Pick, "Picking: clicking in the world changes the selection  (C)", () => P.MouseSelectEnabled = !P.MouseSelectEnabled, "Picking"));
            world.Children.Add(bFrame = Tool(ShellIcons_U27.Frame, "Fly the camera to what is selected  (F)", () => P.ShellCommand_U27("frame"), "Frame"));
            world.Children.Add(bEditLight = Tool(ShellIcons_U27.Bulb, "Edit the props' lights where they stand", () => P.SetEditLight(!P.EditLightActive), "Edit Light"));
            world.Children.Add(ShellTheme_U27.Divider(true));
            world.Children.Add(bProject = Tool(ShellIcons_U27.Project, "Show or hide the map project", () =>
            {
                var pw = P.ProjectWindow; if (pw != null) pw.Visible = !pw.Visible;
            }, "Project"));
            worldTools = world;
            bar.Children.Add(world);

            bPanel = Tool(ShellIcons_U27.PanelRight, "Show or hide the right panel  (F10)", () => P.ShowRightPanel = !P.ShowRightPanel);
            bPanel.Margin = new Thickness(0, 0, 10, 0);
            bPanel.VerticalAlignment = VerticalAlignment.Center;
            var row = new DockPanel();
            DockPanel.SetDock(bPanel, Dock.Right);
            row.Children.Add(bPanel);
            row.Children.Add(bar);
            return new Avalonia.Controls.Border
            {
                Background = ShellTheme_U27.Panel,
                BorderBrush = ShellTheme_U27.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 58,
                Child = row,
            };
        }

        private void SetGizmo(WorldGizmoMode m)
        {
            var g = P.WorldGizmo;
            if (g != null) g.Mode = m;
        }

        private Control BuildPalette()
        {
            var grid = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(6) };
            for (int i = 0; i < LightPanel.SelectionModeNames.Length; i++)
            {
                if (!LightPanel.SelectionModeAvailable[i]) continue;
                var b = new ShellButton_U27(ShellIcons_U27.Modes[i], ShellIcons_U27.ModeLabels[i],
                    "Picks: " + LightPanel.SelectionModeNames[i], 22, vertical: true)
                { Margin = new Thickness(2), Height = 60 };
                int mode = i;
                b.Click += () => Act(() => P.SelectionMode = mode);
                tiles.Add((i, b));
                grid.Children.Add(b);
            }
            var head = new TextBlock
            {
                Text = "PICK",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = ShellTheme_U27.Faint,
                Margin = new Thickness(12, 10, 0, 0),
            };
            var close = new ShellButton_U27(ShellIcons_U27.Close, null, "Close the pick palette", 14) { Height = 24, Margin = new Thickness(0, 6, 6, 0) };
            close.Click += () => { paletteWanted = false; Sync(); form.FocusViewport_U27(); };
            var headRow = new DockPanel();
            DockPanel.SetDock(close, Dock.Right);
            headRow.Children.Add(close);
            headRow.Children.Add(head);
            var stack = new StackPanel();
            stack.Children.Add(headRow);
            stack.Children.Add(grid);
            return new Avalonia.Controls.Border
            {
                Background = ShellTheme_U27.Panel,
                BorderBrush = ShellTheme_U27.Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Width = 156,
                Child = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
            };
        }

        private bool outputWanted = true;
        private bool paletteWanted;
        private RowDefinition outputRow;
        private GridSplitter outputSplitter;
        private GridLength outputHeight = new GridLength(150);
        private ShellButton_U27 bPicks, bPanel;

        private void SetOutput(bool on)
        {
            outputWanted = on;
            ApplyOutput();
            form.FocusViewport_U27();
        }

        private void ApplyOutput()
        {
            bool show = outputWanted && !fullscreen;
            if (!show && outputRow.Height.Value > 0) outputHeight = outputRow.Height;
            outputArea.IsVisible = show;
            outputSplitter.IsVisible = show;
            outputRow.Height = show ? outputHeight : new GridLength(0);
        }

        private Control BuildOutput()
        {
            outTabAll = new ShellButton_U27(null, "Output", null, underline: true) { Padding = new Thickness(12, 0), Height = 30 };
            outTabErrors = new ShellButton_U27(null, "Problems", null, underline: true) { Padding = new Thickness(12, 0), Height = 30 };
            outTabAll.Click += () => { outputErrorsOnly = false; logVersion = -1; Sync(); };
            outTabErrors.Click += () => { outputErrorsOnly = true; logVersion = -1; Sync(); AppLog_U21.MarkSeen(); };
            outputCount = new TextBlock { Foreground = ShellTheme_U27.Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
            var clear = new ShellButton_U27(null, "Clear", "Clear the log") { Height = 26 };
            clear.Click += () => { AppLog_U21.Clear(); logVersion = -1; Sync(); };
            var folder = new ShellButton_U27(ShellIcons_U27.Open, null, "Open the log folder", 15) { Height = 26 };
            folder.Click += () => Act(() => P.ShellCommand_U27("logfolder"));
            var hide = new ShellButton_U27(ShellIcons_U27.Minus, null, "Hide the output panel", 15) { Height = 26 };
            hide.Click += () => SetOutput(false);

            var head = new DockPanel { LastChildFill = false, Height = 30 };
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(outTabAll);
            left.Children.Add(outTabErrors);
            left.Children.Add(outputCount);
            var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 0, 6, 0) };
            right.Children.Add(clear);
            right.Children.Add(folder);
            right.Children.Add(hide);
            DockPanel.SetDock(left, Dock.Left);
            DockPanel.SetDock(right, Dock.Right);
            head.Children.Add(left);
            head.Children.Add(right);

            outputText = new SelectableTextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, $Default"),
                FontSize = 12,
                Margin = new Thickness(12, 6),
                TextWrapping = TextWrapping.NoWrap,
            };
            outputScroll = new ScrollViewer { Content = outputText, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };

            var dp = new DockPanel();
            var headBorder = new Avalonia.Controls.Border { Child = head, BorderBrush = ShellTheme_U27.Border, BorderThickness = new Thickness(0, 0, 0, 1) };
            DockPanel.SetDock(headBorder, Dock.Top);
            dp.Children.Add(headBorder);
            dp.Children.Add(outputScroll);
            return new Avalonia.Controls.Border
            {
                Background = ShellTheme_U27.PanelDeep,
                BorderBrush = ShellTheme_U27.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                MinHeight = 60,
                Child = dp,
            };
        }

        private Control BuildStatus()
        {
            statusLeft = new TextBlock { Foreground = ShellTheme_U27.Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0), FontSize = 12 };
            statusRight = new TextBlock { Foreground = ShellTheme_U27.Dim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0), FontSize = 12 };
            bOutput = new ShellButton_U27(ShellIcons_U27.Output, "Output", "Show or hide the output panel", 14) { Height = 24, Padding = new Thickness(8, 0) };
            bOutput.Click += () => SetOutput(!outputWanted);
            var dp = new DockPanel { LastChildFill = false };
            DockPanel.SetDock(statusLeft, Dock.Left);
            DockPanel.SetDock(bOutput, Dock.Right);
            DockPanel.SetDock(statusRight, Dock.Right);
            dp.Children.Add(statusLeft);
            dp.Children.Add(bOutput);
            dp.Children.Add(statusRight);
            return new Avalonia.Controls.Border
            {
                Background = ShellTheme_U27.Window,
                BorderBrush = ShellTheme_U27.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Height = 26,
                Child = dp,
            };
        }

        private MenuItem Item(string header, Action act, Func<bool> enabled = null, Func<bool> check = null,
            string gesture = null, Func<string> dynHeader = null, bool radio = false)
        {
            var mi = new MenuItem { Header = header };
            if (gesture != null) mi.InputGesture = KeyGesture.Parse(gesture);
            if (check != null) mi.ToggleType = radio ? MenuItemToggleType.Radio : MenuItemToggleType.CheckBox;
            mi.Click += (s, e) => Act(act);
            menuRefresh.Add(() =>
            {
                if (P == null) return;
                if (enabled != null) mi.IsEnabled = enabled();
                if (check != null) mi.IsChecked = check();
                if (dynHeader != null) mi.Header = dynHeader();
            });
            return mi;
        }

        private MenuItem Top(string header, params object[] items)
        {
            var mi = new MenuItem { Header = header };
            foreach (var it in items) mi.Items.Add(it);
            mi.SubmenuOpened += (s, e) => { foreach (var r in menuRefresh) r(); };
            return mi;
        }

        private MenuItem BuildProjectMenu()
        {
            ProjectWindow PW() => P?.ProjectWindow;
            void Pw(Action<ProjectWindow> a) { var pw = PW(); if (pw != null) a(pw); }
            var session = new MenuItem { Header = "Lighting session (.rlep)" };
            session.Items.Add(Item("New session", () => P.ShellCommand_U27("session.new")));
            session.Items.Add(Item("Open session...", () => P.ShellCommand_U27("session.open")));
            session.Items.Add(Item("Save session", () => P.ShellCommand_U27("session.save")));
            session.Items.Add(Item("Save session as...", () => P.ShellCommand_U27("session.saveas")));
            return Top("Project",
                Item("Project Window", () => Pw(pw => pw.Visible = !pw.Visible), () => PW() != null, () => PW()?.Visible == true, "Ctrl+Shift+P"),
                Item("Project in its own window", () => P.SetProjectInOwnWindow_U28(!P.ProjectInOwnWindow_U28), () => PW() != null, () => P.ProjectInOwnWindow_U28),
                Item("New Project", () => Pw(pw => { pw.RequestNewProject = true; pw.Visible = true; }), () => PW() != null),
                Item("Open Project...", () => Pw(pw => { pw.RequestOpenProject = true; pw.Visible = true; }), () => PW() != null),
                Item("Save All", () => Pw(pw => pw.RequestSaveAll = true), () => PW()?.Project != null),
                Item("Close Project", () => Pw(pw => pw.RequestCloseProject = true), () => PW()?.Project != null),
                new Separator(),
                Item("New Ymap", () => Pw(pw => { pw.RequestNewYmap = true; pw.Visible = true; }), () => PW() != null),
                Item("New Ytyp", () => Pw(pw => { pw.RequestNewYtyp = true; pw.Visible = true; }), () => PW() != null),
                Item("Open Files...", () => Pw(pw => { pw.RequestOpenAny = true; pw.Visible = true; }), () => PW() != null),
                new Separator(),
                session);
        }

        private MenuItem BuildFileMenu() => Top("File",
            Item("Open...", () => P.ShellCommand_U27("open")),
            Item("Add prop...", () => P.ShellCommand_U27("addprop")),
            Item("New light prop...", () => P.ShellCommand_U27("newlightprop")),
            Item("Load YTD...", () => P.ShellCommand_U27("loadytd")),
            new Separator(),
            Item("Save", () => P.ShellCommand_U27("save"), () => P.ShellCanSave_U27, null, "Ctrl+S",
                () => P.ShellMultiSave_U27 ? "Save all" : "Save"),
            Item("Save As...", () => P.ShellCommand_U27("saveas"), () => P.ShellCanSave_U27),
            new Separator(),
            Item("Open files with this editor (register file types)", () => P.ShellCommand_U27("registertypes")),
            Item("Remove those file associations", () => P.ShellCommand_U27("unregistertypes")));

        private MenuItem BuildEditMenu() => Top("Edit",
            Item("Undo", () => P.ShellCommand_U27("undo"), () => P.ShellCanUndo_U27, null, "Ctrl+Z"),
            Item("Redo", () => P.ShellCommand_U27("redo"), () => P.ShellCanRedo_U27, null, "Ctrl+Y"),
            new Separator(),
            Item("Undo material", () => P.ShellCommand_U27("matundo"), () => P.MaterialMode && P.Materials.CanUndo, null, "Ctrl+Shift+Z"),
            Item("Redo material", () => P.ShellCommand_U27("matredo"), () => P.MaterialMode && P.Materials.CanRedo, null, "Ctrl+Shift+Y"));

        private MenuItem BuildViewMenu() => Top("View",
            Item("Appearance...", () => P.ShellPopup_U27 = "appearance"),
            Item("Shortcuts...", () => P.ShellPopup_U27 = "shortcuts"),
            new Separator(),
            Item("Pick palette", () => paletteWanted = !paletteWanted, null, () => paletteWanted),
            Item("Right panel", () => P.ShowRightPanel = !P.ShowRightPanel, null, () => P.ShowRightPanel, "F10"),
            Item("Output panel", () => SetOutput(!outputWanted), null, () => outputWanted),
            new Separator(),
            Item("Interface: Classic", () => P.NewUi_U27 = false, null, () => !P.NewUi_U27, radio: true),
            Item("Interface: New", () => P.NewUi_U27 = true, null, () => P.NewUi_U27, radio: true),
            Item("Restart now", () => P.RequestRestart_U27 = true, () => P.InterfaceChangePending_U27));

        private MenuItem BuildFiveMMenu() => Top("FiveM",
            Item("FiveM live link", () => P.FiveMWindowOpen_U12 = !P.FiveMWindowOpen_U12, null, () => P.FiveMWindowOpen_U12,
                dynHeader: () => P.FiveMConnected_U12 ? "FiveM live link  (game connected)" : "FiveM live link"),
            Item("Game view (bottom left)", () => P.GameViewOpen_U13 = !P.GameViewOpen_U13, null, () => P.GameViewOpen_U13),
            Item("Detach the panel into its own window", () =>
            {
                if (P.FiveMDetached_U14) P.RequestFiveMAttach_U14 = true; else P.RequestFiveMDetach_U14 = true;
            }, dynHeader: () => P.FiveMDetached_U14 ? "Attach the panel to the main window" : "Detach the panel into its own window"),
            new Separator(),
            Item("Start the local API for scripts", () => { if (P.MloCreator != null) P.MloCreator.RequestBridgeToggle = true; },
                () => P.MloCreator != null,
                dynHeader: () => P.MloCreator != null && P.MloCreator.BridgeEnabled ? $"Stop the local API  (port {P.MloCreator.BridgePort})" : "Start the local API for scripts"));

        private MenuItem BuildHelpMenu() => Top("Help",
            Item("Tutorial", () => P.ShellCommand_U27("tutorial")),
            Item("Mirror surprise", () => P.ShellCommand_U27("mirrorjoke"), null, () => P.MirrorSurprise_U27),
            new Separator(),
            Item("Show log", () => P.ShellCommand_U27("log"), null, null, "Ctrl+L"),
            Item("Open log folder", () => P.ShellCommand_U27("logfolder")));

        private void Sync()
        {
            if (form.IsDisposed) return;
            if (Window.Title != form.Text) Window.Title = form.Text;
            var p = P;
            if (p == null) return;

            foreach (var (space, btn) in tabs) btn.IsOn = p.Workspace == space;

            bool world = p.WorldMode && !p.NavMode;
            worldTools.IsVisible = world;
            paletteArea.IsVisible = world && paletteWanted && !fullscreen;
            bPicks.IsOn = paletteWanted;
            int sm = Math.Clamp(p.SelectionMode, 0, ShellIcons_U27.ModeLabels.Length - 1);
            bPicks.Text = "Picks: " + ShellIcons_U27.ModeLabels[sm];
            bPanel.IsOn = p.ShowRightPanel;
            bPanel.IsVisible = p.ShellOwnsRight_U27;
            bUndo.Enabled = p.ShellCanUndo_U27;
            bRedo.Enabled = p.ShellCanRedo_U27;
            bSave.Enabled = p.ShellCanSave_U27;
            var g = p.WorldGizmo;
            if (world && g != null)
            {
                bSelect.IsOn = g.Mode == WorldGizmoMode.Select;
                bMove.IsOn = g.Mode == WorldGizmoMode.Translate;
                bRotate.IsOn = g.Mode == WorldGizmoMode.Rotate;
                bScale.IsOn = g.Mode == WorldGizmoMode.Scale;
                bool local = g.Space == WorldGizmoSpace.Local;
                bAxes.Text = local ? "Local" : "World";
                bSnap.Text = RotateSnapSteps_U5.Label(RotateSnapSteps_U5.Clamp(g.RotateSnapDeg));
                bSnapDown.Enabled = g.RotateSnapDeg > 0.001f;
                bSnapUp.Enabled = g.RotateSnapDeg < 89.999f;
            }
            bPick.IsOn = p.MouseSelectEnabled;
            bFrame.Enabled = p.ShellHasSelection_U27;
            bEditLight.IsOn = p.EditLightActive;
            var pw = p.ProjectWindow;
            bProject.IsVisible = pw != null;
            if (pw != null)
            {
                bProject.IsOn = pw.Visible;
                bProject.Text = pw.Project?.AnyUnsaved == true ? "Project *" : "Project";
            }
            foreach (var (mode, btn) in tiles) btn.IsOn = p.SelectionMode == mode;

            string doc = p.ShellDocName_U27;
            if (docName.Text != doc) docName.Text = doc;
            var segs = new List<(string text, bool warn)> { (TabLabel(p.Workspace), false) };
            if (world) segs.Add(("Picks: " + p.SelectionModeName, false));
            if (p.WorldMode) segs.AddRange(p.ShellWorldStatus_U27());
            string key = string.Join("|", segs);
            if (key != statusKey)
            {
                statusKey = key;
                var inl = new InlineCollection();
                for (int i = 0; i < segs.Count; i++)
                {
                    if (i > 0) inl.Add(new Run("   ·   ") { Foreground = ShellTheme_U27.Faint });
                    inl.Add(new Run(segs[i].text) { Foreground = segs[i].warn ? ShellTheme_U27.Warn : ShellTheme_U27.Dim });
                }
                statusLeft.Inlines = inl;
            }
            string right = p.StatsText ?? "";
            if (statusRight.Text != right) statusRight.Text = right;

            SyncRight();
            SyncLog();
        }

        private static string TabLabel(Space s)
        {
            foreach (var (space, label) in TabNames) if (space == s) return label;
            return s.ToString();
        }

        private void SyncLog()
        {
            int errs = AppLog_U21.ErrorsUnseen;
            outTabAll.IsOn = !outputErrorsOnly;
            outTabErrors.IsOn = outputErrorsOnly;
            outTabErrors.Text = errs > 0 ? $"Problems ({errs})" : "Problems";
            outTabErrors.OnColour = errs > 0 ? ShellTheme_U27.Error : null;
            bOutput.OnColour = ShellTheme_U27.Error;
            bOutput.IsOn = errs > 0;
            if (AppLog_U21.Version == logVersion || !outputArea.IsVisible) return;
            logVersion = AppLog_U21.Version;
            var entries = AppLog_U21.Snapshot();
            var inl = new InlineCollection();
            int shown = 0, start = Math.Max(0, entries.Count - 400);
            for (int i = start; i < entries.Count; i++)
            {
                var e = entries[i];
                if (outputErrorsOnly && e.Level == AppLog_U21.Level.Info) continue;
                if (shown > 0) inl.Add(new LineBreak());
                inl.Add(new Run(e.Time.ToString("HH:mm:ss") + "  ") { Foreground = ShellTheme_U27.Faint });
                inl.Add(new Run(e.Text.Replace("\r", "").Replace("\n", "  ")) { Foreground = e.Level == AppLog_U21.Level.Error ? ShellTheme_U27.Error : e.Level == AppLog_U21.Level.Warning ? ShellTheme_U27.Warn : ShellTheme_U27.Text });
                shown++;
            }
            outputText.Inlines = inl;
            outputCount.Text = shown == 0 ? "nothing yet" : shown + (shown == 1 ? " line" : " lines");
            Dispatcher.UIThread.Post(() => outputScroll.ScrollToEnd(), DispatcherPriority.Background);
        }
    }
}
