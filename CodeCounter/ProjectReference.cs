//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;
using System.IO;

namespace CodeCounter
{
    /// <summary>
    /// A project that might own namespaces and have sources. ".csproj",  ".vcxproj" 
    /// read this before associated sources so we can get include paths etc.
    /// 
    /// It has dependencies.
    /// </summary>
    public class ProjectReference : ModuleBase
    {
        public bool IsExe;          // is Exe or/else Library ?
        public bool IsTest;         // Has *Test* in its name.

        public List<CodeClass> Classes;     // classes declared in this project.

        // http://graphviz.org/doc/info/colors.html
        const string kColorFail = "[color=\"red1\"]";       // cant read this.
        const string kColorTest = "[color=\"gray53\"]";     // a not distributed test app.
        const string kColorExe = "[color=\"darkorchid\"]";  // top level entry point.  
        const string kColorProject = "[color=\"green2\"]";  // a lib we have sources to and we build.
        const string kColorLib = "[color=\"royalblue\"]";   // a lib/package i dont have sources to. (external)


        public override string ColorShow => (ReadProjectResult < 0) ? kColorFail : IsTest ? kColorTest : IsExe ? kColorExe : ReadProjectResult > 0 ? kColorProject : kColorLib;

        // Declared refs vs used refs. SortedList always lower case sorted.
        public SortedList<string, ProjectReference> ProjectRefs = new SortedList<string, ProjectReference>();      // projects referenced in .csproj file.
        public HashSet<NameSpaceLevel> NameSpaceRefs = new HashSet<NameSpaceLevel>();         // namespaces used that are not also defined in this project.

        public ProjectReference(string filePath, string name, string nameShow)
            : base(name, nameShow)
        {
            FilePath = filePath;
            IsTest = name.IndexOf("Test", 0, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void TrimNameSpacesUsed()
        {
            // remove used namespaces that i also declared
            NameSpaceRefs.RemoveWhere(x => x.Project == this);
        }

        public override void ReadFile(NameSpaces namespaces)
        {
            // read the project file (.csproj) and get my dependent ProjectReference and PackageReference.
            // <Project Sdk=\"Microsoft.NET.Sdk\">"

            if (ReadProjectResult != 0)     // already did this?
                return;

            var reader = new ProjectReader(namespaces, this);
            ReadProjectResult = reader.ReadFile(FilePath );

 
        }

        public void AddNamespaceUsed(NameSpaceLevel level)
        {
            // we use this namespace.
            if (level == null)
                return;
            if (level.Project == this)  // don't bother recording a ref to something i declared
                return;
            NameSpaceRefs.Add(level);
        }

        internal void ShowGraph(TextWriter con, int graphLevel)
        {
            // Deal with my dependencies first.
            if (IsDisplayed)
                return;
            IsDisplayed = true;
            con.WriteLine($"{NameShow} {ColorShow}");

            foreach (var project in ProjectRefs.Values)
            {
                project.ShowGraph(con, graphLevel);
                con.WriteLine($"{NameShow} -> {project.NameShow} {ColorShow};");
            }

            if (graphLevel > 1)
            {
                ShowGraphPackages(con, ColorShow);
            }
        }

        internal void AddClass(CodeClass class0)
        {
            if (class0 == null)
                return;
            if (Classes == null)
            {
                Classes = new List<CodeClass>();
            }
            Classes.Add(class0);
        }
    }
}
