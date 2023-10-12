using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Onova;
using Onova.Models;
using Onova.Services;
using Serilog;

namespace ZeroElectric.Fenestra
{
    public static class Program
    {
        public static Installer Installer { get; private set; } = new Installer();
        public static AppManifest AppManifest { get; private set; } 

        //
        // Args
        //

        public static string AppArgs = "";

        public static bool LauncherUpdated = false;
        public static string LauncherUpdateVer = "";

        public static bool ShowConsole = false;

        public static bool DoUpdate = false;
        public static bool IgnoreLauncherUpdate = false;

        public static bool BuildPkg = false;
        public static bool BuildPort = false;

        public static bool DoDebug = true;
        public static bool DoClean = true;
        public static bool NoLaunch = false;

        public static bool UseCompression = true;
        public static CompressionType CompressionType = CompressionType.LZ4;

        public static bool InternalCompression = true;
        public static CompressionType InternalCompressionType = CompressionType.LZ4;

        [STAThread]
        public static void Main(string[] args)
        {
            // Setup logger
            var logFile = FS.GetFile($"aeoi-{DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")}.log", true, FS.TempLog);

            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose()
                .WriteTo.Console()
                .WriteTo.File(logFile)
                .CreateLogger();

            Log.Information("Starting ZeroElectric.Fenestra...");

            // Detect args
            for (var i = 0; i < args.Length; i++)
            {
                try
                {
                    var split = args[i].Split('=');

                    switch (split[0])
                    {

                        case "-update":
                            {
                                LauncherUpdated = true;
                                LauncherUpdateVer = split[1];

                                // Clear Temp
                                FS.ClearTemp();

                                break;
                            }

                        case "-uninstall":
                            {
                                // TODO(Ken) Uninstall

                                break;
                            }

                        case "-doupdate":
                            {
                                DoUpdate = true;
                                break;
                            }

                        case "-ignorelauncherupdate":
                            {
                                IgnoreLauncherUpdate = true;
                                break;
                            }

                        case "-build":
                            {
                                BuildPkg = true;
                                break;
                            }

                        case "-buildport":
                            {
                                BuildPort = true;
                                break;
                            }

                        case "-nolaunch":
                            {
                                NoLaunch = true;
                                break;
                            }

                        case "-debug":
                            {
                                DoDebug = true;
                                break;
                            }

                        case "-appargs": // eg: -appargs=loadpage?home,resetproj?vitanova
                            {
                                if (split[1] != null)
                                {
                                    AppArgs = split[1];
                                }
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Exception while detecting args, :: {ex}", ex);
                }

                Log.Debug("args found: {args}", args[i]);
            }

            AppSettings.Load();

            Log.Information("ZeroElectric.Fenestra, v{ver}", AppSettings.Settings.FenestraVersion);

            if (AppSettings.Settings.NextUpdateCheck < DateTime.Now)
            {
                DoUpdate = true;
            }

            // Load AppManifest
            if (File.Exists(FS.AppManifest))
            {
                try
                {
                    using (var stream = File.OpenRead(FS.AppManifest))
                    {
                        AppManifest = JsonSerializer.Deserialize<AppManifest>(stream);
                    }

                    if (LauncherUpdated)
                    {
                        AppManifest.launcherVer = new Version(LauncherUpdateVer);

                        Log.Information("Launcher has been updated, v{ver}", AppManifest.launcherVer);

                        using (var stream = File.Create(FS.AppManifest))
                        {
                            JsonSerializer.Serialize(stream, AppManifest);
                        }

                        Log.Debug("AppManifest has been saved");
                    }

                    Log.Information("Launcher, v{ver}", AppManifest.launcherVer);

                    Log.Debug("AppManifest Loaded:\t{@man}", AppManifest);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception wihle loading AppManifest");

                    //TODO show an Error to user

                    AppManifest = null;
                }
            }

            if (DoDebug)
            {
                var application = new App();
                application.InitializeComponent();
                application.Run();

                return;
            }

            // Update Launcher if available
            if (AppManifest != null && Debugger.IsAttached == false && DoUpdate == true && BuildPkg == false && IgnoreLauncherUpdate == false)
            {
                using (var manager = new UpdateManager(
                    new AssemblyMetadata("ZeroElectric.Fenestra", AppSettings.Settings.FenestraVersion, AppManifest.GetType().Assembly.Location),
                    new WebPackageResolver(AppManifest.launcherManifestURI),
                    new ZipPackageExtractor()))
                {
                    var update = manager.CheckForUpdatesAsync();

                    Log.Debug("Checking for Launcher updates...");

                    update.Wait();

                    if (update.Result.CanUpdate)
                    {
                        Log.Information("New Launcher version is available, v{ver}", update.Result.LastVersion);

                        Log.Debug("Preparing update...");

                        // Prepare an update by downloading and extracting the package
                        // (supports progress reporting and cancellation)
                        manager.PrepareUpdateAsync(update.Result.LastVersion).Wait();

                        Log.Information("Updating...");

                        // Launch an executable that will apply the update
                        // (can be instructed to restart the application afterwards)
                        manager.LaunchUpdater(update.Result.LastVersion, true, $"-update={update.Result.LastVersion}");

                        Log.Information("Restaring");

                        // Terminate the running application so that the updater can overwrite files
                        Environment.Exit(0);
                    }
                    else
                    {
                        Log.Information("Launcher is up to date");
                    }

                }
            }

            // Load a default AppManifest if none was present 
            if (AppManifest == null)
            {
                AppManifest = new AppManifest();
            }

            if (BuildPkg)
            {
                //Installer.BuildPkg();
                //Installer.Start_Build();
                //SetupBuilder.Builder.BuildEXE(Installer.MPAKHeader, BuildPort);
            }
            else if (DoUpdate)
            {
                // Run the Launcher like normal

                if (string.IsNullOrEmpty(AppManifest.appManifestURI))
                {
                    Log.Fatal("ingredior.manifest was not found or was invallid");

                    MessageBox.Show("Fenestra is not setup for install mode.\ningredior.manifest was not found or was invallid.", "Fenestra [Fatal Error]!", MessageBoxButton.OK);

                    Environment.Exit(1);
                }

                var application = new App();
                application.InitializeComponent();
                application.Run();
            }
            else
            {
                Installer.Launch();
            }
        }
    }
}