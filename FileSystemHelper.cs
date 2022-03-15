using System;
using System.IO;

namespace AE.Ingredior
{
    public static class FileSystemHelper
    {
        //
        // Directories
        //

        public static string BaseDirectory { get; } = AppDomain.CurrentDomain.BaseDirectory;
        public static string AEI { get; } = GetDirectory(BaseDirectory, ".aei");

        public static string Input { get; } = GetDirectory(AEI, "Input");
        public static string Output { get; } = GetDirectory(AEI, "Output");

        public static string Temp { get; } = GetDirectory(AEI, "Temp");
        public static string TempLog { get; } = GetDirectory(Temp, "Logs");

        //
        // Files
        //

        public static string AppManifest { get; } = Path.Combine(BaseDirectory, "ingredior.manifest");
        public static string AppSettings { get; } = Path.Combine(BaseDirectory, "ingredior.data");

        //

        public static string GetDirectory(params string[] path)
        {
            if (path != null && path.Length > 0)
            {
                var dirPath = Path.Combine(path);

                if (Directory.Exists(dirPath) == false)
                {
                    Directory.CreateDirectory(dirPath);
                }

                return dirPath;
            }

            return string.Empty;
        }

        public static string GetFile(string fileName, bool clearFile, params string[] path)
        {
            if (path != null && path.Length > 0)
            {
                var dirPath = Path.Combine(path);

                if (Directory.Exists(dirPath) == false)
                {
                    Directory.CreateDirectory(dirPath);
                }

                var filePath = Path.Combine(dirPath, fileName);

                if (clearFile || File.Exists(filePath) == false)
                {
                    using (FileStream stream = File.Create(filePath))
                    {
                        stream.Flush();
                    }
                }

                return filePath;
            }

            return string.Empty;
        }
    }
}