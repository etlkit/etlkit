namespace EtlKit.Scripting.Tests;

/// <summary>
/// Helper type whose member is obsolete, so that a script referencing it makes the Roslyn
/// script compilation report a CS0618 warning.
/// </summary>
public static class ObsoleteValueSource
{
    /// <summary>
    /// Obsolete member referenced from script expressions in tests.
    /// </summary>
    [Obsolete("Referenced from a script on purpose to provoke a compiler warning.")]
    public static string Value => "obsolete";
}
