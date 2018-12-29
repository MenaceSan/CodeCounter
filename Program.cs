using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace CodeCounter
{
    internal class LineState
    {
        // The state (of a line) inside a single .cs source file.

        public int LineNumber = 0;      // 1 based.
        public bool LastLineWasClass = false;   // last line was a class.
        public bool LastLineWasMethod = false;   // last line was a method
        public bool LastLineWasComment = false;  // last line was a comment

        public int OpenCommentLine = 0;    // Part of multi line /* comment */ . Can span lines.
        public bool OpenAtQuote = false;        // @"\n" can span lines.

        public Stack OpenBrace = new Stack();   // What line int was open brace on ?
        public int OpenBraceCount { get { return OpenBrace.Count; } }
        public int OpenClassBrace = 0;      // inside a class. make this a stack ? OpenBraceCount
        public int MaxBraceCount = 0;       // Max we see.

        public bool HasCode;            // not a comment. looks like code. (maybe @quoted string = whole line)
        public bool HasComment;         // possibly empty comment. 
        public bool HasCommentText;        // text in comment.
        public bool HasCommentCode;        // looks like commented out code (junk text). This doesnt count as a real/useful comment.
        public bool HasError;           // some sort of syntax error.

        public void ClearLine()
        {
            HasCode = false;
            HasComment = false;
            HasCommentText = false;
            HasCommentCode = false;
            HasError = false;
        }

        private int FindQuoteEnd(string line, int i)
        {
            // handle "\\" and @"\"
            int j = line.IndexOf('"', i + 1);   // find close.
            if (j < 0)
            {
                if (OpenAtQuote)
                {
                    // this is legal. // OpenAtQuote can span lines ! 
                    return -1;
                }
                HasError = true; // NumberOfErrors // weird NO CLOSE ??
                return -1;
            }

            if (!OpenAtQuote && line[j - 1] == '\\' && line[j - 2] != '\\')
                j--;    // consider this another open quote.
            i = j;
            OpenAtQuote = false;
            return i;
        }

        const string kTestMultiLine = @"
This is a test of a multi line constant string.
";

        public string ReadLine(string line)
        {
            // return line minus any comments.

            line = line.Trim();
            if (line.Length == 0)
            {
                return line;
            }

            if (OpenAtQuote)    // look for closing quote. "
            {
                int i = FindQuoteEnd(line, -1);
                if (i < 0)
                {
                    HasCode = true;  // consider this a blank code line.
                    return "";
                }
                i++;
                line = line.Substring(i, line.Length - i); // get after quote
            }

            if (OpenCommentLine > 0)    // look for close comment. */
            {
                HasComment = true;

                int i = line.IndexOf("*/");
                if (i < 0)  // no end OpenComment
                {
                    HasCommentCode = true;
                    // Commented out code is not a real comment ?? No HasCommentText
                    return line;
                }

                if (OpenCommentLine == LineNumber)  // HasCommentText only if one line. otherwise assume its just commented out code.
                {
                    HasCommentText |= line.Length > 0;
                }

                OpenCommentLine = 0;    // comment closed.
                return ReadLine(line.Substring(i + 2));
            }

            if (line.StartsWith(@"//"))     // line comment
            {
                HasComment = true;
                HasCommentText |= line.Length > 2;
                return "";
            }

            if (line.StartsWith(@"/*"))
            {
                // HasComment is not true unless there is text in this case.
                OpenCommentLine = LineNumber;
                return ReadLine(line.Substring(2));
            }

            HasCode = true;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    OpenAtQuote = (i > 1 && line[i - 1] == '@');
                    i = FindQuoteEnd(line, i);
                    if (i < 0)  // no close?
                        break;
                    continue;
                }
                if (ch == '\'')
                {
                    // handle '\\' and '\''
                    int j = line.IndexOf('\'', i + 1);
                    if (j < 0)  // weird NO CLOSE ??
                    {
                        HasError = true; // NumberOfErrors
                        break;
                    }
                    if (j > 2 && line[j - 1] == '\\' && line[j - 2] != '\\')
                        j--;    // consider this another open quote.
                    i = j;
                    continue;
                }

                if (ch == '{')
                {
                    OpenBrace.Push(LineNumber);
                    if (OpenBraceCount > MaxBraceCount)
                        MaxBraceCount = OpenBraceCount;
                    continue;
                }
                if (ch == '}')
                {
                    if (OpenClassBrace == OpenBraceCount)   // close this method.
                    {
                        OpenClassBrace = 0;
                    }
                    if (OpenBraceCount <= 0)
                    {
                        // No close brace. Error!
                        // Console.WriteLine($"{f.Substring(RootDir.Length)} has {lineState.OpenBraceCount} unmatched braces from {lineState.OpenBrace.Pop()}.");
                        HasError = true;
                        break;
                    }
                    OpenBrace.Pop();
                    continue;
                }

                if (ch == '/')  // comment?
                {
                    if (i >= line.Length - 1)
                        break;

                    char ch2 = line[i + 1];
                    if (ch2 == '/')
                    {
                        HasComment = true;
                        HasCommentText |= (line.Length - (i + 2)) > 0;
                        return line.Substring(0, i).TrimEnd();
                    }
                    if (ch2 == '*')
                    {
                        HasComment = true;
                        OpenCommentLine = LineNumber;
                        return line.Substring(0, i).TrimEnd() + ReadLine(line.Substring(i + 2));    // trim out comment.
                    }
                }
            }

            return line;
        }
    }

    // This is a comment prefixing the class.
    internal class Stats
    {
        // similar to :
        //  https://www.ndepend.com/sample-reports/

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

        internal string RootDir;
        internal bool Verbose = true;         // Print the class/methods i find.
        internal bool MakeTree = false;

        internal long NumberOfCharsMsg = 0;      // NumberOfChars when i printed status last.

        public void DumpStats()
        {
            Console.WriteLine($"{nameof(NumberOfDirectories)} = {NumberOfDirectories}");
            Console.WriteLine($"{nameof(NumberOfFiles)} = {NumberOfFiles}");
            Console.WriteLine($"{nameof(NumberOfErrors)} = {NumberOfErrors}");

            Console.WriteLine($"{nameof(NumberOfLines)} = {NumberOfLines}");

            Console.WriteLine($"{nameof(NumberOfLinesBlank)} = {NumberOfLinesBlank}");
            Console.WriteLine($"{nameof(NumberOfCommentBlank)} = {NumberOfCommentBlank}");
            Console.WriteLine($"{nameof(NumberOfCommentLines)} = {NumberOfCommentLines}");
            Console.WriteLine($"{nameof(NumberOfCommentedOutCode)} = {NumberOfCommentedOutCode}");
            Console.WriteLine($"{nameof(NumberOfLinesCode)} = {NumberOfLinesCode}");
            Console.WriteLine($"{nameof(NumberOfLinesCodeAndComment)} = {NumberOfLinesCodeAndComment}");

            Console.WriteLine($"{nameof(NumberOfClasses)} = {NumberOfClasses}");
            Console.WriteLine($"{nameof(NumberOfClassComments)} = {NumberOfClassComments}");
            Console.WriteLine($"{nameof(NumberOfMethods)} = {NumberOfMethods}");
            Console.WriteLine($"{nameof(NumberOfMethodComments)} = {NumberOfMethodComments}");
        }

        public static readonly string[] _exts = { ".cs" };     // what file types do we read?
        public static readonly string[] _dirsEx = { "bin", "obj", "packages" };
        public static readonly string[] _classes = { "class", "enum", "struct", "interface" };
        public static readonly char[] _methodEx = { '=', '{' };

        private string GetTree(int i)
        {
            if (!this.MakeTree) // && Verbose
                return "";
            switch (i)
            {
                case 0: return "";
                case 1: return "├ ";
                case 2: return "│├ ";
                case 3: return "││├ ";
            }
            return "";
        }

        /// <summary>
        /// Count the number of lines, etc in the file specified.
        /// </summary>
        /// <param name="f">The filename to count.</param>
        /// <returns>The number of lines in the file.</returns>  
        public void ReadFile(string f)
        {
            if (f.EndsWith("AssemblyInfo.cs"))  // ignore this whole file.
                return;

            if (Verbose)
            {
                Console.WriteLine($"{GetTree(1)}File: {f.Substring(RootDir.Length)}");
            }

            NumberOfFiles++;
            var lineState = new LineState();

            using (var r = new StreamReader(f))
            {
                bool hasCode = false;   // found some code in the file.
                string lineRaw;
                while ((lineRaw = r.ReadLine()) != null)
                {
                    lineState.LineNumber++;
                    NumberOfLines++;
                    NumberOfChars += lineRaw.Length;

                    if (NumberOfChars - NumberOfCharsMsg > 16 * 1024 * 1024)
                    {
                        Console.WriteLine(".");
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

                    if (lineState.HasError)
                    {
                        NumberOfErrors++;
                        Console.WriteLine($"Error at {f.Substring(RootDir.Length)}:{lineState.LineNumber}");
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
                            if (Verbose)
                            {
                                Console.WriteLine($"{GetTree(2)}Class: {lineCode}");
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
                                if (Verbose)
                                {
                                    Console.WriteLine($"{GetTree(3)}Method: {lineCode}");
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
                Console.WriteLine($"{f.Substring(RootDir.Length)} has {lineState.OpenBraceCount} unmatched braces from {lineState.OpenBrace.Pop()}.");
                NumberOfErrors++;
            }
        }

        public void ReadDir(string dirPath)
        {
            // Recursive dir reader.

            var d = new DirectoryInfo(dirPath);     //  Assuming Test is your Folder

            bool showDir = false;   // only show dir if it has files.

            // deal with files first.
            int filesInDir = 0;
            var Files = d.GetFiles("*.*");      // Getting all files
            foreach (FileInfo file in Files)
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden) && file.Attributes.HasFlag(FileAttributes.System))   // e.g. Thumb.db
                    continue;

                string ext = Path.GetExtension(file.Name);
                if (!_exts.Contains(ext))    // ignore this
                    continue;

                if (this.Verbose && !showDir)
                {
                    Console.WriteLine($"{GetTree(0)}Dir: {dirPath.Substring(RootDir.Length)}");
                    showDir = true;
                }

                filesInDir++;
                ReadFile(file.FullName);
            }

            if (filesInDir > 0)
            {
                NumberOfDirectories++;
            }

            // Recurse into dirs.
            var Dirs = d.GetDirectories();
            foreach (var dir in Dirs)
            {
                if (_dirsEx.Contains(dir.Name))     // excluded dir
                    continue;
                if (dir.Name.StartsWith("."))       // hidden
                    continue;
                ReadDir(dir.FullName);
            }
        }
    }

    public class Program
    {
        // The main entry point.
        // https://blog.codinghorror.com/coding-without-comments/

        static void Main(string[] args)
        {
            // Main entry point.

            Console.WriteLine("Code Counter v1");

            bool waitOnDone = false;
            var stats = new Stats();
            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                    continue;

                string argL = arg.ToLower();

                if (argL == "-help" || argL == "-?")
                {
                    Console.WriteLine("CodeCounter walks a directory of .cs sources and compiles some statistics.");
                    Console.WriteLine("Use: CodeCounter -flag directory");
                    Console.WriteLine("-wait");
                    Console.WriteLine("-tree");
                    Console.WriteLine("-verbose");
                    return;
                }
                if (argL == "-wait")
                {
                    waitOnDone = true;
                    continue;
                }
                if (argL == "-tree")
                {
                    stats.MakeTree = true;
                    continue;
                }
                if (argL == "-verbose")
                {
                    stats.Verbose = true;
                    continue;
                }
                if (argL.StartsWith("-"))
                {
                    Console.WriteLine("Bad Arg");
                    return;
                }
                stats.RootDir = arg;
            }
            if (string.IsNullOrWhiteSpace(stats.RootDir))
                stats.RootDir = Environment.CurrentDirectory;       // just use current dir.

            Console.WriteLine($"Read Dir '{stats.RootDir}' for files of type {String.Join(",", Stats._exts)}");

            stats.ReadDir(stats.RootDir);
            stats.DumpStats();

            if (waitOnDone)
            {
                Console.WriteLine("Press Enter to Continue");
                Console.ReadKey();
            }
        }
    }
}
