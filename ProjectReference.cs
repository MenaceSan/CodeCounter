﻿//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections.Generic;
using System.IO;

namespace CodeCounter
{
    /// <summary>
    /// A project that might own namespaces.
    /// It has dependencies.
    /// </summary>
    public class ProjectReference : ModuleBase
    {
        public string DirBase;      // (lower case) Project owns all files under this directory. (except if claimed by a sub project. which is a bad idea but allow it)
        public bool IsExe;      // is Library or Exe ?
        public bool IsTest;     // Has *Test* in name.

        // http://graphviz.org/doc/info/colors.html
        const string colorFail = "[color=\"red1\"]";   // 0.002 0.999 0.999
        const string colorExe = "[color=\"yellow1\"]";   // top level entry point. 0.348 0.348 0.999
        const string colorProject = "[color=\"green2\"]"; // 0.348 0.839 0.839
        const string colorLib = "[color=\"royalblue\"]"; // blue 0.650 0.700 0.700

        const string colorTest = "[color=\"gray53\"]";   // 

        public string ColorShow => FailRead ? colorFail : IsTest ? colorTest : IsExe ? colorExe : IsRead ? colorProject : colorLib;

        public bool IsDisplayed { get; set; }
        public bool IsRead { get; set; }         // We found and read the project file.
        public bool FailRead;

        // Declared refs vs used refs. SortedList always lower case sorted.
        public SortedList<string, ProjectReference> ProjectRefs = new SortedList<string, ProjectReference>();      // projects declared in .csproj file.
        public HashSet<NameSpaceLevel> NameSpacesUsed = new HashSet<NameSpaceLevel>();         // namespaces used that are not defined in my project.

        public ProjectReference(string dir, string name)
            : base(Path.GetFileNameWithoutExtension(name))
        {
            DirBase = dir;
            IsTest = name.IndexOf("Test", 0, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void TrimNameSpacesUsed()
        {
            // remove used namespaces that i also declared
            NameSpacesUsed.RemoveWhere(x => x.Project == this);
        }

        public string FindAttr(string lineRaw, string name)
        {
            // Get XML attribute by name.

            name += "=";
            int i = lineRaw.IndexOf(name);
            if (i < 0)
                return null;
            i += name.Length;
            int j = lineRaw.IndexOf('\"', i);   // open quote
            if (j < 0)
                return null;
            i = j + 1;
            j = lineRaw.IndexOf('\"', i);   // close quote
            if (j < 0)
                return null;
            return lineRaw.Substring(i, j - i);
        }

        const string cmdExe = "<OutputType>Exe</OutputType>";

        public void ReadProjectFile(NameSpaces namespaces, string dirProject, string fileName)
        {
            // read the project file (.csproj) and get my dependent ProjectReference and PackageReference.
            // <Project Sdk=\"Microsoft.NET.Sdk\">"

            if (IsRead || FailRead)
                return;

            string filePath = Path.Combine(dirProject, fileName);

            try
            {
                using (var rdr = new StreamReader(filePath))
                {
                    IsRead = true;
                    string lineRaw;
                    while ((lineRaw = rdr.ReadLine()) != null)
                    {
                        if (lineRaw.Contains(cmdExe))
                        {
                            IsExe = true;
                            continue;
                        }
                        if (lineRaw.Contains("<PackageReference"))   // Visual Studio "Core" Style 
                        {
                            // <PackageReference Include="Plugin.Fingerprint" Version="2.1.1" />
                            string name = FindAttr(lineRaw, "Include");
                            if (name == null || PackageRefs.ContainsKey(name))
                                continue;
                            PackageRefs.Add(name, namespaces.AddPackageRef(name));
                            continue;
                        }
                        if (lineRaw.Contains("<ProjectReference"))  // Visual Studio "Core" Style 
                        {
                            // <ProjectReference
                            string nameRaw = FindAttr(lineRaw, "Include");
                            if (nameRaw == null)
                                continue;
                            string dir = Path.GetDirectoryName(nameRaw);
                            string name = Path.GetFileName(nameRaw);
                            string nameL = name.ToLower();
                            if (ProjectRefs.ContainsKey(nameL))
                                continue;
                            ProjectRefs.Add(nameL, namespaces.AddProjectRef(dir, name));
                            continue;
                        }
                    }
                }
            }
            catch
            {
                // TODO: Log error 
                FailRead = true;
            }
        }

        public void AddUsed(NameSpaceLevel level)
        {
            // we use this namespace.
            if (level == null)
                return;
            if (level.Project == this)  // don't bother recording a ref to something i declared
                return;
            NameSpacesUsed.Add(level);
        }

        internal void ShowGraph0(TextWriter con)
        {
            // Deal with my dependencies first.
            if (IsDisplayed)
                return;
            IsDisplayed = true;
            con.WriteLine($"{NameShow} {ColorShow}");
            foreach (var project in ProjectRefs.Values)
            {
                project.ShowGraph0(con);
                con.WriteLine($"{NameShow} -> {project.NameShow} {ColorShow};");
            }

            // if verbose ?
            foreach (var package in PackageRefs.Values)
            {

            }
        }
    }
}
