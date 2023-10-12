using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using K4os.Compression.LZ4.Internal;
using K4os.Compression.LZ4.Streams;
using MessagePack;
using Onova.Services;
using Serilog;
using ZeroElectric.Fenestra.Helpers;

namespace ZeroElectric.Fenestra
{
    public class Installer
    {
        public Thread BK_Thread;

        public volatile string AEPKGPath;
        public volatile FEPackManifest MPAKHeader;

        //
        // UPDATE
        //

        public void Update()
        {
            if (BK_Thread != null)
            {
                BK_Thread.Abort();
            }
            BK_Thread = new Thread(STAGE_UPADTE);
            BK_Thread.Start();

            Log.Debug("New work Thread started ID: {id}", BK_Thread.ManagedThreadId);
        }
        async void STAGE_UPADTE()
        {
            StageManager.StartStage(Stage.CheckingForUpdate);

            if (AppSettings.Settings.InstalledAppVersion != new Version(0, 0, 0))
            {
                StageManager.SetStageText("Checking for Updates [Stage 1/?]");
            }
            else
            {
                StageManager.SetStageText("Downloading [Stage 1/4]");
            }

            Log.Information("Checking for updates...");

            if (Program.DoUpdate)
            {
                var wpr = new WebPackageResolver(Program.AppManifest.appManifestURI);
                var versions = await wpr.GetPackageVersionsAsync();
                var maxVerAvl = versions.Max();

                AppSettings.Settings.NextUpdateCheck = DateTime.Now.AddMinutes(30);

                if (maxVerAvl > AppSettings.Settings.InstalledAppVersion)
                {
                    Log.Information("New App version is available, v{ver}", maxVerAvl);

                    StageManager.DisplayWork($"New version available [v{maxVerAvl}]...");

                    StageManager.StartStage(Stage.Downloading);

                    if (AppSettings.Settings.InstalledAppVersion != new Version(0, 0, 0))
                    {
                        StageManager.SetStageText("Downloading Update [Stage 1/4]");
                    }

                    // Update is available
                    var aepkgPath = Path.Combine(FS.Temp, $"{maxVerAvl}.aepkg");

                    StageManager.DisplayWork($"Downloading AEPKG...");
                    Log.Information("Downloading update...");

                    await wpr.DownloadPackageAsync(maxVerAvl, aepkgPath);

                    StageManager.DisplayWork($"Download complete...");
                    Log.Information("Updating...");

                    if (AppSettings.Settings.InstalledAppVersion != new Version(0, 0, 0))
                    {
                        StageManager.SetStageText("Installing Update [Stage 2/4]");
                    }
                    else
                    {
                        StageManager.SetStageText("Installing [Stage 2/4]");
                    }

                    AEPKGPath = aepkgPath;

                    STAGE_DECOMPRESS();
                }
                else
                {
                    Log.Information("{appname} is up to date", AppSettings.Settings.LastHeader.pkgName);
                }
            }
            else
            {
                Log.Information("{appname} is up to date, next check at: {datetime}", AppSettings.Settings.LastHeader.pkgName, AppSettings.Settings.NextUpdateCheck);
            }

            if (Program.DoDebug == false)
            {
                STAGE_LAUNCH();
            }
        }

        //
        // DECOMPRESS
        //

        long totlFilesMoved = 0;
        long compedFileOffset = 0;
        public void DecompressPAK()
        {
            if (BK_Thread != null)
            {
                BK_Thread.Abort();
            }
            BK_Thread = new Thread(STAGE_DECOMPRESS);
            BK_Thread.Start();

            Log.Debug("New work Thread started ID: {id}", BK_Thread.ManagedThreadId);
        }
        void STAGE_DECOMPRESS()
        {
            StageManager.StartStage(Stage.Decompressing);

            totlFilesMoved = 1;
            compedFileOffset = 0;

            var outputPath = "";
            var pkgPath = Path.Combine(FS.Output, "ze-demo-1.0.0.aepkg"); //TODO

            FEPack mPack;
            FEPackManifest packHeader;

            var tempPAKFile = Path.Combine(FS.Temp, "pakdata.temp");

            using (var output = File.Create(tempPAKFile))
            {
                StageManager.DisplayWork("Decompressing Package, this will take some time...");
                Log.Information("[PAK DECOMPRESS] Decompressing PKG");

                byte Header = 0;

                using (var input = File.OpenRead(pkgPath))
                {
                    var version = input.ReadByte();                 // VERSION  (byte)
                    if (version == 1)
                    {
                        Header = (byte)input.ReadByte();            // HEADER   (byte)

                        var isConpressed = GetBit(Header, 0, 0);
                        var compressedType = GetBit(Header, 1, 1);

                        Log.Debug("[PAK DECOMPRESS] PAKInfo, version: {ver}, header: {head}", version, Header);

                        input.ReadByte();                           // BLANK    (byte) 

                        if (isConpressed == 1)
                        {
                            if (compressedType == 1)
                            {
                                // LZMA Compression
                                LZMA.Decompress(input, output);
                            }
                            else
                            {
                                // LZ4 Compression
                                using (var decoder = LZ4Stream.Decode(input, Mem.M4))
                                {
                                    decoder.CopyTo(output);
                                }
                            }
                        }

                        StageManager.DisplayWork("Package Decompressed...");
                        Log.Information("[PAK DECOMPRESS] PKG decompress completed");

                        output.Flush();
                        output.Position = 0;

                        output.ReadByte();                              // BLANK    (byte)

                        var pakManifestSizeBuffer = new byte[4];
                        output.Read(pakManifestSizeBuffer, 0, 4);       // pak-manifest size (int)
                        var manifestSize = BitConverter.ToInt32(pakManifestSizeBuffer, 0);

                        output.ReadByte();                              // BLANK    (byte)

                        var pakdataSizeBuffer = new byte[8];
                        output.Read(pakdataSizeBuffer, 0, 8);           // pak-data size (long)
                        var dataEndOffset = BitConverter.ToInt64(pakdataSizeBuffer, 0);

                        output.ReadByte();                              // BLANK    (byte)

                        var pakManifestBin = new byte[manifestSize];
                        output.Read(pakManifestBin, 0, manifestSize);   // MemoryPack (pak-manifest)
                        mPack = MessagePackSerializer.Deserialize<FEPack>(pakManifestBin);

                        output.ReadByte();                              // BLANK    (byte)

                        StageManager.DisplayWork("Package Manifest Deserialized...");
                        Log.Information("[PAK DECOMPRESS] PKG-Manifest deserialized completed");

                        var offset = output.Position;

                        //

                        StageManager.StartStage(Stage.Installing);

                        packHeader = mPack.Manifest;
                        outputPath = Path.Combine(FS.BaseDirectory, $"{packHeader.pkgName}-{packHeader.pkgVer}");

                        //
                        // Directory Output
                        // 

                        foreach (var directory in mPack.Directories)
                        {
                            var fullPath = Path.Combine(outputPath, directory.RelativePath);

                            Directory.CreateDirectory(fullPath);

                            StageManager.DisplayWork($"Directory: [{directory.RelativePath}] created...");
                            Log.Debug("[PAK DECOMPRESS] Directory created\n|\tRelativePath: {rel}\n|\tPATH: {path}", directory.RelativePath, fullPath);
                        }

                        //
                        // File Output
                        //

                        var taskToRun = Environment.ProcessorCount / 2;
                        var mFiles = mPack.Files.Split(taskToRun);
                        var tasks = new Task[mFiles.Count()];

                        var i = -1;
                        foreach (var item in mFiles)
                        {
                            i++;

                            tasks[i] = new Task(outPutFiles, item);
                            tasks[i].Start();
                        }

                        Task.WaitAll(tasks);

                        void outPutFiles(object filesObj)
                        {
                            if (filesObj is IEnumerable<FEPackFile> files)
                            {
                                var uID = Ulid.NewUlid();

                                var tempFile = Path.Combine(FS.Temp, $"filedata-{Task.CurrentId}-{uID}.temp");

                                using (var tempData = File.Open(tempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                                {
                                    foreach (var file in files)
                                    {
                                        var fullPath = Path.Combine(outputPath, file.RelativePath);

                                        var useFileCompression = GetBit(Header, 2, 2);
                                        var fileCompressedType = GetBit(Header, 3, 3);

                                        updateStageText();

                                        Interlocked.Increment(ref totlFilesMoved);

                                        lock (tempData)
                                        {
                                            tempData.SetLength(0);
                                        }

                                        var lastByte = -1;
                                        long fileLength;
                                        lock (output)
                                        {
                                            output.Position = offset + file.Offset;
                                            for (var y = 0; y < file.Size; y++)
                                            {
                                                tempData.WriteByte((byte)output.ReadByte());
                                            }
                                            lastByte = output.ReadByte();
                                            fileLength = tempData.Length;
                                        }

                                        Interlocked.Add(ref compedFileOffset, fileLength + 1);

                                        if (lastByte == 0)
                                        {
                                            Log.Information($"!!File: [{file.Name}], Data appears to be correct!!");
                                        }
                                        else
                                        {
                                            Log.Warning("!!File: [{file.Name}], Data appears to be corrupted!!");
                                        }

                                        tempData.Flush();
                                        tempData.Position = 0;

                                        SHA1 shaHash = new SHA1CryptoServiceProvider();

                                        if (useFileCompression == 1)
                                        {
                                            using (var fileStream = File.Create(fullPath))
                                            {
                                                if (fileCompressedType == 1) // LZMA
                                                {
                                                    LZMA.Decompress(tempData, fileStream);
                                                }
                                                else // LZ4
                                                {
                                                    using (var decoder = LZ4Stream.Decode(tempData, Mem.M4, leaveOpen: true))
                                                    {
                                                        decoder.CopyTo(fileStream);
                                                    }
                                                }

                                                fileStream.Position = 0;

                                                shaHash.ComputeHash(fileStream);
                                                if (Enumerable.SequenceEqual(file.UncompressedHash, shaHash.Hash))
                                                {
                                                    Log.Information($"!!FileHash: [{file.Name}], Data appears to be correct!!");
                                                }
                                                else
                                                {
                                                    Log.Warning("!!FileHash: [{file.Name}], Data appears to be corrupted!!");
                                                }
                                            }
                                        }

                                        StageManager.DisplayWork($"Installed: [{file.Name}]");
                                        Log.Information("Output of {file} completed\n|\tRelativePath: {rel}\n|\tPATH: {path}", file.Name, file.RelativePath, fullPath);
                                    }
                                }
                            }

                            void updateStageText()
                            {
                                if (AppSettings.Settings.InstalledAppVersion != new Version(0, 0, 0))
                                {
                                    StageManager.SetStageText($"Installing Update [File {Interlocked.Read(ref totlFilesMoved) + 1}/{mPack.Files.Count}] [Stage 3/4]");
                                }
                                else
                                {
                                    StageManager.SetStageText($"Installing [File {Interlocked.Read(ref totlFilesMoved) + 1}/{mPack.Files.Count}] [Stage 3/4]");
                                }
                            }
                        }

                        //
                        // End of file check
                        //

                        output.Position = dataEndOffset + manifestSize + 4;

                        var pakEndBuffer = new byte[4];
                        output.Read(pakEndBuffer, 0, 4);
                        var EOFD = BitConverter.ToInt32(pakEndBuffer, 0);

                        if (EOFD == 0)
                        {
                            AppSettings.Settings.InstallPath = outputPath;
                            AppSettings.Settings.AutoRunPath = Path.Combine(outputPath, packHeader.autoRun);

                            MPAKHeader = packHeader;

                            if (Program.DoClean)
                            {
                                File.Delete(pkgPath);

                                STAGE_UPDATE_CLEAN();
                            }
                        }
                    }
                    else
                    {
                        StageManager.ShowError(""); //TODO Show Error
                    }
                }
            }

        }

        //
        // BUILD
        //

        public void BuildPAK()
        {
            if (BK_Thread != null)
            {
                BK_Thread.Abort();
            }
            BK_Thread = new Thread(STAGE_BUILD);
            BK_Thread.Start();

            Log.Debug("[PAK BUILD] New work Thread started ID: {id}", BK_Thread.ManagedThreadId);
        }
        void STAGE_BUILD()
        {
            StageManager.StartStage(Stage.BuildStarting);

            var pgkMan = Path.Combine(FS.Input, "aepkg.manifest");

            if (File.Exists(pgkMan))
            {
                using (var steam = File.OpenRead(pgkMan))
                {
                    MPAKHeader = JsonSerializer.Deserialize<FEPackManifest>(steam);
                }
            }
            else
            {
                Log.Fatal("[PAK BUILD] aepkg.manifest does not exists in imput folder");

                StageManager.ShowError(""); //TODO SHOW MESSAGE TO USER

                return; 
            }

            var tempPAKPath = Path.Combine(FS.Output, $"{MPAKHeader.output.pkgName}-{MPAKHeader.pkgVer}.data.temp");
            var outputPakPath = Path.Combine(FS.Output, $"{MPAKHeader.output.pkgName}-{MPAKHeader.pkgVer}.aepkg");

            var compression_LZ4_Settings = new LZ4EncoderSettings()
            {
                BlockSize = Mem.M1,
                ChainBlocks = true,
                CompressionLevel = K4os.Compression.LZ4.LZ4Level.L12_MAX,
                ExtraMemory = Mem.M1
            };

            var internalCompression_LZ4_Settings = new LZ4EncoderSettings()
            {
                BlockSize = Mem.K64,
                ChainBlocks = true,
                CompressionLevel = K4os.Compression.LZ4.LZ4Level.L12_MAX,
                ExtraMemory = Mem.K64
            };

            var mpak = new FEPack
            {
                Manifest = MPAKHeader
            };

            var PAK_HEADER_BYTE = new byte();

            try
            {
                //
                // File Directories
                //

                #region Directories

                var alldirs = Directory.GetDirectories(FS.Input, "*", SearchOption.AllDirectories);

                if (alldirs.Length > 0)
                {
                    for (var i = 0; i < alldirs.Length; i++)
                    {
                        var dir = alldirs[i];

                        var dirInfo = new DirectoryInfo(dir);

                        var relPath = GetRelativePath(dir, FS.Input);

                        relPath += Path.DirectorySeparatorChar;

                        mpak.Directories.Add(new FEPackDirectory { Name = dirInfo.Name, RelativePath = relPath });

                        Log.Debug("[PAK BUILD] New directory found!\n\tName:{name}\n\tPath:{path}", dirInfo.Name, relPath);
                    }
                }
                else
                {
                    Log.Debug("[PAK BUILD] No directory found!");
                }

                #endregion

                //
                // File Compression
                //

                StageManager.StartStage(Stage.CompressingFiles);

                #region File Compression 

                var files = Directory.GetFiles(FS.Input, "*", SearchOption.AllDirectories);
                var compfiles = new List<(FEPackFile, FileInfo)>();

                long dataFileOffset = 0;
                for (var i = 0; i < files.Length; i++)
                {
                    var file = files[i];
                    var fileInfo = new FileInfo(file);

                    if (fileInfo.Extension != ".pdb" && fileInfo.Name != "aepkg.manifest")
                    {
                        var uID = Ulid.NewUlid();

                        var relPath = GetRelativePath(file, FS.Input);
                        var compPath = Path.Combine(FS.TempBuild, uID.ToString() + ".lz4");

                        FileInfo info = null;
                        SHA1 shaHash = new SHA1CryptoServiceProvider();

                        if (Program.InternalCompression)
                        {
                            switch (Program.InternalCompressionType)
                            {
                                case CompressionType.LZ4:
                                    {
                                        // Compress File by LZ4
                                        using (var inputFile = fileInfo.Open(FileMode.Open))
                                        using (var aepkgOutput = File.Create(compPath))
                                        using (var decoder = LZ4Stream.Encode(aepkgOutput, internalCompression_LZ4_Settings))
                                        {
                                            Log.Debug("[PAK BUILD] Compressing file: {name}", fileInfo.Name);

                                            inputFile.CopyTo(decoder);

                                            inputFile.Position = 0;
                                            shaHash.ComputeHash(inputFile);
                                        }

                                        SetBit(ref PAK_HEADER_BYTE, 4, 4, 0);
                                        SetBit(ref PAK_HEADER_BYTE, 5, 5, 1);

                                        break;
                                    }
                                case CompressionType.LZMA:
                                    {
                                        // Compress File by LZMA
                                        using (var inputFile = fileInfo.Open(FileMode.Open))
                                        using (var aepkgOutput = File.Create(compPath))
                                        {
                                            LZMA.Compress(inputFile, aepkgOutput);

                                            inputFile.Position = 0;
                                            shaHash.ComputeHash(inputFile);
                                        }

                                        SetBit(ref PAK_HEADER_BYTE, 4, 4, 1);
                                        SetBit(ref PAK_HEADER_BYTE, 5, 5, 1);

                                        break;
                                    }
                            }

                            info = new FileInfo(compPath);
                        }
                        else
                        {
                            info = fileInfo;
                        }

                        var mPAK = new FEPackFile { Name = fileInfo.Name, RelativePath = relPath, Size = info.Length, Offset = dataFileOffset, UncompressedHash = shaHash.Hash };

                        compfiles.Add((mPAK, info));
                        mpak.Files.Add(mPAK);

                        Log.Debug("[PAK BUILD] File {name} was Compress\n\tFrom: {path}\n\tTo: {compPath}\n\tInfo: UnCompFileSize:{unsize}, CompFileSize:{compFileInfo}, PAKOffset:{fileOffset}",
                            fileInfo.Name, relPath, compPath, fileInfo.Length, info.Length, dataFileOffset);

                        Interlocked.Add(ref dataFileOffset, info.Length + 1);
                    }
                    else
                    {
                        Log.Debug("[PAK BUILD] File {name} was skiped", fileInfo.Name);
                    }
                }

                #endregion

                //
                // Start of PAK Write 
                //

                StageManager.StartStage(Stage.BuildingPAK);

                #region PAK-Manifest

                var stream = File.Create(tempPAKPath);

                if (stream.CanWrite == false)
                {
                    stream.Close();
                    return;
                }

                var bin = MessagePackSerializer.Serialize(mpak);

                var pakSize = BitConverter.GetBytes(bin.Length);
                var offsetSize = BitConverter.GetBytes(dataFileOffset + bin.Length + 4);

                Log.Debug("[PAK BUILD] Writting file.. PAK Size: {name}", pakSize.Length);

                stream.WriteByte(0b_0000_0000);                             // BLANK

                stream.Write(pakSize, 0, pakSize.Length);                   // pak-manifest size (int)

                stream.WriteByte(0b_0000_0000);                             // BLANK

                stream.Write(offsetSize, 0, offsetSize.Length);             // pak-data size (long)

                stream.WriteByte(0b_0000_0000);                             // BLANK

                stream.Write(bin, 0, bin.Length);                           // MessagePack (PakManifesest)

                stream.WriteByte(0b_0000_0000);                             // BLANK

                Log.Debug("[PAK BUILD] Writting PakManifesest, Size: {name}", bin.Length);

                #endregion

                //
                // Start of pak-data
                //

                for (var i = 0; i < compfiles.Count; i++)
                {
                    var (mPAK, fileInfo) = compfiles[i];

                    using (var comFileSteam = fileInfo.OpenRead())
                    {
                        comFileSteam.CopyTo(stream);                        // byte[MessagePack]  (pak-MPack)[]

                        Log.Debug("[PAK BUILD] Writting compressed file {name}, Size: {size}, Offset{offset}", fileInfo.Name, fileInfo.Length, mPAK.Offset);
                    }

                    stream.WriteByte(0b_0000_0000);                         // BLANK
                }

                stream.WriteByte(0b_0000_0000);                             // BLANK
                stream.WriteByte(0b_0000_0000);                             // BLANK
                stream.WriteByte(0b_0000_0000);                             // BLANK
                stream.WriteByte(0b_0000_0000);                             // BLANK

                //
                // File hedder, Finalization & PAK Compression
                //

                StageManager.StartStage(Stage.CompressingPAK);

                using (var aepkgOutput = File.Create(outputPakPath))
                {
                    aepkgOutput.WriteByte(0b_0000_0001);                     // VERSION

                    if (Program.UseCompression && Program.CompressionType != CompressionType.None)
                    {
                        Log.Information("[PAK BUILD] Compressing PAK, Size: {size}, Type: {compType}\n\tPath: {path}", stream.Length, Program.CompressionType, tempPAKPath);

                        switch (Program.CompressionType)
                        {
                            case CompressionType.LZMA:
                                {
                                    SetBit(ref PAK_HEADER_BYTE, 7, 7, 1);
                                    SetBit(ref PAK_HEADER_BYTE, 6, 6, 1);

                                    aepkgOutput.WriteByte(PAK_HEADER_BYTE);  // HEADER (0b_1100_0000) File is Compressed, & is type LZMA
                                    aepkgOutput.WriteByte(0b_0000_0000);     // BLANK   

                                    LZMA.Compress(stream, aepkgOutput);

                                    break;
                                }
                            case CompressionType.LZ4:
                                {
                                    SetBit(ref PAK_HEADER_BYTE, 7, 7, 1);
                                    SetBit(ref PAK_HEADER_BYTE, 6, 6, 0);

                                    aepkgOutput.WriteByte(PAK_HEADER_BYTE);  // HEADER (0b_1000_0000) File is Compressed, & is type LZ4
                                    aepkgOutput.WriteByte(0b_0000_0000);     // BLANK

                                    using (var decoder = LZ4Stream.Encode(aepkgOutput, compression_LZ4_Settings, leaveOpen: true))
                                    {
                                        stream.Position = 0;
                                        stream.CopyTo(decoder);
                                    }

                                    break;
                                }
                        }

                        Log.Information("[PAK BUILD] PAK Compressed! NewSize: {size}, Type: {compType}\n\tPath: {path}", aepkgOutput.Length, Program.CompressionType, outputPakPath);
                    }
                    else
                    {
                        aepkgOutput.WriteByte(0b_0000_0000);                // HEADER (0b_0000_0000) File is not Compressed
                        aepkgOutput.WriteByte(0b_0000_0000);                // BLANK 

                        stream.Position = 0;
                        stream.CopyTo(aepkgOutput);
                    }

                    Log.Information("[PAK BUILD] Build Complete! Size: {size}\n\tPath: {path}", aepkgOutput.Length, outputPakPath);

                    aepkgOutput.Flush();
                    aepkgOutput.Close();
                }

                //

                stream.Flush();
                stream.Close();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[PAK BUILD] Exception while building AEPKG");
            }

            GC.Collect(3);
            GC.Collect(2);

            string GetRelativePath(string filespec, string folder)
            {
                // Folders must end in a slash
                if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    folder += Path.DirectorySeparatorChar;
                }

                return Uri.UnescapeDataString(new Uri(folder).MakeRelativeUri(new Uri(filespec)).ToString().Replace('/', Path.DirectorySeparatorChar));
            }
        }

        //
        // CLEAN & LAUNCH
        //

        public void Clean()
        {
            if (BK_Thread != null)
            {
                BK_Thread.Abort();
            }
            BK_Thread = new Thread(STAGE_UPDATE_CLEAN);
            BK_Thread.Start();

            Log.Debug("New work Thread started ID: {id}", BK_Thread.ManagedThreadId);
        }
        void STAGE_UPDATE_CLEAN()
        {
            StageManager.StartStage(Stage.Cleaning);

            StageManager.ClearWorkText();
            StageManager.SetStageText($"Cleaning [Stage 4/4]");

            AppSettings.Settings.InstalledAppVersion = MPAKHeader.pkgVer;
            AppSettings.Settings.LastHeader = MPAKHeader;

            AppSettings.Save();

            FS.ClearTemp();

            GC.Collect(3);
            GC.Collect(2);
            GC.Collect(1);
        }

        public void Launch()
        {
            STAGE_LAUNCH();
        }
        void STAGE_LAUNCH()
        {
            StageManager.StartStage(Stage.LaunchingAPP);

            if (string.IsNullOrEmpty(AppSettings.Settings.AutoRunPath) == false)
            {
                Log.Debug("Launching app, {apppath}", AppSettings.Settings.AutoRunPath);
            
                StageManager.ClearWorkText();
                StageManager.SetStageText($"Launching [{AppSettings.Settings.LastHeader.pkgName}]...");

                if (string.IsNullOrEmpty(Program.AppArgs))
                {
                    Process.Start(AppSettings.Settings.AutoRunPath);
                }
                else
                {
                    Process.Start(AppSettings.Settings.AutoRunPath, Program.AppArgs);
                }

                if (Program.DoDebug == false)
                {
                    Environment.Exit(0);
                }
            }
            else
            {
                Log.Fatal("For some reason AutoRunPath is empty");

                MessageBox.Show("Fenestra could not find EXE.\n AEPKG was invallid. Could not find EXE!", "Fenestra [Fatal Error]!", MessageBoxButton.OK);

                if (Program.DoDebug == false)
                {
                    Environment.Exit(1);
                }
            }
        }

        //
        //

        static byte GetBit(byte bytes, int start, int end)
        {
            // Calculate the number of bits to read
            var n = end - start + 1;

            // Create a mask of n ones at the given range
            var mask = (byte)(((1 << n) - 1) << (7 - end));

            // Bitwise and the byte and the mask and right shift by the start index
            var result = (byte)((bytes & mask) >> (7 - end));

            // Return the result
            return result;
        }
        static byte SetBit(ref byte b, int start, int end, int value)
        {
            // Create a mask with the bits from start to end set to 1
            var mask = ((1 << (end - start + 1)) - 1) << start;

            // Clear the bits from start to end in the byte
            b &= (byte)~mask;

            // Set the bits from start to end in the byte according to the value
            b |= (byte)((value << start) & mask);

            // Return the modified byte
            return b;
        }
    }
}