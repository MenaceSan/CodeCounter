//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeCounter
{
    public class CodeStatsProject
    {
        // Stats for a single Project

        [Description("Number of dirs with files. Ignore empty dirs.")]
        public int NumberOfDirectories = 0;
        [Description("Number of (not empty) source files read")]
        public int NumberOfFiles = 0;
        [Description("Number of total characters read")]
        public long NumberOfChars = 0;

        [Description("Number of syntax errors found in the files")]
        public int NumberOfErrors = 0;

        [Description("Total number of lines (Sum of next 6 stats)")]
        public int NumberOfLines = 0;

        [Description("Empty or Whitespace")]
        public int NumberOfLinesBlank = 0;
        [Description("Comments that are empty or multi line quotes")]
        public int NumberOfCommentBlank = 0;
        [Description("Comment lines that look like commented out code (or junk)")]
        public int NumberOfCommentedOutCode = 0;
        [Description("Comments that seem to contain text")]
        public int NumberOfCommentLines = 0;
        [Description("Lines that just have code")]
        public int NumberOfLinesCode = 0;
        [Description("Lines that have code and comments")]
        public int NumberOfLinesCodeAndComment = 0;

        [Description("Number of class, struct, interface, or enum")]
        public int NumberOfClasses = 0;
        [Description("Number of classes that have a comment immediately before or after")]
        public int NumberOfClassComments = 0;
        [Description("Number of class methods (Not properties or anon, lambda etc.)")]
        public int NumberOfMethods = 0;
        [Description("Number of methods that have a comment immediately before or after")]
        public int NumberOfMethodComments = 0;

        public void DumpStats(TextWriter con)
        {
            con.WriteLine($"{nameof(NumberOfDirectories)} = {NumberOfDirectories}");
            con.WriteLine($"{nameof(NumberOfFiles)} = {NumberOfFiles}");
            con.WriteLine($"{nameof(NumberOfErrors)} = {NumberOfErrors}");

            con.WriteLine($"{nameof(NumberOfLines)} = {NumberOfLines}");

            con.WriteLine($"{nameof(NumberOfLinesBlank)} = {NumberOfLinesBlank}");
            con.WriteLine($"{nameof(NumberOfCommentBlank)} = {NumberOfCommentBlank}");
            con.WriteLine($"{nameof(NumberOfCommentLines)} = {NumberOfCommentLines}");
            con.WriteLine($"{nameof(NumberOfCommentedOutCode)} = {NumberOfCommentedOutCode}");
            con.WriteLine($"{nameof(NumberOfLinesCode)} = {NumberOfLinesCode}");
            con.WriteLine($"{nameof(NumberOfLinesCodeAndComment)} = {NumberOfLinesCodeAndComment}");

            con.WriteLine($"{nameof(NumberOfClasses)} = {NumberOfClasses}");
            con.WriteLine($"{nameof(NumberOfClassComments)} = {NumberOfClassComments}");
            con.WriteLine($"{nameof(NumberOfMethods)} = {NumberOfMethods}");
            con.WriteLine($"{nameof(NumberOfMethodComments)} = {NumberOfMethodComments}");

            // Summary of percentages:
            // Percent of classes with comments
            // Percent of methods with comments
        }
    }

    public class CodeClass
    {
        public readonly string Name;
        public readonly List<string> Methods = new List<string>();

        public CodeClass(string name)
        {
            Name = name;
        }
    }

    // This is a comment prefixing the class. Leave it here as an internal test for comment counting.
    public class CodeStats : CodeStatsProject
    {
        // Gather stats on a bunch of .cs files.
        // similar to :
        //  https://www.ndepend.com/sample-reports/

        [Description("Number of .csproj files. One per directory.")]
        public int NumberOfProjects = 0;

        internal string RootDir;            // read everything under here.
        private readonly TextWriter _con;            // Console for Verbose messages and errors.

        internal bool Verbose = false;         // Print the class/methods i find.
        internal bool MakeTree = false;     // Output a tree of Dir/File/Class/Methods.
        internal bool Graph0 = false;       // Build the GraphViz markup.
        internal string Unprefix;           // strip this prefix from names. TODO

        internal readonly List<string> Ignore = new List<string>();     // ignore these dirs by name.
        internal readonly NameSpaces NameSpaces;

        public static readonly string[] kExtsAll = { ".cs", ".csproj" };     // what file types do we read? , ".cpp", ".vcxproj"
        public static readonly string[] kExtsProj = { ".csproj" };     // what project file types do we read? , ".vcxproj" 
        public static readonly string[] kExtsSrc = { ".cs" };     // what source file types do we read? , ".cpp" 

        public static readonly string[] kDirsIgnore = { "bin", "obj", "packages" };     // exclude these.

        public CodeStats(TextWriter con)
        {
            _con = con;
            NameSpaces = new NameSpaces();
        }

        public void DumpStats()
        {
            // dump all stats.
            _con.WriteLine($"{nameof(NumberOfProjects)} = {NumberOfProjects}");
            base.DumpStats(_con);
        }

        private int _TreeLastMask = 0;
        private string GetTree(int level, bool isLast)
        {
            if (!this.MakeTree)
                return "";
            if (level == 0)     // directories.
                return "";
            level--;
            if (isLast)
                _TreeLastMask |= (1 << level);        // set this.
            else
                _TreeLastMask &= ~(1 << level);        // clear this.

            string prefix = "";
            for (int j = 0; j < level; j++)
            {
                prefix += ((_TreeLastMask & (1 << j)) == 0) ? "│" : " ";
            }

            prefix += ((_TreeLastMask & (1 << level)) == 0) ? "├ " : "└ ";
            return prefix;
        }

        public void DumpClasses(List<CodeClass> classes)
        {
            // assume Verbose || MakeTree
            if (classes == null)
                return;
            int i = 0;
            foreach (var class1 in classes)
            {
                i++;
                _con.WriteLine($"{GetTree(2, i == classes.Count)}Class: {class1.Name}");
                int j = 0;
                foreach (string method in class1.Methods)
                {
                    j++;
                    _con.WriteLine($"{GetTree(3, j == class1.Methods.Count)}Method: {method}");
                }
            }
        }

        public void DumpFile(string name, bool isLast, List<string> errors)
        {
            if (Verbose || MakeTree)
            {
                _con.WriteLine($"{GetTree(1, isLast)}File: {name}");
            }
            foreach (string err in errors)
            {
                NumberOfErrors++;
                _con.WriteLine($"Error: {err}");
            }
        }

        private long _NumberOfCharsMsg = 0;      // NumberOfChars when i printed status last. // tick to show we are alive.
        public void OnReadLine(int numberOfChars)
        {
            NumberOfChars += numberOfChars;

            if (!Verbose && NumberOfChars - _NumberOfCharsMsg > 16 * 1024 * 1024)
            {
                _con.WriteLine(".");    // tick to show we are alive.
                _NumberOfCharsMsg = NumberOfChars;
            }
        }

        /// <summary>
        /// Count the number of lines, etc in the file specified.
        /// </summary>
        /// <param name="filePath">The filename to count.</param>
        /// <returns>The number of lines in the file.</returns>  
        private void ReadSrcFile(string filePath, ProjectReference proj, bool isLast)
        {
            using (var rdr = new StreamReader(filePath))
            {
                string fileRel = filePath.Substring(RootDir.Length);
                int lines = 0;
                if (filePath.EndsWith(".cs"))
                {
                    var lineState = new CsLineState();
                    lines = lineState.ReadFile(this, rdr, fileRel, proj, isLast);
                }
                if (filePath.EndsWith(".cpp"))
                {
                    var lineState = new CppLineState();
                    lines = lineState.ReadFile(this, rdr, fileRel, proj, isLast);
                }
                if (lines > 0)                   // Empty files don't count.
                {
                    NumberOfFiles++;
                    NumberOfLines += lines;
                }
            }
        }

        private bool IsIgnoredDir(string name)
        {
            // Do we ignore this directory ?
            if (string.IsNullOrWhiteSpace(name))
                return true;
            if (name.StartsWith("."))
                return true;
            if (kDirsIgnore.Contains(name))
                return true;
            foreach (string ignored in Ignore)
            {
                if (Regex.IsMatch(name, ignored))
                {
                    return true;
                }
            }
            return false;
        }

        public void ReadDir(string dirPath, bool isLast, ProjectReference proj = null)
        {
            // Recursive directory reader.

            var d = new DirectoryInfo(dirPath);     //  Assuming Test is your Folder

            // Process all the files first.
            // Ignore system/hidden files. e.g. Thumb.db 

            var files = d.GetFiles("*.*")
                .Where(x => !x.Attributes.HasFlag(FileAttributes.Hidden) || !x.Attributes.HasFlag(FileAttributes.System))
                .Where(x => kExtsAll.Contains(x.Extension))
                .ToList();

            if ((this.Verbose || MakeTree) && files.Count > 0)
            {
                _con.WriteLine($"{GetTree(0, isLast)}Dir: {dirPath.Substring(RootDir.Length)}");
            }

            // Get project info first.
            ProjectReference projDef = null;      // only one per directory.
            var filesProj = files.Where(x => kExtsProj.Contains(x.Extension)).ToList();
            foreach (FileInfo file in filesProj)
            {
                // Multi projects in the same directory count as the same project.
                if (projDef == null)
                {
                    NumberOfProjects++;
                    proj = projDef = NameSpaces.AddProjectFile(dirPath, file.Name);
                }
            }

            var filesSrc = files.Where(x => kExtsSrc.Contains(x.Extension)).ToList();
            int filesProcessed = 0;
            foreach (FileInfo file in filesSrc)
            {
                ReadSrcFile(file.FullName, proj, filesProcessed >= files.Count);
            }

            if (files.Count > 0)
            {
                NumberOfDirectories++;
            }

            // Recurse into directories. // NOT hidden/excluded directories. 
            int dirsProcessed = 0;

            var dirs = d.GetDirectories()
                .Where(x => !IsIgnoredDir(x.Name))
                .ToList();
            foreach (var dir in dirs)
            {
                dirsProcessed++;
                ReadDir(dir.FullName, dirsProcessed >= dirs.Count, proj);
            }
        }
    }
}
