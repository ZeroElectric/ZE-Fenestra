using MessagePack;
using System;
using System.Collections.Generic;

namespace ZeroElectric.Fenestra
{
    [MessagePackObject]
    public class FEPack
    {
        [MessagePack.Key(0)]
        public FEPackManifest Manifest { get; set; }

        [MessagePack.Key(1)]
        public List<FEPackDirectory> Directories { get; set; } = new List<FEPackDirectory>();

        [MessagePack.Key(2)]
        public List<FEPackFile> Files { get; set; } = new List<FEPackFile>();
    }

    //
    // Manifest
    //

    [MessagePackObject]
    public class FEPackManifest
    {
        [MessagePack.Key(0)]
        public string pkgName { get; set; }

        [MessagePack.Key(1)]
        public Version pkgVer { get; set; }

        [MessagePack.Key(2)]
        public string autoRun { get; set; }

        [MessagePack.Key(3)]
        public string netDependency { get; set; }

        [MessagePack.Key(4)]
        public OutputDefinition output { get; set; }

        [MessagePack.Key(5)]
        public SpecialDirectory[] SpecialDirectories { get; set; }
    }

    [MessagePackObject]
    public class OutputDefinition
    {
        [MessagePack.Key(0)]
        public string launcherName { get; set; }

        [MessagePack.Key(1)]
        public string installerName { get; set; }

        [MessagePack.Key(2)]
        public string pkgName { get; set; }

        [MessagePack.Key(3)]
        public string shortcutName { get; set; }
    }

    [MessagePackObject]
    public class SpecialDirectory
    {
        [MessagePack.Key(0)]
        public string dirPath { get; set; }

        [MessagePack.Key(1)]
        public FEPackOutputStrategy outputStrategy { get; set; }

        [MessagePack.Key(2)]
        public FEPackUpdateStrategy updateStrategy { get; set; }
    }

    public enum FEPackOutputStrategy
    {
        AppRelative,
        LauncherRelative
    }
    public enum FEPackUpdateStrategy
    {
        Replace, 
        CleanAndReplace
    }

    //
    // File / Directory
    //

    [MessagePackObject]
    public class FEPackDirectory
    {
        [MessagePack.Key(0)]
        public string Name { get; set; }

        [MessagePack.Key(1)]
        public string RelativePath { get; set; }
    }

    [MessagePackObject]
    public class FEPackFile
    {
        [MessagePack.Key(0)]
        public string Name { get; set; }

        [MessagePack.Key(1)]
        public string RelativePath { get; set; }

        [MessagePack.Key(2)]
        public long Size { get; set; }

        [MessagePack.Key(3)]
        public long Offset { get; set; }

        [MessagePack.Key(4)]
        public byte[] UncompressedHash { get; set; }
    }
}
