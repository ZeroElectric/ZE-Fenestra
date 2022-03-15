using MessagePack;
using Onova.Services;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AE.Ingredior
{
    public class Installer
    {

        public async Task RunAppAsync()
        {
            if (Program.AppManifest == null || string.IsNullOrEmpty(Program.AppManifest.updateURL))
            {
                Log.Fatal("AppManifest not found, Ingredior is not setup for install mode");

                Environment.Exit(0);
            }

            if(Program.DoUpdate)
            {
                var wpr = new WebPackageResolver(Program.AppManifest.updateURL);

                Log.Debug("Checking for updates...");

                var versions = await wpr.GetPackageVersionsAsync();

                var maxVerAvl = versions.Max();
            
                AppSettings.Settings.NextUpdateCheck = DateTime.Now.AddMinutes(30);

                if (maxVerAvl > AppSettings.Settings.InstalledVersion)
                {
                    Log.Information("New App version is available, v{ver}", maxVerAvl);

                    // Update is available
                    var aepkgPath = Path.Combine(FileSystemHelper.Temp, $"{maxVerAvl}.aepkg");

                    Log.Debug("Downloading update...");

                    await wpr.DownloadPackageAsync(maxVerAvl, aepkgPath);

                    Log.Information("Updating...");

                    var header = await DeserializedAndOutputPkg(aepkgPath);

                    AppSettings.Settings.LastHeader = header;
                    AppSettings.Settings.InstalledVersion = maxVerAvl;

                    // TODO Cean up temp files, last installed app folder and run SpecialDirectories

                    File.Delete(aepkgPath);

                    GC.Collect(3);
                }
                else
                {
                    Log.Information("{appname} is up to date", AppSettings.Settings.LastHeader.pkgName);
                }

                AppSettings.Save();
            }
            else
            {
                Log.Information("{appname} is up to date, next check at: {datetime}", AppSettings.Settings.LastHeader.pkgName, AppSettings.Settings.NextUpdateCheck);
            }

            if (string.IsNullOrEmpty(AppSettings.Settings.AutoRunPath) == false)
            {
                Log.Debug("Launching app, {apppath}", AppSettings.Settings.AutoRunPath);

                Process process;

                if (string.IsNullOrEmpty(Program.AppArgs))
                {
                    process = Process.Start(AppSettings.Settings.AutoRunPath);
                }
                else
                {
                    process = Process.Start(AppSettings.Settings.AutoRunPath, Program.AppArgs);
                }

                Task.Delay(1500).Wait();

                Environment.Exit(0);
            }
            else
            {
                Log.Fatal("For some reason AutoRunPath is empty");

                Environment.Exit(0);
            }
        }

        public Task<MPackHeader> DeserializedAndOutputPkg(string pkgPath)
        {
            MPack mPack;
            MPackHeader packHeader;
            string endOutputPath;

            #region Decompress & Deserialize

            var tempFile = Path.Combine(FileSystemHelper.Temp, "aepkg.temp");

            using (FileStream tempOutput = File.Create(tempFile))
            using (FileStream pkgInput = File.OpenRead(pkgPath))
            {
                Log.Debug("Decompressing AEPKG...");

                LZMA.Decompress(pkgInput, tempOutput);

                //ICSharpCode.SharpZipLib.BZip2.BZip2.Decompress(pkgInput, tempOutput, false);

                Log.Debug("AEPKG decompressed");

                tempOutput.Seek(0, SeekOrigin.Begin);

                mPack = MessagePackSerializer.Deserialize<MPack>(tempOutput);

                Log.Debug("AEPKG deserialized");
            }

            File.Delete(tempFile);

            #endregion

            GC.Collect(3);

            packHeader = mPack.Header;

            endOutputPath = Path.Combine(FileSystemHelper.BaseDirectory, $"{packHeader.pkgName}-{packHeader.pkgVer}");

            // Directory Output

            foreach (var directory in mPack.Directories)
            {
                var fullPath = Path.Combine(endOutputPath, directory.RelativePath);

                Directory.CreateDirectory(fullPath);

                Log.Debug("Directory created\n|\tRelativePath: {rel}\n|\tPATH: {path}", directory.RelativePath, fullPath);
            }

            // File Output

            for (int i = 0; i < mPack.Files.Count; i++)
            {
                MPackFile file = mPack.Files[i];

                var fullPath = Path.Combine(endOutputPath, file.RelativePath);

                using (FileStream fileStream = File.Create(fullPath))
                {
                    fileStream.Write(file.Data, 0, file.Data.Length);
                    fileStream.Flush();
                }

                Log.Debug("Output of {file} completed\n|\tRelativePath: {rel}\n|\tPATH: {path}", file.Name, file.RelativePath, fullPath);

                file = null;
            }

            Log.Debug("AEPKG output completed, {path}", endOutputPath);

            AppSettings.Settings.InstallPath = endOutputPath;
            AppSettings.Settings.AutoRunPath = Path.Combine(endOutputPath, packHeader.autoRun);

            mPack = null;

            return Task.FromResult(packHeader);
        }

        public Task BuildPkg()
        {
            MPackHeader header;
            var pgkMan = Path.Combine(FileSystemHelper.Input, "aepkg.manifest");

            if (File.Exists(pgkMan))
            {
                try
                {
                    using (var steam = File.OpenRead(pgkMan))
                    {
                        header = JsonSerializer.Deserialize<MPackHeader>(steam);
                    }

                    Compress(header);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Exception while building AEPKG");
                }
            }
            else
            {
                Log.Fatal("aepkg.manifest does not exists in imput folder");
            }

            return Task.CompletedTask;
        }

        public Task Compress(MPackHeader header)
        {
            MPack mPack = new MPack
            {
                Header = header
            };

            string outputPath = Path.Combine(FileSystemHelper.Output, $"{header.output.pkgName}-{header.pkgVer}.aepkg");

            // Get all directories

            var alldirs = Directory.GetDirectories(FileSystemHelper.Input, "*", SearchOption.AllDirectories);

            if (alldirs.Length > 0)
            {
                foreach (var dir in alldirs)
                {
                    var dirInfo = new DirectoryInfo(dir);

                    var relPath = GetRelativePath(dir, FileSystemHelper.Input);

                    relPath += Path.DirectorySeparatorChar;

                    mPack.Directories.Add(new MPackDirectory { Name = dirInfo.Name, RelativePath = relPath });

                    Log.Debug("Directories {name} Found, Path: {path}", dirInfo.Name, relPath);
                }
            }

            Log.Debug("All Directories in 'Imput' have been added, Directory Count: {count}", mPack.Directories.Count);

            // Get all bytes from files 

            var files = Directory.GetFiles(FileSystemHelper.Input, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                FileInfo fileInfo = new FileInfo(file);

                if (fileInfo.Extension != ".pdb" && fileInfo.Name != "aepkg.manifest")
                {
                    var relPath = GetRelativePath(file, FileSystemHelper.Input);

                    mPack.Files.Add(new MPackFile { Name = fileInfo.Name, RelativePath = relPath, Data = File.ReadAllBytes(file) });

                    Log.Debug("File {name} was processed\n\tPath: {path}", fileInfo.Name, relPath);
                }
                else
                {
                    Log.Debug("File {name} was skiped", fileInfo.Name);
                }
            }

            Log.Debug("All files in 'Imput' have been processed, file count: {count}", mPack.Files.Count);

            //
            // MPack File
            //

            var tempFile = Path.Combine(FileSystemHelper.Temp, "mpack.temp");

            using (FileStream tempInput = File.Create(tempFile))
            using (FileStream aepkgOutput = File.Create(outputPath))
            {
                MessagePackSerializer.Serialize(tempInput, mPack);

                Log.Debug("Serialization has finished, length: {len} :: capacity: {cap}", tempInput.Length);

                mPack = null;

                tempInput.Seek(0, SeekOrigin.Begin);
                aepkgOutput.Seek(0, SeekOrigin.Begin);

                LZMA.Compress(tempInput, aepkgOutput);

                //ICSharpCode.SharpZipLib.BZip2.BZip2.Compress(input, mpOSteam, true, level: 9);
            }

            Log.Debug("AEPKG file saved\n\tPath:{path}", outputPath);

            return Task.CompletedTask;
        }

        string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
