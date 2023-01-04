//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections.Generic;
using System.IO;

namespace CodeCounter
{
    public enum CsMode
    {
        //  TODO what is our current reading state at a location in the file. (may be nested)

        InFile, // look for namespace, class, struct, enum, interface, etc,
        ClassDecl,  // We are declaring a class,struct, lok for { or ;
        InClass,        // look for methods etc inslide class, struct. (may be nested)
        MethodDecl,  // we are declaring a method. look for { or ;
        InMethod,       // We can make calls etc, inside the body of a method. 
        InBrace,        // brace in method.
        InComment,    // inside a line spanning comment /* XX */
        InQuote,        // inside a line spanning quote.  @"\n" can span lines.
    }

    /// <summary>
    /// The state (of a line) inside a single .cs source file.
    /// TODO deal with pre-processed code comments.
    /// </summary>
    internal class CsReader : CodeReader
    {
        public const string kExtSrc = ".cs";
        public const string kExtProj = ".csproj";

        public bool LastLineWasClass = false;   // last line was a class. for comment recording.
        public bool LastLineWasMethod = false;   // last line was a method. for comment recording.
        public bool LastLineWasComment = false;  // last line was a comment

        public Stack<CodeMarker> MarkerStack = new Stack<CodeMarker>();   // What line was open brace on ?
        public int OpenBraceCount => MarkerStack.Count;
        public int OpenClassBrace = 0;      // inside a class.  OpenBraceCount depth at open.
        public int OpenMethodDecl = 0;       // line number for method declaration.
        public int OpenCommentLine = 0;    // Part of multi line /* comment */ . Can span lines. 1 based.
        public bool OpenAtQuote = false;        // @"\n" can span lines.

        public int MaxBraceCount = 0;       // Max we saw in this file.

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
                    AddError("No close quote");
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

        public string ProcessLine(string line)
        {
            // return a single command on the line minus any comments.
            // assume ClearLine() was called before this.
            // TODO: CS allows multiple commands on a line. we should deal with this better !!!

            line = line.Trim();
            if (line.Length == 0)
            {
                return string.Empty;    // empty . nothing here.
            }

            if (OpenAtQuote)    // look for closing quote from an open line spanning quote. "
            {
                int i = FindQuoteEnd(line, -1);
                if (i < 0)
                {
                    HasCode = true;  // consider this a blank code line.
                    return string.Empty;
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
                return ProcessLine(line.Substring(i + 2));
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
                return ProcessLine(line.Substring(2));
            }

            HasCode = true;     // this is C# code of some sort.

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
                        AddError("No close of single quote");
                        break;
                    }
                    if (IsSpecial(line, j))
                        j--;    // consider this another open quote.
                    i = j;
                    continue;
                }

                if (ch == '{')
                {
                    MarkerStack.Push(new CodeMarker(LineNumber, i));
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
                        AddError("No close of brace");
                        break;
                    }
                    MarkerStack.Pop();
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
                        return line.Substring(0, i).TrimEnd() + ProcessLine(line.Substring(i + 2));    // trim out comment.
                    }
                }
            }

            return line;
        }

        public static readonly string[] kClassTypes = { "class", "enum", "struct", "interface" };
        public static readonly char[] kMethodEx = { '=', '{', ';' };
        public static readonly char[] kMethodDef = { '{', ';' };

        public int ReadFile(CodeStats stats, StreamReader rdr, string fileRel, ProjectReference proj, bool isLastFile)
        {
            // isLastFile = is Last File in Dir.

            if (fileRel.EndsWith("AssemblyInfo.cs"))  // always ignore this file for comment counting purposes.
                return 0;

            CodeClass class0 = null;  // All the methods for the current class we are reading.

            bool hasCode = false;   // found some code in the file.
            while (true)
            {
                string lineRaw = rdr.ReadLine();
                if (lineRaw == null)
                    break;

                this.LineNumber++;
                stats.OnReadLine(lineRaw.Length);

                if (!hasCode && lineRaw.Contains("<auto-generated"))   // ignore this whole file.
                {
                    stats.DumpSrcFile($"{fileRel} (Auto Generated)", isLastFile, Errors);
                    return 0;   // Dont count this. // Comment total lines might be off now??
                }

                lineRaw = lineRaw.Trim();
                if (lineRaw.StartsWith("#"))  // Ignore preprocess line if, else, pragma , etc.
                {
                    stats.NumberOfLinesCode++;
                    continue;
                }

                this.ClearLine();
                string lineCode = this.ProcessLine(lineRaw);       // process line. strip comments.

                if (OpenMethodDecl > 0)
                {
                    // Method Param defs that can span several lines.
                    // find an opening { or ;
                    int k = lineCode.IndexOfAny(kMethodDef, 0); //  look for { or ;
                    if (k < 0)
                        continue;
                    this.OpenMethodDecl = 0;
                    lineCode = lineCode.Substring(0, k + 1);
                }

                if (lineCode.StartsWith(NameSpaces.kUsingDecl))
                {
                    stats.NameSpaces.AddUsingDecl(proj, lineCode.Substring(NameSpaces.kUsingDecl.Length));
                }
                else if (lineCode.StartsWith(NameSpaces.kNameSpaceDecl))
                {
                    string err = stats.NameSpaces.AddNameSpaceDecl(proj, lineCode.Substring(NameSpaces.kNameSpaceDecl.Length));
                    if (!string.IsNullOrEmpty(err))
                    {
                        AddError(err);
                    }
                }

                hasCode |= this.HasCode;
                if (!stats.OnCountLine(this))
                    continue;

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
                if (!lineCode.StartsWith("{") && this.LastLineWasClass)
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
                    stats.NumberOfClasses++;    // found a new class.

                    if (stats.IsReadingClasses)
                    {
                        proj.AddClass(class0);
                        class0 = new CodeClass(lineCode);
                    }
                    if (this.LastLineWasComment)
                    {
                        stats.NumberOfClassComments++;
                        this.LastLineWasComment = false;
                    }
                    break;
                }

                // is this a method def ? its at the correct brace level. inside a class.
                if (this.OpenClassBrace == this.OpenBraceCount)
                {
                    this.LastLineWasMethod = false;
                    int j = lineCode.IndexOf('('); // looks like a method decl.
                    if (j > 0)
                    {
                        // this is a method and not a prop or field?
                        int k = lineCode.IndexOfAny(kMethodEx, 0, j); // Not a method if = or { appear before the (
                        if (k < 0)
                        {
                            // This looks like a method def.
                            k = lineCode.IndexOfAny(kMethodDef, j); //  look for { or ;
                            if (k < 0)
                            {
                                this.OpenMethodDecl = this.LineNumber;   // look for end of multi line method def.
                            }
                            this.LastLineWasMethod = true;  // This is really a method. Must have an open brace or ; (for interface partial etc)
                            stats.NumberOfMethods++;
                            class0?.Methods.Add(lineCode);   // method sig.

                            if (this.LastLineWasComment)    // had a comment prefix.
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

            if (this.OpenCommentLine != 0)
            {
                AddError($"incomplete comment opened on line {this.OpenCommentLine}.");
            }
            if (this.OpenMethodDecl != 0)
            {
                AddError($"incomplete method opened on line {this.OpenMethodDecl}.");
            }
            if (this.OpenBraceCount != 0)
            {
                // What line is open brace ?? "#if" can mess this up.
                AddError($"{this.OpenBraceCount} unmatched braces from line {this.MarkerStack.Pop().LineNumber}.");
            }

            proj.AddClass(class0);

            stats.DumpSrcFile(fileRel, isLastFile, Errors);
            stats.DumpClasses(proj.Classes);
            return this.LineNumber;
        }
    }
}
