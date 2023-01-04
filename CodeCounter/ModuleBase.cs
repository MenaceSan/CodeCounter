//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections.Generic;
using System.IO;

namespace CodeCounter
{
    public abstract class ModuleBase
    {
        // define a module. base class for a package/lib (PackageReference) or a project (ProjectReference).
        // a module can contain source code files that contain classes.

        public string Name;         // Include="" from .csproj. May be upper case
        public string Version;      // 

        public string FilePath;     // full path to project file or binary for package.

        public SortedList<string, LicenseRef> Licenses = new SortedList<string, LicenseRef>();   // Referenced Licenses.
        public SortedList<string, PackageReference> PackageRefs = new SortedList<string, PackageReference>();   // Referenced / required lib/packages.
        public bool IsDisplayed { get; set; }   // for GraphViz

        public string NameShow;     // more friendly name.

        public int ReadProjectResult;         // HRESULT 2 = We found and read the project file. 1 = lib or package file.
 
        public abstract string ColorShow { get; }

        public ModuleBase(string name, string nameShow)
        {
            Name = name;
            NameShow = nameShow;
        }

        public void ShowGraphPackages(TextWriter con, string color)
        {
            // Deal with my package dependencies for GraphViz.
            foreach (var package in PackageRefs.Values)
            {
                package.ShowGraph(con);
                con.WriteLine($"{NameShow} -> {package.NameShow} {color};");
            }
        }

        public abstract void ReadFile(NameSpaces namespaces);
    }
}
