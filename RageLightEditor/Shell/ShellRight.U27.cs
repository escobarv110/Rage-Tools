using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using Control = Avalonia.Controls.Control;
using Color = Avalonia.Media.Color;
using Brushes = Avalonia.Media.Brushes;
using Cursor = Avalonia.Input.Cursor;
using ToolTip = Avalonia.Controls.ToolTip;
using HorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using Orientation = Avalonia.Layout.Orientation;
using TreeView = Avalonia.Controls.TreeView;
using TextBox = Avalonia.Controls.TextBox;
using Panel = Avalonia.Controls.Panel;

namespace RageLightEditor.Shell
{
    public partial class ShellWindow_U27
    {
        private static readonly (string icon, string label, string tip)[] RightPages =
        {
            (ShellIcons_U27.Info, "Inspector", "What is selected, and the map around you"),
            (ShellIcons_U27.Project, "Project", "The map files you change"),
            (ShellIcons_U27.Assets, "Assets", "Props to place"),
            (ShellIcons_U27.Area, "Area", "Clear a region of the map"),
            (ShellIcons_U27.Sliders, "Options", "How the world is drawn"),
        };

        private ColumnDefinition rightColumn;
        private Control rightHeader, rightBody, rightSplitter;
        private readonly List<ShellButton_U27> pageTabs = new List<ShellButton_U27>();
        private int page;
        private bool rightImGui;

        private ShellButton_U27 subMap, subLights, propTab, detailTab;
        private Control mapPane, lightsPane, propsPane, detailsPane;
        private TextBox filterBox;
        private TreeView tree;
        private readonly Dictionary<uint, TreeViewItem> ymapItems = new Dictionary<uint, TreeViewItem>();
        private string treeKey = "";
        private bool syncingTree;
        private int treeTick;
        private YmapEntityDef lastSel;
        private StackPanel lightsList;
        private Control selBar, noSelNote, multiNote;
        private TextBlock selName, selWhere;
        private Control selIcon, selIconMlo;
        private readonly List<(TextBox box, Func<YmapEntityDef, float> get, Action<LightPanel.EntityEdit, float> set)> fields =
            new List<(TextBox, Func<YmapEntityDef, float>, Action<LightPanel.EntityEdit, float>)>();
        private StackPanel detailRows;
        private TextBlock dirtyText, folderText, editStatus;
        private ShellButton_U27 bSaveYmaps, bDiscard;

        private static readonly IBrush FieldBg = new SolidColorBrush(Color.Parse("#0A0B0D"));

        private void BuildRight_U27()
        {
            var tabsGrid = new UniformGrid { Columns = RightPages.Length, Rows = 1 };
            for (int i = 0; i < RightPages.Length; i++)
            {
                var (icon, label, tip) = RightPages[i];
                var b = new ShellButton_U27(icon, label, tip, 22, vertical: true) { CornerRadius = new CornerRadius(0), Margin = new Thickness(0) };
                int idx = i;
                b.Click += () => SelectPage(idx);
                pageTabs.Add(b);
                tabsGrid.Children.Add(b);
            }
            rightHeader = new Avalonia.Controls.Border
            {
                Background = ShellTheme_U27.PanelDeep,
                BorderBrush = ShellTheme_U27.Border,
                BorderThickness = new Thickness(1, 0, 0, 1),
                Height = 58,
                Child = tabsGrid,
            };

            rightSplitter = new GridSplitter
            {
                Width = 4,
                Background = ShellTheme_U27.Window,
                ResizeDirection = GridResizeDirection.Columns,
            };

            subMap = SubTab("Map");
            subLights = SubTab("Lights");
            subMap.Click += () => ShowSub(false);
            subLights.Click += () => ShowSub(true);
            var subGrid = new UniformGrid { Columns = 2, Rows = 1, Height = 32 };
            subGrid.Children.Add(subMap);
            subGrid.Children.Add(subLights);

            filterBox = Field();
            filterBox.PlaceholderText = "Filter by archetype";
            filterBox.Margin = new Thickness(8, 6, 8, 4);
            filterBox.TextChanged += (s, e) => { foreach (var it in ymapItems.Values) if (it.IsExpanded) FillYmap(it); };

            tree = new TreeView { Margin = new Thickness(2, 0, 2, 2), FontSize = 12 };
            tree.SelectionChanged += (s, e) =>
            {
                if (syncingTree || P == null) return;
                if (tree.SelectedItem is TreeViewItem ti && ti.Tag is YmapEntityDef en) P.RequestWorldSelectEntity = en;
            };
            tree.DoubleTapped += (s, e) =>
            {
                if (P == null) return;
                if (tree.SelectedItem is TreeViewItem ti && ti.Tag is YmapEntityDef en)
                {
                    P.RequestWorldSelectEntity = en;
                    P.RequestWorldFrameSelected = true;
                    form.FocusViewport_U27();
                }
            };
            var mapDock = new DockPanel();
            DockPanel.SetDock(filterBox, Dock.Top);
            mapDock.Children.Add(filterBox);
            mapDock.Children.Add(tree);
            mapPane = mapDock;

            lightsList = new StackPanel { Margin = new Thickness(8, 6) };
            lightsPane = new ScrollViewer { Content = lightsList, IsVisible = false };

            var upper = new Grid();
            upper.Children.Add(mapPane);
            upper.Children.Add(lightsPane);

            BuildSelBar();

            propTab = SubTab("Properties");
            detailTab = SubTab("Details");
            propTab.Click += () => ShowDetails(false);
            detailTab.Click += () => ShowDetails(true);
            var propTabs = new UniformGrid { Columns = 2, Rows = 1, Height = 32 };
            propTabs.Children.Add(propTab);
            propTabs.Children.Add(detailTab);

            propsPane = BuildProps();
            detailRows = new StackPanel { Margin = new Thickness(10, 6) };
            detailsPane = new ScrollViewer { Content = detailRows, IsVisible = false };
            noSelNote = new TextBlock
            {
                Text = "Right-click anything in the world to select it.",
                Foreground = ShellTheme_U27.Dim,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 12),
            };
            var lower = new Grid();
            lower.Children.Add(propsPane);
            lower.Children.Add(detailsPane);
            lower.Children.Add(noSelNote);

            var footer = BuildFooter();

            var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto,1.3*,Auto") };
            void Row(Control c, int r) { Grid.SetRow(c, r); body.Children.Add(c); }
            Row(Underlined(subGrid), 0);
            Row(upper, 1);
            var split = new GridSplitter { Height = 4, Background = ShellTheme_U27.Window, ResizeDirection = GridResizeDirection.Rows };
            Row(split, 2);
            Row(selBar, 3);
            Row(Underlined(propTabs), 4);
            Row(lower, 5);
            Row(footer, 6);

            rightBody = new Avalonia.Controls.Border
            {
                Background = ShellTheme_U27.Panel,
                BorderBrush = ShellTheme_U27.Border,
                BorderThickness = new Thickness(1, 0, 0, 0),
                Child = body,
            };
            ShowSub(false);
            ShowDetails(false);
            SelectPage(0);
        }

        private static Control Underlined(Control c) => new Avalonia.Controls.Border
        {
            Child = c,
            BorderBrush = ShellTheme_U27.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        private static ShellButton_U27 SubTab(string text) =>
            new ShellButton_U27(null, text, null, underline: true) { Padding = new Thickness(8, 0) };

        private static TextBox Field() => new TextBox
        {
            FontSize = 12,
            MinHeight = 24,
            Height = 26,
            Padding = new Thickness(6, 3),
            Background = FieldBg,
            BorderBrush = ShellTheme_U27.Border,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        private void ShowSub(bool lights)
        {
            subMap.IsOn = !lights;
            subLights.IsOn = lights;
            mapPane.IsVisible = !lights;
            lightsPane.IsVisible = lights;
            lastSel = null;
        }

        private void ShowDetails(bool details)
        {
            propTab.IsOn = !details;
            detailTab.IsOn = details;
            propsPane.IsVisible = !details;
            detailsPane.IsVisible = details;
        }

        private void SelectPage(int idx)
        {
            page = idx;
            for (int i = 0; i < pageTabs.Count; i++) pageTabs[i].IsOn = i == idx;
            var pw = P?.ProjectWindow;
            if (pw != null && pw.ShowsDocked) pw.Visible = idx == LightPanel.ShellPageProject_U27;
            SyncRight();
            form.FocusViewport_U27();
        }

        private void BuildSelBar()
        {
            var tintA = new List<Avalonia.Controls.Shapes.Shape>();
            var tintB = new List<Avalonia.Controls.Shapes.Shape>();
            selIcon = ShellTheme_U27.Icon(ShellIcons_U27.Local, 20, tintA);
            selIconMlo = ShellTheme_U27.Icon(ShellIcons_U27.House, 20, tintB);
            foreach (var s in tintA) s.Stroke = ShellTheme_U27.Accent;
            foreach (var s in tintB) s.Stroke = ShellTheme_U27.Accent;
            selName = new TextBlock { FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis };
            selWhere = new TextBlock { FontSize = 11, Foreground = ShellTheme_U27.Dim, TextTrimming = TextTrimming.CharacterEllipsis };
            var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
            names.Children.Add(selName);
            names.Children.Add(selWhere);
            var frame = new ShellButton_U27(ShellIcons_U27.Frame, null, "Fly the camera to it  (F)", 16) { Height = 28 };
            frame.Click += () => Act(() => P.ShellCommand_U27("frame"));
            var locate = new ShellButton_U27(ShellIcons_U27.Pick, null, "Show where it is", 16) { Height = 28 };
            locate.Click += () => Act(() => P.RequestLocateSelected_V19 = true);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btns.Children.Add(frame);
            btns.Children.Add(locate);
            var icons = new Grid { VerticalAlignment = VerticalAlignment.Center };
            icons.Children.Add(selIcon);
            icons.Children.Add(selIconMlo);
            var dp = new DockPanel { Margin = new Thickness(10, 6) };
            DockPanel.SetDock(icons, Dock.Left);
            DockPanel.SetDock(btns, Dock.Right);
            dp.Children.Add(icons);
            dp.Children.Add(btns);
            dp.Children.Add(names);
            multiNote = new TextBlock { Foreground = ShellTheme_U27.Warn, FontSize = 11, Margin = new Thickness(10, 0, 10, 6), TextWrapping = TextWrapping.Wrap };
            var st = new StackPanel();
            st.Children.Add(dp);
            st.Children.Add(multiNote);
            selBar = new Avalonia.Controls.Border
            {
                Background = ShellTheme_U27.AccentSoft,
                Child = st,
            };
        }

        private Control BuildProps()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 4) };
            void Group(string name)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = name,
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = ShellTheme_U27.Faint,
                    Margin = new Thickness(10, 8, 0, 2),
                });
            }
            void Prop(string label, string tip, float step, Func<YmapEntityDef, float> get, Action<LightPanel.EntityEdit, float> set)
            {
                var box = Field();
                var lab = new TextBlock
                {
                    Text = label,
                    Foreground = ShellTheme_U27.Dim,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.SizeWestEast),
                    Background = Brushes.Transparent,
                };
                ToolTip.SetTip(lab, tip + "\nDrag left or right to change it.");
                double startX = 0; float startV = 0; bool drag = false;
                lab.PointerPressed += (s, e) =>
                {
                    var sel = P?.WorldSel;
                    if (sel == null || !e.GetCurrentPoint(lab).Properties.IsLeftButtonPressed) return;
                    drag = true; startX = e.GetPosition(lab).X; startV = get(sel);
                    e.Pointer.Capture(lab);
                    e.Handled = true;
                };
                lab.PointerMoved += (s, e) =>
                {
                    if (!drag) return;
                    float v = startV + (float)(e.GetPosition(lab).X - startX) * step;
                    Apply(set, v);
                    box.Text = v.ToString("0.###", CultureInfo.InvariantCulture);
                };
                lab.PointerReleased += (s, e) => { drag = false; e.Pointer.Capture(null); };
                void Commit()
                {
                    if (float.TryParse((box.Text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                        Apply(set, v);
                }
                box.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter) { Commit(); form.FocusViewport_U27(); e.Handled = true; }
                    else if (e.Key == Key.Escape) { form.FocusViewport_U27(); e.Handled = true; }
                };
                box.LostFocus += (s, e) => Commit();
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("104,*"), Margin = new Thickness(10, 2, 10, 2) };
                Grid.SetColumn(box, 1);
                row.Children.Add(lab);
                row.Children.Add(box);
                stack.Children.Add(row);
                fields.Add((box, get, set));
            }
            Group("POSITION");
            Prop("X", "Position X, world metres", 0.05f, e => e.Position.X, (d, v) => d.Position.X = v);
            Prop("Y", "Position Y, world metres", 0.05f, e => e.Position.Y, (d, v) => d.Position.Y = v);
            Prop("Z", "Position Z, world metres", 0.05f, e => e.Position.Z, (d, v) => d.Position.Z = v);
            Group("ROTATION");
            Prop("Roll", "Rotation X, degrees", 0.5f, e => Euler(e).X, (d, v) => d.RotationDeg.X = v);
            Prop("Pitch", "Rotation Y, degrees", 0.5f, e => Euler(e).Y, (d, v) => d.RotationDeg.Y = v);
            Prop("Yaw", "Rotation Z, degrees", 0.5f, e => Euler(e).Z, (d, v) => d.RotationDeg.Z = v);
            Group("SCALE");
            Prop("X", "Scale X", 0.01f, e => e.Scale.X, (d, v) => d.Scale.X = v);
            Prop("Y", "Scale Y", 0.01f, e => e.Scale.Y, (d, v) => d.Scale.Y = v);
            Prop("Z", "Scale Z", 0.01f, e => e.Scale.Z, (d, v) => d.Scale.Z = v);
            Group("DRAWING");
            Prop("Draw dist", "How far away this placement is still drawn, in metres.\nZero means the archetype's own distance.", 1.0f,
                e => e.LodDist, (d, v) => d.LodDist = Math.Clamp(v, 0f, 20000f));

            Group("EDIT");
            var acts = new UniformGrid { Columns = 4, Rows = 1, Margin = new Thickness(8, 2, 8, 2) };
            ShellButton_U27 A(string text, string tip, Action a, bool danger = false)
            {
                var b = new ShellButton_U27(null, text, tip) { Margin = new Thickness(2), Height = 28, BaseBackground = ShellTheme_U27.Hover, Padding = new Thickness(4, 0) };
                if (danger) b.OnColour = ShellTheme_U27.Error;
                b.Click += () => Act(a);
                acts.Children.Add(b);
                return b;
            }
            A("Copy", "Copy the entity  (Ctrl+C)", () => P.RequestWorldCopy = true);
            A("Duplicate", "A copy beside it, selected  (Ctrl+D)", () => P.RequestWorldDuplicate = true);
            A("Paste", "Into the selected entity's ymap  (Ctrl+V)", () => P.RequestWorldPaste = true);
            A("Delete", "No confirmation - deleting is one Undo away  (Delete)", () => P.RequestWorldDelete = true, true);
            stack.Children.Add(acts);
            var addProj = new ShellButton_U27(ShellIcons_U27.Project, "Add to project", "The ymap this entity lives in joins the project.", 16)
            { Margin = new Thickness(10, 2, 10, 8), Height = 30, BaseBackground = ShellTheme_U27.Hover };
            addProj.Click += () => Act(() => P.RequestAddSelectionToProject = true);
            stack.Children.Add(addProj);
            return new ScrollViewer { Content = stack };
        }

        private Control BuildFooter()
        {
            dirtyText = new TextBlock { Foreground = ShellTheme_U27.Warn, FontSize = 12 };
            folderText = new TextBlock { Foreground = ShellTheme_U27.Dim, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            editStatus = new TextBlock { Foreground = ShellTheme_U27.Dim, FontSize = 11, TextWrapping = TextWrapping.Wrap };
            var folder = new ShellButton_U27(ShellIcons_U27.Open, null, "Where edited .ymap files are written", 15) { Height = 26 };
            folder.Click += () => Act(() => P.RequestWorldChooseOutput = true);
            bSaveYmaps = new ShellButton_U27(ShellIcons_U27.Save, "Save ymaps", null, 15) { Height = 28, BaseBackground = ShellTheme_U27.AccentSoft };
            bSaveYmaps.Click += () => Act(() => P.RequestWorldSaveAll = true);
            bDiscard = new ShellButton_U27(null, "Discard", "Throw the changes away. The world reloads them from the archives.") { Height = 28 };
            bDiscard.Click += () => Act(() => P.RequestWorldDiscard = true);
            var folderRow = new DockPanel();
            DockPanel.SetDock(folder, Dock.Right);
            folderRow.Children.Add(folder);
            folderRow.Children.Add(folderText);
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            btnRow.Children.Add(bSaveYmaps);
            btnRow.Children.Add(bDiscard);
            var st = new StackPanel { Margin = new Thickness(10, 6), Spacing = 4 };
            st.Children.Add(dirtyText);
            st.Children.Add(btnRow);
            st.Children.Add(folderRow);
            st.Children.Add(editStatus);
            return new Avalonia.Controls.Border
            {
                Child = st,
                BorderBrush = ShellTheme_U27.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Background = ShellTheme_U27.PanelDeep,
            };
        }

        private static SharpDX.Vector3 Euler(YmapEntityDef e) => WorldEditor.ToEulerDegrees(e.Orientation);

        private void Apply(Action<LightPanel.EntityEdit, float> set, float v)
        {
            var p = P;
            var e = p?.WorldSel;
            if (e == null) return;
            var r = Euler(e);
            var edit = p.WorldEditApply ?? new LightPanel.EntityEdit
            {
                Position = new System.Numerics.Vector3(e.Position.X, e.Position.Y, e.Position.Z),
                RotationDeg = new System.Numerics.Vector3(r.X, r.Y, r.Z),
                Scale = new System.Numerics.Vector3(e.Scale.X, e.Scale.Y, e.Scale.Z),
                LodDist = e.LodDist,
            };
            set(edit, v);
            p.WorldEditApply = edit;
        }

        private void SyncRight()
        {
            if (rightColumn == null) return;
            var p = P;
            bool owns = p != null && p.ShellOwnsRight_U27;
            rightHeader.IsVisible = owns && !fullscreen;
            rightSplitter.IsVisible = owns && !fullscreen;
            rightColumn.Width = owns && !fullscreen ? (rightColumn.Width.IsAbsolute && rightColumn.Width.Value > 0 ? rightColumn.Width : new GridLength(340)) : new GridLength(0);
            if (!owns || fullscreen)
            {
                rightBody.IsVisible = false;
                Grid.SetColumnSpan(viewportArea, 1);
                if (p != null) p.ShellRightImGui_U27 = false;
                return;
            }

            var pw = p.ProjectWindow;
            if (pw != null && pw.ShowsDocked)
            {
                if (pw.Visible && page != LightPanel.ShellPageProject_U27) { page = LightPanel.ShellPageProject_U27; for (int i = 0; i < pageTabs.Count; i++) pageTabs[i].IsOn = i == page; }
                else if (!pw.Visible && page == LightPanel.ShellPageProject_U27) { page = 0; for (int i = 0; i < pageTabs.Count; i++) pageTabs[i].IsOn = i == 0; }
            }
            var req = p.ConsumeRightTabRequest_U27();
            if (req != null)
            {
                for (int i = 0; i < RightPages.Length; i++)
                    if (string.Equals(RightPages[i].label, req, StringComparison.OrdinalIgnoreCase)) { page = i; for (int k = 0; k < pageTabs.Count; k++) pageTabs[k].IsOn = k == i; }
            }

            bool native = page == LightPanel.ShellPageInspector_U27 && p.ShellInspectorNative_U27;
            rightImGui = !native;
            rightBody.IsVisible = native;
            Grid.SetColumnSpan(viewportArea, native ? 1 : 3);
            p.ShellRightPage_U27 = page;
            p.ShellRightImGui_U27 = rightImGui;
            p.ShellRightPx_U27 = (float)((rightColumn.Width.Value + 4) * Window.RenderScaling);
            if (native) SyncInspector(p);
        }

        private void SyncInspector(LightPanel p)
        {
            var e = p.WorldSel;
            bool has = e != null;
            selBar.IsVisible = has;
            propsPane.IsVisible = has && propTab.IsOn;
            detailsPane.IsVisible = has && detailTab.IsOn;
            noSelNote.IsVisible = !has;

            if (++treeTick % 15 == 1) RefreshTree(p);
            if (!ReferenceEquals(e, lastSel))
            {
                lastSel = e;
                if (e != null) RevealInTree(e);
                FillLights(p, e);
            }

            if (has)
            {
                bool mlo = e.MloInstance != null || e.Archetype is MloArchetype;
                selIcon.IsVisible = !mlo;
                selIconMlo.IsVisible = mlo;
                string name = (e.Archetype?.Name ?? e.Name ?? "(unnamed)") + (e.Ymap != null && e.Ymap.HasChanged ? "  *" : "");
                if (selName.Text != name) selName.Text = name;
                string where = "in " + (string.IsNullOrEmpty(e.Ymap?.Name) ? "(unknown ymap)" : e.Ymap.Name);
                if (selWhere.Text != where) selWhere.Text = where;
                multiNote.IsVisible = p.WorldSelCount_V20 > 1;
                if (multiNote.IsVisible) ((TextBlock)multiNote).Text = p.WorldSelCount_V20 + " selected - the gizmo moves them together";
                foreach (var (box, get, _) in fields)
                {
                    if (box.IsFocused) continue;
                    string s = get(e).ToString("0.###", CultureInfo.InvariantCulture);
                    if (box.Text != s) box.Text = s;
                }
                if (detailsPane.IsVisible) FillDetails(e);
            }

            int dirty = p.WorldDirtyCount;
            dirtyText.IsVisible = dirty > 0;
            dirtyText.Text = $"{dirty} ymap{(dirty == 1 ? "" : "s")} edited";
            bSaveYmaps.Enabled = dirty > 0;
            bDiscard.Enabled = dirty > 0;
            string folder = string.IsNullOrEmpty(p.WorldOutputFolder) ? "No output folder" : "Saves to  " + System.IO.Path.GetFileName(p.WorldOutputFolder.TrimEnd('\\')) + "\\";
            if (folderText.Text != folder) { folderText.Text = folder; ToolTip.SetTip(folderText, p.WorldOutputFolder); }
            string status = p.WorldEditStatus ?? "";
            editStatus.IsVisible = status.Length > 0;
            if (editStatus.Text != status) editStatus.Text = status;
        }

        private static Control Row(Control icon, string text, IBrush fg, string extra = null)
        {
            var st = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (icon != null) st.Children.Add(icon);
            st.Children.Add(new TextBlock { Text = text, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
            if (extra != null) st.Children.Add(new TextBlock { Text = extra, Foreground = ShellTheme_U27.Faint, VerticalAlignment = VerticalAlignment.Center });
            return st;
        }

        private static Control TintedIcon(string data, IBrush b)
        {
            var tint = new List<Avalonia.Controls.Shapes.Shape>();
            var ic = ShellTheme_U27.Icon(data, 15, tint);
            foreach (var s in tint) s.Stroke = b;
            return ic;
        }

        private static readonly IBrush YmapBrush = new SolidColorBrush(Color.Parse("#6FA8FF"));
        private static readonly IBrush EntityBrush = new SolidColorBrush(Color.Parse("#E8B04B"));
        private static readonly IBrush MloBrush = new SolidColorBrush(Color.Parse("#B98CFF"));

        private void RefreshTree(LightPanel p)
        {
            var world = p.WorldRef;
            if (world == null || !world.Ready) return;
            var cam = new SharpDX.Vector3(p.WorldCameraPos.X, p.WorldCameraPos.Y, p.WorldCameraPos.Z);
            var near = new List<(float d, WorldStreamer.MapNode n)>();
            foreach (var n in world.Nodes)
            {
                if (n.Ymap?.AllEntities == null) continue;
                near.Add((n.DistanceTo(cam), n));
            }
            near.Sort((a, b) => a.d.CompareTo(b.d));
            if (near.Count > 40) near.RemoveRange(40, near.Count - 40);
            var hashes = new List<uint>();
            foreach (var (_, n) in near) hashes.Add(n.Hash);
            hashes.Sort();
            string key = string.Join(",", hashes);
            var sel = p.WorldSel;
            bool keepSel = sel?.Ymap != null;
            if (key == treeKey) return;
            treeKey = key;
            syncingTree = true;
            try
            {
                var keep = new HashSet<uint>(hashes);
                foreach (var h in new List<uint>(ymapItems.Keys)) if (!keep.Contains(h) && !(keepSel && ymapItems[h].Tag is WorldStreamer.MapNode mn && ReferenceEquals(mn.Ymap, sel.Ymap))) ymapItems.Remove(h);
                tree.Items.Clear();
                foreach (var (d, n) in near)
                {
                    if (!ymapItems.TryGetValue(n.Hash, out var it))
                    {
                        it = new TreeViewItem { Tag = n };
                        it.Header = Row(TintedIcon(ShellIcons_U27.File, YmapBrush), n.Name, ShellTheme_U27.Text, n.Ymap.AllEntities.Length.ToString());
                        it.Items.Add(new TreeViewItem { Header = "..." });
                        it.PropertyChanged += (s, e) =>
                        {
                            if (e.Property == TreeViewItem.IsExpandedProperty && it.IsExpanded) FillYmap(it);
                        };
                        ymapItems[n.Hash] = it;
                    }
                    tree.Items.Add(it);
                }
            }
            finally { syncingTree = false; }
            if (sel != null) RevealInTree(sel);
        }

        private void FillYmap(TreeViewItem it)
        {
            if (!(it.Tag is WorldStreamer.MapNode n) || n.Ymap?.AllEntities == null) return;
            syncingTree = true;
            try
            {
                it.Items.Clear();
                string filter = filterBox.Text ?? "";
                var ents = n.Ymap.AllEntities;
                int shown = 0, hidden = 0;
                foreach (var en in ents)
                {
                    var nm = en?.Archetype?.Name ?? en?.Name;
                    if (string.IsNullOrEmpty(nm)) continue;
                    if (filter.Length > 0 && nm.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (shown >= 250) { hidden++; continue; }
                    shown++;
                    bool mlo = en.MloInstance != null || en.Archetype is MloArchetype;
                    var child = new TreeViewItem
                    {
                        Tag = en,
                        Header = Row(TintedIcon(mlo ? ShellIcons_U27.House : ShellIcons_U27.Local, mlo ? MloBrush : EntityBrush), nm, ShellTheme_U27.Text),
                    };
                    it.Items.Add(child);
                    if (ReferenceEquals(en, P?.WorldSel)) tree.SelectedItem = child;
                }
                if (hidden > 0) it.Items.Add(new TreeViewItem { Header = new TextBlock { Text = $"... {hidden:N0} more (narrow the filter)", Foreground = ShellTheme_U27.Faint } });
                if (shown == 0 && hidden == 0) it.Items.Add(new TreeViewItem { Header = new TextBlock { Text = "nothing matches", Foreground = ShellTheme_U27.Faint } });
            }
            finally { syncingTree = false; }
        }

        private void RevealInTree(YmapEntityDef e)
        {
            if (e?.Ymap == null) return;
            foreach (var it in ymapItems.Values)
            {
                if (!(it.Tag is WorldStreamer.MapNode n) || !ReferenceEquals(n.Ymap, e.Ymap)) continue;
                syncingTree = true;
                try
                {
                    if (!it.IsExpanded) it.IsExpanded = true;
                    FillYmap(it);
                    syncingTree = true;
                    foreach (var c in it.Items)
                        if (c is TreeViewItem ti && ReferenceEquals(ti.Tag, e)) { tree.SelectedItem = ti; ti.BringIntoView(); break; }
                }
                finally { syncingTree = false; }
                return;
            }
        }

        private void FillLights(LightPanel p, YmapEntityDef e)
        {
            lightsList.Children.Clear();
            if (e?.Archetype == null || p.WorldLightSource == null ||
                !p.WorldLightSource.TryGetDefs(e.Archetype.Hash, out var defs) || defs == null || defs.Length == 0)
            {
                lightsList.Children.Add(new TextBlock { Text = e == null ? "Select a prop to see its lights." : "This prop carries no lights.", Foreground = ShellTheme_U27.Dim, TextWrapping = TextWrapping.Wrap });
                subLights.Text = "Lights";
                return;
            }
            subLights.Text = $"Lights ({defs.Length})";
            for (int i = 0; i < defs.Length; i++)
            {
                var la = defs[i].L;
                if (la == null) continue;
                var swatch = new Avalonia.Controls.Border
                {
                    Width = 14, Height = 14, CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(la.ColorR, la.ColorG, la.ColorB)),
                    BorderBrush = ShellTheme_U27.Border, BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var b = new ShellButton_U27(null, $"{i}: {la.Type}    {la.Intensity:0.#}    {la.Falloff:0.#} m", "Select this light to edit it") { Height = 28, HorizontalAlignment = HorizontalAlignment.Stretch };
                var row = new DockPanel { Margin = new Thickness(0, 1) };
                DockPanel.SetDock(swatch, Dock.Left);
                row.Children.Add(swatch);
                row.Children.Add(b);
                int idx = i;
                b.Click += () => Act(() =>
                {
                    P.RequestSelectWorldLight = true;
                    P.RequestSelectWorldLightEntity = e;
                    P.RequestSelectWorldLightIndex = idx;
                });
                lightsList.Children.Add(row);
            }
        }

        private void FillDetails(YmapEntityDef e)
        {
            var d = e._CEntityDef;
            var rows = new List<(string, string)>
            {
                ("ENTITY", null),
                ("Archetype", d.archetypeName.ToString() + "   # " + d.archetypeName.Hash),
                ("GUID", d.guid.ToString()),
                ("Flags", d.flags.ToString()),
                ("Lod dist", $"{d.lodDist:0.#}   child {d.childLodDist:0.#}"),
                ("Lod level", d.lodLevel.ToString().Replace("LODTYPES_DEPTH_", "")),
                ("Priority", d.priorityLevel.ToString()),
                ("Parent index", d.parentIndex + (e.Parent != null ? "   (" + (e.Parent.Archetype?.Name ?? "?") + ")" : "")),
                ("Children", d.numChildren.ToString()),
                ("AO / artificial", $"{d.ambientOcclusionMultiplier} / {d.artificialAmbientOcclusion}"),
                ("Tint", d.tintValue.ToString()),
                ("Distance", $"{e.Distance:0.#} m"),
            };
            if (e.MloParent != null) rows.Add(("Interior", e.MloParent.Archetype?.Name ?? "(mlo)"));
            var a = e.Archetype;
            if (a != null)
            {
                var b = a._BaseArchetypeDef;
                rows.Add(("ARCHETYPE", null));
                rows.Add(("Name", a.Name + "   # " + a.Hash));
                rows.Add(("Type", a is MloArchetype ? "MLO (interior)" : a is TimeArchetype ? "Time" : "Base"));
                rows.Add(("Asset", b.assetName.ToString() + "   " + b.assetType.ToString().Replace("ASSET_TYPE_", "")));
                rows.Add(("Texture dict", b.textureDictionary.ToString()));
                rows.Add(("Drawable dict", b.drawableDictionary.ToString()));
                rows.Add(("Physics dict", b.physicsDictionary.ToString()));
                rows.Add(("Lod dist", $"{b.lodDist:0.#}   hd tex {b.hdTextureDist:0.#}"));
                rows.Add(("Flags", b.flags.ToString()));
                rows.Add(("BB min", $"{b.bbMin.X:0.##}, {b.bbMin.Y:0.##}, {b.bbMin.Z:0.##}"));
                rows.Add(("BB max", $"{b.bbMax.X:0.##}, {b.bbMax.Y:0.##}, {b.bbMax.Z:0.##}"));
                rows.Add(("BS", $"r {b.bsRadius:0.##} @ {b.bsCentre.X:0.##}, {b.bsCentre.Y:0.##}, {b.bsCentre.Z:0.##}"));
            }
            int k = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var (label, value) = rows[i];
                Grid g;
                if (k < detailRows.Children.Count) g = (Grid)detailRows.Children[k];
                else
                {
                    g = new Grid { ColumnDefinitions = new ColumnDefinitions("104,*"), Margin = new Thickness(0, 2) };
                    var l = new TextBlock { Foreground = ShellTheme_U27.Dim, FontSize = 12 };
                    var v = new SelectableTextBlock { Foreground = ShellTheme_U27.Text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
                    Grid.SetColumn(v, 1);
                    g.Children.Add(l);
                    g.Children.Add(v);
                    detailRows.Children.Add(g);
                }
                k++;
                var lt = (TextBlock)g.Children[0];
                var vt = (SelectableTextBlock)g.Children[1];
                bool head = value == null;
                lt.Text = label;
                lt.FontWeight = head ? FontWeight.SemiBold : FontWeight.Normal;
                lt.Foreground = head ? ShellTheme_U27.Faint : ShellTheme_U27.Dim;
                Grid.SetColumnSpan(lt, head ? 2 : 1);
                lt.Margin = head ? new Thickness(0, 8, 0, 0) : new Thickness(0);
                if (vt.Text != (value ?? "")) vt.Text = value ?? "";
            }
            while (detailRows.Children.Count > k) detailRows.Children.RemoveAt(detailRows.Children.Count - 1);
        }
    }
}
