using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CodeCounter
{
    public class NameSpaces
    {
        // track ALL the namespaces and projects used.
        // Make sure namespaces are defined in just one project.
        // Track which projects use namespaces defined in other projects. build true dependency tree. 
        // Are all referenced projects actually used ?

        public const string kUsingDecl = "using ";
        public const string kNameSpaceDecl = "namespace ";
        
        SortedList<string, NameSpaceLevel> RootNames = new SortedList<string, NameSpaceLevel>();      // 
        SortedList<string, ProjectReference> Projects = new SortedList<string, ProjectReference>();      // flat list of all projects. // SortedList always lower case sorted.
        SortedList<string, PackageReference> Packages = new SortedList<string, PackageReference>();

        public NameSpaces()
        {
        }

        public void ShowModules(TextWriter con)
        {
            // dump format like : dependencies.webgraphviz.txt

            con.WriteLine("Modules: paste below into http://www.webgraphviz.com/ or use http://www.graphviz.org/");

            con.WriteLine("digraph prof { ratio = fill; node[style = filled]; ");

            foreach (ProjectReference project in Projects.Values)
            {
                project.ShowModules(con);
            }

            con.WriteLine("}");
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


        public void AddMethodCall(ProjectReference proj, string method)
        {
            // We found a method call that has a namespace prefix.

        }

        public void AddUsingDecl(ProjectReference proj, string nameSpace)
        {
            // We found a "using " line. record it as a reference.

            if (proj == null)
                return;

            proj.AddUsed(FindOrMakeName(nameSpace));
        }

        public string AddNameSpaceDecl(ProjectReference proj, string nameSpace)
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

        public PackageReference AddPackageRef(string name)
        {
            // do we already have this package?
            PackageReference package;
            if (Packages.TryGetValue(name, out package))
            {
                return package;
            }
            package = new PackageReference(name);
            Packages.Add(name, package);
            return package;
        }

        public ProjectReference AddProjectRef(string dir, string fileName)
        {
            // do we already have this project?
            string fileNameL = fileName.ToLower();

            ProjectReference project;
            if (Projects.TryGetValue(fileNameL, out project))
            {
                return project;
            }
            project = new ProjectReference(dir, fileName);
            Projects.Add(fileNameL, project);
            return project;
        }

        public ProjectReference AddProjectFile(string dirProject, string fileName)
        {
            // This dir has a project. .csproj file. all under it are considered to be its files, except if claimed by a sub project. 
            // Sub-projects are terrible style. Avoid this practice please !!!
            // DONT Read References from csproj. These can be extraneous. We are looking for real / true references.

            string fileNameL = fileName.ToLower();

            ProjectReference proj;
            if (Projects.TryGetValue(fileNameL, out proj))
            {
                proj.ReadProjectFile(this, dirProject, fileName);
                return proj;
            }

            proj = new ProjectReference(dirProject, fileName);
            proj.ReadProjectFile(this, dirProject, fileName);

            Projects.Add(fileNameL, proj);
            return proj;
        }

        public void DumpStats(TextWriter con)
        {
            // What things did i declare i need but never use?
            // 
        }
    }
}
