using System.Dynamic;

namespace EtlKit.DynamicLinq.Benchmarks.TestData;

/// <summary>
/// Builds <see cref="ExpandoObject"/> instances with controlled "shape".
/// Each unique <c>shapeId</c> produces an ExpandoObject with a distinct set of
/// property names, which is what triggers a fresh compile in both engines:
/// for Roslyn it generates a new <c>DynamicTypeN</c> + Assembly.Load;
/// for Dynamic LINQ it generates a new DynamicClass via Reflection.Emit.
/// </summary>
public static class ExpandoFactory
{
    /// <summary>
    /// Builds a row whose shape is unique to <paramref name="shapeId"/>.
    /// Property names include the shapeId so two different ids never collide
    /// on the type cache. Property values are the "canonical" filter inputs
    /// (Reserve > 0, Type == "Day", AdminReserveRatio != AdminReserveRatioPrevious).
    /// </summary>
    public static ExpandoObject NewShape(int shapeId)
    {
        var row = new ExpandoObject();
        var dict = (IDictionary<string, object?>)row;
        var suffix = shapeId == 0 ? string.Empty : $"_S{shapeId}";

        dict[$"AdminReserveRatio{suffix}"] = 25;
        dict[$"AdminReserveRatioPrevious{suffix}"] = 20;
        dict[$"AuthLimit{suffix}"] = 500_000m;
        dict[$"Reserve{suffix}"] = 100m;
        dict[$"Type{suffix}"] = "Day";

        return row;
    }

    /// <summary>
    /// A "canonical" row: same shape across calls, used when only one shape is needed.
    /// </summary>
    public static ExpandoObject Canonical() => NewShape(0);

    /// <summary>
    /// Builds a typed POCO equivalent of <see cref="Canonical"/>.
    /// </summary>
    public static ChangeRatioRow CanonicalTyped() =>
        new()
        {
            AdminReserveRatio = 25,
            AdminReserveRatioPrevious = 20,
            AuthLimit = 500_000m,
            Reserve = 100m,
            Type = "Day",
        };
}
