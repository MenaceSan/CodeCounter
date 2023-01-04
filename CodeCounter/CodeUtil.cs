using System.Collections.Generic;

namespace CodeCounter
{
    public static class CodeUtil
    {
        // shared util functions.

        public static int BitMask(int bitNumber)
        {
            return (1 << bitNumber);
        }
        public static bool IsBit(int bitMask, int bitNumber)
        {
            return (bitMask & (1 << bitNumber)) != 0;
        }


        public static bool IsNL(char ch)
        {
            return ch == '\n' || ch == '\r';
        }
        public static bool IsNameChar(char ch)
        {
            // legal char for symbolic name ?
            if (ch >= 'a' && ch <= 'z')
                return true;
            if (ch >= 'A' && ch <= 'Z')
                return true;
            if (ch == '_')
                return true;
            return false;
        }
    }

    public class CodeClass
    {
        // A class defined in a file in a module. (handle partial?)
        // a SQL table is the same as a class.

        public const string kGlobalName = "_";      // .cpp global namespace container. 

        public readonly string Name;    // name of the struct,class,interface. "_" = global.
        public NameSpaceLevel NameSpace;        // What namespace is this name in ?

        public List<CodeClass> BaseRefs;        // What is this class based on ? Interfaces and overloaded base classes.
        public readonly List<string> Methods = new List<string>();

        public CodeClass(string name)
        {
            Name = name;
        }
    }

    public class CodeMarker
    {
        // mark the location we found some sort of token in the code file. e.g. a brace?
        public readonly int LineNumber;      // 1 based line number in the file.
        public readonly int LineOffset;      // offset into line. 0 based.
        // public readonly int FileOffset;         // Offset into the source file. 0 based. 

        public CodeMarker(int lineNumber, int lineOffset)
        {
            LineNumber = lineNumber;
            LineOffset = lineOffset;
        }
    }

    public abstract class CodeReader
    {
        // base class for CppReader and CsReader.

        public List<string> Errors = null;       // some sort of syntax error on a line

        public int LineNumber = 0;      // 1 based. what line am i processing?

        public bool HasCode;            // line not a comment. looks like code. (maybe @quoted string = whole line)
        public bool HasComment;         // line possibly empty comment. 
        public bool HasCommentText;     // line text in comment.
        public bool HasCommentCode;     // line looks like commented out code (junk text). This doesn't count as a real/useful comment.

        public void AddError(string err)
        {
            // add line and file name prefix to error.
            if (string.IsNullOrWhiteSpace(err))
                return;
            if (Errors == null)
                Errors = new List<string>();
            Errors.Add($"{err} (at line {LineNumber})");
        }

        public void ClearLine()
        {
            // moved to a new line. clear stats.
            
            HasCode = false;
            HasComment = false;
            HasCommentText = false;
            HasCommentCode = false;
        }
    }

}
