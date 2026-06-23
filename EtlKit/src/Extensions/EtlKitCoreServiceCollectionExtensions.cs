using CsvHelper.Configuration;

using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;

using Microsoft.Extensions.DependencyInjection;

namespace EtlKit.Extensions;

/// <summary>
/// Extension methods for registering core EtlKit data flow components with <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class EtlKitCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core EtlKit data flow components as transient services using open generic registrations.
    /// Components can be resolved for any type argument without explicit per-type registration.
    /// Also registers non-generic shorthand types (e.g. <see cref="DbSource"/>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="csvCultureInfo">
    /// The culture info used for <see cref="CsvConfiguration"/> instances resolved from the container.
    /// Defaults to <see cref="CultureInfo.InvariantCulture"/> when not specified.
    /// </param>
    public static IServiceCollection AddEtlKitCore(
        this IServiceCollection services,
        [CanBeNull] CultureInfo csvCultureInfo = null
    )
    {
        var culture = csvCultureInfo ?? CultureInfo.InvariantCulture;
        services.AddTransient(_ => new CsvConfiguration(culture));

        // Sources (open generics — resolve for any T)
        services.AddTransient(typeof(DbSource<>));
        services.AddTransient(typeof(CsvSource<>));
        services.AddTransient(typeof(JsonSource<>));
        services.AddTransient(typeof(XmlSource<>));
        services.AddTransient(typeof(ExcelSource<>));
        services.AddTransient(typeof(MemorySource<>));
        services.AddTransient(typeof(CustomSource<>));
        services.AddTransient(typeof(CrossJoin<,,>));

        // Transformations (open generics)
        services.AddTransient(typeof(RowTransformation<,>));
        services.AddTransient(typeof(RowTransformation<>));
        services.AddTransient(typeof(BlockTransformation<,>));
        services.AddTransient(typeof(Multicast<>));
        services.AddTransient(typeof(Sort<>));
        services.AddTransient(typeof(RowDuplication<>));
        services.AddTransient(typeof(RowMultiplication<,>));
        services.AddTransient(typeof(Aggregation<,>));
        services.AddTransient(typeof(LookupTransformation<,>));
        services.AddTransient(typeof(MergeJoin<,,>));
        services.AddTransient(typeof(DbMerge<>));

        // Destinations (open generics)
        services.AddTransient(typeof(DbDestination<>));
        services.AddTransient(typeof(CsvDestination<>));
        services.AddTransient(typeof(JsonDestination<>));
        services.AddTransient(typeof(XmlDestination<>));
        services.AddTransient(typeof(MemoryDestination<>));
        services.AddTransient(typeof(CustomDestination<>));
        services.AddTransient(typeof(VoidDestination<>));

        // Non-generic shorthand types (always ExpandoObject-based)
        services.AddTransient<DbSource>();
        services.AddTransient<CsvSource>();
        services.AddTransient<JsonSource>();
        services.AddTransient<XmlSource>();
        services.AddTransient<ExcelSource>();
        services.AddTransient<MemorySource>();
        services.AddTransient<CustomSource>();
        services.AddTransient<CrossJoin>();
        services.AddTransient<RowTransformation>();
        services.AddTransient<BlockTransformation>();
        services.AddTransient<Multicast>();
        services.AddTransient<Sort>();
        services.AddTransient<RowDuplication>();
        services.AddTransient<RowMultiplication>();
        services.AddTransient<Aggregation>();
        services.AddTransient<LookupTransformation>();
        services.AddTransient<MergeJoin>();
        services.AddTransient<DbMerge>();
        services.AddTransient<DbDestination>();
        services.AddTransient<CsvDestination>();
        services.AddTransient<JsonDestination>();
        services.AddTransient<XmlDestination>();
        services.AddTransient<MemoryDestination>();

        // Non-generic components
        services.AddTransient<ErrorLogDestination>();

        return services;
    }
}
