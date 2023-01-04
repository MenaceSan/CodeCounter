//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeCounter
{
    public class CodeStatsProject
    {
        // Capture Stats for a single Project

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

    // This is a comment prefixing the class. Leave it here as an internal test for comment counting.
    public class CodeStats : CodeStatsProject
    {
        // Gather stats on a bunch of files.
        // similar to :
        //  https://www.ndepend.com/sample-reports/

        [Description("Number of .csproj files. One per directory.")]
        public int NumberOfProjects = 0;

        internal string RootDir;            // read everything under here.
        internal TextWriter Out;            // Console for Verbose messages and errors.

        public bool Verbose = false;      // Print the class/methods i find.
        public bool MakeTree = false;     // Output a tree of Dir/File/Class/Methods.
        public int GraphLevel = 0;        // Build the GraphViz markup. 0=none, 1=immediate, 2=libraries, 3= 2nd level libraries.

        public readonly List<string> Ignore = new List<string>();     // ignore these dirs by name.
        public readonly NameSpaces NameSpaces = new NameSpaces();

        public static readonly string[] kExtsAll = { CsReader.kExtSrc, CsReader.kExtProj, CppReader.kExtSrc, CppReader.kExtProj };     // what file types do we read? 

        public static readonly string[] kExtsProj = { CsReader.kExtProj, CppReader.kExtProj };     // what project file types do we read?  
        public static readonly string[] kExtsSrc = { CsReader.kExtSrc, CppReader.kExtSrc };     // what source file types do we read? 

        public static readonly string[] kDirsIgnore = { "bin", "obj", "packages" };     // exclude these.

        public bool IsReadingClasses => (Verbose || MakeTree || GraphLevel > 0); // inspect classes ?

        public CodeStats(TextWriter conOut)
        {
            Out = conOut;
        }

        public void DumpStats()
        {
            // dump all stats.
            Out.WriteLine($"{nameof(NumberOfProjects)} = {NumberOfProjects}");
            base.DumpStats(Out);
        }

        private int _TreeLastMask = 0;  // bit mask.
        private string GetTree(int level, bool isLastFile)
        {
            if (!this.MakeTree)
                return "";
            if (level == 0)     // directories.
                return "";
            level--;

            int mask = CodeUtil.BitMask(level);
            if (isLastFile)
                _TreeLastMask |= mask;        // set this.
            else
                _TreeLastMask &= ~mask;        // clear this = Not last.

            string prefix = "";
            for (int j = 0; j < level; j++)
            {
                prefix += CodeUtil.IsBit(_TreeLastMask, j) ? " " : "│";
            }

            prefix += CodeUtil.IsBit(_TreeLastMask, level) ? "└ " : "├ ";
            return prefix;
        }

        public void DumpClasses(List<CodeClass> classes)
        {
            // assume Verbose || MakeTree
            if (classes == null || !Verbose)
                return;
            int i = 0;
            foreach (var class1 in classes)
            {
                i++;
                Out.WriteLine($"{GetTree(2, i == classes.Count)}Class: {class1.Name}");
                int j = 0;
                foreach (string method in class1.Methods)
                {
                    j++;
                    Out.WriteLine($"{GetTree(3, j == class1.Methods.Count)}Method: {method}");
                }
            }
        }

        public void DumpSrcFile(string name, bool isLastFile, List<string> errors)
        {
            // Info about the src file.
            if (Verbose || MakeTree)
            {
                Out.WriteLine($"{GetTree(1, isLastFile)}File: {name}");
            }
            if (errors != null)
            {
                foreach (string err in errors)
                {
                    Out.WriteLine($"Error: {err}");
                    NumberOfErrors++;
                }
            }
        }

        private long _NumberOfCharsMsg = 0;      // NumberOfChars when i printed status last. // tick to show we are alive.
        public void OnReadLine(int numberOfChars)
        {
            NumberOfChars += numberOfChars;
            if (Verbose)    // dont mess up the output if we are Verbose.
                return;
            if (Out != Console.Out) // dont mess up output file ??
                return;
            if (NumberOfChars - _NumberOfCharsMsg > 16 * 1024 * 1024)
            {
                Out.WriteLine(".");    // tick to show we are alive. console.
                _NumberOfCharsMsg = NumberOfChars;
            }
        }

        public bool OnCountLine(CodeReader line)
        {
            if (line.HasCode && line.HasCommentText)
            {
                NumberOfLinesCodeAndComment++;
                return true;
            }
            else if (line.HasCommentText)
            {
                NumberOfCommentLines++;
                return true;
            }
            else if (line.HasCode)
            {
                NumberOfLinesCode++;
                return true;
            }
            else if (line.HasComment)
            {
                NumberOfCommentBlank++;
                return false;
            }
            else
            {
                NumberOfLinesBlank++;     // Has no comment or anything.
                return false;
            }
        }

        /// <summary>
        /// Count the number of lines, etc in the file specified.
        /// </summary>
        /// <param name="filePath">The filename to count.</param>
        /// <returns>The number of lines in the file.</returns>  
        private void ReadSrcFile(string filePath, ProjectReference proj, bool isLastFile)
        {
            using (var rdr = new StreamReader(filePath))
            {
                string fileRel = filePath.Substring(RootDir.Length + 1);
                int lines = 0;
                if (filePath.EndsWith(CsReader.kExtSrc))
                {
                    var lineState = new CsReader();
                    lines = lineState.ReadFile(this, rdr, fileRel, proj, isLastFile);
                }
                if (filePath.EndsWith(CppReader.kExtSrc))
                {
                    var lineState = new CppReader();
                    lines = lineState.ReadFile(this, rdr, fileRel, proj, isLastFile);
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

        public void ReadDir2(string dirPath, bool isLastFile, ref ProjectReference proj)
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
                Out.WriteLine($"{GetTree(0, isLastFile)}Dir: {dirPath.Substring(RootDir.Length)}");
            }

            // Get project info first.
            // Reverse() = Take the last file. Assume higher number version is best.
            var filesProj = files.Where(x => kExtsProj.Contains(x.Extension)).Reverse().ToList();
            ProjectReference proj2 = proj;
            foreach (FileInfo file in filesProj)
            {
                proj2 = NameSpaces.AddProjectRef(file.FullName, false);
                NumberOfProjects++;
                break;      // Multi projects in the same directory count as the same project.
            }

            if (proj != null && proj2 != null)
            {
                // treat this like a child (separate) project.
            }

            var filesSrc = files.Where(x => kExtsSrc.Contains(x.Extension)).ToList();
            int filesProcessed = 0;
            foreach (FileInfo file in filesSrc)
            {
                ReadSrcFile(file.FullName, proj2, filesProcessed >= files.Count);
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
                ReadDir2(dir.FullName, dirsProcessed >= dirs.Count, ref proj2);
            }

            if (proj == null)
            {
                proj = proj2;
            }
        }

        public void ReadRoot(string dirPath, bool isLastFile = true)
        {
            // ProjectReference proj
            this.RootDir = dirPath;
            ProjectReference proj = null;
            this.ReadDir2(dirPath, isLastFile, ref proj);
        }

    }
}
