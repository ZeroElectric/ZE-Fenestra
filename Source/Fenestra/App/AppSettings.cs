using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace ZeroElectric.Fenestra
{
    public class AppSettings
    {
        public Version FenestraVersion { get; } = new Version(1, 6, 0);

        public string AutoRunPath { get; set; } = "";
        public string InstallPath { get; set; } = "";

        public DateTime NextUpdateCheck { get; set; } = DateTime.MinValue;
        public Version InstalledAppVersion { get; set; } = new Version(0, 0, 0);
        public FEPackManifest LastHeader { get; set; } = null;

        #region Static

        public static AppSettings Settings
        {
            get; private set;
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FS.AppSettings))
                {
                    using (FileStream stream = File.OpenRead(FS.AppSettings))
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
                using (FileStream stream = File.Create(FS.AppSettings))
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
