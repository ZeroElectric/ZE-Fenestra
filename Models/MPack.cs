using MessagePack;
using System;
using System.Collections.Generic;

namespace ZeroElectric.Fenestra
{
    [MessagePackObject]
    public class MPack
    {
        [MessagePack.Key(0)]
        public MPackManifest Manifest { get; set; }

        [MessagePack.Key(1)]
        public List<MPackDirectory> Directories { get; set; } = new List<MPackDirectory>();

        [MessagePack.Key(2)]
        public List<MPackFile> Files { get; set; } = new List<MPackFile>();
    }

    #region Header

    [MessagePackObject]
    public class MPackManifest
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
        public MPackOutput output { get; set; }

        [MessagePack.Key(5)]
        public SpecialDirectory[] SpecialDirectories { get; set; }
    }

    [MessagePackObject]
    public class MPackOutput
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
        public MPackOutputStrategy outputStrategy { get; set; }

        [MessagePack.Key(2)]
        public MPackUpdateStrategy updateStrategy { get; set; }
    }

    public enum MPackOutputStrategy
    {
        AppRelative, LauncherRelative
    }
    public enum MPackUpdateStrategy
    {
        Replace, CleanAndReplace
    }

    #endregion

    [MessagePackObject]
    public class MPackDirectory
    {
        [MessagePack.Key(0)]
        public string Name { get; set; }

        [MessagePack.Key(1)]
        public string RelativePath { get; set; }
    }

    [MessagePackObject]
    public class MPackFile
    {
        [MessagePack.Key(0)]
        public string Name { get; set; }

        [MessagePack.Key(1)]
        public string RelativePath { get; set; }

        [MessagePack.Key(2)]
        public byte[] Data { get; set; }
    }
}
