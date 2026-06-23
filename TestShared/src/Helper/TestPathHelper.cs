using System.IO;
using System.Reflection;

namespace EtlKit.TestShared.Helper;

public static class TestPathHelper
{
    public static string GetFullPathToFile(string pathRelativeUnitTestingFile)
    {
        var folderProjectLevel = GetPathToCurrentUnitTestProject();
        return Path.Combine(folderProjectLevel, pathRelativeUnitTestingFile);
    }

    /// <summary>
    /// Get the path to the current unit testing project.
    /// </summary>
    /// <returns></returns>
    private static string GetPathToCurrentUnitTestProject()
    {
        var pathAssembly = Assembly.GetExecutingAssembly().Location;
        var folderAssembly = Path.GetDirectoryName(pathAssembly) ?? Directory.GetCurrentDirectory();
        var folderProjectLevel = Directory.GetParent(folderAssembly)?.Parent?.Parent?.FullName;
        return folderProjectLevel;
    }
}
