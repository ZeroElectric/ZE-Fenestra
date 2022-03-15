using Serilog;
using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;

namespace AE.Ingredior.Launcher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;

            #region Windows 11

            IntPtr hWnd = new WindowInteropHelper(GetWindow(this)).EnsureHandle();
            var attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE;
            var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            Win32.DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));

            #endregion
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            appVer.Text = $"App v{AppSettings.Settings.InstalledVersion}";
            laucherVer.Text = $"Launcher v{Program.AppManifest.appVer}";

            await Program.Installer.RunAppAsync();
        }

        public void SetWorkingText(string message)
        {
            this.workingText.Text = message;
        }

        //

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private async void Test2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Program.Installer.BuildPkg();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception while compressing aepkg");
            }

            GC.Collect(3);
        }

        private async void Test3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string outputPath = Path.Combine(FileSystemHelper.Output, $"AEO-1.0.0.aepkg");

                await Program.Installer.DeserializedAndOutputPkg(outputPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception while decompressing aepkg");
            }

            GC.Collect(3);
        }

        private void Test4_Click(object sender, RoutedEventArgs e)
        {
            InstallBuilder.Builder.BuildEXE();
        }
    }
}