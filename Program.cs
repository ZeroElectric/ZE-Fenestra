using Onova;
using Onova.Models;
using Onova.Services;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace AE.Ingredior
{

    public class Program
    {
        public static Version AppVersion { get; } = new Version(1, 0, 0);

        public static AppManifest AppManifest { get; private set; }

        public static Installer Installer { get; private set; } = new Installer();

        //
        // Args props
        //

        public static bool LauncherUpdated = false;
        public static string LauncherUpdateVer = "";
        public static bool DoUpdate = false;

        public static bool ShowConsole = false;
        public static bool BuildPkg = false;

        public static string AppArgs = "";

        [STAThread]
        public static void Main(string[] args)
        {
            // Detect args
            for (int i = 0; i < args.Length; i++)
            {
                try
                {
                    var split = args[i].Split('=');

                    switch (split[0])
                    {
                        case "update":
                            {
                                LauncherUpdated = true;
                                LauncherUpdateVer = split[1];

                                // Clear Temp
                                foreach (var file in Directory.GetFiles(FileSystemHelper.Temp, "*"))
                                {
                                    File.Delete(file);
                                }

                                break;
                            }

                        case "uninstall":
                            {
                                // Uninstall

                                break;
                            }

                        case "doupdate":
                            {
                                DoUpdate = true;
                                break;
                            }

                        case "build":
                            {
                                BuildPkg = true;
                                break;
                            }

                        case "appargs": // appargs=loadpage?home,resetproj?vitanova
                            {
                                if (split[1] != null)
                                {
                                    AppArgs = split[1];
                                }
                                break;
                            }

                        default:
                            {
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {

                }
            }

            // Setup logger
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose()
                .WriteTo.Console()
                .WriteTo.File(FileSystemHelper.GetFile($"aeoi-{DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")}.log", true, FileSystemHelper.TempLog))
                .CreateLogger();

            Log.Information("AE Ingredior, v{ver}", AppVersion);

            AppSettings.Load();

            if(AppSettings.Settings.NextUpdateCheck < DateTime.Now)
            {
                DoUpdate = true;
            }

            // Load AppManifest
            if (File.Exists(FileSystemHelper.AppManifest))
            {
                try
                {
                    using (FileStream stream = File.OpenRead(FileSystemHelper.AppManifest))
                    {
                        AppManifest = JsonSerializer.Deserialize<AppManifest>(stream);
                    }

                    if (LauncherUpdated)
                    {
                        AppManifest.appVer = new Version(LauncherUpdateVer);

                        Log.Information("Launcher has been updated, v{ver}", AppManifest.appVer);

                        using (FileStream stream = File.Create(FileSystemHelper.AppManifest))
                        {
                            JsonSerializer.Serialize<AppManifest>(stream, AppManifest);
                        }

                        Log.Debug("AppManifest has been saved");
                    }

                    Log.Information("Launcher, v{ver}", AppManifest.appVer);

                    Log.Debug("AppManifest Loaded:\t{@man}", AppManifest);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception wihle loading AppManifest");

                    //TODO show an Error to user

                    AppManifest = null;
                }
            }

            // Update Launcher if available
            if (AppManifest != null && Debugger.IsAttached == false && DoUpdate == true && BuildPkg == false)
            {
                using (var manager = new UpdateManager(new AssemblyMetadata(AppManifest.outputTemplate, AppManifest.appVer, Path.Combine(FileSystemHelper.BaseDirectory, AppManifest.launcherName)),
                   new WebPackageResolver(AppManifest.launcherUpdate),
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
                        manager.LaunchUpdater(update.Result.LastVersion, true, $"update={update.Result.LastVersion}");

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

            // Load a default AppManifest is none was present 
            if (AppManifest == null)
            {
                AppManifest = new AppManifest();
            }

            if (BuildPkg)
            {
                // TODO Show Console and build output

                InstallBuilder.Builder.BuildEXE();

                Console.ReadLine();
            }
            else if (DoUpdate == true)
            {
                // Run the Launcher like normal
              
                var application = new App();
                application.InitializeComponent();
                application.Run();
            }
            else
            {
                Installer.RunAppAsync().Wait();
            }
        }
    }

    public class AppSettings
    {
        public string AutoRunPath { get; set; } = "";
        public string InstallPath { get; set; } = "";

        public DateTime NextUpdateCheck { get; set; } = DateTime.MinValue;
        public Version InstalledVersion { get; set; } = new Version(0, 0, 0);
        public MPackHeader LastHeader { get; set; } = null;

        #region Static

        public static AppSettings Settings { get; private set; }

        public static void Load()
        {
            try
            {
                if (File.Exists(FileSystemHelper.AppSettings))
                {
                    using (FileStream stream = File.OpenRead(FileSystemHelper.AppSettings))
                    {
                        Settings = JsonSerializer.Deserialize<AppSettings>(stream);
                    }
                }
                else
                {
                    Settings = new AppSettings();
                    Save();
                }

                Log.Debug("settings loaded");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception while loading settings");
            }
        }

        public static void Save()
        {
            try
            {
                using (FileStream stream = File.Create(FileSystemHelper.AppSettings))
                {
                    JsonSerializer.Serialize<AppSettings>(stream, Settings);
                }

                Log.Debug("settings saved, {settings}", Settings);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception while saving settings");
            }
        }

        #endregion
    }
}