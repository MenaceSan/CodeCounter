//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.IO;

namespace CodeCounter
{
    // a NuGet package or binary library.
    // Hint = Directory for location of the binary package.
    // 1. Find the binary.
    // 2. 
    public class PackageReference : ModuleBase
    {
        // http://graphviz.org/doc/info/colors.html
        public const string kColorSystem = "[color=\"gold\"]";   // Microsoft supplied.
        public const string kColorPackage = "[color=\"tan1\"]";   // other binary package. Verbose will show (nuGet) packages as well.

        public ulong HashCode;  // This is the hashcode for the library. so we can pull license info from the license library.

        public override string ColorShow => GetColorShow();

        public string GetColorShow()
        {
            // Microsoft. or System. or special
            if (Name.StartsWith("Microsoft.") || Name.StartsWith("System.") || Name.StartsWith("MSTest.") || Name.StartsWith("Xamarin."))
            {
                return kColorSystem;
            }
            return kColorPackage;
        }

        public PackageReference(string name, string nameShow)
            : base(name, nameShow)
        {
        }

        internal void ShowGraph(TextWriter con)
        {
            if (IsDisplayed)
                return;
            IsDisplayed = true;
            string color = ColorShow;
            con.WriteLine($"{NameShow} {color}");
            ShowGraphPackages(con, color);
        }

        private bool FindFilePath()
        {
            // TODO: Find the binary for the library.
            //
            // Standard package locations for Windows: (https://lastexitcode.com/projects/NuGet/FileLocations/)
            // C:\Program Files (x86)\Microsoft SDKs\NuGetPackages
            // %LocalAppData%\NuGet\Cache
            // %UserProfile%\.nuget\packages

            if (FilePath != null)   // got it?
                return true;

            

            return false;   // No idea where the binary is.
        }

        public override void ReadFile(NameSpaces namespaces)
        {
            // Read the binary lib (PE). Get dependencies.
            // Try to find dependencies.

            if (!FindFilePath())
            {
                return;
            }

            // does the lib file exist ?

            // get the HashCode.

            ReadProjectResult = 1;
        }

        internal bool VerifyFile()
        {
            if (!FindFilePath())
            {
                return true;
            }

            // Does the HasCode match ?
            if (HashCode != 0)
            {
                // TODO
            }

            // Does the version match ?
            if (!string.IsNullOrWhiteSpace(Version))
            {

            }

            return true;  // matches.
        }
    }
}
