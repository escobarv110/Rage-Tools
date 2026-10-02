using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

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
    public static class ShellTheme_U27
    {
        public static readonly Color AccentColour = Color.Parse("#3B82F6");
        public static readonly IBrush Window = new SolidColorBrush(Color.Parse("#0E0F11"));
        public static readonly IBrush Panel = new SolidColorBrush(Color.Parse("#141518"));
        public static readonly IBrush PanelDeep = new SolidColorBrush(Color.Parse("#101113"));
        public static readonly IBrush Border = new SolidColorBrush(Color.Parse("#23252A"));
        public static readonly IBrush Text = new SolidColorBrush(Color.Parse("#D4D6DB"));
        public static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#8B9099"));
        public static readonly IBrush Faint = new SolidColorBrush(Color.Parse("#555A63"));
        public static readonly IBrush Accent = new SolidColorBrush(AccentColour);
        public static readonly IBrush AccentSoft = new SolidColorBrush(Color.Parse("#17294A"));
        public static readonly IBrush Hover = new SolidColorBrush(Color.Parse("#1F2227"));
        public static readonly IBrush Press = new SolidColorBrush(Color.Parse("#292D34"));
        public static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#E5B454"));
        public static readonly IBrush Error = new SolidColorBrush(Color.Parse("#F06A5E"));
        public static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#5FC98A"));
        public static readonly FontFamily Font = new FontFamily("fonts:Inter#Inter, $Default");

        public static readonly IBrush Blue = new SolidColorBrush(Color.Parse("#3D8EF0"));
        public static readonly IBrush BlueBright = new SolidColorBrush(Color.Parse("#7AB6FF"));
        public static readonly IBrush BlueFill = new SolidColorBrush(Color.Parse("#593D8EF0"));
        public static readonly IBrush BlueFillBright = new SolidColorBrush(Color.Parse("#737AB6FF"));

        public static Control Icon(string data, double size, List<Shape> tint, bool filled = false)
        {
            var canvas = new Canvas { Width = 24, Height = 24 };
            if (!filled)
            {
                var closed = new System.Text.StringBuilder();
                foreach (var fig in data.Split(" M"))
                {
                    var f = fig.Trim();
                    if (!f.EndsWith("Z")) continue;
                    if (closed.Length > 0) closed.Append(' ');
                    closed.Append(f.StartsWith("M") ? f : "M" + f);
                }
                if (closed.Length > 0)
                {
                    var fill = new Path { Data = Geometry.Parse(closed.ToString()), Fill = BlueFill, Tag = "f" };
                    tint?.Add(fill);
                    canvas.Children.Add(fill);
                }
            }
            var path = new Path
            {
                Data = Geometry.Parse(data),
                StrokeThickness = filled ? 0 : 1.8,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Stroke = filled ? null : Blue,
                Fill = filled ? Blue : null,
                Tag = filled ? "g" : "s",
            };
            tint?.Add(path);
            canvas.Children.Add(path);
            return new Viewbox { Width = size, Height = size, Child = canvas };
        }

        public static Control Divider(bool vertical) => new Avalonia.Controls.Border
        {
            Background = Border,
            Width = vertical ? 1 : double.NaN,
            Height = vertical ? double.NaN : 1,
            Margin = vertical ? new Thickness(6, 6) : new Thickness(0),
        };
    }

    public static class ShellIcons_U27
    {
        public const string Select = "M6,3 L6,19 L10,15 L13,21.5 L15.5,20.4 L12.6,14 L18,14 Z";
        public const string Move = "M12,2.5 V21.5 M2.5,12 H21.5 M9.5,5 L12,2.5 L14.5,5 M9.5,19 L12,21.5 L14.5,19 M5,9.5 L2.5,12 L5,14.5 M19,9.5 L21.5,12 L19,14.5";
        public const string Rotate = "M19.5,12 A7.5,7.5 0 1 1 16.8,6.2 M17.2,2.6 L16.8,6.2 L13.2,5.9";
        public const string Scale = "M4,13 V20 H11 M4,20 L20,4 M13,4 H20 V11";
        public const string Undo = "M9,13.5 L4.5,9 L9,4.5 M4.5,9 H14.5 A5,5 0 0 1 14.5,19 H10";
        public const string Redo = "M15,13.5 L19.5,9 L15,4.5 M19.5,9 H9.5 A5,5 0 0 0 9.5,19 H14";
        public const string Frame = "M12,9.5 A2.5,2.5 0 1 1 11.99,9.5 Z M4,9 V4 H9 M15,4 H20 V9 M20,15 V20 H15 M9,20 H4 V15";
        public const string Open = "M3.5,7 V18.5 A1,1 0 0 0 4.5,19.5 H19.5 A1,1 0 0 0 20.5,18.5 V9 A1,1 0 0 0 19.5,8 H11.5 L9.5,5.5 H4.5 A1,1 0 0 0 3.5,6.5 Z";
        public const string Save = "M5,4 H16 L20,8 V19 A1,1 0 0 1 19,20 H5 A1,1 0 0 1 4,19 V5 A1,1 0 0 1 5,4 Z M8,4 V9 H15 V4 M7.5,20 V14 H16.5 V20";
        public const string Project = "M3.5,8 H20.5 V19.5 H3.5 Z M3.5,8 V5 H9.5 L11.5,8 M7,12 H17 M7,15.5 H13";
        public const string World = "M12,3 A9,9 0 1 1 11.99,3 Z M3,12 H21 M12,3 C8,7 8,17 12,21 M12,3 C16,7 16,17 12,21";
        public const string Local = "M12,3 L20,7.5 V16.5 L12,21 L4,16.5 V7.5 Z M4,7.5 L12,12 L20,7.5 M12,12 V21";
        public const string Pick = "M12,9 A3,3 0 1 1 11.99,9 Z M12,2.5 V7 M12,17 V21.5 M2.5,12 H7 M17,12 H21.5";
        public const string Bulb = "M12,3 A6,6 0 0 1 16,13.4 C15.2,14.2 15,15 15,16 H9 C9,15 8.8,14.2 8,13.4 A6,6 0 0 1 12,3 Z M9.5,18 H14.5 M10.5,21 H13.5";
        public const string Magnet = "M6,4 V12 A6,6 0 0 0 18,12 V4 H14.5 V12 A2.5,2.5 0 0 1 9.5,12 V4 Z M6,7.5 H9.5 M14.5,7.5 H18";
        public const string Minus = "M7,12 H17";
        public const string Plus = "M7,12 H17 M12,7 V17";
        public const string Info = "M12,3 A9,9 0 1 1 11.99,3 Z M12,11 V17 M12,7.6 V7.5";
        public const string Assets = "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z";
        public const string Area = "M4,7 L10,4 L20,6 L18,17 L9,20 L5,15 Z";
        public const string Sliders = "M4,6 H20 M4,12 H20 M4,18 H20 M8,4 V8 M15,10 V14 M10,16 V20";
        public const string File = "M6,3 H14 L19,8 V21 H6 Z M14,3 V8 H19";
        public const string House = "M5.5,10 L12,4.5 L18.5,10 V20 H5.5 Z M3,11.5 L12,4 L21,11.5 M10,20 V14 H14 V20";
        public const string Output = "M4,5 H20 V19 H4 Z M7,9 L10,12 L7,15 M12,15 H16";

        public static readonly string[] Modes =
        {
            Local,
            "M12,7 A5,5 0 1 1 11.99,7 Z M12,2.5 V6.5 M12,17.5 V21.5 M2.5,12 H6.5 M17.5,12 H21.5",
            "M10,4 H14 V10 H20 V14 H14 V20 H10 V14 H4 V10 H10 Z",
            "M4,6 H14 V10 H20 V20 H10 V14 H4 Z",
            "M12,8 A4,4 0 1 1 11.99,8 Z M12,2.5 V5 M12,19 V21.5 M2.5,12 H5 M19,12 H21.5 M5.3,5.3 L7,7 M17,17 L18.7,18.7 M5.3,18.7 L7,17 M17,7 L18.7,5.3",
            "M3,16 V12.5 L5.2,7.5 H18.8 L21,12.5 V16 Z M3,12.5 H21 M6,16 V18.5 M18,16 V18.5 M6.5,14.2 H8 M16,14.2 H17.5",
            "M2.5,21 H21.5 M5,21 C5.5,16 5,12 3.5,9 M9.5,21 C9.5,15 10.5,10 12.5,6 M14.5,21 C14.5,16 13.8,12.5 12,10.5 M19,21 C19,16 19.8,12.5 21.5,9.5",
            "M2.5,9 C5,6.5 7,6.5 9.5,9 C12,11.5 14,11.5 16.5,9 C19,6.5 21,6.5 21.5,7 M2.5,15 C5,12.5 7,12.5 9.5,15 C12,17.5 14,17.5 16.5,15 C19,12.5 21,12.5 21.5,13",
            "M2.5,9.5 C6,7.5 8,11.5 12,9.5 C16,7.5 18,11.5 21.5,9.5 M2.5,15 H21.5 M2.5,19 H21.5",
            "M2.5,17 C5,17 6,6 10,6 C13,6 13.5,10.5 11,12 C15,11.5 17,17 21.5,17 M2.5,20.5 H21.5",
            "M12,3 L19,6 V11 C19,16 16,19 12,21 C8,19 5,16 5,11 V6 Z",
            "M3,19 L9,5 L15,19 Z M9,5 L21,11.5 L15,19",
            "M4,19 L10,13 L14,15 L20,6 M4,17 A2,2 0 1 1 3.99,17 Z M20,4 A2,2 0 1 1 19.99,4 Z",
            "M8.5,3 L5.5,21 M15.5,3 L18.5,21 M7.6,7.5 H16.4 M6.9,12 H17.1 M6.1,16.5 H17.9",
            "M6,6 A1.6,1.6 0 1 1 5.99,6 Z M12,4 A1.6,1.6 0 1 1 11.99,4 Z M18,7 A1.6,1.6 0 1 1 17.99,7 Z M8,14 A1.6,1.6 0 1 1 7.99,14 Z M16,16 A1.6,1.6 0 1 1 15.99,16 Z M11,20 A1.6,1.6 0 1 1 10.99,20 Z",
            House,
            "M12,3.5 A2.5,2.5 0 1 1 11.99,3.5 Z M7,21 V15 C7,12 9,11 12,11 C15,11 17,12 17,15 V21",
            "M4,9 H8 L13,5 V19 L8,15 H4 Z M16.5,8.5 C18,10 18,14 16.5,15.5 M19,6 C22,9 22,15 19,18",
            "M2.5,12 C5.5,6.5 18.5,6.5 21.5,12 C18.5,17.5 5.5,17.5 2.5,12 Z M12,9.5 A2.5,2.5 0 1 1 11.99,9.5 Z M4,4 L20,20",
            Bulb,
        };

        public static readonly string[] ModeLabels =
        {
            "Entity", "Precision", "Entity Ext", "Arch Ext", "Time Cycle", "Car Gen", "Grass", "Water", "Calming",
            "Waves", "Collision", "Nav Mesh", "Path", "Train Track", "LOD Lights", "MLO", "Scenario", "Audio",
            "Occlusion", "Light",
        };
    }

    public class ShellButton_U27 : Avalonia.Controls.Border
    {
        public event Action Click;
        private readonly List<Shape> tint = new List<Shape>();
        private readonly List<TextBlock> labels = new List<TextBlock>();
        private bool hover, press, on, enabled = true;
        private readonly bool underline;

        public ShellButton_U27(string icon, string text, string tip, double iconSize = 18, bool vertical = false,
            bool underline = false)
        {
            this.underline = underline;
            Cursor = new Cursor(StandardCursorType.Hand);
            CornerRadius = new CornerRadius(underline ? 0 : 5);
            Padding = vertical ? new Thickness(4, 7, 4, 5) : new Thickness(text == null ? 6 : 9, 5);
            var stack = new StackPanel
            {
                Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
                Spacing = vertical ? 5 : 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (icon != null)
            {
                var ic = ShellTheme_U27.Icon(icon, iconSize, tint);
                ic.HorizontalAlignment = HorizontalAlignment.Center;
                ic.VerticalAlignment = VerticalAlignment.Center;
                stack.Children.Add(ic);
            }
            if (text != null)
            {
                var tb = new TextBlock
                {
                    Text = text,
                    FontSize = vertical ? 11 : 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                labels.Add(tb);
                stack.Children.Add(tb);
            }
            Child = stack;
            if (tip != null) ToolTip.SetTip(this, tip);
            Refresh();
        }

        public string Text
        {
            get => labels.Count > 0 ? labels[0].Text : null;
            set { if (labels.Count > 0 && labels[0].Text != value) labels[0].Text = value; }
        }

        internal void PerformClick() { if (enabled) Click?.Invoke(); }

        public bool IsOn { get => on; set { if (on == value) return; on = value; Refresh(); } }
        public bool Enabled { get => enabled; set { if (enabled == value) return; enabled = value; Refresh(); } }
        public IBrush OnColour { get; set; }
        private IBrush baseBackground = Brushes.Transparent;
        public IBrush BaseBackground { get => baseBackground; set { baseBackground = value; Refresh(); } }

        protected override void OnPointerEntered(PointerEventArgs e) { base.OnPointerEntered(e); hover = true; Refresh(); }
        protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); hover = false; press = false; Refresh(); }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            press = true; Refresh();
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            bool was = press;
            press = false; Refresh();
            if (was && hover && enabled) Click?.Invoke();
        }

        private void Refresh()
        {
            bool lit = hover || press || on;
            IBrush text = !enabled ? ShellTheme_U27.Faint : on ? (OnColour ?? Brushes.White) : hover ? Brushes.White : ShellTheme_U27.Text;
            IBrush icon = !enabled ? ShellTheme_U27.Faint : on && OnColour != null ? OnColour : lit ? ShellTheme_U27.BlueBright : ShellTheme_U27.Blue;
            IBrush fill = !enabled ? Brushes.Transparent : lit ? ShellTheme_U27.BlueFillBright : ShellTheme_U27.BlueFill;
            if (underline)
            {
                Background = enabled && (press || hover) ? ShellTheme_U27.Hover : Brushes.Transparent;
                BorderBrush = on ? ShellTheme_U27.Accent : Brushes.Transparent;
                BorderThickness = new Thickness(0, 0, 0, 2);
                text = !enabled ? ShellTheme_U27.Faint : on || hover ? Brushes.White : ShellTheme_U27.Dim;
            }
            else
            {
                Background = !enabled ? baseBackground
                    : press ? ShellTheme_U27.Press
                    : on ? ShellTheme_U27.AccentSoft
                    : hover ? ShellTheme_U27.Hover : baseBackground;
                if (on && !enabled) Background = ShellTheme_U27.Hover;
            }
            foreach (var s in tint)
            {
                switch (s.Tag as string)
                {
                    case "f": s.Fill = fill; break;
                    case "g": s.Fill = icon; break;
                    default: s.Stroke = icon; break;
                }
            }
            foreach (var l in labels) l.Foreground = text;
        }
    }
}
