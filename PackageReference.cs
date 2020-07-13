//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
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
