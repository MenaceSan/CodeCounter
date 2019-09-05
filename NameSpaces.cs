using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CodeCounter
{
    public class NameSpaceProject
    {
        public string Name;         // Projects file name. from .csproj
        public string DirBase;      // (lower case) Project owns all files under this directory. (except if claimed by a sub project. which is a bad idea but allow it)

        // Declared refs vs used refs.
        SortedList<string, string> DeclaredProjectDeps = new SortedList<string, string>();      // projects declared in .csproj file.
        HashSet<NameSpaceLevel> NameSpacesUsed = new HashSet<NameSpaceLevel>();         // namespaces used that are not defined in my project.

        public NameSpaceProject()
        {
        }

        public void TrimNameSpacesUsed()
        {
            // remove used namespaces that i also declared
            NameSpacesUsed.RemoveWhere(x => x.Project == this);
        }

        public void AddUsed(NameSpaceLevel level)
        {
            if (level == null)
                return;
            if (level.Project == this)  // dont bother recording a ref to something i declared
                return;
            NameSpacesUsed.Add(level);
        }
    }

    public class NameSpaceLevel
    {
        // Define a level/segment of a namespace.
        public readonly string Name;     // segment/level name.

        public readonly NameSpaceLevel Parent;
        private readonly SortedList<string, NameSpaceLevel> Children;

        public NameSpaceProject Project;    // What project defines this. Should only be 1 !!

        public NameSpaceLevel(string name, NameSpaceLevel parent)
        {
            Name = name;
            Parent = parent;
            Children = new SortedList<string, NameSpaceLevel>();
        }

        public string FullName
        {
            get
            {
                if (Parent == null)
                    return Name;
                // Trace its parent path. recursive.
                return string.Concat(Parent.FullName, '.', Name);
            }
        }

        public int LevelCount
        {
            get
            {
                int i = 0;
                for (var p = Parent; p != null; i++)
                {
                    p = p.Parent;
                }
                return i;
            }
        }

        public NameSpaceLevel FindPartialMatch(string[] names, int i = 1)
        {
            // Find Partial or full match.
            if (i >= names.Length)
                return this;    // full match.
            NameSpaceLevel child;
            if (!Children.TryGetValue(names[i], out child))
                return this;    // maybe not a full match?
            return child.FindPartialMatch(names, i + 1);
        }

        internal NameSpaceLevel AddChildren(string[] names, int levelCount)
        {
            if (levelCount >= names.Length)
                return this;
            var level = new NameSpaceLevel(names[levelCount], this);
            Children.Add(level.Name, level);
            return level.AddChildren(names, levelCount + 1);
        }
    }

    public class NameSpaces
    {
        // track the namespaces and projects used.
        // Make sure namespaces are defined in just one project.
        // Track which projects use namespaces defined in other projects. build true dependency tree. 
        // Are all referenced projects actually used ?

        private readonly TextWriter _con;            // Console for Verbose messages and errors.

        public const string kUsingDecl = "using ";
        public const string kNameSpaceDecl = "namespace ";

        SortedList<string, NameSpaceLevel> RootNames = new SortedList<string, NameSpaceLevel>();      // 
        SortedList<string, NameSpaceProject> Projects = new SortedList<string, NameSpaceProject>();      // 

        public NameSpaces(TextWriter con)
        {
            _con = con;
        }

        private NameSpaceLevel FindOrMakeName(string nameSpace)
        {
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

            if (RootNames.TryGetValue(names[0], out level))
            {
                level = level.FindPartialMatch(names, 1);
                levelCount = level.LevelCount + 1;
            }
            else
            {
                level = new NameSpaceLevel(names[0], null);
                RootNames.Add(level.Name, level);
                levelCount = 1;
            }

            // Must create new level(s). (or not)
            return level.AddChildren(names, levelCount);
        }


        public void AddMethodCall(NameSpaceProject proj, string method)
        {
            // We found a method call that has a namespace prefix.

        }

        public void AddUsingDecl(NameSpaceProject proj, string nameSpace)
        {
            // We found a "using " line. record it as a reference.

            if (proj == null)
                return;

            proj.AddUsed(FindOrMakeName(nameSpace));
        }

        public string AddNameSpaceDecl(NameSpaceProject proj, string nameSpace)
        {
            // I see a declared namespace XX {}.

            if (proj == null)
                return null;

            var level = FindOrMakeName(nameSpace);
            if (level == null)
                return null;

            Debug.Assert(level.FullName == nameSpace);
            if (level.Project == proj)      // all set.
                return null;

            // Error if its already declared in another project.
            if (level.Project != null)
            {
                return $"namespace '{nameSpace}' is defined in multiple projects ! ('{proj.Name}' and '{level.Project.Name}'";
            }

            level.Project = proj;
            return null;
        }

        public NameSpaceProject AddProjectFile(string dirProject, string fileName)
        {
            // This dir has a project. .csproj file. all under it are considered to be its files, except if claimed by a sub project. 
            // Sub-projects are terrible style. Avoid this practice please !!!
            // DONT Read References from csproj. These can be extraneous. We are looking for real / true references.

            fileName = fileName.ToLower();

            NameSpaceProject proj;
            if (Projects.TryGetValue(fileName, out proj))
            {
                return proj;
            }

            proj = new NameSpaceProject
            {
                DirBase = dirProject.ToLower(),
                Name = fileName,
            };

            Projects.Add(fileName, proj);
            return proj;
        }

        public void DumpStats(TextWriter con)
        {
            // What things did i declare i need but never use?
            // 
        }
    }
}
