using Serilog;
using System;
using System.IO;
using System.Text.Json;
using ZeroElectric.Fenestra;

namespace ZeroElectric.Fenestra
{
    public class AppSettings
    {
        public Version FenestraVersion { get; } = new Version(1, 6, 0);

        public string AutoRunPath { get; set; } = "";
        public string InstallPath { get; set; } = "";

        public DateTime NextUpdateCheck { get; set; } = DateTime.MinValue;
        public Version InstalledAppVersion { get; set; } = new Version(0, 0, 0);
        public MPackManifest LastHeader { get; set; } = null;

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
