﻿namespace ServiceControl.Plugin
{
    using NServiceBus;
    using System;
    using System.Diagnostics;
        
    class VersionChecker
    {
        static VersionChecker()
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(typeof(IMessage).Assembly.Location);

            CoreFileVersion = new Version(fileVersion.FileMajorPart, fileVersion.FileMinorPart,
                fileVersion.FileBuildPart);
        }

        public static Version CoreFileVersion { get; set; }

        public static bool CoreVersionIsAtLeast(int major, int minor)
        {
            if (CoreFileVersion.Major > major)
            {
                return true;
            }

            if (CoreFileVersion.Major < major)
            {
                return false;
            }

            return CoreFileVersion.Minor >= minor;
        }
    }
}