﻿namespace CsDebugScript.CodeGen.TypeTrees
{
    /// <summary>
    /// Type tree that represents basic type.
    /// </summary>
    /// <seealso cref="TypeTree" />
    internal class BasicTypeTree : TypeTree
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BasicTypeTree"/> class.
        /// </summary>
        /// <param name="basicType">The basic type string.</param>
        public BasicTypeTree(string basicType)
        {
            BasicType = basicType;
        }

        /// <summary>
        /// Gets the basic type string.
        /// </summary>
        public string BasicType { get; private set; }

        /// <summary>
        /// Gets the string representing this type tree in C# code.
        /// </summary>
        /// <param name="truncateNamespace">if set to <c>true</c> namespace will be truncated from generating type string.</param>
        /// <returns>
        /// The string representing this type tree in C# code.
        /// </returns>
        public override string GetTypeString(bool truncateNamespace = false)
        {
            return BasicType;
        }
    }
}
