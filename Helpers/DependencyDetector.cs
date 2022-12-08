using System.IO;
using System.Text;

namespace ZeroElectric.Fenestra
{
    internal class DependencyDetector
    {
        const string NetPath = @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App";

        public bool DetectNetCore(string netVer)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (Directory.Exists(NetPath))
            {
                string[] dirs = Directory.GetDirectories(NetPath);
                for (int i = 0; i < dirs.Length; i++)
                {
                    stringBuilder.Append(dirs[i]);
                }
            }

            return stringBuilder.ToString().Contains(netVer);
        }

    }
}
