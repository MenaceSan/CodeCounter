//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;

namespace CodeCounter
{
    public class NameSpaces
    {
        // track ALL the namespaces, packages and projects used.
        // Make sure namespaces are defined in just one project.
        // Track which projects use namespaces defined in other projects. build true dependency tree. 
        // Are all referenced projects actually used ?

        public const string kUsingDecl = "using ";
        public const string kNameSpaceDecl = "namespace ";

        SortedList<string, NameSpaceLevel> RootNameSpaces = new SortedList<string, NameSpaceLevel>();      // 
        SortedList<string, ProjectReference> Projects = new SortedList<string, ProjectReference>();      // flat list of all projects. // SortedList always lower case sorted.
        SortedList<string, PackageReference> Packages = new SortedList<string, PackageReference>();     // All Packages we reference, ordered by name

        public string Unprefix;           // strip this prefix from names in GetNameShow().  

        public NameSpaces()
        {
        }

        void FixupProject(ProjectReference proj, string nameL)
        {
            foreach (var proj2 in Projects.Values)
            {
                PackageReference pkg;
                if (proj2.PackageRefs.TryGetValue(nameL, out pkg))  // This package is really a project.
                {
                    proj2.PackageRefs.Remove(nameL);
                    proj2.ProjectRefs[nameL] = proj;
                }
            }
        }

        public void FixupPackages()
        {
            // Some packages are really projects that got resolved late ?

            foreach (var pkg in Packages)
            {
                // Is this really a project ? Was it later resolved to be a project ?
                string nameL = pkg.Key;
                ProjectReference proj;
                if (Projects.TryGetValue(nameL, out proj))  // This package is really a project.
                {
                    FixupProject(proj, nameL);
                }
            }

        }

        public void ShowGraph(TextWriter con, int graphLevel)
        {
            // dump format like : dependencies.webgraphviz.txt
            // https://www.codeproject.com/Articles/1164156/Using-Graphviz-in-your-project-to-create-graphs-fr

            // var graph = new Graphviz();

            con.WriteLine("Modules: paste below into http://www.webgraphviz.com/ or use http://www.graphviz.org/");

            con.WriteLine("digraph prof { ratio = fill; node[style = filled]; ");

            foreach (ProjectReference project in Projects.Values)
            {
                project.ShowGraph(con, graphLevel);
            }

            con.WriteLine("}");
        }

        private NameSpaceLevel FindOrMakeNames(string nameSpace)
        {
            // find full namespace name.

            if (nameSpace == null)
                return null;
            if (nameSpace.EndsWith(";"))
            {
                nameSpace = nameSpace.Substring(0, nameSpace.Length - 1).TrimEnd();
            }
            if (string.IsNullOrWhiteSpace(nameSpace))
                return null;

            string[] names = nameSpace.Split('.');
            if (names == null || names.Length <= 0)     // weird name
                return null;

            if (names[0] == "System")   // ignore System stuff.
                return null;

            int levelCount;
            NameSpaceLevel level;

            if (RootNameSpaces.TryGetValue(names[0], out level))
            {
                level = level.FindPartialMatch(names, 1);
                levelCount = level.LevelCount + 1;
            }
            else
            {
                level = new NameSpaceLevel(names[0], null);
                RootNameSpaces.Add(level.Name, level);
                levelCount = 1;
            }

            // Must create new level(s). (or not)
            return level.AddChildren(names, levelCount);
        }

        public void AddLicenseComment(ProjectReference proj, string method)
        {
            // Add the test of a comment. Is this a license reference?


        }

        public void AddMethodCall_TODO(ProjectReference proj, string method)
        {
            // We found a method call that has a namespace prefix.

        }

        public void AddUsingDecl(ProjectReference proj, string nameSpace)
        {
            // We found a "using " line. record it as a reference.

            if (proj == null)
                return;

            proj.AddNamespaceUsed(FindOrMakeNames(nameSpace));
        }

        public string AddNameSpaceDecl(ProjectReference proj, string nameSpace)
        {
            // I see a declared namespace XX {}.

            if (proj == null)
                return null;

            var level = FindOrMakeNames(nameSpace);
            if (level == null)
                return null;

            Debug.Assert(level.FullName == nameSpace);
            if (level.Project == proj)      // all set.
                return null;

            // Error if its already declared in another project.
            if (level.Project != null)
            {
                // return $"namespace '{nameSpace}' is defined in multiple projects ! ('{proj.Name}' and '{level.Project.Name}'";
                return null;
            }

            level.Project = proj;
            return null;
        }

        internal string GetNameShow(string name)
        {
            // Get the prettier version of the name.
            name = name.Replace('.', '_').Replace('-', '_').Replace('\\', '_');

            if (!string.IsNullOrWhiteSpace(Unprefix))
            {
                if (name.StartsWith(Unprefix))
                    name = name.Substring(Unprefix.Length);
            }
            return name;
        }

        public PackageReference AddPackageRef(string name)
        {
            // Add a reference to a package.
            // do we already have this package?

            string nameL = name.ToLower();
            PackageReference package;
            if (Packages.TryGetValue(nameL, out package))
            {
                return package;
            }

            // Find the binary for the package
            if (PackageLib.IsIgnored(nameL))
                return null;

            package = PackageLib.FindPackageByName(nameL); // In my lib of known packages ?
            if (package != null)
            {
                if (!package.VerifyFile())
                {
                    package = null;     // was not correct file.
                }
            }

            if (package == null)        // This is a new package.
            {
                package = new PackageReference(name, GetNameShow(name));
                package.ReadFile(this);
            }

            Packages.Add(nameL, package);
            return package;
        }

        public ProjectReference AddProjectRef(string filePath, bool isLib)
        {
            // This directory has a project. .csproj file. all under it are considered to be its files, except if claimed by a sub project. 
            // Sub-projects are terrible style. Avoid this practice please !!!
            // NOTE: References from .csproj can be extraneous? Should we just look for real / true references via namespace? OR All binaries pulled into PE count even if dead code.

            string name = Path.GetFileNameWithoutExtension(filePath);
            string nameL = name.ToLower();

            ProjectReference project;
            if (Projects.TryGetValue(nameL, out project))
            {
                return project;
            }

            if (isLib)
            {
                // try to convert the lib name into a project name,

            }

            project = new ProjectReference(filePath, name, GetNameShow(name));
            project.ReadFile(this);

            if (isLib && project.ReadProjectResult < 0)
            {
                return null;    // its a package NOT a project!
            }

            Projects.Add(nameL, project);
            return project;
        }

    }
}
