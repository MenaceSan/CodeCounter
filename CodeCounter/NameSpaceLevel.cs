//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections.Generic;

namespace CodeCounter
{
    /// <summary>
    /// Define a level/segment of a namespace.
    /// </summary>
    public class NameSpaceLevel
    {     
        public readonly string Name;     // segment/level name in a namespace.

        public readonly NameSpaceLevel Parent;
        private readonly SortedList<string, NameSpaceLevel> Children;

        public ProjectReference Project;    // What project defines this. Ideally there should only be 1 !! but allow multiple.

        public NameSpaceLevel(string name, NameSpaceLevel parent)
        {
            Name = name;
            Parent = parent;
            Children = new SortedList<string, NameSpaceLevel>();
        }

        public string FullName
        {
            // Build the full namespace name
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
            // How many levels in the name?
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

}
