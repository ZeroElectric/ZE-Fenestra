using System;

namespace AE.Ingredior
{
    public class AppManifest
    {
        public string updateURL { get; set; }
        public string launcherUpdate { get; set; }
        public string outputTemplate { get; set; }

        public Version appVer { get; set; }
        public string launcherName { get; set; }
    }
}
