using System;
using System.Windows;
using System.Windows.Interop;
using Serilog;
using ZeroElectric.Fenestra.Windows;

namespace ZeroElectric.Fenestra.Launcher
{
    public partial class MainWindow : Window, IStageReceiver
    {
        private readonly Editor Editor = new Editor();

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;

            #region Windows 11

            IntPtr hWnd = new WindowInteropHelper(GetWindow(this)).EnsureHandle();
            DWMWINDOWATTRIBUTE attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE;
            DWM_WINDOW_CORNER_PREFERENCE preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            Win32.DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));

            #endregion
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            appVer.Text = $"App v{AppSettings.Settings.InstalledAppVersion}";
            laucherVer.Text = $"Launcher v{Program.AppManifest.launcherVer}";

            Editor.Show();

            StageManager.SetStageReceiver(this);

            if (Program.DoDebug == false)
            {
                Program.Installer.Update();
            }
        }

        //
        // IStageReceiver
        //

        public void OnStageChanged(Stage stage)
        {
            Dispatch(() => {
                switch (stage)
                {

                }
            });
        }
        public void OnStageTextChanged(string message)
        {
            Dispatch(() => {
                stageText.Text = message;
            });
        }

        public void DisplayWork(string message)
        {
            Dispatch(() => {
                workingText.Text = $"{message}\n{workingText.Text}";
            });
        }
        public void ShowError(string message)
        {
            //TODO
        }

        public void ClearWorkText()
        {
            Dispatch(() => {
                workingText.Text = "";
            });
        }

        //
        public void Dispatch(Action callback)
        {
            Dispatcher.Invoke(() => {
                callback.Invoke();
            });
        }

        //
        //
        //

        private void OnClick_Quit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void OnClick_Build(object sender, RoutedEventArgs e)
        {
            try
            {
                Program.Installer.BuildPAK();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception while compressing aepkg");
            }
        }

        private void OnClick_PkgOut(object sender, RoutedEventArgs e)
        {

        }

        private void OnClick_Launch(object sender, RoutedEventArgs e)
        {
            Program.DoUpdate = false;

            Program.Installer.Launch();
        }

        private void OnClick_Load(object sender, RoutedEventArgs e)
        {
            Program.Installer.DecompressPAK();
        }
    }
}