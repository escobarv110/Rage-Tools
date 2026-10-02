using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using RageLightEditor.Editor;

namespace RageLightEditor.Shell
{
    public class ShellApp_U27 : Avalonia.Application
    {
        public override void Initialize()
        {
            RequestedThemeVariant = ThemeVariant.Dark;
            Styles.Add(new FluentTheme());
            Resources["SystemAccentColor"] = ShellTheme_U27.AccentColour;
            Resources["MenuFlyoutPresenterBackground"] = ShellTheme_U27.Panel;
            Resources["MenuFlyoutPresenterBorderBrush"] = ShellTheme_U27.Border;
            Resources["ToolTipBackground"] = ShellTheme_U27.Panel;
            Resources["ToolTipBorderBrush"] = ShellTheme_U27.Border;
            Resources["ToolTipForeground"] = ShellTheme_U27.Text;
        }
    }

    public static class ShellHost_U27
    {
        private static bool setUp;

        public static ApplicationContext Start(MainForm form)
        {
            try
            {
                if (!setUp)
                {
                    AppBuilder.Configure<ShellApp_U27>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
                    setUp = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("NEWUI could not start, using the classic interface: " + ex.Message);
                return null;
            }
            LightPanel.ShellChrome_U27 = true;
            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.AttachToShell_U27();
            var ctx = new ApplicationContext();
            var shell = new ShellWindow_U27(form);
            form.FormClosed += (s, e) => { shell.CloseFromForm(); ctx.ExitThread(); };
            shell.Window.Show();
            return ctx;
        }
    }

    public class ViewportHost_U27 : NativeControlHost
    {
        private readonly MainForm form;

        public ViewportHost_U27(MainForm form) { this.form = form; }

        [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            var h = form.Handle;
            SetParent(h, parent.Handle);
            if (!form.Visible) form.Show();
            return new PlatformHandle(h, "HWND");
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control) { }
    }
}
