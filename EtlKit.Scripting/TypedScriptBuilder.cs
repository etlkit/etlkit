using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;

namespace EtlKit.Scripting
{
    /// <summary>
    /// Helper class to create a script with a typed global type.
    /// </summary>
    [PublicAPI]
    public class TypedScriptBuilder
    {
        private readonly GlobalsTypeInfo _globalsTypeInfo;
        private readonly IEnumerable<string> _additionalImports;
        private readonly NullableContextOptions _nullableContextOptions;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="globalsTypeInfo">Script "Global" type information</param>
        /// <param name="additionalImports">Additional namespaces to import into the script</param>
        /// <param name="nullableContextOptions">Nullable context for compiled script expressions</param>
        public TypedScriptBuilder(
            GlobalsTypeInfo globalsTypeInfo,
            IEnumerable<string>? additionalImports = null,
            NullableContextOptions nullableContextOptions = NullableContextOptions.Disable
        )
        {
            _globalsTypeInfo = globalsTypeInfo;
            _additionalImports = additionalImports ?? Enumerable.Empty<string>();
            _nullableContextOptions = nullableContextOptions;
        }

        /// <summary>
        /// The type of global context with which the script will be executed.
        /// </summary>
        public Type GlobalsType => _globalsTypeInfo.Type;

        /// <summary>
        /// Copy and add references to the script.
        /// </summary>
        /// <param name="assemblies">List of additional <see cref="Assembly"/> object to reference from script</param>
        /// <returns>Copy of original script builder with references added</returns>
        public TypedScriptBuilder WithReferences(IEnumerable<Assembly> assemblies)
        {
            return new TypedScriptBuilder(
                new GlobalsTypeInfo(
                    assembly: _globalsTypeInfo.Assembly,
                    reference: _globalsTypeInfo.Reference,
                    type: _globalsTypeInfo.Type,
                    referencedAssemblies: _globalsTypeInfo.ReferencedAssemblies.Concat(assemblies)
                ),
                _additionalImports,
                _nullableContextOptions
            );
        }

        /// <summary>
        /// Copy and add namespace imports to the script.
        /// </summary>
        /// <param name="imports">Additional namespaces to import (e.g. <c>"System.Text.Json"</c>)</param>
        /// <returns>Copy of original script builder with imports added</returns>
        public TypedScriptBuilder WithImports(IEnumerable<string> imports)
        {
            return new TypedScriptBuilder(
                _globalsTypeInfo,
                _additionalImports.Concat(imports),
                _nullableContextOptions
            );
        }

        /// <summary>
        /// Copy and set the nullable annotation context for compiled script expressions.
        /// </summary>
        /// <param name="options">Nullable context options</param>
        /// <returns>Copy of original script builder with nullable context set</returns>
        public TypedScriptBuilder WithNullableContextOptions(NullableContextOptions options)
        {
            return new TypedScriptBuilder(_globalsTypeInfo, _additionalImports, options);
        }

        /// <summary>
        /// Create script with arguments of given type and return type of object.
        /// </summary>
        /// <param name="scriptContent">Script source</param>
        /// <returns></returns>
        public ScriptRunner<object> CreateRunner(string scriptContent) =>
            CreateRunner<object>(scriptContent);

        /// <summary>
        /// Create script with arguments of given type and return type of T.
        /// </summary>
        /// <param name="scriptContent">Script source</param>
        /// <typeparam name="TOutput">Return type</typeparam>
        /// <returns></returns>
        public ScriptRunner<TOutput> CreateRunner<TOutput>(string scriptContent)
        {
            //ref: https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md
            var options = ScriptOptions
                .Default.AddImports("System")
                .AddImports("System.Text")
                .AddImports(_additionalImports)
                .AddReferences(_globalsTypeInfo.ReferencedAssemblies)
                .AddReferences(_globalsTypeInfo.Reference);

            // ScriptOptions has no NullableContextOptions API; inject the pragma instead.
            // The `#nullable` directive only accepts enable|disable|restore, optionally
            // followed by `warnings` or `annotations`, so the enum values that toggle a
            // single dimension must be mapped to the explicit `enable <dimension>` form.
            var directive = _nullableContextOptions switch
            {
                NullableContextOptions.Enable => "#nullable enable",
                NullableContextOptions.Warnings => "#nullable enable warnings",
                NullableContextOptions.Annotations => "#nullable enable annotations",
                _ => null,
            };
            var code = directive is null ? scriptContent : $"{directive}\n{scriptContent}";

            using var loader = new InteractiveAssemblyLoader();
            loader.RegisterDependency(_globalsTypeInfo.Assembly);

            var script = CSharpScript.Create<TOutput>(
                code,
                options,
                globalsType: _globalsTypeInfo.Type,
                assemblyLoader: loader
            );

            return new ScriptRunner<TOutput>(script, _globalsTypeInfo);
        }
    }
}
