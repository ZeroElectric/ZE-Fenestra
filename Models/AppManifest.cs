using System;

namespace ZeroElectric.Fenestra
{
    public class AppManifest
    {
        public Version launcherVer { get; set; }

        public string appManifestURI { get; set; }
        public string launcherManifestURI { get; set; }
        public string outputTemplate { get; set; }
    }
}