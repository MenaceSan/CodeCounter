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
    internal class CodeStatsProject
    {
        // Stats for a single Project

        [Description("Number of dirs with files. Ignore empty dirs.")]
        public int NumberOfDirectories = 0;
        [Description("Number of (not empty) .cs files read")]
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
        [Description("Number of classe methods (Not properties or anon, lambda etc.)")]
        public int NumberOfMethods = 0;
        [Description("Number of methods that have a comment immediately before or after")]
        public int NumberOfMethodComments = 0;
    }

    // This is a comment prefixing the class. Leave it here as an internal test for comment counting.
    internal class CodeStats : CodeStatsProject
    {
        // Gather stats on a bunch of .cs files.
        // similar to :
        //  https://www.ndepend.com/sample-reports/

        [Description("Number of .csproj files. One per directory.")]
        public int NumberOfProjects = 0;

        internal string RootDir;            // read everything under here.

        internal bool Verbose = false;         // Print the class/methods i find.
        private readonly TextWriter _con;            // Console for Verbose messages and errors.

        internal bool MakeTree = false;
        internal bool Graph0 = false;

        internal long NumberOfCharsMsg = 0;      // NumberOfChars when i printed status last.

        internal readonly List<string> Ignore = new List<string>();
        internal readonly NameSpaces NameSpaces;

        public CodeStats(TextWriter con)
        {
            _con = con;
            NameSpaces = new NameSpaces();
        }

        public void DumpStats()
        {
            _con.WriteLine($"{nameof(NumberOfProjects)} = {NumberOfProjects}");
            _con.WriteLine($"{nameof(NumberOfDirectories)} = {NumberOfDirectories}");
            _con.WriteLine($"{nameof(NumberOfFiles)} = {NumberOfFiles}");
            _con.WriteLine($"{nameof(NumberOfErrors)} = {NumberOfErrors}");

            _con.WriteLine($"{nameof(NumberOfLines)} = {NumberOfLines}");

            _con.WriteLine($"{nameof(NumberOfLinesBlank)} = {NumberOfLinesBlank}");
            _con.WriteLine($"{nameof(NumberOfCommentBlank)} = {NumberOfCommentBlank}");
            _con.WriteLine($"{nameof(NumberOfCommentLines)} = {NumberOfCommentLines}");
            _con.WriteLine($"{nameof(NumberOfCommentedOutCode)} = {NumberOfCommentedOutCode}");
            _con.WriteLine($"{nameof(NumberOfLinesCode)} = {NumberOfLinesCode}");
            _con.WriteLine($"{nameof(NumberOfLinesCodeAndComment)} = {NumberOfLinesCodeAndComment}");

            _con.WriteLine($"{nameof(NumberOfClasses)} = {NumberOfClasses}");
            _con.WriteLine($"{nameof(NumberOfClassComments)} = {NumberOfClassComments}");
            _con.WriteLine($"{nameof(NumberOfMethods)} = {NumberOfMethods}");
            _con.WriteLine($"{nameof(NumberOfMethodComments)} = {NumberOfMethodComments}");

            // Summary of percentages:
            // Percent of classes
            // Percent of methods

        }

        public static readonly string[] _exts = { ".cs", ".csproj" };     // what file types do we read?
        public static readonly string[] _dirsEx = { "bin", "obj", "packages" };
        public static readonly string[] _classes = { "class", "enum", "struct", "interface" };
        public static readonly char[] _methodEx = { '=', '{' };

        private string GetTree(int i, bool isLast)
        {
            if (!this.MakeTree) // && Verbose
                return "";
            if (isLast)
            {
                switch (i)
                {
                    case 0: return "";
                    case 1: return "└ ";        // ReadFile
                    case 2: return "│└ ";       // class/struct
                    case 3: return "││└ ";      // methods
                }
            }
            else
            {
                switch (i)
                {
                    case 0: return "";
                    case 1: return "├ ";        // ReadFile
                    case 2: return "│├ ";       // class/struct
                    case 3: return "││├ ";      // methods
                }
            }
            return "";  // should not get here ?
        }

        /// <summary>
        /// Count the number of lines, etc in the file specified.
        /// </summary>
        /// <param name="filePath">The filename to count.</param>
        /// <returns>The number of lines in the file.</returns>  
        private void ReadFile(string filePath, ProjectReference proj, bool isLast)
        {
            if (filePath.EndsWith("AssemblyInfo.cs"))  // always ignore this file for comment counting purposes.
                return;

            if (Verbose || MakeTree)
            {
                _con.WriteLine($"{GetTree(1, isLast)}File: {filePath.Substring(RootDir.Length)}");
            }

            NumberOfFiles++;
            var lineState = new LineState();

            using (var rdr = new StreamReader(filePath))
            {
                bool hasCode = false;   // found some code in the file.
                string lineRaw;
                while ((lineRaw = rdr.ReadLine()) != null)
                {
                    lineState.LineNumber++;
                    NumberOfLines++;
                    NumberOfChars += lineRaw.Length;

                    if (NumberOfChars - NumberOfCharsMsg > 16 * 1024 * 1024)
                    {
                        _con.WriteLine(".");
                        NumberOfCharsMsg = NumberOfChars;
                    }

                    if (!hasCode && lineRaw.Contains("<auto-generated"))   // ignore this whole file.
                    {
                        NumberOfLines -= lineState.LineNumber;  // ignore this file.
                        NumberOfFiles--;
                        // Comment total lines might be off now??
                        return;
                    }

                    lineRaw = lineRaw.Trim();
                    if (lineRaw.StartsWith("#"))  // Ignore preprocess line if, else, pragma , etc.
                    {
                        NumberOfLinesCode++;
                        continue;
                    }

                    lineState.ClearLine();
                    string lineCode = lineState.ReadLine(lineRaw);

                    if (lineCode.StartsWith(NameSpaces.kUsingDecl))
                    {
                        NameSpaces.AddUsingDecl(proj, lineCode.Substring(NameSpaces.kUsingDecl.Length));
                    }
                    else if (lineCode.StartsWith(NameSpaces.kNameSpaceDecl))
                    {
                        string err = NameSpaces.AddNameSpaceDecl(proj, lineCode.Substring(NameSpaces.kNameSpaceDecl.Length));
                        if (err != null)
                        {
                            _con.WriteLine($"Error: {err} at {filePath.Substring(RootDir.Length)}:{lineState.LineNumber}");
                            NumberOfErrors++;
                        }
                    }

                    if (lineState.Error.Length > 0)
                    {
                        _con.WriteLine($"Error '{lineState.Error}' at {filePath.Substring(RootDir.Length)}:{lineState.LineNumber}");
                        NumberOfErrors++;
                    }
                    if (lineState.HasCode && lineState.HasCommentText)
                    {
                        hasCode = true;
                        NumberOfLinesCodeAndComment++;
                    }
                    else if (lineState.HasCommentText)
                    {
                        NumberOfCommentLines++;
                    }
                    else if (lineState.HasCode)
                    {
                        hasCode = true;
                        NumberOfLinesCode++;
                    }
                    else if (lineState.HasComment)
                    {
                        NumberOfCommentBlank++;
                        continue;
                    }
                    else
                    {
                        NumberOfLinesBlank++;
                        continue;
                    }

                    if (lineState.HasCommentText && !lineState.HasCode)
                    {
                        lineState.LastLineWasComment = true;
                        if (lineState.LastLineWasClass)
                        {
                            NumberOfClassComments++;
                            lineState.LastLineWasClass = false;
                        }
                        if (lineState.LastLineWasMethod)
                        {
                            NumberOfMethodComments++;
                            lineState.LastLineWasMethod = false;
                        }
                    }

                    if (!lineState.HasCode || lineCode.Length <= 0) // maybe a comment. done with that. 
                        continue;
                    if (lineCode[0] == '[' && lineCode[lineCode.Length - 1] == ']')   // ignore attributes.
                        continue;

                    // Is this a class ?
                    if (!lineCode.StartsWith("{"))
                    {
                        lineState.LastLineWasClass = false;
                    }

                    foreach (string cn in _classes)
                    {
                        int k = lineCode.IndexOf(cn);
                        if (k >= 0
                            && (k == 0 || char.IsWhiteSpace(lineCode[k - 1]))
                            && (k + cn.Length >= lineCode.Length || char.IsWhiteSpace(lineCode[k + cn.Length])))
                        {
                            if (lineState.OpenClassBrace == 0)  // ignore child class ?
                            {
                                lineState.OpenClassBrace = lineState.OpenBraceCount + 1;
                            }
                            lineState.LastLineWasClass = true;
                            NumberOfClasses++;
                            if (Verbose || MakeTree)
                            {
                                _con.WriteLine($"{GetTree(2, false)}Class: {lineCode}");
                            }
                            if (lineState.LastLineWasComment)
                            {
                                NumberOfClassComments++;
                                lineState.LastLineWasComment = false;
                            }
                            break;
                        }
                    }

                    // is this a method ? its at the correct brace level. inside a class.
                    if (lineState.OpenClassBrace == lineState.OpenBraceCount)
                    {
                        lineState.LastLineWasMethod = false;
                        int j = lineCode.IndexOf('(');
                        if (j > 0)
                        {
                            // this is a method and not a prop or field?
                            int k = lineCode.IndexOfAny(_methodEx, 0, j); // Not a method if = or { appear before (
                            if (k < 0)
                            {
                                lineState.LastLineWasMethod = true;
                                NumberOfMethods++;
                                if (Verbose || MakeTree)
                                {
                                    _con.WriteLine($"{GetTree(3, false)}Method: {lineCode}");
                                }
                                if (lineState.LastLineWasComment)
                                {
                                    NumberOfMethodComments++;
                                    lineState.LastLineWasComment = false;
                                }
                            }
                        }
                    }
                    else if (!lineCode.StartsWith("{"))
                    {
                        lineState.LastLineWasMethod = false;
                    }

                    lineState.LastLineWasComment = false;
                }
            }

            if (lineState.LineNumber == 0)
            {
                // Empty files dont count.
                NumberOfFiles--;
            }
            if (lineState.OpenBraceCount != 0)
            {
                // What line is open brace ?? "#if" can mess this up.
                _con.WriteLine($"{filePath.Substring(RootDir.Length)} has {lineState.OpenBraceCount} unmatched braces from {lineState.OpenBrace.Pop()}.");
                NumberOfErrors++;
            }
        }

        public bool IsIgnore(string name)
        {
            foreach(string ignored in Ignore)
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
            // Recursive dir reader.

            var d = new DirectoryInfo(dirPath);     //  Assuming Test is your Folder

            if (IsIgnore(d.Name))
            {
                return;
            }

            bool showDir = false;   // only show dir if it has files.
            ProjectReference projDef = null;      // only one per directory.

            // deal with files first.
            // System.IO.IOException: 'The directory name is invalid.' = this was a file name not a dir name ?

            int filesProcessed = 0;
            int filesInDir = 0;

            // Getting all files.  e.g. NOT Thumb.db 
            var Files = d.GetFiles("*.*")
                .Where(file => !file.Attributes.HasFlag(FileAttributes.Hidden) || !file.Attributes.HasFlag(FileAttributes.System))
                .Where(file => _exts.Contains( Path.GetExtension(file.Name)))
                .ToList(); 

            foreach (FileInfo file in Files)
            {
                filesProcessed++;

                if (file.Name.EndsWith(".csproj"))
                {
                    // Multi projects in the same dir count as the same project.
                    if (projDef == null)
                    {
                        NumberOfProjects++;
                        proj = projDef = NameSpaces.AddProjectFile(dirPath, file.Name);
                    }
                    continue;
                }

                if ((this.Verbose || MakeTree ) && !showDir)
                {
                    _con.WriteLine($"{GetTree(0, isLast)}Dir: {dirPath.Substring(RootDir.Length)}");
                    showDir = true;
                }

                filesInDir++;
                ReadFile(file.FullName, proj, filesProcessed >= Files.Count);
            }

            if (filesInDir > 0)
            {
                NumberOfDirectories++;
            }

            // Recurse into dirs. // NOT excluded dir. NOT hidden
            int dirsProcessed = 0;

            var dirs = d.GetDirectories().Where(dir =>!_dirsEx.Contains(dir.Name) && !dir.Name.StartsWith(".")).ToList();
            foreach (var dir in dirs)
            {
                dirsProcessed++;
                ReadDir(dir.FullName, dirsProcessed >= dirs.Count, proj);
            }
        }
    }
}
