using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;

namespace EtlKit.Scripting;

/// <summary>
/// Creates a global type and a runner for a C# script with dynamic type globals.
/// Workaround for ref: https://github.com/dotnet/roslyn/issues/3194
/// </summary>
/// <example>
/// <code>
/// var expandoObject = new ExpandoObject();
/// expandoObject.X = 1;
/// expandoObject.Y = 2;
/// var addXY = ScriptBuilder.Default.ForType(expandoObject).CreateScript("X+Y").Run().ReturnValue.Result;
/// Assert.Equal(3, addXY);
/// </code>
/// </example>
public class ScriptBuilder
{
    /// <summary>
    /// Default instance of the script builder.
    /// </summary>
    public static ScriptBuilder Default { get; } = new();

    /// <summary>
    /// Global usings for the script.
    /// </summary>
    private const string Usings = "using System; using System.Collections.Generic;";

    /// <summary>
    /// Unique number for each instance of the script builder.
    /// </summary>
    private int _unique; // = 0

    /// <summary>
    /// Type cache for dynamically generated types.
    /// </summary>
    private readonly ConcurrentDictionary<int, GlobalsTypeInfo> _cache = new();

    /// <summary>
    /// Create statically declared type from a dictionary of extension names and types (typically from ExpandoObject).
    /// </summary>
    /// <param name="dynamicObject">Dynamically declared properties (normally an ExpandoObject)</param>
    /// <param name="hashCode">Hash code generated for a type (by default hash will be generated from types of all properties)</param>
    /// <returns>Script builder with a given dynamic type context</returns>
    public TypedScriptBuilder ForType(
        IDictionary<string, object?> dynamicObject,
        int? hashCode = null
    )
    {
        var key = hashCode ?? GetExpandoObjectTypeHash(dynamicObject);
        var typeInfo = _cache.GetOrAdd(key, _ => CreateCore(dynamicObject));
        return new TypedScriptBuilder(typeInfo);
    }

    /// <summary>
    /// Create statically declared type from a dictionary of extension names and types (typically from ExpandoObject).
    /// </summary>
    /// <param name="hashCode">Hash code generated for a type (by default hash will be generated from types of all properties)</param>
    /// <returns>Script builder with a given dynamic type context</returns>
    public TypedScriptBuilder ForType<TInput>(int? hashCode = null)
    {
        var key = hashCode ?? typeof(TInput).GetHashCode();
        GlobalsTypeInfo typeInfo = _cache.GetOrAdd(key, _ => CreateCore(typeof(TInput)));
        return new TypedScriptBuilder(typeInfo);
    }

    private static GlobalsTypeInfo CreateCore(Type type)
    {
        var typeInfo = new GlobalsTypeInfo(
            assembly: type.Assembly,
            reference: MetadataReference.CreateFromFile(type.Assembly.Location),
            type: type,
            referencedAssemblies: GetReferencedAssemblies(type).ToList()
        );
        return typeInfo;
    }

    private GlobalsTypeInfo CreateCore(IDictionary<string, object?> dynamicObject)
    {
        var code =
            $"{Usings}{Environment.NewLine}{BuildClassCode(out var typeName, dynamicObject)}";

        // Using ReflectionEmit with Microsoft.CodeAnalysis.CSharp.Scripting is not supported.
        // The workaround is to use CSharpCompilation instead.
        // ref: https://github.com/dotnet/roslyn/issues/2246
        // ref: https://github.com/dotnet/roslyn/pull/6254
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);

        var referencedAssemblies = GetReferencedAssemblies(dynamicObject).ToList();
        Compilation compilation = CSharpCompilation.Create(
            $"ScriptGlobalTypeBuilder{typeName}",
            new[] { syntaxTree },
            referencedAssemblies.Select(assembly =>
                MetadataReference.CreateFromFile(assembly.Location)
            ),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        ImmutableArray<byte> assemblyBytes = EmitToArray(compilation);
        PortableExecutableReference libRef = MetadataReference.CreateFromImage(assemblyBytes);
        var assembly = Assembly.Load(assemblyBytes.ToArray());

        Type type =
            assembly.GetType(typeName)
            ?? throw new InvalidOperationException($"Type {typeName} not found.");
        return new GlobalsTypeInfo(
            assembly: assembly,
            reference: libRef,
            type: type,
            referencedAssemblies: referencedAssemblies
        );
    }

    private string BuildClassCode(out string typeName, IDictionary<string, object?> dynamicObject)
    {
        var count = Interlocked.Increment(ref _unique);
        typeName = $"DynamicType{count}";

        // Split all members into dynamic and typed lists
        var dynamicMembers = new Dictionary<string, IDictionary<string, object?>>();
        var typedMembers = new Dictionary<string, Type?>();
        foreach (var pair in dynamicObject)
        {
            if (pair.Value is IDictionary<string, object?> expando)
            {
                dynamicMembers.Add(pair.Key, expando);
            }
            else
            {
                if (IsAnonymousType(pair.Value?.GetType()))
                    throw new ArgumentException("Anonymous types are not supported.");
                typedMembers.Add(pair.Key, pair.Value?.GetType());
            }
        }

        // For each of the inner ExpandoObject properties, recursively generate a nested type
        var nestedTypeDeclarations = new Dictionary<string, (string type, string code)>();
        foreach (var member in dynamicMembers)
        {
            var code = BuildClassCode(out var innerType, member.Value);
            nestedTypeDeclarations.Add(member.Key, (innerType, code));
        }

        // Member declarations for both typed and non-typed members
        var memberDeclarations = typedMembers
            .Select(pair => $"public {FullTypeName(pair.Value)} {pair.Key} {{ get; }}")
            .Concat(
                nestedTypeDeclarations.Select(pair =>
                    $"public {pair.Value.type} {pair.Key} {{ get; }}"
                )
            );

        // Generate assignments in constructor for both typed and non-typed members
        var typedArgumentAssignments = typedMembers.Select(pair =>
            $"{pair.Key} = ({FullTypeName(pair.Value)})extensions[\"{pair.Key}\"];"
        );
        var dynamicArgumentAssignments = nestedTypeDeclarations.Select(pair =>
            $"{pair.Key} = new {pair.Value.type}((IDictionary<string, object?>)extensions[\"{pair.Key}\"]);"
        );
        var constructorArguments = typedArgumentAssignments.Concat(dynamicArgumentAssignments);

        // Generate the class code
        return $@"
public class {typeName}
{{
    // Default constructor
    public {typeName}(IDictionary<string, object?> extensions)
    {{
        {string.Join(Environment.NewLine, constructorArguments)}
    }}
    // Member declarations
    {string.Join(Environment.NewLine, memberDeclarations)}
    // Nested type declarations
    {string.Join(Environment.NewLine, nestedTypeDeclarations.Select(pair => pair.Value.code))}
}}";
    }

    /// <summary>
    /// Fix naming of nested types like "ETLBox.Scripting.Tests.ScriptBuilderTests+MyCoolClass"
    /// </summary>
    private static string FullTypeName(Type? type) =>
        type?.FullName?.Replace('+', '.') ?? "dynamic";

    /// <summary>
    /// Detect anonymous types
    /// </summary>
    private static bool IsAnonymousType(Type? type)
    {
        if (type == null)
            return false;
        var hasCompilerGeneratedAttribute =
            type.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
        var nameContainsAnonymousType = type.FullName?.Contains("AnonymousType") ?? false;
        var nameStartsWithLessThan = type.Name.StartsWith("<>");

        return hasCompilerGeneratedAttribute && nameContainsAnonymousType && nameStartsWithLessThan;
    }

    private static ImmutableArray<byte> EmitToArray(Compilation compilation)
    {
        using var assemblyStream = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(assemblyStream);

        if (emitResult.Success)
        {
            return ImmutableArray.Create(assemblyStream.ToArray());
        }

        throw new CompilationErrorException(
            $@"Failed to compile dynamic type.
COMPILATION ERRORS:
{GetErrorMessages(emitResult.Diagnostics)}
SOURCE CODE:
{emitResult.Diagnostics.FirstOrDefault()?.Location.SourceTree?.GetText()}",
            emitResult.Diagnostics
        );
    }

    private static string GetErrorMessages(ImmutableArray<Diagnostic> emitResultDiagnostics) =>
        string.Join(
            Environment.NewLine,
            emitResultDiagnostics
                .Where(diagnostic =>
                    diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error
                )
                .Select(GetLineError)
        );

    private static string GetLineError(Diagnostic diagnostic)
    {
        var text = diagnostic.Location.SourceTree?.GetText();
        var startLinePosition = diagnostic.Location.GetLineSpan().StartLinePosition;
        var line = text?.Lines[startLinePosition.Line];
        var pointer = new string(' ', startLinePosition.Character) + "^";
        var sourceSpan = text?.ToString(diagnostic.Location.SourceSpan);
        return line == null
            ? $"{diagnostic} near '{sourceSpan}'"
            : $"{line}\n{pointer}\n{diagnostic} near '{sourceSpan}'";
    }

    private static int GetExpandoObjectTypeHash(IDictionary<string, object?> expando)
    {
        var keys = expando.Keys.ToArray();
        unchecked // Overflow is fine, just wrap
        {
            var hash = 17;
            for (var i = 0; i < keys.Length; i++)
            {
                hash = hash * 23 + i + keys[i].GetHashCode();
                var value = expando[keys[i]];

                if (value == null)
                    continue;

                if (value is IDictionary<string, object?> nestedExpando)
                {
                    hash = hash * 23 + GetExpandoObjectTypeHash(nestedExpando);
                }
                else
                {
                    hash = hash * 23 + value.GetType().GetHashCode();
                }
            }

            return hash;
        }
    }

    private static IEnumerable<Assembly> GetReferencedAssemblies(Type type)
    {
        HashSet<Assembly> assemblies = new HashSet<Assembly>();

        // Add the assembly of the initial type
        CollectTypeAssemblies(type, assemblies);

        // Add the assemblies of all members (fields, properties, methods)
        foreach (MemberInfo member in type.GetMembers())
        {
            CollectMemberAssemblies(member, assemblies);
        }

        return assemblies;
    }

    private static IEnumerable<Assembly> GetReferencedAssemblies(
        IDictionary<string, object?> expando
    )
    {
        var assemblies = new HashSet<Assembly>
        {
            typeof(Attribute).Assembly,
            typeof(DynamicAttribute).Assembly,
            // Required when struct types from non-core assemblies (e.g. JsonElement) appear as
            // property types: Roslyn resolves ValueType from System.Runtime, not System.Private.CoreLib.
            Assembly.Load(new AssemblyName("System.Runtime")),
        };
        CollectExpandoObjectAssemblies(expando, assemblies);
        return assemblies;
    }

    private static void CollectExpandoObjectAssemblies(
        IDictionary<string, object?> expando,
        HashSet<Assembly> assemblies
    )
    {
        foreach (var value in expando.Values)
        {
            if (value is IDictionary<string, object?> inner)
            {
                CollectExpandoObjectAssemblies(inner, assemblies);
            }
            else
            {
                CollectTypeAssemblies(value?.GetType(), assemblies);
            }
        }
    }

    private static void CollectMemberAssemblies(MemberInfo member, ISet<Assembly> assemblies)
    {
        switch (member.MemberType)
        {
            case MemberTypes.Field:
                CollectTypeAssemblies(((FieldInfo)member).FieldType, assemblies);
                break;
            case MemberTypes.Property:
                CollectTypeAssemblies(((PropertyInfo)member).PropertyType, assemblies);
                break;
            case MemberTypes.Method:
                MethodInfo methodInfo = (MethodInfo)member;
                CollectTypeAssemblies(methodInfo.ReturnType, assemblies);
                foreach (ParameterInfo parameter in methodInfo.GetParameters())
                {
                    CollectTypeAssemblies(parameter.ParameterType, assemblies);
                }

                break;
            case MemberTypes.Event:
                CollectTypeAssemblies(((EventInfo)member).EventHandlerType, assemblies);
                break;
            case MemberTypes.NestedType:
                CollectTypeAssemblies(((System.Reflection.TypeInfo)member).AsType(), assemblies);
                break;
        }
    }

    private static void CollectTypeAssemblies(Type? t, ISet<Assembly> assemblies)
    {
        if (t == null || t == typeof(object) || !assemblies.Add(t.Assembly))
            return;

        // Base type
        CollectTypeAssemblies(t.BaseType, assemblies);

        // Generic arguments
        if (t.IsGenericType)
        {
            foreach (Type? arg in t.GetGenericArguments())
            {
                CollectTypeAssemblies(arg, assemblies);
            }
        }

        // Interfaces
        foreach (Type? @interface in t.GetInterfaces())
        {
            CollectTypeAssemblies(@interface, assemblies);
        }
    }
}
