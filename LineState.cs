//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections;

namespace CodeCounter
{
    /// <summary>
    /// The state (of a line) inside a single .cs source file.
    /// </summary>
    internal class LineState
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
        public bool HasCommentText;        // text in comment.
        public bool HasCommentCode;        // looks like commented out code (junk text). This doesnt count as a real/useful comment.
        public string Error = "";               // some sort of syntax error.

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
                        continue;    // this doesnt count. keep looking
                    }

                    OpenAtQuote = false;
                }
                else if (IsSpecial(line, j))
                {
                    i = j;
                    continue;    // this doesnt count. keep looking
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
                return line;
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
                        // Console.WriteLine($"{f.Substring(RootDir.Length)} has {lineState.OpenBraceCount} unmatched braces from {lineState.OpenBrace.Pop()}.");
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
    }
}
