using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace EtlKit.DynamicLinq;

/// <summary>
/// Maps an <see cref="ExpandoObject"/> to a runtime DynamicClass instance with typed
/// properties so that <c>System.Linq.Dynamic.Core</c> can resolve property names
/// against a real CLR type.
/// </summary>
/// <remarks>
/// <para>
/// Has two execution paths chosen automatically per row:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Fast path</b> for flat shapes (scalars, nullables, strings, byte arrays, custom
/// classes). Compiles a per-shape <c>Func&lt;IDictionary, object&gt;</c> via
/// Expression Trees once per shape and caches it; per-row cost is one dict walk +
/// cache lookup + delegate invoke. No reflection in the hot path. Covers the typical
/// Common.Etl XML flow (DB row -> ExpandoObject with scalar fields).
/// </description></item>
/// <item><description>
/// <b>Slow path</b> for shapes with nested <see cref="IDictionary{TKey,TValue}"/> or
/// homogeneous collections (lists of dictionaries or scalars). Recurses into nested
/// shapes and uses reflection-based property assignment.
/// </description></item>
/// </list>
/// <para>
/// The fast-path cache keys on <c>(field name, runtime value type)</c> tuples. A row
/// with <c>Reserve = null</c> produces a different signature than one with
/// <c>Reserve = 100m</c> (signature for null rows uses <c>typeof(object)</c>,
/// signature for valued rows uses the concrete type) - the cache holds two compiled
/// mappers, one per variant.
/// </para>
/// <para>
/// <c>string</c> and <c>byte[]</c> are deliberately treated as scalar values rather
/// than as <c>IEnumerable</c> collections. Items in a nested collection that share
/// field names are unified: missing fields and null values widen the property type
/// to <c>Nullable&lt;T&gt;</c> for value types so optional values do not throw.
/// Conflicting non-null types for the same field across items still throw
/// <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// Internal API - used by <see cref="ExpressionRowFiltration"/>. Not part of the
/// public surface.
/// </para>
/// </remarks>
internal static class ExpandoTypeMapper
{
    private static readonly ConcurrentDictionary<ShapeSignature, ShapeEntry> _fastPathCache = new();

    /// <summary>
    /// Maps the row to a runtime DynamicClass instance. Routes to the fast compiled
    /// mapper for flat shapes or the recursive reflection mapper for shapes that
    /// contain nested dictionaries or collections.
    /// </summary>
    public static (Type Type, object Instance) Map(ExpandoObject row)
    {
        var dict = (IDictionary<string, object?>)row;

        // One walk: build signature and detect complex fields together. If the row
        // contains a nested IDictionary or non-string/non-byte-array enumerable,
        // ShapeSignature.TryFrom returns false and we route to the slow path.
        if (!ShapeSignature.TryFrom(dict, out var signature))
        {
            return MapWithReflection(dict);
        }

        var entry = _fastPathCache.GetOrAdd(signature, BuildCompiledEntry);
        var instance = entry.Mapper(dict);
        return (entry.Type, instance);
    }

    // === Fast path: compiled per-shape mapper ===

    // Builds a compiled mapper once per unique shape signature. Subject to
    // ConcurrentDictionary.GetOrAdd factory race semantics - multiple invocations
    // under contention are functionally equivalent (same expression, same shape).
    private static ShapeEntry BuildCompiledEntry(ShapeSignature signature)
    {
        var properties = new DynamicProperty[signature.Fields.Count];
        for (var i = 0; i < signature.Fields.Count; i++)
        {
            properties[i] = new DynamicProperty(signature.Fields[i].Name, signature.Fields[i].Type);
        }
        var type = DynamicClassFactory.CreateType(properties);

        // Compile lambda body equivalent to:
        //   dict => new T { F1 = (T1)dict["F1"], F2 = (T2)dict["F2"], ... }
        var dictParam = Expression.Parameter(typeof(IDictionary<string, object?>), "dict");
        var indexer =
            typeof(IDictionary<string, object?>).GetProperty("Item")
            ?? throw new InvalidOperationException(
                "IDictionary<string, object?>.Item indexer not found."
            );

        var bindings = new MemberBinding[signature.Fields.Count];
        for (var i = 0; i < signature.Fields.Count; i++)
        {
            var field = signature.Fields[i];
            var indexAccess = Expression.MakeIndex(
                dictParam,
                indexer,
                new Expression[] { Expression.Constant(field.Name) }
            );

            // Value types - explicit Convert; the compiled mapper for this shape is
            // only used for rows whose value at this position is non-null (signature
            // would have been (object) instead, mapped to a different cache entry).
            // Reference types - TypeAs preserves null safely.
            Expression typedValue = field.Type.IsValueType
                ? (Expression)Expression.Convert(indexAccess, field.Type)
                : Expression.TypeAs(indexAccess, field.Type);

            bindings[i] = Expression.Bind(type.GetProperty(field.Name)!, typedValue);
        }

        var body = Expression.MemberInit(Expression.New(type), bindings);
        var lambda = Expression.Lambda<Func<IDictionary<string, object?>, object>>(
            Expression.Convert(body, typeof(object)),
            dictParam
        );

        return new ShapeEntry(type, lambda.Compile());
    }

    // Slow path: recursive reflection-based mapper for nested dictionaries and
    // collections that the compiled fast path cannot express. Recursion handles
    // arbitrary nesting depth.
    private static (Type Type, object Instance) MapWithReflection(IDictionary<string, object?> dict)
    {
        var properties = new DynamicProperty[dict.Count];
        var values = new object?[dict.Count];

        var i = 0;
        foreach (var pair in dict)
        {
            var (propertyType, value) = ResolveProperty(pair.Value);
            properties[i] = new DynamicProperty(pair.Key, propertyType);
            values[i] = value;
            i++;
        }

        var type = DynamicClassFactory.CreateType(properties);
        var instance = Activator.CreateInstance(type)!;

        i = 0;
        foreach (var pair in dict)
        {
            type.GetProperty(pair.Key)?.SetValue(instance, values[i++]);
        }

        return (type, instance);
    }

    private static (Type Type, object? Value) ResolveProperty(object? raw)
    {
        if (raw is IDictionary<string, object?> nestedDict)
        {
            var (nestedType, nestedInstance) = MapWithReflection(nestedDict);
            return (nestedType, nestedInstance);
        }

        if (TryAsCollection(raw, out var items))
        {
            var (listType, listInstance) = MapCollection(items);
            return (listType, listInstance);
        }

        return (raw?.GetType() ?? typeof(object), raw);
    }

    private static bool TryAsCollection(object? value, out IList<object> items)
    {
        // string is IEnumerable<char> but we treat it as scalar; byte[] is also kept opaque.
        if (value is null or string or byte[])
        {
            items = null!;
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            items = enumerable.Cast<object>().ToList();
            return true;
        }

        items = null!;
        return false;
    }

    // Builds a typed List<T> from a collection of items. For dict elements the
    // element type is unified across all items: a field that is null in some and
    // non-null in others is widened to Nullable<T> for value types, so the common
    // "optional value" case in heterogeneous-shape input doesn't throw. Items
    // with genuinely conflicting non-null types for the same field still throw.
    private static (Type ListType, object ListInstance) MapCollection(IList<object> items)
    {
        if (items.Count == 0)
        {
            // Empty collection: fall back to List<object>. Count() / .Any() work,
            // .Any(predicate) won't (no element type properties).
            var emptyType = typeof(List<object>);
            return (emptyType, Activator.CreateInstance(emptyType)!);
        }

        var sample = items.FirstOrDefault(i => i is not null);
        if (sample is null)
        {
            // All items null: there is no element type to infer.
            var nullListType = typeof(List<object>);
            var nullList = (IList)Activator.CreateInstance(nullListType)!;
            foreach (var _ in items)
                nullList.Add(null);
            return (nullListType, nullList);
        }

        Type elementType;
        Func<object, object?> projectItem;

        if (sample is IDictionary<string, object?>)
        {
            var (props, type) = BuildUnifiedDictShape(items);
            elementType = type;
            projectItem = item =>
            {
                if (item is not IDictionary<string, object?> d)
                    throw new InvalidOperationException(
                        "Heterogeneous collection: mix of dictionary and non-dictionary items."
                    );
                return ProjectDictToShape(d, type, props);
            };
        }
        else
        {
            elementType = sample.GetType();
            projectItem = item => item;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var listInstance = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            listInstance.Add(item is null ? null : projectItem(item));
        }

        return (listType, listInstance);
    }

    // Two-pass walk over every dict item. First pass collects each field name and
    // its most specific non-null type. Second pass marks any field that is missing
    // from at least one dict, so it gets widened to Nullable<T> for value types -
    // a single pass cannot do this because a field discovered for the first time
    // late in the sequence still needs to be marked HadNull for the earlier dicts
    // that did not contain it. Conflicting non-null types for the same field name
    // throw - that is the "real" heterogeneity case.
    private static (DynamicProperty[] Props, Type Type) BuildUnifiedDictShape(IList<object> items)
    {
        var byField = new Dictionary<string, FieldStats>();
        var fieldOrder = new List<string>();

        foreach (var item in items)
        {
            if (item is null)
                continue;
            if (item is not IDictionary<string, object?> dict)
                throw new InvalidOperationException(
                    "Heterogeneous collection: mix of dictionary and non-dictionary items."
                );
            AccumulateFromDict(dict, byField, fieldOrder);
        }

        // Pass 1's invariant holds here: every non-null item is a dict (otherwise
        // Pass 1 would have thrown). OfType is used purely to filter out nulls
        // and let Sonar see a Where-style enumeration, not to silently drop
        // unexpected types.
        foreach (var dict in items.OfType<IDictionary<string, object?>>())
        {
            foreach (var missing in fieldOrder.Where(n => !dict.ContainsKey(n)))
            {
                byField[missing].HadNull = true;
            }
        }

        var props = fieldOrder
            .Select(name => new DynamicProperty(name, ResolvePropertyType(byField[name])))
            .ToArray();
        return (props, DynamicClassFactory.CreateType(props));
    }

    private static void AccumulateFromDict(
        IDictionary<string, object?> dict,
        Dictionary<string, FieldStats> byField,
        List<string> fieldOrder
    )
    {
        foreach (var pair in dict)
        {
            if (!byField.TryGetValue(pair.Key, out var stats))
            {
                stats = new FieldStats();
                byField[pair.Key] = stats;
                fieldOrder.Add(pair.Key);
            }
            UpdateFieldStats(pair.Key, pair.Value, stats);
        }
    }

    private static void UpdateFieldStats(string fieldName, object? value, FieldStats stats)
    {
        if (value is null)
        {
            stats.HadNull = true;
            return;
        }
        var (resolvedType, _) = ResolveProperty(value);
        if (stats.NonNullType is null)
        {
            stats.NonNullType = resolvedType;
        }
        else if (stats.NonNullType != resolvedType)
        {
            throw new InvalidOperationException(
                $"Heterogeneous collection: field '{fieldName}' has conflicting non-null types '{stats.NonNullType}' and '{resolvedType}'."
            );
        }
    }

    private static Type ResolvePropertyType(FieldStats stats)
    {
        if (stats.NonNullType is null)
            return typeof(object);
        if (
            stats.HadNull
            && stats.NonNullType.IsValueType
            && Nullable.GetUnderlyingType(stats.NonNullType) is null
        )
            return typeof(Nullable<>).MakeGenericType(stats.NonNullType);
        return stats.NonNullType;
    }

    private sealed class FieldStats
    {
        public Type? NonNullType;
        public bool HadNull;
    }

    private static object ProjectDictToShape(
        IDictionary<string, object?> dict,
        Type targetType,
        DynamicProperty[] props
    )
    {
        var instance = Activator.CreateInstance(targetType)!;
        for (var i = 0; i < props.Length; i++)
        {
            var name = props[i].Name;
            if (!dict.TryGetValue(name, out var raw))
                continue;

            object? value;
            if (raw is null)
            {
                value = null;
            }
            else
            {
                var (_, projected) = ResolveProperty(raw);
                value = projected;
            }
            targetType.GetProperty(name)?.SetValue(instance, value);
        }
        return instance;
    }

    // === Shape signature for the fast-path cache ===

    private sealed class ShapeEntry
    {
        public Type Type { get; }
        public Func<IDictionary<string, object?>, object> Mapper { get; }

        public ShapeEntry(Type type, Func<IDictionary<string, object?>, object> mapper)
        {
            Type = type;
            Mapper = mapper;
        }
    }

    private readonly struct ShapeSignature : IEquatable<ShapeSignature>
    {
        private readonly FieldKey[] _fields;
        private readonly int _hash;

        public IReadOnlyList<FieldKey> Fields => _fields;

        private ShapeSignature(FieldKey[] fields, int hash)
        {
            _fields = fields;
            _hash = hash;
        }

        // Single-walk entry: builds the signature for flat shapes, or signals a
        // complex shape (nested IDictionary or collection field) by returning false.
        // Caller routes complex shapes to the reflection-based slow path.
        public static bool TryFrom(IDictionary<string, object?> dict, out ShapeSignature signature)
        {
            var fields = new FieldKey[dict.Count];
            var hash = 17;
            var i = 0;
            foreach (var pair in dict)
            {
                if (pair.Value is IDictionary<string, object> || IsCollection(pair.Value))
                {
                    signature = default;
                    return false;
                }

                var t = pair.Value?.GetType() ?? typeof(object);
                fields[i] = new FieldKey(pair.Key, t);
                hash = unchecked(hash * 31 + pair.Key.GetHashCode());
                hash = unchecked(hash * 31 + t.GetHashCode());
                i++;
            }
            signature = new ShapeSignature(fields, hash);
            return true;
        }

        // Treat string and byte[] as scalars (their content is opaque to predicates).
        private static bool IsCollection(object? value) =>
            value is not null and not string and not byte[] and IEnumerable;

        public bool Equals(ShapeSignature other)
        {
            if (_hash != other._hash)
                return false;
            if (_fields.Length != other._fields.Length)
                return false;
            for (var i = 0; i < _fields.Length; i++)
            {
                if (!_fields[i].Equals(other._fields[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is ShapeSignature other && Equals(other);

        public override int GetHashCode() => _hash;

        public readonly struct FieldKey : IEquatable<FieldKey>
        {
            public string Name { get; }
            public Type Type { get; }

            public FieldKey(string name, Type type)
            {
                Name = name;
                Type = type;
            }

            public bool Equals(FieldKey other) => Name == other.Name && Type == other.Type;

            public override bool Equals(object? obj) => obj is FieldKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = 17;
                    h = h * 31 + Name.GetHashCode();
                    h = h * 31 + Type.GetHashCode();
                    return h;
                }
            }
        }
    }
}
