namespace EtlKit.DynamicLinq.Benchmarks.TestData;

/// <summary>
/// Centralised filter expressions used across benchmarks. Two complexity levels:
/// "Simple" - one comparison; "Composite" - boolean combination over multiple fields.
/// Suffix is appended for shape-aware tests where field names carry a per-shape suffix.
/// </summary>
public static class Expressions
{
    public static string Simple(string suffix = "") => $"Reserve{suffix} > 0";

    public static string Composite(string suffix = "") =>
        $"(AdminReserveRatio{suffix} != AdminReserveRatioPrevious{suffix}) && Reserve{suffix} > 0 && Type{suffix} == \"Day\"";
}
