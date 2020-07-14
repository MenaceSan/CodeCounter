//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace CodeCounter
{
    /// <summary>
    /// The state (of a line) inside a single .cs source file.
    /// </summary>
    internal class CsLineState
    {
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
        public bool HasCommentText;     // text in comment.
        public bool HasCommentCode;     // looks like commented out code (junk text). This doesn't count as a real/useful comment.
        public string Error = "";       // some sort of syntax error.

        public void ClearLine()
        {
            HasCode = false;
            HasComment = false;
            HasCommentText = false;
            HasCommentCode = false;
            Error = "";
        }

        private static bool IsSpecial(string line, int j, char ch = '\\')
        {
            // Handle a quoted quote.
            // j = quote. 
            if (j <= 0)
                return false;
            if (line[j - 1] != ch)
                return false;
            if (j <= 1)
                return true;
            if (line[j - 2] != ch)
                return true;
            if (j <= 2)
                return false;
            if (line[j - 3] != ch)
                return false;
            return true;        // something like "\\\"" 
        }

        private int FindQuoteEnd(string line, int i)
        {
            // We are inside a quote. look for the end.
            // handle "\\" and @"\"
            // i = first quote. 

            while (true)
            {
                int j = line.IndexOf('"', i + 1);   // find close.
                if (j < 0)  // no end quote.
                {
                    if (OpenAtQuote)
                    {
                        // this is legal. // OpenAtQuote can span lines ! 
                        return -1;
                    }
                    Error += "Missing End Quote, "; // NumberOfErrors // weird NO CLOSE ??
                    return -1;
                }

                if (OpenAtQuote)
                {
                    // watch for double quote. ""

                    if (line.Length > j + 1 && line[j + 1] == '"')
                    {
                        i = j + 1;
                        continue;    // this doesn't count. keep looking
                    }

                    OpenAtQuote = false;
                }
                else if (IsSpecial(line, j))
                {
                    i = j;
                    continue;    // this doesn't count. keep looking
                }

                return j;
            }
        }

        public const string kTestMultiLine = @"
This is a test of a multi line constant string used for self testing.
";

        public string ReadLine(string line)
        {
            // return line minus any comments.

            line = line.Trim();
            if (line.Length == 0)
            {
                return line;    // empty / complete line.
            }

            if (OpenAtQuote)    // look for closing quote from line spanning quote. "
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
                    OpenAtQuote = (i >= 1 && line[i - 1] == '@');
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
                        Error += "No close of single quote, "; // NumberOfErrors
                        break;
                    }
                    if (IsSpecial(line, j))
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
                        // Console.WriteLine($"{fileRel} has {lineState.OpenBraceCount} unmatched braces from {lineState.OpenBrace.Pop()}.");
                        Error += "No close of brace, ";
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

        public static readonly string[] kClassTypes = { "class", "enum", "struct", "interface" };
        public static readonly char[] kMethodEx = { '=', '{' };

        public int ReadFile(CodeStats stats, StreamReader rdr, string fileRel, ProjectReference proj, bool isLast)
        {
            // isLast = is Last File in Dir.

            if (fileRel.EndsWith("AssemblyInfo.cs"))  // always ignore this file for comment counting purposes.
                return 0;

            var errors = new List<string>();
            var classes = (stats.Verbose || stats.MakeTree) ? new List<CodeClass>() : null;
            CodeClass class0 = null;  // All the methods for the last class.

            bool hasCode = false;   // found some code in the file.
            string lineRaw;
            while ((lineRaw = rdr.ReadLine()) != null)
            {
                this.LineNumber++;
                stats.OnReadLine(lineRaw.Length);

                if (!hasCode && lineRaw.Contains("<auto-generated"))   // ignore this whole file.
                {
                    stats.DumpFile($"{fileRel} (Auto Generated)", isLast, errors);
                    return 0;   // Dont count this. // Comment total lines might be off now??
                }

                lineRaw = lineRaw.Trim();
                if (lineRaw.StartsWith("#"))  // Ignore preprocess line if, else, pragma , etc.
                {
                    stats.NumberOfLinesCode++;
                    continue;
                }

                this.ClearLine();
                string lineCode = this.ReadLine(lineRaw);

                if (lineCode.StartsWith(NameSpaces.kUsingDecl))
                {
                    stats.NameSpaces.AddUsingDecl(proj, lineCode.Substring(NameSpaces.kUsingDecl.Length));
                }
                else if (lineCode.StartsWith(NameSpaces.kNameSpaceDecl))
                {
                    string err = stats.NameSpaces.AddNameSpaceDecl(proj, lineCode.Substring(NameSpaces.kNameSpaceDecl.Length));
                    if (!string.IsNullOrEmpty(err))
                    {
                        errors.Add($"'{err}' at {fileRel}:{this.LineNumber}");
                    }
                }

                if (!string.IsNullOrEmpty(this.Error))
                {
                    errors.Add($"'{this.Error}' at {fileRel}:{this.LineNumber}");
                }
                if (this.HasCode && this.HasCommentText)
                {
                    hasCode = true;
                    stats.NumberOfLinesCodeAndComment++;
                }
                else if (this.HasCommentText)
                {
                    stats.NumberOfCommentLines++;
                }
                else if (this.HasCode)
                {
                    hasCode = true;
                    stats.NumberOfLinesCode++;
                }
                else if (this.HasComment)
                {
                    stats.NumberOfCommentBlank++;
                    continue;
                }
                else
                {
                    stats.NumberOfLinesBlank++;
                    continue;
                }

                if (this.HasCommentText && !this.HasCode)
                {
                    this.LastLineWasComment = true;
                    if (this.LastLineWasClass)
                    {
                        stats.NumberOfClassComments++;
                        this.LastLineWasClass = false;
                    }
                    if (this.LastLineWasMethod)
                    {
                        stats.NumberOfMethodComments++;
                        this.LastLineWasMethod = false;
                    }
                }

                if (!this.HasCode || string.IsNullOrEmpty(lineCode)) // maybe a comment. done with that. 
                    continue;
                if (lineCode[0] == '[' && lineCode[lineCode.Length - 1] == ']')   // ignore attributes.
                    continue;

                // Is this a class ?
                if (!lineCode.StartsWith("{"))
                {
                    this.LastLineWasClass = false;
                }

                // what class type ?

                foreach (string cn in kClassTypes)
                {
                    int k = lineCode.IndexOf(cn);
                    if (k < 0)
                        continue;
                    if (k + cn.Length < lineCode.Length)
                    {
                        if (!char.IsWhiteSpace(lineCode[k + cn.Length]))    // Must have space after keyword.
                            break;
                    }
                    if (k > 0)
                    {
                        int j = k - 1;
                        while (j >= 0 && char.IsWhiteSpace(lineCode[j]))   // must have space before
                            j--;
                        if (j == (k - 1))
                            break;
                        if (j >= 0 && lineCode[j] == ':')   // NOTE: "where T : struct" is not a struct
                            break;
                    }

                    if (this.OpenClassBrace == 0)  // ignore child class ?
                    {
                        this.OpenClassBrace = this.OpenBraceCount + 1;
                    }
                    this.LastLineWasClass = true;
                    stats.NumberOfClasses++;
                    if (classes != null)
                    {
                        if (class0 != null)
                        {
                            classes.Add(class0);
                        }
                        class0 = new CodeClass(lineCode);
                    }
                    if (this.LastLineWasComment)
                    {
                        stats.NumberOfClassComments++;
                        this.LastLineWasComment = false;
                    }
                    break;
                }

                // is this a method ? its at the correct brace level. inside a class.
                if (this.OpenClassBrace == this.OpenBraceCount)
                {
                    this.LastLineWasMethod = false;
                    int j = lineCode.IndexOf('(');
                    if (j > 0)
                    {
                        // TODO Handle the case where ": this()" follows a constructor !!!

                        // this is a method and not a prop or field?
                        int k = lineCode.IndexOfAny(kMethodEx, 0, j); // Not a method if = or { appear before (
                        if (k < 0)
                        {
                            this.LastLineWasMethod = true;  // This is really a method.
                            stats.NumberOfMethods++;
                            if (class0 != null)
                            {
                                class0.Methods.Add(lineCode);
                            }
                            if (this.LastLineWasComment)
                            {
                                stats.NumberOfMethodComments++;
                                this.LastLineWasComment = false;
                            }
                        }
                    }
                }
                else if (!lineCode.StartsWith("{"))
                {
                    this.LastLineWasMethod = false;
                }

                this.LastLineWasComment = false;
            }

            if (this.OpenBraceCount != 0)
            {
                // What line is open brace ?? "#if" can mess this up.
                errors.Add($"{fileRel} has {this.OpenBraceCount} unmatched braces from {this.OpenBrace.Pop()}.");
            }
            if (class0 != null)
            {
                classes.Add(class0);
            }

            stats.DumpFile(fileRel, isLast, errors);
            stats.DumpClasses(classes);
            return this.LineNumber;
        }
    }
}
