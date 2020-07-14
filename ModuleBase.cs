using System.Collections.Generic;

namespace CodeCounter
{
    public class ModuleBase
    {
        // base class for a module. e.g. a package/lib or a project.
        public string Name;         // Include="". from .csproj
        // public string Version; //
        // public string License; // ??

        public SortedList<string, PackageReference> PackageRefs = new SortedList<string, PackageReference>();   // Referenced packages.

        public string NameShow => Name.Replace('.', '_');

        public ModuleBase(string name)
        {
            Name = name;
        }
    }
}
