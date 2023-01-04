//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
//
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CodeCounter
{
    public enum CppModeId
    {
        // What read mode am i in ? based on CPP tokens. (though not really tokens)

        GLOBAL,     // file global level. look for a definition of something, namespace, function, class, etc.
        STATEMENT,      // CppKeyId.FOR, etc.

        ASM,
        ASM_CMD,
        ASM_BRACE,

        // These are nestable.
        BRACE,
        PARENTH,
        BRACKET,

        // This is weird pre-process stuff.
        COMMENT,        // old style.
        LINECOMMENT,
        PREPROCESS,     // #define, etc

        // These are normally just one line but can continue with slash at line end.
        CONSTQUOTE,
        CONSTCHAR,
        CONSTQUOTERAW,  // Raw literal R"(stuff)";
    }
    public class CppModeType
    {
        // CPP mode tokens.
        public readonly CppModeId Id;
        public readonly char Token;      // cpp token for the mode. if applicable
        public readonly string Description;

        public CppModeType(CppModeId id, char token, string desc)
        {
            Id = id;
            Token = token;
            Description = desc;
        }

        public static readonly CppModeType[] kTokTypes = new CppModeType[]
        {
            new CppModeType(CppModeId.GLOBAL,             '.',    "global code space. (default)"),
            new CppModeType(CppModeId.STATEMENT,          ';',    "for() if() switch() - must have following statement."),

            new CppModeType(CppModeId.ASM,                ';',    "_asm These are weird."),
            new CppModeType(CppModeId.ASM_CMD,            ';',    "requires no ;"),
            new CppModeType(CppModeId.ASM_BRACE,          '}',    "ignore stuff in here."),

	        // These are nestable.
            new CppModeType(CppModeId.BRACE,              '}',    "{ optionalfunction(); } We are inside at least one set of braces.") ,
            new CppModeType(CppModeId.PARENTH,            ')',    "() We are inside at least one set of paren"),
            new CppModeType(CppModeId.BRACKET,            ']',    "[]"),

        	// This is weird pre-process stuff.
            new CppModeType(CppModeId.COMMENT,            '/',    "old style comment /* (MSC nests these). interrupts other modes."),
            new CppModeType(CppModeId.LINECOMMENT,        '/',    "Comment to the end of the line. (cannot really continue)"),
            new CppModeType(CppModeId.PREPROCESS,         '#',    "#define or #if takes the whole line. (can continue)"),

	        // These are normally just one line but can continue with slash at line end.
            new CppModeType(CppModeId.CONSTQUOTE,         '\"',   "some thing in double quotes"),
            new CppModeType(CppModeId.CONSTCHAR,          '\'',   "something in single quotes"),
            new CppModeType(CppModeId.CONSTQUOTERAW,      '\"',   "raw in double quotes like R\"(STUFF)\""),
        };
    }

    public enum CppKeyId
    {
        // c,cpp keywords.
        // TODO sorted ?

        DEFAULT,    // no args;   
        BREAK,      // no args;
        CONTINUE,   // no args;

        CASE,       // has arguments
        _ASM,       // special. has arguments

        // After this must have arguments.
        FOR,            // STATEMENT1
        IF,
        SWITCH,
        WHILE,
        ELSE,
        DO,
        STRUCT,		//  
        CLASS,
        UNION,
        ENUM,
        RETURN,
        SIZEOF,
        GOTO,
        TYPEDEF,

        // interface ?
        QTY,
    }
    public class CppKeyword
    {
        // c,cpp keywords that have following syntax expected.

        public readonly CppKeyId Id;
        public readonly string Keyword;      // cpp command keyword
        public readonly string Description;

        public CppKeyword(CppKeyId id, string keyword, string desc)
        {
            Id = id;
            Keyword = keyword;
            Description = desc;
        }

        public static readonly CppKeyword[] kKeywords = new CppKeyword[]
        {
            new CppKeyword(CppKeyId.DEFAULT,    "default",  "default: // in switch "),
            new CppKeyword(CppKeyId.BREAK,      "break",    "break; // in switch or for, while"),
            new CppKeyword(CppKeyId.CONTINUE,   "continue", "continue;"),
            new CppKeyword(CppKeyId.CASE,       "case",     "case x: // label in switch"),

            new CppKeyword(CppKeyId._ASM,        "_asm",     "_asm x or _asm {;} // ignore what is in here ? or"),

            new CppKeyword(CppKeyId.FOR,        "for",      "for (;;) {;}"),
            new CppKeyword(CppKeyId.IF,         "if",       "if (x) {;}"),
            new CppKeyword(CppKeyId.SWITCH,     "switch",   "switch (x) { case: default: ;}"),
            new CppKeyword(CppKeyId.WHILE,      "while",    "while (x) {;}"),

            new CppKeyword(CppKeyId.ELSE,       "else",     "else [if] {;}"),
            new CppKeyword(CppKeyId.DO,         "do",       "do {;} while (x);"),
            new CppKeyword(CppKeyId.STRUCT,     "struct",   "struct x {} ;"),
            new CppKeyword(CppKeyId.CLASS,      "class",    "class x {} ;"),
            new CppKeyword(CppKeyId.UNION,      "union",    "union x {} ;"),
            new CppKeyword(CppKeyId.ENUM,       "enum",     "enum x {,} ;"),

            new CppKeyword(CppKeyId.RETURN,     "return",   "return(;);"),
            new CppKeyword(CppKeyId.SIZEOF,     "sizeof",   "sizeof(x) ;"),

            new CppKeyword(CppKeyId.GOTO,       "goto",     "goto x;"),
            new CppKeyword(CppKeyId.TYPEDEF,    "typedef",  "typedef xtype x;"),
        };

        public static CppKeyId Find(string line, int i, int length)
        {
            // @return = CppKeyId
            for (int j = 0; j < kKeywords.Length; j++)
            {
                if (string.Compare(line, i, kKeywords[j].Keyword, 0, length) == 0 && length == kKeywords[j].Keyword.Length)
                    return (CppKeyId)j;
            }
            return CppKeyId.QTY;
        }
    }

    public class CppMarker : CodeMarker
    {
        // Classify/mark a range of code as this CppModeId of group.
        public readonly CppModeId _eMode;  // what mode was it at the marker ?

        public CppMarker(CppModeId eMode, int lineNumber, int lineOffset)
            : base(lineNumber, lineOffset)
        {
            _eMode = eMode;
        }
    }

    public class CppMarkerStack
    {
        // Formatting a 'C' style file.
        // Keep track of a stack of nested CppMarker. swapped out if preprocessor.

        public const int kMaxNestLevels = 100; // Arbitrary max nesting level.
        public readonly Stack<CppMarker> _Stack; // open braces etc.

        public int LineNumber;  // What line number did this start on ? 0 = from start of file.
        public bool If0;        // its all #if 0 commented out code.

        public int _CountBrace;
        public int _CountParenth;

        public bool isEmpty()
        {
            Debug.Assert(_Stack.Count < kMaxNestLevels);
            return _Stack.Count <= 0;
        }

        public CppModeId GetCurMode()
        {
            if (isEmpty())
                return CppModeId.GLOBAL;
            return _Stack.Peek()._eMode;
        }
        public int GetCurLineNumber()
        {
            if (isEmpty())
                return 0;
            return _Stack.Peek().LineNumber;
        }
        public int GetCurLineOffset()
        {
            CppMarker rTop = _Stack.Peek();
            return rTop.LineOffset;
        }

        public void Push(CppModeId eMode, int lineNumber, int lineOffset)
        {
            // Push marker onto the stack.
            int linePrev = GetCurLineNumber();
            Debug.Assert(lineNumber > linePrev || (lineNumber == linePrev && lineOffset > GetCurLineOffset()));
            switch (eMode)
            {
                case CppModeId.ASM_BRACE:
                case CppModeId.BRACE: ++_CountBrace; break;
                case CppModeId.PARENTH: ++_CountParenth; break;
            }
            _Stack.Push(new CppMarker(eMode, lineNumber, lineOffset));
        }

        public CppModeId PopX(CppModeId eMode, CodeReader reader)
        {
            // Pop expected CppModeId.
            switch (eMode)
            {
                case CppModeId.ASM_BRACE:
                case CppModeId.BRACE: --_CountBrace; break;
                case CppModeId.PARENTH: --_CountParenth; break;
            }
            if (isEmpty())
            {
                // When did we go off the rails ? last block?
                reader.AddError($"Unmatched {eMode} block, mode={GetCurMode()}");
                return CppModeId.GLOBAL;
            }
            if (eMode != GetCurMode()) // This should never happen !
            {
                reader.AddError($"internal error. bad mode {eMode}!={GetCurMode()}");
            }
            _Stack.Pop();
            return GetCurMode(); // previous mode.
        }

        public CppModeId PopStatement(CppModeId eMode, CodeReader reader)
        {
            // This is also the end of the CppModeId.STATEMENT. Pop that too.
            // RETURN: Current mode.
            do
            {
                eMode = PopX(eMode, reader);
            } while (eMode == CppModeId.STATEMENT);     // why More than 1 Statement ???
            return eMode;
        }

        public CppMarkerStack(CppMarkerStack clone, int lineNumber, bool if0)
        {
            // Make a copy of the stack for preprocessors.
            Debug.Assert(clone != null);
            LineNumber = lineNumber;
            _Stack = new Stack<CppMarker>(clone._Stack);
            If0 = clone.If0 || if0;
            _CountBrace = clone._CountBrace;
            _CountParenth = clone._CountParenth;
        }
        public CppMarkerStack()
        {
            LineNumber = 0;
            _Stack = new Stack<CppMarker>();
            _CountBrace = 0;
            _CountParenth = 0;
        }
    }

    //**********************************************************************************************

    /// <summary>
    /// The state (of a line) inside a single .cpp source file.
    /// </summary>
    public class CppReader : CodeReader
    {
        public const string kExtSrc = ".cpp"; // ".cpp";  
        public const string kExtProj = ".vcxproj";  // .vcproj

        private string _Line;

        private CppMarkerStack _MarkerStack = new CppMarkerStack();

        private Stack<CppMarkerStack> _PreProcStacks = new Stack<CppMarkerStack>();  // conditionally compiled code. pre-process. #if

        static int FindIn(string line, int i, string[] table)
        {
            // Find a matching segment in the array
            // @return = int = CppKeyId

            for (int j = 0; j < table.Length; j++)
            {
                if (string.Compare(line, i, table[j], 0, table[j].Length) != 0)
                    continue;
                int k = i + table[j].Length;
                if (k >= line.Length || !CodeUtil.IsNameChar(line[k]))
                    return j;
            }
            return -1;
        }

        int GetNonWhiteSpace(int i)
        {
            // get index past whitespace . assume newline is whitespace.
            int j = i;
            for (; j < _Line.Length && char.IsWhiteSpace(_Line[j]); j++)
            { }
            return j;
        }
        bool IsLineEnd(int i)
        {
            // is i beyond the end?
            if (i < 0 || i >= _Line.Length)
                return true;
            return CodeUtil.IsNL(_Line[i]);
        }
        char GetLineChar(int i)
        {
            if (IsLineEnd(i))
                return '\0';
            return _Line[i];
        }
        bool IsLabelEnd(int k)
        {
            // NOT a double ::
            return GetLineChar(k) == ':' && GetLineChar(k + 1) != ':';
        }

        public int GetNameEndIndex(int i)
        {
            // Skip to the end of a name.
            // @return = index of first non label char.
            int j = i;
            for (; j < _Line.Length; j++)
            {
                char ch = _Line[j];
                if (CodeUtil.IsNameChar(ch))
                    continue;
                if ((ch >= '0' && ch <= '9') && i != j)   // not first. // valid label char.
                    continue;
                break;
            }
            return j;
        }

        static readonly string[] _PreProcs =
        {
            "include", "if", "ifdef", "ifndef", "elif", "else", "endif"
        };
        static readonly string[] _PreProcsIf0 = // commented out code is not parsed.
        {
            "if 0",    // Most common.
            "if(0)",
            "if (0)",
            "if defined(COMMENT)",
            "ifdef(0)",
            "ifdef (0)",
            "ifdef COMMENT",
        };

        bool ProcessLine(CodeStats stats, ProjectReference proj)
        {
            // a single line.
            //  @return = false = fail.

            bool isLineBlank = true; // does it seem to be blank so far?
            if (string.IsNullOrWhiteSpace(_Line))
            {
                return true;
            }

            int iStartWhiteSpace = 0;
            int iTabNum = -1;

            CppModeId eMode = _MarkerStack.GetCurMode(); // what mode are we in right now ?
            CppModeId eStartMode = eMode;

            int i = 0;
            for (; i < _Line.Length; i++)
            {
                char ch = _Line[i];

                if (char.IsWhiteSpace(ch))  // Nobody cares about white space really.
                {
                    if (CodeUtil.IsNL(ch))  // newline is end of line. 
                    {
                        break;
                    }
                    if (isLineBlank) // Count Starting whitespace on the line.
                    {
                        iStartWhiteSpace++;
                    }
                    continue;
                }

                if (isLineBlank)    // was blank so far.
                {
                    // First non white space on the line.
                    // What is the correct tabbing for this.

                    if (_MarkerStack.If0 && ch != '#')  // skip the rest of this line.
                    {
                        break; 
                    }

                    isLineBlank = false; // used for preprocessor stuff
                    iTabNum = _MarkerStack._CountBrace;
                    switch (eMode)
                    {
                        case CppModeId.STATEMENT:
                        case CppModeId.ASM:
                            if (ch != '{')
                                iTabNum++;
                            break;
                        case CppModeId.PARENTH:
                        case CppModeId.BRACKET:
                            iTabNum++;
                            break;
                        case CppModeId.ASM_BRACE:
                        case CppModeId.BRACE:
                            if (ch == '}')
                                iTabNum--;
                            break;
                    }
                }

                // bool globalScope = false;

                // Mode specific stuff. NOT a blank char.
                switch (eMode)
                {
                    case CppModeId.GLOBAL:   // normal code mode. (default)   
                                             // globalScope = true;

                    do_check_statement:  // Look for a statement. // CppModeId.STATEMENT

                        int j = GetNameEndIndex(i);
                        if (j <= i)
                            break;

                        this.HasCode = true; // not a comment and not a space. must be code.

                        // Any statement with '_' can also have 2 for some reason. Skip them and assume they are ignored. is this true ???
                        while (ch == '_' && GetLineChar(i) == '_') i++;

                        // If I wanted this to be fast I would do binary search. ???
                        CppKeyId iCKeyWord = CppKeyword.Find(_Line, i, j - i);
                        int k = GetNonWhiteSpace(j); // skip spaces after statement.

                        switch (iCKeyWord)
                        {
                            case CppKeyId.DEFAULT:
                            case CppKeyId.BREAK:
                            case CppKeyId.CONTINUE:
                                // These take no arguments.
                                break;
                            case CppKeyId.CASE:
                                if (iStartWhiteSpace == i) // white space up to this point to be valid.
                                    iTabNum--;
                                break;

                            case CppKeyId._ASM:   // This is special.
                                eMode = CppModeId.ASM;
                                _MarkerStack.Push(eMode, LineNumber, i);
                                break;

                            case CppKeyId.STRUCT:
                            case CppKeyId.CLASS:
                            case CppKeyId.UNION:
                            case CppKeyId.ENUM:
                                stats.NumberOfClasses++;
                                eMode = CppModeId.STATEMENT;
                                _MarkerStack.Push(eMode, LineNumber, i);
                                break;

                            case CppKeyId.QTY:      // Not a known keyword.
                                if (eMode != CppModeId.GLOBAL)  // Is the statement 'case' or a label: 
                                {
                                    if (IsLabelEnd(k))
                                        goto case CppKeyId.CASE;
                                }
                                if (eMode != CppModeId.STATEMENT)
                                {
                                    eMode = CppModeId.STATEMENT;    // must be followed by ;
                                    _MarkerStack.Push(eMode, LineNumber, i);
                                }
                                break;

                            default:
                                // a keyword that takes arguments. e.g. for if while statements
                                eMode = CppModeId.STATEMENT;
                                _MarkerStack.Push(eMode, LineNumber, i);
                                break;
                        }

                        i = k - 1;
                        continue;

                    case CppModeId.ASM:
                        // _asm command
                        if (ch == '/')  // catch this comment below
                            break;

                        this.HasCode = true;
                        _MarkerStack.PopX(eMode, this);
                        eMode = (ch == '{') ? CppModeId.ASM_BRACE : CppModeId.ASM_CMD;

                    do_open_mode1:
                        _MarkerStack.Push(eMode, LineNumber, i);
                        continue;

                    case CppModeId.ASM_CMD:
                        if (ch == '/')   // catch this comment below
                            break;
                        if (ch == ';')
                        {
                            eMode = CppModeId.LINECOMMENT;
                            goto do_open_mode1;
                        }
                        this.HasCode = true;
                        continue;

                    case CppModeId.ASM_BRACE:    // ignore anything in here.
                        if (ch == '}')
                        {
                            this.HasCode = true;
                            eMode = _MarkerStack.PopX(eMode, this);
                            continue;
                        }
                        goto case CppModeId.ASM_CMD;

                    case CppModeId.STATEMENT:    // "for", "while", "if", or function call statement
                        if (ch == ';')  // end of statement.
                        {
                            this.HasCode = true;
                            eMode = _MarkerStack.PopStatement(eMode, this);
                            continue;
                        }
                        if (ch == '}' || ch == ')')
                        {
                            // Data definitions can end without ; e.g. TYPE x = { statement }; or method( sdfsfd )
                            this.HasCode = true;
                            eMode = _MarkerStack.PopStatement(eMode, this);
                            if (eMode != CppModeId.BRACE && eMode != CppModeId.PARENTH)
                            {
                                // error.
                                AddError($"unmatched ending '{ch}' block (no opening) at offset {i}");
                                return false;
                            }
                            eMode = _MarkerStack.PopStatement(eMode, this);
                            continue;
                        }

                        goto do_check_statement;

                    case CppModeId.BRACE:
                        Debug.Assert(_MarkerStack._CountBrace > 0);
                        if (ch == '}')
                        {
                            eMode = _MarkerStack.PopStatement(eMode, this);
                            continue;
                        }
                        goto do_check_statement;

                    case CppModeId.PARENTH:
                        if (ch == ')')
                        {
                            this.HasCode = true;
                            eMode = _MarkerStack.PopX(eMode, this);
                            continue;
                        }
                        break;

                    case CppModeId.BRACKET:
                        if (ch == ']')
                        {
                            this.HasCode = true;
                            eMode = _MarkerStack.PopX(eMode, this);
                            continue;
                        }
                        break;

                    case CppModeId.COMMENT: // old style comment /* (MSC nests these)

                        HasComment = true;
                        if (ch == '*' && GetLineChar(i + 1) == '/')    // end of comment ?
                        {
                            eMode = _MarkerStack.PopX(eMode, this);
                            i++;    // skip next.
                            continue;
                        }
                        // Allow nested comments ??  
                        HasCommentText = true;
                        continue;

                    case CppModeId.CONSTQUOTE:      // some thing in ""
                    case CppModeId.CONSTCHAR:       // something in ''. Must end before line !
                        // These are normally just one line but can continue with slash at end.
                        char chEnd = (eMode == CppModeId.CONSTCHAR) ? '\'' : '\"';
                        if (ch == '\\') // skip encoded.
                        {
                            if (IsLineEnd(i + 1)) // \ At the end of the line make the line continue to the next line.
                            {
                                // This will continue on the next line
                                i++;
                                goto do_end_of_line;
                            }
                            // Loop for escape character type sequence in the string or constant.
                            this.HasCode = true;
                            i++;    // else just skip the next char. (could be something like "\"")
                            continue;
                        }
                        this.HasCode = true;
                        if (ch == chEnd)
                        {
                            eMode = _MarkerStack.PopX(eMode, this);
                        }
                        continue;

                    case CppModeId.CONSTQUOTERAW:
                        this.HasCode = true;
                        if (ch == '\"' && GetLineChar(i-1) == ')')
                        {
                            eMode = _MarkerStack.PopX(eMode, this);
                        }
                        continue;

                    case CppModeId.LINECOMMENT:  // rest of the line is a comment.
                        HasComment = true;
                        HasCommentText = true;
                        continue;

                    case CppModeId.PREPROCESS:       // #define or #if takes the whole line.
                        // Technically there can be nothing else on this line.  We will allow comments though.
                        if (ch == '/')   // catch this comment below
                            break;
                        if (ch == '\\')
                        {
                            if (IsLineEnd(i + 1)) // \ At the end of the line make the line continue to the next line.
                            {
                                // This will continue on the next line
                                i++;
                                goto do_end_of_line;
                            }
                        }
                        this.HasCode = true;
                        continue;


                    default:
                        Debug.Assert(false);    // should never get here.
                        break;
                }

                // Process normal statement.

                switch (ch)
                {
                    case '\"':
                        this.HasCode = true;    // Quoted constant counts as code.
                        if (GetLineChar(i - 1) == 'R' && GetLineChar(i + 1) == '(')
                        {
                            i++;
                            eMode = CppModeId.CONSTQUOTERAW;
                        }
                        else
                        {
                            eMode = CppModeId.CONSTQUOTE;
                        }
                    do_open_mode2:
                        _MarkerStack.Push(eMode, LineNumber, i);
                        continue;
                    case '\'':
                        this.HasCode = true;    // Quoted constant counts as code.
                        eMode = CppModeId.CONSTCHAR;
                        goto do_open_mode2;
                    case '{':
                        // TODO IS this a method definition?
                        this.HasCode = true;
                        eMode = CppModeId.BRACE;
                        goto do_open_mode2;
                    case '(':
                        this.HasCode = true;
                        eMode = CppModeId.PARENTH;
                        goto do_open_mode2;
                    case '[':
                        this.HasCode = true;
                        eMode = CppModeId.BRACKET;
                        goto do_open_mode2;

                    case '}':
                    case ')':
                    case ']':
                        // Unmatched close brace,bracket, or parenth.
                        AddError($"unmatched '{ch}', looking for '{CppModeType.kTokTypes[(int)eMode].Token}'");
                        // Try the prev 2 levels for a match ???
                        return false;               // continue; ?

                    case '/':
                        if (GetLineChar(i + 1) == '/') // CppModeId.LINECOMMENT
                        {
                            if (eMode == CppModeId.PREPROCESS)
                                _MarkerStack.PopX(eMode, this);
                            i++;    // skip next.
                            HasComment = true;
                            eMode = CppModeId.LINECOMMENT;
                            goto do_open_mode2;
                        }
                        if (GetLineChar(i + 1) == '*')  // old style /* CppModeId.COMMENT */
                        {
                            i++;    // skip next.
                            HasComment = true;
                            eMode = CppModeId.COMMENT;
                            goto do_open_mode2;
                        }
                        continue;

                    case '#':   // Look for start of preprocessor directives.

                        if (iStartWhiteSpace != i)
                            continue;   // MUST have white space up to this point to be valid.
                        eMode = CppModeId.PREPROCESS;   // assume we will goto do_open_mode2
                        iTabNum = 0;       // left flush these always.
                        this.HasCode = true;

                        switch (FindIn(_Line, i + 1, _PreProcs))
                        {
                            case 0:     // #include "" or <>
                                // TODO Read this file now ???
                                i += 7;
                                goto do_open_mode2;
                            case 1:     // #if  
                            case 2:     // #ifdef
                            case 3:     // #ifndef
                                _PreProcStacks.Push(_MarkerStack);
                                _MarkerStack = new CppMarkerStack(_MarkerStack, LineNumber, FindIn(_Line, i + 1, _PreProcsIf0) >= 0);
                                this.HasCommentCode |= _MarkerStack.If0;
                                i += 2;
                                goto do_open_mode2;
                            case 4:     // #elif
                            case 5:     // #else should start from the same point stackwise as the #if did.
                                i += 4;
                                _MarkerStack = new CppMarkerStack(_PreProcStacks.Peek(), LineNumber, false);
                                goto do_open_mode2;
                            case 6:     // #endif
                                if (_PreProcStacks.Count <= 0)  // lost count . do nothing !
                                {
                                    AddError("Mismatched #endif");
                                    return false;
                                }
                                i += 5;
                                var prev = _PreProcStacks.Pop();   // Keep the new _MarkerStack
                                if (_MarkerStack.If0)
                                {
                                    _MarkerStack = prev;
                                }
                                goto do_open_mode2;  // Restore previous code ?

                            default:    // #define etc. no special action.
                                goto do_open_mode2;
                        }
                }
            }

        do_end_of_line:   // End of line. Mode clean up at end of line.  

            switch (eMode)
            {
                case CppModeId.CONSTQUOTE:       // some thing in ""
                case CppModeId.CONSTCHAR:        // something in ''.
                                                 // These modes cannot legally span lines !
                    if (GetLineChar(i - 1) == '\\')
                    {
                        break;
                    }

                    AddError("new line in constant");
                    _MarkerStack.PopX(eMode, this);
                    return false; // fail.

                case CppModeId.ASM_CMD:
                    eMode = _MarkerStack.PopStatement(eMode, this);
                    break;

                case CppModeId.LINECOMMENT:  // Comment to the end of the line. (cannot really continue)
                    eMode = _MarkerStack.PopX(CppModeId.LINECOMMENT, this);    // automatically close the mode at the end of the line.
                    break;

                case CppModeId.PREPROCESS:       // automatically close the mode at the end of the line.
                    iTabNum = (eStartMode == CppModeId.PREPROCESS) ? 1 : 0;
                    if (GetLineChar(i - 1) == '\\')
                    {
                        break;
                    }
                    eMode = _MarkerStack.PopX(CppModeId.PREPROCESS, this); // automatically close the mode at the end of the line.
                    break;
            }

            // Alter code line based on closing modes.
            switch (eMode)
            {
                case CppModeId.BRACE:
                    if (_MarkerStack.GetCurLineNumber() == LineNumber && _MarkerStack.GetCurLineOffset() != iStartWhiteSpace)
                    {
                        // Make sure the open brace is not at the end of the line?                      
                    }
                    break;

                case CppModeId.COMMENT:
                    // end line with open old style comment. its ok.
                    break;
            }

            return true;    // ok
        }

        public int ReadFile(CodeStats stats, StreamReader rdr, string fileRel, ProjectReference proj, bool isLastFile)
        {
            // Process a CPP file.
            // proj = my project.
            // isLastFile

            LineNumber = 0;

            while (true)
            {
                // Did we break the previous line ?

                string lineRaw = rdr.ReadLine();
                if (lineRaw == null)
                    break;
                LineNumber++;
                stats.OnReadLine(lineRaw.Length);
                _Line = lineRaw;

                // Temporary modes for this line only.
                base.ClearLine();

                ProcessLine(stats, proj);
                stats.OnCountLine(this);
            }

            // Test if any open braces or comments were not closed. If so fail !

            if (!_MarkerStack.isEmpty())
            {
                var eMode = _MarkerStack.GetCurMode();
                AddError($"Unclosed {eMode} block type '{CppModeType.kTokTypes[(int)eMode].Token}'");
            }
            if (_PreProcStacks.Count > 0)
            {
                AddError("Unclosed preprocessor block");
            }

            // Show results.
            stats.DumpSrcFile(fileRel, isLastFile, Errors);
            stats.DumpClasses(proj.Classes);
            return this.LineNumber;
        }
    }
}
