using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeCounter
{
    public class PackageReference
    {
        public string Name;         // Include="". from .csproj

        public PackageReference(string name)
        {
            Name = name;
        }
    }
}
