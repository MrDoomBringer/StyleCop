//-----------------------------------------------------------------------
// <copyright file="Token.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.
// </copyright>
// <license>
//   This source code is subject to terms and conditions of the Microsoft 
//   Public License. A copy of the license can be found in the License.html 
//   file at the root of this distribution. If you cannot locate the  
//   Microsoft Public License, please send an email to dlr@microsoft.com. 
//   By using this source code in any fashion, you are agreeing to be bound 
//   by the terms of the Microsoft Public License. You must not remove this 
//   notice, or any other, from this software.
// </license>
//-----------------------------------------------------------------------
namespace Microsoft.StyleCop.CSharp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Describes a single token within a C# document.
    /// </summary>
    /// <subcategory>token</subcategory>
    public abstract class Token : LexicalElement
    {
        #region Internal Static Fields

        ///// <summary>
        ///// An empty array of tokens.
        ///// </summary>
        ////internal static readonly Token[] EmptyTokenArray = new Token[] { };

        #endregion Internal Static Fields

        #region Internal Constructors

        /// <summary>
        /// Initializes a new instance of the Token class.
        /// </summary>
        /// <param name="text">The token string.</param>
        /// <param name="tokenType">The token type.</param>
        /// <param name="location">The location of the token within the code document.</param>
        /// <param name="generated">True if the token is inside of a block of generated code.</param>
        internal Token(string text, TokenType tokenType, CodeLocation location, bool generated)
            : this(text, null, (int)tokenType, location, generated)
        {
            Param.Ignore(text, tokenType, location, generated);
        }

        /// <summary>
        /// Initializes a new instance of the Token class.
        /// </summary>
        /// <param name="text">The token string.</param>
        /// <param name="tokenType">The token type.</param>
        /// <param name="location">The location of the token within the code document.</param>
        /// <param name="generated">True if the token is inside of a block of generated code.</param>
        internal Token(string text, int tokenType, CodeLocation location, bool generated)
            : this(text, null, tokenType, location, generated)
        {
            Param.Ignore(text, tokenType, location, generated);
        }

        /// <summary>
        /// Initializes a new instance of the Token class.
        /// </summary>
        /// <param name="text">The token string.</param>
        /// <param name="proxy">Proxy object for the expression.</param>
        /// <param name="tokenType">The token type.</param>
        /// <param name="location">The location of the token within the code document.</param>
        /// <param name="generated">True if the token is inside of a block of generated code.</param>
        internal Token(string text, CodeUnitProxy proxy, int tokenType, CodeLocation location, bool generated)
            : this(proxy, tokenType, location, generated)
        {
            Param.AssertNotNull(text, "text");
            Param.Ignore(proxy);
            Param.Ignore(tokenType);
            Param.Ignore(location);
            Param.Ignore(generated);

            this.Text = text;
        }

        /// <summary>
        /// Initializes a new instance of the Token class.
        /// </summary>
        /// <param name="proxy">Proxy object for the token.</param>
        /// <param name="tokenType">The token type.</param>
        /// <param name="location">The location of the token within the code document.</param>
        /// <param name="generated">True if the token is inside of a block of generated code.</param>
        internal Token(CodeUnitProxy proxy, TokenType tokenType, CodeLocation location, bool generated)
            : this(proxy, (int)tokenType, location, generated)
        {
            Param.Ignore(proxy);
            Param.Ignore(tokenType);
            Param.Ignore(location);
            Param.Ignore(generated);
        }

        /// <summary>
        /// Initializes a new instance of the Token class.
        /// </summary>
        /// <param name="proxy">Proxy object for the token.</param>
        /// <param name="tokenType">The token type.</param>
        /// <param name="location">The location of the token within the code document.</param>
        /// <param name="generated">True if the token is inside of a block of generated code.</param>
        internal Token(CodeUnitProxy proxy, int tokenType, CodeLocation location, bool generated)
            : base(proxy, tokenType, location, generated)
        {
            Param.Ignore(proxy);
            Param.Ignore(tokenType);
            Param.AssertNotNull(location, "location");
            Param.Ignore(generated);
            Debug.Assert(System.Enum.IsDefined(typeof(TokenType), this.TokenType), "The type is invalid.");
        }

        #endregion Internal Constructors

        #region Public Properties

        /// <summary>
        /// Gets the token Type.
        /// </summary>
        public TokenType TokenType
        {
            get
            {
                return (TokenType)(this.FundamentalType & (int)FundamentalTypeMasks.Token);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the token is composed of multiple child tokens.
        /// </summary>
        public bool IsComplexToken
        {
            get;
            protected set;
        }

        #endregion Public Properties
    }
}