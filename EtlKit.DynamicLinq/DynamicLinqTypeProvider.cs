using System;
using System.Collections.Generic;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Reflection;

namespace EtlKit.DynamicLinq;

/// <summary>
/// <see cref="IDynamicLinqCustomTypeProvider"/> implementation that exposes a known set
/// of types to the parser and applies optional namespace prefixes when resolving short
/// type names.
/// </summary>
/// <remarks>
/// Constructed by <see cref="ExpressionRowFiltration{TInput}"/> when any of
/// <see cref="ExpressionRowFiltration{TInput}.RegisterCustomTypes(Type[])"/>,
/// <see cref="ExpressionRowFiltration{TInput}.AdditionalAssemblyNames"/> or
/// <see cref="ExpressionRowFiltration{TInput}.AdditionalImports"/> is set. The
/// provider itself is stateless after construction. Internal API - not part of the
/// public surface.
/// </remarks>
internal sealed class DynamicLinqTypeProvider : IDynamicLinqCustomTypeProvider
{
    private readonly HashSet<Type> _types;
    private readonly string[] _imports;
    private readonly Dictionary<string, Type> _byFullName;
    private readonly Dictionary<string, List<Type>> _byShortName;

    public DynamicLinqTypeProvider(HashSet<Type> types, string[]? imports = null)
    {
        _types = types;
        _imports = imports ?? Array.Empty<string>();
        _byFullName = new Dictionary<string, Type>(types.Count, StringComparer.Ordinal);
        _byShortName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
        foreach (var t in types)
        {
            if (t.FullName is { } fn)
            {
                _byFullName[fn] = t;
            }
            if (!_byShortName.TryGetValue(t.Name, out var bucket))
            {
                bucket = new List<Type>();
                _byShortName[t.Name] = bucket;
            }
            bucket.Add(t);
        }
    }

    public HashSet<Type> GetCustomTypes() => _types;

    public Dictionary<Type, List<MethodInfo>> GetExtensionMethods() => new();

    public Type? ResolveType(string typeName)
    {
        // Direct match by full or short name. Lookup is O(1) on the indexed
        // dictionaries; falls back to import-prefixed search only when neither
        // direct form is registered. Mirrors ResolveTypeBySimpleName: ambiguous
        // short names throw with a clear pointer rather than silently picking one.
        if (_byFullName.TryGetValue(typeName, out var byFull))
            return byFull;
        if (_byShortName.TryGetValue(typeName, out var byShort))
        {
            if (byShort.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Ambiguous short type name '{typeName}': multiple registered types match. Use the full type name in the expression, or add the resolving namespace to AdditionalImports."
                );
            }
            return byShort[0];
        }

        return ResolveBySimpleNameInImports(typeName);
    }

    public Type? ResolveTypeBySimpleName(string simpleTypeName)
    {
        // Imported namespaces win over plain short-name match - explicit user
        // intent ("I imported this namespace, prefer types from it").
        var fromImport = ResolveBySimpleNameInImports(simpleTypeName);
        if (fromImport is not null)
            return fromImport;

        // Detect ambiguity: two or more registered types share the short name
        // and no import disambiguates them. Throw with a clear pointer instead
        // of silently picking one based on iteration order.
        if (_byShortName.TryGetValue(simpleTypeName, out var matches))
        {
            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Ambiguous short type name '{simpleTypeName}': multiple registered types match. Use the full type name in the expression, or add the resolving namespace to AdditionalImports."
                );
            }
            return matches[0];
        }
        return null;
    }

    private Type? ResolveBySimpleNameInImports(string simpleName)
    {
        foreach (var import in _imports)
        {
            if (_byFullName.TryGetValue($"{import}.{simpleName}", out var t))
                return t;
        }
        return null;
    }
}
