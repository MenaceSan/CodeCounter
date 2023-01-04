//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
//
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;

namespace CodeCounter
{
    /// <summary>
    /// The package library. All the packages I know about.
    /// </summary>
    public static class PackageLib
    {
        public static SortedList<ulong, PackageReference> Hashes;     // The package library. License info for libs we know about. (and might reference)
        public static SortedList<string, PackageReference> Names;    // For reference of packages by name (and version).

        public static SortedList<string, string> Ignored;    // lower case. ignore these system libraries.

        private static void LoadPackageLib()
        {
            // Attempt to learn more about this package.

            if (Hashes != null)
                return;

            // Read PackageIgnore
            var lines = File.ReadLines("PackageIgnore.ini");
            if (lines == null)
            {

            }
            else
            {
                Ignored = new SortedList<string, string>();

                foreach (var line in lines)
                {
                    if (line.StartsWith(";"))
                        continue;
                    string line2 = line.Trim();
                    if (string.IsNullOrEmpty(line2))
                        continue;
                    Ignored.Add(Path.GetFileNameWithoutExtension(line2).ToLower(), line2);
                }
            }
        }

        public static bool IsIgnored(string nameL)
        {
            if (string.IsNullOrWhiteSpace(nameL) )   // ignore these.
                return true;
            LoadPackageLib();
            return Ignored.ContainsKey(nameL);
        }

        public static PackageReference FindPackageByName(string nameL)
        {
            // Do i already know about this pacakge ?

            LoadPackageLib();
            return null;
        }

        public static PackageReference FindPackage(string name, string version)
        {
            LoadPackageLib();
            return null;
        }

        public static PackageReference FindPackageHash(ulong hashCode)
        {
            LoadPackageLib();

            return null;
        }
    }
}
