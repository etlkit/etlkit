using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace EtlKit.Scripting
{
    /// <summary>
    /// Result description for globals type to import into script.
    /// </summary>
    public record GlobalsTypeInfo
    {
        /// <summary>
        /// Result description for dynamically generated global type.
        /// </summary>
        public GlobalsTypeInfo(
            Assembly assembly,
            MetadataReference reference,
            Type type,
            IEnumerable<Assembly> referencedAssemblies
        )
        {
            Assembly = assembly;
            Reference = reference;
            Type = type;
            ReferencedAssemblies = referencedAssemblies;
        }

        /// <summary>
        /// Generated assembly.
        /// </summary>
        public Assembly Assembly { get; }

        /// <summary>
        /// Reference to generated assembly.
        /// </summary>
        public MetadataReference Reference { get; }

        /// <summary>
        /// Generated type.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// List of assemblies, referenced by the generated type.
        /// </summary>
        public IEnumerable<Assembly> ReferencedAssemblies { get; }
    }
}
