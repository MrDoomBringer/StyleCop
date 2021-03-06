//-----------------------------------------------------------------------
// <copyright file="CollectionPropertyDescriptor.cs">
//   MS-PL
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
namespace StyleCop
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;

    /// <summary>
    /// A property descriptor for a collection property.
    /// </summary> 
    public class CollectionPropertyDescriptor : PropertyDescriptor
    {
        #region Private Fields

        /// <summary>
        /// Indicates whether the collection is an aggregate collection.
        /// </summary>
        private bool aggregate;

        #endregion Private Fields

        #region Internal Constructors

        /// <summary>
        /// Initializes a new instance of the CollectionPropertyDescriptor class.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="friendlyName">The friendly name of the property.</param>
        /// <param name="description">The property description.</param>
        /// <param name="merge">Indicates whether to merge the property with parent properties.</param>
        /// <param name="aggregate">Indicates whether the collection is an aggregate collection.</param>
        internal CollectionPropertyDescriptor(
            string propertyName, string friendlyName, string description, bool merge, bool aggregate)
            : base(propertyName, PropertyType.Collection, friendlyName, description, merge, false)
        {
            Param.Ignore(propertyName);
            Param.Ignore(friendlyName);
            Param.Ignore(description);
            Param.Ignore(merge);
            Param.Ignore(aggregate);

            this.aggregate = aggregate;
        }

        #endregion Internal Constructors

        #region Public Properties

        /// <summary>
        /// Gets a value indicating whether the collection is an aggregate collection.
        /// </summary>
        public bool Aggregate
        {
            get
            {
                return this.aggregate;
            }
        }

        #endregion Public Properties
    }
}
