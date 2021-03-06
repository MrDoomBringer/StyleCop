//-----------------------------------------------------------------------
// <copyright file="CodeLexer.cs">
//     MS-PL
// </copyright>
// <license>
//   This source code is subject to terms and conditions of the Microsoft 
//   Public License. A copy of the license can be found in the License.html 
//   file at the root of this distribution. 
//   By using this source code in any fashion, you are agreeing to be bound 
//   by the terms of the Microsoft Public License. You must not remove this 
//   notice, or any other, from this software.
// </license>
//-----------------------------------------------------------------------
namespace StyleCop.CSharp.CodeModel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Breaks the components of a C# code file down into individual symbols.
    /// </summary>
    internal partial class CodeLexer
    {
        #region Private Fields

        /// <summary>
        /// Used for reading the source code.
        /// </summary>
        private CodeReader codeReader;

        /// <summary>
        /// The current marker in the code string.
        /// </summary>
        private MarkerData marker = new MarkerData();

        /// <summary>
        /// The source to read.
        /// </summary>
        private Code source;

        /// <summary>
        /// Keeps track of conditional directives found in the code.
        /// </summary>
        private Stack<bool> conditionalDirectives = new Stack<bool>();

        /// <summary>
        /// The list of defines in the file.
        /// </summary>
        private Dictionary<string, string> defines;

        /// <summary>
        /// The list of undefines in the file.
        /// </summary>
        private Dictionary<string, string> undefines;

        /// <summary>
        /// The C# language service.
        /// </summary>
        private CsLanguageService languageService;

        #endregion Private Fields

        #region Internal Constructors

        /// <summary>
        /// Initializes a new instance of the CodeLexer class.
        /// </summary>
        /// <param name="languageService">The C# language service.</param>
        /// <param name="source">The source to read.</param>
        /// <param name="codeReader">Used for reading the source code.</param>
        internal CodeLexer(CsLanguageService languageService, Code source, CodeReader codeReader)
        {
            Param.AssertNotNull(languageService, "languageService");
            Param.AssertNotNull(source, "source");
            Param.AssertNotNull(codeReader, "codeReader");

            this.languageService = languageService;
            this.source = source;
            this.codeReader = codeReader;
        }

        /// <summary>
        /// Initializes a new instance of the CodeLexer class.
        /// </summary>
        /// <param name="languageService">The C# language service.</param>
        /// <param name="source">The source to read.</param>
        /// <param name="codeReader">Used for reading the source code.</param>
        /// <param name="index">The starting absolute index of the code being parsed.</param>
        /// <param name="indexOnLine">The starting index on line of the code being parsed.</param>
        /// <param name="lineNumber">The starting line number of the code being parsed.</param>
        internal CodeLexer(CsLanguageService languageService, Code source, CodeReader codeReader, int index, int indexOnLine, int lineNumber)
        {
            Param.AssertNotNull(languageService, "languageService");
            Param.AssertNotNull(source, "source");
            Param.AssertNotNull(codeReader, "codeReader");
            Param.AssertGreaterThanOrEqualToZero(index, "index");
            Param.AssertGreaterThanOrEqualToZero(indexOnLine, "indexOnLine");
            Param.AssertGreaterThanZero(lineNumber, "lineNumber");

            this.languageService = languageService;
            this.source = source;
            this.codeReader = codeReader;

            this.marker.Index = index;
            this.marker.IndexOnLine = indexOnLine;
            this.marker.LineNumber = lineNumber;
        }

        #endregion Internal Constructors

        #region Internal Properties

        /// <summary>
        /// Gets the source code.
        /// </summary>
        internal Code SourceCode
        {
            get
            {
                return this.source;
            }
        }

        #endregion Internal Properties

        #region Internal Methods

        /// <summary>
        /// Gets the list of symbols from the code file.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="preprocessorDefinitions">The active configuration.</param>
        /// <returns>Returns the list of symbols in the code file.</returns>
        internal List<Symbol> GetSymbols(CsDocument document, IDictionary<string, object> preprocessorDefinitions)
        {
            Param.AssertNotNull(document, "document");
            Param.Ignore(preprocessorDefinitions);

            // Create the symbol list.
            var symbols = new List<Symbol>();

            // Loop until all the symbols have been read.
            while (true)
            {
                IList<Symbol> symbolList = this.GetSymbol(document, preprocessorDefinitions, true);
                if (symbolList == null)
                {
                    break;
                }

                for (int i = 0; i < symbolList.Count; ++i)
                {
                    symbols.Add(symbolList[i]);
                }
            }

            // Return the list of symbols.
            return symbols;
        }

        /// <summary>
        /// Gets the next symbol in the code, starting at the current marker.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="preprocessorDefinitions">The active preprocessor definitions.</param>
        /// <param name="evaluatePreprocessors">Indicates whether to evaluate preprocessor symbols.</param>
        /// <returns>Returns the next symbol in the document.</returns>
        [SuppressMessage(
            "Microsoft.Maintainability", 
            "CA1502:AvoidExcessiveComplexity",
            Justification = "The method is not overly complex.")]
        [SuppressMessage(
            "Microsoft.Globalization", 
            "CA1303:DoNotPassLiteralsAsLocalizedParameters", 
            Justification = "The literals represent non-localizable C# operators.")]
        internal IList<Symbol> GetSymbol(CsDocument document, IDictionary<string, object> preprocessorDefinitions, bool evaluatePreprocessors)
        {
            Param.Ignore(preprocessorDefinitions);
            Param.AssertNotNull(document, "document");
            Param.Ignore(evaluatePreprocessors);

            List<Symbol> symbols = new List<Symbol>(1);

            // Look at the next character from the buffer.
            char firstCharacter = this.codeReader.Peek();
            if (firstCharacter != char.MinValue)
            {
                switch (firstCharacter)
                {
                    case ' ':
                    case '\t':
                        symbols.Add(this.GetWhitespace());
                        break;

                    case '\'':
                    case '\"':
                        symbols.Add(this.GetString());
                        break;

                    case '@':
                        symbols.Add(this.GetLiteral());
                        break;

                    case '/':
                        // Try to get this as a comment. If it is not a comment, it is an operator symbol.
                        Symbol s1 = this.GetComment();
                        if (s1 == null)
                        {
                            s1 = this.GetOperatorSymbol('/');
                        }

                        if (s1 != null)
                        {
                            symbols.Add(s1);
                        }

                        break;

                    case '\r':
                    case '\n':
                        symbols.Add(this.GetNewLine());
                        break;

                    case '~':
                    case '+':
                    case '-':
                    case '*':
                    case '|':
                    case '&':
                    case '!':
                    case '^':
                    case '>':
                    case '<':
                    case '=':
                    case '%':
                    case ':':
                    case '?':
                        symbols.Add(this.GetOperatorSymbol(firstCharacter));
                        break;

                    case '#':
                        symbols.AddRange(this.GetPreprocessorDirectiveSymbol(document, preprocessorDefinitions, evaluatePreprocessors));
                        break;
                        
                    case '(':
                        symbols.Add(this.CreateAndMovePastSymbol("(", SymbolType.OpenParenthesis));
                        break;

                    case ')':
                        symbols.Add(this.CreateAndMovePastSymbol(")", SymbolType.CloseParenthesis));
                        break;

                    case '[':
                        symbols.Add(this.CreateAndMovePastSymbol("[", SymbolType.OpenSquareBracket));
                        break;

                    case ']':
                        symbols.Add(this.CreateAndMovePastSymbol("]", SymbolType.CloseSquareBracket));
                        break;

                    case '{':
                        symbols.Add(this.CreateAndMovePastSymbol("{", SymbolType.OpenCurlyBracket));
                        break;

                    case '}':
                        symbols.Add(this.CreateAndMovePastSymbol("}", SymbolType.CloseCurlyBracket));
                        break;

                    case ',':
                        symbols.Add(this.CreateAndMovePastSymbol(",", SymbolType.Comma));
                        break;

                    case ';':
                        symbols.Add(this.CreateAndMovePastSymbol(";", SymbolType.Semicolon));
                        break;

                    case '.':
                        Symbol s2 = this.GetNumber();
                        if (s2 == null)
                        {
                            s2 = this.GetOperatorSymbol('.');
                        }

                        if (s2 != null)
                        {
                            symbols.Add(s2);
                        }

                        break;

                    default:
                        // Skip any unknown formatting characters, and skip any unassigned characters.
                        UnicodeCategory category = char.GetUnicodeCategory(firstCharacter);
                        if (category != UnicodeCategory.Format && category != UnicodeCategory.OtherNotAssigned)
                        {
                            Symbol s3 = this.GetNumber();
                            if (s3 == null)
                            {
                                s3 = this.GetOtherSymbol(this.source);
                            }

                            if (s3 != null)
                            {
                                symbols.Add(s3);
                            }
                        }

                        break;
                }
            }

            return symbols.Count == 0 ? null : symbols;
        }

        #endregion Internal Methods

        #region Private Static Methods

        /// <summary>
        /// Gets the type of the given symbol.
        /// </summary>
        /// <param name="text">The symbol to look up.</param>
        /// <returns>Returns the type of the symbol.</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "The method consists of a simple switch statement.")]
        private static SymbolType GetOtherSymbolType(string text)
        {
            Param.AssertValidString(text, "text");

            switch (text)
            {
                case "abstract":
                    return SymbolType.Abstract;

                case "as":
                    return SymbolType.As;

                case "base":
                    return SymbolType.Base;

                case "break":
                    return SymbolType.Break;

                case "case":
                    return SymbolType.Case;

                case "catch":
                    return SymbolType.Catch;

                case "checked":
                    return SymbolType.Checked;

                case "class":
                    return SymbolType.Class;

                case "const":
                    return SymbolType.Const;

                case "continue":
                    return SymbolType.Continue;

                case "default":
                    return SymbolType.Default;

                case "delegate":
                    return SymbolType.Delegate;

                case "do":
                    return SymbolType.Do;

                case "else":
                    return SymbolType.Else;

                case "enum":
                    return SymbolType.Enum;

                case "event":
                    return SymbolType.Event;

                case "explicit":
                    return SymbolType.Explicit;

                case "extern":
                    return SymbolType.Extern;

                case "false":
                    return SymbolType.False;

                case "finally":
                    return SymbolType.Finally;

                case "fixed":
                    return SymbolType.Fixed;

                case "for":
                    return SymbolType.For;

                case "foreach":
                    return SymbolType.Foreach;

                case "goto":
                    return SymbolType.Goto;

                case "if":
                    return SymbolType.If;

                case "implicit":
                    return SymbolType.Implicit;

                case "in":
                    return SymbolType.In;

                case "interface":
                    return SymbolType.Interface;

                case "internal":
                    return SymbolType.Internal;

                case "is":
                    return SymbolType.Is;

                case "lock":
                    return SymbolType.Lock;

                case "namespace":
                    return SymbolType.Namespace;

                case "new":
                    return SymbolType.New;

                case "null":
                    return SymbolType.Null;

                case "operator":
                    return SymbolType.Operator;

                case "out":
                    return SymbolType.Out;

                case "override":
                    return SymbolType.Override;

                case "params":
                    return SymbolType.Params;

                case "private":
                    return SymbolType.Private;

                case "protected":
                    return SymbolType.Protected;

                case "public":
                    return SymbolType.Public;

                case "readonly":
                    return SymbolType.Readonly;

                case "ref":
                    return SymbolType.Ref;

                case "return":
                    return SymbolType.Return;

                case "sealed":
                    return SymbolType.Sealed;

                case "sizeof":
                    return SymbolType.Sizeof;

                case "stackalloc":
                    return SymbolType.Stackalloc;

                case "static":
                    return SymbolType.Static;

                case "struct":
                    return SymbolType.Struct;

                case "switch":
                    return SymbolType.Switch;

                case "this":
                    return SymbolType.This;

                case "throw":
                    return SymbolType.Throw;

                case "true":
                    return SymbolType.True;

                case "try":
                    return SymbolType.Try;

                case "typeof":
                    return SymbolType.Typeof;

                case "unchecked":
                    return SymbolType.Unchecked;

                case "unsafe":
                    return SymbolType.Unsafe;

                case "using":
                    return SymbolType.Using;

                case "virtual":
                    return SymbolType.Virtual;

                case "volatile":
                    return SymbolType.Volatile;

                case "while":
                    return SymbolType.While;

                default:
                    return SymbolType.Other;
            }
        }

        /// <summary>
        /// Creates a CodeLocation from the given marker.
        /// </summary>
        /// <param name="marker">The marker.</param>
        /// <returns>Returns the CodeLocation.</returns>
        private static CodeLocation CodeLocationFromMarker(MarkerData marker)
        {
            Param.AssertNotNull(marker, "marker");

            return new CodeLocation(
                marker.Index,
                marker.Index,
                marker.IndexOnLine,
                marker.IndexOnLine,
                marker.LineNumber,
                marker.LineNumber);
        }

        #endregion Private Static Methods

        #region Private Methods

        /// <summary>
        /// Reads, creates, and moves past a symbol.
        /// </summary>
        /// <param name="text">The symbol text.</param>
        /// <param name="type">The type of the symbol.</param>
        /// <returns>Returns the symbol.</returns>
        private Symbol CreateAndMovePastSymbol(string text, SymbolType type)
        {
            Param.AssertValidString(text, "text");
            Param.Ignore(type);

            this.codeReader.ReadNext();
            Symbol symbol = new Symbol(text, type, CodeLocationFromMarker(this.marker));
            ++this.marker.Index;
            ++this.marker.IndexOnLine;

            return symbol;
        }

        /// <summary>
        /// Gets the next number.
        /// </summary>
        /// <returns>Returns the number.</returns>
        private Symbol GetNumber()
        {
            // The last index of the number.
            int endIndex = -1;

            // The first few characters in the number tell us the type of the number.
            char character = this.codeReader.Peek();
            if (character == '-' || character == '+')
            {
                // This could be a number starting with a negative or positive sign.
                // If that's true, the next character must be a digit between 0 and 9.
                character = this.codeReader.Peek(1);
                if (character >= '0' && character <= '9')
                {
                    endIndex = this.GetPositiveNumber(this.marker.Index + 1);
                }
            }
            else
            {
                // Get the body of the number.
                endIndex = this.GetPositiveNumber(this.marker.Index);
            }

            // Create the NumberSymbol now.
            Symbol number = null;

            // Make sure a number was found.
            if (endIndex >= this.marker.Index)
            {
                // Get the text string for this number.
                int length = endIndex - this.marker.Index + 1;
                string numberText = this.codeReader.ReadString(length);
                CsLanguageService.Debug.Assert(!string.IsNullOrEmpty(numberText), "The text should not be empty");

                // Create the location object.
                var location = new CodeLocation(
                    this.marker.Index,
                    this.marker.Index + length - 1,
                    this.marker.IndexOnLine,
                    this.marker.IndexOnLine + length - 1,
                    this.marker.LineNumber,
                    this.marker.LineNumber);

                number = new Symbol(numberText, SymbolType.Number, location);

                // Update the marker.
                this.marker.Index += length;
                this.marker.IndexOnLine += length;
            }

            return number;
        }

        /// <summary>
        /// Extracts the body of a positive number from the code.
        /// </summary>
        /// <param name="index">The first index of the number.</param>
        /// <returns>Returns the last index of the number.</returns>
        private int GetPositiveNumber(int index)
        {
            Param.AssertGreaterThanOrEqualToZero(index, "index");

            // First, check if this is a hexidecimal number.
            char character = this.codeReader.Peek();
            if (character == '0')
            {
                character = this.codeReader.Peek(1);
                if (character == 'x' || character == 'X')
                {
                    return this.GetHexidecimalIntegerLiteral(index + 2);
                }
            }

            // Treat this like a decimal literal.
            return this.GetDecimalLiteral(index);
        }

        /// <summary>
        /// Extracts a decimal integer literal from the code.
        /// </summary>
        /// <param name="index">The first index of the decimal integer literal.</param>
        /// <returns>Returns the last index of the decimal integer literal.</returns>
        private int GetDecimalLiteral(int index)
        {
            Param.AssertGreaterThanOrEqualToZero(index, "index");

            int startIndex = index;

            while (true)
            {
                char character = this.codeReader.Peek(index - this.marker.Index);

                // Break if this is not a valid decimal digit.
                if (character < '0' || character > '9')
                {
                    break;
                }

                ++index;
            }

            // The last index of the number is one less than the current index.
            --index;

            // See whether we've found at least one digit.
            if (index >= startIndex)
            {
                // Now check whether there is a trailing integer type suffix.
                int endIndex = this.GetIntegerTypeSuffix(index + 1);
                if (endIndex != -1)
                {
                    index = endIndex;
                }
                else
                {
                    // There was no trailing type suffix. This might actually be a real literal.
                    // check if there are any trailing characters which would indicate this.
                    endIndex = this.GetRealLiteralTrailingCharacters(index, false);
                    if (endIndex != -1)
                    {
                        index = endIndex;
                    }
                }
            }
            else
            {
                // No digits were found. This might be a real literal starting with a decimal point.
                // Check if it matches that signature.
                int endIndex = this.GetRealLiteralTrailingCharacters(index, true);
                if (endIndex != -1)
                {
                    index = endIndex;
                }

                // If there are still no digits, then this is not a number.
                if (index < startIndex)
                {
                    index = -1;
                }
            }

            return index;
        }

        /// <summary>
        /// Gets the characters trailing behind a real literal number, if there are any.
        /// </summary>
        /// <param name="index">The start index of the trailing characters.</param>
        /// <param name="requiresDecimalPoint">Indicates whether the number is required to start with a decimal point.</param>
        /// <returns>Returns the last index of the trailing characters.</returns>
        private int GetRealLiteralTrailingCharacters(int index, bool requiresDecimalPoint)
        {
            Param.Ignore(index);
            Param.Ignore(requiresDecimalPoint);

            bool hasTrailingCharacters = false;
            bool hasDecimal = false;

            char character = this.codeReader.Peek(index - this.marker.Index + 1);

            // First, check if the next character is a decimal point.
            if (character == '.')
            {
                int endIndex = this.GetDecimalFraction(index + 2);
                if (endIndex != -1)
                {
                    index = endIndex;
                    hasDecimal = true;
                    hasTrailingCharacters = true;
                }
            }

            if (!requiresDecimalPoint || hasDecimal)
            {
                // Now check whether the number contains an exponent part.
                character = this.codeReader.Peek(index - this.marker.Index + 1);
                if (character == 'e' || character == 'E')
                {
                    int endIndex = this.GetRealLiteralExponent(index + 1);
                    if (endIndex != -1)
                    {
                        index = endIndex;
                        hasTrailingCharacters = true;
                    }
                }

                // Now check whether the number ends in a real type suffix.
                character = this.codeReader.Peek(index - this.marker.Index + 1);
                if (character == 'm' ||
                    character == 'M' ||
                    character == 'd' ||
                    character == 'D' ||
                    character == 'f' ||
                    character == 'F')
                {
                    ++index;
                    hasTrailingCharacters = true;
                }
            }

            if (!hasTrailingCharacters)
            {
                index = -1;
            }

            return index;
        }

        /// <summary>
        /// Gets the decimal digits that appear after a decimal point in a real literal.
        /// </summary>
        /// <param name="index">The start index of the remainder numbers.</param>
        /// <returns>Returns the last index of the remainder nubmers.</returns>
        private int GetDecimalFraction(int index)
        {
            Param.AssertGreaterThanOrEqualToZero(index, "index");

            // Get the decimal digits that appear after a decimal point.
            int startIndex = index;

            while (true)
            {
                char character = this.codeReader.Peek(index - this.marker.Index);

                // Break if this is not a valid decimal digit.
                if (character < '0' || character > '9')
                {
                    break;
                }

                ++index;
            }

            // The last index of the number is one less than the current index.
            --index;

            // If there is not at least one decimal digit, return -1.
            if (index < startIndex)
            {
                index = -1;
            }

            return index;
        }

        /// <summary>
        /// Gets an exponent at the end of a real literal number.
        /// </summary>
        /// <param name="index">The start index of the exponent.</param>
        /// <returns>Returns the last index of the exponent.</returns>
        private int GetRealLiteralExponent(int index)
        {
            Param.AssertGreaterThanOrEqualToZero(index, "index");

            int endIndex = -1;

            // The exponent must start with e or E.
            char character = this.codeReader.Peek(index - this.marker.Index);
            if (character == 'e' || character == 'E')
            {
                ++index;

                // The exponent can optionally contain a positive or negative sign.
                character = this.codeReader.Peek(index - this.marker.Index);
                {
                    if (character == '-' || character == '+')
                    {
                        ++index;
                    }
                }

                // The rest of the numbers must be decimal digits.
                while (true)
                {
                    character = this.codeReader.Peek(index - this.marker.Index);
                    if (character >= '0' && character <= '9')
                    {
                        endIndex = index;
                        ++index;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return endIndex;
        }

        /// <summary>
        /// Extracts a hexidecimal integer literal from the code.
        /// </summary>
        /// <param name="index">The first index of the hexidecimal integer literal.</param>
        /// <returns>Returns the last index of the hexidecimal integer literal.</returns>
        private int GetHexidecimalIntegerLiteral(int index)
        {
            Param.AssertGreaterThanOrEqualToZero(index, "index");

            int startIndex = index;
            
            while (true)
            {
                char character = this.codeReader.Peek(index - this.marker.Index);

                // Break if this is not a valid hexidecimal digit.
                if (!(character >= '0' && character <= '9') &&
                    !(character >= 'a' && character <= 'f') &&
                    !(character >= 'A' && character <= 'F'))
                {
                    break;
                }

                ++index;
            }

            // The last index of the number is one less than the current index.
            --index;

            // See whether we've found at least one digit.
            if (index >= startIndex)
            {
                // Now check whether there is a trailing integer type suffix.
                int endIndex = this.GetIntegerTypeSuffix(index + 1);
                if (endIndex != -1)
                {
                    index = endIndex;
                }
            }
            else
            {
                // If there are no digits, this is not a number.
                index = -1;
            }

            return index;
        }

        /// <summary>
        /// Gets the type suffix tacked onto the end of an integer literal.
        /// </summary>
        /// <param name="index">The start index of the literal.</param>
        /// <returns>Returns the index of the integer type suffix, if there is one.</returns>
        private int GetIntegerTypeSuffix(int index)
        {
            Param.AssertGreaterThanOrEqualToZero(index, "index");

            int endIndex = -1;

            // Now check whether the current character is an integer type suffix.
            char character = this.codeReader.Peek(index - this.marker.Index);

            if (character == 'u' || character == 'U')
            {
                // This is a uint suffix.
                endIndex = index;

                // Check whether this is actually a ulong suffix.
                character = this.codeReader.Peek(index + 1 - this.marker.Index);

                if (character == 'l' || character == 'L')
                {
                    endIndex = index + 1;
                }
            }
            else if (character == 'l' || character == 'L')
            {
                // This is a long suffix.
                endIndex = index;

                // Check whether this is actually a ulong suffix.
                character = this.codeReader.Peek(index + 1 - this.marker.Index);

                if (character == 'u' || character == 'U')
                {
                    endIndex = index + 1;
                }
            }

            return endIndex;
        }

        /// <summary>
        /// Gets an unknown symbol type.
        /// </summary>
        /// <param name="sourceCode">The source code containing the symbols.</param>
        /// <returns>Returns the item.</returns>
        private Symbol GetOtherSymbol(Code sourceCode)
        {
            Param.AssertNotNull(sourceCode, "sourceCode");

            var text = new StringBuilder();
            this.ReadToEndOfOtherSymbol(text);
            if (text.Length == 0)
            {
                throw new SyntaxException(sourceCode, 1);
            }

            string symbolText = text.ToString();

            // Get the token location.
            var location = new CodeLocation(
                this.marker.Index,
                this.marker.Index + text.Length - 1,
                this.marker.IndexOnLine,
                this.marker.IndexOnLine + text.Length - 1,
                this.marker.LineNumber,
                this.marker.LineNumber);

            // Create the symbol.
            var symbol = new Symbol(
                symbolText,
                CodeLexer.GetOtherSymbolType(symbolText),
                location);

            // Reset the marker index.
            this.marker.Index += text.Length;
            this.marker.IndexOnLine += text.Length;

            // Return the symbol.
            return symbol;
        }

        /// <summary>
        /// Gathers all the characters up to the last index of an unknown word.
        /// </summary>
        /// <param name="text">The texst buffer to add the symbol text to.</param>
        private void ReadToEndOfOtherSymbol(StringBuilder text)
        {
            Param.AssertNotNull(text, "text");

            bool seenLetter = false;

            // Loop until we find the end of the word.
            while (true)
            {
                char character = this.codeReader.Peek();
                if (character == char.MinValue)
                {
                    break;
                }
                else if (char.IsLetter(character) || character == '_')
                {
                    // Mark that we've seen a letter in this word, and continue.
                    seenLetter = true;
                }
                else if (seenLetter && char.IsNumber(character))
                {
                    // Numbers are ok as long as we've previously seen at least one
                    // letter in this word.
                }
                else
                {
                    // This is an invalid character, so break out of the loop.
                    break;
                }

                // Add the character to the text buffer and advance the reader past it.
                text.Append(character);
                this.codeReader.ReadNext();
            }
        }

        /// <summary>
        /// Gets the next newline character from the document.
        /// </summary>
        /// <returns>Returns the newline.</returns>
        [SuppressMessage(
            "Microsoft.Globalization", 
            "CA1303:DoNotPassLiteralsAsLocalizedParameters",
            MessageId = "StyleCop.CSharp.Symbol.#ctor(System.String,StyleCop.CSharp.SymbolType,StyleCop.CodeLocation)",
            Justification = "The literal is a non-localizable newline character")]
        private Symbol GetNewLine()
        {
            Symbol symbol = null;

            char character = this.codeReader.Peek();
            if (character != char.MinValue)
            {
                // Get the character
                this.codeReader.ReadNext();

                // Save the original start and end indexes of the newline character.
                int startIndex = this.marker.Index;
                int endIndex = this.marker.Index;

                // Check if this is an \r\n sequence in which case we need to adjust the end index.
                if (character == '\r')
                {
                    character = this.codeReader.Peek();
                    if (character == '\n')
                    {
                        this.codeReader.ReadNext();
                        ++this.marker.Index;
                        ++endIndex;
                    }
                }

                // Create the code location.
                var location = new CodeLocation(
                    startIndex,
                    endIndex,
                    this.marker.IndexOnLine,
                    this.marker.IndexOnLine + (endIndex - startIndex),
                    this.marker.LineNumber,
                    this.marker.LineNumber);

                // Create the symbol.
                symbol = new Symbol("\n", SymbolType.EndOfLine, location);

                // Update the marker.
                ++this.marker.Index;
                ++this.marker.LineNumber;
                this.marker.IndexOnLine = 0;
            }

            return symbol;
        }

        /// <summary>
        /// Gets the next whitespace stream.
        /// </summary>
        /// <returns>Returns the whitespace.</returns>
        private Symbol GetWhitespace()
        {
            var text = new StringBuilder();

            // Get all of the characters in the whitespace.
            while (true)
            {
                char character = this.codeReader.Peek();
                if (character == char.MinValue || (character != ' ' && character != '\t'))
                {
                    break;
                }

                text.Append(character);
                
                // Advance past this character.
                this.codeReader.ReadNext();
            }

            // Create the whitespace location object.
            var location = new CodeLocation(
                this.marker.Index,
                this.marker.Index + text.Length - 1,
                this.marker.IndexOnLine,
                this.marker.IndexOnLine + text.Length - 1,
                this.marker.LineNumber,
                this.marker.LineNumber);

            // Create the whitespace object.
            var whitespace = new Symbol(text.ToString(), SymbolType.WhiteSpace, location);

            // Update the marker.
            this.marker.Index += text.Length;
            this.marker.IndexOnLine += text.Length;

            // Return the whitespace object.
            return whitespace;
        }

        /// <summary>
        /// Gets the next string from the code.
        /// </summary>
        /// <returns>Returns the string.</returns>
        private Symbol GetString()
        {
            var text = new StringBuilder();

            // Read the opening quote character and add it to the string.
            char quoteType = this.codeReader.ReadNext();
            CsLanguageService.Debug.Assert(quoteType == '\'' || quoteType == '\"', "Expected a quote character");
            text.Append(quoteType);
            
            bool slash = false;

            // Read through to the end of the string.
            while (true)
            {
                char character = this.codeReader.Peek();
                if (character == char.MinValue || (character == quoteType && !slash))
                {
                    // This is the end of the string. Add the character and quit.
                    text.Append(character);
                    this.codeReader.ReadNext();
                    break;
                }

                if (character == '\\')
                {
                    slash = !slash;
                }
                else
                {
                    slash = false;

                    if (character == '\r' || character == '\n')
                    {
                        // We've hit the end of the line. Just exit.
                        break;
                    }
                }

                text.Append(character);

                // Advance past this character.
                this.codeReader.ReadNext();
            }

            // Create the code location.
            var location = new CodeLocation(
                this.marker.Index,
                this.marker.Index + text.Length - 1,
                this.marker.IndexOnLine,
                this.marker.IndexOnLine + text.Length - 1,
                this.marker.LineNumber,
                this.marker.LineNumber);

            // Create the symbol.
            var symbol = new Symbol(text.ToString(), SymbolType.String, location);

            // Update the marker.
            this.marker.Index += text.Length;
            this.marker.IndexOnLine += text.Length;

            // Return the symbol.
            return symbol;
        }

        /// <summary>
        /// Gets the next literal from the code.
        /// </summary>
        /// <returns>Returns the literal.</returns>
        private Symbol GetLiteral()
        {
            var text = new StringBuilder();

            // Read the literal string character and add it to the string buffer.
            char character = this.codeReader.ReadNext();
            CsLanguageService.Debug.Assert(character == '@', "Expected an @ keyword");
            text.Append(character);

            // Make sure there is enough code left to contain at least @ plus one additional character.
            character = this.codeReader.Peek();
            if (character == char.MinValue)
            {
                throw new SyntaxException(this.source, this.marker.LineNumber);
            }

            // Get the next character to determine what type this is.
            if (character == '\'' || character == '\"')
            {
                return this.GetLiteralString(text);
            }
            else if (char.IsLetter(character) || character == '_')
            {
                return this.GetLiteralKeyword(text);
            }
            else
            {
                // This is an unexpected character.
                throw new SyntaxException(this.source, this.marker.LineNumber);
            }
        }

        /// <summary>
        /// Gets the next literal string from the code.
        /// </summary>
        /// <param name="text">The text buffer to add the string text to.</param>
        /// <returns>Returns the literal string.</returns>
        private Symbol GetLiteralString(StringBuilder text)
        {
            Param.AssertNotNull(text, "text");
            CsLanguageService.Debug.Assert(text.Length == 1 && text[0] == '@', "Expected an @ symbol");

            // Initialize the location of the start of the string.
            int startIndex = this.marker.Index;
            int endIndex = this.marker.Index;
            int startIndexOnLine = this.marker.IndexOnLine;
            int endIndexOnLine = this.marker.IndexOnLine;
            int lineNumber = this.marker.LineNumber;
            int endLineNumber = this.marker.LineNumber;

            // Get the opening string character to determine what type of string this is.
            char stringType = this.codeReader.Peek();
            CsLanguageService.Debug.Assert(stringType == '\'' || stringType == '\"', "Expected a quote character");

            // Add the opening quote character and move past it.
            text.Append(stringType);
            this.codeReader.ReadNext();

            // Advance the end indexes past the literal character and the open quote character.
            endIndex += 2;
            endIndexOnLine += 2;

            while (true)
            {
                char character = this.codeReader.Peek();
                if (character == stringType)
                {
                    // Read the character and add it to the text buffer.
                    this.codeReader.ReadNext();
                    text.Append(character);
                    ++endIndex;
                    ++endIndexOnLine;

                    // If the next character is also the same string type, then this is internal to the string.
                    character = this.codeReader.Peek();
                    if (character == stringType)
                    {
                        // Also move past this character and add it.
                        this.codeReader.ReadNext();
                        text.Append(character);

                        ++endIndex;
                        ++endIndexOnLine;
                        continue;
                    }
                    else
                    {
                        // This is the end of the string. We're done now.
                        break;
                    }
                }
                else if (character == '\r' || character == '\n')
                {
                    if (stringType == '\'')
                    {
                        // This is a syntax error in the code as single-quoted literal strings
                        // cannot allowed to span across multiple lines, although double-quoted 
                        // strings can.
                        throw new SyntaxException(this.source, this.marker.LineNumber);
                    }
                    else if (character == '\n')
                    {
                        ++endLineNumber;
                        endIndexOnLine = -1;
                    }
                    else if (character == '\r')
                    {
                        // Just move past this character without adding it.
                        this.codeReader.ReadNext();
                        continue;
                    }
                }

                this.codeReader.ReadNext();
                text.Append(character);
                ++endIndex;
                ++endIndexOnLine;
            }

            // Make sure the end index is correct now.
            if (text.Length <= 2 || text[text.Length - 1] != stringType)
            {
                throw new SyntaxException(this.source, this.marker.LineNumber);
            }

            // Create the location object.
            var location = new CodeLocation(
                startIndex, endIndex, startIndexOnLine, endIndexOnLine, lineNumber, endLineNumber);

            // Create the symbol.
            var token = new Symbol(text.ToString(), SymbolType.String, location);

            // Update the marker.
            this.marker.Index = endIndex + 1;
            this.marker.IndexOnLine = endIndexOnLine + 1;
            this.marker.LineNumber = endLineNumber;

            // Return the token.
            return token;
        }

        /// <summary>
        /// Gets the next literal keyword token from the code.
        /// </summary>
        /// <param name="text">The text buffer to add the string text to.</param>
        /// <returns>Returns the literal keyword token.</returns>
        private Symbol GetLiteralKeyword(StringBuilder text)
        {
            Param.AssertNotNull(text, "text");
            CsLanguageService.Debug.Assert(text.Length > 0 && text[0] == '@', "Expected an @ character");

            // Advance to the end of the token.
            this.ReadToEndOfOtherSymbol(text);
            if (text.Length == 1)
            {
                // Nothing was read.
                throw new SyntaxException(this.source, this.marker.LineNumber);
            }

            // Get the token location.
            var location = new CodeLocation(
                this.marker.Index,
                this.marker.Index + text.Length - 1,
                this.marker.IndexOnLine,
                this.marker.IndexOnLine + text.Length - 1,
                this.marker.LineNumber,
                this.marker.LineNumber);

            // Create the symbol.
            var symbol = new Symbol(text.ToString(), SymbolType.Other, location);

            // Reset the marker index.
            this.marker.Index += text.Length;
            this.marker.IndexOnLine += text.Length;

            // Return the symbol.
            return symbol;
        }

        /// <summary>
        /// Gets the next comment.
        /// </summary>
        /// <returns>Returns the comment.</returns>
        private Symbol GetComment()
        {
            Symbol symbol = null;

            // The current character must be a forward slash.
            CsLanguageService.Debug.Assert(this.codeReader.Peek() == '/', "Expected a forward slash character");

            var text = new StringBuilder();
            
            // Peek at the type of the next character.
            char character = this.codeReader.Peek(1);
           
            if (character != char.MinValue)
            {
                if (character == '*')
                {
                    // This looks like a comment. Move past the first slash.
                    text.Append(this.codeReader.ReadNext());

                    // Get the rest of the comment.
                    symbol = this.GetMultiLineComment(text);
                }
                else if (character == '/')
                {
                    // This looks like a comment. Move past the first slash.
                    text.Append(this.codeReader.ReadNext());

                    // Add this second slash as well.
                    text.Append(this.codeReader.ReadNext());

                    // Peek at the type of the next character.
                    character = this.codeReader.Peek();
                    if (character == '/')
                    {
                        // Add this character and move past it.
                        text.Append(this.codeReader.ReadNext());

                        // Peek at the type of the next character.
                        character = this.codeReader.Peek();
                        if (character != '/')
                        {
                            // The line starts with three slashes in a row.
                            symbol = this.GetXmlHeaderLine(text);
                        }
                        else
                        {
                            // The line starts with four slashes in a row.
                            symbol = this.GetSingleLineComment(text);
                        }
                    }
                    else
                    {
                        symbol = this.GetSingleLineComment(text);
                    }
                }
            }

            return symbol;
        }

        /// <summary>
        /// Gets the next multi-line comment.
        /// </summary>
        /// <param name="text">The buffer to add the text to.</param>
        /// <returns>Returns the comment.</returns>
        private Symbol GetMultiLineComment(StringBuilder text)
        {
            Param.AssertNotNull(text, "text");

            // Initialize the location of the start of the comment. Add one to the end indexes since we know the 
            // comment starts with /*, which is two characters long.
            int startIndex = this.marker.Index;
            int endIndex = this.marker.Index + 1;
            int startIndexOnLine = this.marker.IndexOnLine;
            int endIndexOnLine = this.marker.IndexOnLine + 1;
            int lineNumber = this.marker.LineNumber;
            int endLineNumber = this.marker.LineNumber;

            // Initialize loop trackers.
            bool asterisk = false;
            bool first = false;

            // Find the end of the comment.
            while (true)
            {
                char character = this.codeReader.Peek();
                if (character == char.MinValue)
                {
                    break;
                }

                // Add the character and move the index past it.
                text.Append(this.codeReader.ReadNext());

                if (asterisk && character == '/')
                {
                    // This is the end of the comment.
                    break;
                }
                else
                {
                    if (character == '*')
                    {
                        if (first)
                        {
                            // Mark the asterisk.
                            asterisk = true;
                        }
                        else
                        {
                            first = true;
                        }
                    }
                    else
                    {
                        // This is not an asterisk.
                        asterisk = false;

                        // Check for newlines.
                        if (character == '\n')
                        {
                            ++endLineNumber;
                            endIndexOnLine = -1;
                        }
                        else if (character == '\r')
                        {
                            // Peek at the next character and check the type.
                            character = this.codeReader.Peek();
                            if (character != '\n')
                            {
                                ++endLineNumber;
                                endIndexOnLine = -1;
                            }
                        }
                    }
                }

                ++endIndex;
                ++endIndexOnLine;
            }

            // Create the location object.
            var location = new CodeLocation(
                startIndex, endIndex, startIndexOnLine, endIndexOnLine, lineNumber, endLineNumber);

            // Create the symbol object.
            var symbol = new Symbol(text.ToString(), SymbolType.MultiLineComment, location);

            // Update the marker.
            this.marker.Index = endIndex + 1;
            this.marker.IndexOnLine = endIndexOnLine + 1;
            this.marker.LineNumber = endLineNumber;

            // Return the symbol.
            return symbol;
        }

        /// <summary>
        /// Gets the next single line comment from the code.
        /// </summary>
        /// <param name="text">The buffer in which to store the text.</param>
        /// <returns>Returns the single line comment.</returns>
        private Symbol GetSingleLineComment(StringBuilder text)
        {
            Param.AssertNotNull(text, "text");

            // Find the end of the current line.
            this.AdvanceToEndOfLine(text);

            // Create the code location.
            var location = new CodeLocation(
                this.marker.Index,
                this.marker.Index + text.Length - 1,
                this.marker.IndexOnLine,
                this.marker.IndexOnLine + text.Length - 1,
                this.marker.LineNumber,
                this.marker.LineNumber);

            // Create the symbol.
            var symbol = new Symbol(text.ToString(), SymbolType.SingleLineComment, location);

            // Update the marker.
            this.marker.Index += text.Length;
            this.marker.IndexOnLine += text.Length;

            // Return the symbol.
            return symbol;
        }

        /// <summary>
        /// Gets the next Xml header line from the code.
        /// </summary>
        /// <param name="text">The buffer in which to store the text.</param>
        /// <returns>Returns the Xml header line.</returns>
        private Symbol GetXmlHeaderLine(StringBuilder text)
        {
            Param.AssertNotNull(text, "text");

            // Find the end of the current line.
            this.AdvanceToEndOfLine(text);

            // Create the code location.
            var location = new CodeLocation(
                this.marker.Index,
                this.marker.Index + text.Length - 1,
                this.marker.IndexOnLine,
                this.marker.IndexOnLine + text.Length - 1,
                this.marker.LineNumber,
                this.marker.LineNumber);

            // Create the symbol.
            var symbol = new Symbol(text.ToString(), SymbolType.XmlHeaderLine, location);

            // Update the marker.
            this.marker.Index += text.Length;
            this.marker.IndexOnLine += text.Length;

            // Return the symbol.
            return symbol;
        }

        /// <summary>
        /// Gets the next preprocessor directive keyword.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="preprocessorDefinition">The active configuration.</param>
        /// <param name="evaluate">Indicates whether to evaluate the preprocessor symbol if it is a conditional
        /// directive.</param>
        /// <returns>Returns the next preprocessor directive keyword.</returns>
        private ICollection<Symbol> GetPreprocessorDirectiveSymbol(CsDocument document, IDictionary<string, object> preprocessorDefinition, bool evaluate)
        {
            Param.AssertNotNull(document, "document");
            Param.Ignore(preprocessorDefinition);
            Param.Ignore(evaluate);

            // Find the end of the current line.
            var text = new StringBuilder();
            this.AdvanceToEndOfLine(text);
            if (text.Length == 1)
            {
                throw new SyntaxException(document, this.marker.LineNumber, Strings.UnexpectedEndOfFile);
            }

            // Create the code location.
            var location = new CodeLocation(
                this.marker.Index,
                this.marker.Index + text.Length - 1,
                this.marker.IndexOnLine,
                this.marker.IndexOnLine + text.Length - 1,
                this.marker.LineNumber,
                this.marker.LineNumber);

            // Create the symbol.
            List<Symbol> symbols = new List<Symbol>(3);
            symbols.Add(new Symbol(text.ToString(), SymbolType.PreprocessorDirective, location));

            // Update the marker.
            this.marker.Index += text.Length;
            this.marker.IndexOnLine += text.Length;

            if (evaluate && preprocessorDefinition != null)
            {
                // Check the type of the symbol. If this is a conditional preprocessor symbol which resolves to false,
                // then we need to advance past all of the code which is not in scope.
                symbols.AddRange(this.CheckForConditionalCompilationDirective(document, symbols[0], preprocessorDefinition));
            }

            // Return the symbols.
            return symbols;
        }

        /// <summary>
        /// Checks the given preprocessor symbol to determine whether it is a conditional preprocessor directive.
        /// If so, determines whether we should skip past code which is out of scope.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="preprocessorSymbol">The symbol to check.</param>
        /// <param name="preprocessorDefinitions">The active configuration.</param>
        /// <returns>Returns the collection of symbols found while processing the directive.</returns>
        private ICollection<Symbol> CheckForConditionalCompilationDirective(CsDocument document, Symbol preprocessorSymbol, IDictionary<string, object> preprocessorDefinitions)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(preprocessorSymbol, "preprocessorSymbol");
            Param.Ignore(preprocessorDefinitions);

            List<Symbol> symbols = new List<Symbol>();

            while (true)
            {
                // Get the type of this preprocessor directive.
                int bodyIndex;
                string type = CsLanguageService.GetPreprocessorDirectiveType(preprocessorSymbol, out bodyIndex);
                if (type == "define")
                {
                    this.GetDefinePreprocessorDirective(document, preprocessorSymbol, bodyIndex, preprocessorDefinitions);
                    break;
                }
                else if (type == "undef")
                {
                    this.GetUndefinePreprocessorDirective(document, preprocessorSymbol, bodyIndex, preprocessorDefinitions);
                    break;
                }
                else if (type == "endif")
                {
                    // Pop this conditional directive off of the stack.
                    if (this.conditionalDirectives.Count == 0)
                    {
                        throw new SyntaxException(document, preprocessorSymbol.LineNumber);
                    }

                    this.conditionalDirectives.Pop();
                    break;
                }
                else
                {
                    // Extract an if, endif, or else directive.
                    bool skip;
                    if (!this.GetIfElsePreprocessorDirectives(
                        document, preprocessorSymbol, preprocessorDefinitions, bodyIndex, type, out skip))
                    {
                        // Check whether the code needs to be skipped.
                        if (skip)
                        {
                            // We will skip all code between this conditional preprocessor symbol and the next one.
                            StringBuilder skippedSectionText = new StringBuilder();
                            CodeLocation startLocation = null;
                            CodeLocation endLocation = null;
                            Symbol endSymbol = null;
                            int subConditionalPreprocessorCount = 0;

                            while (true)
                            {
                                // Get the next symbol.
                                IList<Symbol> symbolList = this.GetSymbol(document, preprocessorDefinitions, false);
                                if (symbolList == null || symbolList.Count != 1)
                                {
                                    throw new SyntaxException(document, preprocessorSymbol.LineNumber);
                                }

                                Symbol symbol = symbolList[0];

                                // Check whether this is another preprocessor.
                                if (symbol.SymbolType == SymbolType.PreprocessorDirective)
                                {
                                    // Check to see if this is a conditional preprocessor symbol, which indicates that we have
                                    // reached the end of out-of-scope code.
                                    type = CsLanguageService.GetPreprocessorDirectiveType(symbol, out bodyIndex);
                                    if (type == "if")
                                    {
                                        ++subConditionalPreprocessorCount;
                                    }
                                    else if (subConditionalPreprocessorCount > 0 && type == "endif")
                                    {
                                        --subConditionalPreprocessorCount;
                                    }
                                    else if (subConditionalPreprocessorCount == 0 &&
                                        (type == "elif" || type == "else" || type == "endif"))
                                    {
                                        // Reset the markers to the start of this preprocessor symbol.
                                        this.marker.Index = symbol.Location.StartPoint.Index;
                                        this.marker.IndexOnLine = symbol.Location.StartPoint.IndexOnLine;
                                        this.marker.LineNumber = symbol.Location.StartPoint.LineNumber;

                                        // Break from this loop.
                                        endSymbol = symbol;
                                        break;
                                    }
                                }

                                skippedSectionText.Append(symbol.Text);
                                if (startLocation == null)
                                {
                                    startLocation = symbol.Location;
                                }

                                endLocation = symbol.Location;
                            }

                            if (skippedSectionText.Length > 0)
                            {
                                symbols.Add(new Symbol(skippedSectionText.ToString(), SymbolType.SkippedSection, CodeLocation.Join(startLocation, endLocation)));
                            }

                            if (endSymbol != null)
                            {
                                symbols.Add(endSymbol);
                                preprocessorSymbol = endSymbol;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            // Indicate that this conditional directive has been entered.
                            if (this.conditionalDirectives.Count == 0)
                            {
                                throw new SyntaxException(document, preprocessorSymbol.LineNumber);
                            }

                            this.conditionalDirectives.Pop();
                            this.conditionalDirectives.Push(true);

                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return symbols;
        }

        /// <summary>
        /// Extracts an if, endif, or else directive.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="preprocessorSymbol">The preprocessor symbol being parsed.</param>
        /// <param name="preprocessorDefinitions">The current code configuration.</param>
        /// <param name="startIndex">The start index of the item within the symbols.</param>
        /// <param name="type">The type of the preprocessor symbol.</param>
        /// <param name="skip">Returns a value indicating whether the item should be skipped.</param>
        /// <returns>Returns a value indicating whether to ignore the item.</returns>
        private bool GetIfElsePreprocessorDirectives(
            CsDocument document, Symbol preprocessorSymbol, IDictionary<string, object> preprocessorDefinitions, int startIndex, string type, out bool skip)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(preprocessorSymbol, "preprocessorSymbol");
            Param.AssertNotNull(preprocessorDefinitions, "preprocessorDefinitions");
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");
            Param.AssertValidString(type, "type");

            bool ignore = false;

            skip = false;
            if (type == "if")
            {
                // Add this directive to the stack and indicate that it has not been entered.
                this.conditionalDirectives.Push(false);

                // Extract the body of the directive.
                var expressionProxy = new CodeUnitProxy(document);
                Expression body = CodeParser.GetConditionalPreprocessorBodyExpression(
                    document, this.source, expressionProxy, this.languageService, preprocessorDefinitions, preprocessorSymbol, startIndex);
                if (body == null)
                {
                    throw new SyntaxException(document, preprocessorSymbol.LineNumber);
                }

                // Determine whether the code under this directive needs to be skipped because it is out of scope.
                skip = !this.EvaluateConditionalDirectiveExpression(document, body, preprocessorDefinitions);
            }
            else if (type == "elif")
            {
                if (this.conditionalDirectives.Count == 0)
                {
                    throw new SyntaxException(document, preprocessorSymbol.LineNumber);
                }

                bool entered = this.conditionalDirectives.Peek();
                if (entered)
                {
                    skip = true;
                }
                else
                {
                    // Extract the body of the directive.
                    var expressionProxy = new CodeUnitProxy(document);
                    Expression body = CodeParser.GetConditionalPreprocessorBodyExpression(
                        document, this.source, expressionProxy, this.languageService, preprocessorDefinitions, preprocessorSymbol, startIndex);
                    if (body != null)
                    {
                        // Determine whether the code under this directive needs to be skipped because it is out of scope.
                        skip = !this.EvaluateConditionalDirectiveExpression(document, body, preprocessorDefinitions);
                    }
                }
            }
            else if (type == "else")
            {
                if (this.conditionalDirectives.Count == 0)
                {
                    throw new SyntaxException(document, preprocessorSymbol.LineNumber);
                }

                bool entered = this.conditionalDirectives.Peek();
                if (entered)
                {
                    skip = true;
                }
            }
            else
            {
                // This is not a conditional preprocessor directive.
                ignore = true;
            }

            return ignore;
        }

        /// <summary>
        /// Gets a define preprocessor directive from the code.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="preprocessorSymbol">The preprocessor symbol being parsed.</param>
        /// <param name="startIndex">The start index within the symbols.</param>
        /// <param name="preprocessorDefinitions">Optional preprocessor definitions.</param>
        private void GetDefinePreprocessorDirective(CsDocument document, Symbol preprocessorSymbol, int startIndex, IDictionary<string, object> preprocessorDefinitions)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(preprocessorSymbol, "preprocessorSymbol");
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");
            Param.Ignore(preprocessorDefinitions);

            var expressionProxy = new CodeUnitProxy(document);

            // Get the body of the define directive.
            LiteralExpression body = CodeParser.GetConditionalPreprocessorBodyExpression(
                document, this.source, expressionProxy, this.languageService, preprocessorDefinitions, preprocessorSymbol, startIndex) as LiteralExpression;
            if (body == null)
            {
                throw new SyntaxException(document, preprocessorSymbol.LineNumber);
            }

            // Create the defines list if necessary.
            if (this.defines == null)
            {
                this.defines = new Dictionary<string, string>();
            }

            // Add the item to the list.
            this.defines.Add(body.Text, body.Text);

            // Remove the item from the undefines list if it exists.
            if (this.undefines != null)
            {
                this.undefines.Remove(body.Text);
            }
        }

        /// <summary>
        /// Gets an undefine preprocessor directive from the code.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="preprocessorSymbol">The preprocessor symbol being parsed.</param>
        /// <param name="startIndex">The start index within the symbols.</param>
        /// <param name="preprocessorDefinitions">Optional preprocessor definitions.</param>
        private void GetUndefinePreprocessorDirective(CsDocument document, Symbol preprocessorSymbol, int startIndex, IDictionary<string, object> preprocessorDefinitions)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(preprocessorSymbol, "preprocessorSymbol");
            Param.AssertGreaterThanOrEqualToZero(startIndex, "startIndex");
            Param.Ignore(preprocessorDefinitions);

            var expressionProxy = new CodeUnitProxy(document);

            // Get the body of the undefine directive.
            LiteralExpression body = CodeParser.GetConditionalPreprocessorBodyExpression(
                document, this.source, expressionProxy, this.languageService, preprocessorDefinitions, preprocessorSymbol, startIndex) as LiteralExpression;
            if (body == null)
            {
                throw new SyntaxException(document, preprocessorSymbol.LineNumber);
            }

            // Create the undefines list if necessary.
            if (this.undefines == null)
            {
                this.undefines = new Dictionary<string, string>();
            }

            // Add the item to the list.
            this.undefines.Add(body.Text, body.Text);

            // Remove the item from the defines list if it exists.
            if (this.defines != null)
            {
                this.defines.Remove(body.Text);
            }
        }

        /// <summary>
        /// Evaluates an expression from within a conditional compiliation directive to determine
        /// whether it resolves to true or false.
        /// </summary>
        /// <param name="document">The parent document.</param>
        /// <param name="expression">The expression to evalulate.</param>
        /// <param name="preprocessorDefinitions">The active configuration.</param>
        /// <returns>Returns true if the expression evaluates to true, otherwise returns false.</returns>
        private bool EvaluateConditionalDirectiveExpression(
            CsDocument document, Expression expression, IDictionary<string, object> preprocessorDefinitions)
        {
            Param.AssertNotNull(document, "document");
            Param.AssertNotNull(expression, "expression");
            Param.Ignore(preprocessorDefinitions);

            bool value = false;

            // Switch on the possible expression type.
            switch (expression.ExpressionType)
            {
                case ExpressionType.Literal:
                    // Check the value of the literal.
                    LiteralExpression literal = expression as LiteralExpression;
                    if (literal.Text == "false")
                    {
                        // This is the 'false' keyword.
                        value = false;
                    }
                    else if (literal.Text == "true")
                    {
                        // This is the 'true' keyword.
                        value = true;
                    }
                    else
                    {
                        // Check whether this flag is defined in the document. If so, this resolves to true.
                        // Otherwise, this resolves to false.
                        if (this.undefines != null && this.undefines.ContainsKey(literal.Text))
                        {
                            value = false;
                        }
                        else if (this.defines != null && this.defines.ContainsKey(literal.Text))
                        {
                            value = true;
                        }
                        else
                        {
                            value = preprocessorDefinitions != null && preprocessorDefinitions.ContainsKey(literal.Text);
                        }
                    }

                    break;

                case ExpressionType.ConditionalLogical:
                    ConditionalLogicalExpression conditionalLogicalExpression = expression as ConditionalLogicalExpression;

                    // Evaluate the left side of the expression.
                    bool leftSide = this.EvaluateConditionalDirectiveExpression(
                        document, conditionalLogicalExpression.LeftHandSide, preprocessorDefinitions);

                    // Evaluate the right side of the expression.
                    bool rightSide = this.EvaluateConditionalDirectiveExpression(
                        document, conditionalLogicalExpression.RightHandSide, preprocessorDefinitions);

                    // Check whether this is a conditional OR or a conditional AND expression.
                    if (conditionalLogicalExpression.ConditionalLogicalExpressionType == ConditionalLogicalExpressionType.ConditionalAnd)
                    {
                        value = leftSide && rightSide;
                    }
                    else
                    {
                        value = leftSide || rightSide;
                    }

                    break;

                case ExpressionType.Relational:
                    RelationalExpression relationalExpression = expression as RelationalExpression;

                    // Evaluate the left side of the expression.
                    leftSide = this.EvaluateConditionalDirectiveExpression(
                        document, relationalExpression.LeftHandSide, preprocessorDefinitions);

                    // Evaluate the right side of the expression.
                    rightSide = this.EvaluateConditionalDirectiveExpression(
                        document, relationalExpression.RightHandSide, preprocessorDefinitions);

                    // Check whether this is an equality or an inequality expression.
                    if (relationalExpression.RelationalExpressionType == RelationalExpressionType.EqualTo)
                    {
                        value = leftSide == rightSide;
                    }
                    else if (relationalExpression.RelationalExpressionType == RelationalExpressionType.NotEqualTo)
                    {
                        value = leftSide != rightSide;
                    }
                    else
                    {
                        // Any other type of relational operator is not allowed in a conditional compilation directive.
                        throw new SyntaxException(document, expression.LineNumber);
                    }

                    break;

                case ExpressionType.Unary:
                    UnaryExpression unaryExpression = expression as UnaryExpression;
                    if (unaryExpression.UnaryExpressionType == UnaryExpressionType.Not)
                    {
                        // Evaluate the right side of the expression.
                        value = !this.EvaluateConditionalDirectiveExpression(
                            document, unaryExpression.Value, preprocessorDefinitions);
                    }
                    else
                    {
                        // Any other type of unary operator is not allowed in a conditional compilation directive.
                        throw new SyntaxException(document, expression.LineNumber);
                    }

                    break;

                case ExpressionType.Parenthesized:
                    // Evaluate the inner expression.
                    ParenthesizedExpression parenthesizedExpression = expression as ParenthesizedExpression;
                    value = this.EvaluateConditionalDirectiveExpression(
                        document, parenthesizedExpression.InnerExpression, preprocessorDefinitions);
                    break;

                default:
                    // Any other type of expression is not allowed within a conditional compilation directive.
                    throw new SyntaxException(document, expression.LineNumber);
            }

            return value;
        }

        /// <summary>
        /// Gets the next operator symbol.
        /// </summary>
        /// <param name="character">The first character of the symbol.</param>
        /// <returns>Returns the next operator symbol.</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode", Justification = "The method long, but simple.")]
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "The method long, but simple.")]
        private Symbol GetOperatorSymbol(char character)
        {
            Param.Ignore(character);

            SymbolType type = SymbolType.Other;
            var text = new StringBuilder();

            if (character == '.')
            {
                text.Append(".");
                type = SymbolType.Dot;
                this.codeReader.ReadNext();
            }
            else if (character == '~')
            {
                text.Append("~");
                type = SymbolType.Tilde;
                this.codeReader.ReadNext();
            }
            else if (character == '+')
            {
                text.Append("+");
                type = SymbolType.Plus;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '+')
                {
                    text.Append("+");
                    type = SymbolType.Increment;
                    this.codeReader.ReadNext();
                }
                else if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.PlusEquals;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '-')
            {
                text.Append("-");
                type = SymbolType.Minus;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '-')
                {
                    text.Append("-");
                    type = SymbolType.Decrement;
                    this.codeReader.ReadNext();
                }
                else if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.MinusEquals;
                    this.codeReader.ReadNext();
                }
                else if (character == '>')
                {
                    text.Append(">");
                    type = SymbolType.Pointer;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '*')
            {
                text.Append("*");
                type = SymbolType.Multiplication;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("*");
                    type = SymbolType.MultiplicationEquals;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '/')
            {
                text.Append("/");
                type = SymbolType.Division;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.DivisionEquals;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '|')
            {
                text.Append("|");
                type = SymbolType.LogicalOr;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.OrEquals;
                    this.codeReader.ReadNext();
                }
                else if (character == '|')
                {
                    text.Append("|");
                    type = SymbolType.ConditionalOr;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '&')
            {
                text.Append("&");
                type = SymbolType.LogicalAnd;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.AndEquals;
                    this.codeReader.ReadNext();
                }
                else if (character == '&')
                {
                    text.Append("&");
                    type = SymbolType.ConditionalAnd;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '^')
            {
                text.Append("^");
                type = SymbolType.LogicalXor;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.XorEquals;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '!')
            {
                text.Append("!");
                type = SymbolType.Not;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.NotEquals;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '%')
            {
                text.Append("%");
                type = SymbolType.Mod;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.ModEquals;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '=')
            {
                text.Append("=");
                type = SymbolType.Equals;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.ConditionalEquals;
                    this.codeReader.ReadNext();
                }
                else if (character == '>')
                {
                    text.Append(">");
                    type = SymbolType.Lambda;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == '<')
            {
                text.Append("<");
                type = SymbolType.LessThan;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.LessThanOrEquals;
                    this.codeReader.ReadNext();
                }
                else if (character == '<')
                {
                    text.Append("<");
                    type = SymbolType.LeftShift;
                    this.codeReader.ReadNext();

                    character = this.codeReader.Peek();
                    if (character == '=')
                    {
                        text.Append("=");
                        type = SymbolType.LeftShiftEquals;
                        this.codeReader.ReadNext();
                    }
                }
            }
            else if (character == '>')
            {
                text.Append(">");
                type = SymbolType.GreaterThan;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '=')
                {
                    text.Append("=");
                    type = SymbolType.GreaterThanOrEquals;
                    this.codeReader.ReadNext();
                }

                // Note: The right-shift symbol confuses the parsing of generics. 
                // If there are two greater-thans in a row then this may be a right-shift
                // symbol, but we cannot create it as such right now because it may also
                // be a couple of closing generic symbols in a row. This will have to be 
                // parsed out in the code. If this is a right-shift-equals then this will 
                // be created as three separate symbols, two greater-thans and then an 
                // equals. Later on we will recombine these.
            }
            else if (character == '?')
            {
                text.Append("?");
                type = SymbolType.QuestionMark;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == '?')
                {
                    text.Append("?");
                    type = SymbolType.NullCoalescingSymbol;
                    this.codeReader.ReadNext();
                }
            }
            else if (character == ':')
            {
                text.Append(":");
                type = SymbolType.Colon;
                this.codeReader.ReadNext();

                character = this.codeReader.Peek();
                if (character == ':')
                {
                    text.Append(":");
                    type = SymbolType.QualifiedAlias;
                    this.codeReader.ReadNext();
                }
            }

            // Make sure we have a symbol now.
            if (text == null || text.Length == 0)
            {
                throw new SyntaxException(this.source, this.marker.LineNumber);
            }

            // Create the code location.
            var location = new CodeLocation(
                this.marker.Index,
                this.marker.Index + text.Length - 1,
                this.marker.IndexOnLine,
                this.marker.IndexOnLine + text.Length - 1,
                this.marker.LineNumber,
                this.marker.LineNumber);

            // Create the token.
            var symbol = new Symbol(text.ToString(), type, location);

            // Update the marker.
            this.marker.Index += text.Length;
            this.marker.IndexOnLine += text.Length;

            // Return the symbol.
            return symbol;
        }

        /// <summary>
        /// Advances to the next end of line character and adds all characters to the given text buffer.
        /// </summary>
        /// <param name="text">The text buffer in which to store the rest of the line.</param>
        private void AdvanceToEndOfLine(StringBuilder text)
        {
            Param.AssertNotNull(text, "text");

            int offsetIndex = this.FindNextEndOfLine();
            if (offsetIndex != -1)
            {
                text.Append(this.codeReader.ReadString(offsetIndex));
            }
        }

        /// <summary>
        /// Finds the offset index of the next end-of-line character.
        /// </summary>
        /// <returns>Returns the offset index of the next end-of-line character. If there are no more end-of-line
        /// characters, returns the index of the character past the end of the file.</returns>
        private int FindNextEndOfLine()
        {
            int endOfLine = -1;
            int carriageReturn = -1;

            int index = 0;
            while (true)
            {
                char character = this.codeReader.Peek(index);
                if (character == char.MinValue)
                {
                    break;
                }
                else if (character == '\r')
                {
                    if (carriageReturn == -1)
                    {
                        carriageReturn = index;

                        if (endOfLine != -1)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else if (character == '\n')
                {
                    if (endOfLine == -1)
                    {
                        endOfLine = index;

                        if (carriageReturn != -1)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                ++index;
            }

            if (endOfLine != -1 && carriageReturn != -1)
            {
                return Math.Min(endOfLine, carriageReturn);
            }
            else if (carriageReturn != -1)
            {
                return carriageReturn;
            }
            else if (endOfLine != -1)
            {
                return endOfLine;
            }

            // No end of line character was found. This means that we read all the way to the end of the
            // file. In this case the index is sitting at one past the end of the file, so return the
            // offset index of the last character in the file.
            return index;
        }

        #endregion Private Methods
    }
}
