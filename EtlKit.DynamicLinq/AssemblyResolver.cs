using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EtlKit.DynamicLinq;

/// <summary>
/// Helpers for resolving an assembly from a name or file path and enumerating
/// its public types in a way that survives partial-load failures.
/// </summary>
/// <remarks>
/// Used by <see cref="ExpressionRowFiltration{TInput}.AdditionalAssemblyNames"/> to
/// turn a list of strings (assembly names or paths) into loaded <see cref="Assembly"/>
/// instances and harvest their public exported types for the dynamic LINQ type provider.
/// Internal API - not part of the public surface.
/// </remarks>
internal static class AssemblyResolver
{
    /// <summary>
    /// Resolves an assembly given a name or a file path, in three steps:
    /// (1) already loaded in <see cref="AppDomain.CurrentDomain"/> by short or full name,
    /// (2) <see cref="Assembly.Load(AssemblyName)"/> via probing path / GAC,
    /// (3) <see cref="Assembly.LoadFrom(string)"/> as a fallback for explicit paths.
    /// Throws <see cref="InvalidOperationException"/> if all three fail.
    /// </summary>
    public static Assembly Load(string nameOrPath)
    {
        // 1. Already loaded in AppDomain (most common case for published packages).
        var existing = AppDomain
            .CurrentDomain.GetAssemblies()
            .FirstOrDefault(a =>
                a.GetName().Name == nameOrPath || a.GetName().FullName == nameOrPath
            );
        if (existing is not null)
            return existing;

        // 2. AssemblyName-style load (resolves via probing path / GAC).
        try
        {
            return Assembly.Load(new AssemblyName(nameOrPath));
        }
        catch (Exception nameEx)
            when (nameEx is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            // 3. Path-based load (fallback for explicit file paths). Assembly.LoadFrom
            // is the intentional choice here - the input could be a path provided in
            // an XML config, and Assembly.Load only accepts AssemblyName / display name.
#pragma warning disable S3885 // "Assembly.Load" should be used - LoadFrom is intentional path fallback
            try
            {
                return Assembly.LoadFrom(nameOrPath);
            }
#pragma warning restore S3885
            catch (Exception pathEx)
            {
                throw new InvalidOperationException(
                    $"Could not load assembly '{nameOrPath}'. Tried Assembly.Load by name and Assembly.LoadFrom by path. Original load error: {nameEx.Message}",
                    pathEx
                );
            }
        }
    }

    /// <summary>
    /// Returns the public exported types of an assembly, falling back to whatever
    /// types could be loaded if a <see cref="ReflectionTypeLoadException"/> occurs.
    /// Avoids surfacing partial-load failures from referenced assemblies that could
    /// not be resolved.
    /// </summary>
    public static IEnumerable<Type> GetExportedTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
