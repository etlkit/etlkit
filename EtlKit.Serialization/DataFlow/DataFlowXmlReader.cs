using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using EtlKit.Primitives;

using EtlKit.DataFlow;

using JetBrains.Annotations;

#pragma warning disable CS0618 // Type or member is obsolete - DataFlowActivator backward compat

namespace EtlKit.Serialization.DataFlow;

/// <summary>
/// Read data flow configuration from XML
/// </summary>
[PublicAPI]
public sealed class DataFlowXmlReader : IDataFlowXmlContext
{
    // Type cache for all serializable Dataflow types
    private readonly Type[] _types;

    // Data flow instance to configure
    private readonly IDataFlow _dataFlow;

    // Universal error handler for all supporting sources
    private readonly IDataFlowDestination<EtlKitError>? _linkAllErrorsTo;

    // Check if the universal error destination was added to data flow
    private bool _allErrorsDestinationAdded;

    // Activator used to create data flow component instances
    private readonly IDataFlowActivator _activator;

    // Culture settings for deserialization
    public CultureInfo CurrentCulture { get; set; }

    public DataFlowXmlReader(
        IDataFlow dataFlow,
        CultureInfo? currentCulture = null,
        IDataFlowDestination<EtlKitError>? linkAllErrorsTo = null,
        IServiceProvider? serviceProvider = null
    )
    {
        _linkAllErrorsTo = linkAllErrorsTo;
        _dataFlow = dataFlow ?? throw new ArgumentNullException(nameof(dataFlow));
        _dataFlow.Destinations = new List<IDataFlowDestination<ExpandoObject>>();
        _dataFlow.ErrorDestinations = new List<IDataFlowDestination<EtlKitError>>();
        CurrentCulture = currentCulture ?? CultureInfo.CurrentCulture;
        _activator =
            serviceProvider != null
                ? new ServiceProviderActivator(serviceProvider)
                : new DefaultDataFlowActivator();

        var types = new List<Type>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        var files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll");

        foreach (var file in files)
        {
            try
            {
                var name = AssemblyName.GetAssemblyName(file);
                if (assemblies.Find(a => a.FullName == name.FullName) is not null)
                {
                    continue;
                }

                var assembly = Assembly.Load(name);
                assemblies.Add(assembly);
            }
            catch (BadImageFormatException)
            {
                // Ignore
            }
        }

        foreach (var currentTypes in assemblies.Select(GetDataFlowTypes))
        {
            types.AddRange(currentTypes);
        }

        _types = types.ToArray();
    }

    /// <summary>
    /// Creates a new instance of <see cref="DataFlowXmlReader"/> with a custom activator.
    /// </summary>
    public DataFlowXmlReader(
        IDataFlow dataFlow,
        IDataFlowActivator activator,
        CultureInfo? currentCulture = null,
        IDataFlowDestination<EtlKitError>? linkAllErrorsTo = null
    )
        : this(dataFlow, currentCulture, linkAllErrorsTo)
    {
        _activator = activator ?? throw new ArgumentNullException(nameof(activator));
    }

    /// <inheritdoc />
    Type? IDataFlowXmlContext.ResolveType(string typeName)
    {
        try
        {
            return GetTypeByName(_types, typeName);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    object? IDataFlowXmlContext.CreateObject(string typeName, XElement element)
    {
        Type type;
        try
        {
            type = GetTypeByName(_types, typeName);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        var result = CreateObject(type, element);
        if (
            result is IDataFlowDestination<ExpandoObject> dest
            && !_dataFlow.Destinations.Contains(dest)
        )
            _dataFlow.Destinations.Add(dest);
        if (
            result is IDataFlowDestination<EtlKitError> errDest
            && !_dataFlow.ErrorDestinations.Contains(errDest)
        )
            _dataFlow.ErrorDestinations.Add(errDest);
        return result;
    }

    public void Read(XmlReader reader)
    {
        reader.MoveToContent();
        var root = reader.Name;
        var namespaceUri = reader.NamespaceURI;
        reader.Read();
        reader.MoveToContent();
        while (!reader.EOF)
        {
            while (reader.NodeType == XmlNodeType.Whitespace)
                reader.Skip();
            if (reader.NodeType == XmlNodeType.None)
                reader.Skip();
            if (
                reader.NodeType == XmlNodeType.EndElement
                && reader.LocalName == root
                && reader.NamespaceURI == namespaceUri
            )
            {
                break;
            }

            InitializeRootPropertiesFromXml(reader);
        }
    }

    public static T Deserialize<T>(
        string xml,
        ErrorLogDestination errorLogDestination,
        IServiceProvider? serviceProvider = null
    )
        where T : IDataFlow, new()
    {
        using var stream = new MemoryStream(Encoding.Default.GetBytes(xml));
        using var xmlReader = XmlReader.Create(stream);
        var step = Activator.CreateInstance<T>();
        var reader = new DataFlowXmlReader(
            step,
            CultureInfo.InvariantCulture,
            errorLogDestination,
            serviceProvider
        );
        reader.Read(xmlReader);
        return step;
    }

    private void InitializeRootPropertiesFromXml(XmlReader reader)
    {
        var prop = _dataFlow.GetType().GetProperties().LastOrDefault(p => p.Name == reader.Name);

        var type = GetPropertyType(prop, reader.Name);

        if (IsSourceType(type))
        {
            _dataFlow.Source =
                CreateSource(type, reader)
                ?? throw new InvalidOperationException(
                    $"Invalid configuration. Root source '{reader.Name}' must implement {nameof(IDataFlowSource<ExpandoObject>)}"
                );
            // When Pipeline is the root source with no external LinkTo, Destinations would be empty
            // and EtlDataFlowStep.Invoke() would return without awaiting its completion.
            // If an external destination exists it already waits via AddPredecessorCompletion,
            // so this is redundant but harmless (children are not yet parsed here to check).
            if (
                _dataFlow.Source is IDataFlowDestination<ExpandoObject> sourceDest
                && !_dataFlow.Destinations.Contains(sourceDest)
            )
                _dataFlow.Destinations.Add(sourceDest);
            return;
        }

        if (prop is null)
        {
            throw new InvalidDataException(
                $"Invalid configuration. Property '{reader.Name}' not found"
            );
        }

        var element = (XElement)XNode.ReadFrom(reader);

        if (IsValueTypeProperty(prop))
        {
            SetValueTypeProperty(_dataFlow, prop, element, CurrentCulture);
            return;
        }

        if (!prop.PropertyType.IsClass)
        {
            throw new InvalidDataException(
                $"Invalid configuration. Property '{reader.Name}' deserialization is not implemented"
            );
        }

        SetClassProperty(_dataFlow, prop, element);
    }

    private void SetClassProperty(object instance, PropertyInfo prop, XElement? element)
    {
        if (element is null)
        {
            throw new InvalidDataException(
                $"Invalid configuration. Property '{prop.Name}' xml element is null"
            );
        }

        if (
            typeof(IDisposable).IsAssignableFrom(prop.PropertyType)
            && _dataFlow is IDataFlowResourceOwner owner
        )
        {
            var innerXml = string.Concat(
                element.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting))
            );
            var key = prop.PropertyType.FullName + ":" + innerXml;
            AssignDisposableResource(instance, prop, prop.PropertyType, element, key, owner);
        }
        else
        {
            var value = CreateObject(prop.PropertyType, element);
            if (value is null)
                return;
            prop.SetValue(instance, value);
        }
    }

    /// <summary>
    /// Assigns an <see cref="IDisposable"/> property. When the activator reports the type as
    /// externally owned (e.g. a service resolved from a DI container), the instance is created and
    /// assigned without taking flow ownership or deduplicating — its lifetime is managed by that
    /// external scope. Otherwise, the instance is added to the flow's resource pool (deduplicated by
    /// <paramref name="key"/> and disposed with the flow).
    /// </summary>
    private void AssignDisposableResource(
        object instance,
        PropertyInfo prop,
        Type propType,
        XContainer element,
        string key,
        IDataFlowResourceOwner owner
    )
    {
        if (IsExternallyOwned(propType))
        {
            var external = CreateObject(propType, element);
            if (external is not null)
                prop.SetValue(instance, external);
            return;
        }

        var value = owner.GetOrAddResource(
            key,
            () =>
                (IDisposable)(
                    CreateObject(propType, element)
                    ?? throw new InvalidDataException(
                        $"Failed to create disposable resource of type {propType}"
                    )
                )
        );
        prop.SetValue(instance, value);
    }

    /// <summary>
    /// Whether instances of <paramref name="type"/> are owned by an external lifetime scope
    /// (e.g. a DI container) per the activator, and therefore must not be flow-owned/disposed.
    /// Activators that do not implement <see cref="ILifetimeAwareActivator"/> are treated as always
    /// creating flow-owned instances.
    /// </summary>
    private bool IsExternallyOwned(Type type) =>
        _activator is ILifetimeAwareActivator lifetimeAware
        && lifetimeAware.IsExternallyOwned(type);

    private static void SetValueTypeProperty(
        object instance,
        PropertyInfo prop,
        XElement? element,
        CultureInfo cultureInfo
    )
    {
        if (element is null)
        {
            throw new InvalidDataException(
                $"Invalid configuration. Property '{prop.Name}' xml element is null"
            );
        }

        var value = GetValue(prop.PropertyType, cultureInfo, element.Value);

        if (value is null)
        {
            return;
        }

        prop.SetValue(instance, value);
    }

    private static bool IsValueTypeProperty(PropertyInfo prop)
    {
        return IsValueType(prop.PropertyType);
    }

    private static bool IsSourceType(Type type)
    {
        var interfaces = type.GetInterfaces();

        return Array.Exists(
            interfaces,
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDataFlowSource<>)
        );
    }

    private Type GetPropertyType(PropertyInfo? prop, string typeName)
    {
        return prop?.PropertyType ?? GetTypeByName(_types, typeName);
    }

    private IDataFlowSource<ExpandoObject>? CreateSource(Type type, XmlReader reader)
    {
        var element = (XElement)XNode.ReadFrom(reader);

        return CreateObject(type, element) as IDataFlowSource<ExpandoObject>;
    }

    private object? CreateObject(Type type, XContainer node)
    {
        var result = CreateObjectCore(type, node);
        ApplyLinkAllErrorsTo(result);
        return result;
    }

    // Creates an object using full type routing without applying global post-creation actions.
    private object? CreateObjectCore(Type type, XContainer node)
    {
        if (IsValueType(type))
            return CreateValueType(type, node, CurrentCulture);
        if (type.IsArray)
            return CreateArray(type, node);
        if (
            Array.Exists(
                type.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)
            )
        )
            return CreateList(type, node);
        return IsDictionary(type) ? CreateDictionary(type, node) : CreateInstance(type, node);
    }

    private object? CreateList(Type type, XContainer node)
    {
        var elementType =
            type.GenericTypeArguments[0]
            ?? throw new InvalidDataException(
                $"Invalid configuration. Implementation for element type of array '{type}' not found"
            );

        var list = _activator.CreateInstance(type);
        var set = type.GetMethod("Add");
        if (set == null)
            return list;
        foreach (var t in node.Elements())
        {
            var item = CreateObject(elementType, t);
            if (item != null)
            {
                set.Invoke(list, [item]);
            }
        }

        return list;
    }

    private object? CreateInstance(Type type, XContainer node)
    {
        var instance = _activator.CreateInstance(type);

        if (instance is null)
        {
            return null;
        }

        if (instance is IDataFlowXmlSerializable xmlSerializable)
        {
            xmlSerializable.ReadXml((XElement)node, this);
            return instance;
        }

        // Standard property-by-property deserialization
        foreach (var propXml in node.Elements())
        {
            InitializeInstanceProperty(instance, type, propXml);
        }

        return instance;
    }

    private void ApplyLinkAllErrorsTo(object? instance)
    {
        if (_linkAllErrorsTo == null || instance is not ILinkErrorSource errorSource)
            return;

        errorSource.LinkErrorTo(_linkAllErrorsTo);
        if (_allErrorsDestinationAdded)
            return;

        _dataFlow.ErrorDestinations.Add(_linkAllErrorsTo);
        _allErrorsDestinationAdded = true;
    }

    private void InitializeInstanceProperty(object instance, MemberInfo type, XElement? propXml)
    {
        if (propXml is null)
        {
            throw new InvalidDataException($"Cant get a property for type '{type}'");
        }

        var prop = instance.GetType().GetProperty(propXml.Name.LocalName);

        if (prop is null || !prop.CanWrite)
        {
            TryInvokeSourceMethod(instance, propXml);
            return;
        }

        if (prop.PropertyType.IsEnum)
        {
            SetEnumProperty(instance, prop, propXml);
            return;
        }

        if (IsNullableEnum(prop.PropertyType))
        {
            SetNullableEnumProperty(instance, prop, propXml);
            return;
        }

        if (IsValueType(prop.PropertyType))
        {
            SetValueTypeProperty(instance, prop, propXml, CurrentCulture);
            return;
        }

        // Special handling for IDictionary<string, object?> - parse as nested XML
        if (IsDictionaryWithObjectValue(prop.PropertyType))
        {
            var dict = CreateDictionary(prop.PropertyType, propXml);
            prop.SetValue(instance, dict);
            return;
        }

        if (prop.PropertyType.IsInterface || prop.PropertyType.IsAbstract)
        {
            SetInterfaceProperty(instance, type.Name, propXml, prop);
            return;
        }

        if (prop.PropertyType.IsClass)
        {
            SetClassProperty(instance, prop, propXml);
        }
    }

    private static bool IsNullableEnum(Type t)
    {
        var underlyingType = Nullable.GetUnderlyingType(t);
        return underlyingType is { IsEnum: true };
    }

    private static void SetEnumProperty(object instance, PropertyInfo prop, XElement? element)
    {
        if (element is null)
        {
            throw new InvalidDataException(
                $"Invalid configuration. Property '{prop.Name}' xml element is null"
            );
        }

        var eval = Enum.Parse(prop.PropertyType, element.Value);
        prop.SetValue(instance, eval);
    }

    private static void SetNullableEnumProperty(
        object instance,
        PropertyInfo prop,
        XElement? element
    )
    {
        if (string.IsNullOrEmpty(element?.Value))
        {
            if (prop.PropertyType.IsNullable())
            {
                prop.SetValue(instance, null);
            }

            return;
        }

        var underlyingType =
            Nullable.GetUnderlyingType(prop.PropertyType) ?? throw new InvalidOperationException();
        var eval = Enum.Parse(underlyingType, element!.Value);
        prop.SetValue(instance, eval);
    }

    private void SetInterfaceProperty(
        object instance,
        string typeName,
        XElement propXml,
        PropertyInfo prop
    )
    {
        var propType = GetPropertyType(typeName, propXml, prop);
        if (typeof(IConnectionManager).IsAssignableFrom(propType))
        {
            var connectionString = propXml
                .Element(nameof(IConnectionManager.ConnectionString))
                ?.Value;
            var value = _dataFlow.GetOrAddConnectionManager(
                propType,
                connectionString,
                (type, _) =>
                    CreateInstance(type, propXml) as IConnectionManager
                    ?? throw new InvalidDataException(
                        $"Failed to create connection manager instance of type {type}"
                    )
            );
            prop.SetValue(instance, value);
        }
        else if (
            typeof(IDisposable).IsAssignableFrom(propType)
            && _dataFlow is IDataFlowResourceOwner owner
        )
        {
            var key = propType.FullName + ":" + propXml.ToString(SaveOptions.DisableFormatting);
            AssignDisposableResource(instance, prop, propType, propXml, key, owner);
        }
        else
        {
            var value = CreateObject(propType, propXml);
            if (value is not null)
            {
                prop.SetValue(instance, value);
            }
        }
    }

    private static Type GetPropertyType(string typeName, XElement propXml, PropertyInfo prop)
    {
        if (
            prop.PropertyType.IsGenericType
            && prop.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        )
        {
            // If Type is IEnumerable, assign array
            return prop.PropertyType.GenericTypeArguments[0].MakeArrayType();
        }

        var propTypeName = propXml.Attribute("type")?.Value;
        if (string.IsNullOrEmpty(propTypeName))
        {
            throw new InvalidDataException(
                $"Type attribute is required to assign '{typeName}.{prop.Name}' value"
            );
        }
        return GetType(propTypeName!, prop.PropertyType)
            ?? throw new InvalidDataException($"Type  is not found for '{typeName}.{prop.Name}'");
    }

    private Array CreateArray(Type type, XContainer node)
    {
        var elementType =
            type.GetElementType()
            ?? throw new InvalidDataException(
                $"Invalid configuration. Implementation for element type of array '{type}' not found"
            );

        var elements = node.Elements().ToArray();
        var array = Array.CreateInstance(elementType, elements.Length);
        var set = type.GetMethod("SetValue", [typeof(object), typeof(int)]);
        for (var i = 0; i < elements.Length; i++)
        {
            var item = CreateObject(elementType, elements[i]);
            if (item != null)
            {
                set?.Invoke(array, [item, i]);
            }
        }

        return array;
    }

    private static object? CreateValueType(Type type, XContainer node, CultureInfo cultureInfo)
    {
        var builder = new StringBuilder();
        foreach (var item in node.DescendantNodes().Where(n => n != null))
        {
            builder.Append((item as XText)?.Value.Trim() ?? "");
        }

        return GetValue(type, cultureInfo, builder.ToString());
    }

    private object CreateDictionary(Type type, XContainer node)
    {
        var arguments = type.GetGenericArguments();

        if (arguments is null || arguments.Length < 2)
        {
            throw new InvalidDataException(
                $"Invalid configuration. Correct implementation of dictionary type '{type}' not found"
            );
        }

        var keyType = arguments[0];
        var valueType = arguments[1];

        if (keyType is null || keyType != typeof(string))
        {
            throw new InvalidDataException(
                $"Invalid configuration. Key type of dictionary '{type}' should be string"
            );
        }

        if (valueType is null)
        {
            throw new InvalidDataException(
                $"Invalid configuration. Implementation for key value of dictionary '{type}' not found"
            );
        }

        var dictToCreate = typeof(Dictionary<,>).MakeGenericType(arguments);
        var dict =
            Activator.CreateInstance(dictToCreate)
            ?? throw new InvalidOperationException(
                $"Invalid configuration. Can't create dictionary of type '{type}'"
            );

        var elements = node.Elements().ToArray();
        var add = type.GetMethod("Add", [keyType, valueType]);

        // Special handling for IDictionary<string, object?> - parse nested structures
        // Note: For object? values, all leaf values are deserialized as strings (XML has no type info)
        // Only nested elements with children become Dictionary<string, object?>
        var isObjectValueType = valueType == typeof(object);

        foreach (var element in elements)
        {
            var item =
                // For object? values, parse recursively to handle nested structures
                isObjectValueType
                    ? ParseXmlElementToDictionary(element)
                    : CreateObject(valueType, element);

            if (item != null)
            {
                add?.Invoke(dict, [element.Name.LocalName, item]);
            }
        }

        return dict;
    }

    /// <summary>
    /// Recursively parses XML element to object, creating dictionaries for nested elements.
    /// Leaf values (elements without children) are returned as strings since XML has no type information.
    /// Empty or whitespace-only leaf elements are returned as null and will be excluded from the dictionary.
    /// </summary>
    private static object? ParseXmlElementToDictionary(XElement element)
    {
        // If element has no child elements, return text value as string
        // Note: All leaf values are strings - caller must convert if needed
        // Empty values return null, which causes them to be skipped in CreateDictionary
        if (!element.HasElements)
        {
            return string.IsNullOrEmpty(element.Value) ? null : element.Value;
        }

        // If element has child elements, create a dictionary
        var nestedDict = new Dictionary<string, object?>();

        foreach (var childElement in element.Elements())
        {
            var childValue = ParseXmlElementToDictionary(childElement);
            nestedDict[childElement.Name.LocalName] = childValue;
        }

        return nestedDict;
    }

    private void TryInvokeSourceMethod(object instance, XElement propXml)
    {
        var method = GetMethod(instance, propXml);

        if (method is null)
        {
            return;
        }

        using var reader = propXml.CreateReader();
        reader.MoveToContent();
        var root = (XElement)XNode.ReadFrom(reader);
        foreach (var element in root.Elements())
        {
            AddDestinationAndInvokeMethod(element, method, instance);
        }
    }

    private void AddDestinationAndInvokeMethod(XElement element, MethodBase method, object source)
    {
        // This should be LinkTo, LinkErrorTo, or similar linking method with single IDataFlowLinkTarget<> parameter
        if (
            method.GetParameters().Length != 1
            || !method.GetParameters()[0].ParameterType.IsInterface
            || !method.GetParameters()[0].ParameterType.IsGenericType
            || !typeof(IDataFlowLinkTarget<>).IsAssignableFrom(
                method.GetParameters()[0].ParameterType.GetGenericTypeDefinition()
            )
        )
            return;

        var parameterType = method.GetParameters()[0].ParameterType;
        var type = GetTypeByName(_types, element.Name.LocalName);

        if (!parameterType.IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"Type '{type.Name}' is not assignable to parameter type '{parameterType.Name}' in method '{method.Name}'"
            );
        }

        var obj = CreateObject(type, element);

        if (obj is IDataFlowDestination<ExpandoObject> dest)
        {
            _dataFlow.Destinations.Add(dest);
        }

        if (obj is IDataFlowDestination<EtlKitError> err)
        {
            _dataFlow.ErrorDestinations.Add(err);
        }

        // Call LinkTo or LinkErrorTo method
        method.Invoke(source, [obj]);
    }

    private static MethodInfo? GetMethod(object instance, XElement propXml)
    {
        return instance
            .GetType()
            .GetMethods()
            .Where(m => m.Name == propXml.Name.LocalName)
            .OrderBy(m => m.GetParameters().Length)
            .FirstOrDefault();
    }

    private static Type? GetType(string typeName, Type? baseType = null)
    {
        var isArray = false;
        if (typeName.EndsWith("[]"))
        {
            isArray = true;
            typeName = typeName.Remove(typeName.Length - 2, 2);
        }

        Type? type = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var types = SafeGetTypes(assembly).Where(t => t.Name == typeName);
            if (baseType is { IsInterface: true })
            {
                types = types.Where(t => Array.Exists(t.GetInterfaces(), i => i == baseType));
            }

            type = types.LastOrDefault(t => t.Name == typeName);
            if (type != null)
            {
                break;
            }
        }

        return isArray ? type?.MakeArrayType() : type;
    }

    private static object? GetValue(Type type, CultureInfo cultureInfo, string value)
    {
        value = value.Trim();

        if (type.IsNullable() && string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            if (value.TryParse(type, cultureInfo, out var objValue))
            {
                return objValue;
            }
        }
        catch
        {
            throw new InvalidOperationException(
                $"Invalid configuration. Value '{value}' for type '{type}' is not valid"
            );
        }

        return value;
    }

    private static bool IsDictionary(Type type)
    {
        return type.IsGenericType
            && Array.Exists(
                type.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
            );
    }

    private static bool IsDictionaryWithObjectValue(Type type)
    {
        // Handle both IDictionary<string, object> and IDictionary<string, object?>
        var dictInterface = type is { IsInterface: true, IsGenericType: true }
            ? type
            : Array.Find(
                type.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
            );

        if (
            dictInterface == null
            || dictInterface.GetGenericTypeDefinition() != typeof(IDictionary<,>)
        )
        {
            return false;
        }

        var genericArgs = dictInterface.GetGenericArguments();
        if (genericArgs.Length != 2 || genericArgs[0] != typeof(string))
        {
            return false;
        }

        // Check for both object and object? (Nullable<object> doesn't exist, but the generic arg could still be an object)
        var valueType = genericArgs[1];
        return valueType == typeof(object);
    }

    private static bool IsValueType(Type type)
    {
        return type.IsValueType || type == typeof(string);
    }

    private static Type GetTypeByName(Type[] types, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidDataException("Invalid configuration. Type name is empty");
        }

        // First look for non-generic types, exactly matching the name
        var type = Array.Find(types, t => t.Name == name && !t.IsGenericTypeDefinition);
        if (type != null)
        {
            return type;
        }

        // Now look for generic types, matching the name, accepting single type parameter
        type = Array.Find(
            types,
            t =>
                t.Name.StartsWith($"{name}`")
                && t.IsGenericTypeDefinition
                && t.GetGenericArguments().Length == 1
        );
        if (type is null)
        {
            throw new InvalidOperationException($"Could not find type by name '{name}'");
        }

        return type.MakeGenericType(typeof(ExpandoObject));
    }

    private static IEnumerable<Type> GetDataFlowTypes(Assembly assembly)
    {
        return SafeGetTypes(assembly)
            .Where(t =>
                t.IsClass
                && Array.Exists(
                    t.GetInterfaces(),
                    i =>
                        i.IsGenericType
                        && (
                            i.GetGenericTypeDefinition().FullName
                                == typeof(IDataFlowLinkSource<>).FullName
                            || i.GetGenericTypeDefinition().FullName
                                == typeof(IDataFlowLinkTarget<>).FullName
                            || i.GetGenericTypeDefinition().FullName
                                == typeof(IDataFlowTransformation<,>).FullName
                        )
                )
            );
    }

    /// <summary>
    /// Safely retrieves types from an assembly, handling <see cref="ReflectionTypeLoadException"/>
    /// that occurs when the assembly references types from unavailable dependencies.
    /// </summary>
    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).ToArray();
        }
    }

    /// <inheritdoc />
    IDataFlowXmlContext IDataFlowXmlContext.WithoutGlobalActions() =>
        new SuppressedActionsContext(this);

    /// <summary>
    /// A delegating context that forwards all calls to the parent reader except
    /// <see cref="CreateObject"/>, where global post-creation actions are suppressed.
    /// Returned by <see cref="IDataFlowXmlContext.WithoutGlobalActions"/>.
    /// </summary>
    private sealed class SuppressedActionsContext : IDataFlowXmlContext
    {
        private readonly DataFlowXmlReader _reader;

        internal SuppressedActionsContext(DataFlowXmlReader reader) => _reader = reader;

        public Type? ResolveType(string typeName) =>
            ((IDataFlowXmlContext)_reader).ResolveType(typeName);

        public object? CreateObject(string typeName, XElement element)
        {
            Type type;
            try
            {
                type = GetTypeByName(_reader._types, typeName);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }

            // Use CreateObjectCore to skip global post-creation actions (ApplyLinkAllErrorsTo).
            var result = _reader.CreateObjectCore(type, element);
            if (
                result is IDataFlowDestination<ExpandoObject> dest
                && !_reader._dataFlow.Destinations.Contains(dest)
            )
                _reader._dataFlow.Destinations.Add(dest);
            if (
                result is IDataFlowDestination<EtlKitError> errDest
                && !_reader._dataFlow.ErrorDestinations.Contains(errDest)
            )
                _reader._dataFlow.ErrorDestinations.Add(errDest);
            return result;
        }

        public IDataFlowXmlContext WithoutGlobalActions() => this;
    }
}
